using Microsoft.Data.Sqlite;
using RomM.Client.Saves;
using RomMBat.Core.Paths;

namespace RomMBat.Core.Store;

/// <summary>What the server holds for one <c>(rom_id, slot)</c>.</summary>
/// <param name="FileName">
/// The server's identity, tagged with the upload timestamp. Not a name to write on disk.
/// </param>
/// <param name="OnDiskPath">
/// Where a restore of this slot goes. The path the local save already occupies when there is
/// one, because that is a path this device has watched an emulator read. Otherwise
/// <c>saves/&lt;folder&gt;/&lt;the ROM's own stem&gt;.&lt;file_extension&gt;</c>, which is the
/// ordinary new-device restore. <b>Not <c>file_name_no_tags</c></b>: the server strips general
/// tags rather than only its own, so a real save came back with <c>Phantasy Star</c> for a ROM
/// called <c>Phantasy Star (Brazil)</c>, and that filename is invisible to libretro. Null only
/// when the ROM is not held either, where there is genuinely no folder to name.
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

    /// <summary>
    /// Records the server identity a restore just took, which no upload response will supply.
    /// </summary>
    /// <remarks>
    /// <b>Only a class C restore needs this, and without it the next flush uploads.</b> The wire
    /// hash for a bundled save that has not changed since it was written is
    /// <c>server_content_hash</c>, because the server's digest over an archive cannot be
    /// recomputed here. A restore that leaves this row holding the pre-download digest therefore
    /// submits a hash the server no longer recognises, and negotiate answers <c>upload</c> for a
    /// unit that is already in step. Found on hardware: the flush after a class C restore
    /// reported one upload, which the server then deduplicated into an existing row.
    /// <para>
    /// The names are left as they were, since a download carries the tagged name only, and
    /// <c>origin_device_id</c> is cleared rather than kept: the device that uploaded this save
    /// is not the one recorded against the save it replaced, and null reads as "not known"
    /// rather than as a claim.
    /// </para>
    /// </remarks>
    public void RecordRestored(
        long romId,
        string slot,
        int? saveId,
        string? serverContentHash,
        DateTimeOffset? serverUpdatedAt,
        DateTimeOffset now)
    {
        using var command = _connection.Command(
            """
            INSERT INTO save_slot (rom_id, slot, save_id, server_content_hash, server_updated_at,
                                   last_negotiated_at)
            VALUES ($romId, $slot, $saveId, $hash, $updatedAt, $now)
            ON CONFLICT (rom_id, slot) DO UPDATE SET
              save_id             = excluded.save_id,
              server_content_hash = excluded.server_content_hash,
              server_updated_at   = excluded.server_updated_at,
              origin_device_id    = NULL,
              last_negotiated_at  = excluded.last_negotiated_at;
            """)
            .With("$romId", romId)
            .With("$slot", slot)
            .With("$saveId", SqliteValues.OrNull(saveId))
            .With("$hash", SqliteValues.OrNull(serverContentHash))
            .With("$updatedAt", SqliteValues.ToTextOrNull(serverUpdatedAt))
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
                    WHERE l.rom_id = s.rom_id AND l.slot = s.slot LIMIT 1),
                   (SELECT f.folder FROM local_file f
                    WHERE f.rom_id = s.rom_id AND f.kind = 'rom' LIMIT 1),
                   (SELECT f.file_name FROM local_file f
                    WHERE f.rom_id = s.rom_id AND f.kind = 'rom' LIMIT 1)
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
                    WHERE l.rom_id = s.rom_id AND l.slot = s.slot LIMIT 1),
                   (SELECT f.folder FROM local_file f
                    WHERE f.rom_id = s.rom_id AND f.kind = 'rom' LIMIT 1),
                   (SELECT f.file_name FROM local_file f
                    WHERE f.rom_id = s.rom_id AND f.kind = 'rom' LIMIT 1)
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

    /// <summary>
    /// Builds the record, including where a downloaded save would go on disk.
    /// </summary>
    /// <remarks>
    /// <b>The name comes from the ROM's own stem, not from <c>file_name_no_tags</c>, and that is
    /// a correction rather than a preference.</b> The server does not undo its own timestamp
    /// tag, it runs a general tag stripper: a real save measured live came back as
    /// <c>Phantasy Star (Brazil) [2026-08-17_17-01-00].srm</c> with <c>file_name_no_tags</c> of
    /// <c>Phantasy Star</c>, because <c>(Brazil)</c> is part of the ROM's name and the server
    /// took it for a tag. Writing that produces a file libretro cannot see, which is exactly the
    /// failure the untagged-name rule exists to prevent.
    /// <para>
    /// The ROM's stem is the only sound source, and it needs no regex: it is the same
    /// <c>(folder, stem)</c> key class A attribution already uses, run backwards. The extension
    /// still comes from the server, since nothing local knows what the save was called.
    /// </para>
    /// <para>
    /// <b>This path is reachable, which stage 2a said it was not.</b> Measurement 132 read
    /// negotiate's empty answer as "never volunteers a slot the client did not submit"; re-driven,
    /// negotiate returns a download for every save the device has no sync record for, so a
    /// restore onto a device that never held the slot is an ordinary case rather than a dead one.
    /// </para>
    /// </remarks>
    private static SaveSlotRecord Map(SqliteDataReader reader)
    {
        var noTags = reader.GetStringOrNull(4);
        var extension = reader.GetStringOrNull(5);
        var storedPath = reader.GetStringOrNull(9);
        var romFolder = reader.GetStringOrNull(10);
        var romFileName = reader.GetStringOrNull(11);

        RelativePath? onDisk = null;

        if (storedPath is not null && RelativePath.TryCreate(storedPath, out var parsed))
        {
            // A path this device already holds, which is a path an emulator has proven it reads.
            onDisk = parsed;
        }
        else if (romFolder is not null && romFileName is not null && extension is not null)
        {
            var stem = Path.GetFileNameWithoutExtension(romFileName);
            var trimmed = extension.TrimStart('.');

            if (RelativePath.TryCreate($"saves/{romFolder}/{stem}.{trimmed}", out var derived))
            {
                onDisk = derived;
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
