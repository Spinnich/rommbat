using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RomM.Client.Generated;

namespace RomM.Client;

/// <summary>
/// One configured RomM instance, and every call RomMBat makes against it.
/// </summary>
/// <remarks>
/// The handler is owned here rather than taken from a factory, because
/// <see cref="SocketsHttpHandler.ConnectTimeout"/> has to be set explicitly on every
/// instance and nothing sets it by default. See M0 probe 6b.
/// </remarks>
public sealed partial class RomMConnection : IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly HttpMessageHandler _handler;
    private readonly HttpMessageHandler? _ownedHandler;

    /// <summary>Opens a connection over a handler this instance owns.</summary>
    public RomMConnection(RomMClientOptions options)
        : this(options, CreateHandler(options), ownsHandler: true)
    {
    }

    /// <summary>
    /// Opens a connection over a caller-supplied handler. Tests use this to stand in a stub
    /// that can be switched to unreachable mid-operation.
    /// </summary>
    public RomMConnection(RomMClientOptions options, HttpMessageHandler handler)
        : this(options, handler, ownsHandler: false)
    {
    }

    private RomMConnection(RomMClientOptions options, HttpMessageHandler handler, bool ownsHandler)
    {
        ArgumentNullException.ThrowIfNull(options);

        Options = options;
        _handler = handler;
        _ownedHandler = ownsHandler ? handler : null;
        _http = new HttpClient(handler, disposeHandler: false)
        {
            // Bounds the whole request including the body. ConnectTimeout on the handler is
            // what bounds the handshake, and it is the one that matters for reachability.
            Timeout = options.RequestTimeout,
        };

        _http.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (!string.IsNullOrEmpty(options.AccessToken))
        {
            // An Authorization header means CSRF does not apply, so there is no cookie handling.
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", options.AccessToken);
        }
    }

    /// <summary>The options this connection was opened with.</summary>
    public RomMClientOptions Options { get; }

    /// <summary>True when an access token was supplied, so authenticated calls can be made.</summary>
    public bool IsAuthenticated => !string.IsNullOrEmpty(Options.AccessToken);

    /// <summary>
    /// Builds the handler every connection uses, with the connect timeout set explicitly.
    /// </summary>
    internal static SocketsHttpHandler CreateHandler(RomMClientOptions options) => new()
    {
        ConnectTimeout = options.ConnectTimeout,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        AutomaticDecompression = DecompressionMethods.All,
        // No cookie container: authentication is a Bearer header and CSRF does not apply.
        UseCookies = false,
    };

    /// <summary>
    /// Reachability and version probe. Unauthenticated, so it works before pairing.
    /// </summary>
    /// <exception cref="RomMUnreachableException">The server did not answer.</exception>
    public async Task<ServerProbe> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        using var request = new HttpRequestMessage(HttpMethod.Get, Resolve("api/heartbeat"));
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        var roundTrip = Stopwatch.GetElapsedTime(started);

        await ThrowIfErrorAsync(response, cancellationToken).ConfigureAwait(false);

        var heartbeat = await ReadAsync<HeartbeatResponse>(response, cancellationToken).ConfigureAwait(false);
        var reported = heartbeat?.SYSTEM?.VERSION;

        return new ServerProbe(
            reported,
            RomMServerVersion.Check(reported),
            response.Headers.Date,
            roundTrip,
            heartbeat);
    }

    /// <summary>
    /// Starts a pairing flow. Unauthenticated and rate limited to 10/min/IP.
    /// </summary>
    /// <exception cref="RomMUnreachableException">The server did not answer.</exception>
    /// <exception cref="RomMApiException">The server rejected the request.</exception>
    public async Task<DeviceAuthInitResponse> BeginPairingAsync(
        DeviceAuthInitPayload payload,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Resolve("api/auth/device/init"))
        {
            Content = JsonContent.Create(payload, options: SerializerOptions),
        };

        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        await ThrowIfErrorAsync(response, cancellationToken).ConfigureAwait(false);

        return await ReadAsync<DeviceAuthInitResponse>(response, cancellationToken).ConfigureAwait(false)
            ?? throw new RomMApiException(response.StatusCode, null, "The server returned an empty pairing response.");
    }

    /// <summary>
    /// Polls once for the pairing result.
    /// </summary>
    /// <remarks>
    /// Every documented outcome comes back as HTTP 400 with the reason in <c>detail</c>, so
    /// none of them is an exception here. Only a transport failure or an unexpected status
    /// throws.
    /// </remarks>
    /// <exception cref="RomMUnreachableException">The server did not answer.</exception>
    public async Task<PairingPollResult> PollPairingAsync(
        string deviceCode,
        CancellationToken cancellationToken = default)
    {
        var payload = new DeviceAuthTokenPayload { Device_code = deviceCode };
        using var request = new HttpRequestMessage(HttpMethod.Post, Resolve("api/auth/device/token"))
        {
            Content = JsonContent.Create(payload, options: SerializerOptions),
        };

        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            var granted = await ReadAsync<DeviceAuthTokenResponse>(response, cancellationToken).ConfigureAwait(false);
            return granted is null
                ? PairingPollResult.Failed(PairingOutcome.ServerError, "The server returned an empty token response.")
                : PairingPollResult.Approved(granted);
        }

        var detail = await ReadDetailAsync(response, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return PairingPollResult.Failed(
                PairingOutcome.RateLimited,
                detail ?? "The server is rate limiting this client. Polling will back off.");
        }

        return detail switch
        {
            "authorization_pending" => PairingPollResult.Pending(),
            "slow_down" => PairingPollResult.SlowDown(),
            "access_denied" => PairingPollResult.Failed(
                PairingOutcome.Denied,
                "The pairing request was declined in the RomM web UI."),
            "expired_token" => PairingPollResult.Failed(
                PairingOutcome.Expired,
                "The pairing code expired. Start again for a new one."),
            _ => PairingPollResult.Failed(
                PairingOutcome.ServerError,
                detail ?? $"The server answered {(int)response.StatusCode} while polling for the token."),
        };
    }

    /// <summary>
    /// Lists the devices registered to the token's owner. Needs <c>devices.read</c>.
    /// </summary>
    /// <remarks>
    /// This is the only authenticated call in M1, and it exists to answer two questions
    /// pairing has to answer: does the stored token still work, and does this install appear
    /// as one device rather than two.
    /// </remarks>
    public Task<RomMResponse<ICollection<DeviceSchema>>> ListDevicesAsync(
        CancellationToken cancellationToken = default) =>
        GetAuthenticatedAsync<ICollection<DeviceSchema>>("api/devices", cancellationToken);

    public void Dispose()
    {
        _http.Dispose();

        // Shares the handler with _http and is only built if something downloaded.
        _downloadHttp?.Dispose();

        // A caller-supplied handler outlives the connection; tests reuse one across several.
        _ownedHandler?.Dispose();
    }

    /// <summary>
    /// Joins a relative API path onto the configured origin, preserving any subpath the
    /// origin carries so an instance behind a reverse proxy works.
    /// </summary>
    internal Uri Resolve(string relativePath) => JoinOrigin(Options.Origin, relativePath);

    /// <summary>
    /// Joins a server-supplied relative path onto an origin.
    /// </summary>
    /// <remarks>
    /// <c>verification_path_complete</c> comes back as <c>/pair/device?user_code=...</c>
    /// because the server is deliberately origin-agnostic, so joining is the client's job.
    /// A leading slash on the path must not discard a subpath in the origin, which is what
    /// <c>new Uri(origin, "/pair/device")</c> would do.
    /// </remarks>
    public static Uri JoinOrigin(Uri origin, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(relativePath);

        var basePath = origin.GetLeftPart(UriPartial.Path);
        if (!basePath.EndsWith('/'))
        {
            basePath += "/";
        }

        return new Uri(new Uri(basePath, UriKind.Absolute), relativePath.TrimStart('/'));
    }

    internal async Task<RomMResponse<T>> GetAuthenticatedAsync<T>(string path, CancellationToken cancellationToken)
    {
        if (!IsAuthenticated)
        {
            return RomMResponse.Failure<T>(RomMResponseStatus.Unauthorized, "No access token is stored. Pair first.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, Resolve(path));
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            var value = await ReadAsync<T>(response, cancellationToken).ConfigureAwait(false);
            return RomMResponse.Success<T>(value!);
        }

        var detail = await ReadDetailAsync(response, cancellationToken).ConfigureAwait(false);

        // 401 is an expected state, not an exception: an expiring token is the recommended
        // default for a portable install. The caller keeps its database and drops to pairing.
        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => RomMResponse.Failure<T>(
                RomMResponseStatus.Unauthorized,
                detail ?? "The stored token is expired or revoked. Pair again."),
            HttpStatusCode.Forbidden => RomMResponse.Failure<T>(
                RomMResponseStatus.Forbidden,
                detail ?? "The stored token was not granted the scope this needs."),

            // 404 is about the library, not the request, which is what the status exists to
            // say. Folded into ServerError here, a caller that wanted to treat one absent row
            // as drift had to treat a 500 as drift too, and a picked set's hydrate did.
            HttpStatusCode.NotFound => RomMResponse.Failure<T>(
                RomMResponseStatus.NotFound,
                detail ?? "The server does not have it."),
            _ => RomMResponse.Failure<T>(
                RomMResponseStatus.ServerError,
                detail ?? $"The server answered {(int)response.StatusCode}."),
        };
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            throw RomMTransportErrors.Classify(ex, request.RequestUri, cancellationToken);
        }
    }

    private static async Task<T?> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content
                .ReadFromJsonAsync<T>(SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new RomMApiException(
                response.StatusCode,
                null,
                $"The server answered with a body this client could not read: {ex.Message}");
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            throw RomMTransportErrors.Classify(ex, response.RequestMessage?.RequestUri, cancellationToken);
        }
    }

    /// <summary>Pulls the <c>detail</c> field out of a FastAPI error body, if there is one.</summary>
    private static async Task<string?> ReadDetailAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("detail", out var detail))
            {
                return null;
            }

            return detail.ValueKind == JsonValueKind.String ? detail.GetString() : detail.ToString();
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException)
        {
            return null;
        }
    }

    private static async Task ThrowIfErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail = await ReadDetailAsync(response, cancellationToken).ConfigureAwait(false);
        throw new RomMApiException(
            response.StatusCode,
            detail,
            detail ?? $"The server answered {(int)response.StatusCode} {response.ReasonPhrase}.");
    }
}
