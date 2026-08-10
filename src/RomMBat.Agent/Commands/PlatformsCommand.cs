using RomM.Client;
using RomM.Client.Catalog;
using RomMBat.Core.Mapping;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;

namespace RomMBat.Agent.Commands;

/// <summary>
/// <c>platforms</c>: the mapping surface, without a UI.
/// </summary>
/// <remarks>
/// This is the data M7's Platform Mapping screen renders. Everything it shows is here:
/// each platform, the folder it resolved to, which layer of the chain answered, and the
/// alternatives. <c>list</c> works with the server switched off, reading the last
/// resolution back out of the store.
/// </remarks>
internal static class PlatformsCommand
{
    public static async Task<int> RunAsync(CommandLine command, CancellationToken cancellationToken)
    {
        using var context = AgentContext.Open(command, Console.Error, out var exitCode);
        if (context is null)
        {
            return exitCode;
        }

        var verb = command.Positional.Count > 0 ? command.Positional[0] : "list";

        return verb switch
        {
            "list" => await ListAsync(context, command, cancellationToken).ConfigureAwait(false),
            "map" => Map(context, command),
            "unmap" => Unmap(context, command),
            _ => Usage($"unknown 'platforms' verb '{verb}'"),
        };
    }

    private static async Task<int> ListAsync(
        AgentContext context,
        CommandLine command,
        CancellationToken cancellationToken)
    {
        // Refreshing needs the server; showing what was last resolved does not. Offline is
        // the normal state for this app, so the offline path is the one that always works.
        if (!command.Has("offline"))
        {
            var refreshed = await RefreshAsync(context, command, cancellationToken).ConfigureAwait(false);
            if (refreshed != ExitCode.Ok && refreshed != ExitCode.Offline)
            {
                return refreshed;
            }
        }

        var rows = context.Store.PlatformMap.List();
        if (rows.Count == 0)
        {
            Console.WriteLine("No platforms known yet. Run 'platforms list' with the server reachable.");
            return ExitCode.Ok;
        }

        // Listed by fs_slug, because that is what RomM keeps unique and what 'platforms map'
        // takes. Two platforms can share a slug, so a slug column alone would look duplicated.
        Console.WriteLine($"{"Platform (fs_slug)",-26} {"Folder",-18} {"From",-11} Notes");
        Console.WriteLine(new string('-', 100));

        foreach (var row in rows)
        {
            var folder = row.Folder ?? (row.SuggestedFolder is { } suggestion ? $"({suggestion}?)" : "-");
            Console.WriteLine($"{Trim(row.FsSlug, 26),-26} {Trim(folder, 18),-18} {Describe(row.ResolvedBy),-11} {row.Explanation}");
        }

        Console.WriteLine();
        Console.WriteLine($"{rows.Count(row => row.Folder is not null)} of {rows.Count} mapped. "
            + $"{rows.Count(row => row.IsUserChoice)} set by you.");

        var choices = rows.Where(row => row.RequiresChoice && row.Folder is null).ToList();
        foreach (var row in choices)
        {
            Console.WriteLine($"  '{row.FsSlug}' needs a folder chosen: {string.Join(", ", row.CandidateFolders)}");
        }

        var suggestions = rows.Where(row => row.SuggestedFolder is not null).ToList();
        foreach (var row in suggestions)
        {
            Console.WriteLine($"  '{row.FsSlug}' looks like '{row.SuggestedFolder}'. "
                + $"Confirm with: platforms map {row.FsSlug} {row.SuggestedFolder}");
        }

        return ExitCode.Ok;
    }

    private static async Task<int> RefreshAsync(
        AgentContext context,
        CommandLine command,
        CancellationToken cancellationToken)
    {
        using var connection = context.Authenticate(command, Console.Error, out var exitCode);
        if (connection is null)
        {
            return exitCode;
        }

        RomMResponse<IReadOnlyList<PlatformRow>> response;
        try
        {
            response = await connection.ListPlatformsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (RomMUnreachableException)
        {
            Console.Error.WriteLine("Server unreachable. Showing the last resolution instead.");
            return ExitCode.Offline;
        }

        if (!response.IsSuccess)
        {
            Console.Error.WriteLine(response.Message);
            return response.NeedsRepairing ? ExitCode.NotPaired : ExitCode.Refused;
        }

        var install = EsSystemsFile.Load(context.Install);
        var resolver = new PlatformResolver(install, context.Store.PlatformMap.Overrides());
        var now = DateTimeOffset.UtcNow;

        context.Store.InTransaction(() =>
        {
            foreach (var platform in response.Value!)
            {
                var resolution = resolver.Resolve(new RomMPlatform(
                    platform.Id,
                    platform.Slug,
                    platform.FsSlug,
                    platform.Label,
                    platform.RomCount));

                context.Store.PlatformMap.Record(resolution, now);
            }
        });

        return ExitCode.Ok;
    }

    private static int Map(AgentContext context, CommandLine command)
    {
        if (command.Positional.Count < 3)
        {
            return Usage("platforms map <romm-fs-slug> <retrobat-folder>");
        }

        var fsSlug = command.Positional[1];
        var folder = command.Positional[2];

        var install = EsSystemsFile.Load(context.Install);
        if (!install.HasFolder(folder))
        {
            Console.Error.WriteLine(
                $"'{folder}' is not a system in this install's es_systems.cfg. Games synced there would "
                    + "never appear in EmulationStation.");
            return ExitCode.Refused;
        }

        var known = context.Store.PlatformMap.Find(fsSlug);
        context.Store.PlatformMap.SetOverride(fsSlug, folder, DateTimeOffset.UtcNow, known?.Slug, known?.PlatformId);
        Console.WriteLine($"'{fsSlug}' now maps to '{folder}'. This is a choice, so nothing will overwrite it.");
        return ExitCode.Ok;
    }

    private static int Unmap(AgentContext context, CommandLine command)
    {
        if (command.Positional.Count < 2)
        {
            return Usage("platforms unmap <romm-fs-slug>");
        }

        var fsSlug = command.Positional[1];

        if (!context.Store.PlatformMap.ClearOverride(fsSlug, DateTimeOffset.UtcNow))
        {
            Console.Error.WriteLine($"'{fsSlug}' has no override to clear.");
            return ExitCode.Usage;
        }

        Console.WriteLine($"Cleared the override for '{fsSlug}'. It will be resolved again on the next refresh.");
        return ExitCode.Ok;
    }

    private static string Describe(MappingSource source) => source switch
    {
        MappingSource.User => "you",
        MappingSource.FsSlug => "fs_slug",
        MappingSource.Bundled => "bundled",
        MappingSource.Normalized => "suggested",
        _ => "unmapped",
    };

    private static string Trim(string value, int width) =>
        value.Length <= width ? value : value[..(width - 1)] + "~";

    private static int Usage(string message)
    {
        Console.Error.WriteLine($"rommbat-agent: {message}");
        Console.Error.WriteLine("  platforms list [--offline]");
        Console.Error.WriteLine("  platforms map <romm-fs-slug> <retrobat-folder>");
        Console.Error.WriteLine("  platforms unmap <romm-fs-slug>");
        return ExitCode.Usage;
    }
}
