using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;

namespace RomMBat.Core.Content;

/// <summary>What one route said about a key.</summary>
internal sealed record RouteAnswer(BindingSource Source, long RomId, RelativePath RomPath, string Detail);

/// <summary>The outcome of attributing one save unit.</summary>
/// <param name="Detail">
/// Why the answer is what it is, or which routes disagreed. Carried into the binding row and
/// into the unsyncable report, so a user asking "why is this save not going up" gets the
/// reason rather than the absence.
/// </param>
public sealed record Attribution(long? RomId, RelativePath? RomPath, BindingSource? Source, string Detail)
{
    public bool IsResolved => RomId is not null;
}

/// <summary>
/// Works out which ROM a class C save unit belongs to.
/// </summary>
/// <remarks>
/// <b>RomM stores no serial, title id or product code anywhere</b>, on the ROM model or in any
/// response, so there is no API lookup and every route here is client-side. Three of them ship,
/// and each covers what the others cannot:
/// <list type="number">
/// <item><b>The key is already a filename.</b> MAME's <c>nvram</c> short name <i>is</i> the ROM
/// basename, so it resolves through the same <c>(folder, stem)</c> index class A uses and needs
/// no binding at all: nothing was learned, so nothing is cached.</item>
/// <item><b>The save-state sidecar.</b> RetroBat writes a <c>.txt</c> beside a state holding the
/// emulator's native basename, and where that is identifier-keyed it is a Game ID already joined
/// to a ROM filename. Measured: PPSSPP's <c>3rd Birthday, The (Europe).txt</c> holds
/// <c>ULES01513_1.00</c>, matching <c>SAVEDATA/ULES01513SYSDATA</c>. Free, reads no ROM, and
/// reaches saves that predate RomMBat, on any game that has a state.</item>
/// <item><b>The launch window.</b> A unit whose newest member was written inside a launch of the
/// same system belongs to that launch's ROM. Generalises to every odd case and needs no format
/// parsing, and it is the only route that reaches a WAD.</item>
/// <item><b>The ROM header.</b> A game code at a fixed offset. Measured at 100% of GameCube and
/// 75.5% of Wii, and <b>0% of PSP, PS3 and PSX</b>, so it is irreplaceable on the two systems
/// whose save key <i>is</i> the game code and useless on the rest.</item>
/// </list>
/// <para>
/// <b>Disagreement fails closed.</b> Two routes naming different ROMs means the unit is not
/// uploaded, a binding is written with a null <c>rom_id</c> so the work is not repeated every
/// scan, and both candidates are named in the report. Picking a side would upload one game's
/// save under another game's name, and the cache would then make that permanent.
/// </para>
/// </remarks>
public sealed class GameIdAttributor
{
    /// <summary>
    /// How close two launches may sit before the newest-file rule stops being able to separate them.
    /// </summary>
    /// <remarks>
    /// exFAT and FAT32 both quantise mtime to two seconds and round up, so a save written at the
    /// very end of one session can be stamped inside the next. Two launches naming different
    /// ROMs inside that band are indistinguishable by mtime and the unit is refused rather than
    /// given to whichever sorts first.
    /// </remarks>
    public static TimeSpan LaunchAmbiguity { get; } = TimeSpan.FromSeconds(4);

    private readonly RetroBatInstall _install;
    private readonly LocalStore _store;
    private readonly RomIndex _roms;
    private readonly TimeProvider _time;
    private readonly IReadOnlyList<LaunchRecord> _launches;
    private readonly Dictionary<string, Dictionary<string, RouteAnswer>> _headers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, RouteAnswer>> _sidecars = new(StringComparer.OrdinalIgnoreCase);

    public GameIdAttributor(
        RetroBatInstall install,
        LocalStore store,
        RomIndex roms,
        IReadOnlyList<LaunchRecord>? launches = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(roms);

        _install = install;
        _store = store;
        _roms = roms;
        _time = timeProvider ?? TimeProvider.System;
        _launches = launches ?? ReadLaunches(install);
    }

    /// <summary>Attributes one unit, using the cache when it already knows.</summary>
    public Attribution Attribute(SaveUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        // The key is a filename, so nothing has to be learned or cached. MAME is the whole of
        // this case and it is why MAME needed no attribution work at all.
        if (_roms.Find(unit.System, unit.Key) is { } direct)
        {
            return new Attribution(
                direct.RomId,
                direct.Path,
                null,
                $"the unit key {unit.Key} is the name of a ROM in saves/{unit.System}");
        }

        if (_store.GameIdBindings.Find(unit.System, unit.Key) is { } cached)
        {
            // Reused without re-reading a ROM or re-walking the launch log, which is the whole
            // point of the cache: an odd case is worked out once. A row with no rom_id is a
            // decision too, and repeating the work would not change it.
            return cached.IsResolved
                ? new Attribution(cached.RomId, cached.RomPath, cached.LearnedFrom, Describe(cached))
                : new Attribution(null, null, null, cached.Detail ?? "nothing could attribute this unit");
        }

        var answers = new List<RouteAnswer>();

        if (FromSidecar(unit) is { } sidecar)
        {
            answers.Add(sidecar);
        }

        if (FromLaunchWindow(unit) is { } journal)
        {
            answers.Add(journal);
        }

        if (FromRomHeader(unit) is { } header)
        {
            answers.Add(header);
        }

        var now = _time.GetUtcNow();

        if (answers.Count == 0)
        {
            var reason =
                $"no route could say which game {unit.Key} belongs to: no save state names it, "
                    + "no launch of this system covers when it was written, and no ROM header carries it";

            Remember(unit, null, null, BindingSource.Journal, reason, now);
            return new Attribution(null, null, null, reason);
        }

        var distinct = answers.Select(answer => answer.RomId).Distinct().ToList();

        if (distinct.Count > 1)
        {
            // The fail-closed case. Named in full rather than counted, because the user is the
            // only one who can settle it and `saves bind` is how.
            var reason =
                $"two routes disagree about {unit.Key}: "
                    + string.Join("; ", answers.Select(answer => answer.Detail))
                    + ". It is left alone until `saves bind` settles it";

            Remember(unit, null, null, BindingSource.Journal, reason, now);
            return new Attribution(null, null, null, reason);
        }

        var agreed = answers[0];

        Remember(unit, agreed.RomId, agreed.RomPath, agreed.Source, agreed.Detail, now);
        return new Attribution(agreed.RomId, agreed.RomPath, agreed.Source, agreed.Detail);
    }

    private void Remember(
        SaveUnit unit,
        long? romId,
        RelativePath? romPath,
        BindingSource source,
        string detail,
        DateTimeOffset now) =>
        _store.GameIdBindings.Record(new GameIdBinding(
            unit.System,
            unit.Key,
            romId,
            romPath,
            source,
            detail,
            now));

    /// <summary>
    /// The save-state sidecar route: a ROM filename already joined to a native identifier.
    /// </summary>
    /// <remarks>
    /// Built once per system and cached, because it is a scan of <c>local_state</c> rather than
    /// a per-unit lookup. Absence means nothing: <c>libretro</c> writes no sidecar under either
    /// core and <c>bizhawk</c> writes its own truncated name plus the core, so only contents
    /// that parse as an identifier are used and everything else is ignored.
    /// </remarks>
    private RouteAnswer? FromSidecar(SaveUnit unit)
    {
        if (!_sidecars.TryGetValue(unit.System, out var byKey))
        {
            byKey = new Dictionary<string, RouteAnswer>(StringComparer.OrdinalIgnoreCase);

            foreach (var state in _store.States.List())
            {
                if (state.NativeName is not { } native
                    || state.RomId is not { } romId
                    || state.RomPath is not { } romPath
                    || !string.Equals(state.System, unit.System, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // ULES01513_1.00 carries the disc version behind an underscore, and SLUS-00404
                // does not. Taking the part before the first underscore covers both without
                // knowing which system wrote it.
                var identifier = native.Split('_', 2)[0].Trim();

                if (identifier.Length == 0)
                {
                    continue;
                }

                byKey[identifier] = new RouteAnswer(
                    BindingSource.Sidecar,
                    romId,
                    romPath,
                    $"the save state beside {romPath.Name} names {native}");
            }

            _sidecars[unit.System] = byKey;
        }

        return byKey.GetValueOrDefault(unit.Key);
    }

    /// <summary>
    /// The launch-window route: whoever was running when the newest member was written.
    /// </summary>
    /// <remarks>
    /// The window is bounded by the next launch of the same system rather than by a duration,
    /// so a long session is not lost and a save written a week after anything ran is not given
    /// to the last thing that did. Two launches naming different ROMs within
    /// <see cref="LaunchAmbiguity"/> of the mtime are refused, since a coarse filesystem clock
    /// cannot separate them.
    /// </remarks>
    private RouteAnswer? FromLaunchWindow(SaveUnit unit)
    {
        if (unit.NewestMtimeUtc is not { } written)
        {
            return null;
        }

        var candidates = _launches
            .Where(launch => !launch.IsMenuLaunch && launch.RomPath is not null)
            .Where(launch => string.Equals(launch.System, unit.System, StringComparison.OrdinalIgnoreCase))
            .Where(launch => launch.At <= written + LaunchAmbiguity)
            .OrderByDescending(launch => launch.At)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        var covering = candidates[0];

        // Anything else this close names a different session and the mtime cannot say which,
        // because exFAT and FAT32 round to two seconds and round up.
        var ambiguous = candidates
            .Skip(1)
            .Where(launch => covering.At - launch.At <= LaunchAmbiguity)
            .Any(launch => launch.RomPath != covering.RomPath);

        if (ambiguous || _roms.Find(unit.System, Stem(covering.RomPath!.Value)) is not { } rom)
        {
            return null;
        }

        return new RouteAnswer(
            BindingSource.Journal,
            rom.RomId,
            rom.Path,
            $"{covering.RomPath!.Value.Name} was running when {unit.Key} was last written "
                + $"({covering.At:u} against {written:u})");
    }

    /// <summary>
    /// The ROM-header route: 256 bytes off the head of every ROM in the system, once.
    /// </summary>
    /// <remarks>
    /// Reversed rather than looked up, because the question is "which ROM carries this code"
    /// and a code cannot be turned back into a filename. On the two systems where this route
    /// works that is 178 and 40 files of 256 bytes, which is free; on the three where it does
    /// not, every read refuses with a reason and the index comes back empty.
    /// </remarks>
    private RouteAnswer? FromRomHeader(SaveUnit unit)
    {
        if (!_headers.TryGetValue(unit.System, out var byCode))
        {
            byCode = new Dictionary<string, RouteAnswer>(StringComparer.OrdinalIgnoreCase);

            foreach (var (romId, romPath) in _roms.InFolder(unit.System))
            {
                var read = RomGameId.Read(_install.Resolve(romPath));

                if (read.GameId is not { } code)
                {
                    continue;
                }

                // First wins. Two ROMs sharing a game code are a revision pair, and either is
                // as good an answer as the other for a save keyed on the code they share.
                byCode.TryAdd(
                    code,
                    new RouteAnswer(
                        BindingSource.RomHeader,
                        romId,
                        romPath,
                        $"{romPath.Name} carries the game code {code} in its header"));
            }

            _headers[unit.System] = byCode;
        }

        return byCode.GetValueOrDefault(unit.Key);
    }

    private static string Describe(GameIdBinding binding) =>
        binding.Detail ?? $"learned from {binding.LearnedFrom}";

    private static string Stem(RelativePath path) => Path.GetFileNameWithoutExtension(path.Value);

    private static IReadOnlyList<LaunchRecord> ReadLaunches(RetroBatInstall install)
    {
        try
        {
            return new LaunchLog(install).Read();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // No launch log is the ordinary state of a fresh install, and it costs this route
            // rather than the whole pass.
            return [];
        }
    }
}
