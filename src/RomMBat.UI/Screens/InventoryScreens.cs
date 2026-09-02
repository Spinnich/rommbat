using RomMBat.Core;
using RomMBat.Core.Content;
using RomMBat.UI.Input;
using RomMBat.UI.Shell;

namespace RomMBat.UI.Screens;

/// <summary>
/// Checking that what the budget is counting is actually on the drive.
/// </summary>
/// <remarks>
/// <b>Reached from the disk screen, because that is where a person meets the wrong number.</b>
/// Measured on the live install: 5,512 of 5,932 rows pointed at files that were not there,
/// claiming 18.22 GiB against 1.41 GiB of real content. An 8 GB cap on that install read as
/// permanently 10 GB over, so every sync blocked every game with 334 problems and nothing
/// pointing at the cause. It took a database diff to explain. See #113.
/// <para>
/// <b>Never automatic.</b> Removing a row is safe by the rollback's own argument, that a row
/// must never outlive its bytes, but it costs a re-download and that is the user's call. The
/// count is offered on the disk screen and the repair is one more press behind a preview.
/// </para>
/// </remarks>
public static class InventoryScreens
{
    /// <summary>What the inventory claims against what is there, and the offer to fix it.</summary>
    public static IScreen Check(InstallSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var sweep = new InventorySweep(session.Install, session.Store);
        InventoryReport? report = null;

        return new ListScreen(
            "Files RomMBat has recorded",
            () => CheckRows(report),
            _ => ScreenCommand.Stay,
            acceptLabel: string.Empty,
            backLabel: "Back")
        {
            Reading = true,
            LoadingMessage = "Checking every recorded file against the drive.",

            // Offered exactly when it works. There was no hint at all here, so from the couch
            // this screen counted the problem and named no way to fix it: the footer said Back
            // and nothing else, while Start quietly did the repair.
            ExtraHints = () => report is { IsClean: false, NothingFound: false }
                ? [new FooterHint(NavAction.Start, "Forget the files that are not there")]
                : [],
            Load = token =>
            {
                report = sweep.Plan();
                token.ThrowIfCancellationRequested();
                return Task.FromResult<string?>(null);
            },
            Verbs = (action, _) => action switch
            {
                NavAction.Start when report is { IsClean: false, NothingFound: false } found =>
                    ScreenCommand.Push(Repair(session, found)),
                _ => null,
            },
        }.Started();
    }

    private static ListScreen Repair(InstallSession session, InventoryReport report)
    {
        InventoryRepair? repaired = null;

        return new ListScreen(
            "Forgetting files that are not there",
            () => repaired is { } done
                ?
                [
                    new ListRow("Done", null, done.Summary, false),
                    new ListRow(
                        "What this changed",
                        null,
                        "Only what RomMBat had written down. No file was deleted, because there "
                            + "was nothing there to delete. The next sync fetches these games "
                            + "again if a set still wants them.",
                        false),
                ]
                : [],
            _ => ScreenCommand.Stay,
            acceptLabel: string.Empty,
            backLabel: "Done")
        {
            Reading = true,
            LoadingMessage = "Removing records for files that are gone.",
            Load = token =>
            {
                repaired = new InventorySweep(session.Install, session.Store).Apply(report);
                token.ThrowIfCancellationRequested();
                return Task.FromResult<string?>(null);
            },
        }.Started();
    }

    private static List<ListRow> CheckRows(InventoryReport? report)
    {
        if (report is not { } found)
        {
            return [];
        }

        var rows = new List<ListRow>
        {
            new(
                "Recorded",
                $"{found.Rows:N0} files",
                found.IsClean
                    ? "Every one of them is on this drive, so the disk figures are right."
                    : "This is what the disk limit is counted from.",
                false),
        };

        if (found.IsClean)
        {
            return rows;
        }

        if (found.NothingFound)
        {
            rows.Add(new ListRow(
                "None of them found",
                $"{found.Missing.Count:N0} files",
                "Not one recorded file is on this drive, which does not look like an install "
                    + "that lost some games. Nothing will be forgotten here, because emptying "
                    + "the whole record would cost a re-download of the entire library.",
                false));

            return rows;
        }

        rows.Add(new ListRow(
            "Not on the drive",
            $"{found.Missing.Count:N0} files, {ByteSize.Format(found.MissingBytes)}",
            "RomMBat is counting these against the disk limit and they are not here. Until they "
                + "are forgotten, the limit is that much smaller than it looks.",
            false));

        // The shape of it, so a person can recognise "I cleared that folder by hand" rather
        // than being handed one number they have no way to place.
        rows.AddRange(found.Folders.Take(8).Select(entry => new ListRow(
            entry.Folder,
            $"{entry.Count:N0} files, {ByteSize.Format(entry.Bytes)}",
            null,
            false)));

        return rows;
    }
}
