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

/// <summary>How far a screen's loader has got.</summary>
public sealed record LoadProgress(int Done, int Total)
{
    public double Fraction => Total > 0 ? Math.Clamp((double)Done / Total, 0, 1) : 0;

    /// <summary>The count as a person reads it.</summary>
    public string Counted => string.Create(
        System.Globalization.CultureInfo.CurrentCulture,
        $"{Done:N0} of {Total:N0}");
}

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
public sealed class ListScreen : IScreen, IWindowedScreen, IReturnAware, ILiveScreen, IDisposable
{
    private readonly Func<IReadOnlyList<ListRow>> _rows;
    private readonly Func<int, ScreenCommand> _choose;
    private readonly string _acceptLabel;
    private readonly FooterHint[] _extra;

    private readonly CancellationTokenSource _load = new();

    private volatile ListState _state;
    private bool _reading;
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
        _choose = choose;
        _acceptLabel = acceptLabel;
        _extra = extra ?? [];

        // Never park on a row that cannot be chosen, or the first thing a user does is press
        // Accept and get nothing with no explanation.
        var initial = rows();
        _state = new ListState(initial, FirstAvailable(initial, 0, 1));
    }

    public string Title { get; }

    public IReadOnlyList<ListRow> Rows => _state.Rows;

    public string BackLabel { get; }

    /// <summary>
    /// Which row is selected, or -1 when nothing is.
    /// </summary>
    /// <remarks>
    /// <b>A reading list that fits on screen has no cursor, and that is a fix rather than an
    /// omission.</b> Every row on one is a fact rather than a choice, so a highlight moving
    /// through them says they can be chosen when they cannot: a hands-on pass read a game's
    /// detail screen as "all its information as navigable selections even though they're not".
    /// <para>
    /// It appears the moment the list is longer than the window, because then the highlight is
    /// no longer a claim about choosing but the only way to say where scrolling has got to.
    /// That is the case <see cref="Reading"/> was invented for, on a run's problems list that
    /// would not scroll at all.
    /// </para>
    /// </remarks>
    public int Cursor => Reading && _state.Rows.Count <= ListWindow.CapacityFor(true)
        ? -1
        : _state.Cursor;

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
    public ListView Window
    {
        get
        {
            var state = _state;

            // The stored cursor, not the published one: a reading list that fits hides its
            // cursor and still has to draw every row it holds.
            //
            // Fewer rows when each one is taller, or the block runs off the bottom of the
            // window and Avalonia draws a scroll bar no gamepad can reach.
            return ListWindow.Compute(state.Cursor, state.Rows.Count, ListWindow.CapacityFor(Reading));
        }
    }


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
    /// The footer's extra hints, when which of them apply depends on what the screen loaded.
    /// </summary>
    /// <remarks>
    /// <b>A function rather than the fixed array, for the reason <see cref="Note"/> became
    /// one</b>: it states a fact the rows can change. A screen whose verb only works once a
    /// preview has come back cannot say so with a hint chosen at construction, and three screens
    /// got the same rule wrong three different ways because of it. The repair screen and the
    /// set-removal screen answered <see cref="NavAction.Start"/> and never offered it, so from
    /// the couch the only thing the footer named was Back; the per-game removal screen offered
    /// it always, including when the preview had just said nothing would go, so the press walked
    /// through two screens and removed nothing.
    /// <para>
    /// Both halves are one rule: <b>offer it exactly when it works</b>. A footer promising an
    /// action that does nothing and a footer silent about one that does are the same defect
    /// pointed two ways, and round 8 of stage 7b-1 found the first while
    /// <see cref="AlwaysOfferAccept"/> exists for the second.
    /// </para>
    /// </remarks>
    public Func<IReadOnlyList<FooterHint>>? ExtraHints { get; init; }

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
    /// Offers the accept hint only while this says so, on a screen of facts.
    /// </summary>
    /// <remarks>
    /// <b>The conditional form of <see cref="AlwaysOfferAccept"/>, for a confirm screen whose
    /// answer is not known until its preview lands.</b> A removal preview has no choosable row,
    /// so the cursor cannot decide the hint, and the verb only becomes real once the plan says
    /// something can go. Offering it before that is a press that walks through a second screen
    /// and does nothing, which is what a hands-on pass met; never offering it is a screen whose
    /// only named answer is the one that changes nothing, which is what the same pass met twice
    /// more.
    /// </remarks>
    public Func<bool>? OfferAcceptWhen { get; init; }

    /// <summary>
    /// What Back does, when leaving means closing more than this screen.
    /// </summary>
    /// <remarks>
    /// <b>For a screen that invalidated the ones under it.</b> Deleting a set leaves its
    /// confirmation, its preview and its own detail screen all describing something that does
    /// not exist, so backing out of the last of them has to close all four rather than walk a
    /// person through three stale screens to reach the list.
    /// </remarks>
    public Func<ScreenCommand>? OnBack { get; init; }

    /// <summary>
    /// Every row is text to read rather than a choice, so the cursor walks all of them.
    /// </summary>
    /// <remarks>
    /// <b>Because an unavailable row is normally one the cursor skips, and a list of nothing
    /// but unavailable rows therefore does not scroll at all.</b> That is right for a picker,
    /// where parking on a row Accept cannot act on is a press that does nothing, and wrong for
    /// a list whose whole purpose is reading: the sync run's problems opened with a cursor of
    /// -1, so a hands-on pass could see the first few and move through none of them.
    /// <para>
    /// Accept still offers nothing, because <see cref="Hints"/> asks the row under the cursor
    /// and every row here is unavailable. The renderer also draws these rows at a uniform
    /// height, since text long enough to wrap is what makes a windowed list change size as it
    /// scrolls.
    /// </para>
    /// </remarks>
    public bool Reading
    {
        get => _reading;

        init
        {
            _reading = value;

            // Set here rather than read in the constructor, because an object initialiser runs
            // after it: the constructor computed a cursor with this still false, parked at -1
            // because no row is available, and the first press then stepped from -2. Caught by
            // the test below rather than from the couch, which is the only reason it is not a
            // fourth hands-on round.
            if (value && _state.Cursor < 0 && _state.Rows.Count > 0)
            {
                _state = _state with { Cursor = 0 };
            }
        }
    }

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
    public string LoadingMessage { get; init; } = "Asking RomM...";

    /// <summary>
    /// How far through the load is, when it can say, as a fraction and a count.
    /// </summary>
    /// <remarks>
    /// <b>Because a sentence that does not change is a hung screen.</b> The file check and its
    /// repair are one filesystem check per row and a live install measured 5,932 of them off a
    /// USB stick, so both sat on a fixed line for seconds with nothing to say they were still
    /// going. Found on a hands-on pass, and it is the same finding stage 7b-2b recorded about a
    /// resolve that showed no movement.
    /// <para>
    /// Null while a load has nothing countable to report, which is most of them: a request to a
    /// server has one step and a bar over it would be a fiction.
    /// </para>
    /// </remarks>
    public LoadProgress? Progress { get; private set; }

    /// <summary>Records how far a load has got, and asks for a redraw.</summary>
    /// <remarks>
    /// Handed to <see cref="Load"/> as an <see cref="IProgress{T}"/> so the work reports rather
    /// than the screen polling, which is the shape every other progress on this surface uses.
    /// </remarks>
    public IProgress<(int Done, int Total)> Reporter => new Immediate(this);

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

            var state = _state;

            var offerAccept = OfferAcceptWhen is { } when
                ? when()
                : AlwaysOfferAccept || (state.Cursor >= 0 && state.Rows[state.Cursor].Available);

            if (offerAccept)
            {
                hints.Add(new FooterHint(NavAction.Accept, _acceptLabel));
            }

            hints.AddRange(ExtraHints is { } dynamic ? dynamic() : _extra);
            hints.Add(new FooterHint(NavAction.Back, BackLabel));

            return hints;
        }
    }

    /// <summary>Re-reads the rows, because something above this screen may have written.</summary>
    public void Returned()
    {
        var rows = _rows();
        var cursor = _state.Cursor;

        if (Reading)
        {
            // Only clamped. Availability decides nothing here, and re-reading must not throw
            // away where the user had scrolled to.
            cursor = rows.Count == 0 ? -1 : Math.Clamp(cursor, 0, rows.Count - 1);
        }
        else if (cursor >= rows.Count || (cursor >= 0 && !rows[cursor].Available))
        {
            cursor = FirstAvailable(rows, Math.Max(0, Math.Min(cursor, rows.Count - 1)), 1);
        }
        else if (cursor < 0)
        {
            cursor = FirstAvailable(rows, 0, 1);
        }

        _state = new ListState(rows, cursor);
    }

    public ScreenCommand Handle(NavAction action)
    {
        var state = _state;

        if (Verbs?.Invoke(action, state.Cursor) is { } answered)
        {
            return answered;
        }

        switch (action)
        {
            case NavAction.Up:
                _state = state with { Cursor = Step(state.Rows, state.Cursor - 1, -1) };
                return ScreenCommand.Stay;

            case NavAction.Down:
                _state = state with { Cursor = Step(state.Rows, state.Cursor + 1, 1) };
                return ScreenCommand.Stay;

            case NavAction.Accept when state.Cursor >= 0 && state.Rows[state.Cursor].Available:
            {
                var answer = _choose(state.Cursor);

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
                return OnBack is { } leave ? leave() : ScreenCommand.Pop;

            default:
                return ScreenCommand.Stay;
        }
    }

    /// <summary>Where the cursor lands next, which on a reading list is simply the next row.</summary>
    private int Step(IReadOnlyList<ListRow> rows, int from, int step) =>
        Reading
            ? rows.Count == 0 ? -1 : ((from % rows.Count) + rows.Count) % rows.Count
            : FirstAvailable(rows, from, step);

    /// <summary>
    /// The first choosable row from <paramref name="from"/>, wrapping, or -1 if there is none.
    /// </summary>
    /// <remarks>
    /// Bounded by the row count rather than looping until it finds one, because a list whose
    /// rows are all unavailable is a real state: a pairing with no collection scope viewing a
    /// picker offering only collections would otherwise spin here forever.
    /// </remarks>
    private static int FirstAvailable(IReadOnlyList<ListRow> rows, int from, int step)
    {
        if (rows.Count == 0)
        {
            return -1;
        }

        var index = ((from % rows.Count) + rows.Count) % rows.Count;

        for (var tried = 0; tried < rows.Count; tried++)
        {
            if (rows[index].Available)
            {
                return index;
            }

            index = ((index + step) % rows.Count + rows.Count) % rows.Count;
        }

        return -1;
    }

    /// <summary>Reports a load's progress on whatever thread the work is on.</summary>
    private sealed class Immediate : IProgress<(int Done, int Total)>
    {
        private readonly ListScreen _screen;

        public Immediate(ListScreen screen) => _screen = screen;

        public void Report((int Done, int Total) value)
        {
            _screen.Progress = value.Total > 0
                ? new LoadProgress(value.Done, value.Total)
                : null;

            _screen.Invalidated?.Invoke(_screen, EventArgs.Empty);
        }
    }

    /// <summary>
    /// The rows and the cursor into them, which are one value rather than two.
    /// </summary>
    /// <remarks>
    /// <b>Because a loader finishes on the thread pool.</b> <see cref="Returned"/> runs in the
    /// load's continuation, before <see cref="Invalidated"/> is raised and marshalled, so it
    /// writes while the drawing thread may be inside <c>Handle</c> or <c>Hints</c>. Written as
    /// two fields it set the rows first and clamped the cursor second, and a read landing
    /// between them indexed a shorter list with the old cursor. One reference assignment
    /// publishes both, so a reader sees the pair before or the pair after and never a mix.
    /// </remarks>
    private sealed record ListState(IReadOnlyList<ListRow> Rows, int Cursor);
}
