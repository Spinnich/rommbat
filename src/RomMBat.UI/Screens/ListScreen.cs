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
public sealed class ListScreen : IScreen, IReturnAware, ILiveScreen, IDisposable
{
    private readonly Func<IReadOnlyList<ListRow>> _rows;
    private readonly Func<int, ScreenCommand> _choose;
    private readonly string _acceptLabel;
    private readonly FooterHint[] _extra;

    private readonly CancellationTokenSource _load = new();

    private IReadOnlyList<ListRow> _current;
    private bool _disposed;
    private bool _started;

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
        : this(title, () => rows, choose, acceptLabel, backLabel, extra)
    {
        ArgumentNullException.ThrowIfNull(rows);
    }

    /// <param name="rows">
    /// Re-read whenever this screen becomes current again.
    /// <para>
    /// <b>A fixed list goes stale the moment anything above it writes.</b> Creating a set left
    /// the list underneath still showing the sets from before, and it only corrected itself
    /// when the whole screen was rebuilt by leaving and coming back. Same shape as the bug that
    /// made <c>Status</c> stop being a snapshot in stage 7b-1: a screen that captured state
    /// once and kept showing it.
    /// </para>
    /// </param>
    public ListScreen(
        string title,
        Func<IReadOnlyList<ListRow>> rows,
        Func<int, ScreenCommand> choose,
        string acceptLabel = "Choose",
        string backLabel = "Back",
        params FooterHint[] extra)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(choose);

        Title = title;
        BackLabel = backLabel;
        _rows = rows;
        _current = rows();
        _choose = choose;
        _acceptLabel = acceptLabel;
        _extra = extra ?? [];

        // Never park on a row that cannot be chosen, or the first thing a user does is press
        // Accept and get nothing with no explanation.
        Cursor = FirstAvailable(0, 1);
    }

    public string Title { get; }

    public IReadOnlyList<ListRow> Rows => _current;

    public string BackLabel { get; }

    /// <summary>Which row is selected. Always in range; -1 only when the list is empty.</summary>
    public int Cursor { get; private set; }

    /// <summary>
    /// Which slice of the rows is on screen.
    /// </summary>
    /// <remarks>
    /// <b>Decided here rather than in the renderer, because the renderer cannot be tested.</b>
    /// The windowing arithmetic lived in <c>ScreenView</c>, so a screen that simply never called
    /// it drew every row it had and everything past the height of the display went off it, with
    /// the cursor moving somewhere invisible. That happened twice, to two screens, and the
    /// second time was found from the couch on a twenty-two row filter editor. A property is
    /// something a test can assert on; a call the renderer might not make is not.
    /// </remarks>
    public ListView Window => ListWindow.Compute(Cursor, Rows.Count);


    /// <summary>
    /// A line above the rows, or null.
    /// </summary>
    /// <remarks>
    /// A function rather than a string, because it can depend on state the rows themselves
    /// change: a facet picker's note names the operator, and a fixed string went on saying
    /// "matching any of" after the operator had been set to none.
    /// </remarks>
    public Func<string?>? Note { get; init; }

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

    /// <summary>
    /// Offers the accept hint even when no row can be chosen.
    /// </summary>
    /// <remarks>
    /// <b>For a screen whose rows are all informational but which still answers Accept.</b> The
    /// detail screen is exactly that: every row is a fact rather than a choice, so the cursor
    /// has nowhere to sit, and the hint was suppressed while <see cref="Verbs"/> went on
    /// handling the press. The action worked and the footer never said so, which is the same
    /// failure as promising an action that does not exist, pointed the other way.
    /// </remarks>
    public bool AlwaysOfferAccept { get; init; }

    /// <summary>
    /// Work that has to finish before the rows mean anything.
    /// </summary>
    /// <remarks>
    /// <b>Because some of these lists come from the server, and asking blocks.</b> The
    /// collection picker, the filter facets and the platform counts are all one request, and
    /// the filter values are computed across the whole library: measured in minutes on an
    /// 88,000-rom instance, not seconds. Fetching those on the thread that draws meant the
    /// interface stopped responding for the duration, with nothing on screen saying why, which
    /// from the couch is indistinguishable from a crash.
    /// <para>
    /// Set this and the screen opens immediately saying it is loading, fills in when the work
    /// lands, and stays leavable throughout. Same shape as the resolve screen, and the reason
    /// <see cref="ILiveScreen"/> exists.
    /// </para>
    /// </remarks>
    public Func<CancellationToken, Task<string?>>? Load { get; init; }

    /// <summary>What to say while <see cref="Load"/> runs.</summary>
    public string LoadingMessage { get; init; } = "Asking RomM.";

    /// <summary>True until the loader has finished, or immediately when there is none.</summary>
    public bool IsLoading { get; private set; }

    /// <summary>Why the load failed, when it did.</summary>
    public string? LoadProblem { get; private set; }

    public event EventHandler? Invalidated;

    /// <summary>Starts the loader. Called by whoever pushes the screen.</summary>
    /// <remarks>
    /// Not started in the constructor, so a screen can be built and inspected in a test without
    /// reaching for a network, and so the caller decides when the work begins.
    /// </remarks>
    public ListScreen Started() => Begin(hideRows: true);

    /// <summary>
    /// Starts the loader without hiding the rows behind a loading state.
    /// </summary>
    /// <remarks>
    /// For a list that is already correct before the work finishes and only gets richer after
    /// it. The platform picker reads its rows from <c>platform_map</c> with no network, and the
    /// game counts are enrichment; showing "loading" over rows that are already there would
    /// trade a working screen for a spinner.
    /// </remarks>
    public ListScreen Enriching() => Begin(hideRows: false);

    private ListScreen Begin(bool hideRows)
    {
        if (Load is null || IsLoading || _started)
        {
            return this;
        }

        _started = true;
        IsLoading = hideRows;

        _ = Task.Run(
            async () =>
            {
                try
                {
                    LoadProblem = await Load(_load.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Left before it finished, which is the point of it being cancellable.
                    return;
                }
                catch (Exception ex)
                {
                    // A throw used to fault this task unobserved, so the screen drew its empty
                    // message and told the user the library had none of what it had failed to
                    // ask for. A filter picker said "this library reports no genres" against a
                    // library with 343, because one ROM over 2 GiB broke the response.
                    //
                    // Broad on purpose. A loader talks to a server, a disk and a database, and
                    // the alternative to catching everything here is drawing "nothing" for
                    // whatever was not listed.
                    LoadProblem = ex.Message;
                }
                finally
                {
                    IsLoading = false;
                    Returned();

                    // Raised from whatever thread did the work. The shell marshals it.
                    Invalidated?.Invoke(this, EventArgs.Empty);
                }
            },
            CancellationToken.None);

        return this;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Cancelled, never disposed: a request still unwinding can register on this token.
        _load.Cancel();
    }

    public IReadOnlyList<FooterHint> Hints
    {
        get
        {
            var hints = new List<FooterHint>();

            if (AlwaysOfferAccept || (Cursor >= 0 && Rows[Cursor].Available))
            {
                hints.Add(new FooterHint(NavAction.Accept, _acceptLabel));
            }

            hints.AddRange(_extra);
            hints.Add(new FooterHint(NavAction.Back, BackLabel));

            return hints;
        }
    }

    /// <summary>Re-reads the rows, because something above this screen may have written.</summary>
    public void Returned()
    {
        _current = _rows();

        if (Cursor >= _current.Count || (Cursor >= 0 && !_current[Cursor].Available))
        {
            Cursor = FirstAvailable(Math.Max(0, Math.Min(Cursor, _current.Count - 1)), 1);
        }
        else if (Cursor < 0)
        {
            Cursor = FirstAvailable(0, 1);
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
            {
                var answer = _choose(Cursor);

                // A choice that stays put has changed something this list shows, which is what
                // makes a multi-select possible without a second screen kind: the rows are a
                // factory, so re-reading them is how a tick appears next to what was chosen.
                if (answer.Kind == ScreenCommandKind.Stay)
                {
                    Returned();
                }

                return answer;
            }

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
