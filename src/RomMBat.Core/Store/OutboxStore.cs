using Microsoft.Data.Sqlite;
using RomMBat.Core.Paths;

namespace RomMBat.Core.Store;

/// <summary>What kind of work is waiting to go up.</summary>
public enum OutboxKind
{
    Save,
    State,
    PlaySession,
}

/// <summary>Where an entry is in its life.</summary>
public enum OutboxState
{
    Pending,
    Sent,
    Failed,
}

/// <summary>
/// One piece of work produced offline.
/// </summary>
/// <param name="LocalSequence">
/// Monotonic and independent of the wall clock, so ordering survives a flat RTC.
/// </param>
/// <param name="FileMtimeUtc">
/// The file's <b>real</b> mtime, never the sync time. Coarse on FAT and exFAT (2 seconds,
/// rounded up), which is why <paramref name="ContentHash"/> is the primary comparison.
/// </param>
/// <param name="Emulator">
/// Carried rather than re-derived from the slot at flush time, because the server writes it
/// into the stored save's <c>file_path</c> as a directory segment.
/// </param>
/// <param name="BatchKey">
/// Ties the rows of one logical save together. Class B takes one slot per file, so saturn's
/// <c>.bcr</c> and <c>.bkr</c> are two entries describing one save and a flush that lands one
/// and fails the other has to report that rather than two independent successes.
/// </param>
public sealed record OutboxEntry(
    long Id,
    long LocalSequence,
    OutboxKind Kind,
    long? RomId,
    string? Slot,
    string? Emulator,
    string? BatchKey,
    RelativePath? RelativePath,
    string? ContentHash,
    long? SizeBytes,
    DateTimeOffset? FileMtimeUtc,
    DateTimeOffset RecordedAtUtc,
    string? Payload,
    OutboxState State,
    int Attempts,
    string? LastError);

/// <summary>
/// The queue of work produced while offline.
/// </summary>
/// <remarks>
/// Replay is safe by design and no ack protocol is invented here: play sessions dedup
/// server-side on truncated-to-the-second timestamps, and save uploads dedup on
/// <c>content_hash</c> within a slot.
/// </remarks>
public sealed class OutboxStore
{
    private readonly SqliteConnection _connection;
    private readonly LocalStore _store;

    internal OutboxStore(SqliteConnection connection, LocalStore store)
    {
        _connection = connection;
        _store = store;
    }

    /// <summary>Appends an entry, stamping it with the next sequence number.</summary>
    /// <returns>The sequence number it was given.</returns>
    public long Enqueue(
        OutboxKind kind,
        DateTimeOffset recordedAt,
        long? romId = null,
        string? slot = null,
        string? emulator = null,
        string? batchKey = null,
        RelativePath? relativePath = null,
        string? contentHash = null,
        long? sizeBytes = null,
        DateTimeOffset? fileMtimeUtc = null,
        string? payload = null)
    {
        var sequence = _store.NextSequence();

        using var command = _connection.Command(
            """
            INSERT INTO outbox (local_sequence, kind, rom_id, slot, emulator, batch_key,
                                relative_path, content_hash, size_bytes, file_mtime_utc,
                                recorded_at_utc, payload)
            VALUES ($sequence, $kind, $romId, $slot, $emulator, $batchKey, $path, $hash, $size,
                    $mtime, $recordedAt, $payload);
            """)
            .With("$sequence", sequence)
            .With("$kind", ToText(kind))
            .With("$romId", SqliteValues.OrNull(romId))
            .With("$slot", SqliteValues.OrNull(slot))
            .With("$emulator", SqliteValues.OrNull(emulator))
            .With("$batchKey", SqliteValues.OrNull(batchKey))
            .With("$path", relativePath.HasValue ? relativePath.Value.Value : DBNull.Value)
            .With("$hash", SqliteValues.OrNull(contentHash))
            .With("$size", SqliteValues.OrNull(sizeBytes))
            .With("$mtime", SqliteValues.ToTextOrNull(fileMtimeUtc))
            .With("$recordedAt", SqliteValues.ToText(recordedAt))
            .With("$payload", SqliteValues.OrNull(payload));

        command.ExecuteNonQuery();
        return sequence;
    }

    /// <summary>How many entries are still waiting.</summary>
    public int PendingCount()
    {
        using var command = _connection.Command("SELECT COUNT(*) FROM outbox WHERE state = 'pending';");
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The oldest pending entries, in sequence order.</summary>
    /// <param name="kind">Restricts to one kind, so playtime can flush without saves.</param>
    /// <remarks>
    /// <c>POST /api/play-sessions</c> needs no open sync session, so an agent woken by
    /// <c>game-end</c> with nothing to negotiate flushes playtime on its own rather than
    /// opening a session it has no use for.
    /// </remarks>
    public IReadOnlyList<OutboxEntry> Pending(int limit = 100, OutboxKind? kind = null)
    {
        using var command = _connection.Command(
            """
            SELECT id, local_sequence, kind, rom_id, slot, emulator, batch_key, relative_path,
                   content_hash, size_bytes, file_mtime_utc, recorded_at_utc, payload, state,
                   attempts, last_error
            FROM outbox
            WHERE state = 'pending' AND ($kind IS NULL OR kind = $kind)
            ORDER BY local_sequence
            LIMIT $limit;
            """)
            .With("$limit", limit)
            .With("$kind", kind is null ? DBNull.Value : ToText(kind.Value));

        using var reader = command.ExecuteReader();
        var entries = new List<OutboxEntry>();
        while (reader.Read())
        {
            var pathText = reader.GetStringOrNull(7);
            RelativePath? path = pathText is not null && Paths.RelativePath.TryCreate(pathText, out var parsed)
                ? parsed
                : null;

            entries.Add(new OutboxEntry(
                reader.GetInt64(0),
                reader.GetInt64(1),
                ParseKind(reader.GetString(2)),
                reader.GetInt64OrNull(3),
                reader.GetStringOrNull(4),
                reader.GetStringOrNull(5),
                reader.GetStringOrNull(6),
                path,
                reader.GetStringOrNull(8),
                reader.GetInt64OrNull(9),
                reader.GetTimestampOrNull(10),
                reader.GetTimestampOrNull(11) ?? DateTimeOffset.MinValue,
                reader.GetStringOrNull(12),
                ParseState(reader.GetString(13)),
                (int)reader.GetInt64(14),
                reader.GetStringOrNull(15)));
        }

        return entries;
    }

    /// <summary>Marks an entry as delivered.</summary>
    public void MarkSent(long id, DateTimeOffset now)
    {
        using var command = _connection.Command(
            """
            UPDATE outbox
            SET state = 'sent', attempts = attempts + 1, last_attempt_at = $now, last_error = NULL
            WHERE id = $id;
            """)
            .With("$id", id)
            .With("$now", SqliteValues.ToText(now));

        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Records a failed attempt, leaving the entry pending.
    /// </summary>
    /// <remarks>
    /// Failure does not consume the entry. Being offline is the normal case and a later
    /// replay is safe, so the only thing an attempt costs is a counter.
    /// </remarks>
    public void RecordFailure(long id, string error, DateTimeOffset now)
    {
        using var command = _connection.Command(
            """
            UPDATE outbox
            SET attempts = attempts + 1, last_attempt_at = $now, last_error = $error
            WHERE id = $id;
            """)
            .With("$id", id)
            .With("$error", error)
            .With("$now", SqliteValues.ToText(now));

        command.ExecuteNonQuery();
    }

    private static string ToText(OutboxKind kind) => kind switch
    {
        OutboxKind.Save => "save",
        OutboxKind.State => "state",
        OutboxKind.PlaySession => "play_session",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static OutboxKind ParseKind(string value) => value switch
    {
        "save" => OutboxKind.Save,
        "state" => OutboxKind.State,
        "play_session" => OutboxKind.PlaySession,
        _ => throw new InvalidOperationException($"Unknown outbox kind '{value}' in the database."),
    };

    private static OutboxState ParseState(string value) => value switch
    {
        "pending" => OutboxState.Pending,
        "sent" => OutboxState.Sent,
        "failed" => OutboxState.Failed,
        _ => throw new InvalidOperationException($"Unknown outbox state '{value}' in the database."),
    };
}
