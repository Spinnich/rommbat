using RomMBat.Core;
using RomMBat.Core.Mapping;
using RomMBat.Core.Sets;
using RomMBat.Core.Store;
using RomMBat.UI.Input;
using RomMBat.UI.Shell;

namespace RomMBat.UI.Screens;

/// <summary>
/// Where each RomM platform's games land in RetroBat, and how to change it.
/// </summary>
/// <remarks>
/// <b>M2 calls for this as core UI and it was the last piece of M2 with no face.</b> Until stage
/// 7b-3 an unmapped platform was found out by a resolve stopping partway through a collection
/// that happened to hold one of its games, and the only repair reachable from the couch was a
/// per-set folder override. That is the wrong shape: the mapping is install-wide,
/// <c>platform_map</c> already holds it install-wide, and a per-set override fixes one set while
/// leaving every other set and every future set with the same hole. 7b-2c made it worse by
/// showing unmapped platforms in a second place, on a browse row saying the games cannot be
/// installed, still with no repair.
/// <para>
/// <b>Reached from the root before a sync is attempted, not discovered after one fails.</b> The
/// root menu carries the unmapped count on its own row for that reason.
/// </para>
/// <para>
/// <b>Unmapped is a normal state, not an error.</b> A RomM platform with no RetroBat folder is
/// one of M2's two first-class unmapped states, and arcade reaches it by design because which of
/// the ten folders is right depends on the romset the files came from. So a row with no folder
/// is shown plainly with what to do about it, not as a fault.
/// </para>
/// </remarks>
public static class PlatformScreens
{
    /// <summary>Every platform this install has heard of, and where it maps.</summary>
    /// <remarks>
    /// <b>Unmapped first.</b> The list is otherwise alphabetical, which on a real 123-platform
    /// library puts the three rows a person came here to fix wherever their names happen to
    /// fall. Sorting the actionable rows to the top is what makes the count on the root row
    /// reachable in one press rather than in thirty of scrolling.
    /// <para>
    /// <b>No connection, and it is not a degradation.</b> <c>platform_map</c> is written by every
    /// resolve and every browse, so the whole screen including the repair answers from the store
    /// with the server switched off. The agent's <c>platforms list</c> refreshes first because it
    /// can; here the rows a person came to fix are already local, and a screen that waited on an
    /// unreachable LAN host to show them would be trading the working state for nothing.
    /// </para>
    /// </remarks>
    public static IScreen List(InstallSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        // Re-read rather than captured, because mapping one above this screen leaves the list
        // showing the folders from before.
        IReadOnlyList<PlatformMapRow> platforms = [];

        IReadOnlyList<ListRow> Rows()
        {
            platforms =
            [
                .. session.Store.PlatformMap.List()
                    .OrderBy(row => row.Folder is not null)
                    .ThenBy(row => row.Label, StringComparer.CurrentCultureIgnoreCase),
            ];

            return [.. platforms.Select(ToRow)];
        }

        return new ListScreen(
            "Platforms",
            Rows,
            index => ScreenCommand.Push(Detail(session, platforms[index])),
            acceptLabel: "Change where these land",
            backLabel: "Back")
        {
            EmptyMessage = "No platforms known yet. Sync or query a set once, or open a game in "
                + "browse, and they appear.",
            Note = () => Note(platforms),
        };
    }

    /// <summary>The line above the rows, which is the count the root row promised.</summary>
    private static string? Note(IReadOnlyList<PlatformMapRow> platforms)
    {
        var unmapped = platforms.Count(row => row.Folder is null);

        return unmapped == 0
            ? null
            : $"{unmapped} of {platforms.Count} have no folder. Their games are skipped by a sync "
                + "until one is chosen.";
    }

    /// <summary>One platform as the list shows it.</summary>
    /// <remarks>
    /// The folder is the value because it is the answer the screen exists to give, and where the
    /// answer came from is the detail line: a folder somebody chose and a folder a bundled table
    /// guessed are worth different amounts of trust, which is the whole reason
    /// <c>platform_map.resolved_by</c> is a column.
    /// </remarks>
    private static ListRow ToRow(PlatformMapRow platform) =>
        new(
            platform.Label,
            platform.Folder ?? "no folder",
            platform.Folder is null
                ? platform.SuggestedFolder is { } suggested
                    ? $"'{suggested}' looks like a match, and is waiting for you to say so."
                    : "Nothing here matches this platform. Choose a folder, or leave it."
                : Describe(platform.ResolvedBy));

    /// <summary>Where a folder came from, as a person would say it.</summary>
    /// <remarks>
    /// The enum's own names reach the screen as identifiers rather than English, which is the
    /// same defect the controller availability row had in 7b-1.
    /// </remarks>
    private static string Describe(MappingSource source) => source switch
    {
        MappingSource.User => "You chose this.",
        MappingSource.FsSlug => "RomM's own folder name for this platform is a folder this install has.",
        MappingSource.Bundled => "From RomMBat's bundled table of platform names.",
        MappingSource.Normalized => "A name match, offered rather than applied.",
        MappingSource.Unmapped => "Nothing matched.",
        _ => source.ToString(),
    };

    /// <summary>One platform: where it lands, and the two things that can be done about it.</summary>
    public static IScreen Detail(InstallSession session, PlatformMapRow platform)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(platform);

        // Re-read on every draw, because choosing a folder above this screen changes what it
        // says without this screen being pressed.
        PlatformMapRow Current() => session.Store.PlatformMap.Find(platform.FsSlug) ?? platform;

        return new ListScreen(
            platform.Label,
            () => DetailRows(Current()),
            _ => ScreenCommand.Stay,
            acceptLabel: string.Empty,
            backLabel: "Back")
        {
            Reading = true,

            // Both verbs here, because ExtraHints replaces the constructor's hints rather than
            // adding to them: a Start hint passed there and an ExtraHints that answered only
            // Alternate left the first verb working with nothing in the footer naming it, which
            // is the same defect three screens got three different ways in 7b-2c.
            //
            // The second verb only exists for a row somebody chose. Offering "use the automatic
            // one" on a row that is already the automatic one is a press that does nothing.
            ExtraHints = () => Current().IsUserChoice
                ?
                [
                    new FooterHint(NavAction.Start, "Choose a folder"),
                    new FooterHint(NavAction.Alternate, "Use the automatic choice"),
                ]
                : [new FooterHint(NavAction.Start, "Choose a folder")],

            Verbs = (action, _) => action switch
            {
                NavAction.Start => ScreenCommand.Push(FolderPicker(session, Current())),

                NavAction.Alternate when Current().IsUserChoice =>
                    ScreenCommand.Push(ClearConfirm(session, Current())),

                _ => null,
            },
        };
    }

    private static List<ListRow> DetailRows(PlatformMapRow platform)
    {
        // Unavailable, because every row is a fact rather than a choice. The verbs are on
        // Start and Alternate, and an available row here would put an Accept in the footer that
        // does nothing.
        var rows = new List<ListRow>
        {
            new("Folder", platform.Folder ?? "none", Describe(platform.ResolvedBy), false),
            new(
                "RomM calls it",
                platform.FsSlug,
                "Its folder name on the server, which is what identifies it here.",
                false),
        };

        if (platform.SuggestedFolder is { } suggested && platform.Folder is null)
        {
            rows.Add(new ListRow(
                "Suggested",
                suggested,
                "A name match. It is never applied on its own, because a wrong folder puts games "
                    + "somewhere EmulationStation will not look for them.",
                false));
        }

        if (platform.Explanation is { } explanation)
        {
            rows.Add(new ListRow("Why", explanation, null, false));
        }

        if (platform.CandidateFolders.Count > 1)
        {
            rows.Add(new ListRow(
                "Also possible",
                string.Join(", ", platform.CandidateFolders),
                "This platform's name maps to several RetroBat folders, and which is right "
                    + "depends on the files. Arcade is the usual case.",
                false));
        }

        // Said here because it is the consequence a person is deciding about, and it is not
        // obvious: changing a folder does not move what is already on disk.
        rows.Add(new ListRow(
            "Changing it",
            "affects the next sync",
            "Games already downloaded stay in the folder they went to. A sync after the change "
                + "puts new games in the new folder.",
            false));

        return rows;
    }

    /// <summary>Every folder this install actually has, read live.</summary>
    /// <remarks>
    /// From <c>es_systems.cfg</c> through <see cref="SyncSetService.FoldersKnownHere"/>, because
    /// RetroBat is the authority on which systems exist and a bundled list goes stale every
    /// release. Same source the set editor's folder override picker uses.
    /// </remarks>
    private static ListScreen FolderPicker(InstallSession session, PlatformMapRow platform)
    {
        var folders = new SyncSetService(session).FoldersKnownHere();

        // The suggestion first when there is one, because accepting it is the commonest answer
        // and a person should not have to find it among a hundred alphabetical rows.
        var ordered = platform.SuggestedFolder is { } suggested && folders.Contains(suggested)
            ? (IReadOnlyList<string>)[suggested, .. folders.Where(folder => folder != suggested)]
            : folders;

        return new ListScreen(
            $"Where do {platform.Label} games go?",
            [
                .. ordered.Select(folder => new ListRow(
                    folder,
                    folder == platform.Folder ? "current" : null,
                    folder == platform.SuggestedFolder ? "The suggested match." : null)),
            ],
            index =>
            {
                session.Store.PlatformMap.SetOverride(
                    platform.FsSlug,
                    ordered[index],
                    DateTimeOffset.UtcNow,
                    platform.Slug,
                    platform.PlatformId);

                // Back onto the detail screen, which re-reads and shows the new folder.
                return ScreenCommand.Pop;
            },
            acceptLabel: "Put them here",
            backLabel: "Back")
        {
            EmptyMessage = "This install has no systems in es_systems.cfg, which RomMBat reads "
                + "from the live tree rather than from a bundled list.",
        };
    }

    /// <summary>Dropping a choice so the automatic chain answers again.</summary>
    private static ListScreen ClearConfirm(InstallSession session, PlatformMapRow platform)
    {
        var cleared = false;

        return new ListScreen(
            $"Stop choosing for {platform.Label}?",
            () =>
            [
                cleared
                    ? new ListRow(
                        "Done",
                        null,
                        "The next time RomMBat resolves this platform it works the folder out "
                            + "again. Until then it has none.",
                        false)
                    : new ListRow(
                        "Your choice is dropped",
                        platform.Folder ?? "none",
                        "RomMBat works the folder out again from RomM's own name and its bundled "
                            + "table. Games already downloaded stay where they are.",
                        false),
            ],
            _ => ScreenCommand.Stay,
            acceptLabel: cleared ? string.Empty : "Drop it",
            backLabel: "Back")
        {
            Reading = true,
            OfferAcceptWhen = () => !cleared,

            Verbs = (action, _) =>
            {
                if (action != NavAction.Accept || cleared)
                {
                    return null;
                }

                session.Store.PlatformMap.ClearOverride(platform.FsSlug, DateTimeOffset.UtcNow);
                cleared = true;
                return ScreenCommand.Stay;
            },
        };
    }
}
