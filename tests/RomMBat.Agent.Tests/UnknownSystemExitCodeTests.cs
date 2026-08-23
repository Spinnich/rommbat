using RomMBat.Agent.Tests.Support;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Agent.Tests;

/// <summary>
/// The three commands that validate a user-supplied system folder against
/// <c>es_systems.cfg</c>, and the one exit code they now agree on.
/// </summary>
/// <remarks>
/// <b>Not parallel</b>, because <see cref="AgentRunner"/> redirects <c>Console</c>.
/// <para>
/// They answered the same sentence with two codes: <c>bios</c> said <c>Usage</c> and the other
/// two said <c>Refused</c>, so a script wrapping the agent could not tell a mistyped argument
/// from an environment problem. <c>ExitCode</c>'s own doc comments settle it, since a folder
/// the user typed is the command line and <c>Refused</c> is scoped to a failed precondition.
/// </para>
/// </remarks>
[Collection("agent-console")]
public sealed class UnknownSystemExitCodeTests
{
    private const int Usage = 2;

    [Fact]
    public async Task Bios_answers_Usage_for_a_folder_this_install_does_not_declare()
    {
        using var tree = TempRetroBatTree.Create();
        AgentRunner.WriteEsSystems(tree);

        var run = await AgentRunner.RunAsync(tree, "bios", "snse", "--offline");

        Assert.Equal(Usage, run.ExitCode);
        Assert.True(run.Complained("es_systems.cfg"), run.Error);
    }

    [Fact]
    public async Task Platforms_map_answers_Usage_for_a_folder_this_install_does_not_declare()
    {
        using var tree = TempRetroBatTree.Create();
        AgentRunner.WriteEsSystems(tree);

        var run = await AgentRunner.RunAsync(tree, "platforms", "map", "snes", "snse");

        Assert.Equal(Usage, run.ExitCode);
        Assert.True(run.Complained("es_systems.cfg"), run.Error);
    }

    [Fact]
    public async Task Sets_add_answers_Usage_for_a_folder_this_install_does_not_declare()
    {
        using var tree = TempRetroBatTree.Create();
        AgentRunner.WriteEsSystems(tree);

        var run = await AgentRunner.RunAsync(
            tree, "sets", "add", "mine", "--scope", "platform", "--value", "1", "--folder", "snse");

        Assert.Equal(Usage, run.ExitCode);
        Assert.True(run.Complained("es_systems.cfg"), run.Error);
    }

    [Fact]
    public async Task A_folder_this_install_does_declare_is_not_refused_by_the_gate()
    {
        // The other side of the same gate, so a test that passes because everything answers
        // Usage would fail here.
        using var tree = TempRetroBatTree.Create();
        AgentRunner.WriteEsSystems(tree);

        var run = await AgentRunner.RunAsync(tree, "platforms", "map", "snes", "snes");

        Assert.Equal(0, run.ExitCode);
        Assert.True(run.Wrote("snes"), run.Out);
    }
}
