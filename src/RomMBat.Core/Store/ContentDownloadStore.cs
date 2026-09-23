using Microsoft.Data.Sqlite;
using RomMBat.Core.Paths;

namespace RomMBat.Core.Store;

/// <summary>A download that has started and not finished.</summary>
/// <remarks>
/// There is no byte count here on purpose. After a power cut the length of the <c>.part</c> on
/// disk is the only honest answer, and a stored count that disagrees with it is exactly the
/// offset a resume would splice at.
/// </remarks>
public sealed record ContentDownload
{
    public required int RomId { get; init; }

    /// <summary>RomM's id for the member being fetched, or 0 for a single-file rom.</summary>
    public int FileId { get; init; }

    /// <summary>The partial file, under <c>emulators/rommbat/partial/</c>.</summary>
    public required RelativePath PartPath { get; init; }

    /// <summary>Where it lands once it verifies.</summary>
    public required RelativePath TargetPath { get; init; }

    /// <summary>What the first response said the whole body would be.</summary>
    public long? ExpectedSize { get; init; }

    /// <summary>
    /// The <c>ETag</c> the first response carried, sent back as <c>If-Range</c>.
    /// </summary>
    /// <remarks>
    /// nginx's form is <c>hex(mtime)-hex(size)</c>, so it moves when the file does. A stale
    /// one answers 200 with the whole body rather than a corrupt splice, which is what makes
    /// resuming safe even across a server restart.
    /// </remarks>
    public string? Validator { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public int Attempts { get; init; }

    public string? LastError { get; init; }
}

/// <summary>What is in flight, so an interrupted download can be continued rather than restarted.</summary>
public sealed class ContentDownloadStore
{
    private const string SelectColumns = """
        SELECT rom_id, part_path, target_path, expected_size, validator, started_at,
               updated_at, attempts, last_error, file_id
        FROM content_download
        """;

    private readonly SqliteConnection _connection;

    internal ContentDownloadStore(SqliteConnection connection) => _connection = connection;

    /// <summary>
    /// Opens or refreshes the record for a download.
    /// </summary>
    /// <remarks>
    /// Keyed by ROM and member, so starting a second download of the same file replaces the first
    /// rather than racing it, while each disc of a set keeps its own. The attempt count survives, because it is what tells a caller a
    /// download is failing repeatedly rather than being interrupted once.
    /// </remarks>
    public ContentDownload Begin(ContentDownload download)
    {
        ArgumentNullException.ThrowIfNull(download);

        using var command = _connection.Command(
            """
            INSERT INTO content_download (
              rom_id, file_id, part_path, target_path, expected_size, validator, started_at,
              updated_at, attempts, last_error
            )
            VALUES ($romId, $fileId, $part, $target, $size, $validator, $now, $now, 0, NULL)
            ON CONFLICT (rom_id, file_id) DO UPDATE SET
              part_path     = excluded.part_path,
              target_path   = excluded.target_path,
              expected_size = excluded.expected_size,
              validator     = excluded.validator,
              updated_at    = excluded.updated_at,
              attempts      = content_download.attempts + 1;
            """)
            .With("$romId", download.RomId)
            .With("$fileId", download.FileId)
            .With("$part", download.PartPath.Value)
            .With("$target", download.TargetPath.Value)
            .With("$size", SqliteValues.OrNull(download.ExpectedSize))
            .With("$validator", SqliteValues.OrNull(download.Validator))
            .With("$now", SqliteValues.ToText(download.UpdatedAt));

        command.ExecuteNonQuery();
        return Find(download.RomId, download.FileId) ?? download;
    }

    /// <summary>The in-flight download for a ROM, or for one member of it, or null.</summary>
    public ContentDownload? Find(int romId, int fileId = 0)
    {
        using var command = _connection
            .Command($"{SelectColumns} WHERE rom_id = $romId AND file_id = $fileId;")
            .With("$romId", romId)
            .With("$fileId", fileId);

        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    /// <summary>Everything in flight, oldest first.</summary>
    public IReadOnlyList<ContentDownload> List()
    {
        using var command = _connection.Command($"{SelectColumns} ORDER BY started_at;");
        using var reader = command.ExecuteReader();

        var downloads = new List<ContentDownload>();
        while (reader.Read())
        {
            downloads.Add(Read(reader));
        }

        return downloads;
    }

    /// <summary>
    /// Stores the validator the response carried, without disturbing the attempt count.
    /// </summary>
    /// <remarks>
    /// Written when the headers arrive rather than when the body ends. A transfer that completes
    /// has its row deleted by the commit that follows, so recording it at the end would only ever
    /// write it to a row about to go; the row that needs it is the one an interrupted transfer
    /// leaves behind, and that transfer never reaches its own end.
    /// </remarks>
    public void RecordValidator(int romId, string? validator, DateTimeOffset now, int fileId = 0)
    {
        if (string.IsNullOrWhiteSpace(validator))
        {
            return;
        }

        using var command = _connection
            .Command(
                """
                UPDATE content_download
                SET validator = $validator, updated_at = $now
                WHERE rom_id = $romId AND file_id = $fileId;
                """)
            .With("$validator", validator)
            .With("$now", SqliteValues.ToText(now))
            .With("$romId", romId)
            .With("$fileId", fileId);

        command.ExecuteNonQuery();
    }

    /// <summary>Records that an attempt failed, keeping the partial file for the next one.</summary>
    public void Fail(int romId, string error, DateTimeOffset now, int fileId = 0)
    {
        using var command = _connection
            .Command(
                """
                UPDATE content_download
                SET last_error = $error, updated_at = $now, attempts = attempts + 1
                WHERE rom_id = $romId AND file_id = $fileId;
                """)
            .With("$error", SqliteValues.OrNull(error))
            .With("$now", SqliteValues.ToText(now))
            .With("$romId", romId)
            .With("$fileId", fileId);

        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Forgets every download of a ROM, each member of a set included.
    /// </summary>
    /// <remarks>
    /// Called on success, once the verified file has been renamed into place, and on abandon,
    /// once the partial file has been deleted. The row and the file are removed inside one
    /// transaction so neither can outlive the other.
    /// </remarks>
    public bool Remove(int romId)
    {
        using var command = _connection
            .Command("DELETE FROM content_download WHERE rom_id = $romId;")
            .With("$romId", romId);

        return command.ExecuteNonQuery() > 0;
    }

    /// <summary>Forgets one member's download, leaving the rest of the set's in flight.</summary>
    public bool Remove(int romId, int fileId)
    {
        using var command = _connection
            .Command("DELETE FROM content_download WHERE rom_id = $romId AND file_id = $fileId;")
            .With("$romId", romId)
            .With("$fileId", fileId);

        return command.ExecuteNonQuery() > 0;
    }

    private static ContentDownload Read(SqliteDataReader reader) => new()
    {
        RomId = (int)reader.GetInt64(0),
        PartPath = RelativePath.Create(reader.GetString(1)),
        TargetPath = RelativePath.Create(reader.GetString(2)),
        ExpectedSize = reader.GetInt64OrNull(3),
        Validator = reader.GetStringOrNull(4),
        StartedAt = reader.GetTimestampOrNull(5) ?? default,
        UpdatedAt = reader.GetTimestampOrNull(6) ?? default,
        Attempts = (int)reader.GetInt64(7),
        LastError = reader.GetStringOrNull(8),
        FileId = (int)reader.GetInt64(9),
    };
}
