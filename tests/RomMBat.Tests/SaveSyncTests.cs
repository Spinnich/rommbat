using System.IO.Compression;
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

        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

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

        var first = await fixture.SyncAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, first.Uploaded);

        var afterFirst = fixture.Stub.Saves.Count;

        // Cleared, so the second run negotiates for real rather than being told to upload
        // again. Leaving it set asserts the stub's content dedup, not the client's behaviour.
        fixture.Stub.NegotiateActions.Remove((42, "libretro:battery"));

        var second = await fixture.SyncAsync(TestContext.Current.CancellationToken);

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

        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

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
        await fixture.SyncAsync(TestContext.Current.CancellationToken);

        // Then the file goes, and the next scan forgets the row that named its path.
        File.Delete(fixture.Resolve("saves/gb/Tetris (World).srm"));
        fixture.Scan();
        Assert.Equal(42, Assert.Single(fixture.Store.Saves.List()).RomId);

        // Another device uploads. Nothing local names the slot, so it cannot be in the request.
        fixture.Stub.NegotiateActions.Clear();
        fixture.SeedServerSave(7, "libretro:battery", "Tetris (World)", "srm", "from the other device");
        fixture.Stub.UnsolicitedDownloads.Add((7, "libretro:battery"));

        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

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

        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

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

        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

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

        await fixture.SyncAsync(TestContext.Current.CancellationToken);

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

        await fixture.SyncAsync(TestContext.Current.CancellationToken);
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

        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

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

        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

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
    public async Task A_409_on_upload_becomes_a_conflict_the_user_can_resolve()
    {
        // Stage 1 reported a 409 as a failure with a message. Driven on real hardware in the
        // 2b hands-on pass, that turned out to be the only outcome a genuine two-sided
        // divergence produces: a PSP save changed on both sides negotiated as `upload`, because
        // negotiate decides from the hashes it was handed and the client's mtime was newer, and
        // the server then refused with 409 because this device's sync record was stale, which is
        // the part negotiate could not see.
        //
        // Reported as a failure it is retried forever and never resolved. It is a conflict: both
        // sides moved, and only a person can say which one matters.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(42, "snes", "ActRaiser (USA)", ".zip", ".srm", "progress");
        fixture.Scan();

        fixture.Stub.NegotiateActions[(42, "libretro:battery")] = "upload";
        fixture.Stub.ConflictOnUpload.Add((42, "libretro:battery"));

        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, outcome.Uploaded);
        Assert.Equal(0, outcome.Failed);
        Assert.Equal(1, outcome.Conflicts);

        // Persisted, so it outlives the flush that found it and `saves resolve` has something
        // to settle.
        var conflict = Assert.Single(fixture.Store.SaveConflicts.ListOpen());

        Assert.Equal(42, conflict.RomId);
        Assert.Equal("libretro:battery", conflict.Slot);

        // The local file is untouched and still unsent, and the copy aside was taken before
        // anything else. The safety property from stage 1 is unchanged: a 409 is never retried
        // with overwrite, because that would discard whatever moved on the other side.
        Assert.True(Assert.Single(fixture.Store.Saves.List()).IsUnsent);
        Assert.NotNull(conflict.LocalCopyPath);
        Assert.Empty(fixture.Stub.Saves);
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

        var offlineOutcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, offlineOutcome.Failed);
        Assert.Equal(3, fixture.Store.Outbox.PendingCount());
        Assert.All(fixture.Store.Saves.List(), save => Assert.True(save.IsUnsent));

        // Plugged back in: one flush, and all three saves and all three sessions land.
        fixture.Stub.IsReachable = true;

        var playtime = await fixture.FlushPlaytimeAsync(TestContext.Current.CancellationToken);
        var saves = await fixture.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, playtime.Sent);
        Assert.Equal(0, playtime.Failed);
        Assert.Equal(3, saves.Uploaded);
        Assert.Equal(0, fixture.Store.Outbox.PendingCount());
        Assert.All(fixture.Store.Saves.List(), save => Assert.False(save.IsUnsent));
    }

    [Fact]
    public async Task A_class_B_save_that_half_lands_is_reported_as_one_save_not_two_results()
    {
        // What outbox.batch_key was for, delivered without it. saturn writes .bcr and .bkr for
        // every game and they take one slot each, so a flush that lands one and fails the other
        // otherwise reports two independent results where each looks fine on its own.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(9, "saturn", "Battle Garegga (Japan)", ".chd", ".bcr", "the big one");

        // The sibling, in the same folder under the same stem, which is the class B shape.
        File.WriteAllText(
            fixture.Resolve("saves/saturn/Battle Garegga (Japan).bkr"),
            "the small one");

        Assert.Equal(2, fixture.Scan().Found);

        fixture.Stub.NegotiateActions[(9, "libretro:battery:bcr")] = "upload";
        fixture.Stub.NegotiateActions[(9, "libretro:battery:bkr")] = "upload";

        // One of the two refused, which is what a dropped link mid-flush looks like.
        fixture.Stub.RefuseUploadForSlot = "libretro:battery:bkr";

        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Uploaded);
        Assert.Equal(1, outcome.Failed);

        // The batch line, naming the save rather than the file, is the point.
        var batch = Assert.Single(outcome.Problems, problem => problem.Contains("are one save", StringComparison.Ordinal));

        Assert.Contains("1 of 2 files", batch, StringComparison.Ordinal);
        Assert.Contains("libretro:battery", batch, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_class_B_save_that_lands_whole_is_not_reported_as_a_batch()
    {
        // Only partial batches are named. One that landed whole is the ordinary case and saying
        // so on every flush would drown the report it belongs to.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(9, "saturn", "Battle Garegga (Japan)", ".chd", ".bcr", "the big one");

        File.WriteAllText(
            fixture.Resolve("saves/saturn/Battle Garegga (Japan).bkr"),
            "the small one");

        fixture.Scan();
        fixture.Stub.NegotiateActions[(9, "libretro:battery:bcr")] = "upload";
        fixture.Stub.NegotiateActions[(9, "libretro:battery:bkr")] = "upload";

        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, outcome.Uploaded);
        Assert.DoesNotContain(outcome.Problems, problem => problem.Contains("are one save", StringComparison.Ordinal));
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

        var offline = await fixture.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, offline.Uploaded);
        Assert.All(fixture.Store.Saves.List(), save => Assert.True(save.IsUnsent));

        fixture.Stub.IsReachable = true;
        fixture.Stub.NegotiateActions[(8, "mame:nvram")] = "upload";

        var online = await fixture.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, online.Uploaded);
        Assert.All(fixture.Store.Saves.List(), save => Assert.False(save.IsUnsent));

        // One archive on the server holding both members, not two saves. The entries are read
        // back rather than the row counted: the stub took the filename from the quoted form
        // only, so a bundled upload arrived as zero bytes under no name and every count here
        // still agreed with it.
        var uploaded = Assert.Single(fixture.Stub.Saves.Values);

        Assert.Equal("mame:nvram", uploaded.Slot);
        Assert.Equal("25pacman", uploaded.FileNameNoTags);

        using var archive = new ZipArchive(new MemoryStream(uploaded.Bytes), ZipArchiveMode.Read);

        Assert.Equal(
            ["25pacman/eeprom", "25pacman/flash"],
            archive.Entries.Select(entry => entry.FullName).Order(StringComparer.Ordinal));
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
        Assert.Equal(1, (await fixture.SyncAsync(TestContext.Current.CancellationToken)).Uploaded);

        var afterFirst = fixture.Stub.Saves.Count;

        // Cleared so the replay negotiates for real rather than being told to upload again.
        fixture.Stub.NegotiateActions.Clear();

        fixture.Scan();
        var replay = await fixture.SyncAsync(TestContext.Current.CancellationToken);

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
        await fixture.SyncAsync(TestContext.Current.CancellationToken);
        fixture.Stub.NegotiateActions.Clear();

        // Unchanged: the fold matches what was uploaded, so the wire carries the server's own
        // digest and negotiate answers no_op.
        fixture.Scan();
        Assert.Equal(0, (await fixture.SyncAsync(TestContext.Current.CancellationToken)).Uploaded);

        // Changed: a new member appears, so the fold moves and the unit is sent whole again.
        File.WriteAllText(
            fixture.Resolve("saves/mame/nvram/25pacman/flash"),
            "a second member the game just wrote");

        fixture.Scan();
        Assert.True(fixture.Store.Saves.List().Single(save => save.ShapeClass == SaveShapeClass.C).HasChangedSinceUpload);

        fixture.Stub.NegotiateActions[(8, "mame:nvram")] = "upload";
        Assert.Equal(1, (await fixture.SyncAsync(TestContext.Current.CancellationToken)).Uploaded);
    }

    [Fact]
    public async Task Restoring_a_directory_save_replaces_the_unit_rather_than_merging_into_it()
    {
        // A member the server's archive does not name was deleted on the device that wrote it,
        // usually an in-game slot. Leaving it behind made the restore a merge: the fold over the
        // tree then disagreed with the fold over the archive, the next scan read the unit as
        // changed, and the merged copy went back over the server's. Somebody who asked to discard
        // the local side got the opposite, silently.
        using var fixture = SyncFixture.Create();
        fixture.AddUnit(8, "25pacman", ("eeprom", "one"), ("flash", "two"));

        fixture.Scan();
        fixture.Stub.NegotiateActions[(8, "mame:nvram")] = "upload";
        Assert.Equal(1, (await fixture.SyncAsync(TestContext.Current.CancellationToken)).Uploaded);

        // Another device replaced the archive. Without that this never downloads at all: a save
        // whose origin_device_id is this device is recognised as its own and skipped.
        fixture.Stub.Saves[100] = fixture.Stub.Saves[100] with { OriginDeviceId = "some-other-device" };

        // A third member appears locally, which is what the server's archive does not hold.
        File.WriteAllText(fixture.Resolve("saves/mame/nvram/25pacman/extra"), "a slot deleted elsewhere");
        fixture.Scan();

        fixture.Stub.NegotiateActions[(8, "mame:nvram")] = "download";
        Assert.Equal(1, (await fixture.SyncAsync(TestContext.Current.CancellationToken)).Downloaded);

        Assert.False(File.Exists(fixture.Resolve("saves/mame/nvram/25pacman/extra")));
        Assert.Equal("one", File.ReadAllText(fixture.Resolve("saves/mame/nvram/25pacman/eeprom")));
        Assert.Equal("two", File.ReadAllText(fixture.Resolve("saves/mame/nvram/25pacman/flash")));

        // In step rather than changed, which is the assertion that catches the re-upload: a
        // rescan folds two files and the stored hash was folded over the archive's two entries.
        fixture.Scan();
        Assert.False(fixture.Store.Saves.List().Single(save => save.ShapeClass == SaveShapeClass.C).HasChangedSinceUpload);

        fixture.Stub.NegotiateActions.Clear();
        Assert.Equal(0, (await fixture.SyncAsync(TestContext.Current.CancellationToken)).Uploaded);
    }

    [Fact]
    public async Task A_bundled_save_this_device_uploaded_is_recognised_rather_than_fetched_again()
    {
        // The download skip, which was dead for every class C save. It compared the local fold
        // against the server's digest, and for a bundled unit those are two different functions
        // by construction, so the guard was always false and the archive was fetched and swapped
        // in even when the server was offering back this device's own upload. Noticed on the K:
        // install: bandwidth and a pointless write of the live tree, not a lost save.
        using var fixture = SyncFixture.Create();
        fixture.AddUnit(8, "25pacman", ("eeprom", "one"), ("flash", "two"));

        fixture.Scan();
        fixture.Stub.NegotiateActions[(8, "mame:nvram")] = "upload";
        Assert.Equal(1, (await fixture.SyncAsync(TestContext.Current.CancellationToken)).Uploaded);

        // The server offers back the row this device just uploaded, untouched on both sides.
        fixture.Stub.NegotiateActions[(8, "mame:nvram")] = "download";
        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, outcome.Downloaded);
        Assert.Equal(1, outcome.NoOps);
        Assert.Equal(0, outcome.BytesTransferred);
    }

    [Fact]
    public async Task A_bundled_unit_edited_since_it_went_up_is_still_fetched()
    {
        // The half that keeps the skip above safe. The slot's recorded digest still matches what
        // the server is offering and this device is still the uploader, so the server-vocabulary
        // question alone would skip. The tree has moved on, so the download has to run.
        using var fixture = SyncFixture.Create();
        fixture.AddUnit(8, "25pacman", ("eeprom", "one"), ("flash", "two"));

        fixture.Scan();
        fixture.Stub.NegotiateActions[(8, "mame:nvram")] = "upload";
        Assert.Equal(1, (await fixture.SyncAsync(TestContext.Current.CancellationToken)).Uploaded);

        File.WriteAllText(fixture.Resolve("saves/mame/nvram/25pacman/eeprom"), "edited here since");
        fixture.Scan();

        fixture.Stub.NegotiateActions[(8, "mame:nvram")] = "download";
        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Downloaded);
        Assert.Equal("one", File.ReadAllText(fixture.Resolve("saves/mame/nvram/25pacman/eeprom")));
    }

    [Fact]
    public async Task A_restored_directory_save_negotiates_as_in_step_rather_than_offering_itself_back()
    {
        // The other half of a restore leaving the device in step, and the half a rescan cannot
        // show: the wire hash for an unchanged bundled unit is the server's digest, which this
        // client cannot recompute, so it comes from save_slot. A restore that does not record
        // the save it just took submits the pre-download digest, the server does not recognise
        // it, and the next flush uploads a unit that is already identical. Found on hardware,
        // where the flush after a class C restore reported one upload that the server then
        // deduplicated into a row it already had.
        using var fixture = SyncFixture.Create();
        fixture.AddUnit(8, "25pacman", ("eeprom", "one"), ("flash", "two"));

        fixture.Scan();
        fixture.Stub.NegotiateActions[(8, "mame:nvram")] = "upload";
        await fixture.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(100, fixture.Store.SaveSlots.Read(8, "mame:nvram")!.SaveId);

        // Another device deletes a member and uploads, which is a new row carrying a digest
        // this device has never seen.
        fixture.Stub.Saves.Remove(100);
        fixture.Stub.Saves[101] = new StubRomMServer.StubSave
        {
            Id = 101,
            RomId = 8,
            Slot = "mame:nvram",
            Emulator = "mame",
            Bytes = Archive(("25pacman/eeprom", "one")),
            FileNameNoTags = "25pacman",
            FileExtension = "zip",
            OriginDeviceId = "some-other-device",
            UpdatedAt = fixture.Stub.ServerDate ?? DateTimeOffset.UnixEpoch,
        };

        fixture.Stub.NegotiateActions[(8, "mame:nvram")] = "download";
        Assert.Equal(1, (await fixture.SyncAsync(TestContext.Current.CancellationToken)).Downloaded);

        Assert.False(File.Exists(fixture.Resolve("saves/mame/nvram/25pacman/flash")));

        // The slot names what came down, rather than what this device sent before it.
        var slot = fixture.Store.SaveSlots.Read(8, "mame:nvram");

        Assert.NotNull(slot);
        Assert.Equal(101, slot.SaveId);
        Assert.Equal(fixture.Stub.Saves[101].ContentHash, slot.ServerContentHash);

        // And the next negotiate says so in the server's own vocabulary, which is the thing a
        // rescan cannot tell you and the server answers `upload` to when it is wrong.
        fixture.Stub.NegotiateActions.Clear();
        fixture.Scan();

        var replay = await fixture.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, replay.Uploaded);
        Assert.Equal(
            fixture.Stub.Saves[101].ContentHash,
            fixture.Stub.NegotiatedHashes[(8, "mame:nvram")]);
    }

    /// <summary>A zip holding the named entries, which is what another device would have sent.</summary>
    private static byte[] Archive(params (string Path, string Contents)[] entries)
    {
        using var buffer = new MemoryStream();

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, contents) in entries)
            {
                using var writer = new StreamWriter(archive.CreateEntry(path).Open());
                writer.Write(contents);
            }
        }

        return buffer.ToArray();
    }

    [Fact]
    public async Task Keeping_the_server_side_of_a_directory_save_copies_it_aside_and_swaps_it_whole()
    {
        // The resolver's own route into the same restore, which the hands-on pass found broken
        // twice: no copy aside at all for a container, because File.Exists is false for one, and
        // a verification against server_content_hash that an archive can never satisfy.
        using var fixture = SyncFixture.Create();
        fixture.AddUnit(8, "25pacman", ("eeprom", "one"), ("flash", "two"));

        fixture.Scan();
        fixture.Stub.NegotiateActions[(8, "mame:nvram")] = "upload";
        Assert.Equal(1, (await fixture.SyncAsync(TestContext.Current.CancellationToken)).Uploaded);

        // Another device drops a member and uploads, so the row in the slot is one this device
        // has never held.
        fixture.Stub.Saves.Remove(100);
        fixture.Stub.Saves[101] = new StubRomMServer.StubSave
        {
            Id = 101,
            RomId = 8,
            Slot = "mame:nvram",
            Emulator = "mame",
            Bytes = Archive(("25pacman/eeprom", "one")),
            FileNameNoTags = "25pacman",
            FileExtension = "zip",
            OriginDeviceId = "some-other-device",
            UpdatedAt = fixture.Stub.ServerDate ?? DateTimeOffset.UnixEpoch,
        };

        // And this device wrote too, which is what makes it a conflict rather than a download.
        File.WriteAllText(fixture.Resolve("saves/mame/nvram/25pacman/extra"), "written here");
        fixture.Scan();

        // A real divergence: negotiate answers upload from the hashes it was handed, and the
        // server refuses because this device's sync record is stale.
        fixture.Stub.ConflictOnUpload.Add((8, "mame:nvram"));
        Assert.Equal(1, (await fixture.SyncAsync(TestContext.Current.CancellationToken)).Conflicts);

        var conflict = Assert.Single(fixture.Store.SaveConflicts.ListOpen());

        // Every member copied aside, which File.Exists on a container reported as nothing.
        Assert.NotNull(conflict.LocalCopyPath);
        Assert.Equal(
            3,
            Directory.GetFiles(
                fixture.Resolve(conflict.LocalCopyPath.Value.Value),
                "*",
                SearchOption.AllDirectories).Length);

        var outcome = await fixture.ResolveAsync(
            8,
            "mame:nvram",
            ConflictResolution.KeepServer,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(outcome.Resolved, outcome.Message);
        Assert.Equal("one", File.ReadAllText(fixture.Resolve("saves/mame/nvram/25pacman/eeprom")));
        Assert.False(File.Exists(fixture.Resolve("saves/mame/nvram/25pacman/extra")));
        Assert.False(File.Exists(fixture.Resolve("saves/mame/nvram/25pacman/flash")));

        // In step on both counts: the fold over what landed, and the slot's server identity.
        fixture.Scan();
        Assert.False(fixture.Store.Saves.List().Single(save => save.ShapeClass == SaveShapeClass.C).HasChangedSinceUpload);

        var slot = fixture.Store.SaveSlots.Read(8, "mame:nvram");

        Assert.Equal(101, slot!.SaveId);
        Assert.Equal(fixture.Stub.Saves[101].ContentHash, slot.ServerContentHash);
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

        var offlineSaves = await fixture.SyncAsync(TestContext.Current.CancellationToken);
        var offlineStates = await fixture.PushStatesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, offlineSaves.Uploaded);
        Assert.Equal(0, offlineStates.Uploaded);
        Assert.All(fixture.Store.Saves.List(), save => Assert.True(save.IsUnsent));
        Assert.All(fixture.Store.States.List(), state => Assert.True(state.IsUnsent));

        // Nothing threw, which is the assertion. An unreachable server is a working state.
        Assert.NotEmpty(offlineStates.Problems);

        fixture.Stub.IsReachable = true;
        fixture.Stub.NegotiateActions[(42, "libretro:battery")] = "upload";

        var saves = await fixture.SyncAsync(TestContext.Current.CancellationToken);
        var states = await fixture.PushStatesAsync(TestContext.Current.CancellationToken);

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

        Assert.Equal(0, (await fixture.SyncAsync(TestContext.Current.CancellationToken)).Uploaded);
        Assert.Equal(0, (await fixture.PushStatesAsync(TestContext.Current.CancellationToken)).Uploaded);
        Assert.Equal(serverStates, fixture.Stub.States.Count);
    }

    [Fact]
    public async Task A_replayed_play_session_batch_is_reconciled_per_index_rather_than_inferred()
    {
        using var fixture = SyncFixture.Create();
        fixture.AddGame(10, "snes", "Game", ".zip", ".srm", "x");
        fixture.PlaySession(10, "Game");
        fixture.Correlate();

        var first = await fixture.FlushPlaytimeAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, first.Sent);
        Assert.Equal(0, first.Duplicates);

        // Queue the identical session again, which is what a replayed flush produces.
        fixture.PlaySession(10, "Game");
        fixture.Correlate();

        var second = await fixture.FlushPlaytimeAsync(TestContext.Current.CancellationToken);

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

        var outcome = await fixture.FlushPlaytimeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, outcome.Sent);
        Assert.Equal(1, outcome.Failed);

        // Failure does not consume the entry: being offline is normal and a replay is safe.
        Assert.Equal(1, fixture.Store.Outbox.PendingCount());

        fixture.Stub.IsReachable = true;
        Assert.Equal(1, (await fixture.FlushPlaytimeAsync(TestContext.Current.CancellationToken)).Sent);
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
        await fixture.SyncAsync(TestContext.Current.CancellationToken);

        // Now the server offers it back, which is what a second device's negotiate looks like
        // from here after this device uploaded.
        fixture.Stub.NegotiateActions[(42, "libretro:battery")] = "download";

        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

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
        await fixture.SyncAsync(TestContext.Current.CancellationToken);

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

        public Task<SaveSyncOutcome> SyncAsync(CancellationToken cancellationToken = default) =>
            new SaveSync(Install, Store, _connection, DeviceId).RunAsync(cancellationToken);

        public Task<ConflictResolutionOutcome> ResolveAsync(
            long romId,
            string slot,
            ConflictResolution resolution,
            CancellationToken cancellationToken = default) =>
            new SaveConflictResolver(Install, Store, _connection, DeviceId)
                .ResolveAsync(romId, slot, resolution, cancellationToken);

        public Task<StateSyncOutcome> PushStatesAsync(CancellationToken cancellationToken = default) =>
            new StateSync(Install, Store, _connection).RunAsync(cancellationToken);

        public Task<OutboxFlushOutcome> FlushPlaytimeAsync(CancellationToken cancellationToken = default) =>
            new OutboxFlush(Store, _connection, DeviceId).FlushPlaySessionsAsync(cancellationToken);

        public void Dispose()
        {
            _connection.Dispose();
            Stub.Dispose();
            Store.Dispose();
            _tree.Dispose();
        }
    }
}
