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
    /// Unpacks a fetched archive over a unit, staged and copied aside but not atomically.
    /// </summary>
    /// <remarks>
    /// Nothing touches the live tree until the whole unit is extracted and readable, and the
    /// current members are copied aside before anything is replaced. A half-written directory
    /// save is a corrupt one.
    /// <para>
    /// <b>A unit that already holds what arrived is left alone entirely.</b> No copy aside for a
    /// save nobody replaced, no mtime churn under <c>saves/</c>, and no window where the unit is
    /// half swapped for no reason. It cannot be settled before the transfer, because the wire
    /// hash for an unchanged class C unit is the digest the server returned to this device on
    /// its own last upload and a peer's upload carries one this device has never seen: negotiate
    /// answers <c>download</c> and the bytes have to come. Only the write is avoidable.
    /// </para>
    /// <para>
    /// <b>The swap itself is not atomic, and calling it atomic would be worse than the gap.</b>
    /// The members are removed and then moved in one at a time, so a failure partway leaves some
    /// new members and some old ones in the container, which an emulator may read as corrupt.
    /// Nothing is lost: the pre-restore members are under <c>replaced/</c> and the staged copy
    /// was extracted and hashed before any of this ran, so recovery exists and is manual.
    /// A whole-container swap is not the fix, because the container is shared: <c>saves/psp/SAVEDATA</c>
    /// holds every PSP game on the install. Tracked in #38.
    /// </para>
    /// <para>
    /// <b>This replaces the unit rather than merging into it.</b> A member the archive does not
    /// name is one the device that sent it deleted, usually an in-game slot, so it is removed
    /// here. Leaving it made the fold over the tree disagree with the fold over the archive, and
    /// the next scan then read the merged unit as changed and put it back over the server's copy:
    /// a user who asked to discard the local side got a merge, propagated silently.
    /// </para>
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

            // Read once and used three times, since a scan of the system is what finds a unit
            // and the comparison, the copy aside and the removal below must agree on the same
            // member list.
            var existing = Find(units, local);

            // <b>A restore that would rewrite the tree with what it already holds does not
            // rewrite it.</b> Class C cannot settle this before the transfer: the wire hash for
            // an unchanged unit is the digest the server returned to THIS device on its own last
            // upload, and a peer's upload carries a digest this device has never seen, so
            // negotiate answers `download` and no local comparison can rule it out. The bytes
            // have to come. The write does not.
            //
            // Compared against the fold of what is on disk now rather than against the row,
            // because the row is only as current as the last scan and this is a question about
            // the tree. It costs one pass over a unit that was about to be copied aside and
            // rewritten member by member, so it is cheaper than what it skips.
            //
            // The caller still acks and still records the slot: the server does need telling,
            // and the slot's digest does need to become current, or the next negotiate answers
            // `upload` for a unit that is already in step.
            if (existing is { Files.Count: > 0 }
                && string.Equals(
                    SaveArchive.HashOf(install, existing),
                    restored,
                    StringComparison.OrdinalIgnoreCase))
            {
                return new SaveUnitRestoreResult(restored, entries, CopiedAside: null);
            }

            // Everything above this line is off to one side. Only now is the live tree touched.
            var aside = CopyAside(install, existing, local, asideDirectory, now);

            // Before the moves, so a member that cannot be deleted fails the restore with the
            // unit still whole rather than half swapped.
            Remove(install, existing, entries, container);

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
        ArgumentNullException.ThrowIfNull(units);

        return CopyAside(install, Find(units, local), local, asideDirectory, now);
    }

    private static RelativePath? CopyAside(
        RetroBatInstall install,
        SaveUnit? unit,
        LocalSave local,
        RelativePath asideDirectory,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(local);

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

    /// <summary>
    /// Deletes the members the archive does not name, which is what makes this a replace.
    /// </summary>
    /// <remarks>
    /// Matched case-insensitively, because the archive was written by another device and Windows
    /// does not distinguish the two names anyway. The directories emptied by it go too: a class C
    /// member is a savedata folder the emulator lists, and an empty one is a slot the game shows
    /// as present. Nothing above the container is touched, since other games live there.
    /// </remarks>
    private static void Remove(
        RetroBatInstall install,
        SaveUnit? unit,
        IReadOnlyList<string> entries,
        string container)
    {
        if (unit is null)
        {
            return;
        }

        var keep = new HashSet<string>(entries, StringComparer.OrdinalIgnoreCase);
        var emptied = new List<string>();

        foreach (var file in unit.Files)
        {
            if (keep.Contains(file.ArchivePath))
            {
                continue;
            }

            var absolute = install.Resolve(file.Path);
            File.Delete(absolute);
            emptied.Add(Path.GetDirectoryName(absolute)!);
        }

        PruneEmpty(container, emptied);
    }

    /// <summary>Removes the directories the deletes left empty, no further up than the container.</summary>
    private static void PruneEmpty(string container, IEnumerable<string> directories)
    {
        var root = Path.GetFullPath(container).TrimEnd(Path.DirectorySeparatorChar);

        foreach (var directory in directories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var current = Path.GetFullPath(directory);

            while (current.Length > root.Length
                && current.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (Directory.EnumerateFileSystemEntries(current).Any())
                    {
                        break;
                    }

                    Directory.Delete(current);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // An empty directory left behind is untidy and not a corrupt save, so it
                    // never fails a restore that has already put every member in place.
                    break;
                }

                current = Path.GetDirectoryName(current) ?? root;
            }
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
