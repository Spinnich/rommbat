using RomMBat.Agent.Tests.Support;
using RomMBat.Agent.Commands;
using RomMBat.Core.Content;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Agent.Tests;

/// <summary>
/// The <c>evict</c> command's sweep of <c>partial/</c>.
/// </summary>
/// <remarks>
/// <b>Driven through the command rather than through <see cref="PartialSweep"/>.</b> The case
/// the sweep exists for is an install inside its budget: eviction plans nothing, so the branch
/// that reaches the sweep at all is the command's own, and a suite that only exercises the
/// sweep would pass with that branch deleted.
/// </remarks>
[Collection("agent-console")]
public sealed class EvictCommandTests
{
    private const string DeadRomTransfer = "9.part";

    [Fact]
    public async Task An_install_inside_its_budget_still_reports_what_partial_is_holding()
    {
        using var tree = TempRetroBatTree.Create();
        WritePartial(tree, DeadRomTransfer, "half a rom no set has heard of");

        var run = await AgentRunner.RunAsync(tree, "evict");

        Assert.Equal(0, run.ExitCode);
        Assert.True(run.Wrote("1 abandoned transfer"), run.Out);
        Assert.True(run.Wrote("No disk budget is set"), run.Out);
        Assert.True(run.Wrote("evict --apply"), run.Out);
        Assert.True(File.Exists(Partial(tree, DeadRomTransfer)), "a dry run removed something");
    }

    [Fact]
    public async Task An_install_inside_its_budget_reclaims_it_on_apply()
    {
        // Nothing else ever does. These bytes carry no local_file row, so the budget cannot
        // count them and eviction proper cannot reach them.
        using var tree = TempRetroBatTree.Create();
        WritePartial(tree, DeadRomTransfer, "half a rom no set has heard of");

        var run = await AgentRunner.RunAsync(tree, "evict", "--apply");

        Assert.Equal(0, run.ExitCode);
        Assert.True(run.Wrote("1 abandoned transfer removed"), run.Out);
        Assert.False(File.Exists(Partial(tree, DeadRomTransfer)), run.Out);
    }

    [Fact]
    public async Task Apply_reclaims_nothing_while_a_flush_holds_the_tree_lock()
    {
        // partial/unit-<guid>/ is a class C restore's staging directory, not litter, and the
        // restore holds no handle on it. The command must come away empty rather than delete
        // a save that is halfway back onto the device.
        using var tree = TempRetroBatTree.Create();
        WritePartial(tree, DeadRomTransfer, "half a rom no set has heard of");

        using (TreeLock.TryAcquire(tree.Install()))
        {
            var run = await AgentRunner.RunAsync(tree, "evict", "--apply");

            Assert.Equal(0, run.ExitCode);
            Assert.True(run.Wrote("another agent is writing there"), run.Out);
        }

        Assert.True(File.Exists(Partial(tree, DeadRomTransfer)), "the sweep ran without the lock");
    }

    [Fact]
    public async Task Busy_library_returns_refused_and_leaves_the_partial()
    {
        using var tree = TempRetroBatTree.Create();
        WritePartial(tree, DeadRomTransfer, "pending");
        using var lease = File.Open(Path.Combine(tree.AppDirectory, "library.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        var run = await AgentRunner.RunAsync(tree, "evict", "--apply");

        Assert.Equal(ExitCode.Refused, run.ExitCode);
        Assert.Contains("changing the library", run.Error, StringComparison.Ordinal);
        Assert.True(File.Exists(Partial(tree, DeadRomTransfer)));
    }

    [Theory]
    [InlineData("bios", "--apply")]
    [InlineData("gamelist", "--no-reload")]
    [InlineData("sets", "remove", "existing")]
    public async Task Other_library_mutations_refuse_while_the_library_is_busy(params string[] arguments)
    {
        using var tree = TempRetroBatTree.Create();
        tree.Install().EnsureAppDirectories();
        using var lease = TreeLock.TryAcquireLibrary(tree.Install());
        Assert.NotNull(lease);

        var run = await AgentRunner.RunAsync(tree, arguments);

        Assert.Equal(ExitCode.Refused, run.ExitCode);
        Assert.Contains("changing the library", run.Error, StringComparison.Ordinal);
    }

    private static string Partial(TempRetroBatTree tree, string name) =>
        Path.Combine(tree.AppDirectory, "partial", name);

    private static void WritePartial(TempRetroBatTree tree, string name, string contents)
    {
        var path = Partial(tree, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }
}
