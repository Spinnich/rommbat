using RomMBat.Core.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Battery saves an emulator names after its own title for the game, and the per-(system,
/// emulator) rule table that says whose a battery save is.
/// </summary>
/// <remarks>
/// Issue #151, measured on nes under bizhawk with both cores: <c>StarTropics (USA).zip</c> wrote
/// <c>saves/nes/bizhawk/StarTropics.SaveRAM</c>, and the state sidecar beside
/// <c>StarTropics (USA).QuickSave0.State</c> reads <c>StarTropics.NesHawk</c>. The trees here are
/// that layout.
/// </remarks>
public class DisplayNameSaveTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_bundled_rules_give_each_file_one_owner()
    {
        var shapes = SaveShapes.Bundled;

        Assert.Equal("libretro", shapes.BatteryRuleFor("nes", string.Empty, "Kirby's Adventure (USA) (Rev 1).srm")?.Emulator);
        Assert.Equal("libretro", shapes.BatteryRuleFor("saturn", string.Empty, "Battle Garegga (Japan).BKR")?.Emulator);

        var bizhawk = shapes.BatteryRuleFor("nes", "bizhawk", "StarTropics.SaveRAM");
        Assert.NotNull(bizhawk);
        Assert.Equal(BatteryNaming.DisplayName, bizhawk.NamedAfter);
        Assert.Equal(SaveShapeClass.A, bizhawk.Class);
        Assert.Same(bizhawk, shapes.BatteryRuleForSlot("nes", "bizhawk:battery"));

        // Measured on nes only, so snes under bizhawk stays reported rather than guessed at.
        Assert.Null(shapes.BatteryRuleFor("snes", "bizhawk", "Super Metroid.SaveRAM"));

        // #152, and the two names measured on nes: the hash on the stem is what makes a loose
        // .sav mednafen's, and without one it is mesen standalone's. Never libretro's.
        Assert.Equal("mesen", shapes.BatteryRuleFor("nes", string.Empty, "Crystalis (USA).sav")?.Emulator);
        var mednafen = shapes.BatteryRuleFor("nes", string.Empty, "Final Fantasy (USA).24ae5edf8375162f91a6846d3202e3d6.sav");
        Assert.Equal("mednafen", mednafen?.Emulator);
        Assert.Equal("Final Fantasy (USA)", mednafen!.RomStemOf("Final Fantasy (USA).24ae5edf8375162f91a6846d3202e3d6.sav"));
        Assert.Null(shapes.BatteryRuleFor("snes", string.Empty, "Crystalis (USA).sav"));

        Assert.Equal("jgenesis", shapes.BatteryRuleFor("nes", "jgenesis/nes", "Wizardry (USA).sav")?.Emulator);
        Assert.Equal("ares", shapes.BatteryRuleFor("nes", "ares/Famicom", "Dragon Warrior IV (USA).ram")?.Emulator);

        // megadrive, measured on Sonic & Knuckles + Sonic 3: jgenesis and ares name their own
        // subdirectory after their own system, bizhawk and mednafen keep nes's layout.
        const string Sonic3k = "Sonic & Knuckles + Sonic The Hedgehog 3 (USA) (Lock-on Combination)";
        Assert.Equal("jgenesis", shapes.BatteryRuleFor("megadrive", "jgenesis/md", $"{Sonic3k}.sav")?.Emulator);
        Assert.Null(shapes.BatteryRuleFor("megadrive", "jgenesis/nes", $"{Sonic3k}.sav"));
        Assert.Equal("ares", shapes.BatteryRuleFor("megadrive", "ares/Mega Drive", $"{Sonic3k}.ram")?.Emulator);
        Assert.Null(shapes.BatteryRuleFor("megadrive", "ares/Famicom", $"{Sonic3k}.ram"));
        Assert.Equal("bizhawk", shapes.BatteryRuleFor("megadrive", "bizhawk", "Sonic and Knuckles & Sonic 3 (W) [!].SaveRAM")?.Emulator);
        Assert.Equal(
            "mednafen",
            shapes.BatteryRuleFor("megadrive", string.Empty, $"{Sonic3k}.c5b1c655c19f462ade0ac4e17a844d10.sav")?.Emulator);
        Assert.Equal("libretro", shapes.BatteryRuleFor("megadrive", string.Empty, $"{Sonic3k}.srm")?.Emulator);

        // mesen standalone is not a megadrive emulator, so a plain loose .sav there has no owner.
        Assert.Null(shapes.BatteryRuleFor("megadrive", string.Empty, $"{Sonic3k}.sav"));

        // A loose save named after the ROM needs no rule to place it, so the slot lookup leaves
        // libretro and mesen out; mednafen's needs the hash, so it stays in.
        Assert.Null(shapes.BatteryRuleForSlot("nes", "libretro:battery"));
        Assert.Null(shapes.BatteryRuleForSlot("nes", "mesen:battery"));
        Assert.Same(mednafen, shapes.BatteryRuleForSlot("nes", "mednafen:battery"));
    }

    [Fact]
    public void The_bundled_gba_rules_give_each_of_emeralds_saves_one_owner()
    {
        // Pokemon - Emerald Version (USA, Europe), driven under every gba row on 8.2.1.
        var shapes = SaveShapes.Bundled;
        const string Rom = "Pokemon - Emerald Version (USA, Europe)";
        const string Md5 = "605b89b67018abcea91e693a4dd25be3";

        // Three emulators on one loose .sav, told apart by how narrow the name is.
        var member = shapes.BatteryRuleFor("gba", string.Empty, $"{Rom}.zip#{Rom}.{Md5}.sav");
        Assert.Equal("libretro", member?.Emulator);
        Assert.Equal(BatteryNaming.ArchiveMemberAndContentMd5, member!.NamedAfter);
        Assert.Equal(Rom, member.RomStemOf($"{Rom}.zip#{Rom}.{Md5}.sav"));
        Assert.Equal("libretro:battery:sav", SaveScanner.SlotFor("libretro", member.Class!.Value, ".sav"));

        // A .7z is named the same way, and a bare .gba gets mednafen standalone's own name.
        Assert.Same(member, shapes.BatteryRuleFor("gba", string.Empty, $"{Rom}.7z#{Rom}.{Md5}.sav"));
        Assert.Equal(Rom, member.RomStemOf($"{Rom}.7z#{Rom}.{Md5}.sav"));

        Assert.Equal("mednafen", shapes.BatteryRuleFor("gba", string.Empty, $"{Rom}.{Md5}.sav")?.Emulator);
        Assert.Equal("mgba", shapes.BatteryRuleFor("gba", string.Empty, $"{Rom}.sav")?.Emulator);
        Assert.Equal("libretro", shapes.BatteryRuleFor("gba", string.Empty, $"{Rom}.srm")?.Emulator);
        Assert.Equal("mesen", shapes.BatteryRuleFor("gba", string.Empty, $"{Rom}.rtc")?.Emulator);

        Assert.Equal("jgenesis", shapes.BatteryRuleFor("gba", "jgenesis/gba", $"{Rom}.rtc")?.Emulator);
        Assert.Equal("ares", shapes.BatteryRuleFor("gba", "ares/Game Boy Advance", $"{Rom}.flash")?.Emulator);
        Assert.Equal("bizhawk", shapes.BatteryRuleFor("gba", "bizhawk", $"{Rom}.SaveRAM")?.Emulator);

        // The two libretro slots on gba belong to different rules, and only the named one needs
        // its rule to be placed.
        Assert.Same(member, shapes.BatteryRuleForSlot("gba", "libretro:battery:sav"));
        Assert.Null(shapes.BatteryRuleForSlot("gba", "libretro:battery"));

        // A zip member is gba's alone, and mgba is not a nes emulator.
        Assert.Equal("mednafen", shapes.BatteryRuleFor("nes", string.Empty, $"{Rom}.zip#{Rom}.{Md5}.sav")?.Emulator);
        Assert.Equal("mesen", shapes.BatteryRuleFor("nes", string.Empty, $"{Rom}.sav")?.Emulator);
    }

    [Fact]
    public void A_zip_member_save_is_claimed_when_the_rom_name_carries_a_hash_sign()
    {
        // What a restore names it: the zip's file name, '#', the member's stem, the hash.
        var shapes = SaveShapes.Bundled;
        const string Rom = "Foo #1";
        const string Save = $"{Rom}.zip#{Rom}.605b89b67018abcea91e693a4dd25be3.sav";

        var member = shapes.BatteryRuleFor("gba", string.Empty, Save);
        Assert.Equal(BatteryNaming.ArchiveMemberAndContentMd5, member?.NamedAfter);
        Assert.Equal(Rom, member!.RomStemOf(Save));

        // A bare ROM with a '#' is still mednafen standalone's.
        Assert.Equal("mednafen", shapes.BatteryRuleFor("gba", string.Empty, $"{Rom}.605b89b67018abcea91e693a4dd25be3.sav")?.Emulator);
    }

    [Fact]
    public void The_bundled_gb_rules_give_each_of_yellows_saves_one_owner()
    {
        // Pokemon - Yellow Version, driven under every gb row on 8.2.1.
        var shapes = SaveShapes.Bundled;
        const string Rom = "Pokemon - Yellow Version - Special Pikachu Edition (USA, Europe) (CGB+SGB Enhanced)";
        const string Md5 = "d9290db87b1f0a23b89f99ee4469e34b";

        // The .srm six libretro cores and Mesen share, the .sav mGBA and mednafen share, and
        // mednafen's own hashed name when no plain one is there.
        Assert.Equal("libretro", shapes.BatteryRuleFor("gb", string.Empty, $"{Rom}.srm")?.Emulator);
        Assert.Equal("mgba", shapes.BatteryRuleFor("gb", string.Empty, $"{Rom}.sav")?.Emulator);
        Assert.Equal("mednafen", shapes.BatteryRuleFor("gb", string.Empty, $"{Rom}.{Md5}.sav")?.Emulator);

        Assert.Equal("ares", shapes.BatteryRuleFor("gb", "ares/Game Boy", $"{Rom}.ram")?.Emulator);
        Assert.Equal("jgenesis", shapes.BatteryRuleFor("gb", "jgenesis/gb", $"{Rom}.sav")?.Emulator);
        Assert.Equal("bizhawk", shapes.BatteryRuleFor("gb", "bizhawk", "Pokemon - Yellow Version (USA, Europe).SaveRAM")?.Emulator);

        // The loose .rtc is libretro's second file on gb, beside the .srm in its own slot, and
        // Mesen's on gba. A clock cartridge keeps its clock there under the stock core.
        var rtc = shapes.BatteryRuleFor("gb", string.Empty, "Pokemon - Silver Version (USA, Europe) (SGB Enhanced) (GB Compatible).rtc");
        Assert.Equal("libretro", rtc?.Emulator);
        Assert.Equal("libretro:battery:rtc", SaveScanner.SlotFor("libretro", rtc!.Class!.Value, ".rtc"));
        Assert.Null(shapes.BatteryRuleForSlot("gb", "libretro:battery:rtc"));
        Assert.Equal("mesen", shapes.BatteryRuleFor("gba", string.Empty, $"{Rom}.rtc")?.Emulator);
    }

    [Fact]
    public void The_bundled_gbc_rules_give_each_of_crystals_saves_and_clocks_one_owner()
    {
        // Pokemon - Crystal Version, an MBC3 cartridge with a clock, driven under every gbc row
        // on 8.2.1.
        var shapes = SaveShapes.Bundled;
        const string Rom = "Pokemon - Crystal Version (USA, Europe) (Rev 1)";

        // The .srm and .rtc four libretro cores and Mesen share, the .sav mGBA and mednafen
        // share, and mednafen's own hashed name when no plain one is there.
        Assert.Equal("libretro", shapes.BatteryRuleFor("gbc", string.Empty, $"{Rom}.srm")?.Emulator);
        Assert.Equal("libretro", shapes.BatteryRuleFor("gbc", string.Empty, $"{Rom}.rtc")?.Emulator);
        Assert.Equal("mgba", shapes.BatteryRuleFor("gbc", string.Empty, $"{Rom}.sav")?.Emulator);
        Assert.Equal("mednafen", shapes.BatteryRuleFor("gbc", string.Empty, $"{Rom}.301899b8087289a6436b0a241fbbb474.sav")?.Emulator);
        Assert.Equal("bizhawk", shapes.BatteryRuleFor("gbc", "bizhawk", "Pokemon - Crystal Version (USA, Europe) (Rev A).SaveRAM")?.Emulator);

        // ares and jgenesis each keep the clock in a second file, in its own slot.
        var ares = shapes.BatteryRuleFor("gbc", "ares/Game Boy", $"{Rom}.ram")!;
        Assert.Equal("ares", ares.Emulator);
        Assert.True(ares.OwnsSlot("ares:battery:rtc"));
        Assert.Equal("ares", shapes.BatteryRuleFor("gbc", "ares/Game Boy", $"{Rom}.rtc")?.Emulator);
        Assert.Null(shapes.BatteryRuleFor("gbc", "ares/Game Boy Color", $"{Rom}.ram"));

        var jgenesis = shapes.BatteryRuleFor("gbc", "jgenesis/gbc", $"{Rom}.sav")!;
        Assert.Equal("jgenesis", jgenesis.Emulator);
        Assert.True(jgenesis.OwnsSlot("jgenesis:battery:rtc"));

        // gb's jgenesis rule still reads jgenesis/gb only.
        Assert.Null(shapes.BatteryRuleFor("gb", "jgenesis/gbc", $"{Rom}.sav"));
    }

    [Fact]
    public void Ares_on_gb_keeps_its_ram_slot_and_takes_a_clock_slot_beside_it()
    {
        // Pokemon Silver, a clock cartridge in the gb folder under ares on 8.2.1, wrote a .rtc
        // beside the .ram. The .ram keeps the slot Yellow's went up under.
        var shapes = SaveShapes.Bundled;
        const string Silver = "Pokemon - Silver Version (USA, Europe) (SGB Enhanced) (GB Compatible)";

        var ram = shapes.BatteryRuleFor("gb", "ares/Game Boy", $"{Silver}.ram")!;
        var rtc = shapes.BatteryRuleFor("gb", "ares/Game Boy", $"{Silver}.rtc")!;

        Assert.Equal("ares:battery", SaveScanner.SlotFor("ares", ram.Class!.Value, ".ram"));
        Assert.Equal("ares:battery:rtc", SaveScanner.SlotFor("ares", rtc.Class!.Value, ".rtc"));
    }

    [Fact]
    public void One_emulator_may_hold_two_rules_on_a_system_only_where_class_b_keeps_the_slots_apart()
    {
        var shapes = SaveShapes.Parse(
            """{ "shapes": {} }""",
            Rules(
                """{ "emulator": "libretro", "directory": "", "extensions": [".srm"], "named_after": "rom file" }""",
                """{ "emulator": "libretro", "systems": ["gba"], "directory": "", "extensions": [".sav"], "named_after": "archive member and content md5", "class": "B" }"""));

        Assert.Equal("libretro", shapes.BatteryRuleFor("gba", string.Empty, "Game.zip#Game.0123456789abcdef0123456789abcdef.sav")?.Emulator);
        Assert.Null(shapes.BatteryRuleFor("gba", string.Empty, "Game.0123456789abcdef0123456789abcdef.sav"));

        // One extension in both would put two files in one slot.
        var shared = Assert.Throws<InvalidOperationException>(() => SaveShapes.Parse(
            """{ "shapes": {} }""",
            Rules(
                """{ "emulator": "libretro", "directory": "", "extensions": [".srm", ".sav"], "named_after": "rom file" }""",
                """{ "emulator": "libretro", "systems": ["gba"], "directory": "", "extensions": [".sav"], "named_after": "archive member and content md5", "class": "B" }""")));
        Assert.Contains("two battery rules", shared.Message, StringComparison.Ordinal);

        // And neither being class B puts both under libretro:battery.
        var unslotted = Assert.Throws<InvalidOperationException>(() => SaveShapes.Parse(
            """{ "shapes": {} }""",
            Rules(
                """{ "emulator": "libretro", "directory": "", "extensions": [".srm"], "named_after": "rom file" }""",
                """{ "emulator": "libretro", "systems": ["gba"], "directory": "", "extensions": [".sav"], "named_after": "archive member and content md5", "class": "A" }""")));
        Assert.Contains("two battery rules", unslotted.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_per_extension_slot_belongs_only_to_a_rule_carrying_that_extension()
    {
        var jgenesis = SaveShapes.Bundled.BatteryRuleFor("gba", "jgenesis/gba", "Game.sav")!;

        Assert.True(jgenesis.OwnsSlot("jgenesis:battery:sav"));
        Assert.True(jgenesis.OwnsSlot("jgenesis:battery:rtc"));
        Assert.False(jgenesis.OwnsSlot("jgenesis:battery:srm"));
        Assert.False(jgenesis.OwnsSlot("ares:battery:rtc"));
    }

    [Fact]
    public void A_content_hash_rule_may_share_an_extension_with_a_plain_one_but_not_with_another()
    {
        var shared = SaveShapes.Parse(
            """{ "shapes": {} }""",
            Rules(
                """{ "emulator": "libretro", "directory": "", "extensions": [".srm"], "named_after": "rom file" }""",
                """{ "emulator": "mesen", "systems": ["nes"], "directory": "", "extensions": [".sav"], "named_after": "rom file" }""",
                """{ "emulator": "mednafen", "systems": ["nes"], "directory": "", "extensions": [".sav"], "named_after": "rom file and content md5" }"""));

        Assert.Equal("mednafen", shared.BatteryRuleFor("nes", string.Empty, "Game.0123456789abcdef0123456789abcdef.sav")?.Emulator);
        Assert.Equal("mesen", shared.BatteryRuleFor("nes", string.Empty, "Game.sav")?.Emulator);

        var error = Assert.Throws<InvalidOperationException>(() => SaveShapes.Parse(
            """{ "shapes": {} }""",
            Rules(
                """{ "emulator": "libretro", "directory": "", "extensions": [".srm"], "named_after": "rom file" }""",
                """{ "emulator": "mednafen", "systems": ["nes"], "directory": "", "extensions": [".sav"], "named_after": "rom file and content md5" }""",
                """{ "emulator": "other", "systems": ["nes"], "directory": "", "extensions": [".sav"], "named_after": "rom file and content md5" }""")));

        Assert.Contains("mednafen and other both claim .sav", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_rule_table_giving_one_file_two_owners_is_refused_at_load()
    {
        var error = Assert.Throws<InvalidOperationException>(() => SaveShapes.Parse(
            """{ "shapes": {} }""",
            Rules(
                """{ "emulator": "libretro", "directory": "", "extensions": [".srm", ".sav"], "named_after": "rom file" }""",
                """{ "emulator": "mesen", "systems": ["nes"], "directory": "", "extensions": [".sav"], "named_after": "rom file" }""")));

        Assert.Contains("libretro and mesen both claim .sav", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_rule_table_giving_one_emulator_two_rules_on_a_system_is_refused_at_load()
    {
        // Both would upload under bizhawk:battery, so one file would replace the other on the server.
        var error = Assert.Throws<InvalidOperationException>(() => SaveShapes.Parse(
            """{ "shapes": {} }""",
            Rules(
                """{ "emulator": "libretro", "directory": "", "extensions": [".srm"], "named_after": "rom file" }""",
                """{ "emulator": "bizhawk", "systems": ["nes"], "directory": "bizhawk", "extensions": [".saveram"], "named_after": "display name" }""",
                """{ "emulator": "bizhawk", "systems": ["nes", "snes"], "directory": "bizhawk/other", "extensions": [".saveram"], "named_after": "display name" }""")));

        Assert.Contains("two battery rules", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Two_rules_on_different_systems_do_not_collide()
    {
        var shapes = SaveShapes.Parse(
            """{ "shapes": {} }""",
            Rules(
                """{ "emulator": "libretro", "directory": "", "extensions": [".srm"], "named_after": "rom file" }""",
                """{ "emulator": "mesen", "systems": ["nes"], "directory": "", "extensions": [".sav"], "named_after": "rom file" }""",
                """{ "emulator": "mgba", "systems": ["gba"], "directory": "", "extensions": [".sav"], "named_after": "rom file" }"""));

        Assert.Equal("mesen", shapes.BatteryRuleFor("nes", string.Empty, ".sav")?.Emulator);
        Assert.Equal("mgba", shapes.BatteryRuleFor("gba", string.Empty, ".sav")?.Emulator);
        Assert.Null(shapes.BatteryRuleFor("snes", string.Empty, ".sav"));
    }

    [Fact]
    public void A_bizhawk_save_is_attributed_through_the_title_its_state_sidecar_names()
    {
        using var fixture = new TitleFixture();
        fixture.AddRom(12, "StarTropics (USA).zip");
        fixture.AddBizHawkState("StarTropics (USA)", "NesHawk", "StarTropics.NesHawk");
        fixture.Write("saves/nes/bizhawk/StarTropics.SaveRAM", "battery bytes");

        var outcome = fixture.Scan();

        Assert.Equal(1, outcome.Attributed);

        var save = Assert.Single(fixture.Store.Saves.List());
        Assert.Equal("saves/nes/bizhawk/StarTropics.SaveRAM", save.Path.Value);
        Assert.Equal("bizhawk", save.Emulator);
        Assert.Equal("bizhawk:battery", save.Slot);
        Assert.Equal(SaveShapeClass.A, save.ShapeClass);
        Assert.Equal(12, save.RomId);

        var binding = fixture.Store.GameIdBindings.Find("nes", "StarTropics.SaveRAM");
        Assert.NotNull(binding);
        Assert.Equal(12, binding.RomId);
        Assert.Equal(BindingSource.Sidecar, binding.LearnedFrom);

        // Carried, so the bizhawk directory is no longer reported as holding something deferred,
        // and the state directory below it was never the battery pass's to count.
        Assert.DoesNotContain(fixture.Store.Unsyncable.List(), entry => entry.System == "nes");
    }

    [Fact]
    public void Bizhawks_backup_of_the_previous_save_is_neither_synced_nor_reported()
    {
        // Measured on a real install: BizHawk left StarTropics.SaveRAM.bak, the save it replaced,
        // and counting it named bizhawk under "a shape no declaration covers".
        using var fixture = new TitleFixture();
        fixture.AddRom(12, "StarTropics (USA).zip");
        fixture.AddBizHawkState("StarTropics (USA)", "NesHawk", "StarTropics.NesHawk");
        fixture.Write("saves/nes/bizhawk/StarTropics.SaveRAM", "the save now");
        fixture.Write("saves/nes/bizhawk/StarTropics.SaveRAM.bak", "the save before");

        fixture.Scan();

        Assert.Equal("saves/nes/bizhawk/StarTropics.SaveRAM", Assert.Single(fixture.Store.Saves.List()).Path.Value);
        Assert.DoesNotContain(fixture.Store.Unsyncable.List(), entry => entry.System == "nes");
    }

    [Fact]
    public void A_title_two_roms_answer_to_is_refused_rather_than_given_to_either()
    {
        // The maintainer's case on #151: both regions played under BizHawk, one file between them.
        using var fixture = new TitleFixture();
        fixture.AddRom(12, "StarTropics (USA).zip");
        fixture.AddRom(13, "StarTropics (Europe).zip");
        fixture.AddBizHawkState("StarTropics (USA)", "NesHawk", "StarTropics.NesHawk");
        fixture.AddBizHawkState("StarTropics (Europe)", "NesHawk", "StarTropics.NesHawk");
        fixture.Write("saves/nes/bizhawk/StarTropics.SaveRAM", "whichever region wrote last");

        var outcome = fixture.Scan();

        Assert.Equal(0, outcome.Attributed);
        Assert.Null(Assert.Single(fixture.Store.Saves.List()).RomId);

        var row = Assert.Single(
            fixture.Store.Unsyncable.List(),
            entry => entry.System == "nes" && entry.Reason == UnsyncableReason.Unattributed);
        Assert.Equal("bizhawk", row.Emulator);
        Assert.Contains("more than one game answers to StarTropics.SaveRAM", row.Detail, StringComparison.Ordinal);
        Assert.Contains("StarTropics (USA).zip", row.Detail, StringComparison.Ordinal);
        Assert.Contains("StarTropics (Europe).zip", row.Detail, StringComparison.Ordinal);

        var binding = fixture.Store.GameIdBindings.Find("nes", "StarTropics.SaveRAM");
        Assert.NotNull(binding);
        Assert.Null(binding.RomId);
        Assert.Equal(BindingSource.Contested, binding.LearnedFrom);
    }

    [Fact]
    public void A_bizhawk_launch_covering_the_write_attributes_a_save_with_no_state()
    {
        using var fixture = new TitleFixture();
        fixture.AddRom(14, "Ultima - Quest of the Avatar (USA).zip");

        var attribution = fixture.Attributor(
                Launch(Now.AddMinutes(-30), "Ultima - Quest of the Avatar (USA).zip", "bizhawk"))
            .Attribute("nes", BizHawk, "Ultima - Quest of the Avatar.SaveRAM", Now.AddMinutes(-5));

        Assert.Equal(14, attribution.RomId);
        Assert.Equal(BindingSource.Journal, attribution.Source);
        Assert.Contains("was running under bizhawk", attribution.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_launch_under_another_emulator_says_nothing_about_a_bizhawk_save()
    {
        // Only BizHawk writes a .SaveRAM, so a later libretro session of the same system cannot
        // have written it, however well its window fits.
        using var fixture = new TitleFixture();
        fixture.AddRom(14, "Ultima - Quest of the Avatar (USA).zip");

        var attribution = fixture.Attributor(
                Launch(Now.AddMinutes(-30), "Ultima - Quest of the Avatar (USA).zip", "libretro"))
            .Attribute("nes", BizHawk, "Ultima - Quest of the Avatar.SaveRAM", Now.AddMinutes(-5));

        Assert.Null(attribution.RomId);
        Assert.Equal(AttributionOutcome.NotFound, attribution.Outcome);

        // An absence is never cached, so the ROM being played later still attributes it.
        Assert.Null(fixture.Store.GameIdBindings.Find("nes", "Ultima - Quest of the Avatar.SaveRAM"));
    }

    [Fact]
    public void A_launch_another_launch_has_ended_does_not_claim_a_later_write()
    {
        // EmulationStation runs one game at a time, so the snes session starting is the bizhawk
        // session over. Measured on a real install: without this bound the newest bizhawk launch
        // claimed a write made eight days and many sessions later.
        using var fixture = new TitleFixture();
        fixture.AddRom(12, "StarTropics (USA).zip");

        var bizhawk = Launch(Now.AddHours(-3), "StarTropics (USA).zip", "bizhawk");
        var later = new LaunchRecord(
            Now.AddHours(-2),
            RelativePath.Create("roms/snes/ActRaiser (USA).zip"),
            "snes",
            "libretro",
            "snes9x",
            IsMenuLaunch: false,
            "later");

        var attribution = fixture.Attributor(bizhawk, later)
            .Attribute("nes", BizHawk, "StarTropics.SaveRAM", Now.AddMinutes(-5));

        Assert.Null(attribution.RomId);
        Assert.Equal(AttributionOutcome.NotFound, attribution.Outcome);
    }

    [Fact]
    public void A_restored_save_is_not_credited_to_whichever_bizhawk_session_came_last()
    {
        // The live failure. A restore writes the bytes now, so their mtime is later than every
        // launch, and the newest bizhawk launch (Ultima here, and the last launch of all, so the
        // session bound above cannot help) was credited with them. That contested the sidecar and
        // stopped a correctly restored save from ever syncing again. The row the restore wrote is
        // what says nobody has written to the file since.
        using var fixture = new TitleFixture();
        fixture.AddRom(12, "StarTropics (USA).zip");
        fixture.AddRom(14, "Ultima - Quest of the Avatar (USA).zip");
        fixture.AddBizHawkState("StarTropics (USA)", "NesHawk", "StarTropics.NesHawk");

        var played = Now.AddDays(-8);
        fixture.WriteLaunchLog(
            (played, "StarTropics (USA).zip", "bizhawk"),
            (played.AddHours(1), "Ultima - Quest of the Avatar (USA).zip", "bizhawk"));

        const string save = "saves/nes/bizhawk/StarTropics.SaveRAM";
        fixture.Write(save, "played under NesHawk");
        fixture.Touch(save, played.AddMinutes(10));

        fixture.Scan();
        Assert.Equal(12, Assert.Single(fixture.Store.Saves.List()).RomId);

        // What SaveSync.RecordRestored leaves: new bytes, written now, with their ROM.
        fixture.Write(save, "restored from the server");
        fixture.Touch(save, Now);
        var restored = Assert.Single(fixture.Store.Saves.List());
        fixture.Store.Saves.Record(
            restored with { ContentHash = LogicalContentHash.OfFile(fixture.Install.Resolve(save)) },
            Now);

        fixture.Scan();

        var rescanned = Assert.Single(fixture.Store.Saves.List());
        Assert.Equal(12, rescanned.RomId);
        Assert.Equal(12, fixture.Store.GameIdBindings.Find("nes", "StarTropics.SaveRAM")?.RomId);
        Assert.DoesNotContain(fixture.Store.Unsyncable.List(), entry => entry.System == "nes");
    }

    [Fact]
    public void A_second_rom_writing_a_bound_title_turns_the_binding_into_a_refusal()
    {
        // The cache is an answer here, not a short cut. USA teaches the binding; Europe, given
        // the same title, writes the same file later. Short-cutting on the cache would go on
        // uploading Europe's progress as USA's.
        using var fixture = new TitleFixture();
        fixture.AddRom(12, "StarTropics (USA).zip");
        fixture.AddRom(13, "StarTropics (Europe).zip");

        var usa = Launch(Now.AddHours(-3), "StarTropics (USA).zip", "bizhawk");
        var first = fixture.Attributor(usa).Attribute("nes", BizHawk, "StarTropics.SaveRAM", Now.AddHours(-2));
        Assert.Equal(12, first.RomId);

        var europe = Launch(Now.AddMinutes(-30), "StarTropics (Europe).zip", "bizhawk");
        var second = fixture.Attributor(usa, europe).Attribute("nes", BizHawk, "StarTropics.SaveRAM", Now.AddMinutes(-5));

        Assert.Null(second.RomId);
        Assert.Equal(AttributionOutcome.Contested, second.Outcome);
        Assert.Equal(BindingSource.Contested, fixture.Store.GameIdBindings.Find("nes", "StarTropics.SaveRAM")?.LearnedFrom);
    }

    [Fact]
    public void A_binding_a_person_made_settles_a_contested_title()
    {
        using var fixture = new TitleFixture();
        var usa = fixture.AddRom(12, "StarTropics (USA).zip");
        fixture.AddRom(13, "StarTropics (Europe).zip");

        fixture.Store.GameIdBindings.Record(new GameIdBinding(
            "nes", "StarTropics.SaveRAM", 12, usa, BindingSource.User, null, Now));

        var attribution = fixture.Attributor(Launch(Now.AddMinutes(-30), "StarTropics (Europe).zip", "bizhawk"))
            .Attribute("nes", BizHawk, "StarTropics.SaveRAM", Now.AddMinutes(-5));

        Assert.Equal(12, attribution.RomId);
        Assert.Equal(BindingSource.User, attribution.Source);
    }

    [Fact]
    public void A_title_with_a_dot_of_its_own_loses_only_the_core()
    {
        using var fixture = new TitleFixture();
        fixture.AddRom(15, "Dr. Mario (Japan, USA).zip");
        fixture.AddBizHawkState("Dr. Mario (Japan, USA)", "quickerNES", "Dr. Mario.quickerNES");

        new StateScanner(fixture.Install, fixture.Store, Fixtures.LoadSaveStatesAsLoaded()).Scan();

        var attribution = fixture.Attributor().Attribute("nes", BizHawk, "Dr. Mario.SaveRAM", written: null);

        Assert.Equal(15, attribution.RomId);
        Assert.Equal(BindingSource.Sidecar, attribution.Source);
    }

    [Fact]
    public void The_learned_title_is_what_a_download_is_named_with_and_two_are_refused()
    {
        using var fixture = new TitleFixture();
        var usa = fixture.AddRom(12, "StarTropics (USA).zip");

        Assert.Null(DisplayNameAttributor.LearnedTitle(fixture.Store, "nes", BizHawk, 12));

        fixture.Store.GameIdBindings.Record(new GameIdBinding(
            "nes", "StarTropics.SaveRAM", 12, usa, BindingSource.Sidecar, null, Now));

        Assert.Equal("StarTropics", DisplayNameAttributor.LearnedTitle(fixture.Store, "nes", BizHawk, 12));

        // A class C key under the same system is not a title, whatever ROM it names.
        fixture.Store.GameIdBindings.Record(new GameIdBinding(
            "nes", "SOMEKEY", 12, usa, BindingSource.RomHeader, null, Now));
        Assert.Equal("StarTropics", DisplayNameAttributor.LearnedTitle(fixture.Store, "nes", BizHawk, 12));

        // Two titles for one ROM: only one is the file BizHawk reads, and nothing says which.
        fixture.Store.GameIdBindings.Record(new GameIdBinding(
            "nes", "Star Tropics.SaveRAM", 12, usa, BindingSource.User, null, Now));
        Assert.Null(DisplayNameAttributor.LearnedTitle(fixture.Store, "nes", BizHawk, 12));
    }

    private static BatteryRule BizHawk => SaveShapes.Bundled.BatteryRuleForSlot("nes", "bizhawk:battery")!;

    private static string Rules(params string[] rules) =>
        $$"""{ "battery_saves": [{{string.Join(", ", rules)}}] }""";

    private static LaunchRecord Launch(DateTimeOffset at, string romFile, string emulator) =>
        new(at, RelativePath.Create($"roms/nes/{romFile}"), "nes", emulator, null, IsMenuLaunch: false, $"{at:O}|{romFile}");

    private sealed class TitleFixture : IDisposable
    {
        private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();

        public TitleFixture()
        {
            Install = _tree.Install();
            Store = LocalStore.Open(Install);
        }

        public RetroBatInstall Install { get; }

        public LocalStore Store { get; }

        public RelativePath AddRom(long romId, string fileName)
        {
            var path = RelativePath.Create($"roms/nes/{fileName}");
            Write(path.Value, "rom bytes");

            Store.Files.Record(new LocalFile
            {
                Path = path,
                Folder = "nes",
                RomId = (int)romId,
                Kind = LocalFileKind.Rom,
                FileName = fileName,
                SizeBytes = 9,
            });

            return path;
        }

        /// <summary>A state where RetroBat mirrors BizHawk's, with the sidecar it writes beside it.</summary>
        public void AddBizHawkState(string romStem, string core, string sidecar)
        {
            Write($"saves/nes/bizhawk/sstates/{core}/{romStem}.QuickSave0.State", "a state");
            Write($"saves/nes/bizhawk/sstates/{core}/{romStem}.txt", sidecar);
        }

        public void Write(string relative, string contents)
        {
            var absolute = Install.Resolve(RelativePath.Create(relative));
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllText(absolute, contents);
        }

        public void Touch(string relative, DateTimeOffset at) =>
            File.SetLastWriteTimeUtc(Install.Resolve(RelativePath.Create(relative)), at.UtcDateTime);

        /// <summary>Launches as emulatorLauncher.log records them, in the machine's local time.</summary>
        public void WriteLaunchLog(params (DateTimeOffset At, string RomFile, string Emulator)[] launches) =>
            Write(
                LaunchLog.LivePath.Value,
                string.Concat(launches.Select(launch =>
                    $"{launch.At.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff} [INFO]      [Startup] "
                        + "\"X:\\RetroBat\\emulationstation\\emulatorLauncher.exe\" -system nes "
                        + $"-emulator {launch.Emulator} -rom \"X:\\RetroBat\\roms\\nes\\{launch.RomFile}\"\r\n")));

        /// <summary>States first, as the flush orders them, so the sidecar route can see them.</summary>
        public SaveScanOutcome Scan()
        {
            var states = Fixtures.LoadSaveStatesAsLoaded();
            new StateScanner(Install, Store, states).Scan();
            return new SaveScanner(Install, Store, states: states).Scan();
        }

        public DisplayNameAttributor Attributor(params LaunchRecord[] launches) =>
            new(Store, RomIndex.Build(Store), launches, new TestTimeProvider(Now));

        public void Dispose()
        {
            Store.Dispose();
            _tree.Dispose();
        }
    }
}
