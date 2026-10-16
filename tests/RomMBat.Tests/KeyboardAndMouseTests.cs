using Avalonia.Input;
using RomMBat.Core.RetroBat;
using RomMBat.UI.Input;
using RomMBat.UI.Screens;
using RomMBat.UI.Shell;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The keyboard and the pointer reach the screens as the pad does: by the same names and the
/// same actions, with no window.
/// </summary>
public class KeyboardAndMouseTests
{
    private static string Fixture => Path.Combine(AppContext.BaseDirectory, "fixtures", "es_input.cfg");

    private static KeyboardBindings Keys => KeyboardBindings.From(EsInputMap.Load(Fixture));

    [Theory]
    [InlineData(Key.X, "a")]
    [InlineData(Key.Z, "b")]
    [InlineData(Key.S, "y")]
    [InlineData(Key.Q, "x")]
    [InlineData(Key.Enter, "start")]
    [InlineData(Key.Back, "select")]
    [InlineData(Key.Home, "l2")]
    [InlineData(Key.End, "r2")]
    [InlineData(Key.PageUp, "pageup")]
    [InlineData(Key.PageDown, "pagedown")]
    [InlineData(Key.Up, "up")]
    public void A_key_means_what_the_install_s_keyboard_entry_says(Key key, string name) =>
        Assert.Contains(name, Keys.Names(key));

    [Fact]
    public void Enter_is_start_as_in_EmulationStation_and_not_accept()
    {
        Assert.DoesNotContain("a", Keys.Names(Key.Enter));
        Assert.Contains("b", Keys.Names(Key.Escape));
    }

    [Fact]
    public void A_missing_keyboard_entry_falls_back_to_the_keys_ES_ships()
    {
        var keys = KeyboardBindings.From(null);

        Assert.Contains("a", keys.Names(Key.X));
        Assert.Contains("up", keys.Names(Key.Up));
    }

    [Fact]
    public void The_file_wins_over_an_alias()
    {
        // Enter is bound in the fixture, so the Accept alias does not also fire on it.
        Assert.Equal(["start"], Keys.Names(Key.Enter));
    }

    [Fact]
    public void An_unbound_key_means_nothing()
    {
        Assert.Empty(Keys.Names(Key.W));
    }

    [Fact]
    public void A_held_key_repeats_like_a_pad_direction_and_a_face_button_does_not()
    {
        var repeat = new NavRepeat();
        var start = DateTimeOffset.UtcNow;

        var down = Keys.Held([Key.Down, Key.X]);

        Assert.Equal([NavAction.Down, NavAction.Accept], repeat.Advance(down, start));
        Assert.Equal([NavAction.Down], repeat.Advance(down, start + TimeSpan.FromMilliseconds(500)));
    }

    [Fact]
    public void A_key_the_table_cannot_name_is_not_labeled_as_a_letter()
    {
        Assert.Equal("Insert", SdlKey.Label(SdlKey.From(Key.Insert)!.Value));
        Assert.Equal("Key 1073741922", SdlKey.Label(1073741922));
        Assert.Equal("X", SdlKey.Label('x'));
    }

    [Fact]
    public void The_footer_names_the_key_for_an_action()
    {
        Assert.Equal("X", Keys.KeyFor(NavAction.Accept));
        Assert.Equal("Enter", Keys.KeyFor(NavAction.Start));
    }

    private static ListScreen Rows(Func<int, ScreenCommand>? choose = null) => new(
        "List",
        [new ListRow("one"), new ListRow("two", Available: false), new ListRow("three")],
        choose ?? (_ => ScreenCommand.Stay));

    [Fact]
    public void Pointing_moves_the_cursor_and_chooses_nothing()
    {
        var chosen = -1;
        var navigator = new Navigator(Rows(index =>
        {
            chosen = index;
            return ScreenCommand.Stay;
        }));

        Assert.True(navigator.Point(2));

        Assert.Equal(2, ((ListScreen)navigator.Current).Cursor);
        Assert.Equal(-1, chosen);
    }

    [Fact]
    public void Pointing_at_an_unavailable_row_leaves_the_cursor_where_it_was()
    {
        var navigator = new Navigator(Rows());

        navigator.Point(1);

        Assert.Equal(0, ((ListScreen)navigator.Current).Cursor);
    }

    [Fact]
    public void A_click_selects_and_chooses_the_row()
    {
        var chosen = -1;
        var navigator = new Navigator(Rows(index =>
        {
            chosen = index;
            return ScreenCommand.Stay;
        }));

        navigator.Click(2);

        Assert.Equal(2, chosen);
    }

    [Fact]
    public void A_click_on_an_unavailable_row_does_nothing()
    {
        var chosen = -1;
        var navigator = new Navigator(Rows(index =>
        {
            chosen = index;
            return ScreenCommand.Stay;
        }));

        navigator.Click(1);

        Assert.Equal(-1, chosen);
    }

    [Fact]
    public void The_wheel_scrolls_a_notch_at_a_time()
    {
        var navigator = new Navigator(Rows());

        navigator.Wheel(-1);
        Assert.Equal(2, ((ListScreen)navigator.Current).Cursor);

        navigator.Wheel(1);
        Assert.Equal(0, ((ListScreen)navigator.Current).Cursor);
    }

    [Fact]
    public void A_click_on_a_confirmation_answer_gives_that_answer()
    {
        var yes = false;
        var confirm = ConfirmScreen.YesNo("Delete?", "Delete", () =>
        {
            yes = true;
            return ScreenCommand.Pop;
        }, "Keep");

        new Navigator(confirm).Click(0);

        Assert.True(yes);
    }

    [Fact]
    public void A_confirmation_selects_the_safe_answer_first_and_back_gives_it()
    {
        var yes = false;
        var confirm = ConfirmScreen.YesNo("Delete?", "Delete", () =>
        {
            yes = true;
            return ScreenCommand.Pop;
        }, "Keep");

        Assert.Equal(1, confirm.Selected);
        Assert.Equal(ScreenCommandKind.Pop, confirm.Handle(NavAction.Back).Kind);
        Assert.False(yes);
    }

    [Fact]
    public void A_physical_keyboard_types_into_the_text_screen()
    {
        string? typed = null;
        var navigator = new Navigator(new OnScreenKeyboard("Search", "Type.", string.Empty, text =>
        {
            typed = text;
            return new TypedResult(null);
        }));

        navigator.Type("ab");
        navigator.Type("c");
        ((OnScreenKeyboard)navigator.Current).DeleteLast();
        navigator.Handle(NavAction.Start);

        Assert.Equal("ab", typed);
    }

    [Fact]
    public void The_wheel_leaves_the_on_screen_keyboard_cursor_alone()
    {
        var keyboard = new OnScreenKeyboard("Search", "Type.", string.Empty, _ => new TypedResult(null));
        var navigator = new Navigator(keyboard);
        var row = keyboard.CursorRow;

        navigator.Wheel(1);

        Assert.Equal(row, keyboard.CursorRow);
    }

    [Fact]
    public void A_click_on_an_on_screen_key_presses_it()
    {
        var keyboard = new OnScreenKeyboard("Search", "Type.", string.Empty, _ => new TypedResult(null));
        var navigator = new Navigator(keyboard);
        var index = keyboard.Keys.ToList().FindIndex(key => key.Kind == KeyKind.Character && key.Lower.Length == 1);

        navigator.Click(index);

        Assert.Equal(keyboard.Keys[index].Lower, keyboard.Text);
    }

    [Fact]
    public void A_screen_that_is_not_pointable_ignores_the_pointer()
    {
        var navigator = new Navigator(new MessageScreen("Message", "Hello"));

        Assert.True(navigator.Point(0));
        Assert.True(navigator.Click(0));
        Assert.Equal(1, navigator.Depth);
    }
}
