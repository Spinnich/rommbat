using RomM.Client;
using RomM.Client.Generated;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The pairing protocol: the code, the QR target, and every outcome the poll can return.
/// </summary>
public class DevicePairingTests
{
    private static readonly Uri Origin = new("https://romm.invalid");
    private static readonly DateTimeOffset Start = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_user_code_alphabet_excludes_the_ambiguous_characters()
    {
        // The published docs call this 8 digits. It is not, and the exclusions are the point:
        // I, L, O, 0 and 1 are all absent so nothing is misread off a screen.
        Assert.Equal(8, PairingCode.Length);
        Assert.Equal("ABCDEFGHJKMNPQRSTUVWXYZ23456789", PairingCode.Alphabet);

        foreach (var excluded in "ILO01")
        {
            Assert.DoesNotContain(excluded.ToString(), PairingCode.Alphabet, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("K7M2PQRS", "K7M2-PQRS")]
    [InlineData("k7m2pqrs", "K7M2-PQRS")]
    [InlineData("K7M2-PQRS", "K7M2-PQRS")]
    [InlineData("k7m2 pqrs", "K7M2-PQRS")]
    public void The_code_is_grouped_for_display_and_normalised_the_way_the_server_does(
        string input,
        string expected)
    {
        Assert.Equal(expected, PairingCode.Format(input));
        Assert.Equal("K7M2PQRS", PairingCode.Normalize(input));
        Assert.True(PairingCode.IsWellFormed(input));
    }

    [Theory]
    [InlineData("K7M2PQR")]
    [InlineData("K7M2PQRS9X")]
    [InlineData("K7M2PQR0")]
    [InlineData("K7M2PQRI")]
    [InlineData(null)]
    public void A_code_outside_the_alphabet_or_the_length_is_not_well_formed(string? code)
    {
        Assert.False(PairingCode.IsWellFormed(code));
    }

    [Fact]
    public async Task Begin_returns_the_code_the_QR_target_and_the_deadline()
    {
        using var stub = new StubRomMServer { UserCode = "K7M2PQRS", ExpiresIn = 600, Interval = 5 };
        using var connection = new RomMConnection(new RomMClientOptions { Origin = Origin }, stub);
        var time = new TestTimeProvider(Start);

        var session = await new DevicePairing(connection, time).BeginAsync(
            Payload(),
            TestContext.Current.CancellationToken);

        Assert.Equal("K7M2PQRS", session.UserCode);
        Assert.Equal("K7M2-PQRS", session.DisplayCode);
        Assert.Equal(new Uri("https://romm.invalid/pair/device?user_code=K7M2PQRS"), session.VerificationUri);
        Assert.Equal(Start.AddSeconds(600), session.ExpiresAt);
        Assert.Equal(TimeSpan.FromSeconds(5), session.Interval);
        Assert.Equal(TimeSpan.FromSeconds(600), session.RemainingAt(Start));
    }

    [Fact]
    public async Task Pending_answers_keep_the_loop_going_until_approval()
    {
        using var stub = new StubRomMServer();
        stub.ThenPending().ThenPending().ThenApproved(RomMScopes.Requested);

        using var connection = new RomMConnection(new RomMClientOptions { Origin = Origin }, stub);
        var time = new TestTimeProvider(Start);
        var pairing = new DevicePairing(connection, time);

        var session = await pairing.BeginAsync(Payload(), TestContext.Current.CancellationToken);
        var result = await pairing.AwaitApprovalAsync(
            session,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(PairingOutcome.Approved, result.Outcome);
        Assert.Equal(3, stub.TokenPolls);
        Assert.NotNull(result.Token);
    }

    [Fact]
    public async Task Slow_down_widens_the_interval_rather_than_failing()
    {
        using var stub = new StubRomMServer();
        stub.ThenSlowDown().ThenApproved(RomMScopes.Requested);

        using var connection = new RomMConnection(new RomMClientOptions { Origin = Origin }, stub);
        var time = new TestTimeProvider(Start);
        var pairing = new DevicePairing(connection, time);

        var session = await pairing.BeginAsync(Payload(), TestContext.Current.CancellationToken);

        var reported = new List<PairingProgress>();
        var progress = new Progress<PairingProgress>(reported.Add);
        var result = await pairing.AwaitApprovalAsync(session, progress, TestContext.Current.CancellationToken);

        Assert.Equal(PairingOutcome.Approved, result.Outcome);

        // The virtual clock advanced by the widened interval, not the original one.
        Assert.True(time.GetUtcNow() >= Start.AddSeconds(10));
    }

    [Fact]
    public async Task A_declined_request_stops_polling()
    {
        using var stub = new StubRomMServer();
        stub.ThenPending().ThenDenied();

        using var connection = new RomMConnection(new RomMClientOptions { Origin = Origin }, stub);
        var pairing = new DevicePairing(connection, new TestTimeProvider(Start));

        var session = await pairing.BeginAsync(Payload(), TestContext.Current.CancellationToken);
        var result = await pairing.AwaitApprovalAsync(
            session,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(PairingOutcome.Denied, result.Outcome);
        Assert.Equal(2, stub.TokenPolls);
    }

    [Fact]
    public async Task An_expired_token_answer_ends_the_flow()
    {
        using var stub = new StubRomMServer();
        stub.ThenExpired();

        using var connection = new RomMConnection(new RomMClientOptions { Origin = Origin }, stub);
        var pairing = new DevicePairing(connection, new TestTimeProvider(Start));

        var session = await pairing.BeginAsync(Payload(), TestContext.Current.CancellationToken);
        var result = await pairing.AwaitApprovalAsync(
            session,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(PairingOutcome.Expired, result.Outcome);
    }

    [Fact]
    public async Task The_loop_gives_up_at_the_600_second_deadline_the_server_enforces()
    {
        // Pending state lives only in Redis with a hard 600 s TTL, so the deadline is real
        // and the client must not poll a code that cannot come back.
        using var stub = new StubRomMServer { ExpiresIn = 600, Interval = 5 };

        using var connection = new RomMConnection(new RomMClientOptions { Origin = Origin }, stub);
        var time = new TestTimeProvider(Start);
        var pairing = new DevicePairing(connection, time);

        var session = await pairing.BeginAsync(Payload(), TestContext.Current.CancellationToken);
        var result = await pairing.AwaitApprovalAsync(
            session,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(PairingOutcome.Expired, result.Outcome);
        Assert.True(time.GetUtcNow() >= session.ExpiresAt);

        // 600 seconds at a 5 second interval, and no more.
        Assert.InRange(stub.TokenPolls, 100, 122);
    }

    [Fact]
    public async Task A_rate_limited_poll_backs_off_instead_of_giving_up()
    {
        using var stub = new StubRomMServer();
        stub.ThenRateLimited().ThenApproved(RomMScopes.Requested);

        using var connection = new RomMConnection(new RomMClientOptions { Origin = Origin }, stub);
        var time = new TestTimeProvider(Start);
        var pairing = new DevicePairing(connection, time);

        var session = await pairing.BeginAsync(Payload(), TestContext.Current.CancellationToken);
        var result = await pairing.AwaitApprovalAsync(
            session,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(PairingOutcome.Approved, result.Outcome);
        Assert.True(time.GetUtcNow() >= Start.AddSeconds(30));
    }

    [Fact]
    public async Task A_user_cancellation_is_not_reported_as_the_server_being_down()
    {
        using var stub = new StubRomMServer();
        using var connection = new RomMConnection(new RomMClientOptions { Origin = Origin }, stub);
        var pairing = new DevicePairing(connection, new TestTimeProvider(Start));

        var session = await pairing.BeginAsync(Payload(), TestContext.Current.CancellationToken);

        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pairing.AwaitApprovalAsync(session, cancellationToken: source.Token));
    }

    [Fact]
    public void The_init_payload_carries_the_GUID_and_no_host_fingerprint()
    {
        // POST /api/devices dedups on mac_address alone, so pairing owns device creation and
        // no host detail is ever sent. This is what makes moving the drive a non-event.
        var payload = DevicePairing.BuildPayload("11111111-2222-3333-4444-555555555555", "Handheld", "0.1.0");

        Assert.Equal("11111111-2222-3333-4444-555555555555", payload.Client_device_identifier);
        Assert.Equal("RomMBat", payload.Client);
        Assert.Equal("windows", payload.Platform);
        Assert.Equal(RomMScopes.Requested, payload.Requested_scopes);
    }

    [Fact]
    public void The_requested_scopes_are_exactly_the_ones_the_README_lists_and_nothing_dangerous()
    {
        Assert.All(
            RomMScopes.Requested,
            scope => Assert.DoesNotContain(scope, RomMScopes.NeverNeeded));

        Assert.Contains(RomMScopes.MeRead, RomMScopes.Requested);
        Assert.DoesNotContain(RomMScopes.MeWrite, RomMScopes.Requested);
        Assert.Equal(11, RomMScopes.Requested.Count);
    }

    private static DeviceAuthInitPayload Payload() =>
        DevicePairing.BuildPayload("11111111-2222-3333-4444-555555555555", "Test device", "0.1.0");
}
