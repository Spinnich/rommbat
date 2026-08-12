using RomMBat.Core.Content;
using RomMBat.Core.RetroBat;

namespace RomMBat.Agent.Commands;

/// <summary>
/// <c>gamelist</c>: rewrite <c>roms/&lt;folder&gt;/gamelist.xml</c> from local state.
/// </summary>
/// <remarks>
/// Separate from <c>sync</c> because it needs no server at all. Everything it writes comes
/// from the file index and <c>rom_metadata</c>, which is what lets a handheld that has been
/// off the network for a week rewrite a folder after an eviction.
/// <para>
/// It is also where the media policy is read and set, since which artwork a sync fetches and
/// which artwork a gamelist references are the same decision.
/// </para>
/// </remarks>
internal static class GamelistCommand
{
    public static async Task<int> RunAsync(CommandLine command, CancellationToken cancellationToken)
    {
        using var context = AgentContext.Open(command, Console.Error, out var exitCode);
        if (context is null)
        {
            return exitCode;
        }

        if (command.Value("media") is { } media)
        {
            var kinds = MediaPolicy.Parse(media);
            context.Store.Settings.Set(MediaPolicy.SettingKey, MediaPolicy.Format(kinds), DateTimeOffset.UtcNow);
            Console.WriteLine($"Media to fetch: {MediaPolicy.Format(kinds)}");
            return ExitCode.Ok;
        }

        var sync = new GamelistSync(context.Install, context.Store);
        var folders = command.Positional.Count > 0 ? command.Positional : null;

        using var emulationStation = command.Has("no-reload") ? null : new EmulationStationClient();

        var outcome = await sync
            .ApplyAsync(folders, emulationStation, cancellationToken)
            .ConfigureAwait(false);

        Report(outcome);
        return outcome.Folders.Any(folder => folder.Problem is not null) ? ExitCode.Refused : ExitCode.Ok;
    }

    /// <summary>Prints what a gamelist pass did, and what it declined to do.</summary>
    internal static void Report(GamelistSyncOutcome outcome)
    {
        Console.WriteLine($"  {outcome.Summary}");

        foreach (var folder in outcome.Folders.Where(folder => folder.Wrote || folder.Problem is not null))
        {
            if (folder.Problem is { } problem)
            {
                Console.Error.WriteLine($"    {problem}");
                continue;
            }

            var parts = new List<string>();
            if (folder.Added > 0)
            {
                parts.Add($"{folder.Added} added");
            }

            if (folder.Updated > 0)
            {
                parts.Add($"{folder.Updated} updated");
            }

            if (folder.Removed > 0)
            {
                parts.Add($"{folder.Removed} removed");
            }

            if (folder.Foreign > 0)
            {
                parts.Add($"{folder.Foreign} left alone (scraped by something else)");
            }

            Console.WriteLine(
                $"    roms/{folder.Folder}/gamelist.xml: {folder.Entries} entries"
                    + (parts.Count > 0 ? ", " + string.Join(", ", parts) : string.Empty));
        }

        foreach (var warning in outcome.Warnings)
        {
            Console.WriteLine($"    {warning}");
        }

        // Named rather than silent. A reload that did not happen is the difference between the
        // user seeing new box art now and seeing it after they restart EmulationStation.
        switch (outcome.Reload)
        {
            case EsCallResult.Answered:
                Console.WriteLine("    EmulationStation was asked to reload.");
                break;

            case EsCallResult.Failed:
                Console.WriteLine(
                    "    EmulationStation is running but did not accept the reload. If a game is up, it "
                        + "ignores the request; the new metadata appears next time it starts.");
                break;

            default:
                if (outcome.Written > 0)
                {
                    Console.WriteLine(
                        "    EmulationStation is not running, so nothing was reloaded. It reads the new "
                            + "metadata when it next starts.");
                }

                break;
        }
    }
}
