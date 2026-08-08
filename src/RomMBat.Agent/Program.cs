using RomMBat.Core;

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

    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine($"rommbat-agent: expected one of {string.Join(", ", Subcommands)}");
            return 2;
        }

        if (!Subcommands.Contains(args[0], StringComparer.Ordinal))
        {
            Console.Error.WriteLine($"rommbat-agent: unknown subcommand '{args[0]}'");
            return 2;
        }

        Console.Error.WriteLine(
            $"rommbat-agent: '{args[0]}' is not implemented yet. "
                + $"Targets RetroBat {RetroBatRoot.MinimumVersion} or newer. See docs/PLAN.md.");
        return 70; // EX_SOFTWARE
    }
}
