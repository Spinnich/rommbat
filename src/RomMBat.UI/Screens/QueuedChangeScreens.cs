using System.Globalization;
using RomMBat.Core;
using RomMBat.Core.Content;
using RomMBat.Core.Store;
using RomMBat.UI.Input;
using RomMBat.UI.Shell;

namespace RomMBat.UI.Screens;

/// <summary>
/// The settings RomMBat is holding until EmulationStation closes.
/// </summary>
/// <remarks>
/// <b>A user who queued a change from the console can change their mind from the couch.</b> The
/// status screen says a change is waiting; this cancels it. Canceling is the whole of the write
/// half here, because the only other thing that can happen to a queued row is being applied, and
/// that cannot happen while this interface is on screen.
/// <para>
/// <b>The UI can never write <c>es_settings.cfg</c> itself, and there is no arrangement under
/// which it can.</b> EmulationStation loads that file at startup and serializes its own model
/// over anything written afterwards, and RomMBat is launched from the ES menu, so it runs under
/// a live ES every single time. Queueing is not a convenience here, it is the only mechanism,
/// which is why "waiting for you to quit EmulationStation" is the permanent honest answer rather
/// than a transient one. The boundary is asserted structurally: this project never references
/// <c>EsSettingsFile</c>.
/// </para>
/// <para>
/// <b>A finished row keeps its outcome.</b> Nothing is watching when <c>background quit</c>
/// drains the queue, because the UI exited before the quit hook fired, so the result outliving
/// the apply is what lets this screen say what happened while RomMBat was not running. Only a
/// cancellation deletes, because nothing happened and there is nothing to report.
/// </para>
/// </remarks>
public static class QueuedChangeScreens
{
    /// <summary>How many finished rows are worth showing before the list stops being readable.</summary>
    private const int RecentlyDone = 10;

    /// <summary>What is waiting, and what recently happened.</summary>
    public static IScreen List(InstallSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        List<PendingConfig> shown = [];

        IReadOnlyList<ListRow> Rows()
        {
            var store = session.Store.PendingConfig;
            var outstanding = store.ListOutstanding();
            var finished = store.ListFinished(RecentlyDone);

            shown = [.. outstanding, .. finished];

            return
            [
                .. outstanding.Select(Waiting),
                .. finished.Select(Finished),
            ];
        }

        // Kept so the question draws over this list.
        ListScreen? screen = null;

        screen = new ListScreen(
            "Pending settings",
            Rows,
            index => shown[index].IsOutstanding
                ? ScreenCommand.Push(CancelConfirm(session, shown[index], screen))
                : ScreenCommand.Stay,
            acceptLabel: "Cancel this change",
            backLabel: "Back")
        {
            EmptyMessage = "Nothing is queued. RomMBat holds a setting here when it cannot write "
                + "it yet, which is any time EmulationStation is running, which is any time you "
                + "are looking at this.",
            Note = () => Note(session),
        };

        return screen;
    }

    /// <summary>The line above the rows, which says why anything is waiting at all.</summary>
    private static string? Note(InstallSession session) =>
        session.Store.PendingConfig.ListOutstanding().Count == 0
            ? null
            : "Applied when you next quit EmulationStation. Nothing has been written yet.";

    /// <summary>One outstanding change, which is the only kind that can be acted on.</summary>
    private static ListRow Waiting(PendingConfig change) =>
        new(
            change.FsName,
            "waiting",
            $"{change.System}: {change.Reason}");

    /// <summary>
    /// One change something already got to.
    /// </summary>
    /// <remarks>
    /// Shown but not choosable, rather than hidden. A refusal is the one a person most needs to
    /// see and it is the one that produces no other sign: the setting is simply not what they
    /// asked for, weeks later, with the console output that explained it long gone.
    /// </remarks>
    private static ListRow Finished(PendingConfig change) =>
        new(
            change.FsName,
            change.Result switch
            {
                PendingConfigResult.Applied => "done",
                PendingConfigResult.Refused => "refused",
                PendingConfigResult.Failed => "failed",
                _ => "finished",
            },
            change.Detail is { } detail
                ? $"{Moment(change.AppliedAtUtc)}: {detail}"
                : $"{change.System}, {Moment(change.AppliedAtUtc)}",
            Available: false);

    /// <summary>Dropping a queued change before anything acts on it.</summary>
    /// <remarks>
    /// Confirmed rather than done on the press, because the queue is where a conversion a person
    /// set up from the console lands, and the row does not say enough on its own for a mispress
    /// to be obviously wrong.
    /// </remarks>
    private static ConfirmScreen CancelConfirm(InstallSession session, PendingConfig change, IScreen? underneath)
    {
        ConfirmScreen? box = null;

        box = new ConfirmScreen(
            "Cancel this change?",
            [
                new ConfirmButton(
                    "Cancel it",
                    () =>
                    {
                        session.Store.PendingConfig.Cancel(change.System, change.FsName, change.SettingKey);

                        return box!.Answer(
                            "Change canceled. Nothing was written and nothing will be. The setting stays as it is.");
                    }),
                new ConfirmButton("Keep it queued", () => ScreenCommand.Pop),
            ],
            1,
            underneath)
        {
            Details = () =>
            [
                new ListRow(
                    change.FsName,
                    change.System,
                    // The stored reason is a phrase, not a sentence, because the list row prints
                    // it after the system's name.
                    $"Queued: {change.Reason.TrimEnd('.')}. Nothing has been written yet, so "
                        + "canceling leaves the setting exactly as it is now.",
                    false),
            ],
        };

        return box;
    }

    private static string Moment(DateTimeOffset? moment) =>
        moment is { } at
            ? at.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture)
            : "never";
}
