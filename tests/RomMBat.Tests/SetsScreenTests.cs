using System.Diagnostics;
using System.Globalization;
using RomM.Client.Catalog;
using RomMBat.Core;
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

        // Start on the status screen opens the sets list. Nothing here uses a mouse, and
        // nothing here types except the name, which is the on-screen keyboard.
        navigator.Handle(NavAction.Start);
        Assert.IsType<ListScreen>(navigator.Current);

        // Start again opens the editor for a new set.
        navigator.Handle(NavAction.Start);
        var editor = Assert.IsType<SetEditorViewModel>(navigator.Current);
        Assert.True(editor.IsNew);

        // Name it.
        navigator.Handle(NavAction.Accept);
        var keyboard = Assert.IsType<OnScreenKeyboard>(navigator.Current);
        Type(navigator, keyboard, "snes");
        Assert.Same(editor, navigator.Current);

        // Scope: the second row, already Platform, so step down to the platform row instead.
        navigator.Handle(NavAction.Down);
        navigator.Handle(NavAction.Down);
        navigator.Handle(NavAction.Accept);

        var platforms = Assert.IsType<ListScreen>(navigator.Current);
        Assert.NotEmpty(platforms.Rows);
        navigator.Handle(NavAction.Accept);
        Assert.Same(editor, navigator.Current);

        // A cap, stepped rather than typed.
        navigator.Handle(NavAction.Down);
        navigator.Handle(NavAction.Right);

        navigator.Handle(NavAction.Start);

        // Back on the list, with the set defined.
        Assert.IsType<ListScreen>(navigator.Current);
        Assert.Contains(new SyncSetService(_session).List(), summary => summary.Set.Name == "snes");
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

        Assert.Equal(["Name", "Scope", "Platform"], editor.Rows.Select(row => row.Label));
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

        // A platform and a collection both already have a name in RomM, so making somebody
        // spell one out on a d-pad to mirror it is work for nothing. This is also what takes
        // the on-screen keyboard off the common path entirely.
        Assert.Equal("named after what you choose", editor.Rows.Single(r => r.Label == "Name").Value);

        var picker = Assert.IsType<ListScreen>(OpenRow(editor, 2));
        picker.Handle(NavAction.Accept);

        var named = editor.Rows.Single(r => r.Label == "Name").Value;
        Assert.NotEqual("named after what you choose", named);
        Assert.Equal(new SyncSetService(_session).PlatformsKnownHere()[0].Label, named);
    }

    [Fact]
    public void A_name_somebody_typed_is_not_overwritten_by_a_later_choice()
    {
        SeedPlatform(4, "snes");
        SeedPlatform(9, "megadrive");

        var editor = SetEditorViewModel.ForNew(_session);

        var keyboard = Assert.IsType<OnScreenKeyboard>(OpenRow(editor, 0));
        keyboard.Handle(NavAction.Accept);
        keyboard.Handle(NavAction.Start);

        var typed = editor.Rows.Single(r => r.Label == "Name").Value;

        var picker = Assert.IsType<ListScreen>(OpenRow(editor, 2));
        picker.Handle(NavAction.Accept);

        // Silently replacing a name somebody entered would be worse than asking for one.
        Assert.Equal(typed, editor.Rows.Single(r => r.Label == "Name").Value);
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
        var editor = SetEditorViewModel.ForNew(_session);

        foreach (var action in new[] { 0, 1, 2 })
        {
            var opened = OpenRow(editor, action);
            if (opened is not null)
            {
                screens.Add(opened);
            }
        }

        var resolve = new ResolveViewModel(_session, set);
        screens.Add(resolve);

        return screens;
    }

    /// <summary>Opens the picker behind row <paramref name="row"/> of a new-set editor.</summary>
    private static IScreen? OpenRow(SetEditorViewModel editor, int row)
    {
        while (editor.Cursor != row)
        {
            editor.Handle(NavAction.Down);
        }

        return editor.Handle(NavAction.Accept).Screen;
    }

    private static void Type(Navigator navigator, OnScreenKeyboard keyboard, string text)
    {
        foreach (var character in text)
        {
            Move(navigator, keyboard, character);
            navigator.Handle(NavAction.Accept);
        }

        navigator.Handle(NavAction.Start);
    }

    /// <summary>
    /// Walks the keyboard cursor onto one character, the way a thumb would.
    /// </summary>
    /// <remarks>
    /// Both directions, because the grid is four rows: pressing only Right walks one row for
    /// ever and never reaches the home row, which is where most letters are.
    /// </remarks>
    private static void Move(Navigator navigator, OnScreenKeyboard keyboard, char character)
    {
        var wanted = character.ToString();

        for (var row = 0; row < keyboard.Keys.Count; row++)
        {
            for (var column = 0; column < keyboard.Keys[keyboard.CursorRow].Length; column++)
            {
                if (keyboard.Selected == wanted)
                {
                    return;
                }

                navigator.Handle(NavAction.Right);
            }

            navigator.Handle(NavAction.Down);
        }

        Assert.Fail($"'{character}' is not reachable on the keyboard");
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
