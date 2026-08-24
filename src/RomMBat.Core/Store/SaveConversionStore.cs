using System.Globalization;
using Microsoft.Data.Sqlite;

namespace RomMBat.Core.Store;

/// <summary>What was in <c>es_settings.cfg</c> before RomMBat wrote to it.</summary>
/// <remarks>
/// Two states, not one, and they are not interchangeable. Restoring "absent" over a key that
/// held a value leaves the user somewhere they never were, and so does the reverse.
/// </remarks>
public enum PriorSettingState
{
    /// <summary>The key was not in the file.</summary>
    Absent,

    /// <summary>The key was there, and <c>PriorValue</c> is what it held.</summary>
    Present,
}

/// <summary>One game RomMBat opted into a per-game save container.</summary>
public sealed record SaveConversion
{
    public required int RomId { get; init; }

    public required string System { get; init; }

    /// <summary>The ROM's filename with its extension, which is what the ES key is built from.</summary>
    public required string FsName { get; init; }

    /// <summary>The bare option, e.g. <c>pcsx2_slot1_memory</c>, never the full scoped key.</summary>
    public required string SettingKey { get; init; }

    public required string AppliedValue { get; init; }

    public required PriorSettingState PriorState { get; init; }

    /// <summary>Non-null exactly when <see cref="PriorState"/> is <see cref="PriorSettingState.Present"/>.</summary>
    public string? PriorValue { get; init; }

    public DateTimeOffset ConvertedAtUtc { get; init; }
}

/// <summary>
/// The record of every conversion, which is what makes one reversible.
/// </summary>
/// <remarks>
/// <b>This table is the only thing that knows what the file used to look like.</b> Reading
/// <c>es_settings.cfg</c> later cannot recover it, and M6 stage 2c measured why in both
/// directions: ES prunes a setting equal to its own default, so absence is not evidence of a
/// revert, and ES also adds keys on its own, so presence is not evidence of the user's intent.
/// See <c>docs/retrobat-findings.md</c>, 170.
/// </remarks>
public sealed class SaveConversionStore
{
    private readonly SqliteConnection _connection;

    internal SaveConversionStore(SqliteConnection connection) => _connection = connection;

    /// <summary>Records a conversion, replacing any earlier one for the same key.</summary>
    public void Record(SaveConversion conversion)
    {
        ArgumentNullException.ThrowIfNull(conversion);

        if ((conversion.PriorState == PriorSettingState.Present) != (conversion.PriorValue is not null))
        {
            throw new ArgumentException(
                "A prior state of 'present' needs a value and 'absent' must not have one; the two "
                    + "describe different files to restore.",
                nameof(conversion));
        }

        using var command = _connection.Command(
            """
            INSERT INTO save_conversion
                (rom_id, system, fs_name, setting_key, applied_value, prior_state, prior_value,
                 converted_at_utc)
            VALUES ($romId, $system, $fsName, $key, $applied, $priorState, $priorValue, $at)
            ON CONFLICT (system, fs_name, setting_key) DO UPDATE SET
                rom_id           = excluded.rom_id,
                applied_value    = excluded.applied_value,
                prior_state      = excluded.prior_state,
                prior_value      = excluded.prior_value,
                converted_at_utc = excluded.converted_at_utc;
            """)
            .With("$romId", conversion.RomId)
            .With("$system", conversion.System)
            .With("$fsName", conversion.FsName)
            .With("$key", conversion.SettingKey)
            .With("$applied", conversion.AppliedValue)
            .With("$priorState", conversion.PriorState == PriorSettingState.Present ? "present" : "absent")
            .With("$priorValue", (object?)conversion.PriorValue ?? DBNull.Value)
            .With("$at", conversion.ConvertedAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));

        command.ExecuteNonQuery();
    }

    /// <summary>The conversion for one game and option, or null when there is none.</summary>
    public SaveConversion? Find(string system, string fsName, string settingKey)
    {
        using var command = _connection.Command(
            $"{SelectColumns} WHERE system = $system AND fs_name = $fsName AND setting_key = $key;")
            .With("$system", system)
            .With("$fsName", fsName)
            .With("$key", settingKey);

        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    /// <summary>Every conversion on this install, oldest first.</summary>
    public IReadOnlyList<SaveConversion> List()
    {
        using var command = _connection.Command($"{SelectColumns} ORDER BY converted_at_utc, id;");
        using var reader = command.ExecuteReader();

        var results = new List<SaveConversion>();
        while (reader.Read())
        {
            results.Add(Read(reader));
        }

        return results;
    }

    /// <summary>Drops the record, which is what reverting does once the file is back.</summary>
    /// <remarks>
    /// No tombstone. Once the setting holds its prior state there is nothing left to reverse,
    /// and the container the conversion produced is left on disk deliberately: it holds real
    /// progress, and where it came from is evident from where it sits.
    /// </remarks>
    public bool Forget(string system, string fsName, string settingKey)
    {
        using var command = _connection.Command(
            "DELETE FROM save_conversion WHERE system = $system AND fs_name = $fsName AND setting_key = $key;")
            .With("$system", system)
            .With("$fsName", fsName)
            .With("$key", settingKey);

        return command.ExecuteNonQuery() > 0;
    }

    private const string SelectColumns =
        """
        SELECT rom_id, system, fs_name, setting_key, applied_value, prior_state, prior_value,
               converted_at_utc
        FROM save_conversion
        """;

    private static SaveConversion Read(SqliteDataReader reader) => new()
    {
        RomId = reader.GetInt32(0),
        System = reader.GetString(1),
        FsName = reader.GetString(2),
        SettingKey = reader.GetString(3),
        AppliedValue = reader.GetString(4),
        PriorState = reader.GetString(5) == "present" ? PriorSettingState.Present : PriorSettingState.Absent,
        PriorValue = reader.IsDBNull(6) ? null : reader.GetString(6),
        ConvertedAtUtc = DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture),
    };
}
