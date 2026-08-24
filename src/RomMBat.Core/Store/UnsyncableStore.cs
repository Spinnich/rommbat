using Microsoft.Data.Sqlite;

namespace RomMBat.Core.Store;

/// <summary>Why something under <c>saves/</c> is not going up.</summary>
public enum UnsyncableReason
{
    /// <summary>The shape is understood and this build does not carry it. Stage 2's list.</summary>
    NotInThisVersion,

    /// <summary>No shape definition claims this path, so nothing may be assumed about it.</summary>
    UnknownShape,

    /// <summary>A container holding several games' saves, which has no rom to belong to.</summary>
    SharedContainer,

    /// <summary>The shape is supported and the save could not be tied to a ROM.</summary>
    Unattributed,

    /// <summary>
    /// Understood, attributable, and already being kept in step by something that is not
    /// RomMBat. Reported so the user knows a second copy exists, never acted on.
    /// </summary>
    ManagedElsewhere,
}

/// <summary>One thing that cannot be synced, and why.</summary>
public sealed record UnsyncableEntry(
    string System,
    string Emulator,
    UnsyncableReason Reason,
    string Detail,
    int FileCount,
    DateTimeOffset ObservedAtUtc);

/// <summary>
/// What RomMBat found and is not syncing.
/// </summary>
/// <remarks>
/// <b>The alternative to this table is silence.</b> This build discovers class C and class D
/// and ships neither, so without a report a user's PS3 saves simply never go up and nothing
/// says so. The plan's own words are "reported as not syncable, here is why", and that is a
/// table rather than a log line because <c>status</c> has to be able to answer it with the
/// server unreachable.
/// <para>
/// Rewritten wholesale on each scan rather than accumulated, so a system that becomes
/// syncable stops being listed. That is what makes the count an honest measure of coverage,
/// and it is why save states stopped appearing here the moment they started syncing rather
/// than needing the rows cleaned up.
/// </para>
/// </remarks>
public sealed class UnsyncableStore
{
    private readonly SqliteConnection _connection;

    internal UnsyncableStore(SqliteConnection connection) => _connection = connection;

    /// <summary>Records one reason, replacing any earlier one for the same trio.</summary>
    public void Record(
        string system,
        string emulator,
        UnsyncableReason reason,
        string detail,
        int fileCount,
        DateTimeOffset observedAt)
    {
        using var command = _connection.Command(
            """
            INSERT INTO unsyncable (system, emulator, reason_kind, detail, file_count, observed_at_utc)
            VALUES ($system, $emulator, $reason, $detail, $count, $observedAt)
            ON CONFLICT (system, emulator, reason_kind) DO UPDATE SET
              detail          = excluded.detail,
              file_count      = excluded.file_count,
              observed_at_utc = excluded.observed_at_utc;
            """)
            .With("$system", system)
            .With("$emulator", emulator ?? string.Empty)
            .With("$reason", ToText(reason))
            .With("$detail", detail)
            .With("$count", fileCount)
            .With("$observedAt", SqliteValues.ToText(observedAt));

        command.ExecuteNonQuery();
    }

    /// <summary>Everything currently unsyncable, worst coverage first.</summary>
    public IReadOnlyList<UnsyncableEntry> List()
    {
        using var command = _connection.Command(
            """
            SELECT system, emulator, reason_kind, detail, file_count, observed_at_utc
            FROM unsyncable
            ORDER BY file_count DESC, system, emulator;
            """);

        using var reader = command.ExecuteReader();
        var entries = new List<UnsyncableEntry>();

        while (reader.Read())
        {
            entries.Add(new UnsyncableEntry(
                reader.GetString(0),
                reader.GetString(1),
                ParseReason(reader.GetString(2)),
                reader.GetString(3),
                (int)reader.GetInt64(4),
                reader.GetTimestampOrNull(5) ?? DateTimeOffset.MinValue));
        }

        return entries;
    }

    /// <summary>Clears everything observed before a scan, so a fixed system stops being listed.</summary>
    public void ForgetOlderThan(DateTimeOffset cutoff)
    {
        using var command = _connection
            .Command("DELETE FROM unsyncable WHERE observed_at_utc < $cutoff;")
            .With("$cutoff", SqliteValues.ToText(cutoff));

        command.ExecuteNonQuery();
    }

    private static string ToText(UnsyncableReason value) => value switch
    {
        UnsyncableReason.NotInThisVersion => "not_in_this_version",
        UnsyncableReason.UnknownShape => "unknown_shape",
        UnsyncableReason.SharedContainer => "shared_container",
        UnsyncableReason.Unattributed => "unattributed",
        UnsyncableReason.ManagedElsewhere => "managed_elsewhere",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static UnsyncableReason ParseReason(string value) => value switch
    {
        "not_in_this_version" => UnsyncableReason.NotInThisVersion,
        "unknown_shape" => UnsyncableReason.UnknownShape,
        "shared_container" => UnsyncableReason.SharedContainer,
        "unattributed" => UnsyncableReason.Unattributed,
        "managed_elsewhere" => UnsyncableReason.ManagedElsewhere,
        _ => throw new InvalidOperationException($"Unknown unsyncable reason '{value}' in the database."),
    };
}
