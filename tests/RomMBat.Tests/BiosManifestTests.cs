using System.Text.Json;
using Microsoft.Data.Sqlite;
using RomMBat.Core.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The bundled BIOS manifest, and the rules its shape forces on everything that reads it.
/// </summary>
/// <remarks>
/// The manifest is bundled rather than read from the install because a real RetroBat 8.2 has
/// no <c>batocera-systems.json</c> anywhere: it is a string resource inside
/// <c>emulationstation/batocera-systems.exe</c>. These tests hold the generated copy to the
/// vendored original, so a regeneration that quietly dropped a system fails here.
/// </remarks>
public sealed class BiosManifestTests
{
    [Fact]
    public void The_bundled_manifest_carries_every_entry_the_vendored_one_does()
    {
        var manifest = Fixtures.LoadBiosManifest();
        using var document = JsonDocument.Parse(File.ReadAllText(Fixtures.BatoceraSystemsJson));

        var upstream = document.RootElement
            .EnumerateObject()
            .SelectMany(system => system.Value.GetProperty("biosFiles").EnumerateArray()
                .Select(file => (
                    System: system.Name,
                    Md5: file.GetProperty("md5").GetString()!.Trim().ToLowerInvariant(),
                    Path: file.GetProperty("file").GetString()!)))
            .ToList();

        Assert.Equal(353, upstream.Count);

        // The seven entries under emulators/ are refused by the reader, so the bundled set is
        // the rest. All seven are hashless, which is why refusing them costs nothing.
        var expected = upstream.Where(entry => BiosManifest.IsInsideBios(entry.Path)).ToList();
        Assert.Equal(346, expected.Count);
        Assert.All(upstream.Except(expected), entry => Assert.Empty(entry.Md5));

        Assert.Equal(
            expected.Select(entry => (entry.System, entry.Md5, entry.Path)).Order().ToList(),
            manifest.Requirements
                .Select(requirement => (requirement.System, Md5: requirement.Md5 ?? string.Empty, Path: requirement.Path.Value))
                .Order()
                .ToList());
    }

    [Fact]
    public void The_counts_the_plan_quotes_come_out_of_the_bundled_copy()
    {
        var manifest = Fixtures.LoadBiosManifest();

        Assert.Equal(99, manifest.Folders.Count);
        Assert.Equal(346, manifest.Requirements.Count);
        Assert.Equal(7, manifest.RejectedPaths.Count);

        // 156, not 157. The set is built without the blank md5, which 179 of the 353 entries
        // carry; counting it inflated every number derived from this set by one.
        Assert.Equal(156, manifest.Requirements.Where(r => r.Md5 is not null).Select(r => r.Md5).Distinct().Count());
        Assert.Equal(172, manifest.Requirements.Count(requirement => requirement.IsUnverifiable));
    }

    [Fact]
    public void A_hashless_entry_is_unverifiable_rather_than_missing()
    {
        var manifest = Fixtures.LoadBiosManifest();

        // mastersystem is one of the 28 systems with no joinable entry at all. Reporting these
        // as "not in your library" would send a user looking for a file RomMBat could not
        // recognise if they already had it.
        var mastersystem = manifest.For("mastersystem");

        Assert.NotEmpty(mastersystem);
        Assert.All(mastersystem, requirement => Assert.True(requirement.IsUnverifiable));
        Assert.All(mastersystem, requirement => Assert.Null(requirement.Md5));
    }

    [Fact]
    public void MAME_software_lists_need_no_special_case_because_none_of_them_carries_a_hash()
    {
        var manifest = Fixtures.LoadBiosManifest();

        var mame = manifest.Requirements
            .Where(requirement => requirement.Path.Value.StartsWith("bios/mame/", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Equal(64, mame.Count);
        Assert.All(mame, requirement => Assert.True(requirement.IsUnverifiable));
    }

    [Fact]
    public void Nothing_the_manifest_asks_for_lands_outside_bios()
    {
        var manifest = Fixtures.LoadBiosManifest();

        Assert.All(
            manifest.Requirements,
            requirement => Assert.StartsWith("bios/", requirement.Path.Value, StringComparison.Ordinal));

        // The seven that would have are named, so a future upstream entry growing an md5
        // outside bios/ shows up here rather than writing into an emulator's install.
        Assert.All(
            manifest.RejectedPaths,
            path => Assert.StartsWith("emulators/", path, StringComparison.Ordinal));
    }

    [Fact]
    public void A_hashed_entry_outside_bios_is_refused_rather_than_written()
    {
        // Synthesized rather than waiting for upstream to do it: the rule only earns its
        // keep on the day the manifest changes.
        var manifest = BiosManifest.Parse(Json("""
            {
              "systems": {
                "camplynx": {
                  "name": "Camputers Lynx",
                  "folder": "camplynx",
                  "files": [
                    { "md5": "0123456789abcdef0123456789abcdef", "path": "emulators/jynx/lynx48-1.rom" },
                    { "md5": "fedcba9876543210fedcba9876543210", "path": "bios/lynx48-1.rom" }
                  ]
                }
              }
            }
            """));

        var kept = Assert.Single(manifest.Requirements);
        Assert.Equal("bios/lynx48-1.rom", kept.Path.Value);
        Assert.Equal(["emulators/jynx/lynx48-1.rom"], manifest.RejectedPaths);
    }

    [Fact]
    public void Every_destination_the_manifest_names_is_a_storable_relative_path()
    {
        // Paths built from a ROM name were covered by M3 and M4. These are built from the
        // manifest instead, and they go six segments deep, so they get the same check.
        var manifest = Fixtures.LoadBiosManifest();
        var limits = FilesystemLimits.For("NTFS", availableFreeBytes: long.MaxValue);

        Assert.All(manifest.Requirements, requirement =>
        {
            Assert.True(RelativePath.TryCreate(requirement.Path.Value, out var round));
            Assert.Equal(requirement.Path, round);

            // The ceiling that actually binds is the 255-character file name, not MAX_PATH.
            Assert.InRange(requirement.FileName.Length, 1, 255);
            Assert.DoesNotContain(requirement.FileName, character => character is '<' or '>' or ':' or '"' or '|' or '?' or '*');
            Assert.True(limits.CanHold(0));
        });

        Assert.Equal(6, manifest.Requirements.Max(requirement => requirement.Path.Value.Count(c => c == '/') + 1));
    }

    [Fact]
    public void Every_destination_survives_a_round_trip_through_the_database()
    {
        // The paths are stored, so RelativePath agreeing is not enough on its own: the CHECK
        // on relative_path has to agree too, for all 346 of them.
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());
        var manifest = Fixtures.LoadBiosManifest();

        foreach (var requirement in manifest.Requirements.DistinctBy(requirement => requirement.Path))
        {
            store.Files.Record(new LocalFile
            {
                Path = requirement.Path,
                Kind = LocalFileKind.Firmware,
                FileName = requirement.FileName,
                Md5Hash = requirement.Md5,
            });
        }

        Assert.Equal(
            manifest.Requirements.Select(requirement => requirement.Path).Distinct().Count(),
            store.Files.List(kind: LocalFileKind.Firmware).Count);
    }

    [Fact]
    public void One_md5_can_owe_several_destinations_and_no_destination_takes_two()
    {
        var manifest = Fixtures.LoadBiosManifest();

        // The same ColecoVision bios, wanted at three paths, one of them inside openMSX's
        // user-data tree. This is what makes the destination the key and the md5 not.
        var coleco = manifest.Requirements
            .Where(requirement => requirement.Md5 == "2c66f5911e5b42b8ebe113403548eee7")
            .ToList();

        Assert.Equal(
            ["bios/coleco.rom", "bios/colecovision.rom", "bios/openMSX/share/systemroms/coleco.rom"],
            coleco.Select(requirement => requirement.Path.Value).Order(StringComparer.Ordinal));

        var byMd5 = manifest.Requirements
            .Where(requirement => requirement.Md5 is not null)
            .GroupBy(requirement => requirement.Md5!)
            .Count(group => group.Select(requirement => requirement.Path).Distinct().Count() > 1);

        Assert.Equal(6, byMd5);

        var byPath = manifest.Requirements
            .Where(requirement => requirement.Md5 is not null)
            .GroupBy(requirement => requirement.Path)
            .Count(group => group.Select(requirement => requirement.Md5).Distinct().Count() > 1);

        Assert.Equal(0, byPath);
    }

    [Fact]
    public void Every_system_the_manifest_names_resolves_to_a_folder_RetroBat_has()
    {
        // The manifest is keyed by batocera system names, a third vocabulary beside
        // es_systems.cfg's <name> and its <path> basename. 97 of 99 keys are already a folder;
        // the generator aliases the two that are not, and this is what catches a new one.
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

    private static MemoryStream Json(string text) => new(System.Text.Encoding.UTF8.GetBytes(text));
}
