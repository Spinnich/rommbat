using RomMBat.Core;
using System.Globalization;
using RomM.Client;
using RomM.Client.Catalog;
using RomMBat.Core.Mapping;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;

namespace RomMBat.Agent.Commands;

/// <summary>
/// <c>sets</c>: define what this device syncs, and see what a definition resolves to.
/// </summary>
/// <remarks>
/// The milestone's demonstration surface. "My SNES favourites, max 40 games, 8 GB" is
/// <c>sets add</c> plus <c>sets resolve</c>, and everything except <c>resolve</c> works with
/// the server switched off.
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
        var sets = context.Store.SyncSets.List();
        if (sets.Count == 0)
        {
            Console.WriteLine("No sync sets defined. Add one with 'sets add'.");
            return ExitCode.Ok;
        }

        foreach (var set in sets)
        {
            var (games, bytes) = context.Store.SyncSets.MemberTotals(set.Id);
            Console.WriteLine($"{set.Name}");
            Console.WriteLine($"  scope:     {SyncSetStore.ScopeText(set.Scope)} {set.ScopeValue}");
            Console.WriteLine($"  policy:    {DescribePolicy(set)}");
            Console.WriteLine($"  resolves:  {games} games, {ByteSize.Format(bytes)}");
            Console.WriteLine($"  last run:  {(set.LastResolvedAt is { } at ? at.ToUniversalTime().ToString("u", CultureInfo.InvariantCulture) : "never")}");

            if (set.LastResolutionSummary is { } summary)
            {
                Console.WriteLine($"  summary:   {summary}");
            }
        }

        return ExitCode.Ok;
    }

    private static async Task<int> AddAsync(AgentContext context, CommandLine command, CancellationToken cancellationToken)
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
            return Usage($"'{scopeText}' is not a scope. Use collection, smart_collection, virtual_collection, platform or filter.");
        }

        // A narrowed grant degrades by feature rather than failing later with a 403 in the
        // middle of a sync, so the refusal happens here, at the point of definition.
        if (RequiresCollections(scope))
        {
            var scopes = context.Store.Device.Read()?.Scopes ?? GrantedScopes.None;
            if (!scopes.Allows(RomMFeature.CollectionSets))
            {
                var requirement = GrantedScopes.Requirements.Single(r => r.Feature == RomMFeature.CollectionSets);
                Console.Error.WriteLine(
                    $"This pairing was not granted {string.Join(", ", requirement.RequiredScopes)}, so {requirement.WithoutIt.ToLowerInvariant()}.");
                Console.Error.WriteLine("Use --scope platform or --scope filter, or pair again and approve collections.read.");
                return ExitCode.Refused;
            }
        }

        var value = command.Value("value") ?? string.Empty;
        if (scope != CatalogScopeKind.Filter && string.IsNullOrWhiteSpace(value))
        {
            return Usage($"a {scopeText} scope needs --value");
        }

        // A platform scope takes the fs_slug the mapping table lists as readily as the
        // numeric id the API wants, because the fs_slug is the one a person has in front of
        // them. Resolved here so the stored scope stays the id the endpoint accepts.
        if (scope == CatalogScopeKind.Platform && !int.TryParse(value, out _))
        {
            var known = context.Store.PlatformMap.Find(value);
            if (known?.PlatformId is not { } platformId)
            {
                Console.Error.WriteLine(
                    $"'{value}' is not a platform this install knows. Run 'platforms list' first, then use "
                        + "the fs_slug from its first column or the numeric RomM id.");
                return ExitCode.Usage;
            }

            value = platformId.ToString(CultureInfo.InvariantCulture);
        }

        if (scope == CatalogScopeKind.Filter)
        {
            value = CatalogFilterJson.Write(new CatalogFilter
            {
                SearchTerm = command.Value("search"),
                Favorite = command.Has("favourite") || command.Has("favorite") ? true : null,
                Genres = Split(command.Value("genres")),
                Regions = Split(command.Value("regions")),
                Languages = Split(command.Value("languages")),
                Tags = Split(command.Value("tags")),
            });
        }

        var folder = command.Value("folder");
        if (folder is not null && !EsSystemsFile.Load(context.Install).HasFolder(folder))
        {
            // Usage, not Refused: a name this install has never heard of is a wrong command
            // line, which is what a wrapping script needs to be able to tell apart from an
            // environment problem. The unknown --value a few lines above already answers Usage.
            Console.Error.WriteLine($"'{folder}' is not a system in this install's es_systems.cfg.");
            return ExitCode.Usage;
        }

        var definition = new SyncSetDefinition
        {
            Name = name,
            Scope = scope,
            ScopeValue = value,
            MaxGames = ParseInt(command.Value("max-games")),
            MaxBytes = ByteSize.Parse(command.Value("max-bytes")),
            Ordering = SyncSetStore.ParseOrdering(command.Value("order")),
            FolderOverride = folder,
        };

        SyncSetDefinition added;
        try
        {
            added = context.Store.SyncSets.Add(definition, DateTimeOffset.UtcNow);
        }
        catch (SyncSetExistsException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCode.Usage;
        }

        Console.WriteLine($"Added '{added.Name}': {DescribePolicy(added)}");
        await PushConfigAsync(context, command, cancellationToken).ConfigureAwait(false);
        return ExitCode.Ok;
    }

    private static int Remove(AgentContext context, CommandLine command)
    {
        if (command.Positional.Count < 2)
        {
            return Usage("sets remove <name>");
        }

        if (!context.Store.SyncSets.Remove(command.Positional[1]))
        {
            Console.Error.WriteLine($"No sync set named '{command.Positional[1]}'.");
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

        var set = context.Store.SyncSets.Find(command.Positional[1]);
        if (set is null)
        {
            Console.Error.WriteLine($"No sync set named '{command.Positional[1]}'.");
            return ExitCode.Usage;
        }

        var members = context.Store.SyncSets.Members(set.Id);
        var (games, bytes) = context.Store.SyncSets.MemberTotals(set.Id);

        Console.WriteLine($"{set.Name}");
        Console.WriteLine($"  scope:    {SyncSetStore.ScopeText(set.Scope)} {set.ScopeValue}");
        Console.WriteLine($"  policy:   {DescribePolicy(set)}");
        Console.WriteLine($"  resolved: {(set.LastResolvedAt is { } at ? at.ToUniversalTime().ToString("u", CultureInfo.InvariantCulture) : "never")}");
        Console.WriteLine($"  contents: {games} games, {ByteSize.Format(bytes)}");
        Console.WriteLine();

        foreach (var member in members)
        {
            Console.WriteLine($"  {member.Position,4}. {member.DisplayName}  [{member.Folder}/{member.FsName}, {ByteSize.Format(member.SizeBytes)}]");
        }

        var departed = context.Store.SyncSets.Members(set.Id, MemberState.Departed);
        if (departed.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"  {departed.Count} left the set since the last resolve. They are eviction candidates, not deletions:");
            foreach (var member in departed)
            {
                Console.WriteLine($"    {member.DisplayName}");
            }
        }

        foreach (var exclusion in context.Store.SyncSets.Exclusions(set.Id))
        {
            // Only the format exclusion is about the extension. Listing them beside the
            // unmapped group reads as though the format were the problem there too.
            var formats = exclusion.State == MemberState.ExcludedExtension && exclusion.Extensions.Count > 0
                ? $" ({string.Join(", ", exclusion.Extensions.Select(extension => "." + extension))})"
                : string.Empty;

            Console.WriteLine();
            Console.WriteLine($"  {exclusion.Count} {Describe(exclusion.State)}{formats}");
        }

        return ExitCode.Ok;
    }

    /// <summary>
    /// Picks the sets a verb applies to: the one named, or all of them.
    /// </summary>
    /// <remarks>
    /// Shared with <c>sync</c> and <c>evict</c>, which take a set name the same way.
    /// </remarks>
    internal static IReadOnlyList<SyncSetDefinition>? Select(AgentContext context, string? name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            var named = context.Store.SyncSets.Find(name);
            if (named is not null)
            {
                return [named];
            }

            Console.Error.WriteLine($"No sync set named '{name}'.");
            return null;
        }

        var all = context.Store.SyncSets.List();
        if (all.Count > 0)
        {
            return all;
        }

        Console.Error.WriteLine("No sync sets defined. Add one with 'sets add'.");
        return null;
    }

    private static async Task<int> ResolveAsync(AgentContext context, CommandLine command, CancellationToken cancellationToken)
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

        var resolved = await ResolveSetsAsync(context, connection, sets, cancellationToken).ConfigureAwait(false);
        await PushConfigAsync(context, command, cancellationToken).ConfigureAwait(false);
        return resolved;
    }

    /// <summary>
    /// Walks each set's scope and records what it resolves to.
    /// </summary>
    /// <remarks>
    /// Shared with <c>sync</c>, which re-resolves before it decides what to fetch, because
    /// smart-collection membership drifts server-side and syncing a stale membership would pull
    /// games the set no longer contains.
    /// </remarks>
    internal static async Task<int> ResolveSetsAsync(
        AgentContext context,
        RomMConnection connection,
        IReadOnlyList<SyncSetDefinition> sets,
        CancellationToken cancellationToken)
    {
        var install = EsSystemsFile.Load(context.Install);
        var resolver = new SetResolver(install, new PlatformResolver(install, context.Store.PlatformMap.Overrides()));
        var worst = ExitCode.Ok;

        foreach (var set in sets)
        {
            var endpoint = $"roms:set:{set.Id}";
            var cursor = context.Store.Cursors.BeginWalk(endpoint, DateTimeOffset.UtcNow);
            var startOffset = cursor.ResumeOffset ?? 0;
            var pager = new RomPager(connection, SetResolver.QueryFor(set), startOffset: startOffset);

            // Every row this walk writes is stamped with the walk's start, so a segment can
            // tell what this walk has already found from what the last one left behind.
            var walkStartedAt = cursor.WalkStartedAt ?? DateTimeOffset.UtcNow;
            var carried = startOffset > 0
                ? context.Store.SyncSets.MembersFrom(set.Id, walkStartedAt)
                : [];

            SetResolution resolution;
            try
            {
                resolution = await resolver
                    .ResolveAsync(set, pager, walkStartedAt, carried, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (RomMUnreachableException ex)
            {
                Console.Error.WriteLine(ex.Message);
                context.Store.Cursors.RecordProgress(endpoint, pager.Offset, pager.Total, DateTimeOffset.UtcNow);
                return ExitCode.Offline;
            }

            worst = Math.Max(worst, Report(context, set, resolution, endpoint, pager, walkStartedAt));
        }

        return worst;
    }

    private static int Report(
        AgentContext context,
        SyncSetDefinition set,
        SetResolution resolution,
        string endpoint,
        RomPager pager,
        DateTimeOffset walkStartedAt)
    {
        var now = DateTimeOffset.UtcNow;

        if (resolution.Outcome is ResolutionOutcome.Refused or ResolutionOutcome.NeedsFolderChoice)
        {
            context.Store.Cursors.AbandonWalk(endpoint, now);
            Console.Error.WriteLine($"{set.Name}: {resolution.Problem}");

            if (resolution.Outcome == ResolutionOutcome.NeedsFolderChoice)
            {
                Console.Error.WriteLine($"  sets add ... --folder <name>, or edit this set and set one.");
            }

            return ExitCode.Refused;
        }

        var complete = resolution.Outcome == ResolutionOutcome.Resolved;

        // A segment of a walk is an accumulator, not a statement about what the set holds, so
        // only a completed walk retires the rows it did not find.
        context.Store.SyncSets.ReplaceMembers(
            set.Id,
            [.. resolution.Members, .. resolution.Excluded],
            resolution.Summary,
            walkStartedAt,
            complete);

        // Upserted rather than replaced, and after the membership: a resumed walk only carries
        // metadata for the segment it just read, and the rows an earlier segment wrote are
        // still the only copy of that game's description.
        foreach (var metadata in resolution.Metadata)
        {
            context.Store.Metadata.Record(metadata);
        }

        if (!complete)
        {
            context.Store.Cursors.RecordProgress(endpoint, pager.Offset, pager.Total, now);
            Console.Error.WriteLine($"{set.Name}: {resolution.Summary}");
            Console.Error.WriteLine($"  stopped at offset {pager.Offset} of {pager.Total}. The next run continues from there.");
            return ExitCode.Offline;
        }

        context.Store.Cursors.CompleteWalk(endpoint, now);
        Console.WriteLine($"{set.Name}: {resolution.Summary}");
        return ExitCode.Ok;
    }

    /// <summary>
    /// Pushes the definitions to <c>Device.sync_config</c> so they follow the user.
    /// </summary>
    /// <remarks>
    /// Best effort by design. Failing to roam a definition is not a reason to fail the
    /// command that created it: the local store is the authority and the push is a mirror.
    /// </remarks>
    private static async Task<bool> PushConfigAsync(
        AgentContext context,
        CommandLine command,
        CancellationToken cancellationToken)
    {
        var device = context.Store.Device.Read();
        if (device?.RomMDeviceId is null || !device.Scopes.Allows(RomMFeature.DeviceSync))
        {
            Console.WriteLine("  (definitions stay on this device: devices.write was not granted)");
            return false;
        }

        try
        {
            using var connection = context.Authenticate(command, TextWriter.Null, out _);
            if (connection is null)
            {
                return false;
            }

            var current = await connection.GetDeviceAsync(device.RomMDeviceId, cancellationToken).ConfigureAwait(false);
            var document = RoamingSyncConfig.FromStore(context.Store, DateTimeOffset.UtcNow);
            var merged = document.MergeInto(current.IsSuccess ? current.Value!.Sync_config : null);

            var pushed = await connection
                .UpdateDeviceSyncConfigAsync(device.RomMDeviceId, merged, cancellationToken)
                .ConfigureAwait(false);

            if (!pushed.IsSuccess)
            {
                Console.WriteLine($"  (definitions stay on this device: {pushed.Message})");
            }

            return pushed.IsSuccess;
        }
        catch (RomMUnreachableException)
        {
            Console.WriteLine("  (definitions stay on this device until the server is reachable)");
            return false;
        }
    }

    private static bool RequiresCollections(CatalogScopeKind scope) =>
        scope is CatalogScopeKind.Collection or CatalogScopeKind.SmartCollection or CatalogScopeKind.VirtualCollection;

    private static string DescribePolicy(SyncSetDefinition set)
    {
        var parts = new List<string>
        {
            set.MaxGames is { } games ? $"max {games} games" : "no game cap",
            set.MaxBytes is { } bytes ? $"max {ByteSize.Format(bytes)}" : "no size cap",
            $"ordered by {SyncSetStore.OrderingText(set.Ordering)}",
        };

        if (set.FolderOverride is { } folder)
        {
            parts.Add($"into {folder}");
        }

        return string.Join(", ", parts);
    }

    private static string Describe(MemberState state) => state switch
    {
        MemberState.ExcludedExtension => "skipped, format not supported by this system",
        MemberState.ExcludedUnmapped => "skipped, no RetroBat folder for their platform",
        MemberState.ExcludedMultiFile => "skipped, held as several files which this version cannot sync yet",
        MemberState.ExcludedFilesystemLimit => "skipped, too large for this drive's filesystem",
        MemberState.ExcludedOverCount => "past the game cap",
        MemberState.ExcludedOverBytes => "past the byte budget",
        MemberState.Departed => "no longer in the scope",
        _ => "in the set",
    };

    private static string[] Split(string? value) =>
        string.IsNullOrWhiteSpace(value) ? [] : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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
