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
        "content_download",
        "setting",
        "rom_metadata",
        "local_save",
        "save_slot",
        "launch_cursor",
        "unsyncable",
        "local_state",
        "save_conflict",
    ];

    /// <summary>
    /// Every column the "no absolute path" rule has to cover, with a value it must accept.
    /// </summary>
    /// <remarks>
    /// The accepted value is per column rather than shared because two columns carry a second
    /// CHECK pinning them to one subtree, the way 005 pinned firmware under <c>bios/</c>: a
    /// <c>local_save</c> row has to be under <c>saves/</c>, so a ROM path is correctly refused
    /// there and would make a shared example prove the wrong thing.
    /// </remarks>
    private static readonly (string Table, string Column, string Accepted)[] PathColumns =
    [
        ("local_file", "relative_path", "roms/snes/Gradius 2 (Japan, Europe) (En).zip"),
        ("outbox", "relative_path", "saves/snes/ActRaiser (USA).srm"),
        ("journal", "rom_relative_path", "roms/snes/Gradius 2 (Japan, Europe) (En).zip"),
        ("game_id_binding", "rom_relative_path", "roms/dreamcast/Bangai-O (USA).chd"),
        ("content_download", "part_path", "emulators/rommbat/partial/1.part"),
        ("content_download", "target_path", "roms/snes/Gradius 2 (Japan, Europe) (En).zip"),
        ("local_save", "relative_path", "saves/saturn/Battle Garegga (Japan).bcr"),
        ("local_save", "rom_relative_path", "roms/saturn/Battle Garegga (Japan).chd"),
        ("local_state", "relative_path", "saves/snes/libretro.snes9x/ActRaiser (USA).state1"),
        ("local_state", "rom_relative_path", "roms/snes/ActRaiser (USA).zip"),
        ("local_state", "screenshot_path", "saves/snes/libretro.snes9x/ActRaiser (USA).state1.png"),
        ("save_conflict", "local_path", "saves/snes/ActRaiser (USA).srm"),
        ("save_conflict", "local_copy_path", "emulators/rommbat/replaced/20260817T120000-ActRaiser (USA).srm"),
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
        ("sync_set", "folder_override"),
        ("local_file", "folder"),
        ("local_file", "file_name"),
        ("rom_metadata", "folder"),
        ("rom_metadata", "fs_name"),
        ("local_save", "system"),
        ("local_save", "emulator"),
        ("game_id_binding", "system"),
        ("game_id_binding", "game_id"),
        ("outbox", "emulator"),
        ("unsyncable", "system"),
        ("local_state", "system"),
        ("local_state", "emulator"),
        ("save_conversion", "system"),
        ("save_conversion", "fs_name"),
        ("pending_config", "system"),
        ("pending_config", "fs_name"),
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

        foreach (var (table, column, _) in PathColumns)
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

        foreach (var (table, column, accepted) in PathColumns)
        {
            InsertPath(store, table, column, accepted);
        }
    }

    [Theory]
    [InlineData("UCES01011/nested")]
    [InlineData("UCES01011\\nested")]
    [InlineData("C:UCES01011")]
    [InlineData("UCES01011\nsecond line")]
    public void A_unit_key_is_refused_anything_shaped_like_a_path(string value)
    {
        // A unit key is one segment read off a directory or a filename (UCES01011, 1944, GXBE,
        // RSBE), and it is concatenated into a real location when a unit is restored. It is not
        // in the shared name-column table for one reason: the empty string is legal here and
        // nowhere else, because class A and B rows carry it to mean "the unit is the file at
        // relative_path". That is also why the column is NOT NULL DEFAULT '' rather than
        // nullable, since SQLite treats NULLs as distinct in the UNIQUE index it takes part in.
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());

        Assert.Throws<SqliteException>(() => InsertUnitKey(store, value));
    }

    [Fact]
    public void A_unit_key_may_be_empty_because_that_is_what_a_class_A_row_carries()
    {
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());

        InsertUnitKey(store, string.Empty);
        InsertUnitKey(store, "UCES01011");
    }

    [Fact]
    public void One_container_holds_many_units_and_one_unit_only_once()
    {
        // The whole point of migration 008. Every PSP save on an install shares the container
        // saves/psp/SAVEDATA, so the identity is the pair, and a repeat of the pair is the
        // rescan case that must update rather than duplicate.
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());

        InsertUnitKey(store, "UCES01011");
        InsertUnitKey(store, "ULES01513");

        Assert.Throws<SqliteException>(() => InsertUnitKey(store, "UCES01011"));
    }

    private static void InsertUnitKey(LocalStore store, string value)
    {
        using var command = store.Connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO local_save (relative_path, unit_key, system, emulator, shape_class,
                                    slot, scanned_at_utc)
            VALUES ('saves/psp/SAVEDATA', $key, 'psp', 'ppsspp', 'C',
                    'ppsspp:savedata', '2026-01-01T00:00:00Z');
            """;

        command.Parameters.AddWithValue("$key", value);
        command.ExecuteNonQuery();
    }

    [Theory]
    [InlineData("roms/snes/Game.sfc")]
    [InlineData("emulators/rommbat/rommbat.db")]
    [InlineData("bios/scph5501.bin")]
    public void A_save_row_is_refused_a_path_outside_the_saves_tree(string value)
    {
        // The same discipline 005 landed for firmware under bios/. A shape definition that
        // named the wrong directory would otherwise have RomMBat treating a ROM as a save and,
        // worse, restoring over it.
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());

        Assert.True(RelativePath.TryCreate(value, out _));
        Assert.Throws<SqliteException>(() => InsertPath(store, "local_save", "relative_path", value));
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

    [Theory]
    [InlineData("{\"Image\":\"/assets/romm/resources/roms/1/2/cover/big.png\"}")]
    [InlineData("{\"Image\":\"roms\\\\1\\\\2\\\\cover\\\\big.png\"}")]
    public void The_media_path_column_refuses_a_value_that_is_not_resource_relative(string json)
    {
        // media_paths holds server-side resource paths rather than local ones, so
        // RelativePath cannot guard it. The CHECK still holds it to the same shape: no leading
        // slash, no backslash. The prefix is put back at the point of use, which is also the
        // step that stops a prefix-less request answering 200 with the web UI's page.
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());

        using var command = store.Connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO rom_metadata (rom_id, folder, fs_name, name, media_paths, fetched_at)
            VALUES (abs(random()), 'snes', 'Game.sfc', 'Game', $json, '2026-01-01T00:00:00Z');
            """;

        command.Parameters.AddWithValue("$json", json);

        Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
    }

    [Fact]
    public void An_m1_database_upgrades_without_losing_a_row()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        install.EnsureAppDirectories();
        var path = install.DatabasePath;

        // A v1 file with something in every table 002 rebuilds. Two of those rebuilds drop a
        // table another one references, and with foreign keys on that would cascade the
        // membership away rather than carry it across.
        using (var seed = new SqliteConnection($"Data Source={path}"))
        {
            seed.Open();
            Execute(seed, ReadMigration("001-initial.sql"));
            Execute(
                seed,
                """
                INSERT INTO sync_set (id, name, scope_kind, scope_value, created_at, updated_at)
                VALUES (7, 'favourites', 'platform', '1', '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z');

                INSERT INTO sync_set_member (sync_set_id, rom_id, state, resolved_at)
                VALUES (7, 42, 'member', '2026-01-01T00:00:00Z');

                INSERT INTO platform_map (romm_platform_slug, romm_platform_id, folder, resolved_by, updated_at)
                VALUES ('gb', 9, 'gb', 'user', '2026-01-01T00:00:00Z');

                INSERT INTO sync_cursor (endpoint, updated_after) VALUES ('roms', '2026-01-01T00:00:00Z');

                INSERT INTO local_file (relative_path, folder, rom_id, file_name, size_bytes, md5_hash, origin)
                VALUES ('roms/gb/Tetris (World).zip', 'gb', 42, 'Tetris (World).zip', 4105,
                        'fab05f70b7e480d9dee494f65b95ab52', 'adopted');

                INSERT INTO outbox (local_sequence, kind, rom_id, slot, relative_path, content_hash,
                                    size_bytes, file_mtime_utc, recorded_at_utc)
                VALUES (3, 'save', 42, 'libretro:battery:srm', 'saves/gb/Tetris (World).srm',
                        'fab05f70b7e480d9dee494f65b95ab52', 8192, '2026-01-01T00:00:00Z',
                        '2026-01-01T00:00:00Z');

                PRAGMA user_version = 1;
                """);
        }

        using var store = LocalStore.OpenAt(path);

        Assert.Equal(LocalStore.ExpectedSchemaVersion, store.SchemaVersion);

        var set = Assert.Single(store.SyncSets.List());
        Assert.Equal("favourites", set.Name);
        Assert.Null(set.FolderOverride);

        var member = Assert.Single(store.SyncSets.Members(set.Id));
        Assert.Equal(42, member.RomId);

        // platform_map is rekeyed onto fs_slug, which is the identifier RomM keeps unique.
        Assert.Equal(["gb"], QueryStrings(store, "SELECT romm_fs_slug FROM platform_map;"));
        Assert.Equal(["roms"], QueryStrings(store, "SELECT endpoint FROM sync_cursor;"));

        // 003 rebuilds local_file to put a CHECK on its two name columns, so its rows are
        // copied like everything else rather than assumed to be absent.
        var file = Assert.Single(store.Files.List());
        Assert.Equal(42, file.RomId);
        Assert.Equal("fab05f70b7e480d9dee494f65b95ab52", file.Md5Hash);
        Assert.Equal(FileOrigin.Adopted, file.Origin);
        Assert.Equal(HashScope.File, file.HashScope);

        // 006 rebuilds outbox to put a CHECK on the emulator column it adds, so a queued save
        // written by an earlier build has to survive the copy with its sequence intact. Losing
        // one here is losing someone's save.
        var queued = Assert.Single(store.Outbox.Pending());
        Assert.Equal(3, queued.LocalSequence);
        Assert.Equal(42, queued.RomId);
        Assert.Equal("libretro:battery:srm", queued.Slot);
        Assert.Equal("saves/gb/Tetris (World).srm", queued.RelativePath?.Value);
        Assert.Null(queued.Emulator);
        Assert.Null(queued.BatchKey);
    }

    [Fact]
    public void A_name_column_accepts_an_ordinary_name()
    {
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());

        InsertName(store, "sync_set_member", "folder", "snes");
        InsertName(store, "platform_map", "folder", "megadrive-msu");
        InsertName(store, "sync_set_member", "fs_name", "Gradius 2 (Japan, Europe) (En).zip");
        InsertName(store, "sync_set", "folder_override", "fbneo");
        InsertName(store, "local_file", "folder", "megadrive");
        InsertName(store, "local_file", "file_name", "Gradius 2 (Japan, Europe) (En).zip");
        InsertName(store, "local_save", "system", "megacd");
        InsertName(store, "local_save", "emulator", "libretro.genesis_plus_gx");
        InsertName(store, "outbox", "emulator", "libretro");
        InsertName(store, "unsyncable", "system", "ps3");
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

    [Fact]
    public void A_recorded_file_is_found_by_its_content_hash_whatever_case_it_was_written_in()
    {
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());

        store.Files.Record(new LocalFile
        {
            Path = RelativePath.Create("roms/nes/That's Whack.zip"),
            Folder = "nes",
            RomId = 224439,
            FileName = "That's Whack.zip",
            SizeBytes = 1025,

            // Upper case on purpose: RomM lower-cases its hashes and nothing guarantees the
            // next writer will, so the store normalises rather than trusting the caller.
            Md5Hash = "DD768E2EECC95EB27E8CAE274570E04C",
            HashScope = HashScope.ArchiveContent,
            VerifiedBy = VerifiedBy.Md5,
            Origin = FileOrigin.Adopted,
        });

        var found = Assert.Single(store.Files.ByMd5("dd768e2eecc95eb27e8cae274570e04c"));

        Assert.Equal(224439, found.RomId);
        Assert.Equal(HashScope.ArchiveContent, found.HashScope);
        Assert.Equal(VerifiedBy.Md5, found.VerifiedBy);
        Assert.Equal((1, 1025L), store.Files.Totals("nes"));
    }

    [Fact]
    public void Restarting_a_download_keeps_the_attempt_count_rather_than_racing_the_first()
    {
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());

        var download = new ContentDownload
        {
            RomId = 7,
            PartPath = RelativePath.Create("emulators/rommbat/partial/7.part"),
            TargetPath = RelativePath.Create("roms/snes/Game.sfc"),
            ExpectedSize = 4105,
            Validator = "\"6a45147a-1009\"",
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        store.Downloads.Begin(download);
        store.Downloads.Fail(7, "the drive was removed", DateTimeOffset.UtcNow);
        var resumed = store.Downloads.Begin(download with { Validator = "\"6a45147a-2000\"" });

        Assert.Equal(2, resumed.Attempts);
        Assert.Equal("\"6a45147a-2000\"", resumed.Validator);
        Assert.Single(store.Downloads.List());

        store.Downloads.Remove(7);

        Assert.Empty(store.Downloads.List());
    }

    [Fact]
    public void A_setting_round_trips_and_a_null_removes_it()
    {
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());
        var now = DateTimeOffset.UtcNow;

        store.Settings.Set(SettingStore.ContentMaxBytes, 64L * 1024 * 1024 * 1024, now);

        Assert.Equal(64L * 1024 * 1024 * 1024, store.Settings.GetInt64(SettingStore.ContentMaxBytes));

        store.Settings.Set(SettingStore.ContentMaxBytes, (string?)null, now);

        Assert.Null(store.Settings.GetInt64(SettingStore.ContentMaxBytes));
        Assert.Empty(store.Settings.All());
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
                    + "VALUES ('dreamcast', 'id-' || abs(random()), $path, 'journal', '2026-01-01T00:00:00Z');",

            // The other path column carries a valid value for the same reason content_download
            // does, and relative_path is UNIQUE, so the fixed one is randomised.
            "local_save" => column == "relative_path"
                ? "INSERT INTO local_save (relative_path, system, emulator, shape_class, slot, scanned_at_utc) "
                    + "VALUES ($path, 'saturn', 'libretro', 'A', 'libretro:battery:bcr', '2026-01-01T00:00:00Z');"
                : "INSERT INTO local_save (relative_path, system, emulator, shape_class, slot, "
                    + "rom_relative_path, scanned_at_utc) "
                    + "VALUES ('saves/saturn/' || abs(random()) || '.bcr', 'saturn', 'libretro', 'A', "
                    + "'libretro:battery:bcr', $path, '2026-01-01T00:00:00Z');",

            // Three path columns on one table, so the two not under test carry valid values and
            // relative_path is randomised where it is not the one being probed.
            "local_state" => column switch
            {
                "relative_path" =>
                    "INSERT INTO local_state (relative_path, system, emulator, slot, scanned_at_utc) "
                        + "VALUES ($path, 'snes', 'libretro', 'libretro:snes9x:1', '2026-01-01T00:00:00Z');",
                "rom_relative_path" =>
                    "INSERT INTO local_state (relative_path, system, emulator, slot, "
                        + "rom_relative_path, scanned_at_utc) "
                        + "VALUES ('saves/snes/libretro.snes9x/' || abs(random()) || '.state1', 'snes', "
                        + "'libretro', 'libretro:snes9x:1', $path, '2026-01-01T00:00:00Z');",
                _ =>
                    "INSERT INTO local_state (relative_path, system, emulator, slot, "
                        + "screenshot_path, scanned_at_utc) "
                        + "VALUES ('saves/snes/libretro.snes9x/' || abs(random()) || '.state1', 'snes', "
                        + "'libretro', 'libretro:snes9x:1', $path, '2026-01-01T00:00:00Z');",
            },

            // local_copy_path points into emulators/rommbat/replaced/ rather than under saves/,
            // so unlike local_state's columns it carries no subtree CHECK.
            "save_conflict" => column == "local_path"
                ? "INSERT INTO save_conflict (rom_id, slot, local_path, first_seen_at_utc, last_seen_at_utc) "
                    + "VALUES (abs(random()), 'libretro:battery', $path, '2026-01-01T00:00:00Z', "
                    + "'2026-01-01T00:00:00Z');"
                : "INSERT INTO save_conflict (rom_id, slot, local_path, local_copy_path, "
                    + "first_seen_at_utc, last_seen_at_utc) "
                    + "VALUES (abs(random()), 'libretro:battery', 'saves/snes/Game.srm', $path, "
                    + "'2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z');",

            // The other path column has to carry a valid value, or a rejected insert could not
            // be attributed to the column under test.
            "content_download" => column == "part_path"
                ? "INSERT INTO content_download (rom_id, part_path, target_path, started_at, updated_at) "
                    + "VALUES (abs(random()), $path, 'roms/snes/Game.sfc', '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z');"
                : "INSERT INTO content_download (rom_id, part_path, target_path, started_at, updated_at) "
                    + "VALUES (abs(random()), 'emulators/rommbat/partial/' || abs(random()) || '.part', $path, "
                    + "'2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z');",
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
            ("sync_set", "folder_override") =>
                """
                INSERT INTO sync_set (name, scope_kind, scope_value, folder_override, created_at, updated_at)
                VALUES ('set-' || abs(random()), 'platform', '1', $name,
                        '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z');
                """,
            ("local_file", "folder") =>
                """
                INSERT INTO local_file (relative_path, folder, file_name)
                VALUES ('roms/snes/' || abs(random()) || '.sfc', $name, 'Game.sfc');
                """,
            ("local_file", "file_name") =>
                """
                INSERT INTO local_file (relative_path, folder, file_name)
                VALUES ('roms/snes/' || abs(random()) || '.sfc', 'snes', $name);
                """,
            ("rom_metadata", "folder") =>
                """
                INSERT INTO rom_metadata (rom_id, folder, fs_name, name, fetched_at)
                VALUES (abs(random()), $name, 'Game.sfc', 'Game', '2026-01-01T00:00:00Z');
                """,
            ("rom_metadata", "fs_name") =>
                """
                INSERT INTO rom_metadata (rom_id, folder, fs_name, name, fetched_at)
                VALUES (abs(random()), 'snes', $name, 'Game', '2026-01-01T00:00:00Z');
                """,
            ("local_save", "system") =>
                """
                INSERT INTO local_save (relative_path, system, emulator, shape_class, slot, scanned_at_utc)
                VALUES ('saves/snes/' || abs(random()) || '.srm', $name, 'libretro', 'A',
                        'libretro:battery:srm', '2026-01-01T00:00:00Z');
                """,
            ("local_save", "emulator") =>
                """
                INSERT INTO local_save (relative_path, system, emulator, shape_class, slot, scanned_at_utc)
                VALUES ('saves/snes/' || abs(random()) || '.srm', 'snes', $name, 'A',
                        'libretro:battery:srm', '2026-01-01T00:00:00Z');
                """,

            ("game_id_binding", "system") =>
                """
                INSERT INTO game_id_binding (system, game_id, learned_from, learned_at)
                VALUES ($name, 'ID-' || abs(random()), 'journal', '2026-01-01T00:00:00Z');
                """,

            // The Game ID reaches a path on two routes: it is the unit key a container is
            // joined to, and it is what a report names back to the user.
            ("game_id_binding", "game_id") =>
                """
                INSERT INTO game_id_binding (system, game_id, learned_from, learned_at)
                VALUES ('sys-' || abs(random()), $name, 'journal', '2026-01-01T00:00:00Z');
                """,

            // Whatever RomMBat sends as `emulator` becomes a directory segment in the stored
            // save's server-side file_path, so it is a name and never a path.
            ("outbox", "emulator") =>
                """
                INSERT INTO outbox (local_sequence, kind, emulator, recorded_at_utc)
                VALUES ((SELECT COALESCE(MAX(local_sequence), 0) + 1 FROM outbox), 'save', $name,
                        '2026-01-01T00:00:00Z');
                """,
            ("unsyncable", "system") =>
                """
                INSERT INTO unsyncable (system, emulator, reason_kind, detail, observed_at_utc)
                VALUES ($name, 'rpcs3', 'not_in_this_version', 'directory saves land in stage 2',
                        '2026-01-01T00:00:00Z');
                """,
            ("local_state", "system") =>
                """
                INSERT INTO local_state (relative_path, system, emulator, slot, scanned_at_utc)
                VALUES ('saves/snes/libretro.snes9x/' || abs(random()) || '.state1', $name,
                        'libretro', 'libretro:snes9x:1', '2026-01-01T00:00:00Z');
                """,

            // Measured live: the server writes `emulator` into the stored state's file_path as
            // a directory segment and does not sanitise it, so a value carrying a separator
            // becomes two segments there. The client refuses it before that can happen.
            ("local_state", "emulator") =>
                """
                INSERT INTO local_state (relative_path, system, emulator, slot, scanned_at_utc)
                VALUES ('saves/snes/libretro.snes9x/' || abs(random()) || '.state1', 'snes',
                        $name, 'libretro:snes9x:1', '2026-01-01T00:00:00Z');
                """,
            // fs_name here is the rom filename the per-game es_settings.cfg key is built
            // from, and it is concatenated straight into `<system>["<fs_name>"].<key>`. A
            // separator in either half would write a key naming somewhere else entirely.
            ("save_conversion", "system") =>
                """
                INSERT INTO save_conversion (rom_id, system, fs_name, setting_key, applied_value,
                                             prior_state, converted_at_utc)
                VALUES (abs(random()), $name, 'Game.chd', 'pcsx2_slot1_memory', 'game',
                        'absent', '2026-01-01T00:00:00Z');
                """,
            ("save_conversion", "fs_name") =>
                """
                INSERT INTO save_conversion (rom_id, system, fs_name, setting_key, applied_value,
                                             prior_state, converted_at_utc)
                VALUES (abs(random()), 'sys-' || abs(random()), $name, 'pcsx2_slot1_memory',
                        'game', 'absent', '2026-01-01T00:00:00Z');
                """,
            ("pending_config", "system") =>
                """
                INSERT INTO pending_config (rom_id, system, fs_name, setting_key, desired_state,
                                            desired_value, reason, queued_at_utc)
                VALUES (abs(random()), $name, 'Game.chd', 'pcsx2_slot1_memory', 'set',
                        'game', 'queued by a test', '2026-01-01T00:00:00Z');
                """,
            ("pending_config", "fs_name") =>
                """
                INSERT INTO pending_config (rom_id, system, fs_name, setting_key, desired_state,
                                            desired_value, reason, queued_at_utc)
                VALUES (abs(random()), 'sys-' || abs(random()), $name, 'pcsx2_slot1_memory',
                        'set', 'game', 'queued by a test', '2026-01-01T00:00:00Z');
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(table)),
        };

        command.Parameters.AddWithValue("$name", value);
        command.ExecuteNonQuery();
    }

    private static string ReadMigration(string fileName)
    {
        var assembly = typeof(LocalStore).Assembly;
        var name = $"RomMBat.Core.Store.Migrations.{fileName}";

        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Migration resource '{name}' is missing from the assembly.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
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

    [Fact]
    public async Task A_second_thread_cannot_use_the_connection_while_a_command_is_open()
    {
        // One SqliteConnection is shared by every store class and it is not thread-safe.
        // Nothing serialised it until M7 stage 7b-2b, and the failure is not a clean exception:
        // two threads mutating one connection's prepared-statement list threw "Collection was
        // modified" out of SqliteCommand.Dispose during a full test run.
        //
        // The stage is what made it reachable. Before it the only background work touching the
        // store was a resolve; a sync writes from a background thread for minutes, once per ROM
        // and once per artwork file, while the drawing thread reads the same connection on every
        // redraw to build the screen underneath.
        //
        // **This asserts the gate, not the race, and that is deliberate.** A test that simply
        // hammers the store from several threads was written first and thrown away: with the
        // gate removed it caught the corruption in about one run of three at one load and in
        // none of six at another, which is a test that would have blessed a broken gate most of
        // the time. Mutual exclusion is the property the gate actually promises, and it can be
        // asserted exactly.
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());

        var token = TestContext.Current.CancellationToken;
        var connection = store.Connection;
        using var opened = new SemaphoreSlim(0);
        using var release = new ManualResetEventSlim(false);

        // Synchronous, and on its own thread, because the gate is a Monitor and a Monitor
        // belongs to the thread that took it. An `await` between opening a command and
        // disposing it can resume on a different pool thread, and the release then throws
        // rather than letting go. Nothing in the store does that, because every store method is
        // synchronous, but a test that did was flaky until this was understood.
        var holder = Task.Factory.StartNew(
            () =>
            {
                using var held = connection.Command("SELECT 1;");
                opened.Release();
                release.Wait(TimeSpan.FromSeconds(10), token);
            },
            token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        Assert.True(await opened.WaitAsync(TimeSpan.FromSeconds(10), token), "the first command was never opened");

        var second = Task.Run(
            () =>
            {
                using var blocked = connection.Command("SELECT 2;");
            },
            token);

        // The second thread must not get in while the first still holds an open command.
        var raced = await Task.WhenAny(second, Task.Delay(TimeSpan.FromMilliseconds(500), token));

        Assert.NotSame(second, raced);

        release.Set();

        // And it must get in once the first is disposed, or the gate is a deadlock rather than
        // a guard. Monitor is re-entrant, so nothing single-threaded would ever notice a gate
        // that is taken and never released.
        await holder.WaitAsync(TimeSpan.FromSeconds(10), token);
        await second.WaitAsync(TimeSpan.FromSeconds(10), token);
    }

    [Fact]
    public async Task A_transaction_holds_the_gate_for_the_whole_transaction()
    {
        // The gate above is taken per command, and a transaction is not a command. BeginTransaction
        // and Commit issue their own BEGIN and COMMIT through the connection directly, so a
        // transaction that relied on the store calls inside it to gate themselves would drop the
        // gate between every statement.
        //
        // That is the path the gate was added for. ContentSync.Commit writes every ROM through
        // InTransaction, on the sync thread, while the drawing thread reads the same connection to
        // build the screen underneath, and a read landing inside an open transaction also sees
        // rows that have not committed.
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());

        var token = TestContext.Current.CancellationToken;
        var connection = store.Connection;
        using var inside = new SemaphoreSlim(0);
        using var release = new ManualResetEventSlim(false);

        // On its own thread, for the reason the test above records: a Monitor belongs to the
        // thread that took it.
        var holder = Task.Factory.StartNew(
            () => store.InTransaction(() =>
            {
                inside.Release();
                release.Wait(TimeSpan.FromSeconds(10), token);
            }),
            token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        Assert.True(await inside.WaitAsync(TimeSpan.FromSeconds(10), token), "the transaction never opened");

        var second = Task.Run(
            () =>
            {
                using var blocked = connection.Command("SELECT 1;");
            },
            token);

        var raced = await Task.WhenAny(second, Task.Delay(TimeSpan.FromMilliseconds(500), token));

        Assert.NotSame(second, raced);

        release.Set();

        await holder.WaitAsync(TimeSpan.FromSeconds(10), token);
        await second.WaitAsync(TimeSpan.FromSeconds(10), token);
    }

    [Fact]
    public void The_store_takes_concurrent_use_without_falling_over()
    {
        // A smoke test rather than the guard above: it exercises the shape the sync screen
        // actually creates, a background writer against a foreground reader, and would notice a
        // gate that serialised nothing at all. It is not evidence on its own, for the reason
        // recorded above.
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());

        var problems = new System.Collections.Concurrent.ConcurrentBag<string>();

        Parallel.For(0, 8, worker =>
        {
            try
            {
                for (var i = 0; i < 60; i++)
                {
                    store.Files.Record(new LocalFile
                    {
                        Path = RelativePath.Create($"roms/snes/w{worker}-{i}.sfc"),
                        Folder = "snes",
                        RomId = (worker * 1000) + i,
                        Kind = LocalFileKind.Rom,
                        FileName = $"w{worker}-{i}.sfc",
                        SizeBytes = i,
                    });

                    _ = store.Files.List();
                }
            }
            catch (Exception ex)
            {
                problems.Add(ex.Message);
            }
        });

        Assert.Empty(problems);
        Assert.Equal(8 * 60, store.Files.List().Count);
    }

    /// <summary>
    /// Closing the connection waits for a reader on another thread instead of racing it.
    /// </summary>
    /// <remarks>
    /// Every command serialises through <c>StoreGate</c> and disposal did not, so
    /// <c>SqliteConnection.Close</c> walked its prepared-statement list while a background reader
    /// was still mutating it and threw "Collection was modified" out of <c>Dispose</c>. It
    /// surfaced as the screen sweeps failing only when both test projects ran together, because a
    /// screen's loader is cancelled on dispose and not waited for, so under load it is still
    /// running when the session closes.
    /// </remarks>
    [Fact]
    public async Task Disposing_the_store_under_a_running_reader_does_not_throw()
    {
        var token = TestContext.Current.CancellationToken;

        // Repeated because it is a race: one pass proves nothing, and pre-fix this reproduced
        // well inside this count.
        for (var attempt = 0; attempt < 40; attempt++)
        {
            using var tree = TempRetroBatTree.Create();
            var store = LocalStore.Open(tree.Install());

            using var reading = new CancellationTokenSource();

            var reader = Task.Run(
                () =>
                {
                    while (!reading.IsCancellationRequested)
                    {
                        try
                        {
                            _ = store.Metadata.Count();
                        }
                        catch (Exception)
                        {
                            // A closed connection is the ordinary end of this loop. What must
                            // not happen is the close itself throwing, which is what fails here.
                            return;
                        }
                    }
                },
                token);

            // Long enough for the reader to be inside a command rather than starting one.
            await Task.Delay(2, token);

            store.Dispose();

            await reading.CancelAsync();
            await reader.WaitAsync(TimeSpan.FromSeconds(10), token);
        }
    }
}
