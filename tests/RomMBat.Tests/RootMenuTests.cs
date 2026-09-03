using RomM.Client;
using RomMBat.Core;
using RomMBat.Core.Identity;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using RomMBat.UI.Input;
using RomMBat.UI.Screens;
using RomMBat.UI.Shell;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The root menu, which stage 7b-3 made a list because the buttons ran out.
/// </summary>
/// <remarks>
/// <b>The claim these assert is that no verb is unreachable.</b> Until this stage the root put
/// one action on each of Accept, Start, Extra and Alternate, which is every button a screen has,
/// and 7b-3 needs three more entry points than that. A row can be added where a button cannot,
/// so the failure mode moved: instead of a verb with nowhere to go, the risk is a row that goes
/// nowhere, and that is what the first test here refuses to let happen.
/// </remarks>
public class RootMenuTests
{
    private static GamepadStatus NoPad =>
        new(GamepadAvailability.NoDevice, null, null, "No controller is connected.");

    [Fact]
    public void Every_row_on_the_root_opens_a_screen()
    {
        using var tree = TempRetroBatTree.Create();
        using var session = InstallSession.Open(tree.Root).Session!;

        var opened = 0;

        IScreen Stub()
        {
            opened++;
            return new ListScreen("stub", [new ListRow("x")], _ => ScreenCommand.Stay);
        }

        var menu = Assert.IsType<ListScreen>(RootScreens.Menu(
            session,
            () => NoPad,
            new RootScreens.RootRoutes
            {
                StartPairing = Stub,
                OpenSets = Stub,
                OpenBrowse = Stub,
                OpenBudget = Stub,
                OpenConflicts = Stub,
            }));

        var navigator = new Navigator(menu);
        var rows = menu.Rows.Count;

        Assert.True(rows > 0, "the root menu has no rows at all");

        // Every row, by walking rather than by index, because walking is what a person does and
        // an unavailable row is one the cursor skips.
        for (var step = 0; step < rows; step++)
        {
            var label = menu.Rows[menu.Cursor].Label;

            navigator.Handle(NavAction.Accept);

            Assert.True(
                navigator.Depth == 2,
                $"the root row '{label}' did nothing when it was chosen");

            navigator.Handle(NavAction.Back);
            navigator.Handle(NavAction.Down);
        }

        // The last row is the status pane, which the menu builds itself rather than taking as a
        // route, so it is one fewer than the rows walked.
        Assert.Equal(rows - 1, opened);
    }

    [Fact]
    public void The_root_names_no_face_button()
    {
        using var tree = TempRetroBatTree.Create();
        using var session = InstallSession.Open(tree.Root).Session!;

        var menu = Assert.IsType<ListScreen>(
            RootScreens.Menu(session, () => NoPad, new RootScreens.RootRoutes()));

        var strings = menu.Rows
            .SelectMany(row => new[] { row.Label, row.Value, row.Detail })
            .Concat(menu.Hints.Select(hint => hint.Label))
            .Append(menu.Title)
            .Where(text => text is not null);

        foreach (var text in strings)
        {
            // A screen cannot name a face button: the letter differs per pad layout, and "press
            // A" reaches a Switch Pro user as back, which closes RomMBat.
            Assert.DoesNotContain("press ", text!, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("button", text!, StringComparison.OrdinalIgnoreCase);
        }

        Assert.All(menu.Hints, hint => Assert.Contains(hint.Action, NavRepeat.Bound));
    }

    [Fact]
    public void The_pairing_row_says_which_of_the_three_states_this_install_is_in()
    {
        using var tree = TempRetroBatTree.Create();
        using var session = InstallSession.Open(tree.Root).Session!;

        var menu = Assert.IsType<ListScreen>(
            RootScreens.Menu(session, () => NoPad, new RootScreens.RootRoutes()));

        Assert.Equal("not paired", menu.Rows.Single(row => row.Label == "Pair with RomM").Value);

        Pair(session, DateTimeOffset.UtcNow.AddDays(90));
        menu.Returned();
        Assert.Equal("paired", menu.Rows.Single(row => row.Label == "Pair again").Value);

        // An expired token is ordinary on a portable drive rather than a fault, and it is the
        // one state that needs acting on, so it is named rather than folded into "paired".
        Pair(session, DateTimeOffset.UtcNow.AddDays(-1));
        menu.Returned();
        Assert.Equal("token expired", menu.Rows.Single(row => row.Label == "Pair again").Value);
    }

    [Fact]
    public void The_status_pane_scrolls_rather_than_drawing_every_line_off_the_display()
    {
        using var tree = TempRetroBatTree.Create();
        using var session = InstallSession.Open(tree.Root).Session!;

        // The shape that overflows: paired, with a queue behind it. Four sections is the short
        // form and fits, which is why this went unseen while status was the root screen.
        Pair(session, DateTimeOffset.UtcNow.AddDays(90));

        for (var index = 0; index < 12; index++)
        {
            session.Store.PendingConfig.Queue(new PendingConfigRequest
            {
                RomId = index + 1,
                System = "ps2",
                FsName = $"Game {index}.iso",
                SettingKey = "pcsx2_slot1_memory",
                DesiredState = DesiredSettingState.Set,
                DesiredValue = "per-game",
                Reason = "So its saves can be told apart.",
                QueuedAtUtc = DateTimeOffset.UtcNow,
            });
        }

        var status = new StatusViewModel(session, NoPad);

        Assert.True(
            status.LineCount > ListWindow.StatusCapacity,
            "this fixture no longer produces a status long enough to need scrolling");

        // Never more than fits, whatever it holds. Before stage 7b-3 this screen drew every line
        // it had and everything past the display was drawn off it with nothing able to scroll.
        Assert.Equal(ListWindow.StatusCapacity, status.Window.Count);
        Assert.Equal(0, status.Window.Above);
        Assert.True(status.Window.Below > 0);

        // A pane scrolls by an offset and has no cursor at all, so every press moves the view.
        status.Handle(NavAction.Down);
        Assert.Equal(1, status.Window.Above);
        Assert.Equal(ListWindow.StatusCapacity, status.Window.Count);

        // And clamps at both ends rather than scrolling past into a blank pane.
        for (var press = 0; press < status.LineCount + 5; press++)
        {
            status.Handle(NavAction.Down);
        }

        Assert.Equal(0, status.Window.Below);
        Assert.Equal(ListWindow.StatusCapacity, status.Window.Count);

        for (var press = 0; press < status.LineCount + 5; press++)
        {
            status.Handle(NavAction.Up);
        }

        Assert.Equal(0, status.Window.Above);
    }

    [Fact]
    public void A_window_of_status_lines_is_never_taller_than_an_ordinary_list()
    {
        // The capacity is a claim about heights, and this is the arithmetic that says so. Same
        // shape as the reading-row check, which exists because eight taller rows overflowed the
        // window and Avalonia drew a scroll bar no gamepad can reach.
        var status = ListWindow.BlockHeight(
            ListWindow.StatusCapacity,
            ListWindow.StatusLineHeight,
            ListWindow.StatusLineSpacing);

        var ordinary = ListWindow.BlockHeight(ListWindow.Capacity, ListWindow.RowHeight);

        Assert.True(
            status <= ordinary,
            $"{ListWindow.StatusCapacity} status lines draw {status}px against an ordinary "
                + $"list's {ordinary}px, which the smallest supported display is known to hold");
    }

    private static void Pair(InstallSession session, DateTimeOffset expiresAt)
    {
        session.Store.Device.EnsureIdentity(DeviceIdentity.ReadOrCreate(session.Install));
        session.Store.Device.SavePairing(
            new PairingResult(
                new Uri("https://romm.invalid"),
                "device-9",
                "Handheld",
                new GrantedScopes(RomMScopes.Requested),
                TokenProtector.Protect("rmm_token", null, expiresAt)),
            DateTimeOffset.UtcNow);
    }
}
