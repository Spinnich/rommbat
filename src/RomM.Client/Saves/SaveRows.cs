using System.Text.Json.Serialization;

namespace RomM.Client.Saves;

/// <summary>A save as the server holds it.</summary>
/// <param name="FileName">
/// <b>The server's identity, not a name to write on disk.</b> The upload is renamed to
/// <c>&lt;name&gt; [YYYY-MM-DD_HH-MM-SS]&lt;ext&gt;</c>, and a file written under that name is
/// invisible to the emulator, which finds a battery save by matching the ROM name.
/// </param>
/// <param name="FileNameNoTags">
/// <b>The stem to write on disk</b>, with <paramref name="FileExtension"/>. The server returns
/// it already stripped, so no client-side regex is involved; Freegosy answered the same
/// problem with a hand-written one and shipped two filename bugs doing it.
/// </param>
/// <param name="OriginDeviceId">
/// Which device uploaded this. The cheapest way to recognise your own save coming back down,
/// which decides whether a <c>download</c> operation is worth acting on.
/// </param>
public sealed record SaveRow(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("rom_id")] int RomId,
    [property: JsonPropertyName("file_name")] string? FileName,
    [property: JsonPropertyName("file_name_no_tags")] string? FileNameNoTags,
    [property: JsonPropertyName("file_extension")] string? FileExtension,
    [property: JsonPropertyName("file_size_bytes")] long FileSizeBytes,
    [property: JsonPropertyName("content_hash")] string? ContentHash,
    [property: JsonPropertyName("slot")] string? Slot,
    [property: JsonPropertyName("emulator")] string? Emulator,
    [property: JsonPropertyName("origin_device_id")] string? OriginDeviceId,
    [property: JsonPropertyName("updated_at")] DateTimeOffset? UpdatedAt,
    [property: JsonPropertyName("device_syncs")] IReadOnlyList<SaveDeviceSync>? DeviceSyncs)
{
    /// <summary>
    /// The name to write on disk, which is not <see cref="FileName"/>.
    /// </summary>
    /// <remarks>
    /// Null when the server sent neither half, which is not a case observed but is a case a
    /// caller must not paper over by falling back to the tagged name.
    /// </remarks>
    public string? OnDiskFileName => FileNameNoTags is null
        ? null
        : string.IsNullOrEmpty(FileExtension) ? FileNameNoTags : $"{FileNameNoTags}.{FileExtension}";
}

/// <summary>One device's sync record for a save.</summary>
/// <remarks>
/// <b>The array this comes from is empty unless the request carried <c>device_id</c></b>, and
/// empty reads exactly like "nobody has ever synced this" while meaning "you did not ask". A
/// device that has genuinely never synced is <b>absent</b> rather than present with
/// <c>is_current: false</c>, so a missing entry is the strongest reason to pull.
/// </remarks>
public sealed record SaveDeviceSync(
    [property: JsonPropertyName("device_id")] string? DeviceId,
    [property: JsonPropertyName("is_current")] bool IsCurrent,
    [property: JsonPropertyName("is_untracked")] bool IsUntracked);

/// <summary>What the server wants done about one slot.</summary>
public enum SyncAction
{
    /// <summary>Nothing to do. The device already holds what the server holds.</summary>
    NoOp,

    /// <summary>Send the local save up.</summary>
    Upload,

    /// <summary>Fetch the server's save down.</summary>
    Download,

    /// <summary>Both sides moved. Never resolved silently.</summary>
    Conflict,
}

/// <summary>One line of a negotiate answer.</summary>
public sealed record SyncOperation(
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("rom_id")] int RomId,
    [property: JsonPropertyName("save_id")] int? SaveId,
    [property: JsonPropertyName("file_name")] string? FileName,
    [property: JsonPropertyName("slot")] string? Slot,
    [property: JsonPropertyName("emulator")] string? Emulator,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("server_updated_at")] DateTimeOffset? ServerUpdatedAt,
    [property: JsonPropertyName("server_content_hash")] string? ServerContentHash)
{
    /// <summary>
    /// The action, or <see cref="SyncAction.Conflict"/> for anything unrecognised.
    /// </summary>
    /// <remarks>
    /// An unknown action from a newer server is treated as a conflict rather than ignored,
    /// because the two safe outcomes are "do nothing" and "ask", and ignoring it would let a
    /// slot the server wanted handled fall silently through.
    /// </remarks>
    public SyncAction Parsed => Action switch
    {
        "no_op" => SyncAction.NoOp,
        "upload" => SyncAction.Upload,
        "download" => SyncAction.Download,
        _ => SyncAction.Conflict,
    };
}

/// <summary>What the client claims to hold, one entry per slot.</summary>
/// <param name="UpdatedAt">
/// The file's <b>real local mtime</b>, never the sync time. Sending the sync time makes every
/// offline edit lose every conflict.
/// </param>
public sealed record NegotiateSave(
    [property: JsonPropertyName("rom_id")] int RomId,
    [property: JsonPropertyName("file_name")] string FileName,
    [property: JsonPropertyName("slot")] string Slot,
    [property: JsonPropertyName("emulator")] string Emulator,
    [property: JsonPropertyName("content_hash")] string? ContentHash,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("file_size_bytes")] long FileSizeBytes);

/// <summary>The negotiate request body.</summary>
/// <remarks>
/// <c>device_id</c> is sent even though a device-bound token makes it optional. Ours comes
/// from pairing so it qualifies, measured; sending it is harmless and more explicit.
/// </remarks>
public sealed record NegotiateRequest(
    [property: JsonPropertyName("device_id")] string DeviceId,
    [property: JsonPropertyName("saves")] IReadOnlyList<NegotiateSave> Saves);

/// <summary>The negotiate answer.</summary>
public sealed record NegotiateResult(
    [property: JsonPropertyName("session_id")] int SessionId,
    [property: JsonPropertyName("operations")] IReadOnlyList<SyncOperation> Operations);

/// <summary>One play session, as the standalone ingest wants it.</summary>
/// <remarks>
/// <c>device_id</c> lives on the envelope and not here. A bare array of entries carrying their
/// own device id, which is what Freegosy sends, answers 422.
/// </remarks>
public sealed record PlaySessionEntry(
    [property: JsonPropertyName("rom_id")] int? RomId,
    [property: JsonPropertyName("save_slot")] string? SaveSlot,
    [property: JsonPropertyName("start_time")] DateTimeOffset StartTime,
    [property: JsonPropertyName("end_time")] DateTimeOffset EndTime,
    [property: JsonPropertyName("duration_ms")] long DurationMs);

/// <summary>
/// The one rom-user property RomMBat writes, and it writes it to put it back.
/// </summary>
/// <remarks>
/// <b>Every field of <c>RomUserData</c> is optional and omitting one leaves it alone</b>, which
/// is measured rather than read off the schema: the schema declares all eight nullable with none
/// required, which is equally consistent with "omit means null it". Driven against the live
/// instance, a <c>PUT</c> carrying only <c>now_playing</c> left a rating of 7, the difficulty,
/// the status, the backlog flag and <c>last_played</c> all untouched. That is what makes writing
/// this one field safe; sending the whole record would need RomMBat to know values it has no
/// business holding.
/// </remarks>
public sealed record RomUserNowPlaying(
    [property: JsonPropertyName("now_playing")] bool NowPlaying);

/// <summary>The play-session request body, whose shape is an envelope.</summary>
public sealed record PlaySessionBatch(
    [property: JsonPropertyName("device_id")] string DeviceId,
    [property: JsonPropertyName("sessions")] IReadOnlyList<PlaySessionEntry> Sessions);

/// <summary>What happened to one entry of a play-session batch.</summary>
/// <remarks>
/// <b>Per index, so a partially failed flush is reconciled exactly rather than inferred.</b> A
/// replayed session comes back <c>"duplicate"</c>, which is the server naming what it skipped
/// instead of leaving the client to guess.
/// </remarks>
public sealed record PlaySessionResult(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("id")] int? Id,
    [property: JsonPropertyName("detail")] string? Detail)
{
    /// <summary>True when the server already had this session and skipped it.</summary>
    public bool IsDuplicate => string.Equals(Status, "duplicate", StringComparison.Ordinal);

    /// <summary>True when the entry landed, whether created now or already present.</summary>
    public bool IsAccepted =>
        IsDuplicate || string.Equals(Status, "created", StringComparison.Ordinal);
}

/// <summary>The play-session answer.</summary>
public sealed record PlaySessionOutcome(
    [property: JsonPropertyName("results")] IReadOnlyList<PlaySessionResult> Results,
    [property: JsonPropertyName("created_count")] int CreatedCount,
    [property: JsonPropertyName("skipped_count")] int SkippedCount);
