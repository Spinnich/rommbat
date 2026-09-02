using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;

namespace RomMBat.Core.Content;

/// <summary>What the inventory claims against what the tree actually holds.</summary>
/// <param name="Rows">Every <c>local_file</c> row, of every kind.</param>
/// <param name="Missing">Rows whose file is not there.</param>
/// <param name="MissingBytes">What those rows claim, which is what the budget is counting in error.</param>
/// <param name="Folders">Which folders they are in, worst first, so a person can see the shape of it.</param>
public sealed record InventoryReport(
    int Rows,
    IReadOnlyList<LocalFile> Missing,
    long MissingBytes,
    IReadOnlyList<(string Folder, int Count, long Bytes)> Folders)
{
    public bool IsClean => Missing.Count == 0;

    /// <summary>
    /// True when nothing recorded was found at all, which is a tree to distrust rather than one
    /// to repair.
    /// </summary>
    /// <remarks>
    /// <b>Found by a probe, and it disproved the argument written here first.</b> The claim was
    /// that an unplugged drive cannot reach this, because a tree that does not open has no
    /// session behind it. That is true and it is not enough: a tree carrying
    /// <c>retrobat.ini</c>, <c>system/version.info</c> and the database but no <c>roms/</c>
    /// opens perfectly and reports every one of its rows missing. A copied install, a restored
    /// backup and a <c>roms/</c> that lives on a second volume all reach it.
    /// <para>
    /// A repair there would throw away the entire inventory and cost a re-download and
    /// re-verification of the whole library, which is the most expensive mistake this class
    /// could make. The real state it is for looks nothing like this: on the install that
    /// motivated #113, 5,512 of 5,932 rows were missing and <b>420 were still there</b>.
    /// </para>
    /// </remarks>
    public bool NothingFound => Rows > 0 && Missing.Count == Rows;

    /// <summary>The one line <c>status</c> prints.</summary>
    public string Summary => IsClean
        ? $"{Rows:N0} recorded, all present"
        : NothingFound
            ? $"{Rows:N0} recorded, none of them found. This does not look like the tree they "
                + "were written to, so nothing will be forgotten."
            : $"{Rows:N0} recorded, {Missing.Count:N0} missing ({ByteSize.Format(MissingBytes)})";
}

/// <summary>
/// Finds <c>local_file</c> rows whose bytes are gone, and takes them out.
/// </summary>
/// <remarks>
/// <b>The inventory is what makes a second sync a no-op, and an inventory nobody checks stops
/// being one.</b> Measured on the live install: <b>5,512 of 5,932 rows pointed at files that
/// were not there, claiming 18.22 GiB against 1.41 GiB of real content.</b> The rows are
/// <c>origin='synced'</c> and look exactly like healthy ones. See #113.
/// <para>
/// <b>The damage is not untidiness, it is arithmetic.</b> The disk budget sums
/// <c>local_file</c>, so it counted bytes that do not exist: an 8 GB cap on that install read
/// as permanently 10 GB over, which blocked every game of every sync with 334 problems, none of
/// which pointed at the cause. <c>evict</c> plans against the same figure and would offer to
/// free space that is already free.
/// </para>
/// <para>
/// <b>It self-heals only by accident.</b> <see cref="ContentPlanner"/> checks the filesystem, so
/// a row whose file is gone is planned as a download and reconciled when that game is next
/// synced. Nothing reconciles a row nobody re-syncs, and nothing told anyone the state existed.
/// </para>
/// <para>
/// <b>Removing a row whose file is gone is safe by the rollback's own argument: a row must
/// never outlive its bytes.</b> The row is the claim that the bytes are here, and it is the
/// claim that is wrong. The next sync re-downloads, which is what would have happened anyway.
/// </para>
/// <para>
/// <b>An unplugged drive cannot reach this, and that turned out not to be enough.</b> Every
/// path is resolved against a tree <see cref="InstallSession"/> already opened, which means
/// <c>retrobat.ini</c> and <c>system/version.info</c> were both read off it, so a stick that is
/// not there does not get this far. A probe then found the case that does: a tree carrying
/// those two files and the database but no <c>roms/</c> opens perfectly and reports every one
/// of its rows missing. <see cref="InventoryReport.NothingFound"/> is what refuses that.
/// <para>
/// What this is actually for is a stick whose <c>roms/</c> folders were cleared by hand while
/// the database persisted, which is the state that was measured and which a portable install on
/// a removable drive will reach.
/// </para>
/// </para>
/// </remarks>
public sealed class InventorySweep
{
    private readonly RetroBatInstall _install;
    private readonly LocalStore _store;

    public InventorySweep(RetroBatInstall install, LocalStore store)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(store);

        _install = install;
        _store = store;
    }

    /// <summary>
    /// Checks every row against the tree, without touching anything.
    /// </summary>
    /// <remarks>
    /// One <c>File.Exists</c> per row, which the issue was right to call too slow to put inside
    /// <c>status</c> unconditionally. Measured at 5,932 rows on the live install off a USB
    /// stick, it is a fraction of a second; it is offered rather than run on every invocation
    /// so that a command that answers from the database alone goes on doing so.
    /// </remarks>
    /// <param name="progress">
    /// How many rows have been checked, out of how many. Reported because this is one filesystem
    /// check per row and a live install measured 5,932 of them off a USB stick: a screen with
    /// nothing moving on it is indistinguishable from a hung one, which is what a hands-on pass
    /// said of it.
    /// </param>
    public InventoryReport Plan(IProgress<(int Done, int Total)>? progress = null)
    {
        var rows = _store.Files.List();
        var missing = new List<LocalFile>();

        for (var index = 0; index < rows.Count; index++)
        {
            var file = rows[index];

            if (!Exists(file.Path))
            {
                missing.Add(file);
            }

            // Every hundredth, not every row. The screen redraws its whole panel on each report
            // and five thousand of those is what starves the pad, which is the same reason the
            // sync screen rate-limits its own progress.
            if (progress is not null && (index % 100 == 99 || index == rows.Count - 1))
            {
                progress.Report((index + 1, rows.Count));
            }
        }

        var folders = missing
            .GroupBy(file => file.Folder ?? "bios", StringComparer.OrdinalIgnoreCase)
            .Select(group => (group.Key, group.Count(), group.Sum(file => file.SizeBytes)))
            .OrderByDescending(entry => entry.Item3)
            .ToList();

        return new InventoryReport(
            rows.Count,
            missing,
            missing.Sum(file => file.SizeBytes),
            folders);
    }

    /// <summary>
    /// Removes the rows a report found, re-checking each one first.
    /// </summary>
    /// <remarks>
    /// Re-checked rather than trusted, for the same reason
    /// <see cref="EvictionPlanner.Apply"/> re-asks the guard: a report can be shown to a person,
    /// sat on, and applied later, and a sync in between may have put the file back. Removing
    /// its row then would cost a re-download of a file that is already correct.
    /// </remarks>
    public InventoryRepair Apply(
        InventoryReport report,
        IProgress<(int Done, int Total)>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (report.NothingFound)
        {
            // Refused rather than carried out. See InventoryReport.NothingFound: a tree in
            // which nothing recorded can be found is one to distrust, and emptying the whole
            // inventory costs a re-download and re-verification of the entire library.
            return new InventoryRepair(0, 0, report.Missing.Count);
        }

        var removed = 0;
        var bytes = 0L;
        var returned = 0;

        for (var index = 0; index < report.Missing.Count; index++)
        {
            var file = report.Missing[index];

            if (Exists(file.Path))
            {
                returned++;
            }
            else
            {
                _store.Files.Remove(file.Path);
                removed++;
                bytes += file.SizeBytes;
            }

            if (progress is not null && (index % 100 == 99 || index == report.Missing.Count - 1))
            {
                progress.Report((index + 1, report.Missing.Count));
            }
        }

        return new InventoryRepair(removed, bytes, returned);
    }

    /// <summary>
    /// Whether the bytes are there.
    /// </summary>
    /// <remarks>
    /// Unreadable counts as present. Being unable to answer is not evidence that a file is
    /// gone, and the cost of guessing wrong here is a re-download of something already correct.
    /// </remarks>
    private bool Exists(RelativePath path)
    {
        try
        {
            return File.Exists(_install.Resolve(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }
}

/// <summary>What a repair took out.</summary>
/// <param name="Returned">Rows whose file was back by the time the repair ran, and were kept.</param>
public sealed record InventoryRepair(int Removed, long BytesReclaimed, int Returned)
{
    public string Summary => Removed == 0
        ? "no rows were removed"
        : $"{Removed:N0} {(Removed == 1 ? "row" : "rows")} removed, "
            + $"{ByteSize.Format(BytesReclaimed)} no longer counted against the budget"
            + (Returned > 0 ? $"; {Returned} kept, the file was back" : string.Empty);
}
