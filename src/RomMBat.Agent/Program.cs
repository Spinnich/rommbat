using RomM.Client;
using RomMBat.Agent.Commands;

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

        try
        {
            return command.Subcommand switch
            {
                "pair" => await PairCommand.RunAsync(command, cancellation.Token).ConfigureAwait(false),
                "status" => await StatusCommand.RunAsync(command, cancellation.Token).ConfigureAwait(false),
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
        Console.Error.WriteLine("  sync        Not implemented yet (M2-M5)");
        Console.Error.WriteLine("  game-start  Not implemented yet (M6)");
        Console.Error.WriteLine("  game-end    Not implemented yet (M6)");
        Console.Error.WriteLine("  flush       Not implemented yet (M6)");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Options");
        Console.Error.WriteLine("  --root <path>     The RetroBat root, when discovery cannot find it");
        Console.Error.WriteLine("  --server <url>    The RomM origin. Remembered after the first pairing");
        Console.Error.WriteLine("  --name <label>    How this device appears in the RomM device list");
        Console.Error.WriteLine("  --protect         Encrypt the stored token with a passphrase you type");
        Console.Error.WriteLine("  --offline         status only: skip the reachability probe");
    }
}
