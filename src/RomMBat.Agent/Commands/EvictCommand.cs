using RomMBat.Core;
using RomMBat.Core.Content;
using RomMBat.Core.Sets;

namespace RomMBat.Agent.Commands;

/// <summary>
/// <c>evict</c>: free space by removing content, showing what would go before anything goes.
/// </summary>
/// <remarks>
/// <b>A preview by default, and deleting takes <c>--apply</c>.</b> This is the only command in
/// the agent that destroys anything, and the plan requires it to be a first-class operation
/// with a preview rather than a side effect of syncing. So a sync that runs out of budget stops
/// and says so, and removing something is always a separate decision a person makes.
/// <para>
/// Two things are never removed, whatever the budget says: a file RomMBat did not download, and
/// a game whose saves have not reached the server. See <see cref="SaveGuard"/>.
/// </para>
/// <para>
/// <b>This is a printer over <see cref="EvictionService"/>.</b> The scan order, the planning
/// and the sweep live there, because the gamepad UI needs the same sequence and two copies of
/// it would drift.
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

        var service = new EvictionService(context.Session);
        var report = service.Preview(ByteSize.Parse(command.Value("bytes")));

        // Reported before the budget question and applied after it, because these bytes are the
        // ones neither bound can see: they carry no local_file row, so the budget does not count
        // them, and they are gone from the volume's free space attributed to nothing.
        if (!report.Abandoned.IsEmpty)
        {
            Console.WriteLine(report.Abandoned.Summary);

            foreach (var candidate in report.Abandoned.Candidates)
            {
                Console.WriteLine(
                    $"  {ByteSize.Format(candidate.SizeBytes),10}  {EvictionService.Describe(candidate)}  {candidate.Name}");
            }

            Console.WriteLine();
        }

        if (report.Plan.BytesToFree <= 0)
        {
            Console.WriteLine(report.HasBudget
                ? report.Plan.Summary
                : "No disk budget is set, so nothing is over it. Set one with 'budget --max 64GB'.");

            return await FinishAsync(context, command, service, report, cancellationToken).ConfigureAwait(false);
        }

        Console.WriteLine(report.Plan.Summary);
        Console.WriteLine();

        foreach (var candidate in report.Plan.Selected)
        {
            // The media count is shown because it is where the surprise is: a game whose ROM
            // is 128 KB can be carrying 3 MB of artwork out with it.
            var media = candidate.Media.Count > 0 ? $" (+{candidate.Media.Count} media)" : string.Empty;

            Console.WriteLine(
                $"  {ByteSize.Format(candidate.Bytes),10}  {EvictionService.Describe(candidate)}  {candidate.File.FileName}{media}");
        }

        foreach (var candidate in report.Plan.Refused)
        {
            Console.WriteLine($"  {"kept",10}  {candidate.File.FileName}: {candidate.Refusal}");
        }

        if (report.Plan.IsShort)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"This frees {ByteSize.Format(report.Plan.BytesFreed)} of the "
                    + $"{ByteSize.Format(report.Plan.BytesToFree)} needed. Raise the budget, or remove a sync set.");
        }

        if (!command.Has("apply"))
        {
            Console.WriteLine();
            Console.WriteLine("Nothing was removed. Run 'evict --apply' to carry this out.");
            return ExitCode.Ok;
        }

        var applied = await service.ApplyAsync(report, cancellationToken).ConfigureAwait(false);

        ReportSweep(applied.Swept);

        if (applied.Evicted is { } evicted)
        {
            Console.WriteLine();
            Console.WriteLine(evicted.Summary);

            foreach (var problem in evicted.Problems)
            {
                Console.Error.WriteLine($"  {problem}");
            }
        }

        if (applied.Gamelists is { } gamelists)
        {
            Console.WriteLine();
            GamelistCommand.Report(gamelists);
        }

        return ExitCode.Ok;
    }

    /// <summary>
    /// The preview exit, which still has the sweep to do.
    /// </summary>
    /// <remarks>
    /// Taken when nothing is over budget, which is the ordinary case and the one where dead
    /// transfers would otherwise sit forever: eviction has nothing to plan, so without this the
    /// command returns having reported bytes it then declines to reclaim.
    /// </remarks>
    private static async Task<int> FinishAsync(
        AgentContext context,
        CommandLine command,
        EvictionService service,
        EvictionReport report,
        CancellationToken cancellationToken)
    {
        _ = context;

        if (report.Abandoned.IsEmpty)
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

        var applied = await service.ApplyAsync(report, cancellationToken).ConfigureAwait(false);
        ReportSweep(applied.Swept);

        return ExitCode.Ok;
    }

    private static void ReportSweep(PartialSweepOutcome? swept)
    {
        if (swept is null)
        {
            return;
        }

        // Skipped carries its own sentence: another RomMBat pass holds the tree lock, and the
        // next pass reclaims these. That is an ordinary outcome, not a failure.
        Console.WriteLine(swept.Summary);

        foreach (var problem in swept.Problems)
        {
            Console.Error.WriteLine($"  {problem}");
        }
    }
}
