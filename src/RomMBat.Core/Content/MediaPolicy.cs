using RomM.Client.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;

namespace RomMBat.Core.Content;

/// <summary>
/// Which kinds of media a sync fetches.
/// </summary>
/// <remarks>
/// A setting rather than a constant because the sizes differ by more than an order of
/// magnitude, and the right answer depends on the drive. At the measured medians a game costs
/// 104 KB of thumbnail, 445 KB of marquee, 525 KB of cover, 1.99 MB of video and 2.45 MB of
/// manual.
/// <para>
/// <b>The default is covers, marquee and video</b>, about 3.1 MB per game, which is what this
/// milestone is done-when: box art, descriptions and videos. Manuals are left out because
/// nothing needs them and they are the single largest kind, and because only 46.1% of a real
/// library has one at all.
/// </para>
/// </remarks>
public static class MediaPolicy
{
    /// <summary>Comma-separated kind names, or <c>none</c>.</summary>
    public const string SettingKey = "media.kinds";

    /// <summary>What a fresh install fetches.</summary>
    public static IReadOnlyList<MediaKind> Default { get; } =
        [MediaKind.Image, MediaKind.Thumbnail, MediaKind.Marquee, MediaKind.Video];

    /// <summary>Every kind there is, in the order a sync fetches them.</summary>
    public static IReadOnlyList<MediaKind> All { get; } =
        [MediaKind.Image, MediaKind.Thumbnail, MediaKind.Marquee, MediaKind.Video, MediaKind.Manual];

    /// <summary>The configured kinds, or the default when nothing is set.</summary>
    public static IReadOnlyList<MediaKind> Read(SettingStore settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return Parse(settings.Get(SettingKey));
    }

    /// <summary>
    /// The configured kinds, defaulting to what RetroBat's own scraper is set to fetch.
    /// </summary>
    /// <remarks>
    /// <b>RetroBat already has this setting, so RomMBat asks it rather than inventing a second
    /// one.</b> A hands-on pass turned video off in RetroBat's scraper and RomMBat kept
    /// downloading it, which is two switches that look like they should agree and do not. Same
    /// reasoning that made the on-screen keyboard follow <c>Language</c>: the install is the
    /// authority on what the user already asked for.
    /// <para>
    /// <b>Only two kinds are covered, because only two exist upstream.</b> ES writes
    /// <c>ScrapeVideos</c> and <c>ScrapeManual</c> and has no toggle for the cover, the
    /// thumbnail or the marquee, so those three follow RomMBat's own default. Do not invent
    /// keys for them: an absent setting means ES's own default, which for both of these is on.
    /// </para>
    /// <para>
    /// <b>An explicit RomMBat setting still wins.</b> <c>media.kinds</c> is what somebody typed,
    /// and a preference stated here should not be overridden by one stated elsewhere. RetroBat's
    /// toggles shape the <i>default</i>, which is what a fresh install gets.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<MediaKind> Read(SettingStore settings, RetroBatInstall install)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(install);

        if (settings.Get(SettingKey) is { } chosen && !string.IsNullOrWhiteSpace(chosen))
        {
            return Parse(chosen);
        }

        var es = EsSettingsFile.Load(install.Resolve(EsSettingsFile.Location));

        // Built up from the kinds ES has no switch for, rather than filtered down from Default.
        // Filtering was wrong and the wrongness was invisible: Manual is not in Default, so the
        // ScrapeManual arm could never fire and a user with manuals turned on in RetroBat got
        // none. A branch that cannot fire is the shape #101 and #107 are open about, written
        // here by the change that introduced the setting.
        return
        [
            .. All.Where(kind => kind switch
            {
                MediaKind.Video => Wanted(es, "ScrapeVideos", MediaKind.Video),
                MediaKind.Manual => Wanted(es, "ScrapeManual", MediaKind.Manual),

                // Cover, thumbnail and marquee. ES offers no toggle, so RomMBat's own default
                // decides, and that is to fetch them.
                _ => Default.Contains(kind),
            }),
        ];
    }

    /// <summary>
    /// What RetroBat says about a kind, or RomMBat's own default when it says nothing.
    /// </summary>
    /// <remarks>
    /// <b>Only a key that is actually present overrides.</b> EmulationStation writes a setting
    /// when it differs from its own default, so an absent key is not the same as a "no" and is
    /// not reliably a "yes" either. Treating absent as on turned manuals, the largest kind by
    /// median, on for every install whose ES had never written the key, which is a behaviour
    /// change nobody asked for delivered through a setting they never touched.
    /// </remarks>
    private static bool Wanted(EsSettingsFile es, string key, MediaKind kind) =>
        es.Has(key)
            ? !string.Equals(es.Value(key), "false", StringComparison.OrdinalIgnoreCase)
            : Default.Contains(kind);

    /// <summary>Parses a setting value. An unreadable one falls back to the default rather than to nothing.</summary>
    public static IReadOnlyList<MediaKind> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Default;
        }

        if (value.Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        if (value.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return All;
        }

        var kinds = new List<MediaKind>();
        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // 'thumb' as well as 'thumbnail', because that is the suffix on disk and the name a
            // user would type after seeing it there.
            var name = part.Equals("thumb", StringComparison.OrdinalIgnoreCase) ? "thumbnail" : part;

            if (Enum.TryParse<MediaKind>(name, ignoreCase: true, out var kind) && !kinds.Contains(kind))
            {
                kinds.Add(kind);
            }
        }

        return kinds.Count == 0 ? Default : kinds;
    }

    /// <summary>How a set of kinds is written back to the setting.</summary>
    public static string Format(IEnumerable<MediaKind> kinds)
    {
        ArgumentNullException.ThrowIfNull(kinds);

        var names = kinds.Select(kind => kind.ToString().ToLowerInvariant()).ToList();
        return names.Count == 0 ? "none" : string.Join(",", names);
    }
}
