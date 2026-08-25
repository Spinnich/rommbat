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
        nav.Forget();
        Assert.Equal([NavAction.Down], nav.Advance(Held("joystick1down"), T0));
        nav.Forget();
        Assert.Equal([NavAction.Left], nav.Advance(Held("joystick1left"), T0));
        nav.Forget();
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
    public void Forgetting_makes_a_still_held_button_a_fresh_press_for_the_next_screen()
    {
        var nav = new NavRepeat();

        Assert.Equal([NavAction.Accept], nav.Advance(Held("a"), T0));

        // A screen just opened under a finger that never came off the button. Without this the
        // new screen never sees the press that opened it and the user presses twice.
        nav.Forget();

        Assert.Equal([NavAction.Accept], nav.Advance(Held("a"), T0.AddMilliseconds(10)));
    }

    [Fact]
    public void Two_directions_at_once_both_fire_which_is_what_a_diagonal_is()
    {
        var nav = new NavRepeat();

        var fired = nav.Advance(Held("up", "right"), T0);

        Assert.Contains(NavAction.Up, fired);
        Assert.Contains(NavAction.Right, fired);
    }
}
