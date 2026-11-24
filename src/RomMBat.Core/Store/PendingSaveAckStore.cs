using Microsoft.Data.Sqlite;
using RomMBat.Core.Paths;

namespace RomMBat.Core.Store;

internal sealed record PendingSaveAck(
    int RomId, string Slot, int SaveId, string Hash, RelativePath Path, DateTimeOffset? ServerUpdatedAt,
    bool Installed = false);

internal sealed class PendingSaveAckStore(SqliteConnection connection)
{
    public void Record(string origin, string deviceId, PendingSaveAck save)
    {
        using var command = connection.Command(
            """
            INSERT INTO pending_save_ack (server_origin, device_id, rom_id, slot, save_id, content_hash,
                                          relative_path, server_updated_at, installed)
            VALUES ($origin, $deviceId, $romId, $slot, $saveId, $hash, $path, $updatedAt, $installed)
            ON CONFLICT (server_origin, device_id, save_id) DO UPDATE SET
                rom_id = excluded.rom_id, slot = excluded.slot, content_hash = excluded.content_hash,
                relative_path = excluded.relative_path, server_updated_at = excluded.server_updated_at,
                installed = excluded.installed;
            """)
            .With("$origin", origin)
            .With("$deviceId", deviceId)
            .With("$romId", save.RomId)
            .With("$slot", save.Slot)
            .With("$saveId", save.SaveId)
            .With("$hash", save.Hash)
            .With("$path", save.Path.Value)
            .With("$updatedAt", SqliteValues.ToTextOrNull(save.ServerUpdatedAt))
            .With("$installed", save.Installed ? 1 : 0);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<PendingSaveAck> List(string origin, string deviceId, int? romId = null)
    {
        using var command = connection.Command(
            """
            SELECT rom_id, slot, save_id, content_hash, relative_path, server_updated_at, installed FROM pending_save_ack
            WHERE server_origin = $origin AND device_id = $deviceId AND ($romId IS NULL OR rom_id = $romId)
            ORDER BY save_id;
            """)
            .With("$origin", origin)
            .With("$deviceId", deviceId)
            .With("$romId", SqliteValues.OrNull(romId));
        using var reader = command.ExecuteReader();
        var pending = new List<PendingSaveAck>();
        while (reader.Read())
        {
            pending.Add(new PendingSaveAck(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3),
                RelativePath.Create(reader.GetString(4)), reader.GetTimestampOrNull(5), reader.GetInt32(6) != 0));
        }

        return pending;
    }

    public void Remove(string origin, string deviceId, PendingSaveAck save)
    {
        using var command = connection.Command(
            """
            DELETE FROM pending_save_ack
            WHERE server_origin = $origin AND device_id = $deviceId AND save_id = $saveId AND content_hash = $hash;
            """)
            .With("$origin", origin)
            .With("$deviceId", deviceId)
            .With("$saveId", save.SaveId)
            .With("$hash", save.Hash);
        command.ExecuteNonQuery();
    }
}
