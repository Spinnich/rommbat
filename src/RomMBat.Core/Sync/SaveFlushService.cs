using RomM.Client;
using RomMBat.Core.Content;
using RomMBat.Core.Store;

namespace RomMBat.Core.Sync;

/// <summary>How a flush ended.</summary>
public enum FlushState
{
    /// <summary>Everything asked for happened.</summary>
    Done,

    /// <summary>
    /// Another pass held the tree lock, so nothing ran at all.
    /// </summary>
    /// <remarks>
    /// <b>An outcome rather than an error, and the reason it is a value is the whole point.</b>
    /// Two flushes overlap whenever somebody runs one beside a sync, and the second exits rather
    /// than waiting, because the work is already being done. A caller reports this and carries
    /// on; nothing about it is a failure. Same shape as <see cref="PartialSweepOutcome.Skipped"/>.
    /// </remarks>
    Skipped,

    /// <summary>Asked to stay offline. Everything local ran and nothing was sent.</summary>
    LocalOnly,

    /// <summary>There is no pairing to send through. Everything local still ran.</summary>
    NotPaired,

    /// <summary>The server was not there. Everything stays queued for the next pass.</summary>
    Unreachable,

    /// <summary>Sent, and something failed. The next pass tries it again.</summary>
    Partial,
}

/// <summary>What a flush was asked to do.</summary>
/// <param name="Offline">Do everything the local tree can answer, send nothing.</param>
public sealed record FlushOptions(bool Offline = false);

/// <summary>
/// What one flush did, in the order it did it.
/// </summary>
/// <remarks>
/// <b>Every member is null exactly when that pass did not run</b>, so a caller can print what
/// happened without being told separately what was reached. Nothing here is prose about a
/// front end: each outcome carries its own <c>Summary</c>, and the sentences that name a
/// subcommand stay with the caller that has one.
/// </remarks>
public sealed record FlushReport
{
    public required FlushState State { get; init; }

    /// <summary>The spool. Null only when the lock was held.</summary>
    public SpoolDrainOutcome? Drained { get; init; }

    public CorrelationOutcome? Correlated { get; init; }

    public SaveScanOutcome? Saves { get; init; }

    /// <summary>
    /// Null when <c>es_savestates.cfg</c> is not in this install, which is a real fact about
    /// the tree rather than a case to pass over: the file ships with RetroBat.
    /// </summary>
    public StateScanOutcome? States { get; init; }

    public OutboxFlushOutcome? Playtime { get; init; }

    public SaveSyncOutcome? SavesSent { get; init; }

    public StateSyncOutcome? StatesSent { get; init; }

    /// <summary>
    /// Every open conflict, not only the ones this pass found.
    /// </summary>
    /// <remarks>
    /// Read from the store rather than from this pass's outcome, so a conflict found by an
    /// earlier flush and never resolved is still reported. Stage 1 printed the in-memory list
    /// once and a user who looked away lost the only record of it.
    /// </remarks>
    public IReadOnlyList<SaveConflictRecord> Conflicts { get; init; } = [];

    /// <summary>How many items are still waiting to go up.</summary>
    public int Queued { get; init; }

    /// <summary>
    /// The sentence for a state that has one: the lock, a missing device id, an unreachable
    /// server.
    /// </summary>
    public string? Problem { get; init; }

    /// <summary>True when the sending half ran, whether or not all of it worked.</summary>
    public bool Sent => Playtime is not null;

    /// <summary>True when the local half ran, which is every state but <see cref="FlushState.Skipped"/>.</summary>
    public bool Scanned => Saves is not null;
}

/// <summary>
/// One pass over everything waiting, then done.
/// </summary>
/// <remarks>
/// <b>This is <c>FlushCommand</c>'s orchestration with the console taken out.</b> It composes
/// <see cref="SpoolDrain"/>, <see cref="PlaytimeCorrelator"/>, <see cref="StateScanner"/>,
/// <see cref="SaveScanner"/>, <see cref="OutboxFlush"/>, <see cref="SaveSync"/> and
/// <see cref="StateSync"/>; none of them is reimplemented and none of them is touched.
/// <para>
/// <b>This is the whole of RomMBat's background work, and it has no daemon to live in.</b> A
/// portable install cannot register a service or a scheduled task, so the design is one short
/// pass that anything can invoke, then exit. The hooks write a spool file and start nothing,
/// because <c>game-start</c> and <c>game-end</c> run inside the game-launch path; what drains
/// the spool is a <c>sync</c>, a <c>flush</c>, or the detached <c>background</c> pass that
/// <c>start</c> and <c>quit</c> spawn.
/// </para>
/// <para>
/// <b>Failing to take the lock is success.</b> Two of these overlap whenever a person runs a
/// flush beside a sync, and the measured case of three <c>game-end</c> hooks in flight at once
/// applies once anything invokes it from the launch path. The second and third return
/// <see cref="FlushState.Skipped"/> rather than waiting, because the work is already being done
/// and waiting would put a process to sleep inside the game-launch path. The lock is taken
/// here, and a refusal is a value: that is what keeps a front end from ever naming
/// <see cref="TreeLock"/>.
/// </para>
/// <para>
/// <b>The local half always runs and only sending needs a link.</b> Draining the spool,
/// correlating play sessions and rescanning saves and states all happen with the server
/// unreachable, so an offline flush still moves work forward and leaves less for the next one.
/// A caller that could not authenticate passes no connection and still gets the local half.
/// </para>
/// <para>
/// <b>The connection is a parameter rather than something this opens.</b> Authenticating reads
/// a passphrase off a command line and reports its refusal in that front end's words, and the
/// exit code it maps to is the agent's. Same shape as <c>LibrarySyncService.RunAsync</c>.
/// </para>
/// </remarks>
public sealed class SaveFlushService
{
    private readonly InstallSession _session;
    private readonly TimeProvider? _time;

    /// <param name="timeProvider">
    /// Handed to the two scanners. Taken so a test can assert that the state scan really ran
    /// before the save scan, which is otherwise only observable through a class C save whose
    /// attribution depends on it.
    /// </param>
    public SaveFlushService(InstallSession session, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _time = timeProvider;
    }

    /// <summary>Runs one pass.</summary>
    /// <param name="connection">
    /// Null to send nothing. A caller that could not authenticate passes null and gets
    /// <see cref="FlushState.NotPaired"/> with the local half done.
    /// </param>
    public async Task<FlushReport> RunAsync(
        FlushOptions options,
        RomMConnection? connection = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var install = _session.Install;
        var store = _session.Store;

        using var held = TreeLock.TryAcquire(install);

        if (held is null)
        {
            // Ordinary, not an error: another pass is draining the same queue.
            return new FlushReport
            {
                State = FlushState.Skipped,
                Problem = "Another flush is already running.",
            };
        }

        // 1. The hooks wrote spool files and nothing else. Turn them into journal rows.
        var drained = new SpoolDrain(install, store).Drain();

        // 2. Pair each game-end with its launch, and queue what that produces.
        var correlated = new PlaytimeCorrelator(install, store).Correlate();

        // 3. Work out what is on disk. Local, and the thing eviction depends on.
        //
        // The state schema goes into both passes: the save scan needs it so it does not report
        // a state as unsyncable in the same run that uploads it.
        //
        // The state scan runs first. The sidecar attribution route reads local_state and
        // SaveScanner is what runs it, so scanning saves first left the route reading an empty
        // table on the first flush after an install is set up, and the class C saves it would
        // have attributed went up on the second flush instead (#64).
        var schema = StateScanner.LoadSchema(install);

        var scannedStates = schema is null
            ? null
            : new StateScanner(install, store, schema, _time).Scan();

        var scanned = new SaveScanner(install, store, states: schema, timeProvider: _time).Scan();

        var local = new FlushReport
        {
            State = FlushState.Done,
            Drained = drained,
            Correlated = correlated,
            Saves = scanned,
            States = scannedStates,
            Conflicts = store.SaveConflicts.ListOpen(),
            Queued = store.Outbox.PendingCount(),
        };

        // 4. Everything above this line worked without a server. Only sending needs one.
        if (options.Offline)
        {
            return local with { State = FlushState.LocalOnly };
        }

        if (connection is null)
        {
            // Not paired is a real refusal; anything queued is safe and stays queued.
            return local with { State = FlushState.NotPaired };
        }

        if (store.Device.Read()?.RomMDeviceId is not { } deviceId)
        {
            return local with
            {
                State = FlushState.NotPaired,
                Problem = "This install is paired but has no RomM device id. Pair again.",
            };
        }

        try
        {
            var playtime = await new OutboxFlush(store, connection, deviceId)
                .FlushPlaySessionsAsync(cancellationToken)
                .ConfigureAwait(false);

            var saves = await new SaveSync(install, store, connection, deviceId)
                .RunAsync(cancellationToken)
                .ConfigureAwait(false);

            // States go last because they are the only part of this pass that cannot fail in a
            // way anyone has to act on: nothing negotiates, nothing conflicts, and an unsent
            // state is simply sent again next time.
            var states = await new StateSync(install, store, connection)
                .RunAsync(cancellationToken)
                .ConfigureAwait(false);

            var failed = saves.Failed > 0 || playtime.Failed > 0 || states.Failed > 0;

            return local with
            {
                State = failed ? FlushState.Partial : FlushState.Done,
                Playtime = playtime,
                SavesSent = saves,
                StatesSent = states,

                // Re-read after sending: a conflict this pass just found belongs in the report,
                // and the count queued has moved.
                Conflicts = store.SaveConflicts.ListOpen(),
                Queued = store.Outbox.PendingCount(),
            };
        }
        catch (RomMUnreachableException ex)
        {
            // Offline is a working state and the headline feature of M6. Everything stays
            // queued and the next flush picks it up.
            return local with
            {
                State = FlushState.Unreachable,
                Problem = $"The server is not reachable: {ex.Message}",
                Queued = store.Outbox.PendingCount(),
            };
        }
    }
}
