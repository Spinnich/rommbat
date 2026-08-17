namespace RomMBat.Core.Sync;

/// <summary>
/// The directory hooks write into and the agent drains.
/// </summary>
/// <remarks>
/// <b>One file per event, written then renamed, is what makes concurrency a non-problem.</b>
/// EmulationStation spawns hooks fire-and-forget and M0 probe 1 caught three <c>game-end</c>
/// hooks in flight at once, interleaving their writes to a shared file. Nothing here is
/// shared: each hook picks a name no other process will choose and renames it into place when
/// the bytes are down, so a reader never sees a partial record and there is no lock to wait on
/// inside the game-launch path.
/// <para>
/// The rename is the commit, the same discipline M3 landed for ROM downloads. A power loss
/// leaves a <c>.tmp</c>, which the drain ignores and cleans up.
/// </para>
/// <para>
/// This type is duplicated in shape rather than shared, because the hook references no other
/// project on purpose: four copies of it are installed and every megabyte is paid four times.
/// The agent reads the same directory through <c>RomMBat.Core</c>, and a test drives one
/// against the other so the two cannot drift.
/// </para>
/// </remarks>
public static class Spool
{
    /// <summary>Where the spool lives, relative to the RetroBat root.</summary>
    public const string RelativeDirectory = "emulators/rommbat/spool";

    /// <summary>The extension a committed record carries.</summary>
    public const string Extension = ".hook";

    /// <summary>Writes one record, and does not return until it is committed.</summary>
    public static void Write(string root, SpoolRecord record)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(record);

        var directory = Path.Combine(root, RelativeDirectory.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(directory);

        // Sortable by name, so the drain reads events in roughly the order they happened even
        // before it looks at the timestamps, and unique without asking anything: the GUID is
        // what stops two hooks in the same millisecond colliding.
        var stem = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{record.At:yyyyMMddTHHmmssfffffff}-{record.ProcessId}-{Guid.NewGuid():N}");

        var temporary = Path.Combine(directory, stem + ".tmp");
        var committed = Path.Combine(directory, stem + Extension);

        // WriteThrough, because the interesting case is ES being torn down seconds later and
        // the machine losing power before the cache is flushed.
        using (var stream = new FileStream(
            temporary,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.WriteThrough))
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(record.Render());
            stream.Write(bytes, 0, bytes.Length);
        }

        File.Move(temporary, committed);
    }
}
