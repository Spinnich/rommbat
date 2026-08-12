using System.Net;
using RomM.Client;
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
/// The conversions between RomM's metadata and what a gamelist wants, and the media paths.
/// </summary>
/// <remarks>
/// Every value here is a measured one. RomM and EmulationStation agree on almost nothing
/// about units, scale, vocabulary or shape, and each disagreement produces a value that is
/// written without error and read as a different fact.
/// </remarks>
public sealed class MetadataAndMediaTests
{
    // ------------------------------------------------------------------ units and scales

    [Fact]
    public void The_release_date_is_read_as_milliseconds_because_that_is_what_it_is()
    {
        // Star Wars: Knights of the Old Republic, 2003-07-15, as the live instance returns it.
        Assert.Equal("20030715T000000", GameMetadata.ReleaseDateOf(1_058_227_200_000));

        // The same number read as seconds is the year 35528, which is what makes this the
        // kind of bug that ships: it produces a date, just not the right one.
        Assert.NotEqual("20030715T000000", GameMetadata.ReleaseDateOf(1_058_227_200));
    }

    [Fact]
    public void A_release_date_outside_the_representable_range_loses_the_field_not_the_entry()
    {
        Assert.Null(GameMetadata.ReleaseDateOf(long.MaxValue));
        Assert.Null(GameMetadata.ReleaseDateOf(null));
    }

    [Theory]
    [InlineData(93.45, "0.93")]
    [InlineData(100.0, "1.00")]
    [InlineData(5.0, "0.05")]
    [InlineData(0.0, "0.00")]
    public void The_rating_is_divided_by_a_hundred(double outOfHundred, string expected) =>
        Assert.Equal(expected, GameMetadata.RatingOf(outOfHundred));

    [Fact]
    public void A_rating_above_the_scale_is_clamped_rather_than_written_out_of_range()
    {
        Assert.Equal("1.00", GameMetadata.RatingOf(140));
        Assert.Equal("0.00", GameMetadata.RatingOf(-5));
        Assert.Null(GameMetadata.RatingOf(double.NaN));
    }

    // ------------------------------------------------------------------ vocabularies

    [Theory]
    [InlineData("USA", "us")]
    [InlineData("Japan", "jp")]
    [InlineData("Europe", "eu")]
    [InlineData("World", "wr")]
    [InlineData("Brazil", "br")]
    public void RomM_region_names_become_the_codes_a_real_install_writes(string name, string code) =>
        Assert.Equal(code, EsVocabulary.Region([name]));

    [Fact]
    public void An_unmapped_region_falls_through_to_the_next_one_rather_than_being_guessed()
    {
        // No code was observed for Asia in a real install, and region codes follow no standard
        // that could be extrapolated, so inventing one would assert something unchecked.
        Assert.Equal("us", EsVocabulary.Region(["Asia", "USA"]));
        Assert.Null(EsVocabulary.Region(["Asia"]));
        Assert.Null(EsVocabulary.Region([]));
    }

    [Fact]
    public void Languages_are_comma_joined_in_the_form_a_real_install_writes()
    {
        Assert.Equal("de,en,es,fr,it", EsVocabulary.LanguageList(
            ["German", "English", "Spanish", "French", "Italian"]));

        // Japanese and Korean depart from ISO 639-1, which is what a real install shows.
        Assert.Equal("jp,kr", EsVocabulary.LanguageList(["Japanese", "Korean"]));
        Assert.Null(EsVocabulary.LanguageList([]));
    }

    // ------------------------------------------------------------------ companies

    [Fact]
    public void Companies_go_into_developer_and_nothing_goes_into_publisher()
    {
        var metadata = GameMetadata.From(
            new RomRow
            {
                Id = 1,
                FsName = "Star Wars - Knights of the Old Republic (USA).iso",
                Name = "Star Wars: Knights of the Old Republic",
                Metadata = new RomMetadata
                {
                    // Exactly as the live instance returns it: alphabetical, both roles merged.
                    Companies = ["Activision", "Aspyr Media", "BioWare", "LucasArts"],
                },
            },
            "xbox",
            DateTimeOffset.UnixEpoch);

        Assert.Equal("Activision, Aspyr Media, BioWare, LucasArts", metadata.Developer);
    }

    [Fact]
    public void A_company_repeated_by_the_server_is_written_once()
    {
        // Chrono Trigger arrives as ["Squaresoft", "Squaresoft"], the same company as both
        // developer and publisher, flattened.
        var metadata = GameMetadata.From(
            new RomRow
            {
                Id = 2,
                FsName = "Chrono Trigger (USA).sfc",
                Metadata = new RomMetadata { Companies = ["Squaresoft", "Squaresoft"] },
            },
            "snes",
            DateTimeOffset.UnixEpoch);

        Assert.Equal("Squaresoft", metadata.Developer);
    }

    [Fact]
    public void Genres_are_joined_and_a_repeated_franchise_is_deduped()
    {
        var metadata = GameMetadata.From(
            new RomRow
            {
                Id = 3,
                FsName = "Game.sfc",
                Metadata = new RomMetadata
                {
                    // Joining is the convention rather than a departure from it: 2,079 of one
                    // real install's 4,440 genre values already contain a comma or a slash.
                    Genres = ["Racing", "Driving"],
                    Franchises = ["Star Wars", "Star Wars"],
                },
            },
            "snes",
            DateTimeOffset.UnixEpoch);

        Assert.Equal("Racing, Driving", metadata.Genre);
        Assert.Equal("Star Wars", metadata.Family);
    }

    [Fact]
    public void The_player_count_is_copied_because_it_is_already_the_right_shape()
    {
        var metadata = GameMetadata.From(
            new RomRow { Id = 4, FsName = "Game.sfc", Metadata = new RomMetadata { PlayerCount = "1-2" } },
            "snes",
            DateTimeOffset.UnixEpoch);

        Assert.Equal("1-2", metadata.Players);
    }

    [Fact]
    public void A_row_with_no_metadata_at_all_still_produces_a_name()
    {
        var metadata = GameMetadata.From(
            new RomRow { Id = 5, FsName = "Unknown Game (USA).sfc" },
            "snes",
            DateTimeOffset.UnixEpoch);

        Assert.Equal("Unknown Game (USA).sfc", metadata.Name);
        Assert.True(metadata.IsBare);
    }

    // ------------------------------------------------------------------ media paths

    [Fact]
    public void Both_shapes_of_media_path_normalise_onto_the_asset_prefix_exactly_once()
    {
        // A cover arrives already rooted at the prefix, and carries a ?ts= query holding a
        // raw space.
        Assert.Equal(
            "/assets/romm/resources/roms/20/1393/cover/big.png",
            MediaResource.Normalize("/assets/romm/resources/roms/20/1393/cover/big.png?ts=2026-07-21 18:07:17"));

        // A manual, a video and a logo arrive relative to it.
        Assert.Equal(
            "/assets/romm/resources/roms/20/1393/manual/1393.pdf",
            MediaResource.Normalize("roms/20/1393/manual/1393.pdf"));
    }

    [Fact]
    public void The_request_path_is_encoded_a_segment_at_a_time()
    {
        var resource = new MediaResource
        {
            Kind = MediaKind.Image,
            SourcePath = "roms/1/2/cover/big name.png",
        };

        Assert.Equal("/assets/romm/resources/roms/1/2/cover/big%20name.png", resource.RequestPath);
    }

    [Theory]
    [InlineData("/assets/romm/resources/roms/20/1393/cover/big.png?ts=2026-07-21 18:07:17", ".png")]
    [InlineData("roms/20/1393/video/video.mp4", ".mp4")]
    [InlineData("roms/20/1393/manual/1393.pdf", ".pdf")]
    [InlineData("roms/20/1393/cover/noextension", "")]
    public void The_extension_comes_off_the_source_path(string path, string expected) =>
        Assert.Equal(expected, new MediaResource { Kind = MediaKind.Image, SourcePath = path }.Extension);

    [Fact]
    public async Task A_media_response_that_is_a_web_page_is_refused_rather_than_written()
    {
        // The measured trap: a media path requested without the asset prefix answers 200,
        // with an ETag and Accept-Ranges, and 5,826 bytes of the web UI's index.html. Status
        // alone would write that to disk as a PDF.
        using var stub = new StubHandler(request =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<!doctype html>\n<html></html>"),
            };

            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");
            return response;
        });

        using var connection = Connect(stub);
        using var destination = new MemoryStream();

        var result = await connection.DownloadMediaAsync(
            new MediaResource { Kind = MediaKind.Manual, SourcePath = "roms/1/2/manual/2.pdf" },
            destination);

        Assert.False(result.IsSuccess);
        Assert.Contains("web page", result.Message, StringComparison.Ordinal);
        Assert.Equal(0, destination.Length);
    }

    [Fact]
    public async Task A_media_file_larger_than_the_room_left_is_refused_before_its_body_is_read()
    {
        using var stub = new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[4096]),
            };

            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            return response;
        });

        using var connection = Connect(stub);
        using var destination = new MemoryStream();

        var result = await connection.DownloadMediaAsync(
            new MediaResource { Kind = MediaKind.Image, SourcePath = "roms/1/2/cover/big.png" },
            destination,
            maximumBytes: 1024);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, destination.Length);
    }

    [Fact]
    public async Task A_media_file_inside_the_budget_is_written_whole()
    {
        using var stub = new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[2048]),
            };

            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            return response;
        });

        using var connection = Connect(stub);
        using var destination = new MemoryStream();

        var result = await connection.DownloadMediaAsync(
            new MediaResource { Kind = MediaKind.Image, SourcePath = "roms/1/2/cover/big.png" },
            destination,
            maximumBytes: 4096);

        Assert.True(result.IsSuccess);
        Assert.Equal(2048, destination.Length);
        Assert.Equal("image/png", result.Value!.ContentType);
    }

    // ------------------------------------------------------------------ names we construct

    [Fact]
    public void Media_is_named_the_way_RetroBats_own_scraper_names_it()
    {
        const string rom = "Sonic Chaos (USA, Europe).zip";

        Assert.Equal("Sonic Chaos (USA, Europe)-image.png", MediaNaming.FileNameFor(rom, MediaKind.Image, ".png"));
        Assert.Equal("Sonic Chaos (USA, Europe)-thumb.png", MediaNaming.FileNameFor(rom, MediaKind.Thumbnail, ".png"));
        Assert.Equal("Sonic Chaos (USA, Europe)-marquee.png", MediaNaming.FileNameFor(rom, MediaKind.Marquee, ".png"));
        Assert.Equal("Sonic Chaos (USA, Europe)-video.mp4", MediaNaming.FileNameFor(rom, MediaKind.Video, ".mp4"));
        Assert.Equal("Sonic Chaos (USA, Europe)-manual.pdf", MediaNaming.FileNameFor(rom, MediaKind.Manual, ".pdf"));

        // The marquee lives under images/, not a folder of its own.
        Assert.Equal("images", MediaNaming.FolderFor(MediaKind.Marquee));
        Assert.Equal("videos", MediaNaming.FolderFor(MediaKind.Video));
        Assert.Equal("manuals", MediaNaming.FolderFor(MediaKind.Manual));
    }

    [Fact]
    public void Only_the_last_extension_comes_off_the_stem()
    {
        // A real library holds this exact shape, and a scraper's stem for it keeps the .xiso.
        Assert.Equal(
            "Star Wars - Knights of the Old Republic (USA) (Rev 1).xiso",
            MediaNaming.StemOf("Star Wars - Knights of the Old Republic (USA) (Rev 1).xiso.iso"));
    }

    [Fact]
    public void A_colon_in_a_name_becomes_an_underscore_rather_than_an_alternate_data_stream()
    {
        // The one forbidden character that does not fail: Windows accepts it and opens an NTFS
        // stream, so the write succeeds, the directory lists a truncated name, and the file the
        // gamelist points at is not there.
        var name = MediaNaming.FileNameFor("Game: The Sequel.zip", MediaKind.Image, ".png");

        Assert.Equal("Game_ The Sequel-image.png", name);
        Assert.DoesNotContain(":", name, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<")]
    [InlineData(">")]
    [InlineData("\"")]
    [InlineData("|")]
    [InlineData("?")]
    [InlineData("*")]
    [InlineData("/")]
    [InlineData("\\")]
    public void Every_character_Windows_refuses_is_replaced(string character)
    {
        var name = MediaNaming.FileNameFor($"Game{character}Name.zip", MediaKind.Image, ".png");

        Assert.Equal("Game_Name-image.png", name);
    }

    [Fact]
    public void A_name_ending_in_a_dot_or_a_space_loses_it_before_Windows_does()
    {
        // Windows strips both silently, so a name written with one is not the name that
        // landed and the gamelist would reference a file that is not there.
        Assert.Equal("Game-image.png", MediaNaming.Safe("Game. ", "-image.png"));
        Assert.Equal("_-image.png", MediaNaming.Safe("   ", "-image.png"));
    }

    [Fact]
    public void A_name_over_the_component_limit_is_truncated_and_stays_unique()
    {
        // 255 is the ceiling, and it is the file name rather than MAX_PATH: 255 writes, 256
        // fails with or without the \\?\ prefix.
        var first = MediaNaming.FileNameFor(new string('a', 400) + "-one.zip", MediaKind.Image, ".png");
        var second = MediaNaming.FileNameFor(new string('a', 400) + "-two.zip", MediaKind.Image, ".png");

        Assert.Equal(MediaNaming.MaximumFileNameLength, first.Length);
        Assert.EndsWith("-image.png", first, StringComparison.Ordinal);

        // Two names that differ only past the cut must not collide, or the second game shows
        // the first one's box art.
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void A_name_inside_the_limit_is_left_exactly_as_it_is()
    {
        const string rom = "Perman 2 - Down with the Secret Madou Society! (Japan) [T-En].zip";
        var name = MediaNaming.FileNameFor(rom, MediaKind.Video, ".mp4");

        Assert.Equal("Perman 2 - Down with the Secret Madou Society! (Japan) [T-En]-video.mp4", name);
    }

    [Fact]
    public void A_media_target_is_relative_to_the_RetroBat_root()
    {
        var target = MediaSync.TargetFor("gamegear", "Sonic Chaos (USA).zip", MediaKind.Image, ".png");

        Assert.Equal("roms/gamegear/images/Sonic Chaos (USA)-image.png", target.Value);
    }

    // ------------------------------------------------------------------ the media policy

    [Fact]
    public void The_default_media_policy_is_covers_marquee_and_video()
    {
        Assert.Equal(
            [MediaKind.Image, MediaKind.Thumbnail, MediaKind.Marquee, MediaKind.Video],
            MediaPolicy.Parse(null));

        // Manuals are the largest kind at a 2.45 MB median and nothing in M4 needs them.
        Assert.DoesNotContain(MediaKind.Manual, MediaPolicy.Parse(null));
    }

    [Theory]
    [InlineData("none", 0)]
    [InlineData("all", 5)]
    [InlineData("image,video", 2)]
    [InlineData("image, thumb", 2)]
    [InlineData("nonsense", 4)]
    public void The_media_policy_reads_what_a_user_would_type(string value, int expected) =>
        Assert.Equal(expected, MediaPolicy.Parse(value).Count);

    private static RomMConnection Connect(StubHandler handler) =>
        new(new RomMClientOptions { Origin = new Uri("http://stub.invalid"), AccessToken = "rmm_test" }, handler);
}

/// <summary>A handler that answers every request from one function.</summary>
internal sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        Task.FromResult(respond(request));
}
