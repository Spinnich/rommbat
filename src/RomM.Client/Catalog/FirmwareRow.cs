using System.Text.Json.Serialization;

namespace RomM.Client.Catalog;

/// <summary>
/// One firmware record, cut down to what an md5 join needs.
/// </summary>
/// <remarks>
/// Slim for the same reason <see cref="PlatformRow"/> is: these arrive inlined on
/// <c>GET /api/platforms</c>, 656 of them on a real library, and a generated DTO would drag
/// the whole <c>PlatformSchema</c> in with its <c>int32</c> <c>fs_size_bytes</c>.
/// <para>
/// <b>Two fields decide whether a record is a match, and neither is the obvious one.</b>
/// <see cref="Md5Hash"/> is the only join, because file names differ between the two projects
/// and <see cref="IsVerified"/> is false on files RetroBat requires. And
/// <see cref="MissingFromFs"/> means the server has the row but not the bytes: 142 of the 656
/// records measured carry it, and their content route answers <b>500</b> rather than 404.
/// </para>
/// </remarks>
public sealed record FirmwareRow
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    /// <summary>The name RomM serves it under, which is not the name RetroBat wants it at.</summary>
    [JsonPropertyName("file_name")]
    public string FileName { get; init; } = string.Empty;

    [JsonPropertyName("file_size_bytes")]
    public long SizeBytes { get; init; }

    /// <summary>The only thing the join reads.</summary>
    [JsonPropertyName("md5_hash")]
    public string? Md5Hash { get; init; }

    [JsonPropertyName("sha1_hash")]
    public string? Sha1Hash { get; init; }

    [JsonPropertyName("crc_hash")]
    public string? CrcHash { get; init; }

    /// <summary>
    /// RomM's own verdict on the file, which RomMBat never filters on.
    /// </summary>
    /// <remarks>
    /// Carried so a diagnostic can show it, and read by nothing that decides anything. RomM
    /// knows 63 of the 156 md5s RetroBat requires, and <c>psxonpsp660.bin</c> is false on
    /// every copy while being exactly what RetroBat asks for.
    /// </remarks>
    [JsonPropertyName("is_verified")]
    public bool IsVerified { get; init; }

    /// <summary>True when the row outlived the file it describes.</summary>
    [JsonPropertyName("missing_from_fs")]
    public bool MissingFromFs { get; init; }

    /// <summary>True when this record can actually be fetched and joined on.</summary>
    public bool IsFetchable => !MissingFromFs && !string.IsNullOrWhiteSpace(Md5Hash);

    /// <summary>The hash, lower-cased, which is the form every comparison uses.</summary>
    public string? NormalizedMd5 =>
        string.IsNullOrWhiteSpace(Md5Hash) ? null : Md5Hash.Trim().ToLowerInvariant();
}
