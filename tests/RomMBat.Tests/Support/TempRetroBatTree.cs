using System.Collections.Concurrent;
using RomMBat.Core.Paths;
using Xunit;

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
    private static readonly ConcurrentDictionary<string, TreeRecord> Created = new(StringComparer.OrdinalIgnoreCase);

    private readonly TreeRecord _record;

    private TempRetroBatTree(string root)
    {
        Root = root;

        // Taken here because the leak check runs after every test has finished, when xunit
        // reports a failure against whichever tests were last rather than the one that leaked.
        _record = new TreeRecord(TestContext.Current.Test?.TestDisplayName ?? "no test");
        Created.TryAdd(root, _record);
    }

    /// <summary>Every tree created in this run, for <see cref="TempTreeLeakCheck"/>.</summary>
    internal static IEnumerable<string> CreatedRoots => Created.Keys;

    public string Root { get; }

    /// <summary>Where the agent's executable would sit, four levels down as in a real tree.</summary>
    public string AppDirectory => Path.Combine(Root, "emulators", "rommbat");

    public static TempRetroBatTree Create(string version = "8.2.1-stable-win64")
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

            _record.Disposal = TreeDisposal.Deleted;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _record.Disposal = TreeDisposal.Failed;
            _record.Problem = ex.Message;

            // A test leaving a file handle open must not turn into a failure in teardown.
            // Windows reports a still-mapped native library as ERROR_ACCESS_DENIED, which
            // surfaces from RemoveDirectoryRecursive as UnauthorizedAccessException and not
            // as IOException, so catching only the latter misses the case this exists for.
            // TempTreeLeakCheck reports the leftover once the run ends.
        }
    }

    /// <summary>
    /// Who made the tree at <paramref name="root"/>, and what its disposal found, as one line.
    /// </summary>
    /// <remarks>
    /// The three outcomes need three different fixes. A tree deleted cleanly and back on disk
    /// afterwards was written to by work its test started and never waited for, since
    /// <c>GamelistDocument</c> and most writers create the folders they
    /// write into.
    /// </remarks>
    internal static string Explain(string root)
    {
        if (!Created.TryGetValue(root, out var record))
        {
            return "not made by TempRetroBatTree";
        }

        var outcome = record.Disposal switch
        {
            TreeDisposal.Deleted => "deleted at disposal, then written to again by work the test did not wait for",
            TreeDisposal.Failed => $"still open at disposal: {record.Problem}",
            _ => "never disposed",
        };

        return $"made by {record.Owner}; {outcome}";
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

internal enum TreeDisposal
{
    NotYet,
    Deleted,
    Failed,
}

internal sealed class TreeRecord(string owner)
{
    public string Owner { get; } = owner;

    // Written by the disposing test and read by the leak check after the run, never together.
    public TreeDisposal Disposal { get; set; }

    public string? Problem { get; set; }
}
