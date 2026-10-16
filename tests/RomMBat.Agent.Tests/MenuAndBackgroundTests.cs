using RomMBat.Agent.Tests.Support;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Agent.Tests;

/// <summary>
/// <c>menu</c> and <c>background</c>, driven through the same dispatch a user gets.
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
        // Defense in depth on the rule-4 boundary. The hook already starts nothing for these,
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
    public async Task Background_quit_forces_the_folder_card_over_another_value()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        install.EnsureAppDirectories();

        using (var store = LocalStore.Open(install))
        {
            AddRom(install, store, 42, "ps2", "Armored Core 3 (USA).chd");
        }

        WriteSettings(install, """<config><string name="ps2.pcsx2_slot1_memory" value="standard" /></config>""");

        var run = await AgentRunner.RunAsync(tree, "background", "quit");

        // The pass returns the flush's exit code, and an unpaired install is 4, NotPaired.
        // That is the honest answer and it does not mean the pass did nothing: the setting and
        // the whole local half of the flush ran before anything needed a server.
        Assert.Equal(4, run.ExitCode);
        Assert.Equal("folder", ReadSetting(install));

        var log = File.ReadAllText(Path.Combine(install.LogDirectoryPath, "background.log"));
        Assert.Contains("set ps2.pcsx2_slot1_memory = folder (was standard)", log, StringComparison.Ordinal);
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
        }

        WriteSpoolRecord(install, "quit");
        await AgentRunner.RunAsync(tree, "background", "quit");

        var settingsAfterFirst = File.ReadAllBytes(install.Resolve(EsSettingsFile.Location));
        int journalRows;

        using (var store = LocalStore.OpenAt(install.DatabasePath))
        {
            journalRows = store.Journal.All(limit: 100).Count;
        }

        WriteSpoolRecord(install, "quit");
        var again = await AgentRunner.RunAsync(tree, "background", "quit");

        Assert.Equal(4, again.ExitCode);
        Assert.Equal(settingsAfterFirst, File.ReadAllBytes(install.Resolve(EsSettingsFile.Location)));

        using (var store = LocalStore.OpenAt(install.DatabasePath))
        {
            // The second spool record became its own journal row, which is right: two quits
            // happened. Nothing else moved.
            Assert.Equal(journalRows + 1, store.Journal.All(limit: 100).Count);
        }
    }

    [Fact]
    public async Task An_install_with_no_ps2_game_keeps_a_settings_file_RomMBat_never_touched()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        install.EnsureAppDirectories();

        using (var store = LocalStore.Open(install))
        {
            AddRom(install, store, 7, "nes", "Contra (USA).nes");
        }

        WriteSettings(install, "<config />");
        var before = File.ReadAllBytes(install.Resolve(EsSettingsFile.Location));

        await AgentRunner.RunAsync(tree, "background", "quit");

        Assert.Equal(before, File.ReadAllBytes(install.Resolve(EsSettingsFile.Location)));
    }

    [Fact]
    public async Task A_setting_that_cannot_be_written_does_not_stop_the_flush()
    {
        // The settings never hold the saves hostage. A write that throws is logged, the flush
        // still runs, and the next quit tries again because the file still lacks the value.
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        install.EnsureAppDirectories();

        using (var store = LocalStore.Open(install))
        {
            AddRom(install, store, 42, "ps2", "Armored Core 3 (USA).chd");
        }

        // Readable, so the rest of the pass is unaffected, and unwritable, so the rename at the
        // end of WriteIfChanged throws where a full or read-only volume would.
        var settings = WriteSettings(install, "<config />");
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
        Assert.Null(ReadSetting(install));

        var log = File.ReadAllText(Path.Combine(install.LogDirectoryPath, "background.log"));
        Assert.Contains("settings could not be written", log, StringComparison.Ordinal);
        Assert.Contains("finished, flush exit 4", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Saves_names_a_pending_setting_until_a_quit_writes_it()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        install.EnsureAppDirectories();

        using (var store = LocalStore.Open(install))
        {
            AddRom(install, store, 42, "ps2", "Armored Core 3 (USA).chd");
        }

        var before = await AgentRunner.RunAsync(tree, "saves", "--no-scan");
        Assert.True(before.Wrote("will be written when EmulationStation next closes"));
        Assert.True(before.Wrote("ps2.pcsx2_slot1_memory = folder  (now absent)"));

        await AgentRunner.RunAsync(tree, "background", "quit");

        var after = await AgentRunner.RunAsync(tree, "saves", "--no-scan");
        var lines = after.Out.Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.DoesNotContain("will be written when EmulationStation next closes", lines, StringComparison.Ordinal);
        Assert.DoesNotContain("\n\n\n", lines, StringComparison.Ordinal);
    }

    private static string WriteSettings(RetroBatInstall install, string xml)
    {
        var settings = install.Resolve(EsSettingsFile.Location);
        Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
        File.WriteAllText(settings, xml);
        return settings;
    }

    private static string? ReadSetting(RetroBatInstall install) =>
        EsSettingsFile.Load(install.Resolve(EsSettingsFile.Location)).Value("ps2.pcsx2_slot1_memory");

    private static void WriteSpoolRecord(RetroBatInstall install, string hookEvent)
    {
        Directory.CreateDirectory(install.Resolve(SpoolDrain.Directory));
        Spool.Write(install.RootPath, new SpoolRecord(hookEvent, DateTimeOffset.UtcNow, 4242, []));
    }

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
