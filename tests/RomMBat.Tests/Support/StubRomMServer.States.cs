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

    /// <summary>Fails every <c>GET /api/states</c> with this status.</summary>
    /// <remarks>
    /// A token whose scopes do not cover the route answers 403 and a broken instance answers 500,
    /// and neither is a reason for the save half of a restore to do nothing.
    /// </remarks>
    public HttpStatusCode? FailStateList { get; set; }

    /// <summary>Fails every state content read with this status.</summary>
    public HttpStatusCode? FailStateDownload { get; set; }

    /// <summary>Fails every screenshot content read with this status.</summary>
    public HttpStatusCode? FailScreenshotDownload { get; set; }

    /// <summary>Screenshot ids served, in order.</summary>
    public IList<int> ScreenshotRequests { get; } = [];

    /// <summary>
    /// Accepts a screenshot and answers as though it was never attached.
    /// </summary>
    /// <remarks>
    /// Stands for any reason the server stores the image and does not link it. The naming reason
    /// is modelled separately and always on, in <see cref="Binds"/>; this switch covers the rest,
    /// so the client's report of it stays tested whatever the name.
    /// </remarks>
    public bool DropScreenshots { get; set; }

    /// <summary>True when the path is one this half of the stub serves.</summary>
    public static bool IsStateRoute(string path) =>
        path.EndsWith("/api/states", StringComparison.Ordinal)
        || path.EndsWith("/api/states/delete", StringComparison.Ordinal)
        || ((path.Contains("/api/states/", StringComparison.Ordinal)
                || path.Contains("/api/screenshots/", StringComparison.Ordinal))
            && path.EndsWith("/content", StringComparison.Ordinal));

    private async Task<HttpResponseMessage> StateRouteAsync(
        HttpRequestMessage request,
        string path,
        CancellationToken cancellationToken)
    {
        if (path.EndsWith("/api/states/delete", StringComparison.Ordinal))
        {
            return Json(HttpStatusCode.OK, new { ok = true });
        }

        // GET /api/screenshots/{id}/content. The id is the one Describe hands out, the state's
        // own id plus 1000.
        if (path.Contains("/api/screenshots/", StringComparison.Ordinal))
        {
            var screenshotId = int.Parse(
                path.Split('/', StringSplitOptions.RemoveEmptyEntries)[^2],
                CultureInfo.InvariantCulture);

            ScreenshotRequests.Add(screenshotId);

            if (FailScreenshotDownload is { } screenshotStatus)
            {
                return Detail(screenshotStatus, "the screenshot could not be read");
            }

            return States.Values.FirstOrDefault(state => state.Id + 1000 == screenshotId) is { ScreenshotBytes: { } image }
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(image) }
                : Detail(HttpStatusCode.NotFound, "no such screenshot");
        }

        // GET /api/states/{id}/content, which is what a restore reads. States carry no hash, so
        // there is nothing for the caller to verify and the stub simply serves the bytes.
        if (path.EndsWith("/content", StringComparison.Ordinal))
        {
            if (FailStateDownload is { } downloadStatus)
            {
                return Detail(downloadStatus, "the state content could not be read");
            }

            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var stateId = int.Parse(segments[^2], CultureInfo.InvariantCulture);

            return States.TryGetValue(stateId, out var held)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(held.Bytes) }
                : Detail(HttpStatusCode.NotFound, "no such state");
        }

        if (request.Method == HttpMethod.Get)
        {
            if (FailStateList is { } listStatus)
            {
                return Detail(listStatus, "the state list could not be read");
            }

            // An absent rom_id means every state, which is how a restore discovers them. Filtering
            // on a defaulted 0 returned nothing and made the unfiltered call look empty.
            var wanted = ParseQuery(request.RequestUri).GetValueOrDefault("rom_id");

            var rows = string.IsNullOrEmpty(wanted)
                ? States.Values
                : States.Values.Where(state =>
                    state.RomId == int.Parse(wanted, CultureInfo.InvariantCulture));

            return Json(HttpStatusCode.OK, rows.Select(Describe).ToArray());
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
            ScreenshotName = DropScreenshots ? null : screenshot?.Name,
            ScreenshotBytes = DropScreenshots ? null : screenshot?.Bytes,
            UpdatedAt = ServerDate ?? DateTimeOffset.UnixEpoch,
        };

        States[id] = stored;
        return Json(HttpStatusCode.OK, Describe(stored));
    }

    private object Describe(StubState state) => new
    {
        id = state.Id,
        rom_id = state.RomId,

        // Not renamed. A save at this point would be "<name> [timestamp]<ext>".
        file_name = state.FileName,
        // <b>RomM strips parenthesised groups as tags, not only bracketed ones.</b> Measured
        // live: "Legend of Zelda, The (USA) (Rev 1) [libretro.nestopia].state1" comes back as
        // "Legend of Zelda, The", losing the region and revision. A stub that echoed the stem
        // would let a caller build a destination the emulator never looks at and still pass.
        file_name_no_tags = StripTags(Path.GetFileNameWithoutExtension(state.FileName)),
        file_extension = Path.GetExtension(state.FileName).TrimStart('.'),
        file_size_bytes = state.Bytes.Length,
        emulator = state.Emulator,
        missing_from_fs = false,
        created_at = state.UpdatedAt,
        updated_at = state.UpdatedAt,
        screenshot = ScreenshotFor(state) is not { } shot
            ? null
            : new
            {
                id = shot.Id + 1000,
                file_name = shot.ScreenshotName,
                file_size_bytes = shot.ScreenshotBytes!.Length,
            },
    };

    /// <summary>
    /// The image RomM's <c>State.screenshot</c> answers with, chosen from every image held for the ROM.
    /// </summary>
    /// <remarks>
    /// <c>get_screenshot</c> filters the ROM's images through <see cref="Binds"/>, ranks an image
    /// whose <c>file_name_no_ext</c> is the state's <c>file_name</c> first, then takes the highest
    /// id. So one image can answer for several states, which is how libretro slot 0's image reaches
    /// every other slot. The holding state's id stands in for the image's.
    /// </remarks>
    private StubState? ScreenshotFor(StubState state) =>
        States.Values
            .Where(held => held.RomId == state.RomId
                && held.ScreenshotBytes is not null
                && Binds(state.FileName, held.ScreenshotName!))
            .OrderBy(held => string.Equals(NoExtension(held.ScreenshotName!), state.FileName, StringComparison.Ordinal) ? 0 : 1)
            .ThenByDescending(held => held.Id)
            .FirstOrDefault();

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

    /// <summary>
    /// Whether RomM's <c>State.screenshot</c> lookup finds this image for this state.
    /// </summary>
    /// <remarks>
    /// The filter half of the lookup, and <see cref="ScreenshotFor"/> is the choice among the
    /// images it lets through. Ported from RomM, not approximated: <c>db_screenshot_handler.get_screenshot</c> filters on
    /// the image's <c>file_name</c> or <c>file_name_no_ext</c> being either the state's
    /// <c>file_name</c> or its <c>file_name_no_ext</c>, and <c>compute_file_name_no_ext</c> strips
    /// <c>\.(([a-z]+\.)*\w+)$</c>. Identical at 5.2.0, 5.3.0-alpha.2 and 5.3.0-alpha.3. The
    /// multi-part half matters: <c>.jst.png</c> is one extension to RomM and <c>.p2s.png</c> is
    /// not, because a digit ends the letters-only group.
    /// </remarks>
    internal static bool Binds(string stateName, string screenshotName)
    {
        string[] state = [stateName, NoExtension(stateName)];

        return state.Contains(screenshotName, StringComparer.Ordinal)
            || state.Contains(NoExtension(screenshotName), StringComparer.Ordinal);
    }

    private static string NoExtension(string name) =>
        System.Text.RegularExpressions.Regex.Replace(name, @"\.(([a-z]+\.)*\w+)$", string.Empty).Trim();

    /// <summary>How RomM derives <c>file_name_no_tags</c>, measured rather than guessed.</summary>
    private static string StripTags(string stem) =>
        System.Text.RegularExpressions.Regex
            .Replace(stem, @"\s*[\(\[][^\)\]]*[\)\]]", string.Empty)
            .Trim();

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
