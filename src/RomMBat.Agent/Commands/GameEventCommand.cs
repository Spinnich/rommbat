using RomMBat.Core.Paths;
using RomMBat.Core.Store;

namespace RomMBat.Agent.Commands;

/// <summary>
/// <c>game-start</c> and <c>game-end</c>: append one journal row and exit.
/// </summary>
/// <remarks>
/// <b>Not what EmulationStation invokes.</b> ES runs <c>rommbat-hook.exe</c>, which writes a
/// spool file and touches no database, because four copies of it are installed and it must stay
/// small and must never block. These subcommands are the same events reachable by hand, for
/// driving a test and for the UI, and they write the journal directly because the agent already
/// has the store open.
/// <para>
/// They still never open a socket. CLAUDE.md rule 4 is about the event, not about which binary
/// serves it, and a user typing this while a game runs is in the same launch path.
/// </para>
/// </remarks>
internal static class GameEventCommand
{
    public static Task<int> RunAsync(CommandLine command, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        using var context = AgentContext.Open(command, Console.Error, out var exitCode);
        if (context is null)
        {
            return Task.FromResult(exitCode);
        }

        var journalEvent = command.Subcommand switch
        {
            "game-start" => JournalEvent.GameStart,
            "game-end" => JournalEvent.GameEnd,
            _ => JournalEvent.Quit,
        };

        RelativePath? romPath = null;
        string? basename = null;
        string? displayName = null;

        if (journalEvent == JournalEvent.GameStart)
        {
            // $1 absolute rom path, $2 basename, $3 gamelist display name. Batocera documents
            // $3 as the system; in RetroBat it is the display name and the system is never
            // passed, which is why the launch log is the source of the launch facts.
            var argument = command.Positional.Count > 0 ? command.Positional[0] : null;
            basename = command.Positional.Count > 1 ? command.Positional[1] : null;
            displayName = command.Positional.Count > 2 ? command.Positional[2] : null;

            if (argument is not null && context.Install.Contains(argument))
            {
                // Relativising at this boundary is mandatory work, not an optimisation: the
                // hook is handed an absolute path and no absolute path is ever persisted.
                romPath = context.Install.Relativize(argument);
            }
            else if (argument is not null)
            {
                Console.Error.WriteLine(
                    $"'{argument}' is outside the RetroBat tree, so the event was recorded without it.");
            }
        }

        var sequence = context.Store.Journal.Append(
            journalEvent,
            DateTimeOffset.UtcNow,
            romPath,
            basename,
            displayName,
            Environment.ProcessId);

        if (!command.Has("quiet"))
        {
            Console.WriteLine($"{command.Subcommand} recorded at sequence {sequence}.");
        }

        return Task.FromResult(ExitCode.Ok);
    }
}
