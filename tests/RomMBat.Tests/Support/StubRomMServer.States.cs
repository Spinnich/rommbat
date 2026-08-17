using System.Globalization;
using System.Net;

namespace RomMBat.Tests.Support;

/// <summary>
/// The save-state half of the stub.
/// </summary>
/// <remarks>
/// <b>Three behaviours are modelled because they were measured against a live RomM, and a stub
/// that smoothed any of them over would let the matching client bug through.</b>
/// <para>
/// <c>POST /api/states</c> is an <b>upsert keyed on <c>(rom_id, file_name)</c></b>: three posts
/// of one name reused one row across two different payloads. The <c>emulator</c> is <b>not</b>
/// part of that key: five posts of one name under five different emulator values also reused one
/// row, overwriting the row's emulator and moving its stored path each time. That second fact is
/// the one this stub exists to enforce, because a client that dropped the scope from the
/// uploaded name would silently lose one of two cores' states and nothing else would notice.
/// </para>
/// <para>
/// And the server <b>does not rename a state</b>, unlike a save, which comes back tagged with
/// its upload timestamp.
/// </para>
/// </remarks>
internal sealed partial class StubRomMServer
{
    /// <summary>States the stub holds, by id.</summary>
    public IDictionary<int, StubState> States { get; } = new Dictionary<int, StubState>();

    /// <summary>Fails the next state upload with this status, once.</summary>
    public HttpStatusCode? FailNextStateUpload { get; set; }

    /// <summary>True when the path is one this half of the stub serves.</summary>
    public static bool IsStateRoute(string path) =>
        path.EndsWith("/api/states", StringComparison.Ordinal)
        || path.EndsWith("/api/states/delete", StringComparison.Ordinal);

    private async Task<HttpResponseMessage> StateRouteAsync(
        HttpRequestMessage request,
        string path,
        CancellationToken cancellationToken)
    {
        if (path.EndsWith("/api/states/delete", StringComparison.Ordinal))
        {
            return Json(HttpStatusCode.OK, new { ok = true });
        }

        if (request.Method == HttpMethod.Get)
        {
            var romId = int.Parse(
                ParseQuery(request.RequestUri).GetValueOrDefault("rom_id", "0"),
                CultureInfo.InvariantCulture);

            return Json(
                HttpStatusCode.OK,
                States.Values.Where(state => state.RomId == romId).Select(Describe).ToArray());
        }

        if (FailNextStateUpload is { } status)
        {
            FailNextStateUpload = null;
            return Detail(status, "the state upload failed");
        }

        var query = ParseQuery(request.RequestUri);
        var uploadRom = int.Parse(query.GetValueOrDefault("rom_id", "0"), CultureInfo.InvariantCulture);
        var emulator = query.GetValueOrDefault("emulator", string.Empty);

        var content = await request.Content!.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var body = ExtractSaveFile(content, out var sentName);
        var screenshot = ExtractScreenshot(content);

        // The measured key: rom_id and file_name, and nothing else. Not the emulator.
        var existing = States.Values.FirstOrDefault(state =>
            state.RomId == uploadRom && string.Equals(state.FileName, sentName, StringComparison.Ordinal));

        var id = existing?.Id ?? (States.Count == 0 ? 700 : States.Keys.Max() + 1);

        var stored = new StubState
        {
            Id = id,
            RomId = uploadRom,

            // Overwritten on every upsert, exactly as the live server did.
            Emulator = emulator,
            FileName = sentName,
            Bytes = body,
            ScreenshotName = screenshot?.Name,
            ScreenshotBytes = screenshot?.Bytes,
            UpdatedAt = ServerDate ?? DateTimeOffset.UnixEpoch,
        };

        States[id] = stored;
        return Json(HttpStatusCode.OK, Describe(stored));
    }

    private static object Describe(StubState state) => new
    {
        id = state.Id,
        rom_id = state.RomId,

        // Not renamed. A save at this point would be "<name> [timestamp]<ext>".
        file_name = state.FileName,
        file_name_no_tags = Path.GetFileNameWithoutExtension(state.FileName),
        file_extension = Path.GetExtension(state.FileName).TrimStart('.'),
        file_size_bytes = state.Bytes.Length,
        emulator = state.Emulator,
        missing_from_fs = false,
        created_at = state.UpdatedAt,
        updated_at = state.UpdatedAt,
        screenshot = state.ScreenshotBytes is null
            ? null
            : new
            {
                id = state.Id + 1000,
                file_name = state.ScreenshotName,
                file_size_bytes = state.ScreenshotBytes.Length,
            },
    };

    /// <summary>
    /// Pulls the optional <c>screenshotFile</c> part out, if there is one.
    /// </summary>
    /// <remarks>
    /// The part name is matched with the quotes optional, because .NET only quotes a
    /// Content-Disposition parameter when the value needs it and <c>screenshotFile</c> does not.
    /// </remarks>
    private static (string Name, byte[] Bytes)? ExtractScreenshot(byte[] content)
    {
        var text = System.Text.Encoding.Latin1.GetString(content);
        var marker = System.Text.RegularExpressions.Regex.Match(text, "name=\"?screenshotFile\"?");

        if (!marker.Success)
        {
            return null;
        }

        const string NameMarker = "filename=";
        var nameStart = text.IndexOf(NameMarker, marker.Index, StringComparison.Ordinal) + NameMarker.Length;
        var quoted = text[nameStart] == '"';
        var nameEnd = quoted ? text.IndexOf('"', nameStart + 1) : text.IndexOf('\r', nameStart);
        var name = text[(quoted ? nameStart + 1 : nameStart)..nameEnd];

        var bodyStart = text.IndexOf("\r\n\r\n", nameEnd, StringComparison.Ordinal) + 4;
        var bodyEnd = text.IndexOf("\r\n--", bodyStart, StringComparison.Ordinal);

        return (name, System.Text.Encoding.Latin1.GetBytes(text[bodyStart..(bodyEnd < 0 ? text.Length : bodyEnd)]));
    }

    /// <summary>A save state as the stub holds it.</summary>
    public sealed record StubState
    {
        public required int Id { get; init; }

        public required int RomId { get; init; }

        public required string Emulator { get; init; }

        public required string FileName { get; init; }

        public required byte[] Bytes { get; init; }

        public string? ScreenshotName { get; init; }

        public byte[]? ScreenshotBytes { get; init; }

        public DateTimeOffset UpdatedAt { get; init; }
    }
}
