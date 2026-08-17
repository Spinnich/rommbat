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

        public SaveScanOutcome Scan() => new SaveScanner(Install, Store).Scan();

        public CorrelationOutcome Correlate() => new PlaytimeCorrelator(Install, Store).Correlate();

        public Task<SaveSyncOutcome> SyncAsync() =>
            new SaveSync(Install, Store, _connection, DeviceId).RunAsync();

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
