using RomMBat.UI.Input;
using RomMBat.UI.Shell;

namespace RomMBat.UI.Screens;

/// <summary>
/// One row of a list.
/// </summary>
/// <param name="Value">The right-hand column, when the row has one.</param>
/// <param name="Detail">A second line under it, usually the reason a row is unavailable.</param>
/// <param name="Available">
/// False for a row that is shown but cannot be chosen. A picker that hid what this install
/// cannot offer would leave a user concluding RomMBat does not support it, where the reason is
/// their own pairing and is fixable.
/// </param>
public sealed record ListRow(string Label, string? Value = null, string? Detail = null, bool Available = true);

/// <summary>
/// A list of rows with a cursor and one action per row.
/// </summary>
/// <remarks>
/// <b>One screen kind for four screens.</b> The sets list, the scope picker, the platform
/// picker and the folder picker are the same shape, and giving each its own view model and its
/// own arm in <c>ScreenView</c> would have quadrupled the file 7b-1's ledger already named as
/// the one that would grow worst.
/// <para>
/// <b>Accept opens, it never adjusts.</b> A list of choices answers Accept by acting on the
/// row, not by stepping through the list, which is what <see cref="NavAction.Left"/> and
/// <see cref="NavAction.Right"/> are for elsewhere.
/// </para>
/// <para>
/// <b>The cursor wraps.</b> A d-pad held at the bottom of a long list with no wrap feels
/// broken, and the alternative is a user paging back up through forty systems.
/// </para>
/// </remarks>
public sealed class ListScreen : IScreen
{
    private readonly Func<int, ScreenCommand> _choose;
    private readonly string _acceptLabel;
    private readonly FooterHint[] _extra;

    /// <param name="choose">What Accept on a row does. Only called for an available row.</param>
    /// <param name="acceptLabel">
    /// What the footer promises Accept will do, in this screen's own words. It is quoted
    /// beside the glyph, and it may never name a button.
    /// </param>
    public ListScreen(
        string title,
        IReadOnlyList<ListRow> rows,
        Func<int, ScreenCommand> choose,
        string acceptLabel = "Choose",
        string backLabel = "Back",
        params FooterHint[] extra)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(choose);

        Title = title;
        Rows = rows;
        BackLabel = backLabel;
        _choose = choose;
        _acceptLabel = acceptLabel;
        _extra = extra ?? [];

        // Never park on a row that cannot be chosen, or the first thing a user does is press
        // Accept and get nothing with no explanation.
        Cursor = FirstAvailable(0, 1);
    }

    public string Title { get; }

    public IReadOnlyList<ListRow> Rows { get; }

    public string BackLabel { get; }

    /// <summary>Which row is selected. Always in range; -1 only when the list is empty.</summary>
    public int Cursor { get; private set; }

    /// <summary>An extra line under the title, when the list needs explaining.</summary>
    public string? Note { get; init; }

    /// <summary>What is shown instead of rows when there are none.</summary>
    public string? EmptyMessage { get; init; }

    /// <summary>
    /// The screen's own verbs, for the actions a list does not define.
    /// </summary>
    /// <remarks>
    /// Consulted before the navigation below, so a caller can answer Start or Alternate and
    /// leave Up, Down, Accept and Back alone. It is given the selected row, because every verb
    /// that has wanted this so far acts on one.
    /// </remarks>
    public Func<NavAction, int, ScreenCommand?>? Verbs { get; init; }

    public IReadOnlyList<FooterHint> Hints
    {
        get
        {
            var hints = new List<FooterHint>();

            if (Cursor >= 0 && Rows[Cursor].Available)
            {
                hints.Add(new FooterHint(NavAction.Accept, _acceptLabel));
            }

            hints.AddRange(_extra);
            hints.Add(new FooterHint(NavAction.Back, BackLabel));

            return hints;
        }
    }

    public ScreenCommand Handle(NavAction action)
    {
        if (Verbs?.Invoke(action, Cursor) is { } answered)
        {
            return answered;
        }

        switch (action)
        {
            case NavAction.Up:
                Cursor = FirstAvailable(Cursor - 1, -1);
                return ScreenCommand.Stay;

            case NavAction.Down:
                Cursor = FirstAvailable(Cursor + 1, 1);
                return ScreenCommand.Stay;

            case NavAction.Accept when Cursor >= 0 && Rows[Cursor].Available:
                return _choose(Cursor);

            case NavAction.Back:
                return ScreenCommand.Pop;

            default:
                return ScreenCommand.Stay;
        }
    }

    /// <summary>
    /// The first choosable row from <paramref name="from"/>, wrapping, or -1 if there is none.
    /// </summary>
    /// <remarks>
    /// Bounded by the row count rather than looping until it finds one, because a list whose
    /// rows are all unavailable is a real state: a pairing with no collection scope viewing a
    /// picker offering only collections would otherwise spin here forever.
    /// </remarks>
    private int FirstAvailable(int from, int step)
    {
        if (Rows.Count == 0)
        {
            return -1;
        }

        var index = ((from % Rows.Count) + Rows.Count) % Rows.Count;

        for (var tried = 0; tried < Rows.Count; tried++)
        {
            if (Rows[index].Available)
            {
                return index;
            }

            index = ((index + step) % Rows.Count + Rows.Count) % Rows.Count;
        }

        return -1;
    }
}
