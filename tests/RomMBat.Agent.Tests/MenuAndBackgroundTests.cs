using RomMBat.Agent.Tests.Support;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Agent.Tests;

/// <summary>
/// The two subcommands M7 stage 7a adds, driven through the same dispatch a user gets.
/// </summary>
/// <remarks>
/// <c>background</c> is the one subcommand nobody is expected to type. It is exercised here
/// anyway, because the interesting cases are the ones a person never sees: an unreachable
/// server, and an event that must start nothing.
/// </remarks>
[Collection("agent-console")]
public sealed class MenuAndBackgroundTests
{
    [Fact]
    public async Task Menu_install_reports_every_path_and_status_then_finds_it()
    {
        using var tree = TempRetroBatTree.Create();

        var installed = await AgentRunner.RunAsync(tree, "menu", "install");

        Assert.Equal(0, installed.ExitCode);
        Assert.True(installed.Wrote("system/es_menu/rommbat.menu"));
        Assert.True(installed.Wrote("system/es_menu/gamelist.xml"));
        Assert.True(installed.Wrote("system/es_menu/media/rommbat-logo.png"));

        var status = await AgentRunner.RunAsync(tree, "menu", "status");

        Assert.Equal(0, status.ExitCode);
        Assert.True(status.Wrote("RomMBat is in the EmulationStation menu."));
        Assert.True(status.Wrote("It shows as   RomMBat"));
    }

    [Fact]
    public async Task Menu_status_says_a_name_the_user_changed_is_theirs_rather_than_correcting_it()
    {
        using var tree = TempRetroBatTree.Create();
        await AgentRunner.RunAsync(tree, "menu", "install");

        var install = tree.Install();
        var path = install.Resolve(EsMenuEntry.GamelistPath);
        var document = GamelistDocument.Load(path);
        document.Apply(new GamelistEntry(EsMenuEntry.EntryPath, [new("name", "Game Sync")]));
        document.WriteIfChanged(path);

        var status = await AgentRunner.RunAsync(tree, "menu", "status");

        Assert.True(status.Wrote("It shows as   Game Sync"));
        Assert.True(status.Wrote("is left alone"));
    }

    [Fact]
    public async Task Menu_status_names_half_a_registration_because_the_screen_cannot()
    {
        // Only the .menu shows as a bare filename; only the gamelist element shows as nothing
        // at all. Neither is diagnosable from the front end.
        using var tree = TempRetroBatTree.Create();
        await AgentRunner.RunAsync(tree, "menu", "install");

        File.Delete(tree.Install().Resolve(EsMenuEntry.MenuPath));

        var status = await AgentRunner.RunAsync(tree, "menu", "status");

        Assert.True(status.Wrote("Only the gamelist half is there"));
        Assert.True(status.Wrote("menu install"));
    }

    [Fact]
    public async Task Menu_uninstall_is_clean_when_there_is_nothing_to_remove()
    {
        using var tree = TempRetroBatTree.Create();

        var removed = await AgentRunner.RunAsync(tree, "menu", "uninstall");

        Assert.Equal(0, removed.ExitCode);
        Assert.True(removed.Wrote("absent"));
        Assert.Equal(string.Empty, removed.Error);
    }

    [Theory]
    [InlineData("game-start")]
    [InlineData("game-end")]
    [InlineData("shutdown")]
    [InlineData("")]
    public async Task Background_refuses_any_event_that_is_not_start_or_quit(string hookEvent)
    {
        // Defence in depth on the rule-4 boundary. The hook already starts nothing for these,
        // and if something ever does, the pass itself says no and names the rule.
        using var tree = TempRetroBatTree.Create();

        var run = hookEvent.Length == 0
            ? await AgentRunner.RunAsync(tree, "background")
            : await AgentRunner.RunAsync(tree, "background", hookEvent);

        Assert.Equal(2, run.ExitCode);
        Assert.True(run.Complained("is not an event this runs for"));
        Assert.True(run.Complained("rule 4"));
    }

    [Fact]
    public async Task Background_start_flushes_with_the_server_unreachable_and_leaves_a_log()
    {
        // The ordinary case on a handheld away from the network, and the case nobody watches:
        // it is spawned with no window, so the log is the only account of it.
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        install.EnsureAppDirectories();

        WriteSpoolRecord(install, "start");

        var run = await AgentRunner.RunAsync(tree, "background", "start");

        // Not paired is a real refusal and everything local still happened.
        Assert.Empty(Directory.GetFiles(install.Resolve(SpoolDrain.Directory), "*" + SpoolDrain.Extension));

        using var store = LocalStore.OpenAt(install.DatabasePath);
        Assert.Single(store.Journal.All(limit: 10));

        var log = File.ReadAllText(Path.Combine(install.LogDirectoryPath, "background.log"));
        Assert.Contains("background start started", log, StringComparison.Ordinal);
        Assert.Contains("background start finished", log, StringComparison.Ordinal);
        _ = run;
    }

    [Fact]
    public async Task Background_quit_applies_what_was_queued_for_exactly_this_moment()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        install.EnsureAppDirectories();

        var key = "ps2[\"Armored Core 3 (USA).chd\"].pcsx2_slot1_memory";
        int queuedId;

        using (var store = LocalStore.Open(install))
        {
            AddRom(install, store, 42, "ps2", "Armored Core 3 (USA).chd");
            queuedId = QueueConversion(store);
        }

        var run = await AgentRunner.RunAsync(tree, "background", "quit");

        // The pass returns the flush's exit code, and an unpaired install is 4, NotPaired.
        // That is the honest answer and it does not mean the pass did nothing: the config
        // half and the whole local half of the flush ran before anything needed a server.
        Assert.Equal(4, run.ExitCode);

        var settings = EsSettingsFile.Load(install.Resolve(EsSettingsFile.Location));
        Assert.Equal("game", settings.Value(key));

        using (var store = LocalStore.OpenAt(install.DatabasePath))
        {
            Assert.Empty(store.PendingConfig.ListOutstanding());

            // Readable afterwards, which is the whole reason the row survives being applied.
            var finished = Assert.Single(store.PendingConfig.ListFinished());
            Assert.Equal(queuedId, finished.Id);
            Assert.Equal(PendingConfigResult.Applied, finished.Result);
            Assert.Contains("pcsx2_slot1_memory", finished.Detail!, StringComparison.Ordinal);

            // And the conversion went through the ordinary writer, so save_conversion knows
            // what the file held before.
            var conversion = Assert.Single(store.SaveConversions.List());
            Assert.Equal(PriorSettingState.Absent, conversion.PriorState);
        }
    }

    [Fact]
    public async Task Replaying_a_quit_pass_offline_changes_nothing_the_first_one_did()
    {
        // Offline is a working state, and a quit pass is the one thing that can run twice for
        // reasons outside anyone's control: ES restarts, a stick is pulled, a hook fires twice.
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        install.EnsureAppDirectories();

        using (var store = LocalStore.Open(install))
        {
            AddRom(install, store, 42, "ps2", "Armored Core 3 (USA).chd");
            QueueConversion(store);
        }

        WriteSpoolRecord(install, "quit");
        await AgentRunner.RunAsync(tree, "background", "quit");

        var settingsAfterFirst = File.ReadAllBytes(install.Resolve(EsSettingsFile.Location));

        string finishedDetail;
        int journalRows;

        using (var store = LocalStore.OpenAt(install.DatabasePath))
        {
            finishedDetail = Assert.Single(store.PendingConfig.ListFinished()).Detail!;
            journalRows = store.Journal.All(limit: 100).Count;
        }

        WriteSpoolRecord(install, "quit");
        var again = await AgentRunner.RunAsync(tree, "background", "quit");

        Assert.Equal(4, again.ExitCode);

        // The setting is not rewritten, no second conversion is recorded, and the finished row
        // is the one the first pass wrote.
        Assert.Equal(settingsAfterFirst, File.ReadAllBytes(install.Resolve(EsSettingsFile.Location)));

        using (var store = LocalStore.OpenAt(install.DatabasePath))
        {
            Assert.Single(store.SaveConversions.List());
            Assert.Equal(finishedDetail, Assert.Single(store.PendingConfig.ListFinished()).Detail);

            // The second spool record became its own journal row, which is right: two quits
            // happened. Nothing else moved.
            Assert.Equal(journalRows + 1, store.Journal.All(limit: 100).Count);
        }
    }

    [Fact]
    public async Task A_queued_change_survives_a_pass_that_could_not_apply_it()
    {
        // Nothing is lost when the world moved: the row is finished with the reason, so the
        // next time a person opens RomMBat there is something to read.
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        install.EnsureAppDirectories();

        using (var store = LocalStore.Open(install))
        {
            AddRom(install, store, 42, "ps2", "Armored Core 3 (USA).chd");
            QueueConversion(store);

            // Evicted between queueing and quitting.
            store.Files.Remove(RelativePath.Create("roms/ps2/Armored Core 3 (USA).chd"));
        }

        await AgentRunner.RunAsync(tree, "background", "quit");

        using (var store = LocalStore.OpenAt(install.DatabasePath))
        {
            Assert.Empty(store.PendingConfig.ListOutstanding());

            var finished = Assert.Single(store.PendingConfig.ListFinished());
            Assert.Equal(PendingConfigResult.Refused, finished.Result);
            Assert.Contains("no longer on this device", finished.Detail!, StringComparison.Ordinal);
        }

        var report = await AgentRunner.RunAsync(tree, "saves", "--no-scan");
        Assert.True(report.Wrote("refused"));
    }

    [Fact]
    public async Task A_change_that_throws_is_finished_rather_than_left_to_poison_the_next_quit()
    {
        // The queue is never allowed to hold the saves hostage. A write that throws rather than
        // refusing, and is left unrecorded, would be re-entered by every later quit before it
        // reached the flush, so one bad row would stop this machine flushing at all.
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        install.EnsureAppDirectories();

        using (var store = LocalStore.Open(install))
        {
            AddRom(install, store, 42, "ps2", "Armored Core 3 (USA).chd");
            QueueConversion(store);
        }

        // Readable, so the rest of the pass is unaffected, and unwritable, so the rename at the
        // end of WriteIfChanged throws where a full or read-only volume would.
        var settings = install.Resolve(EsSettingsFile.Location);
        Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
        File.WriteAllText(settings, "<config />");
        File.SetAttributes(settings, FileAttributes.ReadOnly);

        AgentRun run;

        try
        {
            run = await AgentRunner.RunAsync(tree, "background", "quit");
        }
        finally
        {
            File.SetAttributes(settings, FileAttributes.Normal);
        }

        // 4 is NotPaired, from the flush. A pass the throw reached would not have one.
        Assert.Equal(4, run.ExitCode);

        using (var store = LocalStore.OpenAt(install.DatabasePath))
        {
            Assert.Empty(store.PendingConfig.ListOutstanding());

            var finished = Assert.Single(store.PendingConfig.ListFinished());
            Assert.Equal(PendingConfigResult.Failed, finished.Result);
            Assert.False(string.IsNullOrWhiteSpace(finished.Detail));

            // Nothing was recorded as converted, so a later revert has nothing to undo.
            Assert.Empty(store.SaveConversions.List());
        }

        // And it said so in the one place a pass with no console window can.
        var log = File.ReadAllText(Path.Combine(install.LogDirectoryPath, "background.log"));
        Assert.Contains("ps2/Armored Core 3 (USA).chd: Failed", log, StringComparison.Ordinal);
        Assert.Contains("finished, flush exit 4", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_change_that_worked_leaves_no_empty_section_behind_it()
    {
        // saves reports only the queued changes that did not work, so a history of nothing but
        // successes has nothing to say and must not say it with a blank line.
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        install.EnsureAppDirectories();

        using (var store = LocalStore.Open(install))
        {
            AddRom(install, store, 42, "ps2", "Armored Core 3 (USA).chd");
            QueueConversion(store);
        }

        await AgentRunner.RunAsync(tree, "background", "quit");

        var report = await AgentRunner.RunAsync(tree, "saves", "--no-scan");
        var lines = report.Out.Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.DoesNotContain("will be made when EmulationStation next closes", lines, StringComparison.Ordinal);
        Assert.DoesNotContain("\n\n\n", lines, StringComparison.Ordinal);
    }

    private static void WriteSpoolRecord(RetroBatInstall install, string hookEvent)
    {
        Directory.CreateDirectory(install.Resolve(SpoolDrain.Directory));
        Spool.Write(install.RootPath, new SpoolRecord(hookEvent, DateTimeOffset.UtcNow, 4242, []));
    }

    private static int QueueConversion(LocalStore store) =>
        store.PendingConfig.Queue(new PendingConfigRequest
        {
            RomId = 42,
            System = "ps2",
            FsName = "Armored Core 3 (USA).chd",
            SettingKey = "pcsx2_slot1_memory",
            DesiredState = DesiredSettingState.Set,
            DesiredValue = "game",
            Reason = "a per-game memory card for 'Armored Core 3 (USA).chd'",
            QueuedAtUtc = DateTimeOffset.UtcNow,
        });

    private static void AddRom(RetroBatInstall install, LocalStore store, int romId, string folder, string fileName)
    {
        var path = RelativePath.Create($"roms/{folder}/{fileName}");
        var absolute = install.Resolve(path);

        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllText(absolute, "rom bytes");

        store.Files.Record(new LocalFile
        {
            Path = path,
            Folder = folder,
            RomId = romId,
            Kind = LocalFileKind.Rom,
            FileName = fileName,
            SizeBytes = 9,
        });
    }
}
