using System.Globalization;
using RomM.Client;
using RomM.Client.Catalog;
using RomMBat.Core;
using RomMBat.Core.Sets;
using RomMBat.Core.Store;
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
            new FooterHint(NavAction.Extra, "Resolve everything"))
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
            $"{summary.Games} games, {ByteSize.Format(summary.Bytes)}",
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
            new FooterHint(NavAction.Extra, "Resolve now"),
            new FooterHint(NavAction.Alternate, "Delete set"))
        {
            // Every row here is a fact rather than a choice, so the cursor has nowhere to sit
            // and the accept hint was suppressed while Verbs went on handling the press. The
            // edit worked and the footer never said so.
            AlwaysOfferAccept = editable,
            Note = () => "Syncing pulls this set onto the device. Resolving only asks RomM what "
                + "it contains now. Both need the network.",
            Verbs = (action, _) => action switch
            {
                NavAction.Accept when editable =>
                    ScreenCommand.Push(SetEditorViewModel.ForExisting(session, detail!.Set, connect)),
                NavAction.Start => ScreenCommand.Push(Sync(session, [detail!.Set], connect, pair)),
                NavAction.Extra => ScreenCommand.Push(Resolve(session, [detail!.Set], connect)),
                NavAction.Alternate => ScreenCommand.Push(ConfirmDelete(session, detail!.Set.Name)),
                _ => null,
            },
        };
    }

    private static List<ListRow> DetailRows(InstallSession session, SetDetail detail)
    {
        var rows = new List<ListRow>
        {
            new("Scope", ScopeValue(session, detail.Set), null, false),
            new("Holds", $"{detail.Games} games, {ByteSize.Format(detail.Bytes)}", null, false),
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
                "Eviction candidates, not deletions. Nothing has been removed.",
                false));
        }

        return rows;
    }

    /// <summary>
    /// Deleting, behind one confirmation, saying what it does and does not touch.
    /// </summary>
    /// <remarks>
    /// The sentence matters more than the confirmation. <c>sets remove</c> has always said
    /// "nothing on disk was touched", and a person deleting a set from a couch has no other way
    /// to find out that their games are still there.
    /// </remarks>
    public static IScreen ConfirmDelete(InstallSession session, string name) =>
        new ListScreen(
            $"Delete '{name}'?",
            [
                new ListRow(
                    "Delete this set",
                    null,
                    "The set is forgotten. Nothing on disk is touched and no game is removed."),
            ],
            _ =>
            {
                new SyncSetService(session).Remove(name);

                // Back to the list, closing the detail screen underneath, whose set no longer
                // exists. It used to land on a message screen instead, which said the right
                // sentence at the wrong moment and left the only way onward being to quit
                // RomMBat entirely. The sentence belongs on the confirmation below, before the
                // press rather than after it, which is where a warning is any use.
                return ScreenCommand.PopMany(2);
            },
            acceptLabel: "Delete",
            backLabel: "Keep it");

    /// <summary>Syncing one or more sets, which is what puts games on the device.</summary>
    public static IScreen Sync(
        InstallSession session,
        IReadOnlyList<SyncSetDefinition> sets,
        Func<Uri, RomMConnection>? connect,
        Func<IScreen>? pair = null) =>
        new SyncViewModel(session, sets, connect, pair, () => EvictionScreens.Preview(session));

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
