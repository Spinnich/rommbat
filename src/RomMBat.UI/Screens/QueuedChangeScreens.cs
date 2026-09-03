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
/// <b>Stage 7b-1 could read this queue and could not touch it, and that was the gap.</b> A user
/// who queued a change from the console had a row on the status screen saying it was waiting and
/// no way to change their mind about it from the couch. Cancelling is the whole of the write
/// half here, because the only other thing that can happen to a queued row is being applied, and
/// that cannot happen while this interface is on screen.
/// <para>
/// <b>The UI can never write <c>es_settings.cfg</c> itself, and there is no arrangement under
/// which it can.</b> EmulationStation loads that file at startup and serialises its own model
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

        return new ListScreen(
            "Queued changes",
            Rows,
            index => shown[index].IsOutstanding
                ? ScreenCommand.Push(CancelConfirm(session, shown[index]))
                : ScreenCommand.Stay,
            acceptLabel: "Cancel this change",
            backLabel: "Back")
        {
            EmptyMessage = "Nothing is queued. RomMBat holds a setting here when it cannot write "
                + "it yet, which is any time EmulationStation is running, which is any time you "
                + "are looking at this.",
            Note = () => Note(session),
        };
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
    private static ListScreen CancelConfirm(InstallSession session, PendingConfig change)
    {
        var cancelled = false;

        return new ListScreen(
            "Cancel this change?",
            () =>
            [
                cancelled
                    ? new ListRow(
                        "Cancelled",
                        null,
                        "Nothing was written and nothing will be. The setting stays as it is.",
                        false)
                    : new ListRow(
                        change.FsName,
                        change.System,
                        $"{change.Reason} Nothing has been written yet, so cancelling leaves the "
                            + "setting exactly as it is now.",
                        false),
            ],
            _ => ScreenCommand.Stay,
            acceptLabel: "Cancel it",
            backLabel: cancelled ? "Done" : "Keep it queued")
        {
            Reading = true,
            OfferAcceptWhen = () => !cancelled,

            Verbs = (action, _) =>
            {
                if (action != NavAction.Accept || cancelled)
                {
                    return null;
                }

                session.Store.PendingConfig.Cancel(change.System, change.FsName, change.SettingKey);
                cancelled = true;
                return ScreenCommand.Stay;
            },
        };
    }

    /// <summary>
    /// Whether this game can be given a memory card of its own.
    /// </summary>
    /// <remarks>
    /// <b>Asked of <see cref="SaveConverter"/>, never worked out here.</b> Which save shapes can
    /// be converted, which discs of a set are refused, and what a shared container leaves behind
    /// are all rules with their own tests, and a screen deciding any of them would be a second
    /// copy that drifts.
    /// <para>
    /// A preview writes nothing and reads only the local tree and store, so it is cheap enough
    /// for a footer to ask on every draw. It is also the honest gate: offering the verb on a
    /// game the converter would refuse is a press that walks through a screen and does nothing.
    /// </para>
    /// </remarks>
    public static bool CanConvert(InstallSession session, int romId)
    {
        ArgumentNullException.ThrowIfNull(session);

        // Already queued is not offered again. Queue() would replace the outstanding row, which
        // is right for a person changing their mind and reads as a no-op for a person pressing
        // the same thing twice; the change is on the queued-changes screen either way.
        if (session.Store.PendingConfig.ListOutstandingForRom(romId).Count > 0)
        {
            return false;
        }

        // PreviewQueue, never Preview. Preview describes writing the setting now and so refuses
        // while EmulationStation is running, and this interface is launched from the ES menu, so
        // ES is running every single time it runs. Asking the wrong one made this verb invisible
        // on every game on every install until a hands-on pass went looking for it.
        return new SaveConverter(session.Install, session.Store).PreviewQueue(romId).Status
            == ConversionStatus.Ready;
    }

    /// <summary>
    /// Queueing a per-game memory card for one game.
    /// </summary>
    /// <remarks>
    /// <b>Queued, never written.</b> There is no apply path from here and there cannot be one:
    /// EmulationStation serialises its own model over anything written to
    /// <c>es_settings.cfg</c> while it is running, and this interface only ever runs while it
    /// is. The console has <c>--apply</c> because it can be run with ES closed.
    /// <para>
    /// The warning is shown before the press rather than after it. A conversion moves the game
    /// off the console's shared card, so its existing saves stay on the old one, and that is the
    /// thing a person needs to know while they can still decline.
    /// </para>
    /// </remarks>
    public static IScreen Convert(InstallSession session, int romId, string title)
    {
        ArgumentNullException.ThrowIfNull(session);

        var converter = new SaveConverter(session.Install, session.Store);

        // The same question the footer's gate asked, for the same reason: this screen can only
        // queue, so previewing an apply would describe a refusal about something it never does.
        var preview = converter.PreviewQueue(romId);

        ConversionResult? queued = null;

        return new ListScreen(
            $"Give '{title}' its own memory card?",
            () =>
            [
                queued is { } done
                    ? new ListRow(
                        done.Ok ? "Queued" : "Not queued",
                        null,
                        done.Detail,
                        false)
                    : new ListRow("What changes", null, preview.Detail, false),

                .. (queued is null && preview.Warning is { } warning)
                    ? new[] { new ListRow("Worth knowing", null, warning, false) }
                    : [],

                .. queued is null
                    ?
                    [
                        new ListRow(
                            "When",
                            "on quitting",
                            "Nothing is written now. RomMBat holds the change and makes it when "
                                + "you next quit EmulationStation, which is the only time it can.",
                            false),
                    ]
                    : Array.Empty<ListRow>(),
            ],
            _ => ScreenCommand.Stay,
            acceptLabel: "Queue it",
            backLabel: queued is null ? "Leave it alone" : "Done")
        {
            Reading = true,

            // Offered once, and only while the converter still says it would work. A second
            // press would replace the row it just wrote, which reads as nothing happening.
            OfferAcceptWhen = () => queued is null && preview.Status == ConversionStatus.Ready,

            Verbs = (action, _) =>
            {
                if (action != NavAction.Accept || queued is not null)
                {
                    return null;
                }

                queued = converter.Queue(romId);
                return ScreenCommand.Stay;
            },
        };
    }

    private static string Moment(DateTimeOffset? moment) =>
        moment is { } at
            ? at.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture)
            : "never";
}
