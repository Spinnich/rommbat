using RomMBat.Core.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// RetroBat's own GameCube save reconciliation, detected and reported and never acted on.
/// </summary>
/// <remarks>
/// <b>Every number here came off a real install, and the repository said something else first.</b>
/// Four documents described <c>dolphin_sync_saves</c> as RetroBat copying between the dolphin
/// and libretro-dolphin save folders on its own schedule. It is GameCube only, it runs once per
/// launch inside emulatorlauncher before Dolphin starts, and the two locations are one
/// directory and a <c>Card A</c> subdirectory of it.
/// <para>
/// The case these tests exist for is the resurrection: a <c>.gci</c> present in <c>Card A</c>
/// and absent beside it is copied back out at the next launch. Driven on K:, deleting the
/// region-root file and launching brought it back holding the <i>previous</i> session's bytes,
/// and the only trace was one INFO line.
/// </para>
/// </remarks>
public class DolphinSaveSyncTests
{
    private const string Region = "saves/gamecube/dolphin-emu/User/GC/USA";
    private const string Gci = "41-G3SE-BUST A MOVE 3000.gci";

    [Theory]
    [InlineData("true")]
    [InlineData("1")]
    [InlineData("ENABLED")]
    [InlineData("on")]
    [InlineData("Yes")]
    public void Every_value_RetroBat_reads_as_true_is_read_as_true_here(string value)
    {
        var settings = SettingsWith(("gamecube.dolphin_sync_saves", value));

        Assert.Equal(DolphinSyncScope.System, DolphinSaveSync.Read(settings).Scope);
    }

    [Theory]
    [InlineData("false")]
    [InlineData("0")]
    [InlineData("auto")]
    [InlineData("")]
    public void A_key_present_at_any_other_value_is_off_because_that_is_what_RetroBat_does(string value)
    {
        // getOptBoolean accepts five spellings and nothing else, so a key present at "auto"
        // leaves the sync disabled. Reporting it as on would warn about something that is not
        // going to happen.
        var settings = SettingsWith(("gamecube.dolphin_sync_saves", value));

        Assert.Equal(DolphinSyncScope.Off, DolphinSaveSync.Read(settings).Scope);
    }

    [Fact]
    public void The_per_game_level_wins_over_the_system_level_and_the_system_level_over_global()
    {
        var settings = SettingsWith(
            ("global.dolphin_sync_saves", "true"),
            ("gamecube.dolphin_sync_saves", "false"),
            ("gamecube[\"Bust-A-Move 3000 (USA).rvz\"].dolphin_sync_saves", "true"));

        var (scope, key) = DolphinSaveSync.Read(settings, "Bust-A-Move 3000 (USA).rvz");

        Assert.Equal(DolphinSyncScope.PerGame, scope);
        Assert.Equal("gamecube[\"Bust-A-Move 3000 (USA).rvz\"].dolphin_sync_saves", key);

        // Without a rom name the per-game level cannot be consulted, and the system level is
        // off, so global decides. That ordering is es_settings.cfg's, not ours.
        Assert.Equal(DolphinSyncScope.Global, DolphinSaveSync.Read(settings).Scope);
    }

    [Fact]
    public void A_rom_name_with_no_extension_never_reaches_the_per_game_level()
    {
        // The per-game form keys on the rom *filename*. A bare title is a caller mistake and
        // building a key from it would silently look up something that can never be set.
        var settings = SettingsWith(("gamecube[\"Bust-A-Move 3000\"].dolphin_sync_saves", "true"));

        Assert.Equal(DolphinSyncScope.Off, DolphinSaveSync.Read(settings, "Bust-A-Move 3000").Scope);
    }

    [Fact]
    public void A_copy_with_a_live_file_beside_it_is_counted_but_cannot_resurrect_anything()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        Write(install, $"{Region}/{Gci}", "the live save");
        Write(install, $"{Region}/Card A/{Gci}", "last session");

        var state = DolphinSaveSync.Inspect(install, SettingsWith(("gamecube.dolphin_sync_saves", "true")));

        Assert.True(state.Enabled);
        Assert.Equal(1, state.CopiedFiles);
        Assert.Equal(0, state.RestorableFiles);
        Assert.Contains("RomMBat does not read, upload or evict", DolphinSaveSync.Describe(state), StringComparison.Ordinal);
    }

    [Fact]
    public void A_copy_with_nothing_beside_it_is_named_as_one_the_next_launch_puts_back()
    {
        // This is the measured failure. The region root is empty because something removed the
        // save; Card A still holds it; the next launch copies it back out.
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        Write(install, $"{Region}/Card A/{Gci}", "the previous session");

        var state = DolphinSaveSync.Inspect(install, SettingsWith(("gamecube.dolphin_sync_saves", "true")));

        Assert.Equal(1, state.RestorableFiles);
        Assert.Contains("reappears", DolphinSaveSync.Describe(state), StringComparison.Ordinal);
    }

    [Fact]
    public void Turning_the_option_off_does_not_make_the_copies_go_away_so_they_are_still_reported()
    {
        // Nothing deletes Card A. A user who switched the option off still has the copies, and
        // they regain their effect the moment it is switched back on.
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        Write(install, $"{Region}/Card A/{Gci}", "left behind");

        var state = DolphinSaveSync.Inspect(install, SettingsWith(("gamecube.dolphin_sync_saves", "false")));

        Assert.False(state.Enabled);
        Assert.True(state.WorthReporting);
        Assert.Contains("still here", DolphinSaveSync.Describe(state), StringComparison.Ordinal);
    }

    [Fact]
    public void The_option_on_with_no_copies_yet_warns_about_what_the_next_launch_will_do()
    {
        // The useful moment to catch a user is before the first launch makes the copies. An
        // earlier draft said "0 save files sit in a 'Card A'", which described a directory that
        // does not exist.
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        Write(install, $"{Region}/{Gci}", "an ordinary save");

        var state = DolphinSaveSync.Inspect(install, SettingsWith(("gamecube.dolphin_sync_saves", "true")));
        var described = DolphinSaveSync.Describe(state);

        Assert.True(state.WorthReporting);
        Assert.Equal(0, state.CopiedFiles);
        Assert.Contains("starts making copies", described, StringComparison.Ordinal);
        Assert.DoesNotContain("0 save files", described, StringComparison.Ordinal);
    }

    [Fact]
    public void An_install_with_no_Card_A_and_the_option_off_has_nothing_to_say()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        Write(install, $"{Region}/{Gci}", "an ordinary save");

        Assert.False(DolphinSaveSync.Inspect(install, SettingsWith()).WorthReporting);
    }

    [Fact]
    public void A_dot_old_left_by_an_earlier_reconciliation_is_not_counted_as_a_save()
    {
        // SyncGCSaves renames the loser to .old and never cleans it up. Counting that litter
        // would inflate the number the user is being asked to act on.
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        Write(install, $"{Region}/Card A/{Gci}.old", "litter");

        Assert.False(DolphinSaveSync.Inspect(install, SettingsWith()).WorthReporting);
    }

    [Fact]
    public void The_scanner_reports_it_and_the_row_survives_a_round_trip_through_the_store()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        using var store = LocalStore.Open(install);

        Write(install, $"{Region}/{Gci}", "the live save");
        Write(install, $"{Region}/Card A/{Gci}", "last session");
        WriteSettings(install, ("gamecube.dolphin_sync_saves", "true"));

        new SaveScanner(install, store).Scan();

        var row = Assert.Single(
            store.Unsyncable.List(),
            entry => entry.Reason is UnsyncableReason.ManagedElsewhere);

        Assert.Equal("gamecube", row.System);
        Assert.Equal("dolphin-emu", row.Emulator);
        Assert.Equal(1, row.FileCount);
        Assert.Contains("Card A", row.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_scan_of_an_install_with_no_es_settings_file_reports_nothing_and_does_not_throw()
    {
        // The option cannot be read, so it is treated as off. Nothing here acts on the answer,
        // which is what makes that the safe direction.
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        using var store = LocalStore.Open(install);

        Assert.False(File.Exists(install.Resolve(EsSettingsFile.Location)));

        new SaveScanner(install, store).Scan();

        Assert.DoesNotContain(store.Unsyncable.List(), e => e.Reason is UnsyncableReason.ManagedElsewhere);
    }

    [Fact]
    public void The_copies_are_still_reported_when_es_settings_is_missing_because_the_files_are_real()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        using var store = LocalStore.Open(install);

        Write(install, $"{Region}/Card A/{Gci}", "left behind");

        new SaveScanner(install, store).Scan();

        Assert.Single(store.Unsyncable.List(), e => e.Reason is UnsyncableReason.ManagedElsewhere);
    }

    [Fact]
    public void Only_gamecube_is_looked_at_because_the_Wii_branch_never_calls_the_sync()
    {
        // The option is declared twice in es_features.cfg and both are under gamecube. A wii
        // key would be a setting the menu can produce and emulatorlauncher ignores.
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        Write(install, "saves/wii/dolphin-emu/User/GC/USA/Card A/something.gci", "not reachable");

        var settings = SettingsWith(("wii.dolphin_sync_saves", "true"));

        Assert.False(DolphinSaveSync.Inspect(install, settings).WorthReporting);
    }

    private static void Write(RetroBatInstall install, string relative, string contents)
    {
        var absolute = install.Resolve(RelativePath.Create(relative));
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllText(absolute, contents);
    }

    private static EsSettingsFile SettingsWith(params (string Name, string Value)[] settings)
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        File.WriteAllText(path, Render(settings));

        try
        {
            return EsSettingsFile.Load(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void WriteSettings(RetroBatInstall install, params (string Name, string Value)[] settings)
    {
        var absolute = install.Resolve(EsSettingsFile.Location);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllText(absolute, Render(settings));
    }

    private static string Render((string Name, string Value)[] settings)
    {
        var body = string.Concat(settings.Select(setting =>
            $"\t<bool name=\"{System.Security.SecurityElement.Escape(setting.Name)}\" "
                + $"value=\"{System.Security.SecurityElement.Escape(setting.Value)}\" />\n"));

        return "<?xml version=\"1.0\"?>\n<config>\n" + body + "</config>\n";
    }
}
