using RomMBat.UI.Input;
using RomMBat.UI.Shell;

namespace RomMBat.UI.Screens;

/// <summary>What the caller made of what was typed.</summary>
/// <param name="Next">Where to go next, or null when the text was no good.</param>
/// <param name="Problem">Why it was no good, shown above the grid so it can be corrected.</param>
public sealed record TypedResult(IScreen? Next, string? Problem = null);

/// <summary>
/// Typing on a gamepad, which the plan names as the one genuinely hostile step.
/// </summary>
/// <remarks>
/// <b>This exists because a server URL has to be typed and a d-pad is not a keyboard.</b> The
/// risks table calls it out by name, and the mitigation is this plus remembering the answer so
/// it is typed once.
/// <para>
/// <b>The layout is fixed and reachable rather than clever.</b> Every key is on one grid, so
/// there is no shift state to get lost in and no mode a user can end up in without knowing how
/// they got there. The rows carry what a URL needs, which is why <c>.</c>, <c>:</c>, <c>/</c>
/// and <c>-</c> are on the main grid rather than behind a symbols page.
/// </para>
/// <para>
/// <b>Movement wraps.</b> On a grid this small, a cursor that stops dead at the edge costs more
/// presses than it saves, and wrapping is what every console keyboard does.
/// </para>
/// </remarks>
public sealed class OnScreenKeyboard : IScreen
{
    /// <summary>The grid, row by row. Every row is the same width so movement is predictable.</summary>
    private static readonly string[] Rows =
    [
        "1234567890",
        "abcdefghij",
        "klmnopqrst",
        "uvwxyz.-_:",
        "/ABCDEFGHI",
        "JKLMNOPQRS",
        "TUVWXYZ~?=",
    ];

    private readonly Func<string, TypedResult> _accepted;
    private readonly string _prompt;

    /// <param name="accepted">
    /// Given what was typed, either the next screen or the reason it cannot be used. Validation
    /// lives with the caller, because what makes a string usable is the caller's business: for
    /// a server address that is <see cref="Core.InstallSession.ResolveOrigin"/>, which already
    /// owns the rule and its words.
    /// </param>
    public OnScreenKeyboard(string title, string prompt, string initial, Func<string, TypedResult> accepted)
    {
        ArgumentNullException.ThrowIfNull(accepted);

        Title = title;
        _prompt = prompt;
        Text = initial ?? string.Empty;
        _accepted = accepted;
    }

    public string Title { get; }

    /// <summary>What has been typed so far.</summary>
    public string Text { get; private set; }

    /// <summary>The line above the grid, explaining what is being asked for.</summary>
    public string Prompt => _prompt;

    /// <summary>Why the last attempt to commit was refused, or null.</summary>
    public string? Problem { get; private set; }

    public int CursorRow { get; private set; }

    public int CursorColumn { get; private set; }

    /// <summary>The character the cursor is on.</summary>
    public string Selected => Rows[CursorRow][CursorColumn].ToString();

    public static IReadOnlyList<string> Grid => Rows;

    public IReadOnlyList<FooterHint> Hints =>
    [
        new("A", "Type", 3),
        new("X", "Backspace", 2),
        new("Start", "Done", 3),
        new("B", "Cancel", 1),
    ];

    public ScreenCommand Handle(NavAction action)
    {
        switch (action)
        {
            case NavAction.Up:
                CursorRow = (CursorRow - 1 + Rows.Length) % Rows.Length;
                ClampColumn();
                break;

            case NavAction.Down:
                CursorRow = (CursorRow + 1) % Rows.Length;
                ClampColumn();
                break;

            case NavAction.Left:
                CursorColumn = (CursorColumn - 1 + Rows[CursorRow].Length) % Rows[CursorRow].Length;
                break;

            case NavAction.Right:
                CursorColumn = (CursorColumn + 1) % Rows[CursorRow].Length;
                break;

            case NavAction.Accept:
                Text += Selected;
                Problem = null;
                break;

            case NavAction.Alternate:
                Backspace();
                break;

            case NavAction.Start:
                // Committing an empty string would ask the caller to make sense of nothing.
                if (Text.Length == 0)
                {
                    break;
                }

                var result = _accepted(Text);
                Problem = result.Problem;

                // Three answers, and the third is the one worth naming: a caller that took the
                // text and has nowhere to send the user is done with this screen, so it closes.
                // Leaving it open would strand them on a keyboard they have finished with.
                if (result.Next is { } next)
                {
                    // Replace rather than push: back from what follows means "not this", not
                    // "let me retype it".
                    return ScreenCommand.Replace(next);
                }

                return result.Problem is null ? ScreenCommand.Pop : ScreenCommand.Stay;

            case NavAction.Back:
                return ScreenCommand.Pop;

            default:
                break;
        }

        return ScreenCommand.Stay;
    }

    /// Every row is the same width today, so this never fires. It is here because a row
    /// edited to a different length would otherwise index out of range on a vertical move,
    /// which is a crash in a full-screen app with no console behind it.
    private void ClampColumn() =>
        CursorColumn = Math.Min(CursorColumn, Rows[CursorRow].Length - 1);

    private void Backspace()
    {
        Problem = null;

        if (Text.Length > 0)
        {
            Text = Text[..^1];
        }
    }
}
