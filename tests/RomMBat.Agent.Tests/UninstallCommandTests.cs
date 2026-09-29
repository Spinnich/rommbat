using RomMBat.Agent.Tests.Support;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Agent.Tests;

/// <summary>
/// The <c>uninstall</c> command: a preview by default, and a refusal that says what to send.
/// </summary>
[Collection("agent-console")]
public sealed class UninstallCommandTests
{
    [Fact]
    public async Task Without_apply_it_names_what_would_go_and_removes_nothing()
    {
        using var tree = TempRetroBatTree.Create();
        InstallHooks(tree);

        var run = await AgentRunner.RunAsync(tree, "uninstall");

        Assert.Equal(0, run.ExitCode);
        Assert.True(run.Wrote(EsHooks.PathFor("game-end").Value), run.Out);
        Assert.True(run.Wrote("uninstall --apply"), run.Out);
        Assert.True(run.Wrote("Add --content, --bios or both"), run.Out);
        Assert.True(new EsHooks(tree.Install()).IsInstalled(), "a preview removed the hooks");
    }

    [Fact]
    public async Task Apply_takes_the_hooks_out_and_says_what_is_left()
    {
        using var tree = TempRetroBatTree.Create();
        InstallHooks(tree);

        var run = await AgentRunner.RunAsync(tree, "uninstall", "--apply");

        Assert.Equal(0, run.ExitCode);
        Assert.False(new EsHooks(tree.Install()).IsInstalled(), run.Out);
        Assert.True(run.Wrote("Delete that folder to finish"), run.Out);
    }

    [Fact]
    public async Task Apply_with_an_unsent_save_refuses_and_names_the_flush()
    {
        using var tree = TempRetroBatTree.Create();
        InstallHooks(tree);

        using (var store = LocalStore.Open(tree.Install()))
        {
            store.Outbox.Enqueue(OutboxKind.Save, DateTimeOffset.UtcNow, romId: 7, slot: "main");
        }

        var run = await AgentRunner.RunAsync(tree, "uninstall", "--apply");

        Assert.Equal(ExitCode.Refused, run.ExitCode);
        Assert.True(run.Complained("rommbat-agent flush"), run.Error);
        Assert.True(new EsHooks(tree.Install()).IsInstalled(), "the hooks went while a save was unsent");
    }

    private static void InstallHooks(TempRetroBatTree tree)
    {
        var standIn = Path.Combine(tree.Root, "stand-in-hook.exe");
        File.WriteAllBytes(standIn, [0x4D, 0x5A]);
        new EsHooks(tree.Install()).Install(standIn);
    }
}
