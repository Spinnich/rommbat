using RomM.Client;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;

namespace RomMBat.Agent.Commands;

/// <summary>
/// <c>flush</c>: one pass over everything waiting, then exit.
/// </summary>
/// <remarks>
/// <b>This is a printer over <see cref="SaveFlushService"/>.</b> The passes, their order and the
/// argument for each live there, because the gamepad UI runs the same flush and two
/// implementations of what a flush does would drift. What is left here is <c>--quiet</c>, the
/// conflict block, and the mapping from an outcome to an exit code.
/// <para>
/// <b>What invokes a flush today is <c>sync</c>, which runs it before anything else, a person
/// typing <c>flush</c>, and the detached <c>background</c> pass that <c>start</c> and
/// <c>quit</c> spawn.</b> The <c>game-start</c> and <c>game-end</c> hooks write a spool file and
/// exit without starting a process, because they run inside the game-launch path.
/// </para>
/// <para>
/// <b>Every sentence here that names a subcommand stays here.</b> "Pick a side with
/// <c>rommbat-agent saves resolve</c>" would be false on the other front end, which is why the
/// conflict block is the caller's and only the rows are Core's.
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

        var quiet = command.Has("quiet");
        var offline = command.Has("offline");
        var exitCode = ExitCode.Ok;
        RomMConnection? connection = null;

        // Buffered, not written straight out. Authenticating has to happen before the service
        // runs, because the service takes the connection; the refusal still belongs after the
        // local half's own lines, which is where it has always appeared. Without this the
        // stderr sentence overtakes the stdout it used to follow.
        using var refusal = new StringWriter();

        try
        {
            if (!offline)
            {
                // Authenticating is the agent's, because it reads --passphrase off this command
                // line and its refusal maps to an exit code the service cannot know.
                connection = context.Authenticate(command, refusal, out exitCode);
            }

            var report = await new SaveFlushService(context.Session)
                .RunAsync(new FlushOptions(offline), connection, cancellationToken)
                .ConfigureAwait(false);

            return Show(report, quiet, exitCode, refusal.ToString());
        }
        finally
        {
            connection?.Dispose();
        }
    }

    /// <summary>
    /// Turns one report into the lines the console has always written.
    /// </summary>
    /// <param name="authExitCode">
    /// What authenticating said, which is the exit code a refusal maps to. The service reports
    /// <see cref="FlushState.NotPaired"/> either way and cannot tell an expired token from an
    /// install that was never paired.
    /// </param>
    /// <param name="authRefusal">
    /// Why authenticating failed, held back so it prints where it always has: after the passes
    /// that needed no server.
    /// </param>
    private static int Show(FlushReport report, bool quiet, int authExitCode, string authRefusal)
    {
        if (report.State == FlushState.Skipped)
        {
            Console.WriteLine("Another flush is already running. Nothing to do.");
            return ExitCode.Ok;
        }

        if (report.Drained is { IsNoOp: false } drained && !quiet)
        {
            Console.WriteLine($"hooks: {drained.Ingested} events read"
                + (drained.Malformed > 0 ? $", {drained.Malformed} unrecognised and discarded" : string.Empty)
                + (drained.Unreadable > 0
                    ? $", {drained.Unreadable} written by a newer hook and kept for a newer agent"
                    : string.Empty)
                + (drained.Abandoned > 0 ? $", {drained.Abandoned} abandoned" : string.Empty));
        }

        if (report.Correlated is { IsNoOp: false } correlated && !quiet)
        {
            Console.WriteLine($"play: {correlated.Sessions} sessions"
                + (correlated.Orphans > 0
                    ? $", {correlated.Orphans} discarded ({correlated.MenuLaunches} menu launches)"
                    : string.Empty)
                + (correlated.Unresolved > 0 ? $", {correlated.Unresolved} still running" : string.Empty));
        }

        if (!quiet)
        {
            Console.WriteLine(report.Saves!.Summary);

            if (report.States is { } states)
            {
                Console.WriteLine(states.Summary);

                foreach (var miss in states.NearMisses)
                {
                    // One line here rather than the two `saves` prints: a flush is not the
                    // report, and the point is that the file stops being invisible.
                    Console.WriteLine($"  {miss.FileName}: {miss.Detail}");
                }
            }
            else
            {
                // The file ships with RetroBat, so its absence is a real fact about this install
                // rather than a case to pass over quietly.
                Console.WriteLine("states: es_savestates.cfg is not in this install, so none were looked for");
            }
        }

        switch (report.State)
        {
            case FlushState.LocalOnly:
                ReportQueued(report, quiet);
                return ExitCode.Ok;

            case FlushState.NotPaired:
                if (report.Problem is { } problem)
                {
                    Console.Error.WriteLine(problem);
                    return ExitCode.NotPaired;
                }

                Console.Error.Write(authRefusal);

                // Anything queued is safe and stays queued.
                ReportQueued(report, quiet);
                return authExitCode;

            case FlushState.Unreachable:
                // Offline is a working state and the headline feature of this milestone.
                Console.WriteLine(report.Problem);
                ReportQueued(report, quiet: false);
                return ExitCode.Ok;

            default:
                break;
        }

        var playtime = report.Playtime!;

        if (!quiet || playtime.Failed > 0)
        {
            Console.WriteLine(playtime.Summary);
        }

        foreach (var line in playtime.Problems)
        {
            Console.Error.WriteLine($"  {line}");
        }

        var saves = report.SavesSent!;

        if (!quiet || saves.Failed > 0 || saves.Conflicts > 0)
        {
            Console.WriteLine(saves.Summary);
        }

        foreach (var line in saves.Problems)
        {
            Console.Error.WriteLine($"  {line}");
        }

        var sentStates = report.StatesSent!;

        if (!quiet || sentStates.Failed > 0)
        {
            Console.WriteLine(sentStates.Summary);
        }

        foreach (var line in sentStates.Problems)
        {
            Console.Error.WriteLine($"  {line}");
        }

        ReportConflicts(report.Conflicts);

        return report.State == FlushState.Partial ? ExitCode.Partial : ExitCode.Ok;
    }

    private static void ReportQueued(FlushReport report, bool quiet)
    {
        if (report.Queued > 0 && !quiet)
        {
            Console.WriteLine($"{report.Queued} items are waiting to go up. They will on the next flush.");
        }
    }

    /// <summary>
    /// Says what the user has to choose between, which a 409 body cannot.
    /// </summary>
    /// <remarks>
    /// Measured, the conflict body is a bare sentence with no save id and no timestamps, so
    /// everything shown here comes from the negotiate operation and the local row instead.
    /// <para>
    /// The rows are every open conflict rather than this pass's, so one found by an earlier
    /// flush and never resolved is still reported. Stage 1 printed the in-memory list once and
    /// a user who looked away lost the only record of it.
    /// </para>
    /// </remarks>
    private static void ReportConflicts(IReadOnlyList<SaveConflictRecord> conflicts)
    {
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
