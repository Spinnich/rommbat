using System.Text;
using RomMBat.Agent.Tests.Support;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Agent.Tests;

/// <summary>
/// The guide's command-line reference is <c>rommbat-agent --help</c>, and nothing else.
/// </summary>
/// <remarks>
/// A hand-kept copy of the help text drifts the first time a flag changes. Built from the help
/// the agent really prints, through <see cref="Program.DispatchAsync"/>, the page can only be
/// stale by not being regenerated, which this fails on.
/// </remarks>
[Collection("agent-console")]
public sealed class CliReferencePageTests
{
    public const string PagePath = "wiki/reference/cli.md";

    [Fact]
    public async Task The_command_line_page_is_the_help_the_agent_prints()
    {
        using var tree = TempRetroBatTree.Create();

        var run = await AgentRunner.RunAsync(tree, "status", "--help");

        Assert.Equal(0, run.ExitCode);
        Assert.StartsWith("rommbat-agent <subcommand>", run.Error, StringComparison.Ordinal);
        GeneratedPage.AssertCurrent(PagePath, Render(run.Error));
    }

    private static string Render(string help)
    {
        var page = new StringBuilder();
        page.Append("# Command line\n\n");
        page.Append("<!-- Generated from rommbat-agent --help by tests/RomMBat.Agent.Tests/CliReferencePageTests.cs.\n");
        page.Append("     Edit the help text in src/RomMBat.Agent/Program.cs and regenerate; an edit here fails the test. -->\n\n");
        page.Append("`rommbat-agent.exe` sits in RetroBat's `emulators\\rommbat` folder beside the RomMBat app, and does\n");
        page.Append("one job each time you run it. Everything the app does, it can do from a terminal. This is\n");
        page.Append("what `rommbat-agent --help` prints.\n\n");
        page.Append("```text\n");
        page.Append(help.ReplaceLineEndings("\n").TrimEnd('\n'));
        page.Append("\n```\n");
        return page.ToString();
    }
}
