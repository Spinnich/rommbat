using RomMBat.Core.Content;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Core;

namespace RomMBat.Agent.Commands;

/// <summary>
/// <c>evict</c>: free space by removing content, showing what would go before anything goes.
/// </summary>
/// <remarks>
/// <b>A dry run by default, and deleting takes <c>--apply</c>.</b> This is the only command in
/// the agent that destroys anything, and the plan requires it to be a first-class operation
/// with a dry run rather than a side effect of syncing. So a sync that runs out of budget stops
/// and says so, and removing something is always a separate decision a person makes.
/// <para>
/// Two things are never removed, whatever the budget says: a file RomMBat did not download, and
/// a game whose saves have not reached the server. See <see cref="SaveGuard"/>.
/// </para>
/// </remarks>
internal static class EvictCommand
{
    public static async Task<int> RunAsync(CommandLine command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var context = AgentContext.Open(command, Console.Error, out var exitCode);
        if (context is null)
        {
            return exitCode;
        }

        // Before anything is planned, because SaveGuard reads local_save and local_state and
        // nothing else here refreshes either. Without this the guard answers from the last
        // flush: play a game, evict, and the ROM goes while the save it just wrote is still
        // unsent. The save then survives as bytes with no ROM to attribute it to, which is
        // permanent.
        //
        // Both tables, not just the first. A save state is save data the guard refuses on, so
        // scanning one and not the other leaves exactly the stale-table gap for states that
        // this line exists to close for battery saves.
        //
        // States first, because the sidecar attribution route reads local_state and SaveScanner
        // is what runs it. Scanning saves first meant the route saw an empty table on any tree
        // whose states had not been recorded yet, so a class C unit went unattributed until a
        // second invocation. See #64.
        var schema = StateScanner.LoadSchema(context.Install);

        if (schema is not null)
        {
            new StateScanner(context.Install, context.Store, schema).Scan();
        }

        new SaveScanner(context.Install, context.Store, states: schema).Scan();

        var planner = new EvictionPlanner(context.Store);
        var requested = ByteSize.Parse(command.Value("bytes"));
        var plan = planner.Plan(requested);

        // Reported before the budget question and applied after it, because these bytes are the
        // ones neither bound can see: they carry no local_file row, so the budget does not count
        // them, and they are gone from the volume's free space attributed to nothing. An install
        // inside its budget with dead transfers under partial/ has nothing to evict and space to
        // reclaim, which is exactly the case the old early return walked away from.
        var sweep = new PartialSweep(context.Install, context.Store);
        var abandoned = sweep.Plan();

        if (!abandoned.IsEmpty)
        {
            Console.WriteLine(abandoned.Summary);

            foreach (var candidate in abandoned.Candidates)
            {
                Console.WriteLine(
                    $"  {ByteSize.Format(candidate.SizeBytes),10}  {Describe(candidate)}  {candidate.Name}");
            }

            Console.WriteLine();
        }

        if (plan.BytesToFree <= 0)
        {
            var budget = context.Store.Settings.GetInt64(SettingStore.ContentMaxBytes);
            Console.WriteLine(budget is null
                ? "No disk budget is set, so nothing is over it. Set one with 'budget --max 64GB'."
                : plan.Summary);

            return Finish(command, sweep, abandoned);
        }

        Console.WriteLine(plan.Summary);
        Console.WriteLine();

        foreach (var candidate in plan.Selected)
        {
            // The media count is shown because it is where the surprise is: a game whose ROM
            // is 128 KB can be carrying 3 MB of artwork out with it.
            var media = candidate.Media.Count > 0
                ? $" (+{candidate.Media.Count} media)"
                : string.Empty;

            Console.WriteLine(
                $"  {ByteSize.Format(candidate.Bytes),10}  {Describe(candidate)}  {candidate.File.FileName}{media}");
        }

        foreach (var candidate in plan.Refused)
        {
            Console.WriteLine($"  {"kept",10}  {candidate.File.FileName}: {candidate.Refusal}");
        }

        if (plan.IsShort)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"This frees {ByteSize.Format(plan.BytesFreed)} of the "
                    + $"{ByteSize.Format(plan.BytesToFree)} needed. Raise the budget, or remove a sync set.");
        }

        if (!command.Has("apply"))
        {
            Console.WriteLine();
            Console.WriteLine("Nothing was removed. Run 'evict --apply' to carry this out.");
            return ExitCode.Ok;
        }

        Sweep(sweep, abandoned);

        var outcome = planner.Apply(plan, context.Install);

        Console.WriteLine();
        Console.WriteLine(outcome.Summary);

        foreach (var problem in outcome.Problems)
        {
            Console.Error.WriteLine($"  {problem}");
        }

        // A gamelist that still names a removed game is inert, since EmulationStation does not
        // list an entry whose file is missing, but it survives ES's own rewrite and would sit
        // there forever. This needs no server: the entry and its metadata are both local.
        if (outcome.FoldersToRewrite.Count > 0)
        {
            var gamelists = new GamelistSync(context.Install, context.Store);
            using var emulationStation = new EmulationStationClient();

            var written = await gamelists
                .ApplyAsync(outcome.FoldersToRewrite, emulationStation, cancellationToken)
                .ConfigureAwait(false);

            Console.WriteLine();
            GamelistCommand.Report(written);
        }

        return ExitCode.Ok;
    }

    /// <summary>
    /// The dry-run exit, which still has the sweep to do.
    /// </summary>
    /// <remarks>
    /// Taken when nothing is over budget, which is the ordinary case and the one where dead
    /// transfers would otherwise sit forever: eviction has nothing to plan, so without this the
    /// command returns having reported bytes it then declines to reclaim.
    /// </remarks>
    private static int Finish(CommandLine command, PartialSweep sweep, PartialSweepPlan abandoned)
    {
        if (abandoned.IsEmpty)
        {
            return ExitCode.Ok;
        }

        if (!command.Has("apply"))
        {
            Console.WriteLine();
            Console.WriteLine("Nothing was removed. Run 'evict --apply' to reclaim these.");
            return ExitCode.Ok;
        }

        Console.WriteLine();
        Sweep(sweep, abandoned);
        return ExitCode.Ok;
    }

    private static void Sweep(PartialSweep sweep, PartialSweepPlan abandoned)
    {
        if (abandoned.IsEmpty)
        {
            return;
        }

        var swept = sweep.Apply(abandoned);
        Console.WriteLine(swept.Summary);

        foreach (var problem in swept.Problems)
        {
            Console.Error.WriteLine($"  {problem}");
        }
    }

    private static string Describe(PartialCandidate candidate) => candidate.Reason switch
    {
        PartialReason.Unclaimed => "no set wants it",
        _ => "transfer died",
    };

    private static string Describe(EvictionCandidate candidate) => candidate.Reason switch
    {
        EvictionReason.Departed => $"left {candidate.SetName ?? "its set"}",
        EvictionReason.Orphaned => "in no set",
        _ => candidate.Position is { } position
            ? $"#{position} in {candidate.SetName}"
            : $"in {candidate.SetName}",
    };
}
