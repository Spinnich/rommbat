using System.Globalization;
using System.Text.Json.Serialization;

namespace RomM.Client.Catalog;

/// <summary>
/// One ROM, cut down to what browsing, sync-set resolution and the gamelist need.
/// </summary>
/// <remarks>
/// Deliberately not <c>SimpleRomSchema</c>, for two reasons that both bite at scale.
/// <para>
/// <b>Size overflows.</b> The pinned schema declares <c>fs_size_bytes</c> as a bare
/// <c>integer</c>, so the generated DTO carries it as an <see cref="int"/> and any ROM at or
/// above 2 GiB fails to deserialize, taking the whole page with it. PS2, GameCube and Wii
/// images routinely cross that line, and M3 compares the same field against the FAT32 4 GB
/// ceiling. Here it is a <see cref="long"/>.
/// </para>
/// <para>
/// <b>Cost.</b> A full walk of an 83k library is 333 pages of 250, and the generated schema
/// carries roughly seventy fields per ROM including eight metadata sub-objects. Most of that
/// is still skipped here.
/// </para>
/// <para>
/// <b>M4 widened this rather than adding a request.</b> The gamelist fields are already in
/// the page: <c>metadatum</c>, <c>summary</c>, the media paths, <c>regions</c> and
/// <c>languages</c> account for 15.7% of a 250-row page that the walk fetches anyway.
/// <c>GET /api/roms/{id}</c> would add 0.15 s per ROM, 150 s for a thousand-game set, and
/// its only extra fields are user arrays this client never reads. There is also no
/// id-list parameter on <c>/api/roms</c>, so "metadata for exactly these ROMs" cannot be
/// asked for at all. Nothing holds more than one page of these, and only selected members
/// keep theirs past the walk.
/// </para>
/// </remarks>
public sealed record RomRow
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("platform_id")]
    public int PlatformId { get; init; }

    [JsonPropertyName("platform_slug")]
    public string PlatformSlug { get; init; } = string.Empty;

    /// <summary>The platform's own folder name in RomM, and layer 2 of the mapping chain.</summary>
    [JsonPropertyName("platform_fs_slug")]
    public string? PlatformFsSlug { get; init; }

    [JsonPropertyName("platform_display_name")]
    public string? PlatformDisplayName { get; init; }

    /// <summary>The file name with its extension. The per-game override key is built from this.</summary>
    [JsonPropertyName("fs_name")]
    public string FsName { get; init; } = string.Empty;

    /// <summary>The extension as RomM reports it, without a leading dot.</summary>
    [JsonPropertyName("fs_extension")]
    public string? FsExtension { get; init; }

    [JsonPropertyName("fs_size_bytes")]
    public long SizeBytes { get; init; }

    /// <summary>
    /// The md5 of this ROM's <b>uncompressed</b> content, or null.
    /// </summary>
    /// <remarks>
    /// All three hash fields describe the content rather than the stored file, which the plan
    /// previously said only of <see cref="CrcHash"/>. Measured: a 1,025-byte <c>.zip</c>
    /// reports the hashes of the 16,400-byte <c>.nes</c> inside it, and a <c>.chd</c> reports
    /// the hashes of its own bytes. So comparing an archive's own bytes against this is always
    /// wrong.
    /// <para>
    /// Null is ordinary. Of 1,895 single-file ROMs sampled from a real library, 91.0% carried
    /// an md5 and 96.3% a sha1, so verification has to degrade to size for the rest.
    /// </para>
    /// </remarks>
    [JsonPropertyName("md5_hash")]
    public string? Md5Hash { get; init; }

    [JsonPropertyName("sha1_hash")]
    public string? Sha1Hash { get; init; }

    /// <summary>The CRC-32 of the uncompressed content, as lower-case hex.</summary>
    [JsonPropertyName("crc_hash")]
    public string? CrcHash { get; init; }

    /// <summary>
    /// True when RomM holds this ROM as several files and would serve it as a zip.
    /// </summary>
    /// <remarks>
    /// Decides the download before it is made: any <c>Range</c> header on a multi-file ROM is
    /// refused 403 by nginx, so the header that makes a single-file download resumable breaks
    /// this one outright. v1 does not sync them at all.
    /// <para>
    /// It travels with an empty <see cref="FsExtension"/>: 105 of 105 multi-file ROMs sampled
    /// were extensionless and every extensionless ROM was multi-file. The flag is read rather
    /// than the extension, because the flag is what states the fact.
    /// </para>
    /// </remarks>
    [JsonPropertyName("has_multiple_files")]
    public bool HasMultipleFiles { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("name_sort_key")]
    public string? NameSortKey { get; init; }

    /// <summary>
    /// Kept as text, because the pinned schema declares it a bare string with no
    /// <c>date-time</c> format and the server has been seen to send it without a zone.
    /// </summary>
    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; init; }

    /// <summary>The description a gamelist calls <c>desc</c>. Present on 81.9% of a real library.</summary>
    /// <remarks>
    /// The longest in a 5,000-row sample is 11,719 characters, which is why nothing holds more
    /// than one page of these at a time and only selected members keep theirs.
    /// </remarks>
    [JsonPropertyName("summary")]
    public string? Summary { get; init; }

    [JsonPropertyName("metadatum")]
    public RomMetadata? Metadata { get; init; }

    /// <summary>Rooted at the asset prefix already, unlike the three below it.</summary>
    [JsonPropertyName("path_cover_small")]
    public string? CoverSmallPath { get; init; }

    [JsonPropertyName("path_cover_large")]
    public string? CoverLargePath { get; init; }

    /// <summary>Relative to the asset prefix, and 200 with an HTML body if used as given.</summary>
    [JsonPropertyName("path_manual")]
    public string? ManualPath { get; init; }

    [JsonPropertyName("path_video")]
    public string? VideoPath { get; init; }

    /// <summary>
    /// The ScreenScraper block, read for one field.
    /// </summary>
    /// <remarks>
    /// <c>logo_path</c> is EmulationStation's marquee. ScreenScraper's own <c>marquee_path</c>
    /// is an arcade cabinet marquee and is a different picture; RomM's exporter maps the same
    /// way. Provider-scoped, so it is absent for about a fifth of a real library.
    /// </remarks>
    [JsonPropertyName("ss_metadata")]
    public RomScreenScraperMetadata? ScreenScraper { get; init; }

    /// <summary>Release regions, in RomM's vocabulary (<c>USA</c>, <c>Japan</c>, <c>World</c>).</summary>
    [JsonPropertyName("regions")]
    public IReadOnlyList<string> Regions { get; init; } = [];

    /// <summary>Languages, in RomM's vocabulary (<c>English</c>). Present on only 18.3%.</summary>
    [JsonPropertyName("languages")]
    public IReadOnlyList<string> Languages { get; init; } = [];

    /// <summary>The display name a user would recognise, falling back to the file name.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? FsName : Name;

    /// <summary>What a set orders by when ordering by name.</summary>
    public string SortKey => string.IsNullOrWhiteSpace(NameSortKey) ? DisplayName : NameSortKey;

    /// <summary>
    /// <see cref="UpdatedAt"/> as an instant, or null when it is absent or unparseable.
    /// </summary>
    /// <remarks>
    /// A value with no zone is read as UTC. The server stores UTC and the field is only ever
    /// fed back into <c>updated_after</c>, so reading it as local time would move the cursor
    /// by the offset and silently skip or repeat work.
    /// </remarks>
    public DateTimeOffset? UpdatedAtUtc
    {
        get
        {
            if (string.IsNullOrWhiteSpace(UpdatedAt))
            {
                return null;
            }

            if (DateTimeOffset.TryParse(
                UpdatedAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
            {
                return parsed;
            }

            return null;
        }
    }
}

/// <summary>
/// The metadata block, carried on every row of the paged read.
/// </summary>
/// <remarks>
/// <b>Nothing here means what its name suggests without a conversion.</b>
/// <see cref="FirstReleaseDate"/> is milliseconds, not seconds; <see cref="AverageRating"/>
/// is 0-100, not 0-1; <see cref="Companies"/> merges developer and publisher into one
/// alphabetically sorted array, so neither role survives. Only <see cref="PlayerCount"/> is
/// already in EmulationStation's form. <c>RomMBat.Core.Metadata.GameMetadata</c> owns every
/// one of those conversions; nothing else should do them inline.
/// </remarks>
public sealed record RomMetadata
{
    [JsonPropertyName("genres")]
    public IReadOnlyList<string> Genres { get; init; } = [];

    [JsonPropertyName("franchises")]
    public IReadOnlyList<string> Franchises { get; init; } = [];

    /// <summary>
    /// Every company involved, in one sorted list.
    /// </summary>
    /// <remarks>
    /// Sorted on 4,197 of 4,197 rows that carry one, so indexing it reads the alphabet rather
    /// than a role. Chrono Trigger arrives as <c>["Squaresoft", "Squaresoft"]</c>.
    /// </remarks>
    [JsonPropertyName("companies")]
    public IReadOnlyList<string> Companies { get; init; } = [];

    /// <summary>Already <c>1</c>, <c>1-2</c>, <c>1-4</c>, which is what <c>&lt;players&gt;</c> wants.</summary>
    [JsonPropertyName("player_count")]
    public string? PlayerCount { get; init; }

    /// <summary>Unix time in <b>milliseconds</b>. Read as seconds it lands in year 0.</summary>
    [JsonPropertyName("first_release_date")]
    public long? FirstReleaseDate { get; init; }

    /// <summary>On a 0-100 scale. A gamelist rating is 0-1 to two decimals.</summary>
    [JsonPropertyName("average_rating")]
    public double? AverageRating { get; init; }
}

/// <summary>The ScreenScraper provider block, read only for the logo.</summary>
public sealed record RomScreenScraperMetadata
{
    /// <summary>Relative to the asset prefix. This is EmulationStation's marquee.</summary>
    [JsonPropertyName("logo_path")]
    public string? LogoPath { get; init; }
}

/// <summary>One page of <see cref="RomRow"/>, with the sidecars deliberately absent.</summary>
public sealed record RomPage
{
    [JsonPropertyName("items")]
    public IReadOnlyList<RomRow> Items { get; init; } = [];

    /// <summary>
    /// How many rows the query matches in total.
    /// </summary>
    /// <remarks>
    /// The only one of the four default-on flags kept on. M0 probe 5 measured it at zero
    /// bytes (it is an integer, while the other three cost a flat 841 KB per request), and it
    /// is what lets an interrupted walk report progress and know when it is finished.
    /// </remarks>
    [JsonPropertyName("total")]
    public int Total { get; init; }

    [JsonPropertyName("limit")]
    public int Limit { get; init; }

    [JsonPropertyName("offset")]
    public int Offset { get; init; }
}
