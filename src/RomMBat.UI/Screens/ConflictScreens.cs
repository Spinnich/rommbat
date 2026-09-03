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
/// tell which side matters. The console has refused to guess since M6 stage 1 and the couch
/// refuses for the same reason: a button that resolved twelve conflicts at once would be that
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
        // showing the list from before. Same shape as the sets list in 7b-2a.
        IReadOnlyList<SaveConflictRecord> open = service.Open();

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
    /// The slot is in the label because a game with four save slots produces four rows that are
    /// otherwise identical, and the two dates are the value because "which is newer" is the
    /// question a person actually arrives with.
    /// </remarks>
    private static ListRow ToRow(SaveConflictRecord conflict) =>
        new(
            $"Game {conflict.RomId}, slot {conflict.Slot}",
            Moment(conflict.FirstSeenAtUtc),
            conflict.Reason);

    /// <summary>
    /// One conflict: what each side is, and the two verbs.
    /// </summary>
    /// <remarks>
    /// <b>A pane of facts with two verbs on it, not a list of two choices.</b> Every row is
    /// something to read before deciding, so the cursor has nowhere to sit, and the sides are on
    /// <see cref="NavAction.Start"/> and <see cref="NavAction.Alternate"/> rather than on Accept:
    /// a screen that put one side on the button that also confirms would make the commonest
    /// mispress the destructive one.
    /// </remarks>
    public static IScreen Detail(
        InstallSession session,
        SaveConflictRecord conflict,
        Func<Uri, RomMConnection>? connect = null,
        Func<IScreen>? pair = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(conflict);

        return new ListScreen(
            $"Game {conflict.RomId}, slot {conflict.Slot}",
            () => DetailRows(conflict),
            _ => ScreenCommand.Stay,
            acceptLabel: string.Empty,
            backLabel: "Back",
            new FooterHint(NavAction.Start, "Keep this device's save"),
            new FooterHint(NavAction.Alternate, "Keep the server's save"))
        {
            Reading = true,
            Verbs = (action, _) => action switch
            {
                NavAction.Start => ScreenCommand.Push(
                    Confirm(session, conflict, ConflictResolution.KeepLocal, connect, pair)),

                NavAction.Alternate => ScreenCommand.Push(
                    Confirm(session, conflict, ConflictResolution.KeepServer, connect, pair)),

                _ => null,
            },
        };
    }

    /// <summary>What the detail screen shows about the two sides.</summary>
    private static List<ListRow> DetailRows(SaveConflictRecord conflict)
    {
        // Every row unavailable, because every row is a fact. An available row on a reading
        // pane makes the footer promise an Accept that does nothing, which is the same defect
        // as an action with no hint, pointed the other way.
        var rows = new List<ListRow>
        {
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
        // server's runs the same verified restore an ordinary download does, over a local file
        // that was copied aside when the conflict was first seen, and keeping this device's
        // leaves the server's previous copy in the slot's own history.
        rows.Add(new ListRow(
            "Either way",
            "nothing is deleted",
            "The side you do not keep stays: here as the copy above, and on RomM as an earlier "
                + "version of the save.",
            false));

        return rows;
    }

    /// <summary>
    /// The confirmation, then the work.
    /// </summary>
    /// <remarks>
    /// Confirmed on its own screen rather than on the press, because this is the one action in
    /// RomMBat whose two answers are both irreversible in the sense that matters: the file the
    /// emulator loads next time changes either way.
    /// </remarks>
    private static ListScreen Confirm(
        InstallSession session,
        SaveConflictRecord conflict,
        ConflictResolution resolution,
        Func<Uri, RomMConnection>? connect,
        Func<IScreen>? pair)
    {
        var keepingLocal = resolution == ConflictResolution.KeepLocal;

        return new ListScreen(
            keepingLocal ? "Keep this device's save?" : "Keep the server's save?",
            () =>
            [
                new ListRow(
                    keepingLocal ? "Sent to RomM" : "Fetched from RomM",
                    $"slot {conflict.Slot}",
                    keepingLocal
                        ? "The save on this device is uploaded and becomes the one every other "
                            + "device takes. RomM keeps what was there as an earlier version."
                        : "The server's save is downloaded and verified, replacing the file here. "
                            + "The copy taken when the conflict was first seen is kept.",
                    false),
            ],
            _ => ScreenCommand.Stay,
            acceptLabel: keepingLocal ? "Send this device's save" : "Fetch the server's save",
            backLabel: "Back")
        {
            Reading = true,
            AlwaysOfferAccept = true,
            Verbs = (action, _) => action == NavAction.Accept
                ? ScreenCommand.Push(Apply(session, conflict, resolution, connect, pair))
                : null,
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

            // A resolved conflict makes the confirmation, the detail screen and the list under
            // them all describe something that is no longer open, so leaving lands on the list,
            // which re-reads. An unresolved one leaves the same three screens correct, and the
            // person is most likely to want the other side, which is one press back.
            OnBack = () => outcome?.Resolved == true ? ScreenCommand.PopMany(3) : ScreenCommand.Pop,

            // Pairing is the only thing a person can do about a token the server has stopped
            // accepting, and a screen that reported the refusal without a route to it strands
            // them. Offered only when that is what happened.
            ExtraHints = () => outcome?.State == ConflictOutcomeState.Failed && pair is not null
                ? [new FooterHint(NavAction.Start, "Pair with RomM")]
                : [],

            Verbs = (action, _) => action == NavAction.Start
                && outcome?.State == ConflictOutcomeState.Failed
                && pair is { } start
                    ? ScreenCommand.Push(start())
                    : null,
        }.Started();
    }

    private static string Short(string? hash) =>
        hash is null ? "no hash" : hash[..Math.Min(8, hash.Length)];

    private static string Moment(DateTimeOffset? moment) =>
        moment is { } at
            ? at.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture)
            : "never";
}
