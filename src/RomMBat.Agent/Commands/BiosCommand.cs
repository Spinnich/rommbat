using RomM.Client;
using RomM.Client.Catalog;
using RomMBat.Core;
using RomMBat.Core.Content;

namespace RomMBat.Agent.Commands;

/// <summary>
/// <c>bios</c>: fetch what RetroBat requires under <c>bios/</c>, and report what is missing.
/// </summary>
/// <remarks>
/// <b>The report is the point, and it works offline.</b> Required firmware RomM does not have
/// is the single most useful thing this feature can tell a user, because the alternative is a
/// game that appears in EmulationStation and dies on launch. Without the server it still
/// answers from the bundled manifest and what is on disk; with it, the absent half splits into
/// "RomM has it" and "not in your library" for the price of one request.
/// <para>
/// A dry run by default, in the shape of <c>evict</c>: nothing is fetched without
/// <c>--apply</c>. <c>sync</c> runs the same pass itself, before that platform's ROMs.
/// </para>
/// </remarks>
internal static class BiosCommand
{
    public static async Task<int> RunAsync(CommandLine command, CancellationToken cancellationToken)
    {
        using var context = AgentContext.Open(command, Console.Error, out var exitCode);
        if (context is null)
        {
            return exitCode;
        }

        var offline = command.Has("offline");
        var apply = command.Has("apply");

        // Which systems to cover. --all is every system the manifest names, which is the
        // whole-library report; the default is the folders this install actually holds ROMs
        // for, because BIOS for a system with no games is noise.
        var planner = new BiosPlanner(context.Install, context.Store);
        var folders = command.Has("all")
            ? null
            : command.Positional.Count > 0
                ? command.Positional
                : planner.FoldersNeedingBios();

        RomMConnection? connection = null;

        try
        {
            IReadOnlyDictionary<string, FirmwareRow>? candidates = null;

            if (!offline)
            {
                connection = context.Authenticate(command, Console.Error, out exitCode);
                if (connection is null)
                {
                    return exitCode;
                }

                var (index, problem) = await ReadCandidatesAsync(connection, cancellationToken).ConfigureAwait(false);
                if (problem is not null)
                {
                    Console.Error.WriteLine(problem);
                    Console.Error.WriteLine("Reporting from the manifest and what is on disk instead.");
                }

                candidates = index;
            }

            var plan = planner.Plan(folders, candidates);

            Report(plan);

            // IsNoOp rather than DownloadCount, because adopting a file the user copied in by
            // hand is a write too, and it is the only thing to do on a library RomM has none
            // of the required hashes for.
            if (!apply || plan.IsNoOp)
            {
                if (!plan.IsNoOp)
                {
                    Console.WriteLine();
                    Console.WriteLine("Nothing was written. Run 'bios --apply' to bring these in.");
                }

                return ExitCode.Ok;
            }

            // A null connection is an offline run, which carries adoptions and no downloads.
            var sync = new BiosSync(context.Install, context.Store, connection);
            var outcome = await sync
                .ApplyAsync(plan, new Progress<BiosSyncProgress>(Show), cancellationToken)
                .ConfigureAwait(false);

            ClearProgressLine();
            Console.WriteLine();
            Console.WriteLine(outcome.Summary);

            foreach (var problem in outcome.Problems)
            {
                Console.Error.WriteLine($"  {problem}");
            }

            return outcome.Failed > 0 ? ExitCode.Offline : ExitCode.Ok;
        }
        finally
        {
            connection?.Dispose();
        }
    }

    /// <summary>
    /// Reads every firmware record RomM holds, in one request.
    /// </summary>
    /// <remarks>
    /// <c>GET /api/platforms</c> rather than one <c>GET /api/firmware?platform_id=</c> per
    /// platform: the inlined <c>firmware[]</c> is complete, carries an md5 on every record, and
    /// costs 424 KB and 0.40 s for a 123-platform library against 79 requests.
    /// <para>
    /// A failure here is not fatal. The gap report is still worth printing from the manifest
    /// and what is on disk, which is exactly what an offline run does.
    /// </para>
    /// </remarks>
    internal static async Task<(IReadOnlyDictionary<string, FirmwareRow>? Index, string? Problem)> ReadCandidatesAsync(
        RomMConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await connection.ListPlatformsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccess)
            {
                return (null, $"RomM's platform list could not be read: {response.Message}");
            }

            return (BiosPlanner.IndexByMd5(response.Value!), null);
        }
        catch (RomMUnreachableException ex)
        {
            return (null, ex.Message);
        }
    }

    /// <summary>Prints the plan, grouped so the states a user has to act on come last.</summary>
    internal static void Report(BiosPlan plan)
    {
        Console.WriteLine(plan.Summary);

        if (plan.Steps.Count == 0)
        {
            return;
        }

        if (!plan.CheckedAgainstServer)
        {
            Console.WriteLine("(the server was not asked, so nothing here says whether RomM has a file)");
        }

        Console.WriteLine();

        foreach (var group in plan.Steps.GroupBy(step => step.Requirement.Folder).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            var interesting = group.Where(step => step.Action != BiosAction.AlreadyPresent).ToList();

            if (interesting.Count == 0)
            {
                Console.WriteLine($"  {group.Key}: all {group.Count()} present");
                continue;
            }

            Console.WriteLine($"  {group.Key}");

            foreach (var step in interesting.OrderBy(step => Order(step.Action)).ThenBy(step => step.Path.Value, StringComparer.Ordinal))
            {
                Console.WriteLine($"    {Describe(step),-24} {step.Path}");

                // The hash is what a user needs to go looking with, so it is printed for the
                // states where looking is the next step and withheld where it is noise.
                if (step.Action is BiosAction.MissingFromLibrary && step.Requirement.Md5 is { } md5)
                {
                    Console.WriteLine($"      {md5}");
                }

                if (step.Reason is { } reason && step.Action is BiosAction.Mismatch or BiosAction.Blocked)
                {
                    Console.WriteLine($"      {reason}");
                }
            }
        }

        var unverifiable = plan.Count(BiosAction.Unverifiable);
        if (unverifiable > 0)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"{unverifiable} of these are files RetroBat lists without a hash. RomMBat can neither find "
                    + "them in RomM nor recognise them on disk, so it says nothing about whether you have them.");
        }
    }

    private static int Order(BiosAction action) => action switch
    {
        BiosAction.Download => 0,
        BiosAction.Adopt => 1,
        BiosAction.Blocked => 2,
        BiosAction.Mismatch => 3,
        BiosAction.MissingFromLibrary => 4,
        BiosAction.NotChecked => 5,
        _ => 6,
    };

    private static string Describe(BiosStep step) => step.Action switch
    {
        BiosAction.Download => $"fetch {ByteSize.Format(step.BytesToTransfer)}",
        BiosAction.Adopt => "already on disk",
        BiosAction.AlreadyPresent => "present",
        BiosAction.Mismatch => "a different file",
        BiosAction.MissingFromLibrary => "not in your library",
        BiosAction.NotChecked => "not on disk",
        BiosAction.Blocked => "blocked",
        _ => "no hash to check",
    };

    private static void Show(BiosSyncProgress progress) =>
        Console.Write($"\r    [{progress.Index}/{progress.Total}] {Trim(progress.Step.Requirement.FileName, 48),-52}");

    private static void ClearProgressLine() => Console.Write("\r" + new string(' ', 78) + "\r");

    private static string Trim(string value, int width) =>
        value.Length <= width ? value : value[..(width - 1)] + "…";
}
