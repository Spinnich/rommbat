using RomMBat.Core;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The hooks, the spool they write, and the journal they end up in.
/// </summary>
/// <remarks>
/// The interleaving test is the one the plan names by hand. Everything else here exists
/// because a hook runs unattended inside the game-launch path, so its failures are silent by
/// nature and a test is the only place they are visible.
/// </remarks>
public class HookAndJournalTests
{
    /// <summary>
    /// The rom name that defeated both scripted hook forms, and two worse ones.
    /// </summary>
    /// <remarks>
    /// The first is verbatim from M0 probe 7b: a <c>.bat</c> would not start on it because ES
    /// quotes an argument containing a space and cmd then mangles the line, and a <c>.ps1</c>
    /// would not start on it because ES omits <c>-File</c> and PowerShell reparses the
    /// parenthesis as code. The others check that the spool format cannot be forged by a name.
    /// </remarks>
    public static TheoryData<string> HardNames =>
    [
        "Gradius 2 (Japan, Europe) (En) (Wii U Virtual Console).zip",
        "Metal Gear Solid (USA) (Disc 1) (Rev 1).chd",
        @"weird\name\with=equals.zip",
        "name\nwith\r\nnewlines.zip",
        "Pokémon Édition Rouge Feu (France).gba",
    ];

    [Theory]
    [MemberData(nameof(HardNames))]
    public void A_spool_record_round_trips_a_name_that_broke_the_scripted_hooks(string name)
    {
        var record = new SpoolRecord(
            "game-start",
            new DateTimeOffset(2026, 8, 16, 11, 22, 18, 72, TimeSpan.Zero),
            4321,
            [$@"E:\RetroBat\roms\msx1\{name}", Path.GetFileNameWithoutExtension(name), name]);

        var parsed = SpoolRecord.Parse(record.Render());

        Assert.NotNull(parsed);
        Assert.Equal(record.Event, parsed.Event);
        Assert.Equal(record.At, parsed.At);
        Assert.Equal(record.ProcessId, parsed.ProcessId);
        Assert.Equal(record.Arguments, parsed.Arguments);
    }

    [Fact]
    public void A_name_carrying_a_newline_cannot_forge_a_second_field()
    {
        // The whole reason the format escapes rather than trusting its input. A rom named
        // "x\nevent=quit" must not turn one game-start into a quit.
        var record = new SpoolRecord(
            "game-start",
            DateTimeOffset.UnixEpoch,
            1,
            ["x\nevent=quit\narg=forged"]);

        var parsed = SpoolRecord.Parse(record.Render());

        Assert.NotNull(parsed);
        Assert.Equal("game-start", parsed.Event);
        Assert.Equal(["x\nevent=quit\narg=forged"], parsed.Arguments);
    }

    [Theory]
    [InlineData("game-start", "game-start")]
    [InlineData("GAME-END", "game-end")]
    [InlineData("quit", "quit")]
    [InlineData("game-selected", null)]
    [InlineData("scripts", null)]
    public void The_event_comes_from_the_folder_the_hook_was_installed_into(string folder, string? expected)
    {
        // One built file serves all four events, which is what keeps the installed cost to
        // four copies of one 12.8 MB exe rather than four different builds.
        Assert.Equal(expected, SpoolRecord.EventFromDirectory(Path.Combine("x", "scripts", folder)));
    }

    [Fact]
    public void A_hook_four_levels_down_walks_up_to_the_root()
    {
        // The arithmetic docs/PLAN.md had wrong for three revisions. RetroBat's own
        // start/updatestores.bat goes up three levels because it is calling
        // emulatorLauncher.exe in emulationstation/; the root is a fourth. The hook walks to a
        // marker rather than counting, so a changed layout is a miss and never a wrong answer.
        using var tree = TempRetroBatTree.Create();
        var hookDirectory = Path.Combine(
            tree.Root, "emulationstation", ".emulationstation", "scripts", "game-start");
        Directory.CreateDirectory(hookDirectory);

        Assert.Equal(4, Path.GetRelativePath(tree.Root, hookDirectory).Split(Path.DirectorySeparatorChar).Length);
        Assert.Equal(tree.Root, RootMarkers.WalkUp(hookDirectory));

        // Three levels reaches emulationstation/, which is not a root and must not answer.
        Assert.False(RootMarkers.IsRoot(Path.Combine(tree.Root, "emulationstation")));

        // The agent's discovery is the same code, so the two cannot come to disagree.
        Assert.Equal(tree.Root, RetroBatRoot.Locate(tree.Root)?.RootPath);
        Assert.True(RetroBatRoot.IsRoot(tree.Root));
    }

    [Fact]
    public void Installing_adds_a_file_beside_what_is_already_there_and_uninstalling_takes_only_ours()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        // RetroBat ships this one, and ES runs every file in the folder in name order.
        var shipped = Path.Combine(
            tree.Root, "emulationstation", ".emulationstation", "scripts", "start", "updatestores.bat");
        Directory.CreateDirectory(Path.GetDirectoryName(shipped)!);
        File.WriteAllText(shipped, @"%~dp0..\..\..\emulatorLauncher.exe -updatestores");

        var source = StandInHook(tree, "build one");
        var hooks = new EsHooks(install);

        var first = hooks.Install(source);

        Assert.Equal(4, first.Installed);
        Assert.Equal(0, first.Failed);
        Assert.True(File.Exists(shipped));
        Assert.True(hooks.IsInstalled());

        foreach (var hookEvent in EsHooks.Events)
        {
            Assert.True(File.Exists(install.Resolve(EsHooks.PathFor(hookEvent))));
        }

        // The zz- prefix is what puts RomMBat after anything shipped.
        Assert.EndsWith(
            "zz-rommbat-hook.exe",
            EsHooks.PathFor("start").Value,
            StringComparison.Ordinal);
        Assert.True(
            string.CompareOrdinal("updatestores.bat", EsHooks.FileName) < 0,
            "the installed name must sort after RetroBat's own start hook");

        var removed = hooks.Uninstall();

        Assert.Equal(4, removed.Removed);
        Assert.True(File.Exists(shipped));
        Assert.False(hooks.IsInstalled());
    }

    [Fact]
    public void Installing_twice_is_a_no_op_and_a_new_build_replaces_the_old_one()
    {
        using var tree = TempRetroBatTree.Create();
        var hooks = new EsHooks(tree.Install());

        Assert.Equal(4, hooks.Install(StandInHook(tree, "build one")).Installed);

        var again = hooks.Install(StandInHook(tree, "build one"));
        Assert.Equal(4, again.Steps.Count(step => step.Action == EsHookAction.AlreadyCurrent));
        Assert.Equal(0, again.Installed);

        // Same length, different bytes, which is exactly the case a size check would miss and
        // the one that matters: a hook left over from a previous release.
        var upgraded = hooks.Install(StandInHook(tree, "build two"));
        Assert.Equal(4, upgraded.Updated);
    }

    [Fact]
    public void Draining_relativises_the_absolute_path_the_hook_was_given()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        using var store = LocalStore.Open(install);

        var rom = Path.Combine(tree.Root, "roms", "msx1", "Gradius 2 (Japan, Europe) (En).zip");
        Write(install, new SpoolRecord(
            "game-start",
            DateTimeOffset.Parse("2026-08-16T11:22:18Z", System.Globalization.CultureInfo.InvariantCulture),
            99,
            [rom, "Gradius 2 (Japan, Europe) (En)", "Gradius 2"]));

        var outcome = new SpoolDrain(install, store).Drain();

        Assert.Equal(1, outcome.Ingested);

        var entry = Assert.Single(store.Journal.Open());
        Assert.Equal(JournalEvent.GameStart, entry.Event);
        Assert.Equal("roms/msx1/Gradius 2 (Japan, Europe) (En).zip", entry.RomPath?.Value);
        Assert.Equal("Gradius 2", entry.DisplayName);
        Assert.Equal(99, entry.ProcessId);

        // The file is gone, so a second pass is a no-op rather than a duplicate session.
        Assert.True(new SpoolDrain(install, store).Drain().IsNoOp);
        Assert.Single(store.Journal.Open());
    }

    [Fact]
    public void A_rom_path_outside_the_tree_is_recorded_without_it_rather_than_dropped()
    {
        // Never persist an absolute path is the rule, and losing the event is not the
        // alternative: the basename and display name still identify what ran.
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        using var store = LocalStore.Open(install);

        Write(install, new SpoolRecord(
            "game-start",
            DateTimeOffset.UnixEpoch,
            7,
            [@"Z:\SomewhereElse\roms\snes\Game.sfc", "Game", "Game"]));

        Assert.Equal(1, new SpoolDrain(install, store).Drain().Ingested);

        var entry = Assert.Single(store.Journal.Open());
        Assert.Null(entry.RomPath);
        Assert.Equal("Game", entry.RomBasename);
    }

    [Fact]
    public void A_half_written_record_is_left_alone_until_it_is_old_enough_to_be_abandoned()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        using var store = LocalStore.Open(install);

        var spool = install.Resolve(SpoolDrain.Directory);
        Directory.CreateDirectory(spool);
        var partial = Path.Combine(spool, "20260816T000000-1-abc.tmp");
        File.WriteAllText(partial, "rommbat-hook-1\nevent=game-st");

        // Age is measured against the file's own mtime, so the fake clock has to start where
        // the file says it was written rather than at an arbitrary date.
        var now = DateTimeOffset.Parse("2026-08-16T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        File.SetLastWriteTimeUtc(partial, now.UtcDateTime);
        var clock = new TestTimeProvider(now);

        // A hook mid-write is the ordinary concurrent case, so a fresh .tmp is not touched.
        Assert.True(new SpoolDrain(install, store, clock).Drain().IsNoOp);
        Assert.True(File.Exists(partial));

        clock.Advance(SpoolDrain.AbandonedAfter + TimeSpan.FromMinutes(1));

        Assert.Equal(1, new SpoolDrain(install, store, clock).Drain().Abandoned);
        Assert.False(File.Exists(partial));
        Assert.Empty(store.Journal.All());
    }

    [Fact]
    public void Only_one_process_holds_the_flush_lock_and_losing_the_race_is_not_an_error()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        install.EnsureAppDirectories();

        using var held = TreeLock.TryAcquire(install);
        Assert.NotNull(held);

        // Three game-end hooks in flight at once is measured, not hypothetical. The second
        // agent exits rather than waiting, because the work is already being done.
        Assert.Null(TreeLock.TryAcquire(install));

        held.Dispose();

        using var next = TreeLock.TryAcquire(install);
        Assert.NotNull(next);
    }

    /// <summary>
    /// The plan's own test: interleaved appends from separate processes leave a readable
    /// journal.
    /// </summary>
    /// <remarks>
    /// Driven with the <b>real</b> hook executable in separate OS processes, started together,
    /// because the failure this guards against is cross-process and a single-process
    /// simulation cannot reproduce it. M0 probe 1 caught three <c>game-end</c> hooks in flight
    /// at once, interleaving writes to one shared file; a spool file per record is what makes
    /// that unrepresentable rather than merely unlikely.
    /// <para>
    /// Skipped when the hook has not been published, so a clone that has only run
    /// <c>dotnet test</c> stays green. CI publishes it.
    /// </para>
    /// </remarks>
    [Fact]
    public void Interleaved_appends_from_separate_processes_leave_a_readable_journal()
    {
        var hook = PublishedHook();

        Assert.SkipWhen(
            hook is null,
            "rommbat-hook.exe has not been published. Run: dotnet publish src/RomMBat.Hook "
                + "-c Release -r win-x64 --self-contained");

        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        using var store = LocalStore.Open(install);

        Assert.Equal(4, new EsHooks(install).Install(hook).Installed);

        const int PerEvent = 8;
        var processes = new List<System.Diagnostics.Process>();

        foreach (var hookEvent in EsHooks.Events)
        {
            var path = install.Resolve(EsHooks.PathFor(hookEvent));

            for (var i = 0; i < PerEvent; i++)
            {
                var start = new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = false };

                if (hookEvent == "game-start")
                {
                    // The name both scripted forms could not survive.
                    start.ArgumentList.Add(Path.Combine(
                        tree.Root, "roms", "msx1", $"Gradius 2 (Japan, Europe) (En) {i}.zip"));
                    start.ArgumentList.Add($"Gradius 2 (Japan, Europe) (En) {i}");
                    start.ArgumentList.Add($"Gradius 2 ({i})");
                }

                processes.Add(System.Diagnostics.Process.Start(start)!);
            }
        }

        foreach (var process in processes)
        {
            process.WaitForExit();
            Assert.Equal(0, process.ExitCode);
            process.Dispose();
        }

        var outcome = new SpoolDrain(install, store).Drain();

        Assert.Equal(EsHooks.Events.Count * PerEvent, outcome.Ingested);
        Assert.Equal(0, outcome.Malformed);

        var entries = store.Journal.All(limit: 1000);
        Assert.Equal(EsHooks.Events.Count * PerEvent, entries.Count);

        // Every record is whole: nothing lost a field to another process's write, and the
        // shared sequence handed out one number each.
        Assert.Equal(entries.Count, entries.Select(entry => entry.LocalSequence).Distinct().Count());

        var launches = entries.Where(entry => entry.Event == JournalEvent.GameStart).ToList();
        Assert.Equal(PerEvent, launches.Count);
        Assert.All(launches, entry => Assert.StartsWith("roms/msx1/Gradius 2 (Japan, Europe) (En) ", entry.RomPath!.Value.Value, StringComparison.Ordinal));
        Assert.Equal(PerEvent, launches.Select(entry => entry.RomPath!.Value.Value).Distinct().Count());
    }

    /// <summary>Where a publish would have put the hook, or null when nothing has.</summary>
    private static string? PublishedHook()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "rommbat-hook.exe"),
            Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..", "src", "RomMBat.Hook",
                "bin", "Release", "net10.0", "win-x64", "publish", "rommbat-hook.exe")),
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// A stand-in for the 12.8 MB hook, so the install tests do not need a publish.
    /// </summary>
    /// <remarks>
    /// Padded to a fixed length so two different "builds" have equal sizes, which is what
    /// makes the replace-on-upgrade test meaningful.
    /// </remarks>
    private static string StandInHook(TempRetroBatTree tree, string body)
    {
        var path = Path.Combine(tree.AppDirectory, "rommbat-hook.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, body.PadRight(64, '.'));
        return path;
    }

    private static void Write(RetroBatInstall install, SpoolRecord record) =>
        Spool.Write(install.RootPath, record);
}
