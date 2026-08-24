using RomMBat.Agent.Tests.Support;
using RomMBat.Core.Paths;
using RomMBat.Core.Store;
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
    public async Task The_sidecar_route_attributes_a_directory_save_on_the_first_run()
    {
        // SaveScanner constructs the GameIdAttributor, and the sidecar route reads local_state,
        // which StateScanner writes. All three commands ran SaveScanner first, so on any tree
        // whose local_state was still empty the route read an empty list and answered nothing.
        // It only worked from the second invocation onward.
        //
        // Observed on a real install with a real PPSSPP state and its .txt sidecar: run one
        // said "0 of 3 directory saves attributed" and printed the "no matching ROM" reason
        // with its "their ROMs are not on this device" explanation, which is actively
        // misleading when the ROM is right there and the route that finds it had not run.
        using var tree = TempRetroBatTree.Create();
        AgentRunner.WriteEsSystems(tree);
        AgentRunner.WriteEsSaveStates(tree);

        SeedPspGame(tree);

        var run = await AgentRunner.RunAsync(tree, "saves");

        Assert.Equal(0, run.ExitCode);
        Assert.True(run.Wrote("1 of 1 directory saves attributed"), run.Out);
        Assert.True(run.Wrote("psp/ULES01513"), run.Out);
        Assert.False(run.Wrote("their ROMs are not on this device"), run.Out);
    }

    /// <summary>
    /// A PSP game with a save state, its name sidecar, and the directory save the sidecar names.
    /// </summary>
    /// <remarks>
    /// The shape measured on a real install: <c>ppsspp/3rd Birthday, The (Europe).txt</c> holds
    /// <c>ULES01513_1.00</c>, whose <c>ULES01513</c> prefix joins <c>SAVEDATA/ULES01513SYSDATA</c>
    /// while the stem resolves through <c>RomIndex</c>.
    /// </remarks>
    private static void SeedPspGame(TempRetroBatTree tree)
    {
        const string stem = "3rd Birthday, The (Europe)";
        var install = tree.Install();

        Write(install, $"roms/psp/{stem}.cso", "rom bytes");
        Write(install, $"saves/psp/ppsspp/{stem}_0.ppst", "state bytes");
        Write(install, $"saves/psp/ppsspp/{stem}.txt", "ULES01513_1.00");
        Write(install, "saves/psp/SAVEDATA/ULES01513SYSDATA/DATA.BIN", "the save");

        using var store = LocalStore.Open(install);

        store.Files.Record(new LocalFile
        {
            Path = RelativePath.Create($"roms/psp/{stem}.cso"),
            Folder = "psp",
            RomId = 391,
            Kind = LocalFileKind.Rom,
            FileName = $"{stem}.cso",
            SizeBytes = 9,
        });
    }

    private static void Write(RetroBatInstall install, string relativePath, string content)
    {
        var absolute = install.Resolve(relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllText(absolute, content);
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
