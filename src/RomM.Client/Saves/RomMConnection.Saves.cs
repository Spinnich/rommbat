using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RomM.Client.Saves;

namespace RomM.Client;

/// <summary>How a save upload ended.</summary>
/// <param name="Conflict">
/// True on 409. <b>The body carries nothing to show the user</b>: measured at 5.1.1-beta.1 it
/// is a bare string, <c>{"detail": "Slot has a newer save since your last sync"}</c>, not the
/// structured object other clients document. So surfacing a useful conflict needs a separate
/// fetch of the save row for its timestamp and hash.
/// </param>
public sealed record SaveUploadResult(SaveRow? Save, bool Conflict, string? Detail);

/// <summary>The save, state and play-session surface.</summary>
/// <remarks>
/// <b>Two rules here are not preferences and the review checks them as rules.</b>
/// <c>optimistic=false</c> travels with the ack, and the upload's returned <c>file_name</c> is
/// persisted while a different field is written to disk. Both are measured; both lose saves
/// when got wrong.
/// </remarks>
public sealed partial class RomMConnection
{
    /// <summary>
    /// The retention this client asks for on every upload.
    /// </summary>
    /// <remarks>
    /// The server defaults to <c>autocleanup=false</c> with a limit of 10, which is unbounded
    /// growth: a slot gains a row per genuine change forever. Measured,
    /// <c>autocleanup=true&amp;autocleanup_limit=2</c> held a slot at exactly two across three
    /// further uploads, keeping the newest. Ten is kept because it is the server's own limit
    /// and is enough history to recover from a bad save, and it travels with the
    /// <c>keep_both</c> conflict default rather than instead of it: keep_both unbounded fills
    /// the library, and pruning without keep_both silently discards one side of a conflict.
    /// </remarks>
    public const int AutoCleanupLimit = 10;

    /// <summary>
    /// Asks the server what to do about every slot this device holds. Needs <c>assets.read</c>.
    /// </summary>
    /// <remarks>
    /// Full-state reconciliation, so a week offline is a bigger payload and nothing else, as
    /// long as every <c>updated_at</c> is the file's real mtime.
    /// </remarks>
    public Task<RomMResponse<NegotiateResult>> NegotiateSavesAsync(
        NegotiateRequest request,
        CancellationToken cancellationToken = default) =>
        PostAuthenticatedAsync<NegotiateRequest, NegotiateResult>(
            "api/sync/negotiate",
            request,
            cancellationToken);

    /// <summary>
    /// Uploads one save. Needs <c>assets.write</c>.
    /// </summary>
    /// <remarks>
    /// <b>Persist the <c>file_name</c> that comes back, and write a different field to disk.</b>
    /// The server renames the upload to <c>&lt;name&gt; [timestamp]&lt;ext&gt;</c>, and a file
    /// written under that name is invisible to the emulator.
    /// <para>
    /// <b>A 409 is not a failure to retry.</b> It fires when <b>this device's</b> sync record
    /// is stale for the slot, not when the save is newest overall, so the device that uploaded
    /// the current save may upload again while one that never synced it is refused. Retrying
    /// with <c>overwrite=true</c> is only correct after the user has resolved it.
    /// </para>
    /// <para>
    /// <c>emulator</c> is not a free-form label: the server writes it into the stored save's
    /// <c>file_path</c> as a directory segment.
    /// </para>
    /// </remarks>
    /// <param name="fileName">The name to send. The server replaces it and tells you what with.</param>
    /// <param name="content">The bytes. Capped at 512 MiB server-side.</param>
    /// <param name="overwrite">Only after a conflict has been resolved.</param>
    public async Task<RomMResponse<SaveUploadResult>> UploadSaveAsync(
        int romId,
        string slot,
        string emulator,
        string deviceId,
        int? sessionId,
        string fileName,
        Stream content,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slot);
        ArgumentException.ThrowIfNullOrWhiteSpace(emulator);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);

        if (!IsAuthenticated)
        {
            return RomMResponse.Failure<SaveUploadResult>(
                RomMResponseStatus.Unauthorized,
                "No access token is stored. Pair first.");
        }

        var query = new List<string>
        {
            "rom_id=" + romId.ToString(CultureInfo.InvariantCulture),
            "slot=" + Uri.EscapeDataString(slot),
            "emulator=" + Uri.EscapeDataString(emulator),
            "device_id=" + Uri.EscapeDataString(deviceId),
            "overwrite=" + (overwrite ? "true" : "false"),
            "autocleanup=true",
            "autocleanup_limit=" + AutoCleanupLimit.ToString(CultureInfo.InvariantCulture),
        };

        if (sessionId is { } session)
        {
            query.Add("session_id=" + session.ToString(CultureInfo.InvariantCulture));
        }

        using var form = new MultipartFormDataContent();
        using var file = new StreamContent(content);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        // A battery save has no screenshot to send. The screenshotFile half of this endpoint
        // belongs to states, and UploadStateAsync is where it is used.
        form.Add(file, "saveFile", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, Resolve("api/saves?" + string.Join('&', query)))
        {
            Content = form,
        };

        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var detail = await ReadDetailAsync(response, cancellationToken).ConfigureAwait(false);
            return RomMResponse.Success(new SaveUploadResult(null, true, detail));
        }

        if (response.IsSuccessStatusCode)
        {
            var row = await ReadAsync<SaveRow>(response, cancellationToken).ConfigureAwait(false);
            return RomMResponse.Success(new SaveUploadResult(row, false, null));
        }

        return await FailureAsync<SaveUploadResult>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Streams one save down. Needs <c>assets.read</c>.
    /// </summary>
    /// <remarks>
    /// <b><c>optimistic=false</c> is not optional and the ack is not decoration.</b> The
    /// parameter defaults to true and records the device as current <b>on the request</b>:
    /// measured, a device that had never synced a save went from <c>is_current: false</c> to
    /// <c>true</c> by issuing the GET and nothing else. A transfer that dies mid-body then
    /// leaves the server believing the device holds a save it does not, the next negotiate
    /// answers <c>no_op</c> for that slot, and the save never comes down again, silently.
    /// <para>
    /// So the two travel together: this passes <c>optimistic=false</c>, and
    /// <see cref="AcknowledgeSaveAsync"/> is called only after the bytes are written and
    /// verified. Same discipline as M3's <c>.part</c> rename.
    /// </para>
    /// </remarks>
    public async Task<RomMResponse<long>> DownloadSaveAsync(
        int saveId,
        string deviceId,
        int? sessionId,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (!IsAuthenticated)
        {
            return RomMResponse.Failure<long>(RomMResponseStatus.Unauthorized, "No access token is stored. Pair first.");
        }

        var query = new List<string>
        {
            "device_id=" + Uri.EscapeDataString(deviceId),
            "optimistic=false",
        };

        if (sessionId is { } session)
        {
            query.Add("session_id=" + session.ToString(CultureInfo.InvariantCulture));
        }

        // Built from the id rather than from the row's download_path, which is not a usable
        // URL: it is served with a raw space and an unencoded + in its query string.
        var path = string.Create(
            CultureInfo.InvariantCulture,
            $"api/saves/{saveId}/content?{string.Join('&', query)}");

        using var request = new HttpRequestMessage(HttpMethod.Get, Resolve(path));
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return await FailureAsync<long>(response, cancellationToken).ConfigureAwait(false);
        }

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var written = await CopyAsync(body, destination, cancellationToken).ConfigureAwait(false);

        return RomMResponse.Success(written);
    }

    /// <summary>
    /// Tells the server the bytes really arrived. Needs <c>assets.read</c>.
    /// </summary>
    /// <remarks>
    /// Called after the file is written and its hash checked, never after the response headers.
    /// Without <c>optimistic=false</c> on the download this call changes nothing, because the
    /// record is already written by the time it is made.
    /// </remarks>
    public Task<RomMResponse<bool>> AcknowledgeSaveAsync(
        int saveId,
        string deviceId,
        CancellationToken cancellationToken = default) =>
        PostAuthenticatedAsync<AcknowledgeBody, bool>(
            string.Create(CultureInfo.InvariantCulture, $"api/saves/{saveId}/downloaded"),
            new AcknowledgeBody(deviceId),
            cancellationToken,
            emptyBodyValue: true);

    /// <summary>Fetches one save row, which is how a 409 gets something to show the user.</summary>
    public Task<RomMResponse<IReadOnlyList<SaveRow>>> ListSavesAsync(
        int romId,
        string deviceId,
        CancellationToken cancellationToken = default) =>
        GetAuthenticatedAsync<IReadOnlyList<SaveRow>>(
            string.Create(
                CultureInfo.InvariantCulture,
                $"api/saves?rom_id={romId}&device_id={Uri.EscapeDataString(deviceId)}"),
            cancellationToken);

    /// <summary>Closes a negotiate session. Needs <c>assets.write</c>.</summary>
    public Task<RomMResponse<bool>> CompleteSyncSessionAsync(
        int sessionId,
        int completed,
        int failed,
        IReadOnlyList<PlaySessionEntry>? playSessions = null,
        CancellationToken cancellationToken = default) =>
        PostAuthenticatedAsync<CompleteBody, bool>(
            string.Create(CultureInfo.InvariantCulture, $"api/sync/sessions/{sessionId}/complete"),
            new CompleteBody(completed, failed, playSessions ?? []),
            cancellationToken,
            emptyBodyValue: true);

    /// <summary>
    /// Sends play sessions through their own ingest. Needs <c>roms.user.write</c>.
    /// </summary>
    /// <remarks>
    /// <b>This endpoint stands alone</b>, so playtime does not wait on a save negotiation.
    /// That matters for the agent woken by <c>game-end</c> with nothing to negotiate: routing
    /// playtime only through <c>/complete</c> would couple two things the server does not.
    /// <para>
    /// At most <see cref="PlaySessionBatchLimit"/> per call, enforced with a 400, and
    /// <c>end_time</c> must be strictly after <c>start_time</c>, enforced with a 422.
    /// </para>
    /// </remarks>
    public Task<RomMResponse<PlaySessionOutcome>> SendPlaySessionsAsync(
        PlaySessionBatch batch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (batch.Sessions.Count > PlaySessionBatchLimit)
        {
            return Task.FromResult(RomMResponse.Failure<PlaySessionOutcome>(
                RomMResponseStatus.ServerError,
                $"A play-session batch holds at most {PlaySessionBatchLimit} entries, and this one holds "
                    + $"{batch.Sessions.Count}. Split it."));
        }

        return PostAuthenticatedAsync<PlaySessionBatch, PlaySessionOutcome>(
            "api/play-sessions",
            batch,
            cancellationToken);
    }

    /// <summary>The server's own cap, measured: 101 entries answer 400.</summary>
    public const int PlaySessionBatchLimit = 100;

    private async Task<RomMResponse<TResult>> PostAuthenticatedAsync<TBody, TResult>(
        string path,
        TBody body,
        CancellationToken cancellationToken,
        TResult? emptyBodyValue = default)
    {
        if (!IsAuthenticated)
        {
            return RomMResponse.Failure<TResult>(
                RomMResponseStatus.Unauthorized,
                "No access token is stored. Pair first.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, Resolve(path))
        {
            Content = JsonContent.Create(body, options: SerializerOptions),
        };

        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return await FailureAsync<TResult>(response, cancellationToken).ConfigureAwait(false);
        }

        // Some of these routes answer with a body worth reading and some answer 200 with
        // nothing. A caller that only needs "it worked" passes the value to hand back.
        if (emptyBodyValue is not null)
        {
            return RomMResponse.Success(emptyBodyValue);
        }

        var value = await ReadAsync<TResult>(response, cancellationToken).ConfigureAwait(false);
        return RomMResponse.Success(value!);
    }

    private static async Task<RomMResponse<T>> FailureAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var detail = await ReadDetailAsync(response, cancellationToken).ConfigureAwait(false);

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => RomMResponse.Failure<T>(
                RomMResponseStatus.Unauthorized,
                detail ?? "The stored token is expired or revoked. Pair again."),
            HttpStatusCode.Forbidden => RomMResponse.Failure<T>(
                RomMResponseStatus.Forbidden,
                detail ?? "The stored token was not granted the scope this needs."),
            _ => RomMResponse.Failure<T>(
                RomMResponseStatus.ServerError,
                detail ?? $"The server answered {(int)response.StatusCode}."),
        };
    }

    private static async Task<long> CopyAsync(Stream source, Stream destination, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        var total = 0L;

        while (true)
        {
            int read;

            try
            {
                read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException)
            {
                // A link that dropped mid-body. This is precisely the case optimistic=false
                // exists for: the server has not recorded the device as current, so the next
                // negotiate still offers the save.
                throw RomMTransportErrors.Classify(ex, null, cancellationToken);
            }

            if (read == 0)
            {
                return total;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            total += read;
        }
    }

    private sealed record AcknowledgeBody(
        [property: System.Text.Json.Serialization.JsonPropertyName("device_id")] string DeviceId);

    private sealed record CompleteBody(
        [property: System.Text.Json.Serialization.JsonPropertyName("operations_completed")] int OperationsCompleted,
        [property: System.Text.Json.Serialization.JsonPropertyName("operations_failed")] int OperationsFailed,
        [property: System.Text.Json.Serialization.JsonPropertyName("play_sessions")] IReadOnlyList<PlaySessionEntry> PlaySessions);
}
