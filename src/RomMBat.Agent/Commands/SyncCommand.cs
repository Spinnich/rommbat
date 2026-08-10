using RomMBat.Core;
using System.Globalization;
using RomM.Client;
using RomMBat.Core.Content;
using RomMBat.Core.Store;

namespace RomMBat.Agent.Commands;

/// <summary>
/// <c>sync</c>: turn a resolved set into files in the RetroBat tree.
/// </summary>
/// <remarks>
/// Three passes, in this order and for a reason. The set is re-resolved, because
/// smart-collection membership drifts server-side and fetching a stale membership downloads
/// games the set no longer contains. Then a plan is worked out and printed, because being told
/// what is about to happen is worth more than a progress bar. Then, unless this is a dry run,
/// the plan is carried out.
/// <para>
/// <c>--dry-run</c> and <c>--offline</c> both work with the server unreachable: the plan is
/// made from the membership already in the store, so a handheld away from the network can still
/// answer "what would this sync do".
/// </para>
/// </remarks>
internal static class SyncCommand
{
    public static async Task<int> RunAsync(CommandLine command, CancellationToken cancellationToken)
    {
        using var context = AgentContext.Open(command, Console.Error, out var exitCode);
        if (context is null)
        {
            return exitCode;
        }

        var sets = SetsCommand.Select(context, command.Positional.Count > 0 ? command.Positional[0] : null);
        if (sets is null)
        {
            return ExitCode.Usage;
        }

        var dryRun = command.Has("dry-run");
        var offline = command.Has("offline");
        var limits = FilesystemLimits.Inspect(context.Install.RootPath);

        ReportFilesystem(limits);

        RomMConnection? connection = null;

        try
        {
            if (!offline)
            {
                connection = context.Authenticate(command, Console.Error, out exitCode);
                if (connection is null)
                {
                    return exitCode;
                }

                if (!command.Has("no-resolve"))
                {
                    var resolved = await SetsCommand
                        .ResolveSetsAsync(context, connection, sets, cancellationToken)
                        .ConfigureAwait(false);

                    if (resolved != ExitCode.Ok)
                    {
                        return resolved;
                    }

                    // Re-read: resolution rewrote both the definitions and the membership.
                    sets = [.. sets.Select(set => context.Store.SyncSets.Find(set.Name) ?? set)];
                }
            }

            var planner = new ContentPlanner(context.Install, context.Store, limits);
            var worst = ExitCode.Ok;

            foreach (var set in sets)
            {
                var members = context.Store.SyncSets.Members(set.Id);
                var plan = planner.Plan(set, members);

                Console.WriteLine();
                Console.WriteLine(set.Name);
                Console.WriteLine($"  plan: {plan.Summary}");
                Report(plan);

                if (dryRun || offline)
                {
                    if (offline && plan.Downloads.Any())
                    {
                        Console.WriteLine("  (offline, so nothing was fetched)");
                    }

                    continue;
                }

                if (connection is null)
                {
                    continue;
                }

                var sync = new ContentSync(context.Install, context.Store, connection);
                var outcome = await sync
                    .ApplyAsync(plan, new Progress<ContentSyncProgress>(Show), cancellationToken)
                    .ConfigureAwait(false);

                ClearProgressLine();
                Console.WriteLine($"  done: {outcome.Summary}");

                foreach (var problem in outcome.Problems)
                {
                    Console.Error.WriteLine($"  {problem}");
                }

                if (outcome.Failed > 0)
                {
                    worst = Math.Max(worst, ExitCode.Offline);
                }
            }

            ReportBudget(context, planner);
            return worst;
        }
        finally
        {
            connection?.Dispose();
        }
    }

    /// <summary>Prints what the plan would do, one line per game that is not already present.</summary>
    private static void Report(ContentPlan plan)
    {
        foreach (var step in plan.Steps.Where(step => step.Action != ContentAction.AlreadyPresent))
        {
            var action = step.Action switch
            {
                ContentAction.Download => $"download {ByteSize.Format(step.BytesToTransfer)}",
                ContentAction.Resume => $"resume, {ByteSize.Format(step.BytesToTransfer)} left",
                ContentAction.Adopt => "adopt",
                _ => "blocked",
            };

            var reason = step.Reason is { } text ? $" ({text})" : string.Empty;
            Console.WriteLine($"    {action,-28} {step.Member.DisplayName}{reason}");
        }
    }

    private static void ReportFilesystem(FilesystemLimits limits)
    {
        if (limits.MaximumFileSizeBytes is not { } maximum)
        {
            return;
        }

        // Said up front rather than per game, because on a FAT32 stick it explains every
        // refusal that follows, and the operating system's own message for it is misleading.
        Console.WriteLine(
            $"This drive is formatted {limits.Format}, which cannot hold a file larger than "
                + $"{ByteSize.Format(maximum)}. Larger games are left out of every set.");
    }

    private static void ReportBudget(AgentContext context, ContentPlanner planner)
    {
        var budget = context.Store.Settings.GetInt64(SettingStore.ContentMaxBytes);
        if (budget is not { } cap)
        {
            return;
        }

        var used = planner.ManagedBytes();
        Console.WriteLine();
        Console.WriteLine(
            $"Budget: {ByteSize.Format(used)} of {ByteSize.Format(cap)} used"
                + (used > cap ? $", {ByteSize.Format(used - cap)} over. Run 'evict' to see what would go." : "."));
    }

    private static void Show(ContentSyncProgress progress)
    {
        if (progress.Progress is not { } transfer)
        {
            return;
        }

        var percent = transfer.Fraction is { } fraction
            ? (fraction * 100).ToString("0", CultureInfo.InvariantCulture) + "%"
            : ByteSize.Format(transfer.Position);

        // One line, rewritten, because a download of forty games should not scroll a console.
        Console.Write(
            $"\r    [{progress.Index}/{progress.Total}] {percent,5} {Trim(progress.Step.Member.DisplayName, 48)}   ");
    }

    private static void ClearProgressLine() => Console.Write("\r" + new string(' ', 78) + "\r");

    private static string Trim(string value, int width) =>
        value.Length <= width ? value : value[..(width - 1)] + "…";
}
