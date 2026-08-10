using Microsoft.Data.Sqlite;
using RomMBat.Core.Paths;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The local store: schema, migration, and the invariant that keeps the app portable.
/// </summary>
public class LocalStoreTests
{
    /// <summary>Every table the schema is required to stand up now, later milestones included.</summary>
    private static readonly string[] RequiredTables =
    [
        "device",
        "local_file",
        "sync_set",
        "sync_set_member",
        "platform_map",
        "outbox",
        "journal",
        "game_id_binding",
        "sync_cursor",
        "clock",
    ];

    /// <summary>Every column the "no absolute path" rule has to cover.</summary>
    private static readonly (string Table, string Column)[] PathColumns =
    [
        ("local_file", "relative_path"),
        ("outbox", "relative_path"),
        ("journal", "rom_relative_path"),
        ("game_id_binding", "rom_relative_path"),
    ];

    /// <summary>
    /// Columns that hold a single name and must never be handed a path at all.
    /// </summary>
    /// <remarks>
    /// The neighbouring half of the same rule. These are not relative paths, so
    /// <see cref="RelativePath"/> does not guard them, and a path smuggled into one would
    /// still end up concatenated into a real location later. Each carries a CHECK rejecting
    /// both separators and a drive colon.
    /// </remarks>
    private static readonly (string Table, string Column)[] NameColumns =
    [
        ("sync_set_member", "folder"),
        ("sync_set_member", "fs_name"),
        ("platform_map", "folder"),
    ];

    [Fact]
    public void A_fresh_database_lands_at_the_expected_schema_version()
    {
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());

        Assert.Equal(LocalStore.ExpectedSchemaVersion, store.SchemaVersion);
        Assert.True(File.Exists(store.DatabasePath));
    }

    [Fact]
    public void The_database_lives_inside_the_tree()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        using var store = LocalStore.Open(install);

        Assert.True(install.Contains(store.DatabasePath));
    }

    [Fact]
    public void Every_table_later_milestones_need_exists_now()
    {
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());

        var tables = QueryStrings(store, "SELECT name FROM sqlite_master WHERE type = 'table';");

        foreach (var table in RequiredTables)
        {
            Assert.Contains(table, tables, StringComparer.Ordinal);
        }
    }

    [Fact]
    public void Reopening_is_a_no_op()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        using (var first = LocalStore.Open(install))
        {
            first.Device.EnsureIdentity("11111111-2222-3333-4444-555555555555");
        }

        using var second = LocalStore.Open(install);

        Assert.Equal(LocalStore.ExpectedSchemaVersion, second.SchemaVersion);
        Assert.Equal("11111111-2222-3333-4444-555555555555", second.Device.Read()?.ClientDeviceIdentifier);
    }

    [Fact]
    public void A_database_from_a_newer_build_is_refused_rather_than_downgraded()
    {
        // Real on a portable drive: the stick may have been used with a newer RomMBat on
        // another PC, and an older build must not write to a schema it does not understand.
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        using (var store = LocalStore.Open(install))
        {
            using var command = store.Connection.CreateCommand();
            command.CommandText = $"PRAGMA user_version = {LocalStore.ExpectedSchemaVersion + 5};";
            command.ExecuteNonQuery();
        }

        var exception = Assert.Throws<LocalStoreVersionException>(() => LocalStore.Open(install));

        Assert.Contains("newer build", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(RelativePathTests.Rejected), MemberType = typeof(RelativePathTests))]
    public void The_database_rejects_every_path_the_typed_API_rejects(string value)
    {
        // The two enforcement layers are proven against the same list, so they cannot drift.
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());

        Assert.False(RelativePath.TryCreate(value, out _));

        foreach (var (table, column) in PathColumns)
        {
            var exception = Record.Exception(() => InsertPath(store, table, column, value));

            Assert.True(
                exception is SqliteException,
                $"{table}.{column} accepted '{value}', which the typed API refuses");
        }
    }

    [Fact]
    public void The_database_accepts_an_ordinary_relative_path()
    {
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());

        foreach (var (table, column) in PathColumns)
        {
            InsertPath(store, table, column, "roms/snes/Gradius 2 (Japan, Europe) (En).zip");
        }
    }

    [Theory]
    [InlineData("roms/snes/Game.sfc")]
    [InlineData("roms\\snes\\Game.sfc")]
    [InlineData("C:/roms/snes")]
    [InlineData("/roms/snes")]
    [InlineData("")]
    public void A_name_column_refuses_anything_shaped_like_a_path(string value)
    {
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());

        foreach (var (table, column) in NameColumns)
        {
            var exception = Record.Exception(() => InsertName(store, table, column, value));

            Assert.True(
                exception is SqliteException,
                $"{table}.{column} accepted '{value}', which is a path where a name belongs");
        }
    }

    [Fact]
    public void A_name_column_accepts_an_ordinary_name()
    {
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());

        InsertName(store, "sync_set_member", "folder", "snes");
        InsertName(store, "platform_map", "folder", "megadrive-msu");
        InsertName(store, "sync_set_member", "fs_name", "Gradius 2 (Japan, Europe) (En).zip");
    }

    [Fact]
    public void The_sequence_is_monotonic_and_shared_by_the_outbox_and_the_journal()
    {
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());

        var values = Enumerable.Range(0, 5).Select(_ => store.NextSequence()).ToArray();

        Assert.Equal(values.OrderBy(value => value).ToArray(), values);
        Assert.Equal(values.Distinct().Count(), values.Length);
        Assert.Equal(values[^1], store.CurrentSequence());
    }

    [Fact]
    public void The_sequence_survives_the_store_being_reopened()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        long last;
        using (var first = LocalStore.Open(install))
        {
            first.NextSequence();
            last = first.NextSequence();
        }

        using var second = LocalStore.Open(install);

        Assert.True(second.NextSequence() > last);
    }

    [Fact]
    public void An_outbox_entry_carries_its_real_mtime_alongside_the_sequence()
    {
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());

        var written = DateTimeOffset.UtcNow.AddHours(-6);
        var recorded = DateTimeOffset.UtcNow;

        var sequence = store.Outbox.Enqueue(
            OutboxKind.Save,
            recorded,
            romId: 42,
            slot: "libretro:battery",
            relativePath: RelativePath.Create("saves/snes/libretro/game.srm"),
            contentHash: "d41d8cd98f00b204e9800998ecf8427e",
            sizeBytes: 8192,
            fileMtimeUtc: written);

        var pending = store.Outbox.Pending();
        var entry = Assert.Single(pending);

        Assert.Equal(sequence, entry.LocalSequence);
        Assert.Equal(1, store.Outbox.PendingCount());

        // The real local mtime, never the sync time. A week offline is only a bigger payload
        // as long as the timestamps stay honest.
        Assert.Equal(written.ToUnixTimeSeconds(), entry.FileMtimeUtc!.Value.ToUnixTimeSeconds());
        Assert.NotEqual(entry.FileMtimeUtc, entry.RecordedAtUtc);
    }

    [Fact]
    public void A_failed_attempt_leaves_the_entry_pending_for_a_later_replay()
    {
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());

        store.Outbox.Enqueue(OutboxKind.PlaySession, DateTimeOffset.UtcNow, romId: 7, payload: "{}");

        var entry = store.Outbox.Pending().Single();
        store.Outbox.RecordFailure(entry.Id, "server unreachable", DateTimeOffset.UtcNow);

        var again = store.Outbox.Pending().Single();

        Assert.Equal(1, again.Attempts);
        Assert.Equal(OutboxState.Pending, again.State);
        Assert.Equal("server unreachable", again.LastError);

        store.Outbox.MarkSent(again.Id, DateTimeOffset.UtcNow);

        Assert.Equal(0, store.Outbox.PendingCount());
    }

    [Fact]
    public void The_clock_records_skew_against_the_server_Date_header()
    {
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());

        var serverSaid = new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);
        var deviceSaid = serverSaid.AddMinutes(4);

        var skew = store.Clock.RecordContact(serverSaid, deviceSaid, TimeSpan.FromMilliseconds(40));

        Assert.NotNull(skew);
        Assert.InRange(skew.Value.TotalSeconds, 239, 241);

        var record = store.Clock.Read();

        Assert.True(record.IsSkewSuspicious);
        Assert.Equal(deviceSaid, record.LastContactUtc);
    }

    [Fact]
    public void A_server_that_sends_no_Date_header_still_records_the_contact()
    {
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());

        var skew = store.Clock.RecordContact(null, DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(12));

        Assert.Null(skew);
        Assert.NotNull(store.Clock.Read().LastContactUtc);
    }

    [Fact]
    public void The_future_timestamp_check_tolerates_FAT_rounding()
    {
        // M0 probe 7: FAT32 and exFAT both store mtimes to 2 seconds and round up, so a file
        // written at 08:03:16.097 is stamped 08:03:18.000. Without the tolerance every FAT
        // install would look like it had a broken clock.
        var now = new DateTimeOffset(2026, 8, 9, 8, 3, 16, 97, TimeSpan.Zero);
        var stampedByFat = new DateTimeOffset(2026, 8, 9, 8, 3, 18, 0, TimeSpan.Zero);

        Assert.Equal(TimeSpan.FromSeconds(2), ClockSkew.FilesystemTimestampTolerance);
        Assert.False(ClockSkew.IsImplausiblyInTheFuture(stampedByFat, now));
        Assert.True(ClockSkew.IsImplausiblyInTheFuture(now.AddSeconds(120), now));
    }

    private static void InsertPath(LocalStore store, string table, string column, string value)
    {
        using var command = store.Connection.CreateCommand();

        // Table and column come from a constant in this file, never from input.
        command.CommandText = table switch
        {
            "local_file" =>
                $"INSERT INTO local_file ({column}, folder, file_name) VALUES ($path, 'snes', 'x');",
            "outbox" =>
                $"INSERT INTO outbox (local_sequence, kind, {column}, recorded_at_utc) "
                    + "VALUES ((SELECT COALESCE(MAX(local_sequence), 0) + 1 FROM outbox), 'save', $path, '2026-01-01T00:00:00Z');",
            "journal" =>
                $"INSERT INTO journal (local_sequence, event, {column}, recorded_at_utc) "
                    + "VALUES ((SELECT COALESCE(MAX(local_sequence), 0) + 1 FROM journal), 'game-end', $path, '2026-01-01T00:00:00Z');",
            "game_id_binding" =>
                $"INSERT INTO game_id_binding (system, game_id, {column}, learned_from, learned_at) "
                    + "VALUES ('dreamcast', $path, $path, 'journal', '2026-01-01T00:00:00Z');",
            _ => throw new ArgumentOutOfRangeException(nameof(table)),
        };

        command.Parameters.AddWithValue("$path", value);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Inserts a value into a name column, with the row's other required fields filled in.
    /// </summary>
    /// <remarks>
    /// Each insert gets a fresh rom id, so a rejected value and an accepted one cannot
    /// collide on the primary key and turn a CHECK failure into a uniqueness failure.
    /// </remarks>
    private static void InsertName(LocalStore store, string table, string column, string value)
    {
        using var command = store.Connection.CreateCommand();

        // Table and column both come from a constant in this file, never from input, and each
        // pair gets its own statement so the column under test is never also filled in as a
        // fixed value. SQLite accepts a duplicated column in an INSERT and keeps the first.
        command.CommandText = (table, column) switch
        {
            ("sync_set_member", "folder") =>
                """
                INSERT INTO sync_set (name, scope_kind, scope_value, created_at, updated_at)
                VALUES ('set-' || abs(random()), 'platform', '1', '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z');
                INSERT INTO sync_set_member (sync_set_id, rom_id, platform_slug, fs_name, folder,
                                             display_name, sort_key, resolved_at)
                VALUES (last_insert_rowid(), abs(random()), 'snes', 'Game.sfc', $name,
                        'Game', 'Game', '2026-01-01T00:00:00Z');
                """,
            ("sync_set_member", "fs_name") =>
                """
                INSERT INTO sync_set (name, scope_kind, scope_value, created_at, updated_at)
                VALUES ('set-' || abs(random()), 'platform', '1', '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z');
                INSERT INTO sync_set_member (sync_set_id, rom_id, platform_slug, fs_name, folder,
                                             display_name, sort_key, resolved_at)
                VALUES (last_insert_rowid(), abs(random()), 'snes', $name, 'snes',
                        'Game', 'Game', '2026-01-01T00:00:00Z');
                """,
            ("platform_map", "folder") =>
                """
                INSERT INTO platform_map (romm_fs_slug, romm_platform_slug, folder, resolved_by, updated_at)
                VALUES ('slug-' || abs(random()), 'snes', $name, 'user', '2026-01-01T00:00:00Z');
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(table)),
        };

        command.Parameters.AddWithValue("$name", value);
        command.ExecuteNonQuery();
    }

    private static List<string> QueryStrings(LocalStore store, string sql)
    {
        using var command = store.Connection.CreateCommand();
        command.CommandText = sql;

        using var reader = command.ExecuteReader();
        var values = new List<string>();
        while (reader.Read())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }
}
