using RomM.Client;
using RomMBat.Core.Identity;
using RomMBat.Core.Server;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The real pairing flow against a real RomM, driven headlessly.
/// </summary>
/// <remarks>
/// Skipped unless both environment variables are set, so a clone with no server still runs
/// green. Nothing in this file names an instance; the URL and the token come from the
/// environment and never from the repository.
/// <code>
/// ROMMBAT_TEST_SERVER=https://your-romm-instance
/// ROMMBAT_TEST_APPROVER_TOKEN=rmm_...
/// </code>
/// <para>
/// <b>The approver token is not a RomMBat token</b>, and the README scopes table does not
/// apply to it. That table is what a device requests; RomMBat never asks for
/// <c>me.write</c>. The token here needs <c>me.read</c> and <c>me.write</c> and nothing
/// else, because <c>/approve</c> and <c>/deny</c> are <c>[Scope.ME_WRITE]</c> routes. Its
/// <b>account</b> separately needs every scope in that table, because <c>allowed_scopes</c>
/// is computed from <c>request.user.oauth_scopes</c> and caps what can be granted.
/// </para>
/// <para>
/// The two fail differently: a token without <c>me.write</c> gives a bare 403 before the
/// code is looked up, while an account short of the device scopes gets that far and fails
/// on <c>Assert.Empty(completion.Scopes.Degradations)</c>.
/// </para>
/// <para>
/// These create devices and mint real tokens, and clean both up in
/// <see cref="DisposeAsync"/>. Run them under a <b>dedicated non-admin account</b>: devices
/// and client tokens are per-user rows, so that is what keeps them off anyone else's data.
/// See DEVELOPER_SETUP.md section 3.
/// </para>
/// </remarks>
public sealed class LivePairingTests : IAsyncDisposable
{
    /// <summary>
    /// Undone after every test. xUnit builds a fresh instance per test, so each one cleans
    /// up only what it created.
    /// </summary>
    private readonly PairingLitter _litter = new();

    private const string ServerVariable = "ROMMBAT_TEST_SERVER";
    private const string TokenVariable = "ROMMBAT_TEST_APPROVER_TOKEN";

    private static string? Server => Environment.GetEnvironmentVariable(ServerVariable);

    private static string? ApproverToken => Environment.GetEnvironmentVariable(TokenVariable);

    private const string NotConfigured =
        "Set ROMMBAT_TEST_SERVER and ROMMBAT_TEST_APPROVER_TOKEN to run the live tests.";

    private static bool IsConfigured => !string.IsNullOrWhiteSpace(Server) && !string.IsNullOrWhiteSpace(ApproverToken);

    /// <summary>
    /// Deletes the devices and revokes the tokens this test created.
    /// </summary>
    /// <remarks>
    /// Not optional politeness. Each approval mints a real credential with the full RomMBat
    /// scope set, and without this a suite run leaves one set behind every time.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (!IsConfigured || _litter.IsEmpty)
        {
            return;
        }

        var problems = await _litter.CleanUpAsync(new Uri(Server!), ApproverToken!);

        Assert.True(
            problems.Count == 0,
            "Live test litter was left on the server: " + string.Join("; ", problems));
    }

    [Fact]
    public async Task Pairing_end_to_end_yields_a_token_that_survives_a_restart()
    {
        Assert.SkipUnless(IsConfigured, NotConfigured);

        var origin = new Uri(Server!);
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        string identifier;
        string deviceId;

        using (var store = LocalStore.Open(install))
        using (var connection = new RomMConnection(new RomMClientOptions { Origin = origin }))
        {
            var contact = await ServerProbes.TryContactAsync(
                connection,
                store,
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.NotNull(contact);
            Assert.False(contact.MustRefuse);

            var pairing = new PairingService(install, store);
            var session = await pairing.BeginAsync(
                connection,
                "RomMBat integration test",
                TestContext.Current.CancellationToken);

            Assert.True(PairingCode.IsWellFormed(session.UserCode));
            Assert.StartsWith(
                origin.ToString().TrimEnd('/'),
                session.VerificationUri.ToString(),
                StringComparison.Ordinal);

            var completion = await ApproveWhilePollingAsync(pairing, connection, session, RomMScopes.Requested);

            Assert.True(completion.IsPaired);
            Assert.NotNull(completion.RomMDeviceId);
            Assert.Empty(completion.Scopes.Degradations);

            identifier = session.ClientDeviceIdentifier;
            deviceId = completion.RomMDeviceId!;
        }

        // Restart: a new process would read exactly this back off the disk.
        using (var reopened = LocalStore.Open(install))
        {
            var device = reopened.Device.Read();
            Assert.NotNull(device);
            Assert.True(device.IsPaired);
            Assert.Equal(identifier, device.ClientDeviceIdentifier);

            var token = new PairingService(install, reopened).UnlockToken(null);
            using var authenticated = new RomMConnection(
                new RomMClientOptions { Origin = origin, AccessToken = token });

            var devices = await authenticated.ListDevicesAsync(TestContext.Current.CancellationToken);

            Assert.True(devices.IsSuccess);
            Assert.Equal(1, devices.Value!.Count(d => string.Equals(d.Id, deviceId, StringComparison.Ordinal)));
        }
    }

    [Fact]
    public async Task Re_pairing_on_the_same_identifier_updates_the_device_rather_than_duplicating_it()
    {
        Assert.SkipUnless(IsConfigured, NotConfigured);

        var origin = new Uri(Server!);
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        using var store = LocalStore.Open(install);
        using var connection = new RomMConnection(new RomMClientOptions { Origin = origin });

        var pairing = new PairingService(install, store);

        var first = await ApproveWhilePollingAsync(
            pairing,
            connection,
            await pairing.BeginAsync(connection, "RomMBat integration test", TestContext.Current.CancellationToken),
            RomMScopes.Requested);

        Assert.True(first.IsPaired);

        // The install now moves to a different location, exactly as a drive letter change
        // would look, and pairs again on the identity that travelled with it.
        using var moved = tree.CopyToNewLocation();
        var movedInstall = moved.Install();
        using var movedStore = LocalStore.Open(movedInstall);
        var movedPairing = new PairingService(movedInstall, movedStore);

        var session = await movedPairing.BeginAsync(
            connection,
            "RomMBat integration test",
            TestContext.Current.CancellationToken);
        Assert.Equal(DeviceIdentity.Read(install), session.ClientDeviceIdentifier);

        var second = await ApproveWhilePollingAsync(movedPairing, connection, session, RomMScopes.Requested);

        Assert.True(second.IsPaired);
        Assert.Equal(first.RomMDeviceId, second.RomMDeviceId);

        var token = movedPairing.UnlockToken(null);
        using var authenticated = new RomMConnection(new RomMClientOptions { Origin = origin, AccessToken = token });
        var devices = await authenticated.ListDevicesAsync(TestContext.Current.CancellationToken);

        Assert.True(devices.IsSuccess);
        Assert.Equal(
            1,
            devices.Value!.Count(d => string.Equals(d.Id, first.RomMDeviceId, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_narrowed_grant_comes_back_narrowed_and_degrades()
    {
        Assert.SkipUnless(IsConfigured, NotConfigured);

        var origin = new Uri(Server!);
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        using var store = LocalStore.Open(install);
        using var connection = new RomMConnection(new RomMClientOptions { Origin = origin });

        var pairing = new PairingService(install, store);
        var session = await pairing.BeginAsync(
            connection,
            "RomMBat integration test (narrowed)",
            TestContext.Current.CancellationToken);

        string[] narrowed = [RomMScopes.MeRead, RomMScopes.RomsRead, RomMScopes.PlatformsRead];
        var completion = await ApproveWhilePollingAsync(pairing, connection, session, narrowed);

        Assert.True(completion.IsPaired);
        Assert.Equal(narrowed.Order(StringComparer.Ordinal), completion.Scopes.All);
        Assert.True(completion.Scopes.Allows(RomMFeature.Library));
        Assert.False(completion.Scopes.Allows(RomMFeature.SavePush));
        Assert.NotEmpty(completion.Scopes.Degradations);
        Assert.False(completion.Scopes.Allows(RomMFeature.Firmware));

        // What a token without firmware.read actually answers, driven rather than guessed,
        // because the degraded path in FeatureAvailability is written against it.
        //
        // The split matters: platforms.read alone still carries every firmware md5, since the
        // records are inlined on the platform list, so the BIOS gap report keeps working on a
        // narrowed grant and only the fetch is refused.
        using (var narrowedConnection = new RomMConnection(
            new RomMClientOptions { Origin = origin, AccessToken = pairing.UnlockToken(null) }))
        {
            var platforms = await narrowedConnection.ListPlatformsAsync(
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(platforms.IsSuccess, platforms.Message);

            var listed = await narrowedConnection.ListFirmwareAsync(
                platforms.Value![0].Id,
                TestContext.Current.CancellationToken);
            Assert.Equal(RomMResponseStatus.Forbidden, listed.Status);

            var fetchable = platforms.Value
                .SelectMany(platform => platform.Firmware)
                .FirstOrDefault(firmware => firmware.IsFetchable);

            if (fetchable is not null)
            {
                var refused = await narrowedConnection.DownloadFirmwareAsync(
                    fetchable,
                    Stream.Null,
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.Equal(RomMResponseStatus.Forbidden, refused.Status);
                Assert.Contains("firmware.read", refused.Message, StringComparison.Ordinal);
            }
        }

        // The remedy the client prints is "pair again and grant the missing scopes", so
        // exercise it: the same identifier, a wider grant, the same device, no degradations.
        var widened = await ApproveWhilePollingAsync(
            pairing,
            connection,
            await pairing.BeginAsync(
                connection,
                "RomMBat integration test (narrowed)",
                TestContext.Current.CancellationToken),
            RomMScopes.Requested);

        Assert.True(widened.IsPaired);
        Assert.Equal(completion.RomMDeviceId, widened.RomMDeviceId);
        Assert.Empty(widened.Scopes.Degradations);
        Assert.Equal(RomMScopes.Requested.Order(StringComparer.Ordinal), widened.Scopes.All);
    }

    [Fact]
    public async Task A_declined_request_is_reported_rather_than_polled_forever()
    {
        Assert.SkipUnless(IsConfigured, NotConfigured);

        var origin = new Uri(Server!);
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        using var store = LocalStore.Open(install);
        using var connection = new RomMConnection(new RomMClientOptions { Origin = origin });

        var pairing = new PairingService(install, store);
        var session = await pairing.BeginAsync(
            connection,
            "RomMBat integration test (declined)",
            TestContext.Current.CancellationToken);

        using var approver = new ApprovingUser(origin, ApproverToken!);
        var pollTask = pairing.CompleteAsync(
            connection,
            session,
            cancellationToken: TestContext.Current.CancellationToken);

        await approver.DenyAsync(session.UserCode, TestContext.Current.CancellationToken);

        var completion = await pollTask;

        Assert.Equal(PairingOutcome.Denied, completion.Outcome);
        Assert.False(store.Device.Read()!.IsPaired);
    }

    /// <summary>
    /// Starts polling, has the harness approve out of band, and waits for the client to
    /// notice, which is exactly the shape of the real flow.
    /// </summary>
    private async Task<PairingCompletion> ApproveWhilePollingAsync(
        PairingService pairing,
        RomMConnection connection,
        PairingSession session,
        IEnumerable<string> approvedScopes)
    {
        using var approver = new ApprovingUser(new Uri(Server!), ApproverToken!);

        var pending = await approver.ReadPendingAsync(session.UserCode);
        Assert.Equal(session.ClientDeviceIdentifier, pending.Client_device_identifier);
        Assert.Equal("RomMBat", pending.Client);

        // A token can never exceed its owner's scopes, so anything outside allowed_scopes
        // would be refused by the server rather than silently trimmed.
        var grantable = approvedScopes.Where(pending.Allowed_scopes.Contains).ToArray();
        Assert.NotEmpty(grantable);

        var pollTask = pairing.CompleteAsync(connection, session);
        await approver.ApproveAsync(session.UserCode, grantable, session.DeviceName);

        var completion = await pollTask;

        if (completion.IsPaired && completion.RomMDeviceId is not null)
        {
            // Tracked with the raw token, because that is the only credential in play
            // carrying devices.write and so the only one that can delete the device.
            _litter.Track(completion.RomMDeviceId, pairing.UnlockToken(null), completion.Scopes.All);
        }

        return completion;
    }
}
