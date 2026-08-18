using RomMBat.Core.Paths;
using RomMBat.Core.Store;

namespace RomMBat.Core.Content;

/// <summary>What putting a unit back produced.</summary>
/// <param name="ContentHash">
/// The logical fold over what actually landed, which is what a later scan compares the tree
/// against. Never the server's digest, which is a different function.
/// </param>
public sealed record SaveUnitRestoreResult(string ContentHash, IReadOnlyList<string> Entries, RelativePath? CopiedAside);

/// <summary>
/// Moving a class C unit on and off the device, in one place because two callers need it.
/// </summary>
/// <remarks>
/// <b>This exists because the duplicate nearly shipped.</b> The ordinary sync path and the
/// conflict resolver each had their own download-and-swap, and the resolver's was written for a
/// single file: it verified the bytes against <c>server_content_hash</c> and moved one file into
/// place. Both are wrong for a unit, and the hands-on pass found them one after the other, the
/// second only because the first had already been fixed somewhere else.
/// <para>
/// <b>The verification asymmetry is the reason a shared helper is worth the indirection.</b> A
/// class A download is checked against the server's hash, which for a plain file is the MD5 of
/// the bytes. For an archive the server's hash is a digest over the contents by a function this
/// client cannot reproduce, so that check can never pass and must not be attempted. What stands
/// in for it is extraction validating every entry's CRC, plus refusing any entry that would
/// escape the container. Anywhere that forgets this fails every class C restore.
/// </para>
/// </remarks>
public static class SaveUnitTransfer
{
    /// <summary>Writes a unit to a temporary archive and returns its path.</summary>
    public static string Pack(RetroBatInstall install, SaveUnit unit, string partialDirectory)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(unit);

        Directory.CreateDirectory(partialDirectory);
        var bundle = Path.Combine(partialDirectory, $"unit-{Guid.NewGuid():N}.zip");

        using (var archive = new FileStream(bundle, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            SaveArchive.Pack(install, unit, archive);
        }

        return bundle;
    }

    /// <summary>
    /// Unpacks a fetched archive over a unit, atomically.
    /// </summary>
    /// <remarks>
    /// Nothing touches the live tree until the whole unit is extracted and readable, and the
    /// current members are copied aside before anything is replaced. A half-written directory
    /// save is a corrupt one.
    /// </remarks>
    /// <param name="part">The archive already on disk, as fetched.</param>
    public static SaveUnitRestoreResult Restore(
        RetroBatInstall install,
        SaveUnitScanner units,
        LocalSave local,
        string part,
        string partialDirectory,
        RelativePath asideDirectory,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(local);

        var staging = Path.Combine(partialDirectory, $"unit-{Guid.NewGuid():N}");
        var container = install.Resolve(local.Path);

        try
        {
            Directory.CreateDirectory(staging);

            IReadOnlyList<string> entries;

            using (var archive = File.OpenRead(part))
            {
                // Validates every entry's CRC and refuses anything that would escape, which is
                // what stands in for a byte hash the server cannot give us.
                entries = SaveArchive.Extract(archive, staging);
            }

            if (entries.Count == 0)
            {
                throw new InvalidDataException(
                    "the archive holds nothing, so there is no save in it to put back");
            }

            var restored = SaveArchive.HashOfExtracted(staging, entries);

            // Everything above this line is off to one side. Only now is the live tree touched.
            var aside = CopyAside(install, units, local, asideDirectory, now);

            foreach (var entry in entries)
            {
                var native = entry.Replace('/', Path.DirectorySeparatorChar);
                var destination = Path.Combine(container, native);

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Move(Path.Combine(staging, native), destination, overwrite: true);
            }

            return new SaveUnitRestoreResult(restored, entries, aside);
        }
        finally
        {
            try
            {
                if (Directory.Exists(staging))
                {
                    Directory.Delete(staging, recursive: true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Staging litter under partial/ costs disk and nothing else.
            }
        }
    }

    /// <summary>
    /// Copies a unit's current members aside, and returns where they went.
    /// </summary>
    /// <remarks>
    /// Copied rather than moved: if anything after this fails, the unit the emulator reads is
    /// still the one that was always there. A unit with nothing on disk is the new-device
    /// restore and has nothing to keep.
    /// </remarks>
    public static RelativePath? CopyAside(
        RetroBatInstall install,
        SaveUnitScanner units,
        LocalSave local,
        RelativePath asideDirectory,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(local);

        var unit = Find(units, local);

        if (unit is null || unit.Files.Count == 0)
        {
            return null;
        }

        var aside = asideDirectory.Combine($"{now:yyyyMMddTHHmmss}-{local.System}-{local.UnitKey}");
        var asidePath = install.Resolve(aside);

        try
        {
            foreach (var file in unit.Files)
            {
                var destination = Path.Combine(
                    asidePath,
                    file.ArchivePath.Replace('/', Path.DirectorySeparatorChar));

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(install.Resolve(file.Path), destination, overwrite: true);
            }

            return aside;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Reported by the caller failing rather than swallowed: replacing a unit with no
            // copy aside is exactly what principle 1 forbids.
            throw new IOException(
                $"the existing save at {local.Path}/{local.UnitKey} could not be copied aside, so "
                    + $"it was not replaced: {ex.Message}",
                ex);
        }
    }

    /// <summary>Re-reads a unit off disk, since a stored row is a record and not the tree.</summary>
    public static SaveUnit? Find(SaveUnitScanner units, LocalSave local)
    {
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(local);

        return units
            .Scan(local.System)
            .FirstOrDefault(candidate =>
                candidate.Container == local.Path
                && string.Equals(candidate.Key, local.UnitKey, StringComparison.OrdinalIgnoreCase));
    }
}
