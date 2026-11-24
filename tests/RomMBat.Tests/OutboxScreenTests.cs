using RomM.Client;
using RomMBat.Core;
using RomMBat.Core.Identity;
using RomMBat.Core.Metadata;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Paths;
using RomMBat.Core.Sync;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using RomMBat.UI.Input;
using RomMBat.UI.Screens;
using RomMBat.UI.Shell;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The outbox screen, where what has not reached the server can be dropped from the couch.
/// </summary>
/// <remarks>
/// <b>Each drop deletes exactly the rows <c>rommbat-agent outbox drop</c> would.</b> A refused
/// entry alone or every refused entry, never a pending one; every pending entry, never a refused
/// one. These assert the store after the press, not the sentence on the screen.
/// </remarks>
public class OutboxScreenTests : IDisposable
{
    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();
    private readonly InstallSession _session;

    public OutboxScreenTests()
    {
        _session = InstallSession.Open(_tree.Root).Session!;
    }

    public void Dispose()
    {
        _session.Dispose();
        _tree.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void An_empty_outbox_says_what_would_wait_here()
    {
        var list = Assert.IsType<ListScreen>(OutboxScreens.List(_session));

        Assert.Empty(list.Rows);
        Assert.Contains("play session", list.EmptyMessage!, StringComparison.Ordinal);
        Assert.Null(list.Note!());
    }

    [Fact]
    public async Task An_offline_flush_shows_pending_SRAM_with_no_outbox_entries()
    {
        const string title = "Zelda II";
        var save = AddNesSave(42, title, "offline progress");
        var token = TestContext.Current.CancellationToken;
        var flush = await new SaveFlushService(_session).RunAsync(
            new FlushOptions(Offline: true) { Mode = FlushMode.InSessionUpload }, cancellationToken: token);

        Assert.Equal(FlushState.LocalOnly, flush.State);
        Assert.Equal(0, flush.Queued);
        var indexed = Assert.Single(_session.Store.Saves.List(42));
        Assert.Equal(save, indexed.Path);
        Assert.True(indexed.IsUnsent);

        var model = new StatusViewModel(_session, () => NoGamepad);
        Assert.Equal("1 item", model.Sections().Single(section => section.Title == "Waiting")
            .Rows.Single(row => row.Label == "Waiting to upload").Value);
        var menu = Assert.IsType<ListScreen>(RootScreens.Menu(_session, () => NoGamepad,
            new RootScreens.RootRoutes { OpenOutbox = () => OutboxScreens.List(_session) }));
        Assert.Equal("1 waiting", menu.Rows.Single(row => row.Label == "Waiting to upload").Value);

        var navigator = new Navigator(menu);
        RootMenuDriver.Open(navigator, "Waiting to upload");
        var screen = Assert.IsType<ListScreen>(navigator.Current);
        var row = Assert.Single(screen.Rows);
        Assert.Equal(title, row.Label);
        Assert.Equal("Battery save", row.Value);
        Assert.Contains("libretro:battery", row.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(screen.Hints, hint => hint.Action == NavAction.Accept);
        navigator.Handle(NavAction.Accept);
        Assert.Same(screen, navigator.Current);
        navigator.Handle(NavAction.Back);
        Assert.Same(menu, navigator.Current);
        Assert.Equal("Back to EmulationStation", Assert.Single(menu.Hints, hint => hint.Action == NavAction.Back).Label);
        Assert.Equal("offline progress", File.ReadAllText(_session.Install.Resolve(save)));

        var origin = new Uri("https://romm.invalid");
        var now = DateTimeOffset.UtcNow;
        _session.Store.Device.EnsureIdentity(DeviceIdentity.ReadOrCreate(_session.Install));
        _session.Store.Device.SavePairing(
            new PairingResult(origin, "device-1", "Handheld", new GrantedScopes(["assets.write"]),
                TokenProtector.Protect("rmm_test", null, now.AddYears(1))), now);
        using var stub = new StubRomMServer();
        stub.NegotiateActions[(42, "libretro:battery")] = "upload";
        using var connection = new RomMConnection(new RomMClientOptions { Origin = origin, AccessToken = "rmm_test" }, stub);
        var sent = await new SaveFlushService(_session).RunAsync(
            new FlushOptions { Mode = FlushMode.InSessionUpload }, connection, token);

        Assert.Equal(1, sent.SavesSent?.Uploaded);
        Assert.Empty(Assert.IsType<ListScreen>(OutboxScreens.List(_session)).Rows);
        var waiting = model.Sections().Single(section => section.Title == "Waiting");
        Assert.Equal("empty", waiting.Rows.Single(row => row.Label == "Waiting to upload").Value);
        Assert.Contains(waiting.Rows, item => item.Label == "Last in-session upload");
    }

    [Fact]
    public async Task Only_changed_attributed_and_non_conflicted_saves_are_listed_beside_the_outbox()
    {
        AddNesSave(41, "Already sent", "unchanged");
        AddNesSave(42, "Needs a decision", "local side");
        var pending = AddNesSave(43, "Waiting game", "original progress");
        var install = _session.Install;
        File.WriteAllText(install.Resolve(RelativePath.Create("saves/nes/Orphan.srm")), "unattributed");
        var flush = new SaveFlushService(_session);
        var token = TestContext.Current.CancellationToken;
        await flush.RunAsync(new FlushOptions(Offline: true), cancellationToken: token);
        var now = DateTimeOffset.UtcNow;
        var sent = Assert.Single(_session.Store.Saves.List(41));
        _session.Store.Saves.MarkUploaded(sent.Path, sent.UnitKey, sent.ContentHash!, now);
        var previous = Assert.Single(_session.Store.Saves.List(43));
        _session.Store.Saves.MarkUploaded(previous.Path, previous.UnitKey, previous.ContentHash!, now);
        File.SetLastWriteTimeUtc(install.Resolve(sent.Path), now.UtcDateTime.AddMinutes(1));
        File.WriteAllText(install.Resolve(pending), "new offline progress");

        var conflict = Assert.Single(_session.Store.Saves.List(42));
        _session.Store.SaveConflicts.Record(new SaveConflictRecord(42, conflict.Slot, conflict.Path,
            null, conflict.ContentHash, new string('a', 32), now, 27, "Both sides changed", now, now, null, null), now);
        await flush.RunAsync(new FlushOptions(Offline: true), cancellationToken: token);
        Assert.Contains(_session.Store.Saves.List(), save => save.RomId is null && save.Path.Name == "Orphan.srm");
        _session.Store.Outbox.Enqueue(OutboxKind.PlaySession, now, romId: 50);

        var list = Assert.IsType<ListScreen>(OutboxScreens.List(_session));
        Assert.Equal(["Not sent yet", "Waiting game"], list.Rows.Select(row => row.Label));
        var navigator = new Navigator(list);
        Open(navigator, "Not sent yet");
        navigator.Handle(NavAction.Left);
        navigator.Handle(NavAction.Accept);
        Assert.Equal(0, _session.Store.Outbox.PendingCount());
        Assert.Equal("new offline progress", File.ReadAllText(install.Resolve(pending)));
        Assert.Single(_session.Store.Saves.PendingUploads());
        Assert.Single(_session.Store.SaveConflicts.ListOpen());
    }

    private static GamepadStatus NoGamepad =>
        new(GamepadAvailability.NoDevice, null, null, "No controller is connected.");

    private RelativePath AddNesSave(int id, string title, string contents)
    {
        var rom = RelativePath.Create($"roms/nes/{title}.nes");
        var save = RelativePath.Create($"saves/nes/{title}.srm");
        foreach (var (path, text) in new[] { (rom, "rom"), (save, contents) })
        {
            var full = _session.Install.Resolve(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, text);
        }

        _session.Store.Files.Record(new LocalFile
        {
            Path = rom, Folder = "nes", RomId = id, FileName = rom.Name, SizeBytes = 3,
        });
        _session.Store.Metadata.Record(new GameMetadata
        {
            RomId = id, Folder = "nes", FsName = rom.Name, Name = title,
        });
        return save;
    }

    [Fact]
    public void A_refused_entry_is_named_after_its_game_and_dropping_it_leaves_the_rest()
    {
        RecordGame(41, "Super Metroid (USA)");
        var refused = Refuse(Enqueue(41));
        var other = Refuse(Enqueue(42));
        Enqueue(43);

        var list = Assert.IsType<ListScreen>(OutboxScreens.List(_session));

        Assert.Equal("Super Metroid (USA)", list.Rows[0].Label);
        Assert.Equal("save refused", list.Rows[0].Value);
        Assert.Equal("Battery save. HTTP 422: bad slot.", list.Rows[0].Detail);

        // No metadata for the second, so it falls back to the id rather than a blank label.
        Assert.Equal("Game 42", list.Rows[1].Label);
        Assert.NotNull(list.Note!());

        var navigator = new Navigator(list);
        navigator.Handle(NavAction.Accept);

        var confirm = Assert.IsType<ConfirmScreen>(navigator.Current);
        Assert.Same(list, confirm.Underneath);

        // Nothing goes until the confirmation is answered, and Keep is selected first.
        Assert.Equal("Keep", confirm.Buttons[confirm.Selected].Label);
        Assert.Equal(2, _session.Store.Outbox.FailedCount());

        navigator.Handle(NavAction.Left);
        navigator.Handle(NavAction.Accept);

        Assert.Equal([other], _session.Store.Outbox.Failed().Select(entry => entry.Id));
        Assert.Equal(1, _session.Store.Outbox.PendingCount());
        Assert.NotEqual(refused, other);

        // Answered once: the box says what happened, and its only button is Done, which closes
        // it rather than dropping again.
        Assert.True(confirm.IsAnswered);
        Assert.StartsWith("Save dropped.", confirm.Question, StringComparison.Ordinal);
        Assert.DoesNotContain("exists only", confirm.Question, StringComparison.Ordinal);
        Assert.Equal(ListScreen.DoneLabel, Assert.Single(confirm.Hints, hint => hint.Action == NavAction.Accept).Label);
        Assert.Equal(ScreenCommandKind.Pop, confirm.Handle(NavAction.Accept).Kind);
    }

    [Fact]
    public void A_server_reason_without_a_full_stop_still_ends_before_the_next_sentence()
    {
        // RomM sends "end_time is too far in the future" bare, and the agent tree read
        // "...too far in the future Dropping deletes RomMBat's record of it."
        var id = Enqueue(41);
        _session.Store.Outbox.MarkFailed(id, "end_time is too far in the future", DateTimeOffset.UtcNow);

        var navigator = new Navigator(OutboxScreens.List(_session));
        navigator.Handle(NavAction.Accept);

        var confirm = Assert.IsType<ConfirmScreen>(navigator.Current);
        Assert.Contains("in the future. Dropping deletes", Assert.Single(confirm.Details!()).Detail ?? string.Empty, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Is the clock right?", "Is the clock right? Dropping deletes")]
    [InlineData("Too late!", "Too late! Dropping deletes")]
    public void A_server_reason_that_already_ends_a_sentence_gets_no_full_stop_after_it(string reason, string expected)
    {
        var id = Enqueue(41);
        _session.Store.Outbox.MarkFailed(id, reason, DateTimeOffset.UtcNow);

        var navigator = new Navigator(OutboxScreens.List(_session));
        navigator.Handle(NavAction.Accept);

        var confirm = Assert.IsType<ConfirmScreen>(navigator.Current);
        Assert.Contains(expected, Assert.Single(confirm.Details!()).Detail ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_refused_entry_goes_together_and_no_pending_one_with_them()
    {
        Refuse(Enqueue(1));
        Refuse(Enqueue(2));
        Enqueue(3);

        var navigator = new Navigator(OutboxScreens.List(_session));
        Open(navigator, "Every refused entry");
        navigator.Handle(NavAction.Left);
        navigator.Handle(NavAction.Accept);

        Assert.Equal(0, _session.Store.Outbox.FailedCount());
        Assert.Equal(1, _session.Store.Outbox.PendingCount());
        var answer = Assert.IsType<ConfirmScreen>(navigator.Current).Question;
        Assert.StartsWith("Refused entries dropped.", answer, StringComparison.Ordinal);

        // Not "they exist only on this device", which read as the opposite of dropped.
        Assert.Contains("RomM never got", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("exist only", answer, StringComparison.Ordinal);
    }

    [Fact]
    public void One_refused_entry_is_not_offered_twice()
    {
        Refuse(Enqueue(1));

        var list = Assert.IsType<ListScreen>(OutboxScreens.List(_session));

        Assert.Single(list.Rows);
    }

    [Fact]
    public void Unsent_entries_go_only_all_together_and_leave_refused_ones()
    {
        var refused = Refuse(Enqueue(1));
        Enqueue(2);
        Enqueue(3);

        var list = Assert.IsType<ListScreen>(OutboxScreens.List(_session));
        var row = Assert.Single(list.Rows, row => row.Label == "Not sent yet");
        Assert.Equal("2 waiting", row.Value);

        var navigator = new Navigator(list);
        Open(navigator, "Not sent yet");
        navigator.Handle(NavAction.Left);
        navigator.Handle(NavAction.Accept);

        Assert.Equal(0, _session.Store.Outbox.PendingCount());
        Assert.Equal([refused], _session.Store.Outbox.Failed().Select(entry => entry.Id));
    }

    [Fact]
    public void An_entry_gone_before_the_press_reports_nothing_dropped()
    {
        var id = Refuse(Enqueue(1));

        var navigator = new Navigator(OutboxScreens.List(_session));
        navigator.Handle(NavAction.Accept);

        // The console dropped it while the confirmation was on screen.
        _session.Store.Outbox.DropFailed(id);
        navigator.Handle(NavAction.Left);
        navigator.Handle(NavAction.Accept);

        Assert.StartsWith("Nothing dropped.", Assert.IsType<ConfirmScreen>(navigator.Current).Question, StringComparison.Ordinal);
    }

    [Fact]
    public void The_root_row_counts_both_kinds_and_opens_the_outbox()
    {
        Refuse(Enqueue(1));
        Enqueue(2);

        var menu = Assert.IsType<ListScreen>(RootScreens.Menu(
            _session,
            () => new GamepadStatus(GamepadAvailability.NoDevice, null, null, "No controller is connected."),
            new RootScreens.RootRoutes { OpenOutbox = () => OutboxScreens.List(_session) }));

        Assert.Equal("1 waiting, 1 refused", menu.Rows.Single(row => row.Label == "Waiting to upload").Value);

        var navigator = new Navigator(menu);
        RootMenuDriver.Open(navigator, "Waiting to upload");

        Assert.Equal("Waiting to upload", Assert.IsType<ListScreen>(navigator.Current).Title);
    }

    [Fact]
    public void A_refused_play_session_names_no_save()
    {
        // A session carries its emulator's battery slot, and calling it a battery save would name
        // something that is not in the entry.
        _session.Store.Outbox.Enqueue(OutboxKind.PlaySession, DateTimeOffset.UtcNow, romId: 41, slot: "libretro:battery");
        Refuse(_session.Store.Outbox.Pending()[^1].Id);

        var row = Assert.Single(Assert.IsType<ListScreen>(OutboxScreens.List(_session)).Rows);

        Assert.Equal("play session refused", row.Value);
        Assert.Equal("HTTP 422: bad slot.", row.Detail);
    }

    private long Enqueue(int romId)
    {
        _session.Store.Outbox.Enqueue(OutboxKind.Save, DateTimeOffset.UtcNow, romId: romId, slot: "libretro:battery");
        var pending = _session.Store.Outbox.Pending();
        return pending[^1].Id;
    }

    private long Refuse(long id)
    {
        _session.Store.Outbox.MarkFailed(id, "HTTP 422: bad slot.", DateTimeOffset.UtcNow);
        return id;
    }

    private void RecordGame(int romId, string name) =>
        _session.Store.Metadata.Record(new GameMetadata
        {
            RomId = romId,
            Folder = "snes",
            FsName = $"{name}.sfc",
            Name = name,
        });

    /// <summary>Moves down to the row with this label and opens it.</summary>
    private static void Open(Navigator navigator, string label)
    {
        var list = Assert.IsType<ListScreen>(navigator.Current);

        for (var step = 0; step < list.Rows.Count; step++)
        {
            if (list.Rows[list.Cursor].Label == label)
            {
                navigator.Handle(NavAction.Accept);
                return;
            }

            navigator.Handle(NavAction.Down);
        }

        Assert.Fail($"No selectable row is labeled '{label}'.");
    }
}
