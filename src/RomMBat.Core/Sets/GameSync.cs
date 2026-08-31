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
        var walked = 0;

        var roms = new ContentSync(_install, _store, _connection, _time);
        var artwork = new MediaSync(_install, _store, _connection, _limits, _time);

        foreach (var game in Group(plan))
        {
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
                RollBack(game, progress, problems, ref rolledBack);
                stopped = true;
                break;
            }

            content = ContentSyncOutcome.Merge(content, landed);

            if (landed.Failed > 0)
            {
                RollBack(game, progress, problems, ref rolledBack);
                continue;
            }

            // Straight after this game's ROMs, before the next game's. The whole point of #102.
            var fetched = await artwork
                .ApplyAsync(
                    [.. game.RomIds],
                    new Immediate<string>(what => progress.Report(new MediaProgressed(what))),
                    cancellationToken)
                .ConfigureAwait(false);

            media = MediaSyncOutcome.Merge(media, fetched);
        }

        return new GameSyncOutcome
        {
            Content = content,
            Media = media,
            RolledBack = rolledBack,
            RollbackProblems = problems,
            Stopped = stopped,
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

    /// <summary>Takes back every file this run placed for one game, and the rows with them.</summary>
    private void RollBack(
        PlannedGame game,
        IProgress<SyncEvent> progress,
        List<string> problems,
        ref int rolledBack)
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
            // only written on commit.
            Delete(_install.Resolve(ContentPlanner.PartFor(romId)));
            _store.Downloads.Remove(romId);
        }

        problems.AddRange(failures);

        if (removed > 0 || failures.Count > 0)
        {
            rolledBack++;
            progress.Report(new GameRolledBack(game.Title, removed, freed, failures));
        }
    }

    /// <summary>Deletes one file, or says why it could not be.</summary>
    /// <remarks>
    /// Windows refuses a file operation two ways and only one of them is an
    /// <see cref="IOException"/>: <see cref="UnauthorizedAccessException"/> does not derive from
    /// it, and a read-only attribute or a revoked ACL raises that one instead. Both are caught,
    /// which is the shape <c>TreeLock.TryAcquire</c> already uses.
    /// </remarks>
    private static string? Delete(string absolute)
    {
        try
        {
            File.Delete(absolute);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ex.Message;
        }
    }
}
