using System.Globalization;
using RomM.Client;
using RomM.Client.Catalog;
using RomMBat.Core.Sync;

namespace RomMBat.Agent.Commands;

/// <summary>
/// <c>browse</c>: one page of the catalog, to show the pager working without a UI.
/// </summary>
/// <remarks>
/// Deliberately one page. It exists to make the paged read observable from a terminal, not
/// to be a search tool: the sidecars stay off, nothing is cached, and nothing accumulates.
/// </remarks>
internal static class BrowseCommand
{
    public static async Task<int> RunAsync(CommandLine command, CancellationToken cancellationToken)
    {
        using var context = AgentContext.Open(command, Console.Error, out var exitCode);
        if (context is null)
        {
            return exitCode;
        }

        using var connection = context.Authenticate(command, Console.Error, out exitCode);
        if (connection is null)
        {
            return exitCode;
        }

        var platform = command.Value("platform");
        var query = new CatalogQuery
        {
            Scope = platform is null ? CatalogScopeKind.Filter : CatalogScopeKind.Platform,
            ScopeId = platform,
            SearchTerm = command.Value("search"),
        };

        var limit = int.TryParse(command.Value("limit"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, 1, RomPager.MaximumPageSize)
            : RomPager.DefaultPageSize;

        var offset = int.TryParse(command.Value("offset"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var start)
            ? Math.Max(start, 0)
            : 0;

        var response = await connection.GetRomPageAsync(query, limit, offset, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccess)
        {
            Console.Error.WriteLine(response.Message);
            return response.NeedsRepairing ? ExitCode.NotPaired : ExitCode.Refused;
        }

        var page = response.Value!;
        foreach (var row in page.Items)
        {
            Console.WriteLine($"{row.Id,8}  {row.PlatformSlug,-14} {SetResolver.FormatBytes(row.SizeBytes),9}  {row.DisplayName}");
        }

        Console.WriteLine();
        Console.WriteLine($"{page.Items.Count} of {page.Total} from offset {offset}. "
            + $"Next page: --offset {offset + page.Items.Count}");

        return ExitCode.Ok;
    }
}
