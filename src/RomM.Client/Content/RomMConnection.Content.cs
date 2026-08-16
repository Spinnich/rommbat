using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using RomM.Client.Catalog;
using RomM.Client.Content;

namespace RomM.Client;

/// <summary>The content reads M3 needs: downloading a ROM, and identifying one already on disk.</summary>
public sealed partial class RomMConnection
{
    /// <summary>
    /// How long a transfer may go without a single byte arriving before it is called dead.
    /// </summary>
    /// <remarks>
    /// A download has no overall deadline, because a 4 GB ROM over a slow link is a legitimate
    /// hour. What it does have is a stall watchdog: the connection is only healthy while bytes
    /// keep coming, and a yanked drive or a dropped Wi-Fi link shows up as silence rather than
    /// as an error.
    /// </remarks>
    public static TimeSpan DownloadStallTimeout => TimeSpan.FromSeconds(60);

    /// <summary>How much is read at a time. Large enough to keep a fast link busy, small enough to stay off the LOH.</summary>
    private const int DownloadBufferSize = 64 * 1024;

    private HttpClient? _downloadHttp;

    /// <summary>
    /// Streams a ROM's content into <paramref name="destination"/>.
    /// </summary>
    /// <remarks>
    /// <b>The <c>Range</c> header is conditional, which is the opposite of what the plan
    /// originally said.</b> A single-file ROM is asked for with <c>Range: bytes=&lt;from&gt;-</c>,
    /// which answers 206 with a <c>Content-Range</c> and an <c>ETag</c>, and resumes exactly.
    /// A multi-file ROM is asked for plainly, because any <c>Range</c> on one is refused 403.
    /// <para>
    /// This method owns HTTP semantics and nothing else. The caller owns the file: it opens the
    /// <c>.part</c>, positions it, and decides what a verified transfer becomes. The one
    /// exception is <see cref="RomContentResult.RestartedFromScratch"/>, where the server
    /// answers a ranged request with the whole body and the destination has to be truncated
    /// before the first byte lands.
    /// </para>
    /// </remarks>
    /// <param name="request">Which ROM, and where to carry on from.</param>
    /// <param name="destination">A writable, seekable stream. Its length is set by this call.</param>
    /// <param name="progress">Reported roughly every buffer, for a progress bar.</param>
    /// <param name="onValidator">
    /// Called with the response's <c>ETag</c> before a byte of the body is read. A transfer that
    /// finishes has nothing to resume, so the only run that needs the validator is one that dies
    /// partway, and by then the result this method returns will never arrive.
    /// </param>
    /// <param name="cancellationToken">A user cancellation, which is kept distinct from a stall.</param>
    /// <exception cref="RomMUnreachableException">The transfer stalled or the link dropped.</exception>
    public async Task<RomMResponse<RomContentResult>> DownloadRomContentAsync(
        RomContentRequest request,
        Stream destination,
        IProgress<RomContentProgress>? progress = null,
        Action<string?>? onValidator = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(destination);

        if (!IsAuthenticated)
        {
            return RomMResponse.Failure<RomContentResult>(
                RomMResponseStatus.Unauthorized,
                "No access token is stored. Pair first.");
        }

        if (request.IsMultiFile && request.ResumeFrom > 0)
        {
            return RomMResponse.Failure<RomContentResult>(
                RomMResponseStatus.ServerError,
                $"'{request.FsName}' is a multi-file ROM, which the server will not serve a range of, "
                    + "so an interrupted transfer has to start again rather than resume.");
        }

        var path = $"api/roms/{request.RomId.ToString(CultureInfo.InvariantCulture)}/content/"
            + Uri.EscapeDataString(request.FsName);

        using var message = new HttpRequestMessage(HttpMethod.Get, Resolve(path));

        // Single-file only. On a multi-file ROM this header is what turns a working download
        // into a 403, whatever offset it names.
        if (!request.IsMultiFile)
        {
            message.Headers.Range = new RangeHeaderValue(request.ResumeFrom, null);

            if (request.ResumeFrom > 0 && !string.IsNullOrWhiteSpace(request.Validator))
            {
                message.Headers.TryAddWithoutValidation("If-Range", request.Validator);
            }
        }

        using var response = await SendForDownloadAsync(message, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return await DescribeContentFailureAsync(request, response, cancellationToken).ConfigureAwait(false);
        }

        var resumed = response.StatusCode == HttpStatusCode.PartialContent;
        var restarted = request.ResumeFrom > 0 && !resumed;

        onValidator?.Invoke(response.Headers.ETag?.ToString());

        if (restarted)
        {
            // A stale validator, so the bytes already on disk describe a file that no longer
            // exists. Throwing them away costs one download; keeping them would splice.
            destination.SetLength(0);
        }

        destination.Seek(0, SeekOrigin.End);

        var resumedFrom = restarted ? 0 : request.ResumeFrom;
        var total = TotalLength(response, resumedFrom);
        var written = await CopyAsync(response, destination, resumedFrom, total, progress, cancellationToken)
            .ConfigureAwait(false);

        return RomMResponse.Success(new RomContentResult
        {
            BytesWritten = written,
            TotalBytes = total,
            Validator = response.Headers.ETag?.ToString(),
            Resumed = resumed && request.ResumeFrom > 0,
            RestartedFromScratch = restarted,
        });
    }

    /// <summary>
    /// Finds the ROM whose content hashes to this, for adopting a file already on disk.
    /// </summary>
    /// <remarks>
    /// Takes an md5, a sha1 or a CRC, all of the <b>uncompressed</b> content. A hit is a
    /// ~12 KB body in 133-385 ms; a <b>miss costs 8.3 s</b>, which is why this identifies a
    /// handful of unattributed files and is never run across a library.
    /// <para>
    /// The response is parsed into the same slim <see cref="RomRow"/> the paged read uses, so
    /// the <c>int32</c> <c>fs_size_bytes</c> in the generated schema cannot overflow on a large
    /// title.
    /// </para>
    /// </remarks>
    /// <returns>A row, or a null value when nothing matches.</returns>
    public async Task<RomMResponse<RomRow?>> FindRomByHashAsync(
        string hash,
        RomHashKind kind = RomHashKind.Md5,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);

        if (!IsAuthenticated)
        {
            return RomMResponse.Failure<RomRow?>(
                RomMResponseStatus.Unauthorized,
                "No access token is stored. Pair first.");
        }

        var field = kind switch
        {
            RomHashKind.Sha1 => "sha1_hash",
            RomHashKind.Crc => "crc_hash",
            _ => "md5_hash",
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            Resolve($"api/roms/by-hash?{field}={Uri.EscapeDataString(hash.Trim())}"));

        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return RomMResponse.Success(await ReadAsync<RomRow>(response, cancellationToken).ConfigureAwait(false));
        }

        // A miss is a 404, and that is an answer rather than a failure: the file is not in the
        // library, so it is not adoptable, which is ordinary for a user's own ROMs.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return RomMResponse.Success<RomRow?>(null);
        }

        var detail = await ReadDetailAsync(response, cancellationToken).ConfigureAwait(false);

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => RomMResponse.Failure<RomRow?>(
                RomMResponseStatus.Unauthorized,
                detail ?? "The stored token is expired or revoked. Pair again."),
            HttpStatusCode.Forbidden => RomMResponse.Failure<RomRow?>(
                RomMResponseStatus.Forbidden,
                detail ?? "The stored token was not granted roms.read."),
            _ => RomMResponse.Failure<RomRow?>(
                RomMResponseStatus.ServerError,
                detail ?? $"The server answered {(int)response.StatusCode} to the hash lookup."),
        };
    }

    /// <summary>
    /// Every ROM id the account can see, if the server can produce them inside a budget.
    /// </summary>
    /// <remarks>
    /// <b>Not the deletion-reconcile mechanism.</b> This endpoint takes no parameters, so it
    /// can be neither scoped nor paged, and it answered <b>504 after 300 s</b> on an 83,131 ROM
    /// library. Deletion is reconciled through set re-resolution, where a member a completed
    /// walk no longer finds becomes an eviction candidate.
    /// <para>
    /// It is still worth asking on a small library, where it is quick and catches an orphan no
    /// set claims. So it is called under a short budget and a failure is an ordinary answer:
    /// null, meaning the cross-check did not run.
    /// </para>
    /// </remarks>
    /// <param name="budget">How long to wait before giving up on it entirely.</param>
    public async Task<IReadOnlyList<int>?> TryGetRomIdentifiersAsync(
        TimeSpan budget,
        CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(budget);

        try
        {
            var response = await GetAuthenticatedAsync<IReadOnlyList<int>>("api/roms/identifiers", deadline.Token)
                .ConfigureAwait(false);

            return response.IsSuccess ? response.Value : null;
        }
        catch (Exception ex) when (ex is RomMUnreachableException or OperationCanceledException)
        {
            // The caller's own cancellation still has to propagate: only the budget elapsing is
            // an ordinary "the cross-check did not run".
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
    }

    /// <summary>
    /// The client used for transfers, which deliberately has no overall timeout.
    /// </summary>
    /// <remarks>
    /// <see cref="RomMClientOptions.RequestTimeout"/> bounds the response body as well as the
    /// headers, so the 30 s that suits an API call would abort every ROM over a few hundred
    /// megabytes. The handler is shared with the ordinary client, so
    /// <see cref="SocketsHttpHandler.ConnectTimeout"/> still applies and an unreachable host
    /// still fails in 2 s rather than 21.
    /// </remarks>
    private HttpClient DownloadClient
    {
        get
        {
            if (_downloadHttp is not null)
            {
                return _downloadHttp;
            }

            _downloadHttp = new HttpClient(_handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
            _downloadHttp.DefaultRequestHeaders.UserAgent.ParseAdd(Options.UserAgent);

            if (!string.IsNullOrEmpty(Options.AccessToken))
            {
                _downloadHttp.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", Options.AccessToken);
            }

            return _downloadHttp;
        }
    }

    /// <summary>Reads the body, giving up only when the bytes stop arriving.</summary>
    /// <param name="ceiling">
    /// Stops reading once this many bytes have been written, for a caller whose budget cannot
    /// be checked up front because the response declared no length. Overshoot is one buffer.
    /// The caller is told nothing here: it compares the return against its own room.
    /// </param>
    private static async Task<long> CopyAsync(
        HttpResponseMessage response,
        Stream destination,
        long resumedFrom,
        long? total,
        IProgress<RomContentProgress>? progress,
        CancellationToken cancellationToken,
        long? ceiling = null)
    {
        var buffer = new byte[DownloadBufferSize];
        var written = 0L;

        // Opening the body can fail on its own, and does when the link drops before anything
        // arrives, so it is classified the same way every read below is.
        Stream source;
        try
        {
            source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            throw Transport(ex, response, cancellationToken);
        }

        await using var body = source;

        while (true)
        {
            int read;

            using (var stall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                stall.CancelAfter(DownloadStallTimeout);

                try
                {
                    read = await body.ReadAsync(buffer, stall.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    // The distinction this whole path exists to keep: a user cancelling and a
                    // link going away are the same exception type and must not read alike.
                    throw new RomMUnreachableException(
                        UnreachableReason.RequestTimeout,
                        $"The download stopped receiving data for {DownloadStallTimeout.TotalSeconds:0} seconds. "
                            + "The server, the network or the drive went away.",
                        ex);
                }
                catch (Exception ex) when (ex is HttpRequestException or IOException)
                {
                    throw Transport(ex, response, cancellationToken);
                }
            }

            if (read == 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            written += read;
            progress?.Report(new RomContentProgress(written, resumedFrom, total));

            if (ceiling is { } limit && written > limit)
            {
                break;
            }
        }

        // Committed before the caller is told anything succeeded, so a power cut immediately
        // after this call finds the bytes on disk rather than in a buffer.
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);

        return written;
    }

    /// <summary>
    /// Turns a mid-transfer failure into one a caller can act on.
    /// </summary>
    /// <remarks>
    /// An <see cref="IOException"/> from a dropped link carries no request context of its own,
    /// so it is wrapped before classification rather than surfacing as a bare I/O error the
    /// caller would have to guess at.
    /// </remarks>
    private static Exception Transport(Exception ex, HttpResponseMessage response, CancellationToken cancellationToken) =>
        RomMTransportErrors.Classify(
            ex as HttpRequestException ?? new HttpRequestException(ex.Message, ex),
            response.RequestMessage?.RequestUri,
            cancellationToken);

    /// <summary>The whole file's length, from <c>Content-Range</c> when the response is partial.</summary>
    private static long? TotalLength(HttpResponseMessage response, long resumedFrom)
    {
        if (response.Content.Headers.ContentRange is { Length: { } length })
        {
            return length;
        }

        return response.Content.Headers.ContentLength is { } declared ? declared + resumedFrom : null;
    }

    private async Task<HttpResponseMessage> SendForDownloadAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await DownloadClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            throw RomMTransportErrors.Classify(ex, request.RequestUri, cancellationToken);
        }
    }

    private static async Task<RomMResponse<RomContentResult>> DescribeContentFailureAsync(
        RomContentRequest request,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var detail = await ReadDetailAsync(response, cancellationToken).ConfigureAwait(false);

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => RomMResponse.Failure<RomContentResult>(
                RomMResponseStatus.Unauthorized,
                detail ?? "The stored token is expired or revoked. Pair again."),

            HttpStatusCode.Forbidden => RomMResponse.Failure<RomContentResult>(
                RomMResponseStatus.Forbidden,
                // 403 has two quite different causes here, and only one is about scopes. nginx
                // answers 403 to any ranged request for a multi-file ROM, with its own HTML
                // page rather than a RomM error body.
                request.IsMultiFile
                    ? $"The server refused to serve part of '{request.FsName}'. Multi-file ROMs cannot be "
                        + "downloaded in ranges, so this transfer cannot resume."
                    : detail ?? "The stored token was not granted roms.read."),

            HttpStatusCode.NotFound => RomMResponse.Failure<RomContentResult>(
                RomMResponseStatus.ServerError,
                detail ?? $"'{request.FsName}' is no longer on the server."),

            HttpStatusCode.RequestedRangeNotSatisfiable => RomMResponse.Failure<RomContentResult>(
                RomMResponseStatus.RangeNotSatisfiable,
                $"The server rejected the resume point for '{request.FsName}'. The partial file is stale "
                    + "and has been discarded."),

            _ => RomMResponse.Failure<RomContentResult>(
                RomMResponseStatus.ServerError,
                detail ?? $"The server answered {(int)response.StatusCode} while downloading '{request.FsName}'."),
        };
    }
}

/// <summary>Which hash <c>GET /api/roms/by-hash</c> is being asked about.</summary>
public enum RomHashKind
{
    Md5,
    Sha1,
    Crc,
}
