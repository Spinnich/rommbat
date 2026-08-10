using System.IO.Compression;
using System.Security.Cryptography;
using RomMBat.Core.Store;

namespace RomMBat.Core.Content;

/// <summary>What a local file hashes to, and what those hashes describe.</summary>
public sealed record ContentFingerprint
{
    public required string Md5 { get; init; }

    public required string Sha1 { get; init; }

    /// <summary>Lower-case hex, matching how RomM writes <c>crc_hash</c>.</summary>
    public required string Crc32 { get; init; }

    /// <summary>Whether the hashes describe the file or the single entry inside it.</summary>
    public required HashScope Scope { get; init; }

    /// <summary>
    /// True when these hashes describe the same bytes RomM's do, so comparing them means something.
    /// </summary>
    /// <remarks>
    /// False for an archive whose single entry could not be reached: a <c>.7z</c>, or a
    /// <c>.zip</c> that holds more than one file. Both are hashed as files while RomM's hashes
    /// describe content, so a mismatch between the two says nothing about the file, and treating
    /// it as one would refuse a correct download on every run. Verification falls back to size.
    /// </remarks>
    public required bool DescribesLibraryContent { get; init; }

    /// <summary>The length of whatever was hashed.</summary>
    public long HashedBytes { get; init; }

    /// <summary>The length of the file on disk, which for an archive is not what was hashed.</summary>
    public long FileBytes { get; init; }
}

/// <summary>
/// Hashes a local file the way RomM hashes it, so the two can be compared.
/// </summary>
/// <remarks>
/// <b>RomM's hashes describe uncompressed content.</b> Measured against a live instance: a
/// 1,025-byte <c>.zip</c> reports the md5, sha1 and CRC of the 16,400-byte <c>.nes</c> inside
/// it, and a <c>.chd</c> reports the hashes of its own bytes. Hashing the archive would
/// therefore disagree with the server about every compressed ROM in the library, which would
/// re-download an entire adopted collection and then fail to verify what it downloaded.
/// <para>
/// <b>Only <c>.zip</c> can be looked inside.</b> It is the one archive format the base class
/// library reads, and adding a dependency to reach <c>.7z</c> is not this milestone's
/// business. A <c>.7z</c> is hashed as a file, which will not match the server, so verification
/// of one degrades to size and says so. RetroBat accepts both formats for many systems, so
/// this is a real and stated limitation rather than an oversight.
/// </para>
/// <para>
/// A multi-entry archive has no single content hash, so it is also hashed as a file. RomM's
/// own rule only holds for the one-file-in-one-archive case.
/// </para>
/// </remarks>
public static class ContentHasher
{
    /// <summary>Read in chunks, so a 4 GB ISO costs one buffer rather than its own size.</summary>
    private const int BufferSize = 1024 * 1024;

    private static readonly string[] OpaqueArchiveExtensions = [".7z", ".rar"];

    /// <summary>
    /// Hashes a file, looking inside a single-entry zip.
    /// </summary>
    /// <param name="absolutePath">The file to read.</param>
    /// <param name="effectiveFileName">
    /// The name that decides the format, when the file on disk does not carry it. A finished
    /// download is verified while it is still <c>&lt;rom id&gt;.part</c>, and judging that name
    /// would hash a zip's own bytes and disagree with the server about every archived ROM.
    /// </param>
    public static ContentFingerprint Compute(string absolutePath, string? effectiveFileName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);

        var fileBytes = new FileInfo(absolutePath).Length;
        var name = effectiveFileName ?? absolutePath;

        if (LooksLikeZip(name))
        {
            var inside = TryComputeInsideZip(absolutePath, fileBytes);
            if (inside.Fingerprint is not null)
            {
                return inside.Fingerprint;
            }

            // A zip that opened and held more than one file has no single content hash, so its
            // own bytes describe nothing the server stored. One that would not open at all is
            // damaged, and there a mismatch is the answer that re-downloads it.
            using var archive = Open(absolutePath);
            return Hash(archive, HashScope.File, fileBytes, fileBytes, !inside.Opaque);
        }

        using var stream = Open(absolutePath);
        return Hash(stream, HashScope.File, fileBytes, fileBytes, !LooksLikeOpaqueArchive(name));
    }

    /// <summary>Hashes a stream that is already open, for verifying what was just written.</summary>
    public static ContentFingerprint Compute(Stream stream, long fileBytes)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return Hash(stream, HashScope.File, fileBytes, fileBytes, describesLibraryContent: true);
    }

    /// <summary>True when the name says zip, which is the only archive this can see inside.</summary>
    public static bool LooksLikeZip(string path) =>
        Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True for an archive format RetroBat accepts that the base class library cannot read.
    /// </summary>
    /// <remarks>
    /// Both appear in real <c>&lt;extension&gt;</c> sets, so both reach a sync set. Their own
    /// bytes are not what RomM hashed and nothing here can reach what it did.
    /// </remarks>
    public static bool LooksLikeOpaqueArchive(string path) =>
        OpaqueArchiveExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// True when two hashes name the same content.
    /// </summary>
    /// <remarks>
    /// Case-insensitive because RomM lower-cases and nothing guarantees another writer did, and
    /// false when either side is missing: an absent hash is not a match, it is an unknown, and
    /// treating it as a match would accept any file at all.
    /// </remarks>
    public static bool Matches(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left)
        && !string.IsNullOrWhiteSpace(right)
        && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Hashes the one file inside a zip, and says why it could not when it could not.
    /// </summary>
    /// <returns>
    /// <c>Opaque</c> is true when the archive opened but holds no single entry to hash, which is
    /// a stated limitation rather than damage. A zip that would not open leaves it false, so the
    /// file hash is still compared and a damaged download is still refused.
    /// </returns>
    private static (ContentFingerprint? Fingerprint, bool Opaque) TryComputeInsideZip(
        string absolutePath,
        long fileBytes)
    {
        try
        {
            using var archive = ZipFile.OpenRead(absolutePath);

            // Directory entries carry an empty Name, and RomM's rule is about the one file the
            // archive holds, so anything else is hashed as a file.
            var entries = archive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name)).ToList();
            if (entries.Count != 1)
            {
                return (null, true);
            }

            using var content = entries[0].Open();
            return (Hash(content, HashScope.ArchiveContent, entries[0].Length, fileBytes, true), false);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or NotSupportedException)
        {
            // A truncated or unreadable archive is hashed as a file, which will not match and
            // so will be re-downloaded. That is the right outcome for a damaged file.
            return (null, false);
        }
    }

    private static FileStream Open(string absolutePath) =>
        new(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.SequentialScan);

    private static ContentFingerprint Hash(
        Stream stream,
        HashScope scope,
        long hashedBytes,
        long fileBytes,
        bool describesLibraryContent)
    {
        // MD5, SHA-1 and CRC-32 are all here because RomM stores all three and none of them is
        // being used as a security primitive: the question is only whether two files are the
        // same file, and matching the server's choice is the whole point.
#pragma warning disable CA5350, CA5351 // Weak algorithms, deliberately: these mirror RomM's.
        using var md5 = MD5.Create();
        using var sha1 = SHA1.Create();
#pragma warning restore CA5350, CA5351

        var crc = 0xFFFFFFFFu;
        var buffer = new byte[BufferSize];
        var read = 0L;

        while (true)
        {
            var count = stream.Read(buffer, 0, buffer.Length);
            if (count == 0)
            {
                break;
            }

            md5.TransformBlock(buffer, 0, count, null, 0);
            sha1.TransformBlock(buffer, 0, count, null, 0);
            crc = Crc32.Continue(crc, buffer.AsSpan(0, count));
            read += count;
        }

        md5.TransformFinalBlock([], 0, 0);
        sha1.TransformFinalBlock([], 0, 0);

        return new ContentFingerprint
        {
            Md5 = Convert.ToHexString(md5.Hash!).ToLowerInvariant(),
            Sha1 = Convert.ToHexString(sha1.Hash!).ToLowerInvariant(),
            Crc32 = (~crc).ToString("x8", System.Globalization.CultureInfo.InvariantCulture),
            Scope = scope,
            DescribesLibraryContent = describesLibraryContent,
            HashedBytes = hashedBytes > 0 ? hashedBytes : read,
            FileBytes = fileBytes,
        };
    }
}

/// <summary>CRC-32, because RomM stores one and the base class library does not provide it.</summary>
internal static class Crc32
{
    private static readonly uint[] Table = BuildTable();

    public static uint Continue(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var value in data)
        {
            crc = Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];

        for (var index = 0u; index < table.Length; index++)
        {
            var value = index;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
            }

            table[index] = value;
        }

        return table;
    }
}
