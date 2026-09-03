using System.Globalization;
using RomM.Client.Catalog;
using RomMBat.Core;
using RomMBat.Core.Sets;
using RomMBat.Core.Store;

namespace RomMBat.Agent.Commands;

/// <summary>
/// <c>sets</c>: define what this device syncs, and see what a definition resolves to.
/// </summary>
/// <remarks>
/// The milestone's demonstration surface. "My SNES favourites, max 40 games, 8 GB" is
/// <c>sets add</c> plus <c>sets resolve</c>, and everything except <c>resolve</c> works with
/// the server switched off.
/// <para>
/// <b>This is a printer now.</b> Every decision it used to make lives in
/// <see cref="SyncSetService"/> and <see cref="SetResolveService"/>, because the gamepad UI
/// needs the same decisions and the alternative was two implementations of them. What is left
/// here is parsing a command line, choosing an exit code, and writing lines: the parts that
/// are genuinely a console's.
/// </para>
/// <para>
/// <b>The remedy sentences are this file's and not Core's.</b> Core says what is wrong; the
/// line telling you to run <c>platforms list</c> would be false on a screen with no terminal,
/// so it is written here.
/// </para>
/// </remarks>
internal static class SetsCommand
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
            "list" => List(context),
            "add" => await AddAsync(context, command, cancellationToken).ConfigureAwait(false),
            "remove" => Remove(context, command),
            "show" => Show(context, command),
            "resolve" => await ResolveAsync(context, command, cancellationToken).ConfigureAwait(false),
            _ => Usage($"unknown 'sets' verb '{verb}'"),
        };
    }

    private static int List(AgentContext context)
    {
        var sets = context.Sets.List();

        if (sets.Count == 0)
        {
            Console.WriteLine("No sync sets defined. Add one with 'sets add'.");
            return ExitCode.Ok;
        }

        foreach (var summary in sets)
        {
            var set = summary.Set;

            Console.WriteLine($"{set.Name}");
            Console.WriteLine($"  scope:     {SyncSetStore.ScopeText(set.Scope)} {set.ScopeValue}");
            Console.WriteLine($"  policy:    {summary.Policy}");
            Console.WriteLine($"  resolves:  {summary.Games} games, {ByteSize.Format(summary.Bytes)}");
            Console.WriteLine($"  last run:  {Moment(set.LastResolvedAt)}");

            if (set.LastResolutionSummary is { } text)
            {
                Console.WriteLine($"  summary:   {text}");
            }
        }

        return ExitCode.Ok;
    }

    private static async Task<int> AddAsync(
        AgentContext context,
        CommandLine command,
        CancellationToken cancellationToken)
    {
        if (command.Positional.Count < 2)
        {
            return Usage("sets add <name> --scope <kind> --value <id>");
        }

        var name = command.Positional[1];
        var scopeText = command.Value("scope") ?? "platform";

        CatalogScopeKind scope;
        try
        {
            scope = SyncSetStore.ParseScope(scopeText);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Usage(
                $"'{scopeText}' is not a scope. Use collection, smart_collection, virtual_collection, platform or filter.");
        }

        // A filter scope is built from its own flags, so --value has nothing to be. It used to
        // be accepted, never read and never complained about, which produced the widest possible
        // scope from a command that named three games: the empty filter matches the entire
        // library, and the only thing that caught it was an unrelated refusal to resolve arcade
        // without --folder. Refused rather than ignored. #78.
        if (scope == CatalogScopeKind.Filter && command.Value("value") is { Length: > 0 })
        {
            return Usage(
                "a filter scope is built from --search, --favourite, --genres, --regions, "
                    + "--languages and --tags, so --value has nothing to name. Without this "
                    + "refusal the filter would be empty, which matches the whole library.");
        }

        var draft = new SetDraft
        {
            Name = name,
            Scope = scope,
            ScopeValue = command.Value("value"),
            Filter = scope == CatalogScopeKind.Filter
                ? new CatalogFilter
                {
                    SearchTerm = command.Value("search"),
                    Favorite = command.Has("favourite") || command.Has("favorite") ? true : null,
                    Genres = Split(command.Value("genres")),
                    Regions = Split(command.Value("regions")),
                    Languages = Split(command.Value("languages")),
                    Tags = Split(command.Value("tags")),
                }
                : null,
            MaxGames = ParseInt(command.Value("max-games")),
            MaxBytes = ByteSize.Parse(command.Value("max-bytes")),
            Ordering = SyncSetStore.ParseOrdering(command.Value("order")),
            FolderOverride = command.Value("folder"),
        };

        var outcome = context.Sets.Add(draft, DateTimeOffset.UtcNow);

        switch (outcome.Refusal)
        {
            case SetRefusal.None:
                break;

            case SetRefusal.MissingScope:
                Console.Error.WriteLine(outcome.Problem);
                Console.Error.WriteLine(
                    "Use --scope platform or --scope filter, or pair again and approve collections.read.");
                return ExitCode.Refused;

            case SetRefusal.MissingValue:
                return Usage($"a {scopeText} scope needs --value");

            case SetRefusal.UnknownPlatform:
                Console.Error.WriteLine(
                    $"{outcome.Problem} Run 'platforms list' first, then use "
                        + "the fs_slug from its first column or the numeric RomM id.");
                return ExitCode.Usage;

            case SetRefusal.UnknownFolder:
                // Usage, not Refused: a name this install has never heard of is a wrong command
                // line, which is what a wrapping script needs to be able to tell apart from an
                // environment problem. The unknown --value above already answers Usage.
                Console.Error.WriteLine(outcome.Problem);
                return ExitCode.Usage;

            default:
                Console.Error.WriteLine(outcome.Problem);
                return ExitCode.Usage;
        }

        Console.WriteLine($"Added '{outcome.Set!.Name}': {SyncSetService.DescribePolicy(outcome.Set)}");
        await PushConfigAsync(context, command, cancellationToken).ConfigureAwait(false);
        return ExitCode.Ok;
    }

    private static int Remove(AgentContext context, CommandLine command)
    {
        if (command.Positional.Count < 2)
        {
            return Usage("sets remove <name>");
        }

        var outcome = context.Sets.Remove(command.Positional[1]);

        if (outcome.IsRefused)
        {
            Console.Error.WriteLine(outcome.Problem);
            return ExitCode.Usage;
        }

        Console.WriteLine($"Removed '{command.Positional[1]}'. Nothing on disk was touched.");
        return ExitCode.Ok;
    }

    private static int Show(AgentContext context, CommandLine command)
    {
        if (command.Positional.Count < 2)
        {
            return Usage("sets show <name>");
        }

        var detail = context.Sets.Show(command.Positional[1]);

        if (detail is null)
        {
            Console.Error.WriteLine($"No sync set named '{command.Positional[1]}'.");
            return ExitCode.Usage;
        }

        var set = detail.Set;

        Console.WriteLine($"{set.Name}");
        Console.WriteLine($"  scope:    {SyncSetStore.ScopeText(set.Scope)} {set.ScopeValue}");
        Console.WriteLine($"  policy:   {detail.Policy}");
        Console.WriteLine($"  resolved: {Moment(set.LastResolvedAt)}");
        Console.WriteLine($"  contents: {detail.Games} games, {ByteSize.Format(detail.Bytes)}");
        Console.WriteLine();

        foreach (var member in detail.Members)
        {
            Console.WriteLine(
                $"  {member.Position,4}. {member.DisplayName}  [{member.Folder}/{member.FsName}, {ByteSize.Format(member.SizeBytes)}]");
        }

        if (detail.Departed.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"  {detail.Departed.Count} left the set since the last resolve. They are eviction candidates, not deletions:");

            foreach (var member in detail.Departed)
            {
                Console.WriteLine($"    {member.DisplayName}");
            }
        }

        foreach (var exclusion in detail.Exclusions)
        {
            // Only the format exclusion is about the extension. Listing them beside the
            // unmapped group reads as though the format were the problem there too.
            var formats = exclusion.State == MemberState.ExcludedExtension && exclusion.Extensions.Count > 0
                ? $" ({string.Join(", ", exclusion.Extensions.Select(extension => "." + extension))})"
                : string.Empty;

            Console.WriteLine();
            Console.WriteLine($"  {exclusion.Count} {SyncSetService.Describe(exclusion.State)}{formats}");
        }

        return ExitCode.Ok;
    }

    /// <summary>
    /// Picks the sets a verb applies to, and says why when there are none.
    /// </summary>
    /// <remarks>Shared with <c>sync</c> and <c>evict</c>, which take a set name the same way.</remarks>
    internal static IReadOnlyList<SyncSetDefinition>? Select(AgentContext context, string? name)
    {
        var selection = context.Sets.Select(name);

        if (!selection.IsEmpty)
        {
            return selection.Sets;
        }

        // The remedy is the console's. Core states that there are none; only a terminal can be
        // told to type a command.
        Console.Error.WriteLine(
            name is null or "" ? "No sync sets defined. Add one with 'sets add'." : selection.Problem);

        return null;
    }

    private static async Task<int> ResolveAsync(
        AgentContext context,
        CommandLine command,
        CancellationToken cancellationToken)
    {
        var sets = Select(context, command.Positional.Count > 1 ? command.Positional[1] : null);
        if (sets is null)
        {
            return ExitCode.Usage;
        }

        using var connection = context.Authenticate(command, Console.Error, out var exitCode);
        if (connection is null)
        {
            return exitCode;
        }

        var resolved = await ReportResolveAsync(context, connection, sets, cancellationToken).ConfigureAwait(false);
        await PushConfigAsync(context, command, cancellationToken).ConfigureAwait(false);
        return resolved;
    }

    /// <summary>
    /// Resolves each set and prints what happened, returning the worst exit code.
    /// </summary>
    /// <remarks>
    /// Shared with <c>sync</c>, which re-resolves before it decides what to fetch, because
    /// smart-collection membership drifts server-side and syncing a stale membership would pull
    /// games the set no longer contains.
    /// </remarks>
    internal static async Task<int> ReportResolveAsync(
        AgentContext context,
        RomM.Client.RomMConnection connection,
        IReadOnlyList<SyncSetDefinition> sets,
        CancellationToken cancellationToken)
    {
        var reports = await new SetResolveService(context.Session, connection)
            .ResolveAsync(sets, progress: null, cancellationToken)
            .ConfigureAwait(false);

        var worst = ExitCode.Ok;

        foreach (var report in reports)
        {
            switch (report.State)
            {
                case ResolveState.Resolved:
                    Console.WriteLine($"{report.SetName}: {report.Summary}");
                    break;

                case ResolveState.Refused:
                case ResolveState.NeedsFolderChoice:
                    Console.Error.WriteLine($"{report.SetName}: {report.Problem}");

                    if (report.State == ResolveState.NeedsFolderChoice)
                    {
                        Console.Error.WriteLine("  sets add ... --folder <name>, or edit this set and set one.");
                    }

                    return ExitCode.Refused;

                default:
                    // Interrupted. Either the server went away or the walk stopped part way,
                    // and both leave a cursor the next run picks up.
                    Console.Error.WriteLine(
                        report.Problem is null
                            ? $"{report.SetName}: {report.Summary}"
                            : report.Problem);

                    if (report.Problem is null)
                    {
                        Console.Error.WriteLine(
                            $"  stopped at offset {report.Offset} of {report.Total}. The next run continues from there.");
                    }

                    return ExitCode.Offline;
            }
        }

        return worst;
    }

    /// <summary>Pushes the definitions so they follow the user, and says when it could not.</summary>
    private static async Task<bool> PushConfigAsync(
        AgentContext context,
        CommandLine command,
        CancellationToken cancellationToken)
    {
        var push = await new RoamingConfigService(context.Session)
            .PushAsync(command.Value("passphrase"), cancellationToken)
            .ConfigureAwait(false);

        if (push.Note is { } note)
        {
            Console.WriteLine($"  {note}");
        }

        return push.Pushed;
    }

    private static string Moment(DateTimeOffset? at) =>
        at is { } moment
            ? moment.ToUniversalTime().ToString("u", CultureInfo.InvariantCulture)
            : "never";

    private static string[] Split(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static int? ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static int Usage(string message)
    {
        Console.Error.WriteLine($"rommbat-agent: {message}");
        Console.Error.WriteLine("  sets list");
        Console.Error.WriteLine("  sets add <name> --scope platform --value <id> [--max-games N] [--max-bytes 8GB]");
        Console.Error.WriteLine("                  [--order name|size_asc|size_desc|recent] [--folder <retrobat folder>]");
        Console.Error.WriteLine("  sets add <name> --scope filter [--search TEXT] [--favourite] [--genres A,B]");
        Console.Error.WriteLine("  sets show <name>");
        Console.Error.WriteLine("  sets remove <name>");
        Console.Error.WriteLine("  sets resolve [<name>]");
        return ExitCode.Usage;
    }
}
