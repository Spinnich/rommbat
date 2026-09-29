using RomMBat.Core;
using RomMBat.Core.Content;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Sets;

namespace RomMBat.Agent.Commands;

/// <summary>
/// <c>uninstall</c>: take the tree back to its state before RomMBat, a preview unless
/// <c>--apply</c>.
/// </summary>
/// <remarks>
/// <b>Setup appends rather than replaces, so removal undoes exactly what it added.</b> The hooks,
/// the menu entry and every per-game memory card conversion always go. <c>--content</c> adds
/// the synced ROMs with their media and gamelist entries, and <c>--bios</c> the synced firmware.
/// A file RomMBat adopted rather than downloaded, and every save, stays.
/// <para>
/// <b>A printer over <see cref="RemovalService"/></b>, which holds the order and every refusal.
/// </para>
/// </remarks>
internal static class UninstallCommand
{
    public static async Task<int> RunAsync(CommandLine command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var context = AgentContext.Open(command, Console.Error, out var exitCode);
        if (context is null)
        {
            return exitCode;
        }

        var service = new RemovalService(context.Session);
        var report = service.Preview(new RemovalScope(command.Has("content"), command.Has("bios")));

        if (!command.Has("apply"))
        {
            Preview(report);
            return ExitCode.Ok;
        }

        var applied = await service.ApplyAsync(report, cancellationToken).ConfigureAwait(false);

        if (applied.Refusal is { } refusal)
        {
            Console.Error.WriteLine(refusal);
            return ExitCode.Refused;
        }

        Report(applied);
        Console.WriteLine();
        LeftBehind(report.Scope);

        return applied.Ok ? ExitCode.Ok : ExitCode.Refused;
    }

    private static void Preview(RemovalReport report)
    {
        if (report.IsBlocked)
        {
            // Said first, because it decides whether the rest can happen at all. Deleting the
            // folder afterwards would take the outbox and the journal with it.
            Console.WriteLine("This install still has work the server has not seen, so --apply will refuse:");

            foreach (var blocker in report.Blockers)
            {
                Console.WriteLine($"  {blocker}");
            }

            Console.WriteLine();
        }

        Console.WriteLine("Removing RomMBat would take out:");
        Console.WriteLine();

        Section("EmulationStation hooks", [.. report.Hooks.Select(path => path.Value)]);
        Section("The EmulationStation menu entry", [.. report.Menu.Select(path => path.Value)]);
        Section(
            "Per-game memory card settings in es_settings.cfg, each put back as it was",
            [.. report.Conversions.Select(conversion => $"{conversion.System}: {conversion.FsName}")]);
        Section(
            "Memory card changes queued for the next EmulationStation quit, called off",
            [.. report.Queued.Select(queued => $"{queued.System}: {queued.FsName}")]);

        if (report.Conversions.Count > 0)
        {
            Console.WriteLine(
                "  Each of those games goes back to the shared memory card. What it saved while it had its");
            Console.WriteLine(
                "  own card stays in that card on disk, and the game will not read it from the shared one.");
            Console.WriteLine();
        }

        if (report.Content is { } content)
        {
            Section(
                $"Synced games, with their media and gamelist entries ({ByteSize.Format(content.Plan.BytesFreed)})",
                [.. content.Plan.Selected.Select(candidate => candidate.File.Path.Value)]);
        }

        if (report.Scope.Firmware)
        {
            Section("Synced firmware under bios/", [.. report.Firmware.Select(file => file.Path.Value)]);
        }

        if (report.EmulationStation is { } running)
        {
            Console.WriteLine($"{running} Quit it before running this with --apply, which refuses while it runs.");
            Console.WriteLine();
        }

        Console.WriteLine("Nothing was removed. Run 'uninstall --apply' to carry this out.");

        if (!report.Scope.Content || !report.Scope.Firmware)
        {
            Console.WriteLine(
                (report.Scope.Content, report.Scope.Firmware) switch
                {
                    (false, false) => "Synced games and firmware stay. Add --content, --bios or both to take them too.",
                    (false, true) => "Synced games stay. Add --content to take them too.",
                    _ => "Synced firmware stays. Add --bios to take it too.",
                });
        }
    }

    private static void Section(string heading, IReadOnlyList<string> lines)
    {
        Console.WriteLine(lines.Count == 0 ? $"{heading}: none" : $"{heading}:");

        foreach (var line in lines)
        {
            Console.WriteLine($"  {line}");
        }

        Console.WriteLine();
    }

    private static void Report(RemovalApplied applied)
    {
        foreach (var step in applied.Hooks!.Steps.Where(step => step.Action is not EsHookAction.NotPresent))
        {
            Console.WriteLine($"  {(step.Action is EsHookAction.Uninstalled ? "removed" : "FAILED"),-9}  {step.Path}");
            Problem(step.Problem);
        }

        foreach (var step in applied.Menu!.Steps.Where(step => step.Action is not EsMenuAction.NotPresent))
        {
            var verb = step.Action switch
            {
                EsMenuAction.Uninstalled => "removed",
                EsMenuAction.LeftAlone => "left",
                _ => "FAILED",
            };

            Console.WriteLine($"  {verb,-9}  {step.Path}   {step.What}");
            Problem(step.Problem);
        }

        if (applied.Cancelled > 0)
        {
            Console.WriteLine($"  called off {applied.Cancelled} queued memory card change(s). Nothing had been written.");
        }

        foreach (var result in applied.Reverted ?? [])
        {
            Console.WriteLine(result.Ok ? $"  {result.Detail}" : $"  FAILED     {result.Detail}");
        }

        if (applied.Content is { } content)
        {
            if (content.Evicted is { } evicted)
            {
                Console.WriteLine();
                Console.WriteLine(evicted.Summary);

                foreach (var problem in evicted.Problems)
                {
                    Console.Error.WriteLine($"  {problem}");
                }
            }

            if (content.Gamelists is { } gamelists)
            {
                Console.WriteLine();
                GamelistCommand.Report(gamelists);
            }
        }

        if (applied.FirmwareRemoved > 0)
        {
            Console.WriteLine($"  removed {applied.FirmwareRemoved} firmware file(s) under bios/");
        }

        foreach (var kept in applied.Kept ?? [])
        {
            Console.WriteLine($"  {kept}");
        }

        foreach (var problem in applied.Problems ?? [])
        {
            Console.Error.WriteLine($"  {problem}");
        }
    }

    private static void Problem(string? problem)
    {
        if (problem is not null)
        {
            Console.Error.WriteLine($"             {problem}");
        }
    }

    /// <summary>What stays, said once so nobody assumes the tree is clean when it is not.</summary>
    private static void LeftBehind(RemovalScope scope)
    {
        Console.WriteLine("Left in place:");
        Console.WriteLine("  every save and save state, which RomMBat never removes;");
        Console.WriteLine("  games RomMBat found already on disk, with the artwork and gamelist entries it added");
        Console.WriteLine("    for them, since those describe your own files;");

        if (!scope.Content)
        {
            Console.WriteLine("  synced games, their media and gamelist entries (uninstall --content takes them);");
        }

        if (!scope.Firmware)
        {
            Console.WriteLine("  synced firmware under bios/ (uninstall --bios takes it);");
        }

        Console.WriteLine("  emulators/rommbat, which holds this program, its database and the pairing token.");
        Console.WriteLine("    Delete that folder to finish. This device also stays in RomM's device list,");
        Console.WriteLine("    where you can remove it.");
    }
}
