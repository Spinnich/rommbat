using RomM.Client;
using RomMBat.Core.Identity;
using RomMBat.Core.Server;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The offline simulation: drive the whole client against a stub that can be switched to
/// unreachable mid-operation.
/// </summary>
/// <remarks>
/// `docs/ARCHITECTURE.md` calls this the highest-value suite, and the reason is that being
/// offline is the normal case for this app rather than an error path. Every test here
/// asserts the same thing from a different angle: work either completes locally or queues,
/// and nothing is lost.
/// </remarks>
public class OfflineSimulationTests
{
    private static readonly Uri Origin = new("https://romm.invalid");
    private static readonly DateTimeOffset Start = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task An_unreachable_server_is_a_null_contact_rather_than_an_exception()
    {
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());
        using var stub = new StubRomMServer { IsReachable = false };
        using var connection = new RomMConnection(new RomMClientOptions { Origin = Origin }, stub);

        var contact = await ServerProbes.TryContactAsync(connection, store);

        Assert.Null(contact);
    }

    [Fact]
    public async Task First_contact_records_the_skew_and_the_last_successful_contact()
    {
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());
        var time = new TestTimeProvider(Start.AddMinutes(3));

        using var stub = new StubRomMServer { ServerDate = Start };
        using var connection = new RomMConnection(new RomMClientOptions { Origin = Origin }, stub);

        var contact = await ServerProbes.TryContactAsync(connection, store, time);

        Assert.NotNull(contact);
        Assert.True(contact.IsSkewSuspicious);

        var clock = store.Clock.Read();
        Assert.NotNull(clock.LastContactUtc);
        Assert.InRange(clock.Skew!.Value.TotalSeconds, 179, 181);
    }

    [Fact]
    public async Task Going_offline_mid_pairing_does_not_end_the_flow()
    {
        // A dropped Wi-Fi during pairing is not a failure of the pairing. Only the code's own
        // deadline decides, so the loop keeps trying and succeeds when the link comes back.
        using var stub = new StubRomMServer();
        stub.ThenPending().ThenUnreachable(3).ThenApproved(RomMScopes.Requested);

        using var connection = new RomMConnection(new RomMClientOptions { Origin = Origin }, stub);
        var time = new TestTimeProvider(Start);
        var pairing = new DevicePairing(connection, time);

        var session = await pairing.BeginAsync(
            DevicePairing.BuildPayload("11111111-2222-3333-4444-555555555555", "Handheld", "0.1.0"));

        var result = await pairing.AwaitApprovalAsync(session);

        Assert.Equal(PairingOutcome.Approved, result.Outcome);
        Assert.Equal(5, stub.TokenPolls);
    }

    [Fact]
    public async Task A_server_that_disappears_after_pairing_leaves_the_pairing_intact()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        using var store = LocalStore.Open(install);

        using var stub = new StubRomMServer();
        stub.ThenApproved(RomMScopes.Requested, "device-77");

        using var connection = new RomMConnection(new RomMClientOptions { Origin = Origin }, stub);
        var pairing = new PairingService(install, store, new TestTimeProvider(Start));

        var session = await pairing.BeginAsync(connection);
        var completion = await pairing.CompleteAsync(connection, session);

        Assert.True(completion.IsPaired);

        stub.IsReachable = false;

        Assert.Null(await ServerProbes.TryContactAsync(connection, store));

        var device = store.Device.Read();
        Assert.NotNull(device);
        Assert.True(device.IsPaired);
        Assert.Equal("device-77", device.RomMDeviceId);
    }

    [Fact]
    public async Task Work_produced_offline_queues_and_survives_a_restart()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        using (var store = LocalStore.Open(install))
        {
            using var stub = new StubRomMServer { IsReachable = false };
            using var connection = new RomMConnection(new RomMClientOptions { Origin = Origin }, stub);

            Assert.Null(await ServerProbes.TryContactAsync(connection, store));

            store.Outbox.Enqueue(
                OutboxKind.PlaySession,
                Start,
                romId: 1,
                payload: """{"started_at":"2026-08-09T12:00:00Z"}""");

            store.Outbox.Enqueue(
                OutboxKind.Save,
                Start.AddMinutes(30),
                romId: 1,
                slot: "libretro:battery",
                relativePath: RomMBat.Core.Paths.RelativePath.Create("saves/snes/libretro/game.srm"),
                contentHash: "0123456789abcdef0123456789abcdef",
                sizeBytes: 8192,
                fileMtimeUtc: Start.AddMinutes(29));

            Assert.Equal(2, store.Outbox.PendingCount());
        }

        using var reopened = LocalStore.Open(install);
        var pending = reopened.Outbox.Pending();

        Assert.Equal(2, pending.Count);
        Assert.True(pending[0].LocalSequence < pending[1].LocalSequence);
    }

    [Fact]
    public async Task A_401_drops_the_token_and_keeps_the_database_and_the_outbox()
    {
        // An expired or revoked token must never cost data. Everything except the token
        // survives, so re-pairing on the same identifier resumes the flush.
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        using var store = LocalStore.Open(install);

        using var stub = new StubRomMServer();
        stub.ThenApproved(RomMScopes.Requested, "device-77");

        using var connection = new RomMConnection(new RomMClientOptions { Origin = Origin }, stub);
        var pairing = new PairingService(install, store, new TestTimeProvider(Start));

        var session = await pairing.BeginAsync(connection);
        await pairing.CompleteAsync(connection, session);

        store.Outbox.Enqueue(OutboxKind.PlaySession, Start, romId: 1, payload: "{}");
        var identifier = store.Device.Read()!.ClientDeviceIdentifier;

        stub.DevicesStatus = System.Net.HttpStatusCode.Unauthorized;
        using var authenticated = new RomMConnection(
            new RomMClientOptions { Origin = Origin, AccessToken = "rmm_stale" },
            stub);

        var response = await authenticated.ListDevicesAsync();
        Assert.True(response.NeedsRepairing);

        pairing.DropTokenForRepairing();

        var device = store.Device.Read();
        Assert.NotNull(device);
        Assert.Null(device.Token);
        Assert.False(device.IsPaired);
        Assert.Equal(identifier, device.ClientDeviceIdentifier);
        Assert.Equal(1, store.Outbox.PendingCount());

        // The identity file is untouched, so a re-pair updates the same RomM device.
        Assert.Equal(identifier, DeviceIdentity.Read(install));
    }

    [Fact]
    public async Task Re_pairing_after_a_move_keeps_the_same_identifier()
    {
        // M0 probe 7 moved a stick between two machines under different Windows users. The
        // identity has to follow the drive, so the device list must not grow a second row.
        using var original = TempRetroBatTree.Create();
        var firstIdentifier = DeviceIdentity.ReadOrCreate(original.Install());

        using (var store = LocalStore.Open(original.Install()))
        {
            using var stub = new StubRomMServer();
            stub.ThenApproved(RomMScopes.Requested, "device-77");
            using var connection = new RomMConnection(new RomMClientOptions { Origin = Origin }, stub);

            var pairing = new PairingService(original.Install(), store, new TestTimeProvider(Start));
            var session = await pairing.BeginAsync(connection);
            await pairing.CompleteAsync(connection, session);
        }

        using var moved = original.CopyToNewLocation();
        using var movedStore = LocalStore.Open(moved.Install());

        using var movedStub = new StubRomMServer();
        movedStub.ThenApproved(RomMScopes.Requested, "device-77");
        using var movedConnection = new RomMConnection(new RomMClientOptions { Origin = Origin }, movedStub);

        var movedPairing = new PairingService(moved.Install(), movedStore, new TestTimeProvider(Start));
        var movedSession = await movedPairing.BeginAsync(movedConnection);

        Assert.Equal(firstIdentifier, movedSession.ClientDeviceIdentifier);
        Assert.Equal(firstIdentifier, DeviceIdentity.Read(moved.Install()));

        var completion = await movedPairing.CompleteAsync(movedConnection, movedSession);

        Assert.Equal("device-77", completion.RomMDeviceId);
    }

    [Fact]
    public async Task A_narrowed_grant_degrades_by_feature_instead_of_failing()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        using var store = LocalStore.Open(install);

        // The approver ticked read-only: no assets.write, no devices.*, no roms.user.*.
        string[] narrowed = [RomMScopes.MeRead, RomMScopes.RomsRead, RomMScopes.PlatformsRead, RomMScopes.AssetsRead];

        using var stub = new StubRomMServer();
        stub.ThenApproved(narrowed, "device-77");

        using var connection = new RomMConnection(new RomMClientOptions { Origin = Origin }, stub);
        var pairing = new PairingService(install, store, new TestTimeProvider(Start));

        var session = await pairing.BeginAsync(connection);
        var completion = await pairing.CompleteAsync(connection, session);

        Assert.True(completion.IsPaired);
        Assert.True(completion.Scopes.Allows(RomMFeature.Library));
        Assert.True(completion.Scopes.Allows(RomMFeature.SavePull));
        Assert.False(completion.Scopes.Allows(RomMFeature.SavePush));
        Assert.False(completion.Scopes.Allows(RomMFeature.DeviceSync));
        Assert.False(completion.Scopes.Allows(RomMFeature.Playtime));
        Assert.False(completion.Scopes.Allows(RomMFeature.Firmware));

        Assert.Contains(RomMScopes.AssetsWrite, completion.Scopes.MissingFor(RomMFeature.SavePush));
        Assert.Equal(5, completion.Scopes.Degradations.Count);
        Assert.Contains("narrowed", completion.Message, StringComparison.Ordinal);

        // Reopening reads the same picture back, so status can report it offline.
        Assert.Equal(completion.Scopes.All, store.Device.Read()!.Scopes.All);
    }

    [Fact]
    public void The_server_URL_is_remembered_before_pairing_so_it_is_typed_once()
    {
        // The URL is the one gamepad-hostile step left, so a lapsed code must not cost the
        // user the typing again.
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        using var store = LocalStore.Open(install);

        var identifier = new PairingService(install, store).RememberServer(Origin);

        var device = store.Device.Read();

        Assert.NotNull(device);
        Assert.Equal(Origin, device.ServerOrigin);
        Assert.Equal(identifier, device.ClientDeviceIdentifier);
        Assert.False(device.IsPaired);
    }

    [Fact]
    public void An_over_granted_token_is_visible_rather_than_silently_accepted()
    {
        var scopes = new GrantedScopes([.. RomMScopes.Requested, RomMScopes.UsersRead, RomMScopes.TasksRun]);

        Assert.Equal([RomMScopes.TasksRun, RomMScopes.UsersRead], scopes.OverGranted);
        Assert.Empty(scopes.Degradations);
    }

    [Fact]
    public void An_empty_grant_turns_everything_off_without_throwing()
    {
        var none = GrantedScopes.None;

        Assert.Equal(GrantedScopes.Requirements.Count, none.Degradations.Count);
        Assert.All(GrantedScopes.Requirements, requirement => Assert.False(none.Allows(requirement.Feature)));
        Assert.Equal(RomMScopes.Requested, none.NotGranted);
    }
}
