using System.Globalization;
using RomM.Client;
using RomMBat.Core;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
using RomMBat.UI.Input;
using RomMBat.UI.Shell;

namespace RomMBat.UI.Screens;

/// <summary>
/// The saves both sides changed, and the choice only a person can make.
/// </summary>
/// <remarks>
/// <b>Nothing here decides anything.</b> <see cref="ConflictResolutionService"/> holds the tree
/// lock, refuses when a flush has it, and words every outcome; this arranges the rows and turns
/// a press into a call. That is what keeps this file from ever naming <c>TreeLock</c>, which is
/// asserted structurally against the built assembly.
/// <para>
/// <b>There is no default side and there is no "resolve all".</b> Either default silently
/// discards somebody's progress, and the whole reason a conflict exists is that RomMBat cannot
/// tell which side matters. The console refuses to guess and the couch refuses for the same
/// reason: a button that resolved twelve conflicts at once would be that
/// guess made twelve times.
/// </para>
/// <para>
/// <b>Nothing was overwritten while it waited.</b> Both sides are on disk: the server's copy is
/// where it always was and the local file was copied aside when the conflict was first seen. A
/// screen that read as "pick which one to lose" would be describing a design this one does not
/// have, so the rows say what is kept rather than what goes.
/// </para>
/// </remarks>
public static class ConflictScreens
{
    /// <summary>The conflicts waiting on a decision.</summary>
    /// <param name="pair">
    /// Where pairing starts, for a token the server has stopped accepting. Null leaves the offer
    /// off rather than opening a blank screen.
    /// </param>
    public static IScreen List(
        InstallSession session,
        Func<Uri, RomMConnection>? connect = null,
        Func<IScreen>? pair = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        var service = new ConflictResolutionService(session.Install, session.Store);

        // Re-read rather than captured, because resolving one above this screen leaves it
        // showing the list from before. Same shape as the sets list.
        IReadOnlyList<OpenConflict> open = service.Open();

        IReadOnlyList<ListRow> Rows()
        {
            open = service.Open();
            return [.. open.Select(ToRow)];
        }

        return new ListScreen(
            "Conflicts",
            Rows,
            index => ScreenCommand.Push(Detail(session, open[index], connect, pair)),
            acceptLabel: "Choose a side",
            backLabel: "Back")
        {
            EmptyMessage = "No conflicts. A conflict happens when a save changed here and on "
                + "another device between two syncs, and RomMBat keeps both sides rather than "
                + "picking one.",
        };
    }

    /// <summary>One conflict as the list shows it.</summary>
    /// <remarks>
    /// <b>The game's name is the label, and the file name goes under it.</b> Both, for the
    /// reason browse measured: a title alone cannot be matched against what is on disk and a
    /// file name alone is a romset code on some platforms. With neither, a row reads
    /// "Game 295079".
    /// <para>
    /// The slot is on the row too, because a game with four save slots produces four rows that
    /// are otherwise identical.
    /// </para>
    /// </remarks>
    private static ListRow ToRow(OpenConflict open)
    {
        var conflict = open.Conflict;

        return new ListRow(
            open.Title ?? $"Game {conflict.RomId}",
            Moment(conflict.FirstSeenAtUtc),
            open.FileName is { } file
                ? $"{file} ({conflict.Slot}). {conflict.Reason}"
                : $"Slot {conflict.Slot}. {conflict.Reason}");
    }

    /// <summary>
    /// One conflict: what each side is, and the two verbs.
    /// </summary>
    /// <remarks>
    /// <b>A pane of facts, then a choice, then a confirmation.</b> Every row here is something to
    /// read before deciding, so the cursor has nowhere to sit, and the bottom button moves on to
    /// the two sides. Neither side is ever one press: the choice is its own list and the side
    /// chosen is confirmed after it, so the commonest mispress opens a question rather than
    /// answering one.
    /// </remarks>
    public static IScreen Detail(
        InstallSession session,
        OpenConflict open,
        Func<Uri, RomMConnection>? connect = null,
        Func<IScreen>? pair = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(open);

        var conflict = open.Conflict;

        // Kept so the choice draws over this screen.
        ListScreen? screen = null;

        screen = new ListScreen(
            open.Title ?? $"Game {conflict.RomId}",
            () => DetailRows(open),
            _ => ScreenCommand.Stay,
            acceptLabel: "Choose which to keep",
            backLabel: "Back")
        {
            Reading = true,
            AlwaysOfferAccept = true,
            Verbs = (action, _) => action == NavAction.Accept
                ? ScreenCommand.Push(Sides(session, open, connect, pair, screen))
                : null,
        };

        return screen;
    }

    /// <summary>What the detail screen shows about the two sides.</summary>
    private static List<ListRow> DetailRows(OpenConflict open)
    {
        var conflict = open.Conflict;
        // Every row unavailable, because every row is a fact. An available row on a reading
        // pane makes the footer promise an Accept that does nothing, which is the same defect
        // as an action with no hint, pointed the other way.
        var rows = new List<ListRow>
        {
            // The file name here rather than only on the list, because this is the screen a
            // decision is made on and "which game is this" must not need a press to answer.
            new("Game", open.FileName ?? $"id {conflict.RomId}", null, false),
            new("Slot", conflict.Slot, null, false),
            new("Why", conflict.Reason, null, false),
            new("First seen", Moment(conflict.FirstSeenAtUtc), null, false),
            new(
                "This device",
                Short(conflict.LocalHash),
                conflict.LocalCopyPath is { } copy
                    ? $"The save as it stands here. A copy is already kept at {copy.Value}."
                    : "The save as it stands here.",
                false),
            new(
                "The server",
                Short(conflict.ServerHash),
                conflict.ServerUpdatedAt is { } at
                    ? $"Last changed {Moment(at)}, by this or another device."
                    : "The copy RomM holds.",
                false),
        };

        // Said on the screen where the decision is made, rather than left to be inferred from
        // the absence of a warning. Neither side is discarded by either choice: keeping the
        // server's leaves this device's save under replaced/, which nothing prunes, and keeping
        // this device's leaves the server's previous copy in the slot's own history (#326).
        rows.Add(new ListRow(
            "Either way",
            "nothing is deleted",
            "The side you do not keep stays: this device's in emulators/rommbat/replaced/, and "
                + "the server's on RomM as an earlier version of the save.",
            false));

        return rows;
    }

    /// <summary>The two sides as a box of three answers, each side confirmed before it acts.</summary>
    /// <remarks>
    /// Deciding later is selected first, so the commonest mispress closes the question. A side
    /// chosen here opens its own confirmation in this box's place.
    /// </remarks>
    private static ConfirmScreen Sides(
        InstallSession session,
        OpenConflict open,
        Func<Uri, RomMConnection>? connect,
        Func<IScreen>? pair,
        IScreen? underneath)
    {
        var conflict = open.Conflict;

        ScreenCommand Choose(ConflictResolution resolution) =>
            ScreenCommand.Replace(Confirm(session, conflict, resolution, connect, pair, underneath));

        return new ConfirmScreen(
            "Which save do you keep?",
            [
                new ConfirmButton("This device's", () => Choose(ConflictResolution.KeepLocal)),
                new ConfirmButton("The server's", () => Choose(ConflictResolution.KeepServer)),
                new ConfirmButton("Decide later", () => ScreenCommand.Pop),
            ],
            2,
            underneath)
        {
            Details = () =>
            [
                new ListRow(
                    "This device's",
                    null,
                    "Sends it to RomM. The server's copy stays there as an earlier version.",
                    false),
                new ListRow(
                    "The server's",
                    null,
                    "Fetches it here. This device's copy is kept in emulators/rommbat/replaced/.",
                    false),
            ],
        };
    }

    /// <summary>
    /// The confirmation, then the work.
    /// </summary>
    /// <remarks>
    /// Confirmed on its own rather than on the choice, because this is the one action in
    /// RomMBat whose two answers are both irreversible in the sense that matters: the file the
    /// emulator loads next time changes either way.
    /// </remarks>
    private static ConfirmScreen Confirm(
        InstallSession session,
        SaveConflictRecord conflict,
        ConflictResolution resolution,
        Func<Uri, RomMConnection>? connect,
        Func<IScreen>? pair,
        IScreen? underneath)
    {
        var keepingLocal = resolution == ConflictResolution.KeepLocal;

        return new ConfirmScreen(
            keepingLocal ? "Keep this device's save?" : "Keep the server's save?",
            [
                new ConfirmButton(
                    keepingLocal ? "Send it" : "Fetch it",
                    () => ScreenCommand.Replace(Apply(session, conflict, resolution, connect, pair))),
                new ConfirmButton("Back", () => ScreenCommand.Pop),
            ],
            1,
            underneath)
        {
            Details = () =>
            [
                new ListRow(
                    keepingLocal ? "Sent to RomM" : "Fetched from RomM",
                    $"slot {conflict.Slot}",
                    keepingLocal
                        ? "The save on this device is uploaded and becomes the one every other "
                            + "device takes. RomM keeps what was there as an earlier version."
                        : "The server's save is downloaded and verified, replacing the file here. "
                            + "This device's save is kept in emulators/rommbat/replaced/.",
                    false),
            ],
        };
    }

    /// <summary>Carries out the decision and reports what happened.</summary>
    private static ListScreen Apply(
        InstallSession session,
        SaveConflictRecord conflict,
        ConflictResolution resolution,
        Func<Uri, RomMConnection>? connect,
        Func<IScreen>? pair)
    {
        ConflictOutcome? outcome = null;

        return new ListScreen(
            "Resolving",
            () => outcome is { } done
                ?
                [
                    new ListRow(
                        done.Resolved ? "Done" : "Not done",
                        null,
                        done.Message,
                        false),
                ]
                : [],
            _ => ScreenCommand.Stay,
            acceptLabel: string.Empty,
            backLabel: "Done")
        {
            Reading = true,
            LoadingMessage = resolution == ConflictResolution.KeepLocal
                ? "Sending this device's save to RomM..."
                : "Fetching the server's save and verifying it...",

            Load = async token =>
            {
                try
                {
                    // A factory rather than a connection, so the service takes the tree lock
                    // before anything is asked of the server.
                    outcome = await new ConflictResolutionService(session.Install, session.Store)
                        .ResolveAsync(
                            conflict.RomId,
                            conflict.Slot,
                            resolution,
                            () => UiConnection.Open(session, connect),
                            token)
                        .ConfigureAwait(false);
                }
                catch (RomMUnreachableException ex)
                {
                    // Offline is a working state, so an unreachable host is a sentence rather
                    // than a screen that has fallen over.
                    outcome = new ConflictOutcome(
                        ConflictOutcomeState.Offline,
                        $"The server could not be reached ({ex.Message}). Nothing was changed and "
                            + "the conflict is still here.");
                }

                return null;
            },

            // A resolved conflict makes the detail screen and the list under it describe
            // something that is no longer open, so leaving lands on the list, which re-reads. The
            // choice and its confirmation were replaced by this screen, so they are not on the
            // stack. An unresolved one leaves the detail correct, and the person is most likely
            // to want the other side, which is one press back.
            OnBack = () => outcome?.Resolved == true ? ScreenCommand.PopMany(2) : ScreenCommand.Pop,

            // Pairing is the only thing a person can do about a token that will not unlock or
            // one the server has stopped accepting, and a screen that reported the refusal
            // without a route to it strands them. Offered only when that is what happened.
            ActionList = () => Pairable(outcome) && pair is { } start
                ? [new ScreenAction("Pair with RomM", () => ScreenCommand.Push(start()))]
                : [],
        }.Started();
    }

    /// <summary>Whether pairing is the route out of what just happened.</summary>
    /// <remarks>
    /// <b>Three states, not one.</b> Gating on <see cref="ConflictOutcomeState.Failed"/> alone
    /// withheld the offer from the two cases that are nothing but a pairing problem: a store
    /// with no token or one that will not unlock, and a paired install missing its RomM device
    /// id. Failed stays in because a 401 during the transfer itself is the same repair, and
    /// Offline and Busy stay out because pairing does nothing about either.
    /// </remarks>
    private static bool Pairable(ConflictOutcome? outcome) => outcome?.State is
        ConflictOutcomeState.Failed
        or ConflictOutcomeState.NotPaired
        or ConflictOutcomeState.NoDeviceId;

    private static string Short(string? hash) =>
        hash is null ? "no hash" : hash[..Math.Min(8, hash.Length)];

    private static string Moment(DateTimeOffset? moment) =>
        moment is { } at
            ? at.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture)
            : "never";
}
