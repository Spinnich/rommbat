using RomMBat.Core;
using RomMBat.Core.Metadata;
using RomMBat.Core.RetroBat;
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
        Assert.Equal(ListScreen.DoneLabel, Assert.Single(confirm.Hints, hint => hint.Action == NavAction.Accept).Label);
        Assert.Equal(ScreenCommandKind.Pop, confirm.Handle(NavAction.Accept).Kind);
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
        Assert.StartsWith("Refused entries dropped.", Assert.IsType<ConfirmScreen>(navigator.Current).Question, StringComparison.Ordinal);
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
