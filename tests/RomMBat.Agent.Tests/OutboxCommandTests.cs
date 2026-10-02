using RomMBat.Agent.Tests.Support;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Agent.Tests;

/// <summary>
/// The <c>outbox</c> command: list what the server refused, and delete it only with <c>--apply</c>.
/// </summary>
[Collection("agent-console")]
public sealed class OutboxCommandTests
{
    [Fact]
    public async Task Drop_previews_without_apply_then_deletes_only_the_refused_entry_with_it()
    {
        using var tree = TempRetroBatTree.Create();
        var now = DateTimeOffset.UtcNow;

        using (var store = LocalStore.Open(tree.Install()))
        {
            store.Outbox.Enqueue(OutboxKind.PlaySession, now, romId: 1);
            store.Outbox.Enqueue(OutboxKind.PlaySession, now, romId: 2);
            store.Outbox.MarkFailed(store.Outbox.Pending()[0].Id, "400: refused", now);
        }

        var listed = await AgentRunner.RunAsync(tree, "outbox");
        Assert.Equal(0, listed.ExitCode);
        Assert.True(listed.Wrote("400: refused"), listed.Out);

        var preview = await AgentRunner.RunAsync(tree, "outbox", "drop", "--all-failed");
        Assert.Equal(0, preview.ExitCode);
        Assert.True(preview.Wrote("--apply"), preview.Out);

        var applied = await AgentRunner.RunAsync(tree, "outbox", "drop", "--all-failed", "--apply");
        Assert.Equal(0, applied.ExitCode);

        using var after = LocalStore.Open(tree.Install());
        Assert.Equal(0, after.Outbox.FailedCount());
        Assert.Equal(1, after.Outbox.PendingCount());
    }

    [Fact]
    public async Task Drop_with_nothing_failed_refuses_rather_than_reporting_success()
    {
        using var tree = TempRetroBatTree.Create();

        var run = await AgentRunner.RunAsync(tree, "outbox", "drop", "--all-failed", "--apply");

        Assert.Equal(ExitCode.Refused, run.ExitCode);
    }
}
