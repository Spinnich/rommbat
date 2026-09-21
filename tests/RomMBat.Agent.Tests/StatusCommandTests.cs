using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using RomM.Client;
using RomMBat.Agent.Tests.Support;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Agent.Tests;

/// <summary>
/// The <c>Playtime</c> block, which is the only place RomMBat can see the server half of
/// playtime.
/// </summary>
/// <remarks>
/// <b>Driven through a real socket</b>, because the agent builds its own handler from the
/// stored origin and there is no seam to hand it a stub, which is the same reason
/// <c>SavesCommandTests</c> stands one up.
/// <para>
/// <b>Not parallel</b>, because <see cref="AgentRunner"/> redirects <c>Console</c>.
/// </para>
/// </remarks>
[Collection("agent-console")]
public sealed class StatusCommandTests
{
    [Fact]
    public async Task The_playtime_block_reports_the_newest_session_and_not_the_first_row()
    {
        // The endpoint promises no order and the stub serves none, so a caller taking the first
        // row is wrong on a server that answers oldest first. The timestamps carry no zone,
        // which is how RomM serialises every datetime it holds while storing UTC, so a client
        // reading them as local is out by the machine's own offset and right only where the
        // offset is zero.
        using var server = CannedRomMServer.Serving(
            """
            [
              {"id": 1, "device_id": "romm-device-1", "rom_id": 11, "save_slot": null,
               "start_time": "2026-08-16T10:00:00", "end_time": "2026-08-16T10:30:00",
               "duration_ms": 1800000},
              {"id": 3, "device_id": "romm-device-1", "rom_id": 33, "save_slot": null,
               "start_time": "2026-09-01T08:00:00", "end_time": "2026-09-01T10:03:04",
               "duration_ms": 7384000},
              {"id": 2, "device_id": "romm-device-1", "rom_id": 22, "save_slot": null,
               "start_time": "2026-08-20T09:00:00", "end_time": "2026-08-20T09:05:00",
               "duration_ms": 300000}
            ]
            """);

        using var tree = TempRetroBatTree.Create();
        Pair(tree, server.Origin, RomMScopes.RomsUserRead);

        var run = await AgentRunner.RunAsync(tree, "status");

        Assert.Equal(ExitCode.Ok, run.ExitCode);
        Assert.True(run.Wrote("server holds:    3 sessions for romm device romm-device-1"), run.Out);
        Assert.True(
            run.Wrote("last session:    2026-09-01 08:00:00Z to 2026-09-01 10:03:04Z, 2h 3m 4s"),
            run.Out);
        Assert.True(run.Wrote("its rom:         33"), run.Out);

        // The RomM-side id, never the local one. Asking with the local one answers 200 with
        // zero rows, which reads exactly like a session that was never written.
        Assert.Contains("device_id=romm-device-1", server.LastPlaySessionQuery, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_playtime_block_survives_an_answer_it_cannot_read()
    {
        // A timestamp this client cannot parse is a 200 whose body will not deserialize, and
        // GetAuthenticatedAsync throws RomMApiException for it rather than answering a failed
        // response. The block promises that every refusal here is a line and the exit code
        // stays what the rest of the command decided, and before the catch it left the process
        // on an unhandled exception instead.
        using var server = CannedRomMServer.Serving(
            """
            [
              {"id": 1, "device_id": "romm-device-1", "rom_id": 11, "save_slot": null,
               "start_time": "not-a-date", "end_time": "2026-08-16T10:30:00", "duration_ms": 0}
            ]
            """);

        using var tree = TempRetroBatTree.Create();
        Pair(tree, server.Origin, RomMScopes.RomsUserRead);

        var run = await AgentRunner.RunAsync(tree, "status");

        Assert.Equal(ExitCode.Ok, run.ExitCode);
        Assert.True(run.Wrote("reachable:       yes"), run.Out);
        Assert.True(run.Wrote("not readable:    the server's answer could not be read."), run.Out);
    }

    [Fact]
    public async Task A_heartbeat_it_cannot_read_is_not_reachable_rather_than_a_crash()
    {
        // #211. A captive portal's login page answers the heartbeat 200, and so would a newer
        // RomM whose heartbeat moved. The probe throws RomMApiException for a body it cannot
        // read, and status used to leave the process on it, before the playtime block's own
        // catch was ever reached.
        using var server = CannedRomMServer.HeartbeatAnswering(
            "<html><body>Sign in to the hotel Wi-Fi</body></html>",
            "text/html");
        using var tree = TempRetroBatTree.Create();
        Pair(tree, server.Origin, RomMScopes.RomsUserRead);

        var run = await AgentRunner.RunAsync(tree, "status");

        Assert.Equal(ExitCode.Offline, run.ExitCode);
        Assert.True(run.Wrote($"reachable:       no ({server.Origin})"), run.Out);
        Assert.True(run.Wrote("not as a RomM server this client can read"), run.Out);
        Assert.Null(server.LastPlaySessionQuery);
    }

    [Fact]
    public async Task Pairing_against_a_heartbeat_it_cannot_read_says_what_answered()
    {
        // The same probe, the other caller. Offline rather than a crash, and not "did not
        // answer within 2 seconds", because something did.
        using var server = CannedRomMServer.HeartbeatAnswering(
            "<html><body>Sign in to the hotel Wi-Fi</body></html>",
            "text/html");
        using var tree = TempRetroBatTree.Create();

        var run = await AgentRunner.RunAsync(tree, "pair", "--server", server.Origin.ToString());

        Assert.Equal(ExitCode.Offline, run.ExitCode);
        Assert.Contains("not as a RomM server this client can read", run.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("did not answer within", run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_playtime_block_says_when_the_pairing_was_not_granted_the_scope()
    {
        // Nothing above this line needs the token, so a narrowed pairing is an ordinary state
        // and not a fault. Refused before the request, so the server is never asked.
        using var server = CannedRomMServer.Serving("[]");
        using var tree = TempRetroBatTree.Create();
        Pair(tree, server.Origin, "assets.read");

        var run = await AgentRunner.RunAsync(tree, "status");

        Assert.Equal(ExitCode.Ok, run.ExitCode);
        Assert.True(run.Wrote($"not readable:    this pairing was not granted {RomMScopes.RomsUserRead}."), run.Out);
        Assert.Null(server.LastPlaySessionQuery);
    }

    [Fact]
    public async Task An_empty_answer_is_reported_as_found_nothing_and_not_as_sent_nothing()
    {
        // The rows are scoped to the authenticated user and no scope widens that, so a token on
        // another account answers 200 with zero rows for the right device id. Identity rather
        // than permission, which is why the block says so instead of printing a bare zero.
        using var server = CannedRomMServer.Serving("[]");
        using var tree = TempRetroBatTree.Create();
        Pair(tree, server.Origin, RomMScopes.RomsUserRead);

        var run = await AgentRunner.RunAsync(tree, "status");

        Assert.Equal(ExitCode.Ok, run.ExitCode);
        Assert.True(run.Wrote("server holds:    nothing for romm device romm-device-1"), run.Out);
        Assert.True(run.Wrote("these rows belong to the paired account and no scope widens that"), run.Out);
    }

    [Fact]
    public async Task A_full_window_is_reported_as_a_floor_and_not_as_a_count()
    {
        // The read asks for the server's own default of 50, so exactly 50 rows back is "there
        // were at least this many" rather than "there were this many".
        var rows = Enumerable.Range(1, 50).Select(index => string.Create(
            CultureInfo.InvariantCulture,
            $$"""
              {"id": {{index}}, "device_id": "romm-device-1", "rom_id": 11, "save_slot": null,
               "start_time": "2026-08-16T10:00:00", "end_time": "2026-08-16T10:00:30",
               "duration_ms": 30000}
              """));

        using var server = CannedRomMServer.Serving("[" + string.Join(',', rows) + "]");
        using var tree = TempRetroBatTree.Create();
        Pair(tree, server.Origin, RomMScopes.RomsUserRead);

        var run = await AgentRunner.RunAsync(tree, "status");

        Assert.Equal(ExitCode.Ok, run.ExitCode);
        Assert.True(run.Wrote("server holds:    50 or more sessions for romm device romm-device-1"), run.Out);
        Assert.True(run.Wrote("2026-08-16 10:00:30Z, 30s"), run.Out);
    }

    private static void Pair(TempRetroBatTree tree, Uri origin, string scope)
    {
        var install = tree.Install();
        using var store = LocalStore.Open(install);
        var now = DateTimeOffset.UtcNow;

        store.Device.EnsureIdentity(RomMBat.Core.Identity.DeviceIdentity.ReadOrCreate(install));
        store.Device.SavePairing(
            new PairingResult(
                origin,
                "romm-device-1",
                "Handheld",
                new GrantedScopes([scope]),
                RomMBat.Core.Identity.TokenProtector.Protect("rmm_token", null, now.AddYears(1))),
            now);
    }

    /// <summary>
    /// A loopback server that answers the heartbeat and serves one canned play-session body.
    /// </summary>
    /// <remarks>
    /// The heartbeat has to be real, because <c>status</c> stops at the <c>Server</c> block when
    /// the probe fails and never reaches the one under test.
    /// </remarks>
    private sealed class CannedRomMServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly string _playSessions;
        private readonly (string Body, string ContentType)? _heartbeat;
        private readonly Task _serving;

        private CannedRomMServer(string playSessions, (string Body, string ContentType)? heartbeat)
        {
            _playSessions = playSessions;
            _heartbeat = heartbeat;
            _listener.Start();
            Origin = new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}");
            _serving = ServeAsync(_stop.Token);
        }

        public static CannedRomMServer Serving(string playSessions) => new(playSessions, null);

        /// <summary>Answers the heartbeat 200 with this body instead of RomM's.</summary>
        public static CannedRomMServer HeartbeatAnswering(string body, string contentType) =>
            new("[]", (body, contentType));

        public Uri Origin { get; }

        /// <summary>The query the read actually sent, or null when it never asked.</summary>
        public string? LastPlaySessionQuery { get; private set; }

        public void Dispose()
        {
            _stop.Cancel();
            _listener.Stop();

            try
            {
                _serving.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
            }

            _stop.Dispose();
        }

        private async Task ServeAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                var stream = client.GetStream();
                var buffer = new byte[8192];
                var head = new StringBuilder();

                while (!head.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
                {
                    var read = await stream.ReadAsync(buffer, cancellationToken);

                    if (read == 0)
                    {
                        break;
                    }

                    head.Append(Encoding.ASCII.GetString(buffer, 0, read));
                }

                var target = head.ToString().Split(' ').ElementAtOrDefault(1) ?? string.Empty;
                string body;
                var contentType = "application/json";

                if (target.StartsWith("/api/play-sessions", StringComparison.Ordinal))
                {
                    LastPlaySessionQuery = target;
                    body = _playSessions;
                }
                else if (_heartbeat is { } canned)
                {
                    (body, contentType) = canned;
                }
                else
                {
                    body = """{"SYSTEM": {"VERSION": "5.3.0-beta.1"}}""";
                }

                var bytes = Encoding.UTF8.GetBytes(body);
                await stream.WriteAsync(
                    Encoding.ASCII.GetBytes(
                        $"HTTP/1.1 200 OK\r\nContent-Type: {contentType}\r\n"
                            + $"Content-Length: {bytes.Length}\r\nConnection: close\r\n\r\n"),
                    cancellationToken);
                await stream.WriteAsync(bytes, cancellationToken);
            }
        }
    }
}
