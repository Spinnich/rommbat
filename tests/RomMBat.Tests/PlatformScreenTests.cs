using RomMBat.Core;
using RomMBat.Core.Mapping;
using RomMBat.Core.Sets;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using RomMBat.UI.Input;
using RomMBat.UI.Screens;
using RomMBat.UI.Shell;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The Platform Mapping screen M2 called for and nothing had built.
/// </summary>
/// <remarks>
/// <b>The repair being install-wide is the point.</b> Before this, an unmapped platform was found
/// out by a resolve stopping partway through a collection that happened to hold one of its games,
/// and the only fix reachable from the couch was a per-set folder override, which mends one set
/// and leaves every other set and every future set with the same hole. <c>platform_map</c> is
/// install-wide and always was; what was missing was a way to reach it.
/// </remarks>
public class PlatformScreenTests : IDisposable
{
    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();
    private readonly InstallSession _session;

    public PlatformScreenTests()
    {
        // RetroBat is the authority on which systems exist, and the folder picker reads the
        // live file rather than a bundled list, so a tree without one has no folders to offer.
        var location = Path.Combine(_tree.Root, "emulationstation", ".emulationstation", "es_systems.cfg");
        Directory.CreateDirectory(Path.GetDirectoryName(location)!);
        File.Copy(Fixtures.EsSystemsTemplate, location);

        _session = InstallSession.Open(_tree.Root).Session!;
    }

    public void Dispose()
    {
        _session.Dispose();
        _tree.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void An_unmapped_platform_sorts_above_the_mapped_ones_and_says_what_it_costs()
    {
        Map("snes", "snes", "Super Nintendo");
        Unmapped("arcade", "Arcade");
        Map("megadrive", "megadrive", "Mega Drive");

        var list = Assert.IsType<ListScreen>(PlatformScreens.List(_session));

        // Alphabetically arcade would lead anyway, so the assertion is on the rule rather than
        // on this order: everything without a folder comes first, whatever it is called.
        Assert.Equal("Arcade", list.Rows[0].Label);
        Assert.Equal("no folder", list.Rows[0].Value);

        Assert.All(list.Rows.Skip(1), row => Assert.NotEqual("no folder", row.Value));

        // The count the root menu's row promised, said again where the repair is.
        Assert.NotNull(list.Note);
        Assert.Contains("1 of 3", list.Note!()!, StringComparison.Ordinal);
        Assert.Contains("skipped", list.Note!()!, StringComparison.Ordinal);
    }

    [Fact]
    public void Choosing_a_folder_writes_an_install_wide_override_rather_than_a_per_set_one()
    {
        Unmapped("arcade", "Arcade");

        var list = Assert.IsType<ListScreen>(PlatformScreens.List(_session));
        var navigator = new Navigator(list);

        navigator.Handle(NavAction.Accept);
        var detail = Assert.IsType<ListScreen>(navigator.Current);

        // A pane of facts with the verbs on Start and Alternate, so nothing is selected.
        Assert.True(detail.Reading);
        Assert.Equal(-1, detail.Cursor);
        Assert.Contains(detail.Hints, hint => hint.Action == NavAction.Start);

        navigator.Handle(NavAction.Start);
        var picker = Assert.IsType<ListScreen>(navigator.Current);

        // Read from the live es_systems.cfg, because RetroBat is the authority on which systems
        // exist and a bundled list goes stale every release.
        var folders = new SyncSetService(_session).FoldersKnownHere();
        Assert.Equal(folders.Count, picker.Rows.Count);

        var target = picker.Rows.ToList().FindIndex(row => row.Label == "snes");
        Assert.True(target >= 0, "the fixture install has no snes folder to map onto");

        for (var step = 0; step < target; step++)
        {
            navigator.Handle(NavAction.Down);
        }

        navigator.Handle(NavAction.Accept);

        var row = _session.Store.PlatformMap.Find("arcade");

        Assert.NotNull(row);
        Assert.Equal("snes", row!.Folder);

        // A choice, not a guess. That is what stops a later re-resolve overwriting it.
        Assert.True(row.IsUserChoice);
        Assert.Equal(MappingSource.User, row.ResolvedBy);

        // And the detail screen underneath re-reads rather than showing the folder from before.
        Assert.Equal("snes", detail.Rows.Single(r => r.Label == "Folder").Value);
    }

    [Fact]
    public void Dropping_a_choice_is_offered_only_where_there_is_one_to_drop()
    {
        Unmapped("arcade", "Arcade");

        var guessed = Assert.IsType<ListScreen>(
            PlatformScreens.Detail(_session, _session.Store.PlatformMap.Find("arcade")!));

        // Nothing to drop, so nothing offers it: a press that does nothing is the defect three
        // screens got three different ways in 7b-2c.
        Assert.DoesNotContain(guessed.Hints, hint => hint.Action == NavAction.Alternate);

        _session.Store.PlatformMap.SetOverride("arcade", "fbneo", DateTimeOffset.UtcNow);

        var chosen = Assert.IsType<ListScreen>(
            PlatformScreens.Detail(_session, _session.Store.PlatformMap.Find("arcade")!));

        Assert.Contains(chosen.Hints, hint => hint.Action == NavAction.Alternate);

        var navigator = new Navigator(chosen);
        navigator.Handle(NavAction.Alternate);

        var confirm = Assert.IsType<ListScreen>(navigator.Current);
        navigator.Handle(NavAction.Accept);

        var row = _session.Store.PlatformMap.Find("arcade");

        Assert.NotNull(row);
        Assert.Null(row!.Folder);
        Assert.False(row.IsUserChoice);

        // Answered once. The confirmation stops offering it rather than letting a second press
        // run a change that has already happened.
        Assert.DoesNotContain(confirm.Hints, hint => hint.Action == NavAction.Accept);
    }

    [Fact]
    public void The_whole_screen_answers_with_the_server_switched_off()
    {
        // Nothing here takes a connection, and that is deliberate: platform_map is written by
        // every resolve and every browse, so the rows a person came to fix are already local.
        // A screen that waited on an unreachable LAN host would trade the working state for
        // nothing. This install has never been paired.
        Unmapped("arcade", "Arcade");
        Map("snes", "snes", "Super Nintendo");

        Assert.Null(_session.Store.Device.Read()?.RomMDeviceId);

        var list = Assert.IsType<ListScreen>(PlatformScreens.List(_session));

        Assert.Equal(2, list.Rows.Count);
        Assert.Null(list.Load);
    }

    [Fact]
    public void An_install_that_has_never_synced_says_how_platforms_appear()
    {
        var list = Assert.IsType<ListScreen>(PlatformScreens.List(_session));

        Assert.Empty(list.Rows);
        Assert.NotNull(list.EmptyMessage);
        Assert.Contains("Sync", list.EmptyMessage!, StringComparison.Ordinal);

        // Empty is not the same as every platform being mapped, so the note says nothing rather
        // than claiming an all-clear about a table with no rows in it.
        Assert.Null(list.Note!());
    }

    [Fact]
    public void The_platform_screens_name_no_face_button()
    {
        Unmapped("arcade", "Arcade");

        var list = Assert.IsType<ListScreen>(PlatformScreens.List(_session));
        var navigator = new Navigator(list);
        navigator.Handle(NavAction.Accept);

        var detail = Assert.IsType<ListScreen>(navigator.Current);
        navigator.Handle(NavAction.Start);

        var picker = Assert.IsType<ListScreen>(navigator.Current);

        foreach (var screen in new[] { list, detail, picker })
        {
            var strings = screen.Rows
                .SelectMany(row => new[] { row.Label, row.Value, row.Detail })
                .Concat(screen.Hints.Select(hint => hint.Label))
                .Append(screen.Title)
                .Append(screen.EmptyMessage)
                .Append(screen.Note?.Invoke())
                .Where(text => text is not null);

            foreach (var text in strings)
            {
                Assert.DoesNotContain("press ", text!, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("button", text!, StringComparison.OrdinalIgnoreCase);
            }

            Assert.All(screen.Hints, hint => Assert.Contains(hint.Action, NavRepeat.Bound));
        }
    }

    /// <summary>A platform the chain answered, as a resolve would have recorded it.</summary>
    private void Map(string fsSlug, string folder, string name) =>
        _session.Store.PlatformMap.Record(
            new PlatformResolution(
                fsSlug,
                fsSlug,
                PlatformId: 1,
                name,
                folder,
                MappingSource.Bundled,
                Suggestion: null,
                Candidates: [folder],
                RequiresExplicitChoice: false,
                FolderMissingFromInstall: false,
                $"The bundled table names '{folder}'."),
            DateTimeOffset.UtcNow);

    /// <summary>A platform nothing matched, which is a normal state rather than an error.</summary>
    private void Unmapped(string fsSlug, string name) =>
        _session.Store.PlatformMap.Record(
            new PlatformResolution(
                fsSlug,
                fsSlug,
                PlatformId: 2,
                name,
                Folder: null,
                MappingSource.Unmapped,
                Suggestion: null,
                Candidates: [],
                RequiresExplicitChoice: true,
                FolderMissingFromInstall: false,
                "Which folder is right depends on the romset the files came from."),
            DateTimeOffset.UtcNow);
}
