using RomMBat.Core.Paths;

namespace RomMBat.Core.Sync;

/// <summary>
/// The one-writer-at-a-time lock over the tree, held for as long as the returned handle lives.
/// </summary>
/// <remarks>
/// <b>Mandatory rather than defensive.</b> A portable install cannot register a service, so
/// the flush is a short-lived process invoked from the <c>start</c>, <c>game-end</c> and
/// <c>quit</c> hooks and from the UI. EmulationStation spawns those hooks fire-and-forget and
/// does not wait for them, and M0 probe 1 observed <b>three <c>game-end</c> hooks in flight at
/// once</b>. Several agents racing the same outbox is the normal case here, not an edge one.
/// <para>
/// <b>The handle is the lock, not the file's existence.</b> A lock built on "does the file
/// exist" goes stale the moment a process is killed, and an emulator being killed from Task
/// Manager is a case M0 measured. Windows releases an open handle when the owning process
/// dies however it dies, so a crashed flush leaves the next one able to start immediately and
/// there is no timeout to tune and no stale lock to break.
/// </para>
/// <para>
/// <b>For a flush, failing to acquire is a success.</b> Another process is already draining the
/// same queue, so the right thing is to exit rather than wait: the work is being done, and a
/// hook that blocks is a hook running inside the game-launch path.
/// </para>
/// <para>
/// <b>The other two holders cannot say that, because nobody is doing their work.</b>
/// <c>saves resolve</c> runs the same class C restore a flush does, and refuses with an exit
/// code rather than returning as though it had resolved anything. <c>evict</c>'s sweep of
/// <c>partial/</c> reclaims nothing and says the next pass will, because one of the things it
/// would delete is a restore's staging directory and no handle protects it.
/// </para>
/// </remarks>
public sealed class TreeLock : IDisposable
{
    /// <summary>Where the lock file lives, inside the tree like everything else.</summary>
    public static RelativePath Path { get; } = RetroBatInstall.AppDirectory.Combine("flush.lock");

    private FileStream? _handle;

    private TreeLock(FileStream handle) => _handle = handle;

    /// <summary>
    /// Takes the lock if it is free.
    /// </summary>
    /// <returns>Null when another process holds it, which is an ordinary outcome.</returns>
    public static TreeLock? TryAcquire(RetroBatInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);

        var path = install.Resolve(Path);

        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);

            // FileShare.None is the whole mechanism. DeleteOnClose is deliberately not set: it
            // would make an unrelated reader opening the file able to keep a deleted-but-open
            // entry alive, and an empty file left behind costs nothing.
            var handle = new FileStream(
                path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.WriteThrough);

            return new TreeLock(handle);
        }
        catch (IOException)
        {
            // Held by another agent. Sharing violations and "file in use" both land here.
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            // A read-only volume, or a stick pulled mid-write. Not flushing is the safe answer.
            return null;
        }
    }

    public void Dispose()
    {
        _handle?.Dispose();
        _handle = null;
    }
}
