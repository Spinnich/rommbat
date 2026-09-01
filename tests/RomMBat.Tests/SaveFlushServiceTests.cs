using RomM.Client;
using RomMBat.Core;
using RomMBat.Core.Identity;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The flush, driven without a console.
/// </summary>
/// <remarks>
/// <b>Everything asserted here used to need a redirected <c>Console</c> to observe.</b> The pass
/// was 289 lines welded to <c>Console.WriteLine</c>, so the only way to check that the lock
/// refusal was benign, or that states were sent after saves, was to compare the positions of two
/// printed strings. That couples a rule to how one front end happens to format it and says
/// nothing about the path the other one takes through the same code.
/// <para>
/// The three rules below are each load-bearing and each predates this seam. They are asserted
/// against the service rather than against the agent, because the gamepad UI runs the same pass.
/// </para>
/// </remarks>
public sealed class SaveFlushServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Failing_to_take_the_tree_lock_is_an_outcome_rather_than_an_error()
    {
        // Two flushes overlap whenever somebody runs one beside a sync, and the measured case
        // of three game-end hooks in flight at once applies once anything invokes it from the
        // launch path. The second exits rather than waiting, because the work is already being
        // done and waiting would put a process to sleep inside the game-launch path.
        using var fixture = FlushTree.Create();

        using var held = TreeLock.TryAcquire(fixture.Session.Install);
        Assert.NotNull(held);

        var report = await fixture.RunAsync();

        Assert.Equal(FlushState.Skipped, report.State);

        // A value with its own sentence, never an exception and never a null report. This is
        // what keeps a front end from ever having to name TreeLock to explain itself.
        Assert.NotNull(report.Problem);

        // Nothing ran, so nothing is claimed to have run.
        Assert.False(report.Scanned);
        Assert.Null(report.Drained);
    }

    [Fact]
    public async Task The_local_half_runs_with_the_server_unreachable_and_nothing_it_found_is_lost()
    {
        // Offline is a working state and the headline feature of M6. Draining, correlating and
        // both scans answer from the tree, so an unreachable server costs the sending only.
        using var fixture = FlushTree.Create();
        fixture.Pair();
        fixture.AddGame(42, "snes", "ActRaiser (USA)");
        fixture.Stub.IsReachable = false;

        var report = await fixture.RunAsync(fixture.Connect());

        // Partial rather than Unreachable, and that is the behaviour this lift preserved rather
        // than chose. All three sending passes absorb RomMUnreachableException per item and
        // report it, so the outer catch that FlushCommand has always carried never fires for
        // them. Recorded as what it is rather than tidied away inside a refactor.
        Assert.Equal(FlushState.Partial, report.State);

        // The half that needs no server ran anyway, and said so.
        Assert.True(report.Scanned);
        Assert.Equal(1, report.Saves!.Found);
        Assert.Equal(1, report.Saves.Attributed);
        Assert.NotNull(report.Drained);
        Assert.NotNull(report.Correlated);

        // And nothing crossed the network, so nothing is half sent. The save stays on disk,
        // recorded and unsent, for the next pass to try again.
        Assert.Equal(0, report.SavesSent!.Uploaded);
        Assert.Empty(fixture.Stub.Saves);
        Assert.Null(fixture.Session.Store.SaveSlots.Read(42, "libretro:battery"));
    }

    [Fact]
    public async Task A_flush_with_no_pairing_still_does_everything_the_tree_can_answer()
    {
        // The same rule from the other side: a caller that could not authenticate passes no
        // connection, and the local pass is not the thing being refused.
        using var fixture = FlushTree.Create();
        fixture.AddGame(42, "snes", "ActRaiser (USA)");

        var report = await fixture.RunAsync();

        Assert.Equal(FlushState.NotPaired, report.State);
        Assert.True(report.Scanned);
        Assert.Equal(1, report.Saves!.Attributed);
        Assert.False(report.Sent);
    }

    [Fact]
    public async Task States_are_scanned_before_saves_because_the_save_scan_reads_what_the_state_scan_writes()
    {
        // #64. The sidecar attribution route reads local_state and SaveScanner is what runs it,
        // so scanning saves first left the route reading an empty table on the first flush after
        // an install is set up, and the class C saves it would have attributed went up on the
        // second flush instead.
        //
        // Observed through the clock rather than through a class C fixture: each scan stamps its
        // rows with one reading of the TimeProvider, so a ticking clock puts the two scans in an
        // order a swap of the two statements reverses.
        using var fixture = FlushTree.Create(new TickingClock(Now));
        fixture.WriteSaveStateSchema();
        fixture.AddGame(42, "snes", "ActRaiser (USA)");
        fixture.AddSave("snes/libretro.snes9x", "ActRaiser (USA).state1", "a state");

        var report = await fixture.RunAsync();

        Assert.NotNull(report.States);
        Assert.Equal(1, report.States.Found);
        Assert.Equal(1, report.Saves!.Found);

        var state = Assert.Single(fixture.Session.Store.States.List());
        var save = Assert.Single(fixture.Session.Store.Saves.List());

        Assert.True(
            state.ScannedAtUtc < save.ScannedAtUtc,
            $"the state scan stamped {state.ScannedAtUtc:O} and the save scan {save.ScannedAtUtc:O}, "
                + "so the saves were scanned first");
    }

    [Fact]
    public async Task States_go_up_last_because_they_are_the_only_part_nobody_has_to_act_on()
    {
        // Nothing about a state negotiates, nothing conflicts, and an unsent state is simply
        // sent again next time. Play sessions and saves both produce something a user may have
        // to answer, so they go first while there is still a link.
        using var fixture = FlushTree.Create();
        fixture.Pair();
        fixture.WriteSaveStateSchema();
        fixture.AddGame(42, "snes", "ActRaiser (USA)");
        fixture.AddSave("snes/libretro.snes9x", "ActRaiser (USA).state1", "a state");
        fixture.WantsUpload(42);

        var report = await fixture.RunAsync(fixture.Connect());

        Assert.True(report.Sent);
        Assert.NotNull(report.StatesSent);

        var saveAt = LastIndexOf(fixture.Stub.RequestLog, "/api/saves");
        var stateAt = LastIndexOf(fixture.Stub.RequestLog, "/api/states");

        Assert.True(saveAt >= 0, "no save was sent, so the ordering was never exercised");
        Assert.True(stateAt >= 0, "no state was sent, so the ordering was never exercised");
        Assert.True(saveAt < stateAt, "the states went up before the saves");
    }

    [Fact]
    public async Task A_second_pass_over_an_unchanged_tree_sends_nothing_and_says_so()
    {
        // Draining is idempotent and a spool file waits indefinitely, which is the whole
        // premise of a pass with no daemon to live in: anything may invoke it, at any time,
        // as often as it likes.
        using var fixture = FlushTree.Create();
        fixture.Pair();
        fixture.WriteSaveStateSchema();
        fixture.AddGame(42, "snes", "ActRaiser (USA)");
        fixture.WantsUpload(42);

        var first = await fixture.RunAsync(fixture.Connect());

        Assert.Equal(FlushState.Done, first.State);
        Assert.Equal(1, first.SavesSent!.Uploaded);

        var afterFirst = fixture.Stub.RequestLog.Count;

        // Cleared, so the second pass negotiates for real rather than being told to upload
        // again. Leaving it set asserts the stub's content dedup, not the client's behaviour.
        fixture.StopWantingUpload(42);

        var second = await fixture.RunAsync(fixture.Connect());

        Assert.Equal(FlushState.Done, second.State);

        // The save is still found, because finding it is what the scan does. What must not
        // happen twice is the upload.
        Assert.Equal(1, second.Saves!.Found);
        Assert.Equal(0, second.SavesSent!.Uploaded);
        Assert.Equal(0, second.SavesSent.Failed);
        Assert.Equal(0, second.SavesSent.Conflicts);

        // The negotiation still happens, so this is not asserting that nothing was asked. It is
        // asserting that nothing was written twice.
        Assert.Single(fixture.Stub.Saves);
        Assert.True(fixture.Stub.RequestLog.Count > afterFirst);
    }

    private static int LastIndexOf(IReadOnlyList<string> log, string prefix)
    {
        for (var index = log.Count - 1; index >= 0; index--)
        {
            if (log[index].StartsWith(prefix, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>A clock that moves one second every time it is read.</summary>
    /// <remarks>
    /// Each scan takes one reading and stamps every row it writes with it, so this is what makes
    /// "which scan ran first" a thing a test can see. A fixed clock would stamp both scans
    /// identically and the ordering assertion would hold whichever way round they ran.
    /// </remarks>
    private sealed class TickingClock(DateTimeOffset start) : TimeProvider
    {
        private long _ticks;

        public override DateTimeOffset GetUtcNow() =>
            start.AddSeconds(Interlocked.Increment(ref _ticks));
    }

    private sealed class FlushTree : IDisposable
    {
        private static readonly Uri Origin = new("https://romm.invalid");

        private readonly TempRetroBatTree _tree;
        private readonly TimeProvider? _time;
        private readonly List<RomMConnection> _connections = [];

        private FlushTree(TempRetroBatTree tree, InstallSession session, TimeProvider? time)
        {
            _tree = tree;
            Session = session;
            _time = time;
        }

        public InstallSession Session { get; }

        public StubRomMServer Stub { get; } = new() { ServerDate = Now };

        public static FlushTree Create(TimeProvider? time = null)
        {
            var tree = TempRetroBatTree.Create();
            var session = InstallSession.Open(tree.Root).Session!;

            return new FlushTree(tree, session, time);
        }

        public Task<FlushReport> RunAsync(RomMConnection? connection = null) =>
            new SaveFlushService(Session, _time)
                .RunAsync(new FlushOptions(), connection, TestContext.Current.CancellationToken);

        public RomMConnection Connect()
        {
            var connection = new RomMConnection(
                new RomMClientOptions { Origin = Origin, AccessToken = "rmm_test" },
                Stub);

            _connections.Add(connection);
            return connection;
        }

        public void Pair()
        {
            Session.Store.Device.EnsureIdentity(DeviceIdentity.ReadOrCreate(Session.Install));
            Session.Store.Device.SavePairing(
                new PairingResult(
                    Origin,
                    "device-1",
                    "Handheld",
                    new GrantedScopes(["roms.read", "assets.read", "assets.write"]),
                    TokenProtector.Protect("rmm_token", null, Now.AddYears(1))),
                Now);
        }

        /// <summary>
        /// Puts <c>es_savestates.cfg</c> in the tree, which is what makes states exist at all.
        /// </summary>
        /// <remarks>
        /// The file ships with RetroBat, so an install without one is a real fact rather than a
        /// case to pass over, and the flush says so. A test about state ordering has to put it
        /// there or it is asserting the ordering of a pass that never ran.
        /// </remarks>
        public void WriteSaveStateSchema()
        {
            var target = Session.Install.Resolve(SaveStateSchema.ConfigPath);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Fixtures.EsSaveStatesTemplate, target, overwrite: true);
        }

        /// <summary>Tells the stub to ask for this game's battery save.</summary>
        /// <remarks>
        /// The negotiation decides whether anything is uploaded at all, so without this the
        /// pass has nothing to send and an ordering assertion over the sends would hold
        /// vacuously.
        /// </remarks>
        public void WantsUpload(int romId) =>
            Stub.NegotiateActions[(romId, "libretro:battery")] = "upload";

        public void StopWantingUpload(int romId) =>
            Stub.NegotiateActions.Remove((romId, "libretro:battery"));

        /// <summary>A ROM on disk and indexed, with an unsent battery save beside it.</summary>
        public void AddGame(int romId, string folder, string stem)
        {
            AddRom(romId, folder, $"{stem}.zip");
            AddSave(folder, $"{stem}.srm", "battery bytes");
        }

        public void AddRom(int romId, string folder, string fileName)
        {
            var path = RelativePath.Create($"roms/{folder}/{fileName}");
            var absolute = Session.Install.Resolve(path);

            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllText(absolute, "rom bytes");

            Session.Store.Files.Record(new LocalFile
            {
                Path = path,
                Folder = folder,
                RomId = romId,
                Kind = LocalFileKind.Rom,
                FileName = fileName,
                SizeBytes = 9,
            });
        }

        public void AddSave(string system, string relative, string contents)
        {
            var absolute = Session.Install.Resolve(RelativePath.Create($"saves/{system}/{relative}"));
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllText(absolute, contents);
        }

        public void Dispose()
        {
            foreach (var connection in _connections)
            {
                connection.Dispose();
            }

            Session.Dispose();
            _tree.Dispose();
            Stub.Dispose();
        }
    }
}
