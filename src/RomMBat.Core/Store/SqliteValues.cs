using System.Data;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace RomMBat.Core.Store;

/// <summary>
/// Reading and writing the few value shapes the schema uses.
/// </summary>
/// <remarks>
/// Timestamps are stored as ISO-8601 round-trip strings in UTC. SQLite has no date type, and
/// a text timestamp sorts correctly, survives a copy between machines and is readable in a
/// support dump, which a unix epoch integer is not.
/// </remarks>
internal static class SqliteValues
{
    public static string ToText(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    public static object ToTextOrNull(DateTimeOffset? value) =>
        value.HasValue ? ToText(value.Value) : DBNull.Value;

    public static object OrNull(string? value) =>
        string.IsNullOrEmpty(value) ? DBNull.Value : value;

    public static object OrNull(byte[]? value) =>
        value is null ? DBNull.Value : value;

    public static object OrNull(long? value) =>
        value.HasValue ? value.Value : DBNull.Value;

    public static string? GetStringOrNull(this SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    public static long? GetInt64OrNull(this SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);

    public static double? GetDoubleOrNull(this SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetDouble(ordinal);

    public static byte[]? GetBlobOrNull(this SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        using var stream = reader.GetStream(ordinal);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    public static DateTimeOffset? GetTimestampOrNull(this SqliteDataReader reader, int ordinal)
    {
        var text = reader.GetStringOrNull(ordinal);
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var value)
            ? value
            : null;
    }

    public static SqliteCommand Command(this SqliteConnection connection, string sql)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        return command;
    }

    public static SqliteCommand With(this SqliteCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        if (value is DBNull)
        {
            parameter.DbType = DbType.String;
        }

        command.Parameters.Add(parameter);
        return command;
    }
}
