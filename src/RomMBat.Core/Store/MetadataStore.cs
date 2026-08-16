using System.Text.Json;
using Microsoft.Data.Sqlite;
using RomM.Client.Content;
using RomMBat.Core.Metadata;

namespace RomMBat.Core.Store;

/// <summary>
/// The metadata a gamelist is written from, kept per selected ROM.
/// </summary>
/// <remarks>
/// <b>Not a catalog mirror.</b> A row exists only for a ROM some sync set selected, which is
/// the narrow caching core principle 2 permits; nothing here ever holds the library.
/// <para>
/// Its second job is ownership. ES's own scraper writes entries into the same
/// <c>gamelist.xml</c>, and a row here is what says an entry is RomMBat's to rewrite or
/// remove. A ROM with no row is a ROM RomMBat has never resolved, and its entry is left
/// alone.
/// </para>
/// </remarks>
public sealed class MetadataStore
{
    private const string SelectColumns = """
        SELECT rom_id, folder, fs_name, name, description, developer, genre, family, players,
               lang, region, release_date, rating, media_paths, fetched_at
        FROM rom_metadata
        """;

    private static readonly JsonSerializerOptions MediaJson = new(JsonSerializerDefaults.Web);

    private readonly SqliteConnection _connection;

    internal MetadataStore(SqliteConnection connection) => _connection = connection;

    /// <summary>Writes a ROM's metadata, replacing whatever was known about it.</summary>
    public void Record(GameMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        using var command = _connection.Command(
            """
            INSERT INTO rom_metadata (
              rom_id, folder, fs_name, name, description, developer, genre, family, players,
              lang, region, release_date, rating, media_paths, fetched_at
            )
            VALUES (
              $romId, $folder, $fsName, $name, $description, $developer, $genre, $family, $players,
              $lang, $region, $releaseDate, $rating, $mediaPaths, $fetchedAt
            )
            ON CONFLICT (rom_id) DO UPDATE SET
              folder       = excluded.folder,
              fs_name      = excluded.fs_name,
              name         = excluded.name,
              description  = excluded.description,
              developer    = excluded.developer,
              genre        = excluded.genre,
              family       = excluded.family,
              players      = excluded.players,
              lang         = excluded.lang,
              region       = excluded.region,
              release_date = excluded.release_date,
              rating       = excluded.rating,
              media_paths  = excluded.media_paths,
              fetched_at   = excluded.fetched_at;
            """)
            .With("$romId", metadata.RomId)
            .With("$folder", metadata.Folder)
            .With("$fsName", metadata.FsName)
            .With("$name", metadata.Name)
            .With("$description", SqliteValues.OrNull(metadata.Description))
            .With("$developer", SqliteValues.OrNull(metadata.Developer))
            .With("$genre", SqliteValues.OrNull(metadata.Genre))
            .With("$family", SqliteValues.OrNull(metadata.Family))
            .With("$players", SqliteValues.OrNull(metadata.Players))
            .With("$lang", SqliteValues.OrNull(metadata.Languages))
            .With("$region", SqliteValues.OrNull(metadata.Region))
            .With("$releaseDate", SqliteValues.OrNull(metadata.ReleaseDate))
            .With("$rating", SqliteValues.OrNull(metadata.Rating))
            .With("$mediaPaths", SqliteValues.OrNull(SerializeMedia(metadata.MediaPaths)))
            .With("$fetchedAt", SqliteValues.ToText(metadata.FetchedAt ?? DateTimeOffset.UtcNow));

        command.ExecuteNonQuery();
    }

    /// <summary>What is known about one ROM, or null when RomMBat has never resolved it.</summary>
    public GameMetadata? Find(int romId)
    {
        using var command = _connection
            .Command($"{SelectColumns} WHERE rom_id = $romId;")
            .With("$romId", romId);

        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    /// <summary>Metadata for a set of ROMs, keyed by id, for building one folder's gamelist.</summary>
    public IReadOnlyDictionary<int, GameMetadata> ForRoms(IEnumerable<int> romIds)
    {
        ArgumentNullException.ThrowIfNull(romIds);

        var found = new Dictionary<int, GameMetadata>();

        // Chunked rather than one statement per ROM, and rather than one statement holding
        // every id: a folder can hold thousands of games and SQLite's parameter limit is not
        // something to find out about on a user's machine.
        foreach (var chunk in romIds.Distinct().Chunk(500))
        {
            var names = chunk.Select((_, index) => $"$id{index}").ToArray();
            using var command = _connection.Command(
                $"{SelectColumns} WHERE rom_id IN ({string.Join(", ", names)});");

            for (var index = 0; index < chunk.Length; index++)
            {
                command.With(names[index], chunk[index]);
            }

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var metadata = Read(reader);
                found[metadata.RomId] = metadata;
            }
        }

        return found;
    }

    /// <summary>Every ROM RomMBat has metadata for in one folder, which is one gamelist's worth.</summary>
    public IReadOnlyList<GameMetadata> ForFolder(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        using var command = _connection
            .Command($"{SelectColumns} WHERE folder = $folder COLLATE NOCASE ORDER BY rom_id;")
            .With("$folder", folder);

        using var reader = command.ExecuteReader();

        var rows = new List<GameMetadata>();
        while (reader.Read())
        {
            rows.Add(Read(reader));
        }

        return rows;
    }

    /// <summary>Forgets a ROM's metadata. Returns false when there was none.</summary>
    public bool Remove(int romId)
    {
        using var command = _connection
            .Command("DELETE FROM rom_metadata WHERE rom_id = $romId;")
            .With("$romId", romId);

        return command.ExecuteNonQuery() > 0;
    }

    /// <summary>How many ROMs have metadata, for status.</summary>
    public int Count()
    {
        using var command = _connection.Command("SELECT COUNT(*) FROM rom_metadata;");
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Stores media paths with the asset prefix stripped.
    /// </summary>
    /// <remarks>
    /// So the value is relative and carries the same shape guarantee as every other path in
    /// this schema. <see cref="MediaResource.Normalize"/> puts the prefix back at the moment
    /// of use, which is also the step that stops a prefix-less request answering 200 with the
    /// web UI's page.
    /// </remarks>
    private static string? SerializeMedia(IReadOnlyDictionary<MediaKind, string> paths)
    {
        if (paths.Count == 0)
        {
            return null;
        }

        var stripped = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (kind, path) in paths)
        {
            stripped[kind.ToString()] = MediaResource
                .Normalize(path)[MediaResource.AssetPrefix.Length..];
        }

        return JsonSerializer.Serialize(stripped, MediaJson);
    }

    private static Dictionary<MediaKind, string> DeserializeMedia(string? json)
    {
        var paths = new Dictionary<MediaKind, string>();

        if (string.IsNullOrWhiteSpace(json))
        {
            return paths;
        }

        try
        {
            var stored = JsonSerializer.Deserialize<Dictionary<string, string>>(json, MediaJson);
            foreach (var (key, value) in stored ?? [])
            {
                if (Enum.TryParse<MediaKind>(key, ignoreCase: true, out var kind)
                    && !string.IsNullOrWhiteSpace(value))
                {
                    paths[kind] = MediaResource.AssetPrefix + value;
                }
            }
        }
        catch (JsonException)
        {
            // A row written by a build that stored something else. The gamelist loses its
            // artwork references for that game until the next resolve, which beats refusing to
            // write the folder at all.
        }

        return paths;
    }

    private static GameMetadata Read(SqliteDataReader reader) => new()
    {
        RomId = (int)reader.GetInt64(0),
        Folder = reader.GetString(1),
        FsName = reader.GetString(2),
        Name = reader.GetString(3),
        Description = reader.GetStringOrNull(4),
        Developer = reader.GetStringOrNull(5),
        Genre = reader.GetStringOrNull(6),
        Family = reader.GetStringOrNull(7),
        Players = reader.GetStringOrNull(8),
        Languages = reader.GetStringOrNull(9),
        Region = reader.GetStringOrNull(10),
        ReleaseDate = reader.GetStringOrNull(11),
        Rating = reader.GetStringOrNull(12),
        MediaPaths = DeserializeMedia(reader.GetStringOrNull(13)),
        FetchedAt = reader.GetTimestampOrNull(14),
    };
}
