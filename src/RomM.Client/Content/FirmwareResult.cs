using System.Globalization;

namespace RomM.Client.Content;

/// <summary>What a firmware download produced.</summary>
public sealed record FirmwareResult
{
    public long BytesWritten { get; init; }

    /// <summary>
    /// The <c>Content-Type</c> the server sent.
    /// </summary>
    /// <remarks>
    /// Guessed from the extension rather than declared, so a <c>.rom</c> arrives as
    /// <c>text/plain</c> and a <c>.bin</c> as <c>application/octet-stream</c>. Useful for
    /// refusing an HTML page and for nothing else.
    /// </remarks>
    public string? ContentType { get; init; }

    /// <summary>The <c>etag</c>, which is Starlette's and not a hash of the content.</summary>
    public string? Validator { get; init; }

    public override string ToString() =>
        $"{BytesWritten.ToString("N0", CultureInfo.InvariantCulture)} bytes, {ContentType ?? "no content type"}";
}
