using RomM.Client;
using RomMBat.Core.Content;
using RomMBat.Core.Sync;

namespace RomMBat.Agent.Commands;

/// <summary>
/// <c>flush</c>: one pass over everything waiting, then exit.
/// </summary>
/// <remarks>
/// <b>This is the whole of RomMBat's background work, and it has no daemon to live in.</b> A
/// portable install cannot register a service or a scheduled task, so this is invoked from the
/// <c>start</c>, <c>game-end</c> and <c>quit</c> hooks, by <c>sync</c> before anything else,
/// and by the UI while it runs. One pass, then exit.
/// <para>
/// <b>The local half always runs and the network half is optional.</b> Draining the spool,
/// correlating play sessions and rescanning saves all happen with the server unreachable, so
/// an offline flush still moves work forward and leaves less for the next one. Only the
/// sending needs a link.
/// </para>
/// <para>
/// <b>Failing to take the lock is success.</b> Three <c>game-end</c> hooks in flight at once is
/// measured, not hypothetical, and each of them wakes one of these. The second and third exit
/// rather than wait, because the work is already being done and waiting would put a process to
/// sleep inside the game-launch path.
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
                + (drained.Malformed > 0 ? $", {drained.Malformed} unreadable" : string.Empty)
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
        var scanned = new SaveScanner(context.Install, context.Store).Scan();

        if (!quiet)
        {
            Console.WriteLine(scanned.Summary);
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

                ReportConflicts(saves);

                return saves.Failed > 0 || playtime.Failed > 0 ? ExitCode.Partial : ExitCode.Ok;
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
    /// </remarks>
    private static void ReportConflicts(SaveSyncOutcome outcome)
    {
        if (outcome.Unresolved.Count == 0)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine("These saves changed in both places and nothing was overwritten:");

        foreach (var conflict in outcome.Unresolved)
        {
            Console.WriteLine($"  rom {conflict.RomId}, slot {conflict.Slot}");
            Console.WriteLine($"    here    {conflict.LocalPath}  {Short(conflict.LocalHash)}");
            Console.WriteLine($"    server  {Short(conflict.ServerHash)}  {conflict.ServerUpdatedAt:u}");

            if (conflict.LocalCopy is { } copy)
            {
                Console.WriteLine($"    a copy of the local file is at {copy}");
            }
        }
    }

    private static string Short(string? hash) =>
        hash is null ? "(no hash)" : hash[..Math.Min(8, hash.Length)];
}
