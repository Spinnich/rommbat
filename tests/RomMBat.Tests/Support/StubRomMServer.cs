using System.Globalization;
using System.Net;
using System.Net.Http.Json;
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
    string UpdatedAt = "2026-01-01T00:00:00");

/// <summary>One platform in the stub library.</summary>
internal sealed record StubPlatform(int Id, string Slug, string FsSlug, string Name);

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
internal sealed class StubRomMServer : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _tokenResponses = new();
    private readonly List<string> _requestLog = [];
    private readonly List<string> _queryLog = [];

    /// <summary>Flip to false to make every subsequent call fail as a connect timeout.</summary>
    public bool IsReachable { get; set; } = true;

    /// <summary>What <c>GET /api/heartbeat</c> reports as <c>SYSTEM.VERSION</c>.</summary>
    public string ServerVersion { get; set; } = "5.1.0";

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

    /// <summary>The <c>sync_config</c> last written by <c>PUT /api/devices/{id}</c>.</summary>
    public JsonElement? StoredSyncConfig { get; set; }

    /// <summary>Fails the next <c>GET /api/roms</c> with this status, once.</summary>
    public HttpStatusCode? NextRomsStatus { get; set; }

    /// <summary>
    /// Reports this as <c>total</c> instead of the real match count.
    /// </summary>
    /// <remarks>
    /// Lets a three-row stub claim to be an 83,000 ROM library, which is how the refusal of
    /// an uncapped scope is tested without building one.
    /// </remarks>
    public int? TotalOverride { get; set; }

    /// <summary>How many pages of <c>/api/roms</c> were served.</summary>
    public int RomPagesServed { get; private set; }

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
            }).ToArray());
        }

        if (path.EndsWith("/api/roms", StringComparison.Ordinal))
        {
            return Roms(request.RequestUri);
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

    private HttpResponseMessage Roms(Uri? uri)
    {
        if (NextRomsStatus is { } status)
        {
            NextRomsStatus = null;
            return Detail(status, "roms refused");
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

        var items = matching.Skip(offset).Take(limit).Select(rom => new
        {
            id = rom.Id,
            platform_id = rom.PlatformId,
            platform_slug = rom.PlatformSlug,
            platform_fs_slug = rom.PlatformFsSlug,
            platform_display_name = rom.PlatformSlug,
            fs_name = rom.FsName,
            fs_extension = rom.Extension,
            fs_size_bytes = rom.SizeBytes,
            name = rom.Name,
            name_sort_key = rom.Name,
            updated_at = rom.UpdatedAt,
        }).ToArray();

        return Json(HttpStatusCode.OK, new
        {
            items,
            total = TotalOverride ?? matching.Count,
            limit,
            offset,
        });
    }

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
}
