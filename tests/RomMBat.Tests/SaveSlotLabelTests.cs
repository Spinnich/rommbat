using RomMBat.Core.Sync;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The words a player reads in place of a slot such as <c>libretro:battery</c>.
/// </summary>
/// <remarks>
/// Each form here is one RomMBat writes: <c>SaveScanner.SlotFor</c> and
/// <c>BatteryRule.SlotOf</c> for battery saves, <c>SaveStateMatch.SlotKey</c> for states, and a
/// unit path's or conversion's <c>slot</c> in <c>save_shapes.json</c> for the rest.
/// </remarks>
public class SaveSlotLabelTests
{
    [Theory]
    [InlineData("libretro:battery", "Battery save")]
    [InlineData("mednafen:battery", "Battery save")]
    [InlineData("pcsx2:battery", "Battery save")]
    [InlineData("libretro:battery:rtc", "Battery save (rtc)")]
    [InlineData("melonds:battery:dsv", "Battery save (dsv)")]
    [InlineData("libretro:battery:japan.srm", "Battery save (japan.srm)")]
    [InlineData("libretro:snes9x:3", "Save state 3")]
    [InlineData("libretro:snes9x:0", "Save state 0")]
    [InlineData("libretro:snes9x:auto", "Autosave state")]
    [InlineData("pcsx2::2", "Save state 2")]
    [InlineData("dolphin-emu:gci", "Memory card")]
    [InlineData("dolphin-emu:nand", "Console memory")]
    [InlineData("mame:nvram", "Machine memory")]
    [InlineData("rpcs3:savedata", "Save data")]
    [InlineData("ppsspp:savedata", "Save data")]
    public void Each_slot_RomMBat_writes_reads_as_plain_words(string slot, string expected) =>
        Assert.Equal(expected, SaveSlotLabel.Describe(slot));

    [Theory]
    [InlineData("libretro:something")]
    [InlineData("libretro")]
    [InlineData("a:b:c:d")]
    public void A_slot_it_does_not_know_is_a_save_named_by_what_follows_the_emulator(string slot) =>
        Assert.StartsWith("Save (", SaveSlotLabel.Describe(slot), StringComparison.Ordinal);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void No_slot_is_a_save_with_no_name(string? slot) =>
        Assert.Equal("Save", SaveSlotLabel.Describe(slot));

    [Fact]
    public void A_label_never_shows_the_raw_slot()
    {
        foreach (var slot in new[] { "libretro:battery", "libretro:snes9x:3", "dolphin-emu:gci" })
        {
            Assert.DoesNotContain(slot, SaveSlotLabel.Describe(slot), StringComparison.Ordinal);
            Assert.DoesNotContain(":", SaveSlotLabel.Describe(slot), StringComparison.Ordinal);
        }
    }
}
