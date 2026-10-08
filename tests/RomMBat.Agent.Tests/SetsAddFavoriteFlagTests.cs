using RomMBat.Agent.Tests.Support;
using RomMBat.Core.Sets;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Agent.Tests;

/// <summary>
/// <c>sets add --scope filter</c> takes the favorite filter under either spelling.
/// </summary>
/// <remarks>
/// <b>Not parallel</b>, because <see cref="AgentRunner"/> redirects <c>Console</c>.
/// <para>
/// The help names <c>--favorite</c>, and <c>--favourite</c> stays accepted so a script that
/// already passes it keeps building the same filter.
/// </para>
/// </remarks>
[Collection("agent-console")]
public sealed class SetsAddFavoriteFlagTests
{
    [Theory]
    [InlineData("--favorite")]
    [InlineData("--favourite")]
    public async Task Either_spelling_stores_a_favorite_filter(string flag)
    {
        using var tree = TempRetroBatTree.Create();
        AgentRunner.WriteEsSystems(tree);

        var run = await AgentRunner.RunAsync(tree, "sets", "add", "mine", "--scope", "filter", flag);

        Assert.True(run.Wrote("Added 'mine'"), run.Out + run.Error);
        using var store = LocalStore.Open(tree.Install());
        var set = store.SyncSets.Find("mine");
        Assert.NotNull(set);
        Assert.True(SyncSetService.FilterOf(set).Favorite);
    }

    [Fact]
    public async Task The_usage_names_the_american_spelling()
    {
        using var tree = TempRetroBatTree.Create();

        var run = await AgentRunner.RunAsync(tree, "sets", "add");

        Assert.True(run.Complained("[--favorite]"), run.Error);
        Assert.False(run.Complained("--favourite"), run.Error);
    }
}
