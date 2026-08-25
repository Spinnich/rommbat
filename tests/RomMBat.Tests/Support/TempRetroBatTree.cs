using RomMBat.Core.Paths;

namespace RomMBat.Tests.Support;

/// <summary>
/// A throwaway directory shaped like a RetroBat install.
/// </summary>
/// <remarks>
/// Real enough for discovery, versioning and the store: the markers discovery looks for, a
/// <c>system/version.info</c>, and nothing else. Tests that need more add it.
/// </remarks>
internal sealed class TempRetroBatTree : IDisposable
{
    private TempRetroBatTree(string root) => Root = root;

    public string Root { get; }

    /// <summary>Where the agent's executable would sit, four levels down as in a real tree.</summary>
    public string AppDirectory => Path.Combine(Root, "emulators", "rommbat");

    public static TempRetroBatTree Create(string version = "8.2.0-stable-win64")
    {
        var root = Path.Combine(Path.GetTempPath(), "rommbat-tests", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(Path.Combine(root, "emulationstation", ".emulationstation"));
        Directory.CreateDirectory(Path.Combine(root, "roms"));
        Directory.CreateDirectory(Path.Combine(root, "saves"));
        Directory.CreateDirectory(Path.Combine(root, "bios"));
        Directory.CreateDirectory(Path.Combine(root, "system", "es_menu"));
        Directory.CreateDirectory(Path.Combine(root, "emulators", "rommbat"));

        File.WriteAllText(Path.Combine(root, "retrobat.ini"), "[RetroBat]\n");

        if (version.Length > 0)
        {
            File.WriteAllText(Path.Combine(root, "system", "version.info"), version + "\n");
        }

        return new TempRetroBatTree(root);
    }

    public RetroBatInstall Install(RootDiscoverySource source = RootDiscoverySource.Explicit) =>
        new(Root, source);

    /// <summary>
    /// Copies the whole tree to a new location, which is how a drive-letter change is
    /// simulated without a USB stick.
    /// </summary>
    public TempRetroBatTree CopyToNewLocation()
    {
        var destination = Path.Combine(Path.GetTempPath(), "rommbat-tests", Guid.NewGuid().ToString("N"));
        CopyDirectory(Root, destination);
        return new TempRetroBatTree(destination);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A test leaving a file handle open must not turn into a failure in teardown.
            // Windows reports a still-mapped native library as ERROR_ACCESS_DENIED, which
            // surfaces from RemoveDirectoryRecursive as UnauthorizedAccessException and not
            // as IOException, so catching only the latter misses the case this exists for.
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.GetDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }
}
