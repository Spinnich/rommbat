using RomMBat.Agent.Tests.Support;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Agent.Tests;

/// <summary>
/// The <c>saves</c> command's gates.
/// </summary>
/// <remarks>
/// <b>Not parallel</b>, because <see cref="AgentRunner"/> redirects <c>Console</c>.
/// </remarks>
[Collection("agent-console")]
public sealed class SavesCommandTests
{
    [Fact]
    public async Task Resolve_refuses_while_a_flush_holds_the_tree_lock()
    {
        // It runs the same class C restore a flush does, into the same shared container, so two
        // at once leaves the container half swapped. Refused rather than reported as done: a
        // person asked for this one, and exit 0 would read as having resolved it.
        using var tree = TempRetroBatTree.Create();

        using (TreeLock.TryAcquire(tree.Install()))
        {
            var run = await AgentRunner.RunAsync(
                tree, "saves", "resolve", "42", "libretro:battery", "--keep-local");

            Assert.Equal(3, run.ExitCode);
            Assert.True(run.Complained("A flush is running"), run.Error);
            Assert.True(run.Complained("Nothing was changed"), run.Error);
        }
    }

    [Fact]
    public async Task Resolve_still_names_the_side_before_it_looks_at_the_lock()
    {
        // The usage gate stays first. "You must name a side" is the answer to a command line
        // that named none, whatever else is running.
        using var tree = TempRetroBatTree.Create();

        using (TreeLock.TryAcquire(tree.Install()))
        {
            var run = await AgentRunner.RunAsync(tree, "saves", "resolve", "42", "libretro:battery");

            Assert.Equal(2, run.ExitCode);
            Assert.False(run.Complained("A flush is running"), run.Error);
        }
    }
}
