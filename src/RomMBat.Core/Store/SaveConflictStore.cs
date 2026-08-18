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
/// <b>Resolved rows are kept rather than deleted.</b> <c>saves</c> reads them back to say what was
/// decided, and <see cref="Record"/> reads them to tell a slot conflicting again over an unmoved
/// server side from one conflicting over a genuinely new one. Deleting them would put every
/// settled slot back in front of the user on the next flush, with another copy taken aside.
/// </para>
/// </remarks>
public sealed class SaveConflictStore
{
    private readonly SqliteConnection _connection;

    internal SaveConflictStore(SqliteConnection connection) => _connection = connection;

    /// <summary>
    /// Records a conflict, or notes that a standing one was seen again.
    /// </summary>
    /// <remarks>
    /// A slot that conflicts again after being resolved is reopened rather than left resolved,
    /// because the decision the user took was about the two sides as they then were. A slot where
    /// neither side has moved since the decision is left settled, so the row the caller reads
    /// back says whether there is anything still waiting on the user.
    /// <para>
    /// <b>The save id is compared as well as the digest, and comparing the digest alone produced
    /// a slot with no way out.</b> A bundled save's <c>content_hash</c> is computed over the
    /// archive's contents, so a slot that returns to contents it held before carries the digest
    /// that was already settled while being a different row entirely. The guard then dropped a
    /// real conflict: nothing was stored, so <c>saves</c> listed nothing and <c>saves resolve</c>
    /// answered "already resolved", while every flush went on counting it and this device's write
    /// was refused with a 409 forever. Driven on hardware, one device deleting a save slot and
    /// another restoring it. A new row in the slot is the server side moving, whatever it holds.
    /// </para>
    /// </remarks>
    public void Record(SaveConflictRecord conflict, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(conflict);

        var existing = Read(conflict.RomId, conflict.Slot);

        // Re-reporting a conflict the user already settled would make the resolution command
        // useless, so a slot where neither side has moved is left exactly as the decision left it.
        if (existing is { IsOpen: false }
            && string.Equals(existing.ServerHash, conflict.ServerHash, StringComparison.OrdinalIgnoreCase)
            && existing.ServerSaveId == conflict.ServerSaveId)
        {
            return;
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
    }

    /// <summary>Notes where the copy taken aside went, once it has been taken.</summary>
    /// <remarks>
    /// Separate from <see cref="Record"/> because the conflict is written before the copy is
    /// attempted: an unwritable <c>replaced/</c> must cost the copy and not the record of the
    /// conflict itself.
    /// </remarks>
    public void RecordCopy(long romId, string slot, RelativePath copy)
    {
        using var command = _connection.Command(
            """
            UPDATE save_conflict
            SET local_copy_path = $copy
            WHERE rom_id = $romId AND slot = $slot;
            """)
            .With("$romId", romId)
            .With("$slot", slot)
            .With("$copy", copy.Value);

        command.ExecuteNonQuery();
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

    /// <summary>Forgets where the copy aside was, once the copy itself has been removed.</summary>
    /// <remarks>
    /// The row stays. Only the pointer goes, so a slot that conflicts again is not offered a copy
    /// that was pruned when the previous conflict was settled.
    /// </remarks>
    public void ForgetCopy(long romId, string slot)
    {
        using var command = _connection.Command(
            """
            UPDATE save_conflict
            SET local_copy_path = NULL
            WHERE rom_id = $romId AND slot = $slot;
            """)
            .With("$romId", romId)
            .With("$slot", slot);

        command.ExecuteNonQuery();
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
