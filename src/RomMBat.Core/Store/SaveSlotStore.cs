using Microsoft.Data.Sqlite;
using RomM.Client.Saves;
using RomMBat.Core.Paths;

namespace RomMBat.Core.Store;

/// <summary>What the server holds for one <c>(rom_id, slot)</c>.</summary>
/// <param name="FileName">
/// The server's identity, tagged with the upload timestamp. Not a name to write on disk.
/// </param>
/// <param name="OnDiskPath">
/// Where a restore of this slot goes, built from the untagged stem and the extension. Null
/// when the server told us neither, which is not a case observed and is not one to paper over
/// by falling back to the tagged name.
/// </param>
public sealed record SaveSlotRecord(
    long RomId,
    string Slot,
    int? SaveId,
    string? FileName,
    string? FileNameNoTags,
    string? FileExtension,
    string? ServerContentHash,
    DateTimeOffset? ServerUpdatedAt,
    string? OriginDeviceId,
    RelativePath? OnDiskPath);

/// <summary>
/// The server-side identity of a slot.
/// </summary>
/// <remarks>
/// Four of these fields cannot be recomputed locally: <c>save_id</c> is the only way to address
/// a save for download or ack, <c>file_name</c> is what the server renamed the upload to,
/// <c>server_content_hash</c> is what decides a conflict, and <c>origin_device_id</c> is how
/// this device recognises its own upload coming back down.
/// </remarks>
public sealed class SaveSlotStore
{
    private readonly SqliteConnection _connection;

    internal SaveSlotStore(SqliteConnection connection) => _connection = connection;

    /// <summary>Records what an upload or a listing said about a slot.</summary>
    public void Record(SaveRow row, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(row);

        using var command = _connection.Command(
            """
            INSERT INTO save_slot (rom_id, slot, save_id, file_name, file_name_no_tags,
                                   file_extension, server_content_hash, server_updated_at,
                                   origin_device_id, last_negotiated_at)
            VALUES ($romId, $slot, $saveId, $fileName, $noTags, $extension, $hash, $updatedAt,
                    $origin, $now)
            ON CONFLICT (rom_id, slot) DO UPDATE SET
              save_id             = excluded.save_id,
              file_name           = excluded.file_name,
              file_name_no_tags   = excluded.file_name_no_tags,
              file_extension      = excluded.file_extension,
              server_content_hash = excluded.server_content_hash,
              server_updated_at   = excluded.server_updated_at,
              origin_device_id    = excluded.origin_device_id,
              last_negotiated_at  = excluded.last_negotiated_at;
            """)
            .With("$romId", row.RomId)
            .With("$slot", row.Slot ?? string.Empty)
            .With("$saveId", row.Id)
            .With("$fileName", SqliteValues.OrNull(row.FileName))
            .With("$noTags", SqliteValues.OrNull(row.FileNameNoTags))
            .With("$extension", SqliteValues.OrNull(row.FileExtension))
            .With("$hash", SqliteValues.OrNull(row.ContentHash))
            .With("$updatedAt", SqliteValues.ToTextOrNull(row.UpdatedAt))
            .With("$origin", SqliteValues.OrNull(row.OriginDeviceId))
            .With("$now", SqliteValues.ToText(now));

        command.ExecuteNonQuery();
    }

    /// <summary>What is known about one slot, or null when nothing is.</summary>
    public SaveSlotRecord? Read(long romId, string slot)
    {
        using var command = _connection.Command(
            """
            SELECT s.rom_id, s.slot, s.save_id, s.file_name, s.file_name_no_tags, s.file_extension,
                   s.server_content_hash, s.server_updated_at, s.origin_device_id,
                   (SELECT l.relative_path FROM local_save l
                    WHERE l.rom_id = s.rom_id AND l.slot = s.slot LIMIT 1)
            FROM save_slot s
            WHERE s.rom_id = $romId AND s.slot = $slot;
            """)
            .With("$romId", romId)
            .With("$slot", slot);

        using var reader = command.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    /// <summary>Every slot known to the server, for <c>status</c>.</summary>
    public IReadOnlyList<SaveSlotRecord> List()
    {
        using var command = _connection.Command(
            """
            SELECT s.rom_id, s.slot, s.save_id, s.file_name, s.file_name_no_tags, s.file_extension,
                   s.server_content_hash, s.server_updated_at, s.origin_device_id,
                   (SELECT l.relative_path FROM local_save l
                    WHERE l.rom_id = s.rom_id AND l.slot = s.slot LIMIT 1)
            FROM save_slot s
            ORDER BY s.rom_id, s.slot;
            """);

        using var reader = command.ExecuteReader();
        var records = new List<SaveSlotRecord>();

        while (reader.Read())
        {
            records.Add(Map(reader));
        }

        return records;
    }

    /// <summary>
    /// True when the current save in a slot is one this device uploaded.
    /// </summary>
    /// <remarks>
    /// <c>origin_device_id</c> names the uploader, which is the cheapest way to recognise your
    /// own save coming back down and decide whether a download is worth acting on.
    /// </remarks>
    public bool IsOwnUpload(long romId, string slot, string deviceId) =>
        Read(romId, slot)?.OriginDeviceId is { } origin
        && string.Equals(origin, deviceId, StringComparison.Ordinal);

    private static SaveSlotRecord Map(SqliteDataReader reader)
    {
        var noTags = reader.GetStringOrNull(4);
        var extension = reader.GetStringOrNull(5);
        var storedPath = reader.GetStringOrNull(9);

        RelativePath? onDisk = null;

        if (storedPath is not null && RelativePath.TryCreate(storedPath, out var parsed))
        {
            onDisk = parsed;
        }
        else if (noTags is not null)
        {
            // No local file for this slot yet, so the name is built from what the server
            // returned: the untagged stem plus the extension, never file_name.
            var name = string.IsNullOrEmpty(extension) ? noTags : $"{noTags}.{extension}";
            if (RelativePath.TryCreate($"saves/{name}", out var built))
            {
                onDisk = built;
            }
        }

        return new SaveSlotRecord(
            reader.GetInt64(0),
            reader.GetString(1),
            (int?)reader.GetInt64OrNull(2),
            reader.GetStringOrNull(3),
            noTags,
            extension,
            reader.GetStringOrNull(6),
            reader.GetTimestampOrNull(7),
            reader.GetStringOrNull(8),
            onDisk);
    }
}
