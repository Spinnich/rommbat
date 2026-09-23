using System.Globalization;
using System.Text.Json.Serialization;

namespace RomM.Client.Content;

/// <summary>One member of a multi-file rom, as <c>files[]</c> on the rom's detail row describes it.</summary>
/// <remarks>
/// A slim read for the reason <c>RomRow</c> is one: the pinned schema's <c>RomFileSchema</c> is
/// generated with an <c>int32</c> <c>file_size_bytes</c>, and a PS1 disc image is 700 MB.
/// </remarks>
public sealed record RomFileRow
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("file_name")]
    public string FileName { get; init; } = string.Empty;

    [JsonPropertyName("file_size_bytes")]
    public long SizeBytes { get; init; }

    /// <summary>The member's md5, blank when RomM computed none. Settled to null by <see cref="Md5"/>.</summary>
    [JsonPropertyName("md5_hash")]
    public string? Md5Hash { get; init; }

    /// <summary>
    /// What RomM files the member as: <c>game</c> for a disc and its playlist, or one of the
    /// categories that belong in other folders, such as <c>dlc</c> or <c>update</c>.
    /// </summary>
    [JsonPropertyName("category")]
    public string? Category { get; init; }

    /// <summary>False for a file in a subfolder of the rom's folder.</summary>
    [JsonPropertyName("is_top_level")]
    public bool IsTopLevel { get; init; } = true;

    /// <summary>The md5 with a blank one read as absent, which is how RomM sends "none".</summary>
    [JsonIgnore]
    public string? Md5 => string.IsNullOrWhiteSpace(Md5Hash) ? null : Md5Hash;
}

/// <summary>The part of a rom's detail row this client reads to fetch its members.</summary>
internal sealed record RomFilesDetail
{
    [JsonPropertyName("files")]
    public List<RomFileRow>? Files { get; init; }
}

/// <summary>Formats the per-file content route's query.</summary>
internal static class RomFileQuery
{
    /// <summary>
    /// <c>file_ids=&lt;id&gt;</c>, which makes RomM serve that one member directly.
    /// </summary>
    /// <remarks>
    /// Read at the 5.3.0 tag, <c>get_rom_content</c> filters <c>rom.files</c> to the ids named and,
    /// left with one, redirects to nginx for the file itself rather than building a zip. Measured
    /// on a live 5.3.0: a ranged request answered 206 with an <c>ETag</c> and a
    /// <c>Content-Range</c> over the member's full length, and the body began with the CHD magic.
    /// So a member resumes and verifies like a single-file rom.
    /// </remarks>
    public static string For(int fileId) => "file_ids=" + fileId.ToString(CultureInfo.InvariantCulture);
}
