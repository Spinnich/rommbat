using System.Globalization;
using Microsoft.Data.Sqlite;

namespace RomMBat.Core.Store;

/// <summary>What a queued change wants the setting to end up as.</summary>
/// <remarks>
/// Removing is its own state rather than a null value. Finding 170 established that "the key
/// was absent" and "the key held the stock value" are different files to restore, so a queued
/// revert of a conversion whose prior state was absent has to say <see cref="Remove"/> and mean
/// it.
/// </remarks>
public enum DesiredSettingState
{
    /// <summary>Write <c>DesiredValue</c>.</summary>
    Set,

    /// <summary>Take the key out of the file.</summary>
    Remove,
}

/// <summary>How a queued change turned out, once something got to it.</summary>
public enum PendingConfigResult
{
    /// <summary>The setting was written and the conversion recorded.</summary>
    Applied,

    /// <summary>A rule said no, and <c>Detail</c> is the reason the user gets.</summary>
    Refused,

    /// <summary>Something went wrong that is not a rule. Also <c>Detail</c>.</summary>
    Failed,
}

/// <summary>One configuration change RomMBat means to make when it next can.</summary>
public sealed record PendingConfig
{
    public required int Id { get; init; }

    public required int RomId { get; init; }

    public required string System { get; init; }

    /// <summary>The ROM's filename with its extension, which is what the ES key is built from.</summary>
    public required string FsName { get; init; }

    /// <summary>The bare option, e.g. <c>pcsx2_slot1_memory</c>, never the full scoped key.</summary>
    public required string SettingKey { get; init; }

    public required DesiredSettingState DesiredState { get; init; }

    /// <summary>Non-null exactly when <see cref="DesiredState"/> is <see cref="DesiredSettingState.Set"/>.</summary>
    public string? DesiredValue { get; init; }

    /// <summary>Why, in the words the user will read weeks after queueing it.</summary>
    public required string Reason { get; init; }

    public DateTimeOffset QueuedAtUtc { get; init; }

    /// <summary>Null while the change is still outstanding.</summary>
    public DateTimeOffset? AppliedAtUtc { get; init; }

    /// <summary>Null while the change is still outstanding.</summary>
    public PendingConfigResult? Result { get; init; }

    public string? Detail { get; init; }

    /// <summary>True when nothing has tried to apply this yet.</summary>
    public bool IsOutstanding => AppliedAtUtc is null;
}

/// <summary>
/// The queue of configuration changes waiting for EmulationStation to be gone.
/// </summary>
/// <remarks>
/// <b>This exists because the UI can never write <c>es_settings.cfg</c> itself.</b> ES loads
/// that file at startup and serialises its own model over anything written afterwards, and the
/// UI is launched from the ES menu, so it runs under a live ES every single time. Queueing is
/// the only way a per-game setting is reachable from the interface RomMBat ships.
/// <para>
/// <b>The result outlives the apply on purpose.</b> Nothing is watching when
/// <c>background quit</c> drains this: the UI exited before the quit hook fired. A row deleted
/// on success would leave the next session unable to tell "applied" from "cancelled" from
/// "never queued". So a finished row keeps its outcome, and only a cancellation deletes,
/// because nothing happened and there is nothing to report.
/// </para>
/// </remarks>
public sealed class PendingConfigStore
{
    private readonly SqliteConnection _connection;

    internal PendingConfigStore(SqliteConnection connection) => _connection = connection;

    /// <summary>
    /// Queues a change, replacing any outstanding one for the same target.
    /// </summary>
    /// <remarks>
    /// Replacing rather than accumulating: queueing the same target twice is a user changing
    /// their mind, and two contradictory pending rows for one key would apply in an order
    /// nothing defines. Finished rows are untouched, so the history survives the replacement.
    /// </remarks>
    /// <returns>The row id, so a caller can report exactly what it queued.</returns>
    public int Queue(PendingConfigRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if ((request.DesiredState == DesiredSettingState.Set) != (request.DesiredValue is not null))
        {
            throw new ArgumentException(
                "A desired state of 'set' needs a value and 'remove' must not have one; removing a "
                    + "key and writing an empty one leave the file in different states.",
                nameof(request));
        }

        using var transaction = _connection.BeginTransaction();

        using (var clear = _connection.Command(
            """
            DELETE FROM pending_config
            WHERE system = $system AND fs_name = $fsName AND setting_key = $key
              AND applied_at_utc IS NULL;
            """)
            .With("$system", request.System)
            .With("$fsName", request.FsName)
            .With("$key", request.SettingKey))
        {
            clear.Transaction = transaction;
            clear.ExecuteNonQuery();
        }

        using var insert = _connection.Command(
            """
            INSERT INTO pending_config
                (rom_id, system, fs_name, setting_key, desired_state, desired_value, reason,
                 queued_at_utc)
            VALUES ($romId, $system, $fsName, $key, $state, $value, $reason, $at)
            RETURNING id;
            """)
            .With("$romId", request.RomId)
            .With("$system", request.System)
            .With("$fsName", request.FsName)
            .With("$key", request.SettingKey)
            .With("$state", request.DesiredState == DesiredSettingState.Set ? "set" : "remove")
            .With("$value", (object?)request.DesiredValue ?? DBNull.Value)
            .With("$reason", request.Reason)
            .With("$at", Stamp(request.QueuedAtUtc));

        insert.Transaction = transaction;
        var id = Convert.ToInt32(insert.ExecuteScalar(), CultureInfo.InvariantCulture);

        transaction.Commit();
        return id;
    }

    /// <summary>Everything still waiting, oldest first, which is the order it is applied in.</summary>
    public IReadOnlyList<PendingConfig> ListOutstanding() =>
        Query($"{SelectColumns} WHERE applied_at_utc IS NULL ORDER BY queued_at_utc, id;");

    /// <summary>The outstanding change for one target, or null when there is none.</summary>
    public PendingConfig? FindOutstanding(string system, string fsName, string settingKey)
    {
        using var command = _connection.Command(
            $"""
            {SelectColumns}
            WHERE system = $system AND fs_name = $fsName AND setting_key = $key
              AND applied_at_utc IS NULL;
            """)
            .With("$system", system)
            .With("$fsName", fsName)
            .With("$key", settingKey);

        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    /// <summary>
    /// What happened to changes that have already been applied, newest first.
    /// </summary>
    /// <remarks>
    /// The half the UI reads. It was not running when these were applied, so this is the only
    /// account of it there is.
    /// </remarks>
    public IReadOnlyList<PendingConfig> ListFinished(int limit = 20) =>
        Query($"{SelectColumns} WHERE applied_at_utc IS NOT NULL ORDER BY applied_at_utc DESC, id DESC LIMIT {limit};");

    /// <summary>
    /// Drops an outstanding change, which is what cancelling one means.
    /// </summary>
    /// <remarks>
    /// No tombstone, and it is the one case that deletes rather than recording an outcome:
    /// nothing was written, so there is nothing for a later reader to want.
    /// </remarks>
    /// <returns>False when there was nothing outstanding to cancel.</returns>
    public bool Cancel(string system, string fsName, string settingKey)
    {
        using var command = _connection.Command(
            """
            DELETE FROM pending_config
            WHERE system = $system AND fs_name = $fsName AND setting_key = $key
              AND applied_at_utc IS NULL;
            """)
            .With("$system", system)
            .With("$fsName", fsName)
            .With("$key", settingKey);

        return command.ExecuteNonQuery() > 0;
    }

    /// <summary>Stamps a row with what happened, which is what takes it out of the queue.</summary>
    public void RecordResult(int id, PendingConfigResult result, string detail, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);

        using var command = _connection.Command(
            """
            UPDATE pending_config
            SET applied_at_utc = $at, result = $result, detail = $detail
            WHERE id = $id AND applied_at_utc IS NULL;
            """)
            .With("$id", id)
            .With("$at", Stamp(at))
            .With("$result", result switch
            {
                PendingConfigResult.Applied => "applied",
                PendingConfigResult.Refused => "refused",
                _ => "failed",
            })
            .With("$detail", detail);

        command.ExecuteNonQuery();
    }

    private List<PendingConfig> Query(string sql)
    {
        using var command = _connection.Command(sql);
        using var reader = command.ExecuteReader();

        var results = new List<PendingConfig>();
        while (reader.Read())
        {
            results.Add(Read(reader));
        }

        return results;
    }

    private static string Stamp(DateTimeOffset at) =>
        at.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private const string SelectColumns =
        """
        SELECT id, rom_id, system, fs_name, setting_key, desired_state, desired_value, reason,
               queued_at_utc, applied_at_utc, result, detail
        FROM pending_config
        """;

    private static PendingConfig Read(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        RomId = reader.GetInt32(1),
        System = reader.GetString(2),
        FsName = reader.GetString(3),
        SettingKey = reader.GetString(4),
        DesiredState = reader.GetString(5) == "set" ? DesiredSettingState.Set : DesiredSettingState.Remove,
        DesiredValue = reader.IsDBNull(6) ? null : reader.GetString(6),
        Reason = reader.GetString(7),
        QueuedAtUtc = DateTimeOffset.Parse(reader.GetString(8), CultureInfo.InvariantCulture),
        AppliedAtUtc = reader.IsDBNull(9)
            ? null
            : DateTimeOffset.Parse(reader.GetString(9), CultureInfo.InvariantCulture),
        Result = reader.IsDBNull(10) ? null : reader.GetString(10) switch
        {
            "applied" => PendingConfigResult.Applied,
            "refused" => PendingConfigResult.Refused,
            _ => PendingConfigResult.Failed,
        },
        Detail = reader.IsDBNull(11) ? null : reader.GetString(11),
    };
}

/// <summary>What to queue. Separate from <see cref="PendingConfig"/>, which carries an id.</summary>
public sealed record PendingConfigRequest
{
    public required int RomId { get; init; }

    public required string System { get; init; }

    public required string FsName { get; init; }

    public required string SettingKey { get; init; }

    public required DesiredSettingState DesiredState { get; init; }

    public string? DesiredValue { get; init; }

    public required string Reason { get; init; }

    public DateTimeOffset QueuedAtUtc { get; init; }
}
