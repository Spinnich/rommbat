using System.Net;
using RomM.Client.Content;

namespace RomM.Client;

/// <summary>Downloading cover art, marquees, videos and manuals, which are static files.</summary>
public sealed partial class RomMConnection
{
    /// <summary>
    /// What a media response has to look like before its bytes are kept.
    /// </summary>
    /// <remarks>
    /// The check exists because the failure it catches does not look like one. A media path
    /// requested without the asset prefix answers <b>200</b>, with an <c>ETag</c> and
    /// <c>Accept-Ranges</c>, and a body of the web UI's <c>index.html</c>. Status alone would
    /// write 5,826 bytes of HTML to disk and call it a manual.
    /// </remarks>
    private static readonly string[] RejectedContentTypes = ["text/html", "application/xhtml+xml"];

    /// <summary>
    /// Streams one media file into <paramref name="destination"/>.
    /// </summary>
    /// <remarks>
    /// No token is sent, because none is needed: nginx serves these paths and bearer and
    /// anonymous requests were byte-identical on every one measured. The transfer still runs
    /// on the download client, so it has no overall deadline and the same stall watchdog a
    /// ROM download has.
    /// <para>
    /// Media is small enough that a partial one is not worth resuming: the largest kind is a
    /// manual at a 2.45 MB median. A failed transfer starts again.
    /// </para>
    /// </remarks>
    /// <param name="resource">Which file, in either of the two path shapes RomM emits.</param>
    /// <param name="destination">A writable stream. The caller owns it and what it becomes.</param>
    /// <param name="maximumBytes">
    /// How much room the caller has. Checked against <c>Content-Length</c> before the body is
    /// read, so a disk budget refuses a file rather than overshooting it, which is the only
    /// pre-flight available: media has no size on the ROM row and a HEAD would be a second
    /// request per file. A response that declares no length is bounded during the read
    /// instead, and fails once it passes the limit.
    /// </param>
    /// <param name="progress">Reported roughly every buffer.</param>
    /// <param name="cancellationToken">A user cancellation, kept distinct from a stall.</param>
    /// <exception cref="RomMUnreachableException">The transfer stalled or the link dropped.</exception>
    public async Task<RomMResponse<MediaResult>> DownloadMediaAsync(
        MediaResource resource,
        Stream destination,
        long? maximumBytes = null,
        IProgress<RomContentProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(destination);

        using var message = new HttpRequestMessage(HttpMethod.Get, Resolve(resource.RequestPath.TrimStart('/')));
        using var response = await SendForDownloadAsync(message, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return response.StatusCode switch
            {
                HttpStatusCode.NotFound => RomMResponse.Failure<MediaResult>(
                    RomMResponseStatus.NotFound,
                    $"the server no longer has {DescribeKind(resource.Kind)} at that path."),
                _ => RomMResponse.Failure<MediaResult>(
                    RomMResponseStatus.ServerError,
                    $"the server answered {(int)response.StatusCode} for {DescribeKind(resource.Kind)}."),
            };
        }

        var contentType = response.Content.Headers.ContentType?.MediaType;

        if (contentType is not null
            && RejectedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            return RomMResponse.Failure<MediaResult>(
                RomMResponseStatus.ServerError,
                $"the server answered {DescribeKind(resource.Kind)} with a web page rather than a file. "
                    + "That is what a media path missing its resource prefix returns, so this is a bug in RomMBat "
                    + "rather than something on the server.");
        }

        var total = response.Content.Headers.ContentLength;

        if (maximumBytes is { } room && total is { } declared && declared > room)
        {
            return RomMResponse.Failure<MediaResult>(
                RomMResponseStatus.ServerError,
                $"{DescribeKind(resource.Kind)} is {declared:N0} bytes and only {room:N0} are left inside the budget.");
        }

        var written = await CopyAsync(
                response,
                destination,
                0,
                total,
                progress,
                cancellationToken,
                ceiling: maximumBytes)
            .ConfigureAwait(false);

        // The header check above cannot fire on a response that declared no length, so the
        // budget is enforced again on what actually arrived. The caller discards the partial
        // file, so nothing over budget reaches the folder.
        if (maximumBytes is { } limit && written > limit)
        {
            return RomMResponse.Failure<MediaResult>(
                RomMResponseStatus.ServerError,
                $"{DescribeKind(resource.Kind)} passed the {limit:N0} bytes left inside the budget "
                    + "and the server declared no length up front, so the transfer was stopped.");
        }

        return RomMResponse.Success(new MediaResult
        {
            BytesWritten = written,
            ContentType = contentType,
            Validator = response.Headers.ETag?.ToString(),
        });
    }

    private static string DescribeKind(MediaKind kind) => kind switch
    {
        MediaKind.Image => "the cover",
        MediaKind.Thumbnail => "the thumbnail",
        MediaKind.Marquee => "the marquee",
        MediaKind.Video => "the video",
        _ => "the manual",
    };
}
