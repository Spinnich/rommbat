using System.Globalization;
using RomM.Client;
using RomMBat.Core;
using RomMBat.Core.Sets;
using RomMBat.Core.Store;
using RomMBat.UI.Input;
using RomMBat.UI.Shell;

namespace RomMBat.UI.Screens;

/// <summary>
/// One game, and the two things a person can do to it.
/// </summary>
/// <remarks>
/// <b>A <see cref="ListScreen"/> in reading mode</b>, because every row is a fact rather than a
/// choice and the verbs are the footer's. That mode exists for exactly this: an ordinary list
/// skips unavailable rows, so a screen of nothing but facts would not scroll at all.
/// <para>
/// <b>Every decision belongs to Core.</b> Whether this game can join a set is
/// <see cref="PickedSetService"/>'s answer, whether it can come off is
/// <see cref="EvictionService"/>'s, and what a removal costs is theirs too. What this file owns
/// is which words go on which row.
/// </para>
/// </remarks>
public static class BrowseScreens
{
    /// <summary>The game's detail screen, reached from a browse row.</summary>
    /// <param name="changed">
    /// Called once something has been installed or removed, so the page behind this screen stops
    /// saying what it said before.
    /// </param>
    public static IScreen Detail(
        InstallSession session,
        BrowseGame game,
        Func<Uri, RomMConnection>? connect = null,
        Action? changed = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(game);

        var picked = new PickedSetService(session);

        // Re-read on return, because installing and removing both happen on screens above this
        // one and used to leave the rows saying what they said before the press.
        IReadOnlyList<ListRow> Rows() => DetailRows(session, game);

        return new ListScreen(
            game.DisplayName,
            Rows,
            _ => ScreenCommand.Stay,
            acceptLabel: string.Empty,
            backLabel: "Back")
        {
            Reading = true,

            // Each verb offered exactly when it works, which the fixed pair got wrong offline:
            // the note said the game could not be installed from here and the footer went on
            // promising it, because there is no server row behind an offline page to pick from.
            // Found by the sweep, one screen over from the three a hands-on pass had just met.
            ExtraHints = () =>
            [
                .. game.Row is not null
                    ? new[] { new FooterHint(NavAction.Start, "Put this game on the device") }
                    : [],
                .. game.IsHere
                    ? new[] { new FooterHint(NavAction.Alternate, "Take it off this device") }
                    : [],
            ],
            Note = () => game.Row is null
                ? "This game is on the device. RomM is not reachable, so it cannot be installed "
                    + "again from here."
                : "Installing puts it on the device now. Taking it off never removes a save.",
            Verbs = (action, _) => action switch
            {
                // The pick writes the member row and the sync opens over the set it joined,
                // which is the shape SetEditorViewModel already uses for create-then-resolve
                // and the reason ReplaceThenOpen exists.
                NavAction.Start when game.Row is not null =>
                    Install(session, picked, game, connect, changed),

                NavAction.Alternate when game.IsHere =>
                    ScreenCommand.Push(ConfirmRemoval(session, game, connect, changed)),

                _ => null,
            },
        };
    }

    /// <summary>
    /// Picks the game and syncs it immediately, which is what one press has to mean.
    /// </summary>
    /// <remarks>
    /// <b>Not "added to a set, sync later".</b> Ruled with Spinnich: one press, game on disk.
    /// The set is created on the first pick and is ordinary in every other way.
    /// <para>
    /// A refusal is a screen rather than a silent no-op. An unmapped platform, a format the
    /// folder cannot launch and a multi-file ROM are all facts about the library that a person
    /// can act on in RomM, and a press that appeared to work and produced nothing is the worse
    /// half of every one of them.
    /// </para>
    /// </remarks>
    private static ScreenCommand Install(
        InstallSession session,
        PickedSetService picked,
        BrowseGame game,
        Func<Uri, RomMConnection>? connect,
        Action? changed)
    {
        var outcome = picked.Pick(game.Row!, DateTimeOffset.UtcNow);

        if (outcome.Member is null)
        {
            return ScreenCommand.Push(new MessageScreen(
                game.DisplayName,
                outcome.Problem ?? "This game cannot be put on this device."));
        }

        changed?.Invoke();

        // Replaces this screen with the set it joined and opens the install over it, so backing
        // out of the install lands on the set rather than skipping past what was just made.
        return ScreenCommand.ReplaceThenOpen(
            SetsScreens.Detail(session, outcome.Set.Name, connect),
            new SyncViewModel(session, outcome.Set, outcome.Member, connect));
    }

    /// <summary>
    /// What taking this one game off would do, before it does it.
    /// </summary>
    /// <remarks>
    /// The same Core path a set delete takes, given one id instead of a set's worth. The picked
    /// set is released so its own claim does not hold the game back against the person
    /// un-picking it; every other enabled set's claim still does, and says so.
    /// </remarks>
    internal static ListScreen ConfirmRemoval(
        InstallSession session,
        BrowseGame game,
        Func<Uri, RomMConnection>? connect,
        Action? changed)
    {
        var picked = new PickedSetService(session);
        var eviction = new EvictionService(session);

        EvictionReport? report = null;
        IReadOnlyList<string> unvouchable = [];

        return new ListScreen(
            $"Take '{game.DisplayName}' off?",
            () => RemovalRows(report, unvouchable),
            _ => ScreenCommand.Stay,
            acceptLabel: "Take it off this device",
            backLabel: "Keep it")
        {
            Reading = true,
            LoadingMessage = "Working out what can go...",

            // Accept, and only once the preview says something can go. A yes-or-no screen is
            // answered with the confirm button, and this one had the hint on Start with no gate:
            // the press walked through a second screen and removed nothing, where the preview
            // had already said the game would stay.
            OfferAcceptWhen = () => report is { } ready && ready.Plan.Selected.Count > 0,
            Load = token =>
            {
                var releasing = picked.Find() is { } set ? new[] { set.Id } : [];

                report = eviction.PreviewRemoval([game.RomId], releasing);
                unvouchable = eviction.Unvouchable([game.RomId]);

                token.ThrowIfCancellationRequested();
                return Task.FromResult<string?>(null);
            },
            Verbs = (action, _) => action switch
            {
                NavAction.Accept when report is { } ready && ready.Plan.Selected.Count > 0 =>
                    ScreenCommand.Push(ApplyRemoval(session, picked, game, ready, changed)),
                _ => null,
            },
        }.Started();
    }

    internal static ListScreen ApplyRemoval(
        InstallSession session,
        PickedSetService picked,
        BrowseGame game,
        EvictionReport report,
        Action? changed)
    {
        EvictionApplied? applied = null;

        return new ListScreen(
            $"Taking '{game.DisplayName}' off",
            () => applied is { } done
                ?
                [
                    new ListRow(
                        "Removed",
                        done.Evicted is { } evicted
                            ? $"{evicted.Removed} {(evicted.Removed == 1 ? "file set" : "file sets")}, "
                                + ByteSize.Format(evicted.BytesFreed)
                            : "nothing",
                        "Saves and save states were not touched. They are not files this can reach.",
                        false),
                    .. (done.Evicted?.Problems ?? []).Select(problem =>
                        new ListRow("Problem", null, problem, false)),
                ]
                : [],
            _ => ScreenCommand.Stay,
            acceptLabel: string.Empty,
            backLabel: "Done")
        {
            Reading = true,
            LoadingMessage = "Removing the game and rewriting the list EmulationStation reads...",
            Load = async token =>
            {
                applied = await new EvictionService(session)
                    .ApplyAsync(report, token)
                    .ConfigureAwait(false);

                // The pick goes whatever the files did. The user said take it off, and a pick
                // left behind would have the next sync fetch it again; a game another set still
                // wants stays on disk and that set goes on claiming it, which is what the
                // refusal above already told them.
                picked.Unpick(game.RomId, DateTimeOffset.UtcNow);

                changed?.Invoke();
                return null;
            },

            // Back closes this screen and the preview under it, landing on the game's detail,
            // which re-reads its rows. Popping one left the preview on the stack holding the
            // report from before the removal, still offering to take off a game that is
            // already gone. Same defect the set-side path fixed and this one reintroduced by
            // adding a screen above it.
            OnBack = () => ScreenCommand.PopMany(2),
        }.Started();
    }

    private static List<ListRow> DetailRows(InstallSession session, BrowseGame game)
    {
        var placement = session.Store.Files.PlacementFor([game.RomId])
            .GetValueOrDefault(game.RomId, new RomPlacement([], 0));

        var rows = new List<ListRow>
        {
            new("Platform", game.PlatformSlug, null, false),
            new(
                "Size in RomM",
                game.Row is null ? "not known offline" : ByteSize.Format(game.SizeBytes),
                null,
                false),
        };

        if (placement.IsHere)
        {
            rows.Add(new ListRow(
                placement.Folders.Count == 1 ? "In folder" : "In folders",
                string.Join(", ", placement.Folders),
                placement.Folders.Count > 1
                    // Stated rather than left as an unexplained number. Both sets are correct in
                    // EmulationStation and the bytes really are spent twice; what was wrong
                    // before was that nobody could see why.
                    ? "Two sync sets put this game in two folders, which is correct for both of "
                        + "them in EmulationStation. It takes the room twice."
                    : null,
                false));

            rows.Add(new ListRow(
                "Taking up",
                ByteSize.Format(placement.Bytes),
                "The game and its artwork together, in every folder it is in.",
                false));
        }
        else
        {
            rows.Add(new ListRow("On this device", "no", null, false));
        }

        rows.Add(new ListRow(
            "Wanted by",
            game.Sets.Count == 0 ? "no sync set" : string.Join(", ", game.Sets),
            game.Sets.Count == 0
                ? "Nothing is keeping this game here, so the next eviction may take it."
                : "Taking it off is refused while another of these still wants it.",
            false));

        if (game.Row is { } row)
        {
            rows.Add(new ListRow("Identifier", row.Id.ToString(CultureInfo.InvariantCulture), null, false));

            // Both describe the uncompressed content, so for an archive they are hashes of what
            // is inside it. Shown because a mismatch is the one thing a person can check against
            // RomM's own page.
            rows.Add(new ListRow("md5", row.Md5Hash ?? "none published", null, false));
            rows.Add(new ListRow("sha1", row.Sha1Hash ?? "none published", null, false));
        }

        return rows;
    }

    private static List<ListRow> RemovalRows(EvictionReport? report, IReadOnlyList<string> unvouchable)
    {
        if (report is not { } ready)
        {
            return [];
        }

        var rows = new List<ListRow>
        {
            new(
                ready.Plan.Selected.Count == 0 ? "It would stay" : "It goes",
                ready.Plan.Selected.Count == 0 ? null : ByteSize.Format(ready.Plan.BytesFreed),
                "Saves and save states are never removed. They live in different tables and "
                    + "nothing that removes content can reach them.",
                false),
        };

        rows.AddRange(ready.Plan.Selected.Select(candidate => new ListRow(
            candidate.File.Folder ?? candidate.File.FileName,
            ByteSize.Format(candidate.Bytes),
            $"goes, {EvictionService.Describe(candidate)}",
            false)));

        rows.AddRange(ready.Plan.Refused.Select(candidate => new ListRow(
            candidate.File.FileName,
            null,
            $"kept, because {candidate.Refusal}",
            false)));

        rows.AddRange(unvouchable.Select(container => new ListRow(
            container,
            null,
            "This save belongs to no one game, so RomMBat cannot say whether removing this game "
                + "costs anything in it. It is left where it is.",
            false)));

        return rows;
    }
}
