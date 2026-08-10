using Microsoft.Data.Sqlite;

namespace RomMBat.Core.Store;

/// <summary>Where one endpoint's incremental sync starts, and where an interrupted walk stopped.</summary>
public sealed record SyncCursor
{
    public required string Endpoint { get; init; }

    /// <summary>
    /// Only fetch rows changed after this.
    /// </summary>
    /// <remarks>
    /// The normal path, and the reason a routine sync is seconds rather than the fourteen
    /// minutes a full 83k walk takes. Advanced only when a walk completes.
    /// </remarks>
    public DateTimeOffset? UpdatedAfter { get; init; }

    /// <summary>The offset an interrupted walk stopped at, or null when no walk is in flight.</summary>
    public int? ResumeOffset { get; init; }

    /// <summary>
    /// When the in-flight walk started.
    /// </summary>
    /// <remarks>
    /// Captured before the first page and promoted to <see cref="UpdatedAfter"/> only when the
    /// walk finishes. Taking the clock at the end instead would silently skip everything that
    /// changed while the walk was running.
    /// </remarks>
    public DateTimeOffset? WalkStartedAt { get; init; }

    /// <summary>How many rows the in-flight walk expects.</summary>
    public int? WalkTotal { get; init; }

    public DateTimeOffset? LastRunAt { get; init; }

    public DateTimeOffset? LastSuccessAt { get; init; }

    /// <summary>True when a walk was started and never finished.</summary>
    public bool HasWalkInFlight => ResumeOffset is not null;
}

/// <summary>
/// The cursors that make a sync incremental, and a full walk resumable.
/// </summary>
/// <remarks>
/// A full walk is a first-run or repair operation, not routine, and at roughly 2.5 s per
/// 250-row page it takes about fourteen minutes on an 83k library. Something that long gets
/// interrupted, so the offset is written after every page and a later run picks it up.
/// </remarks>
public sealed class SyncCursorStore
{
    private readonly SqliteConnection _connection;

    internal SyncCursorStore(SqliteConnection connection) => _connection = connection;

    /// <summary>Reads a cursor, or null when the endpoint has never been walked.</summary>
    public SyncCursor? Read(string endpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        using var command = _connection
            .Command(
                """
                SELECT endpoint, updated_after, resume_offset, walk_started_at, walk_total,
                       last_run_at, last_success_at
                FROM sync_cursor WHERE endpoint = $endpoint;
                """)
            .With("$endpoint", endpoint);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new SyncCursor
        {
            Endpoint = reader.GetString(0),
            UpdatedAfter = reader.GetTimestampOrNull(1),
            ResumeOffset = (int?)reader.GetInt64OrNull(2),
            WalkStartedAt = reader.GetTimestampOrNull(3),
            WalkTotal = (int?)reader.GetInt64OrNull(4),
            LastRunAt = reader.GetTimestampOrNull(5),
            LastSuccessAt = reader.GetTimestampOrNull(6),
        };
    }

    /// <summary>Every cursor, for <c>status</c>.</summary>
    public IReadOnlyList<SyncCursor> List()
    {
        using var command = _connection.Command("SELECT endpoint FROM sync_cursor ORDER BY endpoint;");
        using var reader = command.ExecuteReader();

        var endpoints = new List<string>();
        while (reader.Read())
        {
            endpoints.Add(reader.GetString(0));
        }

        return [.. endpoints.Select(Read).Where(cursor => cursor is not null).Select(cursor => cursor!)];
    }

    /// <summary>
    /// Opens a walk, keeping the existing <c>updated_after</c> untouched.
    /// </summary>
    /// <remarks>
    /// Resumes rather than restarts when one is already in flight, which is what makes an
    /// interrupted fourteen-minute walk cost only the pages it had not reached.
    /// </remarks>
    public SyncCursor BeginWalk(string endpoint, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        var existing = Read(endpoint);
        if (existing?.HasWalkInFlight == true)
        {
            Touch(endpoint, now);
            return existing with { LastRunAt = now };
        }

        using var command = _connection.Command(
            """
            INSERT INTO sync_cursor (endpoint, resume_offset, walk_started_at, last_run_at)
            VALUES ($endpoint, 0, $now, $now)
            ON CONFLICT (endpoint) DO UPDATE SET
              resume_offset   = 0,
              walk_started_at = excluded.walk_started_at,
              last_run_at     = excluded.last_run_at;
            """)
            .With("$endpoint", endpoint)
            .With("$now", SqliteValues.ToText(now));

        command.ExecuteNonQuery();
        return Read(endpoint)!;
    }

    /// <summary>Records progress after a page, so the next run starts here.</summary>
    public void RecordProgress(string endpoint, int offset, int? total, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);

        using var command = _connection
            .Command(
                """
                UPDATE sync_cursor
                SET resume_offset = $offset, walk_total = $total, last_run_at = $now
                WHERE endpoint = $endpoint;
                """)
            .With("$offset", offset)
            .With("$total", SqliteValues.OrNull(total))
            .With("$now", SqliteValues.ToText(now))
            .With("$endpoint", endpoint);

        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Closes a walk and advances <c>updated_after</c> to when that walk started.
    /// </summary>
    /// <remarks>
    /// The start time, not the finish time. Anything changed while the walk was running is
    /// then picked up by the next incremental pass rather than falling into the gap.
    /// </remarks>
    public void CompleteWalk(string endpoint, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        using var command = _connection
            .Command(
                """
                UPDATE sync_cursor
                SET updated_after   = COALESCE(walk_started_at, $now),
                    resume_offset   = NULL,
                    walk_started_at = NULL,
                    walk_total      = NULL,
                    last_run_at     = $now,
                    last_success_at = $now
                WHERE endpoint = $endpoint;
                """)
            .With("$now", SqliteValues.ToText(now))
            .With("$endpoint", endpoint);

        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Closes a walk that read nothing, leaving <c>updated_after</c> where it was.
    /// </summary>
    /// <remarks>
    /// A refusal is not an interrupted walk. Nothing was read, so there is nothing to resume,
    /// and leaving the walk in flight would pin <c>walk_started_at</c> at the first refusal
    /// for every run after it.
    /// </remarks>
    public void AbandonWalk(string endpoint, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        using var command = _connection
            .Command(
                """
                UPDATE sync_cursor
                SET resume_offset   = NULL,
                    walk_started_at = NULL,
                    walk_total      = NULL,
                    last_run_at     = $now
                WHERE endpoint = $endpoint;
                """)
            .With("$now", SqliteValues.ToText(now))
            .With("$endpoint", endpoint);

        command.ExecuteNonQuery();
    }

    /// <summary>Throws away the incremental cursor, so the next run walks everything again.</summary>
    public void Reset(string endpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        using var command = _connection
            .Command("DELETE FROM sync_cursor WHERE endpoint = $endpoint;")
            .With("$endpoint", endpoint);

        command.ExecuteNonQuery();
    }

    private void Touch(string endpoint, DateTimeOffset now)
    {
        using var command = _connection
            .Command("UPDATE sync_cursor SET last_run_at = $now WHERE endpoint = $endpoint;")
            .With("$now", SqliteValues.ToText(now))
            .With("$endpoint", endpoint);

        command.ExecuteNonQuery();
    }
}
