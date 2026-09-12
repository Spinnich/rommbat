using System.Globalization;
using RomM.Client;
using RomMBat.Core;
using RomMBat.Core.Sets;
using RomMBat.UI.Input;
using RomMBat.UI.Shell;

namespace RomMBat.UI.Screens;

/// <summary>
/// Finding one game, a page at a time.
/// </summary>
/// <remarks>
/// <b>The first screen that pages the server rather than reading the store</b>, which is why it
/// is its own view model and not a fifth <see cref="ListScreen"/> caller: everything else on
/// this surface has all its rows the moment it opens, and <c>ListScreen</c>'s loader fills a
/// list in rather than moving through one.
/// <para>
/// <b>Nothing here holds more than one page.</b> M2's rule is that the catalog is never mirrored
/// wholesale, and <c>RomRow</c> and <c>RomPager</c> both restate it: an 83k library is 333 pages
/// and the longest description in a 5,000-row sample is 11,719 characters. Moving past the
/// bottom fetches the next offset and <b>replaces</b> what is held; moving past the top fetches
/// the previous one. A test asserts the row count never exceeds the page size across several
/// pages, because this is the rule most likely to be broken here and it breaks silently and
/// only at scale.
/// </para>
/// <para>
/// <b>It degrades rather than refusing, and it says which of the two it is showing.</b> With a
/// server it pages the library; without one it lists what this device holds. That decision is
/// <see cref="BrowseService"/>'s and the wording is this file's, which is the split every screen
/// here follows.
/// </para>
/// <para>
/// <b>The cursor stops at the end of the last page rather than wrapping.</b> Every other list in
/// this app wraps, and a paged one that wraps to page one makes a different promise: a person
/// who has paged through nine thousand rows loses their place with no warning, and the refetch
/// of page one looks exactly like the stall a failed fetch produces. A library that fits in one
/// page still wraps, because there is no paging to undo. Ruled with Spinnich.
/// </para>
/// <para>
/// <b>No cover art.</b> Text rows, like every other screen. Art is its own stage with its own
/// measurement, and "just the selected row" is the version of it that gets reintroduced by
/// accident.
/// </para>
/// </remarks>
public sealed class BrowseViewModel : IScreen, IWindowedScreen, ILiveScreen, IDisposable
{
    private readonly InstallSession _session;
    private readonly Func<Uri, RomMConnection>? _connect;
    private readonly BrowseService _service;
    private readonly CancellationTokenSource _load = new();
    private readonly Lock _gate = new();

    private volatile BrowseState _state = new(null, true, null, null, null, null, 0, false);
    private RomMConnection? _connection;
    private bool _disposed;

    /// <summary>
    /// True while a fetch is on the way, which is not the same as the screen saying so.
    /// </summary>
    /// <remarks>
    /// <c>BrowseState.IsLoading</c> is what the screen draws and it starts true, because the
    /// constructor fetches immediately and a screen that opened claiming to be idle would flash
    /// an empty library. Reusing it as the in-flight guard therefore refuses the first fetch of
    /// every screen. They are two facts and this is the second one.
    /// </remarks>
    private bool _fetching;

    /// <param name="connect">
    /// How the screen reaches the server. Taken so a test can stand a stub in its place, the way
    /// every other screen that talks to RomM already does.
    /// </param>
    public BrowseViewModel(
        InstallSession session,
        Func<Uri, RomMConnection>? connect = null,
        PlatformOption? platform = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        _connect = connect;
        _service = new BrowseService(session);

        _state = _state with
        {
            PlatformId = platform?.PlatformId.ToString(CultureInfo.InvariantCulture),
            Folder = platform?.Folder,
            PlatformLabel = platform?.Label,
        };

        Fetch(0);
    }

    /// <summary>
    /// Where finding a game starts: which platform, then the games in it.
    /// </summary>
    /// <remarks>
    /// <b>The library is the wrong first screen.</b> A live instance holds 96,060 games, so
    /// opening on all of them is 1,922 pages of scrolling and a person looking for a Mega Drive
    /// title has no reason to be shown Windows games first. Narrowing is the first thing anyone
    /// does, so it is the first thing offered. Found on the first hands-on pass.
    /// <para>
    /// Every platform is still an option on that screen, so nothing is taken away; it is one
    /// press rather than the default.
    /// </para>
    /// </remarks>
    public static IScreen Start(InstallSession session, Func<Uri, RomMConnection>? connect = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        var platforms = new SyncSetService(session).PlatformsKnownHere();

        return new ListScreen(
            "Find a game",
            [
                new ListRow("Every platform", $"{platforms.Count} known here", "Everything RomM holds."),
                .. platforms.Select(platform => new ListRow(
                    platform.Label,
                    platform.Folder ?? "no folder",
                    platform.Folder is null
                        ? "This platform has no RetroBat folder, so its games cannot be installed."
                        : null)),
            ],
            index => ScreenCommand.Push(
                new BrowseViewModel(session, connect, index == 0 ? null : platforms[index - 1])),
            acceptLabel: "Show these games",
            backLabel: "Back")
        {
            EmptyMessage = "No platforms known yet. Sync or query a set once and they appear.",
        };
    }

    public event EventHandler? Invalidated;

    /// <summary>Everything the renderer draws, read once so it cannot change mid-draw.</summary>
    /// <remarks>
    /// One value rather than six fields, for the reason <c>SyncSnapshot</c> and
    /// <c>ListScreen.ListState</c> both give: a fetch finishes on the thread pool while the
    /// drawing thread is inside <c>Handle</c> or <c>Hints</c>, and written separately the page
    /// and the cursor into it could be read a page apart.
    /// </remarks>
    public BrowseState State => _state;

    public string Title
    {
        get
        {
            var state = _state;

            var what = state.Page?.Source == BrowseSource.ThisDevice
                ? "Games on this device"
                : state.PlatformLabel ?? "Every platform";

            return state.Search is { } term ? $"{what}: '{term}'" : what;
        }
    }

    /// <summary>The rows, which are never more than one page of them.</summary>
    public IReadOnlyList<ListRow> Rows =>
    [
        .. (_state.Page?.Games ?? []).Select(game => ToRow(game, _state.PlatformLabel is not null)),
        .. EndRow(_state),
    ];

    public int Cursor => _state.Cursor;

    /// <summary>
    /// Ordinary rows, not reading rows, and the renderer is told rather than assuming.
    /// </summary>
    /// <remarks>
    /// <b>A browse row is a name, a place and one short line</b>, which is the sets list's shape
    /// and not the problems list's: the reading row is 122px with a three-line wrapped sentence
    /// in it, and drawing fifty one-liners that way costs 44px a row for nothing.
    /// <para>
    /// It also has to be said here rather than in <c>ScreenView</c>, because the count of rows
    /// and the height of one are the same decision. Told separately, this screen computed a
    /// window of eight and was drawn at the reading height, which overflows the display by
    /// exactly the margin <see cref="ListWindow.ReadingCapacity"/> exists to avoid. That is
    /// 7b-2b's round-four defect, reintroduced on the screen beside it.
    /// </para>
    /// </remarks>
    public bool Reading => false;

    /// <summary>Which slice is on screen, decided here rather than in the renderer.</summary>
    public ListView Window => ListWindow.Compute(_state.Cursor, Rows.Count, ListWindow.CapacityFor(Reading));

    public bool IsLoading => _state.IsLoading;

    /// <summary>What the body says while a page is on its way.</summary>
    /// <remarks>
    /// Here rather than on the renderer, because whether there is a server to ask is this
    /// screen's answer and a hardcoded string in <c>ScreenView</c> is outside every sweep that
    /// checks these. The ellipsis is the rule <c>ListScreen</c> defaults to.
    /// </remarks>
    public string LoadingMessage => _state.Offline
        ? "Reading what is on this device..."
        : "Asking RomM...";

    /// <summary>What is being shown and where it came from, in one line above the rows.</summary>
    public string Note
    {
        get
        {
            var state = _state;

            // Nothing while a page is on its way. The renderer draws the loading message in the
            // body, so saying it here as well put "Asking RomM" on screen twice, once centred
            // and once left, which reads as a screen that has drawn itself wrong.
            if (state.IsLoading)
            {
                return string.Empty;
            }

            if (state.Page is not { } page)
            {
                return "Nothing to show.";
            }

            var counted = page.Total == 0
                ? "nothing"
                : string.Create(
                    CultureInfo.CurrentCulture,
                    $"{page.Offset + 1:N0} to {page.Offset + page.Games.Count:N0} of {page.Total:N0}");

            // Which of the two it is showing, always, rather than only when it degraded. A
            // person who never sees the online form has no way to tell the offline one apart
            // from a library that has shrunk.
            var source = page.Source == BrowseSource.Library
                ? "RomM's library"
                : "the games on this device";

            return page.Problem is { } problem
                ? $"Showing {source}, {counted}. RomM could not be reached: {problem}"
                : $"Showing {source}, {counted}.";
        }
    }

    public IReadOnlyList<FooterHint> Hints
    {
        get
        {
            var state = _state;
            var hints = new List<FooterHint>();

            if (!state.IsLoading && state.Page is { } page && state.Cursor >= 0 && state.Cursor < page.Games.Count)
            {
                hints.Add(new FooterHint(NavAction.Accept, "Open this game"));
            }

            hints.Add(new FooterHint(NavAction.Start, "Search"));

            // No platform verb. Choosing one is how this screen is reached now, so a picker
            // here pops back to the screen already underneath and is a second Back button
            // wearing a different label. Found from the couch.
            hints.Add(new FooterHint(NavAction.Back, _state.PlatformLabel is null ? "Back" : "Another platform"));

            return hints;
        }
    }

    public ScreenCommand Handle(NavAction action)
    {
        var state = _state;

        // Nothing moves while a page is on its way. A held d-pad repeats several times a second
        // and a page takes about 280 ms, so every press between the request and its answer used
        // to start another one: half a dozen fetches in flight, landing out of order, each
        // resetting the cursor to the top of whatever arrived last. From the couch that is the
        // selection snapping backwards, which is what a hands-on pass called rubberbanding.
        //
        // Swallowed rather than queued. A person holding the pad wants the list to keep moving,
        // not to replay six presses into a page they are no longer looking at, and the fetch
        // they are waiting for is already running.
        //
        // This is the cursor half only. Refusing to start a second fetch is Fetch's own job,
        // because listing the actions here left the search path out: Start opens the keyboard,
        // whose typed callback fetches with no check at all, so a search submitted while a page
        // was still in flight raced it and the later answer won regardless of which was asked
        // for second. #118.
        if (state.IsLoading && action is NavAction.Up or NavAction.Down or NavAction.Accept)
        {
            return ScreenCommand.Stay;
        }

        switch (action)
        {
            case NavAction.Up when state.Cursor > 0:
                Publish(current => current with { Cursor = current.Cursor - 1 });
                return ScreenCommand.Stay;

            case NavAction.Up when state.Page is { Offset: > 0 } page:
                // Past the top, so back a page. The cursor lands on the last row of it, which is
                // where the eye already is.
                Fetch(Math.Max(0, page.Offset - BrowseService.PageSize), landOnLast: true);
                return ScreenCommand.Stay;

            // Bounded by the rows drawn, not by the games in the page. The end-of-list row is a
            // row, and bounding on Games.Count left it visible and unreachable, which is this
            // repository's recurring shape: a rule enforced in one place and broken in the place
            // beside it. Accept still refuses it, because that arm asks about the games.
            case NavAction.Down when state.Cursor + 1 < Rows.Count:
                Publish(current => current with { Cursor = current.Cursor + 1 });
                return ScreenCommand.Stay;

            case NavAction.Down when state.Page is { IsLastPage: false } more:
                Fetch(more.Offset + more.Games.Count);
                return ScreenCommand.Stay;

            case NavAction.Down when state.Page is { IsLastPage: true, Offset: 0, Games.Count: > 0 }:
                // A library that fits in one page wraps, because there is no paging to undo and
                // a list that will not move at all reads as broken.
                Publish(current => current with { Cursor = 0 });
                return ScreenCommand.Stay;

            case NavAction.Accept when state.Page is { } opened
                && state.Cursor >= 0 && state.Cursor < opened.Games.Count:
                return ScreenCommand.Push(BrowseScreens.Detail(
                    _session,
                    opened.Games[state.Cursor],
                    _connect,
                    Reload));

            case NavAction.Start:
                return ScreenCommand.Push(SearchKeyboard());

            case NavAction.Back:
                return ScreenCommand.Pop;

            default:
                return ScreenCommand.Stay;
        }
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

        // Under the same lock the fetch opens it under. This runs on the thread that draws and
        // a fetch still unwinding reads the same field from the pool.
        lock (_gate)
        {
            _connection?.Dispose();
            _connection = null;
        }
    }

    /// <summary>Re-reads the current page, because a screen above this one installed or removed.</summary>
    private void Reload() => Fetch(_state.Page?.Offset ?? 0);

    /// <summary>
    /// The on-screen keyboard, which is EmulationStation's own and needed no third layer.
    /// </summary>
    /// <remarks>
    /// 7b-2a transcribed all four faces of all three ES layouts from upstream's source, so the
    /// "third layer" the 7b-2 brief left for this stage does not exist. It is reused unchanged,
    /// typing in whatever language ES is set to.
    /// </remarks>
    private OnScreenKeyboard SearchKeyboard() =>
        new OnScreenKeyboard(
            "Search RomM",
            "Type part of a game's name, then press Start.",
            _state.Search ?? string.Empty,
            typed =>
            {
                // Back to the first page, because a term that kept the offset would open at row
                // 400 of a result that has nine.
                Publish(current => current with { Search = Blank(typed) });
                Fetch(0);
                return new TypedResult(null);
            },
            _session.EmulationStationLanguage());

    /// <summary>
    /// Fetches one page and replaces what is held.
    /// </summary>
    /// <remarks>
    /// <b>Replaces, never appends.</b> That single word is the whole of "nothing holds more than
    /// one page", and it is the thing an accumulating list would break silently: a screen that
    /// concatenated would look identical for the first few pages and hold a library by the end.
    /// </remarks>
    /// <remarks>
    /// <b>One fetch at a time, refused here rather than at the presses that start one.</b> The
    /// guard used to name three navigation actions in <c>Handle</c>, which the search path went
    /// around entirely, and two fetches racing meant the later answer won whichever was asked
    /// for second: on a slow library a stale page overwrote a search result, leaving the
    /// previous list under a title naming the search term. Refusing at the one place that
    /// starts the work covers every route into it, including the ones not yet written. #118.
    /// </remarks>
    private void Fetch(int offset, bool landOnLast = false)
    {
        // Tested and set together under the lock, so two callers cannot both read "not running"
        // and both start. Marked before the work rather than inside it, for the same reason.
        lock (_gate)
        {
            if (_fetching)
            {
                return;
            }

            _fetching = true;
        }

        Publish(current => current with { IsLoading = true });

        _ = Task.Run(
            async () =>
            {
                try
                {
                    var connection = Connection();

                    // Published before the await so the loading line names where the rows are
                    // coming from. Saying "Asking RomM" over a read of the local store is the
                    // one thing the Note line goes out of its way to get right.
                    Publish(current => current with { Offline = connection is null });

                    var page = await _service
                        .PageAsync(
                            connection,
                            offset,
                            _state.PlatformId,
                            _state.Folder,
                            _state.Search,
                            _load.Token)
                        .ConfigureAwait(false);

                    Publish(current => current with
                    {
                        Page = page,
                        IsLoading = false,
                        Cursor = page.Games.Count == 0
                            ? -1
                            : landOnLast ? page.Games.Count - 1 : 0,
                    });
                }
                catch (OperationCanceledException)
                {
                    // Left before it finished, which is the point of it being cancellable.
                }
                catch (Exception ex)
                {
                    // Broad, for the reason ListScreen's loader is: this talks to a server, a
                    // disk and a database, and the alternative is drawing an empty library and
                    // telling somebody that is what they have.
                    Publish(current => current with
                    {
                        IsLoading = false,
                        Page = new BrowsePage(BrowseSource.ThisDevice, [], 0, 0, true, ex.Message),
                        Cursor = -1,
                    });
                }
                finally
                {
                    // Every exit, the cancelled one included. A guard left set by a path that
                    // did not clear it is a screen that never fetches again, which from the
                    // couch is indistinguishable from a hang.
                    lock (_gate)
                    {
                        _fetching = false;
                    }
                }
            },
            CancellationToken.None);
    }

    /// <summary>
    /// The connection, opened once and kept, or null when there is nothing to open.
    /// </summary>
    /// <remarks>
    /// Kept rather than opened per page, because paging is the thing a person does repeatedly
    /// here and a fresh handler per press pays the connect cost every time. Null is an ordinary
    /// answer: <see cref="BrowseService"/> browses this device instead.
    /// </remarks>
    private RomMConnection? Connection()
    {
        // Under the lock, because this runs on the thread pool and Dispose reads the same field
        // from the thread that draws. Two fetches with no connection yet could both open one
        // and one RomMConnection was dropped unclosed, holding a handler and its sockets. #118.
        lock (_gate)
        {
            _connection ??= UiConnection.Open(_session, _connect);
            return _connection;
        }
    }

    /// <summary>
    /// One game as a row: what it is, and whether it is here.
    /// </summary>
    /// <remarks>
    /// <b>The second column is where it is, not merely whether.</b> One ROM in two folders is
    /// legitimate, it costs twice the room, and a row that said only "here" would leave the
    /// doubling invisible, which is what made it a crash nobody could explain rather than a
    /// state somebody could see. The bytes are on the detail screen, where there is room to say
    /// why there are two of them.
    /// <para>
    /// <b>The title is the label and the file name is the line under it, on every row.</b> Both
    /// are needed and both were measured: every arcade file name is a romset code, so the title
    /// has to be the label, and 69 megadrive and 67 psx titles are shared by two or more rows,
    /// so the file name has to be under it. Showing the file name only where there were no tags
    /// to parse made the rule change platform to platform and read as arbitrary.
    /// <see cref="BrowseGame.Release"/> holds the argument and the numbers.
    /// </para>
    /// </remarks>
    /// <param name="scoped">
    /// True when the whole list is one platform, which is when naming it on every row is a
    /// column of the same word. The header already says which platform it is.
    /// </param>
    private static ListRow ToRow(BrowseGame game, bool scoped) => new(
        game.DisplayName,
        game.IsHere ? "here: " + string.Join(", ", game.Folders) : "not here",

        // The file name leads, because it is the half that tells two rows with one title apart
        // and a trimmed line loses its end: what goes is a translation credit rather than the
        // region and revision, which sit early. Size follows and is a press away besides.
        $"{game.Release}  ·  {ByteSize.Format(game.SizeBytes)}"
            + (scoped ? string.Empty : $"  ·  {game.PlatformSlug}")
            + (game.Sets.Count > 0 ? $"  ·  in {string.Join(", ", game.Sets)}" : string.Empty),
        false);

    /// <summary>
    /// The row that says the list has ended, on the last page only.
    /// </summary>
    /// <remarks>
    /// Because stopping silently is what a couch reads as a frozen screen, which is the failure
    /// both previous stages found repeatedly. A one-page result gets no such row: the cursor
    /// wraps there and nothing has ended.
    /// </remarks>
    private static IEnumerable<ListRow> EndRow(BrowseState state)
    {
        if (state.Page is { IsLastPage: true, Offset: > 0 } page && page.Games.Count > 0)
        {
            yield return new ListRow(
                "End of the list",
                null,
                "Nothing past here. Search, or narrow to one platform, to find something else.",
                false);
        }
    }

    private static string? Blank(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>Applies a change under the lock, then redraws off whatever thread did the work.</summary>
    private void Publish(Func<BrowseState, BrowseState> change)
    {
        lock (_gate)
        {
            _state = change(_state);
        }

        Invalidated?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Everything a browse screen draws, as one value.</summary>
/// <param name="Page">The one page held. Null only before the first fetch lands.</param>
/// <param name="Offline">
/// True once a fetch found nothing to connect with, which is what the loading line words.
/// </param>
public sealed record BrowseState(
    BrowsePage? Page,
    bool IsLoading,
    string? Search,
    string? PlatformId,
    string? Folder,
    string? PlatformLabel,
    int Cursor,
    bool Offline);
