using RomM.Client;
using RomMBat.Core.Diagnostics;

namespace RomMBat.Core.Paths;

/// <summary>How the RetroBat root was found, so <c>status</c> can say.</summary>
public enum RootDiscoverySource
{
    /// <summary>Handed in explicitly, by <c>--root</c> or by a test.</summary>
    Explicit,

    /// <summary>The <c>ROMMBAT_RETROBAT_ROOT</c> environment variable.</summary>
    Environment,

    /// <summary>Walked up from <see cref="AppContext.BaseDirectory"/>. The normal path.</summary>
    ExecutableDirectory,

    /// <summary>Walked up from the current working directory.</summary>
    WorkingDirectory,

    /// <summary>Read from <c>HKCU\Software\RetroBat\LatestKnownInstallPath</c>. Last resort.</summary>
    Registry,
}

/// <summary>
/// A located RetroBat installation, and the only place a stored relative path becomes an
/// absolute one.
/// </summary>
/// <remarks>
/// Nothing else in the tree concatenates a root. If a class needs a real path it takes one
/// of these and calls <see cref="Resolve(RelativePath)"/>; if it needs to store one it calls
/// <see cref="Relativize"/> first.
/// </remarks>
public sealed class RetroBatInstall
{
    /// <summary>
    /// Where RomMBat lives inside the tree.
    /// </summary>
    /// <remarks>
    /// Not a free choice. M0 probe 4 measured that a <c>system/es_menu/*.menu</c> entry
    /// resolves its executable under <c>emulators\</c> and that <c>emulatorLauncher</c>
    /// refuses <c>..\</c> escapes outright, so an app installed anywhere else cannot be
    /// launched from the ES menu at all.
    /// </remarks>
    public static RelativePath AppDirectory { get; } = RelativePath.Create("emulators/rommbat");

    /// <summary>The single-line version file. There is no <c>build.ini</c>; M0 probe 4 confirmed it.</summary>
    public static RelativePath VersionFile { get; } = RelativePath.Create("system/version.info");

    public RetroBatInstall(string rootPath, RootDiscoverySource source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        RootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        Source = source;
    }

    /// <summary>The absolute root, resolved now and never persisted.</summary>
    public string RootPath { get; }

    /// <summary>How it was found.</summary>
    public RootDiscoverySource Source { get; }

    /// <summary>The RomMBat directory, absolute.</summary>
    public string AppDirectoryPath => Resolve(AppDirectory);

    /// <summary>The SQLite database, absolute.</summary>
    public string DatabasePath => Resolve(AppDirectory.Combine("rommbat.db"));

    /// <summary>
    /// The file holding the <c>client_device_identifier</c> GUID, absolute.
    /// </summary>
    /// <remarks>
    /// Deliberately a file beside the database rather than a row inside it. Identity has to
    /// outlive the database: if the store is deleted or rebuilt, re-pairing on the same GUID
    /// must still update the existing RomM device rather than creating a second one.
    /// </remarks>
    public string DeviceIdentityPath => Resolve(AppDirectory.Combine("device.id"));

    /// <summary>The log directory, absolute.</summary>
    public string LogDirectoryPath => Resolve(AppDirectory.Combine("logs"));

    /// <summary>The outbox directory, absolute. Content produced offline lands here.</summary>
    public string OutboxDirectoryPath => Resolve(AppDirectory.Combine("outbox"));

    /// <summary>Turns a stored relative path into an absolute one.</summary>
    public string Resolve(RelativePath path) => Path.Combine(RootPath, path.Value.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Turns a stored relative path into an absolute one.</summary>
    public string Resolve(string relativePath) => Resolve(RelativePath.Create(relativePath));

    /// <summary>
    /// Turns an absolute path inside the tree into a storable relative one.
    /// </summary>
    /// <remarks>
    /// The boundary this exists for is the ES hooks, which receive an <b>absolute</b> rom
    /// path in their first argument. Relativising there is mandatory work, not an
    /// optimisation.
    /// </remarks>
    /// <exception cref="ArgumentException">The path is outside the RetroBat tree.</exception>
    public RelativePath Relativize(string absolutePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);

        var full = Path.GetFullPath(absolutePath);
        var relative = Path.GetRelativePath(RootPath, full);

        if (!RelativePath.TryCreate(relative, out var path))
        {
            throw new ArgumentException(
                $"'{absolutePath}' is outside the RetroBat tree at '{RootPath}', so it cannot be stored.",
                nameof(absolutePath));
        }

        return path;
    }

    /// <summary>True when the path is inside the tree and therefore storable.</summary>
    public bool Contains(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            return false;
        }

        var relative = Path.GetRelativePath(RootPath, Path.GetFullPath(absolutePath));
        return RelativePath.TryCreate(relative, out _);
    }

    /// <summary>Creates the RomMBat directories if they are not there yet.</summary>
    public void EnsureAppDirectories()
    {
        Directory.CreateDirectory(AppDirectoryPath);
        Directory.CreateDirectory(LogDirectoryPath);
        Directory.CreateDirectory(OutboxDirectoryPath);
    }

    /// <summary>
    /// Reads <c>system/version.info</c>.
    /// </summary>
    /// <remarks>
    /// A single line carrying a channel and an architecture suffix, <c>8.2.0-stable-win64</c>,
    /// so it is not a bare semantic version. <see cref="ProductVersion"/> splits it.
    /// </remarks>
    public string? ReadVersionString()
    {
        var path = Resolve(VersionFile);
        if (!File.Exists(path))
        {
            return null;
        }

        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0)
            {
                return trimmed;
            }
        }

        return null;
    }

    /// <summary>Checks the installed RetroBat version against the supported range.</summary>
    public CompatibilityCheck CheckVersion() => RetroBatVersion.Check(ReadVersionString());
}
