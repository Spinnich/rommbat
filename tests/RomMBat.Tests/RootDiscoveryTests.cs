using RomMBat.Core;
using RomMBat.Core.Diagnostics;
using RomMBat.Tests.Support;
using Xunit;
using CompatibilityVerdict = RomM.Client.CompatibilityVerdict;

namespace RomMBat.Tests;

/// <summary>
/// Locating the RetroBat root, which every path in the app hangs off.
/// </summary>
public class RootDiscoveryTests
{
    [Fact]
    public void A_stock_tree_is_recognised()
    {
        using var tree = TempRetroBatTree.Create();

        Assert.True(RetroBatRoot.IsRoot(tree.Root));
    }

    [Fact]
    public void The_marker_directories_alone_are_enough_without_retrobat_ini()
    {
        using var tree = TempRetroBatTree.Create();
        File.Delete(Path.Combine(tree.Root, "retrobat.ini"));

        Assert.True(RetroBatRoot.IsRoot(tree.Root));
    }

    [Fact]
    public void One_marker_directory_on_its_own_is_not_enough()
    {
        // 'roms' partway up an unrelated tree is a common enough name to be a false positive.
        var directory = Path.Combine(Path.GetTempPath(), "rommbat-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "roms"));

        try
        {
            Assert.False(RetroBatRoot.IsRoot(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Nothing_is_found_in_a_directory_that_does_not_exist()
    {
        Assert.False(RetroBatRoot.IsRoot(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
    }

    [Fact]
    public void An_explicit_root_wins_over_everything_else()
    {
        using var tree = TempRetroBatTree.Create();

        var install = RetroBatRoot.Locate(tree.Root);

        Assert.NotNull(install);
        Assert.Equal(Path.TrimEndingDirectorySeparator(tree.Root), install.RootPath);
        Assert.Equal(RomMBat.Core.Paths.RootDiscoverySource.Explicit, install.Source);
    }

    [Fact]
    public void A_supplied_root_that_is_not_a_RetroBat_tree_is_refused_rather_than_built_in()
    {
        // Quietly creating a tree in the wrong directory is a worse outcome than saying so.
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        Assert.Null(RetroBatRoot.Locate(missing));

        var exception = Assert.Throws<RetroBatNotFoundException>(() => RetroBatRoot.Require(missing));

        Assert.Contains(missing, exception.Message, StringComparison.Ordinal);
        Assert.Contains("emulators/rommbat", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_app_directory_is_under_emulators_because_the_ES_menu_forces_it()
    {
        // M0 probe 4: a .menu executable path resolves under emulators\ and emulatorLauncher
        // refuses ..\ escapes outright, so anywhere else cannot be menu-launched at all.
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        Assert.Equal("emulators/rommbat", RomMBat.Core.Paths.RetroBatInstall.AppDirectory.Value);
        Assert.StartsWith(
            Path.Combine(tree.Root, "emulators", "rommbat"),
            install.DatabasePath,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_version_comes_from_system_version_info_and_there_is_no_build_ini()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        Assert.Equal("8.2.0-stable-win64", install.ReadVersionString());
        Assert.False(File.Exists(Path.Combine(tree.Root, "build.ini")));
        Assert.Equal("system/version.info", RomMBat.Core.Paths.RetroBatInstall.VersionFile.Value);
    }

    [Fact]
    public void A_missing_version_file_is_unreadable_rather_than_assumed_current()
    {
        using var tree = TempRetroBatTree.Create(version: string.Empty);
        var install = tree.Install();

        Assert.Null(install.ReadVersionString());
        Assert.Equal(CompatibilityVerdict.Unreadable, install.CheckVersion().Verdict);
    }

    [Fact]
    public void An_old_install_is_refused_with_both_versions_named()
    {
        using var tree = TempRetroBatTree.Create("8.1.0-stable-win64");

        var check = tree.Install().CheckVersion();

        Assert.True(check.MustRefuse);
        Assert.Contains("8.1.0-stable-win64", check.Message, StringComparison.Ordinal);
        Assert.Contains(RetroBatVersion.Minimum.ToString(), check.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_marker_is_relative_so_the_portable_case_holds()
    {
        Assert.NotEmpty(RetroBatRoot.Markers);
        Assert.All(RetroBatRoot.Markers, marker => Assert.False(Path.IsPathRooted(marker)));
    }

    [Fact]
    public void The_app_directories_are_created_inside_the_tree_and_nowhere_else()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        install.EnsureAppDirectories();

        Assert.True(Directory.Exists(install.AppDirectoryPath));
        Assert.True(Directory.Exists(install.LogDirectoryPath));
        Assert.True(Directory.Exists(install.OutboxDirectoryPath));

        foreach (var path in new[] { install.AppDirectoryPath, install.LogDirectoryPath, install.OutboxDirectoryPath })
        {
            Assert.True(install.Contains(path), $"{path} escaped the tree");
        }
    }
}
