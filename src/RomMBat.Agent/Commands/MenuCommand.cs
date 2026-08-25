using RomMBat.Core.RetroBat;

namespace RomMBat.Agent.Commands;

/// <summary>
/// <c>menu</c>: put RomMBat in the EmulationStation menu, or take it out.
/// </summary>
/// <remarks>
/// <b>Installed on the first <c>sync</c> as well, announced, and that is a wider claim than
/// the hooks make.</b> A hook adds a file beside the existing scripts and changes nothing a
/// user sees; a menu entry adds a visible item to their front end. It is installed anyway
/// because it is the only route to RomMBat that does not need a terminal, and the whole point
/// of the entry is a user who never opens one. What that buys is owed back in candour: the
/// install names every path it wrote, and <c>menu uninstall</c> takes all of it out.
/// </remarks>
internal static class MenuCommand
{
    public static Task<int> RunAsync(CommandLine command, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        using var context = AgentContext.Open(command, Console.Error, out var exitCode);
        if (context is null)
        {
            return Task.FromResult(exitCode);
        }

        var entry = new EsMenuEntry(context.Install);
        var action = command.Positional.Count > 0 ? command.Positional[0] : "status";

        switch (action)
        {
            case "install":
                return Task.FromResult(Report(entry.Install()));

            case "uninstall":
                return Task.FromResult(Report(entry.Uninstall()));

            case "status":
                return Task.FromResult(Status(context, entry));

            default:
                Console.Error.WriteLine($"menu: unknown action '{action}'. Use install, uninstall or status.");
                return Task.FromResult(ExitCode.Usage);
        }
    }

    /// <summary>
    /// Says whether the entry is there, and what it looks like now rather than what RomMBat
    /// would have written.
    /// </summary>
    /// <remarks>
    /// A name or an image the user changed is reported and not corrected, the same rule that
    /// governs a per-game setting somebody else wrote. What is worth flagging is the opposite
    /// case: half a registration, which shows in the menu as a bare filename or does not show
    /// at all, and which the user cannot diagnose from the screen.
    /// </remarks>
    private static int Status(AgentContext context, EsMenuEntry entry)
    {
        var installed = entry.IsInstalled();

        Console.WriteLine(installed
            ? "RomMBat is in the EmulationStation menu."
            : "RomMBat is not in the EmulationStation menu. Run 'rommbat-agent menu install', or just sync.");

        var menu = context.Install.Resolve(EsMenuEntry.MenuPath);
        var gamelist = context.Install.Resolve(EsMenuEntry.GamelistPath);
        var logo = context.Install.Resolve(EsMenuEntry.LogoPath);

        Console.WriteLine($"  {(File.Exists(menu) ? "present" : "absent "),-8}  {EsMenuEntry.MenuPath}");

        var hasEntry = false;
        string? name = null;
        string? image = null;

        try
        {
            if (File.Exists(gamelist))
            {
                var document = GamelistDocument.Load(gamelist);
                hasEntry = document.Contains(EsMenuEntry.EntryPath);
                name = document.ValueOf(EsMenuEntry.EntryPath, "name");
                image = document.ValueOf(EsMenuEntry.EntryPath, "image");
            }
        }
        catch (GamelistParseException ex)
        {
            Console.Error.WriteLine($"  {ex.Message}");
            return ExitCode.Refused;
        }

        Console.WriteLine($"  {(hasEntry ? "present" : "absent "),-8}  {EsMenuEntry.GamelistPath}  (one <game> element)");
        Console.WriteLine($"  {(File.Exists(logo) ? "present" : "absent "),-8}  {EsMenuEntry.LogoPath}");

        if (hasEntry)
        {
            Console.WriteLine();
            Console.WriteLine($"  It shows as   {name ?? "(no name, so ES shows the filename)"}");
            Console.WriteLine($"  Artwork       {image ?? "(none)"}");

            // Reported, never corrected. A user who renamed the entry or pointed it at their
            // own artwork keeps that, and saying so here is how they find out RomMBat noticed.
            if (name is not null && name != "RomMBat")
            {
                Console.WriteLine("  The name is not the one RomMBat writes, so it was set here and is left alone.");
            }

            if (image is not null && image != "./media/rommbat-logo.png")
            {
                Console.WriteLine("  The artwork is not the one RomMBat writes, so it is left alone too.");
            }
        }

        if (File.Exists(menu) != hasEntry)
        {
            Console.WriteLine();
            Console.WriteLine(hasEntry
                ? "Only the gamelist half is there. EmulationStation does not list an entry whose "
                    + ".menu file is missing, so nothing appears in the menu at all."
                : "Only the .menu file is there, so the entry appears under its bare filename with "
                    + "no artwork.");
            Console.WriteLine("Run 'rommbat-agent menu install' to put the other half back.");
        }

        return ExitCode.Ok;
    }

    /// <summary>
    /// Prints what changed, path by path.
    /// </summary>
    /// <remarks>
    /// Every path is named because this writes into a directory RomMBat does not own: 93 of
    /// RetroBat's own entries live in that gamelist, and someone reading this output has to be
    /// able to find and delete exactly what was added.
    /// </remarks>
    private static int Report(EsMenuOutcome outcome)
    {
        foreach (var step in outcome.Steps)
        {
            var verb = step.Action switch
            {
                EsMenuAction.Installed => "installed",
                EsMenuAction.Updated => "updated",
                EsMenuAction.AlreadyCurrent => "current",
                EsMenuAction.Uninstalled => "removed",
                EsMenuAction.NotPresent => "absent",
                EsMenuAction.LeftAlone => "left alone",
                _ => "FAILED",
            };

            Console.WriteLine($"  {verb,-10}  {step.Path}   {step.What}");

            if (step.Problem is { } problem)
            {
                Console.Error.WriteLine($"              {problem}");
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
