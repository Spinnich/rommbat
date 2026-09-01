using RomM.Client;
using RomM.Client.Catalog;
using RomM.Client.Content;
using RomMBat.Core.Content;
using RomMBat.Core.Mapping;
using RomMBat.Core.Metadata;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Resolving a set, fetching its media, writing its gamelist, and doing it all again.
/// </summary>
/// <remarks>
/// The end-to-end shape of M4, driven against the stub so the interesting cases (an unreachable
/// server, a full budget, a moved install) are reachable at all.
/// </remarks>
public sealed class MediaSyncTests : IDisposable
{
    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();

    public MediaSyncTests()
    {
        // Written out because the fixture's bare tree has no es_settings.cfg and a real RetroBat
        // always does: its installer seeds one from system/templates carrying both switches as
        // true. These tests are about what the media pass does rather than about what decides
        // it, so they get the stock answer. The policy tests below write their own.
        WriteEsSettings(_tree.Install(), videos: true, manuals: false);
    }

    public void Dispose()
    {
        _tree.Dispose();
        GC.SuppressFinalize(this);
    }

    // ------------------------------------------------------------------ what a walk costs

    [Fact]
    public async Task Metadata_for_every_member_arrives_without_one_extra_request()
    {
        using var stub = Library(6);
        using var store = LocalStore.Open(_tree.Install());

        var resolution = await ResolveAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        // The claim this milestone's whole shape rests on. GET /api/roms/{id} would be one
        // request per game, 0.15 s each; the paged read already carries every field.
        Assert.Equal(1, stub.RomPagesServed);
        Assert.DoesNotContain(stub.RequestLog, path => path.Contains("/api/roms/1", StringComparison.Ordinal));

        Assert.Equal(6, resolution.Members.Count);
        Assert.Equal(6, resolution.Metadata.Count);
        Assert.Equal(6, store.Metadata.Count());

        var one = store.Metadata.Find(1)!;
        Assert.Equal("snes", one.Folder);
        Assert.Equal("Nintendo", one.Developer);
        Assert.Equal("1-2", one.Players);
        Assert.Equal("19940916T000000", one.ReleaseDate);
        Assert.Equal("0.83", one.Rating);
        Assert.Equal("us", one.Region);
        Assert.Equal("en", one.Languages);
    }

    [Fact]
    public async Task A_set_capped_far_below_its_scope_holds_metadata_for_the_cap_not_the_scope()
    {
        // The bounded-memory claim. A description runs to 11,719 characters on a real library,
        // so holding one per scanned row would make a walk's memory a function of the library.
        using var stub = Library(2_000);
        using var store = LocalStore.Open(_tree.Install());

        var resolution = await ResolveAsync(
            stub,
            store,
            maxGames: 25,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2_000, resolution.Scanned);
        Assert.Equal(25, resolution.Members.Count);
        Assert.Equal(25, resolution.Metadata.Count);

        // 8 pages of 250 for 2,000 ROMs, and not one request more for the metadata.
        Assert.Equal(8, stub.RomPagesServed);
        Assert.Equal(25, store.Metadata.Count());
    }

    // ------------------------------------------------------------------ media

    [Fact]
    public async Task Media_lands_beside_the_roms_under_the_names_RetroBat_expects()
    {
        using var stub = Library(2);
        using var store = LocalStore.Open(_tree.Install());

        await SyncAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        var images = Path.Combine(_tree.Root, "roms", "snes", "images");
        var videos = Path.Combine(_tree.Root, "roms", "snes", "videos");

        Assert.True(File.Exists(Path.Combine(images, "Game 1 (USA)-image.png")));
        Assert.True(File.Exists(Path.Combine(images, "Game 1 (USA)-thumb.png")));
        Assert.True(File.Exists(Path.Combine(images, "Game 1 (USA)-marquee.png")));
        Assert.True(File.Exists(Path.Combine(videos, "Game 1 (USA)-video.mp4")));

        // Manuals are off by default, and this library has none anyway.
        Assert.False(Directory.Exists(Path.Combine(_tree.Root, "roms", "snes", "manuals")));

        // Each is recorded against its ROM, which is what lets eviction take it along.
        var files = store.Files.ForRom(1);
        Assert.Equal(5, files.Count);
        Assert.Single(files, file => file.Kind == LocalFileKind.Rom);
        Assert.Single(files, file => file.Kind == LocalFileKind.Marquee);
    }

    [Fact]
    public async Task A_second_run_fetches_no_media_and_writes_no_gamelist()
    {
        using var stub = Library(2);
        using var store = LocalStore.Open(_tree.Install());

        await SyncAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        var requestsAfterFirst = stub.AssetRequests.Count;
        var gamelist = Path.Combine(_tree.Root, "roms", "snes", "gamelist.xml");
        var bytes = File.ReadAllBytes(gamelist);

        var second = await SyncAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, second.Media.Downloaded);
        Assert.Equal(8, second.Media.AlreadyPresent);
        Assert.Equal(requestsAfterFirst, stub.AssetRequests.Count);

        Assert.True(second.Gamelists.IsNoOp);
        Assert.Equal(bytes, File.ReadAllBytes(gamelist));
    }

    [Fact]
    public async Task Artwork_a_user_scraped_is_adopted_rather_than_overwritten()
    {
        using var stub = Library(1);
        using var store = LocalStore.Open(_tree.Install());

        // The user's own scraper writes to exactly the name RomMBat would use.
        var images = Path.Combine(_tree.Root, "roms", "snes", "images");
        Directory.CreateDirectory(images);
        var theirs = Path.Combine(images, "Game 1 (USA)-image.png");
        File.WriteAllBytes(theirs, [1, 2, 3, 4, 5]);

        var outcome = await SyncAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([1, 2, 3, 4, 5], File.ReadAllBytes(theirs));
        Assert.True(outcome.Media.Adopted >= 1);

        var recorded = store.Files.ForRom(1, LocalFileKind.Image).Single();
        Assert.Equal(FileOrigin.Adopted, recorded.Origin);

        // Adopted media is the user's, so it never counts against RomMBat's budget.
        Assert.DoesNotContain(
            store.Files.List().Where(file => file.Origin == FileOrigin.Synced),
            file => file.Kind == LocalFileKind.Image);
    }

    [Fact]
    public async Task A_full_budget_stops_the_artwork_rather_than_the_games()
    {
        using var stub = Library(3);
        using var store = LocalStore.Open(_tree.Install());

        // Enough for the ROMs and almost nothing else.
        store.Settings.Set(SettingStore.ContentMaxBytes, 3 * 1024L + 512, DateTimeOffset.UtcNow);

        var outcome = await SyncAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(3, outcome.Content.Downloaded);
        Assert.True(outcome.Media.Blocked > 0);
        Assert.Contains(outcome.Media.Problems, problem => problem.Contains("budget is full", StringComparison.Ordinal));

        // And the gamelist is still written, referencing only what actually landed.
        var written = File.ReadAllText(Path.Combine(_tree.Root, "roms", "snes", "gamelist.xml"));
        Assert.Contains("<name>Game 1</name>", written, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ the whole thing, moved

    [Fact]
    public async Task A_populated_install_with_media_and_a_gamelist_moves_without_re_fetching()
    {
        using var stub = Library(3);

        using (var store = LocalStore.Open(_tree.Install()))
        {
            await SyncAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);
        }

        var requestsBefore = stub.AssetRequests.Count;
        Assert.True(requestsBefore > 0);

        // The whole tree turns up somewhere else, database, artwork and gamelist included,
        // which is what a drive letter changing from E: to F: does.
        using var moved = _tree.CopyToNewLocation();
        var relocated = moved.Install();

        using var store2 = LocalStore.OpenAt(relocated.DatabasePath);

        var gamelist = Path.Combine(moved.Root, "roms", "snes", "gamelist.xml");
        var bytes = File.ReadAllBytes(gamelist);

        // Nothing may have recorded where the install used to be, so a second pass at the new
        // location writes nothing and fetches nothing.
        var media = new MediaSync(relocated, store2, Connect(stub));
        var mediaOutcome = await media.ApplyAsync([1, 2, 3], cancellationToken: TestContext.Current.CancellationToken);

        var gamelists = await new GamelistSync(relocated, store2).ApplyAsync(
            ["snes"],
            emulationStation: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotEqual(_tree.Root, moved.Root);
        Assert.Equal(0, mediaOutcome.Downloaded);
        Assert.Equal(requestsBefore, stub.AssetRequests.Count);
        Assert.True(gamelists.IsNoOp);
        Assert.Equal(bytes, File.ReadAllBytes(gamelist));

        // And the gamelist still holds relative references, which is why the move was a non-event.
        var text = File.ReadAllText(gamelist);
        Assert.Contains("<image>./images/", text, StringComparison.Ordinal);
        Assert.DoesNotContain(_tree.Root, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(moved.Root, text, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------ eviction

    [Fact]
    public async Task Eviction_takes_a_games_media_and_its_gamelist_entry_with_it()
    {
        using var stub = Library(3);
        using var store = LocalStore.Open(_tree.Install());

        await SyncAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        var install = _tree.Install();
        var planner = new EvictionPlanner(store);
        var plan = planner.Plan(bytesToFree: long.MaxValue);

        // Every candidate carries its artwork, and the bytes freed say so.
        Assert.All(plan.Selected, candidate => Assert.Equal(4, candidate.Media.Count));
        Assert.All(plan.Selected, candidate => Assert.True(candidate.Bytes > candidate.File.SizeBytes));

        var outcome = planner.Apply(plan, install);

        Assert.Equal(3, outcome.Removed);
        Assert.Contains("snes", outcome.FoldersToRewrite);
        Assert.Empty(Directory.GetFiles(Path.Combine(_tree.Root, "roms", "snes", "images")));
        Assert.Empty(store.Files.List());

        // The gamelist is rewritten from local state, with no server involved at all.
        await new GamelistSync(install, store).ApplyAsync(
            outcome.FoldersToRewrite,
            emulationStation: null,
            cancellationToken: TestContext.Current.CancellationToken);

        var written = new System.Xml.XmlDocument();
        written.Load(Path.Combine(_tree.Root, "roms", "snes", "gamelist.xml"));

        Assert.Empty(written.SelectNodes("/gameList/game")!);
    }

    [Fact]
    public async Task Eviction_leaves_artwork_the_user_scraped_where_it_is()
    {
        using var stub = Library(1);
        using var store = LocalStore.Open(_tree.Install());

        var images = Path.Combine(_tree.Root, "roms", "snes", "images");
        Directory.CreateDirectory(images);
        var theirs = Path.Combine(images, "Game 1 (USA)-image.png");
        File.WriteAllBytes(theirs, [9, 9, 9]);

        await SyncAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        var planner = new EvictionPlanner(store);
        var outcome = planner.Apply(planner.Plan(bytesToFree: long.MaxValue), _tree.Install());

        Assert.Equal(1, outcome.Removed);

        // The ROM and the artwork RomMBat fetched are gone; the user's cover is untouched.
        Assert.False(File.Exists(Path.Combine(_tree.Root, "roms", "snes", "Game 1 (USA).sfc")));
        Assert.True(File.Exists(theirs));
        Assert.Equal([9, 9, 9], File.ReadAllBytes(theirs));
    }

    [Fact]
    public async Task A_media_file_that_will_not_delete_still_leaves_its_folder_to_be_rewritten()
    {
        using var stub = Library(1);
        using var store = LocalStore.Open(_tree.Install());

        await SyncAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        var video = store.Files.ForRom(1, LocalFileKind.Video).Single();
        var absolute = _tree.Install().Resolve(video.Path);

        // What a media player with the file open does to a delete.
        using var held = new FileStream(absolute, FileMode.Open, FileAccess.Read, FileShare.None);

        var planner = new EvictionPlanner(store);
        var outcome = planner.Apply(planner.Plan(bytesToFree: long.MaxValue), _tree.Install());

        // The ROM went, so its gamelist entry has to go too, whatever happened to the video.
        Assert.Equal(1, outcome.Removed);
        Assert.Contains("snes", outcome.FoldersToRewrite);
        Assert.False(File.Exists(Path.Combine(_tree.Root, "roms", "snes", "Game 1 (USA).sfc")));

        // Reported against the file that actually failed, and the cover still went.
        Assert.Contains(outcome.Problems, problem => problem.Contains("left behind", StringComparison.Ordinal));
        Assert.True(File.Exists(absolute));
        Assert.Empty(Directory.GetFiles(Path.Combine(_tree.Root, "roms", "snes", "images")));
    }

    [Fact]
    public async Task Media_the_server_declares_no_length_for_is_still_held_to_the_budget()
    {
        using var stub = Library(1);
        stub.MediaWithoutLength = true;

        using var store = LocalStore.Open(_tree.Install());

        // The ROM fits and 32 bytes are left over, against 64-byte media. With no
        // Content-Length there is nothing to refuse up front, so the read has to stop.
        store.Settings.Set(SettingStore.ContentMaxBytes, 1024L + 32, DateTimeOffset.UtcNow);

        var outcome = await SyncAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Content.Downloaded);
        Assert.True(outcome.Media.Failed > 0);
        Assert.Contains(
            outcome.Media.Problems,
            problem => problem.Contains("declared no length", StringComparison.Ordinal));

        // Nothing over budget reached the folder, and no partial file was left behind.
        var images = Path.Combine(_tree.Root, "roms", "snes", "images");
        Assert.True(!Directory.Exists(images) || Directory.GetFiles(images).Length == 0);
        Assert.Equal(0, outcome.Media.Downloaded);
    }

    // ------------------------------------------------------------------ helpers

    private sealed record SyncOutcome(ContentSyncOutcome Content, MediaSyncOutcome Media, GamelistSyncOutcome Gamelists);

    // ------------------------------------------------------------------ what RetroBat already says

    [Fact]
    public void RetroBats_own_scraper_toggles_decide_whether_video_and_manuals_are_fetched()
    {
        // Found by a hands-on pass: video was turned off in RetroBat's scraper and RomMBat kept
        // downloading it, which is two switches that look like they should agree and do not.
        // Same reasoning that makes the on-screen keyboard follow ES's Language setting.
        var install = _tree.Install();
        using var store = LocalStore.Open(install);

        WriteEsSettings(install, videos: true, manuals: true);
        Assert.Contains(MediaKind.Video, MediaPolicy.Read(store.Settings, install));

        WriteEsSettings(install, videos: false, manuals: true);
        Assert.DoesNotContain(MediaKind.Video, MediaPolicy.Read(store.Settings, install));

        // Manuals are not in RomMBat's own default, so a rule that filtered the default down
        // could never turn them on however RetroBat was set. A hands-on pass found exactly
        // that: manuals on upstream, none downloaded.
        Assert.Contains(MediaKind.Manual, MediaPolicy.Read(store.Settings, install));

        WriteEsSettings(install, videos: false, manuals: false);
        Assert.DoesNotContain(MediaKind.Manual, MediaPolicy.Read(store.Settings, install));

        // The three kinds ES has no toggle for are unaffected, because inventing keys for them
        // would be guessing at settings upstream does not have.
        var kinds = MediaPolicy.Read(store.Settings, install);
        Assert.Contains(MediaKind.Image, kinds);
        Assert.Contains(MediaKind.Thumbnail, kinds);
        Assert.Contains(MediaKind.Marquee, kinds);
    }

    [Fact]
    public void An_explicit_RomMBat_setting_still_wins_over_RetroBats()
    {
        // media.kinds is what somebody typed. A preference stated here is not overridden by one
        // stated elsewhere; ES's toggles shape the default, which is what a fresh install gets.
        var install = _tree.Install();
        using var store = LocalStore.Open(install);

        WriteEsSettings(install, videos: false, manuals: false);
        store.Settings.Set(MediaPolicy.SettingKey, "image,video", DateTimeOffset.UtcNow);

        var kinds = MediaPolicy.Read(store.Settings, install);

        Assert.Contains(MediaKind.Video, kinds);
        Assert.DoesNotContain(MediaKind.Marquee, kinds);
    }

    [Fact]
    public void A_scraper_switch_that_is_absent_is_off_rather_than_unknown()
    {
        // RetroBat seeds both switches as true from system/templates, and ES deletes a bool
        // whose value equals its own compiled default, which for both of these is false. So
        // turning one off deletes the key and a literal false never appears: absent is somebody
        // having said no, not an install nobody configured. Reading absent as RomMBat's own
        // default instead made turning video off do nothing at all, found by a hands-on pass
        // with 389 MB of video on one platform that no setting could reach.
        var install = _tree.Install();
        using var store = LocalStore.Open(install);

        File.Delete(install.Resolve(EsSettingsFile.Location));

        var nothingWritten = MediaPolicy.Read(store.Settings, install);
        Assert.DoesNotContain(MediaKind.Video, nothingWritten);
        Assert.DoesNotContain(MediaKind.Manual, nothingWritten);

        // The three ES has no switch for are untouched by any of this.
        Assert.Contains(MediaKind.Image, nothingWritten);
        Assert.Contains(MediaKind.Thumbnail, nothingWritten);
        Assert.Contains(MediaKind.Marquee, nothingWritten);

        // A file that exists and carries neither key is the same state, because that is exactly
        // what ES leaves behind when both switches are turned off.
        WriteEsSettings(install, videos: false, manuals: false);
        File.WriteAllText(
            install.Resolve(EsSettingsFile.Location),
            File.ReadAllText(install.Resolve(EsSettingsFile.Location))
                .Replace("""<bool name="ScrapeVideos" value="false" />""", string.Empty, StringComparison.Ordinal)
                .Replace("""<bool name="ScrapeManual" value="false" />""", string.Empty, StringComparison.Ordinal));

        var pruned = MediaPolicy.Read(store.Settings, install);
        Assert.DoesNotContain(MediaKind.Video, pruned);
        Assert.DoesNotContain(MediaKind.Manual, pruned);
    }

    [Fact]
    public async Task An_advertised_path_the_server_does_not_serve_is_forgotten_rather_than_re_asked()
    {
        // Measured on a live library: 39 of 40 games on one platform advertised a video that
        // answered 404, so every sync spent 39 requests and printed 39 problems, for ever.
        // Forgetting the path turns it into the ordinary Missing case, and a resolve rewrites
        // metadata from the server, so it comes back the moment RomM starts serving it.
        using var stub = Library(1);
        using var store = LocalStore.Open(_tree.Install());

        // Every kind is served except the video, whose path the row still advertises.
        stub.Media.Remove("/assets/romm/resources/roms/1/1/video/video.mp4");

        var first = await SyncAsync(stub, store, TestContext.Current.CancellationToken);

        Assert.Equal(1, first.Media.Failed + first.Media.Missing);
        Assert.DoesNotContain(MediaKind.Video, store.Metadata.Find(1)!.MediaPaths.Keys);

        var asked = stub.AssetRequests.Count(path => path.Contains("/video/", StringComparison.Ordinal));

        var second = await new MediaSync(_tree.Install(), store, Connect(stub))
            .ApplyAsync([1], cancellationToken: TestContext.Current.CancellationToken);

        // Not asked a second time, and not reported as a failure either.
        Assert.Equal(asked, stub.AssetRequests.Count(path => path.Contains("/video/", StringComparison.Ordinal)));
        Assert.Equal(0, second.Failed);
    }

    [Fact]
    public async Task Artwork_of_a_kind_that_has_been_turned_off_is_taken_back()
    {
        // Turning a kind off used to stop future downloads and nothing else, so what had already
        // been fetched stayed for ever: eviction removes whole games under budget pressure and
        // has no notion of a kind. Measured on a real install, 1.09 GB of video on one platform.
        using var stub = Library(1);
        using var store = LocalStore.Open(_tree.Install());

        await SyncAsync(stub, store, TestContext.Current.CancellationToken);

        var video = Path.Combine(_tree.Root, "roms", "snes", "videos", "Game 1 (USA)-video.mp4");
        Assert.True(File.Exists(video));

        // The user turns video off in RetroBat, exactly as a hands-on pass did.
        WriteEsSettings(_tree.Install(), videos: false, manuals: false);

        var outcome = await new MediaSync(_tree.Install(), store, Connect(stub))
            .ApplyAsync([1], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Removed);
        Assert.False(File.Exists(video), "the video stayed after its kind was turned off");
        Assert.DoesNotContain(store.Files.ForRom(1), file => file.Kind == LocalFileKind.Video);

        // And the kinds still wanted are untouched.
        Assert.Contains(store.Files.ForRom(1), file => file.Kind == LocalFileKind.Image);
    }

    [Fact]
    public async Task A_users_own_scrape_is_never_taken_back_when_a_kind_is_turned_off()
    {
        // The fence. Adopted means the user's own file at exactly the name RomMBat would use,
        // and RomMBat's setting is not a licence to delete what it did not download.
        using var stub = Library(1);
        using var store = LocalStore.Open(_tree.Install());

        var videos = Path.Combine(_tree.Root, "roms", "snes", "videos");
        Directory.CreateDirectory(videos);
        var theirs = Path.Combine(videos, "Game 1 (USA)-video.mp4");
        File.WriteAllBytes(theirs, [9, 9, 9]);

        await SyncAsync(stub, store, TestContext.Current.CancellationToken);
        Assert.Equal(FileOrigin.Adopted, store.Files.ForRom(1, LocalFileKind.Video).Single().Origin);

        WriteEsSettings(_tree.Install(), videos: false, manuals: false);

        var outcome = await new MediaSync(_tree.Install(), store, Connect(stub))
            .ApplyAsync([1], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, outcome.Removed);
        Assert.Equal([9, 9, 9], File.ReadAllBytes(theirs));
    }

    [Fact]
    public async Task An_install_with_no_readable_settings_file_is_never_a_licence_to_delete()
    {
        // The read and the delete are two questions and only the read has an answer here. An
        // absent key is somebody having turned a switch off, which the test above pins, but an
        // absent *file* is an install that has never said anything, and EsSettingsFile answers
        // both with the same empty <config>. Sweeping the artwork on the strength of that is
        // deleting a user's files because RomMBat could not find a file of RetroBat's.
        //
        // Both unreadable shapes, because a truncated file after a power cut on a handheld is
        // the case this project is built for and XDocument.Load throws on it, which nothing
        // between here and the sync screen catches.
        using var stub = Library(1);
        using var store = LocalStore.Open(_tree.Install());

        await SyncAsync(stub, store, TestContext.Current.CancellationToken);

        var video = Path.Combine(_tree.Root, "roms", "snes", "videos", "Game 1 (USA)-video.mp4");
        var settings = _tree.Install().Resolve(EsSettingsFile.Location);
        Assert.True(File.Exists(video));

        File.Delete(settings);

        var missing = await new MediaSync(_tree.Install(), store, Connect(stub))
            .ApplyAsync([1], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, missing.Removed);
        Assert.True(File.Exists(video), "the video was swept because es_settings.cfg was not there");

        // Half a file, which is what an interrupted write leaves.
        File.WriteAllText(settings, """<?xml version="1.0"?><config><bool name="ScrapeVi""");

        var truncated = await new MediaSync(_tree.Install(), store, Connect(stub))
            .ApplyAsync([1], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, truncated.Removed);
        Assert.True(File.Exists(video), "the video was swept because es_settings.cfg would not parse");

        // And the read is unchanged by any of it: video is still off, which is what stops the
        // fix from quietly restoring the downloads a hands-on pass turned off.
        Assert.DoesNotContain(MediaKind.Video, MediaPolicy.Read(store.Settings, _tree.Install()));
    }

    /// <summary>Writes the two scraper toggles ES actually has.</summary>
    private static void WriteEsSettings(RetroBatInstall install, bool videos, bool manuals)
    {
        var path = install.Resolve(EsSettingsFile.Location);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Wrapped in <config>, as RetroBat writes it. A flat list of elements is not the shape
        // on disk and would be testing a file EmulationStation never produces.
        var lines = new[]
        {
            """<?xml version="1.0"?>""",
            "<config>",
            $"""	<bool name="ScrapeVideos" value="{(videos ? "true" : "false")}" />""",
            $"""	<bool name="ScrapeManual" value="{(manuals ? "true" : "false")}" />""",
            "</config>",
        };

        File.WriteAllLines(path, lines);
    }

    /// <summary>A stub library of <paramref name="count"/> SNES games, each with metadata and media.</summary>
    private static StubRomMServer Library(int count)
    {
        var stub = new StubRomMServer();
        stub.Platforms.Add(new StubPlatform(1, "snes", "snes", "Super Nintendo"));

        for (var id = 1; id <= count; id++)
        {
            stub.Library.Add(new StubRom(
                id,
                1,
                "snes",
                "snes",
                $"Game {id}",
                $"Game {id} (USA).sfc",
                "sfc",
                1024)
            {
                Metadata = new StubRomMetadata(),
            });

            stub.Content[id] = new byte[1024];

            foreach (var kind in new[] { "cover/big.png", "cover/small.png", "video/video.mp4", "logo/logo.png" })
            {
                stub.Media[$"/assets/romm/resources/roms/1/{id}/{kind}"] = new byte[64];
            }
        }

        return stub;
    }

    private static RomMConnection Connect(StubRomMServer stub) =>
        new(new RomMClientOptions { Origin = new Uri("http://stub.invalid"), AccessToken = "rmm_test" }, stub);

    private static async Task<SetResolution> ResolveAsync(
        StubRomMServer stub,
        LocalStore store,
        int? maxGames = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = Connect(stub);

        var set = store.SyncSets.Find("snes") ?? store.SyncSets.Add(
            new SyncSetDefinition
            {
                Name = "snes",
                Scope = CatalogScopeKind.Platform,
                ScopeValue = "1",
                MaxGames = maxGames,
            },
            DateTimeOffset.UtcNow);

        var install = Fixtures.Synthesize(("snes", ".sfc .smc"));
        var resolver = new SetResolver(install, new PlatformResolver(install, store.PlatformMap.Overrides()));
        var resolvedAt = DateTimeOffset.UtcNow;

        var resolution = await resolver.ResolveAsync(
            set,
            new RomPager(connection, SetResolver.QueryFor(set)),
            resolvedAt,
            cancellationToken: cancellationToken);

        store.SyncSets.ReplaceMembers(
            set.Id,
            [.. resolution.Members, .. resolution.Excluded],
            resolution.Summary,
            resolvedAt);

        foreach (var metadata in resolution.Metadata)
        {
            store.Metadata.Record(metadata);
        }

        return resolution;
    }

    /// <summary>Resolves, pulls, fetches media and writes gamelists, as <c>sync</c> does.</summary>
    private async Task<SyncOutcome> SyncAsync(
        StubRomMServer stub,
        LocalStore store,
        CancellationToken cancellationToken = default)
    {
        await ResolveAsync(stub, store, cancellationToken: cancellationToken);

        using var connection = Connect(stub);
        var install = _tree.Install();
        var set = store.SyncSets.Find("snes")!;
        var members = store.SyncSets.Members(set.Id);

        var plan = new ContentPlanner(install, store).Plan(set, members);
        var content = await new ContentSync(install, store, connection)
            .ApplyAsync(plan, cancellationToken: cancellationToken);

        var romIds = members.Select(member => member.RomId).ToList();
        var media = await new MediaSync(install, store, connection)
            .ApplyAsync(romIds, cancellationToken: cancellationToken);

        var gamelists = await new GamelistSync(install, store)
            .ApplyAsync(["snes"], emulationStation: null, cancellationToken);

        return new SyncOutcome(content, media, gamelists);
    }
}
