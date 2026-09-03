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
        Journal = new JournalStore(connection, this);
        LaunchCursor = new LaunchCursorStore(connection);
        Saves = new LocalSaveStore(connection);
        States = new LocalStateStore(connection);
        Unsyncable = new UnsyncableStore(connection);
        SaveSlots = new SaveSlotStore(connection);
        SaveConflicts = new SaveConflictStore(connection);
        GameIdBindings = new GameIdBindingStore(connection);
        SaveConversions = new SaveConversionStore(connection);
        PendingConfig = new PendingConfigStore(connection);
        SyncSets = new SyncSetStore(connection);
        PlatformMap = new PlatformMapStore(connection);
        Cursors = new SyncCursorStore(connection);
        Files = new LocalFileStore(connection);
        Downloads = new ContentDownloadStore(connection);
        Settings = new SettingStore(connection);
        Metadata = new MetadataStore(connection);
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

    /// <summary>What the ES hooks append, which is the only thing they do.</summary>
    public JournalStore Journal { get; }

    /// <summary>Where emulatorLauncher.log was last read to, across its rotation.</summary>
    public LaunchCursorStore LaunchCursor { get; }

    /// <summary>What is under saves/, and whether each has ever reached the server.</summary>
    public LocalSaveStore Saves { get; }

    /// <summary>Save states, which are tracked locally because they never negotiate.</summary>
    public LocalStateStore States { get; }

    /// <summary>What RomMBat found under saves/ and is not syncing, with the reason.</summary>
    public UnsyncableStore Unsyncable { get; }

    /// <summary>The server-side identity of each (rom_id, slot).</summary>
    public SaveSlotStore SaveSlots { get; }

    /// <summary>Slots where both sides moved, which outlive the flush that found them.</summary>
    public SaveConflictStore SaveConflicts { get; }

    /// <summary>Learned Game ID to ROM bindings, and the keys nothing could bind.</summary>
    public GameIdBindingStore GameIdBindings { get; }

    /// <summary>Games opted into a per-game save container, and what the setting was before.</summary>
    public SaveConversionStore SaveConversions { get; }

    /// <summary>Configuration changes waiting for EmulationStation to be gone, and how they went.</summary>
    public PendingConfigStore PendingConfig { get; }

    /// <summary>Sync set definitions and what each one last resolved to.</summary>
    public SyncSetStore SyncSets { get; }

    /// <summary>RomM platform to RetroBat folder, and where each answer came from.</summary>
    public PlatformMapStore PlatformMap { get; }

    /// <summary>Where each endpoint's incremental sync starts, and where a walk stopped.</summary>
    public SyncCursorStore Cursors { get; }

    /// <summary>What is on disk, which is what makes a second sync a no-op.</summary>
    public LocalFileStore Files { get; }

    /// <summary>Downloads that started and have not finished.</summary>
    public ContentDownloadStore Downloads { get; }

    /// <summary>Install-wide settings, including the global disk budget.</summary>
    public SettingStore Settings { get; }

    /// <summary>What a gamelist is written from when the server is unreachable.</summary>
    public MetadataStore Metadata { get; }

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
        using var command = _connection.Command(
            "UPDATE local_sequence SET value = value + 1 WHERE id = 1 RETURNING value;");
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>The highest sequence handed out so far, without taking one.</summary>
    public long CurrentSequence()
    {
        using var command = _connection.Command("SELECT value FROM local_sequence WHERE id = 1;");
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Runs an action inside one transaction.
    /// </summary>
    /// <remarks>
    /// <b>The gate is taken for the whole transaction, not left to the store calls inside it.</b>
    /// <c>BeginTransaction</c> and <c>Commit</c> issue their own <c>BEGIN</c> and <c>COMMIT</c>
    /// through the connection directly, so a transaction that only relied on its inner calls to
    /// gate themselves would drop the gate between statements: another thread could create and
    /// dispose a command on the same connection mid-transaction, which is exactly the
    /// unserialised prepared-statement-list mutation <see cref="StoreGate"/> exists to stop, and
    /// its reads would land inside an open transaction and see uncommitted rows.
    /// <see cref="StoreGate"/> is re-entrant, which is what lets the inner calls take it again.
    /// </remarks>
    public void InTransaction(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        StoreGate.Enter(_connection);

        try
        {
            using var transaction = _connection.BeginTransaction();
            action();
            transaction.Commit();
        }
        finally
        {
            StoreGate.Leave(_connection);
        }
    }

    /// <summary>Closes the connection, blocking until no other thread is inside it.</summary>
    /// <remarks>
    /// <b>Disposal is ordered by <see cref="StoreGate"/> like every other use of the connection,
    /// and for a while it was the one path that was not.</b> A screen's loader is cancelled when
    /// the screen is disposed and is not waited for, so it can still be mid-read when the session
    /// closes; <c>SqliteConnection.Close</c> then enumerated its prepared-statement list while
    /// that reader mutated it and threw out of here. <b>The symptom varies</b>, so do not match
    /// on one string: "Collection was modified" from the enumeration itself, or an
    /// <c>ObjectDisposedException</c> naming <c>SQLitePCL.sqlite3_stmt</c> when the reader's
    /// statement is torn down first. Reproducing this six times gave four of the first and two
    /// of the second.
    /// <para>
    /// <b>The wait is bounded by the longest gated hold, and no I/O happens inside one.</b> That
    /// hold is the transaction in <c>ContentSync.Commit</c>, which moves the file before it
    /// opens the transaction, so the bound is a few statements plus the five-second busy timeout
    /// a single command can sit on under hook contention. This runs on the UI thread at
    /// shutdown, so the worst case is a stall of that order in place of a crash.
    /// </para>
    /// </remarks>
    public void Dispose()
    {
        StoreGate.EnterForClose(_connection);

        try
        {
            _connection.Dispose();
        }
        finally
        {
            StoreGate.LeaveAfterClose(_connection);
        }
    }

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

        var pending = Migrations.Where(m => m.Version > current).ToList();
        if (pending.Count == 0)
        {
            return;
        }

        // SQLite cannot add a CHECK to an existing column, so a migration that tightens one
        // rebuilds the table. Dropping a parent with foreign keys on runs an implicit DELETE
        // that cascades into its children, which would take the membership with it, and the
        // pragma is a no-op inside a transaction. Off for the run, and every migration is
        // checked before its transaction commits.
        Execute("PRAGMA foreign_keys = OFF;");

        try
        {
            foreach (var migration in pending)
            {
                using var transaction = _connection.BeginTransaction();

                using (var command = _connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = migration.Sql;
                    command.ExecuteNonQuery();
                }

                EnsureReferencesIntact(transaction, migration);

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
        finally
        {
            Execute("PRAGMA foreign_keys = ON;");
        }
    }

    /// <summary>Refuses to commit a migration that left a row pointing at a parent that is gone.</summary>
    private void EnsureReferencesIntact(SqliteTransaction transaction, Migration migration)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "PRAGMA foreign_key_check;";

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            throw new LocalStoreVersionException(
                $"Migration {migration.Version} left orphaned rows in '{reader.GetString(0)}'. The "
                    + "database was not changed.");
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
