using RomMBat.Core;
using RomMBat.Core.RetroBat;
using RomMBat.Tests.Support;
using RomMBat.UI.Input;
using RomMBat.UI.Screens;
using RomMBat.UI.Shell;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Every screen walked with the gamepad map alone, and no window anywhere.
/// </summary>
/// <remarks>
/// <b>This is the test that makes "no primary flow requires a mouse" checkable.</b> The screens
/// carry no Avalonia types, so a test drives them exactly as the render loop does: hand
/// <see cref="Navigator.Advance"/> a set of held input names and a clock. Nothing here
/// simulates a pointer, because nothing in the UI can be reached with one.
/// </remarks>
public class NavigationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    private static GamepadStatus NoPad =>
        new(GamepadAvailability.NoDevice, null, null, "No controller is connected.");

    private static HashSet<string> Held(params string[] names) => new(names, StringComparer.Ordinal);

    /// <summary>Presses and releases one button, which is two polls.</summary>
    private static bool Press(Navigator navigator, string name, ref DateTimeOffset clock)
    {
        var running = navigator.Advance(Held(name), clock);
        clock = clock.AddMilliseconds(50);
        navigator.Advance(Held(), clock);
        clock = clock.AddMilliseconds(50);
        return running;
    }

    [Fact]
    public void Back_on_the_first_screen_leaves_RomMBat_rather_than_stranding_the_user()
    {
        using var tree = TempRetroBatTree.Create();
        using var session = InstallSession.Open(tree.Root).Session!;

        var navigator = new Navigator(new StatusViewModel(session, NoPad));
        var clock = T0;

        // There is nothing under the first screen. The user came from the EmulationStation
        // menu, so back has to be the way out or there is no way out without a menu item.
        Assert.False(Press(navigator, "b", ref clock));
        Assert.True(navigator.HasExited);
    }

    [Fact]
    public void Accept_on_an_unpaired_install_opens_pairing_and_back_returns_to_status()
    {
        using var tree = TempRetroBatTree.Create();
        using var session = InstallSession.Open(tree.Root).Session!;

        var typed = string.Empty;
        var status = new StatusViewModel(session, NoPad)
        {
            StartPairing = () => new OnScreenKeyboard(
                "Pair with RomM", "Where is your RomM server?", string.Empty, text => { typed = text; return new TypedResult(null); }),
        };

        var navigator = new Navigator(status);
        var clock = T0;

        Assert.Equal(1, navigator.Depth);

        Press(navigator, "a", ref clock);
        Assert.Equal(2, navigator.Depth);
        Assert.IsType<OnScreenKeyboard>(navigator.Current);

        Press(navigator, "b", ref clock);
        Assert.Equal(1, navigator.Depth);
        Assert.IsType<StatusViewModel>(navigator.Current);
        Assert.False(navigator.HasExited);
        Assert.Empty(typed);
    }

    [Fact]
    public void A_server_url_can_be_typed_and_committed_with_nothing_but_a_d_pad_and_two_buttons()
    {
        var committed = (string?)null;
        var keyboard = new OnScreenKeyboard(
            "Pair with RomM", "Where is your RomM server?", string.Empty, text => { committed = text; return new TypedResult(null); });

        var navigator = new Navigator(keyboard);
        var clock = T0;

        // "h" is row 1 ("abcdefghij"), column 7. Reached by pressing down once and right seven
        // times, which is the whole interaction model: move, then accept.
        Type(navigator, ref clock, "http://romm");

        Assert.Equal("http://romm", keyboard.Text);

        Press(navigator, "start", ref clock);

        Assert.Equal("http://romm", committed);
        Assert.True(navigator.HasExited);
    }

    [Fact]
    public void A_refused_address_leaves_back_going_back_rather_than_out_of_RomMBat()
    {
        using var tree = TempRetroBatTree.Create();
        using var session = InstallSession.Open(tree.Root).Session!;

        var status = new StatusViewModel(session, NoPad)
        {
            StartPairing = () => new OnScreenKeyboard(
                "Pair with RomM",
                "Where is your RomM server?",
                "nonsense",
                text => new TypedResult(null, session.ResolveOrigin(text).Problem)),
        };

        var navigator = new Navigator(status);
        var clock = T0;

        Press(navigator, "a", ref clock);
        Assert.Equal(2, navigator.Depth);

        // Submit something Core refuses. The keyboard stays open with the reason.
        Press(navigator, "start", ref clock);
        Assert.Equal(2, navigator.Depth);
        Assert.False(navigator.HasExited);

        // One back returns to status, and must not leave RomMBat.
        Press(navigator, "b", ref clock);

        Assert.False(navigator.HasExited);
        Assert.Equal(1, navigator.Depth);
        Assert.IsType<StatusViewModel>(navigator.Current);
    }

    [Fact]
    public void Delete_is_on_L1_where_EmulationStations_own_keyboard_puts_it()
    {
        var keyboard = new OnScreenKeyboard("t", "p", "abc", _ => new TypedResult(null));
        var navigator = new Navigator(keyboard);
        var clock = T0;

        // ES's own on-screen keyboard binds DELETE to L and SPACE to R, so a RetroBat user
        // already has the habit. Putting the case toggle here instead, which is what this UI
        // did first, means their thumb deletes nothing and changes case instead.
        Press(navigator, "pageup", ref clock);
        Assert.Equal("ab", keyboard.Text);

        // Accept types the selected character rather than deleting, so a mistyped URL cannot be
        // made worse by the button the user reaches for first.
        Press(navigator, "a", ref clock);
        Assert.Equal("ab1", keyboard.Text);

        // R1 is ES's SPACE and stays unbound: a space is never part of a server address.
        Press(navigator, "pagedown", ref clock);
        Assert.Equal("ab1", keyboard.Text);
    }

    [Fact]
    public void Committing_nothing_is_refused_rather_than_handing_the_caller_an_empty_string()
    {
        var committed = (string?)null;
        var keyboard = new OnScreenKeyboard("t", "p", string.Empty, text => { committed = text; return new TypedResult(null); });
        var navigator = new Navigator(keyboard);
        var clock = T0;

        Press(navigator, "start", ref clock);

        Assert.Null(committed);
        Assert.False(navigator.HasExited);
    }

    [Fact]
    public void The_cursor_wraps_in_both_directions_on_every_row()
    {
        var keyboard = new OnScreenKeyboard("t", "p", string.Empty, _ => new TypedResult(null));
        var navigator = new Navigator(keyboard);
        var clock = T0;

        Assert.Equal("1", keyboard.Selected);

        Press(navigator, "left", ref clock);
        Assert.Equal("0", keyboard.Selected);

        Press(navigator, "right", ref clock);
        Assert.Equal("1", keyboard.Selected);

        Press(navigator, "up", ref clock);
        Assert.Equal(keyboard.Keys.Count - 1, keyboard.CursorRow);
    }

    [Fact]
    public void Shift_changes_the_key_under_the_cursor_without_moving_it()
    {
        var keyboard = new OnScreenKeyboard("t", "p", string.Empty, _ => new TypedResult(null));
        var navigator = new Navigator(keyboard);
        var clock = T0;

        // Down one row and along to "q", the key that proves this is QWERTY and not alphabetical.
        Press(navigator, "down", ref clock);
        Assert.Equal("q", keyboard.Selected);

        // The EmulationStation name, which is the button an Xbox-layout pad prints X on, and
        // where ES's own keyboard puts SHIFT. The footer follows the printed label rather than
        // the file's name for it.
        Press(navigator, "y", ref clock);

        Assert.True(keyboard.IsShifted);
        Assert.Equal("Q", keyboard.Selected);
        Assert.Equal(1, keyboard.CursorRow);
        Assert.Equal(0, keyboard.CursorColumn);

        // A toggle, so the same button comes back. It must not repeat while held: a shift that
        // flickers under a thumb is worse than one that needs a second press.
        Press(navigator, "y", ref clock);
        Assert.False(keyboard.IsShifted);
        Assert.Equal("q", keyboard.Selected);
    }

    [Fact]
    public void The_two_layers_are_the_same_shape_so_the_cursor_can_never_be_stranded()
    {
        var lower = OnScreenKeyboard.Layers[0];
        var upper = OnScreenKeyboard.Layers[1];

        // Toggling case must never move the cursor or index out of range, which is exactly what
        // layers of different shapes would do on the row or column the shorter one lacks.
        Assert.Equal(lower.Count, upper.Count);

        for (var row = 0; row < lower.Count; row++)
        {
            Assert.Equal(lower[row].Length, upper[row].Length);
        }

        Assert.All(lower, row => Assert.Equal(lower[0].Length, row.Length));
    }

    [Fact]
    public void Both_cases_and_the_characters_a_url_needs_are_all_reachable()
    {
        var everything = OnScreenKeyboard.Layers
            .SelectMany(layer => layer.SelectMany(row => row))
            .ToHashSet();

        // Letters in both cases, digits, and the punctuation an address is made of.
        Assert.All("abcdefghijklmnopqrstuvwxyz", c => Assert.Contains(c, everything));
        Assert.All("ABCDEFGHIJKLMNOPQRSTUVWXYZ", c => Assert.Contains(c, everything));
        Assert.All("0123456789", c => Assert.Contains(c, everything));
        Assert.All(":/.-_~?=@%", c => Assert.Contains(c, everything));
    }

    [Fact]
    public void A_url_needs_no_case_toggle_for_its_punctuation()
    {
        var unshifted = OnScreenKeyboard.Layers[0].SelectMany(row => row).ToHashSet();

        // http:// would otherwise need the toggle twice in four characters, which is why these
        // two deviate from a real keyboard's shift pairs.
        Assert.Contains(':', unshifted);
        Assert.Contains('/', unshifted);
    }

    /// <summary>Types a string by walking the grid, which is what a person does.</summary>
    private static void Type(Navigator navigator, ref DateTimeOffset clock, string text)
    {
        var keyboard = (OnScreenKeyboard)navigator.Current;

        foreach (var character in text)
        {
            var target = character.ToString();

            // Bounded: the grid is finite, so a character that is not on it fails loudly here
            // rather than looping.
            var guard = 0;
            while (!string.Equals(keyboard.Selected, target, StringComparison.Ordinal))
            {
                Press(navigator, "right", ref clock);

                if (keyboard.CursorColumn == 0)
                {
                    Press(navigator, "down", ref clock);
                }

                Assert.True(++guard < 500, $"'{target}' is not reachable on the keyboard grid.");
            }

            Press(navigator, "a", ref clock);
        }
    }

    [Fact]
    public void No_screen_promises_a_button_that_nothing_is_bound_to()
    {
        // A hint carries the action rather than a button name, so a screen cannot write "X" and
        // mean the button printed Y. What it can still do is promise an action the controller
        // map never produces, which a user discovers only by pressing and getting nothing.
        using var tree = TempRetroBatTree.Create();
        using var session = InstallSession.Open(tree.Root).Session!;

        var screens = new IScreen[]
        {
            new StatusViewModel(session, NoPad),
            new OnScreenKeyboard("t", "p", "abc", _ => new TypedResult(null)),
            new MessageScreen("title", "body"),
        };

        foreach (var screen in screens)
        {
            Assert.NotEmpty(screen.Hints);
            Assert.All(
                screen.Hints,
                hint => Assert.Contains(hint.Action, NavRepeat.Bound));
        }
    }
}
