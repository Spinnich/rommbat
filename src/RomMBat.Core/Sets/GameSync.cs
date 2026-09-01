using RomM.Client;
using RomMBat.Core.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.Store;

namespace RomMBat.Core.Sets;

/// <summary>
/// One game, and every file a sync would place for it.
/// </summary>
/// <remarks>
/// <b>One member is always one file, so the only game with two members is a multi-disc title.</b>
/// <c>SetResolver</c> refuses multi-file ROMs outright, which leaves the disc marker as the one
/// thing that binds two rows into one game, and <see cref="DiscSet.Parse"/> already reads it.
/// Nothing new had to learn what a game is.
/// </remarks>
/// <param name="Title">What a person calls it, for a screen and for a problem line.</param>
public sealed record PlannedGame(string Title, IReadOnlyList<ContentStep> Steps)
{
    /// <summary>The rom ids this game covers, which is one per disc.</summary>
    public IEnumerable<int> RomIds => Steps.Select(step => step.Member.RomId);

    /// <summary>
    /// True when this run would fetch any part of it, rather than finding it already here.
    /// </summary>
    /// <remarks>
    /// The fence that keeps the rollback to this run's own writes. A game that entered the run
    /// as <see cref="ContentAction.AlreadyPresent"/> was on disk before the sync started and is
    /// not this run's to remove, whatever happens afterwards.
    /// </remarks>
    public bool IsThisRunsToRemove =>
        Steps.Any(step => step.Action is ContentAction.Download or ContentAction.Resume);
}

/// <summary>What one set's content pass did, ROMs and artwork together.</summary>
public sealed record GameSyncOutcome
{
    public required ContentSyncOutcome Content { get; init; }

    public required MediaSyncOutcome Media { get; init; }

    /// <summary>Games removed because they did not land whole.</summary>
    public int RolledBack { get; init; }

    /// <summary>What could not be removed, in the words the user should see.</summary>
    public IReadOnlyList<string> RollbackProblems { get; init; } = [];

    /// <summary>
    /// True when the server refused this device's identity and the run stopped for it.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="Stopped"/>, which is the user's own press. Nothing about this
    /// is recoverable by trying again: the caller offers to pair.
    /// </remarks>
    public bool Rejected { get; init; }

    /// <summary>
    /// True when the user stopped the run.
    /// </summary>
    /// <remarks>
    /// Returned rather than thrown. 7b-2a's cancelled resolve threw and lost the membership the
    /// walk had accumulated, and the fix was to make the stop an ordinary way out. Same rule
    /// here: everything this pass did before the press is real and is reported.
    /// </remarks>
    public bool Stopped { get; init; }
}

/// <summary>
/// Syncing a set one whole game at a time.
/// </summary>
/// <remarks>
/// <b>A sync leaves every game either wholly present, with its gamelist entry and whatever
/// artwork the server actually had for it, or wholly absent.</b> Whether it ran to the end, was
/// stopped, or lost the server. That sentence is what this type exists for, and the two halves
/// of it are the interleave and the rollback.
/// <para>
/// <b>Artwork is fetched per game, straight after that game's ROMs, and that is a fix rather
/// than a tidy-up.</b> Media used to be one pass after every ROM of every set, so
/// <c>ContentPlanner.Plan</c> filled the cap with ROMs and <c>MediaSync</c> then found no room
/// at all: the games landed in EmulationStation with no covers, and no later run repaired it,
/// because nothing frees space by itself. Interleaving makes a budget that runs out truncate
/// the tail of the library instead of stripping the artwork off all of it. Nothing is reserved
/// for artwork, because the size is free at fetch time and unknowable at plan time: RomM
/// publishes no media size on the rom row, so a reservation would need one HEAD per kind per
/// game. See #102.
/// </para>
/// <para>
/// <b>A game that did not land whole is removed, and the bound is this run's own writes.</b>
/// Removing content is <c>evict</c>'s job and happens behind a preview, and that rule is not
/// weakened here: what goes is what this very run placed seconds ago, on a game that is not
/// finished. Three fences make it true, and each is asserted:
/// </para>
/// <list type="bullet">
/// <item>Only <see cref="FileOrigin.Synced"/> rows. Never <see cref="FileOrigin.Adopted"/>,
/// which is the user's own ROM or their own scrape.</item>
/// <item>Never a game that entered the run as <see cref="ContentAction.AlreadyPresent"/>. It
/// was on disk before the sync started.</item>
/// <item>The <c>local_file</c> row goes with the bytes, which is <c>ContentSync</c>'s own rule
/// that neither outlives the other. A file that cannot be deleted keeps its row and is
/// reported, because a row removed from under a file nothing tracks is worse than a game left
/// half present and named.</item>
/// </list>
/// <para>
/// <b>The rollback is bounded to the ROMs, not to the artwork.</b> A game whose every ROM
/// committed is landed: it is playable and it gets a gamelist entry, and a stop during its
/// artwork leaves it present with the next run filling the rest in, which is exactly what an
/// <see cref="ContentAction.AlreadyPresent"/> game plus a media pass already does.
/// </para>
/// <para>
/// <b>Artwork completeness could not be part of the invariant even if it were wanted, because
/// nothing guarantees a server has the artwork at all.</b> Two independent reasons, neither of
/// them a fault and neither fixable by a re-run: the RomM administrator may not have scraped
/// that kind, and the upstream source (ScreenScraper, IGDB) may never have held it for that
/// game. <see cref="MediaSyncOutcome.Missing"/> is the count of exactly that, and a rule that
/// said "wholly present means every configured kind" would declare most real libraries
/// permanently broken. What the invariant forbids is the systematic stripping #102 caused,
/// where artwork RomM did have went unfetched because the ROMs had eaten the budget first.
/// </para>
/// <para>
/// <b>It fires on any incomplete game and not only on a stop.</b> A multi-disc title whose
/// second disc fails leaves half a game on disk with nobody pressing anything, and
/// <c>ContentSync</c>'s "a failure is per game, not per run" is what makes that the ordinary
/// path rather than the unlucky one. Ruled with Spinnich before this was written. For a
/// single-file game it can only ever be a no-op, since a failed transfer commits nothing.
/// </para>
/// </remarks>
public sealed class GameSync
{
    private readonly RetroBatInstall _install;
    private readonly LocalStore _store;
    private readonly RomMConnection _connection;
    private readonly FilesystemLimits _limits;
    private readonly TimeProvider? _time;

    public GameSync(
        RetroBatInstall install,
        LocalStore store,
        RomMConnection connection,
        FilesystemLimits? limits = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(connection);

        _install = install;
        _store = store;
        _connection = connection;
        _limits = limits ?? FilesystemLimits.Inspect(install.RootPath);
        _time = timeProvider;
    }

    /// <summary>
    /// Groups a plan's steps into games, in the order the plan walks them.
    /// </summary>
    /// <remarks>
    /// Keyed on the folder and the disc marker's base title, so the discs of one title are one
    /// game wherever they sort. Anything with no marker keys on its own file name, which is
    /// unique within a folder by construction: two names cannot occupy one target path.
    /// </remarks>
    public static IReadOnlyList<PlannedGame> Group(ContentPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var order = new List<string>();
        var grouped = new Dictionary<string, List<ContentStep>>(StringComparer.OrdinalIgnoreCase);

        foreach (var step in plan.Steps)
        {
            var marker = DiscSet.Parse(step.Member.FsName);
            var key = $"{step.Member.Folder}/{marker?.BaseTitle ?? step.Member.FsName}";

            if (!grouped.TryGetValue(key, out var steps))
            {
                steps = [];
                grouped[key] = steps;
                order.Add(key);
            }

            steps.Add(step);
        }

        return
        [
            .. order.Select(key => new PlannedGame(
                DiscSet.Parse(grouped[key][0].Member.FsName) is { } marker
                    ? marker.BaseTitle
                    : grouped[key][0].Member.DisplayName,
                grouped[key])),
        ];
    }

    /// <summary>Carries out a plan, one whole game at a time.</summary>
    public async Task<GameSyncOutcome> ApplyAsync(
        ContentPlan plan,
        IProgress<SyncEvent> progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(progress);

        var content = new ContentSyncOutcome();
        var media = new MediaSyncOutcome();
        var rolledBack = 0;
        var problems = new List<string>();
        var stopped = false;
        var rejected = false;
        var walked = 0;

        var roms = new ContentSync(_install, _store, _connection, _time);
        var artwork = new MediaSync(_install, _store, _connection, _limits, _time);

        var games = Group(plan);

        for (var index = 0; index < games.Count; index++)
        {
            var game = games[index];
            var offset = walked;
            walked += game.Steps.Count;

            ContentSyncOutcome landed;

            try
            {
                landed = await roms
                    .ApplyAsync(
                        plan with { Steps = game.Steps },
                        Renumbered(progress, offset, plan.Steps.Count),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Stopped inside this game's transfer, so this game is the one that goes.
                // Returned rather than rethrown: everything the pass already did is real, and
                // 7b-2a's cancelled resolve is the lesson about throwing that work away.
                RollBack(game, progress, problems, ref rolledBack, Resume.Discard);
                stopped = true;
                break;
            }

            content = ContentSyncOutcome.Merge(content, landed);

            if (landed.Failed > 0)
            {
                // A failure is not a cancellation, and the difference is the partial. The whole
                // game still comes off disk, so the invariant holds, but what the next run would
                // continue from is left alone.
                RollBack(game, progress, problems, ref rolledBack, Resume.Keep);

                if (landed.Rejected)
                {
                    // The server refused this device rather than this request, so every game
                    // after it would send the same token and be refused identically. One
                    // expired pairing must not become forty problems.
                    rejected = true;
                    break;
                }

                continue;
            }

            // Straight after this game's ROMs, before the next game's. The whole point of #102.
            //
            // The ROMs still ahead are passed as a reservation. Artwork is bounded by what the
            // budget has left, and interleaving moved that reading to a moment when most of the
            // run's ROMs are not yet on disk to be counted: without this, a 1 MB budget was
            // measured finishing 703 KB over it.
            MediaSyncOutcome fetched;

            try
            {
                fetched = await artwork
                    .ApplyAsync(
                        [.. game.RomIds],
                        new Immediate<string>(what => progress.Report(new MediaProgressed(what))),
                        RemainingRomBytes(games, index + 1),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Stopped while fetching this game's artwork. **The game is not rolled back**:
                // every one of its ROMs committed, so it is playable and gets a gamelist entry,
                // and the next run fills in the artwork exactly as it would for any game that
                // was already present.
                //
                // Returned rather than rethrown, for the same reason the ROM path is: letting
                // it escape skipped the caller's gamelist pass, so a stop during artwork left
                // every finished game on disk and invisible to EmulationStation. That is the
                // same defect a hands-on pass found on the ROM path, in the place beside it.
                stopped = true;
                break;
            }

            media = MediaSyncOutcome.Merge(media, fetched);
        }

        return new GameSyncOutcome
        {
            Content = content,
            Media = media,
            RolledBack = rolledBack,
            RollbackProblems = problems,
            Stopped = stopped,
            Rejected = rejected,
        };
    }

    /// <summary>
    /// Renumbers a sub-plan's progress against the whole plan.
    /// </summary>
    /// <remarks>
    /// <see cref="ContentSync"/> counts within the plan it is given, so running it per game
    /// would report "1 of 1" forty times where a person needs "12 of 40". The arithmetic is
    /// here rather than a new parameter on <see cref="ContentSync"/>, which this stage composes
    /// rather than touches.
    /// </remarks>
    private static Immediate<ContentSyncProgress> Renumbered(
        IProgress<SyncEvent> progress,
        int offset,
        int total) =>
        new Immediate<ContentSyncProgress>(step =>
            progress.Report(new ContentProgressed(step with { Index = offset + step.Index, Total = total })));

    /// <summary>
    /// How many bytes of ROM this run still intends to fetch, from the given game onwards.
    /// </summary>
    /// <remarks>
    /// Only <see cref="ContentAction.Download"/> and <see cref="ContentAction.Resume"/> count.
    /// A step that is already present or adopted is on disk and already in <c>local_file</c>,
    /// and a blocked one is never fetched at all, so counting either would reserve room twice.
    /// <para>
    /// <b>Counted over the games, not over the plan's own steps.</b> Skipping a step count into
    /// <see cref="ContentPlan.Steps"/> is only the same window when every game's steps are
    /// adjacent there, and <see cref="Group"/> makes no such promise: a title whose discs are
    /// apart in the plan would leave the window counting a disc already fetched and missing one
    /// still ahead. The error is one game's bytes, which is the size the reservation exists to
    /// stop being spent.
    /// </para>
    /// </remarks>
    private static long RemainingRomBytes(IReadOnlyList<PlannedGame> games, int from) =>
        games
            .Skip(from)
            .SelectMany(game => game.Steps)
            .Where(step => step.Action is ContentAction.Download or ContentAction.Resume)
            .Sum(step => step.BytesToTransfer);

    /// <summary>Whether a rollback also throws away what a later run could continue from.</summary>
    /// <remarks>
    /// <b>Only a user cancellation discards a partial that has bytes in it.</b> A stopped
    /// transfer is discarded by ruling, and truncating it before the handle closes is what takes
    /// the stop from 20.2 s to 0.2 s. A transfer that failed on its own, an unreachable server
    /// most of all, keeps both its <c>.part</c> and its download row, because resuming from them
    /// is the whole reason one is written: a 929 MB image that lost the LAN at 800 MB must not
    /// start again from zero.
    /// <para>
    /// An empty partial is discarded either way. See <see cref="NothingToResume"/>: there is
    /// nothing in it to continue from, so keeping it is litter rather than progress.
    /// </para>
    /// </remarks>
    private enum Resume
    {
        Keep,
        Discard,
    }

    /// <summary>Takes back every file this run placed for one game, and the rows with them.</summary>
    private void RollBack(
        PlannedGame game,
        IProgress<SyncEvent> progress,
        List<string> problems,
        ref int rolledBack,
        Resume resume)
    {
        if (!game.IsThisRunsToRemove)
        {
            return;
        }

        var removed = 0;
        var freed = 0L;
        var failures = new List<string>();

        foreach (var step in game.Steps.Where(step =>
            step.Action is ContentAction.Download or ContentAction.Resume))
        {
            var romId = step.Member.RomId;

            // Every kind, because a game's artwork is as much this run's write as its ROM, and
            // a cover left behind is a row pointing at a game that is not there.
            foreach (var file in _store.Files.ForRom(romId).Where(file => file.Origin == FileOrigin.Synced))
            {
                if (Delete(_install.Resolve(file.Path)) is { } problem)
                {
                    // The row stays with the bytes. Removing it here would leave a file nothing
                    // tracks, which neither the budget nor eviction could ever reach again.
                    failures.Add($"{game.Title}: {file.Path.Name} could not be removed ({problem}).");
                    continue;
                }

                _store.Files.Remove(file.Path);
                removed++;
                freed += file.SizeBytes;
            }

            // The interrupted transfer itself, which carries no local_file row because a row is
            // only written on commit. Kept on a failure: see Resume.
            var part = _install.Resolve(ContentPlanner.PartFor(romId));

            if (resume == Resume.Discard || NothingToResume(part))
            {
                Delete(part);
                _store.Downloads.Remove(romId);
            }
        }

        problems.AddRange(failures);

        if (removed > 0 || failures.Count > 0)
        {
            rolledBack++;
            progress.Report(new GameRolledBack(game.Title, removed, freed, failures));
        }
    }

    /// <summary>
    /// True when a partial holds nothing a later run could continue from.
    /// </summary>
    /// <remarks>
    /// <b>Bytes are what make a partial worth keeping, and a transfer that failed before any
    /// arrived left none.</b> <see cref="ContentSync"/> opens the <c>.part</c> before it makes
    /// the request, so a response that never carries a body still leaves an empty file and a
    /// download row behind it. Measured on a live install: one RomM instance answering 502 for
    /// three seconds produced <b>155 empty partials and 155 download rows</b>, none of which a
    /// resume could use and all of which a person then has to make sense of.
    /// <para>
    /// Unreadable counts as worth keeping. Being unable to measure a file is not evidence that
    /// it is empty, and the resume is the thing being protected.
    /// </para>
    /// </remarks>
    private static bool NothingToResume(string absolute)
    {
        try
        {
            var info = new FileInfo(absolute);
            return !info.Exists || info.Length == 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Deletes one file, or says why it could not be.</summary>
    /// <remarks>
    /// Windows refuses a file operation two ways and only one of them is an
    /// <see cref="IOException"/>: <see cref="UnauthorizedAccessException"/> does not derive from
    /// it, and a read-only attribute or a revoked ACL raises that one instead. Both are caught,
    /// which is the shape <c>TreeLock.TryAcquire</c> already uses.
    /// <para>
    /// <b>A file that is already gone is a success, and <see cref="File.Delete(string)"/> is
    /// inconsistent about which kind of gone.</b> A missing <i>file</i> returns quietly; a
    /// missing <i>directory</i> throws <see cref="DirectoryNotFoundException"/>, which derives
    /// from <see cref="IOException"/> and was therefore read as "a media player has this open".
    /// The row was then kept for bytes that do not exist, which is the inverse of the rule this
    /// method serves, and the user was told a file they no longer have could not be removed.
    /// Found on a live install whose <c>roms/ps2/images/</c> had been cleaned up out from under
    /// its rows.
    /// </para>
    /// </remarks>
    private static string? Delete(string absolute)
    {
        try
        {
            File.Delete(absolute);
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ex.Message;
        }
    }
}
