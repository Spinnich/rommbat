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

        // The grid is QWERTY, not alphabetical: row 1 is "qwertyuiop". Type walks the cursor to
        // each character and accepts it, which is the whole interaction model.
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
    public void The_shoulders_do_what_EmulationStations_own_keyboard_does_with_them()
    {
        var keyboard = new OnScreenKeyboard("t", "p", "abc", _ => new TypedResult(null));
        var navigator = new Navigator(keyboard);
        var clock = T0;

        // GuiTextEditPopupKeyboard binds pageup to DELETE and pagedown to SPACE, so a RetroBat
        // user already has the habit in their thumbs. Putting the case toggle there instead,
        // which is what this UI did first, means both of them do the wrong thing.
        Press(navigator, "pageup", ref clock);
        Assert.Equal("ab", keyboard.Text);

        Press(navigator, "pagedown", ref clock);
        Assert.Equal("ab ", keyboard.Text);

        // Accept types the selected key rather than deleting, so a mistyped address cannot be
        // made worse by the button the user reaches for first.
        Press(navigator, "a", ref clock);
        Assert.Equal("ab 1", keyboard.Text);
    }

    [Fact]
    public void Reset_puts_back_the_text_the_screen_opened_with()
    {
        var keyboard = new OnScreenKeyboard("t", "p", "https://", _ => new TypedResult(null));
        var navigator = new Navigator(keyboard);
        var clock = T0;

        Press(navigator, "a", ref clock);
        Assert.Equal("https://1", keyboard.Text);

        // The one key that does not do what upstream's does: ES commits the empty string and
        // closes, which is how a setting is cleared there, and neither field RomMBat asks for
        // may be empty. Same key, same word, the useful reading of it here.
        Press(navigator, "x", ref clock);

        Assert.Equal("https://", keyboard.Text);
        Assert.False(navigator.HasExited);
        Assert.Equal(1, navigator.Depth);
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
    public void The_cursor_wraps_in_both_directions_and_crosses_a_key_rather_than_a_cell()
    {
        var keyboard = new OnScreenKeyboard("t", "p", string.Empty, _ => new TypedResult(null));
        var navigator = new Navigator(keyboard);
        var clock = T0;

        Assert.Equal("1", keyboard.SelectedFace);

        // Left from the first cell wraps to the last, which on the digit row is delete.
        Press(navigator, "left", ref clock);
        Assert.Equal(KeyKind.Backspace, keyboard.Selected.Kind);

        Press(navigator, "right", ref clock);
        Assert.Equal("1", keyboard.SelectedFace);

        // The space bar is seven cells wide and takes one press to cross, not seven. Down four
        // rows from the digit row lands on it, and one more right reaches reset.
        for (var i = 0; i < 4; i++)
        {
            Press(navigator, "down", ref clock);
        }

        Assert.Equal(KeyKind.Shift, keyboard.Selected.Kind);

        Press(navigator, "right", ref clock);
        Assert.Equal(KeyKind.Space, keyboard.Selected.Kind);

        Press(navigator, "right", ref clock);
        Assert.Equal(KeyKind.Reset, keyboard.Selected.Kind);
    }

    [Fact]
    public void The_accept_key_is_two_rows_tall_and_is_not_a_hole_the_cursor_falls_into()
    {
        var keyboard = new OnScreenKeyboard("t", "p", string.Empty, _ => new TypedResult(null));
        var navigator = new Navigator(keyboard);
        var clock = T0;

        // Onto delete, then down into the accept key, which occupies the two letter rows. A
        // cursor that stepped by cell would need two presses to leave it and would look stuck.
        Press(navigator, "left", ref clock);
        Press(navigator, "down", ref clock);
        Assert.Equal(KeyKind.Accept, keyboard.Selected.Kind);

        Press(navigator, "down", ref clock);
        Assert.Equal(KeyKind.Layer, keyboard.Selected.Kind);

        Press(navigator, "up", ref clock);
        Assert.Equal(KeyKind.Accept, keyboard.Selected.Kind);
    }

    [Fact]
    public void Shift_changes_the_key_under_the_cursor_without_moving_it()
    {
        var keyboard = new OnScreenKeyboard("t", "p", string.Empty, _ => new TypedResult(null));
        var navigator = new Navigator(keyboard);
        var clock = T0;

        // Down one row and along to "q", the key that proves this is QWERTY and not alphabetical.
        Press(navigator, "down", ref clock);
        Assert.Equal("q", keyboard.SelectedFace);

        // ES's own keyboard puts shift on the button es_input.cfg calls "y", which is the one
        // an Xbox-layout pad prints X on. The footer draws a position rather than a letter.
        Press(navigator, "y", ref clock);

        Assert.True(keyboard.IsShifted);
        Assert.Equal("Q", keyboard.SelectedFace);
        Assert.Equal(1, keyboard.CursorRow);
        Assert.Equal(0, keyboard.CursorColumn);

        // A toggle, so the same button comes back. It must not repeat while held: a shift that
        // flickers under a thumb is worse than one that needs a second press.
        Press(navigator, "y", ref clock);
        Assert.False(keyboard.IsShifted);
        Assert.Equal("q", keyboard.SelectedFace);
    }

    [Fact]
    public void The_layer_key_drops_shift_so_the_accented_layer_is_entered_the_same_way_every_time()
    {
        var keyboard = new OnScreenKeyboard("t", "p", string.Empty, _ => new TypedResult(null));
        var navigator = new Navigator(keyboard);
        var clock = T0;

        Press(navigator, "y", ref clock);
        Assert.True(keyboard.IsShifted);

        // Onto the layer key, which upstream's altKeys() clears shift on the way through.
        Press(navigator, "down", ref clock);
        Press(navigator, "down", ref clock);
        Press(navigator, "down", ref clock);
        Press(navigator, "left", ref clock);
        Assert.Equal(KeyKind.Layer, keyboard.Selected.Kind);

        Press(navigator, "a", ref clock);

        Assert.True(keyboard.IsAlted);
        Assert.False(keyboard.IsShifted);
    }

    [Fact]
    public void A_key_with_nothing_on_this_layer_types_nothing_rather_than_vanishing()
    {
        var keyboard = new OnScreenKeyboard("t", "p", string.Empty, _ => new TypedResult(null));

        // Upstream draws the special layer's blank half and ignores presses on it rather than
        // hiding the keys, which is what keeps every layer the same shape and the cursor
        // impossible to strand. Row 3, column 1 is the first of them on the US grid.
        keyboard.Handle(NavAction.Down);
        keyboard.Handle(NavAction.Down);
        keyboard.Handle(NavAction.Down);
        keyboard.Handle(NavAction.Left);
        keyboard.Handle(NavAction.Accept);

        Assert.True(keyboard.IsAlted);

        while (keyboard.SelectedFace != "€")
        {
            keyboard.Handle(NavAction.Right);
        }

        keyboard.Handle(NavAction.Right);

        Assert.Equal(string.Empty, keyboard.SelectedFace);
        Assert.Equal(KeyKind.Character, keyboard.Selected.Kind);

        keyboard.Handle(NavAction.Accept);
        Assert.Equal(string.Empty, keyboard.Text);
    }

    [Fact]
    public void The_cancel_and_accept_keys_do_what_their_buttons_do()
    {
        var committed = (string?)null;

        TypedResult Take(string text)
        {
            committed = text;
            return new TypedResult(null);
        }

        var cancelling = new OnScreenKeyboard("t", "p", "abc", Take);
        var cancel = new Navigator(cancelling);
        var clock = T0;

        // Bottom row, last key.
        Press(cancel, "up", ref clock);
        while (cancelling.Selected.Kind != KeyKind.Cancel)
        {
            Press(cancel, "right", ref clock);
        }

        Press(cancel, "a", ref clock);
        Assert.Null(committed);
        Assert.True(cancel.HasExited);

        var accepting = new OnScreenKeyboard("t", "p", "abc", Take);
        var accept = new Navigator(accepting);

        Press(accept, "left", ref clock);
        Press(accept, "down", ref clock);
        Assert.Equal(KeyKind.Accept, accepting.Selected.Kind);

        Press(accept, "a", ref clock);
        Assert.Equal("abc", committed);
    }

    [Fact]
    public void Every_layout_is_transcribed_at_the_shape_upstream_holds_it_in()
    {
        // The tables are a copy of a compiled-in C++ array, so the failure mode is a slipped
        // cell rather than a wrong idea. Four faces per key, thirteen columns, five rows.
        foreach (var layout in Enum.GetValues<KeyboardLayout>())
        {
            var table = KeyboardLayouts.Table(layout);

            Assert.Equal(OnScreenKeyboard.Rows * KeyboardLayouts.Faces, table.Length);
            Assert.All(table, row => Assert.Equal(KeyboardLayouts.Columns, row.Length));
        }
    }

    [Fact]
    public void Every_cell_of_every_layout_belongs_to_exactly_one_key()
    {
        // A span that overlaps its neighbour or stops short leaves the cursor somewhere it can
        // sit and do nothing, which is the one way a transcription slip could pass silently.
        foreach (var layout in Enum.GetValues<KeyboardLayout>())
        {
            var covered = new int[OnScreenKeyboard.Rows, KeyboardLayouts.Columns];

            foreach (var key in Keys(layout))
            {
                Assert.InRange(key.Row + key.Height, 1, OnScreenKeyboard.Rows);
                Assert.InRange(key.Column + key.Width, 1, KeyboardLayouts.Columns);

                for (var row = key.Row; row < key.Row + key.Height; row++)
                {
                    for (var column = key.Column; column < key.Column + key.Width; column++)
                    {
                        covered[row, column]++;
                    }
                }
            }

            for (var row = 0; row < OnScreenKeyboard.Rows; row++)
            {
                for (var column = 0; column < KeyboardLayouts.Columns; column++)
                {
                    Assert.True(
                        covered[row, column] == 1,
                        $"{layout} cell {row},{column} belongs to {covered[row, column]} keys");
                }
            }
        }
    }

    [Theory]
    [InlineData(null, KeyboardLayout.UnitedStates)]
    [InlineData("", KeyboardLayout.UnitedStates)]
    [InlineData("en_US", KeyboardLayout.UnitedStates)]
    [InlineData("de_DE", KeyboardLayout.UnitedStates)]
    [InlineData("fr_FR", KeyboardLayout.French)]
    [InlineData("fr", KeyboardLayout.French)]
    [InlineData("FR", KeyboardLayout.French)]
    [InlineData("ko_KR", KeyboardLayout.Korean)]
    public void The_layout_follows_the_language_EmulationStation_is_running_in(
        string? language,
        KeyboardLayout expected)
    {
        // Upstream reads es_settings.cfg's Language on a Windows release build, lowercases the
        // part before the underscore, and knows three keyboards. The bare "FR" case is the one
        // deviation: upstream only lowercases when a region is present, so it would miss.
        Assert.Equal(expected, KeyboardLayouts.For(language));
        Assert.Equal(expected, new OnScreenKeyboard("t", "p", "", _ => new TypedResult(null), language).Layout);
    }

    [Fact]
    public void Both_cases_and_the_characters_a_url_needs_are_all_reachable()
    {
        var everything = KeyboardLayouts.Table(KeyboardLayout.UnitedStates)
            .SelectMany(row => row)
            .ToHashSet(StringComparer.Ordinal);

        // Letters in both cases, digits, and the punctuation an address is made of.
        Assert.All("abcdefghijklmnopqrstuvwxyz", c => Assert.Contains(c.ToString(), everything));
        Assert.All("ABCDEFGHIJKLMNOPQRSTUVWXYZ", c => Assert.Contains(c.ToString(), everything));
        Assert.All("0123456789", c => Assert.Contains(c.ToString(), everything));
        Assert.All(":/.-_~?=@%", c => Assert.Contains(c.ToString(), everything));
    }

    [Fact]
    public void A_url_costs_one_shift_press_now_and_that_is_upstreams_layout_not_a_slip()
    {
        // The layout this replaced put : and / unshifted so http:// needed no toggle. ES puts
        // them on the upper face, and a grid a RetroBat user already knows beats two presses
        // saved on a string that is typed once and then remembered.
        var unshifted = Keys(KeyboardLayout.UnitedStates)
            .Select(key => key.Lower)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain(":", unshifted);
        Assert.DoesNotContain("/", unshifted);

        var shifted = Keys(KeyboardLayout.UnitedStates)
            .Select(key => key.Upper)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(":", shifted);
        Assert.Contains("/", shifted);
    }

    /// <summary>One layout's keys, which is what the screen builds from the table.</summary>
    private static IReadOnlyList<KeyboardKey> Keys(KeyboardLayout layout) =>
        new OnScreenKeyboard(
            "t",
            "p",
            string.Empty,
            _ => new TypedResult(null),
            layout switch
            {
                KeyboardLayout.French => "fr_FR",
                KeyboardLayout.Korean => "ko_KR",
                _ => "en_US",
            }).Keys;

    /// <summary>Types a string by walking the grid, which is what a person does.</summary>
    /// <remarks>
    /// Two layers, because a URL's punctuation lives on the upper face now: it walks what is on
    /// screen, presses shift if that did not find the character, and walks again.
    /// </remarks>
    private static void Type(Navigator navigator, ref DateTimeOffset clock, string text)
    {
        var keyboard = (OnScreenKeyboard)navigator.Current;

        foreach (var character in text)
        {
            var target = character.ToString();

            for (var layer = 0; layer < 2 && keyboard.SelectedFace != target; layer++)
            {
                Walk(navigator, ref clock, target);

                if (keyboard.SelectedFace != target)
                {
                    Press(navigator, "y", ref clock);
                }
            }

            Assert.Equal(target, keyboard.SelectedFace);
            Press(navigator, "a", ref clock);
        }
    }

    /// <summary>Sweeps the layer on screen for one face. Bounded: the grid is finite.</summary>
    private static void Walk(Navigator navigator, ref DateTimeOffset clock, string target)
    {
        var keyboard = (OnScreenKeyboard)navigator.Current;

        for (var step = 0; step < OnScreenKeyboard.Rows * OnScreenKeyboard.Columns; step++)
        {
            if (keyboard.SelectedFace == target)
            {
                return;
            }

            Press(navigator, "right", ref clock);

            if (keyboard.CursorColumn == 0)
            {
                Press(navigator, "down", ref clock);
            }
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
