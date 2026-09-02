using System.Diagnostics;
using RomM.Client;
using RomM.Client.Catalog;
using RomMBat.Core;
using RomMBat.Core.Identity;
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
/// Browse, driven with the gamepad map alone and no window.
/// </summary>
/// <remarks>
/// <b>The rule most likely to be broken here is that nothing holds more than one page</b>, and
/// it breaks silently and only at scale: a screen that appended would look identical for the
/// first few pages and hold the library by the end. That is the first test below.
/// </remarks>
public sealed class BrowseScreenTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Uri Origin = new("https://romm.invalid/");

    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();
    private readonly InstallSession _session;

    public BrowseScreenTests()
    {
        var location = Path.Combine(_tree.Root, "emulationstation", ".emulationstation", "es_systems.cfg");
        Directory.CreateDirectory(Path.GetDirectoryName(location)!);
        File.Copy(Fixtures.EsSystemsTemplate, location);

        _session = InstallSession.Open(_tree.Root).Session!;
        Map(1, "snes");
    }

    public void Dispose()
    {
        _session.Dispose();
        _tree.Dispose();
    }

    // ------------------------------------------------------------------ one page, ever

    /// <summary>
    /// The row count never exceeds one page, across several pages.
    /// </summary>
    /// <remarks>
    /// M2's rule stated as an assertion. A screen that concatenated pages would pass every other
    /// test in this file and hold an 83,000-row library by the time somebody reached the end.
    /// </remarks>
    [Fact]
    public async Task Browse_never_holds_more_than_one_page()
    {
        using var stub = Library(220);
        Pair();
        using var browse = new BrowseViewModel(_session, Connect(stub));

        await Settled(browse);

        for (var page = 0; page < 4; page++)
        {
            Assert.True(
                browse.Rows.Count <= BrowseService.PageSize + 1,
                $"page {page} held {browse.Rows.Count} rows");

            await PageDown(browse);
        }
    }

    [Fact]
    public async Task Moving_past_the_bottom_fetches_the_next_page_and_replaces_what_is_held()
    {
        using var stub = Library(120);
        Pair();
        using var browse = new BrowseViewModel(_session, Connect(stub));

        await Settled(browse);

        var first = browse.Rows[0].Label;
        Assert.Equal(0, browse.State.Page!.Offset);

        await PageDown(browse);

        Assert.Equal(BrowseService.PageSize, browse.State.Page!.Offset);
        Assert.DoesNotContain(browse.Rows, row => row.Label == first);
    }

    [Fact]
    public async Task Moving_past_the_top_fetches_the_previous_page_and_lands_on_its_last_row()
    {
        using var stub = Library(120);
        Pair();
        using var browse = new BrowseViewModel(_session, Connect(stub));

        await Settled(browse);
        await PageDown(browse);

        Assert.Equal(BrowseService.PageSize, browse.State.Page!.Offset);

        browse.Handle(NavAction.Up);
        await Settled(browse);

        Assert.Equal(0, browse.State.Page!.Offset);

        // The cursor lands where the eye already is, which is the bottom of the page it came
        // back to rather than the top of it.
        Assert.Equal(browse.State.Page.Games.Count - 1, browse.Cursor);
    }

    /// <summary>
    /// The end of the last page stops, and says so.
    /// </summary>
    /// <remarks>
    /// Ruled with Spinnich. Every other list here wraps; a paged one that wrapped to page one
    /// would silently undo nine thousand rows of paging and look exactly like the stall a failed
    /// fetch produces. Stopping silently is what a couch reads as a frozen screen, which is the
    /// failure both previous stages found repeatedly, so there is a row.
    /// </remarks>
    [Fact]
    public async Task The_end_of_the_last_page_stops_and_says_so()
    {
        using var stub = Library(60);
        Pair();
        using var browse = new BrowseViewModel(_session, Connect(stub));

        await Settled(browse);
        await PageDown(browse);

        var page = browse.State.Page!;
        Assert.True(page.IsLastPage);
        Assert.Equal(BrowseService.PageSize, page.Offset);

        Assert.Contains(browse.Rows, row => row.Label == "End of the list");

        // Down at the bottom moves onto the end row and no further. It never goes back to
        // offset 0.
        for (var press = 0; press < 20; press++)
        {
            browse.Handle(NavAction.Down);
        }

        Assert.Equal(BrowseService.PageSize, browse.State.Page!.Offset);
        Assert.Equal(browse.Rows.Count - 1, browse.Cursor);
    }

    /// <summary>A library that fits in one page still wraps, because there is no paging to undo.</summary>
    [Fact]
    public async Task A_single_page_library_still_wraps()
    {
        using var stub = Library(3);
        Pair();
        using var browse = new BrowseViewModel(_session, Connect(stub));

        await Settled(browse);

        Assert.DoesNotContain(browse.Rows, row => row.Label == "End of the list");

        browse.Handle(NavAction.Down);
        browse.Handle(NavAction.Down);
        browse.Handle(NavAction.Down);

        Assert.Equal(0, browse.Cursor);
        Assert.Equal(0, browse.State.Page!.Offset);
    }

    /// <summary>
    /// A held d-pad crossing a page boundary starts one fetch, not a dozen.
    /// </summary>
    /// <remarks>
    /// <b>Found from the couch on the first hands-on pass, as the selection rubberbanding.</b> A
    /// held pad repeats several times a second and a page takes about 280 ms against the live
    /// instance, so every press between the request and its answer started another one. Half a
    /// dozen landed out of order, each resetting the cursor to the top of whatever arrived last.
    /// <para>
    /// Driven here by pressing without waiting, which is exactly what the repeat does, and
    /// counted at the stub rather than inferred from the cursor: the symptom is the cursor and
    /// the cause is the request count.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task A_held_dpad_across_a_page_boundary_fetches_one_page()
    {
        using var stub = Library(200);
        Pair();
        using var browse = new BrowseViewModel(_session, Connect(stub));

        await Settled(browse);

        var before = stub.RomPagesServed;

        // To the bottom of the page, then a dozen more presses with no wait, which is a held
        // pad. Only the first of them may start anything.
        for (var press = 0; press < BrowseService.PageSize + 12; press++)
        {
            browse.Handle(NavAction.Down);
        }

        await Settled(browse);

        Assert.Equal(before + 1, stub.RomPagesServed);
        Assert.Equal(BrowseService.PageSize, browse.State.Page!.Offset);

        // And the cursor is where the landing page put it, rather than wherever the last of a
        // dozen races happened to leave it.
        Assert.Equal(0, browse.Cursor);
    }

    // ------------------------------------------------------------------ it degrades

    /// <summary>
    /// With no server it lists what this device holds, says so, and stays responsive.
    /// </summary>
    /// <remarks>
    /// Offline is a working state. A browse that refused would be the one screen on this surface
    /// that stopped working away from the server, and the local subset is what EmulationStation
    /// shows anyway.
    /// </remarks>
    [Fact]
    public async Task With_no_server_it_lists_this_device_and_says_which_it_is_showing()
    {
        Installed(1, "snes", "Chrono Trigger.sfc", 2_048);
        Installed(2, "snes", "Super Metroid.sfc", 1_024);

        var clock = Stopwatch.StartNew();
        using var browse = new BrowseViewModel(_session);
        await Settled(browse);
        clock.Stop();

        Assert.Equal(BrowseSource.ThisDevice, browse.State.Page!.Source);
        Assert.Contains("games on this device", browse.Note, StringComparison.Ordinal);
        Assert.Equal(2, browse.State.Page.Games.Count);

        // Every row says it is here, and where.
        Assert.All(browse.Rows, row => Assert.Contains("here: snes", row.Value, StringComparison.Ordinal));

        Assert.True(clock.ElapsedMilliseconds < 2_000, $"browse took {clock.ElapsedMilliseconds} ms with no server");
    }

    [Fact]
    public async Task An_unreachable_server_falls_back_to_this_device_and_carries_the_reason()
    {
        Installed(1, "snes", "Chrono Trigger.sfc", 2_048);

        using var stub = new StubRomMServer { IsReachable = false };
        Pair();

        var clock = Stopwatch.StartNew();
        using var browse = new BrowseViewModel(_session, Connect(stub));
        await Settled(browse);
        clock.Stop();

        Assert.Equal(BrowseSource.ThisDevice, browse.State.Page!.Source);
        Assert.NotNull(browse.State.Page.Problem);
        Assert.Contains("could not be reached", browse.Note, StringComparison.OrdinalIgnoreCase);
        Assert.Single(browse.State.Page.Games);

        Assert.True(clock.ElapsedMilliseconds < 2_000, $"browse took {clock.ElapsedMilliseconds} ms unreachable");
    }

    // ------------------------------------------------------------------ what a row says

    /// <summary>
    /// A game in two folders names both, which is what made the doubling invisible before.
    /// </summary>
    /// <remarks>
    /// One ROM in two folders is legitimate and costs twice the room. The crash it used to cause
    /// is fixed in <c>EvictionPlanner</c>; this is the other half of the finding, which is that
    /// nobody could see why the bytes had doubled.
    /// </remarks>
    [Fact]
    public async Task A_game_in_two_folders_names_both_on_its_row()
    {
        Installed(1, "fbneo", "mslug.zip", 3_000);
        Installed(1, "mame", "mslug.zip", 3_000);

        using var browse = new BrowseViewModel(_session);
        await Settled(browse);

        var row = Assert.Single(browse.Rows);

        Assert.Contains("fbneo", row.Value, StringComparison.Ordinal);
        Assert.Contains("mame", row.Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// Two dumps of one game are told apart on the row, which a display name cannot do.
    /// </summary>
    /// <remarks>
    /// Found on the first hands-on pass of browse. A library holds a USA and a Japan cut, two
    /// revisions and a translation under one title, and picking the wrong one is a download and
    /// a removal to undo. The tags come from <c>fs_name</c>, which is where the No-Intro and
    /// Redump groups live and which is complete where <c>regions</c> and <c>languages</c> are
    /// sparse: languages are on 18.3% of a real library.
    /// </remarks>
    [Fact]
    public async Task Two_releases_of_one_game_are_told_apart_on_the_row()
    {
        using var stub = new StubRomMServer();
        stub.Library.Add(new StubRom(1, 1, "snes", "snes", "Chrono Trigger", "Chrono Trigger (USA).sfc", "sfc", 1_024));
        stub.Library.Add(new StubRom(2, 1, "snes", "snes", "Chrono Trigger", "Chrono Trigger (Japan) (Rev 1).sfc", "sfc", 1_024));

        Pair();
        using var browse = new BrowseViewModel(_session, Connect(stub));
        await Settled(browse);

        // The name is the label on both, because that is what a person recognises, and the
        // release under it is what tells them apart.
        Assert.Equal(2, browse.Rows.Count);
        Assert.Equal(browse.Rows[0].Label, browse.Rows[1].Label);
        Assert.NotEqual(browse.Rows[0].Detail, browse.Rows[1].Detail);

        Assert.StartsWith("USA", browse.Rows[0].Detail, StringComparison.Ordinal);
        Assert.StartsWith("Japan, Rev 1", browse.Rows[1].Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// An arcade row keeps the name it is recognised by, and shows the romset underneath.
    /// </summary>
    /// <remarks>
    /// <b>The half that stops the file name being the label.</b> Measured on the live library:
    /// 750 of 750 arcade file names carry no tags at all, because they are romset codes, and
    /// 87.3% differ from the display name. A list of <c>10yard.zip</c> and <c>1943kai.zip</c> is
    /// not something a person can read across a room, and there is nothing to parse out of one,
    /// so the whole file name is what goes on the second line.
    /// </remarks>
    [Fact]
    public async Task An_arcade_row_shows_the_title_and_the_romset_underneath()
    {
        using var stub = new StubRomMServer();
        stub.Library.Add(new StubRom(1, 1, "arcade", "fbneo", "1943: The Battle Of Midway", "1943.zip", "zip", 1_024));

        Pair();
        using var browse = new BrowseViewModel(_session, Connect(stub));
        await Settled(browse);

        var row = Assert.Single(browse.Rows);

        Assert.Equal("1943: The Battle Of Midway", row.Label);
        Assert.StartsWith("1943.zip", row.Detail, StringComparison.Ordinal);
    }

    /// <summary>The offline page tells them apart too, from the recorded file name.</summary>
    [Fact]
    public async Task The_offline_page_tells_two_releases_apart_as_well()
    {
        Installed(1, "snes", "Chrono Trigger (USA).sfc", 1_024);
        Installed(2, "snes", "Chrono Trigger (Japan) (Rev 1).sfc", 1_024);

        using var browse = new BrowseViewModel(_session);
        await Settled(browse);

        Assert.Equal(2, browse.Rows.Count);
        Assert.NotEqual(browse.Rows[0].Detail, browse.Rows[1].Detail);
        Assert.Contains(browse.Rows, row => row.Detail!.Contains("Rev 1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_online_row_says_whether_the_game_is_here()
    {
        using var stub = Library(2);
        Pair();
        Installed(1, "snes", "Game 0001.sfc", 1_024);

        using var browse = new BrowseViewModel(_session, Connect(stub));
        await Settled(browse);

        Assert.Contains("here: snes", browse.Rows[0].Value, StringComparison.Ordinal);
        Assert.Equal("not here", browse.Rows[1].Value);
    }

    // ------------------------------------------------------------------ install, end to end

    /// <summary>
    /// Search, one press, and it lands: the sentence this branch is built around.
    /// </summary>
    /// <remarks>
    /// Driven at the view-model level with no window, against the stub, which is what #105
    /// unblocked by threading the connection factory through the sets screens.
    /// </remarks>
    [Fact]
    public async Task A_game_found_in_browse_can_be_installed_in_one_press()
    {
        using var stub = Library(3);

        foreach (var rom in stub.Library)
        {
            stub.Content[rom.Id] = new byte[1_024];
        }

        Pair();

        var navigator = new Navigator(new BrowseViewModel(_session, Connect(stub)));
        var browse = Assert.IsType<BrowseViewModel>(navigator.Current);

        await Settled(browse);

        navigator.Handle(NavAction.Accept);
        var detail = Assert.IsType<ListScreen>(navigator.Current);

        // One press on the detail screen, and the sync opens over the set the game just joined.
        navigator.Handle(NavAction.Start);

        var sync = Assert.IsType<SyncViewModel>(navigator.Current);
        await SyncSettled(sync);

        Assert.Equal(SyncStage.Done, sync.State.Stage);

        // Four passes, and the six that are missing are missing for a reason each. Asserted
        // here because a person watching this screen is the only witness to which ran, and a
        // live install of a 2.6 GB title reported exactly these four.
        Assert.Contains("is on this device", sync.State.Detail, StringComparison.Ordinal);

        // The pick is a set, and it holds exactly the game that was picked.
        var picked = new PickedSetService(_session);
        var set = picked.Find();

        Assert.NotNull(set);
        Assert.Equal(CatalogScopeKind.Picked, set.Scope);
        Assert.Single(picked.Picks());

        // And the file is on the device, where EmulationStation reads it.
        var member = Assert.Single(_session.Store.SyncSets.Members(set.Id));
        Assert.True(File.Exists(Path.Combine(_tree.Root, "roms", member.Folder!, member.FsName)));

        sync.Dispose();
        detail.Dispose();
        browse.Dispose();
    }

    // ------------------------------------------------------------------ the rules that bite here

    [Fact]
    public async Task Nothing_browse_shows_names_a_face_button()
    {
        Installed(1, "snes", "Chrono Trigger.sfc", 2_048);

        using var browse = new BrowseViewModel(_session);
        await Settled(browse);

        // A sweep over every string these screens produce, not a check at one site. On a Switch
        // Pro the button printed A is es_input.cfg's b, which closes RomMBat.
        foreach (var text in Everything(browse))
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
    public async Task Browse_promises_no_action_that_nothing_is_bound_to()
    {
        Installed(1, "snes", "Chrono Trigger.sfc", 2_048);

        using var browse = new BrowseViewModel(_session);
        await Settled(browse);

        Assert.All(browse.Hints, hint => Assert.Contains(hint.Action, NavRepeat.Bound));
    }

    [Fact]
    public async Task Browse_is_reachable_and_leavable_with_the_gamepad_map_alone()
    {
        Installed(1, "snes", "Chrono Trigger.sfc", 2_048);

        var status = new StatusViewModel(
            _session,
            new GamepadStatus(GamepadAvailability.NoDevice, null, null, "No controller."))
        {
            OpenBrowse = () => new BrowseViewModel(_session),
        };

        var navigator = new Navigator(status);

        navigator.Handle(NavAction.Extra);
        var browse = Assert.IsType<BrowseViewModel>(navigator.Current);
        await Settled(browse);

        Assert.True(navigator.Handle(NavAction.Back));
        Assert.Same(status, navigator.Current);
    }

    // ------------------------------------------------------------------ helpers

    private static IEnumerable<string> Everything(BrowseViewModel browse)
    {
        yield return browse.Title;
        yield return browse.Note;

        foreach (var hint in browse.Hints)
        {
            yield return hint.Label;
        }

        foreach (var row in browse.Rows)
        {
            yield return row.Label;

            foreach (var text in new[] { row.Value, row.Detail }.OfType<string>())
            {
                yield return text;
            }
        }
    }

    private static async Task Settled(BrowseViewModel browse)
    {
        for (var attempt = 0; attempt < 300; attempt++)
        {
            if (!browse.IsLoading && browse.State.Page is not null)
            {
                return;
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Fail("The browse never settled.");
    }

    private static async Task SyncSettled(SyncViewModel sync)
    {
        for (var attempt = 0; attempt < 500; attempt++)
        {
            if (sync.State.Stage != SyncStage.Working)
            {
                return;
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Fail("The install never settled.");
    }

    /// <summary>Moves to the bottom of the page and one press past it, then waits.</summary>
    private static async Task PageDown(BrowseViewModel browse)
    {
        var offset = browse.State.Page!.Offset;

        for (var press = 0; press <= BrowseService.PageSize + 1; press++)
        {
            browse.Handle(NavAction.Down);

            if (browse.State.IsLoading || browse.State.Page!.Offset != offset)
            {
                break;
            }
        }

        await Settled(browse);
    }

    private static Func<Uri, RomMConnection> Connect(StubRomMServer stub) =>
        _ => new RomMConnection(new RomMClientOptions { Origin = Origin, AccessToken = "rmm_test" }, stub);

    private static StubRomMServer Library(int count)
    {
        var stub = new StubRomMServer();

        for (var id = 1; id <= count; id++)
        {
            stub.Library.Add(new StubRom(id, 1, "snes", "snes", $"Game {id:0000}", $"Game {id:0000}.sfc", "sfc", 1_024));
        }

        return stub;
    }

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

    private void Map(int platformId, string folder) =>
        _session.Store.PlatformMap.Record(
            new RomMBat.Core.Mapping.PlatformResolver(
                Fixtures.LoadEsSystems(),
                new Dictionary<string, string>())
                .Resolve(new RomMBat.Core.Mapping.RomMPlatform(platformId, folder, folder, folder)),
            Now);

    private void Installed(int romId, string folder, string fileName, long bytes)
    {
        var absolute = Path.Combine(_tree.Root, "roms", folder, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllBytes(absolute, new byte[bytes]);

        _session.Store.Files.Record(new LocalFile
        {
            Path = RelativePath.Create($"roms/{folder}/{fileName}"),
            Folder = folder,
            RomId = romId,
            Kind = LocalFileKind.Rom,
            FileName = fileName,
            SizeBytes = bytes,
            Origin = FileOrigin.Synced,
        });
    }
}
