using System.Globalization;
using System.Reflection;
using Microsoft.Data.Sqlite;
using RomMBat.Core.Paths;

namespace RomMBat.Core.Store;

/// <summary>
/// The local SQLite database, which is the source of truth for local state.
/// </summary>
/// <remarks>
/// <b>Migration approach.</b> Schema version lives in SQLite's own <c>PRAGMA user_version</c>
/// and the migrations are an append-only list of embedded SQL scripts, applied in order,
/// each inside one transaction that also bumps the version. That makes an interrupted
/// upgrade a no-op rather than a half-applied schema, which matters when the database sits
/// on a USB stick that can be pulled.
/// <para>
/// It is not EF Core: the schema is hand-written SQL with CHECK constraints that carry real
/// invariants, and nothing here needs a change tracker or a design-time toolchain. It is
/// not a migrations table either, because <c>user_version</c> is a single integer in the
/// file header that updates atomically with the transaction that earned it.
/// </para>
/// <para>
/// A database written by a newer RomMBat is refused rather than opened. On a portable drive
/// that is a real case: the stick may have been used with a newer build on another PC.
/// </para>
/// </remarks>
public sealed class LocalStore : IDisposable
{
    /// <summary>How long to wait for another process holding the write lock.</summary>
    /// <remarks>
    /// ES spawns hooks fire-and-forget and they run concurrently; M0 probe 1 observed three
    /// <c>game-end</c> hooks in flight at once. Contention is the normal case here.
    /// </remarks>
    private static readonly TimeSpan BusyTimeout = TimeSpan.FromSeconds(5);

    private readonly SqliteConnection _connection;

    private LocalStore(SqliteConnection connection, string databasePath)
    {
        _connection = connection;
        DatabasePath = databasePath;
        Device = new DeviceStore(connection);
        Clock = new ClockStore(connection);
        Outbox = new OutboxStore(connection, this);
        SyncSets = new SyncSetStore(connection);
        PlatformMap = new PlatformMapStore(connection);
        Cursors = new SyncCursorStore(connection);
    }

    /// <summary>The schema version this build expects.</summary>
    public static int ExpectedSchemaVersion => Migrations.Count;

    /// <summary>Where the database file is, absolute. Never persisted anywhere.</summary>
    public string DatabasePath { get; }

    /// <summary>The schema version actually in the file.</summary>
    public int SchemaVersion => ReadUserVersion(_connection);

    /// <summary>The journal mode SQLite settled on, for <c>status</c>.</summary>
    public string JournalMode { get; private set; } = "unknown";

    public DeviceStore Device { get; }

    public ClockStore Clock { get; }

    public OutboxStore Outbox { get; }

    /// <summary>Sync set definitions and what each one last resolved to.</summary>
    public SyncSetStore SyncSets { get; }

    /// <summary>RomM platform to RetroBat folder, and where each answer came from.</summary>
    public PlatformMapStore PlatformMap { get; }

    /// <summary>Where each endpoint's incremental sync starts, and where a walk stopped.</summary>
    public SyncCursorStore Cursors { get; }

    /// <summary>Opens the store inside a located RetroBat install, creating it if needed.</summary>
    public static LocalStore Open(RetroBatInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);

        install.EnsureAppDirectories();
        return OpenAt(install.DatabasePath);
    }

    /// <summary>
    /// Opens the store at an explicit path. Tests use this; the shipped code goes through
    /// <see cref="Open(RetroBatInstall)"/> so the location stays inside the tree.
    /// </summary>
    public static LocalStore OpenAt(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Default,
            Pooling = false,
            DefaultTimeout = (int)BusyTimeout.TotalSeconds,
        };

        var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();

        var store = new LocalStore(connection, Path.GetFullPath(databasePath));
        store.Configure();
        store.Migrate();
        return store;
    }

    /// <summary>
    /// Takes the next value from the shared monotonic sequence.
    /// </summary>
    /// <remarks>
    /// Shared by the outbox and the journal so entries in the two are orderable against each
    /// other, and independent of the wall clock so ordering survives a flat RTC.
    /// </remarks>
    public long NextSequence()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "UPDATE local_sequence SET value = value + 1 WHERE id = 1 RETURNING value;";
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>The highest sequence handed out so far, without taking one.</summary>
    public long CurrentSequence()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT value FROM local_sequence WHERE id = 1;";
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>Runs an action inside one transaction.</summary>
    public void InTransaction(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        using var transaction = _connection.BeginTransaction();
        action();
        transaction.Commit();
    }

    public void Dispose() => _connection.Dispose();

    /// <summary>For tests that need to reach past the typed API to prove a CHECK fires.</summary>
    internal SqliteConnection Connection => _connection;

    private static IReadOnlyList<Migration> Migrations { get; } = LoadMigrations();

    private static IReadOnlyList<Migration> LoadMigrations()
    {
        var assembly = typeof(LocalStore).Assembly;
        const string prefix = "RomMBat.Core.Store.Migrations.";

        return
        [
            .. assembly.GetManifestResourceNames()
                .Where(name => name.StartsWith(prefix, StringComparison.Ordinal)
                    && name.EndsWith(".sql", StringComparison.Ordinal))
                .OrderBy(name => name, StringComparer.Ordinal)
                .Select((name, index) => new Migration(index + 1, name, ReadResource(assembly, name))),
        ];
    }

    private static string ReadResource(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Migration resource '{name}' is missing from the assembly.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static int ReadUserVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private void Configure()
    {
        Execute($"PRAGMA busy_timeout = {(int)BusyTimeout.TotalMilliseconds};");
        Execute("PRAGMA foreign_keys = ON;");

        // WAL keeps a reader from blocking the concurrent hook writers. It is requested
        // rather than required: SQLite refuses it on some filesystems and answers with the
        // mode it kept, which is fine as long as nothing assumes it got WAL.
        using var command = _connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode = WAL;";
        JournalMode = command.ExecuteScalar() as string ?? "unknown";
    }

    private void Migrate()
    {
        var current = ReadUserVersion(_connection);

        if (current > ExpectedSchemaVersion)
        {
            throw new LocalStoreVersionException(
                $"The database at {DatabasePath} is at schema version {current}, and this RomMBat "
                    + $"only knows version {ExpectedSchemaVersion}. It was written by a newer build. "
                    + "Upgrade RomMBat rather than letting an older one write to it.");
        }

        foreach (var migration in Migrations.Where(m => m.Version > current))
        {
            using var transaction = _connection.BeginTransaction();

            using (var command = _connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = migration.Sql;
                command.ExecuteNonQuery();
            }

            using (var version = _connection.CreateCommand())
            {
                version.Transaction = transaction;

                // PRAGMA takes no parameters, and the value is an int from our own list.
                version.CommandText = $"PRAGMA user_version = {migration.Version};";
                version.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }

    private void Execute(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private sealed record Migration(int Version, string ResourceName, string Sql);
}

/// <summary>Thrown when the database on disk is not one this build can use.</summary>
public sealed class LocalStoreVersionException : Exception
{
    public LocalStoreVersionException(string message)
        : base(message)
    {
    }

    public LocalStoreVersionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public LocalStoreVersionException()
        : base("The local database is not at a schema version this build understands.")
    {
    }
}
