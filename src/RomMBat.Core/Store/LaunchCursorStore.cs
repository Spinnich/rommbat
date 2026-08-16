using Microsoft.Data.Sqlite;
using RomMBat.Core.RetroBat;

namespace RomMBat.Core.Store;

/// <summary>
/// Where the launch log was last read to.
/// </summary>
/// <remarks>
/// Deliberately not a byte offset. <c>emulatorLauncher.log</c> rotates at roughly 1 MiB into
/// <c>.log.old</c>, so an offset taken before a rotation points into the wrong file after one.
/// A timestamp plus a signature of the line survives that, and consuming everything strictly
/// newer is idempotent, which is what a flush interrupted halfway needs.
/// </remarks>
public sealed class LaunchCursorStore
{
    private readonly SqliteConnection _connection;

    internal LaunchCursorStore(SqliteConnection connection) => _connection = connection;

    /// <summary>Where to resume, or a null timestamp when nothing has been read yet.</summary>
    public LaunchLogPosition Read()
    {
        using var command = _connection.Command(
            "SELECT last_event_utc, last_signature, live_size_bytes FROM launch_cursor WHERE id = 1;");

        using var reader = command.ExecuteReader();

        if (!reader.Read())
        {
            return new LaunchLogPosition(null, null, 0);
        }

        return new LaunchLogPosition(
            reader.GetTimestampOrNull(0),
            reader.GetStringOrNull(1),
            reader.GetInt64OrNull(2) ?? 0);
    }

    /// <summary>Records where a read stopped.</summary>
    public void Write(LaunchLogPosition position, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(position);

        using var command = _connection.Command(
            """
            UPDATE launch_cursor
            SET last_event_utc = $at, last_signature = $signature, live_size_bytes = $size,
                read_at_utc = $now
            WHERE id = 1;
            """)
            .With("$at", SqliteValues.ToTextOrNull(position.At))
            .With("$signature", SqliteValues.OrNull(position.Signature))
            .With("$size", position.LiveSizeBytes)
            .With("$now", SqliteValues.ToText(now));

        command.ExecuteNonQuery();
    }
}
