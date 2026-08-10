using System.Globalization;
using System.Text.Json.Serialization;

namespace RomM.Client.Catalog;

/// <summary>
/// One ROM, cut down to what browsing and sync-set resolution need.
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
/// carries roughly seventy fields per ROM including eight metadata sub-objects. None of that
/// is read here, and parsing it would be the largest cost in the walk.
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
