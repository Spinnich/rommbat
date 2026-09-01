using System.Globalization;
using RomM.Client;
using RomM.Client.Catalog;
using RomM.Client.Content;
using RomMBat.Core;
using RomMBat.Core.Identity;
using RomMBat.Core.Metadata;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Sets;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using RomMBat.UI.Input;
using RomMBat.UI.Screens;
using RomMBat.UI.Shell;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The sync and eviction screens, driven with the gamepad map alone and no window.
/// </summary>
/// <remarks>
/// <b>A whole sync, end to end, against a stub.</b> Nobody had seen one when 7b-2a closed, and
/// its ledger named that as the next stage's whole subject. #105 is what makes the flow
/// drivable this way: the connection factory now reaches the screens that start network work,
/// so these run against <see cref="StubRomMServer"/> rather than stopping at "not paired".
/// <para>
/// <b>Screens carry no Avalonia types</b>, which is what makes "no primary flow requires a
/// mouse" checkable rather than asserted, and the only reason that stays true is tests like
/// these actually walking it.
/// </para>
/// </remarks>
public sealed class SyncScreenTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly Uri Origin = new("https://romm.invalid/");

    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();
    private readonly InstallSession _session;

    public SyncScreenTests()
    {
        var location = Path.Combine(_tree.Root, "emulationstation", ".emulationstation", "es_systems.cfg");
        Directory.CreateDirectory(Path.GetDirectoryName(location)!);
        File.Copy(Fixtures.EsSystemsTemplate, location);

        _session = InstallSession.Open(_tree.Root).Session!;
    }

    public void Dispose()
    {
        _session.Dispose();
        _tree.Dispose();
    }

    // ------------------------------------------------------------------ reachable and leavable

    [Fact]
    public async Task A_sync_is_reached_from_the_sets_list_and_left_with_the_pad_alone()
    {
        using var stub = Library(2);
        Pair();
        Seed("games", 2);

        var navigator = new Navigator(Status(stub));

        // Start on the status screen opens the sets list; Alternate there syncs everything.
        navigator.Handle(NavAction.Start);
        Assert.IsType<ListScreen>(navigator.Current);

        navigator.Handle(NavAction.Alternate);
        var sync = Assert.IsType<SyncViewModel>(navigator.Current);

        await SettledAsync(sync);

        Assert.Equal(SyncStage.Done, sync.State.Stage);

        // Back leaves once it is over, and lands on the list rather than anywhere else.
        navigator.Handle(NavAction.Back);
        Assert.IsType<ListScreen>(navigator.Current);

        // And out, without ever needing a second way to leave.
        navigator.Handle(NavAction.Back);
        Assert.IsType<StatusViewModel>(navigator.Current);
    }

    [Fact]
    public async Task A_sync_is_reached_from_one_sets_detail_screen_too()
    {
        // Mirrors `sync [set]`. Both routes exist because a person with five sets does not
        // want the other four re-fetched to add one game to this one.
        using var stub = Library(1);
        Pair();
        Seed("games", 1);

        var navigator = new Navigator(Status(stub));
        navigator.Handle(NavAction.Start);

        var list = Assert.IsType<ListScreen>(navigator.Current);
        navigator.Handle(NavAction.Accept);
        Assert.IsType<ListScreen>(navigator.Current);
        Assert.NotSame(list, navigator.Current);

        navigator.Handle(NavAction.Start);
        var sync = Assert.IsType<SyncViewModel>(navigator.Current);

        await SettledAsync(sync);
        Assert.Equal(SyncStage.Done, sync.State.Stage);
    }

    [Fact]
    public async Task A_whole_sync_puts_the_games_their_artwork_and_their_gamelist_on_the_device()
    {
        using var stub = Library(2);
        Pair();
        Seed("games", 2);

        var sync = new SyncViewModel(_session, Set(), Connect(stub));
        await SettledAsync(sync);

        Assert.Equal(SyncStage.Done, sync.State.Stage);
        Assert.Empty(sync.State.Problems);

        foreach (var romId in new[] { 1, 2 })
        {
            var rows = _session.Store.Files.ForRom(romId);

            Assert.Contains(rows, row => row.Kind == LocalFileKind.Rom);
            Assert.Contains(rows, row => row.Kind == LocalFileKind.Image);
        }

        // The gamelist EmulationStation actually reads, written from local state.
        var gamelist = _session.Install.Resolve(RelativePath.Create("roms/psx/gamelist.xml"));

        Assert.True(File.Exists(gamelist), "no gamelist was written, so ES would show nothing");
        Assert.Contains("Game 1", File.ReadAllText(gamelist), StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ the stop

    [Fact]
    public async Task Back_stops_the_run_and_stays_so_the_screen_can_say_what_it_removed()
    {
        // #107, decided with this screen's stop. A press that closed the screen could never
        // report what its rollback took, and the resolve screen answers Back the same way now.
        using var stub = Library(3);
        Pair();
        Seed("games", 3);

        var sync = new SyncViewModel(_session, Set(), Connect(stub));

        var command = sync.Handle(NavAction.Back);

        Assert.Equal(ScreenCommandKind.Stay, command.Kind);

        await SettledAsync(sync);

        Assert.Equal(SyncStage.Stopped, sync.State.Stage);

        // A second Back leaves, which is the half that makes the first one bearable.
        Assert.Equal(ScreenCommandKind.Pop, sync.Handle(NavAction.Back).Kind);
    }

    [Fact]
    public async Task A_stopped_run_still_writes_the_gamelist_for_what_finished()
    {
        // Found by a hands-on pass, and it is the defect this stage would most have deserved to
        // be caught on. The first game of a set landed on the drive and never appeared in
        // EmulationStation, because the gamelist pass was handed the run's cancellation token
        // and threw the instant a stop reached it. A sync that leaves finished games invisible
        // has postponed work, which is exactly what "a stopped sync ends with a correct tree"
        // says it must not do.
        //
        // Deterministic because Immediate reports inline: cancelling while the first game's
        // artwork is being fetched means the second game's transfer throws at its first
        // cancellation check.
        using var stub = Library(2);
        Pair();
        Seed("games", 2);

        using var stopping = new CancellationTokenSource();
        var events = new List<SyncEvent>();

        await new LibrarySyncService(_session).RunAsync(
            [Set()],
            new SyncOptions(NoResolve: true),
            new RomMConnection(new RomMClientOptions { Origin = Origin, AccessToken = "rmm_test" }, stub),
            new Immediate<SyncEvent>(reported =>
            {
                events.Add(reported);

                if (reported is MediaProgressed)
                {
                    stopping.Cancel();
                }
            }),
            _ => Task.CompletedTask,
            stopping.Token);

        // The pass ran at all, which is the half that was missing.
        Assert.Contains(events, reported => reported is GamelistsWritten);

        var gamelist = _session.Install.Resolve(RelativePath.Create("roms/psx/gamelist.xml"));

        Assert.True(File.Exists(gamelist), "a stopped run left no gamelist, so ES shows nothing");
        Assert.Contains("Game 1", File.ReadAllText(gamelist), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_finished_count_comes_from_what_landed_rather_than_from_the_plan()
    {
        // A hands-on pass stopped a 41-game sync after one game and the screen read "41 of 41".
        // The count was taken from the plan's total on the set-finished event, which fires on a
        // run that stopped or failed just as it does on one that completed.
        //
        // Driven here by a failure rather than a stop, because that is deterministic against a
        // stub: right id, wrong length, so ContentSync verifies and refuses.
        using var stub = Library(2);
        stub.Content[2] = new byte[16];

        Pair();
        Seed("games", 2);

        var sync = new SyncViewModel(_session, Set(), Connect(stub));
        await SettledAsync(sync);

        Assert.Equal(2, sync.State.Total);
        Assert.Equal(1, sync.State.Done);
    }

    [Fact]
    public async Task Nothing_stale_is_left_on_the_screen_once_the_run_is_over()
    {
        // A hands-on pass finished a sync and the screen still read "Telling EmulationStation",
        // which is a screen that looks like it has not noticed it is done.
        using var stub = Library(1);
        Pair();
        Seed("games", 1);

        var sync = new SyncViewModel(_session, Set(), Connect(stub));
        await SettledAsync(sync);

        Assert.Equal(SyncStage.Done, sync.State.Stage);
        Assert.Null(sync.State.Pass);
        Assert.Null(sync.State.Game);
        Assert.Null(sync.State.GameProgress);
    }

    [Fact]
    public async Task The_stop_hint_says_what_the_press_costs()
    {
        using var stub = Library(2);
        Pair();
        Seed("games", 2);

        var sync = new SyncViewModel(_session, Set(), Connect(stub));

        // "Stop for now" is honest on the resolve screen because nothing is lost there. This
        // press drops a part-fetched game, and the label has to say so.
        var working = Assert.Single(sync.Hints);

        Assert.Equal(NavAction.Back, working.Action);
        Assert.Contains("drop", working.Label, StringComparison.OrdinalIgnoreCase);

        await SettledAsync(sync);
    }

    // ------------------------------------------------------------------ what the screen says

    [Fact]
    public async Task The_screen_reports_problems_as_they_arrive_rather_than_only_at_the_end()
    {
        // The one part of a run a person cannot read back once it ends, which is why it is the
        // part the screen keeps rather than a scrolling tail of what went right.
        using var stub = Library(2);

        // Right id, wrong length: ContentSync verifies against the declared size and refuses.
        stub.Content[2] = new byte[16];

        Pair();
        Seed("games", 2);

        var sync = new SyncViewModel(_session, Set(), Connect(stub));
        await SettledAsync(sync);

        Assert.Equal(SyncStage.Incomplete, sync.State.Stage);
        Assert.NotEmpty(sync.State.Problems);
        Assert.Contains(sync.State.Problems, problem => problem.Contains("Game 2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_budget_is_on_this_screen_because_this_is_where_it_is_spent()
    {
        using var stub = Library(1);
        Pair();
        Seed("games", 1);
        _session.Store.Settings.Set(SettingStore.ContentMaxBytes, 64L << 30, Now);

        var sync = new SyncViewModel(_session, Set(), Connect(stub));
        await SettledAsync(sync);

        Assert.NotNull(sync.State.Budget);
        Assert.Contains("of", sync.State.Budget!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unpaired_install_offers_pairing_rather_than_a_dead_end()
    {
        // No pairing at all, so nothing is even attempted. The screen has exactly one useful
        // thing to offer and it offers it.
        using var stub = Library(1);
        Seed("games", 1);

        var opened = false;
        var sync = new SyncViewModel(
            _session,
            Set(),
            Connect(stub),
            pair: () =>
            {
                opened = true;
                return new MessageScreen("Pair", "here");
            });

        await SettledAsync(sync);

        Assert.Equal(SyncStage.NotPaired, sync.State.Stage);
        Assert.Contains(sync.Hints, hint => hint.Action == NavAction.Accept);

        Assert.Equal(ScreenCommandKind.Push, sync.Handle(NavAction.Accept).Kind);
        Assert.True(opened);
    }

    [Fact]
    public async Task A_token_the_server_refuses_stops_the_run_and_offers_pairing()
    {
        // A 401 is an identity change, not a transient fault: every game after the first
        // rejection would send the same refused token. So the run stops and the one thing a
        // person can do about it is on the footer.
        //
        // The stage this reaches was measured before it was written. Driving a live 401 through
        // this screen against the real server reported Incomplete and said "Syncing again picks
        // up where this left off", which is false until the user pairs again. The resolve is the
        // first authenticated call a sync makes, so that is where a refused token is met, and
        // the rejection had to be carried out of the resolve rather than only out of the
        // content pass.
        using var stub = Library(2);
        stub.RejectsToken = true;

        Pair();
        Seed("games", 2);

        var opened = false;
        var sync = new SyncViewModel(
            _session,
            Set(),
            Connect(stub),
            pair: () =>
            {
                opened = true;
                return new MessageScreen("Pair", "here");
            });

        await SettledAsync(sync);

        Assert.Equal(SyncStage.Rejected, sync.State.Stage);
        Assert.Contains("Pair again", sync.State.Detail, StringComparison.Ordinal);

        // And nothing telling them to try again, which is the sentence the live probe caught.
        Assert.DoesNotContain("picks up where", sync.State.Detail, StringComparison.Ordinal);

        Assert.Contains(sync.Hints, hint => hint.Action == NavAction.Accept);
        Assert.Equal(ScreenCommandKind.Push, sync.Handle(NavAction.Accept).Kind);
        Assert.True(opened);
    }

    [Fact]
    public async Task Nothing_any_of_these_screens_says_names_a_face_button()
    {
        // es_input.cfg's `x` is the button printed Y and its `y` is the one printed X, so a
        // screen free to write a letter writes the wrong one on two of the three pads the live
        // install has configured. Swept over every string rather than checked at one site,
        // because 7b-1 round 8 found "Press A" in a status row after the footer rule was
        // already in place.
        using var stub = Library(1);
        Pair();
        Seed("games", 1);

        var sync = new SyncViewModel(_session, Set(), Connect(stub));
        await SettledAsync(sync);

        foreach (var text in Strings(sync))
        {
            foreach (var forbidden in new[] { "Press A", "Press B", "Press X", "Press Y", "button A", "button B" })
            {
                Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    // ------------------------------------------------------------------ saying it has finished

    [Fact]
    public async Task A_finished_run_says_so_in_its_title_its_outcome_and_its_footer()
    {
        // A hands-on pass sat on a resolve reading "Querying 'X'" over a full bar
        // and 107 of 107, and could not tell a finished screen from a stuck one. A full bar and
        // a stalled bar are the same picture, so the screen has to say which it is. Three
        // places, because the title is what a person reads first and the footer is where they
        // look for what to press.
        using var stub = Library(2);
        Pair();
        Seed("games", 2);

        var sync = new SyncViewModel(_session, Set(), Connect(stub));

        Assert.StartsWith("Syncing", sync.Title, StringComparison.Ordinal);
        Assert.Null(sync.State.Outcome);
        Assert.Contains(sync.Hints, hint => hint.Label.Contains("Stop", StringComparison.Ordinal));

        await SettledAsync(sync);

        Assert.StartsWith("Synced", sync.Title, StringComparison.Ordinal);
        Assert.Equal("Finished", sync.State.Outcome);

        var back = Assert.Single(sync.Hints, hint => hint.Action == NavAction.Back);
        Assert.Equal("Done", back.Label);

        // The one rule a person has to learn: a stop means it is going, Done means it is over.
        Assert.DoesNotContain(sync.Hints, hint => hint.Label.Contains("Stop", StringComparison.Ordinal));

        sync.Dispose();
    }

    [Fact]
    public async Task A_run_that_did_not_all_work_says_that_rather_than_finished()
    {
        // "Finished" over a list of problems reports a success the run did not have.
        using var stub = Library(3);
        Pair();
        Seed("games", 3);

        foreach (var rom in stub.Library)
        {
            stub.Content[rom.Id] = new byte[2048];
        }

        var sync = new SyncViewModel(_session, Set(), Connect(stub));
        await SettledAsync(sync);

        Assert.Equal(SyncStage.Incomplete, sync.State.Stage);
        Assert.Equal("Finished with problems", sync.State.Outcome);
        Assert.Equal("Done", Assert.Single(sync.Hints, hint => hint.Action == NavAction.Back).Label);

        sync.Dispose();
    }

    [Fact]
    public async Task A_finished_resolve_says_so_the_same_way()
    {
        // Same rule on both screens, or a user learns two.
        using var stub = Library(2);
        Pair();
        Seed("games", 2);

        var resolve = new ResolveViewModel(_session, Set(), Connect(stub));

        Assert.StartsWith("Querying", resolve.Title, StringComparison.Ordinal);
        Assert.Null(resolve.Outcome);

        await SettledAsync(resolve);

        Assert.StartsWith("Queried", resolve.Title, StringComparison.Ordinal);
        Assert.Equal("Finished", resolve.Outcome);
        Assert.Equal("Done", Assert.Single(resolve.Hints, hint => hint.Action == NavAction.Back).Label);

        resolve.Dispose();
    }

    // ------------------------------------------------------------------ the screen holds still

    [Fact]
    public void A_progress_line_keeps_its_unit_and_its_width_as_it_fills()
    {
        // A hands-on pass on a set of small ROMs reported the text vibrating, which it called
        // double vision. Two causes: Format rescales KB to MB to GB as the left side grows, and
        // "0.#" drops the decimal at every round number, so a line rebuilt eight times a second
        // is a different length almost every time. The destination is fixed for the run, so the
        // unit comes from it and the decimal is forced.
        const long Total = 275_900_000;

        var widths = new HashSet<int>();
        var units = new HashSet<string>();

        for (var step = 0; step <= 100; step++)
        {
            var line = ByteSize.Progress(Total * step / 100, Total);

            widths.Add(line.Length);
            units.Add(line[(line.LastIndexOf(' ') + 1)..]);
        }

        Assert.Single(units);
        Assert.Equal("MB", units.Single());

        // Only the digits before the decimal grow, so 0.0 to 275.9 is three widths and not a
        // hundred. What matters is that it does not change on almost every frame.
        Assert.True(widths.Count <= 3, $"the line took {widths.Count} different widths as it filled");

        // A tiny run stays in its own unit rather than being forced into the largest.
        Assert.EndsWith("KB", ByteSize.Progress(0, 40_000), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Every_problem_is_reachable_when_more_arrive_than_the_screen_shows()
    {
        // A hands-on pass on a 594-game arcade set hit 27 problems, could read 6, and had no
        // press that reached the other 21. Driven through the real path rather than by pushing
        // a list in: every rom on this stub serves more bytes than its row declares, which is
        // the size mismatch that produced those 27 on the live instance.
        const int Games = 10;

        using var stub = Library(Games);
        Pair();
        Seed("games", Games);

        foreach (var rom in stub.Library)
        {
            stub.Content[rom.Id] = new byte[2048];
        }

        var sync = new SyncViewModel(_session, Set(), Connect(stub));
        await SettledAsync(sync);

        Assert.True(
            sync.State.Problems.Count > SyncViewModel.ProblemsShown,
            $"the fixture produced only {sync.State.Problems.Count} problems");

        var offer = Assert.Single(sync.Hints, hint => hint.Action == NavAction.Accept);
        Assert.Contains(
            sync.State.Problems.Count.ToString(CultureInfo.CurrentCulture),
            offer.Label,
            StringComparison.Ordinal);

        var opened = sync.Handle(NavAction.Accept);
        var all = Assert.IsType<ListScreen>(opened.Screen);

        Assert.Equal(ScreenCommandKind.Push, opened.Kind);
        Assert.Equal(sync.State.Problems.Count, all.Rows.Count);

        // The first one too, which the run screen drops in favour of the newest few.
        Assert.Equal(sync.State.Problems[0], all.Rows[0].Detail);

        // Nothing to choose, so no row promises a press that does nothing.
        Assert.All(all.Rows, row => Assert.False(row.Available));

        sync.Dispose();
    }

    [Fact]
    public void A_list_of_problems_scrolls_even_though_no_row_can_be_chosen()
    {
        // The rows are unavailable because there is nothing to choose, and on an ordinary list
        // that means the cursor skips every one of them: FirstAvailable returns -1 and nothing
        // moves. A hands-on pass opened the problems screen and could not scroll it.
        var problems = Enumerable.Range(1, 30).Select(n => $"Game {n}: it did not work").ToList();

        var screen = new ListScreen(
            "30 problems",
            [.. problems.Select((problem, index) => new ListRow(
                (index + 1).ToString(CultureInfo.CurrentCulture), null, problem, false))],
            _ => ScreenCommand.Stay,
            acceptLabel: string.Empty)
        {
            Reading = true,
        };

        Assert.Equal(0, screen.Cursor);

        screen.Handle(NavAction.Down);
        screen.Handle(NavAction.Down);
        Assert.Equal(2, screen.Cursor);

        // The window follows, or scrolling moves a cursor nobody can see. That was the defect
        // twice before, on two other screens.
        var start = screen.Window.Start;

        for (var press = 0; press < 20; press++)
        {
            screen.Handle(NavAction.Down);
        }

        Assert.Equal(22, screen.Cursor);
        Assert.True(screen.Window.Start > start, "the window did not follow the cursor down");
        Assert.InRange(screen.Cursor, screen.Window.Start, screen.Window.Start + screen.Window.Count - 1);

        // Up walks back, and the ends wrap rather than sticking.
        screen.Handle(NavAction.Up);
        Assert.Equal(21, screen.Cursor);

        screen.Handle(NavAction.Up);
        Assert.Equal(20, screen.Cursor);

        // Still nothing to press, which is the whole reason the rows are unavailable.
        Assert.DoesNotContain(screen.Hints, hint => hint.Action == NavAction.Accept);
        Assert.Equal(ScreenCommandKind.Stay, screen.Handle(NavAction.Accept).Kind);
    }

    [Fact]
    public void A_window_of_reading_rows_is_no_taller_than_a_window_of_ordinary_ones()
    {
        // The capacity was written for a 78px row and the reading row is 122, so eight of them
        // ran off the bottom of the window and Avalonia drew a scroll bar on it. A gamepad
        // cannot drive that bar, and it is not how this interface scrolls: the window is.
        // Found from the couch one round after the taller row was introduced.
        var ordinary = ListWindow.BlockHeight(ListWindow.Capacity, ListWindow.RowHeight);
        var reading = ListWindow.BlockHeight(ListWindow.ReadingCapacity, ListWindow.ReadingRowHeight);

        Assert.True(
            reading <= ordinary,
            $"a reading window is {reading}px against the {ordinary}px already known to fit");

        // And not so few that the list is a porthole. One more would overflow, which is the
        // other half of the claim and the half that goes stale if a row grows.
        var oneMore = ListWindow.BlockHeight(ListWindow.ReadingCapacity + 1, ListWindow.ReadingRowHeight);

        Assert.True(oneMore > ordinary, $"{ListWindow.ReadingCapacity + 1} reading rows would also have fitted");
    }

    [Fact]
    public void A_reading_list_windows_to_its_own_capacity()
    {
        var screen = new ListScreen(
            "30 problems",
            [.. Enumerable.Range(1, 30).Select(n => new ListRow(n.ToString(CultureInfo.CurrentCulture), null, "why", false))],
            _ => ScreenCommand.Stay)
        {
            Reading = true,
        };

        Assert.Equal(ListWindow.ReadingCapacity, screen.Window.Count);

        var picker = new ListScreen(
            "30 things",
            [.. Enumerable.Range(1, 30).Select(n => new ListRow(n.ToString(CultureInfo.CurrentCulture)))],
            _ => ScreenCommand.Stay);

        Assert.Equal(ListWindow.Capacity, picker.Window.Count);
    }

    [Fact]
    public void A_picker_still_skips_the_rows_that_cannot_be_chosen()
    {
        // The reading flag must not change the ordinary list. Parking on a row Accept cannot
        // act on is a press that does nothing, which is why the cursor skips it.
        var screen = new ListScreen(
            "Pick one",
            [
                new ListRow("first", null, null, false),
                new ListRow("second"),
                new ListRow("third", null, null, false),
                new ListRow("fourth"),
            ],
            _ => ScreenCommand.Stay);

        Assert.Equal(1, screen.Cursor);

        screen.Handle(NavAction.Down);
        Assert.Equal(3, screen.Cursor);
    }

    [Fact]
    public async Task A_run_with_no_problems_offers_no_press_to_go_and_read_them()
    {
        // Offering the press when everything already fits on screen is a press that appears to
        // do nothing.
        using var stub = Library(1);
        Pair();
        Seed("games", 1);

        var sync = new SyncViewModel(_session, Set(), Connect(stub));
        await SettledAsync(sync);

        Assert.True(sync.State.Problems.Count <= SyncViewModel.ProblemsShown);
        Assert.DoesNotContain(sync.Hints, hint => hint.Action == NavAction.Accept);
        Assert.Equal(ScreenCommandKind.Stay, sync.Handle(NavAction.Accept).Kind);

        sync.Dispose();
    }

    // ------------------------------------------------------------------ eviction is not offered

    [Fact]
    public void No_screen_offers_to_free_space_on_the_users_behalf()
    {
        // Ruled with Spinnich: RomMBat guessing which games matter least is a bad policy even
        // when a person starts it, and freeing space belongs to them, by dropping a sync set or
        // (once 7b-2c lands) a single game. EvictionService stays in Core and `rommbat-agent
        // evict` stays, both behind a preview; what went is the screen.
        //
        // Asserted rather than trusted to the delete, because the entry points were two: the
        // budget screen's third press, and the offer a blocked sync made where the user found
        // out the budget had cut it short.
        Pair();
        Seed("games", 1);

        var budget = new BudgetViewModel(_session);

        Assert.DoesNotContain(budget.Hints, hint => hint.Action == NavAction.Alternate);
        Assert.Equal(ScreenCommandKind.Stay, budget.Handle(NavAction.Alternate).Kind);

        foreach (var text in Strings(budget))
        {
            Assert.DoesNotContain("free up space", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task A_run_the_budget_cut_short_still_says_so_without_offering_to_fix_it()
    {
        // Removing the offer must not remove the fact. The count and the reason come from
        // MediaSync and ContentSync, which word it as "the N budget is full", so the user is
        // told what happened and left to decide what to do about it.
        using var stub = Library(3);
        Pair();
        Seed("games", 3);

        _session.Store.Settings.Set(SettingStore.ContentMaxBytes, "1", DateTimeOffset.UtcNow);

        var sync = new SyncViewModel(_session, Set(), Connect(stub));
        await SettledAsync(sync);

        Assert.True(sync.State.Blocked > 0, "the fixture did not reproduce a blocked run");
        Assert.DoesNotContain(sync.Hints, hint => hint.Action == NavAction.Accept);

        Assert.Contains(
            sync.State.Problems,
            problem => problem.Contains("budget", StringComparison.OrdinalIgnoreCase));

        // The count, and against the screen's own text rather than the field behind it. Blocked
        // accumulated and forced a redraw for a whole release without ScreenView ever reading
        // it, so a run cut short reported the reason and never the size of it.
        Assert.NotNull(sync.State.Held);
        Assert.Contains(sync.State.Blocked.ToString(CultureInfo.InvariantCulture), sync.State.Held);
        Assert.Contains("budget", sync.State.Held, StringComparison.OrdinalIgnoreCase);

        sync.Dispose();
    }

    [Fact]
    public async Task A_run_the_budget_stopped_dead_never_reports_that_everything_is_here()
    {
        // Found by a hands-on pass, on a screen reading FINISHED over "Everything in these sync
        // sets is on this device", above "0 of 386" and "386 ROMs were left out". Every one of
        // those numbers was right and the sentence was the exact opposite of them.
        //
        // The cause is that a blocked ROM is not a failed one, so LibrarySyncService's worst
        // state stayed Done and this screen believed it. Telling somebody their library is on
        // the device when none of it is, is worse than any of the counts being wrong.
        //
        // #109 fixed the screen and #114 moved the answer into the service, which is where the
        // next caller will read it: a blocked run is SyncState.Blocked, its own state rather
        // than Incomplete, because Incomplete is what the agent turns into its Offline exit
        // code and a full disk budget is not being offline.
        using var stub = Library(3);
        Pair();
        Seed("games", 3);

        _session.Store.Settings.Set(SettingStore.ContentMaxBytes, "1", DateTimeOffset.UtcNow);

        var sync = new SyncViewModel(_session, Set(), Connect(stub));
        await SettledAsync(sync);

        Assert.True(sync.State.Blocked > 0, "the fixture did not reproduce a blocked run");
        Assert.Equal(SyncStage.Blocked, sync.State.Stage);
        Assert.Equal("Stopped by the disk budget", sync.State.Outcome);

        Assert.DoesNotContain("on this device", sync.State.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("budget", sync.State.Detail, StringComparison.OrdinalIgnoreCase);

        // And it does not promise that running it again gets past this, which is the lie the
        // ordinary Incomplete sentence would tell here: the same budget blocks identically.
        Assert.DoesNotContain("picks up where", sync.State.Detail, StringComparison.OrdinalIgnoreCase);

        sync.Dispose();
    }

    /// <summary>
    /// The service answers it, which is the half #114 was actually about.
    /// </summary>
    /// <remarks>
    /// Asserted at the service rather than only through the screen, because the whole point of
    /// moving it is that the next caller reads it from here. A screen-only assertion would go
    /// on passing if somebody put the derivation back into a view model.
    /// </remarks>
    [Fact]
    public async Task A_run_the_budget_blocked_is_its_own_state_at_the_service()
    {
        using var stub = Library(3);
        Pair();
        Seed("games", 3);

        _session.Store.Settings.Set(SettingStore.ContentMaxBytes, "1", DateTimeOffset.UtcNow);

        using var connection = Connect(stub)(new Uri("https://romm.invalid/"));

        var report = await new LibrarySyncService(_session).RunAsync(
            [Set()],
            new SyncOptions(),
            connection,
            new Immediate<SyncEvent>(_ => { }),
            cancellationToken: TestContext.Current.CancellationToken);

        // Not Done, which was the lie, and not Incomplete, which is what SyncCommand turns into
        // its Offline exit code: the server was reachable throughout and the disk said no.
        Assert.Equal(Core.Sets.SyncState.Blocked, report.State);
    }

    [Fact]
    public void A_dirty_budget_offers_only_save_and_discard_so_nothing_navigates_away_from_it()
    {
        // Offering a third press while there are unsaved changes would be offering to discard
        // them without saying so.
        var budget = new BudgetViewModel(_session);

        budget.Handle(NavAction.Right);

        Assert.True(budget.IsDirty);
        Assert.DoesNotContain(budget.Hints, hint => hint.Action == NavAction.Alternate);
        Assert.Equal(ScreenCommandKind.Stay, budget.Handle(NavAction.Alternate).Kind);
    }

    // ------------------------------------------------------------------ responsiveness

    [Fact]
    public void An_unreachable_server_leaves_every_new_screen_responsive()
    {
        // Offline is a working state. A screen that blocks on an unreachable LAN host is a hung
        // app from the couch, and SocketsHttpHandler's own default is 21 seconds.
        using var stub = new StubRomMServer { IsReachable = false };
        Pair();
        Seed("games", 1);

        var started = System.Diagnostics.Stopwatch.StartNew();

        var sync = new SyncViewModel(_session, Set(), Connect(stub));
        _ = sync.Title;
        _ = sync.Hints;
        _ = sync.State;

        started.Stop();

        Assert.True(
            started.Elapsed < TimeSpan.FromSeconds(2),
            $"the screens took {started.Elapsed.TotalSeconds:0.0}s to become usable with the server off");

        sync.Dispose();
    }

    // ------------------------------------------------------------------ fixture

    /// <summary>Every string a screen would put in front of a person.</summary>
    private static IEnumerable<string> Strings(IScreen screen)
    {
        yield return screen.Title;

        foreach (var hint in screen.Hints)
        {
            yield return hint.Label;
        }

        switch (screen)
        {
            case SyncViewModel sync:
                yield return sync.State.Detail;

                foreach (var text in new[] { sync.State.Pass, sync.State.Game, sync.State.Counted, sync.State.Budget })
                {
                    if (text is not null)
                    {
                        yield return text;
                    }
                }

                foreach (var problem in sync.State.Problems)
                {
                    yield return problem;
                }

                break;

            case ListScreen list:
                if (list.Note?.Invoke() is { } note)
                {
                    yield return note;
                }

                if (list.EmptyMessage is { } empty)
                {
                    yield return empty;
                }

                foreach (var row in list.Rows)
                {
                    yield return row.Label;

                    if (row.Value is not null)
                    {
                        yield return row.Value;
                    }

                    if (row.Detail is not null)
                    {
                        yield return row.Detail;
                    }
                }

                break;

            default:
                break;
        }
    }

    /// <summary>Waits for the run to reach a terminal stage, or gives up and says so.</summary>
    private static async Task SettledAsync(ResolveViewModel resolve)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);

        while (resolve.Stage == ResolveStage.Working && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(15, TestContext.Current.CancellationToken);
        }

        Assert.True(resolve.Stage != ResolveStage.Working, "the resolve never finished");
    }

    private static async Task SettledAsync(SyncViewModel sync)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);

        while (sync.State.Stage == SyncStage.Working && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(15, TestContext.Current.CancellationToken);
        }

        Assert.True(sync.State.Stage != SyncStage.Working, "the sync never finished");
    }

    private static Func<Uri, RomMConnection> Connect(StubRomMServer stub) =>
        _ => new RomMConnection(
            new RomMClientOptions { Origin = Origin, AccessToken = "rmm_test" },
            stub);

    private StatusViewModel Status(StubRomMServer stub) =>
        new(_session, new GamepadStatus(GamepadAvailability.NoDevice, null, null, "No controller."))
        {
            OpenSets = () => SetsScreens.List(_session, Connect(stub)),
            OpenBudget = () => new BudgetViewModel(_session),
        };

    private void Pair()
    {
        _session.Store.Device.EnsureIdentity(DeviceIdentity.ReadOrCreate(_session.Install));
        _session.Store.Device.SavePairing(
            new PairingResult(
                Origin,
                "device-1",
                "Handheld",
                new GrantedScopes(["roms.read", "assets.read", "assets.write"]),
                TokenProtector.Protect("rmm_token", null, Now.AddYears(1))),
            Now);
    }

    private SyncSetDefinition Set() => _session.Store.SyncSets.Find("games")!;

    private static StubRomMServer Library(int count)
    {
        var stub = new StubRomMServer();
        stub.Platforms.Add(new StubPlatform(1, "psx", "psx", "PlayStation"));

        for (var id = 1; id <= count; id++)
        {
            stub.Library.Add(new StubRom(id, 1, "psx", "psx", $"Game {id}", $"Game {id}.chd", "chd", 1024)
            {
                Metadata = new StubRomMetadata(),
            });

            stub.Content[id] = new byte[1024];

            // Every kind the default StubRomMetadata names. A resolve rewrites the metadata
            // rows from the server, so seeding only the cover left the run reporting four
            // "the server no longer has it" problems per game and the test asserting against
            // a library the stub never claimed to have.
            foreach (var kind in new[] { "cover/big.png", "cover/small.png", "video/video.mp4", "logo/logo.png" })
            {
                stub.Media[$"/assets/romm/resources/roms/1/{id}/{kind}"] = new byte[64];
            }
        }

        return stub;
    }

    /// <summary>A set that is already resolved, with the metadata a resolve would have written.</summary>
    private void Seed(string name, int games)
    {
        var set = _session.Store.SyncSets.Add(
            new SyncSetDefinition { Name = name, Scope = CatalogScopeKind.Platform, ScopeValue = "1" },
            Now);

        var members = Enumerable.Range(1, games).Select(id => new SyncSetMember
        {
            RomId = id,
            State = MemberState.Member,
            Folder = "psx",
            PlatformSlug = "psx",
            FsName = $"Game {id}.chd",
            FsExtension = "chd",
            SizeBytes = 1024,
            DisplayName = $"Game {id}",
            SortKey = $"game {id}",
            Position = id,
            ResolvedAt = Now,
        }).ToList();

        _session.Store.SyncSets.ReplaceMembers(set.Id, [.. members], $"{games} games", Now, complete: true);

        foreach (var member in members)
        {
            _session.Store.Metadata.Record(new GameMetadata
            {
                RomId = member.RomId,
                Folder = "psx",
                FsName = member.FsName,
                Name = member.DisplayName,
                MediaPaths = new Dictionary<MediaKind, string>
                {
                    [MediaKind.Image] = $"/assets/romm/resources/roms/1/{member.RomId}/cover/big.png",
                },
            });
        }
    }
}
