using Microsoft.Data.Sqlite;
using RomMBat.Core.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;

namespace RomMBat.Core.Sync;

/// <summary>Whether a save may be written into the tree now, and why not when it may not.</summary>
/// <param name="CanWrite">False means defer the write to a later pass.</param>
/// <param name="Reason">What to tell the user. Null only when writing is allowed.</param>
public sealed record InFlightVerdict(bool CanWrite, string? Reason)
{
    public static InFlightVerdict Allowed { get; } = new(true, null);

    public static InFlightVerdict Defer(string reason) => new(false, reason);
}

/// <summary>
/// Refuses to write a save under an emulator that has the file open.
/// </summary>
/// <remarks>
/// <b>The gap this closes is data loss rather than staleness.</b> A download landing while a game
/// runs is overwritten by the emulator's own copy on exit, so the other device's save is gone and
/// the write happened underneath a process holding the file. Issue #155 has the ordering.
/// <para>
/// <b>This guards the write, not the launch.</b> <c>game-start</c> and <c>game-end</c> run inside
/// the game-launch path and do no network work, which is CLAUDE.md rule 4 and is not up for
/// negotiation; a launch that waits on a round trip is a worse product than one that occasionally
/// plays a stale save. So the freshness check stays where it is, asynchronous and once per ES
/// session, and what changes is that a download arriving too late is deferred instead of applied.
/// A deferral is not a failure: the next negotiate offers the same save again, and the
/// <c>background quit</c> pass is the one that lands it.
/// </para>
/// <para>
/// <b>Two places are read, because either alone has a hole.</b> The journal covers a game launched
/// before this pass began, since a flush drains the spool and correlates before it downloads and
/// <see cref="PlaytimeCorrelator"/> leaves an unmatched <c>game-start</c> open on purpose. The
/// spool covers a game launched during the pass, which is the measured 7 to 16 second window
/// between ES becoming interactive and the download landing: that hook wrote a <c>.hook</c> file
/// the drain has already gone past, so a guard reading only the journal is blind to exactly the
/// case the issue was opened about.
/// </para>
/// <para>
/// <b>What bounds a stale <c>game-start</c> is the sequence of the last <c>start</c> or
/// <c>quit</c>, not a clock.</b> A row left open by a machine that lost power would otherwise
/// block that game's saves forever. Sequence rather than timestamp because the journal's order
/// survives a flat RTC where the wall clock does not, which is the same reason
/// <see cref="PlaytimeCorrelator"/> pairs on it: ES starting or exiting ends every game that was
/// running before it, so the next ES start clears the block on its own.
/// </para>
/// <para>
/// <b>Every branch fails closed</b>, the rule <see cref="SaveGuard"/> set and for its reason. An
/// unreadable store or spool is not evidence that no game is running, a <c>game-start</c> whose
/// rom path is null means a game is running that cannot be named, and the cost of being wrong in
/// the other direction is someone's save.
/// </para>
/// </remarks>
public sealed class InFlightGuard
{
    private readonly RetroBatInstall _install;
    private readonly LocalStore _store;
    private readonly SaveShapes _shapes;

    public InFlightGuard(RetroBatInstall install, LocalStore store, SaveShapes? shapes = null)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(store);

        _install = install;
        _store = store;
        _shapes = shapes ?? SaveShapes.Bundled;
    }

    /// <summary>
    /// Asks whether a save for a ROM may be written to a path now.
    /// </summary>
    /// <param name="romId">The ROM the save belongs to.</param>
    /// <param name="target">Where it would land, under <c>saves/</c>.</param>
    public InFlightVerdict Check(int romId, RelativePath target)
    {
        List<RelativePath?> launches;

        try
        {
            launches = InFlight();
        }
        catch (SqliteException ex)
        {
            return InFlightVerdict.Defer(
                $"the local database could not be read, so it is not safe to say no game is "
                    + $"running ({ex.Message})");
        }

        if (launches.Count == 0)
        {
            return InFlightVerdict.Allowed;
        }

        if (launches.Any(launch => launch is null))
        {
            return InFlightVerdict.Defer(
                "a game is running and RomMBat was not told which one, so writing any save now "
                    + "risks writing under it");
        }

        var romPaths = _store.Files
            .ForRom(romId, LocalFileKind.Rom)
            .Select(file => file.Path)
            .ToHashSet();

        if (launches.Any(launch => romPaths.Contains(launch!.Value)))
        {
            return InFlightVerdict.Defer("this game is running, so its save file is in use");
        }

        // A container shared by more than one game is held by whichever of them is running, so
        // the per-game answer above is not enough for one: Dolphin's GameCube region directory
        // holds every game's .gci side by side, and swapping another game's members into it is
        // the same write under the same open handle. Class A and B land beside the rom as one
        // file per game and are unaffected, which is why an nes download is never deferred for
        // a snes game.
        var system = SystemFolderOf(target, "saves");

        if (system is not null
            && launches.Any(launch => string.Equals(
                SystemFolderOf(launch!.Value, "roms"),
                system,
                StringComparison.OrdinalIgnoreCase))
            && SharedContainerHolding(system, target) is { } container)
        {
            return InFlightVerdict.Defer(
                $"a {system} game is running and {container} is shared by every {system} game, "
                    + "so it is in use");
        }

        return InFlightVerdict.Allowed;
    }

    /// <summary>
    /// The rom path of every game running, with a null for one that cannot be named.
    /// </summary>
    /// <remarks>
    /// Read on every call rather than snapshotted. A flush holds <see cref="TreeLock"/> across
    /// the pass and only a drain writes journal rows under it, but <c>rommbat-agent game-start</c>
    /// writes one directly and takes no lock, so a cached answer could be older than the launch
    /// it is meant to catch.
    /// </remarks>
    private List<RelativePath?> InFlight()
    {
        var launches = new List<RelativePath?>();

        using (var command = _store.Connection.Command(
            """
            SELECT rom_relative_path
            FROM journal
            WHERE event = 'game-start'
              AND state = 'open'
              AND local_sequence > (
                    SELECT COALESCE(MAX(local_sequence), -1)
                    FROM journal
                    WHERE event IN ('start', 'quit'))
            ORDER BY local_sequence;
            """))
        {
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                launches.Add(PathOrNull(reader.GetStringOrNull(0)));
            }
        }

        ApplySpool(launches);
        return launches;
    }

    /// <summary>
    /// Replays what the hooks have spooled since the drain, on top of the journal's answer.
    /// </summary>
    /// <remarks>
    /// Name order is time order, which the hook guarantees by stemming its file with a sortable
    /// UTC timestamp, so this is the same reduction <see cref="PlaytimeCorrelator"/> does over a
    /// journal: a <c>game-start</c> pushes, a <c>game-end</c> pops the newest, and <c>start</c> or
    /// <c>quit</c> ends every game that was running. A record written by a newer hook is counted
    /// as a game that cannot be named rather than ignored, because this build cannot read it and
    /// must not conclude from that that nothing is running.
    /// </remarks>
    private void ApplySpool(List<RelativePath?> launches)
    {
        var directory = _install.Resolve(SpoolDrain.Directory);

        string[] files;

        try
        {
            files = Directory.Exists(directory)
                ? [.. Directory.GetFiles(directory, "*" + SpoolDrain.Extension).Order(StringComparer.Ordinal)]
                : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nothing was ruled out, so nothing may be written. One unnameable entry is how the
            // caller already reports that.
            launches.Add(null);
            return;
        }

        foreach (var file in files)
        {
            string text;

            try
            {
                text = File.ReadAllText(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Being written right now, by a hook whose event is unknown until it commits.
                launches.Add(null);
                continue;
            }

            if (SpoolRecord.Parse(text) is not { } record)
            {
                if (SpoolRecord.IsFromNewerBuild(text))
                {
                    launches.Add(null);
                }

                continue;
            }

            switch (record.Event)
            {
                case "game-start":
                    var argument = record.Arguments.Count > 0 ? record.Arguments[0] : null;

                    launches.Add(
                        !string.IsNullOrWhiteSpace(argument) && _install.Contains(argument)
                            ? _install.Relativize(argument)
                            : null);
                    break;

                case "game-end":
                    if (launches.Count > 0)
                    {
                        launches.RemoveAt(launches.Count - 1);
                    }

                    break;

                case "start":
                case "quit":
                    launches.Clear();
                    break;

                default:
                    break;
            }
        }
    }

    /// <summary>
    /// The shared container <paramref name="target"/> sits in, or null when it sits in none.
    /// </summary>
    /// <remarks>
    /// Both kinds the shape file declares count, because both are shared by every game in the
    /// system: a class C unit container, which is the directory units are keyed inside, and a
    /// class D container, which is one file every game writes to. Matched against the declaration
    /// rather than against the tree, so no directory is listed to answer this, with <c>*</c>
    /// matching one segment the way <c>SaveUnitScanner</c> expands it.
    /// </remarks>
    private string? SharedContainerHolding(string system, RelativePath target)
    {
        var segments = target.Value.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 2)
        {
            return null;
        }

        // Past "saves", so the remainder reads the way a declaration does: the system folder
        // first, then the container beneath it.
        var under = segments[1..];

        foreach (var declared in _shapes.For(system)?.UnitPaths ?? [])
        {
            if (IsUnder(under, declared.Segments, wholeOnly: false))
            {
                return declared.Container;
            }
        }

        foreach (var container in _shapes.SharedContainersFor(system))
        {
            var declared = $"{system}/{container.Key}".Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (IsUnder(under, declared, wholeOnly: true))
            {
                return $"{system}/{container.Key}";
            }
        }

        return null;
    }

    /// <summary>
    /// Whether a path lies inside a declared container.
    /// </summary>
    /// <param name="wholeOnly">
    /// True where the declaration names a file rather than a directory, so only the file itself
    /// matches. A per-game card written beside a shared one is a different file and is not in use.
    /// </param>
    private static bool IsUnder(string[] path, string[] declared, bool wholeOnly)
    {
        if (wholeOnly ? path.Length != declared.Length : path.Length <= declared.Length)
        {
            return false;
        }

        for (var i = 0; i < declared.Length; i++)
        {
            if (declared[i] != "*"
                && !string.Equals(path[i], declared[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The system folder in <c>&lt;root&gt;/&lt;system&gt;/...</c>, or null.</summary>
    private static string? SystemFolderOf(RelativePath path, string root)
    {
        var segments = path.Value.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return segments.Length >= 3
            && string.Equals(segments[0], root, StringComparison.OrdinalIgnoreCase)
            ? segments[1]
            : null;
    }

    private static RelativePath? PathOrNull(string? value) =>
        value is not null && RelativePath.TryCreate(value, out var parsed) ? parsed : null;
}
