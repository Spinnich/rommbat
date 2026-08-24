using RomM.Client;
using RomMBat.Core.Content;
using RomMBat.Core.Sync;

namespace RomMBat.Agent.Commands;

/// <summary>
/// <c>flush</c>: one pass over everything waiting, then exit.
/// </summary>
/// <remarks>
/// <b>This is the whole of RomMBat's background work, and it has no daemon to live in.</b> A
/// portable install cannot register a service or a scheduled task, so the design is one short
/// pass that anything can invoke, then exit.
/// <para>
/// <b>What invokes it today is <c>sync</c>, which runs it before anything else, and a person
/// typing <c>flush</c>.</b> Nothing else does. The hooks write a spool file and exit without
/// starting a process, and the UI that would drive this while it runs is M7. So an install that
/// is never synced accumulates spool files and sends nothing, which is the stage-1 cut rather
/// than a fault: draining is idempotent and a spool file waits indefinitely.
/// </para>
/// <para>
/// <b>The local half always runs and the network half is optional.</b> Draining the spool,
/// correlating play sessions and rescanning saves all happen with the server unreachable, so
/// an offline flush still moves work forward and leaves less for the next one. Only the
/// sending needs a link.
/// </para>
/// <para>
/// <b>Failing to take the lock is success.</b> Two of these can overlap whenever a person runs
/// <c>flush</c> beside a <c>sync</c>, and once hooks invoke it the measured case of three
/// <c>game-end</c> hooks in flight at once applies. The second and third exit rather than wait,
/// because the work is already being done and waiting would put a process to sleep inside the
/// game-launch path.
/// </para>
/// </remarks>
internal static class FlushCommand
{
    public static async Task<int> RunAsync(CommandLine command, CancellationToken cancellationToken)
    {
        using var context = AgentContext.Open(command, Console.Error, out var exitCode);
        if (context is null)
        {
            return exitCode;
        }

        return await RunAsync(context, command, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The same pass, against a store somebody else already opened.
    /// </summary>
    /// <remarks>
    /// <c>sync</c> calls this rather than re-entering the command, because opening a second
    /// connection to the same SQLite file from inside the first one's transaction is a
    /// deadlock waiting for a slow disk.
    /// </remarks>
    public static async Task<int> RunAsync(
        AgentContext context,
        CommandLine command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);

        var exitCode = ExitCode.Ok;

        using var held = TreeLock.TryAcquire(context.Install);

        if (held is null)
        {
            // Ordinary, not an error: another agent is draining the same queue.
            Console.WriteLine("Another flush is already running. Nothing to do.");
            return ExitCode.Ok;
        }

        var quiet = command.Has("quiet");

        // 1. The hooks wrote spool files and nothing else. Turn them into journal rows.
        var drained = new SpoolDrain(context.Install, context.Store).Drain();

        if (!drained.IsNoOp && !quiet)
        {
            Console.WriteLine($"hooks: {drained.Ingested} events read"
                + (drained.Malformed > 0 ? $", {drained.Malformed} unrecognised and discarded" : string.Empty)
                + (drained.Unreadable > 0
                    ? $", {drained.Unreadable} written by a newer hook and kept for a newer agent"
                    : string.Empty)
                + (drained.Abandoned > 0 ? $", {drained.Abandoned} abandoned" : string.Empty));
        }

        // 2. Pair each game-end with its launch, and queue what that produces.
        var correlated = new PlaytimeCorrelator(context.Install, context.Store).Correlate();

        if (!correlated.IsNoOp && !quiet)
        {
            Console.WriteLine($"play: {correlated.Sessions} sessions"
                + (correlated.Orphans > 0
                    ? $", {correlated.Orphans} discarded ({correlated.MenuLaunches} menu launches)"
                    : string.Empty)
                + (correlated.Unresolved > 0 ? $", {correlated.Unresolved} still running" : string.Empty));
        }

        // 3. Work out what is on disk. Local, and the thing eviction depends on.
        //
        // The state schema goes into both passes: the save scan needs it so it does not report
        // a state as unsyncable in the same run that uploads it.
        //
        // The state scan runs first. The sidecar attribution route reads local_state and
        // SaveScanner is what runs it, so scanning saves first left the route reading an empty
        // table on the first flush after an install is set up, and the class C saves it would
        // have attributed went up on the second flush instead (#64).
        var schema = StateScanner.LoadSchema(context.Install);

        var scannedStates = schema is null
            ? null
            : new StateScanner(context.Install, context.Store, schema).Scan();

        var scanned = new SaveScanner(context.Install, context.Store, states: schema).Scan();

        if (!quiet)
        {
            Console.WriteLine(scanned.Summary);
        }

        // 3b. And the states, which are found the same way and sent a different way.
        if (scannedStates is not null)
        {
            if (!quiet)
            {
                Console.WriteLine(scannedStates.Summary);

                foreach (var miss in scannedStates.NearMisses)
                {
                    // One line here rather than the two `saves` prints: a flush is not the
                    // report, and the point is that the file stops being invisible.
                    Console.WriteLine($"  {miss.FileName}: {miss.Detail}");
                }
            }
        }
        else if (!quiet)
        {
            // The file ships with RetroBat, so its absence is a real fact about this install
            // rather than a case to pass over quietly.
            Console.WriteLine("states: es_savestates.cfg is not in this install, so none were looked for");
        }

        if (command.Has("offline"))
        {
            ReportQueued(context, quiet);
            return ExitCode.Ok;
        }

        // 4. Everything above this line worked without a server. Only sending needs one.
        var connection = context.Authenticate(command, Console.Error, out exitCode);
        if (connection is null)
        {
            ReportQueued(context, quiet);

            // Not paired is a real refusal; anything queued is safe and stays queued.
            return exitCode;
        }

        using (connection)
        {
            var device = context.Store.Device.Read();

            if (device?.RomMDeviceId is not { } deviceId)
            {
                Console.Error.WriteLine("This install is paired but has no RomM device id. Pair again.");
                return ExitCode.NotPaired;
            }

            try
            {
                var playtime = await new OutboxFlush(context.Store, connection, deviceId)
                    .FlushPlaySessionsAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (!quiet || playtime.Failed > 0)
                {
                    Console.WriteLine(playtime.Summary);
                }

                foreach (var problem in playtime.Problems)
                {
                    Console.Error.WriteLine($"  {problem}");
                }

                var saves = await new SaveSync(context.Install, context.Store, connection, deviceId)
                    .RunAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (!quiet || saves.Failed > 0 || saves.Conflicts > 0)
                {
                    Console.WriteLine(saves.Summary);
                }

                foreach (var problem in saves.Problems)
                {
                    Console.Error.WriteLine($"  {problem}");
                }

                // States go last because they are the only part of this pass that cannot fail
                // in a way anyone has to act on: nothing negotiates, nothing conflicts, and an
                // unsent state is simply sent again next time.
                var states = await new StateSync(context.Install, context.Store, connection)
                    .RunAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (!quiet || states.Failed > 0)
                {
                    Console.WriteLine(states.Summary);
                }

                foreach (var problem in states.Problems)
                {
                    Console.Error.WriteLine($"  {problem}");
                }

                ReportConflicts(context);

                return saves.Failed > 0 || playtime.Failed > 0 || states.Failed > 0
                    ? ExitCode.Partial
                    : ExitCode.Ok;
            }
            catch (RomMUnreachableException ex)
            {
                // Offline is a working state and the headline feature of this milestone.
                // Everything stays queued and the next flush picks it up.
                Console.WriteLine($"The server is not reachable: {ex.Message}");
                ReportQueued(context, quiet: false);
                return ExitCode.Ok;
            }
        }
    }

    private static void ReportQueued(AgentContext context, bool quiet)
    {
        var pending = context.Store.Outbox.PendingCount();

        if (pending > 0 && !quiet)
        {
            Console.WriteLine($"{pending} items are waiting to go up. They will on the next flush.");
        }
    }

    /// <summary>
    /// Says what the user has to choose between, which a 409 body cannot.
    /// </summary>
    /// <remarks>
    /// Measured, the conflict body is a bare sentence with no save id and no timestamps, so
    /// everything shown here comes from the negotiate operation and the local row instead.
    /// <para>
    /// Read from the store rather than from this pass's outcome, so a conflict found by an
    /// earlier flush and never resolved is still reported. Stage 1 printed the in-memory list
    /// once and a user who looked away lost the only record of it.
    /// </para>
    /// </remarks>
    private static void ReportConflicts(AgentContext context)
    {
        var conflicts = context.Store.SaveConflicts.ListOpen();

        if (conflicts.Count == 0)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine("These saves changed in both places and nothing was overwritten:");

        foreach (var conflict in conflicts)
        {
            Console.WriteLine($"  rom {conflict.RomId}, slot {conflict.Slot}, since {conflict.FirstSeenAtUtc:u}");
            Console.WriteLine($"    here    {conflict.LocalPath}  {Short(conflict.LocalHash)}");
            Console.WriteLine($"    server  {Short(conflict.ServerHash)}  {conflict.ServerUpdatedAt:u}");

            if (conflict.LocalCopyPath is { } copy)
            {
                Console.WriteLine($"    a copy of the local file is at {copy}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Pick a side with: rommbat-agent saves resolve <rom> <slot> --keep-local | --keep-server");
    }

    private static string Short(string? hash) =>
        hash is null ? "(no hash)" : hash[..Math.Min(8, hash.Length)];
}
