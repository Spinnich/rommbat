using System.Text.Json.Serialization;

namespace RomM.Client.Catalog;

/// <summary>
/// One RomM platform, cut down to what the mapping chain needs.
/// </summary>
/// <remarks>
/// Not <c>PlatformSchema</c>, for the reason measured against a real instance: the pinned
/// schema declares <c>fs_size_bytes</c> as a bare <c>integer</c>, so the generated DTO holds
/// it as an <see cref="int"/>, and a platform's total library size passes 2 GiB on the first
/// platform of any real install. <c>GET /api/platforms</c> then fails to deserialize
/// outright, taking the whole mapping surface with it. Here it is a <see cref="long"/>.
/// </remarks>
public sealed record PlatformRow
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("slug")]
    public string Slug { get; init; } = string.Empty;

    /// <summary>
    /// The platform's folder name in RomM.
    /// </summary>
    /// <remarks>
    /// Layer 2 of the mapping chain, and the best signal there is: when a library is already
    /// laid out Batocera-style this <b>is</b> the RetroBat folder name.
    /// </remarks>
    [JsonPropertyName("fs_slug")]
    public string? FsSlug { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("custom_name")]
    public string? CustomName { get; init; }

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("rom_count")]
    public int RomCount { get; init; }

    [JsonPropertyName("fs_size_bytes")]
    public long SizeBytes { get; init; }

    /// <summary>
    /// Every firmware record RomM holds for this platform, inlined on the list endpoint.
    /// </summary>
    /// <remarks>
    /// Complete rather than a preview, measured twice: <c>firmware_count</c> equals
    /// <c>len(firmware)</c> on all 123 platforms of a real library, and the widest platform's
    /// inlined set has the same 75 ids as <c>GET /api/firmware?platform_id=</c>. So the whole
    /// library's BIOS gap is one 424 KB request rather than one request per platform.
    /// </remarks>
    [JsonPropertyName("firmware")]
    public IReadOnlyList<FirmwareRow> Firmware { get; init; } = [];

    /// <summary>What the server says <see cref="Firmware"/> should hold.</summary>
    [JsonPropertyName("firmware_count")]
    public int FirmwareCount { get; init; }

    /// <summary>What to show a user, preferring whatever they renamed it to.</summary>
    public string Label =>
        CustomName is { Length: > 0 } custom ? custom
        : DisplayName is { Length: > 0 } display ? display
        : Name is { Length: > 0 } name ? name
        : Slug;
}
