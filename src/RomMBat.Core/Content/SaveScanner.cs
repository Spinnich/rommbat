using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;

namespace RomMBat.Core.Content;

/// <summary>What a scan found.</summary>
public sealed record SaveScanOutcome
{
    /// <summary>Class A and B saves recorded, attributed or not.</summary>
    public int Found { get; init; }

    /// <summary>Of those, the ones tied to a ROM and therefore uploadable.</summary>
    public int Attributed { get; init; }

    /// <summary>Rows dropped because the file is gone.</summary>
    public int Forgotten { get; init; }

    /// <summary>Reasons written to the unsyncable report.</summary>
    public int Unsyncable { get; init; }

    /// <summary>Bytes hashed, which is what the pass cost.</summary>
    public long BytesHashed { get; init; }

    public string Summary =>
        Found == 0 && Unsyncable == 0
            ? "saves: nothing found"
            : $"saves: {Attributed} of {Found} attributed, {Unsyncable} reported unsyncable";
}

/// <summary>
/// Finds battery saves on disk, works out which ROM each belongs to, and reports the rest.
/// </summary>
/// <remarks>
/// <b>Discovery is not positional, in either direction.</b> M6 re-inventoried a real tree and
/// neither level of <c>saves/&lt;system&gt;/&lt;emulator&gt;/</c> can be read by position: nine
/// top-level directories are not declared systems (<c>dolphin</c>, <c>mesen</c>,
/// <c>gameandwatch</c>, <c>windows</c> and five more), and twelve second-level ones name no
/// emulator (<c>mame/artwork</c>, <c>n64/sram</c>, <c>psp/SYSTEM</c>, <c>switch/user</c>).
/// So the shape definition names the paths, and <b>anything it does not name is reported as
/// unknown rather than guessed at</b>. That is <c>SaveGuard</c>'s fail-closed rule applied one
/// level earlier: the cost of being wrong here is writing over someone's save.
/// <para>
/// <b>Loose does not mean class A, and a class is not a property of a directory.</b>
/// <c>xbox</c> keeps <c>eeprom.bin</c> and a 39 MB disk image loose under the system folder
/// where class A normally lives, and <c>megacd</c> keeps per-game <c>.brm</c> files beside a
/// shared <c>4Mbit_cart.brm</c> at the same level. Both are excluded by name, from a list
/// declared in a finding, because nothing about either file distinguishes it.
/// </para>
/// <para>
/// <b>mtime cannot decide whether a save needs uploading, for any class.</b> A Master System
/// cart booted to its title screen with no save key pressed wrote an 8,188-byte <c>.srm</c> of
/// legible ASCII, which is the cartridge formatting its own backup RAM, and a PS2 launch
/// rewrites both shared memory cards untouched. So every save is content-hashed. That costs
/// 0.51 s across 37 files on a real install, which is why it is affordable to do unconditionally.
/// </para>
/// </remarks>
public sealed class SaveScanner
{
    private readonly RetroBatInstall _install;
    private readonly LocalStore _store;
    private readonly SaveShapes _shapes;
    private readonly TimeProvider _time;

    public SaveScanner(
        RetroBatInstall install,
        LocalStore store,
        SaveShapes? shapes = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(store);

        _install = install;
        _store = store;
        _shapes = shapes ?? SaveShapes.Bundled;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Where saves live.</summary>
    public static RelativePath SavesDirectory { get; } = RelativePath.Create("saves");

    /// <summary>Walks the save tree and records what it finds.</summary>
    public SaveScanOutcome Scan()
    {
        var savesRoot = _install.Resolve(SavesDirectory);
        var now = _time.GetUtcNow();

        if (!Directory.Exists(savesRoot))
        {
            return new SaveScanOutcome();
        }

        var found = 0;
        var attributed = 0;
        var bytes = 0L;
        var seen = new HashSet<RelativePath>();

        // Accumulated rather than written per file, because the report is keyed on
        // (system, emulator, reason): writing each file as it is met would leave one row
        // naming the last file and counting one, which understates the gap it exists to show.
        var report = new UnsyncableReport();

        // ROM basename to (rom_id, path), which is the whole of class A and B attribution:
        // the save is named after the ROM file. Built once rather than queried per save.
        var romsByStem = BuildRomIndex();

        foreach (var systemDirectory in Directory.EnumerateDirectories(savesRoot).Order(StringComparer.Ordinal))
        {
            var system = Path.GetFileName(systemDirectory);
            var shape = _shapes.For(system);

            if (shape is null)
            {
                // Nine top-level directories on a real install are not declared systems at
                // all, and 21 declared ones carry no shape. Both land here, and neither is
                // touched: an unknown tree is not a tree to start writing into.
                var files = CountFiles(systemDirectory);
                if (files > 0)
                {
                    report.Add(
                        system,
                        string.Empty,
                        UnsyncableReason.UnknownShape,
                        $"no shape definition covers saves/{system}/, so nothing under it is read",
                        files);
                }

                continue;
            }

            foreach (var file in Directory.EnumerateFiles(systemDirectory).Order(StringComparer.Ordinal))
            {
                var name = Path.GetFileName(file);
                var extension = Path.GetExtension(name);

                if (_shapes.IsNotASave(extension))
                {
                    continue;
                }

                if (_shapes.SharedContainerReason(system, name) is { } container)
                {
                    report.Add(system, _shapes.LooseEmulator, UnsyncableReason.SharedContainer, container, 1);
                    continue;
                }

                if (!_shapes.IsBatteryExtension(extension))
                {
                    report.Add(
                        system,
                        _shapes.LooseEmulator,
                        UnsyncableReason.UnknownShape,
                        $"{extension} is not an extension RomMBat recognises as a save",
                        1);
                    continue;
                }

                var save = Describe(system, file, romsByStem);
                if (save is null)
                {
                    continue;
                }

                _store.Saves.Record(save, now);
                seen.Add(save.Path);
                found++;
                bytes += save.SizeBytes;

                if (save.RomId is not null)
                {
                    attributed++;
                }
                else
                {
                    report.Add(
                        system,
                        save.Emulator,
                        UnsyncableReason.Unattributed,
                        "matches no ROM this device holds, so there is no game to upload it against",
                        1);
                }
            }

            // Every subdirectory of a system folder is class C, class D or a save state, and
            // stage 1 ships none of them. Counted per system rather than per file.
            AddSubdirectories(report, system, shape, systemDirectory);
        }

        var forgotten = ForgetMissing(seen);
        var unsyncable = report.WriteTo(_store.Unsyncable, now);

        // Anything not re-observed in this pass is no longer true, so a system that becomes
        // syncable stops being listed and the count stays an honest measure of coverage.
        _store.Unsyncable.ForgetOlderThan(now);

        return new SaveScanOutcome
        {
            Found = found,
            Attributed = attributed,
            Forgotten = forgotten,
            Unsyncable = unsyncable,
            BytesHashed = bytes,
        };
    }

    /// <summary>
    /// Builds the slot a save pairs on.
    /// </summary>
    /// <remarks>
    /// <b>Stable and non-null, because a null slot is excluded from pairing and negotiates as
    /// an upload forever.</b> Class A is <c>{emulator}:battery</c>. Class B takes one slot per
    /// file, <c>{emulator}:battery:{ext}</c>, so saturn's <c>.bcr</c> and <c>.bkr</c> do not
    /// overwrite each other in one slot. The extension is part of the key rather than the
    /// filename, so a save renamed on restore still lands in the same slot.
    /// </remarks>
    public static string SlotFor(string emulator, SaveShapeClass shapeClass, string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emulator);

        var trimmed = extension.TrimStart('.').ToLowerInvariant();

        return shapeClass == SaveShapeClass.B && trimmed.Length > 0
            ? $"{emulator}:battery:{trimmed}"
            : $"{emulator}:battery";
    }

    private LocalSave? Describe(
        string system,
        string file,
        Dictionary<string, (long RomId, RelativePath Path)> romsByStem)
    {
        if (!_install.Contains(file))
        {
            return null;
        }

        var path = _install.Relativize(file);
        var info = new FileInfo(file);
        var extension = Path.GetExtension(file);
        var stem = Path.GetFileNameWithoutExtension(file);

        // The class the file is, not the class the system is: megacd is declared BD, and a
        // per-game .brm there is class B while the shared cart is D and never reaches here.
        var shapeClass = _shapes.For(system)?.Classes.FirstOrDefault(value =>
            value is SaveShapeClass.A or SaveShapeClass.B) ?? SaveShapeClass.A;

        romsByStem.TryGetValue(stem, out var rom);

        string? hash = null;
        try
        {
            hash = LogicalContentHash.OfFile(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Locked by a running emulator. Recorded without a hash, which reads as unsent and
            // therefore blocks eviction, which is the fail-closed direction.
        }

        return new LocalSave
        {
            Path = path,
            System = system,
            Emulator = _shapes.LooseEmulator,
            ShapeClass = shapeClass,
            Slot = SlotFor(_shapes.LooseEmulator, shapeClass, extension),
            RomId = rom.RomId == 0 ? null : rom.RomId,
            RomPath = rom.RomId == 0 ? null : rom.Path,
            ContentHash = hash,
            SizeBytes = info.Length,
            FileMtimeUtc = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
        };
    }

    /// <summary>ROM stem to id, which is what a class A or B save is named after.</summary>
    private Dictionary<string, (long RomId, RelativePath Path)> BuildRomIndex()
    {
        var index = new Dictionary<string, (long, RelativePath)>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in _store.Files.List())
        {
            if (file.Kind != LocalFileKind.Rom || file.RomId is not { } romId)
            {
                continue;
            }

            // First wins. Two ROMs sharing a stem across systems is possible, and the save
            // tree is already system-scoped, so a collision here would need the same stem
            // inside one system, which the filesystem forbids.
            index.TryAdd(Path.GetFileNameWithoutExtension(file.FileName), (romId, file.Path));
        }

        return index;
    }

    private static void AddSubdirectories(
        UnsyncableReport report,
        string system,
        SaveShape shape,
        string systemDirectory)
    {
        var subdirectories = Directory.EnumerateDirectories(systemDirectory).ToList();
        var files = subdirectories.Sum(CountFiles);

        if (files == 0)
        {
            return;
        }

        var classes = string.Concat(shape.Classes.Select(value => value.ToString()));
        var names = string.Join(", ", subdirectories.Select(Path.GetFileName).Order(StringComparer.Ordinal));

        // The system's own class is named because a reader will ask why a class A system has
        // anything unsyncable at all. The answer is that the class describes its battery
        // saves, which are the loose files, and these subdirectories are the emulators' own
        // trees: memory cards, directory saves and save states, none of which ship here.
        report.Add(
            system,
            string.Empty,
            UnsyncableReason.NotInThisVersion,
            $"{names} hold save states, directory saves or shared containers. This release syncs "
                + $"the battery saves loose under saves/{system}/ (class {classes}) and nothing else.",
            files);
    }

    private int ForgetMissing(HashSet<RelativePath> seen)
    {
        // A save whose file is gone must stop blocking eviction, but only class A and B rows
        // are rescanned here, so nothing else is touched.
        var gone = _store.Saves
            .List()
            .Where(save => save.ShapeClass is SaveShapeClass.A or SaveShapeClass.B)
            .Where(save => !seen.Contains(save.Path))
            .Select(save => save.Path)
            .ToList();

        return gone.Count == 0 ? 0 : _store.Saves.Forget(gone);
    }

    private static int CountFiles(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Count();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    /// <summary>
    /// Accumulates unsyncable findings so each one is written once with a real count.
    /// </summary>
    /// <remarks>
    /// The table is keyed on (system, emulator, reason), so twelve unattributed saves in one
    /// system are one row saying twelve rather than twelve rows overwriting each other.
    /// </remarks>
    private sealed class UnsyncableReport
    {
        private readonly Dictionary<(string System, string Emulator, UnsyncableReason Reason), (string Detail, int Count)> _entries = [];

        public void Add(string system, string emulator, UnsyncableReason reason, string detail, int count)
        {
            var key = (system, emulator, reason);

            _entries[key] = _entries.TryGetValue(key, out var existing)

                // The first detail is kept, because it names the case rather than the last file
                // that happened to hit it.
                ? (existing.Detail, existing.Count + count)
                : (detail, count);
        }

        public int WriteTo(UnsyncableStore store, DateTimeOffset now)
        {
            foreach (var (key, value) in _entries)
            {
                store.Record(key.System, key.Emulator, key.Reason, value.Detail, value.Count, now);
            }

            return _entries.Count;
        }
    }
}
