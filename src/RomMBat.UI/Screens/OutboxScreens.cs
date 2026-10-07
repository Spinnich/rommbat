using System.Globalization;
using RomMBat.Core;
using RomMBat.Core.Metadata;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
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

        // Kept so each question draws over this list.
        ListScreen? screen = null;

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
                destinations.Add(() => DropOne(session, entry, title, screen));
            }

            if (failed.Count > 1)
            {
                rows.Add(new ListRow(
                    "Every refused entry",
                    $"{failed.Count} refused",
                    "Drop them all at once."));
                destinations.Add(() => DropRefused(session, screen));
            }

            if (pending > 0)
            {
                rows.Add(new ListRow(
                    "Not sent yet",
                    $"{pending} waiting",
                    "These send by themselves on the next sync. Drop them only if their server is gone for good."));
                destinations.Add(() => DropPending(session, screen));
            }

            return rows;
        }

        screen = new ListScreen(
            "Waiting to upload",
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

        return screen;
    }

    /// <summary>One entry the server refused, named after its game where the store knows it.</summary>
    private static ListRow Refused(OutboxEntry entry, string title) =>
        new(
            title,
            $"{Kind(entry.Kind)} refused",
            // A play session carries its emulator's battery slot, which names no save of its own.
            entry.Slot is { } slot && entry.Kind != OutboxKind.PlaySession
                ? $"{SaveSlotLabel.Describe(slot)}. {entry.LastError}"
                : entry.LastError);

    /// <summary>Dropping one refused entry.</summary>
    private static ConfirmScreen DropOne(InstallSession session, OutboxEntry entry, string title, IScreen? underneath) =>
        Confirm(
            $"Drop this {Kind(entry.Kind)}?",
            $"{Capitalised(Kind(entry.Kind))} dropped",
            new ListRow(
                title,
                Moment(entry.RecordedAtUtc),
                $"The server refused it: {entry.LastError} Dropping deletes RomMBat's record of it. "
                    + "Nothing on the drive is deleted.",
                false),
            () => session.Store.Outbox.DropFailed(entry.Id),
            _ => "RomM never got it, and nothing will send it now.",
            underneath);

    /// <summary>Dropping every refused entry.</summary>
    private static ConfirmScreen DropRefused(InstallSession session, IScreen? underneath) =>
        Confirm(
            "Drop every refused entry?",
            "Refused entries dropped",
            new ListRow(
                "Refused",
                $"{session.Store.Outbox.FailedCount()} entries",
                "The server refused each of these and nothing will try them again. Dropping deletes "
                    + "RomMBat's records of them. Nothing on the drive is deleted.",
                false),
            () => session.Store.Outbox.DropFailed(),
            NeverSent,
            underneath);

    /// <summary>Dropping every unsent entry, for an install whose server is gone.</summary>
    /// <remarks>
    /// Said in terms of pairing, because that is how a person arrives here: pairing with a
    /// different server is refused while these name the old one's games.
    /// </remarks>
    private static ConfirmScreen DropPending(InstallSession session, IScreen? underneath) =>
        Confirm(
            "Drop everything not sent yet?",
            "Unsent entries dropped",
            new ListRow(
                "Not sent yet",
                $"{session.Store.Outbox.PendingCount()} waiting",
                "The server has never seen these, and they send by themselves when it can be reached. "
                    + "Drop them only when that server is gone for good, so this device can be paired "
                    + "with another. Nothing on the drive is deleted.",
                false),
            () => session.Store.Outbox.DropPending(),
            NeverSent,
            underneath);

    /// <summary>What dropping several means, after the opening words have said they went.</summary>
    /// <remarks>
    /// Not "they exist only on this device", which read after "dropped" as the opposite of it,
    /// and not the count again: "Unsent entries dropped. 2 entries dropped." said it twice.
    /// </remarks>
    private static string NeverSent(int dropped) =>
        dropped == 1
            ? "RomM never got it, and nothing will send it now."
            : $"RomM never got these {dropped}, and nothing will send them now.";

    /// <summary>
    /// The question with what it would drop, and what dropping did once it is answered.
    /// </summary>
    /// <param name="done">What happened, as the outcome's opening words.</param>
    /// <param name="after">What it means, given how many the store deleted, when that is any.</param>
    private static ConfirmScreen Confirm(
        string title,
        string done,
        ListRow before,
        Func<int> drop,
        Func<int, string> after,
        IScreen? underneath)
    {
        ConfirmScreen? box = null;

        box = new ConfirmScreen(
            title,
            [
                new ConfirmButton(
                    "Drop",
                    () =>
                    {
                        var dropped = drop();

                        return box!.Answer(dropped == 0
                            ? "Nothing dropped. Nothing was left to drop by the time it was chosen."
                            : $"{done}. {after(dropped)}");
                    }),
                new ConfirmButton("Keep", () => ScreenCommand.Pop),
            ],
            1,
            underneath)
        {
            Details = () => [before],
        };

        return box;
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

    private static string Capitalised(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static string Moment(DateTimeOffset moment) =>
        moment.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
}
