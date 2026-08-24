using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;

namespace RomMBat.Core.Content;

/// <summary>What a conversion request did, or why it did nothing.</summary>
public enum ConversionStatus
{
    /// <summary>Would convert. Nothing has been written.</summary>
    Ready,

    /// <summary>The setting was written and recorded.</summary>
    Converted,

    /// <summary>The setting was put back and the record dropped.</summary>
    Reverted,

    /// <summary>Already in the requested state, so nothing was written.</summary>
    NoChange,

    /// <summary>Not done, and <c>Reason</c> says why.</summary>
    Refused,
}

/// <summary>The answer to one conversion request.</summary>
/// <param name="Warning">
/// What the change leaves behind in the shared container, carried on every status the caller
/// prints: ahead of the preview on <c>Ready</c>, and again on <c>Converted</c> and
/// <c>Reverted</c>, where it describes what the write just did. Null when there is nothing to
/// warn about.
/// </param>
public sealed record ConversionResult(
    ConversionStatus Status,
    string Detail,
    string? Warning = null)
{
    public bool Ok => Status is not ConversionStatus.Refused;
}

/// <summary>
/// Opts one game into a per-game save container, and takes it back out.
/// </summary>
/// <remarks>
/// <b>This is the only thing in RomMBat that changes the user's RetroBat configuration</b>, and
/// being wrong here does not lose a save by mishandling it: it loses one by pointing an
/// emulator at a different container while the old one still holds the game. Every branch
/// refuses rather than guesses.
/// <para>
/// <b>The lever is <c>es_settings.cfg</c>, never an emulator INI.</b> <c>emulatorlauncher</c>
/// regenerates each emulator's config from ES options at every launch, so an INI edit is
/// silently undone on the next boot.
/// </para>
/// <para>
/// <b>Migrating what is already in the shared container is out of scope</b>, deliberately and
/// not by omission. Parsing a PS2 memory card is real format work that has to be scoped on its
/// own, so instead the user is told, before the switch, exactly what stays behind. On a real
/// install that container held saves for <b>11 distinct games</b>, so the warning is not
/// hypothetical.
/// </para>
/// </remarks>
public sealed class SaveConverter
{
    private readonly RetroBatInstall _install;
    private readonly LocalStore _store;
    private readonly SaveShapes _shapes;
    private readonly TimeProvider _time;

    public SaveConverter(
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

    /// <summary>Describes what converting this ROM would do, writing nothing.</summary>
    public ConversionResult Preview(int romId) => Run(romId, apply: false, revert: false);

    /// <summary>Converts, after every refusal has been checked.</summary>
    public ConversionResult Convert(int romId) => Run(romId, apply: true, revert: false);

    /// <summary>Puts the setting back to whatever the record says was there before.</summary>
    public ConversionResult Revert(int romId) => Run(romId, apply: true, revert: true);

    private ConversionResult Run(int romId, bool apply, bool revert)
    {
        if (Locate(romId) is not { } rom)
        {
            return Refuse($"no ROM with id {romId} is on this device, so there is nothing to convert.");
        }

        var shape = _shapes.For(rom.Folder);

        if (shape?.Conversion is not { } conversion)
        {
            return Refuse(
                $"{rom.Folder} declares no per-game save option, so there is no lever to pull. "
                    + "Its saves are handled as whatever class the shape definition names.");
        }

        // The declaration carries its own measured reason for not converting, so an
        // out-of-scope system is refused with that rather than with a generic message.
        if (!conversion.Apply || conversion.SetTo is null)
        {
            return Refuse(
                $"{rom.Folder} is deliberately left at its stock setting. "
                    + (conversion.Note.Length > 0 ? conversion.Note : "The measured answer is not to convert it."));
        }

        if (!conversion.YieldsRomNamedContainer)
        {
            return Refuse(
                $"converting {rom.Folder} produces a container keyed by {conversion.KeysBy} rather than "
                    + "by the ROM's filename, so it needs Game-ID attribution that this release does not "
                    + $"carry for it. {conversion.Note}".TrimEnd());
        }

        // Never convert a set. PCSX2 cannot bind discs, so each disc would get its own card and
        // the save would vanish at the disc change that the shared card carries through.
        if (DiscSet.Parse(rom.FsName) is { } marker)
        {
            var siblings = DiscSet.SiblingsOf(rom.FsName, NamesBeside(rom));
            var alsoHere = siblings.Count > 0
                ? $" Disc {marker.Number} of a set; this device also holds {siblings.Count} "
                    + $"other disc{(siblings.Count == 1 ? string.Empty : "s")} of it."
                : $" Disc {marker.Number} of a set, whose other discs are not on this device.";

            return Refuse(
                $"'{rom.FsName}' is one disc of a multi-disc game. PCSX2 cannot bind discs, so a per-game "
                    + "card would be created per disc and the save would be lost at the disc change that "
                    + "the shared card carries through." + alsoHere);
        }

        var key = EsSettingsFile.PerGameKey(rom.Folder, rom.FsName, conversion.Option);
        var path = _install.Resolve(EsSettingsFile.Location);
        var recorded = _store.SaveConversions.Find(rom.Folder, rom.FsName, conversion.Option);
        var file = EsSettingsFile.Load(path);
        var current = file.Value(key);

        if (revert)
        {
            return Revert(rom, conversion, key, path, file, current, recorded);
        }

        if (recorded is not null && current == conversion.SetTo)
        {
            return new ConversionResult(
                ConversionStatus.NoChange,
                $"'{rom.FsName}' is already using a per-game memory card.");
        }

        // Do not take over a setting somebody else made. Presence alone does not establish
        // that: ES adds keys on its own (finding 170), so the test is whether the value is one
        // RomMBat wrote. Anything else is the user's, or an ES default, and is left alone.
        if (current is not null && current != conversion.SetTo && recorded?.AppliedValue != current)
        {
            return Refuse(
                $"'{key}' is already set to '{current}' and RomMBat did not write it. Change or clear it "
                    + "yourself if you want RomMBat to manage this game's memory card.");
        }

        if (EmulationStationProcess.Check(_install) is { IsRunning: true } running)
        {
            return Refuse(
                $"{running.Detail} It rewrites es_settings.cfg from the copy it loaded at startup, so a "
                    + "change made now would be discarded without saying so. Quit EmulationStation and "
                    + "run this again.");
        }

        var warning = Warn(rom, conversion);

        if (!apply)
        {
            return new ConversionResult(
                ConversionStatus.Ready,
                $"would set {key} = {conversion.SetTo}", warning);
        }

        var prior = current is null ? PriorSettingState.Absent : PriorSettingState.Present;

        file.Set(key, conversion.SetTo);
        file.WriteIfChanged(path);

        // Re-read rather than trusting the rename. The one way this fails silently is an ES we
        // did not detect writing over us, and the cost of missing it is a game the user is told
        // is converted while the emulator carries on writing to the shared card.
        if (EsSettingsFile.Load(path).Value(key) != conversion.SetTo)
        {
            return Refuse(
                $"'{key}' was written and is not in the file afterwards. Something else is writing "
                    + "es_settings.cfg, most likely a running EmulationStation. Nothing was recorded.");
        }

        _store.SaveConversions.Record(new SaveConversion
        {
            RomId = rom.RomId,
            System = rom.Folder,
            FsName = rom.FsName,
            SettingKey = conversion.Option,
            AppliedValue = conversion.SetTo,
            PriorState = prior,
            PriorValue = current,
            ConvertedAtUtc = _time.GetUtcNow(),
        });

        return new ConversionResult(
            ConversionStatus.Converted,
            $"set {key} = {conversion.SetTo}", warning);
    }

    private ConversionResult Revert(
        LocatedRom rom,
        PerGameConversion conversion,
        string key,
        string path,
        EsSettingsFile file,
        string? current,
        SaveConversion? recorded)
    {
        if (recorded is null)
        {
            return Refuse(
                $"RomMBat has no record of converting '{rom.FsName}', so it does not know what to put "
                    + "back. Absence of the key is not the same as the stock value having been there, "
                    + "and guessing between them would leave the setting somewhere it never was.");
        }

        if (current is not null && current != recorded.AppliedValue)
        {
            return Refuse(
                $"'{key}' is now '{current}' and RomMBat wrote '{recorded.AppliedValue}', so somebody has "
                    + "changed it since. Reverting would discard that. Clear it yourself if that is what "
                    + "you want.");
        }

        if (EmulationStationProcess.Check(_install) is { IsRunning: true } running)
        {
            return Refuse($"{running.Detail} Quit EmulationStation and run this again.");
        }

        if (recorded.PriorState == PriorSettingState.Present)
        {
            file.Set(key, recorded.PriorValue!);
        }
        else
        {
            file.Remove(key);
        }

        file.WriteIfChanged(path);

        var after = EsSettingsFile.Load(path).Value(key);
        var expected = recorded.PriorState == PriorSettingState.Present ? recorded.PriorValue : null;

        if (after != expected)
        {
            return Refuse(
                $"'{key}' did not go back to its prior state, so the record is kept rather than dropped. "
                    + "Something else is writing es_settings.cfg.");
        }

        _store.SaveConversions.Forget(rom.Folder, rom.FsName, conversion.Option);

        var restored = recorded.PriorState == PriorSettingState.Present
            ? $"restored {key} = {recorded.PriorValue}"
            : $"removed {key}, which was not in the file before";

        return new ConversionResult(
            ConversionStatus.Reverted,
            restored,
            $"'{rom.FsName}' goes back to the shared memory card. Anything it saved while converted "
                + "stays in its own card, which RomMBat keeps syncing but the game will no longer read.");
    }

    /// <summary>
    /// What the user is told before the switch, because migration is out of scope.
    /// </summary>
    /// <remarks>
    /// Two costs, both measured rather than asserted. The stranded save is the one a user hits
    /// first and the cross-game read is the one they hit later and never connect to this.
    /// </remarks>
    private string Warn(LocatedRom rom, PerGameConversion conversion)
    {
        var shared = _shapes.SharedContainersFor(rom.Folder)
            .Select(entry => entry.Key)
            .Where(container => File.Exists(Path.Combine(_install.Resolve($"saves/{rom.Folder}"), container.Replace('/', Path.DirectorySeparatorChar))))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var where = shared.Count switch
        {
            0 => "the shared memory card",
            1 => $"the shared memory card ({shared[0]})",
            _ => $"the shared memory cards ({string.Join(", ", shared)})",
        };

        return $"'{rom.FsName}' will start from an empty memory card. Whatever it has already saved stays "
            + $"in {where}, where this game will no longer look for it, and RomMBat does not move it: "
            + "reading a memory card's format is work this release does not do. Undo with --revert. "
            + "Per-game cards also break games that deliberately read a prequel's save from the same card.";
    }

    /// <summary>Every filename in the ROM's own folder, for naming a set's other discs.</summary>
    private IEnumerable<string> NamesBeside(LocatedRom rom)
    {
        try
        {
            return Directory.EnumerateFiles(_install.Resolve($"roms/{rom.Folder}")).Select(Path.GetFileName).OfType<string>();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private LocatedRom? Locate(int romId)
    {
        using var command = _store.Connection
            .Command("SELECT folder, file_name FROM local_file WHERE rom_id = $romId AND kind = 'rom' LIMIT 1;")
            .With("$romId", romId);

        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new LocatedRom(romId, reader.GetString(0), reader.GetString(1))
            : null;
    }

    private static ConversionResult Refuse(string reason) => new(ConversionStatus.Refused, reason);

    private sealed record LocatedRom(int RomId, string Folder, string FsName);
}
