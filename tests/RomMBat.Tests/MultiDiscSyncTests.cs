using System.Security.Cryptography;
using System.Text;
using RomM.Client;
using RomM.Client.Catalog;
using RomMBat.Core.Content;
using RomMBat.Core.Mapping;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// A multi-disc game on a system whose certification settled the layout: psx.
/// </summary>
/// <remarks>
/// The layout is RomM's own and the one RetroBat's EmulationStation lists as one game: a folder
/// named after the rom holding every disc and an <c>.m3u</c> named after the folder. Measured
/// with every psx row RetroBat offers; see <c>data/retrobat/multi_file.json</c>.
/// </remarks>
public sealed class MultiDiscSyncTests : IDisposable
{
    private const string SetName = "Metal Gear Solid (USA) (Rev 1)";

    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();

    public void Dispose() => _tree.Dispose();

    [Fact]
    public async Task A_disc_set_lands_as_a_folder_with_a_playlist_and_a_second_run_does_nothing()
    {
        using var stub = DiscSet(
            (11, $"Metal Gear Solid (USA) (Disc 1) (Rev 1).chd", Bytes('1', 5000)),
            (12, $"Metal Gear Solid (USA) (Disc 2) (Rev 1).chd", Bytes('2', 4000)));
        using var store = LocalStore.Open(_tree.Install());

        var first = await SyncAsync(stub, store, TestContext.Current.CancellationToken);

        Assert.Equal(1, first.Downloaded);
        Assert.Equal(0, first.Failed);

        var folder = _tree.Install().Resolve(RelativePath.Create($"roms/psx/{SetName}"));
        Assert.Equal(Bytes('1', 5000), File.ReadAllBytes(Path.Combine(folder, "Metal Gear Solid (USA) (Disc 1) (Rev 1).chd")));
        Assert.Equal(Bytes('2', 4000), File.ReadAllBytes(Path.Combine(folder, "Metal Gear Solid (USA) (Disc 2) (Rev 1).chd")));

        // RomM's own format, which every psx row measured loads: names only, LF, no trailing
        // newline, no byte order mark.
        Assert.Equal(
            "Metal Gear Solid (USA) (Disc 1) (Rev 1).chd\nMetal Gear Solid (USA) (Disc 2) (Rev 1).chd"u8.ToArray(),
            File.ReadAllBytes(Path.Combine(folder, SetName + ".m3u")));

        // One game row, the playlist, and the discs beside it as parts.
        var rows = store.Files.ForRom(1);
        var game = Assert.Single(rows, row => row.Kind == LocalFileKind.Rom);
        Assert.Equal($"roms/psx/{SetName}/{SetName}.m3u", game.Path.Value);
        Assert.Equal(2, rows.Count(row => row.Kind == LocalFileKind.RomPart));

        // Each disc through the per-file route, never the whole rom as a zip.
        Assert.Equal(2, stub.ContentRequests.Count);
        Assert.All(stub.ContentRequests, request => Assert.Contains("#", request, StringComparison.Ordinal));

        var plan = new ContentPlanner(_tree.Install(), store).Plan(Set(store), Members(store));
        Assert.True(plan.IsNoOp);

        var second = await SyncAsync(stub, store, TestContext.Current.CancellationToken);

        Assert.True(second.IsNoOp);
        Assert.Equal(2, stub.ContentRequests.Count);
    }

    [Fact]
    public async Task A_playlist_the_server_holds_is_taken_as_it_is()
    {
        var playlist = "Metal Gear Solid (USA) (Disc 1) (Rev 1).chd\nMetal Gear Solid (USA) (Disc 2) (Rev 1).chd"u8.ToArray();

        using var stub = DiscSet(
            (11, "Metal Gear Solid (USA) (Disc 1) (Rev 1).chd", Bytes('1', 5000)),
            (12, "Metal Gear Solid (USA) (Disc 2) (Rev 1).chd", Bytes('2', 4000)),
            (13, SetName + ".m3u", playlist));
        using var store = LocalStore.Open(_tree.Install());

        var outcome = await SyncAsync(stub, store, TestContext.Current.CancellationToken);

        Assert.Equal(0, outcome.Failed);
        Assert.Equal(3, stub.ContentRequests.Count);

        var game = Assert.Single(store.Files.ForRom(1), row => row.Kind == LocalFileKind.Rom);
        Assert.Equal(playlist, File.ReadAllBytes(_tree.Install().Resolve(game.Path)));
    }

    [Fact]
    public async Task A_generated_playlist_names_the_cue_sheets_and_not_the_bins_they_point_at()
    {
        using var stub = DiscSet(
            (21, "Metal Gear Solid (USA) (Disc 1).bin", Bytes('a', 3000)),
            (22, "Metal Gear Solid (USA) (Disc 1).cue", Bytes('b', 97)),
            (23, "Metal Gear Solid (USA) (Disc 2).bin", Bytes('c', 3000)),
            (24, "Metal Gear Solid (USA) (Disc 2).cue", Bytes('d', 97)));
        using var store = LocalStore.Open(_tree.Install());

        await SyncAsync(stub, store, TestContext.Current.CancellationToken);

        var game = Assert.Single(store.Files.ForRom(1), row => row.Kind == LocalFileKind.Rom);
        Assert.Equal(
            "Metal Gear Solid (USA) (Disc 1).cue\nMetal Gear Solid (USA) (Disc 2).cue",
            File.ReadAllText(_tree.Install().Resolve(game.Path)));
        Assert.Equal(4, store.Files.ForRom(1).Count(row => row.Kind == LocalFileKind.RomPart));
    }

    [Theory]
    [InlineData(".cue", new[] { ".img" })]
    [InlineData(".ccd", new[] { ".img", ".sub" })]
    public async Task A_generated_playlist_names_the_sheet_and_not_the_image_that_shares_its_name(
        string sheet,
        string[] companions)
    {
        var files = new List<(int, string, byte[])>();
        foreach (var disc in new[] { 1, 2 })
        {
            files.Add((files.Count + 1, $"X (Disc {disc}){sheet}", Bytes('s', 97)));
            files.AddRange(companions.Select(extension =>
                (files.Count + 1, $"X (Disc {disc}){extension}", Bytes((char)('0' + disc), 3000))));
        }

        using var stub = DiscSet([.. files]);
        using var store = LocalStore.Open(_tree.Install());

        await SyncAsync(stub, store, TestContext.Current.CancellationToken);

        var game = Assert.Single(store.Files.ForRom(1), row => row.Kind == LocalFileKind.Rom);
        Assert.Equal(
            $"X (Disc 1){sheet}\nX (Disc 2){sheet}",
            File.ReadAllText(_tree.Install().Resolve(game.Path)));
    }

    [Fact]
    public async Task A_disc_cut_off_mid_transfer_resumes_and_the_playlist_waits_for_it()
    {
        var second = Bytes('2', 4000);

        using var stub = DiscSet(
            (11, "Metal Gear Solid (USA) (Disc 1) (Rev 1).chd", Bytes('1', 5000)),
            (12, "Metal Gear Solid (USA) (Disc 2) (Rev 1).chd", second));
        using var store = LocalStore.Open(_tree.Install());

        // Disc 1 lands whole and the link drops part way through disc 2.
        stub.DropMemberAfterBytes = (12, 1500);

        var interrupted = await SyncAsync(stub, store, TestContext.Current.CancellationToken);
        Assert.Equal(1, interrupted.Failed);

        var install = _tree.Install();

        // No playlist, so EmulationStation lists nothing rather than a game missing a disc.
        var playlist = install.Resolve(RelativePath.Create($"roms/psx/{SetName}/{SetName}.m3u"));
        Assert.False(File.Exists(playlist));
        Assert.True(File.Exists(install.Resolve(ContentPlanner.PartFor(1, 12))));

        var resumed = await SyncAsync(stub, store, TestContext.Current.CancellationToken);

        Assert.Equal(0, resumed.Failed);
        Assert.True(File.Exists(playlist));
        Assert.Equal(second, File.ReadAllBytes(install.Resolve(RelativePath.Create(
            $"roms/psx/{SetName}/Metal Gear Solid (USA) (Disc 2) (Rev 1).chd"))));

        // Disc 1 was not fetched again, and disc 2 carried on from where it stopped.
        Assert.Single(stub.ContentRequests, request => request.StartsWith("1#11 ", StringComparison.Ordinal));
        Assert.Contains(stub.ContentRequests, request => request.StartsWith("1#12 bytes=1500-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_gamelist_names_the_playlist_inside_the_folder()
    {
        using var stub = DiscSet(
            (11, "Metal Gear Solid (USA) (Disc 1) (Rev 1).chd", Bytes('1', 5000)),
            (12, "Metal Gear Solid (USA) (Disc 2) (Rev 1).chd", Bytes('2', 4000)));
        using var store = LocalStore.Open(_tree.Install());

        await SyncAsync(stub, store, TestContext.Current.CancellationToken);

        new GamelistSync(_tree.Install(), store).Write("psx");

        var gamelist = File.ReadAllText(_tree.Install().Resolve(RelativePath.Create("roms/psx/gamelist.xml")));
        Assert.Contains($"<path>./{SetName}/{SetName}.m3u</path>", gamelist, StringComparison.Ordinal);
        Assert.DoesNotContain(".chd</path>", gamelist, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Removing_the_game_takes_every_disc_the_playlist_and_the_folder()
    {
        using var stub = DiscSet(
            (11, "Metal Gear Solid (USA) (Disc 1) (Rev 1).chd", Bytes('1', 5000)),
            (12, "Metal Gear Solid (USA) (Disc 2) (Rev 1).chd", Bytes('2', 4000)));
        using var store = LocalStore.Open(_tree.Install());
        var install = _tree.Install();

        await SyncAsync(stub, store, TestContext.Current.CancellationToken);

        var planner = new EvictionPlanner(store);
        var outcome = planner.Apply(planner.PlanRemoval([1], releasing: [Set(store).Id]), install);

        Assert.Equal(1, outcome.Removed);
        Assert.Empty(outcome.Problems);
        Assert.Empty(store.Files.ForRom(1));
        Assert.False(Directory.Exists(install.Resolve(RelativePath.Create($"roms/psx/{SetName}"))));
    }

    [Fact]
    public async Task A_disc_set_on_a_system_nobody_certified_is_still_excluded()
    {
        using var stub = DiscSet(
            (11, "Metal Gear Solid (USA) (Disc 1) (Rev 1).chd", Bytes('1', 5000)),
            (12, "Metal Gear Solid (USA) (Disc 2) (Rev 1).chd", Bytes('2', 4000)));
        using var store = LocalStore.Open(_tree.Install());

        var set = store.SyncSets.Add(
            new SyncSetDefinition { Name = "psx", Scope = CatalogScopeKind.Platform, ScopeValue = "1" },
            DateTimeOffset.UtcNow);

        var systems = Fixtures.Synthesize(("psx", ".cue .chd .m3u"));
        var resolver = new SetResolver(systems, new PlatformResolver(systems), layouts: MultiFileLayouts.None);

        using var connection = Connect(stub);
        var resolution = await resolver.ResolveAsync(
            set,
            new RomPager(connection, SetResolver.QueryFor(set)),
            DateTimeOffset.UtcNow,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(resolution.Members);
        Assert.Equal(MemberState.ExcludedMultiFile, Assert.Single(resolution.Excluded).State);
    }

    [Fact]
    public async Task A_set_whose_disc_went_missing_with_its_row_is_fetched_again_rather_than_called_present()
    {
        // The state the media sweep left on a real install: the discs and their rows gone, the
        // playlist and its row intact. Every remaining row agreed with the disk, so the plan
        // called the game present and nothing would ever have repaired it.
        using var stub = DiscSet(
            (11, "Metal Gear Solid (USA) (Disc 1) (Rev 1).chd", Bytes('1', 5000)),
            (12, "Metal Gear Solid (USA) (Disc 2) (Rev 1).chd", Bytes('2', 4000)));
        using var store = LocalStore.Open(_tree.Install());
        var install = _tree.Install();

        await SyncAsync(stub, store, TestContext.Current.CancellationToken);

        var disc = RelativePath.Create($"roms/psx/{SetName}/Metal Gear Solid (USA) (Disc 2) (Rev 1).chd");
        File.Delete(install.Resolve(disc));
        store.Files.Remove(disc);

        var plan = new ContentPlanner(install, store).Plan(Set(store), Members(store));
        Assert.Equal(ContentAction.Download, Assert.Single(plan.Steps).Action);

        await SyncAsync(stub, store, TestContext.Current.CancellationToken);
        Assert.Equal(Bytes('2', 4000), File.ReadAllBytes(install.Resolve(disc)));
    }

    [Fact]
    public void A_disc_is_never_artwork_so_a_media_sweep_cannot_take_it()
    {
        // Found on the live install: MediaSync removed every synced file that was not the game
        // row or firmware and not a wanted media kind, and the discs of a set are neither. They
        // landed, verified, and were deleted by the media pass that followed.
        Assert.False(LocalFileKind.RomPart.IsMedia());
        Assert.False(LocalFileKind.Rom.IsMedia());
        Assert.False(LocalFileKind.Firmware.IsMedia());

        Assert.All(
            Enum.GetValues<LocalFileKind>().Where(kind => kind.IsMedia()),
            kind => Assert.Contains(kind, new[]
            {
                LocalFileKind.Image, LocalFileKind.Thumbnail, LocalFileKind.Marquee,
                LocalFileKind.Video, LocalFileKind.Manual,
            }));
    }

    [Fact]
    public void The_bundled_table_unlocks_psx_and_nothing_else()
    {
        // A system is added to multi_file.json by its own certification pass, so a second entry
        // appearing without one is what this is here to notice.
        Assert.Equal(["psx"], MultiFileLayouts.Bundled.Systems);

        var psx = MultiFileLayouts.Bundled.For("psx")!;
        Assert.True(psx.IsDisc("Game (Disc 1).cue"));
        Assert.True(psx.IsDisc("Game (Disc 1).chd"));
        Assert.False(psx.IsDisc("Game (Disc 1).bin"));
        Assert.False(psx.IsDisc("Game.m3u"));
    }

    [Fact]
    public void A_table_naming_a_layout_nobody_measured_is_refused()
    {
        Assert.Throws<InvalidDataException>(() => MultiFileLayouts.Parse(
            """{ "systems": { "ps2": { "layout": "flat", "playlist": "m3u", "disc_extensions": [".chd"] } } }"""));
    }

    private async Task<ContentSyncOutcome> SyncAsync(StubRomMServer stub, LocalStore store, CancellationToken cancellationToken)
    {
        await ResolveAsync(stub, store, cancellationToken);

        var install = _tree.Install();
        var plan = new ContentPlanner(install, store).Plan(Set(store), Members(store));

        using var connection = Connect(stub);
        return await new ContentSync(install, store, connection).ApplyAsync(plan, cancellationToken: cancellationToken);
    }

    private static async Task ResolveAsync(StubRomMServer stub, LocalStore store, CancellationToken cancellationToken)
    {
        var set = store.SyncSets.Find("psx")
            ?? store.SyncSets.Add(
                new SyncSetDefinition { Name = "psx", Scope = CatalogScopeKind.Platform, ScopeValue = "1" },
                DateTimeOffset.UtcNow);

        var systems = Fixtures.Synthesize(("psx", ".cue .chd .m3u .pbp"));
        var resolver = new SetResolver(systems, new PlatformResolver(systems));

        using var connection = Connect(stub);
        var resolution = await resolver.ResolveAsync(
            set,
            new RomPager(connection, SetResolver.QueryFor(set)),
            DateTimeOffset.UtcNow,
            cancellationToken: cancellationToken);

        store.SyncSets.ReplaceMembers(
            set.Id,
            [.. resolution.Members, .. resolution.Excluded],
            resolution.Summary,
            DateTimeOffset.UtcNow);
    }

    private static SyncSetDefinition Set(LocalStore store) => store.SyncSets.List()[0];

    private static IReadOnlyList<SyncSetMember> Members(LocalStore store) => store.SyncSets.Members(Set(store).Id);

    private static RomMConnection Connect(StubRomMServer stub) =>
        new(new RomMClientOptions { Origin = new Uri("https://romm.test"), AccessToken = "rmm_test" }, stub);

    private static byte[] Bytes(char fill, int length) => Encoding.ASCII.GetBytes(new string(fill, length));

    /// <summary>A psx library holding one multi-file rom, rom 1, made of these members.</summary>
    private static StubRomMServer DiscSet(params (int Id, string Name, byte[] Bytes)[] files)
    {
        var stub = new StubRomMServer();
        stub.Platforms.Add(new StubPlatform(1, "psx", "psx", "PlayStation"));

        var members = files.Select(file => new StubRomFile(file.Id, file.Name, file.Bytes)).ToList();

        stub.Library.Add(new StubRom(1, 1, "psx", "psx", SetName, SetName, string.Empty, members.Sum(file => (long)file.Bytes.Length))
        {
            HasMultipleFiles = true,
            Files = members,
#pragma warning disable CA5351 // MD5, deliberately: it is what RomM publishes.
            Md5Hash = Convert.ToHexString(MD5.HashData(files[0].Bytes)).ToLowerInvariant(),
#pragma warning restore CA5351
        });

        return stub;
    }
}
