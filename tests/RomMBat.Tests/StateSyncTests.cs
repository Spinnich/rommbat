using System.Net;
using RomM.Client;
using RomMBat.Core.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Pushing save states, which is all a state sync can be.
/// </summary>
/// <remarks>
/// Nothing here negotiates, because <c>POST /api/states</c> has no slot, no device, no session
/// and no conflict detection. What these tests hold to account is the one thing that can go
/// wrong silently: the server keys a state on <c>(rom_id, file_name)</c> alone, so a name that
/// does not carry the emulator and core loses one of two states with no error anywhere.
/// </remarks>
public class StateSyncTests
{
    private static readonly Uri Origin = new("https://romm.invalid");

    [Fact]
    public void The_uploaded_name_carries_the_scope_and_the_on_disk_name_does_not()
    {
        Assert.Equal(
            "ActRaiser (USA) [libretro.snes9x].state1",
            StateSync.UploadNameFor("ActRaiser (USA).state1", "libretro", "snes9x"));

        // Unconditional, even where no core exists. A conditional rule would produce different
        // names on two devices for one state, and two names is two rows.
        Assert.Equal(
            "Game (USA).01 [pcsx2].p2s",
            StateSync.UploadNameFor("Game (USA).01.p2s", "pcsx2", null));

        Assert.Equal("libretro.snes9x", StateSync.ScopeOf("libretro", "snes9x"));
        Assert.Equal("pcsx2", StateSync.ScopeOf("pcsx2", string.Empty));
    }

    [Fact]
    public async Task Two_cores_of_one_emulator_land_as_two_states_rather_than_overwriting_each_other()
    {
        // The measured failure this whole naming rule exists for: five posts of one file name
        // under five different emulator values reused a single server row.
        using var fixture = StateFixture.Create();
        fixture.AddRom(42, "snes", "ActRaiser (USA).zip");
        fixture.AddState("snes/libretro.snes9x", "ActRaiser (USA).state1", "snes9x progress");
        fixture.AddState("snes/libretro.bsnes", "ActRaiser (USA).state1", "bsnes progress");
        fixture.Scan();

        var outcome = await fixture.PushAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, outcome.Uploaded);
        Assert.Equal(0, outcome.Failed);

        Assert.Equal(2, fixture.Stub.States.Count);

        Assert.Equal(
            ["ActRaiser (USA) [libretro.bsnes].state1", "ActRaiser (USA) [libretro.snes9x].state1"],
            fixture.Stub.States.Values.Select(state => state.FileName).Order(StringComparer.Ordinal));

        // And both sets of bytes survived, which is the thing that would have been lost.
        Assert.Equal(
            ["bsnes progress", "snes9x progress"],
            fixture.Stub.States.Values
                .Select(state => System.Text.Encoding.UTF8.GetString(state.Bytes))
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Replaying_a_push_sends_nothing_and_reuses_the_row()
    {
        using var fixture = StateFixture.Create();
        fixture.AddRom(42, "snes", "ActRaiser (USA).zip");
        fixture.AddState("snes/libretro.snes9x", "ActRaiser (USA).state1", "progress");
        fixture.Scan();

        var first = await fixture.PushAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, first.Uploaded);

        var id = fixture.Stub.States.Keys.Single();

        fixture.Scan();
        var second = await fixture.PushAsync(TestContext.Current.CancellationToken);

        // Nothing sent, because the local row remembers the hash it sent. The server would have
        // accepted the upsert without complaint, so this is the client declining rather than the
        // server refusing.
        Assert.Equal(0, second.Uploaded);
        Assert.Equal(1, second.AlreadyInStep);
        Assert.Equal(id, fixture.Stub.States.Keys.Single());
    }

    [Fact]
    public async Task A_changed_state_replaces_the_row_it_already_has()
    {
        using var fixture = StateFixture.Create();
        fixture.AddRom(42, "snes", "ActRaiser (USA).zip");
        fixture.AddState("snes/libretro.snes9x", "ActRaiser (USA).state1", "first");
        fixture.Scan();
        await fixture.PushAsync(TestContext.Current.CancellationToken);

        var id = fixture.Stub.States.Keys.Single();

        fixture.AddState("snes/libretro.snes9x", "ActRaiser (USA).state1", "second, longer");
        fixture.Scan();

        var outcome = await fixture.PushAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Uploaded);

        // One row, not two: the upsert keys on the name and the name did not change.
        Assert.Equal(id, fixture.Stub.States.Keys.Single());
        Assert.Equal("second, longer", System.Text.Encoding.UTF8.GetString(fixture.Stub.States[id].Bytes));
    }

    [Fact]
    public async Task The_screenshot_travels_when_there_is_one_and_the_scope_travels_with_it()
    {
        using var fixture = StateFixture.Create();
        fixture.AddRom(42, "ps2", "Game (USA).iso");
        fixture.AddState("ps2/pcsx2", "Game (USA).01.p2s", "state");
        fixture.AddState("ps2/pcsx2", "Game (USA).01.p2s.png", "png bytes");
        fixture.Scan();

        await fixture.PushAsync(TestContext.Current.CancellationToken);

        var state = Assert.Single(fixture.Stub.States.Values);

        Assert.Equal("Game (USA).01 [pcsx2].p2s", state.FileName);
        Assert.Equal("Game (USA).01.p2s [pcsx2].png", state.ScreenshotName);
        Assert.Equal("png bytes", System.Text.Encoding.UTF8.GetString(state.ScreenshotBytes!));
    }

    [Fact]
    public async Task A_screenshot_the_server_does_not_keep_is_counted_rather_than_called_success()
    {
        // Measured against a live instance: the image bytes arrive and are stored against the
        // ROM, but the state comes back with screenshot: null and stays that way. The state
        // itself is complete, so this is not a failure, but reporting plain success for
        // something that did not happen is what this exists to prevent.
        using var fixture = StateFixture.Create();
        fixture.AddRom(42, "ps2", "Game (USA).iso");
        fixture.AddState("ps2/pcsx2", "Game (USA).01.p2s", "state");
        fixture.AddState("ps2/pcsx2", "Game (USA).01.p2s.png", "png bytes");
        fixture.Scan();

        fixture.Stub.DropScreenshots = true;

        var outcome = await fixture.PushAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Uploaded);
        Assert.Equal(0, outcome.Failed);
        Assert.Equal(1, outcome.ScreenshotsDropped);
        Assert.Contains("did not keep the screenshot", Assert.Single(outcome.Problems), StringComparison.Ordinal);
        Assert.Contains("without the screenshot", outcome.Summary, StringComparison.Ordinal);

        // Still recorded as sent, because the state is what matters and re-sending would
        // orphan another copy of the image against the ROM.
        Assert.False(Assert.Single(fixture.Store.States.List()).IsUnsent);
    }

    [Fact]
    public async Task A_zero_byte_screenshot_never_reaches_the_server()
    {
        // Measured: the server accepts one and stores it as a real screenshot row, and
        // RetroBat's mirror produces one by racing the emulator. Nothing downstream refuses it.
        using var fixture = StateFixture.Create();
        fixture.AddRom(42, "ps2", "Game (USA).iso");
        fixture.AddState("ps2/pcsx2", "Game (USA).01.p2s", "state");
        fixture.AddState("ps2/pcsx2", "Game (USA).01.p2s.png", string.Empty);
        fixture.Scan();

        await fixture.PushAsync(TestContext.Current.CancellationToken);

        Assert.Null(Assert.Single(fixture.Stub.States.Values).ScreenshotBytes);
    }

    [Fact]
    public async Task An_unattributed_state_is_reported_rather_than_sent()
    {
        using var fixture = StateFixture.Create();
        fixture.AddState("snes/libretro.snes9x", "Not In The Library (USA).state1", "progress");
        fixture.Scan();

        var outcome = await fixture.PushAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, outcome.Uploaded);
        Assert.Equal(1, outcome.Unattributed);
        Assert.Empty(fixture.Stub.States);
    }

    [Fact]
    public async Task An_unreachable_server_leaves_every_state_recorded_as_unsent()
    {
        using var fixture = StateFixture.Create();
        fixture.AddRom(1, "snes", "One (USA).zip");
        fixture.AddRom(2, "snes", "Two (USA).zip");
        fixture.AddState("snes/libretro.snes9x", "One (USA).state1", "a");
        fixture.AddState("snes/libretro.snes9x", "Two (USA).state1", "b");
        fixture.Scan();

        fixture.Stub.IsReachable = false;

        var outcome = await fixture.PushAsync(TestContext.Current.CancellationToken);

        // Offline is a working state: nothing threw, nothing was lost, and both states are
        // still waiting.
        Assert.Equal(0, outcome.Uploaded);
        Assert.NotEmpty(outcome.Problems);
        Assert.All(fixture.Store.States.List(), state => Assert.True(state.IsUnsent));

        // One attempt, not one per state. Each would have cost a connect timeout.
        Assert.Equal(1, outcome.Failed);

        fixture.Stub.IsReachable = true;

        var second = await fixture.PushAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, second.Uploaded);
    }

    [Fact]
    public async Task A_failed_upload_leaves_that_state_unsent_and_the_others_alone()
    {
        using var fixture = StateFixture.Create();
        fixture.AddRom(1, "snes", "One (USA).zip");
        fixture.AddRom(2, "snes", "Two (USA).zip");
        fixture.AddState("snes/libretro.snes9x", "One (USA).state1", "a");
        fixture.AddState("snes/libretro.snes9x", "Two (USA).state1", "b");
        fixture.Scan();

        fixture.Stub.FailNextStateUpload = HttpStatusCode.InternalServerError;

        var outcome = await fixture.PushAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Uploaded);
        Assert.Equal(1, outcome.Failed);

        // The one that failed is still unsent, so the next pass picks it up. A partial failure
        // costs a retry rather than a state.
        Assert.Single(fixture.Store.States.List(), state => state.IsUnsent);

        var second = await fixture.PushAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, second.Uploaded);
        Assert.DoesNotContain(fixture.Store.States.List(), state => state.IsUnsent);
    }

    [Fact]
    public async Task A_state_with_no_hash_is_never_sent()
    {
        // A file held open by a running emulator is recorded without a hash, and sending bytes
        // whose integrity was never checked is worse than sending nothing.
        using var fixture = StateFixture.Create();
        fixture.AddRom(42, "snes", "ActRaiser (USA).zip");
        fixture.AddState("snes/libretro.snes9x", "ActRaiser (USA).state1", "progress");
        fixture.Scan();

        var scanned = Assert.Single(fixture.Store.States.List());
        fixture.Store.States.Record(scanned with { ContentHash = null }, DateTimeOffset.UnixEpoch);

        var outcome = await fixture.PushAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, outcome.Uploaded);
        Assert.Empty(fixture.Stub.States);
    }

    /// <summary>A temp install, a stub server, and the two passes that move a state.</summary>
    private sealed class StateFixture : IDisposable
    {
        private readonly TempRetroBatTree _tree;
        private readonly RomMConnection _connection;

        private StateFixture(TempRetroBatTree tree, RetroBatInstall install, LocalStore store, StubRomMServer stub)
        {
            _tree = tree;
            Install = install;
            Store = store;
            Stub = stub;
            _connection = new RomMConnection(
                new RomMClientOptions { Origin = Origin, AccessToken = "rmm_test" },
                stub);
        }

        public RetroBatInstall Install { get; }

        public LocalStore Store { get; }

        public StubRomMServer Stub { get; }

        public static StateFixture Create()
        {
            var tree = TempRetroBatTree.Create();
            var install = tree.Install();

            return new StateFixture(tree, install, LocalStore.Open(install), new StubRomMServer
            {
                ServerDate = new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero),
            });
        }

        public void AddRom(int romId, string folder, string fileName)
        {
            var path = RelativePath.Create($"roms/{folder}/{fileName}");
            var absolute = Install.Resolve(path);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllText(absolute, "rom");

            Store.Files.Record(new LocalFile
            {
                Path = path,
                Folder = folder,
                RomId = romId,
                Kind = LocalFileKind.Rom,
                FileName = fileName,
                SizeBytes = 3,
            });
        }

        public void AddState(string directory, string fileName, string contents)
        {
            var absolute = Install.Resolve(RelativePath.Create($"saves/{directory}/{fileName}"));
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllText(absolute, contents);
        }

        public StateScanOutcome Scan() =>
            new StateScanner(Install, Store, Fixtures.LoadSaveStates()).Scan();

        public Task<StateSyncOutcome> PushAsync(CancellationToken cancellationToken = default) =>
            new StateSync(Install, Store, _connection).RunAsync(cancellationToken);

        public void Dispose()
        {
            _connection.Dispose();
            Store.Dispose();
            _tree.Dispose();
        }
    }
}
