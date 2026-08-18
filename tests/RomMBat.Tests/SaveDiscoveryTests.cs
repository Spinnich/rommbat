using RomM.Client.Saves;
using RomMBat.Core.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Finding saves on disk, deciding what they are, and reporting what cannot be synced.
/// </summary>
/// <remarks>
/// The tree these tests build is the shape of the real one: loose class A saves beside a
/// shared container beside an emulator subdirectory, because that is what a real install
/// holds and every one of those is a different answer.
/// </remarks>
public class SaveDiscoveryTests
{
    [Fact]
    public void The_bundled_shapes_cover_what_M0_classified_and_admit_they_do_not_cover_the_rest()
    {
        var shapes = SaveShapes.Bundled;

        Assert.Equal(23, shapes.Count);
        Assert.Equal("libretro", shapes.LooseEmulator);

        // Tracked as a visible number so the gap cannot silently grow. All 21 hold content on
        // the measured install, so this is real missing coverage rather than dead systems.
        Assert.Equal(21, shapes.Unclassified.Count);
        Assert.Contains("nds", shapes.Unclassified, StringComparer.Ordinal);

        Assert.Equal(SaveShapeClass.A, Assert.Single(shapes.For("snes")!.Classes));
        Assert.Equal(SaveShapeClass.B, Assert.Single(shapes.For("saturn")!.Classes));
        Assert.Equal(SaveShapeClass.C, Assert.Single(shapes.For("ps3")!.Classes));

        // megacd is two classes at once: per-game .brm beside a shared RAM cart.
        Assert.Equal([SaveShapeClass.B, SaveShapeClass.D], shapes.For("megacd")!.Classes);

        // psx is the worked example of shape being a property of (system, emulator).
        Assert.True(shapes.For("psx")!.DependsOnEmulator);

        // Nine top-level directories on a real install are not systems at all.
        Assert.Null(shapes.For("mesen"));
        Assert.Null(shapes.For("ports"));
    }

    [Theory]
    [InlineData("megacd", "4Mbit_cart.brm")]
    [InlineData("xbox", "eeprom.bin")]
    [InlineData("xbox", "xbox_hdd.qcow2")]
    [InlineData("saturn", "kronos/bkram.bin")]
    [InlineData("ps2", "pcsx2/memcards/Mcd001.ps2")]
    public void A_shared_container_is_recognised_by_name_because_nothing_else_distinguishes_it(
        string system,
        string path)
    {
        // 4Mbit_cart.brm sits beside per-game .brm files at the same level with the same
        // extension. Only the name separates a 512 KB RAM cart shared by every Mega CD game
        // from one game's save.
        Assert.NotNull(SaveShapes.Bundled.SharedContainerReason(system, path));
        Assert.Null(SaveShapes.Bundled.SharedContainerReason(system, "Ecco the Dolphin (USA).brm"));
    }

    [Fact]
    public void A_loose_class_A_save_is_found_and_tied_to_its_rom()
    {
        using var fixture = SaveTree.Create();

        fixture.AddRom(42, "snes", "ActRaiser (USA).zip");
        fixture.AddSave("snes", "ActRaiser (USA).srm", "battery bytes");

        var outcome = fixture.Scan();

        Assert.Equal(1, outcome.Found);
        Assert.Equal(1, outcome.Attributed);

        var save = Assert.Single(fixture.Store.Saves.List());
        Assert.Equal("saves/snes/ActRaiser (USA).srm", save.Path.Value);
        Assert.Equal(42, save.RomId);
        Assert.Equal("snes", save.System);
        Assert.Equal(SaveShapeClass.A, save.ShapeClass);
        Assert.Equal("libretro:battery", save.Slot);
        Assert.Equal(LogicalContentHash.OfFile(fixture.Resolve(save.Path)), save.ContentHash);

        // Never uploaded, which is SaveGuard's third question answering itself.
        Assert.True(save.IsUnsent);
    }

    [Fact]
    public void Class_B_takes_one_slot_per_file_so_the_pair_cannot_overwrite_itself()
    {
        // Saturn writes .bcr and .bkr for the same game, 512 KB and 32 KB. Sharing one slot
        // would have each upload replace the other.
        using var fixture = SaveTree.Create();

        fixture.AddRom(7, "saturn", "Battle Garegga (Japan).chd");
        fixture.AddSave("saturn", "Battle Garegga (Japan).bcr", "backup cartridge");
        fixture.AddSave("saturn", "Battle Garegga (Japan).bkr", "internal backup");

        fixture.Scan();

        var saves = fixture.Store.Saves.List();

        Assert.Equal(2, saves.Count);
        Assert.All(saves, save => Assert.Equal(7, save.RomId));
        Assert.Equal(
            ["libretro:battery:bcr", "libretro:battery:bkr"],
            saves.Select(save => save.Slot).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_shared_container_beside_a_per_game_save_is_reported_and_never_recorded()
    {
        // megacd is B and D at once, in one directory, at one level.
        using var fixture = SaveTree.Create();

        fixture.AddRom(9, "megacd", "Ecco the Dolphin (USA).chd");
        fixture.AddSave("megacd", "Ecco the Dolphin (USA).brm", "one game's save");
        fixture.AddSave("megacd", "4Mbit_cart.brm", "every game's RAM cart");

        var outcome = fixture.Scan();

        var save = Assert.Single(fixture.Store.Saves.List());
        Assert.Equal("saves/megacd/Ecco the Dolphin (USA).brm", save.Path.Value);

        var reported = Assert.Single(
            fixture.Store.Unsyncable.List(),
            entry => entry.Reason == UnsyncableReason.SharedContainer);
        Assert.Equal("megacd", reported.System);
        Assert.Contains("RAM cart", reported.Detail, StringComparison.Ordinal);
        Assert.True(outcome.Unsyncable > 0);
    }

    [Fact]
    public void A_loose_file_under_a_class_D_system_is_not_taken_for_class_A()
    {
        // xbox keeps eeprom.bin and a disk image loose under the system folder, which is
        // exactly where a class A save lives. Position says class A; the name says otherwise.
        using var fixture = SaveTree.Create();

        fixture.AddSave("xbox", "eeprom.bin", "console eeprom");
        fixture.AddSave("xbox", "xbox_hdd.qcow2", "a whole disk image");

        var outcome = fixture.Scan();

        Assert.Equal(0, outcome.Found);
        Assert.Empty(fixture.Store.Saves.List());

        var reported = Assert.Single(
            fixture.Store.Unsyncable.List(),
            entry => entry.System == "xbox" && entry.Reason == UnsyncableReason.SharedContainer);
        Assert.Equal(2, reported.FileCount);
    }

    [Fact]
    public void A_directory_no_shape_covers_is_reported_rather_than_read()
    {
        // Nine top-level directories on a real install are not declared systems. An unknown
        // tree is not a tree to start writing into.
        using var fixture = SaveTree.Create();

        fixture.AddSave("mesen", "something.srm", "an emulator-named folder, not a system");
        fixture.AddSave("windows", "whatever.dat", "nor is this one");

        var outcome = fixture.Scan();

        Assert.Equal(0, outcome.Found);

        var reasons = fixture.Store.Unsyncable.List();
        Assert.Equal(2, reasons.Count);
        Assert.All(reasons, entry => Assert.Equal(UnsyncableReason.UnknownShape, entry.Reason));
    }

    [Fact]
    public void Directory_saves_and_states_are_reported_once_per_system_with_a_real_count()
    {
        // Stage 1 ships neither, and the alternative to reporting them is a user whose PS3
        // saves never go up with nothing saying so. Counted, because a row saying "1" would
        // understate the gap it exists to show.
        using var fixture = SaveTree.Create();

        for (var i = 0; i < 5; i++)
        {
            fixture.AddSave("ps3", $"rpcs3/dev_hdd0/home/00000001/savedata/GAME{i}/DATA.BIN", "directory save");
        }

        fixture.AddSave("snes", "libretro.snes9x/ActRaiser (USA).state1", "a save state");

        var outcome = fixture.Scan();

        Assert.Equal(0, outcome.Found);

        var ps3 = Assert.Single(fixture.Store.Unsyncable.List(), entry => entry.System == "ps3");
        Assert.Equal(UnsyncableReason.NotInThisVersion, ps3.Reason);
        Assert.Equal(5, ps3.FileCount);
        Assert.Contains("rpcs3", ps3.Detail, StringComparison.Ordinal);

        // Names the directories it found, so a user can go and look at them.
        Assert.Contains("This release syncs", ps3.Detail, StringComparison.Ordinal);

        Assert.Single(fixture.Store.Unsyncable.List(), entry => entry.System == "snes");
    }

    [Fact]
    public void A_save_matching_no_rom_is_kept_and_reported_rather_than_attributed_to_a_guess()
    {
        using var fixture = SaveTree.Create();

        fixture.AddSave("snes", "A Game Nobody Owns (USA).srm", "orphan");

        var outcome = fixture.Scan();

        Assert.Equal(1, outcome.Found);
        Assert.Equal(0, outcome.Attributed);

        // Recorded, because it still exists and eviction has to know about it, but with no
        // rom_id: an upload needs one and guessing is worse than saying so.
        var save = Assert.Single(fixture.Store.Saves.List());
        Assert.Null(save.RomId);

        Assert.Single(fixture.Store.Unsyncable.List(), entry => entry.Reason == UnsyncableReason.Unattributed);
    }

    [Fact]
    public void RetroArch_disc_index_files_are_skipped_because_they_carry_an_absolute_path()
    {
        // The .ldci records which disc is in the drive and its image_path is absolute with a
        // drive letter. Relaying it through RomM restores a dangling pointer elsewhere.
        using var fixture = SaveTree.Create();

        fixture.AddRom(3, "psx", "Metal Gear Solid (USA) (Rev 1).chd");
        fixture.AddSave("psx", "Metal Gear Solid (USA) (Rev 1).srm", "the card");
        fixture.AddSave(
            "psx",
            "Metal Gear Solid (USA) (Rev 1).ldci",
            """{"version":"1.0","image_index":0,"image_path":"E:\\RetroBat\\roms\\psx\\x.chd"}""");

        fixture.Scan();

        var save = Assert.Single(fixture.Store.Saves.List());
        Assert.Equal("saves/psx/Metal Gear Solid (USA) (Rev 1).srm", save.Path.Value);
        Assert.DoesNotContain(fixture.Store.Unsyncable.List(), entry => entry.Detail.Contains(".ldci", StringComparison.Ordinal));
    }

    [Fact]
    public void Rescanning_keeps_what_is_known_about_the_upload_and_forgets_a_deleted_save()
    {
        using var fixture = SaveTree.Create();

        fixture.AddRom(42, "snes", "ActRaiser (USA).zip");
        fixture.AddSave("snes", "ActRaiser (USA).srm", "battery bytes");
        fixture.Scan();

        var save = Assert.Single(fixture.Store.Saves.List());
        fixture.Store.Saves.MarkUploaded(save.Path, save.UnitKey, save.ContentHash!, DateTimeOffset.UnixEpoch);

        // A rescan must not forget that a save went up, or eviction refuses forever and every
        // sync re-uploads everything.
        fixture.Scan();
        var after = Assert.Single(fixture.Store.Saves.List());
        Assert.False(after.IsUnsent);
        Assert.False(after.HasChangedSinceUpload);

        // Changing the file makes it unsent again, on content and never on mtime.
        fixture.AddSave("snes", "ActRaiser (USA).srm", "the player actually saved this time");
        fixture.Scan();
        Assert.True(Assert.Single(fixture.Store.Saves.List()).HasChangedSinceUpload);

        // And a save whose file is gone stops blocking eviction.
        File.Delete(fixture.Resolve(save.Path));
        Assert.Equal(1, fixture.Scan().Forgotten);
        Assert.Empty(fixture.Store.Saves.List());
    }

    [Fact]
    public void Eviction_refuses_a_rom_with_an_un_uploaded_save_on_disk()
    {
        // The M3 seam closing. Before this, a save produced while nothing was watching was
        // invisible to the guard and the gap was covered by never touching a file RomMBat did
        // not download.
        using var fixture = SaveTree.Create();

        fixture.AddRom(42, "snes", "ActRaiser (USA).zip");
        fixture.AddSave("snes", "ActRaiser (USA).srm", "progress");
        fixture.Scan();

        var guard = new SaveGuard(fixture.Store);
        var romPath = RelativePath.Create("roms/snes/ActRaiser (USA).zip");

        var refused = guard.Check(42, romPath);
        Assert.False(refused.CanRemove);
        Assert.Contains("has not reached the server", refused.Reason!, StringComparison.Ordinal);

        // Once it is up, the guard stops objecting.
        var save = Assert.Single(fixture.Store.Saves.List());
        fixture.Store.Saves.MarkUploaded(save.Path, save.UnitKey, save.ContentHash!, DateTimeOffset.UnixEpoch);

        Assert.True(guard.Check(42, romPath).CanRemove);

        // And a game with no save at all was never blocked.
        Assert.True(guard.Check(99, RelativePath.Create("roms/snes/Other.zip")).CanRemove);
    }

    [Fact]
    public void Two_games_sharing_a_name_across_systems_each_keep_their_own_save()
    {
        // The ordinary state of a multi-system library: Contra, Aladdin, Tetris and Batman all
        // exist on several systems. A stem-only attribution index gives both saves to whichever
        // ROM was recorded first, which uploads one game's save against the other's id and puts
        // two rows on one (rom_id, slot).
        using var fixture = SaveTree.Create();

        fixture.AddRom(1, "nes", "Contra (USA).zip");
        fixture.AddRom(2, "snes", "Contra (USA).zip");
        fixture.AddSave("nes", "Contra (USA).srm", "nes progress");
        fixture.AddSave("snes", "Contra (USA).srm", "snes progress");

        var outcome = fixture.Scan();

        Assert.Equal(2, outcome.Found);
        Assert.Equal(2, outcome.Attributed);

        var saves = fixture.Store.Saves.List();

        Assert.Equal(1, Assert.Single(saves, save => save.System == "nes").RomId);
        Assert.Equal(2, Assert.Single(saves, save => save.System == "snes").RomId);

        // Both carry the same slot, so nothing downstream may key on it alone.
        Assert.Equal(2, saves.Count(save => save.Slot == "libretro:battery"));
        Assert.Equal(2, saves.Select(save => (save.RomId, save.Slot)).Distinct().Count());
    }

    [Fact]
    public void A_save_matching_no_rom_in_its_own_system_is_reported_rather_than_given_to_another()
    {
        // Fail closed across the system boundary too. Attributing saves/snes/Contra.srm to the
        // NES ROM because that is the only Contra installed is the same mis-attribution, just
        // harder to notice.
        using var fixture = SaveTree.Create();

        fixture.AddRom(1, "nes", "Contra (USA).zip");
        fixture.AddSave("snes", "Contra (USA).srm", "snes progress");

        var outcome = fixture.Scan();

        Assert.Equal(1, outcome.Found);
        Assert.Equal(0, outcome.Attributed);
        Assert.Null(Assert.Single(fixture.Store.Saves.List()).RomId);
    }

    [Fact]
    public void A_save_whose_hash_could_not_be_taken_still_blocks_eviction()
    {
        // Fail closed. The commonest cause is a running emulator holding the file, and
        // refusing to evict a game that is very likely running is right anyway.
        using var fixture = SaveTree.Create();

        fixture.AddRom(42, "snes", "ActRaiser (USA).zip");

        fixture.Store.Saves.Record(
            new LocalSave
            {
                Path = RelativePath.Create("saves/snes/ActRaiser (USA).srm"),
                System = "snes",
                Emulator = "libretro",
                ShapeClass = SaveShapeClass.A,
                Slot = "libretro:battery",
                RomId = 42,
                ContentHash = null,
                UploadedContentHash = "0123456789abcdef0123456789abcdef",
                UploadedAtUtc = DateTimeOffset.UnixEpoch,
            },
            DateTimeOffset.UnixEpoch);

        var verdict = new SaveGuard(fixture.Store).Check(42, RelativePath.Create("roms/snes/ActRaiser (USA).zip"));

        Assert.False(verdict.CanRemove);
    }

    /// <summary>A temp install with a save tree, a ROM index and a scanner.</summary>
    private sealed class SaveTree : IDisposable
    {
        private readonly TempRetroBatTree _tree;

        private SaveTree(TempRetroBatTree tree, RetroBatInstall install, LocalStore store)
        {
            _tree = tree;
            Install = install;
            Store = store;
        }

        public RetroBatInstall Install { get; }

        public LocalStore Store { get; }

        public static SaveTree Create()
        {
            var tree = TempRetroBatTree.Create();
            var install = tree.Install();
            return new SaveTree(tree, install, LocalStore.Open(install));
        }

        public string Resolve(RelativePath path) => Install.Resolve(path);

        public void AddRom(long romId, string folder, string fileName)
        {
            var path = RelativePath.Create($"roms/{folder}/{fileName}");
            var absolute = Install.Resolve(path);

            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllText(absolute, "rom bytes");

            Store.Files.Record(new LocalFile
            {
                Path = path,
                Folder = folder,
                RomId = (int)romId,
                Kind = LocalFileKind.Rom,
                FileName = fileName,
                SizeBytes = 9,
            });
        }

        public void AddSave(string system, string relative, string contents)
        {
            var absolute = Install.Resolve(RelativePath.Create($"saves/{system}/{relative}"));
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllText(absolute, contents);
        }

        public SaveScanOutcome Scan() => new SaveScanner(Install, Store).Scan();

        public void Dispose()
        {
            Store.Dispose();
            _tree.Dispose();
        }
    }

    [Fact]
    public void A_restore_for_a_slot_this_device_never_held_is_named_from_the_rom_not_from_file_name_no_tags()
    {
        // Measurement 152. The server does not undo its own timestamp tag, it runs a general
        // tag stripper: a real save came back as
        // "Phantasy Star (Brazil) [2026-08-17_17-01-00].srm" with file_name_no_tags of
        // "Phantasy Star", because (Brazil) is part of the ROM's name. Writing that produces a
        // file libretro cannot see, so this fails on the old code, which used the untagged name.
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());

        store.Files.Record(new LocalFile
        {
            Path = RelativePath.Create("roms/mastersystem/Phantasy Star (Brazil).zip"),
            Kind = LocalFileKind.Rom,
            RomId = 239719,
            Folder = "mastersystem",
            FileName = "Phantasy Star (Brazil).zip",
            SizeBytes = 128,
        });

        store.SaveSlots.Record(
            new SaveRow(
                134,
                239719,
                "Phantasy Star (Brazil) [2026-08-17_17-01-00].srm",
                "Phantasy Star",
                "srm",
                32768,
                "338dd456da3b26ae7b1fedf63a289a14",
                "libretro:battery",
                "libretro",
                null,
                DateTimeOffset.UnixEpoch,
                null),
            DateTimeOffset.UnixEpoch);

        var slot = store.SaveSlots.Read(239719, "libretro:battery");

        Assert.NotNull(slot);
        Assert.Equal("saves/mastersystem/Phantasy Star (Brazil).srm", slot.OnDiskPath?.Value);

        // And the server's own two names are still kept, because they are its identity for the
        // row even though neither is what goes on disk.
        Assert.Equal("Phantasy Star (Brazil) [2026-08-17_17-01-00].srm", slot.FileName);
        Assert.Equal("Phantasy Star", slot.FileNameNoTags);
    }
}
