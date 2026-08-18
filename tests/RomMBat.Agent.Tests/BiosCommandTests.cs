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
}

/// <summary>Serialises every test that redirects the console.</summary>
[CollectionDefinition("agent-console", DisableParallelization = true)]
public sealed class ConsoleRedirectingTests;
