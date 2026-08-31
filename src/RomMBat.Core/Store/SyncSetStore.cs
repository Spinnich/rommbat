using System.Globalization;
using Microsoft.Data.Sqlite;
using RomM.Client.Catalog;

namespace RomMBat.Core.Store;

/// <summary>Why a candidate is or is not in the set.</summary>
public enum MemberState
{
    /// <summary>In the set as of the last resolution.</summary>
    Member,

    /// <summary>
    /// Was in the set and is not any more.
    /// </summary>
    /// <remarks>
    /// An eviction candidate, never an immediate delete. Smart-collection membership drifts
    /// server-side, and a game leaving a collection is not a reason to throw away a download
    /// or, worse, a save that has not been flushed yet.
    /// </remarks>
    Departed,

    /// <summary>The resolved folder cannot launch this file format.</summary>
    ExcludedExtension,

    /// <summary>The ROM's platform has no RetroBat folder on this install.</summary>
    ExcludedUnmapped,

    /// <summary>
    /// RomM holds this ROM as several files, which v1 does not sync.
    /// </summary>
    /// <remarks>
    /// Its own state rather than <see cref="ExcludedExtension"/>, because the format is not
    /// what is wrong with it: RomM serves it as a zip built on demand, any <c>Range</c> on
    /// that download is refused 403 by nginx, and the ROM-level hashes describe neither the
    /// zip nor its members. Telling someone their <c>.bin</c>/<c>.cue</c> set is an
    /// unsupported format would send them to fix the wrong thing.
    /// </remarks>
    ExcludedMultiFile,

    /// <summary>
    /// The target volume cannot hold a file this large, which today means over 4 GB on FAT32.
    /// </summary>
    /// <remarks>
    /// Decided before the download starts. The write itself fails as Win32 112
    /// <c>ERROR_DISK_FULL</c>, "There is not enough space on the disk", on a volume with
    /// plenty free, and that message must never reach a user: it sends them to delete files
    /// that are not the problem.
    /// </remarks>
    ExcludedFilesystemLimit,

    /// <summary>Past the set's game count.</summary>
    ExcludedOverCount,

    /// <summary>Past the set's byte budget.</summary>
    ExcludedOverBytes,
}

/// <summary>A sync set as stored: a scope, a policy, and when it last resolved.</summary>
public sealed record SyncSetDefinition
{
    public long Id { get; init; }

    public required string Name { get; init; }

    public required CatalogScopeKind Scope { get; init; }

    /// <summary>A collection, smart-collection or platform id, a virtual collection's string id, or filter JSON.</summary>
    public required string ScopeValue { get; init; }

    public int? MaxGames { get; init; }

    public long? MaxBytes { get; init; }

    /// <summary>
    /// Which games a cap keeps when the scope is bigger than it.
    /// </summary>
    /// <remarks>
    /// <b>Recently updated by default, not by name.</b> The ordering only does anything once a
    /// cap bites, and at that moment "by name" means a set of forty keeps everything beginning
    /// with A, which is nobody's intention and reads as a bug the first time somebody sees it.
    /// Newest-in-RomM is what a person means by "give me some of this platform". Changed on a
    /// hands-on finding in stage 7b-2a; the ordering is still explicit on every set the console
    /// creates with <c>--order</c>.
    /// </remarks>
    public SetOrdering Ordering { get; init; } = SyncSetStore.DefaultOrdering;

    public string EvictionPolicy { get; init; } = "keep_favourites";

    public bool Enabled { get; init; } = true;

    /// <summary>The folder this set writes to when the platform alone cannot say. Arcade needs it.</summary>
    public string? FolderOverride { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public DateTimeOffset? LastResolvedAt { get; init; }

    public string? LastResolutionSummary { get; init; }
}

/// <summary>One resolved candidate, carrying enough to be displayed with no server.</summary>
public sealed record SyncSetMember
{
    public required int RomId { get; init; }

    public required MemberState State { get; init; }

    /// <summary>The RetroBat folder, or null when the platform did not resolve.</summary>
    public string? Folder { get; init; }

    public required string PlatformSlug { get; init; }

    public required string FsName { get; init; }

    public string? FsExtension { get; init; }

    public long SizeBytes { get; init; }

    /// <summary>
    /// What the server says this ROM's content hashes to, or null.
    /// </summary>
    /// <remarks>
    /// Carried on the membership so a re-sync can decide a file on disk is already the right
    /// one without asking the server. Both describe the <b>uncompressed</b> content, so for an
    /// archive they are hashes of what is inside it. Null is ordinary: 9% of a real library
    /// carries no md5 and 4% no sha1.
    /// </remarks>
    public string? Md5Hash { get; init; }

    public string? Sha1Hash { get; init; }

    /// <summary>
    /// Whether RomM serves this ROM as several files rather than one.
    /// </summary>
    /// <remarks>
    /// <b>Carried so <c>ContentSync</c> reads it rather than assuming it.</b> It hardcoded
    /// false, which is true of everything that reaches a plan today, because
    /// <c>SetResolver</c> excludes a multi-file ROM before the extension check. That left the
    /// client's own multi-file guard unreachable from the shipped path: a ranged request is
    /// refused for one of these and the nginx 403 is worded, and both were exercised only by
    /// tests, so re-admitting multi-file ROMs would have leaned on a guard nobody had run.
    /// <para>
    /// Written for every member and not only for the excluded ones. A flag that is only ever
    /// set on rows that never reach <c>ContentSync</c> is the same assumption with a column
    /// around it.
    /// </para>
    /// </remarks>
    public bool IsMultiFile { get; init; }

    public required string DisplayName { get; init; }

    public required string SortKey { get; init; }

    /// <summary>The rom's own <c>updated_at</c> in RomM, which is what a 'recent' set sorts on.</summary>
    public DateTimeOffset? RomUpdatedAt { get; init; }

    /// <summary>Rank in the set's ordering, or null when excluded.</summary>
    public int? Position { get; init; }

    /// <summary>The start of the walk that produced this row, not when the row was written.</summary>
    public DateTimeOffset ResolvedAt { get; init; }
}

/// <summary>An exclusion reason with its count and the extensions that caused it.</summary>
public sealed record ExclusionSummary(MemberState State, int Count, IReadOnlyList<string> Extensions);

/// <summary>
/// Sync set definitions and the membership their last resolution produced.
/// </summary>
/// <remarks>
/// The store is the authority on what a set resolved to. Everything here has to be readable
/// with the server switched off, which is why membership carries names, sizes and folders
/// rather than only rom ids.
/// </remarks>
public sealed class SyncSetStore
{
    private const string SelectColumns = """
        id, name, scope_kind, scope_value, max_games, max_bytes, ordering, eviction_policy,
        enabled, folder_override, created_at, updated_at, last_resolved_at, last_resolution_summary
        """;

    private const string MemberColumns = """
        SELECT rom_id, state, folder, platform_slug, fs_name, fs_extension, size_bytes,
               display_name, sort_key, rom_updated_at, position, resolved_at,
               md5_hash, sha1_hash, has_multiple_files
        FROM sync_set_member
        """;

    private readonly SqliteConnection _connection;

    internal SyncSetStore(SqliteConnection connection) => _connection = connection;

    /// <summary>Every set, by name.</summary>
    public IReadOnlyList<SyncSetDefinition> List()
    {
        using var command = _connection.Command($"SELECT {SelectColumns} FROM sync_set ORDER BY name;");
        using var reader = command.ExecuteReader();

        var sets = new List<SyncSetDefinition>();
        while (reader.Read())
        {
            sets.Add(ReadSet(reader));
        }

        return sets;
    }

    /// <summary>One set by name, or null.</summary>
    public SyncSetDefinition? Find(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        using var command = _connection
            .Command($"SELECT {SelectColumns} FROM sync_set WHERE name = $name;")
            .With("$name", name);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadSet(reader) : null;
    }

    /// <summary>Creates a set and returns it with its assigned id.</summary>
    /// <exception cref="SyncSetExistsException">A set with that name is already defined.</exception>
    public SyncSetDefinition Add(SyncSetDefinition definition, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (Find(definition.Name) is not null)
        {
            throw new SyncSetExistsException($"A sync set named '{definition.Name}' already exists.");
        }

        using var command = _connection.Command(
            """
            INSERT INTO sync_set (
              name, scope_kind, scope_value, max_games, max_bytes, ordering, eviction_policy,
              enabled, folder_override, created_at, updated_at
            )
            VALUES (
              $name, $scopeKind, $scopeValue, $maxGames, $maxBytes, $ordering, $eviction,
              $enabled, $folderOverride, $now, $now
            )
            RETURNING id;
            """)
            .With("$name", definition.Name)
            .With("$scopeKind", ScopeText(definition.Scope))
            .With("$scopeValue", definition.ScopeValue)
            .With("$maxGames", SqliteValues.OrNull(definition.MaxGames))
            .With("$maxBytes", SqliteValues.OrNull(definition.MaxBytes))
            .With("$ordering", OrderingText(definition.Ordering))
            .With("$eviction", definition.EvictionPolicy)
            .With("$enabled", definition.Enabled ? 1 : 0)
            .With("$folderOverride", SqliteValues.OrNull(definition.FolderOverride))
            .With("$now", SqliteValues.ToText(now));

        var id = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        return definition with { Id = id, CreatedAt = now, UpdatedAt = now };
    }

    /// <summary>Removes a set and its membership. Returns false when there was nothing to remove.</summary>
    public bool Remove(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        using var command = _connection
            .Command("DELETE FROM sync_set WHERE name = $name;")
            .With("$name", name);

        return command.ExecuteNonQuery() > 0;
    }

    /// <summary>
    /// Changes a set's caps, ordering and folder, leaving its scope and membership alone.
    /// </summary>
    /// <remarks>
    /// <b>Scope is deliberately not updatable here.</b> Pointing a set at something else makes
    /// its recorded membership an answer to a different question, and there is no migration
    /// from one to the other short of a re-resolve. Removing and re-adding is the honest route
    /// and touches nothing on disk. <see cref="UpdateFilter"/> is the one narrowing of that
    /// rule and it carries its own argument.
    /// <para>
    /// The membership is not swept here either. A cap tightened between resolves is an
    /// intention rather than an outcome, and it is the next resolve that applies it.
    /// </para>
    /// </remarks>
    public void UpdatePolicy(SyncSetDefinition definition, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(definition);

        using var command = _connection.Command(
            """
            UPDATE sync_set
               SET max_games = $maxGames,
                   max_bytes = $maxBytes,
                   ordering = $ordering,
                   folder_override = $folderOverride,
                   updated_at = $now
             WHERE id = $id;
            """)
            .With("$maxGames", SqliteValues.OrNull(definition.MaxGames))
            .With("$maxBytes", SqliteValues.OrNull(definition.MaxBytes))
            .With("$ordering", OrderingText(definition.Ordering))
            .With("$folderOverride", SqliteValues.OrNull(definition.FolderOverride))
            .With("$now", SqliteValues.ToText(now))
            .With("$id", definition.Id);

        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Changes a filter set's filter, and marks it as needing a resolve.
    /// </summary>
    /// <remarks>
    /// <b>A narrowing of "scope is not updatable", not an exception to it.</b> That rule was
    /// written when a resolve was a terminal command, and its reason is that membership becomes
    /// an answer to a different question. It still is. What changed is that answering the new
    /// question costs one press from the couch, and that a filter's scope value is a query
    /// rather than an identity: a set called "European platformers" is still that set when its
    /// genre list gains a genre, where a platform set pointed at a different platform is not.
    /// A set whose scope <i>kind</i> or target changes still has to be made again.
    /// <para>
    /// <b>The resolution stamp is cleared and the membership is not.</b> Deleting members would
    /// orphan whatever is already on disk and hand it to the next eviction pass, on nothing
    /// better than an edit. Clearing the stamp makes the set read as needing a resolve, and the
    /// resolve replaces the membership wholesale, which is the same path a new set takes.
    /// </para>
    /// </remarks>
    public void UpdateFilter(long id, string scopeValue, DateTimeOffset now)
    {
        using var command = _connection.Command(
            """
            UPDATE sync_set
               SET scope_value = $scopeValue,
                   last_resolved_at = NULL,
                   last_resolution_summary = NULL,
                   updated_at = $now
             WHERE id = $id AND scope_kind = 'filter';
            """)
            .With("$scopeValue", scopeValue)
            .With("$now", SqliteValues.ToText(now))
            .With("$id", id);

        command.ExecuteNonQuery();
    }

    /// <summary>Sets the folder this set writes to, which is how an arcade set is answered.</summary>
    public void SetFolderOverride(long id, string? folder, DateTimeOffset now)
    {
        using var command = _connection
            .Command("UPDATE sync_set SET folder_override = $folder, updated_at = $now WHERE id = $id;")
            .With("$folder", SqliteValues.OrNull(folder))
            .With("$now", SqliteValues.ToText(now))
            .With("$id", id);

        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Records what one walk of a set's scope resolved to.
    /// </summary>
    /// <remarks>
    /// <paramref name="resolvedAt"/> is the walk's start, not the moment of the call, and
    /// every row this walk writes carries it. A walk that was interrupted and resumed calls
    /// this once per segment with the same value, so the rows the first segment found are not
    /// mistaken for rows an earlier walk left behind.
    /// <para>
    /// The sweep only runs when <paramref name="complete"/> is true, because a segment of a
    /// walk is not a statement about what the set contains. A member the completed walk did
    /// not find becomes <see cref="MemberState.Departed"/> rather than disappearing, so the
    /// next sync can offer it for eviction instead of silently deleting content the user
    /// still has on disk. An exclusion is deleted outright: it is a fact about the last
    /// resolution rather than something on disk, and keeping it would go on reporting a
    /// skipped game that has left the scope.
    /// </para>
    /// </remarks>
    public void ReplaceMembers(
        long syncSetId,
        IReadOnlyList<SyncSetMember> members,
        string summary,
        DateTimeOffset resolvedAt,
        bool complete = true)
    {
        ArgumentNullException.ThrowIfNull(members);

        var stamp = SqliteValues.ToText(resolvedAt);

        using var transaction = _connection.BeginTransaction();

        using (var upsert = _connection.Command(
            """
            INSERT INTO sync_set_member (
              sync_set_id, rom_id, state, folder, platform_slug, fs_name, fs_extension,
              size_bytes, md5_hash, sha1_hash, display_name, sort_key, rom_updated_at,
              position, resolved_at, has_multiple_files
            )
            VALUES (
              $setId, $romId, $state, $folder, $slug, $fsName, $extension,
              $size, $md5, $sha1, $displayName, $sortKey, $romUpdatedAt,
              $position, $resolvedAt, $multiFile
            )
            ON CONFLICT (sync_set_id, rom_id) DO UPDATE SET
              state          = excluded.state,
              folder         = excluded.folder,
              platform_slug  = excluded.platform_slug,
              fs_name        = excluded.fs_name,
              fs_extension   = excluded.fs_extension,
              size_bytes     = excluded.size_bytes,
              md5_hash       = excluded.md5_hash,
              sha1_hash      = excluded.sha1_hash,
              display_name   = excluded.display_name,
              sort_key       = excluded.sort_key,
              rom_updated_at = excluded.rom_updated_at,
              position       = excluded.position,
              resolved_at    = excluded.resolved_at,
              has_multiple_files = excluded.has_multiple_files;
            """))
        {
            upsert.Transaction = transaction;

            foreach (var member in members)
            {
                upsert.Parameters.Clear();
                upsert
                    .With("$setId", syncSetId)
                    .With("$romId", member.RomId)
                    .With("$state", StateText(member.State))
                    .With("$folder", SqliteValues.OrNull(member.Folder))
                    .With("$slug", member.PlatformSlug)
                    .With("$fsName", member.FsName)
                    .With("$extension", SqliteValues.OrNull(member.FsExtension))
                    .With("$size", member.SizeBytes)
                    .With("$md5", SqliteValues.OrNull(member.Md5Hash))
                    .With("$sha1", SqliteValues.OrNull(member.Sha1Hash))
                    .With("$displayName", member.DisplayName)
                    .With("$sortKey", member.SortKey)
                    .With("$romUpdatedAt", SqliteValues.ToTextOrNull(member.RomUpdatedAt))
                    .With("$position", SqliteValues.OrNull(member.Position))
                    .With("$resolvedAt", stamp)
                    .With("$multiFile", member.IsMultiFile ? 1 : 0);

                upsert.ExecuteNonQuery();
            }
        }

        if (complete)
        {
            using var depart = _connection.Command(
                """
                UPDATE sync_set_member SET state = 'departed', position = NULL
                WHERE sync_set_id = $id AND state = 'member' AND resolved_at <> $resolvedAt;

                DELETE FROM sync_set_member
                WHERE sync_set_id = $id AND state LIKE 'excluded_%' AND resolved_at <> $resolvedAt;
                """)
                .With("$id", syncSetId)
                .With("$resolvedAt", stamp);

            depart.Transaction = transaction;
            depart.ExecuteNonQuery();
        }

        using (var summarize = _connection.Command(
            """
            UPDATE sync_set
            SET last_resolved_at = $resolvedAt, last_resolution_summary = $summary, updated_at = $resolvedAt
            WHERE id = $id;
            """)
            .With("$resolvedAt", stamp)
            .With("$summary", summary)
            .With("$id", syncSetId))
        {
            summarize.Transaction = transaction;
            summarize.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    /// <summary>
    /// The members one walk has already selected, for a run that is resuming that walk.
    /// </summary>
    /// <remarks>
    /// Selection state does not survive the process, so a resumed walk has to be given back
    /// what the interrupted segment kept. Without it each segment applies the set's caps to
    /// its own slice, and a 40-game set resolves 40 twice.
    /// </remarks>
    public IReadOnlyList<SyncSetMember> MembersFrom(long syncSetId, DateTimeOffset resolvedAt)
    {
        using var command = _connection
            .Command($"{MemberColumns} WHERE sync_set_id = $id AND state = 'member' AND resolved_at = $resolvedAt;")
            .With("$id", syncSetId)
            .With("$resolvedAt", SqliteValues.ToText(resolvedAt));

        using var reader = command.ExecuteReader();
        return ReadMembers(reader);
    }

    /// <summary>Reads back a set's membership, ordered as the set orders it.</summary>
    public IReadOnlyList<SyncSetMember> Members(long syncSetId, MemberState? state = MemberState.Member)
    {
        var filter = state is null ? string.Empty : " AND state = $state";
        using var command = _connection
            .Command($"""
                {MemberColumns} WHERE sync_set_id = $id{filter}
                ORDER BY position IS NULL, position, sort_key;
                """)
            .With("$id", syncSetId);

        if (state is { } wanted)
        {
            command.With("$state", StateText(wanted));
        }

        using var reader = command.ExecuteReader();
        return ReadMembers(reader);
    }

    /// <summary>What the set holds now: how many games and how many bytes.</summary>
    public (int Games, long Bytes) MemberTotals(long syncSetId)
    {
        using var command = _connection
            .Command(
                """
                SELECT COUNT(*), COALESCE(SUM(size_bytes), 0)
                FROM sync_set_member WHERE sync_set_id = $id AND state = 'member';
                """)
            .With("$id", syncSetId);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ((int)reader.GetInt64(0), reader.GetInt64(1)) : (0, 0);
    }

    /// <summary>
    /// The exclusions, grouped by reason, with the extensions that caused each.
    /// </summary>
    /// <remarks>
    /// Exclusions are shown, never hidden. "12 games skipped, format not supported by this
    /// system" with the offending extensions is something a user can act on in RomM; a
    /// silently shorter set is not.
    /// </remarks>
    public IReadOnlyList<ExclusionSummary> Exclusions(long syncSetId)
    {
        using var command = _connection
            .Command(
                """
                SELECT state, COUNT(*), GROUP_CONCAT(DISTINCT COALESCE(fs_extension, ''))
                FROM sync_set_member
                WHERE sync_set_id = $id AND state LIKE 'excluded_%'
                GROUP BY state
                ORDER BY COUNT(*) DESC;
                """)
            .With("$id", syncSetId);

        using var reader = command.ExecuteReader();

        var summaries = new List<ExclusionSummary>();
        while (reader.Read())
        {
            var extensions = (reader.GetStringOrNull(2) ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Order(StringComparer.Ordinal)
                .ToArray();

            summaries.Add(new ExclusionSummary(ParseState(reader.GetString(0)), (int)reader.GetInt64(1), extensions));
        }

        return summaries;
    }

    public static string ScopeText(CatalogScopeKind scope) => scope switch
    {
        CatalogScopeKind.Collection => "collection",
        CatalogScopeKind.SmartCollection => "smart_collection",
        CatalogScopeKind.VirtualCollection => "virtual_collection",
        CatalogScopeKind.Platform => "platform",
        CatalogScopeKind.Filter => "filter",
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown scope kind."),
    };

    public static CatalogScopeKind ParseScope(string text) => text switch
    {
        "collection" => CatalogScopeKind.Collection,
        "smart_collection" => CatalogScopeKind.SmartCollection,
        "virtual_collection" => CatalogScopeKind.VirtualCollection,
        "platform" => CatalogScopeKind.Platform,
        "filter" => CatalogScopeKind.Filter,
        _ => throw new ArgumentOutOfRangeException(nameof(text), text, "Unknown scope kind in the database."),
    };

    public static string OrderingText(SetOrdering ordering) => ordering switch
    {
        SetOrdering.Name => "name",
        SetOrdering.SizeAscending => "size_asc",
        SetOrdering.SizeDescending => "size_desc",
        SetOrdering.RecentlyUpdated => "recent",
        _ => throw new ArgumentOutOfRangeException(nameof(ordering), ordering, "Unknown ordering."),
    };

    /// <summary>
    /// What a set is ordered by when nothing says otherwise.
    /// </summary>
    /// <remarks>
    /// <b>One place, because it used to be two.</b> The record's initializer said one thing and
    /// <see cref="ParseOrdering"/> answered a different one for an absent value, so changing
    /// "the default" changed nothing on the path that actually creates sets. Every caller reads
    /// it from here now.
    /// </remarks>
    public static SetOrdering DefaultOrdering => SetOrdering.RecentlyUpdated;

    /// <summary>Reads a stored or supplied ordering. An unrecognised value is the default.</summary>
    public static SetOrdering ParseOrdering(string? text) => text switch
    {
        "name" => SetOrdering.Name,
        "size_asc" => SetOrdering.SizeAscending,
        "size_desc" => SetOrdering.SizeDescending,
        "recent" => SetOrdering.RecentlyUpdated,
        _ => DefaultOrdering,
    };

    internal static string StateText(MemberState state) => state switch
    {
        MemberState.Member => "member",
        MemberState.Departed => "departed",
        MemberState.ExcludedExtension => "excluded_extension",
        MemberState.ExcludedUnmapped => "excluded_unmapped",
        MemberState.ExcludedMultiFile => "excluded_multi_file",
        MemberState.ExcludedFilesystemLimit => "excluded_filesystem_limit",
        MemberState.ExcludedOverCount => "excluded_over_count",
        MemberState.ExcludedOverBytes => "excluded_over_bytes",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown member state."),
    };

    internal static MemberState ParseState(string text) => text switch
    {
        "departed" => MemberState.Departed,
        "excluded_extension" => MemberState.ExcludedExtension,
        "excluded_unmapped" => MemberState.ExcludedUnmapped,
        "excluded_multi_file" => MemberState.ExcludedMultiFile,
        "excluded_filesystem_limit" => MemberState.ExcludedFilesystemLimit,
        "excluded_over_count" => MemberState.ExcludedOverCount,
        "excluded_over_bytes" => MemberState.ExcludedOverBytes,
        _ => MemberState.Member,
    };

    private static List<SyncSetMember> ReadMembers(SqliteDataReader reader)
    {
        var members = new List<SyncSetMember>();
        while (reader.Read())
        {
            members.Add(new SyncSetMember
            {
                RomId = (int)reader.GetInt64(0),
                State = ParseState(reader.GetString(1)),
                Folder = reader.GetStringOrNull(2),
                PlatformSlug = reader.GetString(3),
                FsName = reader.GetString(4),
                FsExtension = reader.GetStringOrNull(5),
                SizeBytes = reader.GetInt64(6),
                DisplayName = reader.GetString(7),
                SortKey = reader.GetString(8),
                RomUpdatedAt = reader.GetTimestampOrNull(9),
                Position = (int?)reader.GetInt64OrNull(10),
                ResolvedAt = reader.GetTimestampOrNull(11) ?? default,
                Md5Hash = reader.GetStringOrNull(12),
                Sha1Hash = reader.GetStringOrNull(13),
                IsMultiFile = reader.GetInt64(14) != 0,
            });
        }

        return members;
    }

    private static SyncSetDefinition ReadSet(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        Name = reader.GetString(1),
        Scope = ParseScope(reader.GetString(2)),
        ScopeValue = reader.GetString(3),
        MaxGames = (int?)reader.GetInt64OrNull(4),
        MaxBytes = reader.GetInt64OrNull(5),
        Ordering = ParseOrdering(reader.GetStringOrNull(6)),
        EvictionPolicy = reader.GetStringOrNull(7) ?? "keep_favourites",
        Enabled = reader.GetInt64(8) != 0,
        FolderOverride = reader.GetStringOrNull(9),
        CreatedAt = reader.GetTimestampOrNull(10) ?? default,
        UpdatedAt = reader.GetTimestampOrNull(11) ?? default,
        LastResolvedAt = reader.GetTimestampOrNull(12),
        LastResolutionSummary = reader.GetStringOrNull(13),
    };
}

/// <summary>Thrown when a set name is already taken.</summary>
public sealed class SyncSetExistsException : Exception
{
    public SyncSetExistsException(string message)
        : base(message)
    {
    }

    public SyncSetExistsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public SyncSetExistsException()
        : base("A sync set with that name already exists.")
    {
    }
}
