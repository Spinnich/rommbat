using RomM.Client;
using RomMBat.Agent.Tests.Support;
using RomMBat.Core;
using RomMBat.Core.Identity;
using RomMBat.Core.Mapping;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Sets;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Agent.Tests;

/// <summary>
/// <c>game</c>: the browse detail screen's three verbs from a terminal.
/// </summary>
/// <remarks>
/// <b>Not parallel</b>, because <see cref="AgentRunner"/> redirects <c>Console</c>.
/// </remarks>
[Collection("agent-console")]
public sealed class GameCommandTests : IDisposable
{
    private const int Chrono = 7;
    private const string RomPath = "roms/snes/Chrono Trigger (USA).sfc";

    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Uri Origin = new("https://romm.invalid/");

    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();
    private readonly StubRomMServer _stub = new();

    public GameCommandTests()
    {
        AgentRunner.WriteEsSystems(_tree);

        _stub.Library.Add(new StubRom(Chrono, 1, "snes", "snes", "Chrono Trigger", "Chrono Trigger (USA).sfc", "sfc", 1_024));
        _stub.Library.Add(new StubRom(8, 2, "n64", "n64", "Super Mario 64", "Super Mario 64 (USA).z64", "z64", 1_024));
        _stub.Content[Chrono] = new byte[1_024];
        _stub.Content[8] = new byte[1_024];

        // snes is mapped and n64 deliberately is not.
        WithSession(session => session.Store.PlatformMap.Record(
            new PlatformResolver(EsSystemsFile.Load(session.Install)).Resolve(new RomMPlatform(1, "snes", "snes", "snes")),
            Now));
    }

    public void Dispose()
    {
        _stub.Dispose();
        _tree.Dispose();
    }

    [Fact]
    public async Task Install_puts_one_game_on_the_device_in_the_picked_set_and_its_gamelist()
    {
        Pair();

        var run = await AgentRunner.RunAgainstAsync(_tree, _stub, "game", "install", "7");

        Assert.True(run.ExitCode == ExitCode.Ok, run.Error);
        Assert.True(File.Exists(Absolute(RomPath)), run.Out);
        Assert.Contains("Chrono Trigger", File.ReadAllText(Absolute("roms/snes/gamelist.xml")), StringComparison.Ordinal);
        Assert.True(run.Wrote("Added Chrono Trigger to 'Picked on"), run.Out);
        Assert.Equal([Chrono], WithSession(session => new PickedSetService(session).Picks()));
    }

    [Fact]
    public async Task Install_pushes_the_pick_so_it_follows_the_user()
    {
        // A pick changes the picked set's ids, so it pushes Device.sync_config as `sets add` and
        // `sets resolve` do. #444.
        Pair(RomMScopes.DevicesRead, RomMScopes.DevicesWrite);

        var run = await AgentRunner.RunAgainstAsync(_tree, _stub, "game", "install", "7");

        Assert.True(run.ExitCode == ExitCode.Ok, run.Error);

        var roamed = RoamingSyncConfig.Extract(_stub.StoredSyncConfig);
        Assert.NotNull(roamed);

        var picked = Assert.Single(roamed.Sets, set => set.Scope == "picked");
        Assert.Equal([Chrono], PickedScopeJson.Parse(picked.ScopeValue));
    }

    [Fact]
    public async Task A_pick_that_cannot_roam_says_so_and_still_installs()
    {
        // Best effort, as a resolve's push is: the note is printed and the install is untouched.
        Pair();

        var run = await AgentRunner.RunAgainstAsync(_tree, _stub, "game", "install", "7");

        Assert.True(run.ExitCode == ExitCode.Ok, run.Error);
        Assert.True(run.Wrote("devices.write was not granted"), run.Out);
        Assert.True(File.Exists(Absolute(RomPath)), run.Out);
        Assert.Null(_stub.StoredSyncConfig);
    }

    [Fact]
    public async Task Install_brings_the_artwork_and_description_RomM_holds()
    {
        // A pick wrote the member row and no metadata, so MediaSync found nothing to work from
        // and skipped the game: found on a live install, where a one-game install of Balloon
        // Fight landed with no cover while RomM held one.
        _stub.Library[0] = _stub.Library[0] with { Metadata = new StubRomMetadata() };
        _stub.Media[$"/assets/romm/resources/roms/1/{Chrono}/cover/big.png"] = new byte[64];
        Pair();

        var run = await AgentRunner.RunAgainstAsync(_tree, _stub, "game", "install", "7");

        Assert.True(run.ExitCode == ExitCode.Ok, run.Error);
        Assert.Contains(_stub.AssetRequests, path => path.Contains("/cover/", StringComparison.Ordinal));
        Assert.Contains(
            "A game the stub library holds.",
            File.ReadAllText(Absolute("roms/snes/gamelist.xml")),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_second_install_of_a_game_already_here_fetches_nothing_and_says_so()
    {
        Pair();
        await AgentRunner.RunAgainstAsync(_tree, _stub, "game", "install", "7");
        var served = _stub.ContentRequests.Count;

        var run = await AgentRunner.RunAgainstAsync(_tree, _stub, "game", "install", "7");

        Assert.Equal(ExitCode.Ok, run.ExitCode);
        Assert.True(run.Wrote("Nothing was fetched"), run.Out);
        Assert.Equal(served, _stub.ContentRequests.Count);
    }

    [Fact]
    public async Task An_unmapped_platform_is_refused_with_the_reason_and_nothing_is_picked()
    {
        Pair();

        var run = await AgentRunner.RunAgainstAsync(_tree, _stub, "game", "install", "8");

        Assert.Equal(ExitCode.Refused, run.ExitCode);
        Assert.True(run.Complained("has no RetroBat folder"), run.Error);
        Assert.Empty(WithSession(session => new PickedSetService(session).Picks()));
    }

    [Fact]
    public async Task An_id_the_server_does_not_have_is_a_usage_error()
    {
        Pair();

        var run = await AgentRunner.RunAgainstAsync(_tree, _stub, "game", "install", "999");

        Assert.Equal(ExitCode.Usage, run.ExitCode);
        Assert.Empty(_stub.ContentRequests);
    }

    [Fact]
    public async Task Install_on_an_unpaired_install_is_refused_as_not_paired()
    {
        var run = await AgentRunner.RunAgainstAsync(_tree, _stub, "game", "install", "7");

        Assert.Equal(ExitCode.NotPaired, run.ExitCode);
    }

    [Fact]
    public async Task Remove_previews_without_apply_and_touches_nothing()
    {
        Pair();
        await AgentRunner.RunAgainstAsync(_tree, _stub, "game", "install", "7");

        var run = await AgentRunner.RunAsync(_tree, "game", "remove", "7");

        Assert.Equal(ExitCode.Ok, run.ExitCode);
        Assert.True(run.Wrote("Nothing was removed"), run.Out);
        Assert.True(File.Exists(Absolute(RomPath)));
        Assert.Equal([Chrono], WithSession(session => new PickedSetService(session).Picks()));
    }

    [Fact]
    public async Task Remove_with_apply_deletes_the_game_and_unpicks_it()
    {
        Pair();
        await AgentRunner.RunAgainstAsync(_tree, _stub, "game", "install", "7");

        var run = await AgentRunner.RunAsync(_tree, "game", "remove", "7", "--apply");

        Assert.True(run.ExitCode == ExitCode.Ok, run.Error);
        Assert.False(File.Exists(Absolute(RomPath)), run.Out);
        Assert.Empty(WithSession(session => new PickedSetService(session).Picks()));
    }

    [Fact]
    public async Task Remove_with_apply_pushes_the_unpick_so_it_follows_the_user()
    {
        // The unpick rewrites the picked set's ids, so it pushes as the pick did. Without this
        // the server went on holding the game until something else pushed. #451.
        Pair(RomMScopes.DevicesRead, RomMScopes.DevicesWrite);
        await AgentRunner.RunAgainstAsync(_tree, _stub, "game", "install", "7");

        var preview = await AgentRunner.RunAgainstAsync(_tree, _stub, "game", "remove", "7");

        Assert.Equal(ExitCode.Ok, preview.ExitCode);
        Assert.Equal([Chrono], RoamedPicks());

        var run = await AgentRunner.RunAgainstAsync(_tree, _stub, "game", "remove", "7", "--apply");

        Assert.True(run.ExitCode == ExitCode.Ok, run.Error);
        Assert.Empty(RoamedPicks());
    }

    [Fact]
    public async Task An_unpick_that_cannot_roam_says_so_and_still_removes()
    {
        Pair();
        await AgentRunner.RunAgainstAsync(_tree, _stub, "game", "install", "7");

        var run = await AgentRunner.RunAgainstAsync(_tree, _stub, "game", "remove", "7", "--apply");

        Assert.True(run.ExitCode == ExitCode.Ok, run.Error);
        Assert.True(run.Wrote("devices.write was not granted"), run.Out);
        Assert.False(File.Exists(Absolute(RomPath)), run.Out);
        Assert.Empty(WithSession(session => new PickedSetService(session).Picks()));
    }

    [Fact]
    public async Task A_game_another_set_still_wants_is_kept_and_the_preview_says_why()
    {
        Pair();
        await AgentRunner.RunAgainstAsync(_tree, _stub, "game", "install", "7");

        // A second set claiming the same game, as a platform set that had resolved it would.
        WithSession(session =>
        {
            var picked = new PickedSetService(session).Find()!;
            var member = session.Store.SyncSets.Members(picked.Id).Single();
            var other = new SyncSetService(session)
                .Add(new SetDraft { Name = "All SNES", Scope = RomM.Client.Catalog.CatalogScopeKind.Platform, ScopeValue = "snes" }, Now)
                .Set!;

            session.Store.SyncSets.UpsertMember(other.Id, member, Now);
        });

        var run = await AgentRunner.RunAsync(_tree, "game", "remove", "7", "--apply");

        Assert.Equal(ExitCode.Ok, run.ExitCode);
        Assert.True(run.Wrote("kept"), run.Out);
        Assert.True(File.Exists(Absolute(RomPath)), run.Out);
    }

    [Fact]
    public async Task Show_offline_reads_what_this_device_holds()
    {
        Pair();
        await AgentRunner.RunAgainstAsync(_tree, _stub, "game", "install", "7");

        var run = await AgentRunner.RunAsync(_tree, "game", "show", "7", "--offline");

        Assert.Equal(ExitCode.Ok, run.ExitCode);
        Assert.True(run.Wrote("Chrono Trigger"), run.Out);
        Assert.True(run.Wrote("size in RomM: not known"), run.Out);
        Assert.True(run.Wrote("Picked on"), run.Out);
    }

    [Fact]
    public async Task Show_of_a_game_RomM_no_longer_has_does_not_call_it_offline()
    {
        // RomM answered, with a 404, so the fallback to this device's copy is not an offline one.
        Pair();
        await AgentRunner.RunAgainstAsync(_tree, _stub, "game", "install", "7");
        _stub.Library.RemoveAt(0);

        var run = await AgentRunner.RunAgainstAsync(_tree, _stub, "game", "show", "7");

        Assert.Equal(ExitCode.Ok, run.ExitCode);
        Assert.True(run.Wrote("size in RomM: not known"), run.Out);
        Assert.False(run.Wrote("offline"), run.Out);
    }

    [Fact]
    public async Task Show_online_names_the_hashes_and_says_a_game_is_not_here()
    {
        Pair();

        var run = await AgentRunner.RunAgainstAsync(_tree, _stub, "game", "show", "7");

        Assert.Equal(ExitCode.Ok, run.ExitCode);
        Assert.True(run.Wrote("on device:    no"), run.Out);
        Assert.True(run.Wrote("md5:"), run.Out);
    }

    [Fact]
    public async Task A_rom_id_that_is_not_a_number_is_a_usage_error()
    {
        var run = await AgentRunner.RunAsync(_tree, "game", "install", "chrono");

        Assert.Equal(ExitCode.Usage, run.ExitCode);
        Assert.True(run.Complained("not a rom id"), run.Error);
    }

    private string Absolute(string relative) => Path.Combine(_tree.Root, relative);

    /// <summary>The picked set's ids as the server last stored them.</summary>
    private IReadOnlyList<int> RoamedPicks()
    {
        var roamed = RoamingSyncConfig.Extract(_stub.StoredSyncConfig);
        Assert.NotNull(roamed);

        return PickedScopeJson.Parse(Assert.Single(roamed.Sets, set => set.Scope == "picked").ScopeValue);
    }

    private void Pair(params string[] extraScopes) => WithSession(session =>
    {
        session.Store.Device.EnsureIdentity(DeviceIdentity.ReadOrCreate(session.Install));
        session.Store.Device.SavePairing(
            new PairingResult(
                Origin,
                "device-1",
                "Handheld",
                new GrantedScopes(["roms.read", "assets.read", "assets.write", .. extraScopes]),
                TokenProtector.Protect("rmm_token", null, Now.AddYears(1))),
            Now);
    });

    private void WithSession(Action<InstallSession> act) => WithSession(session =>
    {
        act(session);
        return 0;
    });

    private T WithSession<T>(Func<InstallSession, T> read)
    {
        using var session = InstallSession.Open(_tree.Root).Session!;
        return read(session);
    }
}
