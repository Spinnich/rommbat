using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Which hooks may start a background pass, which is CLAUDE.md rule 4's boundary.
/// </summary>
/// <remarks>
/// <b>The rule is narrowed by this stage, not bent.</b> It reads "The ES hooks never touch the
/// network" and gives its reason in the next sentence: <i>they run inside the game-launch
/// path</i>. <c>game-start</c> and <c>game-end</c> do. <c>start</c> fires when
/// EmulationStation starts and <c>quit</c> when it exits, and neither is in that path.
/// <para>
/// <b>The next person to edit the hook will not have read any of that</b>, which is why the
/// boundary is a value on <c>SpoolRecord</c> and is asserted here rather than described in a
/// comment. The first test runs everywhere. The second drives the real binaries and proves the
/// hook actually branches on it, because a correct predicate nothing calls is worth nothing.
/// </para>
/// </remarks>
public sealed class HookSpawnTests
{
    [Theory]
    [InlineData("start", true)]
    [InlineData("quit", true)]
    [InlineData("game-start", false)]
    [InlineData("game-end", false)]
    public void Only_the_two_events_outside_the_game_launch_path_spawn_anything(string hookEvent, bool spawns)
    {
        Assert.Equal(spawns, SpoolRecord.SpawnsBackgroundPass(hookEvent));
    }

    [Fact]
    public void The_spawning_events_are_a_subset_of_the_events_a_hook_is_installed_for()
    {
        // A spawn event that is not a hook event would never fire, and a hook event added later
        // must be a deliberate decision about this rule rather than an accident of ordering.
        Assert.Equal(["start", "quit"], SpoolRecord.BackgroundEvents);
        Assert.All(SpoolRecord.BackgroundEvents, hookEvent => Assert.Contains(hookEvent, EsHooks.Events));

        Assert.DoesNotContain("game-start", SpoolRecord.BackgroundEvents);
        Assert.DoesNotContain("game-end", SpoolRecord.BackgroundEvents);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Start")]
    [InlineData("game-selected")]
    [InlineData("shutdown")]
    public void Anything_else_spawns_nothing(string? hookEvent)
    {
        // Ordinal and exact. The event comes from a directory name on the user's disk, and
        // a case-insensitive match would make a folder called Start behave differently from
        // the one ES actually uses.
        Assert.False(SpoolRecord.SpawnsBackgroundPass(hookEvent));
    }

    /// <summary>
    /// The same boundary, driven with the real hook and the real agent in real processes.
    /// </summary>
    /// <remarks>
    /// The value test above proves the predicate. This proves the hook calls it, which is the
    /// half that would silently rot. Evidence is the agent's own log and a drained spool: the
    /// background pass reads the record the hook has just written, so for <c>start</c> and
    /// <c>quit</c> the spool file is gone afterwards and for the other two it is still sitting
    /// there.
    /// <para>
    /// Skipped when either binary has not been published, so a clone that has only run
    /// <c>dotnet test</c> stays green. CI publishes both before it tests.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("start", true)]
    [InlineData("quit", true)]
    [InlineData("game-start", false)]
    [InlineData("game-end", false)]
    public void The_real_hook_starts_a_pass_for_those_two_events_and_for_no_other(string hookEvent, bool spawns)
    {
        var hook = PublishedBinary("RomMBat.Hook", "rommbat-hook.exe");
        var agent = PublishedBinary("RomMBat.Agent", "rommbat-agent.exe");

        Assert.SkipWhen(
            hook is null || agent is null,
            "the hook and the agent have not both been published. Run: dotnet publish "
                + "src/RomMBat.Hook -c Release -r win-x64 --self-contained, and the same for "
                + "src/RomMBat.Agent");

        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        Assert.Equal(4, new EsHooks(install).Install(hook).Installed);

        // Where the hook looks for it, derived from the same constant the hook uses.
        var installedAgent = Path.Combine(
            tree.Root,
            SpoolRecord.AgentRelativePath.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(Path.GetDirectoryName(installedAgent)!);
        CopyPublishedTree(Path.GetDirectoryName(agent!)!, Path.GetDirectoryName(installedAgent)!);

        var log = Path.Combine(install.LogDirectoryPath, "background.log");
        var spool = install.Resolve(SpoolDrain.Directory);

        using (var process = System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(install.Resolve(EsHooks.PathFor(hookEvent)))
            {
                UseShellExecute = false,
            })!)
        {
            process.WaitForExit();
            Assert.Equal(0, process.ExitCode);
        }

        // The hook returns in milliseconds and does not wait for what it started, so the test
        // has to. Process.Start creates the process before the hook can exit, so a pass that
        // has not written its first line within this budget was never going to: the agent
        // reaches Main in 34 ms measured, and this is three orders of magnitude above it.
        var appeared = WaitFor(() => File.Exists(log), NoSpawnBudget);

        if (!spawns)
        {
            // The load-bearing half. Nothing was started, so nothing wrote a log, no database
            // was created, and the record the hook made is still waiting for a pass that
            // something else runs.
            Assert.False(appeared, $"the {hookEvent} hook started a background pass and must not");
            Assert.False(File.Exists(install.DatabasePath));
            Assert.NotEmpty(Directory.GetFiles(spool, "*" + SpoolDrain.Extension));
            return;
        }

        Assert.True(appeared, $"the {hookEvent} hook started no background pass");

        // Wait for the pass to finish before looking at what it did, and before the tree is
        // deleted: it holds the SQLite native library open until it exits.
        Assert.True(
            WaitFor(() => ReadShared(log).Contains($"background {hookEvent} finished", StringComparison.Ordinal),
                TimeSpan.FromSeconds(120)),
            "the background pass never finished");

        Assert.Contains($"background {hookEvent} started", ReadShared(log), StringComparison.Ordinal);

        // It did the work rather than merely starting: the record the hook wrote moments
        // earlier has been drained into the journal.
        Assert.Empty(Directory.GetFiles(spool, "*" + SpoolDrain.Extension));

        using (var store = LocalStore.OpenAt(install.DatabasePath))
        {
            var entry = Assert.Single(store.Journal.All(limit: 10));
            Assert.Equal(hookEvent, entry.Event switch
            {
                JournalEvent.Start => "start",
                JournalEvent.Quit => "quit",
                _ => entry.Event.ToString(),
            });
        }

        // The process writes its last line before returning from Main, so it is on its way out
        // rather than gone. Give the handles a moment or the tree cannot be deleted.
        WaitFor(() => CanTake(Path.Combine(Path.GetDirectoryName(installedAgent)!, "e_sqlite3.dll")),
            TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// How long to wait before concluding no pass was started.
    /// </summary>
    /// <remarks>
    /// Not arbitrary and not generous for its own sake. <c>Process.Start</c> creates the child
    /// before the hook can return, and the hook has already exited by the time this is
    /// measured, so a pass that has written nothing by now does not exist. The agent reaches
    /// <c>Main</c> in 34 ms on a USB stick (finding 195) and opens the log immediately after.
    /// </remarks>
    private static TimeSpan NoSpawnBudget => TimeSpan.FromSeconds(15);

    /// <summary>Reads a file another process still has open for writing.</summary>
    private static string ReadShared(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }

    /// <summary>Whether a file can be opened exclusively, which means nothing else holds it.</summary>
    private static bool CanTake(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool WaitFor(Func<bool> condition, TimeSpan budget)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();

        while (clock.Elapsed < budget)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(100);
        }

        return condition();
    }

    /// <summary>
    /// Copies a publish output beside the hook's expected agent path.
    /// </summary>
    /// <remarks>
    /// A single-file publish is one executable plus a native SQLite library beside it, so the
    /// whole directory travels rather than the one file.
    /// </remarks>
    private static void CopyPublishedTree(string from, string to)
    {
        foreach (var file in Directory.GetFiles(from))
        {
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)), overwrite: true);
        }
    }

    /// <summary>Where a publish would have put one binary, or null when nothing has.</summary>
    private static string? PublishedBinary(string project, string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, fileName),
            Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..", "src", project,
                "bin", "Release", "net10.0", "win-x64", "publish", fileName)),
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
