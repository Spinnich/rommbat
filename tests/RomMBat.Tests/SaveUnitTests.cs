using RomMBat.Core.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Scoping a class C save unit out of a tree, which is the whole class C problem.
/// </summary>
/// <remarks>
/// Every tree here is the shape of a real one, taken from a read-only sweep of an install with
/// a substantial library. That matters more than usual: the plan called class C "a directory
/// per game" and three of these systems refute it, so a synthetic tree invented from the prose
/// would have agreed with the prose and proved nothing.
/// </remarks>
public class SaveUnitTests
{
    [Fact]
    public void A_shape_that_names_an_emulator_data_root_is_refused_rather_than_hashed()
    {
        // The RPCS3 case, which is the fixture the plan names. Hashing saves/ps3/rpcs3 takes
        // 426.07 s over 32,451 files because that is dev_hdd0 entire: installed games, firmware
        // and caches. The savedata subtree is 77 files and 0.06 s. So the test is not "is it
        // slow", it is "does anything outside the declared container get read at all".
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        // The bulk that must never be read, sitting exactly where it sits on a real install.
        Write(tree, "saves/ps3/rpcs3/dev_hdd0/game/BLUS30443/USRDIR/EBOOT.BIN", "an installed game");
        Write(tree, "saves/ps3/rpcs3/dev_hdd0/disc/cache/blob.bin", "a cache");
        Write(tree, "saves/ps3/rpcs3/dev_flash/sys/external/lib.sprx", "firmware");

        // The save data, which is what a unit is.
        Write(tree, "saves/ps3/rpcs3/dev_hdd0/home/00000001/savedata/BLUS30061/PARAM.SFO", "one");
        Write(tree, "saves/ps3/rpcs3/dev_hdd0/home/00000001/savedata/BLUS30061/SAVE.DAT", "two");

        var units = new SaveUnitScanner(install).Scan("ps3");

        var unit = Assert.Single(units);
        Assert.Equal("BLUS30061", unit.Key);
        Assert.Equal("saves/ps3/rpcs3/dev_hdd0/home/00000001/savedata", unit.Container.Value);

        // The proof is the file list, not the timing: nothing outside the container is a member
        // of any unit, so the game, the cache and the firmware are never opened.
        Assert.Equal(
            ["BLUS30061/PARAM.SFO", "BLUS30061/SAVE.DAT"],
            unit.Files.Select(file => file.ArchivePath));
    }

    [Fact]
    public void One_title_id_owning_several_directories_is_one_unit()
    {
        // Measured on a real install: BLUS30109 owns three directories and BCUS98111 owns two.
        // Treating each directory as a unit would put three saves on the server for one game
        // and three rows on one (rom_id, slot).
        using var tree = TempRetroBatTree.Create();

        Write(tree, "saves/ps3/rpcs3/dev_hdd0/home/00000001/savedata/BLUS30109G6A383E91/DATA.BIN", "a");
        Write(tree, "saves/ps3/rpcs3/dev_hdd0/home/00000001/savedata/BLUS30109G6A3B071C/DATA.BIN", "b");
        Write(tree, "saves/ps3/rpcs3/dev_hdd0/home/00000001/savedata/BLUS30109S/DATA.BIN", "c");
        Write(tree, "saves/ps3/rpcs3/dev_hdd0/home/00000001/savedata/BCUS98111-AUTOSAVE/DATA.BIN", "d");
        Write(tree, "saves/ps3/rpcs3/dev_hdd0/home/00000001/savedata/BCUS98111-USERDATA/DATA.BIN", "e");

        var units = new SaveUnitScanner(tree.Install()).Scan("ps3");

        Assert.Equal(["BCUS98111", "BLUS30109"], units.Select(unit => unit.Key));
        Assert.Equal(2, units.Single(unit => unit.Key == "BCUS98111").Files.Count);
        Assert.Equal(3, units.Single(unit => unit.Key == "BLUS30109").Files.Count);
    }

    [Fact]
    public void A_psp_savedata_key_is_a_prefix_of_the_directory_name()
    {
        // UCES01011 is bare and ULES01513SYSDATA carries a suffix the game chose. Matching the
        // whole segment finds neither as a title id, which is measurement 141.
        using var tree = TempRetroBatTree.Create();

        Write(tree, "saves/psp/SAVEDATA/UCES01011/PARAM.SFO", "game data");
        Write(tree, "saves/psp/SAVEDATA/ULES01513SYSDATA/SYSDATA.BIN", "system data");

        // Not a title id at all, so not part of any unit. A real install keeps SYSTEM, GAME,
        // PLUGINS and TEXTURES in the same tree.
        Write(tree, "saves/psp/SAVEDATA/NOTATITLEID/whatever.bin", "not a save");

        var units = new SaveUnitScanner(tree.Install()).Scan("psp");

        Assert.Equal(["UCES01011", "ULES01513"], units.Select(unit => unit.Key));
        Assert.All(units, unit => Assert.Equal("ppsspp", unit.Emulator));
        Assert.All(units, unit => Assert.Equal("savedata", unit.Slot));
    }

    [Fact]
    public void A_soft_deleted_gci_is_excluded_and_several_gci_for_one_game_are_one_unit()
    {
        // Dolphin soft-deletes by appending .deleted and leaves the file in place, so a live
        // save and a deleted one sit side by side under one game code. Both cases were observed
        // together on a real install for GUNE.
        using var tree = TempRetroBatTree.Create();

        Write(tree, "saves/gamecube/dolphin-emu/User/GC/USA/69-GXBE-game1.ssx.gci", "one");
        Write(tree, "saves/gamecube/dolphin-emu/User/GC/USA/69-GXBE-settings.ssx.gci", "two");
        Write(tree, "saves/gamecube/dolphin-emu/User/GC/USA/5D-GUNE-Gauntlet.gci", "live");
        Write(tree, "saves/gamecube/dolphin-emu/User/GC/USA/5D-GUNE-Gauntlet.gci.deleted", "deleted");

        var units = new SaveUnitScanner(tree.Install()).Scan("gamecube");

        Assert.Equal(["GUNE", "GXBE"], units.Select(unit => unit.Key));

        // Two files for one game code, which is the class B shape nested inside class C.
        Assert.Equal(
            ["69-GXBE-game1.ssx.gci", "69-GXBE-settings.ssx.gci"],
            units.Single(unit => unit.Key == "GXBE").Files.Select(file => file.ArchivePath));

        // The soft-deleted one is not a member of anything. It fails the anchored .gci pattern
        // rather than being filtered by a suffix list, so there is no list to forget to update.
        Assert.Equal(
            ["5D-GUNE-Gauntlet.gci"],
            units.Single(unit => unit.Key == "GUNE").Files.Select(file => file.ArchivePath));
    }

    [Fact]
    public void A_wii_nand_title_yields_its_game_code_and_only_its_data_travels()
    {
        // title/00010000/<hex> is the disc-game tree and the hex is the ASCII game code, so
        // 52534245 is RSBE, which is the same code the .rvz header carries at 0x58.
        using var tree = TempRetroBatTree.Create();

        Write(tree, "saves/wii/dolphin-emu/User/Wii/title/00010000/52534245/data/advsv0.bin", "a save");
        Write(tree, "saves/wii/dolphin-emu/User/Wii/title/00010000/52534245/content/title.tmd", "metadata");

        // An installed title with no data/ at all. Observed on a real install as 524d4745
        // (RMGE), and it is a stub rather than a save: uploading an archive for it would put a
        // save on the server that never existed.
        Write(tree, "saves/wii/dolphin-emu/User/Wii/title/00010000/524d4745/content/title.tmd", "metadata");

        var units = new SaveUnitScanner(tree.Install()).Scan("wii");

        var unit = Assert.Single(units);
        Assert.Equal("RSBE", unit.Key);

        // Container-relative, which is what a restore needs: extracting this back into
        // title/00010000/ rebuilds the tree the emulator reads.
        // content/title.tmd is the installed title's metadata and stays behind.
        Assert.Equal(["52534245/data/advsv0.bin"], unit.Files.Select(file => file.ArchivePath));
    }

    [Fact]
    public void A_nand_directory_that_is_not_a_printable_game_code_is_not_a_unit()
    {
        // Only the disc-game container is declared, so system titles under title/00000001 are
        // never even reached. This covers the other half: something inside the declared
        // container whose name does not decode to a game code.
        using var tree = TempRetroBatTree.Create();

        Write(tree, "saves/wii/dolphin-emu/User/Wii/title/00010000/00000002/data/setting.txt", "system");
        Write(tree, "saves/wii/dolphin-emu/User/Wii/title/00010000/notevenhex/data/thing.bin", "junk");

        Assert.Empty(new SaveUnitScanner(tree.Install()).Scan("wii"));
    }

    [Fact]
    public void A_mame_unit_is_keyed_on_the_short_name_which_needs_no_lookup()
    {
        // The friendly case: the nvram directory name is the rom basename, so this is the one
        // class C system that resolves through the same index class A uses.
        using var tree = TempRetroBatTree.Create();

        Write(tree, "saves/mame/nvram/25pacman/eeprom", "one");
        Write(tree, "saves/mame/nvram/25pacman/flash", "two");
        Write(tree, "saves/mame/nvram/1944/eeprom", "three");

        var units = new SaveUnitScanner(tree.Install()).Scan("mame");

        Assert.Equal(["1944", "25pacman"], units.Select(unit => unit.Key));
        Assert.Equal(
            ["25pacman/eeprom", "25pacman/flash"],
            units.Single(unit => unit.Key == "25pacman").Files.Select(file => file.ArchivePath));
    }

    [Fact]
    public void A_system_with_no_declared_container_yields_nothing_rather_than_defaulting()
    {
        // The rule the whole grammar rests on: an unnamed path reports as unknown rather than
        // falling back to the emulator root. nds is class C on a real install by any reading
        // and is not declared, so it must produce no units at all rather than a guess.
        using var tree = TempRetroBatTree.Create();

        Write(tree, "saves/nds/DeSmuME/something/save.dsv", "a save nobody declared");
        Write(tree, "saves/wiiu/cemu/mlc01/usr/save/00050000/thing.bin", "another");

        var scanner = new SaveUnitScanner(tree.Install());

        Assert.Empty(scanner.Scan("nds"));
        Assert.Empty(scanner.Scan("wiiu"));

        // And a system that has no shape entry at all.
        Assert.Empty(scanner.Scan("mesen"));
    }

    [Fact]
    public void A_class_A_system_declares_no_unit_paths_so_its_loose_saves_are_left_to_the_battery_pass()
    {
        var shapes = SaveShapes.Bundled;

        Assert.False(shapes.For("snes")!.HasUnitPaths);
        Assert.False(shapes.For("saturn")!.HasUnitPaths);
        Assert.False(shapes.For("ps2")!.HasUnitPaths);

        Assert.True(shapes.For("psp")!.HasUnitPaths);
        Assert.True(shapes.For("ps3")!.HasUnitPaths);
        Assert.True(shapes.For("mame")!.HasUnitPaths);
        Assert.True(shapes.For("gamecube")!.HasUnitPaths);
        Assert.True(shapes.For("wii")!.HasUnitPaths);
    }

    [Fact]
    public void The_logical_hash_of_a_unit_is_stable_across_two_scans()
    {
        // Determinism is what makes a replayed flush idempotent: identical content posted twice
        // into one slot reuses the server row, and that only holds if "identical" really is.
        using var tree = TempRetroBatTree.Create();

        Write(tree, "saves/psp/SAVEDATA/UCES01011/PARAM.SFO", "game data");
        Write(tree, "saves/psp/SAVEDATA/UCES01011/DATA.BIN", "the save itself");

        var install = tree.Install();
        var scanner = new SaveUnitScanner(install);

        var first = Hash(install, Assert.Single(scanner.Scan("psp")));
        var second = Hash(install, Assert.Single(scanner.Scan("psp")));

        Assert.Equal(first, second);

        // And it moves when the contents do, or nothing would ever be re-uploaded.
        Write(tree, "saves/psp/SAVEDATA/UCES01011/DATA.BIN", "the save, changed");
        Assert.NotEqual(first, Hash(install, Assert.Single(scanner.Scan("psp"))));
    }

    [Fact]
    public void The_hash_is_taken_over_the_contents_so_the_container_it_sits_in_does_not_change_it()
    {
        // The archive is transport and the hash is identity. A unit restored under a different
        // user id on another machine is the same save, so RPCS3's numeric user directory must
        // not be part of the digest.
        using var one = TempRetroBatTree.Create();
        using var two = TempRetroBatTree.Create();

        Write(one, "saves/ps3/rpcs3/dev_hdd0/home/00000001/savedata/BLUS30061/SAVE.DAT", "identical");
        Write(two, "saves/ps3/rpcs3/dev_hdd0/home/00000009/savedata/BLUS30061/SAVE.DAT", "identical");

        var first = Hash(one.Install(), Assert.Single(new SaveUnitScanner(one.Install()).Scan("ps3")));
        var second = Hash(two.Install(), Assert.Single(new SaveUnitScanner(two.Install()).Scan("ps3")));

        Assert.Equal(first, second);
    }

    private static string Hash(RomMBat.Core.Paths.RetroBatInstall install, SaveUnit unit) =>
        LogicalContentHash.Fold(unit.Files.Select(file =>
            (file.ArchivePath, LogicalContentHash.OfFile(install.Resolve(file.Path)))));

    private static void Write(TempRetroBatTree tree, string relativePath, string content)
    {
        var absolute = Path.Combine(tree.Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllText(absolute, content);
    }

    [Fact]
    public void Eviction_refuses_a_rom_with_an_un_uploaded_directory_save_on_disk()
    {
        // The seam M3 shipped with a mitigation instead of an answer, now closed for class C.
        // The guard needed no new question: a unit is a local_save row like any other, so the
        // query that already asks "is there a save on disk that never went up" counts it.
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        using var store = LocalStore.Open(install);

        store.Files.Record(new LocalFile
        {
            Path = RelativePath.Create("roms/mame/25pacman.zip"),
            Kind = LocalFileKind.Rom,
            RomId = 8,
            Folder = "mame",
            FileName = "25pacman.zip",
            SizeBytes = 64,
        });

        Write(tree, "saves/mame/nvram/25pacman/eeprom", "an nvram nobody has uploaded");

        new SaveScanner(install, store).Scan();

        var guard = new SaveGuard(store);
        var verdict = guard.Check(8, RelativePath.Create("roms/mame/25pacman.zip"));

        Assert.False(verdict.CanRemove);
        Assert.Contains("has not reached the server", verdict.Reason!, StringComparison.Ordinal);

        // Once it is up, the guard stops objecting. Marked on the unit, which is what makes
        // this different from class A: the path alone names a container shared by every game.
        var unit = Assert.Single(store.Saves.List(), save => save.ShapeClass == SaveShapeClass.C);
        store.Saves.MarkUploaded(unit.Path, unit.UnitKey, unit.ContentHash!, DateTimeOffset.UnixEpoch);

        Assert.True(guard.Check(8, RelativePath.Create("roms/mame/25pacman.zip")).CanRemove);
    }

    [Fact]
    public void Marking_one_unit_uploaded_leaves_its_neighbours_in_the_same_container_unsent()
    {
        // The container is shared by every game on the system, so a MarkUploaded keyed on the
        // path would clear all of them at once and let eviction take every one of their ROMs.
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        using var store = LocalStore.Open(install);

        foreach (var (romId, name) in new[] { (1L, "UCES01011"), (2L, "ULES01513") })
        {
            store.Files.Record(new LocalFile
            {
                Path = RelativePath.Create($"roms/psp/{name}.cso"),
                Kind = LocalFileKind.Rom,
                RomId = (int)romId,
                Folder = "psp",
                FileName = $"{name}.cso",
                SizeBytes = 64,
            });

            Write(tree, $"saves/psp/SAVEDATA/{name}/DATA.BIN", $"the save for {name}");
        }

        new SaveScanner(install, store).Scan();

        var units = store.Saves.List().Where(save => save.ShapeClass == SaveShapeClass.C).ToList();

        Assert.Equal(2, units.Count);
        Assert.Equal(["saves/psp/SAVEDATA", "saves/psp/SAVEDATA"], units.Select(unit => unit.Path.Value));

        var first = units[0];
        store.Saves.MarkUploaded(first.Path, first.UnitKey, first.ContentHash!, DateTimeOffset.UnixEpoch);

        var after = store.Saves.List().Where(save => save.ShapeClass == SaveShapeClass.C).ToList();

        Assert.Single(after, unit => !unit.IsUnsent);
        Assert.Single(after, unit => unit.IsUnsent);
    }

    [Fact]
    public void Forgetting_one_unit_leaves_its_neighbours_recorded()
    {
        // The same trap on the other side. Forgetting by path would drop every PSP row the
        // moment one game's savedata was deleted, and eviction would then take ROMs whose saves
        // had never gone up.
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        using var store = LocalStore.Open(install);

        Write(tree, "saves/psp/SAVEDATA/UCES01011/DATA.BIN", "one");
        Write(tree, "saves/psp/SAVEDATA/ULES01513/DATA.BIN", "two");

        new SaveScanner(install, store).Scan();
        Assert.Equal(2, store.Saves.List().Count(save => save.ShapeClass == SaveShapeClass.C));

        Directory.Delete(Path.Combine(tree.Root, "saves", "psp", "SAVEDATA", "UCES01011"), recursive: true);

        new SaveScanner(install, store).Scan();

        var remaining = Assert.Single(store.Saves.List(), save => save.ShapeClass == SaveShapeClass.C);
        Assert.Equal("ULES01513", remaining.UnitKey);
    }
}
