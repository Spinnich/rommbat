using System.Globalization;
using RomM.Client;
using RomM.Client.Catalog;
using RomMBat.Core;
using RomMBat.Core.Content;
using RomMBat.Core.Sets;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
using RomMBat.UI.Input;
using RomMBat.UI.Shell;

namespace RomMBat.UI.Screens;

/// <summary>
/// The sets a person can define from the couch.
/// </summary>
/// <remarks>
/// <b>Every decision here belongs to <see cref="SyncSetService"/>.</b> Which scopes exist,
/// which of them this pairing can use, whether a folder is real, whether a slug resolves: all
/// asked rather than worked out. What this file owns is what a row says and which screen a
/// press opens.
/// <para>
/// <b>Everything except resolving works with the server switched off.</b> The service never
/// touches the network to list, define, edit or remove, so this whole surface is answerable on
/// a handheld away from its server. Only <see cref="SetsScreens.Resolve"/> needs a connection,
/// and it is the only screen that says so.
/// </para>
/// <para>
/// <b>Nothing here names a button.</b> Every footer label is what the action does, and the
/// renderer draws the position. Round 8 of stage 7b-1 found "Press A" in a status row, which on
/// a Switch Pro is <c>es_input.cfg</c>'s <c>b</c>, which closes RomMBat.
/// </para>
/// </remarks>
public static class SetsScreens
{
    /// <summary>The list of sets, which is where this flow starts.</summary>
    /// <param name="pair">
    /// Where pairing starts, for a sync that the server refuses part way through. Null leaves
    /// the offer off rather than opening a blank screen.
    /// </param>
    public static IScreen List(
        InstallSession session,
        Func<Uri, RomMConnection>? connect = null,
        Func<IScreen>? pair = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        // Re-read rather than captured. A set created from the editor above this screen left it
        // showing the sets from before, and it corrected itself only on leaving and returning.
        var service = new SyncSetService(session);
        IReadOnlyList<SetSummary> sets = service.List();

        IReadOnlyList<ListRow> Rows()
        {
            sets = service.List();
            return [.. sets.Select(ToRow)];
        }

        return new ListScreen(
            "Sync sets",
            Rows,
            index => ScreenCommand.Push(Detail(session, sets[index].Set.Name, connect, pair)),
            acceptLabel: "Open",
            backLabel: "Back",
            new FooterHint(NavAction.Start, "New set"),
            new FooterHint(NavAction.Alternate, "Sync everything"),
            new FooterHint(NavAction.Extra, "Query every set"))
        {
            EmptyMessage = "No sync sets yet. A set is what this device keeps: a platform, a "
                + "collection, or a search. How much room they may use together is set under "
                + "disk space.",
            Verbs = (action, _) => action switch
            {
                NavAction.Start => ScreenCommand.Push(SetEditorViewModel.ForNew(session, connect)),

                // Syncing is what the sets are for, so it is the first-tier verb here and
                // resolving moves to the second. A sync re-resolves every set on the way past
                // anyway, so the two are not a choice a person has to make: resolving alone is
                // for finding out what a set holds without spending disk on it.
                NavAction.Alternate when sets.Count > 0 =>
                    ScreenCommand.Push(Sync(session, [.. sets.Select(summary => summary.Set)], connect, pair)),

                // Every set at once, because doing them one at a time is the hassle a person
                // notices first. SetResolveService already walks a list; nothing new is needed
                // except somewhere to press.
                NavAction.Extra when sets.Count > 0 =>
                    ScreenCommand.Push(Resolve(session, [.. sets.Select(summary => summary.Set)], connect)),

                _ => null,
            },
        };
    }

    /// <summary>
    /// One set as the list shows it.
    /// </summary>
    /// <remarks>
    /// The caps are named only when there are any. Every set made from the interface has none,
    /// so quoting the policy on every row spent a third of the line saying "no game cap, no size
    /// cap" about sets that never had one.
    /// </remarks>
    private static ListRow ToRow(SetSummary summary)
    {
        var capped = summary.Set.MaxGames is not null || summary.Set.MaxBytes is not null;

        return new ListRow(
            summary.Set.Name,
            summary.OnDiskBytes > 0
                ? $"{summary.Games} games, {ByteSize.Format(summary.OnDiskBytes)} here"
                : $"{summary.Games} games, {ByteSize.Format(summary.Bytes)}",
            $"{SyncSetStore.ScopeText(summary.Set.Scope)}; "
                + (capped ? $"{summary.Policy}; " : string.Empty)
                + $"last resolved {Moment(summary.Set.LastResolvedAt)}");
    }

    /// <summary>One set: what it holds, and the three things that can be done to it.</summary>
    public static IScreen Detail(
        InstallSession session,
        string name,
        Func<Uri, RomMConnection>? connect,
        Func<IScreen>? pair = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        var service = new SyncSetService(session);
        var detail = service.Show(name);

        if (detail is null)
        {
            return new MessageScreen("Sync sets", $"There is no set named '{name}' any more.");
        }

        // Re-read on return, because resolving happens on a screen above this one and used to
        // leave the counts and the last-resolved time showing what they said before it ran.
        IReadOnlyList<ListRow> Rows()
        {
            detail = service.Show(name) ?? detail;
            return DetailRows(session, detail);
        }

        // The only thing left to edit is the folder, and most sets do not have one. Offering
        // "Edit" on a screen where it opens an empty form is a footer promising nothing, which
        // is the same defect as a footer promising nothing where an action exists.
        var editable = SetEditorViewModel.ForExisting(session, detail.Set).NeedsFolderChoice;

        return new ListScreen(
            detail.Set.Name,
            Rows,
            _ => ScreenCommand.Stay,
            acceptLabel: "Change folder",
            backLabel: "Back",
            new FooterHint(NavAction.Start, "Sync now"),
            new FooterHint(NavAction.Extra, "Query this set"),
            new FooterHint(NavAction.Alternate, "Delete set"))
        {
            // Every row here is a fact rather than a choice, so the cursor has nowhere to sit
            // and the accept hint was suppressed while Verbs went on handling the press. The
            // edit worked and the footer never said so.
            AlwaysOfferAccept = editable,
            Note = () => "Syncing puts this set on the device. Querying only asks RomM what is "
                + "in it, and downloads nothing. Both need the network.",
            Verbs = (action, _) => action switch
            {
                NavAction.Accept when editable =>
                    ScreenCommand.Push(SetEditorViewModel.ForExisting(session, detail!.Set, connect)),
                NavAction.Start => ScreenCommand.Push(Sync(session, [detail!.Set], connect, pair)),
                NavAction.Extra => ScreenCommand.Push(Resolve(session, [detail!.Set], connect)),
                NavAction.Alternate => ScreenCommand.Push(ConfirmDelete(session, detail!.Set.Name, connect)),
                _ => null,
            },
        };
    }

    private static List<ListRow> DetailRows(InstallSession session, SetDetail detail)
    {
        var rows = new List<ListRow>
        {
            new("Scope", ScopeValue(session, detail.Set), null, false),
            new(
                "Holds",
                $"{detail.Games} games, {ByteSize.Format(detail.Bytes)}",
                "What RomM says these games weigh. Artwork has no size until it is fetched.",
                false),
            new(
                "On this device",
                ByteSize.Format(detail.OnDiskBytes),
                "Everything this set has put here, artwork included.",
                false),
            new("Last resolved", Moment(detail.Set.LastResolvedAt), detail.Set.LastResolutionSummary, false),
        };

        // Only when there is one. Every set made from the interface has no caps now, so the row
        // said "no game cap, no size cap" on every one of them, which is a line of noise that
        // outlived the feature it described. A set given caps from the console still shows them.
        if (detail.Set.MaxGames is not null || detail.Set.MaxBytes is not null)
        {
            rows.Insert(1, new ListRow("Limits", detail.Policy, null, false));
        }

        // Shown rather than hidden, so a user can see in RomM what to fix. An exclusion is a
        // fact about the last resolution, not something on disk.
        rows.AddRange(detail.Exclusions.Select(exclusion => new ListRow(
            "Skipped",
            exclusion.Count.ToString(CultureInfo.CurrentCulture),
            SyncSetService.Describe(exclusion.State) + Formats(exclusion),
            false)));

        if (detail.Departed.Count > 0)
        {
            rows.Add(new ListRow(
                "Left the set",
                detail.Departed.Count.ToString(CultureInfo.CurrentCulture),
                "Still on disk. Nothing is removed when a game leaves a set.",
                false));
        }

        return rows;
    }

    /// <summary>
    /// Deleting, behind one confirmation, saying what each answer does before the press.
    /// </summary>
    /// <remarks>
    /// <b>Two answers, because deleting a set and keeping its games is a legitimate thing to
    /// want.</b> Removing is a choice here rather than an automatic consequence, which is #110's
    /// own rule.
    /// <para>
    /// <b>This row used to say "Nothing on disk is touched and no game is removed", and that
    /// stopped being true in this branch.</b> It was the right answer while eviction was the
    /// thing that removed content; 7b-2b took eviction off the interface on the ruling that
    /// freeing space belongs to the user, and dropping a set is the user saying which games they
    /// no longer want.
    /// </para>
    /// <para>
    /// The sentence matters more than the confirmation, and it belongs before the press. A
    /// person on a sofa has no other way to find out what they are about to lose, and moving it
    /// after the press was a defect fixed in 7b-2a.
    /// </para>
    /// </remarks>
    public static IScreen ConfirmDelete(
        InstallSession session,
        string name,
        Func<Uri, RomMConnection>? connect = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new ListScreen(
            $"Delete '{name}'?",
            [
                new ListRow(
                    "Delete it and take its games off this device",
                    null,
                    "Shows what would go before anything goes. Saves and save states are never "
                        + "removed, and a game another set still wants is kept."),
                new ListRow(
                    "Delete it and leave the games where they are",
                    null,
                    "The set is forgotten. Nothing on disk is touched and no game is removed."),
            ],
            index =>
            {
                if (index == 0)
                {
                    return ScreenCommand.Push(ConfirmRemoval(session, name, connect));
                }

                new SyncSetService(session).Remove(name);

                // Back to the list, closing the detail screen underneath, whose set no longer
                // exists. It used to land on a message screen instead, which said the right
                // sentence at the wrong moment and left the only way onward being to quit
                // RomMBat entirely.
                return ScreenCommand.PopMany(2);
            },
            acceptLabel: "Choose",
            backLabel: "Keep it");
    }

    /// <summary>
    /// What deleting a set would take off the device, before it takes anything.
    /// </summary>
    /// <remarks>
    /// <b>The preview is the screen rather than a flag.</b> <c>sync</c>'s <c>--dry-run</c> names
    /// one command's flag; here the preview is simply what the user is looking at, and the
    /// footer is what commits.
    /// <para>
    /// <b>The flush runs first, inside the load.</b> The commonest <see cref="SaveGuard"/>
    /// refusal is a save that has not reached the server, and flushing resolves it rather than
    /// blocking the removal. Offline it is skipped and said so, because offline is a working
    /// state: an unsent save then holds its game back, which is the correct answer.
    /// </para>
    /// <para>
    /// A <c>ListScreen</c> with a loader rather than a screen kind of its own. The work is two
    /// scans and a plan, measured in seconds on a real install, and doing it on the drawing
    /// thread is what made an earlier eviction preview freeze for four seconds with nothing on
    /// screen saying why.
    /// </para>
    /// </remarks>
    public static IScreen ConfirmRemoval(
        InstallSession session,
        string name,
        Func<Uri, RomMConnection>? connect = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        var service = new SyncSetService(session);
        var set = service.Show(name);

        if (set is null)
        {
            return new MessageScreen("Sync sets", $"There is no set named '{name}' any more.");
        }

        var eviction = new EvictionService(session);
        var romIds = set.Members.Select(member => member.RomId).ToList();

        EvictionReport? report = null;
        IReadOnlyList<string> unvouchable = [];
        string? flushNote = null;

        return new ListScreen(
            $"Remove the games in '{name}'?",
            () => RemovalRows(report, unvouchable, flushNote),
            _ => ScreenCommand.Stay,
            acceptLabel: string.Empty,
            backLabel: "Keep them")
        {
            Reading = true,
            LoadingMessage = "Sending saves, then working out what can go.",
            Load = async token =>
            {
                flushNote = await FlushBeforeRemovalAsync(session, connect, token).ConfigureAwait(false);

                report = eviction.PreviewRemoval(romIds, releasing: [set.Set.Id]);
                unvouchable = eviction.Unvouchable(romIds);

                return null;
            },
            Verbs = (action, _) => action switch
            {
                NavAction.Start when report is { } ready && ready.Plan.Selected.Count > 0 =>
                    ScreenCommand.Push(ApplyRemoval(session, name, ready)),
                _ => null,
            },
        }.Started();
    }

    /// <summary>Carrying out a removal, and deleting the set once its files have gone.</summary>
    /// <remarks>
    /// <b>Internal so the string sweeps can reach it.</b> Every other screen on this surface is
    /// constructible from a name, and one that is only reachable by driving a preview to
    /// completion would be the one screen no sweep covers, which is how "a rule enforced in one
    /// place and broken in the place beside it" keeps happening here.
    /// <para>
    /// <b>Files first, then the definition.</b> Either order self-heals and this one is the
    /// honest half: a definition removed ahead of a delete that then failed would leave games on
    /// disk that no set claims, where a file removed ahead of a definition that survived leaves
    /// a set the next sync simply fetches again. Nothing ever claims to have removed something
    /// still on the disk.
    /// </para>
    /// </remarks>
    internal static IScreen ApplyRemoval(InstallSession session, string name, EvictionReport report)
    {
        EvictionApplied? applied = null;

        return new ListScreen(
            $"Removing the games in '{name}'",
            () => applied is { } done ? AppliedRows(done) : [],
            _ => ScreenCommand.Stay,
            acceptLabel: string.Empty,
            backLabel: "Done")
        {
            Reading = true,
            LoadingMessage = "Removing games and rewriting the lists EmulationStation reads.",
            Load = async token =>
            {
                applied = await new EvictionService(session).ApplyAsync(report, token).ConfigureAwait(false);
                new SyncSetService(session).Remove(name);
                return null;
            },
        }.Started();
    }

    /// <summary>
    /// Flushes saves before a removal, or says why it could not.
    /// </summary>
    /// <remarks>
    /// A refusal to take the tree lock and an unreachable server are both ordinary outcomes and
    /// come back as a sentence, which is what keeps this file from ever naming <c>TreeLock</c>.
    /// </remarks>
    private static async Task<string?> FlushBeforeRemovalAsync(
        InstallSession session,
        Func<Uri, RomMConnection>? connect,
        CancellationToken cancellationToken)
    {
        var attempt = session.Authenticate();

        if (attempt.Connection is null)
        {
            return "Saves were not sent, because this device is not signed in to RomM. A game "
                + "whose save has not reached the server is kept.";
        }

        var origin = session.Store.Device.Read()?.ServerOrigin;
        var connection = connect is not null && origin is not null ? connect(origin) : attempt.Connection;

        if (!ReferenceEquals(connection, attempt.Connection))
        {
            attempt.Connection.Dispose();
        }

        try
        {
            var flushed = await new SaveFlushService(session)
                .RunAsync(new FlushOptions(), connection, cancellationToken)
                .ConfigureAwait(false);

            return flushed.State == FlushState.Skipped
                ? "Saves were left to the pass already running, so a game whose save has not "
                    + "reached the server is kept."
                : null;
        }
        catch (RomMUnreachableException ex)
        {
            return $"Saves were not sent ({ex.Message}). A game whose save has not reached the "
                + "server is kept.";
        }
        finally
        {
            connection.Dispose();
        }
    }

    /// <summary>What the preview shows: what goes, what stays, and what cannot be vouched for.</summary>
    private static List<ListRow> RemovalRows(
        EvictionReport? report,
        IReadOnlyList<string> unvouchable,
        string? flushNote)
    {
        if (report is not { } ready)
        {
            return [];
        }

        var rows = new List<ListRow>
        {
            new(
                ready.Plan.Selected.Count == 0
                    ? "Nothing would be removed"
                    : $"{ready.Plan.Selected.Count} "
                        + $"{(ready.Plan.Selected.Count == 1 ? "game goes" : "games go")}",
                ByteSize.Format(ready.Plan.BytesFreed),
                "Saves and save states are never removed. They are not files this can reach.",
                false),
        };

        if (flushNote is { } note)
        {
            rows.Add(new ListRow("Saves", null, note, false));
        }

        // Quoted from Core rather than reworded here, so the console and the couch give the
        // same reason for the same candidate.
        rows.AddRange(ready.Plan.Selected.Select(candidate => new ListRow(
            candidate.File.FileName,
            ByteSize.Format(candidate.Bytes),
            $"goes, {EvictionService.Describe(candidate)}",
            false)));

        rows.AddRange(ready.Plan.Refused.Select(candidate => new ListRow(
            candidate.File.FileName,
            null,
            $"kept, because {candidate.Refusal}",
            false)));

        // Named, and no safety claimed. A shared container has no rom_id, so nothing can say
        // which game its bytes belong to once the ROM is gone.
        rows.AddRange(unvouchable.Select(container => new ListRow(
            container,
            null,
            "This save belongs to no one game, so RomMBat cannot say whether removing these "
                + "games costs anything in it. It is left where it is.",
            false)));

        return rows;
    }

    private static List<ListRow> AppliedRows(EvictionApplied applied)
    {
        var rows = new List<ListRow>
        {
            new(
                "Removed",
                applied.Evicted is { } evicted
                    ? $"{evicted.Removed} {(evicted.Removed == 1 ? "game" : "games")}, "
                        + ByteSize.Format(evicted.BytesFreed)
                    : "nothing",
                "The set is gone. Saves and save states were not touched.",
                false),
        };

        rows.AddRange((applied.Evicted?.Problems ?? []).Select(problem =>
            new ListRow("Problem", null, problem, false)));

        return rows;
    }

    /// <summary>Syncing one or more sets, which is what puts games on the device.</summary>
    public static IScreen Sync(
        InstallSession session,
        IReadOnlyList<SyncSetDefinition> sets,
        Func<Uri, RomMConnection>? connect,
        Func<IScreen>? pair = null) =>
        new SyncViewModel(session, sets, connect, pair);

    /// <summary>Resolving one or more sets, which asks RomM what they contain and fetches nothing.</summary>
    public static IScreen Resolve(
        InstallSession session,
        IReadOnlyList<SyncSetDefinition> sets,
        Func<Uri, RomMConnection>? connect) =>
        new ResolveViewModel(session, sets, connect);

    /// <summary>The scope, with a platform's id shown as the name a person recognises.</summary>
    private static string ScopeValue(InstallSession session, SyncSetDefinition set)
    {
        var text = SyncSetStore.ScopeText(set.Scope);

        if (set.Scope != CatalogScopeKind.Platform)
        {
            return text;
        }

        var platform = new SyncSetService(session).PlatformsKnownHere()
            .FirstOrDefault(option =>
                option.PlatformId.ToString(CultureInfo.InvariantCulture) == set.ScopeValue);

        return platform is null ? $"{text} {set.ScopeValue}" : $"{text}: {platform.Label}";
    }

    private static string Formats(ExclusionSummary exclusion) =>
        exclusion.State == MemberState.ExcludedExtension && exclusion.Extensions.Count > 0
            ? $" ({string.Join(", ", exclusion.Extensions.Select(extension => "." + extension))})"
            : string.Empty;

    /// <summary>A stored instant as the clock on the wall in front of the user.</summary>
    /// <remarks>
    /// Local, not UTC, for the same reason the status screen converts: everything is stored and
    /// compared in UTC, which is what makes the outbox survive a timezone change, and none of
    /// that is the user's problem.
    /// </remarks>
    internal static string Moment(DateTimeOffset? at) =>
        at is { } moment
            ? moment.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture)
            : "never";
}
