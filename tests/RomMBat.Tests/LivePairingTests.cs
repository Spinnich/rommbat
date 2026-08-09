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
/// ROMMBAT_TEST_APPROVER_TOKEN=rmm_...      # needs me.read and me.write
/// </code>
/// These create and update devices, so point them at the <b>disposable</b> instance from
/// DEVELOPER_SETUP.md section 3, never the production one.
/// </remarks>
public class LivePairingTests
{
    private const string ServerVariable = "ROMMBAT_TEST_SERVER";
    private const string TokenVariable = "ROMMBAT_TEST_APPROVER_TOKEN";

    private static string? Server => Environment.GetEnvironmentVariable(ServerVariable);

    private static string? ApproverToken => Environment.GetEnvironmentVariable(TokenVariable);

    private static bool IsConfigured => !string.IsNullOrWhiteSpace(Server) && !string.IsNullOrWhiteSpace(ApproverToken);

    [SkippableFact]
    public async Task Pairing_end_to_end_yields_a_token_that_survives_a_restart()
    {
        Skip.IfNot(IsConfigured);

        var origin = new Uri(Server!);
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        string identifier;
        string deviceId;

        using (var store = LocalStore.Open(install))
        using (var connection = new RomMConnection(new RomMClientOptions { Origin = origin }))
        {
            var contact = await ServerProbes.TryContactAsync(connection, store);
            Assert.NotNull(contact);
            Assert.False(contact.MustRefuse);

            var pairing = new PairingService(install, store);
            var session = await pairing.BeginAsync(connection, "RomMBat integration test");

            Assert.True(PairingCode.IsWellFormed(session.UserCode));
            Assert.StartsWith(origin.ToString().TrimEnd('/'), session.VerificationUri.ToString(), StringComparison.Ordinal);

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

            var devices = await authenticated.ListDevicesAsync();

            Assert.True(devices.IsSuccess);
            Assert.Equal(1, devices.Value!.Count(d => string.Equals(d.Id, deviceId, StringComparison.Ordinal)));
        }
    }

    [SkippableFact]
    public async Task Re_pairing_on_the_same_identifier_updates_the_device_rather_than_duplicating_it()
    {
        Skip.IfNot(IsConfigured);

        var origin = new Uri(Server!);
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        using var store = LocalStore.Open(install);
        using var connection = new RomMConnection(new RomMClientOptions { Origin = origin });

        var pairing = new PairingService(install, store);

        var first = await ApproveWhilePollingAsync(
            pairing,
            connection,
            await pairing.BeginAsync(connection, "RomMBat integration test"),
            RomMScopes.Requested);

        Assert.True(first.IsPaired);

        // The install now moves to a different location, exactly as a drive letter change
        // would look, and pairs again on the identity that travelled with it.
        using var moved = tree.CopyToNewLocation();
        var movedInstall = moved.Install();
        using var movedStore = LocalStore.Open(movedInstall);
        var movedPairing = new PairingService(movedInstall, movedStore);

        var session = await movedPairing.BeginAsync(connection, "RomMBat integration test");
        Assert.Equal(DeviceIdentity.Read(install), session.ClientDeviceIdentifier);

        var second = await ApproveWhilePollingAsync(movedPairing, connection, session, RomMScopes.Requested);

        Assert.True(second.IsPaired);
        Assert.Equal(first.RomMDeviceId, second.RomMDeviceId);

        var token = movedPairing.UnlockToken(null);
        using var authenticated = new RomMConnection(new RomMClientOptions { Origin = origin, AccessToken = token });
        var devices = await authenticated.ListDevicesAsync();

        Assert.True(devices.IsSuccess);
        Assert.Equal(
            1,
            devices.Value!.Count(d => string.Equals(d.Id, first.RomMDeviceId, StringComparison.Ordinal)));
    }

    [SkippableFact]
    public async Task A_narrowed_grant_comes_back_narrowed_and_degrades()
    {
        Skip.IfNot(IsConfigured);

        var origin = new Uri(Server!);
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        using var store = LocalStore.Open(install);
        using var connection = new RomMConnection(new RomMClientOptions { Origin = origin });

        var pairing = new PairingService(install, store);
        var session = await pairing.BeginAsync(connection, "RomMBat integration test (narrowed)");

        string[] narrowed = [RomMScopes.MeRead, RomMScopes.RomsRead, RomMScopes.PlatformsRead];
        var completion = await ApproveWhilePollingAsync(pairing, connection, session, narrowed);

        Assert.True(completion.IsPaired);
        Assert.Equal(narrowed.Order(StringComparer.Ordinal), completion.Scopes.All);
        Assert.True(completion.Scopes.Allows(RomMFeature.Library));
        Assert.False(completion.Scopes.Allows(RomMFeature.SavePush));
        Assert.NotEmpty(completion.Scopes.Degradations);
    }

    [SkippableFact]
    public async Task A_declined_request_is_reported_rather_than_polled_forever()
    {
        Skip.IfNot(IsConfigured);

        var origin = new Uri(Server!);
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        using var store = LocalStore.Open(install);
        using var connection = new RomMConnection(new RomMClientOptions { Origin = origin });

        var pairing = new PairingService(install, store);
        var session = await pairing.BeginAsync(connection, "RomMBat integration test (declined)");

        using var approver = new ApprovingUser(origin, ApproverToken!);
        var pollTask = pairing.CompleteAsync(connection, session);

        await approver.DenyAsync(session.UserCode);

        var completion = await pollTask;

        Assert.Equal(PairingOutcome.Denied, completion.Outcome);
        Assert.False(store.Device.Read()!.IsPaired);
    }

    /// <summary>
    /// Starts polling, has the harness approve out of band, and waits for the client to
    /// notice, which is exactly the shape of the real flow.
    /// </summary>
    private static async Task<PairingCompletion> ApproveWhilePollingAsync(
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

        return await pollTask;
    }
}
