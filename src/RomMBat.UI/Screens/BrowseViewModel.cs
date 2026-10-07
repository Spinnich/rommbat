using System.Globalization;
using RomM.Client;
using RomM.Client.Catalog;
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
/// <b>Nothing here holds more than one page.</b> The catalog is never mirrored wholesale, and
/// <c>RomRow</c> and <c>RomPager</c> both restate it: a 96k library is about 384 pages and the
/// longest description in a 5,000-row sample is 11,719 characters. Moving past the bottom fetches
/// the next offset and <b>replaces</b> what is held; moving past the top fetches the previous one.
/// A test asserts the row count never exceeds the page size across several pages, because this is
/// the rule most likely to be broken here and it breaks silently and only at scale.
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
/// <b>Moving through it is ES's game list, button for button</b> (RB-421, RB-422), because a
/// library of a hundred thousand games is not found by scrolling. L1 and R1 move one screen,
/// L2 and R2 step to the previous or next platform, the left face button searches on the server,
/// and Select opens VIEW OPTIONS: the search again, a jump to a letter through RomM's own letter
/// index, the sort order, and RomM's filters through the set editor's own pickers. The search,
/// sort and filter belong to the list rather than the platform, so they hold across L2 and R2.
/// Ruled with Spinnich.
/// </para>
/// <para>
/// <b>No cover art.</b> Text rows, like every other screen. Art is its own stage with its own
/// measurement, and "just the selected row" is the version of it that gets reintroduced by
/// accident.
/// </para>
/// </remarks>
public sealed class BrowseViewModel : IScreen, IWindowedScreen, ILiveScreen, IActionScreen, IDisposable
{
    private readonly InstallSession _session;
    private readonly Func<Uri, RomMConnection>? _connect;
    private readonly BrowseService _service;
    private readonly CancellationTokenSource _load = new();
    private readonly Lock _gate = new();

    private volatile BrowseState _state = new(null, true, new BrowseView(), null, null, null, 0, false);
    private RomMConnection? _connection;
    private bool _disposed;

    /// <summary>What L2 and R2 step through: every platform first, then the picker's list.</summary>
    private readonly IReadOnlyList<PlatformOption?> _platforms;

    /// <summary>The last letter index read, and the view it was read for.</summary>
    /// <remarks>
    /// One request per view rather than per press: stepping through letters asks nothing, and
    /// a change of platform, search, sort or filter is a different view and reads again.
    /// </remarks>
    private (object Key, BrowseLetters Letters)? _letters;

    /// <summary>The install, for the filter pickers VIEW OPTIONS opens.</summary>
    internal InstallSession Session => _session;

    /// <summary>How those pickers reach the server, which is how this screen does.</summary>
    internal Func<Uri, RomMConnection>? Connect => _connect;

    /// <summary>The facet values, kept so a second visit to the filters does not ask again.</summary>
    internal IReadOnlyDictionary<string, IReadOnlyList<string>>? FacetValues { get; set; }

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
        _platforms = [null, .. new SyncSetService(session).PlatformsKnownHere()];

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

            return state.View.Search is { } term ? $"{what}: '{term}'" : what;
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
    /// and the height of one are the same decision. Told separately, this screen computes a
    /// window of eight and is drawn at the reading height, which overflows the display by
    /// exactly the margin <see cref="ListWindow.ReadingCapacity"/> exists to avoid.
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
            // body, so saying it here as well put "Asking RomM" on screen twice, once centered
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

            // The order and the filter, but only where they apply: this device's page holds no
            // release dates, ratings or facets, so it is by name whatever was asked for.
            var shaped = page.Source == BrowseSource.Library
                ? (state.View.Order == BrowseOrder.NameAscending ? string.Empty : ", " + OrderText(state.View.Order).ToLowerInvariant())
                    + (state.View.Filtered ? ", filtered" : string.Empty)
                : state.View.Order != BrowseOrder.NameAscending || state.View.Filtered
                    ? ", by name and unfiltered until RomM can be read"
                    : string.Empty;

            // Not "could not be reached": a page RomM answered and refused falls back too.
            return page.Problem is { } problem
                ? $"Showing {source}, {counted}{shaped}. RomM's library could not be read: {problem}"
                : $"Showing {source}, {counted}{shaped}.";
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

            hints.AddRange(ScreenAction.Hints(Actions));

            // No platform verb. Choosing one is how this screen is reached now, so a picker
            // here pops back to the screen already underneath and is a second Back button
            // wearing a different label. Found from the couch.
            hints.Add(new FooterHint(NavAction.Back, _state.PlatformLabel is null ? "Back" : "Another platform"));

            return hints;
        }
    }

    /// <summary>
    /// Search, on the left face button where EmulationStation's game list has it (RB-421), and
    /// the view options on Select, where ES has its own (RB-422).
    /// </summary>
    public IReadOnlyList<ScreenAction> Actions =>
    [
        new ScreenAction("Search", () => ScreenCommand.Push(SearchKeyboard())) { Shortcut = NavAction.Alternate },
        new ScreenAction("View options", () => ScreenCommand.Push(new ViewOptionsScreen(this))) { Shortcut = NavAction.Options },
    ];

    /// <summary>How each order reads on screen.</summary>
    internal static string OrderText(BrowseOrder order) => order switch
    {
        BrowseOrder.NameDescending => "Name, Z to A",
        BrowseOrder.ReleaseNewest => "Release date, newest first",
        BrowseOrder.ReleaseOldest => "Release date, oldest first",
        BrowseOrder.RatingHighest => "Rating, highest first",
        _ => "Name, A to Z",
    };

    public ScreenCommand Handle(NavAction action)
    {
        var state = _state;

        // Nothing moves while a page is on its way. A held d-pad repeats several times a second
        // and a page takes about 280 ms, so letting every press between the request and its
        // answer start another one puts half a dozen fetches in flight, landing out of order,
        // each resetting the cursor to the top of whatever arrived last. From the couch that is the
        // selection snapping backwards, which is what a hands-on pass called rubberbanding.
        //
        // Swallowed rather than queued. A person holding the pad wants the list to keep moving,
        // not to replay six presses into a page they are no longer looking at, and the fetch
        // they are waiting for is already running.
        //
        // This is the cursor half only. Refusing to start a second fetch is Fetch's own job,
        // because listing the actions here left the search path out: search opens the keyboard,
        // whose typed callback fetches with no check at all, so a search submitted while a page
        // was still in flight raced it and the later answer won regardless of which was asked
        // for second. #118.
        if (state.IsLoading
            && action is NavAction.Up or NavAction.Down or NavAction.Accept
                or NavAction.PageUp or NavAction.PageDown or NavAction.PreviousGroup or NavAction.NextGroup)
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
                Fetch(Math.Max(0, page.Offset - BrowseService.PageSize), landAt: page.Offset - 1);
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

            case NavAction.PageUp:
                Move(-ListWindow.Capacity);
                return ScreenCommand.Stay;

            case NavAction.PageDown:
                Move(ListWindow.Capacity);
                return ScreenCommand.Stay;

            case NavAction.PreviousGroup:
                StepPlatform(-1);
                return ScreenCommand.Stay;

            case NavAction.NextGroup:
                StepPlatform(1);
                return ScreenCommand.Stay;

            case NavAction.Back:
                return ScreenCommand.Pop;

            default:
                return ScreenCommand.Stay;
        }
    }

    /// <summary>
    /// One screen up or down, fetching the neighboring page when the move leaves this one.
    /// </summary>
    /// <remarks>
    /// <b>Stops at both ends, as a row step does here.</b> The first row of the library and the
    /// end-of-list row are where a held L1 or R1 comes to rest. Measured from the row under the
    /// cursor, so a page fetched for the move opens on the row the press was aimed at, not at
    /// its top.
    /// </remarks>
    private void Move(int rows)
    {
        var state = _state;

        if (state.Page is not { Games.Count: > 0 } page)
        {
            return;
        }

        var target = state.Cursor + rows;

        if (target >= 0 && target < Rows.Count)
        {
            Publish(current => current with { Cursor = target });
        }
        else if (rows > 0 && !page.IsLastPage)
        {
            Fetch(page.Offset + page.Games.Count, landAt: page.Offset + target);
        }
        else if (rows < 0 && page.Offset > 0)
        {
            Fetch(Math.Max(0, page.Offset - BrowseService.PageSize), landAt: Math.Max(0, page.Offset + target));
        }
        else
        {
            Publish(current => current with { Cursor = rows > 0 ? Rows.Count - 1 : 0 });
        }
    }

    /// <summary>
    /// The previous or next platform, in the order the platform list shows them, wrapping.
    /// </summary>
    /// <remarks>
    /// As L2 and R2 change system in ES's game list (RB-421), and through the same list
    /// <see cref="Start"/> offers, every platform first. The view comes along, so a search for one
    /// title can be carried across platforms. The platform changes only if the fetch starts,
    /// which is what keeps the title from naming a platform whose games are not the ones shown.
    /// </remarks>
    private void StepPlatform(int step)
    {
        var current = _state.PlatformId;
        var index = Math.Max(
            0,
            _platforms.ToList().FindIndex(option =>
                option?.PlatformId.ToString(CultureInfo.InvariantCulture) == current));

        var next = _platforms[(index + step + _platforms.Count) % _platforms.Count];

        Fetch(0, change: state => state with
        {
            PlatformId = next?.PlatformId.ToString(CultureInfo.InvariantCulture),
            Folder = next?.Folder,
            PlatformLabel = next?.Label,
        });
    }

    /// <summary>The row under the cursor as an offset into the whole list, for the letter it sits under.</summary>
    internal int Position
    {
        get
        {
            var state = _state;
            return state.Page is { } page
                ? page.Offset + Math.Clamp(state.Cursor, 0, Math.Max(0, page.Games.Count - 1))
                : 0;
        }
    }

    /// <summary>
    /// Where each first character begins in what is shown, read once per view.
    /// </summary>
    /// <remarks>
    /// Asked of the place the page came from: an offline page's letters come from this device,
    /// because RomM's offsets mean nothing in a list RomM did not make.
    /// </remarks>
    internal async Task<BrowseLetters> LettersAsync(CancellationToken cancellationToken)
    {
        var state = _state;
        var source = state.Page?.Source ?? BrowseSource.ThisDevice;
        var key = (state.PlatformId, state.View, source);

        if (_letters is { } cached && Equals(cached.Key, key))
        {
            return cached.Letters;
        }

        var letters = await _service
            .LettersAsync(Connection(), source, state.PlatformId, state.Folder, state.View, cancellationToken)
            .ConfigureAwait(false);

        // Kept only once it worked, so a refused read is asked again on the next visit.
        if (letters.Problem is null)
        {
            _letters = (key, letters);
        }

        return letters;
    }

    /// <summary>Lands on the first game under a letter, fetching its page unless it is this one.</summary>
    internal void JumpTo(BrowseLetter letter)
    {
        ArgumentNullException.ThrowIfNull(letter);

        if (_state.Page is { } page && letter.Offset >= page.Offset && letter.Offset < page.Offset + page.Games.Count)
        {
            Publish(current => current with { Cursor = letter.Offset - page.Offset });
            return;
        }

        Fetch(letter.Offset, landAt: letter.Offset);
    }

    /// <summary>Reads the list again in another order, from its first row.</summary>
    internal void ApplyOrder(BrowseOrder order) =>
        Fetch(0, change: state => state with { View = state.View with { Order = order } });

    /// <summary>Narrows the list by RomM's filters, from its first row. Null clears them.</summary>
    internal void ApplyFilter(CatalogFilter? filter) =>
        Fetch(0, change: state => state with { View = state.View with { Filter = filter is { IsEmpty: false } ? filter : null } });

    public void Dispose()
    {
        // Canceled, never disposed: a request still unwinding can register on this token.
        _load.Cancel();

        // Under the same lock the fetch opens it under. This runs on the thread that draws and
        // a fetch still unwinding reads the same field from the pool. The flag is set inside it
        // too, so a fetch reaching Connection() afterwards cannot open one nothing will close.
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _connection?.Dispose();
            _connection = null;
        }
    }

    /// <summary>Re-reads the current page, because a screen above this one installed or removed.</summary>
    /// <remarks>
    /// The letters are dropped too. Offline they are this device's, so a removal moves every
    /// letter after it, and a cached index jumped a row past the letter (R1.1 on #500).
    /// </remarks>
    internal void Reload()
    {
        _letters = null;
        Fetch(_state.Page?.Offset ?? 0, landAt: Position);
    }

    /// <summary>
    /// The on-screen keyboard, which is EmulationStation's own and needed no third layer.
    /// </summary>
    /// <remarks>
    /// All four faces of all three ES layouts are transcribed from upstream's source, so no
    /// third layer is needed. It is reused unchanged, typing in whatever language ES is set to.
    /// </remarks>
    internal OnScreenKeyboard SearchKeyboard() =>
        new OnScreenKeyboard(
            "Search RomM",
            "Type part of a game's name, then press Start.",
            _state.View.Search ?? string.Empty,
            typed =>
            {
                // Back to the first page, because a term that kept the offset would open at row
                // 400 of a result that has nine. Changed with the fetch rather than before it,
                // so a term typed while a page is still on its way is not shown over that page.
                Fetch(0, change: current => current with { View = current.View with { Search = Blank(typed) } });
                return new TypedResult(null);
            },
            _session.EmulationStationLanguage())
        {
            // Empty is how a search is cleared, back to the whole platform.
            AllowEmpty = true,
        };

    /// <summary>
    /// Fetches one page and replaces what is held.
    /// </summary>
    /// <remarks>
    /// <b>Replaces, never appends.</b> That single word is the whole of "nothing holds more than
    /// one page", and it is the thing an accumulating list would break silently: a screen that
    /// concatenated would look identical for the first few pages and hold a library by the end.
    /// </remarks>
    /// <remarks>
    /// <b>One fetch at a time, refused here rather than at the presses that start one.</b> A
    /// guard naming navigation actions in <c>Handle</c> is one the search path goes around
    /// entirely, and two fetches racing means the later answer wins whichever was asked for
    /// second: on a slow library a stale page overwrites a search result, leaving the previous
    /// list under a title naming the search term. Refusing at the one place that
    /// starts the work covers every route into it, including the ones not yet written. #118.
    /// </remarks>
    /// <param name="landAt">
    /// The row to land on, as an offset into the whole list, or null for the page's first.
    /// </param>
    /// <param name="change">
    /// What the fetch is for: another platform, search, order or filter. Applied only if the
    /// fetch starts, under the same lock that decides it, so a refused fetch changes nothing and
    /// the title never names a view whose rows are not the ones shown.
    /// </param>
    private void Fetch(int offset, int? landAt = null, Func<BrowseState, BrowseState>? change = null)
    {
        BrowseState asked;

        // Tested and set together under the lock, so two callers cannot both read "not running"
        // and both start. Marked before the work rather than inside it, for the same reason.
        lock (_gate)
        {
            if (_fetching)
            {
                return;
            }

            _fetching = true;
            _state = (change?.Invoke(_state) ?? _state) with { IsLoading = true };
            asked = _state;
        }

        Invalidated?.Invoke(this, EventArgs.Empty);

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
                            asked.PlatformId,
                            asked.Folder,
                            asked.View,
                            _load.Token)
                        .ConfigureAwait(false);

                    Publish(current => current with
                    {
                        Page = page,
                        IsLoading = false,
                        Cursor = page.Games.Count == 0
                            ? -1
                            : Math.Clamp((landAt ?? page.Offset) - page.Offset, 0, page.Games.Count - 1),
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
                    // Every exit, the canceled one included. A guard left set by a path that
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
    internal RomMConnection? Connection()
    {
        // Under the lock, because this runs on the thread pool and Dispose reads the same field
        // from the thread that draws. Two fetches with no connection yet could both open one
        // and one RomMConnection was dropped unclosed, holding a handler and its sockets. #118.
        lock (_gate)
        {
            // Null once the screen is gone, which the fetch already handles as offline. Opening
            // one here would assign it to a field Dispose has already emptied. #126.
            if (_disposed)
            {
                return null;
            }

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
/// <param name="View">The search, sort and filter, which hold across a change of platform.</param>
public sealed record BrowseState(
    BrowsePage? Page,
    bool IsLoading,
    BrowseView View,
    string? PlatformId,
    string? Folder,
    string? PlatformLabel,
    int Cursor,
    bool Offline);
