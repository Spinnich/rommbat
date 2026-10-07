using RomM.Client.Catalog;
using RomMBat.Core.Sets;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using RomMBat.UI.Input;
using RomMBat.UI.Screens;
using RomMBat.UI.Shell;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Moving through a long library the way ES's game list does: a screen at a time, a platform at
/// a time, a letter at a time (RB-421, RB-422). #492.
/// </summary>
public sealed partial class BrowseScreenTests
{
    // ------------------------------------------------------------------ L1 and R1

    [Fact]
    public async Task R1_moves_one_screen_and_crosses_into_the_next_page_on_the_row_it_aimed_at()
    {
        using var stub = Library(120);
        Pair();
        using var browse = new BrowseViewModel(_session, Connect(stub));

        await Settled(browse);

        for (var press = 0; press < 6; press++)
        {
            browse.Handle(NavAction.PageDown);
        }

        Assert.Equal(0, browse.State.Page!.Offset);
        Assert.Equal(6 * ListWindow.Capacity, browse.Cursor);

        // Row 56 of the library is on the next page, so the press fetches it and lands there
        // rather than on that page's first row.
        browse.Handle(NavAction.PageDown);
        await Settled(browse);

        Assert.Equal(BrowseService.PageSize, browse.State.Page!.Offset);
        Assert.Equal((7 * ListWindow.Capacity) - BrowseService.PageSize, browse.Cursor);
        Assert.Equal("Game 0057", browse.Rows[browse.Cursor].Label);
    }

    [Fact]
    public async Task L1_back_across_a_page_lands_on_the_row_it_aimed_at()
    {
        using var stub = Library(120);
        Pair();
        using var browse = new BrowseViewModel(_session, Connect(stub));

        await Settled(browse);
        await PageDown(browse);

        browse.Handle(NavAction.Down);
        browse.Handle(NavAction.Down);
        Assert.Equal("Game 0053", browse.Rows[browse.Cursor].Label);

        browse.Handle(NavAction.PageUp);
        await Settled(browse);

        Assert.Equal(0, browse.State.Page!.Offset);
        Assert.Equal("Game 0045", browse.Rows[browse.Cursor].Label);
    }

    [Fact]
    public async Task L1_and_R1_stop_at_both_ends_of_the_library()
    {
        using var stub = Library(60);
        Pair();
        using var browse = new BrowseViewModel(_session, Connect(stub));

        await Settled(browse);

        var served = stub.RomPagesServed;
        browse.Handle(NavAction.PageUp);

        Assert.Equal(0, browse.Cursor);
        Assert.Equal(served, stub.RomPagesServed);

        await PageDown(browse);

        for (var press = 0; press < 5; press++)
        {
            browse.Handle(NavAction.PageDown);
        }

        // At rest on the end-of-list row, and never back at the top.
        Assert.Equal(BrowseService.PageSize, browse.State.Page!.Offset);
        Assert.Equal(browse.Rows.Count - 1, browse.Cursor);
        Assert.Equal("End of the list", browse.Rows[browse.Cursor].Label);
    }

    [Fact]
    public void L1_and_R1_page_an_ordinary_list_by_one_window_and_clamp()
    {
        var list = new ListScreen(
            "Twenty rows",
            [.. Enumerable.Range(0, 20).Select(index => new ListRow($"Row {index}", Available: index != 8))],
            _ => ScreenCommand.Stay);

        list.Handle(NavAction.PageDown);

        // Row 8 cannot be chosen, so the press gives way to the next row that can.
        Assert.Equal(9, list.Cursor);

        list.Handle(NavAction.PageDown);
        list.Handle(NavAction.PageDown);
        Assert.Equal(19, list.Cursor);

        list.Handle(NavAction.PageUp);
        Assert.Equal(11, list.Cursor);

        list.Handle(NavAction.PageUp);
        list.Handle(NavAction.PageUp);
        Assert.Equal(0, list.Cursor);
    }

    // ------------------------------------------------------------------ L2 and R2

    [Fact]
    public async Task L2_and_R2_step_through_the_platform_list_wrapping_and_keep_the_search()
    {
        Map(2, "megadrive");

        using var stub = Library(3);
        stub.Library.Add(new StubRom(4, 2, "genesis", "megadrive", "Game Gen", "Game Gen.md", "md", 1_024));
        Pair();

        var platforms = new SyncSetService(_session).PlatformsKnownHere();
        Assert.Equal(2, platforms.Count);

        using var browse = new BrowseViewModel(_session, Connect(stub));
        await Settled(browse);

        var keyboard = Assert.IsType<OnScreenKeyboard>(Navigator.Press(browse, NavAction.Alternate).Screen);
        keyboard.Handle(NavAction.Accept);
        var term = keyboard.Text;
        keyboard.Handle(NavAction.Start);
        await Settled(browse);

        var seen = new List<string?>();

        for (var press = 0; press < 3; press++)
        {
            browse.Handle(NavAction.NextGroup);
            await Settled(browse);
            seen.Add(browse.State.PlatformLabel);

            Assert.Equal(term, browse.State.View.Search);
        }

        // Every platform, then each platform in the picker's order, and round again.
        Assert.Equal([platforms[0].Label, platforms[1].Label, null], seen);

        browse.Handle(NavAction.PreviousGroup);
        await Settled(browse);
        Assert.Equal(platforms[1].Label, browse.State.PlatformLabel);
        Assert.Contains(
            "platform_ids=" + platforms[1].PlatformId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            stub.QueryLog.Last(query => query.Contains("/api/roms?", StringComparison.Ordinal)),
            StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ Select

    [Fact]
    public async Task Select_opens_view_options_over_the_list()
    {
        using var stub = Library(3);
        Pair();
        using var browse = new BrowseViewModel(_session, Connect(stub));

        await Settled(browse);

        Assert.Contains(browse.Hints, hint => hint.Action == NavAction.Options);
        Assert.All(browse.Hints, hint => Assert.Contains(hint.Action, NavRepeat.Bound));

        var options = Assert.IsType<ViewOptionsScreen>(Navigator.Press(browse, NavAction.Options).Screen);
        Assert.Same(browse, ((IPopupScreen)options).Underneath);
        Assert.Equal(
            ["Search for", "Jump to letter", "Sort by", "Filters"],
            ((IWindowedScreen)options).Rows.Select(row => row.Label));

        // Select closes it again, as ES's does.
        Assert.Equal(ScreenCommandKind.Pop, options.Handle(NavAction.Options).Kind);
    }

    [Fact]
    public async Task The_letter_jump_reads_RomMs_index_once_and_lands_on_the_letter()
    {
        using var stub = new StubRomMServer();
        string[] names =
        [
            "3 Ninjas", "Aladdin", "Axelay", "Bonk",
            .. Enumerable.Range(0, 80).Select(index => $"Mario {index:00}"),
            "Zelda", "Zoop",
        ];

        for (var id = 1; id <= names.Length; id++)
        {
            stub.Library.Add(new StubRom(id, 1, "snes", "snes", names[id - 1], names[id - 1] + ".sfc", "sfc", 1_024));
        }

        Pair();
        using var browse = new BrowseViewModel(_session, Connect(stub));
        await Settled(browse);

        var options = await Letters(browse);

        Assert.Equal(1, stub.CharIndexRequests);
        Assert.Equal("‹ # ›", LetterRow(options).Value);

        // Right through A and B to M, then left back to B: the stepper holds only the letters
        // the list has, digits folded into one.
        options.Handle(NavAction.Down);
        options.Handle(NavAction.Right);
        options.Handle(NavAction.Right);
        options.Handle(NavAction.Right);
        Assert.Equal("‹ M ›", LetterRow(options).Value);

        options.Handle(NavAction.Right);
        Assert.Equal("‹ Z ›", LetterRow(options).Value);

        var jump = options.Handle(NavAction.Accept);
        Assert.Equal(ScreenCommandKind.Dismiss, jump.Kind);
        jump.Follow!();
        await Settled(browse);

        // Z is past the first page, so the jump fetched the page that starts there.
        Assert.Equal(names.Length - 2, browse.State.Page!.Offset);
        Assert.Equal("Zelda", browse.Rows[browse.Cursor].Label);

        // A second visit asks nothing: the view has not changed.
        await Letters(browse);
        Assert.Equal(1, stub.CharIndexRequests);
    }

    [Fact]
    public async Task Under_another_sort_the_letter_row_says_why_there_is_nothing_to_jump_between()
    {
        using var stub = Library(3);
        Pair();
        using var browse = new BrowseViewModel(_session, Connect(stub));
        await Settled(browse);

        var picker = ViewOptionsScreen.SortPicker(browse);
        Assert.Equal("chosen", picker.Rows[0].Value);

        picker.Handle(NavAction.Down);
        picker.Handle(NavAction.Down);
        Assert.Equal(ScreenCommandKind.Pop, picker.Handle(NavAction.Accept).Kind);
        await Settled(browse);

        Assert.Equal(BrowseOrder.ReleaseNewest, browse.State.View.Order);
        Assert.Contains(
            stub.QueryLog,
            query => query.Contains("order_by=first_release_date&order_dir=desc", StringComparison.Ordinal));

        var options = await Letters(browse);
        var row = LetterRow(options);

        Assert.False(row.Available);
        Assert.Contains("other than name", row.Detail, StringComparison.Ordinal);
        Assert.Equal(0, stub.CharIndexRequests);
    }

    [Fact]
    public async Task Filters_are_RomMs_and_reach_the_page_only_from_the_last_row()
    {
        using var stub = Library(4);
        stub.Library.Add(new StubRom(9, 1, "snes", "snes", "Puzzler", "Puzzler.sfc", "sfc", 1_024)
        {
            Metadata = new StubRomMetadata { Genres = ["Puzzle"] },
        });
        Pair();
        using var browse = new BrowseViewModel(_session, Connect(stub));
        await Settled(browse);

        browse.FacetValues = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [FilterFacet.Genres] = ["Platform", "Puzzle"],
        };

        var filters = ViewOptionsScreen.Filters(browse);
        Assert.Equal(FilterFacet.Genres, filters.Rows[0].Label);

        var genres = Assert.IsType<ListScreen>(filters.Handle(NavAction.Accept).Screen);

        // The operator row, then Platform, then Puzzle.
        genres.Handle(NavAction.Down);
        genres.Handle(NavAction.Down);
        genres.Handle(NavAction.Accept);
        filters.Returned();

        Assert.Equal("Puzzle", filters.Rows[0].Value);

        // Back with a change asks, and applies nothing.
        Assert.IsType<ConfirmScreen>(filters.Handle(NavAction.Back).Screen);
        Assert.Null(browse.State.View.Filter);

        for (var press = 0; press < filters.Rows.Count - 1; press++)
        {
            filters.Handle(NavAction.Down);
        }

        Assert.Equal("Show these games", filters.Rows[filters.Cursor].Label);
        Assert.Equal(ScreenCommandKind.Pop, filters.Handle(NavAction.Accept).Kind);
        await Settled(browse);

        Assert.Equal(["Puzzle"], browse.State.View.Filter!.Genres);
        Assert.Equal(["Puzzler"], browse.Rows.Select(row => row.Label));
        Assert.Contains(", filtered.", browse.Note, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Offline_the_letters_come_from_this_device_and_sort_and_filters_wait_for_RomM()
    {
        Installed(1, "snes", "Aladdin.sfc", 10);
        Installed(2, "snes", "Bonk.sfc", 10);
        Installed(3, "snes", "Bubsy.sfc", 10);
        Installed(4, "snes", "Contra.sfc", 10);

        using var browse = new BrowseViewModel(_session);
        await Settled(browse);
        Assert.Equal(BrowseSource.ThisDevice, browse.State.Page!.Source);

        var options = await Letters(browse);

        options.Handle(NavAction.Down);
        options.Handle(NavAction.Right);
        options.Handle(NavAction.Right);
        Assert.Equal("‹ C ›", LetterRow(options).Value);

        options.Handle(NavAction.Accept).Follow!();
        Assert.Equal("Contra.sfc", browse.Rows[browse.Cursor].Label);

        var rows = ((IWindowedScreen)options).Rows;
        Assert.False(rows[2].Available);
        Assert.False(rows[3].Available);
    }

    [Fact]
    public void The_letter_index_is_off_on_every_page_and_on_only_when_asked()
    {
        var query = new CatalogQuery { Scope = CatalogScopeKind.Platform, ScopeId = "1", OrderBy = "name" };

        Assert.Contains("with_char_index=false", query.ToQueryString(50, 0), StringComparison.Ordinal);
        Assert.Contains("with_char_index=true", query.ToQueryString(1, 0, withCharIndex: true), StringComparison.Ordinal);
    }

    [Fact]
    public void Select_and_the_triggers_are_read_from_es_input_names_and_a_trigger_moves_once()
    {
        var nav = new NavRepeat();
        var start = DateTimeOffset.UnixEpoch;

        Assert.Equal([NavAction.Options], nav.Advance(new HashSet<string> { "select" }, start));
        Assert.Equal([NavAction.PreviousGroup], nav.Advance(new HashSet<string> { "l2" }, start.AddSeconds(1)));
        Assert.Empty(nav.Advance(new HashSet<string> { "l2" }, start.AddSeconds(3)));
        Assert.Equal([NavAction.NextGroup], nav.Advance(new HashSet<string> { "r2" }, start.AddSeconds(4)));
    }

    /// <summary>Opens view options and waits for its letters to arrive.</summary>
    private static async Task<ViewOptionsScreen> Letters(BrowseViewModel browse)
    {
        var options = Assert.IsType<ViewOptionsScreen>(Navigator.Press(browse, NavAction.Options).Screen);
        await WaitFor(() => LetterRow(options).Value != "...");
        return options;
    }

    private static ListRow LetterRow(ViewOptionsScreen options) => ((IWindowedScreen)options).Rows[1];
}
