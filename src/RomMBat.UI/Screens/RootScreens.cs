using System.Globalization;
using RomMBat.Core;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.UI.Shell;

namespace RomMBat.UI.Screens;

/// <summary>
/// The first screen, and the way to everything else.
/// </summary>
/// <remarks>
/// <b>Every verb is a row, because the buttons ran out.</b> Until stage 7b-3 the root put one
/// action on each of Accept, Start, Extra and Alternate and had nothing left: this stage adds
/// conflicts, platforms and queued changes, which is three more than there are buttons. A list
/// grows by a row where a footer cannot grow by a button, and a row can say what it is for in
/// words rather than in a glyph a person has to have learned.
/// <para>
/// <b>The count that motivates a verb belongs on its own row.</b> A number a user has to know
/// before they would think to press anything, buried one screen deep behind "this device",
/// would mean the interface knew about a stalled sync and did not say so. The rest of the facts
/// are behind that row, where they are read rather than acted on.
/// </para>
/// <para>
/// <b>Nothing here names a button</b>, and every row is available whether or not this install
/// is paired: defining a set, setting a budget, browsing what is already here and reading the
/// mapping all work with the server switched off, and a row that vanished when it was off would
/// be claiming otherwise.
/// </para>
/// </remarks>
public static class RootScreens
{
    /// <summary>Where each row goes, wired by the shell.</summary>
    /// <remarks>
    /// Taken as functions rather than built here for the reason <c>StatusViewModel</c> took
    /// them: pairing needs a connection and a cancellation token the root has no business
    /// owning, and a test drives these screens by handing in its own.
    /// </remarks>
    public sealed record RootRoutes
    {
        public Func<IScreen>? StartPairing { get; init; }

        public Func<IScreen>? OpenSets { get; init; }

        public Func<IScreen>? OpenBrowse { get; init; }

        public Func<IScreen>? OpenBudget { get; init; }

        public Func<IScreen>? OpenConflicts { get; init; }

        public Func<IScreen>? OpenPlatforms { get; init; }

        public Func<IScreen>? OpenQueued { get; init; }
    }

    /// <summary>The root menu.</summary>
    /// <param name="gamepad">
    /// Asked again on every render rather than captured, because a controller can be switched on
    /// after RomMBat has started. It is passed straight through to the screen behind the last
    /// row, which is where the answer is shown.
    /// </param>
    public static IScreen Menu(InstallSession session, Func<GamepadStatus> gamepad, RootRoutes routes)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(gamepad);
        ArgumentNullException.ThrowIfNull(routes);

        // Re-read on every draw rather than captured. The counts are what make these rows worth
        // showing, and a set synced or a conflict resolved above this screen would otherwise
        // leave it stating the numbers from before, which is the bug that made the sets list
        // stale in 7b-2a and the status screen stop being a snapshot in 7b-1.
        List<Func<IScreen>?> destinations = [];

        IReadOnlyList<ListRow> Rows()
        {
            destinations.Clear();
            var rows = new List<ListRow>();

            void Add(ListRow row, Func<IScreen>? destination)
            {
                rows.Add(row);
                destinations.Add(destination);
            }

            var store = session.Store;
            var device = store.Device.Read();
            var conflicts = store.SaveConflicts.ListOpen().Count;
            var unmapped = store.PlatformMap.List().Count(row => row.Folder is null);
            var queued = store.PendingConfig.ListOutstanding().Count;
            var outbox = store.Outbox.PendingCount();

            Add(
                new ListRow("Sync sets", null, "What this device keeps: a platform, a collection or a search."),
                routes.OpenSets);

            Add(
                new ListRow("Find a game", null, "Search the library, or read what is already here."),
                routes.OpenBrowse);

            Add(
                new ListRow(
                    "Conflicts",
                    conflicts == 0 ? "none" : Plural(conflicts, "save"),
                    conflicts == 0
                        ? "Nothing is waiting on a decision."
                        : "Both sides were kept. Nothing was overwritten, and nothing syncs until you choose."),
                routes.OpenConflicts);

            Add(
                new ListRow(
                    "Platforms",
                    // Not Plural: "unmapped" is an adjective, and pluralising it produced
                    // "5 unmappeds" on the live install. Only count nouns go through Plural.
                    unmapped == 0 ? "all mapped" : Counted(unmapped, "unmapped"),
                    unmapped == 0
                        ? "Where each RomM platform's games land in RetroBat."
                        : "Games on an unmapped platform have nowhere to go, so a sync skips them."),
                routes.OpenPlatforms);

            Add(
                new ListRow(
                    "Queued changes",
                    // "3 waitings" for the same reason.
                    queued == 0 ? "none" : Counted(queued, "waiting"),
                    queued == 0
                        ? "Settings RomMBat is holding until EmulationStation closes."
                        : "Applied when you next quit EmulationStation, which cannot happen while it is running."),
                routes.OpenQueued);

            Add(
                new ListRow("Disk space", Cap(session), "How much room the sync sets may use together."),
                routes.OpenBudget);

            Add(
                new ListRow(
                    device?.IsPaired == true ? "Pair again" : "Pair with RomM",
                    Paired(device),
                    device?.IsPaired == true
                        ? "Sign in again, or move this device to a different server."
                        : "Sync your games, saves and play time."),
                routes.StartPairing);

            Add(
                new ListRow(
                    "This device",
                    session.Install.ReadVersionString() ?? "not readable",
                    outbox == 0
                        ? "RetroBat, the store, the controller and what is waiting to be sent."
                        : $"{Plural(outbox, "item")} waiting to reach the server."),
                () => new StatusViewModel(session, gamepad));

            return rows;
        }

        return new ListScreen(
            "RomMBat",
            Rows,
            index => destinations[index] is { } open ? ScreenCommand.Push(open()) : ScreenCommand.Stay,
            acceptLabel: "Open",
            // Not "RetroBat", and deliberately. EmulationStation is the front end this returns
            // to, and its own menu for the same action reads "QUIT EMULATIONSTATION". You also
            // never left RetroBat: RomMBat runs inside the tree.
            backLabel: "Back to EmulationStation");
    }

    /// <summary>The pairing state as the row says it, including the one that needs acting on.</summary>
    private static string Paired(DeviceRecord? device)
    {
        if (device?.IsPaired != true)
        {
            return "not paired";
        }

        // Named here rather than found out as a failure the next time something syncs. A scoped,
        // expiring token is the recommended default on a portable drive, so this is ordinary.
        return device.IsTokenExpired(DateTimeOffset.UtcNow) ? "token expired" : "paired";
    }

    /// <summary>The disk cap as the row says it, or that there is not one.</summary>
    /// <remarks>
    /// Only the budget, not the free-space floor. The floor is always on and is a safety net
    /// rather than a choice, so quoting it here would put a number on the row that a user
    /// pressing it cannot change into "no cap".
    /// </remarks>
    private static string Cap(InstallSession session) =>
        session.Store.Settings.GetInt64(SettingStore.ContentMaxBytes) is { } bytes and > 0
            ? ByteSize.Format(bytes)
            : "no cap";

    /// <summary>A count of a thing, where the thing is a noun that takes an "s".</summary>
    private static string Plural(int count, string noun) =>
        string.Create(CultureInfo.InvariantCulture, $"{count} {noun}{(count == 1 ? string.Empty : "s")}");

    /// <summary>
    /// A count followed by a word that does not inflect.
    /// </summary>
    /// <remarks>
    /// <b>Because "unmapped" and "waiting" are not nouns.</b> Both went through
    /// <see cref="Plural"/> and reached a real screen as "5 unmappeds" and "3 waitings". An
    /// adjective describing the counted things does not take the plural the things do, and the
    /// only way to tell the two cases apart is at the call site.
    /// </remarks>
    private static string Counted(int count, string word) =>
        string.Create(CultureInfo.InvariantCulture, $"{count} {word}");
}
