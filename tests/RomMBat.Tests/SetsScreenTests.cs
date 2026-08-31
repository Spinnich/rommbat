using System.Diagnostics;
using System.Globalization;
using RomM.Client.Catalog;
using RomMBat.Core;
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
    public void Deleting_a_set_says_that_nothing_on_disk_was_touched_before_it_happens()
    {
        Seed("doomed");

        var confirm = Assert.IsType<ListScreen>(SetsScreens.ConfirmDelete(_session, "doomed"));

        // sets remove has always said this, and a person deleting from a couch has no other way
        // to learn that their games are still there. It is on the confirmation rather than on a
        // screen afterwards, because a warning after the act is not a warning.
        Assert.Contains(
            confirm.Rows,
            row => row.Detail is { } detail
                && detail.Contains("Nothing on disk is touched", StringComparison.Ordinal));

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

        foreach (var screen in AllScreens())
        {
            Assert.All(screen.Hints, hint => Assert.Contains(hint.Action, bound));
        }
    }

    // ---- what the hands-on pass found ----

    [Fact]
    public void Any_screen_that_answers_accept_also_offers_the_hint_for_it()
    {
        Seed("hinted");
        SeedPlatform(4, "snes");

        // The generalising form of a hands-on finding. The set detail screen answered Accept by
        // opening the editor and never offered the hint, because the hint was derived from
        // whether the cursor sat on a choosable row and every row there is a fact. The action
        // worked and the footer never said so, which is the round-8 failure pointed the other
        // way: a rule enforced in one place and broken in the place beside it.
        //
        // Each screen is a fresh instance, so pressing Accept here cannot disturb the next one.
        foreach (var screen in AllScreens())
        {
            var offered = screen.Hints.Any(hint => hint.Action == NavAction.Accept);
            var answered = screen.Handle(NavAction.Accept).Kind != ScreenCommandKind.Stay;

            Assert.False(
                answered && !offered,
                $"{screen.GetType().Name} acts on Accept but its footer never says so");
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
        var command = navigator.Current.Handle(NavAction.Alternate);
        using var resolve = Assert.IsType<ResolveViewModel>(command.Screen);

        Assert.Contains("2", resolve.Title, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_all_is_not_offered_when_there_is_nothing_to_resolve()
    {
        var list = SetsScreens.List(_session);

        // An empty list offering to resolve everything is a footer promising a no-op.
        Assert.Empty(Assert.IsType<ListScreen>(list).Rows);
        Assert.Equal(ScreenCommandKind.Stay, list.Handle(NavAction.Alternate).Kind);
    }

    [Fact]
    public void A_filter_offers_the_facets_it_can_persist_and_no_others()
    {
        var editor = FilterEditor();

        // RomM returns ten facets in filter_values. These are the ones CatalogFilter can store,
        // roam through sync_config and replay against a server that has never seen this device.
        // Offering one that cannot be saved would be a picker that forgets.
        var labels = editor.Rows.Select(row => row.Label).ToList();

        Assert.Contains("Search for", labels);
        Assert.Contains(FilterFacet.Favourites, labels);
        Assert.DoesNotContain("Companies", labels);
        Assert.DoesNotContain("Age ratings", labels);
        Assert.DoesNotContain("Player counts", labels);
    }

    [Fact]
    public void A_filter_with_nothing_set_says_it_matches_everything()
    {
        var editor = FilterEditor();

        // Worth saying before it happens rather than after a resolve walks the whole library.
        Assert.Contains(
            editor.Rows,
            row => row.Detail is { } detail
                && detail.Contains("matches the whole library", StringComparison.Ordinal));
    }

    [Fact]
    public void Favourites_toggles_in_place_rather_than_opening_a_screen()
    {
        var editor = FilterEditor();
        var row = MoveTo(editor, FilterFacet.Favourites);

        Assert.Equal("no", editor.Rows[row].Value);

        // A yes or no is not worth a screen, and Accept is what acts on a row.
        Assert.Equal(ScreenCommandKind.Stay, editor.Handle(NavAction.Accept).Kind);
        Assert.Equal("yes", editor.Rows[row].Value);
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
        foreach (var screen in AllScreens())
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

        foreach (var screen in AllScreens())
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
                if (list.Note is { } note)
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
    private List<IScreen> AllScreens()
    {
        var existing = new SyncSetService(_session).List();
        var set = existing.Count > 0 ? existing[0].Set : Seed("sample");

        var screens = new List<IScreen>
        {
            SetsScreens.List(_session),
            SetsScreens.Detail(_session, set.Name, null),
            SetsScreens.ConfirmDelete(_session, set.Name),
            SetEditorViewModel.ForNew(_session),
            SetEditorViewModel.ForExisting(_session, set),
            new BudgetViewModel(_session),
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

    private StatusViewModel Status() =>
        new(_session, new GamepadStatus(GamepadAvailability.NoDevice, null, null, "No controller."))
        {
            OpenSets = () => SetsScreens.List(_session),
            OpenBudget = () => new BudgetViewModel(_session),
        };
}
