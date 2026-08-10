namespace RomM.Client.Content;

/// <summary>What to fetch, and from where to carry on.</summary>
public sealed record RomContentRequest
{
    public required int RomId { get; init; }

    /// <summary>The ROM's <c>fs_name</c>, which is the last path segment of the endpoint.</summary>
    public required string FsName { get; init; }

    /// <summary>
    /// True when RomM would serve this ROM as a built-on-demand zip.
    /// </summary>
    /// <remarks>
    /// Suppresses the <c>Range</c> header, because such a request is refused <b>403</b> by
    /// nginx in every form measured. It also means the response carries no <c>ETag</c> and no
    /// <c>Accept-Ranges</c>, so nothing about that transfer is resumable.
    /// </remarks>
    public bool IsMultiFile { get; init; }

    /// <summary>How many bytes are already on disk, so the request asks only for the rest.</summary>
    public long ResumeFrom { get; init; }

    /// <summary>The validator the interrupted attempt recorded, sent back as <c>If-Range</c>.</summary>
    public string? Validator { get; init; }
}

/// <summary>How a transfer ended.</summary>
public sealed record RomContentResult
{
    /// <summary>How many bytes this call wrote.</summary>
    public long BytesWritten { get; init; }

    /// <summary>How long the whole file is, when the server said.</summary>
    public long? TotalBytes { get; init; }

    /// <summary>The <c>ETag</c> to record, so a later attempt can resume against it.</summary>
    public string? Validator { get; init; }

    /// <summary>True when the server honoured the range and the transfer continued where it stopped.</summary>
    public bool Resumed { get; init; }

    /// <summary>
    /// True when a resume was asked for and the server sent the whole file instead.
    /// </summary>
    /// <remarks>
    /// The safe outcome of a stale validator, and measured as what actually happens: nginx
    /// answers 200 with the whole body rather than splicing at an offset that no longer means
    /// anything. The destination is truncated before the first byte is written, so the caller
    /// gets a whole file rather than a hybrid.
    /// </remarks>
    public bool RestartedFromScratch { get; init; }
}

/// <summary>Progress for a running transfer.</summary>
/// <param name="BytesWritten">How much this call has written.</param>
/// <param name="ResumedFrom">How much was already on disk when it started.</param>
/// <param name="TotalBytes">The whole file's length, when the server said.</param>
public sealed record RomContentProgress(long BytesWritten, long ResumedFrom, long? TotalBytes)
{
    /// <summary>Where the whole file has reached, resumed bytes included.</summary>
    public long Position => ResumedFrom + BytesWritten;

    /// <summary>How far along, or null when the server sent no length.</summary>
    public double? Fraction => TotalBytes is > 0 ? Math.Min(1.0, (double)Position / TotalBytes.Value) : null;
}
