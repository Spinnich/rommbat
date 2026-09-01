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

    /// <summary>
    /// A command that holds the store's gate until it is disposed.
    /// </summary>
    /// <remarks>
    /// <b>One <see cref="SqliteConnection"/> is shared by every store, and it is not
    /// thread-safe.</b> Nothing serialised it, and the failure is not a clean exception: two
    /// threads mutating one connection's prepared-statement list threw "Collection was modified"
    /// out of <c>SqliteCommand.Dispose</c> in a full test run.
    /// <para>
    /// <b>M7 stage 7b-2b is what made it reachable.</b> Before it, the only background work
    /// touching the store was a resolve. A sync writes from a background thread for minutes,
    /// once per ROM, once per artwork file and once per rollback, while the drawing thread reads
    /// the same connection on every redraw to build the screen underneath.
    /// </para>
    /// <para>
    /// <b>The gate is entered when the command is created and left when it is disposed</b>,
    /// which covers the reader too: every call site in this project reads inside the command's
    /// own <c>using</c> scope, so the reader is always disposed first. <c>lock</c> is re-entrant
    /// for one thread, which is what lets <see cref="LocalStore.InTransaction"/> hold it across
    /// the store calls inside the transaction.
    /// </para>
    /// <para>
    /// This orders threads inside one process and nothing more. WAL still lets the hooks and a
    /// second agent read and write the same file, which is what the tree lock and the
    /// busy timeout are for.
    /// </para>
    /// </remarks>
    public static SqliteCommand Command(this SqliteConnection connection, string sql)
    {
        ArgumentNullException.ThrowIfNull(connection);

        StoreGate.Enter(connection);

        try
        {
            var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Disposed += (_, _) => StoreGate.Leave(connection);
            return command;
        }
        catch
        {
            StoreGate.Leave(connection);
            throw;
        }
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
