namespace RomM.Client.Catalog;

/// <summary>
/// Reads a hash field the server may have left blank.
/// </summary>
/// <remarks>
/// <b>RomM reports an absent hash as an empty string, not as null.</b> Measured on a live
/// 5.1.x instance: <c>GET /api/roms/191723</c> answers <c>"md5_hash": ""</c> and
/// <c>"crc_hash": ""</c> beside a populated sha1. Null is ordinary here, because only 91% of
/// a real library carries an md5, so every consumer asks whether there is a hash to compare
/// against at all.
/// <para>
/// That question has to be settled at the boundary. A blank that survives it is worse than no
/// hash, because it is a value that can never match: the row falls past the adoption path,
/// re-downloads on every sync, and is refused again by verification afterwards.
/// </para>
/// </remarks>
internal static class ServerHash
{
    /// <summary>The hash, or null where the server published none.</summary>
    internal static string? OrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
