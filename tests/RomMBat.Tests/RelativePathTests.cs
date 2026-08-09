using RomMBat.Core.Paths;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The typed half of the "no column ever holds an absolute path" rule.
/// </summary>
/// <remarks>
/// <see cref="LocalStoreTests"/> asserts the database CHECK constraints agree with this
/// case for case, so the two halves cannot drift apart.
/// </remarks>
public class RelativePathTests
{
    /// <summary>
    /// Everything that must never become a stored path. Shared with the store tests so both
    /// enforcement layers are proven against the same list.
    /// </summary>
    public static TheoryData<string> Rejected { get; } = new()
    {
        @"C:\RetroBat\roms\snes\game.sfc",
        "C:/RetroBat/roms/snes/game.sfc",
        @"C:roms\snes\game.sfc",
        @"\RetroBat\roms",
        "/RetroBat/roms",
        @"\\server\share\roms",
        "//server/share/roms",
        @"\\?\C:\RetroBat",
        "../outside.txt",
        "roms/../../outside.txt",
        "roms/..",
        "..",
        "",
        "   ",
    };

    public static TheoryData<string, string> Accepted => new()
    {
        { "roms/snes/game.sfc", "roms/snes/game.sfc" },
        { @"roms\snes\game.sfc", "roms/snes/game.sfc" },
        { "roms//snes///game.sfc", "roms/snes/game.sfc" },
        { "./roms/snes/game.sfc", "roms/snes/game.sfc" },
        { "roms/snes/", "roms/snes" },
        { "saves/nes/bizhawk/sstates/NesHawk/Game.QuickSave0.State", "saves/nes/bizhawk/sstates/NesHawk/Game.QuickSave0.State" },
        { "roms/snes/Gradius 2 (Japan, Europe) (En).zip", "roms/snes/Gradius 2 (Japan, Europe) (En).zip" },
    };

    [Theory]
    [MemberData(nameof(Rejected))]
    public void Refuses_anything_that_is_not_relative_to_the_root(string value)
    {
        Assert.False(RelativePath.TryCreate(value, out _));
        Assert.Throws<ArgumentException>(() => RelativePath.Create(value));
    }

    [Theory]
    [MemberData(nameof(Accepted))]
    public void Normalises_to_forward_slashes(string input, string expected)
    {
        Assert.True(RelativePath.TryCreate(input, out var path));
        Assert.Equal(expected, path.Value);
    }

    [Fact]
    public void Compares_case_insensitively_because_Windows_does()
    {
        Assert.Equal(RelativePath.Create("roms/SNES/Game.sfc"), RelativePath.Create("roms/snes/game.sfc"));
    }

    [Fact]
    public void Name_is_the_last_segment()
    {
        Assert.Equal("game.sfc", RelativePath.Create("roms/snes/game.sfc").Name);
        Assert.Equal("retrobat.ini", RelativePath.Create("retrobat.ini").Name);
    }

    [Fact]
    public void The_default_value_holds_nothing()
    {
        var path = default(RelativePath);

        Assert.False(path.HasValue);
        Assert.Equal(string.Empty, path.Value);
    }

    [Fact]
    public void Resolving_and_relativising_round_trips_through_the_install()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        var stored = RelativePath.Create("roms/snes/game.sfc");
        var absolute = install.Resolve(stored);

        Assert.True(Path.IsPathRooted(absolute));
        Assert.Equal(stored, install.Relativize(absolute));
    }

    [Fact]
    public void A_stored_path_survives_the_tree_moving_to_a_different_location()
    {
        // Standing in for the drive letter changing, which M0 probe 7 did for real: the
        // stick went G: to D: to K: and nothing was allowed to notice.
        using var original = TempRetroBatTree.Create();
        using var moved = original.CopyToNewLocation();

        var stored = RelativePath.Create("roms/snes/game.sfc");

        var before = original.Install().Resolve(stored);
        var after = moved.Install().Resolve(stored);

        Assert.NotEqual(before, after);
        Assert.EndsWith(Path.Combine("roms", "snes", "game.sfc"), after, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_absolute_path_outside_the_tree_cannot_be_relativised()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        var outside = Path.Combine(Path.GetTempPath(), "somewhere-else", "game.sfc");

        Assert.False(install.Contains(outside));
        Assert.Throws<ArgumentException>(() => install.Relativize(outside));
    }

    [Fact]
    public void The_hook_boundary_relativises_the_absolute_rom_path_ES_passes()
    {
        // ES hands game-start an absolute rom path in its first argument, so relativising at
        // that boundary is mandatory work rather than an optimisation.
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        var fromHook = Path.Combine(tree.Root, "roms", "ports", "gong.libretro");

        Assert.Equal(RelativePath.Create("roms/ports/gong.libretro"), install.Relativize(fromHook));
    }
}
