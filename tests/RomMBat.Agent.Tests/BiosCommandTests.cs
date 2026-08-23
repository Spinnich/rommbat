using RomMBat.Agent.Tests.Support;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Agent.Tests;

/// <summary>
/// The <c>bios</c> command, which is the first thing this project covers.
/// </summary>
/// <remarks>
/// <b>Not parallel</b>, because <see cref="AgentRunner"/> redirects <c>Console</c>.
/// </remarks>
[Collection("agent-console")]
public sealed class BiosCommandTests
{
    [Fact]
    public async Task A_system_this_install_does_not_have_is_an_error_naming_the_argument()
    {
        // `bios snse` and `bios snes` printed the same "no BIOS is required for these systems",
        // because the positional went straight to the planner, the manifest answers an empty
        // list for any key it does not hold, and an empty plan produces that line. A typo read
        // as a clean bill of health for a system that was never consulted.
        using var tree = TempRetroBatTree.Create();
        AgentRunner.WriteEsSystems(tree);

        var run = await AgentRunner.RunAsync(tree, "bios", "snse", "--offline");

        Assert.NotEqual(0, run.ExitCode);
        Assert.True(run.Complained("snse"), run.Error);
        Assert.False(run.Wrote("no BIOS is required"), run.Out);
    }

    [Fact]
    public async Task A_real_system_that_needs_no_firmware_says_so_by_name()
    {
        // The other half, and the reason the manifest cannot answer this alone: only 99 of
        // RetroBat's 240 systems appear in batocera-systems.json, so absent from it is the
        // ordinary case rather than the error. snes is a real system RetroBat requires no BIOS
        // for, and that answer has to be distinguishable from the typo above.
        using var tree = TempRetroBatTree.Create();
        AgentRunner.WriteEsSystems(tree);

        var run = await AgentRunner.RunAsync(tree, "bios", "snes", "--offline");

        Assert.Equal(0, run.ExitCode);
        Assert.True(run.Wrote("RetroBat requires no BIOS for snes"), run.Out);
        Assert.Equal(string.Empty, run.Error);
    }

    [Fact]
    public async Task One_bad_name_among_good_ones_fails_and_names_only_the_bad_one()
    {
        // Named rather than counted, because the point is that the user is told which of the
        // words they typed this install has never heard of.
        using var tree = TempRetroBatTree.Create();
        AgentRunner.WriteEsSystems(tree);

        var run = await AgentRunner.RunAsync(tree, "bios", "psx", "nintendo64", "--offline");

        Assert.NotEqual(0, run.ExitCode);
        Assert.True(run.Complained("nintendo64"), run.Error);
        Assert.False(run.Complained("psx"), run.Error);
    }

    [Fact]
    public async Task A_system_that_does_need_firmware_still_reports()
    {
        // The ordinary case, and the one anything added to this gate must not swallow: psx is
        // declared by the install and is in the BIOS manifest, so the report runs.
        using var tree = TempRetroBatTree.Create();
        AgentRunner.WriteEsSystems(tree);

        var run = await AgentRunner.RunAsync(tree, "bios", "psx", "--offline");

        Assert.Equal(0, run.ExitCode);
        Assert.False(run.Wrote("RetroBat requires no BIOS for psx"), run.Out);
        Assert.True(run.Wrote("psx"), run.Out);
    }

    [Fact]
    public async Task Covering_the_whole_install_validates_nothing_because_it_names_nothing()
    {
        // --all passes null to the planner and the no-argument form passes folders that came
        // off this install, so neither names anything a user typed.
        using var tree = TempRetroBatTree.Create();
        AgentRunner.WriteEsSystems(tree);

        var run = await AgentRunner.RunAsync(tree, "bios", "--all", "--offline");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(string.Empty, run.Error);
    }

    [Fact]
    public async Task An_install_that_has_never_started_is_refused_rather_than_crashing()
    {
        // The gate is the first thing on this path to read es_systems.cfg, and RootMarkers.All
        // accepts a root on retrobat.ini alone, so a RetroBat that has been unzipped and never
        // launched reaches it with no file to read. That threw out of Program as an unhandled
        // exception, because the only handlers were for cancellation and an unreachable server.
        using var tree = TempRetroBatTree.Create();

        var run = await AgentRunner.RunAsync(tree, "bios", "psx", "--offline");

        Assert.Equal(3, run.ExitCode);
        Assert.True(run.Complained("es_systems.cfg"), run.Error);
    }
}

/// <summary>Serialises every test that redirects the console.</summary>
[CollectionDefinition("agent-console", DisableParallelization = true)]
public sealed class ConsoleRedirectingTests;
