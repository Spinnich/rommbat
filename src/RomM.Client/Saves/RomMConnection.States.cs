using System.Globalization;
using System.Net.Http.Headers;
using RomM.Client.Saves;

namespace RomM.Client;

/// <summary>
/// The save-state surface, which is deliberately not the save surface.
/// </summary>
/// <remarks>
/// <b>States are outside the negotiate protocol.</b> <c>POST /api/states</c> takes only
/// <c>rom_id</c> and <c>emulator</c>: no slot, no device, no session, no conflict detection and
/// no content hash on the row that comes back. There is nothing here to negotiate with, so this
/// is a best-effort push tracked locally, and the device that decides whether a state needs
/// sending is the one holding the hash it last sent.
/// </remarks>
public sealed partial class RomMConnection
{
    /// <summary>
    /// Uploads one save state, with its screenshot when there is one. Needs <c>assets.write</c>.
    /// </summary>
    /// <remarks>
    /// <b>This is an upsert keyed on <c>(rom_id, file_name)</c> and nothing else</b>, measured
    /// live: three posts of the same name reused one row across two different payloads, and five
    /// posts of one name under five different <c>emulator</c> values also reused one row, moving
    /// the stored file between directories while the id stayed put. So there is no append to
    /// prune and no <c>autocleanup</c> to ask for, and equally <b>the emulator does not separate
    /// two states</b>: the uploaded name has to carry the local scope or a second core silently
    /// overwrites the first.
    /// <para>
    /// <b>Never pass an <c>&lt;image&gt;</c> blind as the screenshot.</b> DeSmuME declares an
    /// <c>&lt;image&gt;</c> template identical to its <c>&lt;file&gt;</c>, so a caller that
    /// expands both and sends what it finds uploads the state a second time as its own preview.
    /// The caller compares the two paths before it gets here.
    /// </para>
    /// </remarks>
    /// <param name="fileName">The name to send. The server tags it and tells you what it used.</param>
    /// <param name="content">The state bytes. Capped at 512 MiB server-side, like every asset.</param>
    /// <param name="screenshot">
    /// Optional, and absent is ordinary rather than a fault: the mirrored image is written by a
    /// race the emulator can lose.
    /// </param>
    public async Task<RomMResponse<StateRow>> UploadStateAsync(
        int romId,
        string emulator,
        string fileName,
        Stream content,
        (string FileName, Stream Content)? screenshot = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emulator);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);

        if (!IsAuthenticated)
        {
            return RomMResponse.Failure<StateRow>(
                RomMResponseStatus.Unauthorized,
                "No access token is stored. Pair first.");
        }

        var path = string.Create(
            CultureInfo.InvariantCulture,
            $"api/states?rom_id={romId}&emulator={Uri.EscapeDataString(emulator)}");

        using var form = new MultipartFormDataContent();
        using var file = new StreamContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "stateFile", fileName);

        using var image = screenshot is { } shot ? new StreamContent(shot.Content) : null;

        if (image is not null && screenshot is { } present)
        {
            image.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(image, "screenshotFile", present.FileName);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, Resolve(path)) { Content = form };
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return await FailureAsync<StateRow>(response, cancellationToken).ConfigureAwait(false);
        }

        var row = await ReadAsync<StateRow>(response, cancellationToken).ConfigureAwait(false);
        return RomMResponse.Success(row!);
    }

    /// <summary>Lists the states one ROM holds. Needs <c>assets.read</c>.</summary>
    public Task<RomMResponse<IReadOnlyList<StateRow>>> ListStatesAsync(
        int romId,
        CancellationToken cancellationToken = default) =>
        GetAuthenticatedAsync<IReadOnlyList<StateRow>>(
            string.Create(CultureInfo.InvariantCulture, $"api/states?rom_id={romId}"),
            cancellationToken);

    /// <summary>
    /// Deletes states by id. Needs <c>assets.write</c>.
    /// </summary>
    /// <remarks>
    /// The save sibling of this route fails the whole batch with a 404 when one id is already
    /// gone, and nothing suggests this one differs, so callers that cannot re-list immediately
    /// beforehand should send one id at a time.
    /// </remarks>
    public Task<RomMResponse<bool>> DeleteStatesAsync(
        IReadOnlyList<int> stateIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stateIds);

        return PostAuthenticatedAsync<DeleteStatesBody, bool>(
            "api/states/delete",
            new DeleteStatesBody(stateIds),
            cancellationToken,
            emptyBodyValue: true);
    }

    private sealed record DeleteStatesBody(
        [property: System.Text.Json.Serialization.JsonPropertyName("states")] IReadOnlyList<int> States);
}
