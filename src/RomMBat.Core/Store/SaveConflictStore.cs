using Microsoft.Data.Sqlite;
using RomMBat.Core.Paths;

namespace RomMBat.Core.Store;

/// <summary>Which side of a conflict the user picked.</summary>
public enum ConflictResolution
{
    /// <summary>Keep what is on this device, and push it over the server's copy.</summary>
    KeepLocal,

    /// <summary>Take the server's copy, and put the local one aside.</summary>
    KeepServer,
}

/// <summary>A slot where both sides moved, waiting on a decision.</summary>
/// <param name="LocalCopyPath">
/// Where the local file was copied before anything else happened. Null when the copy could not
/// be taken, which is tolerable here and only here: a conflict overwrites nothing, so the copy
/// is a courtesy rather than the precondition it is on a restore.
/// </param>
/// <param name="FirstSeenAtUtc">
/// When the conflict was first observed, not when it was last seen. Re-observing a conflict does
/// not reset how long the user has been living with it.
/// </param>
public sealed record SaveConflictRecord(
    long RomId,
    string Slot,
    RelativePath LocalPath,
    RelativePath? LocalCopyPath,
    string? LocalHash,
    string? ServerHash,
    DateTimeOffset? ServerUpdatedAt,
    int? ServerSaveId,
    string Reason,
    DateTimeOffset FirstSeenAtUtc,
    DateTimeOffset LastSeenAtUtc,
    DateTimeOffset? ResolvedAtUtc,
    ConflictResolution? Resolution)
{
    /// <summary>True while the user has not picked a side.</summary>
    public bool IsOpen => ResolvedAtUtc is null;
}

/// <summary>
/// Conflicts that outlive the flush that found them.
/// </summary>
/// <remarks>
/// <b>Without this table a conflict has nowhere to live.</b> Stage 1 detected one, copied the
/// local file aside and returned it on an in-memory list that <c>flush</c> printed once, so the
/// same slot conflicted again on every flush, another dated copy landed under
/// <c>emulators/rommbat/replaced/</c> each time, and once the console output scrolled away the
/// only evidence was a file. That is issue #31.
/// <para>
/// Resolved rows are kept rather than deleted, so <c>saves</c> can say what was decided and the
/// copy taken aside has something pointing at it until it is pruned.
/// </para>
/// </remarks>
public sealed class SaveConflictStore
{
    private readonly SqliteConnection _connection;

    internal SaveConflictStore(SqliteConnection connection) => _connection = connection;

    /// <summary>
    /// Records a conflict, or notes that a standing one was seen again.
    /// </summary>
    /// <returns>True when this is the first time this slot has conflicted.</returns>
    /// <remarks>
    /// The return value is what stops <c>replaced/</c> growing: the caller takes a copy aside
    /// only when the conflict is new, so a slot that conflicts on every flush accumulates one
    /// copy rather than one per run.
    /// <para>
    /// A slot that conflicts again after being resolved is reopened rather than left resolved,
    /// because the decision the user took was about the two sides as they then were.
    /// </para>
    /// </remarks>
    public bool Record(SaveConflictRecord conflict, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(conflict);

        var existing = Read(conflict.RomId, conflict.Slot);

        // Reopened when the server side has moved since the decision, and left alone when it
        // has not: re-reporting a conflict the user already settled would make the resolution
        // command useless.
        if (existing is { IsOpen: false }
            && string.Equals(existing.ServerHash, conflict.ServerHash, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        using var command = _connection.Command(
            """
            INSERT INTO save_conflict (rom_id, slot, local_path, local_copy_path, local_hash,
                                       server_hash, server_updated_at, server_save_id, reason,
                                       first_seen_at_utc, last_seen_at_utc, resolved_at_utc,
                                       resolution)
            VALUES ($romId, $slot, $localPath, $copyPath, $localHash, $serverHash, $serverUpdated,
                    $saveId, $reason, $now, $now, NULL, NULL)
            ON CONFLICT (rom_id, slot) DO UPDATE SET
              local_path        = excluded.local_path,
              local_copy_path   = COALESCE(excluded.local_copy_path, save_conflict.local_copy_path),
              local_hash        = excluded.local_hash,
              server_hash       = excluded.server_hash,
              server_updated_at = excluded.server_updated_at,
              server_save_id    = excluded.server_save_id,
              reason            = excluded.reason,
              last_seen_at_utc  = excluded.last_seen_at_utc,
              resolved_at_utc   = NULL,
              resolution        = NULL;
            """)
            .With("$romId", conflict.RomId)
            .With("$slot", conflict.Slot)
            .With("$localPath", conflict.LocalPath.Value)
            .With("$copyPath", conflict.LocalCopyPath.HasValue ? conflict.LocalCopyPath.Value.Value : DBNull.Value)
            .With("$localHash", SqliteValues.OrNull(conflict.LocalHash))
            .With("$serverHash", SqliteValues.OrNull(conflict.ServerHash))
            .With("$serverUpdated", SqliteValues.ToTextOrNull(conflict.ServerUpdatedAt))
            .With("$saveId", SqliteValues.OrNull(conflict.ServerSaveId))
            .With("$reason", conflict.Reason)
            .With("$now", SqliteValues.ToText(now));

        command.ExecuteNonQuery();

        return existing is null;
    }

    /// <summary>One conflict, resolved or not.</summary>
    public SaveConflictRecord? Read(long romId, string slot)
    {
        using var command = _connection
            .Command(Select + " WHERE rom_id = $romId AND slot = $slot;")
            .With("$romId", romId)
            .With("$slot", slot);

        using var reader = command.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    /// <summary>Conflicts still waiting on the user, oldest first.</summary>
    public IReadOnlyList<SaveConflictRecord> ListOpen()
    {
        using var command = _connection.Command(
            Select + " WHERE resolved_at_utc IS NULL ORDER BY first_seen_at_utc, rom_id, slot;");

        return Read(command);
    }

    /// <summary>Every conflict, including the ones already decided.</summary>
    public IReadOnlyList<SaveConflictRecord> List()
    {
        using var command = _connection.Command(Select + " ORDER BY rom_id, slot;");
        return Read(command);
    }

    /// <summary>Marks a slot decided.</summary>
    /// <returns>False when there was no open conflict on that slot.</returns>
    public bool Resolve(long romId, string slot, ConflictResolution resolution, DateTimeOffset now)
    {
        using var command = _connection.Command(
            """
            UPDATE save_conflict
            SET resolved_at_utc = $now, resolution = $resolution
            WHERE rom_id = $romId AND slot = $slot AND resolved_at_utc IS NULL;
            """)
            .With("$romId", romId)
            .With("$slot", slot)
            .With("$resolution", ToText(resolution))
            .With("$now", SqliteValues.ToText(now));

        return command.ExecuteNonQuery() > 0;
    }

    /// <summary>Drops a resolved conflict once its copy aside has been pruned.</summary>
    public int Forget(long romId, string slot)
    {
        using var command = _connection
            .Command("DELETE FROM save_conflict WHERE rom_id = $romId AND slot = $slot;")
            .With("$romId", romId)
            .With("$slot", slot);

        return command.ExecuteNonQuery();
    }

    private const string Select =
        """
        SELECT rom_id, slot, local_path, local_copy_path, local_hash, server_hash,
               server_updated_at, server_save_id, reason, first_seen_at_utc, last_seen_at_utc,
               resolved_at_utc, resolution
        FROM save_conflict
        """;

    private static List<SaveConflictRecord> Read(SqliteCommand command)
    {
        using var reader = command.ExecuteReader();
        var conflicts = new List<SaveConflictRecord>();

        while (reader.Read())
        {
            conflicts.Add(Map(reader));
        }

        return conflicts;
    }

    private static SaveConflictRecord Map(SqliteDataReader reader)
    {
        var copyText = reader.GetStringOrNull(3);

        return new SaveConflictRecord(
            reader.GetInt64(0),
            reader.GetString(1),
            RelativePath.Create(reader.GetString(2)),
            copyText is not null && RelativePath.TryCreate(copyText, out var copy) ? copy : null,
            reader.GetStringOrNull(4),
            reader.GetStringOrNull(5),
            reader.GetTimestampOrNull(6),
            (int?)reader.GetInt64OrNull(7),
            reader.GetString(8),
            reader.GetTimestampOrNull(9) ?? DateTimeOffset.UnixEpoch,
            reader.GetTimestampOrNull(10) ?? DateTimeOffset.UnixEpoch,
            reader.GetTimestampOrNull(11),
            ParseResolution(reader.GetStringOrNull(12)));
    }

    private static string ToText(ConflictResolution resolution) =>
        resolution == ConflictResolution.KeepLocal ? "keep_local" : "keep_server";

    private static ConflictResolution? ParseResolution(string? value) => value switch
    {
        "keep_local" => ConflictResolution.KeepLocal,
        "keep_server" => ConflictResolution.KeepServer,
        _ => null,
    };
}
