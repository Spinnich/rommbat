using System.Globalization;

namespace RomM.Client.Content;

/// <summary>Which picture, video or document a media path names.</summary>
public enum MediaKind
{
    /// <summary>The large cover, which becomes the gamelist's <c>image</c>.</summary>
    Image,

    /// <summary>The small cover, which becomes <c>thumbnail</c>.</summary>
    Thumbnail,

    /// <summary>The game's logo art, which is what EmulationStation calls a marquee.</summary>
    Marquee,

    Video,

    Manual,
}

/// <summary>
/// One media file on the RomM host, addressed by the resource path a ROM row carries.
/// </summary>
/// <remarks>
/// <b>These are static files behind nginx, not an API route</b>, so none of
/// <c>RomMConnection</c>'s authenticated helpers applies: a bearer token changes nothing,
/// and the response carries an nginx <c>ETag</c> and <c>Accept-Ranges: bytes</c> rather than
/// a RomM error body.
/// </remarks>
public sealed record MediaResource
{
    /// <summary>Where every media path is rooted, whether or not it says so.</summary>
    public const string AssetPrefix = "/assets/romm/resources/";

    public required MediaKind Kind { get; init; }

    /// <summary>The path exactly as the ROM row gave it.</summary>
    public required string SourcePath { get; init; }

    /// <summary>
    /// The extension the saved file should carry, taken from the source path.
    /// </summary>
    /// <remarks>
    /// Covers are <c>.png</c> on this library and videos <c>.mp4</c>, but neither is
    /// promised, and a gamelist entry has to name the file that actually landed.
    /// </remarks>
    public string Extension
    {
        get
        {
            var path = Normalize(SourcePath);
            var query = path.IndexOf('?', StringComparison.Ordinal);
            if (query >= 0)
            {
                path = path[..query];
            }

            var slash = path.LastIndexOf('/');
            var name = slash < 0 ? path : path[(slash + 1)..];
            var dot = name.LastIndexOf('.');
            return dot <= 0 ? string.Empty : name[dot..].ToLowerInvariant();
        }
    }

    /// <summary>
    /// Puts either shape of media path onto the asset prefix exactly once, and drops the query.
    /// </summary>
    /// <remarks>
    /// <b>Both halves are load-bearing.</b> <c>path_cover_small</c> and
    /// <c>path_cover_large</c> arrive already rooted at the prefix, while <c>path_manual</c>,
    /// <c>path_video</c> and <c>ss_metadata.logo_path</c> arrive relative to it. Sending the
    /// relative form as given does not fail: nginx answers <b>200 with the web UI's
    /// index.html</b>, 5,826 bytes, carrying an <c>ETag</c> and <c>Accept-Ranges</c>, which
    /// would be written to disk as a PDF.
    /// <para>
    /// The query is a cache-busting <c>?ts=</c> carrying a <b>raw space</b>. nginx ignores it
    /// and answers the same bytes and the same <c>ETag</c> either way, so it is dropped
    /// rather than escaped.
    /// </para>
    /// </remarks>
    public static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var trimmed = path.Trim();
        var query = trimmed.IndexOf('?', StringComparison.Ordinal);
        if (query >= 0)
        {
            trimmed = trimmed[..query];
        }

        trimmed = trimmed.TrimStart('/');

        return trimmed.StartsWith(AssetPrefix.TrimStart('/'), StringComparison.OrdinalIgnoreCase)
            ? "/" + trimmed
            : AssetPrefix + trimmed;
    }

    /// <summary>The path to request, percent-encoded a segment at a time.</summary>
    public string RequestPath
    {
        get
        {
            var normalized = Normalize(SourcePath);
            var segments = normalized.Split('/');
            for (var index = 0; index < segments.Length; index++)
            {
                segments[index] = Uri.EscapeDataString(segments[index]);
            }

            return string.Join('/', segments);
        }
    }
}

/// <summary>What a media download produced.</summary>
public sealed record MediaResult
{
    public long BytesWritten { get; init; }

    /// <summary>The <c>Content-Type</c> the server sent, which is the only thing that catches a wrong prefix.</summary>
    public string? ContentType { get; init; }

    public string? Validator { get; init; }

    public override string ToString() =>
        $"{BytesWritten.ToString("N0", CultureInfo.InvariantCulture)} bytes, {ContentType ?? "no content type"}";
}
