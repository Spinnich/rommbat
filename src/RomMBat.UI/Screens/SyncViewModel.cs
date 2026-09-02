using System.Globalization;
using RomM.Client;
using RomMBat.Core;
using RomMBat.Core.Content;
using RomMBat.Core.Sets;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
using RomMBat.UI.Input;
using RomMBat.UI.Shell;

namespace RomMBat.UI.Screens;

/// <summary>Where a sync has got to.</summary>
public enum SyncStage
{
    /// <summary>Fetching.</summary>
    Working,

    /// <summary>Everything asked for happened.</summary>
    Done,

    /// <summary>Stopped by the user. The tree is correct, not postponed.</summary>
    Stopped,

    /// <summary>Something could not be fetched. The next run tries again.</summary>
    Incomplete,

    /// <summary>The disk budget is full, so some games were left out.</summary>
    Blocked,

    /// <summary>A set could not be resolved, so the run did not start.</summary>
    Refused,

    /// <summary>There is no pairing to sync against.</summary>
    NotPaired,

    /// <summary>The server refused this device part way through.</summary>
    Rejected,
}

/// <summary>
/// Everything the screen draws, as one value.
/// </summary>
/// <remarks>
/// <b>Because the events arrive on the thread pool.</b> A sync reports through
/// <see cref="IProgress{T}"/> from whatever thread is doing the transfer, while the drawing
/// thread reads these fields inside <c>Handle</c> and the renderer. Written as eight fields it
/// would publish a game name from one event beside a count from another, and a reader landing
/// between two writes would draw a mix that never existed. One reference assignment publishes
/// all of it, so a reader sees the value before or the value after.
/// <para>
/// #103's <c>c735636</c> fixed exactly this on <c>ListScreen</c>, where the rows and the cursor
/// were two fields and a loader finishing on the pool could leave the cursor indexing a shorter
/// list. <c>ListState</c>'s remarks carry the reasoning; this is the same rule on a screen with
/// far more moving parts.
/// </para>
/// </remarks>
/// <param name="Pass">Which of the ten passes is running, in words.</param>
/// <param name="Game">The game being fetched, or null between them.</param>
/// <param name="Problems">
/// Everything that went wrong, in arrival order. Accumulated rather than replaced: they are the
/// only part of a run a person cannot read back once it ends.
/// </param>
public sealed record SyncSnapshot(
    SyncStage Stage,
    string Detail,
    string? Pass = null,
    string? Game = null,
    int Done = 0,
    int Total = 0,
    long? BudgetUsed = null,
    long? BudgetCap = null,
    int Blocked = 0,
    long TransferredBytes = 0,
    long TotalBytes = 0,
    long GameTransferred = 0,
    long GameTotal = 0,
    double BytesPerSecond = 0,
    IReadOnlyList<string>? Problems = null)
{
    public IReadOnlyList<string> Problems { get; init; } = Problems ?? [];

    /// <summary>
    /// One word for how the run ended, or null while it is still going.
    /// </summary>
    /// <remarks>
    /// <b>Said outright rather than left to be inferred from a full progress bar.</b> A bar at
    /// the end and a bar that has stopped moving look identical, and the second is what a user
    /// fears. Incomplete gets its own word because "Finished" over a list of problems would be
    /// reporting a success the run did not have.
    /// </remarks>
    public string? Outcome => Stage switch
    {
        SyncStage.Working => null,
        SyncStage.Done => "Finished",
        SyncStage.Stopped => "Stopped",
        SyncStage.Incomplete => "Finished with problems",
        SyncStage.Blocked => "Stopped by the disk budget",
        _ => "Did not finish",
    };

    /// <summary>The count as a person reads it, or null before the first game.</summary>
    public string? Counted => Total > 0
        ? string.Create(CultureInfo.CurrentCulture, $"{Done:N0} of {Total:N0}")
        : null;

    /// <summary>
    /// How far through the whole run, by bytes, or null when there is nothing to transfer.
    /// </summary>
    /// <remarks>
    /// <b>The run rather than the game, because the game was unreadable.</b> A per-game bar over
    /// a set of small ROMs flashes from empty to full several times a second and tells nobody
    /// anything. Bytes rather than games, because the plan counts games and they are not the
    /// same size: forty Atari cartridges and one PS2 disc are both "1 of 2".
    /// </remarks>
    public double? Fraction => TotalBytes > 0
        ? Math.Clamp((double)TransferredBytes / TotalBytes, 0, 1)
        : null;

    /// <summary>What the run has taken against what it planned to.</summary>
    /// <remarks>
    /// <see cref="ByteSize.Progress"/> rather than two <c>Format</c> calls, so the unit is the
    /// destination's for the whole run and the width does not change eight times a second. See
    /// its remarks: a hands-on pass on a set of small ROMs read the reflow as double vision.
    /// </remarks>
    public string? Transferred => TotalBytes > 0
        ? ByteSize.Progress(TransferredBytes, TotalBytes)
        : null;

    /// <summary>How far through the game in front of the user, as text rather than a bar.</summary>
    public string? GameProgress => GameTotal > 0
        ? ByteSize.Progress(GameTransferred, GameTotal)
        : null;

    /// <summary>Current transfer rate, or null before there is enough to average.</summary>
    public string? Speed => BytesPerSecond > 0
        ? $"{ByteSize.Format((long)BytesPerSecond)}/s"
        : null;

    /// <summary>What the run took against what it may take, or null when no cap is set.</summary>
    public string? Budget => BudgetCap is { } cap && BudgetUsed is { } used
        ? ByteSize.Progress(used, cap)
        : BudgetUsed is { } taken
            ? ByteSize.Format(taken)
            : null;

    /// <summary>
    /// What the budget cost this run, or null when it cost nothing.
    /// </summary>
    /// <remarks>
    /// <b>Stated, and nothing is offered.</b> 7b-2b took eviction off the interface, and one of
    /// the two entry points it removed was the offer this screen used to make in its own footer,
    /// at the moment the user found out the budget had cut the run short. Removing the offer must
    /// not remove the fact: freeing space is theirs to do, by raising the budget or dropping a
    /// set, and they cannot decide either without being told a run ended early.
    /// <para>
    /// The reason arrives separately as a problem line, from <c>ContentPlanner</c>, which names
    /// the cap. This is the count, which no problem line carries.
    /// </para>
    /// </remarks>
    public string? Held => Blocked > 0
        ? $"{Blocked} {(Blocked == 1 ? "ROM was" : "ROMs were")} left out, the disk budget is full"
        : null;
}

/// <summary>
/// Syncing, while somebody watches.
/// </summary>
/// <remarks>
/// <b>The first minutes-long, network-bound, gigabyte-writing thing this interface has ever
/// done.</b> Resolving was minutes long and wrote two SQLite rows; this writes into the user's
/// tree, so what the screen owes them is different in kind: what it is doing now, how far
/// through it is, what it has spent, and what went wrong, all without scrolling.
/// <para>
/// <b>Fixed fields and an accumulating problem list, not a live tail.</b> Forty games in three
/// minutes is unreadable from a sofa, and the count already says how many went by. What cannot
/// be reconstructed afterwards is what failed, so that is the part that is kept.
/// </para>
/// <para>
/// <b>Back stops and stays; a second Back leaves.</b> The stop here destroys something, so a
/// screen that closed on the press would never be able to say what went. That also settles
/// #107, where the resolve screen's carefully-indexed stopped summary was written to a screen
/// that had already been popped: both screens answer Back the same way now, or a user learns
/// two rules.
/// </para>
/// <para>
/// <b>Every decision belongs to <see cref="LibrarySyncService"/>.</b> Which passes run, in what
/// order, what a stop removes, whether a rejection ends the run: all asked. What this file owns
/// is which words go on which line.
/// </para>
/// </remarks>
public sealed class SyncViewModel : IScreen, ILiveScreen, IDisposable
{
    /// <summary>How long the transfer rate is averaged over.</summary>
    /// <remarks>
    /// Between two reports on a fast link is milliseconds, and a rate taken over that swings by
    /// an order of magnitude report to report. A second is long enough to be steady and short
    /// enough that a stall is visible.
    /// </remarks>
    private static readonly TimeSpan RateWindow = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The shortest gap between two redraws caused by transfer progress.
    /// </summary>
    /// <remarks>
    /// <b>Because the screen was starving its own input.</b> Progress arrives per buffer read,
    /// which on a LAN is many times a second, and every one rebuilt the whole visual tree; a
    /// hands-on pass found the stop taking several presses to register during a large download.
    /// Anything a person acts on, which is the stage, the pass, a new game or a problem, is
    /// published at once regardless.
    /// </remarks>
    private static readonly TimeSpan RedrawGap = TimeSpan.FromMilliseconds(120);

    private readonly TimeProvider _clock = TimeProvider.System;
    private readonly InstallSession _session;
    private readonly IReadOnlyList<SyncSetDefinition> _sets;
    private readonly CancellationTokenSource _run = new();
    private readonly List<string> _problems = [];
    private readonly Func<IScreen>? _pair;

    /// <summary>The one game, when this screen is installing rather than syncing a set.</summary>
    private readonly SyncSetMember? _installing;

    /// <summary>
    /// Orders the writers of <see cref="_state"/> and of <see cref="_problems"/>.
    /// </summary>
    /// <remarks>
    /// One lock for both, so there is no order to get wrong: <c>Note</c> appends to the list and
    /// publishes it in the same breath. Held only across a record copy and a list append, never
    /// across a redraw or any I/O.
    /// </remarks>
    private readonly Lock _gate = new();

    private volatile SyncSnapshot _state =
        new(SyncStage.Working, "Working out what this device should hold...");

    private Task? _work;
    private bool _disposed;
    private bool _stopping;

    private long _sent;
    private long _inFlight;
    private long _planned;
    private long _sentBefore;
    private int _game = -1;
    private long _rateFrom;
    private long _rateBytes;
    private long _lastDrawn;

    /// <param name="connect">
    /// How the screen reaches the server. Taken so a test can stand a stub in its place, the
    /// way <see cref="ResolveViewModel"/> and <see cref="PairingViewModel"/> already do.
    /// </param>
    /// <param name="pair">
    /// Where pairing starts, for the one outcome a user can act on from here. Null leaves the
    /// offer off rather than opening a blank screen.
    /// </param>
    public SyncViewModel(
        InstallSession session,
        IReadOnlyList<SyncSetDefinition> sets,
        Func<Uri, RomMConnection>? connect = null,
        Func<IScreen>? pair = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(sets);

        _session = session;
        _sets = sets;
        _pair = pair;

        Start(connect);
    }

    /// <summary>Syncing a single set, which is what the detail screen asks for.</summary>
    public SyncViewModel(
        InstallSession session,
        SyncSetDefinition set,
        Func<Uri, RomMConnection>? connect = null,
        Func<IScreen>? pair = null)
        : this(session, [set], connect, pair)
    {
        ArgumentNullException.ThrowIfNull(set);
    }

    /// <summary>
    /// Installing one game, which is what a pick from browse asks for.
    /// </summary>
    /// <remarks>
    /// <b>A second construction shape rather than a mode.</b> Everything this screen draws and
    /// every rule it follows is the same: what it is doing now, how far through, what it spent,
    /// what went wrong, Back stops and stays, a second Back leaves. A flag would have meant
    /// every one of those reading "unless this is an install", where the only thing that
    /// actually differs is which Core method the run calls.
    /// <para>
    /// <b>No flush.</b> 7b-2b put it first in a whole-library run for eviction's benefit, and
    /// nothing here evicts. <see cref="LibrarySyncService.InstallAsync"/> owns which passes run
    /// and says why for each of the six it leaves out.
    /// </para>
    /// </remarks>
    public SyncViewModel(
        InstallSession session,
        SyncSetDefinition set,
        SyncSetMember member,
        Func<Uri, RomMConnection>? connect = null,
        Func<IScreen>? pair = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(member);

        _session = session;
        _sets = [set];
        _pair = pair;
        _installing = member;

        Start(connect);
    }

    public event EventHandler? Invalidated;

    /// <summary>
    /// What the screen is doing, in the tense it is doing it in.
    /// </summary>
    /// <remarks>
    /// Past tense once the work is over. The present tense is a claim that it is still running,
    /// and a finished run under a full bar is otherwise indistinguishable from a stuck one.
    /// See <see cref="ResolveViewModel.Title"/>, where a hands-on pass found it.
    /// </remarks>
    public string Title
    {
        get
        {
            var working = _state.Stage == SyncStage.Working;

            if (_installing is { } game)
            {
                return working ? $"Installing '{game.DisplayName}'" : $"Installed '{game.DisplayName}'";
            }

            return _sets.Count == 1
                ? working ? $"Syncing '{_sets[0].Name}'" : $"Synced '{_sets[0].Name}'"
                : working ? $"Syncing {_sets.Count} sync sets" : $"Synced {_sets.Count} sync sets";
        }
    }

    /// <summary>Everything the renderer draws, read once so it cannot change mid-draw.</summary>
    public SyncSnapshot State => _state;

    public IReadOnlyList<FooterHint> Hints => _state.Stage switch
    {
        // Says what the press costs. "Stop for now" is honest on the resolve screen because
        // nothing is lost there; this one drops the game it is in, and the label has to say so.
        SyncStage.Working => [new FooterHint(NavAction.Back, "Stop, and drop the game in progress")],

        SyncStage.NotPaired or SyncStage.Rejected when _pair is not null =>
        [
            new FooterHint(NavAction.Accept, "Pair with RomM"),
            new FooterHint(NavAction.Back, "Done"),
        ],

        // Only once there are more than the screen shows. Offering it for two problems that are
        // both already on screen is a press that appears to do nothing.
        _ when _state.Problems.Count > ProblemsShown =>
        [
            new FooterHint(NavAction.Accept, $"See all {_state.Problems.Count} problems"),
            new FooterHint(NavAction.Back, "Done"),
        ],

        // "Done" rather than "Back" once nothing is running. If the footer offers a stop the
        // work is going, and if it says Done it is over: one rule, and the only one a person
        // has to learn to know whether to keep waiting.
        _ => [new FooterHint(NavAction.Back, "Done")],
    };

    /// <summary>
    /// How many problems the run screen itself shows.
    /// </summary>
    /// <remarks>
    /// The renderer shows this many and the footer offers the rest, so the two have to agree:
    /// a screen showing six while the footer stays silent about twenty-seven is what a hands-on
    /// pass found, with no way to reach the other twenty-one.
    /// </remarks>
    public const int ProblemsShown = 6;

    /// <summary>
    /// Every problem the run reported, on a screen that scrolls.
    /// </summary>
    /// <remarks>
    /// <b>A <c>ListScreen</c> rather than a longer panel</b>, because it already windows and
    /// already carries 7b-2a's fix for the window that was shared across instances, and because
    /// a run that fails four hundred games cannot be a wall of text either way. Rows are
    /// unavailable: there is nothing to choose, and marking them so keeps Accept from promising
    /// a press that does nothing. <see cref="ListScreen.Reading"/> is what then lets the cursor
    /// walk them anyway, because a list of nothing but unavailable rows otherwise does not
    /// scroll.
    /// <para>
    /// Numbered oldest first, which is the order they happened. The run screen keeps the newest
    /// few for the opposite reason, that the tail says what was going on most recently.
    /// </para>
    /// </remarks>
    private static ListScreen AllProblems(IReadOnlyList<string> problems) =>
        new ListScreen(
            problems.Count == 1 ? "The problem" : $"{problems.Count} problems",
            [.. problems.Select((problem, index) => new ListRow(
                (index + 1).ToString(CultureInfo.CurrentCulture),
                null,
                problem,
                false))],
            _ => ScreenCommand.Stay,
            acceptLabel: string.Empty)
        {
            // Every row is unavailable, and on an ordinary list that means the cursor skips all
            // of them and never moves. A hands-on pass opened this and could not scroll.
            Reading = true,
        };

    public ScreenCommand Handle(NavAction action)
    {
        switch (action)
        {
            case NavAction.Accept when _pair is not null
                && _state.Stage is SyncStage.NotPaired or SyncStage.Rejected:
                return ScreenCommand.Push(_pair());

            case NavAction.Accept when _state.Problems.Count > ProblemsShown:
                return ScreenCommand.Push(AllProblems(_state.Problems));

            case NavAction.Back when _state.Stage == SyncStage.Working:
                // Stop and stay. The run removes the game it was in, and a screen that closed
                // on the press could never say which one that was.
                Stop();
                return ScreenCommand.Stay;

            case NavAction.Back:
                return ScreenCommand.Pop;

            default:
                return ScreenCommand.Stay;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Cancelled, never disposed. A run still unwinding registers on this token, and
        // disposing it would raise ObjectDisposedException on a thread nobody is watching.
        _run.Cancel();

        // Then wait, briefly. The run breaks out of its loop, rolls back the game it was in and
        // writes the gamelists, and the screen underneath is rebuilt the moment this returns.
        // Bounded because a screen that cannot be left is worse than one briefly out of date,
        // and a request already in flight is abandoned rather than waited on.
        try
        {
            _work?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // A run that ends by throwing its cancellation is an expected way out.
        }
    }

    /// <summary>Asks the run to stop, and says so at once rather than when it notices.</summary>
    /// <remarks>
    /// The press has to change the screen immediately: a stop that removes a part-fetched game
    /// takes as long as the delete, and a footer that still offers to stop reads as ignored.
    /// </remarks>
    private void Stop()
    {
        if (_stopping)
        {
            return;
        }

        _stopping = true;
        Publish(state => state with { Detail = "Stopping, and putting back the game in progress..." });
        _run.Cancel();
    }

    private void Start(Func<Uri, RomMConnection>? connect)
    {
        var attempt = _session.Authenticate();

        if (attempt.Connection is null)
        {
            Publish(_ => new SyncSnapshot(
                attempt.NotPaired ? SyncStage.NotPaired : SyncStage.Refused,
                attempt.Problem ?? "This install is not paired with a RomM server."));
            return;
        }

        var origin = _session.Store.Device.Read()?.ServerOrigin;
        var connection = connect is not null && origin is not null ? connect(origin) : attempt.Connection;

        if (!ReferenceEquals(connection, attempt.Connection))
        {
            attempt.Connection.Dispose();
        }

        _work = Task.Run(() => RunAsync(connection), CancellationToken.None);
    }

    private async Task RunAsync(RomMConnection connection)
    {
        try
        {
            var service = new LibrarySyncService(_session);

            var report = _installing is { } game
                ? await service
                    .InstallAsync(_sets[0], game, connection, new Immediate<SyncEvent>(Observe), _run.Token)
                    .ConfigureAwait(false)
                : await service
                    .RunAsync(
                        _sets,
                        new SyncOptions(),
                        connection,
                        new Immediate<SyncEvent>(Observe),
                        token => FlushAsync(connection, token),
                        _run.Token)
                    .ConfigureAwait(false);

            Settle(report);
        }
        catch (OperationCanceledException)
        {
            // The service returns a stop rather than throwing one, so reaching here means the
            // token fired somewhere that does not, which is still a stop from here.
            Publish(state => state with
            {
                Stage = SyncStage.Stopped,
                Detail = "Stopped. Everything that finished is on this device.",
                Pass = null,
                Game = null,
                GameTotal = 0,
                GameTransferred = 0,
            });
        }
        catch (RomMUnreachableException ex)
        {
            // Offline is a working state, so this is a sentence rather than an error screen.
            Publish(state => state with { Stage = SyncStage.Incomplete, Detail = ex.Message, Game = null });
        }
        finally
        {
            connection.Dispose();
        }
    }

    /// <summary>
    /// The saves flush, which the service runs before it fetches anything.
    /// </summary>
    /// <remarks>
    /// <b>The run's own connection, not a second one.</b> Authenticating again here would open
    /// a connection to whatever the store says the origin is, which is right in production and
    /// wrong everywhere a caller has supplied one: a test standing a stub in front of this
    /// screen watched the sync go to the stub and the flush go to the real address. The screen
    /// has exactly one server and it is the one it was given.
    /// <para>
    /// A refusal to take the tree lock is an ordinary outcome and is reported as a line rather
    /// than an error, which is what keeps this file from ever naming <c>TreeLock</c>.
    /// </para>
    /// </remarks>
    private async Task FlushAsync(RomMConnection connection, CancellationToken cancellationToken)
    {
        var report = await new SaveFlushService(_session)
            .RunAsync(new FlushOptions(), connection, cancellationToken)
            .ConfigureAwait(false);

        if (report.State == FlushState.Skipped)
        {
            Note("Saves were left to the pass already running.");
        }

        foreach (var problem in Problems(report))
        {
            Note(problem);
        }
    }

    private static IEnumerable<string> Problems(FlushReport report)
    {
        foreach (var problem in report.Playtime?.Problems ?? [])
        {
            yield return problem;
        }

        foreach (var problem in report.SavesSent?.Problems ?? [])
        {
            yield return problem;
        }

        foreach (var problem in report.StatesSent?.Problems ?? [])
        {
            yield return problem;
        }
    }

    /// <summary>Turns one reported event into the next value of the screen.</summary>
    private void Observe(SyncEvent reported)
    {
        switch (reported)
        {
            case FlushStarting:
                Publish(state => state with { Pass = "Sending saves and play time...", Game = null });
                break;

            case SetResolved(var resolve):
                Publish(state => state with { Pass = $"Asking RomM what '{resolve.SetName}' contains...", Game = null });
                break;

            case BiosPlanned or BiosApplied:
                Publish(state => state with { Pass = "Fetching firmware...", Game = null });
                break;

            case BiosProblem(var message):
                Note(message);
                break;

            case SetPlanned(var set, var plan):
                _planned = plan.BytesToTransfer;
                _sentBefore = _sent;

                Publish(state => state with
                {
                    Pass = _sets.Count == 1 ? "Downloading..." : $"Downloading '{set.Name}'...",
                    Total = plan.Steps.Count,
                    Done = 0,
                    TotalBytes = _sent + _planned,
                });
                break;

            case ContentProgressed(var step):
                Advance(step);
                break;

            case GameRolledBack(var title, var files, var bytes, var problems):
                Note($"{title} was not finished, so the {files} {(files == 1 ? "file" : "files")} "
                    + $"downloaded for it were removed ({ByteSize.Format(bytes)}).");

                foreach (var problem in problems)
                {
                    Note(problem);
                }

                break;

            case SetSynced(_, var outcome):
                foreach (var problem in outcome.Problems)
                {
                    Note(problem);
                }

                // Counted from the outcome, never from the total. A stopped run reports this
                // too, and setting it to the total told a person who had stopped after one game
                // that all forty-one had finished.
                Publish(state => state with
                {
                    Done = outcome.Downloaded + outcome.Resumed + outcome.Adopted + outcome.AlreadyPresent,
                    Game = null,
                    GameTotal = 0,
                    GameTransferred = 0,
                    Blocked = state.Blocked + outcome.Blocked,
                });
                break;

            case MediaProgressed(var what):
                Publish(state => state with { Pass = "Fetching artwork...", Game = what, GameTotal = 0, GameTransferred = 0 });
                break;

            case MediaApplied(var outcome):
                foreach (var problem in outcome.Problems)
                {
                    Note(problem);
                }

                break;

            case GamelistsWritten:
                // Called exactly as the agent calls it. Finding 233 measured that a reload
                // issued while RomMBat is in front of EmulationStation is deferred rather than
                // discarded, and that ES does not rescan on resume by itself, so the games
                // appear the moment the user leaves. Nothing here tells them to restart it.
                Publish(state => state with { Pass = "Telling EmulationStation...", Game = null });
                break;

            case BudgetReported(var used, var cap):
                Publish(state => state with { BudgetUsed = used, BudgetCap = cap });
                break;

            default:
                break;
        }
    }

    private void Settle(SyncReport report)
    {
        var (stage, detail) = report.State switch
        {
            // Answered by the service now (#114), where it used to be re-derived here from the
            // screen's own blocked count. A blocked ROM is not a failed one, so a run the cap
            // stopped dead came back as Done and this screen rendered it as "Everything in
            // these sync sets is on this device" over 386 games left out. Every future consumer
            // would have had to remember the same check, and this one had already forgotten it.
            Core.Sets.SyncState.Blocked => (
                SyncStage.Blocked,
                "The disk budget is full, so some games were left out. Raise the budget or make "
                    + "room, then sync again."),

            Core.Sets.SyncState.Done => (
                SyncStage.Done,
                _installing is { } game
                    ? $"'{game.DisplayName}' is on this device and EmulationStation has been told."
                    : "Everything in these sync sets is on this device."),

            Core.Sets.SyncState.Stopped => (
                SyncStage.Stopped,
                "Stopped. Everything that finished is on this device, and the game in progress was put back."),

            Core.Sets.SyncState.Rejected => (
                SyncStage.Rejected,
                "RomM would not accept this device. Pair again to sign back in. Your games, saves and "
                    + "settings are kept."),

            Core.Sets.SyncState.Refused => (
                SyncStage.Refused,
                "A sync set could not be resolved, so nothing was fetched."),

            _ => (SyncStage.Incomplete, "Some games could not be fetched. Syncing again picks up where this left off."),
        };

        // Pass and the game go with the run. Leaving them set told a person the sync was still
        // "Telling EmulationStation" after it had finished, which reads as a screen that has
        // not noticed it is done.
        Publish(state => state with
        {
            Stage = stage,
            Detail = detail,
            Pass = null,
            Game = null,
            GameTotal = 0,
            GameTransferred = 0,
        });
    }

    /// <summary>
    /// Folds one transfer report into the run's totals.
    /// </summary>
    /// <remarks>
    /// <b>The run's bytes are the sum of the games behind it plus the one in flight.</b>
    /// <c>ContentSyncProgress</c> reports a position within the current game and nothing about
    /// the run, so the total has to be carried here: <see cref="_sent"/> is everything finished
    /// games moved, and the game in front of the user contributes its own position on top.
    /// <para>
    /// The rate is averaged over a short window rather than taken between two reports, which on
    /// a fast link arrive milliseconds apart and produce a number that swings by an order of
    /// magnitude and reads as instability.
    /// </para>
    /// </remarks>
    private void Advance(ContentSyncProgress step)
    {
        var position = step.Progress?.Position ?? 0;
        var total = step.Progress?.TotalBytes ?? step.Step.Member.SizeBytes;

        if (step.Index != _game)
        {
            // A new game: whatever the last one moved is now behind us for good.
            _sent += _inFlight;
            _inFlight = 0;
            _game = step.Index;
        }

        _inFlight = Math.Max(_inFlight, position);

        var now = _clock.GetTimestamp();
        var moved = _sent + _inFlight;

        if (_rateFrom == 0)
        {
            _rateFrom = now;
            _rateBytes = moved;
        }

        var window = _clock.GetElapsedTime(_rateFrom, now);
        var rate = _state.BytesPerSecond;

        if (window >= RateWindow)
        {
            rate = (moved - _rateBytes) / window.TotalSeconds;
            _rateFrom = now;
            _rateBytes = moved;
        }

        Publish(state => state with
        {
            Game = step.Step.Member.DisplayName,

            // The index is the game being worked on, so the count of finished ones is one
            // behind it. A screen reading "40 of 40" while the last game is still transferring
            // is the thing that makes a progress display untrustworthy.
            Done = Math.Max(0, step.Index - 1),
            Total = step.Total,
            GameTransferred = _inFlight,
            GameTotal = total,
            TransferredBytes = moved,
            TotalBytes = Math.Max(state.TotalBytes, moved),
            BytesPerSecond = rate,
        });
    }

    /// <summary>Adds a problem, in arrival order, and never the same one twice in a row.</summary>
    private void Note(string problem)
    {
        lock (_gate)
        {
            if (_problems.Count > 0 && string.Equals(_problems[^1], problem, StringComparison.Ordinal))
            {
                return;
            }

            _problems.Add(problem);
            Publish(state => state with { Problems = [.. _problems] });
        }
    }

    /// <summary>
    /// Applies a change to the published value, and redraws unless the only change was more bytes.
    /// </summary>
    /// <remarks>
    /// One reference assignment, then a redraw the shell marshals off whatever thread the
    /// transfer is on. The value is always published; what is rate-limited is telling anybody
    /// about it, because the renderer rebuilds the whole panel and doing that on every buffer
    /// read left no time for the pad to be polled.
    /// <para>
    /// <b>A change rather than a finished value, because two threads write this field.</b>
    /// <c>volatile</c> makes the publish atomic, which is what <see cref="SyncSnapshot"/>'s
    /// remarks argue for and is what stops a torn read; it does nothing for the read-modify-write
    /// a <c>with</c> expression performs at the call site. <see cref="Stop"/> runs on the drawing
    /// thread and <c>Advance</c> on the transfer thread fires once per buffer read, so the window
    /// between one of them reading the field and assigning to it is hit often: the press would
    /// appear to do nothing until the stage changed, which is the exact symptom
    /// <see cref="Stop"/>'s immediate publish exists to prevent. The change is applied under the
    /// lock, so the last writer builds on the first one's value rather than on a stale copy.
    /// </para>
    /// </remarks>
    private void Publish(Func<SyncSnapshot, SyncSnapshot> change)
    {
        SyncSnapshot previous;
        SyncSnapshot next;

        lock (_gate)
        {
            previous = _state;
            next = change(previous);
            _state = next;
        }

        if (Interesting(previous, next))
        {
            _lastDrawn = _clock.GetTimestamp();
            Invalidated?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (_clock.GetElapsedTime(_lastDrawn) < RedrawGap)
        {
            return;
        }

        _lastDrawn = _clock.GetTimestamp();
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>True when something a person would act on changed, rather than only a count.</summary>
    private static bool Interesting(SyncSnapshot before, SyncSnapshot after) =>
        before.Stage != after.Stage
        || !string.Equals(before.Detail, after.Detail, StringComparison.Ordinal)
        || !string.Equals(before.Pass, after.Pass, StringComparison.Ordinal)
        || !string.Equals(before.Game, after.Game, StringComparison.Ordinal)
        || before.Problems.Count != after.Problems.Count
        || before.Blocked != after.Blocked
        || before.BudgetUsed != after.BudgetUsed;
}
