using RomM.Client;
using RomMBat.Core.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The save protocol, and what happens to it when the server disappears.
/// </summary>
/// <remarks>
/// The offline half of this is the suite <c>docs/PLAN.md</c> calls the highest value in the
/// repository, and the reason is that being away from the server is the normal case for a
/// handheld rather than an error path. Every test here asserts the same thing from a different
/// angle: work completes or queues, nothing is lost, and a replay is free.
/// </remarks>
public class SaveSyncTests
{
    private static readonly Uri Origin = new("https://romm.invalid");
    private const string DeviceId = "device-under-test";

    [Fact]
    public async Task An_unsent_save_goes_up_and_the_server_name_is_persisted_while_a_different_one_stays_on_disk()
    {
        using var fixture = SyncFixture.Create();
        fixture.AddGame(42, "snes", "ActRaiser (USA)", ".zip", ".srm", "progress");
        fixture.Scan();

        fixture.Stub.NegotiateActions[(42, "libretro:battery")] = "upload";

        var outcome = await fixture.SyncAsync();

        Assert.Equal(1, outcome.Uploaded);
        Assert.Equal(0, outcome.Failed);

        var stored = Assert.Single(fixture.Stub.Saves.Values);
        Assert.Equal("ActRaiser (USA)", stored.FileNameNoTags);

        // The server's identity is the tagged name, and it is what gets persisted.
        var slot = fixture.Store.SaveSlots.Read(42, "libretro:battery");
        Assert.NotNull(slot);
        Assert.Contains("[", slot.FileName!, StringComparison.Ordinal);
        Assert.Equal("ActRaiser (USA)", slot.FileNameNoTags);
        Assert.Equal("srm", slot.FileExtension);

        // And the file on disk kept the name the emulator matches on. A file called
        // "ActRaiser (USA) [2026-...].srm" is invisible to it.
        Assert.True(File.Exists(fixture.Resolve("saves/snes/ActRaiser (USA).srm")));
        Assert.False(File.Exists(fixture.Resolve($"saves/snes/{stored.FileName}")));

        // Recorded as sent, which is what stops eviction refusing forever.
        Assert.False(Assert.Single(fixture.Store.Saves.List()).IsUnsent);
    }

    [Fact]
    public async Task Replaying_the_same_flush_uploads_nothing_new()
    {
        // The measurement this rests on: byte-identical content into one slot reuses the row.
        // It only holds because the content hash is deterministic, which is why the hash is
        // taken over logical contents rather than over anything a library version can change.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(42, "snes", "ActRaiser (USA)", ".zip", ".srm", "progress");
        fixture.Scan();
        fixture.Stub.NegotiateActions[(42, "libretro:battery")] = "upload";

        var first = await fixture.SyncAsync();
        Assert.Equal(1, first.Uploaded);

        var afterFirst = fixture.Stub.Saves.Count;

        // Cleared, so the second run negotiates for real rather than being told to upload
        // again. Leaving it set asserts the stub's content dedup, not the client's behaviour.
        fixture.Stub.NegotiateActions.Remove((42, "libretro:battery"));

        var second = await fixture.SyncAsync();

        Assert.Equal(0, second.Uploaded);
        Assert.Equal(0, second.Downloaded);
        Assert.True(second.IsNoOp);
        Assert.Equal(afterFirst, fixture.Stub.Saves.Count);
        Assert.Equal(2, fixture.Stub.CompletedSessions);
    }

    [Fact]
    public async Task A_download_asks_for_the_non_optimistic_form_and_acks_only_after_the_bytes_land()
    {
        using var fixture = SyncFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World)", ".zip", ".srm", "local, older");
        fixture.Scan();

        fixture.SeedServerSave(7, "libretro:battery", "Tetris (World)", "srm", "newer from another device");
        fixture.Stub.NegotiateActions[(7, "libretro:battery")] = "download";

        var outcome = await fixture.SyncAsync();

        Assert.Equal(1, outcome.Downloaded);

        // The parameter and the ack travel together, and neither is decoration.
        Assert.Empty(fixture.Stub.OptimisticDownloads);
        Assert.Equal([100], fixture.Stub.Acknowledged);

        Assert.Equal(
            "newer from another device",
            File.ReadAllText(fixture.Resolve("saves/gb/Tetris (World).srm")));
    }

    [Fact]
    public async Task A_slot_this_device_no_longer_holds_a_file_for_restores_into_its_roms_folder()
    {
        // The restore case every other download test skips, because they all seed a local save
        // first. Here the local file is gone and the slot's server identity is all that is
        // left, which is what a device looks like after the save was deleted or evicted and
        // another device then uploaded. Resolving the target only from local state answers
        // "nowhere to write it" and the save can never come back.
        using var fixture = SyncFixture.Create();

        // A second game with a save that stays put, because a device holding nothing at all
        // never negotiates: RunAsync has nothing to send and returns before asking. That is a
        // separate gap, and a library with more than one game is the ordinary case anyway.
        fixture.AddGame(42, "snes", "ActRaiser (USA)", ".zip", ".srm", "still here");
        fixture.AddGame(7, "gb", "Tetris (World)", ".zip", ".srm", "played once");
        fixture.Scan();
        fixture.Stub.NegotiateActions[(7, "libretro:battery")] = "upload";

        // Uploading is what records the slot's server-side identity: the untagged stem and the
        // extension, which is what a restore has to write on disk.
        await fixture.SyncAsync();

        // Then the file goes, and the next scan forgets the row that named its path.
        File.Delete(fixture.Resolve("saves/gb/Tetris (World).srm"));
        fixture.Scan();
        Assert.Equal(42, Assert.Single(fixture.Store.Saves.List()).RomId);

        // Another device uploads. Nothing local names the slot, so it cannot be in the request.
        fixture.Stub.NegotiateActions.Clear();
        fixture.SeedServerSave(7, "libretro:battery", "Tetris (World)", "srm", "from the other device");
        fixture.Stub.UnsolicitedDownloads.Add((7, "libretro:battery"));

        var outcome = await fixture.SyncAsync();

        Assert.Equal(1, outcome.Downloaded);
        Assert.Equal(0, outcome.Failed);

        // Back where libretro looks for it: the ROM's own folder, under the untagged name.
        Assert.Equal(
            "from the other device",
            File.ReadAllText(fixture.Resolve("saves/gb/Tetris (World).srm")));
    }

    [Fact]
    public async Task A_download_that_dies_mid_body_leaves_the_server_not_current_and_the_file_untouched()
    {
        // The failure F1 exists to prevent. Without optimistic=false the server would already
        // believe this device has the save, the next negotiate would answer no_op, and the
        // save would never come down again with nothing to show for it.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World)", ".zip", ".srm", "the local save, which must survive");
        fixture.Scan();

        fixture.SeedServerSave(7, "libretro:battery", "Tetris (World)", "srm", new string('x', 4096));
        fixture.Stub.NegotiateActions[(7, "libretro:battery")] = "download";
        fixture.Stub.TruncateSaveDownloadAfter = 512;

        var outcome = await fixture.SyncAsync();

        Assert.Equal(0, outcome.Downloaded);
        Assert.Equal(1, outcome.Failed);

        // Never acked, so the server still offers it next time.
        Assert.Empty(fixture.Stub.Acknowledged);
        Assert.Empty(fixture.Stub.OptimisticDownloads);

        // And the local file is exactly as it was: the partial never reached it.
        Assert.Equal(
            "the local save, which must survive",
            File.ReadAllText(fixture.Resolve("saves/gb/Tetris (World).srm")));
    }

    [Fact]
    public async Task A_download_whose_bytes_do_not_match_the_hash_is_thrown_away_rather_than_written()
    {
        using var fixture = SyncFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World)", ".zip", ".srm", "the local save");
        fixture.Scan();

        fixture.SeedServerSave(7, "libretro:battery", "Tetris (World)", "srm", "server bytes", lieAboutHash: true);
        fixture.Stub.NegotiateActions[(7, "libretro:battery")] = "download";

        var outcome = await fixture.SyncAsync();

        Assert.Equal(1, outcome.Failed);
        Assert.Empty(fixture.Stub.Acknowledged);
        Assert.Equal("the local save", File.ReadAllText(fixture.Resolve("saves/gb/Tetris (World).srm")));
        Assert.Contains(outcome.Problems, problem => problem.Contains("hashes to", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_restore_copies_the_existing_save_aside_before_replacing_it()
    {
        using var fixture = SyncFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World)", ".zip", ".srm", "what was here before");
        fixture.Scan();

        fixture.SeedServerSave(7, "libretro:battery", "Tetris (World)", "srm", "what came down");
        fixture.Stub.NegotiateActions[(7, "libretro:battery")] = "download";

        await fixture.SyncAsync();

        var aside = Directory.GetFiles(fixture.Resolve(SaveSync.AsideDirectory.Value));
        Assert.Single(aside);
        Assert.Equal("what was here before", File.ReadAllText(aside[0]));
        Assert.Equal("what came down", File.ReadAllText(fixture.Resolve("saves/gb/Tetris (World).srm")));
    }

    [Fact]
    public async Task A_restored_save_is_not_offered_straight_back_up()
    {
        // Without recording the restore as already uploaded, the next scan reads the file as
        // unsent, negotiate is told about it as a change, and eviction refuses the game forever.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World)", ".zip", ".srm", "local");
        fixture.Scan();

        fixture.SeedServerSave(7, "libretro:battery", "Tetris (World)", "srm", "from the other device");
        fixture.Stub.NegotiateActions[(7, "libretro:battery")] = "download";

        await fixture.SyncAsync();
        fixture.Scan();

        var save = Assert.Single(fixture.Store.Saves.List());
        Assert.False(save.IsUnsent);
        Assert.False(save.HasChangedSinceUpload);
        Assert.True(new SaveGuard(fixture.Store).Check(7, RelativePath.Create("roms/gb/Tetris (World).zip")).CanRemove);
    }

    [Fact]
    public async Task A_conflict_overwrites_nothing_and_keeps_a_copy_of_the_local_file()
    {
        using var fixture = SyncFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World)", ".zip", ".srm", "what this device did");
        fixture.Scan();

        fixture.SeedServerSave(7, "libretro:battery", "Tetris (World)", "srm", "what the other device did");
        fixture.Stub.NegotiateActions[(7, "libretro:battery")] = "conflict";

        var outcome = await fixture.SyncAsync();

        Assert.Equal(1, outcome.Conflicts);
        Assert.Equal(0, outcome.Uploaded);
        Assert.Equal(0, outcome.Downloaded);

        // Neither side thrown away.
        Assert.Equal("what this device did", File.ReadAllText(fixture.Resolve("saves/gb/Tetris (World).srm")));

        var conflict = Assert.Single(outcome.Unresolved);
        Assert.NotNull(conflict.LocalCopy);
        Assert.Equal("what this device did", File.ReadAllText(fixture.Resolve(conflict.LocalCopy.Value.Value)));
        Assert.NotEqual(conflict.LocalHash, conflict.ServerHash);
    }

    [Fact]
    public async Task A_conflict_for_a_slot_this_device_did_not_submit_costs_one_operation()
    {
        // save_conflict.local_path is NOT NULL and CHECKs for a non-blank value, so recording a
        // conflict with no local save behind it raised SQLITE_CONSTRAINT_CHECK out of the flush,
        // taking the states pass down with it. Measurement 132 says negotiate never volunteers a
        // slot like this, which is why it is a guard and a reported problem rather than a
        // download path.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World)", ".zip", ".srm", "what this device did");
        fixture.Scan();

        fixture.SeedServerSave(9, "libretro:battery", "Zelda (USA)", "srm", "a game never played here");
        fixture.Stub.UnsolicitedConflicts.Add((9, "libretro:battery"));
        fixture.Stub.NegotiateActions[(7, "libretro:battery")] = "upload";

        var outcome = await fixture.SyncAsync();

        Assert.Equal(0, outcome.Conflicts);
        Assert.Equal(1, outcome.Failed);
        Assert.Contains(
            outcome.Problems,
            problem => problem.Contains("no local save to act on", StringComparison.Ordinal));

        // Nothing was written for it, and the slot this device did submit still went up.
        Assert.Empty(fixture.Store.SaveConflicts.List());
        Assert.Equal(1, outcome.Uploaded);
    }

    [Fact]
    public async Task A_409_on_upload_is_surfaced_rather_than_retried_with_overwrite()
    {
        using var fixture = SyncFixture.Create();
        fixture.AddGame(42, "snes", "ActRaiser (USA)", ".zip", ".srm", "progress");
        fixture.Scan();

        fixture.Stub.NegotiateActions[(42, "libretro:battery")] = "upload";
        fixture.Stub.ConflictOnUpload.Add((42, "libretro:battery"));

        var outcome = await fixture.SyncAsync();

        Assert.Equal(0, outcome.Uploaded);
        Assert.Equal(1, outcome.Failed);
        Assert.Contains(
            outcome.Problems,
            problem => problem.Contains("newer save since your last sync", StringComparison.Ordinal));

        // Still unsent, so nothing believes this reached the server.
        Assert.True(Assert.Single(fixture.Store.Saves.List()).IsUnsent);
    }

    [Fact]
    public async Task Everything_produced_offline_queues_and_one_flush_lands_all_of_it()
    {
        // The plan's own "done when", minus the hardware: three games played with the server
        // unplugged, then one flush.
        using var fixture = SyncFixture.Create();

        for (var i = 0; i < 3; i++)
        {
            fixture.AddGame(10 + i, "snes", $"Game {i}", ".zip", ".srm", $"save {i}");
            fixture.PlaySession(10 + i, $"Game {i}");
            fixture.Stub.NegotiateActions[(10 + i, "libretro:battery")] = "upload";
        }

        fixture.Stub.IsReachable = false;

        // Offline: the local half still runs and everything else queues.
        fixture.Scan();
        var correlated = fixture.Correlate();

        Assert.Equal(3, correlated.Sessions);
        Assert.Equal(3, fixture.Store.Outbox.PendingCount());

        var offlineOutcome = await fixture.SyncAsync();
        Assert.Equal(3, offlineOutcome.Failed);
        Assert.Equal(3, fixture.Store.Outbox.PendingCount());
        Assert.All(fixture.Store.Saves.List(), save => Assert.True(save.IsUnsent));

        // Plugged back in: one flush, and all three saves and all three sessions land.
        fixture.Stub.IsReachable = true;

        var playtime = await fixture.FlushPlaytimeAsync();
        var saves = await fixture.SyncAsync();

        Assert.Equal(3, playtime.Sent);
        Assert.Equal(0, playtime.Failed);
        Assert.Equal(3, saves.Uploaded);
        Assert.Equal(0, fixture.Store.Outbox.PendingCount());
        Assert.All(fixture.Store.Saves.List(), save => Assert.False(save.IsUnsent));
    }

    [Fact]
    public async Task A_directory_save_queues_offline_and_lands_in_one_flush()
    {
        // The offline simulation extended to class C. Same assertion as the class A case: every
        // operation completes locally or queues, and one flush lands all of it.
        using var fixture = SyncFixture.Create();
        fixture.AddUnit(8, "25pacman", ("eeprom", "one"), ("flash", "two"));

        fixture.Stub.IsReachable = false;

        // The scan is entirely local, so being offline costs it nothing.
        var offlineScan = fixture.Scan();

        Assert.Equal(1, offlineScan.Units);
        Assert.Equal(1, offlineScan.UnitsAttributed);

        var offline = await fixture.SyncAsync();

        Assert.Equal(0, offline.Uploaded);
        Assert.All(fixture.Store.Saves.List(), save => Assert.True(save.IsUnsent));

        fixture.Stub.IsReachable = true;
        fixture.Stub.NegotiateActions[(8, "mame:nvram")] = "upload";

        var online = await fixture.SyncAsync();

        Assert.Equal(1, online.Uploaded);
        Assert.All(fixture.Store.Saves.List(), save => Assert.False(save.IsUnsent));

        // One archive on the server holding both members, not two saves.
        var uploaded = Assert.Single(fixture.Stub.Saves.Values);
        Assert.Equal("mame:nvram", uploaded.Slot);
    }

    [Fact]
    public async Task Replaying_a_directory_save_flush_sends_nothing_further()
    {
        // Idempotence under replay, which is what makes a flush interrupted halfway safe. It
        // rests on the archive being deterministic and on the wire hash being the one the
        // server itself returned.
        using var fixture = SyncFixture.Create();
        fixture.AddUnit(8, "25pacman", ("eeprom", "one"), ("flash", "two"));

        fixture.Scan();
        fixture.Stub.NegotiateActions[(8, "mame:nvram")] = "upload";
        Assert.Equal(1, (await fixture.SyncAsync()).Uploaded);

        var afterFirst = fixture.Stub.Saves.Count;

        // Cleared so the replay negotiates for real rather than being told to upload again.
        fixture.Stub.NegotiateActions.Clear();

        fixture.Scan();
        var replay = await fixture.SyncAsync();

        Assert.Equal(0, replay.Uploaded);
        Assert.Equal(afterFirst, fixture.Stub.Saves.Count);
    }

    [Fact]
    public async Task A_changed_directory_save_goes_up_again_and_an_unchanged_one_does_not()
    {
        // The two halves of the two-hash design, which is the part most likely to be wrong in a
        // way nothing notices: send the wrong value and a unit either uploads forever or never
        // uploads again.
        using var fixture = SyncFixture.Create();
        fixture.AddUnit(8, "25pacman", ("eeprom", "one"));

        fixture.Scan();
        fixture.Stub.NegotiateActions[(8, "mame:nvram")] = "upload";
        await fixture.SyncAsync();
        fixture.Stub.NegotiateActions.Clear();

        // Unchanged: the fold matches what was uploaded, so the wire carries the server's own
        // digest and negotiate answers no_op.
        fixture.Scan();
        Assert.Equal(0, (await fixture.SyncAsync()).Uploaded);

        // Changed: a new member appears, so the fold moves and the unit is sent whole again.
        File.WriteAllText(
            fixture.Resolve("saves/mame/nvram/25pacman/flash"),
            "a second member the game just wrote");

        fixture.Scan();
        Assert.True(fixture.Store.Saves.List().Single(save => save.ShapeClass == SaveShapeClass.C).HasChangedSinceUpload);

        fixture.Stub.NegotiateActions[(8, "mame:nvram")] = "upload";
        Assert.Equal(1, (await fixture.SyncAsync()).Uploaded);
    }

    [Fact]
    public async Task Everything_this_stage_adds_also_queues_offline_and_lands_in_one_flush()
    {
        // The offline simulation extended to the shapes this stage adds. Same assertion as the
        // three-games case above, in the shapes that carry a state, a screenshot and a conflict
        // rather than three class-A saves.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(42, "snes", "ActRaiser (USA)", ".zip", ".srm", "battery progress");
        fixture.AddState("snes/libretro.snes9x", "ActRaiser (USA).state1", "state progress");
        fixture.AddState("snes/libretro.snes9x", "ActRaiser (USA).state1.png", "screenshot");
        fixture.AddState("snes/libretro.bsnes", "ActRaiser (USA).state1", "other core");

        fixture.Stub.IsReachable = false;

        // Offline: both scans run, because the local half never needs a server.
        Assert.Equal(1, fixture.Scan().Found);
        Assert.Equal(2, fixture.ScanStates().Found);

        var offlineSaves = await fixture.SyncAsync();
        var offlineStates = await fixture.PushStatesAsync();

        Assert.Equal(0, offlineSaves.Uploaded);
        Assert.Equal(0, offlineStates.Uploaded);
        Assert.All(fixture.Store.Saves.List(), save => Assert.True(save.IsUnsent));
        Assert.All(fixture.Store.States.List(), state => Assert.True(state.IsUnsent));

        // Nothing threw, which is the assertion. An unreachable server is a working state.
        Assert.NotEmpty(offlineStates.Problems);

        fixture.Stub.IsReachable = true;
        fixture.Stub.NegotiateActions[(42, "libretro:battery")] = "upload";

        var saves = await fixture.SyncAsync();
        var states = await fixture.PushStatesAsync();

        Assert.Equal(1, saves.Uploaded);
        Assert.Equal(2, states.Uploaded);
        Assert.All(fixture.Store.Saves.List(), save => Assert.False(save.IsUnsent));
        Assert.All(fixture.Store.States.List(), state => Assert.False(state.IsUnsent));

        // Replaying the whole flush sends nothing further and creates nothing further.
        var serverStates = fixture.Stub.States.Count;

        // Cleared so the replay negotiates for real. Leaving it set would assert the stub's
        // content dedup rather than the client declining to send.
        fixture.Stub.NegotiateActions.Remove((42, "libretro:battery"));

        fixture.Scan();
        fixture.ScanStates();

        Assert.Equal(0, (await fixture.SyncAsync()).Uploaded);
        Assert.Equal(0, (await fixture.PushStatesAsync()).Uploaded);
        Assert.Equal(serverStates, fixture.Stub.States.Count);
    }

    [Fact]
    public async Task A_replayed_play_session_batch_is_reconciled_per_index_rather_than_inferred()
    {
        using var fixture = SyncFixture.Create();
        fixture.AddGame(10, "snes", "Game", ".zip", ".srm", "x");
        fixture.PlaySession(10, "Game");
        fixture.Correlate();

        var first = await fixture.FlushPlaytimeAsync();
        Assert.Equal(1, first.Sent);
        Assert.Equal(0, first.Duplicates);

        // Queue the identical session again, which is what a replayed flush produces.
        fixture.PlaySession(10, "Game");
        fixture.Correlate();

        var second = await fixture.FlushPlaytimeAsync();

        Assert.Equal(0, second.Sent);
        Assert.Equal(1, second.Duplicates);
        Assert.Equal(0, second.Failed);
        Assert.Equal(0, fixture.Store.Outbox.PendingCount());
    }

    [Fact]
    public async Task A_batch_the_server_refuses_stays_queued_for_the_next_flush()
    {
        using var fixture = SyncFixture.Create();
        fixture.AddGame(10, "snes", "Game", ".zip", ".srm", "x");
        fixture.PlaySession(10, "Game");
        fixture.Correlate();

        fixture.Stub.IsReachable = false;

        var outcome = await fixture.FlushPlaytimeAsync();

        Assert.Equal(0, outcome.Sent);
        Assert.Equal(1, outcome.Failed);

        // Failure does not consume the entry: being offline is normal and a replay is safe.
        Assert.Equal(1, fixture.Store.Outbox.PendingCount());

        fixture.Stub.IsReachable = true;
        Assert.Equal(1, (await fixture.FlushPlaytimeAsync()).Sent);
    }

    [Fact]
    public async Task A_save_this_device_uploaded_is_not_fetched_back_down()
    {
        // origin_device_id names the uploader, so a download offered for bytes this device
        // already holds and itself sent is a transfer nobody needs.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(42, "snes", "ActRaiser (USA)", ".zip", ".srm", "progress");
        fixture.Scan();

        fixture.Stub.NegotiateActions[(42, "libretro:battery")] = "upload";
        await fixture.SyncAsync();

        // Now the server offers it back, which is what a second device's negotiate looks like
        // from here after this device uploaded.
        fixture.Stub.NegotiateActions[(42, "libretro:battery")] = "download";

        var outcome = await fixture.SyncAsync();

        Assert.Equal(0, outcome.Downloaded);
        Assert.Equal(1, outcome.NoOps);
        Assert.Empty(fixture.Stub.Acknowledged);
    }

    [Fact]
    public async Task Negotiate_sends_the_files_real_mtime_and_never_the_sync_time()
    {
        // Sending the sync time makes every offline edit lose every conflict it is in.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(42, "snes", "ActRaiser (USA)", ".zip", ".srm", "progress");

        var mtime = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        File.SetLastWriteTimeUtc(fixture.Resolve("saves/snes/ActRaiser (USA).srm"), mtime.UtcDateTime);

        fixture.Scan();
        await fixture.SyncAsync();

        var save = Assert.Single(fixture.Store.Saves.List());
        Assert.Equal(mtime, save.FileMtimeUtc);
    }

    /// <summary>An install, a store, a stub server and the plumbing between them.</summary>
    private sealed class SyncFixture : IDisposable
    {
        private readonly TempRetroBatTree _tree;
        private readonly RomMConnection _connection;

        private SyncFixture(TempRetroBatTree tree, RetroBatInstall install, LocalStore store, StubRomMServer stub)
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

        public static SyncFixture Create()
        {
            var tree = TempRetroBatTree.Create();
            var install = tree.Install();

            return new SyncFixture(tree, install, LocalStore.Open(install), new StubRomMServer
            {
                ServerDate = new DateTimeOffset(2026, 8, 16, 12, 0, 0, TimeSpan.Zero),
            });
        }

        public string Resolve(string relative) => Install.Resolve(RelativePath.Create(relative));

        /// <summary>Puts a ROM and its battery save on disk, and indexes the ROM.</summary>
        public void AddGame(
            int romId,
            string folder,
            string stem,
            string romExtension,
            string saveExtension,
            string saveContents)
        {
            var romPath = RelativePath.Create($"roms/{folder}/{stem}{romExtension}");
            var romAbsolute = Install.Resolve(romPath);
            Directory.CreateDirectory(Path.GetDirectoryName(romAbsolute)!);
            File.WriteAllText(romAbsolute, "rom");

            Store.Files.Record(new LocalFile
            {
                Path = romPath,
                Folder = folder,
                RomId = romId,
                Kind = LocalFileKind.Rom,
                FileName = $"{stem}{romExtension}",
                SizeBytes = 3,
            });

            var savePath = Install.Resolve(RelativePath.Create($"saves/{folder}/{stem}{saveExtension}"));
            Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
            File.WriteAllText(savePath, saveContents);
        }

        /// <summary>Puts a save on the stub server, as another device would have.</summary>
        public void SeedServerSave(
            int romId,
            string slot,
            string stem,
            string extension,
            string contents,
            bool lieAboutHash = false)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(contents);

            Stub.Saves[100] = new StubRomMServer.StubSave
            {
                Id = 100,
                RomId = romId,
                Slot = slot,
                Emulator = "libretro",
                Bytes = bytes,
                FileNameNoTags = stem,
                FileExtension = extension,
                OriginDeviceId = "some-other-device",
                UpdatedAt = Stub.ServerDate ?? DateTimeOffset.UnixEpoch,
            };

            if (lieAboutHash)
            {
                // The server naming a hash the bytes do not match, which is what a corrupted
                // transfer looks like from the client's side.
                Stub.HashLie = "ffffffffffffffffffffffffffffffff";
            }
        }

        /// <summary>Writes a game-start and game-end pair straight into the journal.</summary>
        public void PlaySession(int romId, string stem)
        {
            var file = Store.Files.List().First(entry => entry.RomId == romId);

            Store.Journal.Append(
                JournalEvent.GameStart,
                new DateTimeOffset(2026, 8, 16, 10, 0, 0, TimeSpan.Zero),
                file.Path,
                stem,
                stem);

            Store.Journal.Append(
                JournalEvent.GameEnd,
                new DateTimeOffset(2026, 8, 16, 10, 30, 0, TimeSpan.Zero));
        }

        /// <summary>
        /// Puts a MAME rom and its nvram unit on disk, which is class C needing no attribution.
        /// </summary>
        /// <remarks>
        /// MAME because its unit key is the rom basename, so the unit attributes through the
        /// same index class A uses and this stays a test about syncing rather than about the
        /// attribution routes, which have their own suite.
        /// </remarks>
        public void AddUnit(int romId, string shortName, params (string Name, string Contents)[] members)
        {
            var romPath = RelativePath.Create($"roms/mame/{shortName}.zip");
            var romAbsolute = Install.Resolve(romPath);
            Directory.CreateDirectory(Path.GetDirectoryName(romAbsolute)!);
            File.WriteAllText(romAbsolute, "rom");

            Store.Files.Record(new LocalFile
            {
                Path = romPath,
                Folder = "mame",
                RomId = romId,
                Kind = LocalFileKind.Rom,
                FileName = $"{shortName}.zip",
                SizeBytes = 3,
            });

            foreach (var (name, contents) in members)
            {
                var member = Install.Resolve(RelativePath.Create($"saves/mame/nvram/{shortName}/{name}"));
                Directory.CreateDirectory(Path.GetDirectoryName(member)!);
                File.WriteAllText(member, contents);
            }
        }

        /// <summary>Puts a save state, or a file beside one, into a state directory.</summary>
        public void AddState(string directory, string fileName, string contents)
        {
            var absolute = Install.Resolve(RelativePath.Create($"saves/{directory}/{fileName}"));
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllText(absolute, contents);
        }

        public SaveScanOutcome Scan() => new SaveScanner(Install, Store).Scan();

        public StateScanOutcome ScanStates() =>
            new StateScanner(Install, Store, Fixtures.LoadSaveStates()).Scan();

        public CorrelationOutcome Correlate() => new PlaytimeCorrelator(Install, Store).Correlate();

        public Task<SaveSyncOutcome> SyncAsync() =>
            new SaveSync(Install, Store, _connection, DeviceId).RunAsync();

        public Task<StateSyncOutcome> PushStatesAsync() =>
            new StateSync(Install, Store, _connection).RunAsync();

        public Task<OutboxFlushOutcome> FlushPlaytimeAsync() =>
            new OutboxFlush(Store, _connection, DeviceId).FlushPlaySessionsAsync();

        public void Dispose()
        {
            _connection.Dispose();
            Stub.Dispose();
            Store.Dispose();
            _tree.Dispose();
        }
    }
}
