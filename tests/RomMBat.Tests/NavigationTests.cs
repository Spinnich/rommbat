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
                "Pair with RomM", "Where is your RomM server?", string.Empty, text => typed = text),
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
            "Pair with RomM", "Where is your RomM server?", string.Empty, text => committed = text);

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
    public void Backspace_is_not_on_the_button_that_confirms()
    {
        var keyboard = new OnScreenKeyboard("t", "p", "abc", _ => { });
        var navigator = new Navigator(keyboard);
        var clock = T0;

        Press(navigator, "x", ref clock);
        Assert.Equal("ab", keyboard.Text);

        // Accept types the selected character rather than deleting, so a mistyped URL cannot be
        // made worse by the button the user reaches for first.
        Press(navigator, "a", ref clock);
        Assert.Equal("ab1", keyboard.Text);
    }

    [Fact]
    public void Committing_nothing_is_refused_rather_than_handing_the_caller_an_empty_string()
    {
        var committed = (string?)null;
        var keyboard = new OnScreenKeyboard("t", "p", string.Empty, text => committed = text);
        var navigator = new Navigator(keyboard);
        var clock = T0;

        Press(navigator, "start", ref clock);

        Assert.Null(committed);
        Assert.False(navigator.HasExited);
    }

    [Fact]
    public void The_cursor_wraps_in_both_directions_on_every_row()
    {
        var keyboard = new OnScreenKeyboard("t", "p", string.Empty, _ => { });
        var navigator = new Navigator(keyboard);
        var clock = T0;

        Assert.Equal("1", keyboard.Selected);

        Press(navigator, "left", ref clock);
        Assert.Equal("0", keyboard.Selected);

        Press(navigator, "right", ref clock);
        Assert.Equal("1", keyboard.Selected);

        Press(navigator, "up", ref clock);
        Assert.Equal(OnScreenKeyboard.Grid.Count - 1, keyboard.CursorRow);
    }

    [Fact]
    public void Every_key_on_the_grid_is_reachable_and_every_row_is_the_same_width()
    {
        // The vertical move keeps the column, so a row of a different width would either strand
        // keys or index out of range. Both are silent until someone tries that key.
        Assert.All(OnScreenKeyboard.Grid, row => Assert.Equal(OnScreenKeyboard.Grid[0].Length, row.Length));

        var keyboard = new OnScreenKeyboard("t", "p", string.Empty, _ => { });
        var navigator = new Navigator(keyboard);
        var clock = T0;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var _ in OnScreenKeyboard.Grid)
        {
            foreach (var __ in OnScreenKeyboard.Grid[0])
            {
                seen.Add(keyboard.Selected);
                Press(navigator, "right", ref clock);
            }

            Press(navigator, "down", ref clock);
        }

        var everyCharacter = OnScreenKeyboard.Grid.SelectMany(row => row).Select(c => c.ToString()).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(everyCharacter, seen);
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
}
