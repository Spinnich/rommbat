using RomMBat.Core.RetroBat;

namespace RomMBat.Agent.Commands;

/// <summary>
/// <c>hooks</c>: put the EmulationStation event hooks in place, or take them out.
/// </summary>
/// <remarks>
/// <b>Installed on the first <c>sync</c> as well, announced.</b> The opt-in rule that governs
/// class D exists because flipping a memory card mode changes where an emulator writes and
/// strands the saves already there. A hook adds a file beside the existing scripts and changes
/// nothing about how a game runs, so the same ceremony is not warranted; what is warranted is
/// saying plainly what was added and where, and being able to take it back out.
/// <para>
/// Without hooks there is no playtime and no launch window at all, so leaving the milestone's
/// headline feature off by default would be the worse failure.
/// </para>
/// </remarks>
internal static class HooksCommand
{
    public static Task<int> RunAsync(CommandLine command, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        using var context = AgentContext.Open(command, Console.Error, out var exitCode);
        if (context is null)
        {
            return Task.FromResult(exitCode);
        }

        var hooks = new EsHooks(context.Install);
        var action = command.Positional.Count > 0 ? command.Positional[0] : "status";

        switch (action)
        {
            case "install":
                return Task.FromResult(Report(hooks.Install(command.Value("from"))));

            case "uninstall":
                return Task.FromResult(Report(hooks.Uninstall()));

            case "status":
                Console.WriteLine(hooks.IsInstalled()
                    ? "The hooks are installed and current."
                    : "The hooks are not installed. Run 'rommbat-agent hooks install', or just sync.");

                foreach (var hookEvent in EsHooks.Events)
                {
                    var path = EsHooks.PathFor(hookEvent);
                    var present = File.Exists(context.Install.Resolve(path));
                    Console.WriteLine($"  {hookEvent,-11} {(present ? "present" : "absent ")}  {path}");
                }

                return Task.FromResult(ExitCode.Ok);

            default:
                Console.Error.WriteLine($"hooks: unknown action '{action}'. Use install, uninstall or status.");
                return Task.FromResult(ExitCode.Usage);
        }
    }

    /// <summary>
    /// Prints what changed, path by path.
    /// </summary>
    /// <remarks>
    /// Every path is named because this writes into a directory RomMBat does not own: RetroBat
    /// ships its own <c>start/updatestores.bat</c> and a user may have added more, and someone
    /// reading this output has to be able to find and delete exactly what was added.
    /// </remarks>
    private static int Report(EsHookOutcome outcome)
    {
        foreach (var step in outcome.Steps)
        {
            var verb = step.Action switch
            {
                EsHookAction.Installed => "installed",
                EsHookAction.Updated => "updated",
                EsHookAction.AlreadyCurrent => "current",
                EsHookAction.Uninstalled => "removed",
                EsHookAction.NotPresent => "absent",
                _ => "FAILED",
            };

            Console.WriteLine($"  {verb,-9}  {step.Path}");

            if (step.Problem is { } problem)
            {
                Console.Error.WriteLine($"             {problem}");
            }
        }

        if (outcome.Failed > 0)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(
                "The commonest cause is EmulationStation running and holding the file. Quit it and try again.");
            return ExitCode.Refused;
        }

        return ExitCode.Ok;
    }
}
