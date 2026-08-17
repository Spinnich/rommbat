using Microsoft.Data.Sqlite;
using RomMBat.Core.Paths;

namespace RomMBat.Core.Store;

/// <summary>The EmulationStation events RomMBat installs a hook for.</summary>
/// <remarks>
/// The schema admits <c>shutdown</c>, <c>sleep</c> and <c>wake</c> too. ES fires those and
/// RomMBat installs no hook for them, so nothing writes one today.
/// </remarks>
public enum JournalEvent
{
    /// <summary>EmulationStation started. Carries no arguments, and is the heartbeat.</summary>
    Start,

    /// <summary>A game launched. Carries rom path, basename and display name.</summary>
    GameStart,

    /// <summary>A game ended. Carries <b>nothing</b>, and fires with no matching start.</summary>
    GameEnd,

    /// <summary>EmulationStation is exiting.</summary>
    Quit,
}

/// <summary>Where a journal entry is in its life.</summary>
public enum JournalState
{
    /// <summary>Written by a hook and not yet reconciled against the launch log.</summary>
    Open,

    /// <summary>Reconciled, and whatever it produced is in the outbox.</summary>
    Correlated,

    /// <summary>Reconciled to nothing. An orphan <c>game-end</c> ends here.</summary>
    Discarded,
}

/// <summary>One thing a hook saw.</summary>
/// <param name="RomPath">
/// Null whenever the hook was not told one, and also when it was told an absolute path that
/// lies outside the tree, since no absolute path is ever persisted.
/// </param>
/// <param name="System">Filled in later from the launch log. The hook is never told it.</param>
public sealed record JournalEntry(
    long Id,
    long LocalSequence,
    JournalEvent Event,
    RelativePath? RomPath,
    string? RomBasename,
    string? DisplayName,
    string? System,
    string? Emulator,
    string? Core,
    int? ProcessId,
    DateTimeOffset RecordedAtUtc,
    JournalState State);

/// <summary>
/// What the ES hooks append, and the only thing they do.
/// </summary>
/// <remarks>
/// <b>Append-only and dumb, because it is written on the game-launch path.</b> No HTTP, no
/// hashing and no correlation happens here; all of that is too slow to do inside a launch and
/// belongs to the flush. CLAUDE.md rule 4 makes this a rule rather than a preference.
/// <para>
/// <b>Concurrency is the normal case.</b> ES spawns event scripts fire-and-forget, and M0
/// probe 1 caught three <c>game-end</c> hooks in flight at once, interleaving writes to one
/// file. That is why the journal is a SQLite table and not a text log: a line-oriented file
/// gives no cross-process atomicity, and a record split by another process's write is
/// unrecoverable. <see cref="LocalStore"/> opens with WAL and a five-second busy timeout, so
/// an append waits for the writer ahead of it and commits whole.
/// </para>
/// </remarks>
public sealed class JournalStore
{
    private readonly SqliteConnection _connection;
    private readonly LocalStore _store;

    internal JournalStore(SqliteConnection connection, LocalStore store)
    {
        _connection = connection;
        _store = store;
    }

    /// <summary>Appends what a hook saw, stamping it with the next sequence number.</summary>
    /// <returns>The sequence number it was given.</returns>
    public long Append(
        JournalEvent journalEvent,
        DateTimeOffset recordedAt,
        RelativePath? romPath = null,
        string? romBasename = null,
        string? displayName = null,
        int? processId = null)
    {
        var sequence = _store.NextSequence();

        using var command = _connection.Command(
            """
            INSERT INTO journal (local_sequence, event, rom_relative_path, rom_basename,
                                 display_name, process_id, recorded_at_utc)
            VALUES ($sequence, $event, $path, $basename, $display, $pid, $recordedAt);
            """)
            .With("$sequence", sequence)
            .With("$event", ToText(journalEvent))
            .With("$path", romPath.HasValue ? romPath.Value.Value : DBNull.Value)
            .With("$basename", SqliteValues.OrNull(romBasename))
            .With("$display", SqliteValues.OrNull(displayName))
            .With("$pid", SqliteValues.OrNull(processId))
            .With("$recordedAt", SqliteValues.ToText(recordedAt));

        command.ExecuteNonQuery();
        return sequence;
    }

    /// <summary>Everything still waiting to be reconciled, oldest first.</summary>
    public IReadOnlyList<JournalEntry> Open(int limit = 500) => Query(
        """
        SELECT id, local_sequence, event, rom_relative_path, rom_basename, display_name,
               system, emulator, core, process_id, recorded_at_utc, state
        FROM journal
        WHERE state = 'open'
        ORDER BY local_sequence
        LIMIT $limit;
        """,
        command => command.With("$limit", limit));

    /// <summary>Every entry, oldest first. For <c>status</c> and for tests.</summary>
    public IReadOnlyList<JournalEntry> All(int limit = 500) => Query(
        """
        SELECT id, local_sequence, event, rom_relative_path, rom_basename, display_name,
               system, emulator, core, process_id, recorded_at_utc, state
        FROM journal
        ORDER BY local_sequence
        LIMIT $limit;
        """,
        command => command.With("$limit", limit));

    /// <summary>When EmulationStation last reported starting.</summary>
    /// <remarks>
    /// The heartbeat the plan asks for. Both scripted hook forms fail silently on some hosts
    /// (a replaced <c>batfile</c> association, a <c>Restricted</c> execution policy), and
    /// RomMBat ships executables partly for that reason, but an executable can be blocked too.
    /// Play data with no hook activity behind it is a state worth reporting rather than a
    /// reason to lose every play session quietly.
    /// </remarks>
    public DateTimeOffset? LastStart()
    {
        using var command = _connection.Command(
            "SELECT MAX(recorded_at_utc) FROM journal WHERE event = 'start';");

        using var reader = command.ExecuteReader();
        return reader.Read() ? reader.GetTimestampOrNull(0) : null;
    }

    /// <summary>Marks entries reconciled, so a replayed flush does not reconcile them twice.</summary>
    public void Close(IEnumerable<long> ids, JournalState state, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (state == JournalState.Open)
        {
            throw new ArgumentOutOfRangeException(nameof(state), "Closing an entry cannot leave it open.");
        }

        foreach (var id in ids)
        {
            using var command = _connection.Command(
                """
                UPDATE journal
                SET state = $state, correlated_at_utc = $now
                WHERE id = $id AND state = 'open';
                """)
                .With("$id", id)
                .With("$state", ToText(state))
                .With("$now", SqliteValues.ToText(now));

            command.ExecuteNonQuery();
        }
    }

    /// <summary>How many entries are still waiting, for <c>status</c>.</summary>
    public int OpenCount()
    {
        using var command = _connection.Command("SELECT COUNT(*) FROM journal WHERE state = 'open';");
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private List<JournalEntry> Query(string sql, Func<SqliteCommand, SqliteCommand> bind)
    {
        using var command = bind(_connection.Command(sql));
        using var reader = command.ExecuteReader();

        var entries = new List<JournalEntry>();
        while (reader.Read())
        {
            var pathText = reader.GetStringOrNull(3);
            RelativePath? path = pathText is not null && RelativePath.TryCreate(pathText, out var parsed)
                ? parsed
                : null;

            entries.Add(new JournalEntry(
                reader.GetInt64(0),
                reader.GetInt64(1),
                ParseEvent(reader.GetString(2)),
                path,
                reader.GetStringOrNull(4),
                reader.GetStringOrNull(5),
                reader.GetStringOrNull(6),
                reader.GetStringOrNull(7),
                reader.GetStringOrNull(8),
                (int?)reader.GetInt64OrNull(9),
                reader.GetTimestampOrNull(10) ?? DateTimeOffset.MinValue,
                ParseState(reader.GetString(11))));
        }

        return entries;
    }

    private static string ToText(JournalEvent value) => value switch
    {
        JournalEvent.Start => "start",
        JournalEvent.GameStart => "game-start",
        JournalEvent.GameEnd => "game-end",
        JournalEvent.Quit => "quit",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static JournalEvent ParseEvent(string value) => value switch
    {
        "start" => JournalEvent.Start,
        "game-start" => JournalEvent.GameStart,
        "game-end" => JournalEvent.GameEnd,
        "quit" => JournalEvent.Quit,
        _ => throw new InvalidOperationException($"Unknown journal event '{value}' in the database."),
    };

    private static string ToText(JournalState value) => value switch
    {
        JournalState.Open => "open",
        JournalState.Correlated => "correlated",
        JournalState.Discarded => "discarded",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static JournalState ParseState(string value) => value switch
    {
        "open" => JournalState.Open,
        "correlated" => JournalState.Correlated,
        "discarded" => JournalState.Discarded,
        _ => throw new InvalidOperationException($"Unknown journal state '{value}' in the database."),
    };
}
