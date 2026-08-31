using Microsoft.Data.Sqlite;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;

namespace RomMBat.Core.Store;

/// <summary>A save unit found on disk.</summary>
public sealed record LocalSave
{
    public required RelativePath Path { get; init; }

    /// <summary>
    /// What names this unit inside <see cref="Path"/>, or empty for class A and B.
    /// </summary>
    /// <remarks>
    /// <b>Half the identity of a class C row.</b> The unit is a (container, key) pair, so
    /// <see cref="Path"/> alone does not identify one: every PSP save on an install shares the
    /// container <c>saves/psp/SAVEDATA</c>, and every GameCube save shares one region folder.
    /// Empty for class A and B, whose unit is the file at <see cref="Path"/> itself.
    /// </remarks>
    public string UnitKey { get; init; } = string.Empty;

    public required string System { get; init; }

    public required string Emulator { get; init; }

    public required SaveShapeClass ShapeClass { get; init; }

    public required string Slot { get; init; }

    public long? RomId { get; init; }

    public RelativePath? RomPath { get; init; }

    public string? ContentHash { get; init; }

    public long SizeBytes { get; init; }

    /// <summary>
    /// When the scan that recorded this row ran.
    /// </summary>
    /// <remarks>
    /// The column has always been written and nothing could read it back. It is exposed because
    /// the flush's rule that states are scanned before saves (#64) had no witness outside a
    /// class C fixture: the sidecar attribution route reads <c>local_state</c>, so scanning
    /// saves first leaves it reading an empty table, and the two stamps are what a test compares.
    /// </remarks>
    public DateTimeOffset? ScannedAtUtc { get; init; }


    public DateTimeOffset? FileMtimeUtc { get; init; }

    /// <summary>
    /// What went up last, or null when nothing has.
    /// </summary>
    /// <remarks>
    /// Null means this save has never reached the server, which is the question
    /// <c>SaveGuard</c> exists to ask. It holds the hash that went up rather than a flag, so a
    /// save changed since its upload is distinguishable from one that was never sent.
    /// </remarks>
    public string? UploadedContentHash { get; init; }

    public DateTimeOffset? UploadedAtUtc { get; init; }

    /// <summary>True when nothing about this save has reached the server.</summary>
    public bool IsUnsent => UploadedContentHash is null;

    /// <summary>True when the file has changed since whatever was last uploaded.</summary>
    public bool HasChangedSinceUpload =>
        ContentHash is not null
        && !string.Equals(ContentHash, UploadedContentHash, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// What is under <c>saves/</c>, and whether it has ever gone up.
/// </summary>
/// <remarks>
/// The table that closes the seam M3 left open. Eviction could previously only ask the outbox
/// and the journal, so a save produced while nothing was watching was invisible to it and the
/// gap was covered by never touching a file RomMBat did not download. That mitigation stays,
/// but it is no longer the answer.
/// </remarks>
public sealed class LocalSaveStore
{
    private readonly SqliteConnection _connection;

    internal LocalSaveStore(SqliteConnection connection) => _connection = connection;

    /// <summary>Inserts or updates by path, preserving what is known about the upload.</summary>
    /// <remarks>
    /// A rescan must not forget that a save was uploaded, so <c>uploaded_content_hash</c> is
    /// kept unless the caller supplies one. Forgetting it would make eviction refuse forever
    /// and would re-upload every save on every sync.
    /// </remarks>
    public void Record(LocalSave save, DateTimeOffset scannedAt)
    {
        ArgumentNullException.ThrowIfNull(save);

        using var command = _connection.Command(
            """
            INSERT INTO local_save (relative_path, unit_key, system, emulator, shape_class,
                                    rom_id, rom_relative_path, slot, content_hash, size_bytes,
                                    file_mtime_utc, scanned_at_utc, uploaded_content_hash,
                                    uploaded_at_utc)
            VALUES ($path, $unitKey, $system, $emulator, $class, $romId, $romPath, $slot, $hash,
                    $size, $mtime, $scannedAt, $uploaded, $uploadedAt)
            ON CONFLICT (relative_path, unit_key) DO UPDATE SET
              system                = excluded.system,
              emulator              = excluded.emulator,
              shape_class           = excluded.shape_class,
              rom_id                = excluded.rom_id,
              rom_relative_path     = excluded.rom_relative_path,
              slot                  = excluded.slot,
              content_hash          = excluded.content_hash,
              size_bytes            = excluded.size_bytes,
              file_mtime_utc        = excluded.file_mtime_utc,
              scanned_at_utc        = excluded.scanned_at_utc,
              uploaded_content_hash = COALESCE(excluded.uploaded_content_hash, local_save.uploaded_content_hash),
              uploaded_at_utc       = COALESCE(excluded.uploaded_at_utc, local_save.uploaded_at_utc);
            """)
            .With("$path", save.Path.Value)
            .With("$unitKey", save.UnitKey)
            .With("$system", save.System)
            .With("$emulator", save.Emulator)
            .With("$class", ToText(save.ShapeClass))
            .With("$romId", SqliteValues.OrNull(save.RomId))
            .With("$romPath", save.RomPath.HasValue ? save.RomPath.Value.Value : DBNull.Value)
            .With("$slot", save.Slot)
            .With("$hash", SqliteValues.OrNull(save.ContentHash))
            .With("$size", save.SizeBytes)
            .With("$mtime", SqliteValues.ToTextOrNull(save.FileMtimeUtc))
            .With("$scannedAt", SqliteValues.ToText(scannedAt))
            .With("$uploaded", SqliteValues.OrNull(save.UploadedContentHash))
            .With("$uploadedAt", SqliteValues.ToTextOrNull(save.UploadedAtUtc));

        command.ExecuteNonQuery();
    }

    /// <summary>Records that a save reached the server with the content it then had.</summary>
    /// <remarks>
    /// Keyed on the unit rather than on the path, because a path is shared: marking
    /// <c>saves/psp/SAVEDATA</c> uploaded would mark every PSP game on the install uploaded and
    /// let eviction take every one of their ROMs.
    /// </remarks>
    public void MarkUploaded(RelativePath path, string unitKey, string contentHash, DateTimeOffset now)
    {
        using var command = _connection.Command(
            """
            UPDATE local_save
            SET uploaded_content_hash = $hash, uploaded_at_utc = $now
            WHERE relative_path = $path AND unit_key = $unitKey;
            """)
            .With("$path", path.Value)
            .With("$unitKey", unitKey ?? string.Empty)
            .With("$hash", contentHash)
            .With("$now", SqliteValues.ToText(now));

        command.ExecuteNonQuery();
    }

    /// <summary>Everything found, newest scan first.</summary>
    public IReadOnlyList<LocalSave> List(long? romId = null) => Query(
        """
        SELECT relative_path, system, emulator, shape_class, rom_id, rom_relative_path, slot,
               content_hash, size_bytes, file_mtime_utc, uploaded_content_hash, uploaded_at_utc,
               unit_key, scanned_at_utc
        FROM local_save
        WHERE ($romId IS NULL OR rom_id = $romId)
        ORDER BY relative_path, unit_key;
        """,
        command => command.With("$romId", SqliteValues.OrNull(romId)));

    /// <summary>Forgets saves whose files are gone, so a deleted save stops blocking eviction.</summary>
    /// <remarks>
    /// By unit, not by path. A class C container outlives every unit in it, so deleting by path
    /// would forget every PSP save on the install the first time one game's savedata was
    /// removed, and eviction would then happily take ROMs whose saves had never gone up.
    /// </remarks>
    /// <returns>How many rows were removed.</returns>
    public int Forget(IEnumerable<(RelativePath Path, string UnitKey)> units)
    {
        ArgumentNullException.ThrowIfNull(units);

        var removed = 0;

        foreach (var (path, unitKey) in units)
        {
            using var command = _connection
                .Command("DELETE FROM local_save WHERE relative_path = $path AND unit_key = $unitKey;")
                .With("$path", path.Value)
                .With("$unitKey", unitKey ?? string.Empty);

            removed += command.ExecuteNonQuery();
        }

        return removed;
    }

    private List<LocalSave> Query(string sql, Func<SqliteCommand, SqliteCommand> bind)
    {
        using var command = bind(_connection.Command(sql));
        using var reader = command.ExecuteReader();

        var saves = new List<LocalSave>();
        while (reader.Read())
        {
            var romPathText = reader.GetStringOrNull(5);

            saves.Add(new LocalSave
            {
                Path = RelativePath.Create(reader.GetString(0)),
                System = reader.GetString(1),
                Emulator = reader.GetString(2),
                ShapeClass = ParseClass(reader.GetString(3)),
                RomId = reader.GetInt64OrNull(4),
                RomPath = romPathText is not null && RelativePath.TryCreate(romPathText, out var parsed)
                    ? parsed
                    : null,
                Slot = reader.GetString(6),
                ContentHash = reader.GetStringOrNull(7),
                SizeBytes = reader.GetInt64(8),
                FileMtimeUtc = reader.GetTimestampOrNull(9),
                UploadedContentHash = reader.GetStringOrNull(10),
                UploadedAtUtc = reader.GetTimestampOrNull(11),
                UnitKey = reader.GetString(12),
                ScannedAtUtc = reader.GetTimestampOrNull(13),
            });
        }

        return saves;
    }

    private static string ToText(SaveShapeClass value) => value switch
    {
        SaveShapeClass.A => "A",
        SaveShapeClass.B => "B",
        SaveShapeClass.C => "C",
        SaveShapeClass.D => "D",

        // The CHECK admits four letters and nothing else, so an unknown shape has no row: it
        // is reported through `unsyncable` instead, which is the honest place for it.
        _ => throw new ArgumentOutOfRangeException(
            nameof(value),
            "A save with no known shape is reported as unsyncable rather than recorded."),
    };

    private static SaveShapeClass ParseClass(string value) => value switch
    {
        "A" => SaveShapeClass.A,
        "B" => SaveShapeClass.B,
        "C" => SaveShapeClass.C,
        "D" => SaveShapeClass.D,
        _ => throw new InvalidOperationException($"Unknown save shape class '{value}' in the database."),
    };
}
