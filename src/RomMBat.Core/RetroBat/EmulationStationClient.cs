using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace RomMBat.Core.RetroBat;

/// <summary>How a call to the EmulationStation API went.</summary>
public enum EsCallResult
{
    /// <summary>The server answered.</summary>
    Answered,

    /// <summary>Nothing is listening, which is the ordinary case and not a fault.</summary>
    NotRunning,

    /// <summary>It is listening and something went wrong.</summary>
    Failed,
}

/// <summary>One system as EmulationStation's own model reports it, which is not the same thing as a configured one.</summary>
public sealed record EsReportedSystem
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("totalGames")]
    public int TotalGames { get; init; }
}

/// <summary>
/// The EmulationStation HTTP API, used for one thing: telling it to re-read what we wrote.
/// </summary>
/// <remarks>
/// <b>Best-effort by design.</b> ES is running only when a user is at the machine, and a
/// background sync is most often run when it is not, so failing to reach it is the ordinary
/// outcome rather than an error. Every call reports which of the three things happened
/// instead of throwing.
/// <para>
/// <b>The connect timeout is the whole reason this class exists rather than a reused
/// client.</b> A refused connection on loopback takes <b>2.04 s</b> on Windows, measured five
/// times over raw TCP and three over HttpClient, which is the same figure M0 probe 6b
/// recorded for a closed port. The project's 2 s interactive budget would therefore fire at
/// almost exactly the moment the OS gave up anyway and save nothing. ES answers in 1-2 ms
/// when it is there, so a far shorter budget costs nothing and turns 2 s of dead time per
/// sync into a fifth of that.
/// </para>
/// <para>
/// <b>A 200 is not evidence the action happened.</b> Measured: with a game running,
/// <c>/reloadgames</c> answers 200 in 1 ms and does not reload, exactly as <c>/quit</c> and
/// <c>/emukill</c> do. It also answers before doing the work, taking 269 ms to have an effect
/// on a 200-entry system and 1.1 s on 100,000, so its response time measures nothing either.
/// </para>
/// </remarks>
public sealed class EmulationStationClient : IDisposable
{
    /// <summary>Where ES listens. Loopback only unless the user opts into public access.</summary>
    public const string DefaultOrigin = "http://127.0.0.1:1234";

    /// <summary>
    /// How long to wait for the TCP handshake.
    /// </summary>
    /// <remarks>
    /// Loopback, where a healthy connect is sub-millisecond. Short enough that finding ES
    /// absent is nearly free, and orders of magnitude above what a live one needs.
    /// </remarks>
    public static TimeSpan ConnectTimeout => TimeSpan.FromMilliseconds(400);

    private readonly HttpClient _http;
    private readonly SocketsHttpHandler _handler;

    public EmulationStationClient(string? origin = null)
    {
        _handler = new SocketsHttpHandler
        {
            // Never left at its default. Nothing sets it, and the default is the 21 s stall
            // that rule 5 exists to prevent.
            ConnectTimeout = ConnectTimeout,
            PooledConnectionLifetime = TimeSpan.FromMinutes(1),
        };

        _http = new HttpClient(_handler, disposeHandler: false)
        {
            BaseAddress = new Uri((origin ?? DefaultOrigin).TrimEnd('/') + "/"),

            // Bounds the whole call, headers and body. /systems is a few KB even on a huge
            // library, so this only ever fires on something pathological.
            Timeout = TimeSpan.FromSeconds(5),
        };
    }

    /// <summary>
    /// Asks EmulationStation to rescan the ROM folders and re-read the gamelists.
    /// </summary>
    /// <remarks>
    /// Called immediately after writing, because ES holds a stale in-memory model until asked
    /// and serialises that model over the file when it exits. Writing without reloading is
    /// what loses the edit.
    /// </remarks>
    public async Task<EsCallResult> ReloadGamesAsync(CancellationToken cancellationToken = default)
    {
        var (result, _) = await GetAsync("reloadgames", cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Whether EmulationStation is running, and what it reports.
    /// </summary>
    /// <remarks>
    /// <c>/systems</c> rather than <c>/systems/{name}/games</c>: the first is a few KB and
    /// carries <c>totalGames</c>, the second serialises the whole library and reached 99 MB
    /// at 100,000 entries, which loads ES down enough to distort anything measured around it.
    /// </remarks>
    public async Task<(EsCallResult Result, IReadOnlyList<EsReportedSystem> Systems)> ListSystemsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync("systems", cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return (EsCallResult.Failed, []);
            }

            var systems = await response.Content
                .ReadFromJsonAsync<List<EsReportedSystem>>(cancellationToken)
                .ConfigureAwait(false);

            return (EsCallResult.Answered, systems ?? []);
        }
        catch (Exception ex) when (IsAbsent(ex, cancellationToken))
        {
            return (EsCallResult.NotRunning, []);
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException)
        {
            return (EsCallResult.Failed, []);
        }
    }

    public void Dispose()
    {
        _http.Dispose();
        _handler.Dispose();
    }

    private async Task<(EsCallResult Result, string? Body)> GetAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(path, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return (response.IsSuccessStatusCode ? EsCallResult.Answered : EsCallResult.Failed, body);
        }
        catch (Exception ex) when (IsAbsent(ex, cancellationToken))
        {
            return (EsCallResult.NotRunning, null);
        }
        catch (HttpRequestException)
        {
            return (EsCallResult.Failed, null);
        }
    }

    /// <summary>
    /// True when the failure means nothing is listening rather than something went wrong.
    /// </summary>
    /// <remarks>
    /// A connect timeout and a user cancellation are both <see cref="TaskCanceledException"/>
    /// and differ only in the inner exception, so the caller's own token is checked first. A
    /// real cancellation propagates.
    /// <para>
    /// The 5 s request timeout produces that same shape, so an ES that accepts the connection
    /// and then stalls reads as absent. Deliberate, and the same call this project makes for
    /// reachability: both mean "not there inside the budget", and ES stalling with the port
    /// open is not a state a background sync can do anything different about.
    /// </para>
    /// </remarks>
    private static bool IsAbsent(Exception ex, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return ex switch
        {
            TaskCanceledException canceled => canceled.InnerException is TimeoutException,
            HttpRequestException request => request.InnerException is System.Net.Sockets.SocketException,
            _ => false,
        };
    }
}
