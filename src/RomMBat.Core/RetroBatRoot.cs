using Microsoft.Win32;
using RomMBat.Core.Paths;

namespace RomMBat.Core;

/// <summary>
/// Finds the RetroBat installation root.
/// </summary>
/// <remarks>
/// Walking up from <see cref="AppContext.BaseDirectory"/> is the primary path and works on
/// both portable and fixed installs; M0 probe 7 exercised it across two machines and three
/// drive letters. The registry lookup is a genuine last resort and is deliberately last:
/// <c>HKCU\Software\RetroBat\LatestKnownInstallPath</c> records where an install was
/// <i>last seen</i>, so on a portable drive it is stale the moment the letter changes.
/// </remarks>
public static class RetroBatRoot
{
    /// <summary>The environment variable that overrides discovery, for tests and odd layouts.</summary>
    public const string OverrideVariable = "ROMMBAT_RETROBAT_ROOT";

    private const string RegistryKeyPath = @"Software\RetroBat";
    private const string RegistryValueName = "LatestKnownInstallPath";

    /// <summary>
    /// Files and directories whose presence identifies a RetroBat root.
    /// </summary>
    /// <remarks>
    /// M0 probe 4 confirmed all of these in a stock 8.2 tree, and confirmed there is no
    /// <c>build.ini</c> anywhere in it.
    /// </remarks>
    public static IReadOnlyList<string> Markers { get; } = ["retrobat.ini", "emulationstation", "roms"];

    /// <summary>
    /// Minimum supported RetroBat version. Below this, RomMBat refuses to run.
    /// </summary>
    public static Version MinimumVersion { get; } = new(8, 2);

    /// <summary>
    /// Locates the root, or returns null when there is nothing to find.
    /// </summary>
    /// <remarks>
    /// A supplied root is checked against the markers like any other candidate. Pointing at
    /// the wrong directory and having RomMBat quietly build a tree there is a worse outcome
    /// than being told the path is not a RetroBat install.
    /// </remarks>
    /// <param name="explicitRoot">An operator-supplied root, which always wins when it is one.</param>
    public static RetroBatInstall? Locate(string? explicitRoot = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            return IsRoot(explicitRoot)
                ? new RetroBatInstall(explicitRoot, RootDiscoverySource.Explicit)
                : null;
        }

        var fromEnvironment = Environment.GetEnvironmentVariable(OverrideVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return IsRoot(fromEnvironment)
                ? new RetroBatInstall(fromEnvironment, RootDiscoverySource.Environment)
                : null;
        }

        var fromExecutable = WalkUp(AppContext.BaseDirectory);
        if (fromExecutable is not null)
        {
            return new RetroBatInstall(fromExecutable, RootDiscoverySource.ExecutableDirectory);
        }

        // The launched process gets its own directory as CWD (M0 probe 4), so this rarely
        // adds anything, but it covers being run from inside the tree by hand.
        var fromWorkingDirectory = WalkUp(Directory.GetCurrentDirectory());
        if (fromWorkingDirectory is not null)
        {
            return new RetroBatInstall(fromWorkingDirectory, RootDiscoverySource.WorkingDirectory);
        }

        var fromRegistry = ReadRegistryPath();
        if (fromRegistry is not null && IsRoot(fromRegistry))
        {
            return new RetroBatInstall(fromRegistry, RootDiscoverySource.Registry);
        }

        return null;
    }

    /// <summary>Locates the root, throwing a message worth showing the user when it cannot.</summary>
    /// <exception cref="RetroBatNotFoundException">No RetroBat install was found.</exception>
    public static RetroBatInstall Require(string? explicitRoot = null)
    {
        var install = Locate(explicitRoot);
        if (install is not null)
        {
            return install;
        }

        var markers = string.Join(", ", Markers);

        throw new RetroBatNotFoundException(
            string.IsNullOrWhiteSpace(explicitRoot)
                ? "No RetroBat install was found. RomMBat expects to run from inside the tree, at "
                    + $"{RetroBatInstall.AppDirectory}. Pass --root to point at it explicitly, or set "
                    + $"{OverrideVariable}."
                : $"'{explicitRoot}' does not look like a RetroBat install. RomMBat looks for {markers} "
                    + $"there, and installs itself at {RetroBatInstall.AppDirectory}.");
    }

    /// <summary>
    /// True when the directory looks like a RetroBat root.
    /// </summary>
    /// <remarks>
    /// <c>retrobat.ini</c> alone is decisive. Without it, both <c>emulationstation/</c> and
    /// <c>roms/</c> are required, because either on its own is a common enough directory
    /// name to produce a false positive partway up an unrelated tree.
    /// </remarks>
    public static bool IsRoot(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        if (File.Exists(Path.Combine(directory, "retrobat.ini")))
        {
            return true;
        }

        return Directory.Exists(Path.Combine(directory, "emulationstation"))
            && Directory.Exists(Path.Combine(directory, "roms"));
    }

    private static string? WalkUp(string? start)
    {
        if (string.IsNullOrWhiteSpace(start))
        {
            return null;
        }

        var directory = new DirectoryInfo(Path.GetFullPath(start));
        while (directory is not null)
        {
            if (IsRoot(directory.FullName))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string? ReadRegistryPath()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            return key?.GetValue(RegistryValueName) as string;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

/// <summary>Thrown when no RetroBat install could be located.</summary>
public sealed class RetroBatNotFoundException : Exception
{
    public RetroBatNotFoundException(string message)
        : base(message)
    {
    }

    public RetroBatNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public RetroBatNotFoundException()
        : base("No RetroBat install was found.")
    {
    }
}
