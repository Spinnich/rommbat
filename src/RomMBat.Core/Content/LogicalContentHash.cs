using System.Security.Cryptography;
using System.Text;

namespace RomMBat.Core.Content;

/// <summary>
/// The hash of a save's logical contents, which is never the hash of an archive.
/// </summary>
/// <remarks>
/// <b>Hashing the zip bytes is a trap, and the trap has two sides.</b> Archive output is
/// implementation-dependent: entry ordering, timestamps and compression level differ between
/// Go's <c>archive/zip</c> and .NET's <c>ZipArchive</c>, so RomMBat and Grout would compute
/// different hashes for an identical logical save and conflict forever, and a library upgrade
/// could do the same to RomMBat alone. Freegosy reached the same cliff from the opposite
/// direction, writing a timestamped <c>freegosy_sync.txt</c> into every bundle specifically to
/// defeat server-side dedup, and pays for it with a new server row on every sync of an
/// unchanged save.
/// <para>
/// <b>Determinism here is what makes a replayed flush idempotent.</b> Byte-identical content
/// posted twice into one slot reuses the same row, measured. That only holds if "identical"
/// really is identical, so this is defined over the contents rather than over transport:
/// sorted relative paths, each with its own digest, folded into one.
/// </para>
/// <para>
/// <b>MD5 because RomM's <c>content_hash</c> is 32 characters.</b> Not a security boundary:
/// the comparison it feeds is "did this change", and the server does the same.
/// </para>
/// <para>
/// <b>Scope the unit before hashing it.</b> M6 measured a full hash of
/// <c>saves/ps3/rpcs3</c> at 426 s over 52.87 GB, and the savedata subtree a save really is at
/// 0.06 s over 16.3 MB. The 32,451-file figure the plan calls a performance problem is the
/// emulator's whole data root; the cost is a symptom of hashing the wrong thing.
/// </para>
/// </remarks>
public static class LogicalContentHash
{
    /// <summary>The separator between a path and its digest, and between entries.</summary>
    /// <remarks>
    /// A NUL and a newline, neither of which can occur in a path segment, so no filename can
    /// make two different trees fold to the same input.
    /// </remarks>
    private const byte PathTerminator = 0;

    /// <summary>Hashes one file, which is class A and every member of class B.</summary>
    public static string OfFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // A single file's logical content is its bytes. Deliberately not folded through the
        // path: a save restored under a different name is the same save, and the name is
        // carried separately.
        using var stream = File.OpenRead(path);
#pragma warning disable CA5351 // MD5, deliberately: RomM's content_hash is 32 characters.
        return Convert.ToHexStringLower(MD5.HashData(stream));
#pragma warning restore CA5351
    }

    /// <summary>
    /// Hashes a directory, which is class C.
    /// </summary>
    /// <remarks>
    /// Sorted by the forward-slashed relative path using an ordinal comparison, so the result
    /// does not depend on the filesystem's enumeration order or on the machine's culture.
    /// Empty directories contribute nothing, because an archive would not carry them either.
    /// </remarks>
    public static string OfDirectory(string directory, Func<string, bool>? include = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));

        var entries = Directory
            .EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => include?.Invoke(path) ?? true)
            .Select(path => (Relative: Path.GetRelativePath(root, path).Replace('\\', '/'), Path: path))
            .OrderBy(entry => entry.Relative, StringComparer.Ordinal);

        return Fold(entries.Select(entry => (entry.Relative, OfFile(entry.Path))));
    }

    /// <summary>
    /// Folds a set of (relative path, digest) pairs into one digest.
    /// </summary>
    /// <remarks>
    /// Public so a test can prove the fold is stable without touching a filesystem, and so a
    /// caller that already has per-file digests does not have to read the bytes again.
    /// </remarks>
    public static string Fold(IEnumerable<(string RelativePath, string FileHash)> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

#pragma warning disable CA5351 // MD5, deliberately: RomM's content_hash is 32 characters.
        using var folded = MD5.Create();
#pragma warning restore CA5351
        using var stream = new CryptoStream(Stream.Null, folded, CryptoStreamMode.Write);

        foreach (var (relative, hash) in entries.OrderBy(entry => entry.RelativePath, StringComparer.Ordinal))
        {
            var path = Encoding.UTF8.GetBytes(relative);
            stream.Write(path, 0, path.Length);
            stream.WriteByte(PathTerminator);

            var digest = Encoding.ASCII.GetBytes(hash.ToLowerInvariant());
            stream.Write(digest, 0, digest.Length);
            stream.WriteByte((byte)'\n');
        }

        stream.FlushFinalBlock();
        return Convert.ToHexStringLower(folded.Hash!);
    }
}
