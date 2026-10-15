using System.Text.Json;
using Microsoft.Data.Sqlite;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The bundled firmware manifest, and the rules its shape forces on everything that reads it.
/// </summary>
/// <remarks>
/// The manifest joins RetroBat's <c>batocera-systems.json</c> to retrobios's emulator profiles,
/// and is bundled rather than read from the install because a real RetroBat has no
/// <c>batocera-systems.json</c> anywhere. These tests hold the generated copy to the vendored
/// original, so a regeneration that quietly dropped a system fails here. The counts are the
/// ones <c>reference/verify.py</c> derives.
/// </remarks>
public sealed class BiosManifestTests
{
    [Fact]
    public void Every_entry_RetroBat_lists_is_a_slot_or_reported_with_its_path_and_md5_unchanged()
    {
        var manifest = Fixtures.LoadBiosManifest();
        using var document = JsonDocument.Parse(File.ReadAllText(Fixtures.BatoceraSystemsJson));

        var upstream = document.RootElement
            .EnumerateObject()
            .SelectMany(system => system.Value.GetProperty("biosFiles").EnumerateArray()
                .Select(file => (
                    Folder: Alias(system.Name),
                    Md5: file.GetProperty("md5").GetString()!.Trim().ToLowerInvariant(),
                    Path: file.GetProperty("file").GetString()!)))
            .ToList();

        Assert.Equal(360, upstream.Count);

        // Six land outside bios/ and sixty-three are MAME's software lists, which are hash
        // metadata RetroBat ships and not firmware. Both are reported, never slotted.
        var reported = upstream
            .Where(entry => !BiosManifest.IsInsideBios(entry.Path) || entry.Path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Equal(69, reported.Count);
        Assert.All(reported, entry => Assert.Empty(entry.Md5));
        Assert.All(reported, entry => Assert.Contains(
            manifest.ReportedFor(entry.Folder),
            item => item.Path == entry.Path && item.Readers.Contains(BiosAccepted.RetroBat)));

        var slotted = upstream.Except(reported).ToList();
        Assert.Equal(291, slotted.Count);

        Assert.All(slotted, entry =>
        {
            var slot = Assert.Single(manifest.For(entry.Folder), slot => slot.Path.Value == entry.Path);
            Assert.Contains(BiosAccepted.RetroBat, slot.Readers);
            Assert.Equal(entry.Md5.Length == 0 ? null : entry.Md5, slot.RetroBatMd5);
        });
    }

    [Fact]
    public void The_counts_verify_py_derives_come_out_of_the_bundled_copy()
    {
        var manifest = Fixtures.LoadBiosManifest();
        var slots = manifest.Requirements;

        Assert.Equal(127, manifest.Folders.Count);
        Assert.Equal(943, slots.Count);
        Assert.Empty(manifest.RejectedPaths);

        var retrobat = slots.Where(slot => slot.Readers.Contains(BiosAccepted.RetroBat)).ToList();
        Assert.Equal(296, retrobat.Count);
        Assert.Equal(179, retrobat.Count(slot => slot.RetroBatMd5 is not null));

        // RetroBat's hashless entries a second source identifies: 24 zips by their members and
        // 22 single files by a stated hash, a catalog name or RomM's list.
        Assert.Equal(46, retrobat.Count(slot => slot.RetroBatMd5 is null && !slot.IsUnverifiable));

        Assert.Equal(71, slots.Count(slot => slot.IsUnverifiable));
        Assert.Equal(327, slots.Count(slot => slot.Contents.Count > 0));
        Assert.Equal(4, slots.Count(slot => slot.Accepted.Count > 1));
        Assert.Equal(352, slots.SelectMany(slot => slot.Accepted).Select(accepted => accepted.Md5).Distinct().Count());
        Assert.Equal(615, slots.Select(slot => slot.Path).Distinct().Count());
    }

    [Fact]
    public void Master_System_carries_the_hash_RetroBat_lists_without()
    {
        var manifest = Fixtures.LoadBiosManifest();

        // RetroBat names both [BIOS] files without a hash (#533). The US/EU one is No-Intro's own
        // name for 840481177270d5642a14ca71ee72844c, which BizHawk's database states by sha1, and
        // emulatorlauncher points SMSHawk at RetroBat's name (RB-322).
        var usa = Assert.Single(
            manifest.For("mastersystem"),
            slot => slot.Path.Value == "bios/[BIOS] Sega Master System (USA, Europe) (v1.3).sms");

        var accepted = Assert.Single(usa.Accepted);
        Assert.Equal("840481177270d5642a14ca71ee72844c", accepted.Md5);
        Assert.Equal(["bizhawk", "catalog"], accepted.From);
        Assert.Null(usa.RetroBatMd5);
        Assert.Contains("bizhawk/SMSHawk", usa.Readers);
        Assert.Contains(BiosAccepted.RetroBat, usa.Readers);

        // Genesis Plus GX reads its own names, which RomM's list identifies.
        var gpgx = Assert.Single(manifest.For("mastersystem"), slot => slot.Path.Value == "bios/bios_U.sms");
        Assert.Equal("840481177270d5642a14ca71ee72844c", Assert.Single(gpgx.Accepted).Md5);
        Assert.Equal(["libretro/genesis_plus_gx"], gpgx.Readers);
        Assert.False(gpgx.Required);
    }

    [Fact]
    public void A_ps1_library_gets_every_bios_its_libretro_cores_read_beside_RetroBat_s_one()
    {
        var manifest = Fixtures.LoadBiosManifest();
        var psx = manifest.For("psx");

        var retrobat = Assert.Single(psx, slot => slot.Path.Value == "bios/psxonpsp660.bin");
        Assert.Equal("c53ca5908936d412331790f4426c6c33", retrobat.RetroBatMd5);

        foreach (var scph in new[] { "scph5500.bin", "scph5501.bin", "scph5502.bin" })
        {
            var slot = Assert.Single(psx, slot => slot.Path.Value == $"bios/{scph}");
            Assert.Single(slot.Accepted);
            Assert.Contains("libretro/swanstation", slot.Readers);
        }

        // DuckStation reads the same names, but no fact cites where RetroBat points it, so its
        // files are reported rather than given slots of their own.
        Assert.Contains(manifest.ReportedFor("psx"), item => item.Why.Contains("duckstation has no cited firmware directory", StringComparison.Ordinal));
    }

    [Fact]
    public void Gb_takes_the_super_game_boy_files_and_the_color_boot_rom_from_its_siblings()
    {
        var manifest = Fixtures.LoadBiosManifest();

        var gb = manifest.For("gb");
        var supplemented = gb.Where(slot => slot.Supplement is not null).ToList();

        // RetroBat's list names only the boot ROM for gb. libretro/bsnes runs a .gb as a Super
        // Game Boy cartridge and fails to load one without SGB1.sfc, and bizhawk/GBHawk runs a
        // Color-flagged cartridge in Color mode and refuses it without the Color boot ROM. Each
        // is copied onto gb with the md5 RetroBat gives it on its own system.
        Assert.Equal(
            ["bios/gbc_bios.bin", "bios/SGB1.sfc", "bios/SGB2.sfc", "bios/sgb2_boot.bin", "bios/sgb_boot.bin"],
            supplemented.Select(slot => slot.Path.Value).Order(StringComparer.OrdinalIgnoreCase));
        Assert.All(supplemented.Where(slot => slot.FileName.StartsWith("sgb", StringComparison.OrdinalIgnoreCase)), slot =>
            Assert.Contains("bsnes", slot.Supplement, StringComparison.Ordinal));
        Assert.Contains("GBHawk", Assert.Single(supplemented, slot => slot.FileName == "gbc_bios.bin").Supplement, StringComparison.Ordinal);

        var siblings = manifest.For("sgb").Concat(manifest.For("gbc"))
            .Where(slot => slot.Readers.Contains(BiosAccepted.RetroBat))
            .ToDictionary(slot => slot.Path);
        Assert.All(supplemented, slot => Assert.Equal(siblings[slot.Path].RetroBatMd5, slot.RetroBatMd5));

        // Supplemented only where measured: no other system takes another's entries.
        Assert.Equal(5, manifest.Requirements.Count(slot => slot.Supplement is not null));
    }

    [Fact]
    public void A_slot_no_source_identifies_is_unverifiable_rather_than_missing()
    {
        var manifest = Fixtures.LoadBiosManifest();

        // Reporting these as "not in your library" would send a user looking for a file RomMBat
        // could not recognize if they already had it.
        var none = manifest.Requirements.Where(slot => slot.IsUnverifiable).ToList();

        Assert.Equal(71, none.Count);
        Assert.All(none, slot => Assert.Empty(slot.Accepted));
        Assert.All(none, slot => Assert.Empty(slot.Contents));

        // Every one is RetroBat's: a profile's file with no identity is reported, never slotted.
        Assert.All(none, slot => Assert.Contains(BiosAccepted.RetroBat, slot.Readers));
    }

    [Fact]
    public void A_zip_is_identified_by_its_members_and_never_by_a_container_md5_a_profile_states()
    {
        var manifest = Fixtures.LoadBiosManifest();

        // RetroBat lists adam.zip with no hash. MAME's profile lists the seven members it checks.
        var adam = Assert.Single(manifest.For("adam"), slot => slot.Path.Value == "bios/adam.zip");

        Assert.Empty(adam.Accepted);
        var contents = Assert.Single(adam.Contents);
        Assert.Contains("libretro/mame", contents.Readers);
        Assert.Equal(7, contents.Files.Count);
        Assert.All(contents.Files, member => Assert.NotEqual(0u, member.Crc32));
    }

    [Fact]
    public void Nothing_the_manifest_slots_lands_outside_bios()
    {
        var manifest = Fixtures.LoadBiosManifest();

        Assert.All(
            manifest.Requirements,
            slot => Assert.StartsWith("bios/", slot.Path.Value, StringComparison.Ordinal));
        Assert.All(
            manifest.Requirements.SelectMany(slot => slot.Aliases),
            alias => Assert.StartsWith("bios/", alias.Value, StringComparison.Ordinal));

        // Mesen's own directory is outside bios/ (RB-317), so what it reads is reported.
        Assert.Contains(
            manifest.ReportedFor("snes"),
            item => item.Path.StartsWith("emulators/mesen/Firmware/", StringComparison.Ordinal));
    }

    [Fact]
    public void A_hashed_slot_outside_bios_is_refused_rather_than_written()
    {
        // Synthesized rather than waiting for the generator to do it: the rule only earns its
        // keep on the day the manifest changes.
        var manifest = BiosManifest.Parse(Json("""
            {
              "_schema": 2,
              "systems": {
                "camplynx": {
                  "slots": [
                    { "path": "emulators/jynx/lynx48-1.rom", "accepted": [{ "md5": "0123456789abcdef0123456789abcdef", "from": ["retrobat"] }], "readers": ["retrobat"], "required": true },
                    { "path": "bios/lynx48-1.rom", "accepted": [{ "md5": "fedcba9876543210fedcba9876543210", "from": ["retrobat"] }], "readers": ["retrobat"], "required": true }
                  ],
                  "reported": []
                }
              }
            }
            """));

        var kept = Assert.Single(manifest.Requirements);
        Assert.Equal("bios/lynx48-1.rom", kept.Path.Value);
        Assert.Equal(["emulators/jynx/lynx48-1.rom"], manifest.RejectedPaths);
    }

    [Fact]
    public void A_manifest_of_another_schema_is_refused_rather_than_misread()
    {
        var exception = Record.Exception(() => BiosManifest.Parse(Json("""{ "_schema": 1, "systems": {} }""")));

        Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("schema 1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_destination_the_manifest_names_is_a_storable_relative_path()
    {
        // Paths built from a ROM name are covered by the content and media tests. These are built
        // from the manifest instead, and they go six segments deep, so they get the same check.
        var manifest = Fixtures.LoadBiosManifest();

        Assert.All(manifest.Requirements, slot =>
        {
            Assert.True(RelativePath.TryCreate(slot.Path.Value, out var round));
            Assert.Equal(slot.Path, round);

            // The ceiling that actually binds is the 255-character file name, not MAX_PATH.
            Assert.InRange(slot.FileName.Length, 1, 255);
            Assert.DoesNotContain(slot.FileName, character => character is '<' or '>' or ':' or '"' or '|' or '?' or '*');
        });

        Assert.Equal(6, manifest.Requirements.Max(slot => slot.Path.Value.Count(c => c == '/') + 1));
    }

    [Fact]
    public void Every_destination_survives_a_round_trip_through_the_database()
    {
        // The paths are stored, so RelativePath agreeing is not enough on its own: the CHECK
        // on relative_path has to agree too, for all 615 distinct paths.
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());
        var manifest = Fixtures.LoadBiosManifest();

        foreach (var slot in manifest.Requirements.DistinctBy(slot => slot.Path))
        {
            store.Files.Record(new LocalFile
            {
                Path = slot.Path,
                Kind = LocalFileKind.Firmware,
                FileName = slot.FileName,
                Md5Hash = slot.Accepted.Count > 0 ? slot.Accepted[0].Md5 : null,
            });
        }

        Assert.Equal(
            manifest.Requirements.Select(slot => slot.Path).Distinct().Count(),
            store.Files.List(kind: LocalFileKind.Firmware).Count);
    }

    [Fact]
    public void One_md5_can_owe_several_destinations_and_one_destination_can_accept_several()
    {
        var manifest = Fixtures.LoadBiosManifest();

        // The same ColecoVision bios, wanted at four paths: RetroBat's three, one inside
        // openMSX's user-data tree, and blueMSX's. This is what makes the destination the key.
        var coleco = manifest.Requirements
            .Where(slot => slot.Accepts("2c66f5911e5b42b8ebe113403548eee7") is not null)
            .Select(slot => slot.Path.Value)
            .Order(StringComparer.Ordinal);

        Assert.Equal(
            ["bios/Machines/COL - ColecoVision/coleco.rom", "bios/coleco.rom", "bios/colecovision.rom", "bios/openMSX/share/systemroms/coleco.rom"],
            coleco);

        // The other direction: atari800's OS-B, where RetroBat's md5 ranks over the one the
        // atari800 core's profile states.
        var osb = Assert.Single(manifest.For("atari800"), slot => slot.Path.Value == "bios/ATARIOSB.ROM");
        Assert.Equal(2, osb.Accepted.Count);
        Assert.True(osb.Accepted[0].IsRetroBats);
    }

    [Fact]
    public void One_destination_can_be_read_by_several_systems()
    {
        // The plan carries a step per folder, so a destination has to be acted on once rather
        // than once per step, or the second write copies the file onto itself. openMSX's copy
        // of fmpac.rom is the widest: four MSX systems name the same file.
        var manifest = Fixtures.LoadBiosManifest();

        var fmpac = manifest.Requirements
            .Where(slot => slot.Path == RelativePath.Create("bios/openMSX/share/systemroms/fmpac.rom"))
            .Select(slot => slot.Folder)
            .Order(StringComparer.Ordinal);

        Assert.Equal(["msx1", "msx2", "msx2+", "msxturbor"], fmpac);
    }

    [Fact]
    public void Every_system_the_manifest_names_resolves_to_a_folder_RetroBat_has()
    {
        // The manifest is keyed by RetroBat folder. The generator aliases the two batocera keys
        // that are not one, and this is what catches a new one.
        var manifest = Fixtures.LoadBiosManifest();
        var systems = Fixtures.LoadEsSystems();

        var unresolved = manifest.Folders.Where(folder => !systems.HasFolder(folder)).Order(StringComparer.Ordinal).ToList();

        Assert.Empty(unresolved);
    }

    [Fact]
    public void A_firmware_row_has_no_game_and_no_folder()
    {
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());

        store.Files.Record(new LocalFile
        {
            Path = RelativePath.Create("bios/psxonpsp660.bin"),
            Kind = LocalFileKind.Firmware,
            FileName = "psxonpsp660.bin",
            Md5Hash = "c53ca5908936d412331790f4426c6c33",
        });

        var recorded = Assert.Single(store.Files.List(kind: LocalFileKind.Firmware));
        Assert.Null(recorded.Folder);
        Assert.Null(recorded.RomId);
        Assert.Equal("c53ca5908936d412331790f4426c6c33", recorded.Md5Hash);
    }

    [Theory]
    [InlineData("bios/x.bin", "psx", null)]
    [InlineData("bios/x.bin", null, 42)]
    [InlineData("saves/psx/x.bin", null, null)]
    [InlineData("emulators/jynx/lynx48-1.rom", null, null)]
    public void The_database_refuses_a_firmware_row_that_is_shaped_like_a_game(string path, string? folder, int? romId)
    {
        // Three rules in one CHECK, and the rom_id half is what keeps eviction away from
        // firmware: eviction only ever considers rows that have one.
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());

        var exception = Record.Exception(() => store.Files.Record(new LocalFile
        {
            Path = RelativePath.Create(path),
            Folder = folder,
            RomId = romId,
            Kind = LocalFileKind.Firmware,
            FileName = "x.bin",
        }));

        Assert.IsType<SqliteException>(exception);
    }

    [Fact]
    public void A_rom_row_still_has_to_carry_a_folder()
    {
        // The other half of the same CHECK. Making folder nullable for firmware must not make
        // it optional for everything else.
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());

        var exception = Record.Exception(() => store.Files.Record(new LocalFile
        {
            Path = RelativePath.Create("roms/psx/Game.chd"),
            RomId = 42,
            Kind = LocalFileKind.Rom,
            FileName = "Game.chd",
        }));

        Assert.IsType<SqliteException>(exception);
    }

    /// <summary>The two batocera keys that are not a RetroBat folder, as the generator aliases them.</summary>
    private static string Alias(string key) => key switch
    {
        "astrocde" => "astrocade",
        "msx" => "msx1",
        _ => key,
    };

    private static MemoryStream Json(string text) => new(System.Text.Encoding.UTF8.GetBytes(text));
}
