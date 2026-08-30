using System.Globalization;
using RomM.Client;
using RomMBat.Core;
using RomMBat.Core.Content;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Sets;

namespace RomMBat.Agent.Commands;

/// <summary>
/// <c>sync</c>: turn a resolved set into files in the RetroBat tree.
/// </summary>
/// <remarks>
/// <b>This is a printer over <see cref="LibrarySyncService"/>.</b> The passes, their order and
/// the argument for it live there, because the gamepad UI runs the same sync and two
/// implementations of what a sync does would drift. What is left here is turning each reported
/// event into lines on a console.
/// <para>
/// <b>The hooks are installed on the first run, and said so.</b> Without them there is no
/// playtime and no launch window at all, so leaving them off by default would leave the
/// feature off for everyone who never reads the manual. Installing one adds a file beside the
/// scripts already there and changes nothing about how a game runs, unlike the memory card
/// options the opt-in rule was written for. <c>hooks uninstall</c> takes them back out.
/// </para>
/// <para>
/// <b>The ES menu entry goes in on the first run too, and that is a wider claim than the
/// hooks make.</b> A hook is invisible; a menu entry adds an item to the user's own front end.
/// It is installed anyway because it is the only route to RomMBat that does not need a
/// terminal, and a user who never opens one is exactly who it is for. What that costs is owed
/// back in candour: every path is named, and <c>menu uninstall</c> takes all of it out again.
/// </para>
/// <para>
/// <c>--dry-run</c> and <c>--offline</c> both work with the server unreachable: the plan is
/// made from the membership already in the store, so a handheld away from the network can still
/// answer "what would this sync do". The BIOS report is answerable that way too, from the
/// bundled manifest and what is on disk.
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

        var options = new SyncOptions(
            DryRun: command.Has("dry-run"),
            Offline: command.Has("offline"),
            NoResolve: command.Has("no-resolve"),
            Passphrase: command.Value("passphrase"));

        RomMConnection? connection = null;

        try
        {
            if (!options.Offline)
            {
                connection = context.Authenticate(command, Console.Error, out exitCode);
                if (connection is null)
                {
                    return exitCode;
                }
            }

            var report = await new LibrarySyncService(context.Session)
                .RunAsync(
                    sets,
                    options,
                    connection,
                    // Immediate rather than System.Progress: the latter posts to a
                    // synchronization context a console does not have, so every line would
                    // land on the thread pool in whatever order it got there.
                    new Immediate<SyncEvent>(Printer.Show),
                    token => FlushSavesAsync(context, command, token),
                    cancellationToken)
                .ConfigureAwait(false);

            return report.State switch
            {
                SyncState.Refused => ExitCode.Refused,
                SyncState.Incomplete => ExitCode.Offline,
                _ => ExitCode.Ok,
            };
        }
        finally
        {
            connection?.Dispose();
        }
    }

    /// <summary>Runs the same pass 'flush' does, quietly, before the rest of a sync.</summary>
    private static async Task FlushSavesAsync(
        AgentContext context,
        CommandLine command,
        CancellationToken cancellationToken)
    {
        // The store this sync already has open, rather than a second connection to it.
        var arguments = new List<string> { "flush", "--quiet" };

        if (command.Has("offline"))
        {
            arguments.Add("--offline");
        }

        if (command.Value("passphrase") is { } passphrase)
        {
            arguments.Add("--passphrase");
            arguments.Add(passphrase);
        }

        await FlushCommand
            .RunAsync(context, CommandLine.Parse([.. arguments]), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Turns each reported pass into the lines the console has always written.
    /// </summary>
    /// <remarks>
    /// Every method here is a pure function of the event it is given. The ordering that used
    /// to come from statement order in one long method now comes from the order the service
    /// reports in, which is what makes a swapped pass observable.
    /// </remarks>
    private static class Printer
    {
        public static void Show(SyncEvent report)
        {
            switch (report)
            {
                case HooksInstalled(var outcome):
                    Hooks(outcome);
                    break;

                case MenuInstalled(var outcome):
                    Menu(outcome);
                    break;

                case FlushStarting:
                    Console.WriteLine();
                    break;

                case FilesystemNoted(var limits):
                    Filesystem(limits);
                    break;

                case SetResolved(var resolve):
                    Resolve(resolve);
                    break;

                case BiosProblem(var message):
                    Console.Error.WriteLine($"  {message}");
                    break;

                case BiosPlanned(var plan):
                    Console.WriteLine();
                    Console.WriteLine("BIOS");
                    BiosCommand.Report(plan);
                    break;

                case BiosApplied(var outcome):
                    Console.WriteLine($"  {outcome.Summary}");

                    foreach (var problem in outcome.Problems)
                    {
                        Console.Error.WriteLine($"    {problem}");
                    }

                    break;

                case SetPlanned(var set, var plan):
                    Console.WriteLine();
                    Console.WriteLine(set.Name);
                    Console.WriteLine($"  plan: {plan.Summary}");
                    Plan(plan);
                    break;

                case SetSkipped(_, var hadDownloads):
                    if (hadDownloads)
                    {
                        Console.WriteLine("  (offline, so nothing was fetched)");
                    }

                    break;

                case ContentProgressed(var progress):
                    Content(progress);
                    break;

                case SetSynced(_, var outcome):
                    ClearProgressLine();
                    Console.WriteLine($"  done: {outcome.Summary}");

                    foreach (var problem in outcome.Problems)
                    {
                        Console.Error.WriteLine($"  {problem}");
                    }

                    break;

                case MediaProgressed(var what):
                    Console.Write($"\r    {Trim(what, 60),-64}");
                    break;

                case MediaApplied(var outcome):
                    ClearProgressLine();
                    Console.WriteLine();
                    Console.WriteLine($"  {outcome.Summary}");

                    foreach (var problem in outcome.Problems)
                    {
                        Console.Error.WriteLine($"    {problem}");
                    }

                    break;

                case GamelistsWritten(var outcome):
                    Console.WriteLine();
                    GamelistCommand.Report(outcome);
                    break;

                case BudgetReported(var used, var cap):
                    Console.WriteLine();
                    Console.WriteLine(
                        $"Budget: {ByteSize.Format(used)} of {ByteSize.Format(cap)} used"
                            + (used > cap
                                ? $", {ByteSize.Format(used - cap)} over. Run 'evict' to see what would go."
                                : "."));
                    break;

                default:
                    break;
            }
        }

        /// <summary>
        /// Says what was installed, and never fails the sync over it.
        /// </summary>
        /// <remarks>
        /// One line per cause, not one per event folder. The commonest failure by far is the
        /// hook executable not having been published yet, and that is the same sentence four
        /// times over: the same reason for all four folders is reported once.
        /// </remarks>
        private static void Hooks(EsHookOutcome outcome)
        {
            if (outcome.Installed + outcome.Updated > 0)
            {
                Console.WriteLine(
                    $"Installed {outcome.Installed + outcome.Updated} EmulationStation hooks, so play "
                        + "sessions and saves are picked up. Remove them with 'rommbat-agent hooks uninstall'.");

                foreach (var step in outcome.Steps.Where(step =>
                    step.Action is EsHookAction.Installed or EsHookAction.Updated))
                {
                    Console.WriteLine($"  {step.Path}");
                }

                Console.WriteLine();
            }

            foreach (var problem in outcome.Problems
                .Select(problem => problem[(problem.IndexOf(':', StringComparison.Ordinal) + 1)..].Trim())
                .Distinct(StringComparer.Ordinal))
            {
                Console.Error.WriteLine($"The hook could not be installed: {problem}");
            }
        }

        private static void Menu(EsMenuOutcome outcome)
        {
            if (outcome.Installed + outcome.Updated > 0)
            {
                Console.WriteLine(
                    "Added RomMBat to the EmulationStation menu, so it can be opened from the couch. "
                        + "Remove it with 'rommbat-agent menu uninstall'.");

                foreach (var step in outcome.Steps.Where(step =>
                    step.Action is EsMenuAction.Installed or EsMenuAction.Updated))
                {
                    Console.WriteLine($"  {step.Path}");
                }

                Console.WriteLine();
            }

            foreach (var problem in outcome.Problems)
            {
                Console.Error.WriteLine($"The menu entry could not be installed: {problem}");
            }
        }

        private static void Resolve(ResolveReport report)
        {
            switch (report.State)
            {
                case ResolveState.Resolved:
                    Console.WriteLine($"{report.SetName}: {report.Summary}");
                    break;

                case ResolveState.Refused:
                case ResolveState.NeedsFolderChoice:
                    Console.Error.WriteLine($"{report.SetName}: {report.Problem}");

                    if (report.State == ResolveState.NeedsFolderChoice)
                    {
                        Console.Error.WriteLine("  sets add ... --folder <name>, or edit this set and set one.");
                    }

                    break;

                default:
                    Console.Error.WriteLine(
                        report.Problem ?? $"{report.SetName}: {report.Summary}");

                    if (report.Problem is null)
                    {
                        Console.Error.WriteLine(
                            $"  stopped at offset {report.Offset} of {report.Total}. The next run continues from there.");
                    }

                    break;
            }
        }

        /// <summary>
        /// Said up front rather than per game, because on a FAT32 stick it explains every
        /// refusal that follows, and the operating system's own message for it is misleading.
        /// </summary>
        private static void Filesystem(FilesystemLimits limits)
        {
            if (limits.MaximumFileSizeBytes is not { } maximum)
            {
                return;
            }

            Console.WriteLine(
                $"This drive is formatted {limits.Format}, which cannot hold a file larger than "
                    + $"{ByteSize.Format(maximum)}. Larger games are left out of every set.");
        }

        /// <summary>Prints what the plan would do, one line per game that is not already present.</summary>
        private static void Plan(ContentPlan plan)
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

        private static void Content(ContentSyncProgress progress)
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
}
