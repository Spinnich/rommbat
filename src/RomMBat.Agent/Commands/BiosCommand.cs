using RomM.Client;
using RomM.Client.Catalog;
using RomMBat.Core;
using RomMBat.Core.Content;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;

namespace RomMBat.Agent.Commands;

/// <summary>
/// <c>bios</c>: fetch the firmware every emulator a system offers reads, and report what is missing.
/// </summary>
/// <remarks>
/// <b>The report is the point, and it works offline.</b> Listed firmware RomM does not have is
/// the most useful thing this feature can tell a user, because on an emulator that needs it
/// the game appears in EmulationStation and dies on launch. It is information, never a gate:
/// the sources say which emulator reads a file and not which one the user runs, so a gap exits
/// zero and blocks nothing. Without the server it still
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

        if (command.Has("region-order") && SetRegionOrder(context, command.Value("region-order")) is { } refused)
        {
            return refused;
        }

        // Which systems to cover. --all is every system the manifest names, which is the
        // whole-library report; the default is the folders this install actually holds ROMs
        // for, because BIOS for a system with no games is noise.
        var planner = new BiosPlanner(context.Install, context.Store);
        IReadOnlyList<string>? folders;

        if (command.Has("all"))
        {
            folders = null;
        }
        else if (command.Positional.Count > 0)
        {
            if (Validate(context, command.Positional) is { } usage)
            {
                return usage;
            }

            folders = command.Positional;
        }
        else
        {
            folders = planner.FoldersNeedingBios();

            // Falling through to Plan([]) answers "no BIOS is required for these systems" over
            // the empty set: a false clean bill of health, the one Validate refuses a mistyped
            // positional for, in the path Validate cannot cover. Two different installs land
            // here, though, and one sentence cannot serve both: FoldersNeedingBios() filters by
            // the manifest, so it is empty both for an install that has synced nothing and for
            // one whose systems' emulators read no firmware, which an atari2600 or a ports
            // library reaches fully populated.
            if (folders.Count == 0)
            {
                var synced = planner.SyncedFolders();

                if (synced.Count == 0)
                {
                    Console.WriteLine(
                        "Nothing has been synced yet, so there is no library to report BIOS for.");
                    Console.WriteLine(
                        "Run 'sync' first, or 'bios --all' for the firmware every system's "
                            + "emulators read.");
                }
                else
                {
                    var systems = synced.Count == 1 ? "system" : "systems";
                    Console.WriteLine(
                        $"No emulator offered for the {synced.Count} {systems} synced here reads "
                            + "firmware RomMBat can fetch, so there is nothing to do.");
                    Console.WriteLine(
                        "Run 'bios --all' for the firmware every system's emulators read, or name "
                            + "a system to check it on its own.");
                }

                return ExitCode.Ok;
            }
        }

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

                var (index, problem) = await BiosCandidates.ReadAsync(connection, cancellationToken).ConfigureAwait(false);
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

            return outcome.Failed > 0 ? ExitCode.For(outcome.Cause) : ExitCode.Ok;
        }
        finally
        {
            connection?.Dispose();
        }
    }

    /// <summary>
    /// Checks the named systems against the install, and answers the two cases that are not
    /// the report.
    /// </summary>
    /// <remarks>
    /// <b>Here rather than in the planner, because it is a question about user input.</b>
    /// <c>--all</c> passes null and <c>FoldersNeedingBios()</c> returns names that came off
    /// this install in the first place, so neither wants validating.
    /// <para>
    /// <b>The manifest cannot answer this on its own.</b> A system none of whose emulators reads
    /// firmware has no slots, so absent from the manifest is an ordinary case rather than the
    /// error. <see cref="EsSystemsFile"/> is the authority on what an install has, which splits
    /// one outcome into three: named with slots is the report, named without any says nothing is
    /// listed for it, and not named at all is a typo.
    /// </para>
    /// </remarks>
    /// <returns>An exit code when the run should stop, or null to carry on.</returns>
    private static int? Validate(AgentContext context, IReadOnlyList<string> folders)
    {
        var systems = EsSystemsFile.Load(context.Install);
        var unknown = folders.Where(folder => !systems.HasFolder(folder)).ToList();

        if (unknown.Count > 0)
        {
            // Named rather than counted: the whole point is that the user gets told which of
            // the words they typed this install has never heard of.
            Console.Error.WriteLine(
                unknown.Count == 1
                    ? $"'{unknown[0]}' is not a system in this install's es_systems.cfg."
                    : $"these are not systems in this install's es_systems.cfg: {string.Join(", ", unknown)}.");
            Console.Error.WriteLine("Run 'platforms' to see the systems this install declares.");

            return ExitCode.Usage;
        }

        // A real system nothing reads firmware for. Said explicitly, because the alternative is
        // the same "nothing listed" line a mistyped name would get if the check above let it
        // through, and a user who cannot tell those apart cannot trust either.
        var needNothing = folders
            .Where(folder => BiosManifest.Bundled.For(folder).Count == 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var folder in needNothing)
        {
            Console.WriteLine($"No firmware is listed for {folder}.");
        }

        // Only the named systems, and none of them needs anything, so there is no report to
        // print. Not an error: the answer is a clean bill of health for a system that was
        // actually consulted this time.
        return needNothing.Count == folders.Distinct(StringComparer.OrdinalIgnoreCase).Count()
            ? ExitCode.Ok
            : null;
    }

    /// <summary>Records a region order, or refuses one it cannot read.</summary>
    /// <returns>An exit code when the run should stop, or null to carry on.</returns>
    private static int? SetRegionOrder(AgentContext context, string? value)
    {
        if (string.Equals(value, "default", StringComparison.OrdinalIgnoreCase))
        {
            context.Store.Settings.Set(SettingStore.FirmwareRegionOrder, (string?)null, DateTimeOffset.UtcNow);
            return null;
        }

        if (!FirmwareRegionOrder.TryParse(value, out var order))
        {
            Console.Error.WriteLine(
                "--region-order takes a comma-separated list of regions, each once, from: "
                    + $"{string.Join(", ", FirmwareRegionOrder.Known.Order(StringComparer.Ordinal))}. "
                    + "'default' restores usa,europe,japan,world.");
            return ExitCode.Usage;
        }

        context.Store.Settings.Set(SettingStore.FirmwareRegionOrder, order.ToString(), DateTimeOffset.UtcNow);
        return null;
    }

    /// <summary>Prints the plan, grouped so the states a user has to act on come last.</summary>
    internal static void Report(BiosPlan plan)
    {
        Console.WriteLine(plan.Summary);

        if (plan.Steps.Count == 0)
        {
            return;
        }

        Console.WriteLine($"Where files compete for one slot, regions are preferred {plan.RegionOrder.Describe()}.");

        if (!plan.CheckedAgainstServer)
        {
            Console.WriteLine("(the server was not asked, so nothing here says whether RomM has a file)");
        }

        Console.WriteLine();

        foreach (var group in plan.Steps.GroupBy(step => step.Requirement.Folder).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            // A slot no source identifies goes in the footer rather than the list, where it would
            // read as a gap the user could close.
            var interesting = group
                .Where(step => step.Action is not (BiosAction.AlreadyPresent or BiosAction.Unverifiable) || step.Better is not null)
                .ToList();

            if (interesting.Count == 0)
            {
                var present = group.Count(step => step.Action == BiosAction.AlreadyPresent);
                Console.WriteLine(present > 0
                    ? $"  {group.Key}: all {present} present"
                    : $"  {group.Key}: nothing any source identifies");
                continue;
            }

            Console.WriteLine($"  {group.Key}");

            foreach (var step in interesting.OrderBy(step => Order(step.Action)).ThenBy(step => step.Path.Value, StringComparer.Ordinal))
            {
                Console.WriteLine($"    {Describe(step),-24} {step.Path}");
                Console.WriteLine($"      read by {Readers(step.Requirement)}");

                // The hash is what a user needs to go looking with, so it is printed for the
                // states where looking is the next step and withheld where it is noise.
                if (step.Action is BiosAction.MissingFromLibrary && step.Requirement.Accepted.Count > 0)
                {
                    Console.WriteLine($"      {string.Join(" or ", step.Requirement.Accepted.Take(3).Select(accepted => accepted.Md5))}");
                }

                if (step.Reason is { } reason
                    && (step.Action is BiosAction.Mismatch or BiosAction.ContentsMismatch or BiosAction.Blocked
                        or BiosAction.MissingFromLibrary or BiosAction.Ambiguous or BiosAction.Upgrade
                        || step.SameName is not null || step.Better is not null))
                {
                    Console.WriteLine($"      {reason}");
                }
            }
        }

        // The footer lists what nothing identifies and what nothing ranks apart: the two states
        // where RomMBat could not decide, as against the ones where the library lacks a file.
        var unidentified = plan.Steps.Where(step => step.Action == BiosAction.Unverifiable).ToList();
        var ambiguous = plan.Count(BiosAction.Ambiguous);

        if (unidentified.Count == 0 && ambiguous == 0)
        {
            return;
        }

        Console.WriteLine();

        if (unidentified.Count > 0)
        {
            Console.WriteLine(
                $"{unidentified.Count} {(unidentified.Count == 1 ? "file is" : "files are")} listed with no source "
                    + "naming a hash. RomMBat can neither find them in RomM nor recognize them on disk, so it "
                    + "says nothing about whether you have them:");

            foreach (var step in unidentified
                .OrderBy(step => step.Requirement.Folder, StringComparer.OrdinalIgnoreCase)
                .ThenBy(step => step.Path.Value, StringComparer.Ordinal))
            {
                var lead = step.SameName is { } same ? $"  (RomM has a {same.FileName} ({same.NormalizedMd5}))" : string.Empty;
                Console.WriteLine($"  {step.Requirement.Folder}: {step.Path}{lead}");
            }

            var named = unidentified.Count(step => step.SameName is not null);
            if (named > 0)
            {
                Console.WriteLine(
                    $"For {named} of them RomM holds a file under the same name, which is never fetched: "
                        + "whether it is the right one is yours to judge.");
            }
        }

        if (ambiguous > 0)
        {
            Console.WriteLine(
                $"{ambiguous} {(ambiguous == 1 ? "slot has" : "slots have")} several files in RomM that nothing "
                    + "ranks apart, named above. None is fetched while RomM holds more than one of them.");
        }
    }

    /// <summary>Who reads a slot, in the words of es_systems.cfg's rows.</summary>
    private static string Readers(BiosRequirement requirement) =>
        string.Join(", ", requirement.Readers.Select(reader =>
            reader == BiosAccepted.RetroBat ? "RetroBat's own list" : reader));

    private static int Order(BiosAction action) => action switch
    {
        BiosAction.Download => 0,
        BiosAction.Upgrade => 1,
        BiosAction.Adopt => 2,
        BiosAction.Blocked => 3,
        BiosAction.Ambiguous => 4,
        BiosAction.Mismatch => 5,
        BiosAction.ContentsMismatch => 6,
        BiosAction.MissingFromLibrary => 7,
        BiosAction.NotChecked => 8,
        _ => 9,
    };

    private static string Describe(BiosStep step) => step.Action switch
    {
        BiosAction.Download => $"fetch {ByteSize.Format(step.BytesToTransfer)}",
        BiosAction.Upgrade => $"upgrade {ByteSize.Format(step.BytesToTransfer)}",
        BiosAction.Adopt => step.Better is null ? "already on disk" : "on disk, RomM has better",
        BiosAction.AlreadyPresent => step.Better is null ? "present" : "present, RomM has better",
        BiosAction.Mismatch => "a different file",
        BiosAction.ContentsMismatch => "a zip that does not fit",
        BiosAction.Ambiguous => "several to choose from",
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
