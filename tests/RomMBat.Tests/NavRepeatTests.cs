using RomMBat.UI.Input;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Held inputs into intentions, including auto-repeat.
/// </summary>
/// <remarks>
/// <b>These are what make "navigable by gamepad" an assertion rather than a claim.</b>
/// <see cref="NavRepeat"/> takes the clock as an argument and holds no timer, so a screen can
/// be driven end to end in a test with no window, no controller and no real time passing.
/// </remarks>
public class NavRepeatTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    private static HashSet<string> Held(params string[] names) => new(names, StringComparer.Ordinal);

    [Fact]
    public void A_press_fires_once_and_holding_it_does_not_fire_again_immediately()
    {
        var nav = new NavRepeat();

        Assert.Equal([NavAction.Down], nav.Advance(Held("down"), T0));

        // Still held, well inside the repeat delay.
        Assert.Empty(nav.Advance(Held("down"), T0.AddMilliseconds(100)));
        Assert.Empty(nav.Advance(Held("down"), T0.AddMilliseconds(399)));
    }

    [Fact]
    public void A_held_direction_repeats_after_the_delay_and_then_at_the_interval()
    {
        var nav = new NavRepeat();

        nav.Advance(Held("down"), T0);

        Assert.Equal([NavAction.Down], nav.Advance(Held("down"), T0.AddMilliseconds(400)));
        Assert.Empty(nav.Advance(Held("down"), T0.AddMilliseconds(450)));
        Assert.Equal([NavAction.Down], nav.Advance(Held("down"), T0.AddMilliseconds(490)));
    }

    [Fact]
    public void Accept_and_back_never_repeat_however_long_they_are_held()
    {
        var nav = new NavRepeat();

        Assert.Equal([NavAction.Accept], nav.Advance(Held("a"), T0));

        // Holding A must not activate the same thing twice, and holding B must not walk back
        // through two screens. A full minute of holding is still one press.
        foreach (var ms in (int[])[400, 500, 1000, 60_000])
        {
            Assert.Empty(nav.Advance(Held("a"), T0.AddMilliseconds(ms)));
        }
    }

    [Fact]
    public void Releasing_and_pressing_again_is_a_second_press()
    {
        var nav = new NavRepeat();

        Assert.Equal([NavAction.Accept], nav.Advance(Held("a"), T0));
        Assert.Empty(nav.Advance(Held(), T0.AddMilliseconds(50)));
        Assert.Equal([NavAction.Accept], nav.Advance(Held("a"), T0.AddMilliseconds(100)));
    }

    [Fact]
    public void The_stick_reaches_every_direction_the_d_pad_does()
    {
        var nav = new NavRepeat();

        // joystick1down and joystick1right are synthesised by GamepadReader, because
        // es_input.cfg records only one direction per axis. A stick that could move a menu up
        // and never down would read as a broken pad.
        Assert.Equal([NavAction.Up], nav.Advance(Held("joystick1up"), T0));
        nav.CarryNothingOver();
        Assert.Equal([NavAction.Down], nav.Advance(Held("joystick1down"), T0));
        nav.CarryNothingOver();
        Assert.Equal([NavAction.Left], nav.Advance(Held("joystick1left"), T0));
        nav.CarryNothingOver();
        Assert.Equal([NavAction.Right], nav.Advance(Held("joystick1right"), T0));
    }

    [Fact]
    public void One_button_bound_to_two_names_does_not_fire_two_different_actions()
    {
        var nav = new NavRepeat();

        // Measured: select and hotkey are the same physical button on both the 8BitDo and the
        // Xbox pad, so the reader reports both names for one press. Neither is bound to a
        // navigation action, so nothing happens, which is the point.
        Assert.Empty(nav.Advance(Held("select", "hotkey"), T0));
    }

    [Fact]
    public void A_button_still_held_across_a_screen_change_does_not_act_twice()
    {
        var nav = new NavRepeat();

        Assert.Equal([NavAction.Back], nav.Advance(Held("b"), T0));

        // The press has navigated. Holding it a moment longer must not act again on the screen
        // that just appeared: found by using it, where backing out while still holding B popped
        // and then immediately fired Back on the root, closing RomMBat.
        nav.CarryNothingOver();

        Assert.Empty(nav.Advance(Held("b"), T0.AddMilliseconds(10)));
        Assert.Empty(nav.Advance(Held("b"), T0.AddMilliseconds(900)));

        // Released, so the user's next press is theirs again.
        Assert.Empty(nav.Advance(Held(), T0.AddSeconds(2)));
        Assert.Equal([NavAction.Back], nav.Advance(Held("b"), T0.AddSeconds(3)));
    }

    [Fact]
    public void A_button_already_held_when_the_app_starts_is_ignored_until_it_is_released()
    {
        var nav = new NavRepeat();

        // RomMBat is opened from the EmulationStation menu by pressing A, and the shell's first
        // poll happens while that button is very often still down. Observed for real: a pad held
        // at launch walked straight back out of the root screen and closed the app before it
        // drew anything.
        nav.SuppressHeld(Held("a"));

        Assert.Empty(nav.Advance(Held("a"), T0));
        Assert.Empty(nav.Advance(Held("a"), T0.AddMilliseconds(500)));
        Assert.Empty(nav.Advance(Held("a"), T0.AddSeconds(5)));

        // Released, so the next press is the user's.
        Assert.Empty(nav.Advance(Held(), T0.AddSeconds(6)));
        Assert.Equal([NavAction.Accept], nav.Advance(Held("a"), T0.AddSeconds(7)));
    }

    [Fact]
    public void A_direction_held_at_launch_does_not_scroll_the_first_screen()
    {
        var nav = new NavRepeat();
        nav.SuppressHeld(Held("down"));

        Assert.Empty(nav.Advance(Held("down"), T0));

        // The dangerous half: without suppression this would start auto-repeating after the
        // delay and run the cursor down a list nobody touched.
        Assert.Empty(nav.Advance(Held("down"), T0.AddMilliseconds(500)));
        Assert.Empty(nav.Advance(Held("down"), T0.AddMilliseconds(900)));
    }

    [Fact]
    public void Changing_screen_does_not_un_suppress_a_button_held_since_launch()
    {
        var nav = new NavRepeat();
        nav.SuppressHeld(Held("a"));

        Assert.Empty(nav.Advance(Held("a"), T0));

        // Carrying nothing over must not resurrect a button the user never pressed at all.
        nav.CarryNothingOver();

        Assert.Empty(nav.Advance(Held("a"), T0.AddMilliseconds(100)));
    }

    [Fact]
    public void Two_directions_at_once_both_fire_which_is_what_a_diagonal_is()
    {
        var nav = new NavRepeat();

        var fired = nav.Advance(Held("up", "right"), T0);

        Assert.Contains(NavAction.Up, fired);
        Assert.Contains(NavAction.Right, fired);
    }

    [Fact]
    public void Delete_repeats_because_it_clears_an_address_and_shift_does_not_because_it_toggles()
    {
        var nav = new NavRepeat();
        var held = Held("pageup", "y");

        Assert.Contains(NavAction.PageUp, nav.Advance(held, T0));

        // Both fired on the press. Only one may fire again while the thumb stays down: a
        // repeating case toggle flickers between layers many times a second.
        var later = T0 + NavRepeat.DefaultDelay + NavRepeat.DefaultInterval;

        var fired = nav.Advance(held, later);

        Assert.Contains(NavAction.PageUp, fired);
        Assert.DoesNotContain(NavAction.Alternate, fired);
    }
}
