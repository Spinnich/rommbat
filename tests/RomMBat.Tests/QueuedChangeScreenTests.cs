using RomMBat.Core;
using RomMBat.Core.Paths;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using RomMBat.UI.Input;
using RomMBat.UI.Screens;
using RomMBat.UI.Shell;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The queued-config surface, where a queued change can be canceled from the couch.
/// </summary>
/// <remarks>
/// <b>Queueing is not a convenience here, it is the only mechanism.</b> EmulationStation loads
/// <c>es_settings.cfg</c> at startup and serializes its own model over anything written
/// afterwards, and RomMBat is launched from the ES menu, so it runs under a live ES every single
/// time. That is why there is no apply path on any of these screens and why "waiting for you to
/// quit EmulationStation" is a permanent answer rather than a transient one.
/// </remarks>
public class QueuedChangeScreenTests : IDisposable
{
    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();
    private readonly InstallSession _session;

    public QueuedChangeScreenTests()
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
    public void An_empty_queue_says_why_it_is_almost_always_empty()
    {
        var list = Assert.IsType<ListScreen>(QueuedChangeScreens.List(_session));

        Assert.Empty(list.Rows);
        Assert.NotNull(list.EmptyMessage);

        // Nothing is queued is the normal state, so the message explains what the screen is for
        // rather than reading as something having gone wrong.
        Assert.Contains("EmulationStation", list.EmptyMessage!, StringComparison.Ordinal);

        // And no note, because there is nothing waiting to explain.
        Assert.Null(list.Note!());
    }

    [Fact]
    public void An_outstanding_change_can_be_canceled_and_the_row_goes()
    {
        _session.Store.PendingConfig.Queue(Request());

        var list = Assert.IsType<ListScreen>(QueuedChangeScreens.List(_session));
        var row = Assert.Single(list.Rows);

        Assert.Equal("waiting", row.Value);
        Assert.Contains("Nothing has been written", list.Note!()!, StringComparison.Ordinal);

        var navigator = new Navigator(list);
        navigator.Handle(NavAction.Accept);

        var confirm = Assert.IsType<ConfirmScreen>(navigator.Current);

        Assert.Same(list, confirm.Underneath);
        Assert.Equal("Keep it queued", confirm.Buttons[confirm.Selected].Label);
        Assert.Equal("Cancel this change?", confirm.Question);

        // The stored reason is a phrase, so it is made a sentence of its own rather than run
        // into the next one (#448).
        Assert.StartsWith(
            "Queued: a per-game memory card for 'Armored Core 3 (USA).iso'. Nothing",
            Assert.Single(confirm.Rows).Detail,
            StringComparison.Ordinal);

        navigator.Handle(NavAction.Left);
        navigator.Handle(NavAction.Accept);

        // Canceled deletes, because nothing happened and there is nothing to report. Only an
        // applied change keeps its outcome.
        Assert.Empty(_session.Store.PendingConfig.ListOutstanding());
        Assert.Empty(_session.Store.PendingConfig.ListFinished());

        // Answered once: the box says what happened rather than staying a question over its own
        // answer (#448), and its only button is Done, which finishes rather than re-running a
        // change that has already happened.
        Assert.True(confirm.IsAnswered);
        Assert.StartsWith("Change canceled.", confirm.Question, StringComparison.Ordinal);
        Assert.Empty(confirm.Rows);

        var done = Assert.Single(confirm.Hints, hint => hint.Action == NavAction.Accept);
        Assert.Equal(ListScreen.DoneLabel, done.Label);
        Assert.Equal(ScreenCommandKind.Pop, confirm.Handle(NavAction.Accept).Kind);

        navigator.Handle(NavAction.Back);
        Assert.Empty(Assert.IsType<ListScreen>(navigator.Current).Rows);
    }

    [Fact]
    public void A_change_something_already_got_to_is_shown_and_cannot_be_acted_on()
    {
        var id = _session.Store.PendingConfig.Queue(Request());

        _session.Store.PendingConfig.RecordResult(
            id,
            PendingConfigResult.Refused,
            "The console's shared card is in use by another game.",
            DateTimeOffset.UtcNow);

        var list = Assert.IsType<ListScreen>(QueuedChangeScreens.List(_session));
        var row = Assert.Single(list.Rows);

        Assert.Equal("refused", row.Value);

        // Shown but not choosable. A refusal is the outcome a person most needs to see and the
        // one that produces no other sign: the setting is simply not what they asked for, weeks
        // later, with the console output that explained it long gone.
        Assert.False(row.Available);
        Assert.Contains("shared card", row.Detail!, StringComparison.Ordinal);

        // Nothing to cancel, so nothing offers it.
        Assert.DoesNotContain(list.Hints, hint => hint.Action == NavAction.Accept);
    }

    [Fact]
    public void The_queued_screens_name_no_face_button()
    {
        _session.Store.PendingConfig.Queue(Request());

        var list = Assert.IsType<ListScreen>(QueuedChangeScreens.List(_session));
        var navigator = new Navigator(list);
        navigator.Handle(NavAction.Accept);

        var confirm = Assert.IsType<ConfirmScreen>(navigator.Current);

        foreach (var screen in new IScreen[] { list, confirm })
        {
            var strings = Text(screen)
                .Append((screen as ListScreen)?.EmptyMessage)
                .Append((screen as ListScreen)?.Note?.Invoke())
                .Where(text => text is not null);

            foreach (var text in strings)
            {
                Assert.DoesNotContain("press ", text!, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("button", text!, StringComparison.OrdinalIgnoreCase);
            }

            Assert.All(screen.Hints, hint => Assert.Contains(hint.Action, NavRepeat.Bound));
        }
    }

    /// <summary>Every string the screen draws.</summary>
    private static IEnumerable<string> Text(IScreen screen) =>
        ((screen as IWindowedScreen)?.Rows ?? [])
            .SelectMany(row => new[] { row.Label, row.Value, row.Detail })
            .Concat(screen.Hints.Select(hint => hint.Label))
            .Append(screen.Title)
            .Append((screen as ConfirmScreen)?.Question)
            .Concat((screen as ConfirmScreen)?.Buttons.Select(button => button.Label) ?? [])
            .OfType<string>();

    private static PendingConfigRequest Request() => new()
    {
        RomId = 4242,
        System = "ps2",
        FsName = "Armored Core 3 (USA).iso",
        SettingKey = "pcsx2_slot1_memory",
        DesiredState = DesiredSettingState.Set,
        DesiredValue = "game",
        // What SaveConverter stores: a phrase, lower-case and with no full stop.
        Reason = "a per-game memory card for 'Armored Core 3 (USA).iso'",
        QueuedAtUtc = DateTimeOffset.UtcNow,
    };
}
