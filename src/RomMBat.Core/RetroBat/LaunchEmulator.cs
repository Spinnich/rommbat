using RomMBat.Core.Paths;
using RomMBat.Core.Store;

namespace RomMBat.Core.RetroBat;

/// <summary>
/// The emulator EmulationStation would launch a game with, as this install is configured now.
/// </summary>
/// <remarks>
/// <b>ES resolves this itself, from three places, the first that names one winning.</b> The
/// game's own <c>&lt;emulator&gt;</c> in <c>gamelist.xml</c>, which is where ES's game options
/// menu writes it; then <c>&lt;system&gt;.emulator</c> in <c>es_settings.cfg</c>; then the first
/// <c>&lt;emulator&gt;</c> the system lists in <c>es_systems.cfg</c>. A per-game
/// <c>&lt;system&gt;["&lt;rom&gt;"].emulator</c> in <c>es_settings.cfg</c> is never read, so it is
/// not asked (retrobat-layout, settings.md).
/// <para>
/// <b>Null means unknown, never "none".</b> A file that is missing or unreadable answers nothing
/// and the next one is asked, and a caller treats null as having no reason to hold anything
/// back. Each file is read once per instance, so one instance serves one flush.
/// </para>
/// </remarks>
public sealed class LaunchEmulator
{
    private readonly RetroBatInstall _install;
    private readonly Dictionary<string, GamelistDocument?> _gamelists = new(StringComparer.OrdinalIgnoreCase);
    private EsSettingsFile? _settings;
    private bool _settingsRead;
    private EsSystemsFile? _systems;
    private bool _systemsRead;

    public LaunchEmulator(RetroBatInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);
        _install = install;
    }

    /// <summary>The emulator for the game whose files these are, in its system folder, or null.</summary>
    public string? For(string folder, IEnumerable<LocalFile> romFiles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentNullException.ThrowIfNull(romFiles);

        if (Gamelist(folder) is { } gamelist)
        {
            foreach (var file in romFiles)
            {
                if (Named(gamelist.ValueOf(GamelistSync.EntryPathFor(file, folder), "emulator")) is { } chosen)
                {
                    return chosen;
                }
            }
        }

        var system = Systems()?.TryGetFolder(folder, out var declared) == true ? declared : null;

        // es_settings.cfg keys a system by its <name>, which differs from the folder on five
        // systems (RB-68), so the folder stands in only when es_systems.cfg could not be read.
        if (Named(Settings()?.Value(EsSettingsFile.SystemKey(system?.Name ?? folder, "emulator"))) is { } configured)
        {
            return configured;
        }

        return system?.Emulators.Count > 0 ? system.Emulators[0] : null;
    }

    private static string? Named(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private GamelistDocument? Gamelist(string folder)
    {
        if (!_gamelists.TryGetValue(folder, out var document))
        {
            try
            {
                document = GamelistDocument.Load(_install.Resolve(GamelistSync.PathFor(folder)));
            }
            catch (Exception ex) when (ex is GamelistParseException or IOException or UnauthorizedAccessException)
            {
                document = null;
            }

            _gamelists[folder] = document;
        }

        return document;
    }

    private EsSettingsFile? Settings()
    {
        if (!_settingsRead)
        {
            _settingsRead = true;

            try
            {
                _settings = EsSettingsFile.Load(_install.Resolve(EsSettingsFile.Location));
            }
            catch (Exception ex) when (ex is System.Xml.XmlException or IOException or UnauthorizedAccessException)
            {
                _settings = null;
            }
        }

        return _settings;
    }

    private EsSystemsFile? Systems()
    {
        if (!_systemsRead)
        {
            _systemsRead = true;

            try
            {
                _systems = EsSystemsFile.Load(_install);
            }
            catch (Exception ex) when (ex is EsSystemsException or IOException or UnauthorizedAccessException)
            {
                _systems = null;
            }
        }

        return _systems;
    }
}
