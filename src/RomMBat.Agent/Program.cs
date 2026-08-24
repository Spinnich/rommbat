using RomM.Client;
using RomMBat.Agent.Commands;
using RomMBat.Core.RetroBat;

namespace RomMBat.Agent;

/// <summary>
/// Console entry point.
/// </summary>
/// <remarks>
/// Every subcommand does one pass and exits. There is no daemon, because a portable
/// install cannot register a service or a scheduled task.
/// <para>
/// <c>game-start</c> and <c>game-end</c> run inside the game launch path. They append
/// to the local journal and exit; they never open a socket and never wait on a lock.
/// </para>
/// </remarks>
internal static class Program
{
    private static readonly string[] Subcommands =
    [
        "pair",       // device pairing, for headless setup
        "sync",       // resolve sets, pull content, media and BIOS
        "sets",       // define what this device syncs, and resolve it
        "platforms",  // the mapping surface: list, map, unmap
        "browse",     // one page of the catalog, to show the pager working
        "budget",     // how much of this drive RomMBat may use
        "evict",      // free space, dry run unless --apply
        "bios",       // what RetroBat requires under bios/, and what is missing
        "gamelist",   // rewrite gamelist.xml from local state, no server needed
        "hooks",      // install or remove the ES event hooks
        "menu",       // install or remove the ES menu entry
        "saves",      // what is on disk, what went up, what cannot
        "game-start", // journal only, no network
        "game-end",   // journal only, no network
        "flush",      // drain the outbox if the server is reachable
        "status",     // report local state
    ];

    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
        {
            WriteUsage();
            return args.Length == 0 ? ExitCode.Usage : ExitCode.Ok;
        }

        var command = CommandLine.Parse(args);

        if (!Subcommands.Contains(command.Subcommand, StringComparer.Ordinal))
        {
            Console.Error.WriteLine($"rommbat-agent: unknown subcommand '{command.Subcommand}'");
            WriteUsage();
            return ExitCode.Usage;
        }

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        return await DispatchAsync(command, cancellation.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs one parsed subcommand, and turns the exceptions that reach this far into exit codes.
    /// </summary>
    /// <remarks>
    /// Split out of <see cref="Main"/> so the test project drives the same dispatch and the
    /// same handlers a user gets. A test that calls a command class directly runs past the
    /// layer where an exception becomes an exit code, which is the layer this catches.
    /// </remarks>
    internal static async Task<int> DispatchAsync(CommandLine command, CancellationToken cancellationToken)
    {
        try
        {
            return command.Subcommand switch
            {
                "pair" => await PairCommand.RunAsync(command, cancellationToken).ConfigureAwait(false),
                "status" => await StatusCommand.RunAsync(command, cancellationToken).ConfigureAwait(false),
                "sets" => await SetsCommand.RunAsync(command, cancellationToken).ConfigureAwait(false),
                "platforms" => await PlatformsCommand.RunAsync(command, cancellationToken).ConfigureAwait(false),
                "browse" => await BrowseCommand.RunAsync(command, cancellationToken).ConfigureAwait(false),
                "sync" => await SyncCommand.RunAsync(command, cancellationToken).ConfigureAwait(false),
                "budget" => await BudgetCommand.RunAsync(command, cancellationToken).ConfigureAwait(false),
                "evict" => await EvictCommand.RunAsync(command, cancellationToken).ConfigureAwait(false),
                "bios" => await BiosCommand.RunAsync(command, cancellationToken).ConfigureAwait(false),
                "gamelist" => await GamelistCommand.RunAsync(command, cancellationToken).ConfigureAwait(false),
                "hooks" => await HooksCommand.RunAsync(command, cancellationToken).ConfigureAwait(false),
                "menu" => await MenuCommand.RunAsync(command, cancellationToken).ConfigureAwait(false),
                "saves" => await SavesCommand.RunAsync(command, cancellationToken).ConfigureAwait(false),
                "game-start" or "game-end" => await GameEventCommand.RunAsync(command, cancellationToken).ConfigureAwait(false),
                "flush" => await FlushCommand.RunAsync(command, cancellationToken).ConfigureAwait(false),
                _ => NotImplemented(command.Subcommand),
            };
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("Cancelled.");
            return ExitCode.Cancelled;
        }
        catch (RomMUnreachableException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCode.Offline;
        }
        catch (EsSystemsException ex)
        {
            // A missing or unreadable es_systems.cfg is a failed precondition, and the
            // exception's own message was written for the user who hit it.
            Console.Error.WriteLine(ex.Message);
            return ExitCode.Refused;
        }
    }

    private static int NotImplemented(string subcommand)
    {
        Console.Error.WriteLine(
            $"rommbat-agent: '{subcommand}' lands in a later milestone. See docs/PLAN.md.");
        return ExitCode.NotImplemented;
    }

    private static void WriteUsage()
    {
        Console.Error.WriteLine("rommbat-agent <subcommand> [options]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Subcommands");
        Console.Error.WriteLine("  pair        Pair this install with a RomM server");
        Console.Error.WriteLine("  status      Report local state, and probe the server unless --offline");
        Console.Error.WriteLine("  sets        list | add | show | remove | resolve sync sets");
        Console.Error.WriteLine("  platforms   list | map | unmap the RomM to RetroBat folder mapping");
        Console.Error.WriteLine("  browse      Print one page of the catalog");
        Console.Error.WriteLine("  sync        Resolve a set and pull its ROMs into the tree");
        Console.Error.WriteLine("  budget      Show or set how much of this drive RomMBat may use");
        Console.Error.WriteLine("  evict       Show what would be removed to get back inside the budget");
        Console.Error.WriteLine("  bios        Report the BIOS RetroBat needs, and fetch it with --apply");
        Console.Error.WriteLine("  gamelist    Rewrite gamelist.xml from local state, and tell EmulationStation");
        Console.Error.WriteLine("  hooks       status | install | uninstall the EmulationStation event hooks");
        Console.Error.WriteLine("  menu        status | install | uninstall RomMBat's EmulationStation menu entry");
        Console.Error.WriteLine("  saves       What is on disk, what went up, and what is waiting on you");
        Console.Error.WriteLine("              saves resolve <rom> <slot> --keep-local | --keep-server");
        Console.Error.WriteLine("  game-start  Record a launch. Journal only, no network");
        Console.Error.WriteLine("  game-end    Close a launch. Journal only, no network");
        Console.Error.WriteLine("  flush       One pass over everything waiting, then exit");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Options");
        Console.Error.WriteLine("  --root <path>     The RetroBat root, when discovery cannot find it");
        Console.Error.WriteLine("  --server <url>    The RomM origin. Remembered after the first pairing");
        Console.Error.WriteLine("  --name <label>    How this device appears in the RomM device list");
        Console.Error.WriteLine("  --protect         Encrypt the stored token with a passphrase you type");
        Console.Error.WriteLine("  --offline         status, sync, bios: work from local state without the server");
        Console.Error.WriteLine("  --dry-run         sync: say what would happen and write nothing");
        Console.Error.WriteLine("  --apply           evict: actually remove. bios: actually fetch. Without it, neither writes");
        Console.Error.WriteLine("  --all             bios: every system RetroBat knows, not just the ones with games");
        Console.Error.WriteLine("  --max <size>      budget: the cap, as 64GB, 500MB or none");
        Console.Error.WriteLine("  --media <kinds>   gamelist: which artwork to fetch, e.g. image,thumbnail,video");
        Console.Error.WriteLine("  --no-reload       gamelist: write the files without telling EmulationStation");
        Console.Error.WriteLine("  --no-scan         saves: report what is recorded without rescanning the tree");
        Console.Error.WriteLine("  --keep-local      saves resolve: send this device's copy over the server's");
        Console.Error.WriteLine("  --keep-server     saves resolve: take the server's copy over this device's");
    }
}
