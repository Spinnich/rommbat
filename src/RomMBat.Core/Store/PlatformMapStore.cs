using Microsoft.Data.Sqlite;
using RomMBat.Core.Mapping;

namespace RomMBat.Core.Store;

/// <summary>One row of the Platform Mapping screen, readable with the server off.</summary>
public sealed record PlatformMapRow
{
    /// <summary>
    /// RomM's <c>fs_slug</c>, which is the platform's identity here.
    /// </summary>
    /// <remarks>
    /// Not the slug. RomM keeps <c>fs_slug</c> unique and the slug not: a real 123-platform
    /// library carried only 72 distinct slugs, because each system has an "-unofficial" twin
    /// sharing one.
    /// </remarks>
    public required string FsSlug { get; init; }

    /// <summary>RomM's platform slug, which is what the bundled table is looked up by.</summary>
    public required string Slug { get; init; }

    public int? PlatformId { get; init; }

    public string? DisplayName { get; init; }

    /// <summary>The folder that syncs. Null when nothing is applied.</summary>
    public string? Folder { get; init; }

    /// <summary>A normalized match awaiting confirmation. Never syncs on its own.</summary>
    public string? SuggestedFolder { get; init; }

    public required MappingSource ResolvedBy { get; init; }

    public IReadOnlyList<string> CandidateFolders { get; init; } = [];

    public bool RequiresChoice { get; init; }

    public string? Explanation { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>True when a person chose this, rather than a layer guessing it.</summary>
    public bool IsUserChoice => ResolvedBy == MappingSource.User;

    /// <summary>What to show in a list: the name if RomM has one, otherwise the identifier.</summary>
    public string Label => string.IsNullOrWhiteSpace(DisplayName) ? FsSlug : DisplayName;
}

/// <summary>
/// The platform map: the user's overrides, plus the last resolution for every platform seen.
/// </summary>
/// <remarks>
/// <c>resolved_by</c> is the point of this table. It separates a choice from a guess, which
/// is what lets the mapping screen show where a folder came from and lets a re-resolution
/// overwrite a guess without touching a choice.
/// <para>
/// Only overrides are durable input. Everything else is a cache of what the chain decided,
/// rewritten on every resolve, so that the screen and <c>status</c> work offline.
/// </para>
/// </remarks>
public sealed class PlatformMapStore
{
    private const string SelectColumns = """
        romm_fs_slug, romm_platform_slug, romm_platform_id, display_name, folder,
        suggested_folder, resolved_by, candidate_folders, requires_choice, explanation, updated_at
        """;

    private readonly SqliteConnection _connection;

    internal PlatformMapStore(SqliteConnection connection) => _connection = connection;

    /// <summary>Every platform seen, by identifier.</summary>
    public IReadOnlyList<PlatformMapRow> List()
    {
        using var command = _connection.Command(
            $"SELECT {SelectColumns} FROM platform_map ORDER BY romm_fs_slug;");

        using var reader = command.ExecuteReader();

        var rows = new List<PlatformMapRow>();
        while (reader.Read())
        {
            rows.Add(ReadRow(reader));
        }

        return rows;
    }

    /// <summary>One platform by its <c>fs_slug</c>, or null.</summary>
    public PlatformMapRow? Find(string fsSlug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fsSlug);

        using var command = _connection
            .Command($"SELECT {SelectColumns} FROM platform_map WHERE romm_fs_slug = $fsSlug;")
            .With("$fsSlug", fsSlug);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadRow(reader) : null;
    }

    /// <summary>
    /// The user's overrides, <c>fs_slug</c> to folder, which is layer 1 of the chain.
    /// </summary>
    /// <remarks>
    /// Read before every resolve and fed back into <see cref="PlatformResolver"/>, so an
    /// override survives a re-resolution that would otherwise overwrite it with a guess.
    /// </remarks>
    public IReadOnlyDictionary<string, string> Overrides()
    {
        using var command = _connection.Command(
            """
            SELECT romm_fs_slug, folder FROM platform_map
            WHERE resolved_by = 'user' AND folder IS NOT NULL;
            """);

        using var reader = command.ExecuteReader();

        var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            overrides[reader.GetString(0)] = reader.GetString(1);
        }

        return overrides;
    }

    /// <summary>
    /// Records a user's choice of folder for a platform. Always wins from now on.
    /// </summary>
    /// <remarks>
    /// This is also how a normalized suggestion is accepted: the suggested folder is passed
    /// in and the row stops being a suggestion and becomes a choice.
    /// </remarks>
    public void SetOverride(string fsSlug, string folder, DateTimeOffset now, string? slug = null, int? platformId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fsSlug);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        using var command = _connection.Command(
            """
            INSERT INTO platform_map (romm_fs_slug, romm_platform_slug, romm_platform_id, folder,
                                      suggested_folder, resolved_by, explanation, updated_at)
            VALUES ($fsSlug, $slug, $platformId, $folder, NULL, 'user', $explanation, $now)
            ON CONFLICT (romm_fs_slug) DO UPDATE SET
              romm_platform_id = COALESCE(excluded.romm_platform_id, platform_map.romm_platform_id),
              folder           = excluded.folder,
              suggested_folder = NULL,
              resolved_by      = 'user',
              explanation      = excluded.explanation,
              updated_at       = excluded.updated_at;
            """)
            .With("$fsSlug", fsSlug)
            .With("$slug", slug ?? fsSlug)
            .With("$platformId", SqliteValues.OrNull(platformId))
            .With("$folder", folder)
            .With("$explanation", $"Set by you to '{folder}'.")
            .With("$now", SqliteValues.ToText(now));

        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Drops a user's override so the chain resolves the platform again.
    /// </summary>
    /// <remarks>
    /// Leaves the row as unmapped rather than deleting it, so the next resolve rewrites it
    /// with whatever the lower layers say and the screen has something to show meanwhile.
    /// </remarks>
    public bool ClearOverride(string fsSlug, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fsSlug);

        using var command = _connection
            .Command(
                """
                UPDATE platform_map
                SET folder = NULL, resolved_by = 'unmapped', explanation = NULL, updated_at = $now
                WHERE romm_fs_slug = $fsSlug AND resolved_by = 'user';
                """)
            .With("$now", SqliteValues.ToText(now))
            .With("$fsSlug", fsSlug);

        return command.ExecuteNonQuery() > 0;
    }

    /// <summary>
    /// Writes what the chain decided, leaving user choices alone.
    /// </summary>
    /// <remarks>
    /// A resolution whose source is not <see cref="MappingSource.User"/> never overwrites a
    /// row that is, so re-resolving after a RetroBat upgrade cannot quietly undo a decision
    /// somebody made.
    /// </remarks>
    public void Record(PlatformResolution resolution, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(resolution);

        using var command = _connection.Command(
            """
            INSERT INTO platform_map (romm_fs_slug, romm_platform_slug, romm_platform_id, display_name,
                                      folder, suggested_folder, resolved_by, candidate_folders,
                                      requires_choice, explanation, updated_at)
            VALUES ($fsSlug, $slug, $platformId, $displayName, $folder, $suggested, $resolvedBy,
                    $candidates, $requiresChoice, $explanation, $now)
            ON CONFLICT (romm_fs_slug) DO UPDATE SET
              romm_platform_slug = excluded.romm_platform_slug,
              romm_platform_id   = COALESCE(excluded.romm_platform_id, platform_map.romm_platform_id),
              display_name       = COALESCE(excluded.display_name, platform_map.display_name),
              folder             = CASE WHEN platform_map.resolved_by = 'user'
                                        THEN platform_map.folder ELSE excluded.folder END,
              suggested_folder   = CASE WHEN platform_map.resolved_by = 'user'
                                        THEN NULL ELSE excluded.suggested_folder END,
              resolved_by        = CASE WHEN platform_map.resolved_by = 'user'
                                        THEN 'user' ELSE excluded.resolved_by END,
              candidate_folders  = excluded.candidate_folders,
              requires_choice    = excluded.requires_choice,
              explanation        = CASE WHEN platform_map.resolved_by = 'user'
                                        THEN platform_map.explanation ELSE excluded.explanation END,
              updated_at         = excluded.updated_at;
            """)
            .With("$fsSlug", resolution.FsSlug)
            .With("$slug", resolution.Slug)
            .With("$platformId", SqliteValues.OrNull(resolution.PlatformId))
            .With("$displayName", SqliteValues.OrNull(resolution.DisplayName))
            .With("$folder", SqliteValues.OrNull(resolution.Folder))
            .With("$suggested", SqliteValues.OrNull(resolution.Suggestion))
            .With("$resolvedBy", SourceText(resolution.ResolvedBy))
            .With("$candidates", SqliteValues.OrNull(string.Join(' ', resolution.Candidates)))
            .With("$requiresChoice", resolution.RequiresExplicitChoice ? 1 : 0)
            .With("$explanation", SqliteValues.OrNull(resolution.Explanation))
            .With("$now", SqliteValues.ToText(now));

        command.ExecuteNonQuery();
    }

    internal static string SourceText(MappingSource source) => source switch
    {
        MappingSource.User => "user",
        MappingSource.FsSlug => "fs_slug",
        MappingSource.Bundled => "bundled",
        MappingSource.Normalized => "normalized",
        MappingSource.Unmapped => "unmapped",
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown mapping source."),
    };

    internal static MappingSource ParseSource(string text) => text switch
    {
        "user" => MappingSource.User,
        "fs_slug" => MappingSource.FsSlug,
        "bundled" => MappingSource.Bundled,
        "normalized" => MappingSource.Normalized,
        _ => MappingSource.Unmapped,
    };

    private static PlatformMapRow ReadRow(SqliteDataReader reader) => new()
    {
        FsSlug = reader.GetString(0),
        Slug = reader.GetString(1),
        PlatformId = (int?)reader.GetInt64OrNull(2),
        DisplayName = reader.GetStringOrNull(3),
        Folder = reader.GetStringOrNull(4),
        SuggestedFolder = reader.GetStringOrNull(5),
        ResolvedBy = ParseSource(reader.GetString(6)),
        CandidateFolders = (reader.GetStringOrNull(7) ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries),
        RequiresChoice = reader.GetInt64(8) != 0,
        Explanation = reader.GetStringOrNull(9),
        UpdatedAt = reader.GetTimestampOrNull(10) ?? default,
    };
}
