using System.Globalization;
using RomM.Client.Catalog;
using RomM.Client.Content;

namespace RomMBat.Core.Metadata;

/// <summary>
/// One game's metadata, already converted into what a gamelist wants.
/// </summary>
/// <remarks>
/// <b>Every field here is the result of a conversion, not a copy</b>, and the conversions are
/// the point of the type: RomM's release date is milliseconds against a
/// <c>YYYYMMDDT000000</c> string, its rating is 0-100 against 0-1, its genres and franchises
/// are arrays against single-valued elements, and its regions and languages use a different
/// vocabulary in both directions. Doing any of that at the point of writing XML would put
/// five silent bugs in the one place nothing checks them.
/// <para>
/// This is what a gamelist is regenerated from with the server unreachable, so it holds the
/// converted values rather than the raw ones.
/// </para>
/// </remarks>
public sealed record GameMetadata
{
    public required int RomId { get; init; }

    /// <summary>The RetroBat folder whose gamelist this entry belongs in.</summary>
    public required string Folder { get; init; }

    /// <summary>
    /// The ROM's file name, which is what a gamelist entry's <c>path</c> is built from.
    /// </summary>
    /// <remarks>
    /// Held here as well as on the file row because it outlives it: after an eviction the
    /// file row is gone and this is what says the entry left behind in the gamelist is
    /// RomMBat's to remove rather than a user's own scraped one.
    /// </remarks>
    public required string FsName { get; init; }

    /// <summary>The <c>name</c> element. Never empty: it falls back to the file name.</summary>
    public required string Name { get; init; }

    /// <summary>The <c>desc</c> element, from <c>summary</c>.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// The <c>developer</c> element, and there is deliberately no publisher beside it.
    /// </summary>
    /// <remarks>
    /// <c>metadatum.companies</c> merges both roles into one array and sorts it
    /// alphabetically on every row measured, so <c>companies[0]</c> and <c>companies[1]</c>
    /// are the first two entries of the alphabet rather than a developer and a publisher.
    /// RomM's own exporter does index them that way, which is why KOTOR exports from RomM
    /// with Activision as its developer. Writing the whole list into <c>developer</c> claims
    /// only that these companies were involved, which is true.
    /// </remarks>
    public string? Developer { get; init; }

    /// <summary>Every genre, comma-joined, which is the form a real scraped install already holds.</summary>
    public string? Genre { get; init; }

    /// <summary>The <c>family</c> element, from the first franchise after deduping.</summary>
    public string? Family { get; init; }

    /// <summary>Copied straight through: RomM already writes <c>1-2</c>.</summary>
    public string? Players { get; init; }

    /// <summary>Comma-joined ES language codes, or null.</summary>
    public string? Languages { get; init; }

    /// <summary>One ES region code, or null.</summary>
    public string? Region { get; init; }

    /// <summary><c>YYYYMMDDT000000</c>, or null.</summary>
    public string? ReleaseDate { get; init; }

    /// <summary>0 to 1 with two decimals, or null.</summary>
    public string? Rating { get; init; }

    /// <summary>Where each kind of media lives on the server, in RomM's own path shapes.</summary>
    public IReadOnlyDictionary<MediaKind, string> MediaPaths { get; init; } =
        new Dictionary<MediaKind, string>();

    /// <summary>When this was read from the server.</summary>
    public DateTimeOffset? FetchedAt { get; init; }

    /// <summary>True when there is nothing worth writing into a gamelist beyond a name.</summary>
    public bool IsBare =>
        Description is null
        && Developer is null
        && Genre is null
        && Family is null
        && Players is null
        && Languages is null
        && Region is null
        && ReleaseDate is null
        && Rating is null
        && MediaPaths.Count == 0;

    /// <summary>How a gamelist refers to this game, which is also the merge key.</summary>
    public string GamelistPath => "./" + FsName;

    /// <summary>Converts a catalog row into what a gamelist entry needs.</summary>
    public static GameMetadata From(RomRow row, string folder, DateTimeOffset fetchedAt)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        var metadata = row.Metadata;

        return new GameMetadata
        {
            RomId = row.Id,
            Folder = folder,
            FsName = row.FsName,
            Name = row.DisplayName,
            Description = Clean(row.Summary),
            Developer = Join(metadata?.Companies),
            Genre = Join(metadata?.Genres),
            Family = Distinct(metadata?.Franchises).FirstOrDefault(),
            Players = Clean(metadata?.PlayerCount),
            Languages = EsVocabulary.LanguageList(row.Languages),
            Region = EsVocabulary.Region(row.Regions),
            ReleaseDate = ReleaseDateOf(metadata?.FirstReleaseDate),
            Rating = RatingOf(metadata?.AverageRating),
            MediaPaths = MediaPathsOf(row),
            FetchedAt = fetchedAt,
        };
    }

    /// <summary>
    /// The release date as EmulationStation writes it.
    /// </summary>
    /// <remarks>
    /// <b>The value is Unix time in milliseconds.</b> Read as seconds every sampled value
    /// lands in year 0, and RomM's own exporter divides by 1000 before formatting. The time
    /// component is always midnight because RomM only carries a date.
    /// </remarks>
    public static string? ReleaseDateOf(long? milliseconds)
    {
        if (milliseconds is not { } value)
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(value)
                .UtcDateTime
                .ToString("yyyyMMdd'T'000000", CultureInfo.InvariantCulture);
        }
        catch (ArgumentOutOfRangeException)
        {
            // A value outside the representable range is a broken row, not a reason to lose
            // the rest of the entry.
            return null;
        }
    }

    /// <summary>
    /// The rating on the 0-1 scale a gamelist uses.
    /// </summary>
    /// <remarks>
    /// <b>The value arrives on a 0-100 scale.</b> All 3,216 rated ROMs in a 5,000-row sample
    /// were above 1.0, min 5.0 and max 100.0, and RomM's exporter divides by 100 with a
    /// comment naming the scale. Clamped, because a value above 100 would produce a rating ES
    /// cannot draw.
    /// </remarks>
    public static string? RatingOf(double? outOfHundred)
    {
        if (outOfHundred is not { } value || double.IsNaN(value))
        {
            return null;
        }

        var scaled = Math.Clamp(value / 100.0, 0.0, 1.0);
        return scaled.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static Dictionary<MediaKind, string> MediaPathsOf(RomRow row)
    {
        var paths = new Dictionary<MediaKind, string>();

        Add(MediaKind.Image, row.CoverLargePath);
        Add(MediaKind.Thumbnail, row.CoverSmallPath);
        Add(MediaKind.Marquee, row.ScreenScraper?.LogoPath);
        Add(MediaKind.Video, row.VideoPath);
        Add(MediaKind.Manual, row.ManualPath);

        return paths;

        void Add(MediaKind kind, string? path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                paths[kind] = path.Trim();
            }
        }
    }

    /// <summary>Joins a list into one element's text, deduped and in the order given.</summary>
    private static string? Join(IEnumerable<string>? values)
    {
        var distinct = Distinct(values);
        return distinct.Count == 0 ? null : string.Join(", ", distinct);
    }

    /// <summary>
    /// The values with blanks and repeats removed.
    /// </summary>
    /// <remarks>
    /// Repeats are real rather than defensive: Chrono Trigger's companies arrive as
    /// <c>["Squaresoft", "Squaresoft"]</c>, and 18 of 5,000 sampled ROMs repeat a franchise.
    /// </remarks>
    private static List<string> Distinct(IEnumerable<string>? values)
    {
        var seen = new List<string>();
        foreach (var value in values ?? [])
        {
            var cleaned = Clean(value);
            if (cleaned is not null && !seen.Contains(cleaned, StringComparer.OrdinalIgnoreCase))
            {
                seen.Add(cleaned);
            }
        }

        return seen;
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
