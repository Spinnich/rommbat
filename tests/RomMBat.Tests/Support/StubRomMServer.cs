using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace RomMBat.Tests.Support;

/// <summary>One ROM in the stub library, carrying only what the slim row reads.</summary>
/// <param name="SizeBytes">
/// A <see cref="long"/> deliberately, so a test can serve a 4 GB ISO. The generated
/// <c>SimpleRomSchema</c> holds this as an <see cref="int"/> and would fail on one.
/// </param>
internal sealed record StubRom(
    int Id,
    int PlatformId,
    string PlatformSlug,
    string PlatformFsSlug,
    string Name,
    string FsName,
    string Extension,
    long SizeBytes,
    string UpdatedAt = "2026-01-01T00:00:00")
{
    /// <summary>
    /// The md5 of the ROM's <b>uncompressed</b> content, as RomM reports it.
    /// </summary>
    /// <remarks>
    /// For an archived ROM this is the hash of the file inside the archive and not of the bytes
    /// the server sends, which is the trap the real API sets and the client has to survive.
    /// </remarks>
    public string? Md5Hash { get; init; }

    public string? Sha1Hash { get; init; }

    /// <summary>True to serve this ROM the way RomM serves a multi-file one: no ranges, ever.</summary>
    public bool HasMultipleFiles { get; init; }

    /// <summary>
    /// The gamelist metadata the paged read carries, or null for a ROM with none.
    /// </summary>
    /// <remarks>
    /// On the row rather than behind a second endpoint, because that is where the real API
    /// puts it: <c>SimpleRomSchema</c> carries <c>metadatum</c>, <c>summary</c> and every media
    /// path, so reading them costs no request and a test that stubbed them elsewhere would be
    /// testing a client nobody ships.
    /// </remarks>
    public StubRomMetadata? Metadata { get; init; }
}

/// <summary>What a ROM row carries beyond what sync-set resolution reads.</summary>
/// <remarks>
/// The defaults are the shapes measured on a live instance, traps included: companies
/// alphabetically sorted with both roles merged, a release date in <b>milliseconds</b>, a
/// rating out of <b>100</b>, and media paths in the two different shapes RomM emits.
/// </remarks>
internal sealed record StubRomMetadata
{
    public string? Summary { get; init; } = "A game the stub library holds.";

    public IReadOnlyList<string> Companies { get; init; } = ["Nintendo"];

    public IReadOnlyList<string> Genres { get; init; } = ["Platform"];

    public IReadOnlyList<string> Franchises { get; init; } = [];

    public string PlayerCount { get; init; } = "1-2";

    /// <summary>1994-09-16, in milliseconds.</summary>
    public long? FirstReleaseDate { get; init; } = 779_673_600_000;

    /// <summary>Out of 100.</summary>
    public double? AverageRating { get; init; } = 82.5;

    public IReadOnlyList<string> Regions { get; init; } = ["USA"];

    public IReadOnlyList<string> Languages { get; init; } = ["English"];

    /// <summary>Already rooted at the asset prefix, and carrying the raw-space query.</summary>
    public string? CoverLargePath { get; init; } = "/assets/romm/resources/roms/1/{id}/cover/big.png?ts=2026-07-21 18:07:17";

    public string? CoverSmallPath { get; init; } = "/assets/romm/resources/roms/1/{id}/cover/small.png";

    /// <summary>Relative to the prefix, which is the shape that answers 200 with a web page if used as given.</summary>
    public string? VideoPath { get; init; } = "roms/1/{id}/video/video.mp4";

    public string? ManualPath { get; init; }

    public string? LogoPath { get; init; } = "roms/1/{id}/logo/logo.png";
}

/// <summary>One platform in the stub library.</summary>
internal sealed record StubPlatform(int Id, string Slug, string FsSlug, string Name)
{
    /// <summary>The firmware records RomM inlines on this platform.</summary>
    public IReadOnlyList<StubFirmware> Firmware { get; init; } = [];
}

/// <summary>
/// One firmware record, shaped like the live server's.
/// </summary>
/// <remarks>
/// <see cref="FileName"/> defaults to something RetroBat does not use and
/// <see cref="IsVerified"/> to false, because those are the two traps M5 exists to survive: a
/// join on either would throw away files the emulator needs.
/// </remarks>
internal sealed record StubFirmware(int Id, string FileName, byte[] Bytes)
{
#pragma warning disable CA5351 // MD5, deliberately: it is what RomM publishes and RetroBat requires.
    public string Md5Hash => Convert.ToHexString(System.Security.Cryptography.MD5.HashData(Bytes)).ToLowerInvariant();
#pragma warning restore CA5351

    public bool IsVerified { get; init; }

    /// <summary>The row outlived the file. Its content route answers 500, as measured.</summary>
    public bool MissingFromFs { get; init; }
}

/// <summary>
/// A stand-in RomM server that can be switched to unreachable mid-operation.
/// </summary>
/// <remarks>
/// This is the harness `docs/ARCHITECTURE.md` calls the highest-value suite. Being offline
/// is the normal case for this app, so the interesting behaviour is what happens when the
/// server disappears partway through something, and that cannot be tested against a server
/// that is always up.
/// <para>
/// When unreachable it throws exactly what <see cref="SocketsHttpHandler"/> throws on a
/// connect timeout: a <see cref="TaskCanceledException"/> wrapping a
/// <see cref="TimeoutException"/>. That shape is the whole point, because M0 probe 6b
/// measured it as indistinguishable by type from a user cancellation.
/// </para>
/// </remarks>
internal sealed partial class StubRomMServer : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _tokenResponses = new();
    private readonly List<string> _requestLog = [];
    private readonly List<string> _queryLog = [];

    /// <summary>Flip to false to make every subsequent call fail as a connect timeout.</summary>
    public bool IsReachable { get; set; } = true;

    /// <summary>
    /// Flip to true to answer every authenticated call 401, as a revoked or expired token does.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="IsReachable"/> because the two need opposite responses from
    /// the app: an unreachable server is a working state that queues and retries, and a
    /// rejected one is an identity change where retrying sends the same refused token. The
    /// heartbeat and the pairing endpoints are left answering, since neither carries the token
    /// being refused.
    /// </remarks>
    public bool RejectsToken { get; set; }

    /// <summary>What <c>GET /api/heartbeat</c> reports as <c>SYSTEM.VERSION</c>.</summary>
    public string ServerVersion { get; set; } = "5.2.0";

    /// <summary>The <c>Date</c> header the server sends, which is the only clock reference.</summary>
    public DateTimeOffset? ServerDate { get; set; }

    /// <summary>The user code <c>init</c> hands out.</summary>
    public string UserCode { get; set; } = "K7M2PQRS";

    /// <summary>Seconds until the pending state lapses. The server's hard ceiling is 600.</summary>
    public int ExpiresIn { get; set; } = 600;

    /// <summary>Seconds between polls the server asks for.</summary>
    public int Interval { get; set; } = 5;

    /// <summary>What <c>GET /api/devices</c> answers with.</summary>
    public HttpStatusCode DevicesStatus { get; set; } = HttpStatusCode.OK;

    /// <summary>The device ids <c>GET /api/devices</c> returns.</summary>
    public IList<string> DeviceIds { get; } = [];

    /// <summary>Every path this handler was asked for, in order.</summary>
    public IReadOnlyList<string> RequestLog => _requestLog;

    /// <summary>Every full URI, in order, so a test can assert on query parameters.</summary>
    public IReadOnlyList<string> QueryLog => _queryLog;

    /// <summary>
    /// The ROMs <c>GET /api/roms</c> serves, in id order.
    /// </summary>
    /// <remarks>
    /// Paged with <c>limit</c> and <c>offset</c> and filtered by <c>platform_ids</c> and
    /// <c>search_term</c>, which is enough to exercise resumption and scope selection. The
    /// sidecars are never included, which is also what a test asserts about the client.
    /// </remarks>
    public IList<StubRom> Library { get; } = [];

    /// <summary>What <c>GET /api/platforms</c> answers with.</summary>
    public IList<StubPlatform> Platforms { get; } = [];

    /// <summary>
    /// Media files, keyed by their full path under <c>/assets/romm/resources/</c>.
    /// </summary>
    /// <remarks>
    /// A path that is not here answers 404. That is deliberately not what the real server does
    /// for a <b>prefix-less</b> path, which answers 200 with the web UI's page; that case is
    /// covered where the client is tested directly, because reproducing it here would mean the
    /// stub answering 200 to everything.
    /// </remarks>
    public IDictionary<string, byte[]> Media { get; } = new Dictionary<string, byte[]>(StringComparer.Ordinal);

    /// <summary>Every media path requested, in order, so a test can count them.</summary>
    public IList<string> AssetRequests { get; } = [];

    /// <summary>Every firmware id whose content was requested, in order.</summary>
    public IList<int> FirmwareRequests { get; } = [];

    /// <summary>Bytes to serve instead of a record's own, for testing what a wrong file does.</summary>
    public IDictionary<int, byte[]> FirmwareBodyOverrides { get; } = new Dictionary<int, byte[]>();

    /// <summary>
    /// Serves media with no <c>Content-Length</c>, the way a chunked response arrives.
    /// </summary>
    /// <remarks>
    /// nginx sends the header on every measured path, so this is the case the budget's
    /// pre-flight cannot see coming rather than one the real server produces.
    /// </remarks>
    public bool MediaWithoutLength { get; set; }

    /// <summary>The <c>sync_config</c> last written by <c>PUT /api/devices/{id}</c>.</summary>
    public JsonElement? StoredSyncConfig { get; set; }

    /// <summary>Fails the next <c>GET /api/roms</c> with this status, once.</summary>
    public HttpStatusCode? NextRomsStatus { get; set; }

    /// <summary>
    /// Serves this many pages of <c>GET /api/roms</c> and then fails one, once.
    /// </summary>
    /// <remarks>
    /// A walk that dies at its first request never had anything to resume. Interrupting it
    /// partway is what exercises the resume path.
    /// </remarks>
    public int? FailRomsAfterPages { get; set; }

    /// <summary>
    /// Reports this as <c>total</c> instead of the real match count.
    /// </summary>
    /// <remarks>
    /// Lets a three-row stub claim to be an 83,000 ROM library, which is how the refusal of
    /// an uncapped scope is tested without building one.
    /// </remarks>
    public int? TotalOverride { get; set; }

    /// <summary>
    /// Serves this many single-ROM fetches and then answers every later one with this status.
    /// </summary>
    /// <remarks>
    /// Not once, unlike the paged knobs: a hydrate that meets a 500 must stop on it, and a
    /// stub that recovered on the next id would pass whether it stopped or not.
    /// </remarks>
    public (int After, HttpStatusCode Status)? FailRomByIdAfter { get; set; }

    /// <summary>How many single-ROM fetches were served, which is a picked set's only route.</summary>
    public int RomsById { get; private set; }

    /// <summary>How many pages of <c>/api/roms</c> were served.</summary>
    public int RomPagesServed { get; private set; }

    /// <summary>
    /// Holds every <c>/api/roms</c> page open until it is completed.
    /// </summary>
    /// <remarks>
    /// <b>For a test about two requests racing, which otherwise cannot be written.</b> This stub
    /// answers in microseconds, so a fetch started and a second press issued on the same thread
    /// are never actually in flight together and an assertion about the guard between them
    /// passes with the guard deleted. Setting this makes the first page wait, so the second
    /// caller is genuinely racing it.
    /// </remarks>
    public TaskCompletionSource? HoldRomPages { get; set; }

    /// <summary>
    /// How many <c>/api/roms</c> requests have arrived, counted before the hold rather than
    /// after it.
    /// </summary>
    /// <remarks>
    /// <see cref="RomPagesServed"/> counts answers, so two requests released together race each
    /// other to increment it and a test asserting on it while they unwind is not deterministic.
    /// This counts arrivals, which is the question "did a second fetch start" actually asks, and
    /// it can be read while the hold is still on.
    /// </remarks>
    public int RomPagesRequested { get; private set; }

    /// <summary>The bytes each ROM's content endpoint serves, by ROM id.</summary>
    public IDictionary<int, byte[]> Content { get; } = new Dictionary<int, byte[]>();

    /// <summary>Every content request, as <c>&lt;rom id&gt; &lt;range or "-"&gt;</c>.</summary>
    public IList<string> ContentRequests { get; } = [];

    /// <summary>
    /// Cuts the body off after this many bytes, once.
    /// </summary>
    /// <remarks>
    /// A dropped link mid-transfer, which is the case the resume path exists for and cannot be
    /// exercised against a server that always finishes.
    /// </remarks>
    public int? DropContentAfterBytes { get; set; }

    /// <summary>What the content endpoint reports as its <c>ETag</c>. Change it to go stale.</summary>
    public string ContentETag { get; set; } = "\"6a45147a-1009\"";

    /// <summary>What <c>GET /api/roms/identifiers</c> answers with. 504 is what a real one did.</summary>
    public HttpStatusCode IdentifiersStatus { get; set; } = HttpStatusCode.OK;

    /// <summary>How many times the token endpoint was polled.</summary>
    public int TokenPolls { get; private set; }

    /// <summary>Queues one <c>authorization_pending</c> answer.</summary>
    public StubRomMServer ThenPending() => ThenDetail(HttpStatusCode.BadRequest, "authorization_pending");

    /// <summary>Queues one <c>slow_down</c> answer.</summary>
    public StubRomMServer ThenSlowDown() => ThenDetail(HttpStatusCode.BadRequest, "slow_down");

    /// <summary>Queues one <c>access_denied</c> answer.</summary>
    public StubRomMServer ThenDenied() => ThenDetail(HttpStatusCode.BadRequest, "access_denied");

    /// <summary>Queues one <c>expired_token</c> answer.</summary>
    public StubRomMServer ThenExpired() => ThenDetail(HttpStatusCode.BadRequest, "expired_token");

    /// <summary>Queues one 429, which the token endpoint returns past 60 polls a minute.</summary>
    public StubRomMServer ThenRateLimited() =>
        ThenDetail(HttpStatusCode.TooManyRequests, "Too many polling attempts. Try again later.");

    /// <summary>Queues an approval carrying exactly these scopes.</summary>
    public StubRomMServer ThenApproved(
        IEnumerable<string> scopes,
        string deviceId = "device-1",
        DateTimeOffset? expiresAt = null)
    {
        var granted = scopes.ToArray();
        _tokenResponses.Enqueue(() => Json(
            HttpStatusCode.OK,
            new
            {
                access_token = "rmm_" + new string('a', 64),
                device_id = deviceId,
                scopes = granted,
                expires_at = expiresAt?.ToUniversalTime().ToString("O"),
            }));

        return this;
    }

    /// <summary>Makes the server unreachable for the next <paramref name="count"/> calls.</summary>
    public StubRomMServer ThenUnreachable(int count = 1)
    {
        for (var i = 0; i < count; i++)
        {
            _tokenResponses.Enqueue(() => throw Timeout());
        }

        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        await Task.Yield();

        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        _requestLog.Add(path);
        _queryLog.Add(request.RequestUri?.ToString() ?? string.Empty);

        // Checked before the token queue so a mid-flight switch takes effect immediately,
        // which is the case this whole class exists to exercise.
        if (!IsReachable)
        {
            throw Timeout();
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (path.EndsWith("/api/heartbeat", StringComparison.Ordinal))
        {
            return Heartbeat();
        }

        if (RejectsToken && !path.Contains("/api/auth/", StringComparison.Ordinal))
        {
            return Detail(HttpStatusCode.Unauthorized, "Not authenticated");
        }

        if (path.EndsWith("/api/auth/device/init", StringComparison.Ordinal))
        {
            return Json(
                HttpStatusCode.Created,
                new
                {
                    device_code = new string('d', 64),
                    user_code = UserCode,
                    verification_path = "/pair/device",
                    verification_path_complete = $"/pair/device?user_code={UserCode}",
                    expires_in = ExpiresIn,
                    interval = Interval,
                });
        }

        if (path.EndsWith("/api/auth/device/token", StringComparison.Ordinal))
        {
            TokenPolls++;
            return _tokenResponses.Count > 0
                ? _tokenResponses.Dequeue()()
                : Detail(HttpStatusCode.BadRequest, "authorization_pending");
        }

        if (path.EndsWith("/api/devices", StringComparison.Ordinal))
        {
            if (DevicesStatus != HttpStatusCode.OK)
            {
                return Detail(DevicesStatus, "nope");
            }

            return Json(HttpStatusCode.OK, DeviceIds.Select(id => new { id, name = "stub", user_id = 1 }).ToArray());
        }

        if (path.Contains("/api/devices/", StringComparison.Ordinal))
        {
            return await DeviceAsync(request, cancellationToken).ConfigureAwait(false);
        }

        if (path.EndsWith("/api/platforms", StringComparison.Ordinal))
        {
            return Json(HttpStatusCode.OK, Platforms.Select(platform => new
            {
                id = platform.Id,
                slug = platform.Slug,
                fs_slug = platform.FsSlug,
                name = platform.Name,
                display_name = platform.Name,
                rom_count = Library.Count(rom => rom.PlatformId == platform.Id),
                firmware_count = platform.Firmware.Count,
                firmware = platform.Firmware.Select(firmware => new
                {
                    id = firmware.Id,
                    file_name = firmware.FileName,
                    file_size_bytes = firmware.Bytes.Length,
                    md5_hash = firmware.Md5Hash,
                    is_verified = firmware.IsVerified,
                    missing_from_fs = firmware.MissingFromFs,
                }).ToArray(),
            }).ToArray());
        }

        if (path.EndsWith("/api/roms/identifiers", StringComparison.Ordinal))
        {
            return IdentifiersStatus == HttpStatusCode.OK
                ? Json(HttpStatusCode.OK, Library.Select(rom => rom.Id).ToArray())
                : Detail(IdentifiersStatus, "gateway timeout");
        }

        if (path.EndsWith("/api/roms/by-hash", StringComparison.Ordinal))
        {
            return ByHash(request.RequestUri);
        }

        // Saves, sync sessions and play sessions live in the other half of this class.
        if (IsSaveRoute(path))
        {
            return await SaveRouteAsync(request, path, cancellationToken).ConfigureAwait(false);
        }

        // States are a third half, because they share none of the save protocol.
        if (IsStateRoute(path))
        {
            return await StateRouteAsync(request, path, cancellationToken).ConfigureAwait(false);
        }

        if (path.StartsWith("/assets/romm/resources/", StringComparison.Ordinal))
        {
            return Asset(path);
        }

        if (path.Contains("/api/firmware/", StringComparison.Ordinal))
        {
            return FirmwareResponse(path);
        }

        if (path.Contains("/content/", StringComparison.Ordinal))
        {
            return ContentResponse(request, path);
        }

        if (path.EndsWith("/api/roms", StringComparison.Ordinal))
        {
            RomPagesRequested++;

            if (HoldRomPages is { } held)
            {
                await held.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            return Roms(request.RequestUri);
        }

        // One ROM by id, which is the only way a picked set resolves: the paged route takes no
        // id-list parameter, so a set that is a list of ids has no query behind it.
        if (RomById(path) is { } byId)
        {
            return byId;
        }

        return Detail(HttpStatusCode.NotFound, "Not Found");
    }

    private async Task<HttpResponseMessage> DeviceAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Put)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(body);

            StoredSyncConfig = document.RootElement.TryGetProperty("sync_config", out var config)
                ? config.Clone()
                : null;
        }

        var id = request.RequestUri!.AbsolutePath.Split('/')[^1];
        return Json(HttpStatusCode.OK, new
        {
            id,
            name = "stub",
            user_id = 1,
            sync_config = StoredSyncConfig,
        });
    }

    /// <summary>
    /// Serves firmware content the way the measured server does.
    /// </summary>
    /// <remarks>
    /// Two behaviours are copied from a live instance rather than invented. The file name in
    /// the URL is <b>not read</b>: the right id under any name serves the bytes. And a record
    /// flagged <c>missing_from_fs</c> answers <b>500</b> with a bare "Internal Server Error"
    /// rather than 404, which is why a plan must skip such a record instead of promising it.
    /// </remarks>
    private HttpResponseMessage FirmwareResponse(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var index = Array.IndexOf(segments, "content");
        if (index <= 0 || !int.TryParse(segments[index - 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
        {
            return Detail(HttpStatusCode.NotFound, "Not Found");
        }

        FirmwareRequests.Add(id);

        var firmware = Platforms.SelectMany(platform => platform.Firmware).FirstOrDefault(row => row.Id == id);
        if (firmware is null)
        {
            return Detail(HttpStatusCode.NotFound, "Not Found");
        }

        if (firmware.MissingFromFs)
        {
            return new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Internal Server Error", Encoding.UTF8, "text/plain"),
            };
        }

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(FirmwareBodyOverrides.TryGetValue(id, out var override_)
                ? override_
                : firmware.Bytes),
        };

        // Guessed from the extension by the real server, so a .rom arrives as text/plain and a
        // content-type check must not demand octet-stream.
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            firmware.FileName.EndsWith(".rom", StringComparison.OrdinalIgnoreCase) ? "text/plain" : "application/octet-stream");

        return response;
    }

    /// <summary>
    /// Serves ROM content the way the measured server does, traps included.
    /// </summary>
    /// <remarks>
    /// Three behaviours are copied from a live instance rather than invented. A <c>Range</c>
    /// header on a multi-file ROM is refused <b>403</b>, in every form. A single-file request
    /// answers <b>206</b> with a <c>Content-Range</c> and an <c>ETag</c>. And a stale
    /// <c>If-Range</c> answers <b>200</b> with the whole body rather than splicing, which is
    /// what makes resuming safe at all.
    /// </remarks>
    private HttpResponseMessage ContentResponse(HttpRequestMessage request, string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var index = Array.IndexOf(segments, "content");
        if (index <= 0 || !int.TryParse(segments[index - 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var romId))
        {
            return Detail(HttpStatusCode.NotFound, "Not Found");
        }

        var range = request.Headers.Range;
        var validator = request.Headers.TryGetValues("If-Range", out var supplied) ? supplied.First() : null;

        // The validator is recorded, not just acted on: a resume that never sends one still gets
        // an ordinary 206 here, so a test that only checks the bytes cannot tell the difference.
        ContentRequests.Add($"{romId} {range?.ToString() ?? "-"} if-range={validator ?? "-"}");

        var rom = Library.FirstOrDefault(candidate => candidate.Id == romId);
        if (rom is null || !Content.TryGetValue(romId, out var body))
        {
            return Detail(HttpStatusCode.NotFound, "Not Found");
        }

        // nginx refuses a ranged request for a built-on-demand zip with its own error page,
        // not with a RomM error body. Measured for bytes=0-, a bounded range and a mid-file one.
        if (rom.HasMultipleFiles && range is not null)
        {
            return new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent(
                    "<html>\r\n<head><title>403 Forbidden</title></head>\r\n<body>\r\n</body>\r\n</html>\r\n",
                    Encoding.UTF8,
                    new System.Net.Http.Headers.MediaTypeHeaderValue("text/html")),
            };
        }

        var from = range?.Ranges.FirstOrDefault()?.From ?? 0;
        var stale = validator is not null && !string.Equals(validator, ContentETag, StringComparison.Ordinal);

        if (stale || from == 0 || range is null)
        {
            // Includes the stale-validator case: the whole body, and the caller is expected to
            // throw away whatever it had.
            return Body(body, 0, partial: range is not null && !stale && from > 0);
        }

        return from >= body.Length
            ? new HttpResponseMessage(HttpStatusCode.RequestedRangeNotSatisfiable)
            : Body(body, (int)from, partial: true);
    }

    private HttpResponseMessage Body(byte[] body, int from, bool partial)
    {
        var slice = body.AsSpan(from).ToArray();
        var drop = DropContentAfterBytes;

        if (drop is { } cut && cut < slice.Length)
        {
            DropContentAfterBytes = null;

            var response = new HttpResponseMessage(partial ? HttpStatusCode.PartialContent : HttpStatusCode.OK)
            {
                Content = new StreamContent(new FailingStream(slice, cut)),
            };

            response.Headers.TryAddWithoutValidation("ETag", ContentETag);
            response.Content.Headers.ContentLength = slice.Length;
            return response;
        }

        var message = new HttpResponseMessage(partial ? HttpStatusCode.PartialContent : HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(slice),
        };

        message.Headers.TryAddWithoutValidation("ETag", ContentETag);
        message.Headers.AcceptRanges.Add("bytes");

        if (partial)
        {
            message.Content.Headers.ContentRange =
                new System.Net.Http.Headers.ContentRangeHeaderValue(from, body.Length - 1, body.Length);
        }

        return message;
    }

    private HttpResponseMessage ByHash(Uri? uri)
    {
        var query = System.Web.HttpUtility.ParseQueryString(uri?.Query ?? string.Empty);
        var md5 = query["md5_hash"];
        var sha1 = query["sha1_hash"];

        var match = Library.FirstOrDefault(rom =>
            (md5 is not null && string.Equals(rom.Md5Hash, md5, StringComparison.OrdinalIgnoreCase))
            || (sha1 is not null && string.Equals(rom.Sha1Hash, sha1, StringComparison.OrdinalIgnoreCase)));

        return match is null ? Detail(HttpStatusCode.NotFound, "Not Found") : Json(HttpStatusCode.OK, Project(match));
    }

    /// <summary>
    /// Serves a media file the way nginx does, and the web UI's page the way it does too.
    /// </summary>
    /// <remarks>
    /// The second half is the interesting one. A media path used without the asset prefix
    /// answers <b>200</b> with <c>index.html</c> rather than 404, which is why the client has
    /// to check the content type and not the status. Reproduced here so a regression in the
    /// prefix handling shows up as a test failure rather than as a PDF full of HTML.
    /// </remarks>
    private HttpResponseMessage Asset(string path)
    {
        AssetRequests.Add(path);

        if (!Media.TryGetValue(path, out var bytes))
        {
            return Detail(HttpStatusCode.NotFound, "no such asset");
        }

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = MediaWithoutLength
                ? new StreamContent(new UnmeasuredStream(bytes))
                : new ByteArrayContent(bytes),
        };

        response.Content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(ContentTypeFor(path));
        response.Headers.TryAddWithoutValidation("Accept-Ranges", "bytes");
        response.Headers.TryAddWithoutValidation("ETag", $"\"69e6885d-{bytes.Length:x}\"");

        return response;
    }

    private static string ContentTypeFor(string path)
    {
        if (path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
        {
            return "video/mp4";
        }

        return path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf" : "image/png";
    }

    private HttpResponseMessage Roms(Uri? uri)
    {
        if (NextRomsStatus is { } status)
        {
            NextRomsStatus = null;
            return Detail(status, "roms refused");
        }

        if (FailRomsAfterPages is { } after && RomPagesServed >= after)
        {
            FailRomsAfterPages = null;
            return Detail(HttpStatusCode.BadGateway, "roms refused");
        }

        RomPagesServed++;

        var query = System.Web.HttpUtility.ParseQueryString(uri?.Query ?? string.Empty);
        var limit = int.TryParse(query["limit"], out var parsedLimit) ? parsedLimit : 50;
        var offset = int.TryParse(query["offset"], out var parsedOffset) ? parsedOffset : 0;
        var platform = query["platform_ids"];
        var search = query["search_term"];

        var matching = Library
            .Where(rom => platform is null || rom.PlatformId.ToString(CultureInfo.InvariantCulture) == platform)
            .Where(rom => search is null || rom.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(rom => rom.Id)
            .ToList();

        var items = matching.Skip(offset).Take(limit).Select(Project).ToArray();

        return Json(HttpStatusCode.OK, new
        {
            items,
            total = TotalOverride ?? matching.Count,
            limit,
            offset,
        });
    }

    /// <summary>Serves <c>GET /api/roms/{id}</c>, or null when the path is not one.</summary>
    /// <remarks>
    /// Matched last, after every other <c>/api/roms/</c> route, because <c>identifiers</c> and
    /// <c>by-hash</c> sit under the same prefix and a looser match would swallow them.
    /// </remarks>
    private HttpResponseMessage? RomById(string path)
    {
        var marker = path.LastIndexOf("/api/roms/", StringComparison.Ordinal);

        if (marker < 0)
        {
            return null;
        }

        var tail = path[(marker + "/api/roms/".Length)..];

        if (!int.TryParse(tail, NumberStyles.Integer, CultureInfo.InvariantCulture, out var romId))
        {
            return null;
        }

        if (FailRomByIdAfter is { } refusal && RomsById >= refusal.After)
        {
            return Detail(refusal.Status, "rom refused");
        }

        RomsById++;

        return Library.FirstOrDefault(rom => rom.Id == romId) is { } found
            ? Json(HttpStatusCode.OK, Project(found))
            : Detail(HttpStatusCode.NotFound, "Rom not found");
    }

    private static object Project(StubRom rom)
    {
        var meta = rom.Metadata;
        var id = rom.Id.ToString(CultureInfo.InvariantCulture);

        return new
        {
            id = rom.Id,
            platform_id = rom.PlatformId,
            platform_slug = rom.PlatformSlug,
            platform_fs_slug = rom.PlatformFsSlug,
            platform_display_name = rom.PlatformSlug,
            fs_name = rom.FsName,
            fs_extension = rom.Extension,
            fs_size_bytes = rom.SizeBytes,
            md5_hash = rom.Md5Hash,
            sha1_hash = rom.Sha1Hash,
            has_multiple_files = rom.HasMultipleFiles,
            name = rom.Name,
            name_sort_key = rom.Name,
            updated_at = rom.UpdatedAt,
            summary = meta?.Summary,
            regions = meta?.Regions ?? [],
            languages = meta?.Languages ?? [],
            path_cover_large = Personalize(meta?.CoverLargePath, id),
            path_cover_small = Personalize(meta?.CoverSmallPath, id),
            path_video = Personalize(meta?.VideoPath, id),
            path_manual = Personalize(meta?.ManualPath, id),
            ss_metadata = meta is null ? null : new { logo_path = Personalize(meta.LogoPath, id) },
            metadatum = meta is null ? null : new
            {
                rom_id = rom.Id,
                genres = meta.Genres,
                franchises = meta.Franchises,
                companies = meta.Companies,
                player_count = meta.PlayerCount,
                first_release_date = meta.FirstReleaseDate,
                average_rating = meta.AverageRating,
            },
        };
    }

    /// <summary>Puts the ROM id into a media path template, so two ROMs never share a file.</summary>
    private static string? Personalize(string? template, string id) =>
        template?.Replace("{id}", id, StringComparison.Ordinal);

    private static TaskCanceledException Timeout() =>
        new("The request was canceled due to the configured ConnectTimeout.", new TimeoutException());

    private StubRomMServer ThenDetail(HttpStatusCode status, string detail)
    {
        _tokenResponses.Enqueue(() => Detail(status, detail));
        return this;
    }

    private HttpResponseMessage Heartbeat()
    {
        var response = Json(
            HttpStatusCode.OK,
            new
            {
                SYSTEM = new { VERSION = ServerVersion, SHOW_SETUP_WIZARD = false },
            });

        if (ServerDate.HasValue)
        {
            response.Headers.Date = ServerDate.Value;
        }

        return response;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, object body) => new(status)
    {
        Content = JsonContent.Create(body, options: new JsonSerializerOptions(JsonSerializerDefaults.Web)),
    };

    private static HttpResponseMessage Detail(HttpStatusCode status, string detail) =>
        Json(status, new { detail });

    /// <summary>
    /// A body whose length cannot be known before it is read.
    /// </summary>
    /// <remarks>
    /// Unseekable, so <see cref="StreamContent"/> declines to compute a <c>Content-Length</c>
    /// and the response arrives without one.
    /// </remarks>
    private sealed class UnmeasuredStream(byte[] body) : Stream
    {
        private int _position;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var take = Math.Min(count, body.Length - _position);
            Array.Copy(body, _position, buffer, offset, take);
            _position += take;
            return take;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>
    /// A body that hands over some bytes and then dies, the way a dropped link does.
    /// </summary>
    /// <remarks>
    /// A <see cref="StreamContent"/> rather than a custom <see cref="HttpContent"/>, because
    /// the default content-read stream buffers the whole body first: the exception would then
    /// arrive before a single byte reached the caller, and there would be no partial file to
    /// resume, which is the entire thing being tested.
    /// </remarks>
    private sealed class FailingStream(byte[] body, int cut) : Stream
    {
        private int _position;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => body.Length;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= cut)
            {
                throw new IOException("The connection was closed before the whole body arrived.");
            }

            var take = Math.Min(count, cut - _position);
            Array.Copy(body, _position, buffer, offset, take);
            _position += take;
            return take;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
