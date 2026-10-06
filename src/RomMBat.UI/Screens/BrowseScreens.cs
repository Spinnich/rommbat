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
/// <b>Every decision belongs to Core.</b> Whether this game can join a set, whether it can come
/// off and what a removal costs are <see cref="GameService"/>'s answers, which
/// <c>rommbat-agent game</c> prints too. What this file owns
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

        // Re-read on return, because installing and removing both happen on screens above this
        // one, and rows read once would go on saying what they said before the press.
        IReadOnlyList<ListRow> Rows() => DetailRows(session, game);

        return new ListScreen(
            game.DisplayName,
            Rows,
            _ => ScreenCommand.Stay,
            acceptLabel: "Put this game on the device",
            backLabel: "Back")
        {
            Reading = true,

            // The bottom button installs, as it launches a game in EmulationStation: it is the
            // one thing a person opening a game here most likely came to do. Offered exactly when
            // it works, which a fixed hint got wrong offline: there is no server row behind an
            // offline page to pick from.
            OfferAcceptWhen = () => game.Row is not null,

            // Taking it off has no shortcut, as no destructive verb does. The memory card is
            // offered only where the shape allows it, which SaveConverter answers.
            ActionList = () =>
            [
                .. game.Row is not null
                    ? new[] { new ScreenAction("Put this game on the device", () => Install(session, game, connect, changed)) }
                    : [],
                .. game.IsHere
                    ? new[] { new ScreenAction("Take it off this device", () => ScreenCommand.Push(ConfirmRemoval(session, game, connect, changed))) }
                    : [],
                .. QueuedChangeScreens.CanConvert(session, game.RomId)
                    ? new[] { new ScreenAction("Give it its own memory card", () => ScreenCommand.Push(QueuedChangeScreens.Convert(session, game.RomId, game.DisplayName))) }
                    : [],
            ],
            // No row means the page fell back, which an unreachable RomM and a refused page
            // both do, so the note says what holds for either.
            Note = () => game.Row is null
                ? "This game is on the device. RomM's library could not be read, so it cannot be "
                    + "installed again from here."
                : "Installing puts it on the device now. Taking it off never removes a save.",
            Verbs = (action, _) => action switch
            {
                // The pick writes the member row and the sync opens over the set it joined,
                // which is the shape SetEditorViewModel already uses for create-then-resolve
                // and the reason ReplaceThenOpen exists.
                NavAction.Accept when game.Row is not null =>
                    Install(session, game, connect, changed),

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
    /// A refusal is a screen rather than a silent no-op. An unmapped platform, a folder-held
    /// ROM and a multi-file ROM are all facts about the library that a person
    /// can act on in RomM, and a press that appeared to work and produced nothing is the worse
    /// half of every one of them.
    /// </para>
    /// </remarks>
    private static ScreenCommand Install(
        InstallSession session,
        BrowseGame game,
        Func<Uri, RomMConnection>? connect,
        Action? changed)
    {
        // GameService answers both questions: whether it can join, and whether a pass would
        // have anything to do. The second is #116: already picked and already on disk fetches
        // nothing, and a pass would still have reported "Installed".
        var pick = new GameService(session).Pick(game.Row!, DateTimeOffset.UtcNow);
        var outcome = pick.Outcome;

        if (outcome.Member is null)
        {
            return ScreenCommand.Push(new MessageScreen(
                game.DisplayName,
                outcome.Problem ?? "This game cannot be put on this device."));
        }

        if (pick.NothingToFetch)
        {
            return ScreenCommand.Push(new MessageScreen(
                game.DisplayName,
                "This game is already on the device, and this set already claims it. Nothing "
                    + "was fetched."));
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
        var games = new GameService(session);

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
                var preview = games.PreviewRemoval(game.RomId);

                report = preview.Report;
                unvouchable = preview.Unvouchable;

                token.ThrowIfCancellationRequested();
                return Task.FromResult<string?>(null);
            },
            Verbs = (action, _) => action switch
            {
                NavAction.Accept when report is { } ready && ready.Plan.Selected.Count > 0 =>
                    ScreenCommand.Push(ApplyRemoval(session, game, ready, connect, changed)),
                _ => null,
            },
        }.Started();
    }

    internal static ListScreen ApplyRemoval(
        InstallSession session,
        BrowseGame game,
        EvictionReport report,
        Func<Uri, RomMConnection>? connect,
        Action? changed)
    {
        EvictionApplied? applied = null;
        string? unroamed = null;

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
                    .. unroamed is { } note ? new[] { new ListRow("Problem", null, note, false) } : [],
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
                var wasPicked = new PickedSetService(session).Picks().Contains(game.RomId);

                // Unpicks as well, whatever the files did. See GameService.ApplyRemovalAsync.
                applied = await new GameService(session)
                    .ApplyRemovalAsync(game.RomId, report, token)
                    .ConfigureAwait(false);

                changed?.Invoke();

                // An unpick roams as the pick did (#451). Waited on, unlike the install's push,
                // because nothing else here takes long and the screen has no later redraw to
                // carry a note on; not on the screen's token, so leaving does not stop it.
                if (wasPicked)
                {
                    unroamed = (await new RoamingConfigService(session, connect)
                        .PushAsync(cancellationToken: CancellationToken.None)
                        .ConfigureAwait(false)).Note;
                }

                return null;
            },

            // Back closes this screen and the preview under it, landing on the game's detail,
            // which re-reads its rows. Popping one would leave the preview on the stack holding
            // the report from before the removal, still offering to take off a game that is
            // already gone. The set-side removal pops past its preview for the same reason.
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
                game.Row is null ? "not known" : ByteSize.Format(game.SizeBytes),
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
