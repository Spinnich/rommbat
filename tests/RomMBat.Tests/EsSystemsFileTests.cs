using System.Text.Json;
using RomMBat.Core.RetroBat;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Parsing <c>es_systems.cfg</c>, which is the authority on folders and extensions.
/// </summary>
/// <remarks>
/// Every case here is one the shipped 8.2.0 file actually contains. The one that would
/// silently corrupt a sync is <c>&lt;name&gt;</c> being a different vocabulary from the
/// folder, so it is asserted from both directions.
/// </remarks>
public class EsSystemsFileTests
{
    [Theory]
    [InlineData("gw", "gameandwatch")]
    [InlineData("powerbomberman", "pb")]
    [InlineData("casloopy", "loopy")]
    [InlineData("Windows", "windows")]
    public void The_folder_comes_from_path_not_from_name(string name, string folder)
    {
        var systems = Fixtures.LoadEsSystems();

        Assert.True(systems.HasFolder(folder));
        Assert.DoesNotContain(systems.Folders, candidate => string.Equals(candidate, name, StringComparison.Ordinal));
    }

    [Fact]
    public void A_name_used_twice_yields_two_folders()
    {
        var systems = Fixtures.LoadEsSystems();

        // Both are <name>starship</name>. Keying on the name would lose one of them.
        Assert.True(systems.HasFolder("starship"));
        Assert.True(systems.HasFolder("ghostship"));
    }

    [Fact]
    public void Folders_match_case_insensitively()
    {
        var systems = Fixtures.LoadEsSystems();

        Assert.True(systems.HasFolder("WINDOWS"));
        Assert.True(systems.HasFolder("snes"));
        Assert.True(systems.HasFolder("SNES"));
    }

    [Theory]
    [InlineData("library")]
    [InlineData("screenshots")]
    [InlineData("es_menu")]
    [InlineData("mess")]
    public void Systems_with_no_rom_folder_are_not_sync_targets(string name)
    {
        var systems = Fixtures.LoadEsSystems();

        Assert.False(systems.HasFolder(name));
        Assert.Contains(systems.NonRomSystems, system =>
            system.Name.Contains(name, StringComparison.OrdinalIgnoreCase)
            || system.DeclaredPath.Contains(name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Every_rom_folder_in_the_shipped_file_is_a_known_retrobat_system()
    {
        var systems = Fixtures.LoadEsSystems();
        var known = Fixtures.LoadSystemNames().ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain(systems.Folders, folder => !known.Contains(folder));
        Assert.Equal(known.Count, systems.Folders.Count);
    }

    [Fact]
    public void Extensions_are_normalised_and_matched_either_way()
    {
        var systems = Fixtures.LoadEsSystems();

        Assert.True(systems.TryGetFolder("snes", out var snes));
        Assert.Contains("smc", snes.Extensions);
        Assert.DoesNotContain(snes.Extensions, extension => extension.StartsWith('.'));

        // RomM sends fs_extension without a dot; es_systems.cfg writes it with one.
        Assert.True(snes.Accepts("sfc"));
        Assert.True(snes.Accepts(".sfc"));
        Assert.True(snes.Accepts("SFC"));
        Assert.False(snes.Accepts("chd"));
        Assert.False(snes.Accepts(null));
    }

    [Fact]
    public void Archives_are_honoured_per_system_rather_than_assumed_universal()
    {
        var systems = Fixtures.LoadEsSystems();

        Assert.True(systems.TryGetFolder("snes", out var snes));
        Assert.True(systems.TryGetFolder("dreamcast", out var dreamcast));
        Assert.True(systems.TryGetFolder("wiiu", out var wiiu));

        Assert.True(snes.Accepts("zip"));
        Assert.False(dreamcast.Accepts("zip"));

        // The disc-image mismatch the plan warns about, taken from the shipped file rather
        // than imagined: dreamcast launches a .chd, wiiu does not, and neither converts.
        Assert.True(dreamcast.Accepts("chd"));
        Assert.True(dreamcast.Accepts("cue"));
        Assert.True(wiiu.Accepts("iso"));
        Assert.False(wiiu.Accepts("chd"));
    }

    [Fact]
    public void The_shipped_template_agrees_with_a_live_install_on_folders()
    {
        var template = Fixtures.LoadEsSystems();

        using var document = JsonDocument.Parse(File.ReadAllText(Fixtures.LiveEsSystems));
        var live = document.RootElement
            .EnumerateArray()
            .Select(system => system.GetProperty("path").GetString() ?? string.Empty)
            .Where(path => path.Replace('\\', '/').Contains("/roms/", StringComparison.OrdinalIgnoreCase))
            .Select(path => path.Replace('\\', '/').Split('/')[^1])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // They agree exactly, all 240. M0 recorded the live install as carrying four systems
        // upstream does not; that compared 244 <system> elements against 240 folder names.
        // Both files have 244 active systems, four of which own no folder under roms/.
        Assert.DoesNotContain(template.Folders, folder => !live.Contains(folder));
        Assert.DoesNotContain(live, folder => !template.HasFolder(folder));
        Assert.Equal(240, template.Folders.Count);
    }

    [Fact]
    public void A_path_that_escapes_the_tree_is_not_a_sync_target()
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(
            """
            <systemList>
              <system><name>escape</name><path>~\..\..\..\elsewhere\roms\thing</path><extension>.bin</extension></system>
              <system><name>ok</name><path>~\..\roms\snes</path><extension>.sfc</extension></system>
            </systemList>
            """));

        var systems = EsSystemsFile.Parse(stream);

        Assert.Equal(["snes"], systems.Folders.Order(StringComparer.Ordinal));
        Assert.Contains(systems.NonRomSystems, system => system.Name == "escape");
    }

    [Fact]
    public void Unreadable_xml_is_reported_rather_than_swallowed()
    {
        using var stream = new MemoryStream("not xml at all"u8.ToArray());

        Assert.Throws<EsSystemsException>(() => EsSystemsFile.Parse(stream));
    }
}
