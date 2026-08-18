using System.Globalization;
using System.Net;
using System.Text.Json;

namespace RomMBat.Tests.Support;

/// <summary>
/// The save, sync-session and play-session half of the stub.
/// </summary>
/// <remarks>
/// Three behaviours here are modelled rather than stubbed away, because the client's
/// correctness is defined against them: the server renames an upload and hands back the
/// untagged stem, identical content into one slot reuses the row, and a play session repeated
/// inside the same second comes back as a duplicate. A stub that skipped any of the three
/// would let the corresponding client bug through.
/// <para>
/// Negotiate is deliberately <b>not</b> a reimplementation of the server's reconciliation. What
/// this suite tests is what the client does with each answer, so the answer is dictated by
/// <see cref="NegotiateActions"/> rather than computed.
/// </para>
/// </remarks>
internal sealed partial class StubRomMServer
{
    /// <summary>Saves the stub holds, by id. Filled by an upload or seeded by a test.</summary>
    public IDictionary<int, StubSave> Saves { get; } = new Dictionary<int, StubSave>();

    /// <summary>What negotiate answers per <c>(rom_id, slot)</c>. Absent means <c>no_op</c>.</summary>
    public IDictionary<(int RomId, string Slot), string> NegotiateActions { get; } =
        new Dictionary<(int, string), string>();

    /// <summary>Slots that answer 409, the way a stale device record makes the real server.</summary>
    public HashSet<(int RomId, string Slot)> ConflictOnUpload { get; } = [];

    /// <summary>
    /// Slots that answer 409 even for an overwrite.
    /// </summary>
    /// <remarks>
    /// The slot moving again between the conflict being reported and the user deciding, which
    /// means they are choosing against something they never saw. Nothing may be forced past it.
    /// </remarks>
    public HashSet<(int RomId, string Slot)> RefuseOverwrite { get; } = [];

    /// <summary>
    /// Slots negotiate offers a <c>download</c> for that the client did not submit.
    /// </summary>
    /// <remarks>
    /// The new-device restore: the server holds a save for a game this device has never played,
    /// so nothing local names it and it cannot appear in the request. Modelled because the
    /// client has to have somewhere to put such a save, and every other download test seeds a
    /// local one first.
    /// </remarks>
    public HashSet<(int RomId, string Slot)> UnsolicitedDownloads { get; } = [];

    /// <summary>
    /// Slots negotiate offers a <c>conflict</c> for that the client did not submit.
    /// </summary>
    /// <remarks>
    /// Measurement 132 says a real instance never does this, because negotiate reconciles only
    /// the set the client names. Modelled anyway, because the client's answer to it was a
    /// <c>save_conflict.local_path</c> insert with an empty path, which fails the column's CHECK
    /// and takes the whole flush down rather than the one operation.
    /// </remarks>
    public HashSet<(int RomId, string Slot)> UnsolicitedConflicts { get; } = [];

    /// <summary>
    /// Save ids the client acknowledged, in order.
    /// </summary>
    /// <remarks>
    /// Empty after a download means the ack never happened, which is the failure that leaves a
    /// save stranded on the server forever.
    /// </remarks>
    public IList<int> Acknowledged { get; } = [];

    /// <summary>
    /// Save ids fetched <b>without</b> <c>optimistic=false</c>.
    /// </summary>
    /// <remarks>
    /// Must stay empty. A stub that ignored the parameter could not catch a client that
    /// stopped sending it, and that client would lose saves on a flaky link with no symptom.
    /// </remarks>
    public IList<int> OptimisticDownloads { get; } = [];

    /// <summary>The size of each play-session batch received, in order.</summary>
    public IList<int> PlaySessionBatchSizes { get; } = [];

    /// <summary>How many negotiate sessions were closed.</summary>
    public int CompletedSessions { get; private set; }

    /// <summary>Cut a save download off after this many bytes, which is a link dropping.</summary>
    public int? TruncateSaveDownloadAfter { get; set; }

    /// <summary>
    /// Report this content hash on negotiate instead of the real one.
    /// </summary>
    /// <remarks>
    /// A corrupted transfer as the client sees it: the bytes arrive and do not match what the
    /// server said they would. The client has to refuse them and not ack.
    /// </remarks>
    public string? HashLie { get; set; }

    /// <summary>
    /// Fail the upload for this slot, as a dropped link mid-flush would.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="ConflictOnUpload"/>, which is the server refusing on purpose.
    /// This is the transport giving out partway through a set, which is the only way to produce
    /// a class B save where one file lands and its sibling does not.
    /// </remarks>
    public string? RefuseUploadForSlot { get; set; }

    /// <summary>Sessions already seen, keyed the way the server dedups: truncated to the second.</summary>
    private HashSet<string> SeenPlaySessions { get; } = new(StringComparer.Ordinal);

    /// <summary>True when the path is one this half of the stub serves.</summary>
    public static bool IsSaveRoute(string path) =>
        path.EndsWith("/api/sync/negotiate", StringComparison.Ordinal)
        || path.Contains("/api/sync/sessions/", StringComparison.Ordinal)
        || path.EndsWith("/api/play-sessions", StringComparison.Ordinal)
        || path.EndsWith("/api/saves", StringComparison.Ordinal)
        || (path.Contains("/api/saves/", StringComparison.Ordinal)
            && (path.EndsWith("/downloaded", StringComparison.Ordinal)
                || path.EndsWith("/content", StringComparison.Ordinal)));

    private async Task<HttpResponseMessage> SaveRouteAsync(
        HttpRequestMessage request,
        string path,
        CancellationToken cancellationToken)
    {
        if (path.EndsWith("/api/sync/negotiate", StringComparison.Ordinal))
        {
            return await NegotiateAsync(request, cancellationToken).ConfigureAwait(false);
        }

        if (path.Contains("/api/sync/sessions/", StringComparison.Ordinal))
        {
            CompletedSessions++;
            return Json(HttpStatusCode.OK, new { ok = true });
        }

        if (path.EndsWith("/api/play-sessions", StringComparison.Ordinal))
        {
            return await PlaySessionsAsync(request, cancellationToken).ConfigureAwait(false);
        }

        if (path.EndsWith("/api/saves", StringComparison.Ordinal))
        {
            return await UploadSaveAsync(request, cancellationToken).ConfigureAwait(false);
        }

        if (path.EndsWith("/downloaded", StringComparison.Ordinal))
        {
            Acknowledged.Add(SaveIdFrom(path, "downloaded"));
            return Json(HttpStatusCode.OK, new { ok = true });
        }

        return SaveContent(request, SaveIdFrom(path, "content"));
    }

    private async Task<HttpResponseMessage> NegotiateAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using var document = await ReadJsonAsync(request, cancellationToken).ConfigureAwait(false);

        if (!document.RootElement.TryGetProperty("device_id", out var deviceId)
            || string.IsNullOrEmpty(deviceId.GetString()))
        {
            // Verbatim what the real server answers when the token is not device-bound either.
            return Detail(
                HttpStatusCode.BadRequest,
                "device_id is required (either in the request payload or implicit via a device-bound client token)");
        }

        var operations = new List<object>();

        foreach (var save in document.RootElement.GetProperty("saves").EnumerateArray())
        {
            var romId = save.GetProperty("rom_id").GetInt32();
            var slot = save.GetProperty("slot").GetString() ?? string.Empty;
            var action = NegotiateActions.TryGetValue((romId, slot), out var told) ? told : "no_op";
            var existing = Saves.Values.FirstOrDefault(row => row.RomId == romId && row.Slot == slot);

            operations.Add(new
            {
                action,
                rom_id = romId,
                save_id = existing?.Id,
                file_name = existing?.FileName,
                slot,
                emulator = save.GetProperty("emulator").GetString(),
                reason = action == "conflict" ? "Both changed since the last sync" : "stub",
                server_updated_at = existing?.UpdatedAt,
                server_content_hash = HashLie ?? existing?.ContentHash,
            });
        }

        foreach (var (romId, slot) in UnsolicitedDownloads)
        {
            var existing = Saves.Values.FirstOrDefault(row => row.RomId == romId && row.Slot == slot);

            operations.Add(new
            {
                action = "download",
                rom_id = romId,
                save_id = existing?.Id,
                file_name = existing?.FileName,
                slot,
                emulator = existing?.Emulator,
                reason = "held on the server and not on this device",
                server_updated_at = existing?.UpdatedAt,
                server_content_hash = HashLie ?? existing?.ContentHash,
            });
        }

        foreach (var (romId, slot) in UnsolicitedConflicts)
        {
            var existing = Saves.Values.FirstOrDefault(row => row.RomId == romId && row.Slot == slot);

            operations.Add(new
            {
                action = "conflict",
                rom_id = romId,
                save_id = existing?.Id,
                file_name = existing?.FileName,
                slot,
                emulator = existing?.Emulator,
                reason = "Both changed since the last sync",
                server_updated_at = existing?.UpdatedAt,
                server_content_hash = HashLie ?? existing?.ContentHash,
            });
        }

        return Json(HttpStatusCode.OK, new { session_id = 4242, operations = operations.ToArray() });
    }

    private async Task<HttpResponseMessage> UploadSaveAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var query = ParseQuery(request.RequestUri);
        var romId = int.Parse(query.GetValueOrDefault("rom_id", "0"), CultureInfo.InvariantCulture);
        var slot = query.GetValueOrDefault("slot", string.Empty);

        var overwrite = string.Equals(
            query.GetValueOrDefault("overwrite"),
            "true",
            StringComparison.Ordinal);

        if (RefuseUploadForSlot is { } refused && string.Equals(slot, refused, StringComparison.Ordinal))
        {
            return Detail(HttpStatusCode.InternalServerError, "the upload did not complete");
        }

        if (ConflictOnUpload.Contains((romId, slot))
            && (!overwrite || RefuseOverwrite.Contains((romId, slot))))
        {
            // Measured at 5.1.1-beta.1: a bare string, not the structured object other clients
            // document, so a client reading it as an object gets null and shows nothing.
            //
            // overwrite=true is what gets past it, and it replaces in place rather than
            // appending. That is why it is correct only after somebody has chosen a side, and
            // why the stub honours it here rather than refusing unconditionally.
            return Detail(HttpStatusCode.Conflict, "Slot has a newer save since your last sync");
        }

        var content = await request.Content!.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var body = ExtractSaveFile(content, out var sentName);
        var hash = HashOf(body);

        var existing = Saves.Values.FirstOrDefault(row =>
            row.RomId == romId && row.Slot == slot && row.ContentHash == hash);

        if (existing is not null)
        {
            // Identical content into one slot reuses the row. This is what makes replaying a
            // failed flush free, and it only holds if the client's hash is deterministic.
            return Json(HttpStatusCode.OK, Describe(existing));
        }

        // An overwrite replaces the row in the slot rather than appending beside it, which is
        // the difference that makes it a resolution rather than a second opinion.
        var replaced = overwrite
            ? Saves.Values.FirstOrDefault(row => row.RomId == romId && row.Slot == slot)
            : null;

        var id = replaced?.Id ?? (Saves.Count == 0 ? 100 : Saves.Keys.Max() + 1);

        var save = new StubSave
        {
            Id = id,
            RomId = romId,
            Slot = slot,
            Emulator = query.GetValueOrDefault("emulator", string.Empty),
            Bytes = body,
            FileNameNoTags = Path.GetFileNameWithoutExtension(sentName),
            FileExtension = Path.GetExtension(sentName).TrimStart('.'),
            OriginDeviceId = query.GetValueOrDefault("device_id", string.Empty),
            UpdatedAt = ServerDate ?? DateTimeOffset.UnixEpoch,
        };

        Saves[id] = save;
        return Json(HttpStatusCode.OK, Describe(save));
    }

    private HttpResponseMessage SaveContent(HttpRequestMessage request, int saveId)
    {
        if (!Saves.TryGetValue(saveId, out var save))
        {
            return Detail(HttpStatusCode.NotFound, "no such save");
        }

        if (!string.Equals(ParseQuery(request.RequestUri).GetValueOrDefault("optimistic"), "false", StringComparison.Ordinal))
        {
            OptimisticDownloads.Add(saveId);
        }

        Stream body = TruncateSaveDownloadAfter is { } cut && cut < save.Bytes.Length
            ? new TruncatingStream(save.Bytes, cut)
            : new MemoryStream(save.Bytes);

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) };
    }

    private async Task<HttpResponseMessage> PlaySessionsAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using var document = await ReadJsonAsync(request, cancellationToken).ConfigureAwait(false);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            // Freegosy's bare array lands here, and the real server answers 422 for it.
            return Detail(HttpStatusCode.UnprocessableEntity, "Input should be a valid dictionary or object");
        }

        var sessions = document.RootElement.GetProperty("sessions");
        PlaySessionBatchSizes.Add(sessions.GetArrayLength());

        if (sessions.GetArrayLength() > 100)
        {
            return Detail(HttpStatusCode.BadRequest, "Batch size exceeds maximum of 100");
        }

        var results = new List<object>();
        var created = 0;
        var skipped = 0;
        var index = 0;

        foreach (var session in sessions.EnumerateArray())
        {
            var start = session.GetProperty("start_time").GetString() ?? string.Empty;
            var end = session.GetProperty("end_time").GetString() ?? string.Empty;

            if (string.CompareOrdinal(end, start) <= 0)
            {
                return Detail(HttpStatusCode.UnprocessableEntity, "Value error, end_time must be after start_time");
            }

            // Truncated to the second, which is how the server dedups and therefore what makes
            // a replayed flush idempotent rather than a second session.
            var romId = session.TryGetProperty("rom_id", out var rom) ? rom.ToString() : "none";
            var key = $"{romId}|{Second(start)}|{Second(end)}";

            if (SeenPlaySessions.Add(key))
            {
                results.Add(new { index, status = "created", id = 500 + index, detail = (string?)null });
                created++;
            }
            else
            {
                results.Add(new { index, status = "duplicate", id = (int?)null, detail = (string?)null });
                skipped++;
            }

            index++;
        }

        return Json(HttpStatusCode.OK, new
        {
            results = results.ToArray(),
            created_count = created,
            skipped_count = skipped,
        });
    }

    private static object Describe(StubSave save) => new
    {
        id = save.Id,
        rom_id = save.RomId,

        // The rename the real server does. A client that writes this to disk produces a file
        // the emulator cannot see, which is the whole reason both names exist.
        file_name = save.FileName,
        file_name_no_tags = save.FileNameNoTags,
        file_extension = save.FileExtension,
        file_size_bytes = save.Bytes.Length,
        content_hash = save.ContentHash,
        slot = save.Slot,
        emulator = save.Emulator,
        origin_device_id = save.OriginDeviceId,
        updated_at = save.UpdatedAt,
    };

    private static string Second(string timestamp) =>
        timestamp.Length >= 19 ? timestamp[..19] : timestamp;

    private static string HashOf(byte[] bytes)
    {
#pragma warning disable CA5351 // MD5, deliberately: it is what RomM's content_hash is.
        return Convert.ToHexStringLower(System.Security.Cryptography.MD5.HashData(bytes));
#pragma warning restore CA5351
    }

    private static int SaveIdFrom(string path, string suffix)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var index = Array.IndexOf(segments, suffix);

        return index > 0 && int.TryParse(segments[index - 1], CultureInfo.InvariantCulture, out var id) ? id : 0;
    }

    private static Dictionary<string, string> ParseQuery(Uri? uri)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var pair in (uri?.Query ?? string.Empty).TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = pair.IndexOf('=', StringComparison.Ordinal);

            if (equals > 0)
            {
                values[pair[..equals]] = Uri.UnescapeDataString(pair[(equals + 1)..]);
            }
        }

        return values;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return JsonDocument.Parse(body);
    }

    /// <summary>
    /// Pulls the <c>saveFile</c> part out of a multipart body, without a full parser.
    /// </summary>
    /// <remarks>
    /// Latin-1 throughout, so a byte round-trips unchanged and a save's binary content is not
    /// mangled by a decode that assumes text.
    /// <para>
    /// <b>Both markers are looked for in the part's headers only, and the part ends at the real
    /// boundary.</b> The earlier version searched the whole body for both, which held for as
    /// long as every uploaded save was a small text fixture and broke the moment class C started
    /// sending a zip. Deflate output contains the literal bytes of a filename marker and of a
    /// boundary-looking sequence often enough to hit both traps at once: the name was read from
    /// inside the archive, and the body was truncated at the first thing that looked like a
    /// terminator. A save is arbitrary bytes, so a parser over it has to be anchored.
    /// </para>
    /// <para>
    /// <b>The filename is quoted only when it has to be, and reading only the quoted form stored
    /// every bundled save as an empty one.</b> .NET writes <c>filename="Tetris (World).srm"</c>
    /// because of the space and the brackets, and a bare <c>filename=25pacman.zip</c> because a
    /// token needs no quotes. So the class C tests uploaded archives the stub recorded as zero
    /// bytes under no name, and every assertion they made about counts and slots still passed.
    /// Third stub shortcut in this suite to hide behind tidy fixtures.
    /// </para>
    /// </remarks>
    private static byte[] ExtractSaveFile(byte[] content, out string fileName)
    {
        var text = System.Text.Encoding.Latin1.GetString(content);

        // The boundary is the first line of the body, which is where multipart puts it.
        var firstBreak = text.IndexOf("\r\n", StringComparison.Ordinal);
        var boundary = firstBreak < 0 ? "--" : text[..firstBreak];

        // The headers of the first part end at the first blank line, and everything after that
        // is bytes the sender chose.
        var headerEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var headers = headerEnd < 0 ? text : text[..headerEnd];

        const string NameMarker = "filename=";
        var nameStart = headers.IndexOf(NameMarker, StringComparison.Ordinal);

        // filename*= is the RFC 5987 copy .NET writes beside the plain one. Skipped, because the
        // plain value is what the server reads and what the client is being tested on.
        while (nameStart > 0 && headers[nameStart - 1] == '*')
        {
            nameStart = headers.IndexOf(NameMarker, nameStart + NameMarker.Length, StringComparison.Ordinal);
        }

        if (nameStart < 0)
        {
            fileName = string.Empty;
            return [];
        }

        nameStart += NameMarker.Length;

        if (nameStart < headers.Length && headers[nameStart] == '"')
        {
            var quoted = headers.IndexOf('"', nameStart + 1);
            fileName = quoted < 0 ? headers[(nameStart + 1)..] : headers[(nameStart + 1)..quoted];
        }
        else
        {
            var token = headers.IndexOfAny([';', '\r', '\n'], nameStart);
            fileName = (token < 0 ? headers[nameStart..] : headers[nameStart..token]).Trim();
        }

        var bodyStart = headerEnd + 4;
        var bodyEnd = text.IndexOf("\r\n" + boundary, bodyStart, StringComparison.Ordinal);

        return System.Text.Encoding.Latin1.GetBytes(text[bodyStart..(bodyEnd < 0 ? text.Length : bodyEnd)]);
    }

    /// <summary>A save as the stub holds it.</summary>
    public sealed record StubSave
    {
        public required int Id { get; init; }

        public required int RomId { get; init; }

        public required string Slot { get; init; }

        public required string Emulator { get; init; }

        public required byte[] Bytes { get; init; }

        public required string FileNameNoTags { get; init; }

        public required string FileExtension { get; init; }

        public string? OriginDeviceId { get; init; }

        public DateTimeOffset UpdatedAt { get; init; }

        /// <summary>The tagged name the server rewrites an upload to.</summary>
        public string FileName =>
            $"{FileNameNoTags} [{UpdatedAt:yyyy-MM-dd_HH-mm-ss}]"
            + (string.IsNullOrEmpty(FileExtension) ? string.Empty : "." + FileExtension);

        public string ContentHash => HashOf(Bytes);
    }

    /// <summary>A body that stops early, which is what a link dropping mid-transfer looks like.</summary>
    private sealed class TruncatingStream(byte[] bytes, int cut) : MemoryStream(bytes)
    {
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (Position >= cut)
            {
                throw new IOException("the connection was closed before the body finished.");
            }

            return base.Read(buffer, offset, (int)Math.Min(count, cut - Position));
        }
    }
}
