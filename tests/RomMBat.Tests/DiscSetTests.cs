using RomMBat.Core.Content;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The rule that decides whether a game may be converted to a per-game memory card.
/// </summary>
/// <remarks>
/// Every name here is a real one from a measured library. PCSX2 cannot bind discs, so getting
/// this wrong converts a set and loses the save at the disc change, which is the one failure
/// the stock shared card does not have.
/// </remarks>
public class DiscSetTests
{
    [Theory]
    [InlineData("Armored Core - Nexus (USA) (Disc 1) (Evolution).chd", "Armored Core - Nexus (USA)", 1, "(Evolution)")]
    [InlineData("Armored Core - Nexus (USA) (Disc 2) (Revolution).chd", "Armored Core - Nexus (USA)", 2, "(Revolution)")]
    [InlineData("Metal Gear Solid 3 - Subsistence (USA) (En,Es) (Disc 1).chd", "Metal Gear Solid 3 - Subsistence (USA) (En,Es)", 1, "")]
    [InlineData("Metal Gear Solid (USA) (Disc 1) (Rev 1).chd", "Metal Gear Solid (USA)", 1, "(Rev 1)")]
    [InlineData("BrainDead 13 (USA) (Disc 1).chd", "BrainDead 13 (USA)", 1, "")]
    public void A_disc_marker_is_read_off_the_name_with_whatever_follows_it(
        string fsName,
        string expectedBase,
        int expectedNumber,
        string expectedTail)
    {
        // 53 of the 202 disc files on the measured library carry text after the marker, so the
        // base is the text before it. Cutting the marker out of the middle of the stem would
        // put (Rev 1) into the base and split a set from its own revision.
        var marker = DiscSet.Parse(fsName);

        Assert.NotNull(marker);
        Assert.Equal(expectedBase, marker.BaseTitle);
        Assert.Equal(expectedNumber, marker.Number);
        Assert.Equal(expectedTail, marker.Tail);
    }

    [Theory]
    [InlineData("Ape Escape 2 (USA).chd")]
    [InlineData("Shadow of the Colossus (USA).chd")]
    [InlineData("Disco Elysium (USA).chd")]
    [InlineData("Discworld II - Missing Presumed...!  (USA).chd")]
    public void A_single_disc_title_carries_no_marker_even_when_its_name_starts_with_disc(string fsName)
    {
        // The word has to be inside parentheses with a number after it. 'Disco' and 'Discworld'
        // are the cheap way to get this wrong, and the cost would be refusing a conversion the
        // user asked for with a reason that makes no sense to them.
        Assert.Null(DiscSet.Parse(fsName));
        Assert.False(DiscSet.IsOneDiscOfASet(fsName));
    }

    [Theory]
    [InlineData("Game (USA) (Disk 1).chd")]
    [InlineData("Game (USA) (CD 2).chd")]
    [InlineData("Game (USA) (disc 1).chd")]
    [InlineData("Game (USA) (Disc1).chd")]
    public void Marker_spellings_the_measured_library_does_not_use_are_matched_anyway(string fsName)
    {
        // Only (Disc N) appears across 202 real files. The others are matched because the two
        // directions are not symmetric: recognising a marker nobody writes costs a conversion
        // that was never offered, and missing one costs a save.
        Assert.True(DiscSet.IsOneDiscOfASet(fsName));
    }

    [Fact]
    public void A_marked_disc_is_refused_even_when_no_sibling_is_on_disk()
    {
        // The refusal must not depend on what has been synced. A rule keyed on siblings would
        // convert disc 1 today and spring the trap when disc 2 arrives, making the safety of a
        // conversion a property of the order the library was pulled in.
        var alone = "Xenosaga Episode II - Jenseits von Gut und Boese (USA) (Disc 1).chd";

        Assert.True(DiscSet.IsOneDiscOfASet(alone));
        Assert.Empty(DiscSet.SiblingsOf(alone, [alone, "Ape Escape 2 (USA).chd"]));
    }

    [Fact]
    public void Siblings_are_the_files_sharing_a_base_title_and_nothing_else()
    {
        string[] folder =
        [
            "Armored Core - Nexus (USA) (Disc 1) (Evolution).chd",
            "Armored Core - Nexus (USA) (Disc 2) (Revolution).chd",
            "Armored Core 3 (USA).chd",
            "Star Ocean - Till the End of Time (USA) (Disc 1).chd",
            "Star Ocean - Till the End of Time (USA) (Disc 2).chd",
        ];

        // The differing tails must not separate the two Armored Core discs.
        Assert.Equal(
            ["Armored Core - Nexus (USA) (Disc 2) (Revolution).chd"],
            DiscSet.SiblingsOf(folder[0], folder));

        // And a set must not pull in an unrelated title that merely starts the same way.
        Assert.Equal(
            ["Star Ocean - Till the End of Time (USA) (Disc 1).chd"],
            DiscSet.SiblingsOf(folder[4], folder));

        Assert.Empty(DiscSet.SiblingsOf("Armored Core 3 (USA).chd", folder));
    }

    [Fact]
    public void The_measured_ps2_library_splits_where_the_census_said_it_does()
    {
        // 302 single-disc titles against 7 sets of two, counted on a real install. Reproduced
        // here on the seven set names so that a change to the marker rule that silently
        // reclassified a set fails rather than passing quietly.
        string[] sets =
        [
            "Armored Core - Nexus (USA)",
            "Metal Gear Solid 3 - Subsistence (USA) (En,Es)",
            "Onimusha - Dawn of Dreams (USA)",
            "Shadow Hearts - Covenant (USA)",
            "Star Ocean - Till the End of Time (USA)",
            "Xenosaga Episode II - Jenseits von Gut und Boese (USA)",
            "Xenosaga Episode III - Also sprach Zarathustra (USA)",
        ];

        foreach (var title in sets)
        {
            Assert.Equal(title, DiscSet.Parse($"{title} (Disc 1).chd")?.BaseTitle);
            Assert.Equal(title, DiscSet.Parse($"{title} (Disc 2).chd")?.BaseTitle);
        }
    }
}
