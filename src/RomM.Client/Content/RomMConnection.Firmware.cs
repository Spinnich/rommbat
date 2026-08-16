using System.Globalization;
using System.Net;
using RomM.Client.Catalog;
using RomM.Client.Content;

namespace RomM.Client;

/// <summary>The firmware reads M5 needs: listing one platform's records, and fetching one file.</summary>
public sealed partial class RomMConnection
{
    /// <summary>
    /// Lists one platform's firmware. Needs <c>firmware.read</c>.
    /// </summary>
    /// <remarks>
    /// <b>Not the call a whole-library BIOS pass makes.</b> The same records arrive inlined on
    /// <c>GET /api/platforms</c>, complete, for the price of one request rather than 79. This
    /// is what a certification pass over a single platform wants, and what answers "show me
    /// what RomM has for this system" without paying for the rest.
    /// </remarks>
    public Task<RomMResponse<IReadOnlyList<FirmwareRow>>> ListFirmwareAsync(
        int platformId,
        CancellationToken cancellationToken = default) =>
        GetAuthenticatedAsync<IReadOnlyList<FirmwareRow>>(
            "api/firmware?platform_id=" + platformId.ToString(CultureInfo.InvariantCulture),
            cancellationToken);

    /// <summary>
    /// Streams one firmware file into <paramref name="destination"/>.
    /// </summary>
    /// <remarks>
    /// <b>No resume, deliberately.</b> The route supports one, measured: it answers a range
    /// with 206, a <c>content-range</c> and an <c>etag</c>, splices byte-exact, and refuses a
    /// point past the end with 416, exactly as the ROM route does. It is not used because the
    /// largest firmware a real library serves is 4 MiB and the median is 64 KiB, so a failed
    /// transfer starts again, as media does. The measurement is recorded because it means a
    /// larger file could adopt M3's machinery without a server-side surprise.
    /// <para>
    /// <b>The file name in the URL is not read by the server.</b> The right id under any name
    /// serves the bytes. It is still sent, escaped, because that is the documented route and
    /// because a request that names the file is the one a server log can be read against.
    /// </para>
    /// </remarks>
    /// <param name="firmware">The record to fetch. Must not be <c>missing_from_fs</c>.</param>
    /// <param name="destination">A writable stream. The caller owns it and what it becomes.</param>
    /// <param name="progress">Reported roughly every buffer.</param>
    /// <param name="cancellationToken">A user cancellation, kept distinct from a stall.</param>
    /// <exception cref="RomMUnreachableException">The transfer stalled or the link dropped.</exception>
    public async Task<RomMResponse<FirmwareResult>> DownloadFirmwareAsync(
        FirmwareRow firmware,
        Stream destination,
        IProgress<RomContentProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(firmware);
        ArgumentNullException.ThrowIfNull(destination);

        if (!IsAuthenticated)
        {
            return RomMResponse.Failure<FirmwareResult>(
                RomMResponseStatus.Unauthorized,
                "No access token is stored. Pair first.");
        }

        var path = $"api/firmware/{firmware.Id.ToString(CultureInfo.InvariantCulture)}/content/"
            + Uri.EscapeDataString(firmware.FileName);

        using var message = new HttpRequestMessage(HttpMethod.Get, Resolve(path));
        using var response = await SendForDownloadAsync(message, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return await DescribeFirmwareFailureAsync(firmware, response, cancellationToken).ConfigureAwait(false);
        }

        var contentType = response.Content.Headers.ContentType?.MediaType;

        // The same guard media has, and for the same reason: a route that answers 200 with the
        // web UI's page would otherwise write HTML into bios/ under a BIOS name. It cannot
        // demand octet-stream, because the type is guessed from the extension and a .rom
        // arrives as text/plain.
        if (contentType is not null
            && RejectedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            return RomMResponse.Failure<FirmwareResult>(
                RomMResponseStatus.ServerError,
                $"the server answered '{firmware.FileName}' with a web page rather than a file.");
        }

        var total = response.Content.Headers.ContentLength;
        var written = await CopyAsync(response, destination, 0, total, progress, cancellationToken)
            .ConfigureAwait(false);

        return RomMResponse.Success(new FirmwareResult
        {
            BytesWritten = written,
            ContentType = contentType,
            Validator = response.Headers.ETag?.ToString(),
        });
    }

    private static async Task<RomMResponse<FirmwareResult>> DescribeFirmwareFailureAsync(
        FirmwareRow firmware,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var detail = await ReadDetailAsync(response, cancellationToken).ConfigureAwait(false);

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => RomMResponse.Failure<FirmwareResult>(
                RomMResponseStatus.Unauthorized,
                detail ?? "The stored token is expired or revoked. Pair again."),

            HttpStatusCode.Forbidden => RomMResponse.Failure<FirmwareResult>(
                RomMResponseStatus.Forbidden,
                detail ?? "The stored token was not granted firmware.read, so BIOS files cannot be fetched."),

            HttpStatusCode.NotFound => RomMResponse.Failure<FirmwareResult>(
                RomMResponseStatus.ServerError,
                detail ?? $"'{firmware.FileName}' is no longer on the server."),

            // Measured, and it is the ordinary answer for a record flagged missing_from_fs:
            // the row outlived the file. Said in those words because "internal server error"
            // sends a user to the server's logs for something that is not a fault there.
            HttpStatusCode.InternalServerError => RomMResponse.Failure<FirmwareResult>(
                RomMResponseStatus.ServerError,
                $"RomM has a record for '{firmware.FileName}' but no longer has the file itself. "
                    + "Re-scan the library on the server, or put the file back."),

            _ => RomMResponse.Failure<FirmwareResult>(
                RomMResponseStatus.ServerError,
                detail ?? $"The server answered {(int)response.StatusCode} while downloading '{firmware.FileName}'."),
        };
    }
}
