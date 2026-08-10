using System.Globalization;
using Microsoft.Data.Sqlite;

namespace RomMBat.Core.Store;

/// <summary>
/// Install-wide settings, keyed by a dotted name.
/// </summary>
/// <remarks>
/// Free-form on purpose: the keys are owned by the code that reads them.
/// <para>
/// Install-local, and only that: <see cref="Sync.RoamingSyncConfig"/> carries the set
/// definitions and the platform overrides, not these, so a re-paired device starts back at
/// unlimited until its budget is set again.
/// </para>
/// <para>
/// A set's own byte cap lives on <c>sync_set</c>, because it belongs to that set. What lives
/// here is what applies to the install: the global budget and the free-space floor.
/// </para>
/// </remarks>
public sealed class SettingStore
{
    /// <summary>How many bytes of ROM content the whole install may hold. Unset means unbounded.</summary>
    public const string ContentMaxBytes = "content.max_bytes";

    /// <summary>
    /// How much space to leave on the volume, whatever the budget says.
    /// </summary>
    /// <remarks>
    /// A budget alone cannot protect a shared drive: the user may cap RomMBat at 200 GB on a
    /// disk that something else is also filling. This is the second bound, checked against
    /// live free space rather than against anything recorded.
    /// </remarks>
    public const string FreeSpaceFloorBytes = "content.free_space_floor_bytes";

    /// <summary>The floor when nothing is configured: enough for Windows to stay healthy.</summary>
    public const long DefaultFreeSpaceFloorBytes = 2L * 1024 * 1024 * 1024;

    private readonly SqliteConnection _connection;

    internal SettingStore(SqliteConnection connection) => _connection = connection;

    /// <summary>The value for a key, or null.</summary>
    public string? Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        using var command = _connection
            .Command("SELECT value FROM setting WHERE key = $key;")
            .With("$key", key);

        using var reader = command.ExecuteReader();
        return reader.Read() ? reader.GetStringOrNull(0) : null;
    }

    /// <summary>The value for a key as a number, or null when unset or unreadable.</summary>
    public long? GetInt64(string key) =>
        long.TryParse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>Writes a value, or removes the key when the value is null.</summary>
    public void Set(string key, string? value, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (value is null)
        {
            using var delete = _connection
                .Command("DELETE FROM setting WHERE key = $key;")
                .With("$key", key);

            delete.ExecuteNonQuery();
            return;
        }

        using var command = _connection
            .Command(
                """
                INSERT INTO setting (key, value, updated_at) VALUES ($key, $value, $now)
                ON CONFLICT (key) DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at;
                """)
            .With("$key", key)
            .With("$value", value)
            .With("$now", SqliteValues.ToText(now));

        command.ExecuteNonQuery();
    }

    /// <summary>Writes a numeric value, or removes the key when it is null.</summary>
    public void Set(string key, long? value, DateTimeOffset now) =>
        Set(key, value?.ToString(CultureInfo.InvariantCulture), now);

    /// <summary>Every setting, for the roaming document and for status.</summary>
    public IReadOnlyDictionary<string, string> All()
    {
        using var command = _connection.Command("SELECT key, value FROM setting ORDER BY key;");
        using var reader = command.ExecuteReader();

        var settings = new Dictionary<string, string>(StringComparer.Ordinal);
        while (reader.Read())
        {
            settings[reader.GetString(0)] = reader.GetStringOrNull(1) ?? string.Empty;
        }

        return settings;
    }
}
