using System.Text.Json.Serialization;

namespace RomM.Client.Saves;

/// <summary>A save state as the server holds it.</summary>
/// <remarks>
/// <b>Two fields a save has and a state does not: <c>slot</c> and <c>content_hash</c>.</b> Read
/// off the pinned 5.1.0 schema, <c>StateSchema</c> carries neither, so a state has no pairing
/// key on the wire and the server holds no digest to compare against. Both absences shape the
/// client: the <c>{emulator}:{core}:{slot}</c> slot is a purely local identity that never
/// travels, and "is this state in step" is answerable only from the hash the device recorded
/// when it uploaded.
/// </remarks>
/// <param name="FileName">
/// The server's identity, tagged the way a save's is. Not a name to write on disk.
/// </param>
/// <param name="Emulator">
/// Echoed back from the upload query. The only field on a state that says anything about where
/// it came from, which is why the emulator, core and version are also kept locally.
/// </param>
public sealed record StateRow(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("rom_id")] int RomId,
    [property: JsonPropertyName("file_name")] string? FileName,
    [property: JsonPropertyName("file_name_no_tags")] string? FileNameNoTags,
    [property: JsonPropertyName("file_extension")] string? FileExtension,
    [property: JsonPropertyName("file_size_bytes")] long FileSizeBytes,
    [property: JsonPropertyName("emulator")] string? Emulator,
    [property: JsonPropertyName("missing_from_fs")] bool MissingFromFs,
    [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset? UpdatedAt,
    [property: JsonPropertyName("screenshot")] StateScreenshotRow? Screenshot)
{
    /// <summary>The name to write on disk, which is not <see cref="FileName"/>.</summary>
    public string? OnDiskFileName => FileNameNoTags is null
        ? null
        : string.IsNullOrEmpty(FileExtension) ? FileNameNoTags : $"{FileNameNoTags}.{FileExtension}";
}

/// <summary>The screenshot uploaded beside a state, when one was.</summary>
/// <remarks>
/// <b>Absent is the normal case, not the exception.</b> RetroBat mirrors an emulator's own
/// screenshot into the ES-facing directory about 120 ms after the state, and the emulator can
/// still be writing it: across three saves of one PPSSPP game the mirrored image came out
/// correct, zero bytes and absent. So a state with no screenshot says nothing about the state.
/// </remarks>
public sealed record StateScreenshotRow(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("file_name")] string? FileName,
    [property: JsonPropertyName("file_size_bytes")] long FileSizeBytes);
