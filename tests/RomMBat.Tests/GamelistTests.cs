using System.Text;
using RomM.Client.Catalog;
using RomM.Client.Content;
using RomMBat.Core.Content;
using RomMBat.Core.Metadata;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Writing <c>gamelist.xml</c> into a folder another process owns.
/// </summary>
/// <remarks>
/// The fixture is four entries lifted verbatim from a real scraped install, because the
/// question these tests exist to answer is what happens to metadata RomMBat did not write,
/// and a synthesized file would only ever contain what this code already expects.
/// </remarks>
public sealed class GamelistTests : IDisposable
{
    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();
    private readonly LocalStore _store;

    public GamelistTests() => _store = LocalStore.Open(_tree.Install());

    public void Dispose()
    {
        _store.Dispose();
        _tree.Dispose();
        GC.SuppressFinalize(this);
    }

    // ------------------------------------------------------------------ the merge

    [Fact]
    public void A_real_gamelist_round_trips_with_every_field_RomMBat_does_not_own_intact()
    {
        var path = CopyFixture("gamegear");
        var before = new System.Xml.XmlDocument();
        before.Load(path);

        var document = GamelistDocument.Load(path);

        // The entry RomMBat would rewrite: a game it has, with new metadata.
        document.Apply(new GamelistEntry(
            "./Alien Syndrome (Europe).zip",
            [
                new("name", "Alien Syndrome"),
                new("desc", "A description RomMBat fetched."),
                new("developer", "Sims, SEGA"),
            ]));

        document.WriteIfChanged(path);

        var after = new System.Xml.XmlDocument();
        after.Load(path);

        var entry = after.SelectSingleNode("/gameList/game[path='./Alien Syndrome (Europe).zip']")!;

        // What ES wrote, which no sync may touch.
        Assert.Equal("1", entry.SelectSingleNode("playcount")!.InnerText);
        Assert.Equal("20260611T192546", entry.SelectSingleNode("lastplayed")!.InnerText);
        Assert.Equal("22", entry.SelectSingleNode("gametime")!.InnerText);

        // What the user's own scraper wrote.
        Assert.Equal("6c29fc1bae1051774e9a83098f55bd4e", entry.SelectSingleNode("md5")!.InnerText);
        Assert.Equal("6C29FC1BAE1051774E9A83098F55BD4E", entry.SelectSingleNode("cheevosHash")!.InnerText);
        Assert.Equal("ScreenScraper", entry.SelectSingleNode("scrap")!.Attributes!["name"]!.Value);
        Assert.Equal("20260611T192423", entry.SelectSingleNode("scrap")!.Attributes!["date"]!.Value);
        Assert.Equal("12932", entry.Attributes!["id"]!.Value);

        // And what RomMBat does own, which did change.
        Assert.Equal("A description RomMBat fetched.", entry.SelectSingleNode("desc")!.InnerText);
        Assert.Equal("Sims, SEGA", entry.SelectSingleNode("developer")!.InnerText);

        // Every other entry is exactly as it was, including the one carrying cheevosId.
        Assert.Equal(before.SelectNodes("/gameList/game")!.Count, after.SelectNodes("/gameList/game")!.Count);
        Assert.Equal(
            before.SelectSingleNode("/gameList/game[path='./Batman Returns (World).zip']")!.OuterXml,
            after.SelectSingleNode("/gameList/game[path='./Batman Returns (World).zip']")!.OuterXml);
    }

    [Fact]
    public void An_entry_RomMBat_never_wrote_survives_a_field_it_owns_being_cleared()
    {
        var path = CopyFixture("gamegear");
        var document = GamelistDocument.Load(path);

        // A publisher is a field RomMBat writes null for, always: companies cannot separate
        // the roles. Clearing it on an entry the user scraped would delete their data.
        document.Apply(new GamelistEntry("./Baku Baku (USA).zip", [new("name", "Baku Baku Animal")]));
        document.WriteIfChanged(path);

        var after = new System.Xml.XmlDocument();
        after.Load(path);
        var entry = after.SelectSingleNode("/gameList/game[path='./Baku Baku (USA).zip']")!;

        Assert.Equal("Baku Baku Animal", entry.SelectSingleNode("name")!.InnerText);
        Assert.NotNull(entry.SelectSingleNode("publisher"));
        Assert.NotNull(entry.SelectSingleNode("developer"));
    }

    [Fact]
    public void A_null_field_removes_the_element_rather_than_writing_an_empty_one()
    {
        var document = GamelistDocument.Empty();
        document.Apply(new GamelistEntry("./Game.zip", [new("name", "Game"), new("rating", "0.80")]));
        Assert.Contains("<rating>0.80</rating>", document.Render(), StringComparison.Ordinal);

        document.Apply(new GamelistEntry("./Game.zip", [new("name", "Game"), new("rating", null)]));
        Assert.DoesNotContain("rating", document.Render(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_gamelist_that_cannot_be_parsed_is_left_alone_rather_than_overwritten()
    {
        var path = Path.Combine(_tree.Root, "roms", "gamegear", "gamelist.xml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "<gameList><game><path>./x.zip</path>");

        var thrown = Assert.Throws<GamelistParseException>(() => GamelistDocument.Load(path));
        Assert.Equal(path, thrown.Path);

        // The whole point: the bytes are still there.
        Assert.Equal("<gameList><game><path>./x.zip</path>", File.ReadAllText(path));
    }

    [Fact]
    public void A_file_whose_root_is_not_a_gameList_is_refused()
    {
        var path = Path.Combine(_tree.Root, "roms", "gamegear", "gamelist.xml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "<systemList><system /></systemList>");

        Assert.Throws<GamelistParseException>(() => GamelistDocument.Load(path));
    }

    [Fact]
    public void Entry_paths_match_however_the_other_writer_spelled_them()
    {
        var document = GamelistDocument.Empty();
        document.Apply(new GamelistEntry("Game.zip", [new("name", "One")]));
        document.Apply(new GamelistEntry("./game.ZIP", [new("name", "Two")]));

        // One entry, not two: Windows paths are case-insensitive and the './' is decoration.
        Assert.Equal(1, document.Count);
        Assert.Contains("<name>Two</name>", document.Render(), StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ XML under real data

    [Theory]
    [InlineData("Ampersand & Sons")]
    [InlineData("Tag <b>bold</b> and \"quotes\"")]
    [InlineData("Pokémon テスト")]
    [InlineData("Astral 🎮 plane")]
    public void Real_scraped_text_survives_a_round_trip(string value)
    {
        var document = GamelistDocument.Empty();
        document.Apply(new GamelistEntry("./Game.zip", [new("name", value), new("desc", value)]));

        var rendered = document.Render();
        var parsed = new System.Xml.XmlDocument();
        parsed.LoadXml(rendered);

        Assert.Equal(value, parsed.SelectSingleNode("/gameList/game/name")!.InnerText);
    }

    [Fact]
    public void A_control_character_is_dropped_rather_than_losing_the_whole_system()
    {
        // Built from code points rather than written as literals, so they survive this file
        // being edited. XmlWriter throws on either, which would take the folder's whole
        // gamelist rather than one description.
        var description = "Before" + (char)0x01 + (char)0x08 + "after";

        var document = GamelistDocument.Empty();
        document.Apply(new GamelistEntry(
            "./Game.zip",
            [new("desc", description), new("name", "Game")]));

        var rendered = document.Render();
        var parsed = new System.Xml.XmlDocument();
        parsed.LoadXml(rendered);

        Assert.Equal("Beforeafter", parsed.SelectSingleNode("/gameList/game/desc")!.InnerText);
    }

    [Fact]
    public void A_lone_surrogate_is_dropped_rather_than_producing_invalid_utf8()
    {
        var document = GamelistDocument.Empty();
        document.Apply(new GamelistEntry("./Game.zip", [new("name", "Broken\ud83cGame")]));

        var rendered = document.Render();
        var parsed = new System.Xml.XmlDocument();
        parsed.LoadXml(rendered);

        Assert.Equal("BrokenGame", parsed.SelectSingleNode("/gameList/game/name")!.InnerText);
    }

    [Fact]
    public void A_carriage_return_becomes_a_newline_so_a_round_trip_does_not_churn()
    {
        var document = GamelistDocument.Empty();
        document.Apply(new GamelistEntry("./Game.zip", [new("desc", "One\r\nTwo\rThree")]));

        var first = document.Render();

        // What an XML parser hands back has already had its line endings normalised, so a
        // value written with CRLF would differ from the value read back and rewrite forever.
        var reloaded = GamelistDocument.Empty();
        reloaded.Apply(new GamelistEntry("./Game.zip", [new("desc", "One\nTwo\nThree")]));

        Assert.Equal(reloaded.Render(), first);
    }

    [Fact]
    public void The_output_is_written_without_a_byte_order_mark_and_with_LF_endings()
    {
        var path = Path.Combine(_tree.Root, "roms", "gamegear", "gamelist.xml");
        var document = GamelistDocument.Empty();
        document.Apply(new GamelistEntry("./Game.zip", [new("name", "Game")]));
        document.WriteIfChanged(path);

        var bytes = File.ReadAllBytes(path);
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.DoesNotContain("\r\n", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
        Assert.StartsWith("<?xml version=\"1.0\"?>", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ the no-churn rule

    [Fact]
    public void Reloading_a_file_this_class_wrote_renders_the_same_bytes()
    {
        // The property everything else rests on. It fails the moment the reader hands the
        // document its own indentation as text nodes: the writer then sees mixed content,
        // stops indenting, and every second run rewrites the file with no change in it.
        var path = CopyFixture("gamegear");

        var first = GamelistDocument.Load(path);
        first.WriteIfChanged(path);
        var written = File.ReadAllText(path);

        var reloaded = GamelistDocument.Load(path);

        Assert.Equal(written, reloaded.Render());
    }

    [Fact]
    public void Writing_the_same_content_twice_writes_no_bytes_the_second_time()
    {
        var path = CopyFixture("gamegear");

        var first = GamelistDocument.Load(path);
        first.Apply(new GamelistEntry("./Alien Syndrome (Europe).zip", [new("name", "Alien Syndrome")]));
        first.WriteIfChanged(path);

        var stamp = File.GetLastWriteTimeUtc(path);
        var bytes = File.ReadAllBytes(path);

        var second = GamelistDocument.Load(path);
        second.Apply(new GamelistEntry("./Alien Syndrome (Europe).zip", [new("name", "Alien Syndrome")]));

        Assert.False(second.WriteIfChanged(path));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(path));
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void A_second_sync_of_an_unchanged_folder_produces_a_byte_identical_gamelist()
    {
        Populate("gamegear", (10, "Sonic Chaos (USA).zip"), (11, "Shinobi (World).zip"));

        var sync = new GamelistSync(_tree.Install(), _store);
        var path = Path.Combine(_tree.Root, "roms", "gamegear", "gamelist.xml");

        var first = sync.Write("gamegear");
        Assert.True(first.Wrote);
        var bytes = File.ReadAllBytes(path);

        var second = sync.Write("gamegear");

        Assert.False(second.Wrote);
        Assert.Equal(0, second.Added);
        Assert.Equal(0, second.Updated);
        Assert.Equal(0, second.Removed);
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Fact]
    public async Task A_no_op_pass_says_so_rather_than_reporting_work()
    {
        Populate("gamegear", (10, "Sonic Chaos (USA).zip"));

        var sync = new GamelistSync(_tree.Install(), _store);
        sync.Write("gamegear");

        var outcome = await sync.ApplyAsync(["gamegear"], cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(outcome.IsNoOp);
        Assert.Contains("unchanged", outcome.Summary, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ folder grouping

    [Fact]
    public async Task Two_platforms_sharing_one_folder_produce_one_merged_gamelist()
    {
        // snes and sfam both resolve to roms/snes, which is the case that would have the
        // second write clobber the first if generation were keyed by platform.
        Populate("snes", (1, "Super Mario World (USA).sfc"));
        Populate("snes", (2, "Super Mario World (Japan).sfc"));

        var sync = new GamelistSync(_tree.Install(), _store);
        var outcome = await sync.ApplyAsync(cancellationToken: TestContext.Current.CancellationToken);

        var folder = Assert.Single(outcome.Folders);
        Assert.Equal("snes", folder.Folder);
        Assert.Equal(2, folder.Entries);

        var written = new System.Xml.XmlDocument();
        written.Load(Path.Combine(_tree.Root, "roms", "snes", "gamelist.xml"));

        Assert.Equal(2, written.SelectNodes("/gameList/game")!.Count);
        Assert.NotNull(written.SelectSingleNode("/gameList/game[path='./Super Mario World (USA).sfc']"));
        Assert.NotNull(written.SelectSingleNode("/gameList/game[path='./Super Mario World (Japan).sfc']"));
    }

    // ------------------------------------------------------------------ offline, and eviction

    [Fact]
    public async Task A_gamelist_is_written_from_local_state_with_no_connection_at_all()
    {
        Populate("gamegear", (10, "Sonic Chaos (USA).zip"));

        // No RomMConnection is constructed anywhere in this test, and nothing here can reach a
        // network: everything written comes out of the store.
        var sync = new GamelistSync(_tree.Install(), _store);
        var outcome = await sync.ApplyAsync(
            ["gamegear"],
            emulationStation: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(outcome.Folders.Single().Wrote);
        Assert.Equal(EsCallResult.NotRunning, outcome.Reload);

        var written = File.ReadAllText(Path.Combine(_tree.Root, "roms", "gamegear", "gamelist.xml"));
        Assert.Contains("<name>Sonic Chaos</name>", written, StringComparison.Ordinal);
        Assert.Contains("<developer>Sega, Aspect</developer>", written, StringComparison.Ordinal);
    }

    [Fact]
    public void An_entry_RomMBat_owns_is_removed_once_its_file_is_gone_and_a_foreign_one_is_not()
    {
        Populate("gamegear", (10, "Sonic Chaos (USA).zip"), (11, "Shinobi (World).zip"));

        var sync = new GamelistSync(_tree.Install(), _store);
        sync.Write("gamegear");

        // Add an entry nothing in the store knows about, as the user's own scraper would.
        var path = Path.Combine(_tree.Root, "roms", "gamegear", "gamelist.xml");
        var document = GamelistDocument.Load(path);
        document.Apply(new GamelistEntry("./A Game The User Scraped.zip", [new("name", "Theirs")]));
        document.WriteIfChanged(path);

        // Now evict one of ours: the file and its row go, the metadata row stays.
        File.Delete(Path.Combine(_tree.Root, "roms", "gamegear", "Shinobi (World).zip"));
        _store.Files.Remove(RelativePath.Create("roms/gamegear/Shinobi (World).zip"));

        var result = sync.Write("gamegear");

        Assert.Equal(1, result.Removed);
        Assert.Equal(1, result.Foreign);

        var after = new System.Xml.XmlDocument();
        after.Load(path);

        Assert.Null(after.SelectSingleNode("/gameList/game[path='./Shinobi (World).zip']"));
        Assert.NotNull(after.SelectSingleNode("/gameList/game[path='./A Game The User Scraped.zip']"));
        Assert.NotNull(after.SelectSingleNode("/gameList/game[path='./Sonic Chaos (USA).zip']"));
    }

    [Fact]
    public void An_entry_spelled_without_its_prefix_is_ours_in_the_count_as_well_as_in_the_merge()
    {
        Populate("gamegear", (10, "Sonic Chaos (USA).zip"));

        // The shape Entry_paths_match_however_the_other_writer_spelled_them establishes, now
        // on disk: the same ROM, named without the "./" RomMBat writes.
        var path = Path.Combine(_tree.Root, "roms", "gamegear", "gamelist.xml");
        var seed = GamelistDocument.Empty();
        seed.Apply(new GamelistEntry("Sonic Chaos (USA).zip", [new("name", "Theirs")]));
        seed.WriteIfChanged(path);

        var result = new GamelistSync(_tree.Install(), _store).Write("gamegear");

        // Updated, not added, and not then reported back to the user as somebody else's.
        Assert.Equal(1, result.Entries);
        Assert.Equal(1, result.Updated);
        Assert.Equal(0, result.Added);
        Assert.Equal(0, result.Foreign);
    }

    // ------------------------------------------------------------------ the threshold

    [Fact]
    public async Task A_folder_past_the_threshold_is_reported_and_nothing_is_left_out()
    {
        for (var index = 0; index < 12; index++)
        {
            Populate("gamegear", (100 + index, $"Game {index:D3} (USA).zip"));
        }

        _store.Settings.Set(GamelistSync.WarnEntriesSetting, 10L, DateTimeOffset.UtcNow);

        var sync = new GamelistSync(_tree.Install(), _store);
        var outcome = await sync.ApplyAsync(["gamegear"], cancellationToken: TestContext.Current.CancellationToken);

        // Every game is still written. The threshold reports; it does not truncate, because
        // EmulationStation lists the rom files whether or not they have an entry.
        Assert.Equal(12, outcome.Folders.Single().Entries);

        var warning = Assert.Single(outcome.Warnings);
        Assert.Contains("gamegear", warning, StringComparison.Ordinal);
        Assert.Contains("Nothing was left out", warning, StringComparison.Ordinal);
        Assert.Contains("game cap", warning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_threshold_of_zero_says_nothing()
    {
        Populate("gamegear", (10, "Sonic Chaos (USA).zip"));
        _store.Settings.Set(GamelistSync.WarnEntriesSetting, 0L, DateTimeOffset.UtcNow);

        var outcome = await new GamelistSync(_tree.Install(), _store).ApplyAsync(
            ["gamegear"],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(outcome.Warnings);
    }

    // ------------------------------------------------------------------ media references

    [Fact]
    public void Media_on_disk_becomes_a_relative_reference_in_the_entry()
    {
        Populate("gamegear", (10, "Sonic Chaos (USA).zip"));
        RecordMedia(10, "gamegear", MediaKind.Image, "Sonic Chaos (USA)-image.png");
        RecordMedia(10, "gamegear", MediaKind.Video, "Sonic Chaos (USA)-video.mp4");

        new GamelistSync(_tree.Install(), _store).Write("gamegear");

        var written = File.ReadAllText(Path.Combine(_tree.Root, "roms", "gamegear", "gamelist.xml"));

        // Relative, always. An absolute path survives a drive-letter change and points at
        // nothing afterwards.
        Assert.Contains("<image>./images/Sonic Chaos (USA)-image.png</image>", written, StringComparison.Ordinal);
        Assert.Contains("<video>./videos/Sonic Chaos (USA)-video.mp4</video>", written, StringComparison.Ordinal);
        Assert.DoesNotContain(_tree.Root, written, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Media_that_is_not_on_disk_is_not_referenced()
    {
        Populate("gamegear", (10, "Sonic Chaos (USA).zip"));

        new GamelistSync(_tree.Install(), _store).Write("gamegear");

        var written = File.ReadAllText(Path.Combine(_tree.Root, "roms", "gamegear", "gamelist.xml"));

        // The metadata knows where the cover lives on the server. Until the bytes are here,
        // referencing it would be a gamelist pointing at nothing.
        Assert.DoesNotContain("<image>", written, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ helpers

    private string CopyFixture(string folder)
    {
        var directory = Path.Combine(_tree.Root, "roms", folder);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "gamelist.xml");
        File.Copy(Fixtures.GamegearGamelist, path, overwrite: true);
        return path;
    }

    /// <summary>Puts ROM files and their metadata in the store, as a completed sync would.</summary>
    private void Populate(string folder, params (int RomId, string FileName)[] games)
    {
        var directory = Path.Combine(_tree.Root, "roms", folder);
        Directory.CreateDirectory(directory);

        foreach (var (romId, fileName) in games)
        {
            File.WriteAllBytes(Path.Combine(directory, fileName), new byte[64]);

            _store.Files.Record(new LocalFile
            {
                Path = RelativePath.Create($"roms/{folder}/{fileName}"),
                Folder = folder,
                RomId = romId,
                Kind = LocalFileKind.Rom,
                FileName = fileName,
                SizeBytes = 64,
                VerifiedBy = VerifiedBy.Size,
            });

            _store.Metadata.Record(GameMetadata.From(
                new RomRow
                {
                    Id = romId,
                    FsName = fileName,
                    Name = MediaNaming.StemOf(fileName).Split(" (")[0],
                    Summary = "A game.",
                    Regions = ["USA"],
                    Languages = ["English"],
                    Metadata = new RomMetadata
                    {
                        Companies = ["Sega", "Aspect"],
                        Genres = ["Platform"],
                        PlayerCount = "1",
                        AverageRating = 82.5,
                        FirstReleaseDate = 748_137_600_000,
                    },
                    CoverLargePath = "/assets/romm/resources/roms/1/2/cover/big.png",
                },
                folder,
                DateTimeOffset.UnixEpoch));
        }
    }

    private void RecordMedia(int romId, string folder, MediaKind kind, string fileName)
    {
        var relative = $"roms/{folder}/{MediaNaming.FolderFor(kind)}/{fileName}";
        var absolute = Path.Combine(_tree.Root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllBytes(absolute, new byte[16]);

        _store.Files.Record(new LocalFile
        {
            Path = RelativePath.Create(relative),
            Folder = folder,
            RomId = romId,
            Kind = MediaSync.ToFileKind(kind),
            FileName = fileName,
            SizeBytes = 16,
            VerifiedBy = VerifiedBy.Size,
        });
    }
}
