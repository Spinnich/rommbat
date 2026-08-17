using RomMBat.Core.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Finding save states on disk and deciding which ROM each belongs to.
/// </summary>
/// <remarks>
/// The trees built here are the shape of a real one: a core-scoped libretro directory beside a
/// standalone emulator's, battery saves loose in the parent, and the <c>.txt</c> sidecar and the
/// screenshot sitting in with the states.
/// </remarks>
public class StateDiscoveryTests
{
    [Fact]
    public void A_state_resolves_to_its_rom_through_the_same_key_a_battery_save_uses()
    {
        using var tree = StateTree.Create();
        tree.AddRom(42, "snes", "ActRaiser (USA).zip");
        tree.AddState("snes/libretro.snes9x", "ActRaiser (USA).state1", "state bytes");

        var outcome = tree.Scan();

        Assert.Equal(1, outcome.Found);
        Assert.Equal(1, outcome.Attributed);

        var state = Assert.Single(tree.Store.States.List());

        Assert.Equal(42, state.RomId);
        Assert.Equal("snes", state.System);
        Assert.Equal("libretro", state.Emulator);
        Assert.Equal("snes9x", state.Core);
        Assert.Equal("libretro:snes9x:1", state.Slot);
        Assert.NotNull(state.ContentHash);
    }

    [Fact]
    public void The_system_and_the_core_are_read_off_the_tree_rather_than_configured()
    {
        // Neither level of the save tree is positional, so the only sound reading is "which
        // declaration could have produced this path".
        using var tree = StateTree.Create();
        tree.AddRom(1, "nes", "Battle City.zip");
        tree.AddRom(2, "ps2", "Game (USA).iso");

        tree.AddState("nes/bizhawk/sstates/NesHawk", "Battle City.QuickSave0.State", "a");
        tree.AddState("ps2/pcsx2", "Game (USA).03.p2s", "b");

        tree.Scan();

        var states = tree.Store.States.List().ToDictionary(state => state.Emulator, StringComparer.Ordinal);

        Assert.Equal("nes", states["bizhawk"].System);
        Assert.Equal("NesHawk", states["bizhawk"].Core);
        Assert.Equal("bizhawk:NesHawk:0", states["bizhawk"].Slot);

        Assert.Equal("ps2", states["pcsx2"].System);
        Assert.Equal(string.Empty, states["pcsx2"].Core);
        Assert.Equal("pcsx2::3", states["pcsx2"].Slot);
    }

    [Fact]
    public void Two_cores_of_one_emulator_are_two_states_of_one_rom()
    {
        using var tree = StateTree.Create();
        tree.AddRom(7, "snes", "ActRaiser (USA).zip");
        tree.AddState("snes/libretro.snes9x", "ActRaiser (USA).state1", "snes9x bytes");
        tree.AddState("snes/libretro.bsnes", "ActRaiser (USA).state1", "bsnes bytes");

        tree.Scan();

        var states = tree.Store.States.List();

        Assert.Equal(2, states.Count);
        Assert.All(states, state => Assert.Equal(7, state.RomId));

        // The same ROM at the same slot number under two cores. The server keys a state on
        // (rom_id, file_name) alone, so these two would collapse into one row if the uploaded
        // name did not carry the core.
        Assert.Equal(
            ["libretro:bsnes:1", "libretro:snes9x:1"],
            states.Select(state => state.Slot).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_state_whose_own_folder_holds_no_rom_of_that_name_stays_unattributed()
    {
        // The fail-closed direction, and the same rule M6 stage 1's review forced onto battery
        // saves: guessing across systems is what this key exists to prevent.
        using var tree = StateTree.Create();
        tree.AddRom(1, "nes", "Contra (USA).zip");
        tree.AddState("snes/libretro.snes9x", "Contra (USA).state1", "bytes");

        var outcome = tree.Scan();

        Assert.Equal(1, outcome.Found);
        Assert.Equal(0, outcome.Attributed);
        Assert.Null(Assert.Single(tree.Store.States.List()).RomId);
    }

    [Fact]
    public void A_battery_save_sitting_in_a_state_directory_is_not_a_state()
    {
        // DeSmuME declares {{romfilename}}.ds{{slot0}} and writes its battery save as
        // {{romfilename}}.dsv in the same tree. The one-digit anchor is what separates them.
        using var tree = StateTree.Create();
        tree.AddRom(3, "nds", "Game (USA).zip");
        tree.AddState("nds/DeSmuME/stateslots", "Game (USA).ds1", "a state");
        tree.AddState("nds/DeSmuME/stateslots", "Game (USA).dsv", "a battery save");

        tree.Scan();

        var state = Assert.Single(tree.Store.States.List());

        Assert.Equal("saves/nds/DeSmuME/stateslots/Game (USA).ds1", state.Path.Value);
    }

    [Fact]
    public void A_screenshot_beside_a_state_is_carried_and_an_empty_one_is_not()
    {
        using var tree = StateTree.Create();
        tree.AddRom(1, "ps2", "Real (USA).iso");
        tree.AddRom(2, "ps2", "Empty (USA).iso");
        tree.AddRom(3, "ps2", "Absent (USA).iso");

        tree.AddState("ps2/pcsx2", "Real (USA).01.p2s", "state");
        tree.AddState("ps2/pcsx2", "Real (USA).01.p2s.png", "png bytes");

        tree.AddState("ps2/pcsx2", "Empty (USA).01.p2s", "state");
        tree.AddState("ps2/pcsx2", "Empty (USA).01.p2s.png", string.Empty);

        tree.AddState("ps2/pcsx2", "Absent (USA).01.p2s", "state");

        var outcome = tree.Scan();

        // All three states, one screenshot. Correct, zero-byte and absent were all observed
        // across three saves of one game, and the server accepts a zero-byte screenshot and
        // stores it, so refusing it here is the only place it gets refused.
        Assert.Equal(3, outcome.Found);
        Assert.Equal(1, outcome.Screenshots);

        var byRom = tree.Store.States.List().ToDictionary(state => state.RomId!.Value);

        Assert.EndsWith("Real (USA).01.p2s.png", byRom[1].ScreenshotPath!.Value.Value, StringComparison.Ordinal);
        Assert.Null(byRom[2].ScreenshotPath);
        Assert.Null(byRom[3].ScreenshotPath);
    }

    [Fact]
    public void Desmume_never_offers_a_screenshot_because_its_image_is_its_state()
    {
        using var tree = StateTree.Create();
        tree.AddRom(1, "nds", "Game (USA).zip");
        tree.AddState("nds/DeSmuME/stateslots", "Game (USA).ds1", "state bytes");

        var outcome = tree.Scan();

        // <image> and <file> are the identical template, so the only file that could be offered
        // as the screenshot is the state itself.
        Assert.Equal(1, outcome.Found);
        Assert.Equal(0, outcome.Screenshots);
        Assert.Null(Assert.Single(tree.Store.States.List()).ScreenshotPath);
    }

    [Fact]
    public void The_txt_sidecar_is_read_as_the_native_name_and_is_not_itself_a_state()
    {
        using var tree = StateTree.Create();
        tree.AddRom(1, "psx", "Metal Gear Solid (USA) (Disc 1).cue");
        tree.AddState("psx/duckstation", "Metal Gear Solid (USA) (Disc 1)_01.sav", "state");
        tree.AddState("psx/duckstation", "Metal Gear Solid (USA) (Disc 1).txt", "SLUS-00594\n");

        var outcome = tree.Scan();

        Assert.Equal(1, outcome.Found);

        // The serial that class C and D attribution would otherwise read out of a ROM header,
        // collected here because RetroBat writes it unprompted.
        Assert.Equal("SLUS-00594", Assert.Single(tree.Store.States.List()).NativeName);
    }

    [Fact]
    public void An_autosave_takes_its_own_slot_rather_than_slot_zero()
    {
        using var tree = StateTree.Create();
        tree.AddRom(1, "snes", "ActRaiser (USA).zip");
        tree.AddState("snes/libretro.snes9x", "ActRaiser (USA).state.auto", "auto");
        tree.AddState("snes/libretro.snes9x", "ActRaiser (USA).state", "slot zero");

        tree.Scan();

        Assert.Equal(
            ["libretro:snes9x:0", "libretro:snes9x:auto"],
            tree.Store.States.List().Select(state => state.Slot).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void The_declared_directory_of_flycast_and_openmsx_finds_nothing_and_nothing_is_guessed()
    {
        using var tree = StateTree.Create();
        tree.AddRom(1, "dreamcast", "Bangai-O (USA).chd");

        // What a real install has: the declared directory present and empty, and the state
        // where the emulator actually writes it.
        Directory.CreateDirectory(tree.Install.Resolve(RelativePath.Create("saves/dreamcast/flycast/sstates")));
        tree.AddState("dreamcast/reicast/states", "Bangai-O (USA)_1.state", "the real state");

        var outcome = tree.Scan();

        // Zero, and deliberately zero: reading the wrong tree is worse than reading none, and
        // an empty declared directory means "you are looking in the wrong place".
        Assert.Equal(0, outcome.Found);

        Assert.Contains("flycast", StateScanner.WrongDeclaredDirectories.Keys, StringComparer.Ordinal);
        Assert.Contains("openmsx", StateScanner.WrongDeclaredDirectories.Keys, StringComparer.Ordinal);
    }

    [Fact]
    public void A_state_whose_file_is_gone_stops_being_recorded()
    {
        using var tree = StateTree.Create();
        tree.AddRom(1, "snes", "ActRaiser (USA).zip");
        tree.AddState("snes/libretro.snes9x", "ActRaiser (USA).state1", "bytes");

        tree.Scan();
        Assert.Single(tree.Store.States.List());

        File.Delete(tree.Install.Resolve(RelativePath.Create("saves/snes/libretro.snes9x/ActRaiser (USA).state1")));

        var outcome = tree.Scan();

        Assert.Equal(1, outcome.Forgotten);
        Assert.Empty(tree.Store.States.List());
    }

    [Fact]
    public void A_rescan_does_not_forget_that_a_state_was_uploaded()
    {
        using var tree = StateTree.Create();
        tree.AddRom(1, "snes", "ActRaiser (USA).zip");
        tree.AddState("snes/libretro.snes9x", "ActRaiser (USA).state1", "bytes");
        tree.Scan();

        var scanned = Assert.Single(tree.Store.States.List());
        var path = scanned.Path;
        var uploaded = scanned.ContentHash!;

        tree.Store.States.MarkUploaded(
            path, 115, "ActRaiser (USA) [libretro.snes9x].state1", uploaded, DateTimeOffset.UnixEpoch);

        tree.Scan();

        var state = Assert.Single(tree.Store.States.List());

        // Forgetting this would re-send every state on every sync, and the server's upsert
        // would accept every one without complaint.
        Assert.Equal(115, state.StateId);
        Assert.Equal(uploaded, state.UploadedContentHash);
        Assert.Equal("ActRaiser (USA) [libretro.snes9x].state1", state.UploadedFileName);
        Assert.False(state.NeedsUpload);
    }

    [Fact]
    public void A_state_changed_since_its_upload_needs_sending_again()
    {
        using var tree = StateTree.Create();
        tree.AddRom(1, "snes", "ActRaiser (USA).zip");
        tree.AddState("snes/libretro.snes9x", "ActRaiser (USA).state1", "first");
        tree.Scan();

        var path = Assert.Single(tree.Store.States.List()).Path;
        var hash = tree.Store.States.List()[0].ContentHash!;
        tree.Store.States.MarkUploaded(path, 115, "x.state1", hash, DateTimeOffset.UnixEpoch);

        Assert.False(Assert.Single(tree.Store.States.List()).NeedsUpload);

        tree.AddState("snes/libretro.snes9x", "ActRaiser (USA).state1", "second");
        tree.Scan();

        // mtime decides nothing for any class, so this is the content hash and only the
        // content hash.
        Assert.True(Assert.Single(tree.Store.States.List()).NeedsUpload);
    }

    [Fact]
    public void An_unattributed_state_is_never_offered_for_upload()
    {
        using var tree = StateTree.Create();
        tree.AddState("snes/libretro.snes9x", "Nothing Here (USA).state1", "bytes");

        tree.Scan();

        var state = Assert.Single(tree.Store.States.List());

        Assert.Null(state.RomId);
        Assert.False(state.NeedsUpload);
    }

    [Fact]
    public void Every_path_a_scan_records_is_relative_and_under_saves()
    {
        using var tree = StateTree.Create();
        tree.AddRom(1, "ps2", "Game (USA).iso");
        tree.AddState("ps2/pcsx2", "Game (USA).01.p2s", "state");
        tree.AddState("ps2/pcsx2", "Game (USA).01.p2s.png", "png");

        tree.Scan();

        foreach (var state in tree.Store.States.List())
        {
            Assert.StartsWith("saves/", state.Path.Value, StringComparison.Ordinal);
            Assert.DoesNotContain('\\', state.Path.Value);
            Assert.False(Path.IsPathRooted(state.Path.Value));

            if (state.ScreenshotPath is { } screenshot)
            {
                Assert.StartsWith("saves/", screenshot.Value, StringComparison.Ordinal);
                Assert.False(Path.IsPathRooted(screenshot.Value));
            }
        }
    }

    /// <summary>A temp install with a state tree, a ROM index and a scanner.</summary>
    private sealed class StateTree : IDisposable
    {
        private readonly TempRetroBatTree _tree;

        private StateTree(TempRetroBatTree tree, RetroBatInstall install, LocalStore store)
        {
            _tree = tree;
            Install = install;
            Store = store;
        }

        public RetroBatInstall Install { get; }

        public LocalStore Store { get; }

        public static StateTree Create()
        {
            var tree = TempRetroBatTree.Create();
            var install = tree.Install();
            return new StateTree(tree, install, LocalStore.Open(install));
        }

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

        public void AddState(string directory, string fileName, string contents)
        {
            var absolute = Install.Resolve(RelativePath.Create($"saves/{directory}/{fileName}"));
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllText(absolute, contents);
        }

        public StateScanOutcome Scan() =>
            new StateScanner(Install, Store, Fixtures.LoadSaveStates()).Scan();

        public void Dispose()
        {
            Store.Dispose();
            _tree.Dispose();
        }
    }
}
