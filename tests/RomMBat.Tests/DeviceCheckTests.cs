using System.Net;
using RomM.Client;
using RomMBat.Core.Identity;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The check after pairing that this install is one device in RomM, not two.
/// </summary>
/// <remarks>
/// It lives in <see cref="PairingService"/> so the console and the gamepad UI run the same one.
/// Each test pairs against the stub, then asserts what reading the device list back concluded.
/// </remarks>
public class DeviceCheckTests
{
    private static readonly Uri Origin = new("https://romm.invalid");
    private static readonly DateTimeOffset Start = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task One_listed_device_with_this_id_verifies()
    {
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());
        using var stub = new StubRomMServer();
        stub.DeviceIds.Add("other-device");
        stub.DeviceIds.Add("device-77");

        var check = await PairThenCheckAsync(tree, store, stub, RomMScopes.Requested);

        Assert.Equal(DeviceCheckOutcome.OneDevice, check.Outcome);
        Assert.False(check.IsWarning);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Any_other_count_is_a_mismatch_that_names_it(int listed)
    {
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());
        using var stub = new StubRomMServer();
        for (var i = 0; i < listed; i++)
        {
            stub.DeviceIds.Add("device-77");
        }

        var check = await PairThenCheckAsync(tree, store, stub, RomMScopes.Requested);

        Assert.Equal(DeviceCheckOutcome.Mismatch, check.Outcome);
        Assert.True(check.IsWarning);
        Assert.Contains($"found {listed}", check.Message, StringComparison.Ordinal);

        // The check reports; it never undoes a pairing that is already written down.
        Assert.True(store.Device.Read()!.IsPaired);
    }

    [Fact]
    public async Task Without_devices_read_it_is_skipped_and_never_asks()
    {
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());
        using var stub = new StubRomMServer();

        var granted = RomMScopes.Requested.Where(scope => scope != RomMScopes.DevicesRead);
        var check = await PairThenCheckAsync(tree, store, stub, granted);

        Assert.Equal(DeviceCheckOutcome.Skipped, check.Outcome);
        Assert.DoesNotContain(stub.RequestLog, path => path.EndsWith("/api/devices", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_refused_list_is_unverified_rather_than_a_mismatch()
    {
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());
        using var stub = new StubRomMServer { DevicesStatus = HttpStatusCode.Forbidden };

        var check = await PairThenCheckAsync(tree, store, stub, RomMScopes.Requested);

        Assert.Equal(DeviceCheckOutcome.Unverified, check.Outcome);
        Assert.True(check.IsWarning);
    }

    [Fact]
    public async Task A_list_that_is_not_json_is_unverified_rather_than_thrown()
    {
        // A proxy's HTML page answering 200 makes the client throw RomMApiException, which the
        // pairing screen would otherwise turn into Refused over a pairing that is stored and works.
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());
        using var stub = new StubRomMServer();
        stub.ThenApproved(RomMScopes.Requested, "device-77");

        using var connection = new RomMConnection(new RomMClientOptions { Origin = Origin }, stub);
        var pairing = new PairingService(tree.Install(), store, new TestTimeProvider(Start));
        var session = await pairing.BeginAsync(connection, cancellationToken: TestContext.Current.CancellationToken);
        var completion = await pairing.CompleteAsync(
            connection,
            session,
            cancellationToken: TestContext.Current.CancellationToken);

        using var html = new HtmlPage();
        var check = await pairing.VerifyDeviceAsync(
            completion,
            token => new RomMConnection(new RomMClientOptions { Origin = Origin, AccessToken = token }, html),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(DeviceCheckOutcome.Unverified, check.Outcome);
        Assert.True(store.Device.Read()!.IsPaired);
    }

    [Fact]
    public async Task The_check_carries_the_token_pairing_stored()
    {
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());
        using var stub = new StubRomMServer();
        stub.DeviceIds.Add("device-77");

        string? used = null;
        await PairThenCheckAsync(tree, store, stub, RomMScopes.Requested, token => used = token);

        Assert.Equal("rmm_" + new string('a', 64), used);
    }

    private static async Task<DeviceCheck> PairThenCheckAsync(
        TempRetroBatTree tree,
        LocalStore store,
        StubRomMServer stub,
        IEnumerable<string> granted,
        Action<string>? sawToken = null)
    {
        stub.ThenApproved(granted, "device-77");

        using var connection = new RomMConnection(new RomMClientOptions { Origin = Origin }, stub);
        var pairing = new PairingService(tree.Install(), store, new TestTimeProvider(Start));

        var session = await pairing.BeginAsync(connection, cancellationToken: TestContext.Current.CancellationToken);
        var completion = await pairing.CompleteAsync(
            connection,
            session,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(completion.IsPaired);

        return await pairing.VerifyDeviceAsync(
            completion,
            token =>
            {
                sawToken?.Invoke(token);
                return new RomMConnection(new RomMClientOptions { Origin = Origin, AccessToken = token }, stub);
            },
            cancellationToken: TestContext.Current.CancellationToken);
    }

    private sealed class HtmlPage : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html>Sign in</html>", System.Text.Encoding.UTF8, "application/json"),
                RequestMessage = request,
            });
    }
}
