using RomMBat.Core.Paths;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using RomMBat.UI.Input;
using RomMBat.UI.Screens;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The highlighted game's box art: where it comes from, when RomM is asked, and that nothing of
/// it is written to the tree.
/// </summary>
public sealed partial class BrowseScreenTests
{
    private static string SmallCover(int id) => $"/assets/romm/resources/roms/1/{id}/cover/small.png";

    /// <summary>Long enough that no test waits it out, so a fetch seen under it was not rested for.</summary>
    private static readonly TimeSpan NeverRests = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan ShortRest = TimeSpan.FromMilliseconds(20);

    [Fact]
    public async Task A_rested_cursor_shows_RomM_small_cover()
    {
        using var stub = CoveredLibrary(3);
        Pair();
        using var browse = new BrowseViewModel(_session, Connect(stub), covers: new CoverCache(), coverRest: ShortRest);

        await Settled(browse);
        var cover = await CoverSettled(browse);

        Assert.Equal(CoverState.Ready, cover.State);
        Assert.Equal(1, cover.RomId);
        Assert.Equal(CoverBytes(1), cover.Bytes);
        Assert.Equal([SmallCover(1)], stub.AssetRequests);
    }

    /// <summary>
    /// A cursor that never rests asks RomM for nothing, and the box waits for the row it is on.
    /// </summary>
    /// <remarks>
    /// Key repeat moves every 90 ms, well inside the 250 ms rest, so this is a held d-pad.
    /// </remarks>
    [Fact]
    public async Task A_moving_cursor_fetches_no_cover()
    {
        using var stub = CoveredLibrary(10);
        Pair();
        using var browse = new BrowseViewModel(_session, Connect(stub), covers: new CoverCache(), coverRest: NeverRests);

        await Settled(browse);

        for (var press = 0; press < 5; press++)
        {
            browse.Handle(NavAction.Down);
        }

        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Empty(stub.AssetRequests);
        Assert.Equal(new Cover(CoverState.Waiting, 6), browse.Cover);
    }

    [Fact]
    public async Task A_cover_on_this_device_is_shown_without_asking_RomM()
    {
        using var stub = CoveredLibrary(1);
        Pair();
        Installed(1, "snes", "Game 0001.sfc", 1_024);
        var local = LocalThumbnail(1, "snes", "Game 0001");
        using var browse = new BrowseViewModel(_session, Connect(stub), covers: new CoverCache(), coverRest: NeverRests);

        await Settled(browse);
        var cover = await CoverSettled(browse);

        Assert.Equal(local, cover.Bytes);
        Assert.Empty(stub.AssetRequests);
    }

    /// <summary>The ruling: a cover read from RomM lives in memory and never in the tree.</summary>
    [Fact]
    public async Task A_cover_read_from_RomM_writes_nothing_to_the_tree()
    {
        using var stub = CoveredLibrary(1);
        Pair();
        var before = TreeFiles();
        using var browse = new BrowseViewModel(_session, Connect(stub), covers: new CoverCache(), coverRest: ShortRest);

        await Settled(browse);
        Assert.Equal(CoverState.Ready, (await CoverSettled(browse)).State);

        Assert.Equal(before, TreeFiles());
    }

    [Fact]
    public async Task A_cover_seen_once_this_session_is_not_asked_for_again()
    {
        using var stub = CoveredLibrary(3);
        Pair();
        var covers = new CoverCache();

        using (var first = new BrowseViewModel(_session, Connect(stub), covers: covers, coverRest: ShortRest))
        {
            await Settled(first);
            await CoverSettled(first);
        }

        using var second = new BrowseViewModel(_session, Connect(stub), covers: covers, coverRest: NeverRests);
        await Settled(second);

        Assert.Equal(CoverState.Ready, second.Cover.State);
        Assert.Single(stub.AssetRequests);
    }

    [Fact]
    public async Task A_game_RomM_has_no_cover_for_says_so()
    {
        using var stub = new StubRomMServer();
        stub.Library.Add(new StubRom(1, 1, "snes", "snes", "Bare", "Bare.sfc", "sfc", 1_024));
        Pair();
        using var browse = new BrowseViewModel(_session, Connect(stub), covers: new CoverCache(), coverRest: ShortRest);

        await Settled(browse);

        Assert.Equal(CoverState.Missing, (await CoverSettled(browse)).State);
        Assert.Empty(stub.AssetRequests);
    }

    [Fact]
    public async Task A_game_screen_shows_the_same_cover()
    {
        using var stub = CoveredLibrary(2);
        Pair();
        using var browse = new BrowseViewModel(_session, Connect(stub), covers: new CoverCache(), coverRest: NeverRests);

        await Settled(browse);
        browse.Handle(NavAction.Down);

        var detail = Assert.IsType<ListScreen>(browse.Handle(NavAction.Accept).Screen);

        for (var attempt = 0; attempt < 300 && detail.Cover!.Current.State == CoverState.Waiting; attempt++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Equal(new Cover(CoverState.Ready, 2, detail.Cover!.Current.Bytes), detail.Cover.Current);
        Assert.Equal(CoverBytes(2), detail.Cover.Current.Bytes);
        detail.Dispose();
    }

    /// <summary>
    /// The panel is widened for the art box on browse and on what is drawn over it, and nowhere else.
    /// </summary>
    /// <remarks>
    /// Found on the agent tree: at the menu panel's own width the box ran past its right edge.
    /// A popup over browse keeps the width, or opening VIEW OPTIONS would move the list it dims.
    /// </remarks>
    [Fact]
    public async Task Only_a_screen_with_art_widens_the_panel()
    {
        using var stub = CoveredLibrary(1);
        Pair();
        using var browse = new BrowseViewModel(_session, Connect(stub), covers: new CoverCache(), coverRest: NeverRests);
        await Settled(browse);

        using var options = new ViewOptionsScreen(browse);
        using var plain = new ListScreen("Plain", [new ListRow("Row", null, null)], _ => RomMBat.UI.Shell.ScreenCommand.Stay);

        Assert.True(RomMBat.UI.Shell.ScreenView.HasCover(browse));
        Assert.True(RomMBat.UI.Shell.ScreenView.HasCover(options));
        Assert.False(RomMBat.UI.Shell.ScreenView.HasCover(plain));
    }

    [Fact]
    public void The_cover_cache_keeps_to_its_bytes_dropping_the_least_recent()
    {
        var cache = new CoverCache(capacityBytes: 300);

        cache.Add(1, new byte[100]);
        cache.Add(2, new byte[100]);
        cache.Add(3, new byte[100]);
        Assert.True(cache.TryGet(1, out _));
        cache.Add(4, new byte[100]);

        Assert.Equal(300, cache.Bytes);
        Assert.True(cache.TryGet(1, out _));
        Assert.False(cache.TryGet(2, out _));
        Assert.True(cache.TryGet(4, out _));
    }

    private static byte[] CoverBytes(int id) => [0x89, 0x50, 0x4E, 0x47, (byte)id];

    private static StubRomMServer CoveredLibrary(int count)
    {
        var stub = new StubRomMServer();

        for (var id = 1; id <= count; id++)
        {
            stub.Library.Add(new StubRom(id, 1, "snes", "snes", $"Game {id:0000}", $"Game {id:0000}.sfc", "sfc", 1_024)
            {
                Metadata = new StubRomMetadata(),
            });
            stub.Media[SmallCover(id)] = CoverBytes(id);
        }

        return stub;
    }

    private byte[] LocalThumbnail(int romId, string folder, string stem)
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0xAA };
        var relative = $"roms/{folder}/images/{stem}-thumb.png";
        var absolute = Path.Combine(_tree.Root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllBytes(absolute, bytes);

        _session.Store.Files.Record(new LocalFile
        {
            Path = RelativePath.Create(relative),
            Folder = folder,
            RomId = romId,
            Kind = LocalFileKind.Thumbnail,
            FileName = Path.GetFileName(relative),
            SizeBytes = bytes.Length,
            Origin = FileOrigin.Synced,
        });

        return bytes;
    }

    private string[] TreeFiles() =>
        [.. Directory.EnumerateFiles(_tree.Root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(_tree.Root, path))
            .Where(path => !path.Contains("rommbat.db", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)];

    private static async Task<Cover> CoverSettled(BrowseViewModel browse)
    {
        for (var attempt = 0; attempt < 300; attempt++)
        {
            if (browse.Cover.State is CoverState.Ready or CoverState.Missing)
            {
                return browse.Cover;
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Fail("The cover never settled.");
        return browse.Cover;
    }
}
