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

        var planner = new EvictionPlanner(context.Store);
        var requested = ByteSize.Parse(command.Value("bytes"));
        var plan = planner.Plan(requested);

        if (plan.BytesToFree <= 0)
        {
            var budget = context.Store.Settings.GetInt64(SettingStore.ContentMaxBytes);
            Console.WriteLine(budget is null
                ? "No disk budget is set, so nothing is over it. Set one with 'budget --max 64GB'."
                : plan.Summary);

            return ExitCode.Ok;
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

    private static string Describe(EvictionCandidate candidate) => candidate.Reason switch
    {
        EvictionReason.Departed => $"left {candidate.SetName ?? "its set"}",
        EvictionReason.Orphaned => "in no set",
        _ => candidate.Position is { } position
            ? $"#{position} in {candidate.SetName}"
            : $"in {candidate.SetName}",
    };
}
