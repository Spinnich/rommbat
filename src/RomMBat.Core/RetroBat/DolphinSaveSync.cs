using RomMBat.Core.Paths;

namespace RomMBat.Core.RetroBat;

/// <summary>Where RetroBat's GameCube save reconciliation was switched on, if it was.</summary>
/// <remarks>
/// The three levels are es_settings.cfg's own precedence, most specific first. Reported rather
/// than collapsed to a boolean because "you turned this on for one game" and "this is on for
/// everything" are different things to tell somebody.
/// </remarks>
public enum DolphinSyncScope
{
    /// <summary>The key is absent everywhere, which RetroBat reads as off.</summary>
    Off,

    /// <summary><c>global.dolphin_sync_saves</c>.</summary>
    Global,

    /// <summary><c>gamecube.dolphin_sync_saves</c>.</summary>
    System,

    /// <summary><c>gamecube["&lt;rom&gt;"].dolphin_sync_saves</c>.</summary>
    PerGame,
}

/// <summary>One region directory that holds a second copy of its saves.</summary>
/// <param name="CardA">The <c>Card A</c> directory, relative to the RetroBat root.</param>
/// <param name="Mirrored">Files in it that also exist beside it, so a copy of a live save.</param>
/// <param name="OnlyInCardA">
/// Files in it that do <b>not</b> exist beside it. These are the ones that matter: the next
/// launch copies each of them back out, whatever RomMBat did to the original.
/// </param>
public sealed record DolphinCardA(RelativePath CardA, int Mirrored, int OnlyInCardA);

/// <summary>What <c>dolphin_sync_saves</c> is doing to this install.</summary>
public sealed record DolphinSyncState(
    DolphinSyncScope Scope,
    string? SetAt,
    IReadOnlyList<DolphinCardA> Directories)
{
    /// <summary>True when RetroBat will reconcile GameCube saves on the next launch.</summary>
    public bool Enabled => Scope is not DolphinSyncScope.Off;

    /// <summary>Files sitting in a <c>Card A</c> that RomMBat neither reads nor uploads.</summary>
    public int CopiedFiles => Directories.Sum(entry => entry.Mirrored + entry.OnlyInCardA);

    /// <summary>Files a launch would put back into the region root.</summary>
    public int RestorableFiles => Directories.Sum(entry => entry.OnlyInCardA);

    /// <summary>True when there is something to tell the user, on or off.</summary>
    /// <remarks>
    /// Deliberately not the same as <see cref="Enabled"/>. Turning the option off does not
    /// delete anything, so the copies outlive the setting and keep their ability to reappear
    /// the moment it is turned back on.
    /// </remarks>
    public bool WorthReporting => Enabled || CopiedFiles > 0;
}

/// <summary>
/// Detects RetroBat's GameCube save reconciliation, and reports it. It is never acted on.
/// </summary>
/// <remarks>
/// <b>Measured against emulatorlauncher's source and driven on a real install</b>, because
/// four documents in this repository described it wrongly and the wrong description is the
/// one an agent would build against. <c>Dolphin.Generator.cs</c>, <c>SyncGCSaves</c>:
/// <list type="bullet">
/// <item>It is <b>GameCube only</b>. The option is declared twice in <c>es_features.cfg</c>,
/// both under <c>&lt;system name="gamecube"&gt;</c>, and the Wii branch of the generator never
/// calls the method. On Wii the setting exists in the menu and does nothing.</item>
/// <item>It runs <b>once per launch, inside emulatorlauncher, before Dolphin starts</b>. It is
/// not a background schedule, which is what makes it detectable at all: nothing moves while
/// RomMBat is running.</item>
/// <item>The two locations are <b>not</b> two emulator directories. They are
/// <c>GC/&lt;REGION&gt;/</c> and <c>GC/&lt;REGION&gt;/Card A/</c>, the second a subdirectory of
/// the first, because RetroBat points standalone Dolphin at the region root while the stock
/// Dolphin default is the <c>Card A</c> below it.</item>
/// <item>Both directions are <c>File.Copy</c> guarded by <c>try {} catch {}</c>, so every
/// failure is silent, and where both sides hold a file the older is renamed <c>.old</c>.</item>
/// </list>
/// <para>
/// <b>The hazard is not the one the mtime rule suggests.</b> A save RomMBat restores is written
/// with the current time, so it is always the newest and always wins. What bites is the
/// one-sided branch: a file in <c>Card A</c> with nothing beside it is copied back out. Driven
/// on a real install, removing the region-root <c>.gci</c> and launching restored it from
/// <c>Card A</c> holding the previous session's bytes, and the only trace was the log line
/// "GameCube saves have been synced."
/// </para>
/// <para>
/// <b>Nothing here writes.</b> Ruling 5 of stage 2c is detect and report: RomMBat does not read
/// <c>Card A</c>, does not upload it, and does not delete it. Two writers reconciling one
/// directory by different rules is how saves get lost, and the user is told instead.
/// </para>
/// </remarks>
public static class DolphinSaveSync
{
    /// <summary>The es_settings.cfg key.</summary>
    public const string OptionKey = "dolphin_sync_saves";

    /// <summary>The only system the option has any effect on.</summary>
    public const string System = "gamecube";

    /// <summary>The subdirectory emulatorlauncher reconciles each region root against.</summary>
    public const string CardADirectoryName = "Card A";

    /// <summary>The three regions <c>SyncGCSaves</c> iterates, in its own order.</summary>
    public static IReadOnlyList<string> Regions { get; } = ["EUR", "USA", "JAP"];

    /// <summary>Where the GCI region directories live, relative to the RetroBat root.</summary>
    public static RelativePath GcRoot { get; } =
        RelativePath.Create($"saves/{System}/dolphin-emu/User/GC");

    /// <summary>
    /// Reads the option at es_settings.cfg's own precedence.
    /// </summary>
    /// <param name="fsName">
    /// A rom filename to check the per-game level for, or null to stop at the system level.
    /// </param>
    /// <remarks>
    /// RetroBat's <c>isOptSet</c> requires the key to be present and <c>getOptBoolean</c>
    /// accepts <c>true</c>, <c>1</c>, <c>enabled</c>, <c>on</c> and <c>yes</c>, case
    /// insensitively. Anything else, including a key present at some other value, is off. This
    /// mirrors that rather than guessing, because reporting the option as on when RetroBat will
    /// not act on it is worse than saying nothing.
    /// </remarks>
    public static (DolphinSyncScope Scope, string? Key) Read(EsSettingsFile settings, string? fsName = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (fsName is not null && Path.GetExtension(fsName).Length > 0)
        {
            var perGame = EsSettingsFile.PerGameKey(System, fsName, OptionKey);

            if (IsOn(settings.Value(perGame)))
            {
                return (DolphinSyncScope.PerGame, perGame);
            }
        }

        var system = EsSettingsFile.SystemKey(System, OptionKey);

        if (IsOn(settings.Value(system)))
        {
            return (DolphinSyncScope.System, system);
        }

        var global = $"global.{OptionKey}";

        return IsOn(settings.Value(global))
            ? (DolphinSyncScope.Global, global)
            : (DolphinSyncScope.Off, null);
    }

    /// <summary>Everything worth telling the user, from the setting and the tree together.</summary>
    /// <remarks>
    /// The tree is walked whatever the setting says. The copies outlive the option and a user
    /// who turned it off last month still has them, still not going up.
    /// </remarks>
    public static DolphinSyncState Inspect(RetroBatInstall install, EsSettingsFile? settings)
    {
        ArgumentNullException.ThrowIfNull(install);

        var (scope, key) = settings is null ? (DolphinSyncScope.Off, null) : Read(settings);
        var directories = new List<DolphinCardA>();

        foreach (var region in Regions)
        {
            var root = GcRoot.Combine(region);
            var cardA = root.Combine(CardADirectoryName);
            var absolute = install.Resolve(cardA);

            if (!Directory.Exists(absolute))
            {
                continue;
            }

            var beside = Names(install.Resolve(root));
            var inside = Names(absolute);

            if (inside.Count == 0)
            {
                continue;
            }

            // Only .gci is counted, because only .gci is what SyncGCSaves moves. A .old left
            // behind by an earlier reconciliation is litter, and counting it would inflate the
            // number the user is asked to act on.
            var mirrored = inside.Count(name => beside.Contains(name));

            directories.Add(new DolphinCardA(cardA, mirrored, inside.Count - mirrored));
        }

        return new DolphinSyncState(scope, key, directories);
    }

    /// <summary>The sentence the unsyncable report carries.</summary>
    public static string Describe(DolphinSyncState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var where = state.Scope switch
        {
            DolphinSyncScope.Global => $"'{state.SetAt}' is on, so this applies to every system that honours it",
            DolphinSyncScope.System => $"'{state.SetAt}' is on",
            DolphinSyncScope.PerGame => $"'{state.SetAt}' is on for that one game",
            _ => $"'{SystemKeyText}' is off, but the copies an earlier launch made are still here",
        };

        var found = state.CopiedFiles switch
        {
            // Nothing has been copied yet, which is the moment worth catching a user at: the
            // warning is useful before the first launch makes the copies, not only after.
            0 => "No 'Card A' holds anything yet, and the next launch starts making copies "
                + "there that RomMBat does not read, upload or evict.",
            1 => "1 save file sits in a 'Card A' that RomMBat does not read, upload or evict.",
            _ => $"{state.CopiedFiles} save files sit in a 'Card A' that RomMBat does not read, "
                + "upload or evict.",
        };

        var consequence = state.RestorableFiles > 0
            ? $" {state.RestorableFiles} of them have no counterpart beside them, so the next "
                + "launch copies each one back out and a save removed here reappears holding "
                + "whatever that copy held."
            : string.Empty;

        return $"{where}. RetroBat reconciles saves/{System}/dolphin-emu/User/GC/<REGION>/ "
            + $"against its 'Card A' subdirectory once per launch, before Dolphin starts. "
            + $"{found}{consequence}";
    }

    private static string SystemKeyText => EsSettingsFile.SystemKey(System, OptionKey);

    private static bool IsOn(string? value) =>
        value is not null
        && (value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.Ordinal)
            || value.Equals("enabled", StringComparison.OrdinalIgnoreCase)
            || value.Equals("on", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase));

    private static HashSet<string> Names(string directory)
    {
        try
        {
            return
            [
                .. Directory.EnumerateFiles(directory, "*.gci").Select(Path.GetFileName).OfType<string>(),
            ];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A tree that cannot be read reports nothing rather than half of something. The
            // consequence is a missing warning, not a wrong action, because nothing here acts.
            return [];
        }
    }
}
