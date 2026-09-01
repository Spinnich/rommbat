using RomM.Client;
using RomM.Client.Catalog;
using RomMBat.Core.Content;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;

namespace RomMBat.Core.Sets;

/// <summary>One of the passes a sync makes, in the order it makes them.</summary>
public enum SyncPass
{
    /// <summary>The ES event hooks, installed on the first run.</summary>
    Hooks,

    /// <summary>The ES menu entry, installed on the first run.</summary>
    Menu,

    /// <summary>The saves flush, which everything below depends on.</summary>
    Flush,

    /// <summary>What this drive can hold.</summary>
    Filesystem,

    /// <summary>Re-resolving the sets, because server-side membership drifts.</summary>
    Resolve,

    /// <summary>Firmware, ahead of every ROM.</summary>
    Bios,

    /// <summary>The ROMs themselves.</summary>
    Content,

    /// <summary>Artwork for what landed.</summary>
    Media,

    /// <summary>The gamelists EmulationStation reads.</summary>
    Gamelists,

    /// <summary>What the budget has left.</summary>
    Budget,
}

/// <summary>Something a sync did, reported as it happens so a caller can show it in order.</summary>
public abstract record SyncEvent(SyncPass Pass);

public sealed record HooksInstalled(EsHookOutcome Outcome) : SyncEvent(SyncPass.Hooks);

public sealed record MenuInstalled(EsMenuOutcome Outcome) : SyncEvent(SyncPass.Menu);

public sealed record FlushStarting() : SyncEvent(SyncPass.Flush);

public sealed record FilesystemNoted(FilesystemLimits Limits) : SyncEvent(SyncPass.Filesystem);

public sealed record SetResolved(ResolveReport Report) : SyncEvent(SyncPass.Resolve);

public sealed record BiosPlanned(BiosPlan Plan) : SyncEvent(SyncPass.Bios);

public sealed record BiosProblem(string Message) : SyncEvent(SyncPass.Bios);

public sealed record BiosApplied(BiosSyncOutcome Outcome) : SyncEvent(SyncPass.Bios);

public sealed record SetPlanned(SyncSetDefinition Set, ContentPlan Plan) : SyncEvent(SyncPass.Content);

public sealed record SetSkipped(SyncSetDefinition Set, bool HadDownloads) : SyncEvent(SyncPass.Content);

public sealed record ContentProgressed(ContentSyncProgress Progress) : SyncEvent(SyncPass.Content);

public sealed record SetSynced(SyncSetDefinition Set, ContentSyncOutcome Outcome) : SyncEvent(SyncPass.Content);

/// <summary>
/// A game that did not land whole, and the files this run took back for it.
/// </summary>
/// <remarks>
/// Reported rather than absorbed, because it is the one thing a sync does that removes
/// something. The user pressed stop, or a disc of a set failed, and either way what they get
/// told is which game went and how much came back.
/// </remarks>
public sealed record GameRolledBack(
    string Title,
    int Files,
    long Bytes,
    IReadOnlyList<string> Problems) : SyncEvent(SyncPass.Content);

public sealed record MediaProgressed(string What) : SyncEvent(SyncPass.Media);

public sealed record MediaApplied(MediaSyncOutcome Outcome) : SyncEvent(SyncPass.Media);

public sealed record GamelistsWritten(GamelistSyncOutcome Outcome) : SyncEvent(SyncPass.Gamelists);

public sealed record BudgetReported(long UsedBytes, long CapBytes) : SyncEvent(SyncPass.Budget);

/// <summary>What a sync run was asked to do.</summary>
/// <param name="DryRun">Plan and report, write nothing at all.</param>
/// <param name="Offline">Do everything the local store can answer, fetch nothing.</param>
/// <param name="NoResolve">Use the membership already recorded rather than re-resolving.</param>
public sealed record SyncOptions(
    bool DryRun = false,
    bool Offline = false,
    bool NoResolve = false,
    string? Passphrase = null);

/// <summary>How a sync ended.</summary>
public enum SyncState
{
    /// <summary>Everything asked for happened.</summary>
    Done,

    /// <summary>Something could not be fetched. The next run tries again.</summary>
    Incomplete,

    /// <summary>A set could not be resolved and the run stopped.</summary>
    Refused,

    /// <summary>
    /// The server refused this device. Pairing again is the only way on.
    /// </summary>
    /// <remarks>
    /// A 401 mid-run is an identity change rather than a transient fault, so the run stops
    /// instead of sending the same rejected token forty more times. Everything already fetched
    /// stays, and the gamelists for it are still written.
    /// </remarks>
    Rejected,

    /// <summary>
    /// The disk budget stopped it. Nothing failed and nothing more will fit.
    /// </summary>
    /// <remarks>
    /// <b>A fourth state rather than reusing <see cref="Incomplete"/>, and it wants
    /// justifying because this enum grew twice in 7b-2b.</b> A blocked ROM is not a failed one:
    /// <c>ContentSyncOutcome</c> counts it separately and nothing about it would be different
    /// next time, so a run the cap stopped dead was returning <see cref="Done"/> and a screen
    /// rendered that as "Everything in these sync sets is on this device" over 386 games left
    /// out. Met on a live install.
    /// <para>
    /// <b>Reusing <see cref="Incomplete"/> would be a different lie.</b> That is what
    /// <c>SyncCommand</c> turns into its <c>Offline</c> exit code, and a full disk budget is not
    /// being offline. So this changes an agent exit code rather than borrowing a wrong one, and
    /// costs <c>SyncCommand</c> one arm.
    /// </para>
    /// <para>
    /// <b>Ranked below <see cref="Incomplete"/>.</b> A run that both failed a transfer and hit
    /// the cap has a fault in it, and the fault is the thing worth reporting; a run that only
    /// hit the cap has no fault at all.
    /// </para>
    /// </remarks>
    Blocked,

    /// <summary>
    /// The user stopped it. The tree is correct, not postponed.
    /// </summary>
    /// <remarks>
    /// A stop still writes the gamelists for the folders it touched and still reports the
    /// budget, because a run that ends with games on disk that EmulationStation cannot see has
    /// left work behind rather than stopping.
    /// </remarks>
    Stopped,
}

/// <summary>The outcome of a whole run.</summary>
public sealed record SyncReport(SyncState State, IReadOnlyList<SyncPass> Ran);

/// <summary>
/// Turning resolved sets into files in the RetroBat tree.
/// </summary>
/// <remarks>
/// <b>Ten passes, in <see cref="Order"/>, and the order is the design.</b>
/// <para>
/// <b>The saves flush goes first, ahead of everything including BIOS.</b> It is what turns
/// spooled hook events into play sessions and brings <c>local_save</c> up to date, and eviction
/// inside this run asks <c>local_save</c> whether a game's saves are safely up. Flushing
/// afterwards would answer that from the previous run. It is also the only thing that sends a
/// save at all in this build, since the hooks spool and exit, so a user who never leaves
/// EmulationStation has this as their one trigger.
/// </para>
/// <para>
/// <b>BIOS goes ahead of every ROM.</b> A platform synced without its firmware is dead weight
/// in the gallery: the games appear in EmulationStation, look right, and die on launch.
/// Fetching it after the ROMs would leave exactly that state behind on any run that was
/// interrupted, and interrupted is the normal case for a handheld.
/// </para>
/// <para>
/// <b>The set is re-resolved first</b>, because smart-collection membership drifts server-side
/// and fetching a stale membership downloads games the set no longer contains.
/// </para>
/// <para>
/// <b><see cref="Order"/> is declared rather than implied, so a swap is catchable.</b> The
/// runner below is still straight-line code that reports each pass as it enters it; what the
/// declaration buys is a test that asserts the observed sequence against it, which fails when
/// two statements are exchanged. Making the runner iterate the list instead would put the
/// ordering beyond accident but would rewrite the method this seam is trying to prove it did
/// not alter.
/// </para>
/// <para>
/// <b>Report through a synchronous <see cref="IProgress{T}"/>.</b> <c>System.Progress&lt;T&gt;</c>
/// posts rather than calls, which on a console with no synchronization context means the thread
/// pool and therefore no ordering guarantee at all. A caller printing these in order must pass
/// a sink that invokes inline. <see cref="Immediate{T}"/> is that sink.
/// </para>
/// <para>
/// <b>The flush is a delegate rather than a pass this type implements.</b> Flushing is
/// <c>FlushCommand</c>'s 289 lines and lifting it belongs with the stage that gives it a face;
/// what matters here is that this type owns <i>when</i> it runs, which is what the ordering
/// assertion is about. A caller that supplies nothing gets no flush, which is what a dry run
/// wants anyway.
/// </para>
/// </remarks>
public sealed class LibrarySyncService
{
    private readonly InstallSession _session;

    public LibrarySyncService(InstallSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
    }

    /// <summary>
    /// The passes, in the order a run makes them.
    /// </summary>
    /// <remarks>
    /// The ordering argument is in this type's own remarks, beside the thing it orders. A test
    /// asserts a real run's observed sequence against this list.
    /// </remarks>
    public static IReadOnlyList<SyncPass> Order { get; } =
    [
        SyncPass.Hooks,
        SyncPass.Menu,
        SyncPass.Flush,
        SyncPass.Filesystem,
        SyncPass.Resolve,
        SyncPass.Bios,
        SyncPass.Content,
        SyncPass.Media,
        SyncPass.Gamelists,
        SyncPass.Budget,
    ];

    /// <summary>Runs a sync, reporting each pass as it happens.</summary>
    /// <param name="flush">
    /// The saves flush. Skipped on a dry run, which writes nothing by definition.
    /// </param>
    public async Task<SyncReport> RunAsync(
        IReadOnlyList<SyncSetDefinition> sets,
        SyncOptions options,
        RomMConnection? connection,
        IProgress<SyncEvent> progress,
        Func<CancellationToken, Task>? flush = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sets);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(progress);

        var ran = new List<SyncPass>();

        if (!options.DryRun)
        {
            ran.Add(SyncPass.Hooks);
            var hooks = new EsHooks(_session.Install);

            if (!hooks.IsInstalled())
            {
                progress.Report(new HooksInstalled(hooks.Install()));
            }

            ran.Add(SyncPass.Menu);
            var menu = new EsMenuEntry(_session.Install);

            if (!menu.IsInstalled())
            {
                progress.Report(new MenuInstalled(menu.Install()));
            }

            // First, before a byte is fetched. Everything below depends on local_save being
            // current, and a sync that flushed last would answer from the previous run.
            ran.Add(SyncPass.Flush);
            progress.Report(new FlushStarting());

            if (flush is not null)
            {
                await flush(cancellationToken).ConfigureAwait(false);
            }
        }

        ran.Add(SyncPass.Filesystem);
        var limits = FilesystemLimits.Inspect(_session.Install.RootPath);
        progress.Report(new FilesystemNoted(limits));

        var current = sets;

        if (!options.Offline && connection is not null && !options.NoResolve)
        {
            ran.Add(SyncPass.Resolve);

            var reports = await new SetResolveService(_session, connection)
                .ResolveAsync(current, progress: null, cancellationToken)
                .ConfigureAwait(false);

            foreach (var report in reports)
            {
                progress.Report(new SetResolved(report));

                if (report.State is ResolveState.Refused or ResolveState.NeedsFolderChoice)
                {
                    return new SyncReport(SyncState.Refused, ran);
                }

                if (report.Rejected)
                {
                    // The resolve is the first authenticated call a sync makes, so a rejected
                    // token is met here rather than in the content pass. Reported as what it
                    // is: nothing about it is worth trying again, and the caller offers to pair.
                    return new SyncReport(SyncState.Rejected, ran);
                }

                if (report.State == ResolveState.Interrupted)
                {
                    return new SyncReport(SyncState.Incomplete, ran);
                }
            }

            // Re-read: resolution rewrote both the definitions and the membership.
            current = [.. current.Select(set => _session.Store.SyncSets.Find(set.Name) ?? set)];
        }

        var worst = SyncState.Done;

        // Folders touched across every set, so one gamelist pass covers them all. Two RomM
        // platforms can resolve to one folder, and writing per set would have the second
        // set's write clobber the first's.
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        ran.Add(SyncPass.Bios);
        await FetchBiosAsync(current, connection, limits, options, progress, cancellationToken)
            .ConfigureAwait(false);

        ran.Add(SyncPass.Content);
        var planner = new ContentPlanner(_session.Install, _session.Store, limits);
        var artwork = new MediaSyncOutcome();
        var fetchedArtwork = false;

        foreach (var set in current)
        {
            var members = _session.Store.SyncSets.Members(set.Id);
            var plan = planner.Plan(set, members);

            progress.Report(new SetPlanned(set, plan));

            // Collected before the run rather than after it, so an offline pass reaches the
            // gamelist write below. The set is the plan's, not the outcome's, because a folder
            // whose download failed still holds whatever was already there.
            foreach (var step in plan.Steps.Where(step => step.Action != ContentAction.Blocked))
            {
                folders.Add(step.Member.Folder!);
            }

            if (options.DryRun || options.Offline)
            {
                progress.Report(new SetSkipped(set, plan.Downloads.Any()));
                continue;
            }

            if (connection is null)
            {
                continue;
            }

            // One game at a time, artwork included, because a run that stops has to leave every
            // game either wholly present or wholly absent. GameSync owns that sentence.
            var outcome = await new GameSync(_session.Install, _session.Store, connection, limits)
                .ApplyAsync(plan, progress, cancellationToken)
                .ConfigureAwait(false);

            progress.Report(new SetSynced(set, outcome.Content));

            artwork = MediaSyncOutcome.Merge(artwork, outcome.Media);
            fetchedArtwork = true;

            if (outcome.Content.Failed > 0)
            {
                worst = SyncState.Incomplete;
            }
            else if (outcome.Content.Blocked > 0 && worst == SyncState.Done)
            {
                // Answered here rather than left to each caller. #109's hands-on pass met a run
                // that fetched nothing because the cap was full and reported success, and the
                // fix at the time was in the screen, which meant every future consumer had to
                // remember to check Blocked for itself. The sync screen already forgot once.
                worst = SyncState.Blocked;
            }

            if (outcome.Rejected)
            {
                worst = SyncState.Rejected;
                break;
            }

            if (outcome.Stopped)
            {
                // Everything below still runs. A stopped sync ends with a correct tree rather
                // than with work postponed, so the gamelists for the folders it touched are
                // written and the budget is reported.
                worst = SyncState.Stopped;
                break;
            }
        }

        if (fetchedArtwork)
        {
            ran.Add(SyncPass.Media);
            progress.Report(new MediaApplied(artwork));
        }

        // Written even on a dry run's opposite, an offline run: the gamelist comes from local
        // state, so a sync that fetched nothing still leaves ES showing what is there. A dry
        // run writes nothing at all, which is what a dry run means.
        if (!options.DryRun && folders.Count > 0)
        {
            ran.Add(SyncPass.Gamelists);

            using var emulationStation = new EmulationStationClient();

            // Not on the run's token, and this is the whole of "a stopped sync ends with a
            // correct tree rather than with work postponed". Handing the cancelled token here
            // made the pass throw the instant it started, so a stop left every finished game on
            // disk and invisible to EmulationStation, which is worse than not having fetched it.
            // Found by a hands-on pass: the first game of a set landed on the drive and never
            // appeared in the front end.
            //
            // Bounded rather than unbounded: the write is local and the reload has a 400 ms
            // connect timeout, so a screen being disposed waits for two file writes and one
            // refused socket at most.
            progress.Report(new GamelistsWritten(await new GamelistSync(_session.Install, _session.Store)
                .ApplyAsync(folders, emulationStation, CancellationToken.None)
                .ConfigureAwait(false)));
        }

        if (_session.Store.Settings.GetInt64(SettingStore.ContentMaxBytes) is { } cap)
        {
            ran.Add(SyncPass.Budget);
            progress.Report(new BudgetReported(planner.ManagedBytes(), cap));
        }

        return new SyncReport(worst, ran);
    }

    /// <summary>
    /// Puts one game on the device, now, rather than waiting for a whole run.
    /// </summary>
    /// <remarks>
    /// <b>Four of the ten passes, and the six that are missing are missing for a reason each.</b>
    /// Content, Media, Gamelists and Budget run; Hooks and Menu are first-run installs a whole
    /// sync has already done; Filesystem is inspected because the plan needs it, and reported
    /// like any other pass; Resolve does not run because there is nothing to resolve, the member
    /// row having been written from the browse row that was in hand; Flush does not run because
    /// 7b-2b put it first for eviction's benefit and nothing here evicts; BIOS does not run,
    /// which is the one worth arguing with and is stated in the paragraph below.
    /// <para>
    /// <b>It takes a member rather than a <c>PlannedGame</c>, and that is a change from the
    /// brief.</b> <c>PlannedGame</c> is <see cref="GameSync"/>'s grouping type, so a caller
    /// holding one has already run <see cref="ContentPlanner"/> and <c>GameSync.Group</c>, which
    /// is planning done in whatever is calling: on this branch that is a screen. Given the
    /// member, Core plans, groups, and picks up the other discs of a multi-disc title the member
    /// belongs to, which a caller building its own single-step plan would have silently dropped.
    /// </para>
    /// <para>
    /// <b>BIOS is not fetched, and a game that needs it will not launch.</b> Firmware is
    /// per folder rather than per game and a whole sync of the set fetches it, so running the
    /// BIOS pass for one game would fetch a platform's entire firmware on a press that promised
    /// one download. The honest position is that this puts the game on the device and a sync of
    /// the set it joined is what makes a platform complete, and it is stated here rather than
    /// discovered.
    /// </para>
    /// <para>
    /// <b><see cref="GameSync"/> is reused untouched</b>, so the whole-or-absent invariant, its
    /// three rollback fences and the gamelist write on <see cref="CancellationToken.None"/> all
    /// come along rather than being written a second time.
    /// </para>
    /// </remarks>
    /// <param name="set">The set the member belongs to, which decides the folder and the policy.</param>
    /// <param name="member">The one game, as its membership row records it.</param>
    public async Task<SyncReport> InstallAsync(
        SyncSetDefinition set,
        SyncSetMember member,
        RomMConnection connection,
        IProgress<SyncEvent> progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(progress);

        var ran = new List<SyncPass> { SyncPass.Filesystem };
        var limits = FilesystemLimits.Inspect(_session.Install.RootPath);
        progress.Report(new FilesystemNoted(limits));

        // The whole set's membership, then narrowed to this game's discs. A multi-disc title is
        // several member rows and one game, and DiscSet is what binds them; planning the one row
        // would leave half a title on disk, which is the state the invariant exists to forbid.
        var wanted = DiscSet.Parse(member.FsName)?.BaseTitle;

        var members = _session.Store.SyncSets.Members(set.Id)
            .Where(candidate => candidate.RomId == member.RomId
                || (wanted is not null
                    && string.Equals(candidate.Folder, member.Folder, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(DiscSet.Parse(candidate.FsName)?.BaseTitle, wanted, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (members.Count == 0)
        {
            members = [member];
        }

        ran.Add(SyncPass.Content);
        var planner = new ContentPlanner(_session.Install, _session.Store, limits);
        var plan = planner.Plan(set, members);

        progress.Report(new SetPlanned(set, plan));

        var outcome = await new GameSync(_session.Install, _session.Store, connection, limits)
            .ApplyAsync(plan, progress, cancellationToken)
            .ConfigureAwait(false);

        progress.Report(new SetSynced(set, outcome.Content));

        // Reported whenever the artwork pass ran, not only when it fetched something, which is
        // what the whole-library run does and what this got wrong first. A live install of a
        // 2.6 GB Wii U title finished with no artwork on the server and said nothing at all,
        // where MediaSyncOutcome.Missing is exactly the count that explains a game landing
        // without a cover. Nothing guarantees a server holds the artwork, so silence there is
        // the ordinary case and the one most worth wording.
        ran.Add(SyncPass.Media);
        progress.Report(new MediaApplied(outcome.Media));

        var folders = plan.Steps
            .Where(step => step.Action != ContentAction.Blocked)
            .Select(step => step.Member.Folder!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (folders.Count > 0)
        {
            ran.Add(SyncPass.Gamelists);

            using var emulationStation = new EmulationStationClient();

            // Not on the run's token, exactly as the whole-library run does it: a stop that
            // skipped this would leave a finished game on disk and invisible to EmulationStation,
            // which is worse than not having fetched it. Found by a hands-on pass in 7b-2b.
            progress.Report(new GamelistsWritten(await new GamelistSync(_session.Install, _session.Store)
                .ApplyAsync(folders, emulationStation, CancellationToken.None)
                .ConfigureAwait(false)));
        }

        if (_session.Store.Settings.GetInt64(SettingStore.ContentMaxBytes) is { } cap)
        {
            ran.Add(SyncPass.Budget);
            progress.Report(new BudgetReported(planner.ManagedBytes(), cap));
        }

        var state = outcome.Rejected ? SyncState.Rejected
            : outcome.Stopped ? SyncState.Stopped
            : outcome.Content.Failed > 0 ? SyncState.Incomplete
            : outcome.Content.Blocked > 0 ? SyncState.Blocked
            : SyncState.Done;

        return new SyncReport(state, ran);
    }

    /// <summary>
    /// Fetches the firmware every folder in this sync needs, before any of its ROMs.
    /// </summary>
    /// <remarks>
    /// Never fatal. A BIOS RomM does not have is the ordinary case this reports rather than an
    /// error, and a firmware pass that fails outright must not stop the ROMs it was ordered in
    /// front of: the same sync run tomorrow will try again, and the report already says what is
    /// missing.
    /// <para>
    /// The folders come from the membership rather than from the plan, because a set whose
    /// games are all present still needs its BIOS to be.
    /// </para>
    /// </remarks>
    private async Task FetchBiosAsync(
        IReadOnlyList<SyncSetDefinition> sets,
        RomMConnection? connection,
        FilesystemLimits limits,
        SyncOptions options,
        IProgress<SyncEvent> progress,
        CancellationToken cancellationToken)
    {
        var wanted = sets
            .SelectMany(set => _session.Store.SyncSets.Members(set.Id))
            .Select(member => member.Folder)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (wanted.Count == 0)
        {
            return;
        }

        IReadOnlyDictionary<string, FirmwareRow>? candidates = null;

        if (connection is not null)
        {
            var (index, problem) = await BiosCandidates
                .ReadAsync(connection, cancellationToken)
                .ConfigureAwait(false);

            if (problem is not null)
            {
                progress.Report(new BiosProblem(problem));
            }

            candidates = index;
        }

        var plan = new BiosPlanner(_session.Install, _session.Store, limits: limits).Plan(wanted, candidates);

        if (plan.Steps.Count == 0)
        {
            return;
        }

        progress.Report(new BiosPlanned(plan));

        // IsNoOp rather than DownloadCount: a plan that only adopts still has rows to write,
        // and an offline pass can adopt without a connection.
        if (options.DryRun || plan.IsNoOp)
        {
            return;
        }

        progress.Report(new BiosApplied(await new BiosSync(_session.Install, _session.Store, connection)
            .ApplyAsync(plan, cancellationToken: cancellationToken)
            .ConfigureAwait(false)));
    }
}

/// <summary>
/// An <see cref="IProgress{T}"/> that calls rather than posts.
/// </summary>
/// <remarks>
/// <b>Ordering is the whole reason this exists.</b> <c>System.Progress&lt;T&gt;</c> captures the
/// current <see cref="SynchronizationContext"/> and posts to it, and a console process has
/// none, so every report lands on the thread pool in whatever order it gets there. Anything
/// printing a sequence of passes needs them in the order they happened.
/// </remarks>
public sealed class Immediate<T> : IProgress<T>
{
    private readonly Action<T> _report;

    public Immediate(Action<T> report)
    {
        ArgumentNullException.ThrowIfNull(report);
        _report = report;
    }

    public void Report(T value) => _report(value);
}
