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
    public async Task A_no_op_for_a_save_this_device_changed_is_uploaded_anyway()
    {
        // #206, and live at the 5.3.0-beta.1 floor. A save restored from a backup, whose mtime
        // is older than this device's last upload, comes back no_op, "No changes since last
        // sync", and used to be believed. The client holds the evidence the server does not:
        // content_hash differs from uploaded_content_hash. Driven on a real install as well as
        // asked of the server directly (s4-older-mtime.py, M1).
        using var fixture = SyncFixture.Create();
        fixture.AddGame(42, "snes", "ActRaiser (USA)", ".zip", ".srm", "progress");
        fixture.Scan();
        fixture.Stub.NegotiateActions[(42, "libretro:battery")] = "upload";

        Assert.Equal(1, (await fixture.SyncAsync(TestContext.Current.CancellationToken)).Uploaded);

        // The restore: different bytes, and negotiate answers no_op for the slot because the
        // stub is told nothing, which is the default.
        File.WriteAllText(fixture.Resolve("saves/snes/ActRaiser (USA).srm"), "from the backup");
        fixture.Scan();
        fixture.Stub.NegotiateActions.Remove((42, "libretro:battery"));

        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Uploaded);
        Assert.Equal(0, outcome.Failed);
        Assert.False(outcome.IsNoOp);

        var stored = fixture.Stub.Saves.Values.Single(save => save.RomId == 42);
        Assert.Equal("from the backup", System.Text.Encoding.UTF8.GetString(stored.Bytes));

        // And the slot settles, so the next pass has nothing to correct.
        Assert.False(fixture.Store.Saves.List().Single().HasChangedSinceUpload);
    }

    [Fact]
    public async Task An_upload_of_bytes_the_server_already_holds_is_not_sent_again()
    {
        // The other half of #206, finding 259, and a guard rather than a live fix. Nestopia
        // rewrites its .srm with identical bytes on every launch, moving the mtime and nothing
        // else, and on 5.3.0-alpha.3 negotiate asked for the upload anyway: three consecutive
        // flushes each said "saves: 1 up" for save 336, each deduplicated into the same row.
        // It does not reproduce at the 5.3.0-beta.1 floor, where probe case M4 answers
        // "no_op (Content is identical)". Kept because it costs one comparison against a value
        // the operation already carries, and the loop it prevents is silent.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(42, "snes", "ActRaiser (USA)", ".zip", ".srm", "progress");
        fixture.Scan();
        fixture.Stub.NegotiateActions[(42, "libretro:battery")] = "upload";

        Assert.Equal(1, (await fixture.SyncAsync(TestContext.Current.CancellationToken)).Uploaded);

        var afterFirst = fixture.Stub.Saves.Count;

        // Still asking, which is what the real server does for as long as the mtime leads.
        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, outcome.Uploaded);
        Assert.Equal(1, outcome.NoOps);
        Assert.Equal(0, outcome.Failed);
        Assert.True(outcome.IsNoOp);
        Assert.Equal(afterFirst, fixture.Stub.Saves.Count);
    }

    [Fact]
    public async Task An_upload_of_bytes_the_server_holds_from_a_peer_is_not_sent_again_either()
    {
        // Whether the server already holds these bytes has nothing to do with who put them
        // there, and the first cut of this fix got that wrong by reusing AlreadyHeld, which also
        // demands the slot name this device as the uploader. Driven against the paired install:
        // the save the symptom was measured on, Legend of Zelda (USA) (Rev 1), holds
        // libretro:battery as save 344 with a NULL origin_device_id, because that row came down
        // rather than up. Every slot whose current row arrived from a peer or from RomM's
        // browser player is in that state, so the fix missed its own headline case.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(42, "snes", "ActRaiser (USA)", ".zip", ".srm", "progress");
        fixture.Scan();

        var local = Assert.Single(fixture.Store.Saves.List());

        // In step, without this device having been the uploader.
        fixture.Store.Saves.MarkUploaded(
            local.Path,
            local.UnitKey,
            local.ContentHash!,
            new DateTimeOffset(2026, 8, 16, 12, 0, 0, TimeSpan.Zero));

        // The server's head row for the slot holds the same bytes, and names somebody else.
        fixture.SeedServerSave(42, "libretro:battery", "ActRaiser (USA)", "srm", "progress");
        fixture.Stub.NegotiateActions[(42, "libretro:battery")] = "upload";

        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, outcome.Uploaded);
        Assert.Equal(1, outcome.NoOps);
        Assert.Equal(0, outcome.Failed);
        Assert.Single(fixture.Stub.Saves);
    }

    [Fact]
    public async Task A_session_close_the_server_refuses_is_reported_rather_than_swallowed()
    {
        // A refusal returns rather than throws, so a token without devices.write used to report
        // a clean sync while leaving the session open on the server.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(42, "snes", "ActRaiser (USA)", ".zip", ".srm", "progress");
        fixture.Scan();
        fixture.Stub.NegotiateActions[(42, "libretro:battery")] = "upload";
        fixture.Stub.CompleteRefusal = (System.Net.HttpStatusCode.Forbidden, "Forbidden");

        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Uploaded);
        Assert.Contains(outcome.Problems, problem => problem.Contains("could not be closed", StringComparison.Ordinal));
        Assert.True(outcome.SessionLeftOpen);
        Assert.False(outcome.IsNoOp);
    }

    [Fact]
    public async Task A_session_the_server_says_is_already_completed_counts_as_closed()
    {
        using var fixture = SyncFixture.Create();
        fixture.AddGame(42, "snes", "ActRaiser (USA)", ".zip", ".srm", "progress");
        fixture.Scan();
        fixture.Stub.NegotiateActions[(42, "libretro:battery")] = "upload";
        fixture.Stub.CompleteRefusal = (System.Net.HttpStatusCode.BadRequest, "Session is already COMPLETED");

        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Uploaded);
        Assert.Empty(outcome.Problems);
        Assert.False(outcome.SessionLeftOpen);
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
    public async Task A_download_for_a_game_being_played_is_deferred_rather_than_written_under_it()
    {
        // The ordering issue #155 opened on, driven end to end. Before this the download landed
        // on top of the file libretro had open, the emulator wrote its own copy over it on exit,
        // and the other device's save was gone with nothing said.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World)", ".zip", ".srm", "what the emulator has open");
        fixture.Scan();

        fixture.SeedServerSave(7, "libretro:battery", "Tetris (World)", "srm", "newer from another device");
        fixture.Stub.NegotiateActions[(7, "libretro:battery")] = "download";

        fixture.Launch(7, "Tetris (World)");

        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Deferred);
        Assert.Equal(0, outcome.Downloaded);

        // Not a failure, so a flush does not exit Partial over a game being played.
        Assert.Equal(0, outcome.Failed);
        Assert.Contains("waiting on a running game", outcome.Summary, StringComparison.Ordinal);

        // The two halves of "nothing was lost": the file the emulator holds is untouched, and
        // the server was not told this device has the save, so the next negotiate offers it again.
        Assert.Equal(
            "what the emulator has open",
            File.ReadAllText(fixture.Resolve("saves/gb/Tetris (World).srm")));
        Assert.Empty(fixture.Stub.Acknowledged);
    }

    [Fact]
    public async Task The_deferred_download_lands_on_the_pass_after_the_game_closes()
    {
        // A deferral is only defensible because it is temporary. This is the quit pass: the same
        // negotiate, the same save, and nothing needed doing by hand in between.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World)", ".zip", ".srm", "local, older");
        fixture.Scan();

        fixture.SeedServerSave(7, "libretro:battery", "Tetris (World)", "srm", "newer from another device");
        fixture.Stub.NegotiateActions[(7, "libretro:battery")] = "download";

        fixture.Launch(7, "Tetris (World)");
        Assert.Equal(1, (await fixture.SyncAsync(TestContext.Current.CancellationToken)).Deferred);

        // The game ends, and the flush correlates before it downloads, exactly as
        // SaveFlushService orders the two.
        fixture.EndLaunch();
        fixture.Correlate();

        var second = await fixture.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, second.Downloaded);
        Assert.Equal(0, second.Deferred);
        Assert.Equal(
            "newer from another device",
            File.ReadAllText(fixture.Resolve("saves/gb/Tetris (World).srm")));
        Assert.Equal([100], fixture.Stub.Acknowledged);
    }

    [Fact]
    public async Task One_game_being_played_does_not_hold_back_another_games_save()
    {
        // The cost of the guard, bounded. Deferring every write while any game runs would stall a
        // whole library sync on one person playing, and a class A save is one file beside its own
        // rom that nothing else has open.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World)", ".zip", ".srm", "being played");
        fixture.AddGame(42, "snes", "ActRaiser (USA)", ".zip", ".srm", "local, older");
        fixture.Scan();

        fixture.SeedServerSave(42, "libretro:battery", "ActRaiser (USA)", "srm", "newer from another device");
        fixture.Stub.NegotiateActions[(42, "libretro:battery")] = "download";

        fixture.Launch(7, "Tetris (World)");

        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Downloaded);
        Assert.Equal(0, outcome.Deferred);
        Assert.Equal(
            "newer from another device",
            File.ReadAllText(fixture.Resolve("saves/snes/ActRaiser (USA).srm")));
    }

    [Fact]
    public async Task A_restore_asked_for_by_hand_while_a_game_runs_says_which_game_and_writes_nothing()
    {
        // A person typed this one, so it reports per line rather than as a count, and the line
        // has to name the remedy. Nothing is written, which is the same promise a held lock makes.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World)", ".zip", ".srm", "played once");
        fixture.Scan();
        fixture.Stub.NegotiateActions[(7, "libretro:battery")] = "upload";
        await fixture.SyncAsync(TestContext.Current.CancellationToken);
        fixture.Stub.NegotiateActions.Remove((7, "libretro:battery"));

        File.Delete(fixture.Resolve("saves/gb/Tetris (World).srm"));
        fixture.Scan();

        var findings = await fixture.FindRestorableAsync(TestContext.Current.CancellationToken);
        var picks = findings.Value!.Restorable;
        Assert.Single(picks);

        fixture.Launch(7, "Tetris (World)");

        var outcome = await fixture.RestoreAsync(picks, TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Deferred);
        Assert.Equal(0, outcome.Restored);
        Assert.Equal(0, outcome.Failed);
        Assert.False(outcome.Refused);
        Assert.Contains("this game is running", Assert.Single(outcome.Problems), StringComparison.Ordinal);
        Assert.False(File.Exists(fixture.Resolve("saves/gb/Tetris (World).srm")));
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

        // A second game with a save that stays put, so the request is not the empty one the
        // fresh-device test below drives. A library with more than one game is the ordinary
        // case anyway.
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
    public async Task A_download_the_server_gives_no_slot_for_lands_with_a_derived_slot_rather_than_throwing()
    {
        // A save uploaded by a client that sets no slot comes back with "slot": null, which the
        // client keyed as the empty string. local_save.slot is CHECKed non-empty, so the write
        // threw SQLite error 19 after the bytes were already on disk: one save in the tree with
        // no row behind it, and anything later in the same batch never attempted. Found by
        // restoring two real saves on a live install, not by reading the schema.
        using var fixture = SyncFixture.Create();

        fixture.AddGame(7, "gb", "Tetris (World)", ".zip", ".srm", "played once");
        fixture.Scan();
        File.Delete(fixture.Resolve("saves/gb/Tetris (World).srm"));
        fixture.Scan();

        fixture.SeedServerSave(7, "libretro:battery", "Tetris (World)", "srm", "from a slotless client");
        fixture.Stub.SlotlessDownloads.Add(7);

        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, outcome.Failed);
        Assert.Equal(1, outcome.Downloaded);

        Assert.Equal(
            "from a slotless client",
            File.ReadAllText(fixture.Resolve("saves/gb/Tetris (World).srm")));

        // The row exists and its slot is a real one. Which emulator names it is the scanner's
        // to decide on the next pass; what this asserts is that a row could be written at all.
        var recorded = Assert.Single(fixture.Store.Saves.List());
        Assert.False(string.IsNullOrWhiteSpace(recorded.Slot));
        Assert.EndsWith(":battery", recorded.Slot, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_device_with_no_local_saves_negotiates_anyway_and_pulls_what_the_server_holds()
    {
        // RunAsync returned before negotiating when the device held no attributed saves, so the
        // one device with the strongest reason to pull was the one case that never asked. A
        // freshly paired install with ROMs and no saves printed "saves: nothing to sync" and
        // made no negotiate call; every save already on the server stayed there, and the install
        // only started pulling once it happened to write a save of its own.
        //
        // Measured, and the reason the empty request is worth sending: negotiate returns a
        // download for every save the device has no current sync record for, including slots the
        // client did not submit. An empty saves array came back with 13 downloads across two
        // ROMs, one never named by the client.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World)", ".zip", ".srm", "not kept");

        // The ROM is synced and nothing under saves/ is, which is a fresh install exactly.
        File.Delete(fixture.Resolve("saves/gb/Tetris (World).srm"));
        fixture.Scan();
        Assert.Empty(fixture.Store.Saves.List());

        fixture.SeedServerSave(7, "libretro:battery", "Tetris (World)", "srm", "from the other device");
        fixture.Stub.UnsolicitedDownloads.Add((7, "libretro:battery"));

        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Downloaded);
        Assert.Equal(0, outcome.Failed);
        Assert.Empty(outcome.Problems);

        // Where libretro looks for it: the ROM's own folder and the ROM's own stem. The stem is
        // never file_name_no_tags, which strips general tags and would have written
        // "Tetris.srm" for a ROM whose name carries a region.
        Assert.Equal(
            "from the other device",
            File.ReadAllText(fixture.Resolve("saves/gb/Tetris (World).srm")));

        Assert.Equal([100], fixture.Stub.Acknowledged);
    }


    [Fact]
    public async Task A_converted_memory_card_comes_down_into_its_container_and_not_loose()
    {
        // The class D download case, which nothing exercised until this test and which was
        // wrong. A converted PCSX2 card lives three levels down, in the container the shape
        // declares; the derived target for a slot this device has never held builds
        // saves/<folder>/<stem><ext>, which is right for class A and puts a memory card exactly
        // where PCSX2 will never look for it.
        //
        // Not a loud failure either: the bytes land, the ack is sent, the flush reports success,
        // and the game starts from an empty card anyway.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(191723, "ps2", "Armored Core 3 (USA)", ".chd", ".unused", "not kept");

        // A fresh device: the ROM is here and no card is.
        File.Delete(fixture.Resolve("saves/ps2/Armored Core 3 (USA).unused"));
        fixture.Scan();
        Assert.Empty(fixture.Store.Saves.List());

        fixture.SeedServerSave(191723, "pcsx2:battery", "Armored Core 3 (USA)", "ps2", "the other device's card");
        fixture.Stub.UnsolicitedDownloads.Add((191723, "pcsx2:battery"));

        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Downloaded);
        Assert.Equal(0, outcome.Failed);
        Assert.Empty(outcome.Problems);

        // Where PCSX2 actually reads it, measured on hardware.
        Assert.Equal(
            "the other device's card",
            File.ReadAllText(fixture.Resolve("saves/ps2/pcsx2/memcards/Armored Core 3 (USA).ps2")));

        // And emphatically not loose under the system folder, which is where class A lives and
        // where the old target would have put it.
        Assert.False(File.Exists(fixture.Resolve("saves/ps2/Armored Core 3 (USA).ps2")));
    }

    [Fact]
    public async Task A_converted_card_is_not_refused_the_way_a_bundled_unit_is()
    {
        // The two class D and class C answers differ for opposite reasons and the code has to
        // keep them apart. A class C unit cannot be placed on a device holding none, because the
        // container and the key both come from a local unit that is not there. A converted class
        // D container can: it is one file named after the ROM's stem and the shape declares
        // where it goes, so a device that has never run the game can still be handed it.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(191723, "ps2", "Armored Core 3 (USA)", ".chd", ".unused", "not kept");
        File.Delete(fixture.Resolve("saves/ps2/Armored Core 3 (USA).unused"));
        fixture.Scan();

        fixture.SeedServerSave(191723, "pcsx2:battery", "Armored Core 3 (USA)", "ps2", "a card");
        fixture.Stub.UnsolicitedDownloads.Add((191723, "pcsx2:battery"));

        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, outcome.Failed);
        Assert.DoesNotContain(outcome.Problems, problem => problem.Contains("directory save", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_save_for_a_game_this_device_has_not_synced_is_skipped_rather_than_failed()
    {
        // Negotiate is unscoped: it answers with a download for every save the device has no
        // sync record for, so a device holding a subset is offered the whole library. Those
        // have no local ROM, so no folder and no stem, and they used to come back as
        // "nowhere to write it" on stderr, one line each, counted as failures. A user with 500
        // saves in RomM and one 10-game set synced got ~490 lines and ExitCode.Partial on every
        // flush. The message was false as well: the server had named a file, and the device had
        // not "no save in that slot", it had no game.
        using var fixture = SyncFixture.Create();

        fixture.SeedServerSave(4242, "libretro:battery", "Some Other Game (USA)", "srm", "not ours");
        fixture.Stub.UnsolicitedDownloads.Add((4242, "libretro:battery"));

        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Skipped);
        Assert.Equal(0, outcome.Failed);
        Assert.Equal(0, outcome.Downloaded);
        Assert.Empty(outcome.Problems);
        Assert.Contains("skipped", outcome.Summary, StringComparison.Ordinal);

        // Nothing was acked, because nothing landed: the server must keep offering it for the
        // day the game does get synced.
        Assert.Empty(fixture.Stub.Acknowledged);
    }

    [Fact]
    public async Task A_bundled_save_for_a_game_this_device_lacks_is_skipped_rather_than_refused()
    {
        // A class C slot for an absent game takes the skip, not the unplaceable-unit refusal.
        // Those two are not in competition today, because IsUnplaceableUnit needs the ROM's
        // folder to find the shape and so answers false for a ROM that is not here; driven on a
        // real install, the psp operation fell through it to "nowhere to write it" rather than
        // to "run the game once". Pinned so the shape lookup gaining another route cannot start
        // telling someone to run a game this device does not have.
        using var fixture = SyncFixture.Create();

        fixture.SeedServerSave(4243, "ppsspp:savedata", "ULES09999", "zip", "an archive");
        fixture.Stub.UnsolicitedDownloads.Add((4243, "ppsspp:savedata"));

        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Skipped);
        Assert.Equal(0, outcome.Failed);
        Assert.DoesNotContain(
            outcome.Problems,
            problem => problem.Contains("Run the game once", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_bundled_slot_this_device_has_never_held_is_reported_rather_than_written_as_a_file()
    {
        // The half of the entry gate that is not simply deletable. A class C restore needs a
        // container and a unit key, and both come from the local unit the device holds; with
        // none, saves/psp/SAVEDATA may not exist at all until PPSSPP has run once. Taking the
        // single-file route instead would put a .zip where an emulator expects a save.
        //
        // Answered from the shapes table rather than from the filename, because a .zip extension
        // is not evidence of anything: the slot is what a declared unit path names.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(391, "psp", "3rd Birthday, The (Europe)", ".cso", ".srm", "not kept");

        File.Delete(fixture.Resolve("saves/psp/3rd Birthday, The (Europe).srm"));
        fixture.Scan();

        fixture.SeedServerSave(391, "ppsspp:savedata", "ULES01513", "zip", "an archive");
        fixture.Stub.UnsolicitedDownloads.Add((391, "ppsspp:savedata"));

        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, outcome.Downloaded);
        Assert.Equal(1, outcome.Failed);
        Assert.Contains(
            outcome.Problems,
            problem => problem.Contains("directory save", StringComparison.Ordinal));

        // Nothing was written, least of all an archive under a save's name.
        Assert.False(File.Exists(fixture.Resolve("saves/psp/3rd Birthday, The (Europe).zip")));
        Assert.False(File.Exists(fixture.Resolve("saves/psp/3rd Birthday, The (Europe).srm")));
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

        // The server's time is shown to whoever picks a side, and RomM serialises it with no
        // zone while storing UTC, which System.Text.Json reads as local. Without
        // UtcTimestampConverter this is out by the machine's own offset, and right only where
        // that offset is zero, which is what CI is.
        Assert.Equal(fixture.Stub.ServerDate, conflict.ServerUpdatedAt);
    }

    [Fact]
    public async Task A_conflict_for_a_slot_this_device_did_not_submit_costs_one_operation()
    {
        // save_conflict.local_path is NOT NULL and CHECKs for a non-blank value, so recording a
        // conflict with no local save behind it raised SQLITE_CONSTRAINT_CHECK out of the flush,
        // taking the states pass down with it. The constraint is what keeps this safe, not
        // negotiate's silence: measurement 151 withdrew 132 and showed negotiate does volunteer
        // slots the client never submitted, so the case is reachable and stays a guard and a
        // reported problem.
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
    public async Task A_peer_offering_back_identical_contents_transfers_and_does_not_rewrite_the_tree()
    {
        // What #43 left over. The download skip is defined as "recognises this device's own
        // upload", so a peer holding identical bytes is not something it can answer, and no
        // local comparison can rule the download out either: the wire hash for an unchanged
        // class C unit is the digest the server returned to THIS device, and a peer's upload
        // carries one this device has never seen. So negotiate says download and the bytes come.
        //
        // The write is the avoidable half. The fold of what arrived is computed before anything
        // live is touched, so a unit that already holds it is left exactly as it was: no copy
        // under replaced/, no mtime churn, and no window where the container is half swapped.
        using var fixture = SyncFixture.Create();
        fixture.AddUnit(8, "25pacman", ("eeprom", "one"), ("flash", "two"));

        fixture.Scan();
        fixture.Stub.NegotiateActions[(8, "mame:nvram")] = "upload";
        await fixture.SyncAsync(TestContext.Current.CancellationToken);

        // A peer uploads the same contents. A different row, a digest this device has not seen,
        // and origin naming somebody else, so IsOwnUpload is false and the skip cannot fire.
        fixture.Stub.Saves.Remove(100);
        fixture.Stub.Saves[101] = new StubRomMServer.StubSave
        {
            Id = 101,
            RomId = 8,
            Slot = "mame:nvram",
            Emulator = "mame",
            Bytes = Archive(("25pacman/eeprom", "one"), ("25pacman/flash", "two")),
            FileNameNoTags = "25pacman",
            FileExtension = "zip",
            OriginDeviceId = "some-other-device",
            UpdatedAt = fixture.Stub.ServerDate ?? DateTimeOffset.UnixEpoch,
        };

        var eeprom = fixture.Resolve("saves/mame/nvram/25pacman/eeprom");
        var flash = fixture.Resolve("saves/mame/nvram/25pacman/flash");
        var before = (File.GetLastWriteTimeUtc(eeprom), File.GetLastWriteTimeUtc(flash));

        fixture.Stub.NegotiateActions[(8, "mame:nvram")] = "download";
        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

        // The transfer is unavoidable without a protocol change, so it still counts as one.
        Assert.Equal(1, outcome.Downloaded);
        Assert.Empty(outcome.Problems);

        // The tree is untouched: same bytes, same mtimes, and nothing taken aside.
        Assert.Equal("one", File.ReadAllText(eeprom));
        Assert.Equal("two", File.ReadAllText(flash));
        Assert.Equal(before, (File.GetLastWriteTimeUtc(eeprom), File.GetLastWriteTimeUtc(flash)));

        var aside = fixture.Resolve(SaveSync.AsideDirectory.Value);
        Assert.True(
            !Directory.Exists(aside) || Directory.GetFileSystemEntries(aside).Length == 0,
            "a copy aside was taken for a save nobody replaced");

        // The server was still told, and the slot's digest still became current, or the next
        // negotiate answers upload for a unit that is already in step.
        Assert.Contains(101, fixture.Stub.Acknowledged);
        Assert.Equal(101, fixture.Store.SaveSlots.Read(8, "mame:nvram")!.SaveId);
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
    public async Task A_flushed_session_tells_the_server_the_game_is_no_longer_being_played()
    {
        // Ingesting a session sets now_playing and nothing else clears it, so a client that
        // never says otherwise leaves a library claiming the user is playing everything they
        // have ever launched. Measured on the live instance during M7 stage 7b-3's hands-on
        // pass: ten roms all true, one played two days earlier, against a rom RomMBat had never
        // reported reading false.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(10, "snes", "Game", ".zip", ".srm", "x");
        fixture.PlaySession(10, "Game");
        fixture.Correlate();

        var outcome = await fixture.FlushPlaytimeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Sent);
        Assert.Equal(10, Assert.Single(fixture.Stub.NowPlayingCleared));
    }

    [Fact]
    public async Task Two_sessions_of_one_game_clear_it_once()
    {
        // The flag is per rom and the batch is per session, so a person who played the same
        // game twice before a flush must not produce two writes.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(10, "snes", "Game", ".zip", ".srm", "x");

        fixture.PlaySession(10, "Game");
        fixture.Correlate();
        fixture.PlaySession(10, "Game");
        fixture.Correlate();

        await fixture.FlushPlaytimeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(10, Assert.Single(fixture.Stub.NowPlayingCleared));
    }

    [Fact]
    public async Task A_session_the_server_refuses_leaves_its_now_playing_flag_alone()
    {
        // The tidy-up follows the ingest. A refused entry never reached one, so it never set
        // the flag, and writing for it spends one request per rom on a batch that changed
        // nothing.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(10, "snes", "Game", ".zip", ".srm", "x");
        fixture.PlaySession(10, "Game");
        fixture.Correlate();

        fixture.Stub.RefusePlaySessionsFor.Add(10);

        var outcome = await fixture.FlushPlaytimeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, outcome.Sent);
        Assert.Equal(1, outcome.Failed);
        Assert.Empty(fixture.Stub.NowPlayingCleared);

        // Still queued, because a refusal is not a reason to drop a session.
        Assert.Equal(1, fixture.Store.Outbox.PendingCount());
    }

    [Fact]
    public async Task A_half_refused_batch_clears_only_the_half_the_server_took()
    {
        // Per entry rather than per batch, which is the same reason the result array is read by
        // index: a batch with one refusal in it is not a batch that did nothing.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(10, "snes", "Game", ".zip", ".srm", "x");
        fixture.AddGame(11, "snes", "Other", ".zip", ".srm", "y");

        fixture.PlaySession(10, "Game");
        fixture.Correlate();
        fixture.PlaySession(11, "Other");
        fixture.Correlate();

        fixture.Stub.RefusePlaySessionsFor.Add(11);

        await fixture.FlushPlaytimeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(10, Assert.Single(fixture.Stub.NowPlayingCleared));
    }

    [Fact]
    public async Task A_refused_tidy_up_never_costs_a_session()
    {
        // The sessions are accepted and recorded before this runs. A tidy-up that could undo
        // that would be worse than the untidiness it fixes, so it is best effort and silent.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(10, "snes", "Game", ".zip", ".srm", "x");
        fixture.PlaySession(10, "Game");
        fixture.Correlate();

        fixture.Stub.PropsStatus = System.Net.HttpStatusCode.InternalServerError;

        var outcome = await fixture.FlushPlaytimeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Sent);
        Assert.Equal(0, outcome.Failed);
        Assert.Equal(0, fixture.Store.Outbox.PendingCount());
        Assert.Empty(fixture.Stub.NowPlayingCleared);
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
    public async Task A_sent_play_session_can_be_read_back_from_the_server()
    {
        // #208. An accepted post leaves the outbox and was never looked at again, so "nothing
        // queued" read the same whether every session landed or none was ever written. This is
        // the read that makes the server half observable, and step 8 of the certification
        // checklist answerable from the agent.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(10, "snes", "Game", ".zip", ".srm", "x");
        fixture.PlaySession(10, "Game");
        fixture.Correlate();

        Assert.Equal(1, (await fixture.FlushPlaytimeAsync(TestContext.Current.CancellationToken)).Sent);

        var answer = await fixture.ReadPlaySessionsAsync(DeviceId, TestContext.Current.CancellationToken);

        Assert.True(answer.IsSuccess);
        var session = Assert.Single(answer.Value!);
        Assert.Equal(10, session.RomId);
        Assert.Equal(30 * 60 * 1000, session.DurationMs);

        // The server writes UTC and says nothing about the zone, and System.Text.Json reads a
        // zone-less value as local, so without UtcTimestampConverter these are out by the
        // machine's own offset and right only on a UTC machine. Driven against the live
        // instance, a session read back landed four hours after the same run's Date header,
        // which put a finished session in the future.
        Assert.Equal(new DateTimeOffset(2026, 8, 16, 10, 0, 0, TimeSpan.Zero), session.StartTime);
        Assert.Equal(new DateTimeOffset(2026, 8, 16, 10, 30, 0, TimeSpan.Zero), session.EndTime);
    }

    [Fact]
    public void A_timestamp_the_converter_cannot_read_is_a_JsonException()
    {
        // RomMConnection.ReadAsync turns JsonException into RomMApiException, "a body this
        // client could not read", and catches nothing else. DateTimeOffset.Parse throws
        // FormatException, which walks past that and out of Program.DispatchAsync, so one
        // unreadable timestamp in a 200 left the process where every caller is written for a
        // handled failure. The repo warns above the floor instead of refusing, so a newer RomM
        // serialising a field differently is a supported state.
        var unreadable = Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<RomM.Client.Saves.PlaySessionRow>(
                """
                {"id": 1, "start_time": "not-a-date", "end_time": "2026-08-16T10:30:00", "duration_ms": 0}
                """));

        Assert.Contains("not-a-date", unreadable.Message, StringComparison.Ordinal);

        // A null where the schema says non-nullable reached Parse as an empty string, which is
        // the same escape by a different route.
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<RomM.Client.Saves.PlaySessionRow>(
                """
                {"id": 1, "start_time": null, "end_time": "2026-08-16T10:30:00", "duration_ms": 0}
                """));
    }

    [Fact]
    public async Task A_play_session_read_filtered_by_the_wrong_device_answers_with_nothing()
    {
        // The trap this endpoint sets, and it cost a probe while driving the nes record: the row
        // carries the RomM-side device id, status prints that and the local one on adjacent
        // lines, and asking with the wrong one answers 200 with zero rows, which is
        // indistinguishable from a session that was never written. The same shape as a token on
        // another account, which is identity rather than permission and no scope widens.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(10, "snes", "Game", ".zip", ".srm", "x");
        fixture.PlaySession(10, "Game");
        fixture.Correlate();

        await fixture.FlushPlaytimeAsync(TestContext.Current.CancellationToken);

        var answer = await fixture.ReadPlaySessionsAsync("some-other-device", TestContext.Current.CancellationToken);

        Assert.True(answer.IsSuccess);
        Assert.Empty(answer.Value!);
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
    public async Task A_file_save_download_records_the_save_it_took_as_the_slots_server_identity()
    {
        // #157. Measured on a live install: after a negotiate-driven download of save 211 the slot
        // still read save 209 with the pre-download hash, and because the file was then in step the
        // slot was never negotiated again to correct it.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(42, "snes", "ActRaiser (USA)", ".zip", ".srm", "progress");
        fixture.Scan();

        fixture.Stub.NegotiateActions[(42, "libretro:battery")] = "upload";
        await fixture.SyncAsync(TestContext.Current.CancellationToken);
        Assert.Equal(100, fixture.Store.SaveSlots.Read(42, "libretro:battery")!.SaveId);

        // Another device's newer save replaces it at the head of the slot.
        fixture.Stub.Saves.Clear();
        fixture.SeedServerSave(42, "libretro:battery", "ActRaiser (USA)", "srm", "further along", id: 101);
        fixture.Stub.NegotiateActions[(42, "libretro:battery")] = "download";

        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Downloaded);

        var slot = fixture.Store.SaveSlots.Read(42, "libretro:battery");

        Assert.NotNull(slot);
        Assert.Equal(101, slot.SaveId);
        Assert.Equal(fixture.Stub.Saves[101].ContentHash, slot.ServerContentHash);

        // Not this device's upload any more, so its own-upload shortcut must not answer for it.
        Assert.False(slot.IsFrom("device-under-test"));
    }

    [Fact]
    public async Task A_row_rewritten_in_place_still_comes_down_when_this_device_holds_that_row()
    {
        // PUT /api/saves/{id} keeps the id and changes the bytes, which at 5.3.0-alpha.2 RomM's
        // browser player did to the save it loaded. Measured there (s1-browser-save-writer.py, case A):
        // negotiate answers download for the same save id with the new hash. That is the row this
        // device last exchanged rather than an older one, so it is an ordinary download.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(42, "snes", "ActRaiser (USA)", ".zip", ".srm", "progress");
        fixture.Scan();

        fixture.Stub.NegotiateActions[(42, "libretro:battery")] = "upload";
        await fixture.SyncAsync(TestContext.Current.CancellationToken);

        fixture.Stub.Saves[100] = fixture.Stub.Saves[100] with
        {
            Bytes = System.Text.Encoding.UTF8.GetBytes("played on in the browser"),
        };
        fixture.Stub.NegotiateActions[(42, "libretro:battery")] = "download";

        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Downloaded);
        Assert.Equal(0, outcome.Conflicts);
        Assert.Equal("played on in the browser", File.ReadAllText(fixture.Resolve("saves/snes/ActRaiser (USA).srm")));
        Assert.Equal(fixture.Stub.Saves[100].ContentHash, fixture.Store.SaveSlots.Read(42, "libretro:battery")!.ServerContentHash);
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
    [Fact]
    public async Task A_save_the_server_holds_for_a_game_here_with_no_local_file_is_found_and_restored()
    {
        // The case the feature exists for, and the one negotiate can never answer: the device
        // uploaded the save, acknowledged it, and then the file went. Negotiate reads no_op from
        // its own sync record, so GET /api/saves is the only thing that still knows.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World)", ".zip", ".srm", "played once");
        fixture.Scan();

        File.Delete(fixture.Resolve("saves/gb/Tetris (World).srm"));
        fixture.Scan();
        Assert.Empty(fixture.Store.Saves.List());

        fixture.SeedServerSave(7, "libretro:battery", "Tetris (World)", "srm", "from the server");

        var found = await fixture.FindRestorableAsync(TestContext.Current.CancellationToken);
        Assert.True(found.IsSuccess);

        var findings = Assert.IsType<SaveRestoreFindings>(found.Value);
        Assert.Empty(findings.Unrestorable);

        var pick = Assert.Single(findings.Restorable);
        Assert.Equal(7, pick.RomId);
        Assert.Equal("saves/gb/Tetris (World).srm", pick.Destination.Value);

        var outcome = await fixture.RestoreAsync(findings.Restorable, TestContext.Current.CancellationToken);

        Assert.False(outcome.Refused);
        Assert.Equal(1, outcome.Restored);
        Assert.Equal(0, outcome.Failed);
        Assert.Empty(outcome.Problems);

        // Under the name the emulator matches on, not the tagged one the server keeps.
        Assert.Equal(
            "from the server",
            File.ReadAllText(fixture.Resolve("saves/gb/Tetris (World).srm")));

        // Acknowledged, or the server goes on believing this device lacks it.
        Assert.Equal(100, Assert.Single(fixture.Stub.Acknowledged));
        Assert.Empty(fixture.Stub.OptimisticDownloads);
    }

    [Fact]
    public async Task A_save_deleted_since_the_last_scan_is_offered_by_the_first_restore()
    {
        // #147, measured on nes: the store still held the row for a file deleted by hand, so the
        // first restore offered nothing and only a run after some other scan found it. No scan
        // runs between the delete and the find here, which is the whole of the case.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World)", ".zip", ".srm", "played once");
        fixture.Scan();

        File.Delete(fixture.Resolve("saves/gb/Tetris (World).srm"));
        Assert.Single(fixture.Store.Saves.List());

        fixture.SeedServerSave(7, "libretro:battery", "Tetris (World)", "srm", "from the server");

        var found = await fixture.FindRestorableAsync(TestContext.Current.CancellationToken);
        var findings = Assert.IsType<SaveRestoreFindings>(found.Value);

        var pick = Assert.Single(findings.Restorable);
        Assert.Equal("saves/gb/Tetris (World).srm", pick.Destination.Value);
        Assert.Empty(fixture.Store.Saves.List());
    }

    [Fact]
    public async Task Server_saves_that_land_on_one_file_are_offered_once_as_the_newest()
    {
        // #156, measured on nes: four server rows for one ROM, three of a slot's history and one
        // from the web UI with no slot, all resolving to one .srm and listed as four restores.
        // Applying them wrote one file four times and kept whichever came last.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World)", ".zip", ".srm", "played once");
        fixture.Scan();
        File.Delete(fixture.Resolve("saves/gb/Tetris (World).srm"));

        var at = new DateTimeOffset(2026, 9, 13, 11, 32, 0, TimeSpan.Zero);
        SeedAt(fixture, 101, "libretro:battery", "oldest", at);
        SeedAt(fixture, 102, "libretro:battery", "older", at.AddMinutes(6));
        SeedAt(fixture, 103, string.Empty, "from the web UI", at.AddMinutes(67));
        SeedAt(fixture, 104, "libretro:battery", "newest", at.AddMinutes(74));

        var found = await fixture.FindRestorableAsync(TestContext.Current.CancellationToken);
        var pick = Assert.Single(Assert.IsType<SaveRestoreFindings>(found.Value).Restorable);

        Assert.Equal(104, pick.SaveId);
        Assert.Equal([103, 102, 101], pick.Folded.Select(save => save.SaveId));
        Assert.Equal(string.Empty, pick.Folded[0].Slot);

        var outcome = await fixture.RestoreAsync([pick], TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Restored);
        Assert.Equal("newest", File.ReadAllText(fixture.Resolve("saves/gb/Tetris (World).srm")));
    }

    [Fact]
    public async Task Narrowing_to_a_slot_happens_before_saves_on_one_file_are_folded()
    {
        // A newer row with no slot shares the file. Asking for the slot by name has to get the
        // slot's newest row, not an empty answer because the fold kept the other one.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World)", ".zip", ".srm", "played once");
        fixture.Scan();
        File.Delete(fixture.Resolve("saves/gb/Tetris (World).srm"));

        var at = new DateTimeOffset(2026, 9, 13, 11, 32, 0, TimeSpan.Zero);
        SeedAt(fixture, 101, "libretro:battery", "slotted", at);
        SeedAt(fixture, 102, string.Empty, "from the web UI", at.AddHours(1));

        var all = await fixture.FindRestorableAsync(TestContext.Current.CancellationToken);
        Assert.Equal(102, Assert.Single(all.Value!.Restorable).SaveId);

        var narrowed = await fixture.FindRestorableAsync(
            7,
            "libretro:battery",
            TestContext.Current.CancellationToken);

        var pick = Assert.Single(narrowed.Value!.Restorable);
        Assert.Equal(101, pick.SaveId);
        Assert.Empty(pick.Folded);
    }

    [Fact]
    public async Task Server_saves_with_no_slot_for_a_game_here_are_reported_and_nothing_else_is()
    {
        // #138, ruled: report them and derive no slot. Negotiate pairs on the slot, so these
        // are never fetched, and on nes two such saves sat unmentioned through every flush.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World)", ".zip", ".srm", "played once");
        fixture.AddGame(8, "gb", "Dr. Mario (World)", ".zip", ".srm", "played once");
        fixture.Scan();

        fixture.SeedServerSave(7, "libretro:battery", "Tetris (World)", "srm", "slotted", id: 101);
        fixture.SeedServerSave(7, string.Empty, "Tetris (World)", "srm", "from the web UI", id: 102);
        fixture.SeedServerSave(7, "   ", "Tetris (World)", "srm", "whitespace", id: 103);
        fixture.SeedServerSave(8, "libretro:battery", "Dr. Mario (World)", "srm", "null on the wire", id: 104);
        fixture.Stub.SlotlessDownloads.Add(8);
        fixture.SeedServerSave(4242, string.Empty, "Not Here", "srm", "rom not on this device", id: 105);

        var found = await fixture.FindSlotlessAsync(TestContext.Current.CancellationToken);

        Assert.True(found.IsSuccess);
        Assert.Equal([102, 103, 104], found.Value!.Select(save => save.SaveId));

        // Reported, not acted on: nothing was written and no slot was made up.
        Assert.Empty(fixture.Stub.Acknowledged);
        Assert.Equal(
            "played once",
            File.ReadAllText(fixture.Resolve("saves/gb/Tetris (World).srm")));
    }

    private static void SeedAt(SyncFixture fixture, int id, string slot, string contents, DateTimeOffset at)
    {
        fixture.SeedServerSave(7, slot, "Tetris (World)", "srm", contents, id: id);
        fixture.Stub.Saves[id] = fixture.Stub.Saves[id] with { UpdatedAt = at };
    }

    [Fact]
    public async Task A_save_already_in_the_tree_is_not_offered_for_restore()
    {
        // Restoring over a file nobody asked about is the one outcome this feature must never
        // produce, so the File.Exists guard is asserted rather than trusted to the store's row.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World)", ".zip", ".srm", "the local one");
        fixture.Scan();

        fixture.SeedServerSave(7, "libretro:battery", "Tetris (World)", "srm", "the server one");

        var found = await fixture.FindRestorableAsync(TestContext.Current.CancellationToken);
        var findings = Assert.IsType<SaveRestoreFindings>(found.Value);

        Assert.Empty(findings.Restorable);
        Assert.Empty(findings.Unrestorable);

        Assert.Equal(
            "the local one",
            File.ReadAllText(fixture.Resolve("saves/gb/Tetris (World).srm")));
    }

    [Fact]
    public async Task A_save_for_a_game_this_device_does_not_hold_is_skipped_without_a_word()
    {
        // The ordinary case on a device carrying a subset of the library, and the reason the
        // unfiltered list is affordable: it is answered locally rather than reported.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World)", ".zip", ".srm", "the local one");
        fixture.Scan();

        fixture.SeedServerSave(4242, "libretro:battery", "Some Other Game", "srm", "never synced here", id: 101);

        var found = await fixture.FindRestorableAsync(TestContext.Current.CancellationToken);
        var findings = Assert.IsType<SaveRestoreFindings>(found.Value);

        Assert.Empty(findings.Restorable);
        Assert.Empty(findings.Unrestorable);
    }

    [Fact]
    public async Task A_second_restore_finds_nothing_left_to_do()
    {
        // The no-op re-run. A restore that offered the same save again would either overwrite
        // what it just wrote or make a person think the first one failed.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World)", ".zip", ".srm", "played once");
        fixture.Scan();
        File.Delete(fixture.Resolve("saves/gb/Tetris (World).srm"));
        fixture.Scan();

        fixture.SeedServerSave(7, "libretro:battery", "Tetris (World)", "srm", "from the server");

        var first = await fixture.FindRestorableAsync(TestContext.Current.CancellationToken);
        var restored = await fixture.RestoreAsync(
            Assert.IsType<SaveRestoreFindings>(first.Value).Restorable,
            TestContext.Current.CancellationToken);
        Assert.Equal(1, restored.Restored);

        var second = await fixture.FindRestorableAsync(TestContext.Current.CancellationToken);
        var findings = Assert.IsType<SaveRestoreFindings>(second.Value);

        Assert.Empty(findings.Restorable);
        Assert.Empty(findings.Unrestorable);
    }

    [Fact]
    public async Task A_directory_save_for_a_game_this_device_has_never_played_is_named_rather_than_offered()
    {
        // The class C guard the flush applies, at the same decision point. Without it the archive
        // takes the class A path with a null local save and lands as saves/psp/<stem>.zip, which
        // is what the flush guard exists to prevent, and a server row can carry a null
        // content_hash so the verification that would have caught it never runs.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(9, "psp", "Wipeout Pure (USA)", ".iso", ".srm", "not a psp save");
        File.Delete(fixture.Resolve("saves/psp/Wipeout Pure (USA).srm"));
        fixture.Scan();

        fixture.SeedServerSave(9, "ppsspp:savedata", "Wipeout Pure (USA)", "zip", "a bundled unit");

        var found = await fixture.FindRestorableAsync(TestContext.Current.CancellationToken);
        var findings = Assert.IsType<SaveRestoreFindings>(found.Value);

        Assert.Empty(findings.Restorable);

        var named = Assert.Single(findings.Unrestorable);
        Assert.Equal(9, named.RomId);
        Assert.Equal("ppsspp:savedata", named.Slot);
        Assert.Contains("directory save", named.Reason, StringComparison.Ordinal);

        // And nothing reached the tree, which is the half that mattered.
        Assert.False(File.Exists(fixture.Resolve("saves/psp/Wipeout Pure (USA).zip")));
    }

    [Fact]
    public async Task A_restored_save_whose_slot_and_emulator_are_blank_still_gets_a_row()
    {
        // Finding 245 closed the null case; the CHECKs behind it refuse the empty string and
        // whitespace the same way, and SaveScanner.SlotFor throws on a blank emulator before a
        // CHECK is even reached. Both reach this path now that a restore covers any installed
        // ROM rather than only this device's own uploads, so the values are another client's.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World)", ".zip", ".srm", "played once");
        fixture.Scan();
        File.Delete(fixture.Resolve("saves/gb/Tetris (World).srm"));
        fixture.Scan();

        fixture.SeedServerSave(
            7,
            "   ",
            "Tetris (World)",
            "srm",
            "from a client that wrote neither",
            emulator: string.Empty);

        var found = await fixture.FindRestorableAsync(TestContext.Current.CancellationToken);
        var picks = Assert.IsType<SaveRestoreFindings>(found.Value).Restorable;

        var outcome = await fixture.RestoreAsync(picks, TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Restored);
        Assert.Equal(0, outcome.Failed);
        Assert.Empty(outcome.Problems);

        Assert.Equal(
            "from a client that wrote neither",
            File.ReadAllText(fixture.Resolve("saves/gb/Tetris (World).srm")));

        // Derived rather than carried over. Which emulator names it is the scanner's to settle
        // on the next pass; what this asserts is that a row could be written at all.
        var recorded = Assert.Single(fixture.Store.Saves.List());
        Assert.False(string.IsNullOrWhiteSpace(recorded.Slot));
        Assert.False(string.IsNullOrWhiteSpace(recorded.Emulator));
    }

    [Fact]
    public async Task A_restore_refuses_rather_than_writing_while_something_else_holds_the_tree_lock()
    {
        // The ES quit hook spawns a detached background flush fire-and-forget, so a person
        // running a restore at a terminal beside one is ordinary rather than an edge. Refused
        // rather than skipped: a flush treats a held lock as success because somebody else is
        // doing its work, and nobody is doing this one's.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World)", ".zip", ".srm", "played once");
        fixture.Scan();
        File.Delete(fixture.Resolve("saves/gb/Tetris (World).srm"));
        fixture.Scan();

        fixture.SeedServerSave(7, "libretro:battery", "Tetris (World)", "srm", "from the server");

        var found = await fixture.FindRestorableAsync(TestContext.Current.CancellationToken);
        var picks = Assert.IsType<SaveRestoreFindings>(found.Value).Restorable;
        Assert.Single(picks);

        using var flushing = TreeLock.TryAcquire(fixture.Install);
        Assert.NotNull(flushing);

        var outcome = await fixture.RestoreAsync(picks, TestContext.Current.CancellationToken);

        Assert.True(outcome.Refused);
        Assert.Equal(0, outcome.Restored);
        Assert.Equal(0, outcome.Failed);
        Assert.Single(outcome.Problems);

        // Nothing was written and nothing was told to the server, which is what "refused" has to
        // mean for the retry to be safe.
        Assert.False(File.Exists(fixture.Resolve("saves/gb/Tetris (World).srm")));
        Assert.Empty(fixture.Stub.Acknowledged);
    }

    [Fact]
    public async Task A_download_for_another_slot_that_lands_on_this_devices_save_is_a_conflict()
    {
        // Measured on the nes install against RomM 5.3.0-alpha.3 (#205): a browser session
        // started without loading a save files under `autosave`, a slot this device has never
        // held, whose destination is the same saves/<system>/<rom>.srm that libretro:battery
        // keeps. The flush reported "1 down", the played save was gone, and the next scan
        // re-keyed the file and uploaded the browser's save over this device's own slot.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World)", ".zip", ".srm", "played here");
        fixture.Scan();
        fixture.Stub.NegotiateActions[(7, "libretro:battery")] = "upload";
        Assert.Equal(1, (await fixture.SyncAsync(TestContext.Current.CancellationToken)).Uploaded);
        fixture.Stub.NegotiateActions.Remove((7, "libretro:battery"));

        fixture.SeedServerSave(7, "autosave", "Tetris (World)", "srm", "the browser's fresh game", id: 101);
        fixture.Stub.UnsolicitedDownloads.Add((7, "autosave"));

        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, outcome.Downloaded);
        Assert.Equal(0, outcome.Failed);
        Assert.Equal(1, outcome.Conflicts);

        // The file the emulator reads is untouched, which is the whole point.
        Assert.Equal("played here", File.ReadAllText(fixture.Resolve("saves/gb/Tetris (World).srm")));

        var conflict = Assert.Single(fixture.Store.SaveConflicts.ListOpen());

        Assert.Equal("autosave", conflict.Slot);
        Assert.Equal("saves/gb/Tetris (World).srm", conflict.LocalPath.Value);
        Assert.Contains("libretro:battery", conflict.Reason, StringComparison.Ordinal);

        // And it stays a conflict rather than being re-offered into a write on the next pass.
        fixture.Scan();
        var again = await fixture.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, again.Downloaded);
        Assert.Equal(0, again.Uploaded);
        Assert.Equal("played here", File.ReadAllText(fixture.Resolve("saves/gb/Tetris (World).srm")));
    }

    [Fact]
    public async Task Keeping_the_local_side_of_that_conflict_sends_the_file_the_other_slot_holds()
    {
        // The conflict is keyed on the slot the server offered, which this device holds no row
        // for, so keep-local has to find its local side by the file the conflict named.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World)", ".zip", ".srm", "played here");
        fixture.Scan();
        fixture.Stub.NegotiateActions[(7, "libretro:battery")] = "upload";
        await fixture.SyncAsync(TestContext.Current.CancellationToken);
        fixture.Stub.NegotiateActions.Remove((7, "libretro:battery"));

        fixture.SeedServerSave(7, "autosave", "Tetris (World)", "srm", "the browser's fresh game", id: 101);
        fixture.Stub.UnsolicitedDownloads.Add((7, "autosave"));
        Assert.Equal(1, (await fixture.SyncAsync(TestContext.Current.CancellationToken)).Conflicts);

        var outcome = await fixture.ResolveAsync(
            7,
            "autosave",
            ConflictResolution.KeepLocal,
            TestContext.Current.CancellationToken);

        Assert.True(outcome.Resolved, outcome.Message);
        Assert.Equal("played here", File.ReadAllText(fixture.Resolve("saves/gb/Tetris (World).srm")));
        Assert.Empty(fixture.Store.SaveConflicts.ListOpen());

        // The device's own bytes now stand in the slot the browser wrote.
        Assert.Contains(
            fixture.Stub.Saves.Values,
            save => save.Slot == "autosave"
                && System.Text.Encoding.UTF8.GetString(save.Bytes) == "played here");

        // The server offers that save back, and it is the file this device already keeps, so the
        // next pass writes nothing and reopens nothing.
        fixture.Scan();
        var next = await fixture.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, next.Conflicts);
        Assert.Equal(0, next.Downloaded);
        Assert.Empty(fixture.Store.SaveConflicts.ListOpen());
    }

    [Fact]
    public async Task A_download_for_another_slot_carrying_this_devices_own_bytes_is_not_a_conflict()
    {
        // Two devices holding the same save under different slots. The offer names the file
        // libretro:battery keeps, but there is nothing to settle when the bytes are the same.
        using var fixture = SyncFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World)", ".zip", ".srm", "played here");
        fixture.Scan();
        fixture.Stub.NegotiateActions[(7, "libretro:battery")] = "upload";
        await fixture.SyncAsync(TestContext.Current.CancellationToken);
        fixture.Stub.NegotiateActions.Remove((7, "libretro:battery"));

        fixture.SeedServerSave(7, "autosave", "Tetris (World)", "srm", "played here", id: 101);
        fixture.Stub.UnsolicitedDownloads.Add((7, "autosave"));

        var outcome = await fixture.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, outcome.Conflicts);
        Assert.Equal(0, outcome.Downloaded);
        Assert.Equal(0, outcome.Failed);
        Assert.Empty(fixture.Store.SaveConflicts.ListOpen());
        Assert.Equal("played here", File.ReadAllText(fixture.Resolve("saves/gb/Tetris (World).srm")));
    }

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
            bool lieAboutHash = false,
            int id = 100,
            string emulator = "libretro")
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(contents);

            Stub.Saves[id] = new StubRomMServer.StubSave
            {
                Id = id,
                RomId = romId,
                Slot = slot,
                Emulator = emulator,
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

        /// <summary>Writes a game-start with no game-end, which is a game still running.</summary>
        public void Launch(int romId, string stem)
        {
            var file = Store.Files.List().First(entry => entry.RomId == romId);

            Store.Journal.Append(
                JournalEvent.GameStart,
                new DateTimeOffset(2026, 8, 16, 10, 0, 0, TimeSpan.Zero),
                file.Path,
                stem,
                stem);
        }

        /// <summary>Closes whatever <see cref="Launch"/> opened, as leaving the game does.</summary>
        public void EndLaunch() =>
            Store.Journal.Append(
                JournalEvent.GameEnd,
                new DateTimeOffset(2026, 8, 16, 10, 30, 0, TimeSpan.Zero));

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

        public Task<RomMResponse<SaveRestoreFindings>> FindRestorableAsync(
            CancellationToken cancellationToken = default) =>
            FindRestorableAsync(null, null, cancellationToken);

        public Task<RomMResponse<IReadOnlyList<SlotlessSave>>> FindSlotlessAsync(
            CancellationToken cancellationToken = default) =>
            new SaveSync(Install, Store, _connection, DeviceId).FindSlotlessAsync(cancellationToken);

        public Task<RomMResponse<SaveRestoreFindings>> FindRestorableAsync(
            int? onlyRom,
            string? onlySlot,
            CancellationToken cancellationToken = default) =>
            new SaveSync(Install, Store, _connection, DeviceId)
                .FindRestorableAsync(onlyRom, onlySlot, cancellationToken);

        public Task<SaveRestoreOutcome> RestoreAsync(
            IReadOnlyList<RestorableSave> picks,
            CancellationToken cancellationToken = default) =>
            new SaveSync(Install, Store, _connection, DeviceId).RestoreAsync(picks, cancellationToken);

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

        public Task<RomMResponse<IReadOnlyList<RomM.Client.Saves.PlaySessionRow>>> ReadPlaySessionsAsync(
            string? deviceId,
            CancellationToken cancellationToken = default) =>
            _connection.ListPlaySessionsAsync(deviceId: deviceId, cancellationToken: cancellationToken);

        public void Dispose()
        {
            _connection.Dispose();
            Stub.Dispose();
            Store.Dispose();
            _tree.Dispose();
        }
    }
}
