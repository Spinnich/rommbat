using RomMBat.UI.Input;
using RomMBat.UI.Screens;
using RomMBat.UI.Shell;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The rules every screen's buttons follow, on screens built for the purpose.
/// </summary>
/// <remarks>
/// <b>The bottom button confirms or moves on, the right button cancels or goes back, and Start
/// opens a menu</b>, as in EmulationStation (RB-421 to RB-424). These pin the shell pieces
/// that carry the rule, so a screen gets it by using them rather than by remembering it.
/// </remarks>
public sealed class ControlGrammarTests
{
    [Fact]
    public void Start_opens_the_menu_and_a_pick_runs_over_the_screen_it_belongs_to()
    {
        var opened = new MessageScreen("Opened", "by the action");
        var screen = Offering(new ScreenAction("Open it", () => ScreenCommand.Push(opened)));
        var navigator = new Navigator(screen);

        navigator.Handle(NavAction.Start);

        var menu = Assert.IsType<ActionMenuScreen>(navigator.Current);
        Assert.Same(screen, menu.Underneath);
        Assert.Equal(screen.Title, menu.Title);

        navigator.Handle(NavAction.Accept);

        // The menu left before the action ran, so backing out of what it opened lands on the
        // screen rather than on a menu nobody asked to see again.
        Assert.Same(opened, navigator.Current);
        Assert.Equal(2, navigator.Depth);

        navigator.Handle(NavAction.Back);
        Assert.Same(screen, navigator.Current);
    }

    [Fact]
    public void Start_or_back_closes_the_menu_and_nothing_runs()
    {
        var ran = 0;
        var screen = Offering(new ScreenAction("Count", () =>
        {
            ran++;
            return ScreenCommand.Stay;
        }));

        var navigator = new Navigator(screen);

        foreach (var close in new[] { NavAction.Start, NavAction.Back })
        {
            navigator.Handle(NavAction.Start);
            Assert.IsType<ActionMenuScreen>(navigator.Current);

            navigator.Handle(close);
            Assert.Same(screen, navigator.Current);
        }

        Assert.Equal(0, ran);
    }

    [Fact]
    public void An_action_that_acts_in_place_closes_the_menu_and_refreshes_the_screen()
    {
        var state = "before";
        var screen = new ListScreen("Pane", () => [new ListRow(state)], _ => ScreenCommand.Stay)
        {
            ActionList = () =>
            [
                new ScreenAction("Change it", () =>
                {
                    state = "after";
                    return ScreenCommand.Stay;
                }),
            ],
        };

        var navigator = new Navigator(screen);
        ActionMenuDriver.Choose(navigator, "Change it");

        Assert.Same(screen, navigator.Current);
        Assert.Equal("after", screen.Rows[0].Label);
    }

    [Fact]
    public void A_shortcut_runs_its_action_and_the_footer_offers_it()
    {
        var opened = new MessageScreen("Opened", "by the shortcut");
        var screen = Offering(new ScreenAction("Sync", () => ScreenCommand.Push(opened)) { Shortcut = NavAction.Alternate });

        Assert.Contains(screen.Hints, hint => hint.Action == NavAction.Alternate && hint.Label == "Sync");
        Assert.Contains(screen.Hints, hint => hint.Action == NavAction.Start && hint.Label == "Menu");

        var navigator = new Navigator(screen);
        navigator.Handle(NavAction.Alternate);

        Assert.Same(opened, navigator.Current);
    }

    [Fact]
    public void An_unavailable_action_keeps_its_reason_and_neither_its_shortcut_nor_the_menu_runs_it()
    {
        var ran = false;
        var screen = Offering(
            new ScreenAction("Blocked", () =>
            {
                ran = true;
                return ScreenCommand.Stay;
            })
            {
                Shortcut = NavAction.Extra,
                Unavailable = "Not now.",
            },
            new ScreenAction("Fine", () => ScreenCommand.Stay));

        // Not offered on its button, but listed with its reason, as a dimmed row is.
        Assert.DoesNotContain(screen.Hints, hint => hint.Action == NavAction.Extra);

        var navigator = new Navigator(screen);
        navigator.Handle(NavAction.Extra);
        Assert.False(ran);

        navigator.Handle(NavAction.Start);
        var menu = Assert.IsType<ActionMenuScreen>(navigator.Current);

        var blocked = Assert.Single(menu.Rows, row => row.Label == "Blocked");
        Assert.False(blocked.Available);
        Assert.Equal("Not now.", blocked.Detail);

        // The cursor starts on the first choosable action, so a press cannot land on it.
        Assert.Equal("Fine", menu.Rows[menu.Cursor].Label);
        Assert.False(ran);
    }

    [Fact]
    public void A_message_inside_the_app_closes_back_to_its_screen_on_either_button()
    {
        // The confirm button on "this game cannot be put on this device" must not close the app.
        foreach (var press in new[] { NavAction.Accept, NavAction.Back })
        {
            var message = new MessageScreen("Refused", "Why.");
            var pushing = new ListScreen("Pusher", [new ListRow("Go")], _ => ScreenCommand.Push(message));
            var stack = new Navigator(pushing);
            stack.Handle(NavAction.Accept);
            Assert.Same(message, stack.Current);

            Assert.True(stack.Handle(press));
            Assert.Same(pushing, stack.Current);
            Assert.False(stack.HasExited);
        }
    }

    [Fact]
    public void A_startup_refusal_is_the_one_message_that_leaves_RomMBat()
    {
        var navigator = new Navigator(MessageScreen.Fatal("RomMBat cannot start", "No tree."));

        Assert.False(navigator.Handle(NavAction.Accept));
        Assert.True(navigator.HasExited);
    }

    [Fact]
    public void A_confirmation_selects_the_safe_answer_first_and_back_means_it()
    {
        var risky = 0;
        var confirm = ConfirmScreen.YesNo(
            "Stop?",
            "Stop",
            () =>
            {
                risky++;
                return ScreenCommand.Pop;
            },
            "Keep going");

        // A reflexive press of the confirm button changes nothing.
        Assert.Equal("Keep going", confirm.Buttons[confirm.Selected].Label);
        Assert.Equal(ScreenCommandKind.Pop, confirm.Handle(NavAction.Accept).Kind);
        Assert.Equal(0, risky);

        // The right button is the safe answer wherever the selection is.
        confirm.Handle(NavAction.Left);
        Assert.Equal("Stop", confirm.Buttons[confirm.Selected].Label);
        Assert.Equal(ScreenCommandKind.Pop, confirm.Handle(NavAction.Back).Kind);
        Assert.Equal(0, risky);

        // Clamped at the ends rather than wrapped, as ES's row of buttons is.
        confirm.Handle(NavAction.Left);
        Assert.Equal(0, confirm.Selected);

        confirm.Handle(NavAction.Accept);
        Assert.Equal(1, risky);

        // The footer names what each button does now, and the right button only once it differs.
        Assert.Equal("Stop", Assert.Single(confirm.Hints, hint => hint.Action == NavAction.Accept).Label);
        Assert.Equal("Keep going", Assert.Single(confirm.Hints, hint => hint.Action == NavAction.Back).Label);

        confirm.Handle(NavAction.Right);
        Assert.DoesNotContain(confirm.Hints, hint => hint.Action == NavAction.Back);
    }

    [Fact]
    public async Task A_confirmation_with_a_preview_cannot_be_answered_until_the_preview_lands()
    {
        var release = new TaskCompletionSource();
        var ran = 0;
        var rows = new List<ListRow>();

        var box = new ConfirmScreen(
            "Remove these?",
            [
                new ConfirmButton("Remove", () =>
                {
                    ran++;
                    return ScreenCommand.Pop;
                })
                {
                    EnabledWhen = () => rows.Count > 0,
                },
                new ConfirmButton("Keep them", () => ScreenCommand.Pop),
            ],
            1)
        {
            Details = () => rows,
            LoadingMessage = "Working out what can go...",
            Load = async token =>
            {
                await release.Task.WaitAsync(token);
                rows.Add(new ListRow("It goes", "1 KB", "Saves are never removed.", false));
                return null;
            },
        }.Started();

        // While it loads the risky answer cannot be reached, so neither button nor a stray
        // press can act on a preview nobody has seen.
        Assert.True(box.IsLoading);
        box.Handle(NavAction.Left);
        Assert.Equal("Keep them", box.Buttons[box.Selected].Label);
        Assert.Empty(box.Rows);

        release.SetResult();

        for (var attempt = 0; attempt < 200 && box.IsLoading; attempt++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Equal("It goes", Assert.Single(box.Rows).Label);

        box.Handle(NavAction.Left);
        Assert.Equal("Remove", box.Buttons[box.Selected].Label);
        box.Handle(NavAction.Accept);
        Assert.Equal(1, ran);

        box.Dispose();
    }

    [Fact]
    public void An_answered_confirmation_says_what_happened_and_offers_only_done()
    {
        ConfirmScreen? box = null;

        box = ConfirmScreen.YesNo("Drop it?", "Drop", () => box!.Answer("Dropped. It exists only here."), "Keep");

        box.Handle(NavAction.Left);
        Assert.Equal(ScreenCommandKind.Stay, box.Handle(NavAction.Accept).Kind);

        Assert.True(box.IsAnswered);
        Assert.Equal("Dropped. It exists only here.", box.Question);
        Assert.Equal(ListScreen.DoneLabel, Assert.Single(box.Buttons).Label);
        Assert.Equal(ScreenCommandKind.Pop, box.Handle(NavAction.Accept).Kind);
        Assert.Equal(ScreenCommandKind.Pop, box.Handle(NavAction.Back).Kind);
    }

    [Fact]
    public void A_finished_screen_leaves_on_the_confirm_button_and_says_done_there()
    {
        var finished = new ListScreen("Finished", [new ListRow("It happened", null, null, false)], _ => ScreenCommand.Stay, string.Empty, ListScreen.DoneLabel)
        {
            Reading = true,
        };

        Assert.Equal(ListScreen.DoneLabel, Assert.Single(finished.Hints, hint => hint.Action == NavAction.Accept).Label);
        Assert.DoesNotContain(finished.Hints, hint => hint.Action == NavAction.Back);

        Assert.Equal(ScreenCommandKind.Pop, finished.Handle(NavAction.Accept).Kind);
        Assert.Equal(ScreenCommandKind.Pop, finished.Handle(NavAction.Back).Kind);
    }

    [Fact]
    public async Task Done_does_not_leave_while_the_work_behind_it_is_still_running()
    {
        // A screen labeled Done that is still loading is still doing the work, and leaving it
        // cancels that work: a set removal stopped part way leaves the set with some of its
        // games gone (R2.1 on #495). Only Back may cancel it, as it always could.
        var release = new TaskCompletionSource();

        var applying = new ListScreen("Removing", () => [], _ => ScreenCommand.Stay, string.Empty, ListScreen.DoneLabel)
        {
            Reading = true,
            Load = async token =>
            {
                await release.Task.WaitAsync(token);
                return null;
            },
        }.Started();

        Assert.True(applying.IsLoading);
        Assert.DoesNotContain(applying.Hints, hint => hint.Action == NavAction.Accept);
        Assert.Equal(ScreenCommandKind.Stay, applying.Handle(NavAction.Accept).Kind);

        release.SetResult();

        for (var attempt = 0; attempt < 200 && applying.IsLoading; attempt++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.False(applying.IsLoading);
        Assert.Equal(ListScreen.DoneLabel, Assert.Single(applying.Hints, hint => hint.Action == NavAction.Accept).Label);
        Assert.Equal(ScreenCommandKind.Pop, applying.Handle(NavAction.Accept).Kind);

        applying.Dispose();
    }

    [Fact]
    public void A_search_can_be_cleared_and_nothing_else_accepts_no_text()
    {
        string? searched = "old";

        var search = new OnScreenKeyboard("Search", "Type.", string.Empty, typed =>
        {
            searched = typed;
            return new TypedResult(null);
        })
        {
            AllowEmpty = true,
        };

        Assert.Equal(ScreenCommandKind.Pop, search.Handle(NavAction.Start).Kind);
        Assert.Equal(string.Empty, searched);

        var address = new OnScreenKeyboard("Address", "Type.", string.Empty, _ => new TypedResult(null));
        Assert.Equal(ScreenCommandKind.Stay, address.Handle(NavAction.Start).Kind);
    }

    private static ListScreen Offering(params ScreenAction[] actions) =>
        new("Screen", [new ListRow("Row")], _ => ScreenCommand.Stay)
        {
            ActionList = () => actions,
        };
}
