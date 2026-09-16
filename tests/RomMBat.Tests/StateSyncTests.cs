using System.Net;
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
    public async Task A_restored_state_is_named_after_the_rom_on_disk_and_not_after_the_server_row()
    {
        // RomM strips anything parenthesised into its tags, so file_name_no_tags for
        // "ActRaiser (USA) [libretro.snes9x].state1" comes back as "ActRaiser". Writing that
        // puts the state where the emulator will never look and it reads as simply absent.
        // es_savestates.cfg declares {{romfilename}}.state{{slot}}, so the ROM names it.
        using var fixture = StateFixture.Create();
        fixture.AddRom(42, "snes", "ActRaiser (USA).zip");
        fixture.AddState("snes/libretro.snes9x", "ActRaiser (USA).state1", "progress");
        fixture.Scan();

        await fixture.PushAsync(TestContext.Current.CancellationToken);

        // The file goes, which is the case a restore exists for.
        var onDisk = fixture.Install.Resolve(
            RelativePath.Create("saves/snes/libretro.snes9x/ActRaiser (USA).state1"));
        File.Delete(onDisk);

        var found = await fixture.FindRestorableAsync(TestContext.Current.CancellationToken);
        var candidate = Assert.Single(found.Value!.Restorable);

        Assert.Equal(
            "saves/snes/libretro.snes9x/ActRaiser (USA).state1",
            candidate.Destination.Value);

        var outcome = await fixture.RestoreAsync([candidate], TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Restored);
        Assert.Equal(0, outcome.Failed);
        Assert.Equal("progress", File.ReadAllText(onDisk));
    }

    [Fact]
    public async Task A_restored_state_is_not_sent_straight_back_by_the_next_flush()
    {
        // The headline case: a state made on another device has no local row at all, so without
        // one written at restore time the next scan reads the file as never sent and RunAsync
        // uploads what was just fetched.
        using var fixture = StateFixture.Create();
        fixture.AddRom(42, "snes", "ActRaiser (USA).zip");
        fixture.AddState("snes/libretro.snes9x", "ActRaiser (USA).state1", "progress");
        fixture.Scan();
        await fixture.PushAsync(TestContext.Current.CancellationToken);

        // Deleted and forgotten, which is what a state from another device looks like here: the
        // scan drops the row for a file that is gone.
        var onDisk = fixture.Install.Resolve(
            RelativePath.Create("saves/snes/libretro.snes9x/ActRaiser (USA).state1"));
        File.Delete(onDisk);
        fixture.Scan();
        Assert.Empty(fixture.Store.States.List());

        var found = await fixture.FindRestorableAsync(TestContext.Current.CancellationToken);
        var restore = await fixture.RestoreAsync(
            found.Value!.Restorable,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, restore.Restored);

        // The no-op re-sync assertion the checklist wants for a sync change: a second pass over
        // the same tree sends nothing.
        fixture.Scan();
        var after = await fixture.PushAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, after.Uploaded);
        Assert.Equal(1, after.AlreadyInStep);
        Assert.Single(fixture.Stub.States);
    }

    [Fact]
    public async Task A_restored_state_brings_its_screenshot_when_the_server_links_one()
    {
        // #158, measured on nes: the .srm and the .state1 came back byte-identically and the
        // .state1.png did not, because the restore dropped the screenshot before it could fetch it.
        using var fixture = StateFixture.Create();
        fixture.AddRom(42, "snes", "ActRaiser (USA).zip");
        fixture.AddState("snes/libretro.snes9x", "ActRaiser (USA).state1", "progress");
        fixture.AddState("snes/libretro.snes9x", "ActRaiser (USA).state1.png", "png bytes");
        fixture.Scan();
        await fixture.PushAsync(TestContext.Current.CancellationToken);

        var state = fixture.Install.Resolve(RelativePath.Create("saves/snes/libretro.snes9x/ActRaiser (USA).state1"));
        var image = state + ".png";
        File.Delete(state);
        File.Delete(image);

        var found = await fixture.FindRestorableAsync(TestContext.Current.CancellationToken);
        var candidate = Assert.Single(found.Value!.Restorable);

        Assert.Equal(
            "saves/snes/libretro.snes9x/ActRaiser (USA).state1.png",
            candidate.ScreenshotDestination?.Value);

        var outcome = await fixture.RestoreAsync([candidate], TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Restored);
        Assert.Equal(1, outcome.Screenshots);
        Assert.Empty(outcome.Problems);
        Assert.Equal("png bytes", File.ReadAllText(image));

        // Replaying the push over the restored pair sends nothing.
        fixture.Scan();
        var after = await fixture.PushAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, after.Uploaded);
    }

    [Fact]
    public async Task A_state_the_server_links_no_screenshot_to_restores_without_one_and_says_nothing_failed()
    {
        // The upstream half of #158, finding 138: the image was uploaded and not linked, so the
        // state row reads screenshot: null. The state still comes back and counts.
        using var fixture = StateFixture.Create();
        fixture.Stub.DropScreenshots = true;
        fixture.AddRom(42, "snes", "ActRaiser (USA).zip");
        fixture.AddState("snes/libretro.snes9x", "ActRaiser (USA).state1", "progress");
        fixture.AddState("snes/libretro.snes9x", "ActRaiser (USA).state1.png", "png bytes");
        fixture.Scan();
        await fixture.PushAsync(TestContext.Current.CancellationToken);

        var state = fixture.Install.Resolve(RelativePath.Create("saves/snes/libretro.snes9x/ActRaiser (USA).state1"));
        File.Delete(state);
        File.Delete(state + ".png");

        var found = await fixture.FindRestorableAsync(TestContext.Current.CancellationToken);
        var candidate = Assert.Single(found.Value!.Restorable);
        Assert.Null(candidate.ScreenshotDestination);

        var outcome = await fixture.RestoreAsync([candidate], TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Restored);
        Assert.Equal(0, outcome.Screenshots);
        Assert.Empty(fixture.Stub.ScreenshotRequests);
        Assert.False(File.Exists(state + ".png"));
    }

    [Fact]
    public async Task A_screenshot_that_cannot_be_fetched_costs_a_line_and_not_the_state()
    {
        using var fixture = StateFixture.Create();
        fixture.AddRom(42, "snes", "ActRaiser (USA).zip");
        fixture.AddState("snes/libretro.snes9x", "ActRaiser (USA).state1", "progress");
        fixture.AddState("snes/libretro.snes9x", "ActRaiser (USA).state1.png", "png bytes");
        fixture.Scan();
        await fixture.PushAsync(TestContext.Current.CancellationToken);

        var state = fixture.Install.Resolve(RelativePath.Create("saves/snes/libretro.snes9x/ActRaiser (USA).state1"));
        File.Delete(state);
        File.Delete(state + ".png");

        fixture.Stub.FailScreenshotDownload = HttpStatusCode.InternalServerError;

        var found = await fixture.FindRestorableAsync(TestContext.Current.CancellationToken);
        var outcome = await fixture.RestoreAsync(found.Value!.Restorable, TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Restored);
        Assert.Equal(0, outcome.Failed);
        Assert.Equal("progress", File.ReadAllText(state));
        Assert.Contains(outcome.Problems, problem => problem.Contains("without its screenshot", StringComparison.Ordinal));
        Assert.False(File.Exists(state + ".png"));
        Assert.Empty(Directory.EnumerateFiles(fixture.Install.Resolve(RetroBatInstall.PartialDirectory)));
    }

    [Fact]
    public async Task A_state_for_a_rom_this_device_does_not_hold_is_dropped_without_a_word()
    {
        // The one skip that is ordinary rather than a finding. A device holding a subset of the
        // library sees a state for every game it does not have, and reporting each would bury
        // the ones it could actually place.
        using var fixture = StateFixture.Create();
        fixture.AddRom(42, "snes", "ActRaiser (USA).zip");
        fixture.AddState("snes/libretro.snes9x", "ActRaiser (USA).state1", "progress");
        fixture.Scan();
        await fixture.PushAsync(TestContext.Current.CancellationToken);

        // Re-point the uploaded row at a ROM this device has never heard of.
        var held = fixture.Stub.States.Values.Single();
        fixture.Stub.States[held.Id] = held with { RomId = 999 };

        var found = await fixture.FindRestorableAsync(TestContext.Current.CancellationToken);

        Assert.Empty(found.Value!.Restorable);
        Assert.Empty(found.Value!.Unrestorable);
    }

    [Fact]
    public async Task A_state_whose_emulator_is_not_declared_here_is_reported_rather_than_dropped()
    {
        // A state the server holds for an installed ROM that this device cannot place is not
        // nothing, and showing nothing is what sends somebody looking for a bug in the server.
        using var fixture = StateFixture.Create();
        fixture.AddRom(42, "snes", "ActRaiser (USA).zip");
        fixture.AddState("snes/libretro.snes9x", "ActRaiser (USA).state1", "progress");
        fixture.Scan();
        await fixture.PushAsync(TestContext.Current.CancellationToken);

        var held = fixture.Stub.States.Values.Single();
        fixture.Stub.States[held.Id] = held with { Emulator = "notanemulator" };

        var found = await fixture.FindRestorableAsync(TestContext.Current.CancellationToken);

        Assert.Empty(found.Value!.Restorable);
        var reported = Assert.Single(found.Value!.Unrestorable);

        Assert.Equal(42, reported.RomId);
        Assert.Contains("es_savestates.cfg", reported.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_download_failure_is_counted_and_reported_rather_than_thrown()
    {
        using var fixture = StateFixture.Create();
        fixture.AddRom(42, "snes", "ActRaiser (USA).zip");
        fixture.AddState("snes/libretro.snes9x", "ActRaiser (USA).state1", "progress");
        fixture.Scan();
        await fixture.PushAsync(TestContext.Current.CancellationToken);

        var onDisk = fixture.Install.Resolve(
            RelativePath.Create("saves/snes/libretro.snes9x/ActRaiser (USA).state1"));
        File.Delete(onDisk);

        var found = await fixture.FindRestorableAsync(TestContext.Current.CancellationToken);
        fixture.Stub.FailStateDownload = HttpStatusCode.InternalServerError;

        var outcome = await fixture.RestoreAsync(
            found.Value!.Restorable,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, outcome.Restored);
        Assert.Equal(1, outcome.Failed);
        Assert.Single(outcome.Problems);

        // Nothing half-written where an emulator would find it, and no partial left behind.
        Assert.False(File.Exists(onDisk));
        Assert.Empty(Directory.GetFiles(
            fixture.Install.Resolve(RetroBatInstall.PartialDirectory),
            "state-*.part"));
    }

    [Fact]
    public async Task A_restore_refuses_while_another_holder_has_the_tree_lock()
    {
        // The quit hook spawns a detached background flush that holds the lock across
        // StateScanner.Scan() over these same directories, so this race is ordinary rather than
        // theoretical. Refusing is the rule, because nobody else is doing a restore's work.
        using var fixture = StateFixture.Create();
        fixture.AddRom(42, "snes", "ActRaiser (USA).zip");
        fixture.AddState("snes/libretro.snes9x", "ActRaiser (USA).state1", "progress");
        fixture.Scan();
        await fixture.PushAsync(TestContext.Current.CancellationToken);

        var onDisk = fixture.Install.Resolve(
            RelativePath.Create("saves/snes/libretro.snes9x/ActRaiser (USA).state1"));
        File.Delete(onDisk);

        var found = await fixture.FindRestorableAsync(TestContext.Current.CancellationToken);

        using (TreeLock.TryAcquire(fixture.Install))
        {
            var refused = await fixture.RestoreAsync(
                found.Value!.Restorable,
                TestContext.Current.CancellationToken);

            Assert.True(refused.Refused);
            Assert.Equal(0, refused.Restored);
            Assert.Equal(0, refused.Failed);
            Assert.False(File.Exists(onDisk));
        }

        // And it is a refusal rather than a failure, so the same call works once the lock is free.
        var after = await fixture.RestoreAsync(
            found.Value!.Restorable,
            TestContext.Current.CancellationToken);

        Assert.False(after.Refused);
        Assert.Equal(1, after.Restored);
    }

    [Fact]
    public async Task A_state_that_appeared_since_the_preview_is_not_overwritten()
    {
        // A state has no hash and no conflict record, so whatever landed in that window would be
        // destroyed with nothing written down anywhere. BiosSync takes the same choice.
        using var fixture = StateFixture.Create();
        fixture.AddRom(42, "snes", "ActRaiser (USA).zip");
        fixture.AddState("snes/libretro.snes9x", "ActRaiser (USA).state1", "progress");
        fixture.Scan();
        await fixture.PushAsync(TestContext.Current.CancellationToken);

        var onDisk = fixture.Install.Resolve(
            RelativePath.Create("saves/snes/libretro.snes9x/ActRaiser (USA).state1"));
        File.Delete(onDisk);

        var found = await fixture.FindRestorableAsync(TestContext.Current.CancellationToken);

        // The emulator writes one back between the preview and the apply.
        File.WriteAllText(onDisk, "newer progress");

        var outcome = await fixture.RestoreAsync(
            found.Value!.Restorable,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, outcome.Restored);
        Assert.Equal(1, outcome.Failed);
        Assert.Equal("newer progress", File.ReadAllText(onDisk));
    }

    [Fact]
    public async Task A_server_value_local_state_would_refuse_is_reported_rather_than_thrown_mid_restore()
    {
        // The emulator field is free text from the server, and the row a restore now writes goes
        // into CHECKed columns. A slash is the reachable case and a colon is not: RelativePath
        // refuses a colon anywhere, so SaveStateTemplate.Create already answers null for one,
        // while "libretro.snes9x/evil" expands to a perfectly valid directory and only
        // local_state.core objects. Unguarded it threw SQLite error 19 after File.Move had put
        // the state in the tree: a state that landed, counted Failed, with no row behind it and
        // the next flush ready to send it back. Same shape as f337d2e's finding 4c on the save
        // side, and fixed at the same point, in the find.
        using var fixture = StateFixture.Create();
        fixture.AddRom(42, "snes", "ActRaiser (USA).zip");
        fixture.AddState("snes/libretro.snes9x", "ActRaiser (USA).state1", "progress");
        fixture.Scan();
        await fixture.PushAsync(TestContext.Current.CancellationToken);

        var onDisk = fixture.Install.Resolve(
            RelativePath.Create("saves/snes/libretro.snes9x/ActRaiser (USA).state1"));
        File.Delete(onDisk);

        var held = fixture.Stub.States.Values.Single();
        fixture.Stub.States[held.Id] = held with { Emulator = "libretro.snes9x/evil" };

        var found = await fixture.FindRestorableAsync(TestContext.Current.CancellationToken);
        var outcome = await fixture.RestoreAsync(
            found.Value!.Restorable,
            TestContext.Current.CancellationToken);

        // Asserted before the reporting, because this is the defect: unguarded, the row reaches
        // the restore, the file is written, the insert throws, and the state is counted Failed
        // with no row behind it.
        Assert.Equal(0, outcome.Restored);
        Assert.Equal(0, outcome.Failed);
        Assert.False(File.Exists(onDisk));

        // Named rather than dropped, so it is not silence either.
        Assert.Empty(found.Value!.Restorable);
        var reported = Assert.Single(found.Value!.Unrestorable);
        Assert.Contains("separator", reported.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_server_name_the_row_would_refuse_costs_the_note_and_not_the_restore()
    {
        // uploaded_file_name refuses a separator too, but it is only a note about what went up:
        // what decides whether a state still needs sending is the hash beside it. So this one is
        // recorded null rather than refused, and the state still comes back.
        using var fixture = StateFixture.Create();
        fixture.AddRom(42, "snes", "ActRaiser (USA).zip");
        fixture.AddState("snes/libretro.snes9x", "ActRaiser (USA).state1", "progress");
        fixture.Scan();
        await fixture.PushAsync(TestContext.Current.CancellationToken);

        var onDisk = fixture.Install.Resolve(
            RelativePath.Create("saves/snes/libretro.snes9x/ActRaiser (USA).state1"));
        File.Delete(onDisk);
        fixture.Scan();

        var held = fixture.Stub.States.Values.Single();
        fixture.Stub.States[held.Id] = held with { FileName = "some/nested/name.state1" };

        var found = await fixture.FindRestorableAsync(TestContext.Current.CancellationToken);
        var outcome = await fixture.RestoreAsync(
            found.Value!.Restorable,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Restored);
        Assert.Equal(0, outcome.Failed);

        // And the row is still the thing that stops the re-upload, note or no note.
        var row = Assert.Single(fixture.Store.States.List());
        Assert.Null(row.UploadedFileName);
        Assert.False(row.NeedsUpload);
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

            // The real es_savestates.cfg, in the tree rather than only loaded beside it. A
            // restore reads the schema from the install to work out where a state belongs, so a
            // tree without it has nowhere to put one and reports nothing to restore.
            var config = install.Resolve(SaveStateSchema.ConfigPath);
            Directory.CreateDirectory(Path.GetDirectoryName(config)!);
            File.Copy(Fixtures.EsSaveStatesTemplate, config, overwrite: true);

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

        public Task<RomMResponse<StateRestoreFindings>> FindRestorableAsync(
            CancellationToken cancellationToken = default) =>
            new StateSync(Install, Store, _connection).FindRestorableAsync(cancellationToken);

        public Task<StateRestoreOutcome> RestoreAsync(
            IReadOnlyList<RestorableState> picks,
            CancellationToken cancellationToken = default) =>
            new StateSync(Install, Store, _connection).RestoreAsync(picks, cancellationToken);

        public void Dispose()
        {
            _connection.Dispose();
            Store.Dispose();
            _tree.Dispose();
        }
    }
}
