using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;

namespace RomMBat.Core.Content;

/// <summary>What a scan found.</summary>
public sealed record SaveScanOutcome
{
    /// <summary>Class A and B saves recorded, attributed or not.</summary>
    public int Found { get; init; }

    /// <summary>Class C save units recorded, attributed or not.</summary>
    public int Units { get; init; }

    /// <summary>Of those, the ones tied to a ROM and therefore uploadable.</summary>
    public int UnitsAttributed { get; init; }

    /// <summary>Of those, the ones tied to a ROM and therefore uploadable.</summary>
    public int Attributed { get; init; }

    /// <summary>Rows dropped because the file is gone.</summary>
    public int Forgotten { get; init; }

    /// <summary>Reasons written to the unsyncable report.</summary>
    public int Unsyncable { get; init; }

    /// <summary>Bytes hashed, which is what the pass cost.</summary>
    public long BytesHashed { get; init; }

    public string Summary =>
        Found == 0 && Units == 0 && Unsyncable == 0
            ? "saves: nothing found"
            : $"saves: {Attributed} of {Found} attributed"
                + (Units > 0 ? $", {UnitsAttributed} of {Units} directory saves attributed" : string.Empty)
                + $", {Unsyncable} reported unsyncable";
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
    /// <summary>
    /// Save-tree directory names RetroBat spells differently from its
    /// <c>es_savestates.cfg</c> name.
    /// </summary>
    /// <remarks>
    /// The file declares <c>name="dolphin"</c> with <c>&lt;directory&gt;{{system}}/dolphin</c>,
    /// and the save tree beside it is <c>dolphin-emu</c>: finding 740 lists
    /// <c>wii/dolphin-emu</c>, and <c>save_shapes.json</c> carries <c>dolphin-emu</c> for both
    /// the gamecube and wii <c>unit_paths</c>. Without this step the one emulator whose save
    /// states are measured working (finding 971, <c>.s01</c>, written live) would be reported
    /// under a row saying nothing here is restorable, on every install: <c>shared2/</c>,
    /// <c>sys/</c> and <c>title/00000001</c> are never units, so the wii count is never zero.
    /// <para>
    /// One entry, because it is the only divergence among the emulators any shape names.
    /// <c>mame</c>, <c>ppsspp</c> and <c>rpcs3</c> were checked against all 13 declared names.
    /// </para>
    /// </remarks>
    private static readonly Dictionary<string, string> DeclaredNames =
        new(StringComparer.OrdinalIgnoreCase) { ["dolphin-emu"] = "dolphin" };

    private readonly RetroBatInstall _install;
    private readonly LocalStore _store;
    private readonly SaveShapes _shapes;
    private readonly SaveStateSchema? _states;
    private readonly TimeProvider _time;
    private readonly SaveUnitScanner _units;

    /// <param name="states">
    /// The state schema, so save states are not reported as unsyncable while they are being
    /// synced. Null means no <c>es_savestates.cfg</c> was found, in which case nothing under a
    /// state directory is being synced either and counting it is correct.
    /// </param>
    public SaveScanner(
        RetroBatInstall install,
        LocalStore store,
        SaveShapes? shapes = null,
        SaveStateSchema? states = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(store);

        _install = install;
        _store = store;
        _shapes = shapes ?? SaveShapes.Bundled;
        _states = states;
        _time = timeProvider ?? TimeProvider.System;
        _units = new SaveUnitScanner(install, _shapes);
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
        var units = 0;
        var unitsAttributed = 0;
        var bytes = 0L;
        var seen = new HashSet<RelativePath>();
        var seenUnits = new HashSet<(RelativePath Container, string Key)>();

        // Accumulated rather than written per file, because the report is keyed on
        // (system, emulator, reason): writing each file as it is met would leave one row
        // naming the last file and counting one, which understates the gap it exists to show.
        var report = new UnsyncableReport();

        ReportDolphinSaveSync(report);

        // (folder, ROM basename) to (rom_id, path), which is the whole of class A and B
        // attribution: the save is named after the ROM file, inside its system's folder.
        // Built once rather than queried per save.
        var romsByStem = RomIndex.Build(_store);
        // Built once for the whole pass rather than per system: the launch log is read once and
        // the ROM-header index is built per system on first use inside it.
        var launches = GameIdAttributor.ReadLaunches(_install);
        var attributor = new GameIdAttributor(_install, _store, romsByStem, launches, _time);
        var titles = new DisplayNameAttributor(_store, romsByStem, launches, _time);

        foreach (var systemDirectory in Directory.EnumerateDirectories(savesRoot).Order(StringComparer.Ordinal))
        {
            var system = Path.GetFileName(systemDirectory);
            var shape = _shapes.For(system);

            if (shape is null)
            {
                // Nine top-level directories on a real install are not declared systems at
                // all, and 21 declared ones carry no shape. Both land here, and neither is
                // touched: an unknown tree is not a tree to start writing into.
                //
                // Save states under such a system are the exception, and they are excluded
                // from the count for the same reason they are excluded below: state discovery
                // is driven by es_savestates.cfg rather than by the save shapes, so a system
                // with no shape at all still has its states synced. Measured on a real
                // install: saves/ports/ holds a libretro state and its screenshot beside one
                // battery save, and counting all three said three files were being ignored
                // while two of them were going up.
                var files = CountFiles(systemDirectory, savesRoot, []);
                if (files > 0)
                {
                    report.Add(
                        system,
                        string.Empty,
                        UnsyncableReason.UnknownShape,
                        $"no shape definition covers saves/{system}/, so nothing under it is read "
                            + "except any save states, which are found from es_savestates.cfg instead",
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
                    report.Add(system, _shapes.LooseEmulator, UnsyncableReason.SharedContainer, container, 1, Named(file));
                    continue;
                }

                if (_shapes.BatteryRuleFor(system, string.Empty, extension) is not { } rule)
                {
                    // No emulator, because no rule says whose it is: measured on nes, a loose .sav
                    // was mesen standalone's and another mednafen's, and libretro's rule does not
                    // claim .sav there (#152).
                    report.Add(
                        system,
                        string.Empty,
                        UnsyncableReason.UnknownShape,
                        $"{extension} is not an extension RomMBat recognises as a save",
                        1,
                        Named(file));
                    continue;
                }

                var save = Describe(system, file, rule, romsByStem);
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
                        1,
                        save.Path.Value);
                }
            }

            // Class C, before the subdirectory report, because the report has to know which
            // files this pass is carrying. Stage 2a shipped exactly this bug for save states:
            // the report counted them as unsyncable in the same pass that uploaded them.
            var carried = ScanUnits(system, attributor, report, seenUnits, now, ref units, ref unitsAttributed, ref bytes);

            // An emulator's own battery saves, before the subdirectory report for the same reason.
            carried.UnionWith(ScanBelow(system, systemDirectory, romsByStem, titles, report, seen, now, ref found, ref attributed, ref bytes));

            // A converted class D container, before both reports, for the reason the class C
            // pass runs first: a file this pass carries must not also be counted as one nothing
            // carries. Stage 2a shipped exactly that bug for save states.
            var converted = ScanConverted(system, shape, romsByStem, report, seen, now, ref found, ref attributed, ref bytes);

            // Shared containers below the loose level, also before the subdirectory report, so
            // a declared container is named as one rather than counted as an unread file.
            var shared = AddSharedContainers(report, system, systemDirectory);
            shared.UnionWith(converted);

            // Every remaining subdirectory of a system folder is class D or a save state.
            AddSubdirectories(report, system, shape, systemDirectory, savesRoot, carried, shared);
        }

        var forgotten = ForgetMissing(seen, seenUnits);
        var unsyncable = report.WriteTo(_store.Unsyncable, now);

        // Anything not re-observed in this pass is no longer true, so a system that becomes
        // syncable stops being listed and the count stays an honest measure of coverage.
        _store.Unsyncable.ForgetOlderThan(now);

        return new SaveScanOutcome
        {
            Found = found,
            Attributed = attributed,
            Units = units,
            UnitsAttributed = unitsAttributed,
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

    private LocalSave? Describe(string system, string file, BatteryRule rule, RomIndex romsByStem, Attribution? attribution = null)
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
        var shapeClass = rule.Class ?? _shapes.For(system)?.Classes.FirstOrDefault(value =>
            value is SaveShapeClass.A or SaveShapeClass.B) ?? SaveShapeClass.A;

        // Keyed on the folder the save was found under, so a save only ever matches a ROM in
        // its own system. A display-name save arrives already attributed, or not at all.
        var rom = attribution is null
            ? romsByStem.Find(system, stem)
            : attribution is { RomId: { } romId, RomPath: { } romPath } ? (romId, romPath) : null;

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
            Emulator = rule.Emulator,
            ShapeClass = shapeClass,
            Slot = SlotFor(rule.Emulator, shapeClass, extension),
            RomId = rom?.RomId,
            RomPath = rom?.Path,
            ContentHash = hash,
            SizeBytes = info.Length,
            FileMtimeUtc = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
        };
    }

    /// <summary>
    /// Records the battery saves an emulator keeps in its own subdirectory of a system folder.
    /// </summary>
    /// <remarks>
    /// <b>One directory level, from the rule, and never discovered.</b> BizHawk keeps its states
    /// two levels further down in <c>sstates/&lt;core&gt;/</c>, which the state pass owns, so the
    /// rule's directory is listed and nothing below it.
    /// <para>
    /// <b>A display-name save is attributed by what was learned, never by its stem.</b>
    /// <c>StarTropics.SaveRAM</c> belongs to <c>StarTropics (USA).zip</c>, and a stem lookup
    /// would find nothing or, worse, an untagged ROM that happens to share the title.
    /// </para>
    /// </remarks>
    /// <returns>
    /// Every file recorded, and every file the rule says is not a save, so the subdirectory report
    /// does not count either as unread.
    /// </returns>
    private HashSet<RelativePath> ScanBelow(
        string system,
        string systemDirectory,
        RomIndex romsByStem,
        DisplayNameAttributor titles,
        UnsyncableReport report,
        HashSet<RelativePath> seen,
        DateTimeOffset now,
        ref int found,
        ref int attributed,
        ref long bytes)
    {
        var carried = new HashSet<RelativePath>();

        // What each file held when it was last attributed, so a file nobody has written to since
        // does not have its mtime read as a fresh write.
        var known = _store.Saves
            .List()
            .Where(save => save.UnitKey.Length == 0
                && string.Equals(save.System, system, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(save => save.Path);

        foreach (var rule in _shapes.BatteryRulesBelow(system))
        {
            var directory = Path.Combine(systemDirectory, rule.Directory.Replace('/', Path.DirectorySeparatorChar));

            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(directory).Order(StringComparer.Ordinal))
            {
                if (!_install.Contains(file))
                {
                    continue;
                }

                var extension = Path.GetExtension(file).ToLowerInvariant();

                // Not synced and not unsyncable either, so it is left out of the count the
                // subdirectory report makes, the way a loose .txt or .png is.
                if (rule.NotASave.Contains(extension))
                {
                    carried.Add(_install.Relativize(file));
                    continue;
                }

                if (!rule.Carries(extension))
                {
                    continue;
                }

                // A restore records the bytes it wrote with their ROM, and writes them now, so
                // the newest launch before that mtime is a session that never touched them.
                // Measured on a real install, where it contested every restored BizHawk save.
                var unchanged = known.TryGetValue(_install.Relativize(file), out var previous)
                    && previous.RomId is not null
                    && previous.ContentHash is { } previousHash
                    && string.Equals(previousHash, TryHash(file), StringComparison.OrdinalIgnoreCase);

                var attribution = rule.NamedAfter == BatteryNaming.DisplayName
                    ? titles.Attribute(
                        system,
                        rule,
                        Path.GetFileName(file),
                        unchanged ? null : new DateTimeOffset(File.GetLastWriteTimeUtc(file), TimeSpan.Zero))
                    : null;

                if (Describe(system, file, rule, romsByStem, attribution) is not { } save)
                {
                    continue;
                }

                _store.Saves.Record(save, now);
                seen.Add(save.Path);
                carried.Add(save.Path);
                found++;
                bytes += save.SizeBytes;

                if (save.RomId is not null)
                {
                    attributed++;
                    continue;
                }

                report.Add(
                    system,
                    save.Emulator,
                    UnsyncableReason.Unattributed,
                    attribution?.Detail ?? "matches no ROM this device holds, so there is no game to upload it against",
                    1,
                    save.Path.Value);
            }
        }

        return carried;
    }

    private static string? TryHash(string file)
    {
        try
        {
            return LogicalContentHash.OfFile(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Records every class C unit under one system, and reports the ones nothing could attribute.
    /// </summary>
    /// <remarks>
    /// <b>The unit is scoped by the shape definition and never discovered.</b> Hashing an
    /// emulator's data root takes 426.07 s on a real install against 0.06 s for the savedata
    /// subtree, so a container is expanded from what was declared and nothing else is read.
    /// <para>
    /// <b>An unattributed unit is recorded and never uploaded.</b> It still needs a row, because
    /// that row is what stops eviction taking the ROM out from under a save that has not gone
    /// up; the row simply has no <c>rom_id</c> and the reason is in the report.
    /// </para>
    /// </remarks>
    /// <returns>Every file this pass is carrying, so the subdirectory report can exclude them.</returns>
    private HashSet<RelativePath> ScanUnits(
        string system,
        GameIdAttributor attributor,
        UnsyncableReport report,
        HashSet<(RelativePath Container, string Key)> seenUnits,
        DateTimeOffset now,
        ref int units,
        ref int attributed,
        ref long bytes)
    {
        var carried = new HashSet<RelativePath>();

        foreach (var unit in _units.Scan(system))
        {
            var attribution = attributor.Attribute(unit);

            string? hash = null;

            try
            {
                hash = SaveArchive.HashOf(_install, unit);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Held open by a running emulator. Recorded without a hash, which reads as
                // unsent and therefore blocks eviction, which is the fail-closed direction.
            }

            _store.Saves.Record(
                new LocalSave
                {
                    Path = unit.Container,
                    UnitKey = unit.Key,
                    System = unit.System,
                    Emulator = unit.Emulator,
                    ShapeClass = SaveShapeClass.C,
                    Slot = $"{unit.Emulator}:{unit.Slot}",
                    RomId = attribution.RomId,
                    RomPath = attribution.RomPath,
                    ContentHash = hash,
                    SizeBytes = unit.SizeBytes,
                    FileMtimeUtc = unit.NewestMtimeUtc,
                },
                now);

            seenUnits.Add((unit.Container, unit.Key));
            units++;
            bytes += unit.SizeBytes;

            foreach (var file in unit.Files)
            {
                carried.Add(file.Path);
            }

            if (attribution.IsResolved)
            {
                attributed++;
            }
            else
            {
                report.Add(
                    system,
                    unit.Emulator,
                    UnsyncableReason.Unattributed,
                    attribution.Detail,
                    unit.Files.Count,

                    // The unit, not its container: every unit on a system shares the container,
                    // so the report named saves/mame/nvram once per unit and told nobody which.
                    $"{unit.Container.Value}/{unit.Key}");
            }
        }

        return carried;
    }


    /// <summary>
    /// Records the per-game containers a class D conversion produced.
    /// </summary>
    /// <remarks>
    /// <b>Driven end to end before this was written.</b> Setting
    /// <c>ps2["Armored Core 3 (USA).chd"].pcsx2_slot1_memory=game</c> and launching the game
    /// produced <c>saves/ps2/pcsx2/memcards/Armored Core 3 (USA).ps2</c> holding exactly one
    /// game's saves, where the shared <c>Mcd001.ps2</c> beside it holds eleven.
    /// <para>
    /// <b>The card is named from the ROM's stem, and the extension is replaced rather than
    /// appended.</b> So the name is exactly the <c>(folder, stem)</c> key class A attribution
    /// already uses and no new route is needed, which is why PS2 converts into something
    /// syncable while Dreamcast does not. Note the asymmetry with the setting that caused it:
    /// the <c>es_settings.cfg</c> key must carry <c>.chd</c> or it is ignored silently, while
    /// the card it produces drops it.
    /// </para>
    /// <para>
    /// <b>Recorded as class D, not class A.</b> The file is one-per-game in shape, but what it
    /// is is a class D system whose container was made per-game, and the row is the only place
    /// that stays true. Discovery does not consult the conversion record: a card named after a
    /// ROM in the declared container is that ROM's save whether RomMBat set the option or the
    /// user did, and keying on our own record would miss the second case.
    /// </para>
    /// <para>
    /// The container is <b>declared and never discovered</b>, and a shared container inside it
    /// is skipped by name, so <c>Mcd001.ps2</c> and <c>Mcd002.ps2</c> stay reported as shared
    /// rather than being mistaken for ROMs whose stems happen not to resolve.
    /// </para>
    /// </remarks>
    /// <returns>The absolute paths carried, so neither report counts them again.</returns>
    private HashSet<string> ScanConverted(
        string system,
        SaveShape shape,
        RomIndex romsByStem,
        UnsyncableReport report,
        HashSet<RelativePath> seen,
        DateTimeOffset now,
        ref int found,
        ref int attributed,
        ref long bytes)
    {
        var carried = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (shape.Conversion is not { IsDiscoverable: true } conversion)
        {
            return carried;
        }

        var container = Path.Combine(
            _install.Resolve(SavesDirectory.Combine(system)),
            conversion.Container!.Replace('/', Path.DirectorySeparatorChar));

        if (!Directory.Exists(container))
        {
            return carried;
        }

        foreach (var file in Directory.EnumerateFiles(container).Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(file);

            // A shared container sitting in the same directory is the ordinary case, not the
            // odd one: the stock cards stay beside every card a conversion creates.
            if (_shapes.SharedContainerReason(system, $"{conversion.Container}/{name}") is not null)
            {
                continue;
            }

            if (!string.Equals(Path.GetExtension(name), conversion.Extension, StringComparison.OrdinalIgnoreCase)
                || !_install.Contains(file))
            {
                continue;
            }

            var rom = romsByStem.Find(system, Path.GetFileNameWithoutExtension(name));
            var info = new FileInfo(file);

            string? hash = null;
            try
            {
                hash = LogicalContentHash.OfFile(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Held open by a running emulator. No hash reads as unsent, which blocks
                // eviction, which is the fail-closed direction.
            }

            var path = _install.Relativize(file);

            _store.Saves.Record(
                new LocalSave
                {
                    Path = path,
                    System = system,
                    Emulator = conversion.Emulator!,
                    ShapeClass = SaveShapeClass.D,
                    Slot = $"{conversion.Emulator}:{conversion.Slot}",
                    RomId = rom?.RomId,
                    RomPath = rom?.Path,
                    ContentHash = hash,
                    SizeBytes = info.Length,
                    FileMtimeUtc = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
                },
                now);

            seen.Add(path);
            carried.Add(file);
            found++;
            bytes += info.Length;

            if (rom is not null)
            {
                attributed++;
            }
            else
            {
                report.Add(
                    system,
                    conversion.Emulator!,
                    UnsyncableReason.Unattributed,
                    $"'{name}' is a per-game container naming no ROM this device holds, so there is "
                        + "no game to upload it against",
                    1,
                    path.Value);
            }
        }

        return carried;
    }

    /// <summary>
    /// Reports the declared shared containers this install actually holds.
    /// </summary>
    /// <remarks>
    /// <b>Seven of the ten declared containers were unreachable before this.</b>
    /// <c>SharedContainerReason</c>'s only caller asked it with a bare loose filename, and seven
    /// declarations name a path with a separator (<c>pcsx2/memcards/Mcd001.ps2</c>, the four
    /// Dreamcast VMUs, Kronos's backup RAM). A test asserted the lookup table answered for
    /// <c>ps2/pcsx2/memcards/Mcd001.ps2</c> and passed, because it called the table rather than
    /// the scanner: the shared PS2 memory cards were being counted as part of an unread
    /// subdirectory instead of named as the shared cards they are.
    /// <para>
    /// <b>Nothing here opens a file.</b> <c>xbox</c>'s <c>xbox_hdd.qcow2</c> is 39 MB of the
    /// 43 MB the whole loose-file workload reads, and it is a declared container, so the one
    /// method that goes looking for containers by name is exactly where that must stay true.
    /// Existence and a name, never a handle.
    /// </para>
    /// <para>
    /// The emulator is left empty rather than read off the first path segment. The path is
    /// declared, but the declaration does not say whose it is, and the second level of the save
    /// tree is not an emulator: <c>mame/artwork</c>, <c>n64/sram</c> and <c>psp/SYSTEM</c> name
    /// none. Inventing one from a path is the positional read this scanner exists to avoid.
    /// </para>
    /// </remarks>
    /// <returns>The absolute paths reported, so the subdirectory sweep does not count them twice.</returns>
    private HashSet<string> AddSharedContainers(UnsyncableReport report, string system, string systemDirectory)
    {
        var reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (relative, reason) in _shapes.SharedContainersFor(system))
        {
            // Declared with forward slashes, and only ever below the loose level here: a
            // container sitting loose is already named by the file loop above.
            if (!relative.Contains('/', StringComparison.Ordinal))
            {
                continue;
            }

            var path = Path.Combine(systemDirectory, relative.Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(path))
            {
                report.Add(system, string.Empty, UnsyncableReason.SharedContainer, reason, 1);
                reported.Add(path);
                continue;
            }

            if (!Directory.Exists(path))
            {
                continue;
            }

            // A container declared as a directory has not appeared yet, and treating one as
            // absent would report its contents as an unread subdirectory instead. Names only.
            var members = SafeEnumerateFiles(path);
            if (members.Count > 0)
            {
                report.Add(system, string.Empty, UnsyncableReason.SharedContainer, reason, members.Count);
                reported.UnionWith(members);
            }
        }

        return reported;
    }

    /// <summary>
    /// Names RetroBat's own GameCube save reconciliation, when it is running or has run.
    /// </summary>
    /// <remarks>
    /// Outside the per-system loop because it is true of the install rather than of a directory
    /// walk, and because the option is worth reporting before <c>saves/gamecube/</c> exists at
    /// all: a user who turns it on and then syncs a GameCube game should be told before the
    /// first launch makes the copies, not after.
    /// <para>
    /// A missing or unreadable es_settings.cfg is treated as the option being off, and the tree
    /// is still walked. That is the fail-closed direction here: the warning is about files that
    /// exist, and they exist whether or not the setting can be read.
    /// </para>
    /// </remarks>
    private void ReportDolphinSaveSync(UnsyncableReport report)
    {
        EsSettingsFile? settings = null;

        try
        {
            var path = _install.Resolve(EsSettingsFile.Location);

            if (File.Exists(path))
            {
                settings = EsSettingsFile.Load(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            // Reported as off. Nothing here acts on the answer, so a missed warning is the
            // whole cost of being wrong.
        }

        var state = DolphinSaveSync.Inspect(_install, settings);

        if (!state.WorthReporting)
        {
            return;
        }

        report.Add(
            DolphinSaveSync.System,
            "dolphin-emu",
            UnsyncableReason.ManagedElsewhere,
            DolphinSaveSync.Describe(state),
            state.CopiedFiles);
    }

    private static List<string> SafeEnumerateFiles(string directory)
    {
        try
        {
            return [.. Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// Reports the emulator subdirectories whose contents this build does not carry.
    /// </summary>
    /// <remarks>
    /// <b>Files inside a declared save-state directory are excluded, because they are synced.</b>
    /// Without that this report would count a state as unsyncable in the same pass that uploads
    /// it, which is worse than saying nothing: a user checking why their states are not going up
    /// would be told they are not, while they were.
    /// <para>
    /// The exclusion asks the schema rather than matching directory names, so the two passes
    /// cannot disagree about what a state directory is.
    /// </para>
    /// <para>
    /// <b>Two rows, split on whether <c>es_savestates.cfg</c> names the emulator at all.</b> One
    /// row said "this release syncs the save states beside them" over both halves, and for an
    /// emulator with no entry that is false: <c>mednafen</c>, <c>mesen</c> and <c>ares</c> each
    /// wrote a real save state into a directory they name themselves, and those files landed in
    /// a count whose reason is about battery saves and shared containers. Telling someone their
    /// states sync while counting them as unsyncable is the earlier defect in the other
    /// direction. See #150.
    /// </para>
    /// </remarks>
    private void AddSubdirectories(
        UnsyncableReport report,
        string system,
        SaveShape shape,
        string systemDirectory,
        string savesRoot,
        HashSet<RelativePath> carried,
        HashSet<string> shared)
    {
        // Named only where something in them is genuinely not carried, so a system whose only
        // subdirectory is a state directory, or holds nothing but a container already named as
        // shared, is not listed at all.
        var declared = new List<(string Directory, int Files)>();
        var undeclared = new List<(string Directory, int Files)>();

        foreach (var directory in Directory.EnumerateDirectories(systemDirectory))
        {
            var count = CountFiles(directory, savesRoot, carried, shared);

            if (count > 0)
            {
                (DeclaresStates(directory) ? declared : undeclared).Add((directory, count));
            }
        }

        AddDeclaredSubdirectories(report, system, shape, declared);
        AddUndeclaredSubdirectories(report, system, shape, undeclared);
    }

    /// <summary>Reports subdirectories of emulators <c>es_savestates.cfg</c> does declare.</summary>
    private static void AddDeclaredSubdirectories(
        UnsyncableReport report,
        string system,
        SaveShape shape,
        List<(string Directory, int Files)> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        // The system's own class is named because a reader will ask why a class A system has
        // anything unsyncable at all. The answer is that the class describes its battery
        // saves, which are the loose files, and these subdirectories are the emulators' own
        // trees: memory cards and directory saves, which this release does not carry.
        var synced = shape.HasSyncableClass
            ? $"the battery saves loose under saves/{system}/ (class {Classes(shape)}), the save "
                + "states beside them, and the directory saves the shape definition names"
            : "the save states es_savestates.cfg declares and the directory saves the shape "
                + "definition names";

        report.Add(
            system,
            string.Empty,
            UnsyncableReason.NotInThisVersion,
            $"{NameList(entries)} hold shared containers or a shape no declaration covers. This "
                + $"release syncs {synced}.",
            entries.Sum(entry => entry.Files));
    }

    /// <summary>
    /// Reports subdirectories no <c>es_savestates.cfg</c> entry claims.
    /// </summary>
    /// <remarks>
    /// <b>It carries the shape half too, because the same directory holds both.</b>
    /// <c>saves/nes/ares/Famicom/</c> holds a battery save this release defers and a save state
    /// nothing reads, and splitting those into two rows would count one file twice under two
    /// explanations. So this row says everything the declared row says except the one clause
    /// that is false here, and adds why.
    /// <para>
    /// <b>Hedged on the mirror, because nothing here can settle it.</b> PPSSPP writes
    /// <c>psp/PPSSPP_STATE/</c>, which no entry declares, and RetroBat mirrors each state into
    /// the declared <c>psp/ppsspp/</c> while the game is still running, so a flat "nothing here
    /// is synced" is false there and true for <c>mednafen</c>. With no declaration nothing
    /// distinguishes the two, so the row names the mechanism that decides it instead.
    /// </para>
    /// </remarks>
    private static void AddUndeclaredSubdirectories(
        UnsyncableReport report,
        string system,
        SaveShape shape,
        List<(string Directory, int Files)> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        var synced = shape.HasSyncableClass
            ? $"the battery saves loose under saves/{system}/ (class {Classes(shape)}) and the "
                + "directory saves the shape definition names"
            : "the directory saves the shape definition names";

        report.Add(
            system,
            string.Empty,
            UnsyncableReason.NoStateDeclaration,
            $"{NameList(entries)} hold shared containers or a shape no declaration covers, and no "
                + "es_savestates.cfg entry declares a save-state directory under them either. This "
                + $"release syncs {synced}. A save state written under these is found only where "
                + "RetroBat mirrors it into a declared path, and is otherwise not scanned, not "
                + "uploaded and not restorable.",
            entries.Sum(entry => entry.Files));
    }

    /// <summary>
    /// The system's declared classes, named only where a loose battery save can exist.
    /// </summary>
    /// <remarks>
    /// <c>gamecube</c> and <c>wii</c> are class C with no class A or B at all, so
    /// "the battery saves loose under saves/wii/" describes nothing that is there. The clause
    /// is dropped rather than reworded, which is why this is only reached behind
    /// <see cref="SaveShape.HasSyncableClass"/>.
    /// </remarks>
    private static string Classes(SaveShape shape) =>
        string.Concat(shape.Classes.Select(value => value.ToString()));

    private static string NameList(List<(string Directory, int Files)> entries) =>
        string.Join(
            ", ",
            entries.Select(entry => Path.GetFileName(entry.Directory)).Order(StringComparer.Ordinal));

    /// <summary>
    /// Whether <c>es_savestates.cfg</c> names the emulator this directory is called after.
    /// </summary>
    /// <remarks>
    /// The emulator rather than the directory, because the declared template usually sits below
    /// this level: BizHawk declares <c>{{system}}/bizhawk/sstates/{{core}}</c>, so
    /// <c>saves/nes/bizhawk/</c> itself matches no state directory while the emulator is very
    /// much declared. Asking <see cref="SaveStateSchema.MatchDirectory"/> here would put every
    /// such emulator in the undeclared half.
    /// <para>
    /// The directory name goes through <see cref="DeclaredNames"/> first, because RetroBat does
    /// not spell an emulator the same way in both places.
    /// </para>
    /// <para>
    /// <b>No schema is not an absent declaration.</b> The undeclared row's claim is that the
    /// file was read and does not name this emulator, and an install with no
    /// <c>es_savestates.cfg</c> supports neither half of that. Everything stays in the declared
    /// row there, which is what this reported before the split.
    /// </para>
    /// </remarks>
    private bool DeclaresStates(string directory)
    {
        if (_states is null)
        {
            return true;
        }

        var name = Path.GetFileName(directory);

        return _states.For(DeclaredNames.GetValueOrDefault(name, name)) is not null;
    }

    /// <summary>
    /// Drops rows for saves that are no longer on disk, so a deleted save stops blocking eviction.
    /// </summary>
    /// <remarks>
    /// Class C is matched on the whole (container, key) pair rather than on the path. A class C
    /// container outlives every unit in it, so forgetting by path would drop every PSP save on
    /// the install the first time one game's savedata was deleted, and eviction would then take
    /// ROMs whose saves had never gone up.
    /// </remarks>
    private int ForgetMissing(
        HashSet<RelativePath> seen,
        HashSet<(RelativePath Container, string Key)> seenUnits)
    {
        var gone = _store.Saves
            .List()
            .Where(save => save.ShapeClass switch
            {
                SaveShapeClass.A or SaveShapeClass.B => !seen.Contains(save.Path),
                SaveShapeClass.C => !seenUnits.Contains((save.Path, save.UnitKey)),

                // Class D is one file at one path, like class A, because the only class D rows
                // that exist are the per-game containers a conversion produced. It has to be
                // forgettable: leaving the row behind when the card is deleted would block
                // eviction for that ROM forever, which is the third time this scanner has had to
                // learn that a stale row is worse than no row.
                SaveShapeClass.D => !seen.Contains(save.Path),

                _ => false,
            })
            .Select(save => (save.Path, save.UnitKey))
            .ToList();

        return gone.Count == 0 ? 0 : _store.Saves.Forget(gone);
    }

    /// <summary>
    /// Counts files that no other pass is carrying.
    /// </summary>
    /// <remarks>
    /// <b>Three exclusions, and the first two exist because of the same bug shipped once
    /// already.</b> Stage 2a's report counted a save state as unsyncable in the very pass that
    /// uploaded it, which is worse than saying nothing: a user checking why their states were
    /// not going up was told they were not, while they were. Class C would reintroduce it
    /// exactly, so its members are excluded too, and the state exclusion asks the schema rather
    /// than matching directory names so the two passes cannot disagree about what a state
    /// directory is.
    /// <para>
    /// The third is not that bug. A declared shared container below the loose level is already
    /// reported by name with its own reason, so counting it again here would say the same file
    /// is unsyncable twice under two different explanations.
    /// </para>
    /// </remarks>
    private int CountFiles(
        string directory,
        string savesRoot,
        HashSet<RelativePath> carried,
        HashSet<string>? shared = null)
    {
        try
        {
            return Directory
                .EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Count(file =>
                    !IsStateDirectory(Path.GetDirectoryName(file), savesRoot)
                    && shared?.Contains(file) != true
                    && !(_install.Contains(file) && carried.Contains(_install.Relativize(file))));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private bool IsStateDirectory(string? directory, string savesRoot)
    {
        if (directory is null || _states is null)
        {
            return false;
        }

        var relative = Path.GetRelativePath(savesRoot, directory).Replace('\\', '/');
        return _states.MatchDirectory(relative) is not null;
    }

    /// <summary>A file's path from the install root, as the report names it.</summary>
    private string Named(string absolutePath) => _install.Relativize(absolutePath).Value;

    /// <summary>
    /// Accumulates unsyncable findings so each one is written once with a real count.
    /// </summary>
    /// <remarks>
    /// The table is keyed on (system, emulator, reason), so twelve unattributed saves in one
    /// system are one row saying twelve rather than twelve rows overwriting each other.
    /// <para>
    /// <b>A row found one file at a time names its files.</b> A count and a reason cannot be
    /// acted on: a hands-on pass had to read the code to learn which two files "shape not
    /// recognised" meant (#152). The first few go into the detail, so the report needs no
    /// column and a row covering a thousand files stays one readable line.
    /// </para>
    /// </remarks>
    private sealed class UnsyncableReport
    {
        private const int NamedFiles = 5;

        private readonly Dictionary<(string System, string Emulator, UnsyncableReason Reason), Finding> _entries = [];

        public void Add(string system, string emulator, UnsyncableReason reason, string detail, int count, string? file = null)
        {
            var key = (system, emulator, reason);

            // The first detail is kept, because it names the case rather than the last file
            // that happened to hit it.
            if (!_entries.TryGetValue(key, out var finding))
            {
                finding = new Finding(detail);
                _entries[key] = finding;
            }

            finding.Count += count;

            if (file is not null)
            {
                finding.Files.Add(file);
            }
        }

        public int WriteTo(UnsyncableStore store, DateTimeOffset now)
        {
            foreach (var (key, finding) in _entries)
            {
                store.Record(key.System, key.Emulator, key.Reason, finding.Describe(), finding.Count, now);
            }

            return _entries.Count;
        }

        private sealed class Finding(string detail)
        {
            public int Count { get; set; }

            public List<string> Files { get; } = [];

            public string Describe()
            {
                if (Files.Count == 0)
                {
                    return detail;
                }

                var named = string.Join(", ", Files.Take(NamedFiles));
                var more = Files.Count > NamedFiles ? $", and {Files.Count - NamedFiles} more" : string.Empty;

                return $"{detail}. Files: {named}{more}";
            }
        }
    }
}
