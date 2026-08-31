using RomMBat.UI.Input;
using RomMBat.UI.Shell;

namespace RomMBat.UI.Screens;

/// <summary>What the caller made of what was typed.</summary>
/// <param name="Next">Where to go next, or null when the text was no good.</param>
/// <param name="Problem">Why it was no good, shown above the grid so it can be corrected.</param>
public sealed record TypedResult(IScreen? Next, string? Problem = null);

/// <summary>What pressing a key does.</summary>
public enum KeyKind
{
    /// <summary>Appends its own face. Empty on a layer that has nothing there, and then inert.</summary>
    Character,

    /// <summary>Removes the last character. <c>DEL</c> upstream.</summary>
    Backspace,

    /// <summary>Appends a space.</summary>
    Space,

    /// <summary>Upper or lower.</summary>
    Shift,

    /// <summary>The accented layer, and off again. <c>ALT</c> upstream.</summary>
    Layer,

    /// <summary>Back to the text this screen opened with.</summary>
    Reset,

    /// <summary>Commits, exactly as the primary action does.</summary>
    Accept,

    /// <summary>Leaves without committing, exactly as back does.</summary>
    Cancel,
}

/// <summary>
/// One key, placed on the grid rather than merely ordered in a row.
/// </summary>
/// <remarks>
/// Keys span cells, so a row is not a string: the layer key is two cells wide and the accept
/// key two rows tall. <see cref="Row"/> and <see cref="Column"/> are its top-left cell, and the
/// four faces are what it shows on each layer.
/// </remarks>
public sealed record KeyboardKey(
    KeyKind Kind,
    string Lower,
    string Upper,
    string Alted,
    string AltedUpper,
    int Row,
    int Column,
    int Width,
    int Height)
{
    /// <summary>What this key shows, and types, in one layer.</summary>
    public string Face(bool shifted, bool alted) => (alted, shifted) switch
    {
        (true, true) => AltedUpper,
        (true, false) => Alted,
        (false, true) => Upper,
        _ => Lower,
    };
}

/// <summary>
/// Typing on a gamepad, which the plan names as the one genuinely hostile step.
/// </summary>
/// <remarks>
/// <b>This exists because a server URL has to be typed and a d-pad is not a keyboard.</b> The
/// risks table calls it out by name, and the mitigation is this plus remembering the answer so
/// it is typed once.
/// <para>
/// <b>It is EmulationStation's keyboard, and the layout is transcribed rather than designed.</b>
/// Finding 228 established that ES ships one and that RomMBat had quietly disagreed with it;
/// finding 234 read its source and settled what it actually contains. See
/// <see cref="KeyboardLayouts"/> for the tables and their provenance. Everything below follows
/// from them: a thirteen-column grid, four faces per key, delete, accept and the layer key down
/// the right-hand edge, and shift, space, reset and cancel along the bottom.
/// </para>
/// <para>
/// <b>Every one of those four bottom keys is also a button</b>, which is what the footer says
/// and what upstream does. Both routes exist for the same reason: the button is faster once you
/// know it and the key is the only one that can be found without being told.
/// </para>
/// <para>
/// <b>Case costs a press on a URL now, and that is the trade.</b> The layout this replaced put
/// <c>:</c> and <c>/</c> on the unshifted layer so <c>http://</c> needed no toggle. Upstream
/// puts them under shift, and a layout a RetroBat user already has in their fingers beats one
/// that saves two presses on a string typed once and then remembered.
/// </para>
/// <para>
/// <b>Reset is the one key that does not do what upstream's does.</b> ES's commits the empty
/// string and closes, which is how a setting is cleared there. Neither field RomMBat asks for
/// may be empty, so the same key in the same place under the same word puts back the text the
/// screen opened with, which is the useful reading of it here and identical on an empty field.
/// </para>
/// <para>
/// <b>Movement crosses a key rather than a cell</b>, so a wide key is one press to leave and
/// the two-row accept key is not a hole the cursor falls into. It wraps in all four directions:
/// upstream wraps left and right and spends up and down on its text field, which this screen
/// does not have.
/// </para>
/// </remarks>
public sealed class OnScreenKeyboard : IScreen
{
    /// <summary>Cells across, which every row fills exactly.</summary>
    public const int Columns = KeyboardLayouts.Columns;

    /// <summary>Cells down, the last being shift, space, reset and cancel.</summary>
    public const int Rows = 5;

    private static readonly Dictionary<KeyboardLayout, KeyboardKey[]> Built = [];
    private static readonly Dictionary<KeyboardLayout, KeyboardKey?[][]> Cells = [];

    private readonly Func<string, TypedResult> _accepted;
    private readonly string _initial;

    /// <param name="accepted">
    /// Given what was typed, either the next screen or the reason it cannot be used. Validation
    /// lives with the caller, because what makes a string usable is the caller's business: for
    /// a server address that is <see cref="Core.InstallSession.ResolveOrigin"/>, which already
    /// owns the rule and its words.
    /// </param>
    /// <param name="language">
    /// The language EmulationStation is running in, from
    /// <see cref="Core.InstallSession.EmulationStationLanguage"/>. Null gives the same grid ES
    /// itself gives every language but two.
    /// </param>
    public OnScreenKeyboard(
        string title,
        string prompt,
        string initial,
        Func<string, TypedResult> accepted,
        string? language = null)
    {
        ArgumentNullException.ThrowIfNull(accepted);

        Title = title;
        Prompt = prompt;
        _initial = initial ?? string.Empty;
        Text = _initial;
        _accepted = accepted;
        Layout = KeyboardLayouts.For(language);
    }

    public string Title { get; }

    /// <summary>The line above the grid, explaining what is being asked for.</summary>
    public string Prompt { get; }

    /// <summary>Which of EmulationStation's three grids is on screen.</summary>
    public KeyboardLayout Layout { get; }

    /// <summary>What has been typed so far.</summary>
    public string Text { get; private set; }

    /// <summary>Why the last attempt to commit was refused, or null.</summary>
    public string? Problem { get; private set; }

    /// <summary>True while the upper face of the current layer is showing.</summary>
    public bool IsShifted { get; private set; }

    /// <summary>True while the accented layer is showing rather than the alphabetic one.</summary>
    public bool IsAlted { get; private set; }

    public int CursorRow { get; private set; }

    public int CursorColumn { get; private set; }

    /// <summary>Every key of this layout, in reading order.</summary>
    public IReadOnlyList<KeyboardKey> Keys => KeysOf(Layout);

    /// <summary>The key the cursor is on.</summary>
    public KeyboardKey Selected => CellsOf(Layout)[CursorRow][CursorColumn]!;

    /// <summary>What the cursor is on, as it reads on screen.</summary>
    public string SelectedFace => Face(Selected);

    /// <summary>
    /// EmulationStation's own legend, in EmulationStation's own words.
    /// </summary>
    /// <remarks>
    /// The words come from <c>emulationstation2.po</c> and the buttons from
    /// <c>getHelpPrompts</c>, so a RetroBat user reads the same footer here as everywhere else.
    /// Pressing the highlighted key is not among them, upstream either: the grid is visibly a
    /// set of buttons and the action that presses one is the action that presses everything
    /// else in this app.
    /// </remarks>
    public IReadOnlyList<FooterHint> Hints =>
    [
        new FooterHint(NavAction.Start, "OK"),
        new FooterHint(NavAction.Back, "Back"),
        new FooterHint(NavAction.PageDown, "Space"),
        new FooterHint(NavAction.PageUp, "Delete"),
        new FooterHint(NavAction.Alternate, "Shift"),
        new FooterHint(NavAction.Extra, "Reset"),
        FooterHint.Move("Move cursor"),
    ];

    /// <summary>What one key shows, and types, right now.</summary>
    public string Face(KeyboardKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return key.Kind switch
        {
            KeyKind.Character => key.Face(IsShifted, IsAlted),
            KeyKind.Backspace => "←",
            KeyKind.Accept => "✓",
            KeyKind.Shift => "↑",

            // Upstream draws Font Awesome's ellipsis here. RomMBat ships no icon font, so it is
            // the three dots that glyph is, which is also what the key looks like on screen.
            KeyKind.Layer => "...",
            _ => key.Lower,
        };
    }

    public ScreenCommand Handle(NavAction action)
    {
        switch (action)
        {
            case NavAction.Up:
                Step(-1, 0);
                break;

            case NavAction.Down:
                Step(1, 0);
                break;

            case NavAction.Left:
                Step(0, -1);
                break;

            case NavAction.Right:
                Step(0, 1);
                break;

            case NavAction.Accept:
                return Press(Selected);

            case NavAction.PageUp:
                Backspace();
                break;

            case NavAction.PageDown:
                Type(" ");
                break;

            case NavAction.Alternate:
                Shift();
                break;

            case NavAction.Extra:
                Reset();
                break;

            case NavAction.Start:
                return Commit();

            case NavAction.Back:
                return ScreenCommand.Pop;

            default:
                break;
        }

        return ScreenCommand.Stay;
    }

    /// <summary>Builds one layout's keys from upstream's table, once.</summary>
    private static KeyboardKey[] KeysOf(KeyboardLayout layout)
    {
        lock (Built)
        {
            if (Built.TryGetValue(layout, out var cached))
            {
                return cached;
            }

            var table = KeyboardLayouts.Table(layout);
            var keys = new List<KeyboardKey>();

            for (var row = 0; row < Rows; row++)
            {
                var faces = row * KeyboardLayouts.Faces;

                for (var column = 0; column < Columns; column++)
                {
                    var lower = table[faces][column];

                    // A cell swallowed by the key above or to its left. An empty face is not
                    // one of those: that key exists and holds focus, it just does nothing here.
                    if (lower is "-rowspan-" or "-colspan-")
                    {
                        continue;
                    }

                    var width = 1;
                    while (column + width < Columns && table[faces][column + width] == "-colspan-")
                    {
                        width++;
                    }

                    var height = 1;
                    while (row + height < Rows
                        && table[(row + height) * KeyboardLayouts.Faces][column] == "-rowspan-")
                    {
                        height++;
                    }

                    keys.Add(new KeyboardKey(
                        KindOf(lower),
                        lower,
                        table[faces + 1][column],
                        table[faces + 2][column],
                        table[faces + 3][column],
                        row,
                        column,
                        width,
                        height));
                }
            }

            Built[layout] = [.. keys];
            Cells[layout] = Occupy(Built[layout]);
            return Built[layout];
        }
    }

    private static KeyboardKey?[][] CellsOf(KeyboardLayout layout)
    {
        _ = KeysOf(layout);

        lock (Built)
        {
            return Cells[layout];
        }
    }

    private static KeyKind KindOf(string lower) => lower switch
    {
        "DEL" => KeyKind.Backspace,
        "OK" => KeyKind.Accept,
        "SPACE" => KeyKind.Space,
        "SHIFT" => KeyKind.Shift,
        "ALT" => KeyKind.Layer,
        "RESET" => KeyKind.Reset,
        "CANCEL" => KeyKind.Cancel,
        _ => KeyKind.Character,
    };

    private static KeyboardKey?[][] Occupy(KeyboardKey[] keys)
    {
        var cells = new KeyboardKey?[Rows][];

        for (var row = 0; row < Rows; row++)
        {
            cells[row] = new KeyboardKey?[Columns];
        }

        foreach (var key in keys)
        {
            for (var row = key.Row; row < key.Row + key.Height; row++)
            {
                for (var column = key.Column; column < key.Column + key.Width; column++)
                {
                    cells[row][column] = key;
                }
            }
        }

        return cells;
    }

    /// <summary>
    /// Moves to the next key in one direction, stepping over the rest of the current one.
    /// </summary>
    /// <remarks>
    /// Bounded by the grid rather than looping until it finds something, because a layout whose
    /// transcription left a row with one key would otherwise spin here rather than fail where
    /// it is wrong.
    /// </remarks>
    private void Step(int rows, int columns)
    {
        var cells = CellsOf(Layout);
        var from = Selected;

        var row = CursorRow;
        var column = CursorColumn;

        for (var step = 0; step < Math.Max(Rows, Columns); step++)
        {
            row = ((row + rows) % Rows + Rows) % Rows;
            column = ((column + columns) % Columns + Columns) % Columns;

            if (cells[row][column] is { } landed && landed != from)
            {
                CursorRow = row;
                CursorColumn = column;
                return;
            }
        }
    }

    private ScreenCommand Press(KeyboardKey key)
    {
        switch (key.Kind)
        {
            case KeyKind.Character:
                // Blank on this layer, which upstream draws and ignores rather than hiding.
                Type(Face(key));
                break;

            case KeyKind.Space:
                Type(" ");
                break;

            case KeyKind.Backspace:
                Backspace();
                break;

            case KeyKind.Reset:
                Reset();
                break;

            case KeyKind.Shift:
                Shift();
                break;

            case KeyKind.Layer:
                // Upstream drops shift on the way in, so the alted layer is entered the same
                // way every time rather than depending on what the last layer was left in.
                IsShifted = false;
                IsAlted = !IsAlted;
                break;

            case KeyKind.Cancel:
                return ScreenCommand.Pop;

            case KeyKind.Accept:
                return Commit();

            default:
                break;
        }

        return ScreenCommand.Stay;
    }

    private void Type(string face)
    {
        if (face.Length == 0)
        {
            return;
        }

        Text += face;
        Problem = null;
    }

    private void Backspace()
    {
        Problem = null;

        if (Text.Length > 0)
        {
            Text = Text[..^1];
        }
    }

    private void Reset()
    {
        Text = _initial;
        Problem = null;
    }

    // The layers are the same shape, so the cursor stays exactly where it is and the key under
    // it simply changes face.
    private void Shift() => IsShifted = !IsShifted;

    private ScreenCommand Commit()
    {
        // Committing an empty string would ask the caller to make sense of nothing.
        if (Text.Length == 0)
        {
            return ScreenCommand.Stay;
        }

        var result = _accepted(Text);
        Problem = result.Problem;

        // Three answers, and the third is the one worth naming: a caller that took the text and
        // has nowhere to send the user is done with this screen, so it closes. Leaving it open
        // would strand them on a keyboard they have finished with.
        if (result.Next is { } next)
        {
            // Replace rather than push: back from what follows means "not this", not "let me
            // retype it".
            return ScreenCommand.Replace(next);
        }

        return result.Problem is null ? ScreenCommand.Pop : ScreenCommand.Stay;
    }
}
