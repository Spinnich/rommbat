using System.Diagnostics;
using System.Globalization;
using RomM.Client;
using RomM.Client.Catalog;
using RomMBat.Core;
using RomMBat.Core.Content;
using RomMBat.Core.Identity;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Sets;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using RomMBat.UI.Input;
using RomMBat.UI.Screens;
using RomMBat.UI.Shell;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The sets screens, driven with the gamepad map alone and no window.
/// </summary>
/// <remarks>
/// <b>Every screen is walked as a person walks it.</b> Screens carry no Avalonia types, which
/// is what makes "no primary flow requires a mouse" checkable rather than asserted, and the
/// only reason that stays true is tests like these actually doing it.
/// </remarks>
public sealed class SetsScreenTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);
    private static readonly Uri Origin = new("https://romm.invalid/");

    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();
    private readonly InstallSession _session;

    public SetsScreenTests()
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

    // ---- reachable and leavable with the pad alone ----

    [Fact]
    public void A_set_can_be_defined_from_the_status_screen_with_nothing_but_the_pad()
    {
        // The picker offers what platform_map holds, which a sync fills. A fresh install has
        // none, and that empty state is covered separately.
        SeedPlatform(4, "snes");

        var navigator = new Navigator(Status());

        // Start on the status screen opens the sets list, Start again opens the editor.
        navigator.Handle(NavAction.Start);
        Assert.IsType<ListScreen>(navigator.Current);

        navigator.Handle(NavAction.Start);
        var editor = Assert.IsType<SetEditorViewModel>(navigator.Current);
        Assert.True(editor.IsNew);

        // Pick the platform. There is no name to type: a platform is named by RomM already and
        // the set takes that name, so this whole flow is pick and create.
        MoveTo(editor, "Platform");
        navigator.Handle(NavAction.Accept);

        var platforms = Assert.IsType<ListScreen>(navigator.Current);
        Assert.NotEmpty(platforms.Rows);
        navigator.Handle(NavAction.Accept);
        Assert.Same(editor, navigator.Current);

        navigator.Handle(NavAction.Start);

        // Onto the set that was just made, resolving it, rather than back to the list. A set
        // that has never resolved holds nothing, so landing on the list would ask for one more
        // press to get the thing just described. The on-screen keyboard was never opened.
        using var resolving = Assert.IsType<ResolveViewModel>(navigator.Current);

        // The set is underneath, so backing out of the resolve reaches it and not the list.
        Assert.Equal(4, navigator.Depth);
        navigator.Handle(NavAction.Back);
        Assert.IsType<ListScreen>(navigator.Current);

        var made = new SyncSetService(_session).List();
        Assert.Equal(made[0].Set.Name, navigator.Current.Title);
        Assert.Single(made);
        Assert.Equal(new SyncSetService(_session).PlatformsKnownHere()[0].Label, made[0].Set.Name);
    }

    [Fact]
    public void Every_sets_screen_can_be_left_by_going_back()
    {
        Seed("leavable");

        var navigator = new Navigator(Status());

        navigator.Handle(NavAction.Start);
        navigator.Handle(NavAction.Accept);
        Assert.IsType<ListScreen>(navigator.Current);

        navigator.Handle(NavAction.Accept);
        Assert.IsType<SetEditorViewModel>(navigator.Current);

        // Back all the way out, and the last back leaves RomMBat rather than getting stuck.
        Assert.True(navigator.Handle(NavAction.Back));
        Assert.True(navigator.Handle(NavAction.Back));
        Assert.True(navigator.Handle(NavAction.Back));
        Assert.Equal(1, navigator.Depth);
        Assert.False(navigator.Handle(NavAction.Back));
        Assert.True(navigator.HasExited);
    }

    [Fact]
    public void The_budget_screen_is_reachable_and_saves_both_settings()
    {
        var navigator = new Navigator(Status());

        navigator.Handle(NavAction.Alternate);
        var budget = Assert.IsType<BudgetViewModel>(navigator.Current);

        // The floor leads, because it is the one that is always on. The budget is an extra cap
        // for a shared drive and starts unset, and listing it first made the optional one look
        // like the primary one.
        Assert.Equal("Always leave free", budget.Rows[0].Label);
        Assert.Equal("Limit RomMBat to", budget.Rows[1].Label);
        Assert.Equal("no limit", budget.Rows[1].Value);

        Assert.False(budget.IsDirty);
        navigator.Handle(NavAction.Right);
        Assert.True(budget.IsDirty);

        navigator.Handle(NavAction.Start);

        // The floor is written, because that is the row that moved and it is the one always in
        // force. The budget is left unset, because opening a screen must not invent a cap
        // nobody asked for: an unset budget means "no extra limit", and turning it into a
        // number would silently start refusing downloads.
        // One rung above the 2 GB default, which is where a single step right lands.
        Assert.Equal(4L << 30, _session.Store.Settings.GetInt64(SettingStore.FreeSpaceFloorBytes));
        Assert.Null(_session.Store.Settings.GetInt64(SettingStore.ContentMaxBytes));
    }

    [Fact]
    public void The_budget_can_be_set_and_then_it_is_written()
    {
        var navigator = new Navigator(Status());
        navigator.Handle(NavAction.Alternate);

        navigator.Handle(NavAction.Down);
        navigator.Handle(NavAction.Right);
        navigator.Handle(NavAction.Start);

        // Both are persisted once either is touched, so the saved state does not depend on
        // which row somebody happened to move.
        Assert.NotNull(_session.Store.Settings.GetInt64(SettingStore.ContentMaxBytes));
        Assert.NotNull(_session.Store.Settings.GetInt64(SettingStore.FreeSpaceFloorBytes));
    }


    [Fact]
    public void Deleting_a_set_says_what_each_answer_does_before_it_happens()
    {
        Seed("doomed");

        var confirm = Assert.IsType<ListScreen>(SetsScreens.ConfirmDelete(_session, "doomed"));

        // Two answers, because deleting a set and keeping its games is a legitimate thing to
        // want, and removal is a choice rather than a consequence. Both sentences are on the
        // confirmation rather than on a screen afterwards, because a warning after the act is
        // not a warning.
        Assert.Equal(2, confirm.Rows.Count);
        Assert.Contains(
            confirm.Rows,
            row => row.Detail is { } detail
                && detail.Contains("Nothing on disk is touched", StringComparison.Ordinal));
        Assert.Contains(
            confirm.Rows,
            row => row.Detail is { } detail
                && detail.Contains("Saves and save states are never removed", StringComparison.Ordinal));
    }

    [Fact]
    public void Keeping_the_games_deletes_the_set_and_touches_no_file()
    {
        Seed("doomed");

        var confirm = Assert.IsType<ListScreen>(SetsScreens.ConfirmDelete(_session, "doomed"));

        // The second row, which is the one that keeps them. Reached by moving rather than by
        // index, so a row added above it does not silently retarget this press.
        confirm.Handle(NavAction.Down);

        Assert.Equal(ScreenCommandKind.Pop, confirm.Handle(NavAction.Accept).Kind);
        Assert.Empty(new SyncSetService(_session).List());
    }

    [Fact]
    public void Deleting_a_set_lands_back_on_the_list_rather_than_stranding()
    {
        Seed("doomed");
        Seed("survivor");

        var navigator = new Navigator(Status());
        navigator.Handle(NavAction.Start);
        var list = Assert.IsType<ListScreen>(navigator.Current);

        navigator.Handle(NavAction.Accept);
        navigator.Handle(NavAction.Alternate);

        // Down to "leave the games where they are", which is the answer that does not open a
        // preview. The removal half has its own tests, because it is minutes of work.
        navigator.Handle(NavAction.Down);
        navigator.Handle(NavAction.Accept);

        // Back on the list, with the deleted set gone from it. It used to land on a message
        // screen whose only way onward was to leave RomMBat, and the detail screen underneath
        // was describing a set that no longer existed.
        Assert.Same(list, navigator.Current);
        Assert.Equal(2, navigator.Depth);
        Assert.Single(list.Rows);
        Assert.Equal("survivor", list.Rows[0].Label);
    }

    [Fact]
    public async Task The_removal_preview_names_what_goes_and_what_is_kept()
    {
        var doomed = Seed("doomed");
        var wanted = Seed("wanted");

        SeedFile(1, "snes", "shared.sfc", 2_048);
        SeedFile(2, "snes", "only.sfc", 1_024);

        Members(doomed, 1, 2);
        Members(wanted, 1);

        var preview = Assert.IsType<ListScreen>(SetsScreens.ConfirmRemoval(_session, "doomed"));
        await Wait(() => !preview.IsLoading);

        var shown = Render(preview);

        // The game the other set still wants is kept and the reason names that set, beside
        // whatever SaveGuard would have said. Without it, deleting one set silently removes a
        // game a set the user never touched still wants, and the next sync fetches it again.
        Assert.Contains(shown, text => text.Contains("still in 'wanted'", StringComparison.Ordinal));
        Assert.Contains(shown, text => text.Contains("only.sfc", StringComparison.Ordinal));

        // Stated before the press, because it is the thing a person on a sofa cannot otherwise
        // find out, and because it is a schema guarantee rather than an intention.
        Assert.Contains(
            shown,
            text => text.Contains("Saves and save states are never removed", StringComparison.Ordinal));

        // Nothing has happened yet. The preview is the screen, and the footer is what commits.
        Assert.True(File.Exists(Path.Combine(_tree.Root, "roms", "snes", "shared.sfc")));
        Assert.True(File.Exists(Path.Combine(_tree.Root, "roms", "snes", "only.sfc")));
        Assert.Equal(2, new SyncSetService(_session).List().Count);
    }

    [Fact]
    public async Task Removing_takes_the_games_it_named_and_leaves_the_one_it_kept()
    {
        var doomed = Seed("doomed");
        var wanted = Seed("wanted");

        SeedFile(1, "snes", "shared.sfc", 2_048);
        SeedFile(2, "snes", "only.sfc", 1_024);

        Members(doomed, 1, 2);
        Members(wanted, 1);

        var preview = Assert.IsType<ListScreen>(SetsScreens.ConfirmRemoval(_session, "doomed"));
        await Wait(() => !preview.IsLoading);

        // Accept, not Start: a yes-or-no screen is answered with the confirm button now.
        var applying = Assert.IsType<ListScreen>(preview.Handle(NavAction.Accept).Screen);
        await Wait(() => !applying.IsLoading);

        Assert.False(File.Exists(Path.Combine(_tree.Root, "roms", "snes", "only.sfc")));
        Assert.True(File.Exists(Path.Combine(_tree.Root, "roms", "snes", "shared.sfc")));

        // The set goes last, after its files. Either order self-heals; this one never claims to
        // have removed something that is still on the disk.
        Assert.Equal(["wanted"], new SyncSetService(_session).List().Select(summary => summary.Set.Name));
    }

    /// <summary>
    /// Removing a set's games lands back on the sets list, not on three stale screens.
    /// </summary>
    /// <remarks>
    /// Found on a hands-on pass. The set is gone by the time this screen is reached, so the
    /// preview, the confirmation and the set's own detail all describe something that no longer
    /// exists, and leaving them on the stack was four presses through three of them to get to
    /// the list. 7b-2a fixed exactly this on the keep-the-games path and adding two screens
    /// above it brought it back.
    /// </remarks>
    [Fact]
    public async Task Removing_a_sets_games_lands_back_on_the_sets_list()
    {
        var doomed = Seed("doomed");
        Seed("survivor");

        SeedFile(1, "snes", "only.sfc", 1_024);
        Members(doomed, 1);

        var navigator = new Navigator(Status());
        navigator.Handle(NavAction.Start);
        var list = Assert.IsType<ListScreen>(navigator.Current);

        navigator.Handle(NavAction.Accept);
        navigator.Handle(NavAction.Alternate);
        navigator.Handle(NavAction.Accept);

        var preview = Assert.IsType<ListScreen>(navigator.Current);
        await Wait(() => !preview.IsLoading);

        navigator.Handle(NavAction.Accept);
        var applying = Assert.IsType<ListScreen>(navigator.Current);
        await Wait(() => !applying.IsLoading);

        navigator.Handle(NavAction.Back);

        Assert.Same(list, navigator.Current);
        Assert.Equal(2, navigator.Depth);
        Assert.Equal(["survivor"], list.Rows.Select(row => row.Label));
    }

    /// <summary>
    /// Anything that says it is working ends in an ellipsis.
    /// </summary>
    /// <remarks>
    /// A screen that states an ongoing action without one reads as a finished sentence, so a
    /// person cannot tell a screen that is working from a screen that has stopped. Asked for on
    /// a hands-on pass, swept rather than checked at one site because every screen that loads
    /// has one of these and a new one is the easy thing to forget.
    /// </remarks>
    [Fact]
    public void Every_loading_message_says_it_is_still_going()
    {
        using var built = AllScreens();

        foreach (var screen in built)
        {
            if (screen is not ListScreen { Load: not null } loading)
            {
                continue;
            }

            Assert.EndsWith(
                "...",
                loading.LoadingMessage,
                StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A screen of facts never selects anything, whether it fits or scrolls.
    /// </summary>
    /// <remarks>
    /// A hands-on pass called this out twice, and both times the words were "its information
    /// shown as navigable buttons even though they're not". The first report was a highlight
    /// walking rows that cannot be picked; with the highlight gone for a short list, the second
    /// was that the rows were still drawn as filled panels with a ring round them, on a screen
    /// long enough to scroll. Both are the same mistake, which is dressing a pane of text as a
    /// menu, and the fix is that a reading list has no cursor at all and scrolls by an offset.
    /// <para>
    /// The renderer half cannot be asserted here, because nothing in this project draws. What
    /// this pins is that no row is ever selected, which is what the renderer keys its fill on.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_screen_of_facts_never_selects_a_row()
    {
        foreach (var count in new[] { 4, 30 })
        {
            var screen = new ListScreen(
                $"{count} facts",
                [.. Enumerable.Range(1, count).Select(n =>
                    new ListRow(n.ToString(CultureInfo.CurrentCulture), null, "a fact", false))],
                _ => ScreenCommand.Stay)
            {
                Reading = true,
            };

            Assert.Equal(-1, screen.Cursor);

            screen.Handle(NavAction.Down);
            screen.Handle(NavAction.Down);

            Assert.Equal(-1, screen.Cursor);

            // A list that fits has nothing to scroll; one that does not scrolls by the press.
            Assert.Equal(count <= ListWindow.ReadingCapacity ? 0 : 2, screen.Window.Start);
        }
    }

    /// <summary>An empty set can be deleted, which a hands-on pass said it could not.</summary>
    [Fact]
    public async Task An_empty_set_can_be_deleted()
    {
        Seed("empty");

        var confirm = Assert.IsType<ListScreen>(SetsScreens.ConfirmDelete(_session, "empty"));

        // The removal answer first, which is where a person lands.
        var preview = confirm.Handle(NavAction.Accept).Screen;

        if (preview is ListScreen loaded)
        {
            await Wait(() => !loaded.IsLoading);
            Assert.NotEqual(ScreenCommandKind.Stay, loaded.Handle(NavAction.Accept).Kind);
        }

        Assert.Empty(new SyncSetService(_session).List());
    }

    [Fact]
    public void Backing_out_of_the_delete_confirmation_keeps_the_set()
    {
        Seed("kept");

        Assert.Equal(ScreenCommandKind.Pop, SetsScreens.ConfirmDelete(_session, "kept").Handle(NavAction.Back).Kind);
        Assert.Single(new SyncSetService(_session).List());
    }

    // ---- no screen names a button, swept rather than sampled ----

    [Fact]
    public void Nothing_any_sets_screen_shows_names_a_face_button()
    {
        Seed("swept");

        // A sweep, not a check of one site. Round 8 of stage 7b-1 found "Press A" one field
        // over from where naming a button was structurally impossible, and on a Switch Pro the
        // button printed A is es_input.cfg's b, which closes RomMBat. What catches a mistake
        // that moved is a test that looks at every string a screen produces.
        foreach (var text in EverythingShown())
        {
            foreach (var forbidden in new[]
            {
                "Press A", "Press B", "Press X", "Press Y",
                "button A", "button B", "button X", "button Y",
                "Cross", "Circle", "Square", "Triangle",
            })
            {
                Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void No_sets_screen_promises_an_action_that_nothing_is_bound_to()
    {
        var bound = NavRepeat.Bound;

        using var built = AllScreens();

        foreach (var screen in built)
        {
            Assert.All(screen.Hints, hint => Assert.Contains(hint.Action, bound));
        }
    }

    // ---- what the hands-on pass found ----

    /// <summary>
    /// Every screen offers a verb exactly when that verb works, for every action, both ways.
    /// </summary>
    /// <remarks>
    /// <b>This replaces an Accept-only sweep rather than sitting beside it.</b> That one asserted
    /// half of one action's half of the rule and could not settle a screen first, so it started
    /// failing the moment a verb became conditional on a preview landing. Two tests where one
    /// states the rule is how a rule comes to be enforced in one place and broken in the place
    /// beside it, which is this repository's recurring shape.
    /// <b>The sweep above only ever looked at Accept, and three screens got the same rule wrong
    /// on Start.</b> A hands-on pass found all three in one sitting: the file-check screen and
    /// the set-removal screen answered Start and never offered it, so the footer named nothing
    /// but Back while the verb quietly worked; the per-game removal screen offered it always,
    /// including when the preview had just said the game would stay, so the press walked through
    /// a second screen and removed nothing.
    /// <para>
    /// Both halves are one rule and this asserts both. A footer promising an action that does
    /// nothing and a footer silent about one that does are the same defect pointed two ways.
    /// </para>
    /// <para>
    /// Loaded screens are settled first, because the verb these three got wrong is the one that
    /// only becomes possible once a preview has come back, and a screen asked while still
    /// loading would be asked the easy question.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Every_screen_offers_a_verb_exactly_when_that_verb_works()
    {
        Seed("hinted");
        SeedPlatform(4, "snes");

        using var built = AllScreens();

        foreach (var screen in built)
        {
            if (screen is ListScreen loaded)
            {
                await Wait(() => !loaded.IsLoading);
            }

            foreach (var action in new[] { NavAction.Accept, NavAction.Start, NavAction.Alternate, NavAction.Extra })
            {
                var offered = screen.Hints.Any(hint => hint.Action == action);

                var before = Render(screen);
                var navigated = screen.Handle(action).Kind != ScreenCommandKind.Stay;

                // "Did something" is navigating **or** changing what the screen shows. The set
                // editor answers Start on an invalid draft by staying put and saying why, which
                // is a press that plainly did something, and a test reading only the command
                // kind would call that a broken promise.
                var answered = navigated || !Render(screen).SequenceEqual(before, StringComparer.Ordinal);

                Assert.False(
                    answered && !offered,
                    $"{screen.GetType().Name} acts on {action} and its footer never says so");

                Assert.False(
                    offered && !answered,
                    $"{screen.GetType().Name} offers {action} and the press does nothing at all");
            }
        }
    }

    [Fact]
    public void The_set_detail_screen_offers_its_edit_hint()
    {
        var set = Seed("detail");

        // The specific site, kept beside the sweep above. Every row on this screen is
        // informational, so the cursor has nowhere to sit, which is exactly the shape that
        // suppressed the hint.
        var detail = SetsScreens.Detail(_session, set.Name, null);

        Assert.Contains(detail.Hints, hint => hint.Action == NavAction.Accept);
        Assert.Equal(ScreenCommandKind.Push, detail.Handle(NavAction.Accept).Kind);
    }

    [Fact]
    public void A_set_created_above_the_list_is_on_it_when_the_editor_closes()
    {
        SeedPlatform(4, "snes");

        var navigator = new Navigator(Status());
        navigator.Handle(NavAction.Start);

        var list = Assert.IsType<ListScreen>(navigator.Current);
        Assert.Empty(list.Rows);

        // Created underneath the editor, which is the case that was broken: the list captured
        // its rows once and went on showing the sets from before until it was rebuilt.
        new SyncSetService(_session).Add(
            new SetDraft { Name = "fresh", Scope = CatalogScopeKind.Platform, ScopeValue = "4" },
            Now);

        navigator.Handle(NavAction.Start);
        Assert.IsType<SetEditorViewModel>(navigator.Current);
        navigator.Handle(NavAction.Back);

        Assert.Same(list, navigator.Current);
        Assert.Single(list.Rows);
        Assert.Equal("fresh", list.Rows[0].Label);
    }

    [Fact]
    public void The_folder_row_is_offered_only_when_the_platform_cannot_answer_for_itself()
    {
        SeedPlatform(4, "snes");

        var editor = SetEditorViewModel.ForNew(_session);

        // Two rows that looked alike were doing different jobs. Platform is the scope's own
        // value; Folder is a RomM-to-RetroBat mapping override that belongs in platform_map and
        // gets a screen of its own in 7b-3. Offering it on every set made a global setting look
        // like a per-set one, and it is meaningless on a filter, which can span platforms.
        Assert.DoesNotContain(editor.Rows, row => row.Label == "Folder");
        Assert.False(editor.NeedsFolderChoice);
    }

    [Fact]
    public void A_set_that_already_carries_a_folder_override_keeps_showing_it()
    {
        var folder = new SyncSetService(_session).FoldersKnownHere()[0];

        var set = new SyncSetService(_session).Add(
            new SetDraft
            {
                Name = "override",
                Scope = CatalogScopeKind.Platform,
                ScopeValue = "4",
                FolderOverride = folder,
            },
            Now).Set!;

        // One made from the console stays visible and changeable, or hiding the row would strand
        // whoever set it.
        var editor = SetEditorViewModel.ForExisting(_session, set);

        Assert.True(editor.NeedsFolderChoice);
        Assert.Contains(editor.Rows, row => row.Label == "Folder");
    }

    [Fact]
    public void The_editor_asks_for_nothing_but_what_the_set_points_at()
    {
        SeedPlatform(4, "snes");

        var editor = SetEditorViewModel.ForNew(_session);

        // No caps. The bound a person sets is the install-wide disk budget, and a per-set cap
        // made an optional refinement look like a decision every set needs. Which 10 of 9,196
        // is not a question any ordering answers well.
        Assert.DoesNotContain(editor.Rows, row => row.Label is "Most games" or "Most space" or "Keep first");
        Assert.All(editor.Rows, row => Assert.False(row.Steps));

        // No Name row either. A platform and a collection are named by RomM already, and the
        // set takes that name, so only a filter needs one typed.
        Assert.Equal(["Scope", "Platform"], editor.Rows.Select(row => row.Label));
    }

    [Fact]
    public void Only_a_filter_set_asks_for_a_name()
    {
        SeedPlatform(4, "snes");

        var editor = SetEditorViewModel.ForNew(_session);
        Assert.DoesNotContain(editor.Rows, row => row.Label == "Name");

        // A filter is the one scope that is not a mirror of something RomM has already named.
        var scopes = Assert.IsType<ListScreen>(OpenRow(editor, "Scope"));
        MoveTo(scopes, SyncSetStore.ScopeText(CatalogScopeKind.Filter));
        scopes.Handle(NavAction.Accept);

        Assert.Contains(editor.Rows, row => row.Label == "Name");
        Assert.Contains(editor.Rows, row => row.Label == "Search for");
    }

    [Fact]
    public void A_set_with_no_caps_does_not_spend_a_line_saying_so()
    {
        SeedPlatform(4, "snes");

        var uncapped = new SyncSetService(_session).Add(
            new SetDraft { Name = "uncapped", Scope = CatalogScopeKind.Platform, ScopeValue = "4" },
            Now).Set!;

        var detail = Assert.IsType<ListScreen>(SetsScreens.Detail(_session, uncapped.Name, null));
        Assert.DoesNotContain(detail.Rows, row => row.Label == "Limits");

        var list = Assert.IsType<ListScreen>(SetsScreens.List(_session));
        Assert.DoesNotContain("no game cap", list.Rows[0].Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_set_that_does_have_caps_still_shows_them()
    {
        SeedPlatform(4, "snes");

        var capped = new SyncSetService(_session).Add(
            new SetDraft
            {
                Name = "capped",
                Scope = CatalogScopeKind.Platform,
                ScopeValue = "4",
                MaxGames = 40,
            },
            Now).Set!;

        // The interface cannot make one, but sets add can, and hiding a limit somebody set
        // would leave them wondering why their set stopped at forty.
        var detail = Assert.IsType<ListScreen>(SetsScreens.Detail(_session, capped.Name, null));
        Assert.Contains(detail.Rows, row => row.Label == "Limits");
    }

    [Fact]
    public void Editing_a_set_from_the_console_leaves_the_caps_it_was_given()
    {
        var folder = new SyncSetService(_session).FoldersKnownHere()[0];

        var set = new SyncSetService(_session).Add(
            new SetDraft
            {
                Name = "capped",
                Scope = CatalogScopeKind.Platform,
                ScopeValue = "4",
                MaxGames = 40,
                MaxBytes = 8L << 30,
                Ordering = SetOrdering.Name,
                FolderOverride = folder,
            },
            Now).Set!;

        // The screen no longer shows caps, so it must not send the cleared values a hidden row
        // would have produced. Opening a screen must never wipe a limit somebody set elsewhere.
        var editor = SetEditorViewModel.ForExisting(_session, set);
        editor.Handle(NavAction.Start);

        var after = new SyncSetService(_session).Show("capped")!.Set;

        Assert.Equal(40, after.MaxGames);
        Assert.Equal(8L << 30, after.MaxBytes);
        Assert.Equal(SetOrdering.Name, after.Ordering);
    }

    [Fact]
    public void The_detail_screen_offers_an_edit_only_when_there_is_something_to_edit()
    {
        SeedPlatform(4, "snes");

        var plain = new SyncSetService(_session).Add(
            new SetDraft { Name = "plain", Scope = CatalogScopeKind.Platform, ScopeValue = "4" },
            Now).Set!;

        // With caps gone, the folder is the only editable thing left, and most sets have none.
        // Offering "Change folder" on a screen where it opens an empty form is a footer
        // promising nothing, which is the same defect as a footer that promises nothing where
        // an action does exist.
        var detail = SetsScreens.Detail(_session, plain.Name, null);

        Assert.DoesNotContain(detail.Hints, hint => hint.Action == NavAction.Accept);
        Assert.Equal(ScreenCommandKind.Stay, detail.Handle(NavAction.Accept).Kind);
    }

    [Fact]
    public void A_new_set_is_named_after_the_platform_it_mirrors()
    {
        SeedPlatform(4, "snes");

        var editor = SetEditorViewModel.ForNew(_session);

        // No Name row at all: a platform and a collection are named by RomM already, and making
        // somebody spell one out on a d-pad to mirror it is work for nothing. The name is only
        // observable once the set exists.
        Assert.DoesNotContain(editor.Rows, row => row.Label == "Name");

        var picker = Assert.IsType<ListScreen>(OpenRow(editor, "Platform"));
        picker.Handle(NavAction.Accept);
        editor.Handle(NavAction.Start);

        var made = new SyncSetService(_session).List();
        Assert.Single(made);
        Assert.Equal(new SyncSetService(_session).PlatformsKnownHere()[0].Label, made[0].Set.Name);
    }

    [Fact]
    public void A_name_somebody_typed_is_not_overwritten_by_a_later_choice()
    {
        SeedPlatform(4, "snes");

        // Only a filter scope offers a name to type, so that is the path that can produce a
        // hand-typed one. Switching back to a platform afterwards must not silently replace it:
        // overwriting a name somebody entered is worse than never asking for one.
        var editor = FilterEditor();
        MoveTo(editor, "Name");

        var keyboard = Assert.IsType<OnScreenKeyboard>(editor.Handle(NavAction.Accept).Screen);
        keyboard.Handle(NavAction.Accept);
        keyboard.Handle(NavAction.Start);

        var typed = editor.Rows.Single(row => row.Label == "Name").Value;
        Assert.NotEqual("not set", typed);

        var scopes = Assert.IsType<ListScreen>(OpenRow(editor, "Scope"));
        MoveTo(scopes, SyncSetStore.ScopeText(CatalogScopeKind.Platform));
        scopes.Handle(NavAction.Accept);

        var platforms = Assert.IsType<ListScreen>(OpenRow(editor, "Platform"));
        platforms.Handle(NavAction.Accept);

        // A platform set shows no Name row at all, so what it was named can only be read off
        // the set once it exists. That is the assertion that matters anyway: the picker
        // suggests a name for a set that has none, and this one has one.
        editor.Handle(NavAction.Start);

        var made = new SyncSetService(_session).List();
        Assert.Single(made);
        Assert.Equal(typed, made[0].Set.Name);
    }

    [Fact]
    public void Resolving_from_the_detail_screen_updates_what_it_shows()
    {
        var set = Seed("stale");
        var detail = SetsScreens.Detail(_session, set.Name, null);
        var list = Assert.IsType<ListScreen>(detail);

        Assert.Contains(list.Rows, row => row.Label == "Holds" && row.Value == "0 games, 0 B");

        // Stand in for what a resolve writes. It happens on a screen above this one, which used
        // to leave the counts and the last-resolved time saying what they said before it ran.
        _session.Store.SyncSets.ReplaceMembers(
            set.Id,
            [Member(set)],
            "1 game",
            Now,
            complete: true);

        list.Returned();

        Assert.Contains(list.Rows, row => row.Label == "Holds" && row.Value!.StartsWith("1 game", StringComparison.Ordinal));
    }

    [Fact]
    public void Every_set_can_be_resolved_at_once_from_the_list()
    {
        Seed("one");
        Seed("two");

        var navigator = new Navigator(Status());
        navigator.Handle(NavAction.Start);

        // Doing them one at a time is the hassle a person notices first, and the service
        // already walks a list.
        //
        // Moved from Alternate to Extra in 7b-2b, deliberately: syncing is what a set is for
        // and it took the first-tier verb. Resolving alone stays offered because it is how a
        // person finds out what a set holds without spending disk on it, and a sync re-resolves
        // on the way past anyway, so the two are not a choice anybody has to make.
        var command = navigator.Current.Handle(NavAction.Extra);
        using var resolve = Assert.IsType<ResolveViewModel>(command.Screen);

        Assert.Contains("2", resolve.Title, StringComparison.Ordinal);
    }

    [Fact]
    public void Syncing_everything_is_the_first_tier_verb_on_the_list()
    {
        Seed("one");
        Seed("two");

        var navigator = new Navigator(Status());
        navigator.Handle(NavAction.Start);

        using var sync = Assert.IsType<SyncViewModel>(
            navigator.Current.Handle(NavAction.Alternate).Screen);

        Assert.Contains("2", sync.Title, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_all_is_not_offered_when_there_is_nothing_to_resolve()
    {
        var list = SetsScreens.List(_session);

        // An empty list offering to resolve or sync everything is a footer promising a no-op.
        Assert.Empty(Assert.IsType<ListScreen>(list).Rows);
        Assert.Equal(ScreenCommandKind.Stay, list.Handle(NavAction.Extra).Kind);
        Assert.Equal(ScreenCommandKind.Stay, list.Handle(NavAction.Alternate).Kind);
    }

    [Fact]
    public void Every_screen_with_more_rows_than_fit_keeps_its_cursor_inside_its_window()
    {
        // The windowing arithmetic used to live in the renderer, so a screen that never called
        // it drew every row it had and everything past the height of the display went off it.
        // That happened to the folder picker, was fixed there, and happened again to the set
        // editor the moment a filter grew to twenty-two rows. The suite could not see either,
        // because it does not render. The window is a property now, and this walks it.
        var editor = FilterEditor();
        Assert.True(
            editor.Rows.Count > ListWindow.Capacity,
            "this test is vacuous unless the editor is longer than one screen");

        for (var step = 0; step < editor.Rows.Count; step++)
        {
            var window = editor.Window;

            Assert.InRange(editor.Cursor, window.Start, window.Start + window.Count - 1);
            Assert.Equal(editor.Rows.Count, window.Above + window.Count + window.Below);

            editor.Handle(NavAction.Down);
        }

        var list = Assert.IsType<ListScreen>(SetsScreens.List(_session));
        Assert.Equal(list.Rows.Count, list.Window.Above + list.Window.Count + list.Window.Below);
    }

    [Fact]
    public void A_filter_offers_every_facet_RomM_does()
    {
        var editor = FilterEditor();

        // It offered five of eleven facets and two of ten properties, on the reasoning that
        // those were the ones a set could store. They all store: this is one JSON column and
        // one dictionary, so the subset was a subset of nothing.
        var labels = editor.Rows.Select(row => row.Label).ToList();

        Assert.Contains("Search for", labels);
        Assert.All(FilterFacet.Multi, facet => Assert.Contains(facet, labels));
        Assert.All(FilterFacet.Properties, property => Assert.Contains(property, labels));
    }

    [Fact]
    public void Every_facet_and_property_offered_reaches_the_query_string()
    {
        // The screen and the wire are driven off the same two lists, and this is the assertion
        // that says so: a facet added to FilterFacet with no home in CatalogFilter would show
        // as a row that changes nothing.
        Assert.Equal(CatalogFilter.Facets.Count, FilterFacet.Multi.Count);
        Assert.Equal(CatalogFilter.Properties.Count, FilterFacet.Properties.Count);

        foreach (var label in FilterFacet.Multi)
        {
            var key = FilterFacet.KeyOf(label);
            Assert.Contains(key, CatalogFilter.Facets);

            var query = new CatalogQuery
            {
                Scope = CatalogScopeKind.Filter,
                Filter = new CatalogFilter().WithValues(key, ["x"]),
            }.ToQueryString(limit: 1, offset: 0);

            Assert.Contains($"{key}=x", query, StringComparison.Ordinal);
        }

        foreach (var label in FilterFacet.Properties)
        {
            var key = FilterFacet.KeyOf(label);
            Assert.Contains(key, CatalogFilter.Properties);

            var query = new CatalogQuery
            {
                Scope = CatalogScopeKind.Filter,
                Filter = new CatalogFilter().WithProperty(key, true),
            }.ToQueryString(limit: 1, offset: 0);

            Assert.Contains($"{key}=true", query, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_logic_operator_reaches_the_wire_only_when_it_is_not_the_default()
    {
        var chosen = new CatalogFilter().WithValues("genres", ["Platform", "Puzzle"]);

        // Any is RomM's own default, so sending it would add a parameter to every filter query
        // for no change in meaning and would pin the default where a later one could not reach.
        var plain = new CatalogQuery { Scope = CatalogScopeKind.Filter, Filter = chosen }
            .ToQueryString(limit: 1, offset: 0);

        Assert.DoesNotContain("genres_logic", plain, StringComparison.Ordinal);

        var all = new CatalogQuery
        {
            Scope = CatalogScopeKind.Filter,
            Filter = chosen with
            {
                Logic = new Dictionary<string, FilterLogic>(StringComparer.Ordinal)
                {
                    ["genres"] = FilterLogic.All,
                },
            },
        }.ToQueryString(limit: 1, offset: 0);

        Assert.Contains("genres_logic=all", all, StringComparison.Ordinal);
    }

    [Fact]
    public void The_logic_operator_is_set_inside_the_facet_it_belongs_to()
    {
        var editor = FilterEditor();
        editor._facetValues = Library();

        var picker = Assert.IsType<ListScreen>(OpenRow(editor, FilterFacet.Genres));

        // The first row, because an operator belongs to its facet and means nothing without
        // it, and eleven more editor rows would double a list that is already long. Name on
        // the left and the one live setting on the right, like every other row: printing all
        // three choices there read as three things being on at once.
        Assert.Equal("Match", picker.Rows[0].Label);
        Assert.Equal("any of", picker.Rows[0].Value);

        Assert.Equal(ScreenCommandKind.Stay, picker.Handle(NavAction.Accept).Kind);
        Assert.Equal("all of", picker.Rows[0].Value);

        picker.Handle(NavAction.Accept);
        Assert.Equal("none of", picker.Rows[0].Value);

        picker.Handle(NavAction.Accept);
        Assert.Equal("any of", picker.Rows[0].Value);
    }

    [Fact]
    public void The_line_above_a_facet_picker_follows_the_operator_rather_than_asserting_one()
    {
        var editor = FilterEditor();
        editor._facetValues = Library();

        var picker = Assert.IsType<ListScreen>(OpenRow(editor, FilterFacet.Genres));

        // It was a fixed string reading "matching any of", so it went on saying that while the
        // row under it said none. Two statements of the same fact, one of them stale.
        Assert.Equal("Games matching any of the genres chosen here.", picker.Note?.Invoke());

        picker.Handle(NavAction.Accept);
        picker.Handle(NavAction.Accept);

        Assert.Equal("Games matching none of the genres chosen here.", picker.Note?.Invoke());
    }

    [Fact]
    public void The_operator_row_does_not_swallow_the_value_under_it()
    {
        var editor = FilterEditor();
        editor._facetValues = Library();

        var picker = Assert.IsType<ListScreen>(OpenRow(editor, FilterFacet.Genres));

        // An off-by-one here would tick the wrong genre, which is the kind of defect a person
        // finds three screens later when the resolve returns the wrong games.
        picker.Handle(NavAction.Down);
        Assert.Equal("Platform", picker.Rows[1].Label);

        picker.Handle(NavAction.Accept);
        Assert.Equal("chosen", picker.Rows[1].Value);
    }

    [Fact]
    public void A_facet_row_names_its_operator_only_when_it_is_not_the_default()
    {
        var editor = FilterEditor();
        editor._facetValues = Library();

        var picker = Assert.IsType<ListScreen>(OpenRow(editor, FilterFacet.Genres));
        picker.Handle(NavAction.Down);
        picker.Handle(NavAction.Accept);

        // Any is the default, so naming it on every row would be noise on ten rows to make one
        // stand out. It is named as soon as it is something else.
        var row = MoveTo(editor, FilterFacet.Genres);
        Assert.Equal("Platform", editor.Rows[row].Value);

        picker.Handle(NavAction.Up);
        picker.Handle(NavAction.Accept);

        row = MoveTo(editor, FilterFacet.Genres);
        Assert.Equal("Platform, all of", editor.Rows[row].Value);
    }

    /// <summary>Facet values as a library would report them, so a picker has rows offline.</summary>
    private static Dictionary<string, IReadOnlyList<string>> Library() =>
        FilterFacet.Multi.ToDictionary(
            facet => facet,
            IReadOnlyList<string> (_) => ["Platform", "Puzzle", "Shooter"],
            StringComparer.Ordinal);

    [Fact]
    public void An_operator_over_no_values_is_not_offered()
    {
        var editor = FilterEditor();

        // Combining nothing is not a choice, and a picker holding one unusable row would never
        // reach the empty message that explains why it is otherwise blank.
        var picker = Assert.IsType<ListScreen>(OpenRow(editor, FilterFacet.Genres));

        Assert.Empty(picker.Rows);
    }

    [Fact]
    public void A_filter_with_nothing_set_says_it_matches_everything()
    {
        var editor = FilterEditor();

        // Worth saying before it happens rather than after a resolve walks the whole library.
        Assert.Contains(
            editor.Rows,
            row => row.Detail is { } detail
                && detail.Contains("matches every game", StringComparison.Ordinal));
    }

    [Fact]
    public void A_property_cycles_through_three_states_rather_than_two()
    {
        var editor = FilterEditor();
        var row = MoveTo(editor, "Favourite");

        // It was a yes-or-no toggle, so it could say "favourites only" and nothing else. RomM
        // offers all three and "games I have not favourited" is a real thing to sync.
        Assert.Equal("either", editor.Rows[row].Value);

        Assert.Equal(ScreenCommandKind.Stay, editor.Handle(NavAction.Accept).Kind);
        Assert.Equal("yes", editor.Rows[row].Value);

        editor.Handle(NavAction.Accept);
        Assert.Equal("no", editor.Rows[row].Value);

        editor.Handle(NavAction.Accept);
        Assert.Equal("either", editor.Rows[row].Value);
    }

    [Fact]
    public void A_property_answered_from_RomMs_own_records_says_so_once_it_is_set()
    {
        var editor = FilterEditor();
        var row = MoveTo(editor, "Has saves");

        // Unset it says nothing, because a caveat on a row nobody has touched is noise.
        Assert.Null(editor.Rows[row].Detail);

        editor.Handle(NavAction.Accept);

        Assert.Contains(
            "another account",
            editor.Rows[row].Detail ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Editing_a_filter_set_opens_on_the_filter_it_already_has()
    {
        var stored = new CatalogFilter()
            .WithValues("genres", ["Platform"])
            .WithProperty("favorite", true);

        var set = _session.Store.SyncSets.Add(
            new SyncSetDefinition
            {
                Name = "mine",
                Scope = CatalogScopeKind.Filter,
                ScopeValue = RomMBat.Core.Sync.CatalogFilterJson.Write(stored),
            },
            Now);

        // It opened on a blank filter, so every row said "any" for a set that had one. Nothing
        // was lost, because the edit could not write a filter either, but a set defined from
        // the couch could never be looked at from it.
        var editor = SetEditorViewModel.ForExisting(_session, set);
        var labels = editor.Rows.Select(row => row.Label).ToList();

        Assert.Equal("Platform", editor.Rows[labels.IndexOf(FilterFacet.Genres)].Value);
        Assert.Equal("yes", editor.Rows[labels.IndexOf("Favourite")].Value);
    }

    [Fact]
    public void A_facet_with_no_values_in_this_library_says_so_rather_than_opening_an_empty_list()
    {
        var editor = FilterEditor();
        MoveTo(editor, FilterFacet.Genres);

        using var picker = Assert.IsType<ListScreen>(editor.Handle(NavAction.Accept).Screen);

        // A list rather than a message, because whether there are any values is not known until
        // the server answers, and finding that out on the drawing thread is what froze the
        // interface. The sentence is the empty message instead, shown once the load lands.
        Assert.Empty(picker.Rows);
        Assert.NotNull(picker.EmptyMessage);
        Assert.Contains("genres", picker.EmptyMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_load_that_throws_says_what_went_wrong_rather_than_drawing_an_empty_list()
    {
        // The screen reported "this library reports no genres to filter by" against a library
        // with 343 of them: the load threw, Task.Run swallowed it unobserved, LoadProblem
        // stayed null and the empty message was all that was left to draw. An empty list and a
        // failed request look identical to a user and must not look identical here.
        using var screen = new ListScreen(
            "Genres",
            () => [],
            _ => ScreenCommand.Stay)
        {
            EmptyMessage = "This library reports no genres to filter by.",
            Load = _ => throw new InvalidOperationException("RomM could not be read."),
        }.Started();

        await Wait(() => !screen.IsLoading);

        Assert.Equal("RomM could not be read.", screen.LoadProblem);
    }

    /// <summary>Waits for a background load to settle, bounded so a hang fails rather than hangs.</summary>
    private static async Task Wait(Func<bool> until)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (until())
            {
                return;
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Fail("The load never settled.");
    }

    /// <summary>A new-set editor with the filter scope chosen, driven the way a person does it.</summary>
    private SetEditorViewModel FilterEditor()
    {
        var editor = SetEditorViewModel.ForNew(_session);
        var scopes = Assert.IsType<ListScreen>(OpenRow(editor, "Scope"));

        // Walked by label rather than by counting presses. The cursor skips unavailable rows,
        // so pressing down N times does not land on index N, and virtual collections are
        // unavailable. Counting put the cursor on the wrong scope and the test that followed
        // then searched for a row that did not exist.
        MoveTo(scopes, SyncSetStore.ScopeText(CatalogScopeKind.Filter));
        scopes.Handle(NavAction.Accept);

        return editor;
    }

    /// <summary>
    /// Walks a list's cursor onto the row with this label, and fails rather than spinning.
    /// </summary>
    /// <remarks>
    /// Bounded on purpose. An unbounded walk looking for a row that is not there does not fail
    /// the test, it hangs the whole run, which is what it did.
    /// </remarks>
    private static int MoveTo(ListScreen list, string label)
    {
        for (var step = 0; step <= list.Rows.Count; step++)
        {
            if (list.Cursor >= 0 && list.Rows[list.Cursor].Label == label)
            {
                return list.Cursor;
            }

            list.Handle(NavAction.Down);
        }

        Assert.Fail($"no row labelled '{label}' among [{string.Join(", ", list.Rows.Select(r => r.Label))}]");
        return -1;
    }

    /// <summary>The same, for the editor, which has its own cursor.</summary>
    private static int MoveTo(SetEditorViewModel editor, string label)
    {
        for (var step = 0; step <= editor.Rows.Count; step++)
        {
            if (editor.Rows[editor.Cursor].Label == label)
            {
                return editor.Cursor;
            }

            editor.Handle(NavAction.Down);
        }

        Assert.Fail($"no row labelled '{label}' among [{string.Join(", ", editor.Rows.Select(r => r.Label))}]");
        return -1;
    }

    // ---- nothing waits on the network while it is drawing ----

    [Fact]
    public void A_picker_that_asks_the_server_opens_at_once_and_says_it_is_loading()
    {
        var editor = FilterEditor();
        MoveTo(editor, FilterFacet.Genres);

        // The filter values are worked out by RomM across every game in the library, which is
        // minutes on a large one. Fetching that on the thread that draws froze the interface
        // with nothing on screen saying why, which from the couch is a crash.
        var clock = Stopwatch.StartNew();
        var picker = Assert.IsType<ListScreen>(editor.Handle(NavAction.Accept).Screen);
        clock.Stop();

        Assert.True(
            clock.ElapsedMilliseconds < 500,
            $"opening the facet picker took {clock.ElapsedMilliseconds} ms, so it waited on something");

        Assert.NotEmpty(picker.LoadingMessage);
        Assert.Contains(NavAction.Back, picker.Hints.Select(hint => hint.Action));
    }

    [Fact]
    public void A_loading_picker_is_still_leavable()
    {
        var editor = FilterEditor();
        MoveTo(editor, FilterFacet.Genres);

        using var picker = Assert.IsType<ListScreen>(editor.Handle(NavAction.Accept).Screen);

        // The whole point of not blocking. A screen that cannot be left while it waits is the
        // same as a frozen one for anybody holding a controller.
        Assert.Equal(ScreenCommandKind.Pop, picker.Handle(NavAction.Back).Kind);
    }

    [Fact]
    public void The_platform_picker_shows_its_rows_before_any_counts_arrive()
    {
        SeedPlatform(4, "snes");
        SeedPlatform(9, "megadrive");

        var editor = SetEditorViewModel.ForNew(_session);
        var picker = Assert.IsType<ListScreen>(OpenRow(editor, "Platform"));

        // Read from platform_map with no network, so the rows are right from the first frame.
        // The game counts are enrichment, and hiding a working list behind a spinner to wait
        // for a decoration would be a bad trade.
        Assert.False(picker.IsLoading);
        Assert.Equal(2, picker.Rows.Count);
    }

    [Fact]
    public void Resolving_several_sets_says_which_one_it_is_on()
    {
        // Reporting only a running count of games made five sets read as one long operation
        // that kept starting over.
        var progress = new SetResolveProgress("PSX", 250, 9196, 250, SetIndex: 2, SetCount: 5);

        Assert.Equal(2, progress.SetIndex);
        Assert.Equal(5, progress.SetCount);
        Assert.Equal("PSX", progress.SetName);
    }

    // ---- offline is a working state ----

    [Fact]
    public void Every_sets_screen_answers_within_the_budget_with_no_server_configured()
    {
        Seed("offline");

        // Nothing in this install has an origin or a token. Listing, opening, editing and the
        // budget are all local, and the 2 s budget is the same one stage 7b-1 measured an
        // unreachable server against.
        using var built = AllScreens();

        foreach (var screen in built)
        {
            var clock = Stopwatch.StartNew();
            _ = screen.Title;
            _ = screen.Hints;
            _ = Render(screen);
            clock.Stop();

            Assert.True(
                clock.ElapsedMilliseconds < 2000,
                $"{screen.GetType().Name} took {clock.ElapsedMilliseconds} ms with no server");
        }
    }

    [Fact]
    public void Resolving_without_a_pairing_says_so_rather_than_hanging()
    {
        var set = Seed("unpaired");

        using var resolve = new ResolveViewModel(_session, set);

        // Answered from the store, without a socket, because there is no token to try one
        // with. An unpaired install reaching for the network here would be a screen that
        // waits on a timeout to say something it already knew.
        Assert.Equal(ResolveStage.NotPaired, resolve.Stage);
        Assert.NotEmpty(resolve.Detail);
        Assert.Equal(ScreenCommandKind.Pop, resolve.Handle(NavAction.Back).Kind);
    }

    [Fact]
    public async Task Resolving_from_the_interface_mirrors_the_definitions_the_way_the_agent_does()
    {
        // Both `sets add` and `sets resolve` push Device.sync_config, and the interface pushed
        // nothing at all: a set defined from the couch stayed on the device that defined it,
        // while the identical set defined at a prompt followed its user to the next one. Same
        // action, two front ends, different persistence. Roaming is the mechanism M2 gave set
        // definitions, so the front end that has no prompt is the one that needs it most.
        var set = Seed("roaming");

        using var stub = new StubRomMServer();
        stub.ThenApproved(RomMScopes.Requested, "device-77");

        using var pairing = new RomMConnection(new RomMClientOptions { Origin = Origin }, stub);
        var service = new PairingService(_tree.Install(), _session.Store);

        var begun = await service.BeginAsync(pairing, cancellationToken: TestContext.Current.CancellationToken);
        var paired = await service.CompleteAsync(
            pairing,
            begun,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(paired.IsPaired);

        var pushed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var resolve = new ResolveViewModel(
            _session,
            set,
            _ => new RomMConnection(new RomMClientOptions { Origin = Origin }, stub),
            _ =>
            {
                pushed.TrySetResult();
                return Task.FromResult(new RoamingPush(true, null));
            });

        // Bounded rather than polled. The walk against an empty stub library is one request,
        // and the mirror follows it whatever the walk found.
        await pushed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    // ---- rule 1 ----

    [Fact]
    public void No_folder_the_picker_can_store_is_an_absolute_path()
    {
        // A folder override is a system name resolved at point of use. Anything path-shaped
        // reaching the store would be a persisted absolute path in all but name.
        foreach (var folder in new SyncSetService(_session).FoldersKnownHere())
        {
            Assert.False(Path.IsPathRooted(folder));
            Assert.DoesNotContain(':', folder);
        }
    }

    // ---- helpers ----

    /// <summary>Every string every sets screen puts on a display.</summary>
    private List<string> EverythingShown()
    {
        var shown = new List<string>();

        using var built = AllScreens();

        foreach (var screen in built)
        {
            shown.Add(screen.Title);
            shown.AddRange(screen.Hints.Select(hint => hint.Label));
            shown.AddRange(Render(screen));
        }

        return shown;
    }

    /// <summary>The visible text of one screen, whatever kind it is.</summary>
    private static List<string> Render(IScreen screen)
    {
        var text = new List<string>();

        switch (screen)
        {
            case ListScreen list:
                if (list.Note?.Invoke() is { } note)
                {
                    text.Add(note);
                }

                if (list.EmptyMessage is { } empty)
                {
                    text.Add(empty);
                }

                foreach (var row in list.Rows)
                {
                    text.Add(row.Label);
                    text.AddRange(new[] { row.Value, row.Detail }.OfType<string>());
                }

                break;

            case SetEditorViewModel editor:
                text.AddRange(editor.Rows.SelectMany(row =>
                    new[] { row.Label, row.Value, row.Detail }.OfType<string>()));

                if (editor.Problem is { } problem)
                {
                    text.Add(problem);
                }

                break;

            case BudgetViewModel budget:
                text.AddRange(budget.Rows.SelectMany(row =>
                    new[] { row.Label, row.Value, row.Detail }.OfType<string>()));
                break;

            case ResolveViewModel resolve:
                text.Add(resolve.Detail);

                if (resolve.Counted is { } counted)
                {
                    text.Add(counted);
                }

                break;

            case MessageScreen message:
                text.Add(message.Message);
                break;

            default:
                break;
        }

        return text;
    }

    /// <summary>One of each sets screen, in whatever state a person first meets it.</summary>
    /// <summary>
    /// Every screen on this surface, built fresh, and disposed once the caller is done.
    /// </summary>
    /// <remarks>
    /// <b>Disposed, because several of these start work on construction.</b> The file check
    /// walks <c>local_file</c> with one filesystem call per row on the thread pool, and four
    /// sweeps call this helper. Left running they outlived the assertion, contended on
    /// <c>StoreGate</c> with the next one, and were still going when the fixture disposed its
    /// store: two intermittent failures in unrelated tests before it was tracked down.
    /// <para>
    /// A helper that starts work owes its cleanup, which is what an <c>IDisposable</c> wrapper
    /// makes impossible to forget at one of four call sites.
    /// </para>
    /// </remarks>
    private ScreenSet AllScreens() => new(BuildScreens());

    /// <summary>Holds a sweep's screens and cancels whatever they started.</summary>
    private sealed class ScreenSet : IDisposable, IEnumerable<IScreen>
    {
        private readonly List<IScreen> _screens;

        public ScreenSet(List<IScreen> screens) => _screens = screens;

        public IEnumerator<IScreen> GetEnumerator() => _screens.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public void Dispose()
        {
            foreach (var screen in _screens)
            {
                (screen as IDisposable)?.Dispose();
            }
        }
    }

    private List<IScreen> BuildScreens()
    {
        var existing = new SyncSetService(_session).List();
        var set = existing.Count > 0 ? existing[0].Set : Seed("sample");

        // Built before the list, because constructing a screen in it is not side-effect free:
        // ApplyRemoval starts its loader on construction and that loader deletes the set, so a
        // row written for that set afterwards fails its foreign key. Worth knowing rather than
        // worked around silently, since the same is true of every screen here that loads.
        var browsed = Browsed(set);

        var screens = new List<IScreen>
        {
            SetsScreens.List(_session),
            SetsScreens.Detail(_session, set.Name, null),
            SetsScreens.ConfirmDelete(_session, set.Name),
            SetsScreens.ConfirmRemoval(_session, set.Name),

            // Reachable only by driving a preview to completion, so it is constructed directly
            // rather than left as the one screen on this surface no sweep looks at.
            //
            // Named for a set that does not exist, because this screen's loader **deletes the
            // set** as its last act and it starts on construction. Given a real name it raced
            // every assertion made after this list was built, which showed up as one sweep
            // failing in a full run and passing alone. A helper four sweeps share must not
            // mutate.
            SetsScreens.ApplyRemoval(
                _session,
                "a set no test made",
                new EvictionReport(new PartialSweepPlan(), new EvictionPlan(), HasBudget: false)),
            SetEditorViewModel.ForNew(_session),
            SetEditorViewModel.ForExisting(_session, set),
            new BudgetViewModel(_session),

            // The screens 7b-2c added, listed here rather than left to their own file's tests,
            // because the sweeps this list feeds are the whole-surface ones: no face button
            // named, no unbound action promised, no verb offered that does nothing, and nothing
            // slower than two seconds with the server off. A hands-on pass found three verb
            // defects across these in one sitting, and every one of them was on a screen no
            // sweep looked at.
            InventoryScreens.Check(_session),
            BrowseScreens.Detail(_session, browsed),
        };

        // The pickers, which are reached from the editor rather than constructed directly.
        // Named rather than numbered: which rows exist depends on the scope, and asking for an
        // index that no longer exists is what made this hang.
        var editor = SetEditorViewModel.ForNew(_session);

        foreach (var label in editor.Rows.Select(row => row.Label).ToList())
        {
            if (OpenRow(editor, label) is { } opened)
            {
                screens.Add(opened);
            }
        }

        var resolve = new ResolveViewModel(_session, set);
        screens.Add(resolve);

        return screens;
    }

    /// <summary>
    /// Opens whatever the row with this label leads to.
    /// </summary>
    /// <remarks>
    /// <b>By label and bounded, for the second time.</b> This asked for a row by index and
    /// spun the cursor until it matched, which stops being reachable the moment the editor's
    /// rows change: dropping the caps took it from five rows to two, and asking for index 2
    /// left it stepping between 0 and 1 for ever. The same defect was fixed in
    /// <see cref="MoveTo(ListScreen, string)"/> and left standing here, which is what made one
    /// test take 68 seconds.
    /// </remarks>
    private static IScreen? OpenRow(SetEditorViewModel editor, string label)
    {
        MoveTo(editor, label);
        return editor.Handle(NavAction.Accept).Screen;
    }

    private void SeedPlatform(int id, string folder) =>
        _session.Store.PlatformMap.Record(
            new RomMBat.Core.Mapping.PlatformResolver(
                Fixtures.LoadEsSystems(),
                new Dictionary<string, string>())
                .Resolve(new RomMBat.Core.Mapping.RomMPlatform(id, folder, folder, folder)),
            Now);

    private static SyncSetMember Member(SyncSetDefinition set) =>
        new()
        {
            RomId = 1,
            State = MemberState.Member,
            Folder = "snes",
            PlatformSlug = "snes",
            FsName = "g.sfc",
            FsExtension = "sfc",
            SizeBytes = 2048,
            DisplayName = "Game",
            SortKey = "game",
            Position = 1,
            ResolvedAt = Now,
        };

    private void Members(SyncSetDefinition set, params int[] romIds) =>
        _session.Store.SyncSets.ReplaceMembers(
            set.Id,
            [
                .. romIds.Select((romId, index) => new SyncSetMember
                {
                    RomId = romId,
                    State = MemberState.Member,
                    Folder = "snes",
                    PlatformSlug = "snes",
                    FsName = $"rom{romId}.sfc",
                    FsExtension = "sfc",
                    SizeBytes = 1_024,
                    DisplayName = $"Game {romId}",
                    SortKey = $"game {romId}",
                    Position = index + 1,
                    ResolvedAt = Now,
                }),
            ],
            $"{romIds.Length} games",
            Now);

    private void SeedFile(int romId, string folder, string fileName, long bytes)
    {
        var absolute = Path.Combine(_tree.Root, "roms", folder, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllBytes(absolute, new byte[bytes]);

        _session.Store.Files.Record(new LocalFile
        {
            Path = RomMBat.Core.Paths.RelativePath.Create($"roms/{folder}/{fileName}"),
            Folder = folder,
            RomId = romId,
            Kind = LocalFileKind.Rom,
            FileName = fileName,
            SizeBytes = bytes,
            Origin = FileOrigin.Synced,
        });
    }

    private SyncSetDefinition Seed(string name) =>
        _session.Store.SyncSets.Add(
            new SyncSetDefinition
            {
                Name = name,
                Scope = CatalogScopeKind.Platform,
                ScopeValue = "1",
                MaxGames = 40,
            },
            Now);

    /// <summary>A browse row for a game this device holds, so the detail screen has both verbs.</summary>
    private BrowseGame Browsed(SyncSetDefinition set)
    {
        _session.Store.SyncSets.ReplaceMembers(set.Id, [Member(set)], "1 game", Now);

        _session.Store.Files.Record(new LocalFile
        {
            Path = RomMBat.Core.Paths.RelativePath.Create("roms/snes/g.sfc"),
            Folder = "snes",
            RomId = 1,
            Kind = LocalFileKind.Rom,
            FileName = "g.sfc",
            SizeBytes = 2048,
            Origin = FileOrigin.Synced,
        });

        return new BrowseGame(1, "Game", "snes", 2048, ["snes"], 2048, [set.Name], Row: null);
    }

    private StatusViewModel Status() =>
        new(_session, new GamepadStatus(GamepadAvailability.NoDevice, null, null, "No controller."))
        {
            OpenSets = () => SetsScreens.List(_session),
            OpenBudget = () => new BudgetViewModel(_session),
        };
}
