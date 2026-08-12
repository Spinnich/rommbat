using RomM.Client.Content;
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
