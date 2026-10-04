using System.Globalization;
using RomMBat.Core;
using RomMBat.Core.Metadata;
using RomMBat.Core.Store;
using RomMBat.UI.Input;
using RomMBat.UI.Shell;

namespace RomMBat.UI.Screens;

/// <summary>
/// What has not reached the server, and the interface's way to clear it.
/// </summary>
/// <remarks>
/// <b>The same two drops as <c>rommbat-agent outbox</c>, and no others.</b> A refused entry can be
/// dropped one at a time or all together, because nothing retries it. Unsent entries can only be
/// dropped all together, because they send by themselves and the one reason to drop them is that
/// their server is gone for good, which pairing with a different one refuses until they are.
/// <para>
/// <b>Every drop is confirmed, and the confirmation says what is lost.</b> Dropping is the only
/// place a queued record is deleted without reaching the server. The rows go, and nothing on the
/// drive does: the store's deletes touch the outbox table and nothing else.
/// </para>
/// <para>
/// <b>Counted again at the press.</b> A flush may send or refuse entries while a confirmation is
/// on screen, so the result reports what the store deleted rather than what the screen counted.
/// </para>
/// </remarks>
public static class OutboxScreens
{
    /// <summary>The refused entries, then the unsent ones as a single row.</summary>
    public static IScreen List(InstallSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        List<Func<IScreen>> destinations = [];

        IReadOnlyList<ListRow> Rows()
        {
            destinations.Clear();
            var rows = new List<ListRow>();
            var outbox = session.Store.Outbox;
            var failed = outbox.Failed();
            var pending = outbox.PendingCount();

            var titles = session.Store.Metadata.ForRoms(
                failed.Where(entry => entry.RomId is not null).Select(entry => (int)entry.RomId!.Value));

            foreach (var entry in failed)
            {
                var title = Title(entry, titles);
                rows.Add(Refused(entry, title));
                destinations.Add(() => DropOne(session, entry, title));
            }

            if (failed.Count > 1)
            {
                rows.Add(new ListRow(
                    "Every refused entry",
                    $"{failed.Count} refused",
                    "Drop them all at once."));
                destinations.Add(() => DropRefused(session));
            }

            if (pending > 0)
            {
                rows.Add(new ListRow(
                    "Not sent yet",
                    $"{pending} waiting",
                    "These send by themselves on the next sync. Drop them only if their server is gone for good."));
                destinations.Add(() => DropPending(session));
            }

            return rows;
        }

        return new ListScreen(
            "Outbox",
            Rows,
            index => ScreenCommand.Push(destinations[index]()),
            acceptLabel: "Drop",
            backLabel: "Back")
        {
            EmptyMessage = "Nothing is waiting. A save, save state or play session made without the "
                + "server waits here until it is sent.",
            Note = () => session.Store.Outbox.FailedCount() == 0
                ? null
                : "The server refused these and nothing will try them again. They exist only on this device.",
        };
    }

    /// <summary>One entry the server refused, named after its game where the store knows it.</summary>
    private static ListRow Refused(OutboxEntry entry, string title) =>
        new(
            title,
            $"{Kind(entry.Kind)} refused",
            entry.Slot is { } slot
                ? $"Slot {slot}. {entry.LastError}"
                : entry.LastError);

    /// <summary>Dropping one refused entry.</summary>
    private static ListScreen DropOne(InstallSession session, OutboxEntry entry, string title) =>
        Confirm(
            $"Drop this {Kind(entry.Kind)}?",
            new ListRow(
                title,
                Moment(entry.RecordedAtUtc),
                $"The server refused it: {entry.LastError} Dropping deletes RomMBat's record of it. "
                    + "Nothing on the drive is deleted.",
                false),
            () => session.Store.Outbox.DropFailed(entry.Id),
            _ => "The server never received it, so it exists only on this device.");

    /// <summary>Dropping every refused entry.</summary>
    private static ListScreen DropRefused(InstallSession session) =>
        Confirm(
            "Drop every refused entry?",
            new ListRow(
                "Refused",
                $"{session.Store.Outbox.FailedCount()} entries",
                "The server refused each of these and nothing will try them again. Dropping deletes "
                    + "RomMBat's records of them. Nothing on the drive is deleted.",
                false),
            () => session.Store.Outbox.DropFailed(),
            dropped => $"{Entries(dropped)} dropped. The server never received them, so they exist only "
                + "on this device.");

    /// <summary>Dropping every unsent entry, for an install whose server is gone.</summary>
    /// <remarks>
    /// Said in terms of pairing, because that is how a person arrives here: pairing with a
    /// different server is refused while these name the old one's games.
    /// </remarks>
    private static ListScreen DropPending(InstallSession session) =>
        Confirm(
            "Drop everything not sent yet?",
            new ListRow(
                "Not sent yet",
                $"{session.Store.Outbox.PendingCount()} waiting",
                "The server has never seen these, and they send by themselves when it can be reached. "
                    + "Drop them only when that server is gone for good, so this device can be paired "
                    + "with another. Nothing on the drive is deleted.",
                false),
            () => session.Store.Outbox.DropPending(),
            dropped => $"{Entries(dropped)} dropped. They exist only on this device.");

    /// <summary>
    /// A pane of facts with one verb, in <c>QueuedChangeScreens.CancelConfirm</c>'s shape.
    /// </summary>
    /// <param name="after">What happened, given how many the store deleted, when that is any.</param>
    private static ListScreen Confirm(
        string title,
        ListRow before,
        Func<int> drop,
        Func<int, string> after)
    {
        int? dropped = null;
        ListScreen? screen = null;

        screen = new ListScreen(
            title,
            () =>
            [
                dropped is { } count
                    ? count == 0
                        ? new ListRow("Nothing dropped", null, "Nothing was left to drop by the time it was chosen.", false)
                        : new ListRow("Dropped", null, after(count), false)
                    : before,
            ],
            _ => ScreenCommand.Stay,
            acceptLabel: "Drop",
            backLabel: "Keep")
        {
            Reading = true,
            OfferAcceptWhen = () => dropped is null,
            BackLabelWhen = () => dropped is null ? "Keep" : "Done",

            Verbs = (action, _) =>
            {
                if (action != NavAction.Accept || dropped is not null)
                {
                    return null;
                }

                dropped = drop();

                // A verb's Stay does not re-read the rows the way a chosen row's does, and the
                // pane would go on describing what is no longer there.
                screen!.Returned();
                return ScreenCommand.Stay;
            },
        };

        return screen;
    }

    private static string Title(OutboxEntry entry, IReadOnlyDictionary<int, GameMetadata> titles) =>
        entry.RomId is { } romId
            ? titles.TryGetValue((int)romId, out var game) ? game.Name : $"Game {romId}"
            : "No game";

    private static string Kind(OutboxKind kind) => kind switch
    {
        OutboxKind.Save => "save",
        OutboxKind.State => "save state",
        OutboxKind.PlaySession => "play session",
        _ => "entry",
    };

    private static string Entries(int count) =>
        count == 1 ? "1 entry" : $"{count.ToString(CultureInfo.CurrentCulture)} entries";

    private static string Moment(DateTimeOffset moment) =>
        moment.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
}
