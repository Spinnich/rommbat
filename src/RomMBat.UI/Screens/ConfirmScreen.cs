using RomMBat.UI.Input;
using RomMBat.UI.Shell;

namespace RomMBat.UI.Screens;

/// <summary>One answer to a confirmation, and what choosing it does.</summary>
/// <param name="Run">Answered as if the confirmation itself had been pressed: a pop closes it.</param>
public sealed record ConfirmButton(string Label, Func<ScreenCommand> Run)
{
    /// <summary>
    /// Whether the answer can be given yet, read on every draw.
    /// </summary>
    /// <remarks>
    /// For an answer that only means something once a preview has landed, such as removing the
    /// games a preview lists. Unset means always.
    /// </remarks>
    public Func<bool>? EnabledWhen { get; init; }

    public bool Enabled => EnabledWhen?.Invoke() ?? true;
}

/// <summary>
/// A question with a row of answers, drawn as EmulationStation's message box, and every
/// confirmation on this surface.
/// </summary>
/// <remarks>
/// <b>Left and right choose, the bottom button presses, and the right button is always the safe
/// answer</b>, which is how ES's box behaves (RB-424). Where this departs from ES is the first
/// selection: ES selects YES even on a delete, and every question here guards a stop, a delete
/// or a discard, so the safe answer is selected first and a reflexive press changes nothing.
/// <para>
/// <b>A confirmation that needs a preview carries it.</b> <see cref="Details"/> are drawn as a
/// pane of facts inside the box, scrolled with up and down, and <see cref="Load"/> fills them
/// off the drawing thread: removing a set's games is minutes of flushing and planning on a large
/// install. An answer that needs the preview is disabled until it lands.
/// </para>
/// <para>
/// <b>An answer that acts in place says what it did.</b> <see cref="Answer"/> turns the box into
/// one sentence and a Done button, as ES's own boxes report with OK, so a drop or a cancel is
/// not followed by a list that simply has one row fewer and no word on why.
/// </para>
/// </remarks>
public sealed class ConfirmScreen : IScreen, IPopupScreen, IWindowedScreen, ILiveScreen, IDisposable
{
    private readonly CancellationTokenSource _load = new();
    private int _safe;
    private int _offset;
    private bool _started;
    private bool _disposed;

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

    public string Question { get; private set; }

    public IReadOnlyList<ConfirmButton> Buttons { get; private set; }

    public IScreen? Underneath { get; }

    /// <summary>Which answer the bottom button would press now.</summary>
    public int Selected { get; private set; }

    public string Title => Underneath?.Title ?? Question;

    /// <summary>
    /// Facts to read before answering, re-read on every draw, or null for a bare question.
    /// </summary>
    public Func<IReadOnlyList<ListRow>>? Details { get; init; }

    /// <summary>Work that has to finish before the details mean anything.</summary>
    /// <remarks>Returns why it failed, or null. Started by <see cref="Started"/>, never by the constructor.</remarks>
    public Func<CancellationToken, Task<string?>>? Load { get; init; }

    /// <summary>What to say while <see cref="Load"/> runs.</summary>
    public string LoadingMessage { get; init; } = "Working...";

    public bool IsLoading { get; private set; }

    /// <summary>Why the load failed, when it did.</summary>
    public string? LoadProblem { get; private set; }

    /// <summary>True once <see cref="Answer"/> has turned the question into its outcome.</summary>
    public bool IsAnswered { get; private set; }

    public event EventHandler? Invalidated;

    /// <summary>The details as drawn, or none while loading or once answered.</summary>
    public IReadOnlyList<ListRow> Rows =>
        IsAnswered || IsLoading || Details is null ? [] : Details();

    public int Cursor => -1;

    public bool Reading => true;

    /// <summary>
    /// Which details are on screen, scrolled by an offset as any pane of facts is, inside the
    /// box's own fixed height.
    /// </summary>
    public ListView Window =>
        ListWindow.ScrolledByHeight(_offset, [.. Rows.Select(row => ListWindow.FactHeight(row.Detail))], ListWindow.ConfirmDetailsBudget);

    /// <summary>
    /// The selected answer, the d-pad, and the right button only when it answers differently.
    /// </summary>
    /// <remarks>
    /// ES's box names the bottom button and CHOOSE (RB-424). The right button is named once the
    /// selection has moved off the safe answer, because only then do the two buttons differ, and
    /// a footer naming the same answer twice says nothing a person can use.
    /// </remarks>
    public IReadOnlyList<FooterHint> Hints =>
    [
        .. Buttons[Selected].Enabled ? new[] { new FooterHint(NavAction.Accept, Buttons[Selected].Label) } : [],
        .. Buttons.Count > 1 || Rows.Count > 0 ? new[] { FooterHint.Move("Choose") } : [],
        .. Selected != _safe ? new[] { new FooterHint(NavAction.Back, Buttons[_safe].Label) } : [],
    ];

    /// <summary>Starts the loader. Called by whoever opens the box.</summary>
    public ConfirmScreen Started()
    {
        if (Load is null || _started)
        {
            return this;
        }

        _started = true;
        IsLoading = true;

        _ = Task.Run(
            async () =>
            {
                try
                {
                    LoadProblem = await Load(_load.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // Broad for the reason ListScreen's loader is: a preview talks to a server, a
                    // disk and a database, and the alternative is a box that never fills.
                    LoadProblem = ex.Message;
                }
                finally
                {
                    IsLoading = false;
                    Invalidated?.Invoke(this, EventArgs.Empty);
                }
            },
            CancellationToken.None);

        return this;
    }

    /// <summary>
    /// Turns the box into what an answer did, with Done as the only button.
    /// </summary>
    /// <param name="outcome">What happened, as a sentence.</param>
    /// <param name="done">What Done does; closing the box unless given.</param>
    /// <returns>Stay, so a button's <c>Run</c> can return it directly.</returns>
    public ScreenCommand Answer(string outcome, Func<ScreenCommand>? done = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);

        Question = outcome;
        Buttons = [new ConfirmButton(ListScreen.DoneLabel, done ?? (() => ScreenCommand.Pop))];
        _safe = 0;
        Selected = 0;
        _offset = 0;
        IsAnswered = true;

        return ScreenCommand.Stay;
    }

    public ScreenCommand Handle(NavAction action)
    {
        switch (action)
        {
            // Clamped rather than wrapped, as ES's row of buttons is, and never onto an answer
            // that cannot be given yet.
            case NavAction.Left:
                Selected = Step(-1);
                return ScreenCommand.Stay;

            case NavAction.Right:
                Selected = Step(1);
                return ScreenCommand.Stay;

            case NavAction.Up:
                _offset = Math.Max(0, Window.Start - 1);
                return ScreenCommand.Stay;

            case NavAction.Down:
                _offset = Window.Below > 0 ? Window.Start + 1 : Window.Start;
                return ScreenCommand.Stay;

            case NavAction.Accept when Buttons[Selected].Enabled:
                return Buttons[Selected].Run();

            case NavAction.Back:
                return Buttons[_safe].Run();

            default:
                return ScreenCommand.Stay;
        }
    }

    private int Step(int direction)
    {
        for (var index = Selected + direction; index >= 0 && index < Buttons.Count; index += direction)
        {
            if (Buttons[index].Enabled)
            {
                return index;
            }
        }

        return Selected;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Canceled, never disposed: a request still unwinding can register on this token.
        _load.Cancel();
    }
}
