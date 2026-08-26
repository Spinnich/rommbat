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
/// <b>QWERTY, not alphabetical.</b> An alphabetical grid looks tidier and is slower to use:
/// people know where keys are on a keyboard and have to hunt on anything else. Every console
/// keyboard is QWERTY and that is not an accident.
/// </para>
/// <para>
/// <b>Case is a toggle, and the first version of this screen got that wrong deliberately.</b>
/// The argument for putting both cases on one flat grid was that a mode is something a user can
/// end up in without knowing how they got there. The price was seventy keys with the alphabet
/// printed twice, which is far more travel and a hunt on every capital. The answer to a
/// confusing mode is to show it plainly, which the grid does by redrawing under the cursor, not
/// to pay that. Bound to L1 and R1, where a console keyboard puts it.
/// </para>
/// <para>
/// <b>The layers are the same shape</b>, so toggling never moves the cursor, and the pairs
/// follow a real keyboard wherever that does not cost a URL something: <c>:</c> and <c>/</c>
/// stay unshifted, because <c>http://</c> would otherwise need the toggle twice in four
/// characters.
/// </para>
/// <para>
/// <b>Movement wraps.</b> On a grid this small a cursor that stops dead at the edge costs more
/// presses than it saves.
/// </para>
/// </remarks>
public sealed class OnScreenKeyboard : IScreen
{
    private static readonly string[] Lower =
    [
        "1234567890",
        "qwertyuiop",
        "asdfghjkl:",
        "zxcvbnm./-",
    ];

    private static readonly string[] Upper =
    [
        "!@#$%^&*()",
        "QWERTYUIOP",
        "ASDFGHJKL~",
        "ZXCVBNM_?=",
    ];

    private readonly Func<string, TypedResult> _accepted;

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
        Prompt = prompt;
        Text = initial ?? string.Empty;
        _accepted = accepted;
    }

    public string Title { get; }

    /// <summary>The line above the grid, explaining what is being asked for.</summary>
    public string Prompt { get; }

    /// <summary>What has been typed so far.</summary>
    public string Text { get; private set; }

    /// <summary>Why the last attempt to commit was refused, or null.</summary>
    public string? Problem { get; private set; }

    /// <summary>True while the upper layer is showing.</summary>
    public bool IsShifted { get; private set; }

    public int CursorRow { get; private set; }

    public int CursorColumn { get; private set; }

    /// <summary>The layer currently on screen.</summary>
    public IReadOnlyList<string> Keys => IsShifted ? Upper : Lower;

    /// <summary>The character the cursor is on, in the current layer.</summary>
    public string Selected => Keys[CursorRow][CursorColumn].ToString();

    /// <summary>Both layers, for the test that asserts they are the same shape.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> Layers => [Lower, Upper];

    public IReadOnlyList<FooterHint> Hints =>
    [
        new FooterHint("A", "Type", 4),
        new FooterHint("L1/R1", IsShifted ? "abc" : "ABC", 3),
        new FooterHint("X", "Backspace", 2),
        new FooterHint("Start", "Done", 4),
        new FooterHint("B", "Cancel", 1),
    ];

    public ScreenCommand Handle(NavAction action)
    {
        switch (action)
        {
            case NavAction.Up:
                CursorRow = (CursorRow - 1 + Keys.Count) % Keys.Count;
                break;

            case NavAction.Down:
                CursorRow = (CursorRow + 1) % Keys.Count;
                break;

            case NavAction.Left:
                CursorColumn = (CursorColumn - 1 + Keys[CursorRow].Length) % Keys[CursorRow].Length;
                break;

            case NavAction.Right:
                CursorColumn = (CursorColumn + 1) % Keys[CursorRow].Length;
                break;

            case NavAction.PageUp:
            case NavAction.PageDown:
                // The layers are the same shape, so the cursor stays exactly where it is and the
                // key under it simply changes case.
                IsShifted = !IsShifted;
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

    private void Backspace()
    {
        Problem = null;

        if (Text.Length > 0)
        {
            Text = Text[..^1];
        }
    }
}
