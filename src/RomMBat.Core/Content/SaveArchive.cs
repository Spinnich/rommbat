using System.IO.Compression;
using RomMBat.Core.Paths;

namespace RomMBat.Core.Content;

/// <summary>
/// Packs a save unit into one archive, and puts one back.
/// </summary>
/// <remarks>
/// <b>The archive is transport and the hash is identity, and the two must not be confused.</b>
/// Entry ordering, timestamps and compression level all differ between Go's <c>archive/zip</c>
/// and .NET's <c>ZipArchive</c>, so a hash over the bytes would make RomMBat and Grout disagree
/// forever about identical saves, and a library upgrade could do the same to RomMBat alone.
/// Freegosy reached the same cliff from the opposite side, writing a timestamped
/// <c>freegosy_sync.txt</c> into every bundle specifically to defeat server-side dedup, and
/// paying for it with a new server row on every sync of an unchanged save.
/// <para>
/// <b>This one is still written deterministically</b>, even though nothing compares its bytes.
/// A byte-identical archive for unchanged contents means a replayed flush sends the same thing
/// twice rather than something new twice, which is what keeps a partial failure cheap. Every
/// entry gets one fixed timestamp and one compression level, and the order is the same ordinal
/// sort the hash folds.
/// </para>
/// <para>
/// <b>A half-written directory save is a corrupt one, so everything that can be done off to one
/// side is.</b> The unit is extracted beside its container, verified against the hash of what
/// came out, and only then swapped in, with the previous copy kept until the next successful
/// sync. <b>The swap is not one filesystem operation</b>: members go in one at a time, because
/// the container is shared with every other game on the system and cannot be swapped whole. It
/// is all-or-nothing anyway, since a failure partway is rolled back from the copy aside. See
/// <see cref="SaveUnitTransfer.Restore"/>.
/// </para>
/// </remarks>
public static class SaveArchive
{
    /// <summary>
    /// The timestamp every entry carries.
    /// </summary>
    /// <remarks>
    /// The zip epoch. A real mtime would make two archives of one unchanged save differ, which
    /// is the determinism this class exists to keep; the mtime that matters travels separately
    /// as the save's <c>updated_at</c>.
    /// </remarks>
    private static readonly DateTimeOffset EntryTimestamp = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Writes a unit to a stream as one archive.</summary>
    public static void Pack(RetroBatInstall install, SaveUnit unit, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(destination);

        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);

        // Ordinal, the same order the hash folds, so an archive and the digest that names it
        // never describe the members in two different sequences.
        foreach (var file in unit.Files.OrderBy(file => file.ArchivePath, StringComparer.Ordinal))
        {
            var entry = archive.CreateEntry(file.ArchivePath, CompressionLevel.Optimal);
            entry.LastWriteTime = EntryTimestamp;

            using var source = File.OpenRead(install.Resolve(file.Path));
            using var writer = entry.Open();
            source.CopyTo(writer);
        }
    }

    /// <summary>The logical content hash of a unit: sorted paths, each with its own digest.</summary>
    /// <remarks>
    /// <b>This is the local change detector and not what goes on the wire for class C.</b> RomM
    /// computes its own digest over an archive's contents by a function this client cannot
    /// reproduce, measured, so the value to send back is the one the server returned on the last
    /// upload. Comparing this against that is always false.
    /// </remarks>
    public static string HashOf(RetroBatInstall install, SaveUnit unit)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(unit);

        return LogicalContentHash.Fold(unit.Files.Select(file =>
            (file.ArchivePath, LogicalContentHash.OfFile(install.Resolve(file.Path)))));
    }

    /// <summary>
    /// Extracts an archive into a directory, refusing any entry that would escape it.
    /// </summary>
    /// <remarks>
    /// The entry names come off the wire, so they are untrusted: an archive naming
    /// <c>../../../roms/something</c> would otherwise write outside the tree. Every name is put
    /// through <see cref="RelativePath"/>, which is the same type that keeps an absolute path
    /// out of the database, and anything it refuses fails the whole extraction rather than being
    /// skipped, because a partially-extracted save is not a save.
    /// </remarks>
    public static IReadOnlyList<string> Extract(Stream source, string destination)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);

        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);
        var written = new List<string>();

        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith('/'))
            {
                continue;
            }

            if (!RelativePath.TryCreate(entry.FullName, out var safe))
            {
                throw new InvalidDataException(
                    $"the archive holds an entry named '{entry.FullName}', which does not stay "
                        + "inside the directory it would be written to.");
            }

            var target = Path.Combine(destination, safe.Value.Replace('/', Path.DirectorySeparatorChar));

            // Belt and braces. RelativePath already refuses a climb, and this catches anything a
            // future normalisation change might let through, because the cost of being wrong is
            // writing over a file outside the save tree.
            var full = Path.GetFullPath(target);
            var root = Path.GetFullPath(destination);

            if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"the archive holds an entry named '{entry.FullName}', which resolves outside "
                        + "the directory it would be written to.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(full)!);

            using var reader = entry.Open();
            using var writer = File.Create(full);
            reader.CopyTo(writer);

            written.Add(safe.Value);
        }

        return written;
    }

    /// <summary>
    /// The logical hash of what was extracted, folded the same way a unit on disk is.
    /// </summary>
    /// <remarks>
    /// Used to verify a restore against the save that was sent, which is the one comparison the
    /// client can make on both sides: the server's archive digest is not reproducible here, but
    /// the fold over what came out of the archive is the same function as the fold over what
    /// went in.
    /// </remarks>
    public static string HashOfExtracted(string directory, IEnumerable<string> entries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(entries);

        return LogicalContentHash.Fold(entries.Select(entry => (
            entry,
            LogicalContentHash.OfFile(Path.Combine(directory, entry.Replace('/', Path.DirectorySeparatorChar))))));
    }
}
