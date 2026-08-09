using Microsoft.Data.Sqlite;

namespace RomMBat.Core.Store;

/// <summary>What the last successful contact told us about the device clock.</summary>
public sealed record ClockRecord(
    DateTimeOffset? LastServerDateUtc,
    DateTimeOffset? LocalAtObservationUtc,
    TimeSpan? Skew,
    TimeSpan? RoundTrip,
    DateTimeOffset? LastContactUtc,
    DateTimeOffset? RestampOfferedAt)
{
    /// <summary>True when the measured skew is large enough to be worth telling the user about.</summary>
    public bool IsSkewSuspicious => Skew.HasValue && ClockSkew.IsSuspicious(Skew.Value);
}

/// <summary>
/// The thresholds every timestamp comparison has to carry.
/// </summary>
public static class ClockSkew
{
    /// <summary>
    /// How far into the future a file's mtime may sit before it means anything.
    /// </summary>
    /// <remarks>
    /// M0 probe 7 measured FAT32 <b>and exFAT</b> storing mtimes to 2 seconds and rounding
    /// <b>up</b>, so a file written at 08:03:16.097 is stamped 08:03:18.000: up to 2 seconds
    /// ahead of the clock that wrote it. Any "this timestamp is in the future, suspect a bad
    /// RTC" check without this tolerance trips on every FAT install.
    /// </remarks>
    public static TimeSpan FilesystemTimestampTolerance { get; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How much device-to-server skew is tolerated before RomMBat says something.
    /// </summary>
    /// <remarks>
    /// Play sessions dedup server-side on truncated-to-the-second timestamps and save
    /// ordering leans on mtime, so seconds matter. Half a minute is well past NTP jitter or
    /// HTTP <c>Date</c> being second-granular, and points at a flat RTC rather than drift.
    /// </remarks>
    public static TimeSpan WarnThreshold { get; } = TimeSpan.FromSeconds(30);

    /// <summary>True when the skew is large enough to warn about and offer a re-stamp.</summary>
    public static bool IsSuspicious(TimeSpan skew) => skew.Duration() > WarnThreshold;

    /// <summary>
    /// True when a file's modification time is far enough ahead of now to be a real problem
    /// rather than FAT rounding.
    /// </summary>
    public static bool IsImplausiblyInTheFuture(DateTimeOffset fileMtime, DateTimeOffset now) =>
        fileMtime - now > FilesystemTimestampTolerance;

    /// <summary>
    /// Skew from one observation, corrected for half the round trip.
    /// </summary>
    /// <remarks>
    /// Positive means the device clock runs fast. The HTTP <c>Date</c> header is
    /// second-granular, so anything under a second here is noise.
    /// </remarks>
    public static TimeSpan Measure(DateTimeOffset serverDate, DateTimeOffset localNow, TimeSpan roundTrip) =>
        localNow - (serverDate + (roundTrip / 2));
}

/// <summary>The singleton clock row.</summary>
public sealed class ClockStore
{
    private readonly SqliteConnection _connection;

    internal ClockStore(SqliteConnection connection) => _connection = connection;

    /// <summary>Reads what is known, which on a fresh install is nothing.</summary>
    public ClockRecord Read()
    {
        using var command = _connection.Command(
            """
            SELECT last_server_date_utc, local_at_observation_utc, skew_seconds,
                   round_trip_ms, last_contact_utc, restamp_offered_at
            FROM clock WHERE id = 1;
            """);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return new ClockRecord(null, null, null, null, null, null);
        }

        var skew = reader.GetDoubleOrNull(2);
        var roundTrip = reader.GetInt64OrNull(3);

        return new ClockRecord(
            reader.GetTimestampOrNull(0),
            reader.GetTimestampOrNull(1),
            skew.HasValue ? TimeSpan.FromSeconds(skew.Value) : null,
            roundTrip.HasValue ? TimeSpan.FromMilliseconds(roundTrip.Value) : null,
            reader.GetTimestampOrNull(4),
            reader.GetTimestampOrNull(5));
    }

    /// <summary>
    /// Records a successful contact and the skew it revealed.
    /// </summary>
    /// <param name="serverDate">The response <c>Date</c> header, or null when the server omitted it.</param>
    /// <param name="localNow">The device clock at the same moment.</param>
    /// <param name="roundTrip">How long the request took, so the estimate accounts for latency.</param>
    /// <returns>The measured skew, or null when the server sent no Date header.</returns>
    public TimeSpan? RecordContact(DateTimeOffset? serverDate, DateTimeOffset localNow, TimeSpan roundTrip)
    {
        var skew = serverDate.HasValue
            ? ClockSkew.Measure(serverDate.Value, localNow, roundTrip)
            : (TimeSpan?)null;

        using var command = _connection.Command(
            """
            INSERT INTO clock (id, last_server_date_utc, local_at_observation_utc,
                               skew_seconds, round_trip_ms, last_contact_utc)
            VALUES (1, $serverDate, $localNow, $skew, $roundTrip, $localNow)
            ON CONFLICT (id) DO UPDATE SET
              last_server_date_utc     = COALESCE(excluded.last_server_date_utc, clock.last_server_date_utc),
              local_at_observation_utc = excluded.local_at_observation_utc,
              skew_seconds             = COALESCE(excluded.skew_seconds, clock.skew_seconds),
              round_trip_ms            = excluded.round_trip_ms,
              last_contact_utc         = excluded.last_contact_utc;
            """)
            .With("$serverDate", SqliteValues.ToTextOrNull(serverDate))
            .With("$localNow", SqliteValues.ToText(localNow))
            .With("$skew", skew.HasValue ? skew.Value.TotalSeconds : DBNull.Value)
            .With("$roundTrip", (long)roundTrip.TotalMilliseconds);

        command.ExecuteNonQuery();
        return skew;
    }

    /// <summary>Records that the user was offered a re-stamp, so they are not asked twice.</summary>
    public void MarkRestampOffered(DateTimeOffset now)
    {
        using var command = _connection
            .Command("UPDATE clock SET restamp_offered_at = $now WHERE id = 1;")
            .With("$now", SqliteValues.ToText(now));

        command.ExecuteNonQuery();
    }
}
