using RomMBat.Core;
using System.Globalization;
using RomM.Client;
using RomM.Client.Catalog;
using RomMBat.Core.Content;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;

namespace RomMBat.Agent.Commands;

/// <summary>
/// <c>sync</c>: turn a resolved set into files in the RetroBat tree.
/// </summary>
/// <remarks>
/// Six passes, in this order and for a reason. The set is re-resolved, because
/// smart-collection membership drifts server-side and fetching a stale membership downloads
/// games the set no longer contains. Then a plan is worked out and printed, because being told
/// what is about to happen is worth more than a progress bar. Then, unless this is a dry run,
/// the plan is carried out.
/// <para>
/// <b>BIOS goes first, ahead of every ROM.</b> A platform synced without its firmware is dead
/// weight in the gallery: the games appear in EmulationStation, look right, and die on launch.
/// Fetching it after the ROMs would leave exactly that state behind on any run that was
/// interrupted, and interrupted is the normal case for a handheld. The pass covers every folder
/// the sets resolve to, in one <c>GET /api/platforms</c> rather than one request per platform,
/// so ordering it first costs one request and not one per set.
/// </para>
/// <para>
/// <b>The saves flush goes first, ahead of everything, including the BIOS pass.</b> It is what
/// turns spooled hook events into play sessions and brings <c>local_save</c> up to date, and
/// eviction inside this run asks <c>local_save</c> whether a game's saves are safely up.
/// Flushing afterwards would answer that from the previous run. It is also the only thing that
/// sends a save at all in this build, since the hooks spool and exit and nothing else wakes an
/// agent, so a user who never leaves EmulationStation has this as their one trigger. Cheap when
/// there is nothing waiting: one query.
/// </para>
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

        var dryRun = command.Has("dry-run");
        var offline = command.Has("offline");

        if (!dryRun)
        {
            InstallHooks(context);
            InstallMenuEntry(context);

            // First, before a byte is fetched. What the hooks spooled is turned into play
            // sessions and local_save is brought up to date, which is what everything below
            // depends on: eviction asks local_save whether a game's saves are safely up, and a
            // sync that flushed last would answer that question from the previous run.
            await FlushSavesAsync(context, command, cancellationToken).ConfigureAwait(false);
        }

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
                        .ReportResolveAsync(context, connection, sets, cancellationToken)
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

            // Folders touched across every set, so one gamelist pass covers them all. Two RomM
            // platforms can resolve to one folder, and writing per set would have the second
            // set's write clobber the first's.
            var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var syncedRoms = new List<int>();

            // Before a byte of ROM content, and over every folder at once. The folders come from
            // the membership rather than from the plan, because a set whose games are all present
            // still needs its BIOS to be.
            await FetchBiosAsync(
                    context,
                    connection,
                    limits,
                    sets.SelectMany(set => context.Store.SyncSets.Members(set.Id))
                        .Select(member => member.Folder)
                        .OfType<string>(),
                    dryRun,
                    cancellationToken)
                .ConfigureAwait(false);

            foreach (var set in sets)
            {
                var members = context.Store.SyncSets.Members(set.Id);
                var plan = planner.Plan(set, members);

                Console.WriteLine();
                Console.WriteLine(set.Name);
                Console.WriteLine($"  plan: {plan.Summary}");
                Report(plan);

                // Collected before the run rather than after it, so an offline pass reaches
                // the gamelist write below. The set is the plan's, not the outcome's, because
                // a folder whose download failed still holds whatever was already there.
                foreach (var step in plan.Steps.Where(step => step.Action != ContentAction.Blocked))
                {
                    folders.Add(step.Member.Folder!);
                }

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

                foreach (var step in plan.Steps.Where(step => step.Action != ContentAction.Blocked))
                {
                    syncedRoms.Add(step.Member.RomId);
                }
            }

            if (!dryRun && !offline && connection is not null && syncedRoms.Count > 0)
            {
                await FetchMediaAsync(context, connection, limits, syncedRoms, cancellationToken)
                    .ConfigureAwait(false);
            }

            // Written even on a dry run's opposite, an offline run: the gamelist comes from
            // local state, so a sync that fetched nothing still leaves ES showing what is
            // there. A dry run writes nothing at all, which is what a dry run means.
            if (!dryRun && folders.Count > 0)
            {
                await WriteGamelistsAsync(context, folders, cancellationToken).ConfigureAwait(false);
            }

            ReportBudget(context, planner);
            return worst;
        }
        finally
        {
            connection?.Dispose();
        }
    }

    /// <summary>
    /// Puts the ES hooks in place on the first sync, and says exactly what it added.
    /// </summary>
    /// <remarks>
    /// Silent when they are already current, which is every run after the first. A failure is
    /// reported and never fatal: the commonest cause is EmulationStation holding the file, and
    /// a sync that fetched a library should not fail over a hook it can install next time.
    /// </remarks>
    private static void InstallHooks(AgentContext context)
    {
        var hooks = new RomMBat.Core.RetroBat.EsHooks(context.Install);

        if (hooks.IsInstalled())
        {
            return;
        }

        var outcome = hooks.Install();

        if (outcome.Installed + outcome.Updated > 0)
        {
            Console.WriteLine(
                $"Installed {outcome.Installed + outcome.Updated} EmulationStation hooks, so play "
                    + "sessions and saves are picked up. Remove them with 'rommbat-agent hooks uninstall'.");

            foreach (var step in outcome.Steps.Where(step => step.Action
                is RomMBat.Core.RetroBat.EsHookAction.Installed or RomMBat.Core.RetroBat.EsHookAction.Updated))
            {
                Console.WriteLine($"  {step.Path}");
            }

            Console.WriteLine();
        }

        // One line per cause, not one per event folder. The commonest failure by far is the
        // hook executable not having been published yet, and that is the same sentence four
        // times over: the same reason for all four folders is reported once.
        foreach (var problem in outcome.Problems
            .Select(problem => problem[(problem.IndexOf(':', StringComparison.Ordinal) + 1)..].Trim())
            .Distinct(StringComparer.Ordinal))
        {
            Console.Error.WriteLine($"The hook could not be installed: {problem}");
        }
    }

    /// <summary>
    /// Puts RomMBat in the EmulationStation menu on the first sync, and says what it added.
    /// </summary>
    /// <remarks>
    /// Silent once it is there, which is every run after the first. A failure is reported and
    /// never fatal, for the same reason the hooks' is: the commonest cause is EmulationStation
    /// holding a file, and the next sync installs it.
    /// <para>
    /// No <c>/reloadgames</c> from here. The gamelist pass later in this same sync issues one
    /// after it writes, and ES picks a new <c>.menu</c> up from that reload like any other rom,
    /// measured at 209 ms to visible. A second call would cost a round trip to say the same
    /// thing.
    /// </para>
    /// </remarks>
    private static void InstallMenuEntry(AgentContext context)
    {
        var entry = new EsMenuEntry(context.Install);

        if (entry.IsInstalled())
        {
            return;
        }

        var outcome = entry.Install();

        if (outcome.Installed + outcome.Updated > 0)
        {
            Console.WriteLine(
                "Added RomMBat to the EmulationStation menu, so it can be opened from the couch. "
                    + "Remove it with 'rommbat-agent menu uninstall'.");

            foreach (var step in outcome.Steps.Where(step => step.Action
                is EsMenuAction.Installed or EsMenuAction.Updated))
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

    /// <summary>Runs the same pass 'flush' does, quietly, before the rest of a sync.</summary>
    private static async Task FlushSavesAsync(
        AgentContext context,
        CommandLine command,
        CancellationToken cancellationToken)
    {
        Console.WriteLine();

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
    /// Fetches the firmware every folder in this sync needs, before any of its ROMs.
    /// </summary>
    /// <remarks>
    /// Never fatal. A BIOS RomM does not have is the ordinary case this reports rather than an
    /// error, and a firmware pass that fails outright must not stop the ROMs it was ordered in
    /// front of: the same sync run tomorrow will try again, and the report already says what is
    /// missing.
    /// </remarks>
    private static async Task FetchBiosAsync(
        AgentContext context,
        RomMConnection? connection,
        FilesystemLimits limits,
        IEnumerable<string> folders,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var wanted = folders.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (wanted.Count == 0)
        {
            return;
        }

        var planner = new BiosPlanner(context.Install, context.Store, limits: limits);
        IReadOnlyDictionary<string, FirmwareRow>? candidates = null;

        if (connection is not null)
        {
            var (index, problem) = await BiosCommand.ReadCandidatesAsync(connection, cancellationToken).ConfigureAwait(false);
            if (problem is not null)
            {
                Console.Error.WriteLine($"  {problem}");
            }

            candidates = index;
        }

        var plan = planner.Plan(wanted, candidates);

        if (plan.Steps.Count == 0)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine("BIOS");
        BiosCommand.Report(plan);

        // IsNoOp rather than DownloadCount: a plan that only adopts still has rows to write,
        // and an offline pass can adopt without a connection.
        if (dryRun || plan.IsNoOp)
        {
            return;
        }

        var outcome = await new BiosSync(context.Install, context.Store, connection)
            .ApplyAsync(plan, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine($"  {outcome.Summary}");

        foreach (var problem in outcome.Problems)
        {
            Console.Error.WriteLine($"    {problem}");
        }
    }

    /// <summary>
    /// Fetches the artwork for what just landed.
    /// </summary>
    /// <remarks>
    /// After the ROMs rather than alongside them, because a cover for a game whose download
    /// failed is bytes spent on a gamelist entry that will not be written.
    /// </remarks>
    private static async Task FetchMediaAsync(
        AgentContext context,
        RomMConnection connection,
        FilesystemLimits limits,
        IReadOnlyCollection<int> romIds,
        CancellationToken cancellationToken)
    {
        var media = new MediaSync(context.Install, context.Store, connection, limits);

        var outcome = await media
            .ApplyAsync(romIds, new Progress<string>(ShowMedia), cancellationToken)
            .ConfigureAwait(false);

        ClearProgressLine();
        Console.WriteLine();
        Console.WriteLine($"  {outcome.Summary}");

        foreach (var problem in outcome.Problems)
        {
            Console.Error.WriteLine($"    {problem}");
        }
    }

    private static async Task WriteGamelistsAsync(
        AgentContext context,
        IEnumerable<string> folders,
        CancellationToken cancellationToken)
    {
        var gamelists = new GamelistSync(context.Install, context.Store);
        using var emulationStation = new EmulationStationClient();

        var outcome = await gamelists
            .ApplyAsync(folders, emulationStation, cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine();
        GamelistCommand.Report(outcome);
    }

    private static void ShowMedia(string what) =>
        Console.Write($"\r    {Trim(what, 60),-64}");

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
