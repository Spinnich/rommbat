using RomMBat.Core.Paths;
using RomMBat.Core.Store;

namespace RomMBat.Core.RetroBat;

/// <summary>One forced setting the file does not hold yet.</summary>
/// <param name="Key">The full system key, e.g. <c>ps2.pcsx2_slot1_memory</c>.</param>
/// <param name="Current">What the file holds now, or null when the key is absent.</param>
public sealed record ForcedSettingChange(string System, string Key, string? Current, string Value);

/// <summary>What one pass over the forced settings did.</summary>
/// <param name="Written">The changes now in the file.</param>
/// <param name="Problem">Why nothing was written, or null when nothing stood in the way.</param>
public sealed record ForcedSettingsResult(IReadOnlyList<ForcedSettingChange> Written, string? Problem);

/// <summary>
/// The forced settings <c>es_settings.cfg</c> does not hold yet, read and never written.
/// </summary>
/// <remarks>
/// Apart from <see cref="ForcedSettings"/> so the UI can say what the next quit will write with
/// no code path to the writer: it runs under a live EmulationStation, and
/// <c>EsSettingsBoundaryTests</c> asserts it never references one.
/// <para>
/// <b>Only for a system RomMBat holds a ROM in</b>, so an install that never syncs a PS2 game
/// keeps a file RomMBat never touched. A file that cannot be read counts as empty, so the
/// change is still named.
/// </para>
/// </remarks>
public static class PendingSettings
{
    /// <summary>Every forced setting the file does not hold, for a system with a ROM here.</summary>
    public static IReadOnlyList<ForcedSettingChange> Of(RetroBatInstall install, LocalStore store, SaveShapes? shapes = null)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(store);

        var forced = (shapes ?? SaveShapes.Bundled).WithForcedSettings.ToList();

        if (forced.Count == 0)
        {
            return [];
        }

        var folders = store.Files.List()
            .Where(file => file.Kind == LocalFileKind.Rom && file.Folder is not null)
            .Select(file => file.Folder!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var settings = Load(install);
        var changes = new List<ForcedSettingChange>();

        foreach (var shape in forced)
        {
            if (!folders.Contains(shape.System))
            {
                continue;
            }

            var key = EsSettingsFile.SystemKey(shape.System, shape.Forced!.Option);
            var current = settings?.Value(key);

            if (current != shape.Forced.Value)
            {
                changes.Add(new ForcedSettingChange(shape.System, key, current, shape.Forced.Value));
            }
        }

        return changes;
    }

    private static EsSettingsFile? Load(RetroBatInstall install)
    {
        var path = install.Resolve(EsSettingsFile.Location);

        try
        {
            return File.Exists(path) ? EsSettingsFile.Load(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            return null;
        }
    }
}

/// <summary>
/// Writes the system-level settings a save shape declares, so the emulator saves in the shape
/// RomMBat syncs.
/// </summary>
/// <remarks>
/// <b>Over any other value, and with no record of what was there.</b> A forced setting is not
/// the player's choice to make: <c>ps2</c>'s other values put every game on one card or a card
/// per disc, and neither can be synced per game. A per-game key outranks the system one and is
/// left alone, since it is a setting the player made for that game.
/// <para>
/// <b>Never under a running EmulationStation.</b> ES loads <c>es_settings.cfg</c> at startup
/// and writes that model back over the file, so a key written beside it is discarded without a
/// word (RB-178, RB-179). The caller is <c>background quit</c>, which waits for ES to exit first.
/// </para>
/// </remarks>
public sealed class ForcedSettings
{
    private readonly RetroBatInstall _install;
    private readonly LocalStore _store;
    private readonly SaveShapes _shapes;
    private readonly Func<EsRunningVerdict> _emulationStation;

    /// <param name="emulationStation">Injectable because the real check reads the process list.</param>
    public ForcedSettings(
        RetroBatInstall install,
        LocalStore store,
        SaveShapes? shapes = null,
        Func<EsRunningVerdict>? emulationStation = null)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(store);

        _install = install;
        _store = store;
        _shapes = shapes ?? SaveShapes.Bundled;
        _emulationStation = emulationStation ?? (() => EmulationStationProcess.Check(install));
    }

    /// <summary>What <see cref="Apply"/> would write.</summary>
    public IReadOnlyList<ForcedSettingChange> Pending() => PendingSettings.Of(_install, _store, _shapes);

    /// <summary>Writes every pending change, or none when EmulationStation is up.</summary>
    /// <remarks>
    /// Re-read after the write rather than trusted, because the one silent failure is an ES this
    /// did not detect writing over it, and that would leave the emulator on the shared card.
    /// </remarks>
    public ForcedSettingsResult Apply()
    {
        var pending = Pending();

        if (pending.Count == 0)
        {
            return new ForcedSettingsResult([], null);
        }

        if (_emulationStation() is { IsRunning: true } running)
        {
            return new ForcedSettingsResult([], running.Detail);
        }

        var path = _install.Resolve(EsSettingsFile.Location);
        var file = File.Exists(path) ? EsSettingsFile.Load(path) : EsSettingsFile.Empty;

        foreach (var change in pending)
        {
            file.Set(change.Key, change.Value);
        }

        file.WriteIfChanged(path);

        var after = EsSettingsFile.Load(path);
        var missing = pending.Where(change => after.Value(change.Key) != change.Value).ToList();

        return missing.Count == 0
            ? new ForcedSettingsResult(pending, null)
            : new ForcedSettingsResult(
                [.. pending.Except(missing)],
                $"{string.Join(", ", missing.Select(change => change.Key))} was written and is not in the "
                    + "file afterwards. Something else is writing es_settings.cfg, most likely a running "
                    + "EmulationStation.");
    }
}
