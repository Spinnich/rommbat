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
public sealed class BrowseViewModel : IScreen, ILiveScreen, IDisposable
{
    private readonly InstallSession _session;
    private readonly Func<Uri, RomMConnection>? _connect;
    private readonly BrowseService _service;
    private readonly CancellationTokenSource _load = new();
    private readonly Lock _gate = new();

    private volatile BrowseState _state = new(null, true, null, null, null, 0);
    private RomMConnection? _connection;
    private bool _disposed;

    /// <param name="connect">
    /// How the screen reaches the server. Taken so a test can stand a stub in its place, the way
    /// every other screen that talks to RomM already does.
    /// </param>
    public BrowseViewModel(InstallSession session, Func<Uri, RomMConnection>? connect = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        _connect = connect;
        _service = new BrowseService(session);

        Fetch(0);
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
                : "Browse RomM";

            return state.Search is { } term ? $"{what}: '{term}'" : what;
        }
    }

    /// <summary>The rows, which are never more than one page of them.</summary>
    public IReadOnlyList<ListRow> Rows =>
    [
        .. (_state.Page?.Games ?? []).Select(ToRow),
        .. EndRow(_state),
    ];

    public int Cursor => _state.Cursor;

    /// <summary>Which slice is on screen, decided here rather than in the renderer.</summary>
    public ListView Window => ListWindow.Compute(_state.Cursor, Rows.Count, ListWindow.Capacity);

    public bool IsLoading => _state.IsLoading;

    /// <summary>What is being shown and where it came from, in one line above the rows.</summary>
    public string Note
    {
        get
        {
            var state = _state;

            if (state.IsLoading)
            {
                return "Asking RomM.";
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

            if (_session.Store.PlatformMap.List().Count > 0)
            {
                hints.Add(new FooterHint(NavAction.Extra, "Narrow to one platform"));
            }

            hints.Add(new FooterHint(NavAction.Back, "Back"));

            return hints;
        }
    }

    public ScreenCommand Handle(NavAction action)
    {
        var state = _state;

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

            case NavAction.Extra when _session.Store.PlatformMap.List().Count > 0:
                return ScreenCommand.Push(PlatformPicker());

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
        _connection?.Dispose();
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
    /// Narrowing to one platform, from the mapping table rather than from the server.
    /// </summary>
    /// <remarks>
    /// <c>SyncSetService.PlatformsKnownHere</c>, which is what the set editor's picker already
    /// uses, so the two offer the same platforms and a platform absent from one is absent from
    /// both. It also works offline, which the online-only alternative would not.
    /// </remarks>
    private ListScreen PlatformPicker()
    {
        var platforms = new SyncSetService(_session).PlatformsKnownHere();

        return new ListScreen(
            "Which platform?",
            [
                new ListRow("Every platform", null, "Everything RomM holds."),
                .. platforms.Select(platform => new ListRow(
                    platform.Label,
                    platform.Folder ?? "no folder",
                    platform.Folder is null
                        ? "This platform has no RetroBat folder, so its games cannot be installed."
                        : null)),
            ],
            index =>
            {
                var chosen = index == 0 ? null : platforms[index - 1];

                Publish(current => current with
                {
                    PlatformId = chosen?.PlatformId.ToString(CultureInfo.InvariantCulture),
                    Folder = chosen?.Folder,
                });

                Fetch(0);
                return ScreenCommand.Pop;
            },
            acceptLabel: "Show these",
            backLabel: "Back");
    }

    /// <summary>
    /// Fetches one page and replaces what is held.
    /// </summary>
    /// <remarks>
    /// <b>Replaces, never appends.</b> That single word is the whole of "nothing holds more than
    /// one page", and it is the thing an accumulating list would break silently: a screen that
    /// concatenated would look identical for the first few pages and hold a library by the end.
    /// </remarks>
    private void Fetch(int offset, bool landOnLast = false)
    {
        Publish(current => current with { IsLoading = true });

        _ = Task.Run(
            async () =>
            {
                try
                {
                    var page = await _service
                        .PageAsync(
                            Connection(),
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
        if (_connection is not null)
        {
            return _connection;
        }

        var attempt = _session.Authenticate();

        if (attempt.Connection is null)
        {
            return null;
        }

        var origin = _session.Store.Device.Read()?.ServerOrigin;

        if (_connect is not null && origin is not null)
        {
            attempt.Connection.Dispose();
            _connection = _connect(origin);
        }
        else
        {
            _connection = attempt.Connection;
        }

        return _connection;
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
    /// </remarks>
    private static ListRow ToRow(BrowseGame game) => new(
        game.DisplayName,
        game.IsHere ? "here: " + string.Join(", ", game.Folders) : "not here",
        $"{game.PlatformSlug}  {ByteSize.Format(game.SizeBytes)}"
            + (game.Sets.Count > 0 ? $"  in {string.Join(", ", game.Sets)}" : string.Empty),
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
public sealed record BrowseState(
    BrowsePage? Page,
    bool IsLoading,
    string? Search,
    string? PlatformId,
    string? Folder,
    int Cursor);
