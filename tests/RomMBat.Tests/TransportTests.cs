using System.Net;
using System.Net.Sockets;
using RomM.Client;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The connect timeout and the exception classification it forces.
/// </summary>
/// <remarks>
/// M0 probe 6b is the source for all of this: an absent host on the local subnet takes 21
/// seconds to fail, a default <see cref="HttpClient"/> inherits every millisecond, and a
/// timeout and a user cancellation are the same exception type.
/// </remarks>
public class TransportTests
{
    private static readonly Uri Origin = new("https://romm.invalid");

    [Fact]
    public void The_handler_always_sets_a_connect_timeout()
    {
        // Nothing sets this by default, which is the entire finding.
        using var handler = RomMConnection.CreateHandler(new RomMClientOptions { Origin = Origin });

        Assert.Equal(RomMClientOptions.InteractiveConnectTimeout, handler.ConnectTimeout);
        Assert.NotEqual(Timeout.InfiniteTimeSpan, handler.ConnectTimeout);
    }

    [Fact]
    public void The_interactive_budget_is_two_seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(2), RomMClientOptions.InteractiveConnectTimeout);
    }

    [Fact]
    public void The_request_timeout_is_a_different_lever_from_the_connect_timeout()
    {
        // HttpClient.Timeout bounds the response body too, so it cannot be lowered to make
        // reachability feel responsive without aborting large downloads.
        var options = new RomMClientOptions { Origin = Origin };

        Assert.True(options.RequestTimeout > options.ConnectTimeout);
    }

    [Fact]
    public void A_connect_timeout_is_classified_as_unreachable_not_as_a_cancellation()
    {
        var timeout = new TaskCanceledException("timed out", new TimeoutException());

        var classified = RomMTransportErrors.Classify(timeout, Origin, CancellationToken.None);

        var unreachable = Assert.IsType<RomMUnreachableException>(classified);
        Assert.Equal(UnreachableReason.ConnectTimeout, unreachable.Reason);
    }

    [Fact]
    public void A_real_cancellation_stays_a_cancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        var cancelled = new TaskCanceledException("cancelled");

        var classified = RomMTransportErrors.Classify(cancelled, Origin, source.Token);

        Assert.Same(cancelled, classified);
        Assert.IsNotType<RomMUnreachableException>(classified);
    }

    [Theory]
    [InlineData(SocketError.HostNotFound, UnreachableReason.NameResolution)]
    [InlineData(SocketError.ConnectionRefused, UnreachableReason.ConnectionRefused)]
    [InlineData(SocketError.TimedOut, UnreachableReason.ConnectTimeout)]
    [InlineData(SocketError.NetworkUnreachable, UnreachableReason.Network)]
    public void Socket_failures_keep_their_reason(SocketError error, UnreachableReason expected)
    {
        var request = new HttpRequestException("nope", new SocketException((int)error));

        var classified = RomMTransportErrors.Classify(request, Origin, CancellationToken.None);

        Assert.Equal(expected, Assert.IsType<RomMUnreachableException>(classified).Reason);
    }

    [Fact]
    public void Anything_unrecognised_is_returned_unchanged_rather_than_called_offline()
    {
        var unrelated = new InvalidOperationException("something else went wrong");

        Assert.Same(unrelated, RomMTransportErrors.Classify(unrelated, Origin, CancellationToken.None));
    }

    [Theory]
    [InlineData("https://romm.lan", "/pair/device?user_code=ABCD1234", "https://romm.lan/pair/device?user_code=ABCD1234")]
    [InlineData("https://romm.lan/", "/pair/device?user_code=ABCD1234", "https://romm.lan/pair/device?user_code=ABCD1234")]
    [InlineData("https://host.lan/romm", "/pair/device?user_code=ABCD1234", "https://host.lan/romm/pair/device?user_code=ABCD1234")]
    [InlineData("https://host.lan/romm/", "/pair/device?user_code=ABCD1234", "https://host.lan/romm/pair/device?user_code=ABCD1234")]
    [InlineData("http://192.0.2.10:8080", "/pair/device?user_code=ABCD1234", "http://192.0.2.10:8080/pair/device?user_code=ABCD1234")]
    public void The_verification_path_joins_onto_the_configured_origin(string origin, string path, string expected)
    {
        // The server returns a relative path on purpose and stays origin-agnostic, so joining
        // is the client's job, and a leading slash must not discard a reverse-proxy subpath.
        Assert.Equal(new Uri(expected), RomMConnection.JoinOrigin(new Uri(origin), path));
    }

    [Fact]
    public async Task An_unreachable_server_throws_unreachable_from_the_probe()
    {
        using var stub = new StubRomMServer { IsReachable = false };
        using var connection = new RomMConnection(new RomMClientOptions { Origin = Origin }, stub);

        var exception = await Assert.ThrowsAsync<RomMUnreachableException>(() => connection.ProbeAsync());

        Assert.Equal(UnreachableReason.ConnectTimeout, exception.Reason);
    }

    [Fact]
    public async Task The_probe_reports_the_version_and_the_server_clock()
    {
        var serverNow = new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);
        using var stub = new StubRomMServer { ServerVersion = "5.1.1-beta.1", ServerDate = serverNow };
        using var connection = new RomMConnection(new RomMClientOptions { Origin = Origin }, stub);

        var probe = await connection.ProbeAsync();

        Assert.Equal("5.1.1-beta.1", probe.ReportedVersion);
        Assert.Equal(CompatibilityVerdict.Supported, probe.Compatibility.Verdict);
        Assert.Equal(serverNow, probe.ServerDate);
    }

    [Fact]
    public async Task A_401_is_a_result_rather_than_an_exception()
    {
        // An expiring token is the recommended default for a portable install, so 401 is an
        // expected state. Throwing here would make every caller wrap it in a try/catch it
        // only ever uses to swallow.
        using var stub = new StubRomMServer { DevicesStatus = HttpStatusCode.Unauthorized };
        using var connection = new RomMConnection(
            new RomMClientOptions { Origin = Origin, AccessToken = "rmm_expired" },
            stub);

        var response = await connection.ListDevicesAsync();

        Assert.False(response.IsSuccess);
        Assert.True(response.NeedsRepairing);
        Assert.Equal(RomMResponseStatus.Unauthorized, response.Status);
    }

    [Fact]
    public async Task A_403_reads_as_a_missing_scope_rather_than_an_expired_token()
    {
        using var stub = new StubRomMServer { DevicesStatus = HttpStatusCode.Forbidden };
        using var connection = new RomMConnection(
            new RomMClientOptions { Origin = Origin, AccessToken = "rmm_narrow" },
            stub);

        var response = await connection.ListDevicesAsync();

        Assert.Equal(RomMResponseStatus.Forbidden, response.Status);
        Assert.False(response.NeedsRepairing);
    }

    [Fact]
    public async Task An_authenticated_call_without_a_token_does_not_reach_the_network()
    {
        using var stub = new StubRomMServer();
        using var connection = new RomMConnection(new RomMClientOptions { Origin = Origin }, stub);

        var response = await connection.ListDevicesAsync();

        Assert.Equal(RomMResponseStatus.Unauthorized, response.Status);
        Assert.Empty(stub.RequestLog);
    }
}
