using System.Text;
using RomMBat.Core.RetroBat;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The <c>es_savestates.cfg</c> parser, driven against the bytes RetroBat ships.
/// </summary>
/// <remarks>
/// Every trap here is a real property of the shipped file rather than an invented edge case, so
/// the fixture is <c>reference/es_savestates.cfg</c> itself and not a synthesized document. A
/// change upstream that moves one of these is meant to fail here.
/// </remarks>
public class SaveStateSchemaTests
{
    [Fact]
    public void The_shipped_file_declares_thirteen_emulators()
    {
        var schema = Fixtures.LoadSaveStates();

        // The bound on state sync: 13 emulators, not the 243 systems es_systems.cfg declares.
        Assert.Equal(13, schema.Emulators.Count);
    }

    [Fact]
    public void Libretro_declares_no_slot_bounds_and_is_still_usable()
    {
        var libretro = Fixtures.LoadSaveStates().For("libretro");

        Assert.NotNull(libretro);
        Assert.Null(libretro.FirstSlot);
        Assert.Null(libretro.LastSlot);
        Assert.Equal((null, null), libretro.Bounds);

        // The bounds being absent costs nothing, because the slot is read off a filename rather
        // than expanded from a range.
        var template = SaveStateTemplate.Create(libretro, "snes", "snes9x");

        Assert.NotNull(template);
        Assert.Equal(3, template.Match("ActRaiser (USA).state3")?.Slot);
    }

    [Fact]
    public void Desmume_declares_an_image_identical_to_its_file_and_no_screenshot_is_offered()
    {
        var desmume = Fixtures.LoadSaveStates().For("desmume");

        Assert.NotNull(desmume);

        // The trap, stated as an assertion so it cannot quietly stop being true.
        Assert.Equal(desmume.File, desmume.Image);

        var template = SaveStateTemplate.Create(desmume, "nds", core: null);

        Assert.NotNull(template);

        // Uploading this as screenshotFile would upload the save state as its own preview.
        Assert.Null(template.ImageFor(template.Match("Game (USA).ds1")!));
    }

    [Fact]
    public void Desmumes_one_digit_slot_refuses_its_own_battery_save()
    {
        var desmume = Fixtures.LoadSaveStates().For("desmume")!;
        var template = SaveStateTemplate.Create(desmume, "nds", core: null)!;

        Assert.Equal(1, template.Match("Game (USA).ds1")?.Slot);

        // .dsv is DeSmuME's battery save sitting in the same tree. A one-character wildcard
        // would take it for slot "v" and upload a battery save as a save state.
        Assert.Null(template.Match("Game (USA).dsv"));
    }

    [Fact]
    public void Bigpemu_declares_three_digit_bounds_against_a_two_digit_template()
    {
        var bigpemu = Fixtures.LoadSaveStates().For("bigpemu");

        Assert.NotNull(bigpemu);
        Assert.Equal("001", bigpemu.FirstSlot);
        Assert.Equal("999", bigpemu.LastSlot);

        // Read as numbers regardless of the zero padding, so the declaration is legible even
        // though its upper bound cannot be written by its own two-digit filename rule.
        Assert.Equal((1, 999), bigpemu.Bounds);
        Assert.Equal(SlotToken.TwoDigit, bigpemu.Slot);

        var template = SaveStateTemplate.Create(bigpemu, "jaguar", core: null)!;

        Assert.Equal(7, template.Match("Rayman (USA)_state07.bigpstate")?.Slot);

        // Three digits cannot be produced by {{slot2d}}, so a name shaped like the declared
        // upper bound is not a state this rule recognises.
        Assert.Null(template.Match("Rayman (USA)_state999.bigpstate"));

        // The other end. Two digits reach 00, which the declaration excludes, and the slot is
        // read off the name regardless, because the file is evidence and the declaration is
        // only a claim. It is reported now rather than accepted in silence.
        Assert.Equal(0, template.Match("Rayman (USA)_state00.bigpstate")?.Slot);
        Assert.Equal(99, template.Match("Rayman (USA)_state99.bigpstate")?.Slot);
    }

    [Fact]
    public void A_bigpemu_name_that_misses_only_on_slot_width_is_reported_rather_than_dropped()
    {
        // #34. Measurement 166 drove six real states and found the mirror RetroBat writes under
        // saves/ is two-digit, so {{slot2d}} is right and this case is not the routine one #34
        // assumed. It is still the case past slot 99, which nothing has reached, and a name that
        // matches everything except the slot's width is not a sidecar or a screenshot.
        var bigpemu = Fixtures.LoadSaveStates().For("bigpemu")!;
        var template = SaveStateTemplate.Create(bigpemu, "jaguar", core: null)!;

        var miss = template.NearMiss("Rayman (USA)_state999.bigpstate");

        Assert.NotNull(miss);
        Assert.Equal(NearMissKind.SlotWidth, miss.Kind);
        Assert.Contains("999", miss.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_slot_below_the_declared_floor_is_reported_and_still_synced()
    {
        // #65: Bounds had no caller, so slot 0 was accepted, uploaded and never mentioned even
        // though bigpemu declares firstslot="001". Reported rather than refused.
        var bigpemu = Fixtures.LoadSaveStates().For("bigpemu")!;
        var template = SaveStateTemplate.Create(bigpemu, "jaguar", core: null)!;

        var miss = template.NearMiss("Rayman (USA)_state00.bigpstate");

        Assert.NotNull(miss);
        Assert.Equal(NearMissKind.SlotOutsideDeclaredBounds, miss.Kind);

        // Still a state, still read, still uploadable. The report is the whole change.
        Assert.Equal(0, template.Match("Rayman (USA)_state00.bigpstate")?.Slot);
    }

    [Fact]
    public void A_slot_inside_the_declared_bounds_says_nothing()
    {
        var bigpemu = Fixtures.LoadSaveStates().For("bigpemu")!;
        var template = SaveStateTemplate.Create(bigpemu, "jaguar", core: null)!;

        Assert.Null(template.NearMiss("Rayman (USA)_state07.bigpstate"));
        Assert.Null(template.NearMiss("Rayman (USA)_state99.bigpstate"));
    }

    [Fact]
    public void A_sidecar_or_a_screenshot_is_still_passed_over_without_a_word()
    {
        // The rule this deliberately does not relax. Only the slot widens; the rest of the
        // expression stays anchored and escaped, so a state directory's other contents are as
        // silent as they were.
        var bigpemu = Fixtures.LoadSaveStates().For("bigpemu")!;
        var template = SaveStateTemplate.Create(bigpemu, "jaguar", core: null)!;

        Assert.Null(template.NearMiss("Rayman (USA).txt"));
        Assert.Null(template.NearMiss("Rayman (USA)_state07.png"));
        Assert.Null(template.NearMiss("Rayman (USA)_state999.png"));
        Assert.Null(template.NearMiss("notes.md"));
    }

    [Fact]
    public void An_emulator_with_a_free_width_slot_has_no_near_miss_to_report()
    {
        // {{slot}} already accepts any number of digits, so there is no wider reading of it to
        // compare against, and libretro declares no bounds either.
        var libretro = Fixtures.LoadSaveStates().For("libretro")!;
        var template = SaveStateTemplate.Create(libretro, "snes", "snes9x")!;

        Assert.Null(template.NearMiss("ActRaiser (USA).state3"));
        Assert.Null(template.NearMiss("ActRaiser (USA).state123"));
        Assert.Null(template.NearMiss("ActRaiser (USA).state.auto"));
    }

    [Theory]
    [InlineData("libretro", true)]
    [InlineData("bizhawk", true)]
    [InlineData("pcsx2", false)]
    [InlineData("ppsspp", false)]
    public void Core_scoping_is_a_property_of_the_directory_template(string name, bool expected)
    {
        var emulator = Fixtures.LoadSaveStates().For(name);

        Assert.NotNull(emulator);
        Assert.Equal(expected, emulator.IsCoreScoped);
    }

    [Fact]
    public void A_core_scoped_emulator_without_a_core_expands_to_nothing()
    {
        var libretro = Fixtures.LoadSaveStates().For("libretro")!;

        // saves/snes/libretro./ is not a directory any emulator writes, so there is nothing to
        // scan rather than a directory to guess at.
        Assert.Null(SaveStateTemplate.Create(libretro, "snes", core: null));
        Assert.Null(SaveStateTemplate.Create(libretro, "snes", core: "   "));
    }

    [Fact]
    public void Two_cores_of_one_emulator_are_two_directories_and_two_slots()
    {
        var libretro = Fixtures.LoadSaveStates().For("libretro")!;

        var snes9x = SaveStateTemplate.Create(libretro, "snes", "snes9x")!;
        var bsnes = SaveStateTemplate.Create(libretro, "snes", "bsnes")!;

        Assert.Equal("saves/snes/libretro.snes9x", snes9x.Directory.Value);
        Assert.Equal("saves/snes/libretro.bsnes", bsnes.Directory.Value);

        // The same game at the same slot under two cores is two independent states, which is
        // why the core is part of the local slot rather than decoration.
        var match = snes9x.Match("ActRaiser (USA).state1")!;

        Assert.Equal("libretro:snes9x:1", match.SlotKey("libretro", "snes9x"));
        Assert.Equal("libretro:bsnes:1", match.SlotKey("libretro", "bsnes"));
    }

    [Fact]
    public void Bizhawks_core_is_the_last_directory_segment()
    {
        var bizhawk = Fixtures.LoadSaveStates().For("bizhawk")!;
        var template = SaveStateTemplate.Create(bizhawk, "nes", "NesHawk")!;

        Assert.Equal("saves/nes/bizhawk/sstates/NesHawk", template.Directory.Value);
        Assert.Equal(0, template.Match("Battle City.QuickSave0.State")?.Slot);
    }

    [Fact]
    public void The_two_wrong_declared_directories_are_declared_where_the_emulator_does_not_write()
    {
        var schema = Fixtures.LoadSaveStates();

        // Measured on a real install: flycast writes saves/dreamcast/reicast/states/ and
        // openmsx writes bios/openmsx/savestates/. Both declared directories exist and are
        // empty, so trusting the declaration finds nothing and reads that as "no states".
        var flycast = SaveStateTemplate.Create(schema.For("flycast")!, "dreamcast", core: null)!;
        Assert.Equal("saves/dreamcast/flycast/sstates", flycast.Directory.Value);
        Assert.NotEqual("saves/dreamcast/reicast/states", flycast.Directory.Value);

        var openmsx = SaveStateTemplate.Create(schema.For("openmsx")!, "msx1", core: null)!;
        Assert.Equal("saves/msx1/openmsx", openmsx.Directory.Value);

        // openMSX's real tree is not under saves/ at all, so no expansion of this template can
        // reach it. That is why it is reported rather than scanned.
        Assert.StartsWith("saves/", openmsx.Directory.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void An_autosave_is_matched_before_a_numbered_slot()
    {
        var libretro = Fixtures.LoadSaveStates().For("libretro")!;
        var template = SaveStateTemplate.Create(libretro, "snes", "snes9x")!;

        var auto = template.Match("ActRaiser (USA).state.auto");

        Assert.NotNull(auto);
        Assert.True(auto.IsAutosave);
        Assert.Null(auto.Slot);
        Assert.Equal("ActRaiser (USA)", auto.Stem);

        // Free-width {{slot}} matches zero digits, so without the autosave rule running first
        // this file would resolve to a ROM called "ActRaiser (USA).state" at slot zero.
        Assert.Equal("libretro::auto", auto.SlotKey("libretro", null));
    }

    [Fact]
    public void A_free_width_slot_that_renders_nothing_is_slot_zero()
    {
        var libretro = Fixtures.LoadSaveStates().For("libretro")!;
        var template = SaveStateTemplate.Create(libretro, "gb", "gambatte")!;

        // RetroArch's own convention writes the first slot with no digit at all. Whether
        // RetroBat renders it that way was not measured, so the parser accepts both rather
        // than depending on the answer.
        Assert.Equal(0, template.Match("Tetris (World).state")?.Slot);
        Assert.Equal(1, template.Match("Tetris (World).state1")?.Slot);
    }

    [Fact]
    public void The_stem_is_the_rom_filename_without_its_extension()
    {
        var ppsspp = Fixtures.LoadSaveStates().For("ppsspp")!;
        var template = SaveStateTemplate.Create(ppsspp, "psp", core: null)!;

        // Taken from the checked-in launch log, whose line 5 launches
        // "Patapon (Europe) (En,Fr,De,Es,It).cso" and whose state probe 2 recorded is
        // "Patapon (Europe) (En,Fr,De,Es,It)_0.ppst".
        var match = template.Match("Patapon (Europe) (En,Fr,De,Es,It)_0.ppst");

        Assert.NotNull(match);
        Assert.Equal("Patapon (Europe) (En,Fr,De,Es,It)", match.Stem);
        Assert.Equal(0, match.Slot);
    }

    [Fact]
    public void A_rom_whose_own_name_ends_in_the_suffix_still_resolves()
    {
        var pcsx2 = Fixtures.LoadSaveStates().For("pcsx2")!;
        var template = SaveStateTemplate.Create(pcsx2, "ps2", core: null)!;

        // The anchor at the end decides, so a stem that itself contains the literal suffix
        // does not shift the slot capture.
        var match = template.Match("Game.01.p2s.05.p2s");

        Assert.NotNull(match);
        Assert.Equal("Game.01.p2s", match.Stem);
        Assert.Equal(5, match.Slot);
    }

    [Fact]
    public void A_two_digit_slot_keeps_its_padding_when_the_image_is_expanded()
    {
        var pcsx2 = Fixtures.LoadSaveStates().For("pcsx2")!;
        var template = SaveStateTemplate.Create(pcsx2, "ps2", core: null)!;

        Assert.Equal(
            "Game (USA).03.p2s.png",
            template.ImageFor(template.Match("Game (USA).03.p2s")!));

        Assert.Equal(
            "Game (USA).resume.p2s.png",
            template.ImageFor(template.Match("Game (USA).resume.p2s")!));
    }

    [Fact]
    public void A_free_width_slot_renders_the_image_with_the_digits_that_were_on_disk()
    {
        // libretro is the only free-width declaration, and its <image> carries the same token, so
        // formatting the parsed slot back would look for Game.state0.png beside Game.state and
        // upload the state with no screenshot at all.
        var libretro = Fixtures.LoadSaveStates().For("libretro")!;
        var template = SaveStateTemplate.Create(libretro, "megadrive", "picodrive")!;

        var zero = template.Match("Game (USA).state")!;

        Assert.Equal(0, zero.Slot);
        Assert.Equal("Game (USA).state.png", template.ImageFor(zero));

        // And a slot that did render its digit still round-trips.
        Assert.Equal("Game (USA).state1.png", template.ImageFor(template.Match("Game (USA).state1")!));
    }

    [Fact]
    public void A_commented_out_core_element_is_absent_and_an_enabled_one_is_tolerated()
    {
        // The shipped file carries the mechanism only inside an XML comment, so nothing reads
        // one today.
        Assert.Empty(Fixtures.LoadSaveStates().For("libretro")!.Cores);

        // A user can uncomment it, and the sample overrides the system and the directory as
        // well as disabling a core, so all three are carried.
        var xml = """
            <savestates>
              <emulator name="libretro">
                <directory>{{system}}/libretro.{{core}}</directory>
                <file>{{romfilename}}.state{{slot}}</file>
                <core name="mesen" enabled="false"/>
                <core name="fceumm" system="nes" directory="{{system}}"/>
              </emulator>
            </savestates>
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        var cores = SaveStateSchema.Parse(stream).For("libretro")!.Cores;

        Assert.Equal(2, cores.Count);
        Assert.False(cores["mesen"].Enabled);
        Assert.True(cores["fceumm"].Enabled);
        Assert.Equal("nes", cores["fceumm"].System);
        Assert.Equal("{{system}}", cores["fceumm"].Directory);
    }

    [Fact]
    public void An_emulator_with_no_file_template_is_dropped()
    {
        // With no filename rule there is nothing to recognise a state by, and defaulting one is
        // how a client uploads a file that is not a save state.
        var xml = """
            <savestates>
              <emulator name="broken"><directory>{{system}}/broken</directory></emulator>
              <emulator name="fine">
                <directory>{{system}}/fine</directory>
                <file>{{romfilename}}.st{{slot0}}</file>
              </emulator>
            </savestates>
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        var schema = SaveStateSchema.Parse(stream);

        Assert.Null(schema.For("broken"));
        Assert.NotNull(schema.For("fine"));
    }

    [Fact]
    public void Every_shipped_emulator_expands_to_a_relative_path_under_saves()
    {
        foreach (var emulator in Fixtures.LoadSaveStates().Emulators)
        {
            var template = SaveStateTemplate.Create(emulator, "snes", emulator.IsCoreScoped ? "snes9x" : null);

            Assert.NotNull(template);
            Assert.StartsWith("saves/", template.Directory.Value, StringComparison.Ordinal);
            Assert.DoesNotContain('\\', template.Directory.Value);
            Assert.DoesNotContain("..", template.Directory.Value, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Libretro_and_gopher64_render_the_same_filename_for_one_rom()
    {
        var schema = Fixtures.LoadSaveStates();

        var libretro = SaveStateTemplate.Create(schema.For("libretro")!, "n64", "mupen64plus_next")!;
        var gopher64 = SaveStateTemplate.Create(schema.For("gopher64")!, "n64", core: null)!;

        // {{slot}} and {{slot0}} render identically for slots 1 to 9, and both emulators serve
        // n64, so one ROM played under each produces two states with the same filename in two
        // directories. The server keys a state on (rom_id, file_name) alone, so the uploaded
        // name has to carry the scope or the second silently replaces the first.
        Assert.Equal(1, libretro.Match("Dr. Mario 64 (USA).state1")?.Slot);
        Assert.Equal(1, gopher64.Match("Dr. Mario 64 (USA).state1")?.Slot);

        Assert.NotEqual(libretro.Directory, gopher64.Directory);
    }
}
