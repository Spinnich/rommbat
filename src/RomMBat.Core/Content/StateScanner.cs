using System.Diagnostics;
using System.Text.RegularExpressions;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;

namespace RomMBat.Core.Content;

/// <summary>What a state scan found.</summary>
public sealed record StateScanOutcome
{
    /// <summary>States recorded, attributed or not.</summary>
    public int Found { get; init; }

    /// <summary>Of those, the ones tied to a ROM and therefore uploadable.</summary>
    public int Attributed { get; init; }

    /// <summary>Rows dropped because the file is gone.</summary>
    public int Forgotten { get; init; }

    /// <summary>Screenshots found beside a state and worth sending.</summary>
    public int Screenshots { get; init; }

    public long BytesHashed { get; init; }

    /// <summary>
    /// Names in a state directory that are neither a state this build can sync nor a sidecar.
    /// </summary>
    /// <remarks>
    /// <b>The alternative is silence, and silence is the defect.</b> A name matching an
    /// emulator's template except for the width of its slot is a state that emulator really
    /// wrote, and it used to be dropped by the same rule that passes over the <c>.txt</c>
    /// sidecar and the screenshots. A slot outside the declared bounds is synced and was never
    /// mentioned. See #34 and #65.
    /// </remarks>
    public IReadOnlyList<SaveStateNearMiss> NearMisses { get; init; } = [];

    public string Summary => Found == 0 && NearMisses.Count == 0
        ? "states: none found"
        : $"states: {Attributed} of {Found} attributed"
            + (NearMisses.Count == 0 ? string.Empty : $", {NearMisses.Count} worth a look");
}

/// <summary>
/// Finds save states under the directories <c>es_savestates.cfg</c> declares.
/// </summary>
/// <remarks>
/// <b>Discovery reverses both templates rather than enumerating anything.</b> The
/// <c>&lt;directory&gt;</c> template is matched against directories that exist, which yields the
/// system and the core from disk instead of from a guess, and the <c>&lt;file&gt;</c> template is
/// matched against the names inside it, which yields the ROM stem and the slot. Neither level of
/// the save tree is positional, so asking "which declaration could have produced this path" is
/// the only reading that does not invent an emulator out of a directory name.
/// <para>
/// <b>Attribution needs no Game ID.</b> Every <c>&lt;file&gt;</c> template is keyed on
/// <c>{{romfilename}}</c>, and all twelve emulators driven on a real install wrote the name the
/// template predicted, so a state resolves through the same <c>(folder, stem)</c> index a class A
/// battery save does. That is why states ship a stage ahead of directory saves.
/// </para>
/// <para>
/// <b>One declared directory is wrong and this scanner cannot see past that.</b>
/// <c>openmsx</c> writes <c>bios/openmsx/savestates/</c>, a different top-level tree from the
/// declared <c>saves/msx1/openmsx</c>, so no expansion of the declaration can reach it and a
/// scan of it finds an empty directory. Nothing is guessed: the states are reported as
/// unsyncable with the reason, because reading the wrong tree is worse than reading none.
/// </para>
/// <para>
/// <b><c>flycast</c> used to be the other one and no longer is.</b> On RetroBat 8.2.0 it wrote
/// <c>saves/dreamcast/reicast/states/</c> and the declared <c>flycast/sstates</c> stayed empty;
/// 8.2.1 fixed that (<c>emulatorlauncher#1336</c>) and a hands-on pass confirmed the state is
/// mirrored into the declared path in the same millisecond it is written natively. 8.2.1 is the
/// minimum supported version, so the declaration is now the one to read.
/// </para>
/// </remarks>
public sealed class StateScanner
{
    private readonly RetroBatInstall _install;
    private readonly LocalStore _store;
    private readonly SaveStateSchema _schema;
    private readonly TimeProvider _time;
    private readonly string? _retroBatVersion;
    private readonly Dictionary<string, string?> _versions = new(StringComparer.OrdinalIgnoreCase);

    public StateScanner(
        RetroBatInstall install,
        LocalStore store,
        SaveStateSchema schema,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(schema);

        _install = install;
        _store = store;
        _schema = schema;
        _time = timeProvider ?? TimeProvider.System;
        _retroBatVersion = install.ReadVersionString();
    }

    /// <summary>
    /// Emulators whose declared directory is not where they write.
    /// </summary>
    /// <remarks>
    /// Measured on a real install. The declared directory exists and is empty, which is the
    /// trap: a client that trusts the declaration concludes the game has no states rather than
    /// concluding it is looking in the wrong place.
    /// <para>
    /// <b><c>flycast</c> was here until RetroBat 8.2.1 and is not any more.</b> It was filed as
    /// RetroBat-Official/emulatorlauncher#1336 and fixed by pointing RetroBat's save-state
    /// watcher at the directory Flycast really writes. Confirmed by hand on 8.2.1 rather than
    /// taken from the changelog, three runs of `Sega Tetris (Japan) (Rev A)` under
    /// <c>tools/m0-probes/probe2-flycast-mirror.ps1</c>: the state lands in both
    /// <c>reicast/states</c> and the declared <c>flycast/sstates</c>, same bytes, same
    /// millisecond, while the emulator is still running. What makes Dreamcast states sync is
    /// 8.2.1 populating the declared directory, which <see cref="Scan"/> already walks; this
    /// registry documents the trap rather than gating the scan, so the entry coming out
    /// records that the declaration became usable.
    /// </para>
    /// </remarks>
    public static IReadOnlyDictionary<string, string> WrongDeclaredDirectories { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["openmsx"] = "openMSX writes bios/openmsx/savestates/, which is a different tree "
                + "from the declared saves/<system>/openmsx entirely.",
        };

    /// <summary>Opens the schema from the install, or null when the file is not there.</summary>
    public static SaveStateSchema? LoadSchema(RetroBatInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);
        return SaveStateSchema.Load(install.Resolve(SaveStateSchema.ConfigPath));
    }

    /// <summary>Walks every declared state directory that exists and records what is in it.</summary>
    public StateScanOutcome Scan()
    {
        var savesRoot = _install.Resolve(SaveScanner.SavesDirectory);
        var now = _time.GetUtcNow();

        if (!Directory.Exists(savesRoot))
        {
            return new StateScanOutcome();
        }

        var roms = RomIndex.Build(_store);
        var seen = new HashSet<RelativePath>();
        var slots = new HashSet<(long RomId, string Slot)>();
        var nearMisses = new List<SaveStateNearMiss>();
        var found = 0;
        var attributed = 0;
        var screenshots = 0;
        var bytes = 0L;

        foreach (var (template, directory) in ResolveDirectories(savesRoot))
        {
            foreach (var file in Directory.EnumerateFiles(directory).Order(StringComparer.Ordinal))
            {
                if (template.NearMiss(Path.GetFileName(file)) is { } nearMiss)
                {
                    nearMisses.Add(nearMiss);
                }

                var state = Describe(template, file, roms, now);

                if (state is null)
                {
                    continue;
                }

                // local_state is UNIQUE on (rom_id, slot) as well as on the path, and a directory
                // can hold two names that read as one slot: libretro's free-width token matches
                // both Game.state and Game.state0 at slot zero. The scanner takes any file the
                // template matches, whoever wrote it, so this has to cost one file rather than
                // throwing SqliteException out of the whole pass.
                if (state.RomId is { } romId && !slots.Add((romId, state.Slot)))
                {
                    continue;
                }

                _store.States.Record(state, now);
                seen.Add(state.Path);
                found++;
                bytes += state.SizeBytes;

                if (state.RomId is not null)
                {
                    attributed++;
                }

                if (state.ScreenshotPath is not null)
                {
                    screenshots++;
                }
            }
        }

        return new StateScanOutcome
        {
            Found = found,
            Attributed = attributed,
            Forgotten = ForgetMissing(seen),
            Screenshots = screenshots,
            BytesHashed = bytes,

            // Ordinal by name, so two passes over one tree report in one order.
            NearMisses = [.. nearMisses.OrderBy(miss => miss.FileName, StringComparer.Ordinal)],
        };
    }

    /// <summary>
    /// Every (emulator, system, core) whose declared directory is actually on disk.
    /// </summary>
    /// <remarks>
    /// Built by matching each <c>&lt;directory&gt;</c> template against the directories that
    /// exist, so the system and the core come from the tree rather than from a list this client
    /// would have to keep in step with the user's emulator choices.
    /// </remarks>
    private List<(SaveStateTemplate Template, string Directory)> ResolveDirectories(string savesRoot)
    {
        var resolved = new List<(SaveStateTemplate, string)>();

        foreach (var depth in _schema.Emulators.Select(emulator => DepthOf(emulator.Directory)).Distinct())
        {
            foreach (var directory in EnumerateDirectories(savesRoot, depth))
            {
                var relative = Path.GetRelativePath(savesRoot, directory).Replace('\\', '/');

                if (_schema.MatchDirectory(relative) is not { } claimed)
                {
                    continue;
                }

                if (SaveStateTemplate.Create(claimed.Emulator, claimed.System, claimed.Core) is { } template)
                {
                    resolved.Add((template, directory));
                }
            }
        }

        return resolved;
    }

    private LocalState? Describe(SaveStateTemplate template, string file, RomIndex roms, DateTimeOffset now)
    {
        if (!_install.Contains(file))
        {
            return null;
        }

        var name = Path.GetFileName(file);
        var match = template.Match(name);

        if (match is null)
        {
            // Not a state by this emulator's own filename rule. Left alone rather than reported
            // per file: a state directory holds the .txt sidecar and the screenshots too, and
            // naming each of those as unsyncable would drown the report it belongs in.
            //
            // A name that misses only on the width of its slot is not one of those, and the
            // caller has already put it on the near-miss list before reaching here.
            return null;
        }

        var path = _install.Relativize(file);
        var info = new FileInfo(file);
        var rom = roms.Find(template.System, match.Stem);

        string? hash = null;

        try
        {
            hash = LogicalContentHash.OfFile(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Held open by a running emulator. Recorded without a hash, which reads as unsent
            // and is never uploaded, because sending bytes whose integrity was not checked is
            // worse than sending nothing.
        }

        return new LocalState
        {
            Path = path,
            System = template.System,
            Emulator = template.Emulator.Name,
            Core = template.Core ?? string.Empty,
            EmulatorVersion = ReadEmulatorVersion(template),
            RetroBatVersion = _retroBatVersion,
            RomId = rom?.RomId,
            RomPath = rom?.Path,
            Slot = match.SlotKey(template.Emulator.Name, template.Core),
            ScreenshotPath = ResolveScreenshot(template, match, file),
            NativeName = ReadNativeName(file, match.Stem),
            ContentHash = hash,
            SizeBytes = info.Length,
            FileMtimeUtc = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
        };
    }

    /// <summary>
    /// The screenshot beside a state, when there is a real one.
    /// </summary>
    /// <remarks>
    /// Three things have to be true and each rules out a case that was actually observed. The
    /// emulator has to declare an <c>&lt;image&gt;</c> distinct from its <c>&lt;file&gt;</c>,
    /// which DeSmuME does not, so uploading blind would send the state as its own preview. The
    /// file has to be there, and it was missing in four of seven emulators driven. And it has to
    /// be non-empty, because RetroBat's mirror races the emulator writing it and a zero-byte
    /// image was measured; the server accepts one and stores it, so nothing downstream catches
    /// this if the client does not.
    /// </remarks>
    private RelativePath? ResolveScreenshot(SaveStateTemplate template, SaveStateMatch match, string statePath)
    {
        if (template.ImageFor(match) is not { } imageName)
        {
            return null;
        }

        var image = Path.Combine(Path.GetDirectoryName(statePath)!, imageName);

        if (!File.Exists(image) || new FileInfo(image).Length == 0 || !_install.Contains(image))
        {
            return null;
        }

        return _install.Relativize(image);
    }

    /// <summary>
    /// The <c>.txt</c> sidecar's content, which is the emulator's own name for the game.
    /// </summary>
    /// <remarks>
    /// Most states have none, so null is the ordinary answer here. Driven on a real install,
    /// <c>libretro</c> wrote no sidecar under either core, <c>jgenesis</c> wrote the plain ROM
    /// filename, and <c>bizhawk</c> wrote its own truncated name plus the core. Absence and
    /// presence both signal nothing, and only the content is ever worth anything: where it holds
    /// a serial (<c>SLUS-00404</c>, <c>GW7E69</c>) it is the Game ID that directory-save
    /// attribution otherwise reads out of a ROM header. Finding 136.
    /// </remarks>
    private static string? ReadNativeName(string statePath, string stem)
    {
        var sidecar = Path.Combine(Path.GetDirectoryName(statePath)!, stem + ".txt");

        try
        {
            if (!File.Exists(sidecar))
            {
                return null;
            }

            var text = File.ReadAllText(sidecar).Trim();

            // Bounded, because this is an untrusted file in a directory the user owns and the
            // column is a hint rather than a document.
            return text.Length is > 0 and <= 256 ? text : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// What produced the state, so it is never restored silently onto a different build.
    /// </summary>
    /// <remarks>
    /// <b>Best effort, and null is an honest answer.</b> RetroBat downloads emulators on demand
    /// and does not record their versions anywhere this client can read, so the version comes
    /// from the binary: a libretro core is one DLL under RetroArch's <c>cores/</c>, and every
    /// other emulator is looked up only where its folder holds exactly one top-level executable.
    /// Where it holds several there is no rule that says which one ran, and picking is worse than
    /// declining. <c>retrobat_version</c> is recorded alongside for that reason, since RetroBat
    /// ships the emulator builds and its own version always reads.
    /// <para>
    /// <b>Measured on a real install, and it finds nothing on any emulator tried.</b> A libretro
    /// core DLL carries an empty <c>ProductVersion</c> and <c>FileVersion</c>, and both
    /// <c>jgenesis</c> and <c>bizhawk</c> ship two top-level executables
    /// (<c>jgenesis-cli</c>/<c>jgenesis-gui</c>, <c>EmuHawk</c>/<c>DiscoHawk</c>), so the
    /// single-executable rule declines. So this column is null in practice today and
    /// <c>retrobat_version</c> is what actually identifies the build.
    /// </para>
    /// <para>
    /// Left declining rather than loosened. Picking one of two executables by name, or reading
    /// RetroArch's version and calling it the core's, would put a number in the column that does
    /// not describe what wrote the state, and a wrong version is worse than no version for the
    /// one job this field has: refusing to restore a state onto a different build.
    /// </para>
    /// </remarks>
    private string? ReadEmulatorVersion(SaveStateTemplate template)
    {
        var key = $"{template.Emulator.Name}/{template.Core}";

        if (_versions.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var version = ReadVersion(template);
        _versions[key] = version;
        return version;
    }

    private string? ReadVersion(SaveStateTemplate template)
    {
        try
        {
            if (string.Equals(template.Emulator.Name, "libretro", StringComparison.OrdinalIgnoreCase))
            {
                if (template.Core is not { Length: > 0 } core)
                {
                    return null;
                }

                var dll = _install.Resolve(
                    RelativePath.Create($"emulators/retroarch/cores/{core}_libretro.dll"));

                return File.Exists(dll) ? Describe(FileVersionInfo.GetVersionInfo(dll)) : null;
            }

            var folder = _install.Resolve(
                RelativePath.Create($"emulators/{template.Emulator.Name}"));

            if (!Directory.Exists(folder))
            {
                return null;
            }

            var executables = Directory.GetFiles(folder, "*.exe", SearchOption.TopDirectoryOnly);

            return executables.Length == 1 ? Describe(FileVersionInfo.GetVersionInfo(executables[0])) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private static string? Describe(FileVersionInfo info) =>
        info.ProductVersion?.Trim() is { Length: > 0 } product ? product
        : info.FileVersion?.Trim() is { Length: > 0 } file ? file
        : null;

    private int ForgetMissing(HashSet<RelativePath> seen)
    {
        var gone = _store.States
            .List()
            .Where(state => !seen.Contains(state.Path))
            .Select(state => state.Path)
            .ToList();

        return gone.Count == 0 ? 0 : _store.States.Forget(gone);
    }

    /// <summary>How many segments deep a directory template goes below <c>saves/</c>.</summary>
    private static int DepthOf(string template) => template.Split('/', StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>Directories at exactly one depth, so a deep tree is not walked to find a shallow one.</summary>
    private static List<string> EnumerateDirectories(string root, int depth)
    {
        var current = new List<string> { root };

        for (var level = 0; level < depth; level++)
        {
            var next = new List<string>();

            foreach (var directory in current)
            {
                try
                {
                    next.AddRange(Directory.EnumerateDirectories(directory));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // A directory that cannot be listed is one this pass does not know about,
                    // which is the same outcome as it not existing.
                }
            }

            current = next;
        }

        return current;
    }

}
