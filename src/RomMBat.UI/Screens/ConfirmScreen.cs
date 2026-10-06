using RomMBat.UI.Input;
using RomMBat.UI.Shell;

namespace RomMBat.UI.Screens;

/// <summary>One answer to a confirmation, and what choosing it does.</summary>
/// <param name="Run">Answered as if the confirmation itself had been pressed: a pop closes it.</param>
public sealed record ConfirmButton(string Label, Func<ScreenCommand> Run);

/// <summary>
/// A question with a row of answers, drawn as EmulationStation's message box.
/// </summary>
/// <remarks>
/// <b>Left and right choose, the bottom button presses, and the right button is always the safe
/// answer</b>, which is how ES's box behaves (RB-424). Where this departs from ES is the first
/// selection: ES selects YES even on a delete, and every question here guards a stop or a
/// discard, so the safe answer is selected first and a reflexive press changes nothing.
/// </remarks>
public sealed class ConfirmScreen : IScreen, IPopupScreen
{
    private readonly int _safe;

    /// <param name="question">The whole question, saying what the risky answer costs.</param>
    /// <param name="buttons">The answers, left to right, as ES lays them out.</param>
    /// <param name="safe">Which answer changes nothing. Selected first, and what Back means.</param>
    /// <param name="underneath">The screen the question is about, drawn dimmed behind it.</param>
    public ConfirmScreen(string question, IReadOnlyList<ConfirmButton> buttons, int safe, IScreen? underneath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        ArgumentNullException.ThrowIfNull(buttons);
        ArgumentOutOfRangeException.ThrowIfLessThan(buttons.Count, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(safe);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(safe, buttons.Count);

        Question = question;
        Buttons = buttons;
        Underneath = underneath;
        _safe = safe;
        Selected = safe;
    }

    /// <summary>The usual shape: the risky answer, then the safe one, with the safe one selected.</summary>
    public static ConfirmScreen YesNo(
        string question,
        string yes,
        Func<ScreenCommand> onYes,
        string no,
        IScreen? underneath = null) =>
        new(question, [new ConfirmButton(yes, onYes), new ConfirmButton(no, () => ScreenCommand.Pop)], 1, underneath);

    public string Question { get; }

    public IReadOnlyList<ConfirmButton> Buttons { get; }

    public IScreen? Underneath { get; }

    /// <summary>Which answer the bottom button would press now.</summary>
    public int Selected { get; private set; }

    public string Title => Underneath?.Title ?? Question;

    public IReadOnlyList<FooterHint> Hints =>
    [
        new FooterHint(NavAction.Accept, Buttons[Selected].Label),
        new FooterHint(NavAction.Back, Buttons[_safe].Label),
    ];

    public ScreenCommand Handle(NavAction action)
    {
        switch (action)
        {
            // Clamped rather than wrapped, as ES's row of buttons is.
            case NavAction.Left:
                Selected = Math.Max(0, Selected - 1);
                return ScreenCommand.Stay;

            case NavAction.Right:
                Selected = Math.Min(Buttons.Count - 1, Selected + 1);
                return ScreenCommand.Stay;

            case NavAction.Accept:
                return Buttons[Selected].Run();

            case NavAction.Back:
                return Buttons[_safe].Run();

            default:
                return ScreenCommand.Stay;
        }
    }
}
