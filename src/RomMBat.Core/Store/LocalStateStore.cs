using Microsoft.Data.Sqlite;
using RomMBat.Core.Paths;

namespace RomMBat.Core.Store;

/// <summary>A save state found on disk.</summary>
/// <param name="Slot">
/// <c>{emulator}:{core}:{slot}</c>, with <c>auto</c> for an autosave and an empty middle where
/// the emulator is not core-scoped. <b>Local only.</b> <c>POST /api/states</c> has no slot
/// field, so this never goes on the wire; it is what pairs a rescan with the row it already has.
/// </param>
/// <param name="NativeName">
/// The content of the <c>.txt</c> sidecar RetroBat writes beside a state, which is the
/// emulator's own name for the game. Its presence signals nothing, since some hold only the ROM
/// filename, but where it holds a serial (<c>SLUS-00404</c>, <c>UCES00995_1.00</c>,
/// <c>GW7E69</c>) it is the Game ID that class C and D attribution otherwise reads out of a ROM.
/// </param>
/// <param name="UploadedFileName">
/// The name this device sent, which is not the name on disk. It carries the emulator and core,
/// because the server keys a state on <c>(rom_id, file_name)</c> alone and two cores writing one
/// filename would otherwise collapse into a single row.
/// </param>
public sealed record LocalState
{
    public required RelativePath Path { get; init; }

    public required string System { get; init; }

    public required string Emulator { get; init; }

    /// <summary>Empty rather than null where the emulator is not core-scoped.</summary>
    public string Core { get; init; } = string.Empty;

    public string? EmulatorVersion { get; init; }

    public string? RetroBatVersion { get; init; }

    public long? RomId { get; init; }

    public RelativePath? RomPath { get; init; }

    public required string Slot { get; init; }

    public RelativePath? ScreenshotPath { get; init; }

    public string? NativeName { get; init; }

    public string? ContentHash { get; init; }

    public long SizeBytes { get; init; }

    public DateTimeOffset? FileMtimeUtc { get; init; }

    public int? StateId { get; init; }

    public string? UploadedFileName { get; init; }

    public string? UploadedContentHash { get; init; }

    public DateTimeOffset? UploadedAtUtc { get; init; }

    /// <summary>True when nothing about this state has reached the server.</summary>
    public bool IsUnsent => UploadedContentHash is null;

    /// <summary>True when the file has changed since whatever was last uploaded.</summary>
    public bool HasChangedSinceUpload =>
        ContentHash is not null
        && !string.Equals(ContentHash, UploadedContentHash, StringComparison.OrdinalIgnoreCase);

    /// <summary>True when this state is worth sending.</summary>
    /// <remarks>
    /// A state with no hash could not be read, which happens while an emulator holds it open,
    /// and sending bytes that could not be hashed would mean uploading something whose
    /// integrity was never checked.
    /// </remarks>
    public bool NeedsUpload =>
        RomId is not null && ContentHash is not null && (IsUnsent || HasChangedSinceUpload);
}

/// <summary>
/// What is under the declared state directories, and whether each has ever gone up.
/// </summary>
/// <remarks>
/// Separate from <c>local_save</c> because a state is not a save shape, is outside the negotiate
/// protocol, and carries an emulator, a core and a version that decide whether it may ever be
/// restored. See migration 007's header.
/// </remarks>
public sealed class LocalStateStore
{
    private readonly SqliteConnection _connection;

    internal LocalStateStore(SqliteConnection connection) => _connection = connection;

    /// <summary>Inserts or updates by path, preserving what is known about the upload.</summary>
    /// <remarks>
    /// A rescan must not forget that a state was uploaded, so the upload columns are kept unless
    /// the caller supplies them. Forgetting would re-send every state on every sync, and the
    /// upsert on the server would quietly accept every one.
    /// </remarks>
    public void Record(LocalState state, DateTimeOffset scannedAt)
    {
        ArgumentNullException.ThrowIfNull(state);

        using var command = _connection.Command(
            """
            INSERT INTO local_state (relative_path, system, emulator, core, emulator_version,
                                     retrobat_version, rom_id, rom_relative_path, slot,
                                     screenshot_path, native_name, content_hash, size_bytes,
                                     file_mtime_utc, scanned_at_utc, state_id,
                                     uploaded_file_name, uploaded_content_hash, uploaded_at_utc)
            VALUES ($path, $system, $emulator, $core, $emulatorVersion, $retrobatVersion, $romId,
                    $romPath, $slot, $screenshot, $native, $hash, $size, $mtime, $scannedAt,
                    $stateId, $uploadedName, $uploaded, $uploadedAt)
            ON CONFLICT (relative_path) DO UPDATE SET
              system                = excluded.system,
              emulator              = excluded.emulator,
              core                  = excluded.core,
              emulator_version      = excluded.emulator_version,
              retrobat_version      = excluded.retrobat_version,
              rom_id                = excluded.rom_id,
              rom_relative_path     = excluded.rom_relative_path,
              slot                  = excluded.slot,
              screenshot_path       = excluded.screenshot_path,
              native_name           = excluded.native_name,
              content_hash          = excluded.content_hash,
              size_bytes            = excluded.size_bytes,
              file_mtime_utc        = excluded.file_mtime_utc,
              scanned_at_utc        = excluded.scanned_at_utc,
              state_id              = COALESCE(excluded.state_id, local_state.state_id),
              uploaded_file_name    = COALESCE(excluded.uploaded_file_name, local_state.uploaded_file_name),
              uploaded_content_hash = COALESCE(excluded.uploaded_content_hash, local_state.uploaded_content_hash),
              uploaded_at_utc       = COALESCE(excluded.uploaded_at_utc, local_state.uploaded_at_utc);
            """)
            .With("$path", state.Path.Value)
            .With("$system", state.System)
            .With("$emulator", state.Emulator)
            .With("$core", state.Core)
            .With("$emulatorVersion", SqliteValues.OrNull(state.EmulatorVersion))
            .With("$retrobatVersion", SqliteValues.OrNull(state.RetroBatVersion))
            .With("$romId", SqliteValues.OrNull(state.RomId))
            .With("$romPath", state.RomPath.HasValue ? state.RomPath.Value.Value : DBNull.Value)
            .With("$slot", state.Slot)
            .With("$screenshot", state.ScreenshotPath.HasValue ? state.ScreenshotPath.Value.Value : DBNull.Value)
            .With("$native", SqliteValues.OrNull(state.NativeName))
            .With("$hash", SqliteValues.OrNull(state.ContentHash))
            .With("$size", state.SizeBytes)
            .With("$mtime", SqliteValues.ToTextOrNull(state.FileMtimeUtc))
            .With("$scannedAt", SqliteValues.ToText(scannedAt))
            .With("$stateId", SqliteValues.OrNull(state.StateId))
            .With("$uploadedName", SqliteValues.OrNull(state.UploadedFileName))
            .With("$uploaded", SqliteValues.OrNull(state.UploadedContentHash))
            .With("$uploadedAt", SqliteValues.ToTextOrNull(state.UploadedAtUtc));

        command.ExecuteNonQuery();
    }

    /// <summary>Records that a state reached the server, with the identity it was given.</summary>
    public void MarkUploaded(
        RelativePath path,
        int stateId,
        string uploadedFileName,
        string contentHash,
        DateTimeOffset now)
    {
        using var command = _connection.Command(
            """
            UPDATE local_state
            SET state_id = $stateId,
                uploaded_file_name = $name,
                uploaded_content_hash = $hash,
                uploaded_at_utc = $now
            WHERE relative_path = $path;
            """)
            .With("$path", path.Value)
            .With("$stateId", stateId)
            .With("$name", uploadedFileName)
            .With("$hash", contentHash)
            .With("$now", SqliteValues.ToText(now));

        command.ExecuteNonQuery();
    }

    /// <summary>Everything found, in path order.</summary>
    public IReadOnlyList<LocalState> List(long? romId = null)
    {
        using var command = _connection.Command(
            """
            SELECT relative_path, system, emulator, core, emulator_version, retrobat_version,
                   rom_id, rom_relative_path, slot, screenshot_path, native_name, content_hash,
                   size_bytes, file_mtime_utc, state_id, uploaded_file_name,
                   uploaded_content_hash, uploaded_at_utc
            FROM local_state
            WHERE ($romId IS NULL OR rom_id = $romId)
            ORDER BY relative_path;
            """)
            .With("$romId", SqliteValues.OrNull(romId));

        using var reader = command.ExecuteReader();
        var states = new List<LocalState>();

        while (reader.Read())
        {
            states.Add(new LocalState
            {
                Path = RelativePath.Create(reader.GetString(0)),
                System = reader.GetString(1),
                Emulator = reader.GetString(2),
                Core = reader.GetString(3),
                EmulatorVersion = reader.GetStringOrNull(4),
                RetroBatVersion = reader.GetStringOrNull(5),
                RomId = reader.GetInt64OrNull(6),
                RomPath = Parse(reader.GetStringOrNull(7)),
                Slot = reader.GetString(8),
                ScreenshotPath = Parse(reader.GetStringOrNull(9)),
                NativeName = reader.GetStringOrNull(10),
                ContentHash = reader.GetStringOrNull(11),
                SizeBytes = reader.GetInt64(12),
                FileMtimeUtc = reader.GetTimestampOrNull(13),
                StateId = (int?)reader.GetInt64OrNull(14),
                UploadedFileName = reader.GetStringOrNull(15),
                UploadedContentHash = reader.GetStringOrNull(16),
                UploadedAtUtc = reader.GetTimestampOrNull(17),
            });
        }

        return states;
    }

    /// <summary>Forgets states whose files are gone.</summary>
    /// <returns>How many rows were removed.</returns>
    public int Forget(IEnumerable<RelativePath> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var removed = 0;

        foreach (var path in paths)
        {
            using var command = _connection
                .Command("DELETE FROM local_state WHERE relative_path = $path;")
                .With("$path", path.Value);

            removed += command.ExecuteNonQuery();
        }

        return removed;
    }

    private static RelativePath? Parse(string? value) =>
        value is not null && RelativePath.TryCreate(value, out var parsed) ? parsed : null;
}
