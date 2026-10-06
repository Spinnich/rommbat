namespace RomMBat.UI.Screens;

/// <summary>How a slot is drawn. The renderer maps each to a font size and a color.</summary>
public enum SlotStyle
{
    /// <summary>The word for how the run ended, in the accent color.</summary>
    Outcome,

    /// <summary>The sentence under the title.</summary>
    Detail,

    /// <summary>Which pass is running, small and in the accent color.</summary>
    Pass,

    /// <summary>The thing being worked on, largest on the screen.</summary>
    Lead,

    /// <summary>The count, under the thing being worked on.</summary>
    Count,

    /// <summary>A small muted line.</summary>
    Small,

    /// <summary>The run's bar. An unknown fraction draws the empty track.</summary>
    Bar,

    /// <summary>Two small halves, anchored at the middle and growing outwards.</summary>
    Split,

    /// <summary>A headed box of lines, the newest kept.</summary>
    Problems,
}

/// <summary>One line of a box slot, and how many lines of the box it is given.</summary>
public sealed record SlotLine(string Text, int Lines);

/// <summary>
/// One place on a progress screen, drawn whether or not it has anything to say.
/// </summary>
/// <param name="Name">What the slot holds, so a test can say which one moved.</param>
/// <param name="Lines">The height it reserves, in its own style's lines. Text past it is trimmed.</param>
public sealed record ProgressSlot(string Name, SlotStyle Style, int Lines)
{
    public string Text { get; init; } = string.Empty;

    /// <summary>The second half of a <see cref="SlotStyle.Split"/> slot.</summary>
    public string Right { get; init; } = string.Empty;

    /// <summary>How full a <see cref="SlotStyle.Bar"/> is, or null for the empty track.</summary>
    public double? Fraction { get; init; }

    /// <summary>The heading of a <see cref="SlotStyle.Problems"/> box, empty while it has none.</summary>
    public string Heading { get; init; } = string.Empty;

    /// <summary>What a <see cref="SlotStyle.Problems"/> box shows, oldest of them first.</summary>
    public IReadOnlyList<SlotLine> Items { get; init; } = [];
}

/// <summary>
/// The fixed set of slots a progress screen draws.
/// </summary>
/// <remarks>
/// <b>Every slot is present in every state, at the same height.</b> The sync and query screens
/// added each line only once it had a value, and the body is centered, so the pass line, the
/// game, the speed and the outcome word each pushed everything else about as they came and went,
/// worst between games and at the end of a run (#490). Here a slot with nothing to say is blank
/// text at its reserved height, and the renderer draws no line of its own.
/// <para>
/// <b>Line budgets are estimated from characters, as <see cref="ListWindow.FactHeight"/> does</b>,
/// because a view model has no text engine and a layout a test cannot read is in the wrong
/// project. A sentence the estimate gets wrong is trimmed by the renderer at the slot's height,
/// so a misjudged line costs a few characters and never the layout.
/// </para>
/// </remarks>
public static class ProgressLayout
{
    /// <summary>
    /// Lines the sentence under the title is given.
    /// </summary>
    /// <remarks>
    /// Three rather than two: a refused query adds its remedy to the server's sentence, and a
    /// definition that did not roam adds a third clause after that.
    /// </remarks>
    public const int DetailLines = 3;

    /// <summary>Lines the problems box holds under its heading.</summary>
    public const int ProblemLines = 6;

    /// <summary>The most lines one problem is given, so one long one cannot take the box.</summary>
    public const int ProblemMaxLines = 2;

    /// <summary>
    /// How wide a problem line is, in characters.
    /// </summary>
    /// <remarks>
    /// The box is 860px at 15px, about a hundred and ten characters a line. Estimated short, so
    /// the error falls on the side of a line left blank rather than a sentence trimmed.
    /// </remarks>
    public const int ProblemColumns = 100;

    /// <summary>
    /// The newest problems that fit the box, oldest of them first.
    /// </summary>
    /// <remarks>
    /// Shared with the screen's offer to read every problem, so the box and the footer cannot
    /// disagree on whether any are hidden.
    /// </remarks>
    public static IReadOnlyList<SlotLine> Fit(IReadOnlyList<string> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);

        var kept = new List<SlotLine>();
        var used = 0;

        for (var index = problems.Count - 1; index >= 0; index--)
        {
            var lines = Math.Clamp(
                (problems[index].Length + ProblemColumns - 1) / ProblemColumns,
                1,
                ProblemMaxLines);

            if (used + lines > ProblemLines)
            {
                break;
            }

            used += lines;
            kept.Add(new SlotLine(problems[index], lines));
        }

        kept.Reverse();
        return kept;
    }

    /// <summary>The problems box, headed only once there is something in it.</summary>
    public static ProgressSlot Problems(IReadOnlyList<string> problems) =>
        new("problems", SlotStyle.Problems, ProblemLines)
        {
            Heading = problems.Count switch
            {
                0 => string.Empty,
                1 => "PROBLEM",
                _ => $"PROBLEMS ({problems.Count})",
            },
            Items = Fit(problems),
        };
}
