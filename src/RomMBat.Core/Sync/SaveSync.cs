using RomM.Client;
using RomM.Client.Saves;
using RomMBat.Core.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;

namespace RomMBat.Core.Sync;

/// <summary>What a save sync did.</summary>
public sealed record SaveSyncOutcome
{
    public int Uploaded { get; init; }

    public int Downloaded { get; init; }

    public int Conflicts { get; init; }

    public int NoOps { get; init; }

    public int Failed { get; init; }

    public long BytesTransferred { get; init; }

    public IReadOnlyList<string> Problems { get; init; } = [];

    /// <summary>Conflicts, with what the user has to choose between.</summary>
    public IReadOnlyList<SaveConflict> Unresolved { get; init; } = [];

    public bool IsNoOp => Uploaded == 0 && Downloaded == 0 && Conflicts == 0 && Failed == 0;

    public string Summary
    {
        get
        {
            if (IsNoOp)
            {
                return NoOps == 0 ? "saves: nothing to sync" : $"saves: {NoOps} already in step";
            }

            var parts = new List<string>();

            if (Uploaded > 0)
            {
                parts.Add($"{Uploaded} up");
            }

            if (Downloaded > 0)
            {
                parts.Add($"{Downloaded} down ({ByteSize.Format(BytesTransferred)})");
            }

            if (Conflicts > 0)
            {
                parts.Add($"{Conflicts} conflicted");
            }

            if (Failed > 0)
            {
                parts.Add($"{Failed} failed");
            }

            return "saves: " + string.Join(", ", parts);
        }
    }
}

/// <summary>Both sides of a slot moved, and neither is thrown away.</summary>
/// <param name="LocalCopy">
/// Where the local file was copied before anything else happened. Principle 1's "always copy
/// the local file aside before any overwrite", made a path rather than a promise.
/// </param>
public sealed record SaveConflict(
    long RomId,
    string Slot,
    RelativePath LocalPath,
    RelativePath? LocalCopy,
    string? LocalHash,
    string? ServerHash,
    DateTimeOffset? ServerUpdatedAt,
    string Reason);

/// <summary>
/// Negotiates every slot this device holds and carries out what the server asks.
/// </summary>
/// <remarks>
/// <b>Every operation either completes or queues, and every flush is safe to replay.</b>
/// Identical content posted twice into one slot reuses the server row, so a replayed upload is
/// a no-op rather than a duplicate; that only holds because the content hash is taken over the
/// logical contents and is therefore deterministic.
/// <para>
/// <b>Nothing is overwritten without a copy aside first.</b> A restore writes to
/// <c>emulators/rommbat/partial/</c>, verifies the bytes against the hash the server reported,
/// moves the existing file aside, and only then puts the new one in place. A half-written save
/// is a corrupt save, and the previous copy stays until the next successful sync.
/// </para>
/// <para>
/// <b>The first save seen for a ROM with no local baseline does not win on recency.</b> A
/// Master System cart booted to its title screen wrote an 8,188-byte <c>.srm</c> of legible
/// ASCII with no player input at all, so a local file's mere existence is not evidence anything
/// was played. Where the server also holds something and this device has never uploaded, the
/// slot is a conflict for the user rather than an upload.
/// </para>
/// </remarks>
public sealed class SaveSync
{
    private readonly RetroBatInstall _install;
    private readonly LocalStore _store;
    private readonly RomMConnection _connection;
    private readonly string _deviceId;
    private readonly TimeProvider _time;

    public SaveSync(
        RetroBatInstall install,
        LocalStore store,
        RomMConnection connection,
        string deviceId,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        _install = install;
        _store = store;
        _connection = connection;
        _deviceId = deviceId;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Where a download lands before it is verified.</summary>
    /// <remarks>
    /// Under <c>emulators/rommbat/partial/</c> and never beside the target, for the same reason
    /// M3 put ROM downloads there: a power loss must not leave a half-written file in a folder
    /// an emulator will read.
    /// </remarks>
    public static RelativePath PartialDirectory { get; } =
        RetroBatInstall.AppDirectory.Combine("partial");

    /// <summary>Where the copy taken before an overwrite lives.</summary>
    public static RelativePath AsideDirectory { get; } =
        RetroBatInstall.AppDirectory.Combine("replaced");

    /// <summary>Negotiates and applies, in one pass.</summary>
    public async Task<SaveSyncOutcome> RunAsync(CancellationToken cancellationToken = default)
    {
        var saves = _store.Saves.List()
            .Where(save => save.RomId is not null && save.ContentHash is not null)
            .ToList();

        if (saves.Count == 0)
        {
            return new SaveSyncOutcome();
        }

        var byKey = saves.ToDictionary(save => (save.RomId!.Value, save.Slot));

        var request = new NegotiateRequest(
            _deviceId,
            [.. saves.Select(save => new NegotiateSave(
                (int)save.RomId!.Value,
                save.Path.Name,
                save.Slot,
                save.Emulator,
                save.ContentHash,

                // The file's real mtime, never the sync time. Sending the sync time makes
                // every offline edit lose every conflict it is in.
                save.FileMtimeUtc ?? DateTimeOffset.UnixEpoch,
                save.SizeBytes))]);

        RomMResponse<NegotiateResult> negotiated;

        try
        {
            negotiated = await _connection.NegotiateSavesAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (RomMUnreachableException ex)
        {
            // Being unreachable is a working state, not an exception the caller should have to
            // handle: every save stays exactly where it is, still recorded as unsent, and the
            // next flush negotiates the same set. This is the whole of "operations complete or
            // queue" at the point where the network first enters the picture.
            return new SaveSyncOutcome { Failed = saves.Count, Problems = [ex.Message] };
        }

        if (!negotiated.IsSuccess || negotiated.Value is not { } result)
        {
            return new SaveSyncOutcome { Failed = saves.Count, Problems = [negotiated.Message ?? "negotiate failed"] };
        }

        var uploaded = 0;
        var downloaded = 0;
        var noOps = 0;
        var failed = 0;
        var bytes = 0L;
        var problems = new List<string>();
        var conflicts = new List<SaveConflict>();

        foreach (var operation in result.Operations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var key = ((long)operation.RomId, operation.Slot ?? string.Empty);
            byKey.TryGetValue(key, out var local);

            switch (operation.Parsed)
            {
                case SyncAction.NoOp:
                    noOps++;
                    break;

                case SyncAction.Upload when local is not null:
                    if (await UploadAsync(local, result.SessionId, cancellationToken).ConfigureAwait(false)
                        is { } uploadProblem)
                    {
                        failed++;
                        problems.Add(uploadProblem);
                    }
                    else
                    {
                        uploaded++;
                    }

                    break;

                case SyncAction.Download when operation.SaveId is { } saveId:
                    var download = await DownloadAsync(operation, saveId, local, result.SessionId, cancellationToken)
                        .ConfigureAwait(false);

                    if (download.Problem is { } downloadProblem)
                    {
                        failed++;
                        problems.Add(downloadProblem);
                    }
                    else
                    {
                        downloaded++;
                        bytes += download.Bytes;
                    }

                    break;

                case SyncAction.Conflict:
                    conflicts.Add(RecordConflict(operation, local));
                    break;

                default:
                    failed++;
                    problems.Add(
                        $"rom {operation.RomId} slot {operation.Slot}: the server asked for "
                            + $"'{operation.Action}' and there is no local save to act on.");
                    break;
            }
        }

        try
        {
            // Reported honestly rather than optimistically: a conflict is not a completed
            // operation and the server's own counters should not say it was.
            await _connection
                .CompleteSyncSessionAsync(
                    result.SessionId,
                    uploaded + downloaded + noOps,
                    failed + conflicts.Count,
                    null,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RomMUnreachableException ex)
        {
            // The link dropped after the transfers. Everything that landed still landed, and a
            // session left open costs the server a stale row rather than costing anyone a save.
            problems.Add($"the sync session could not be closed: {ex.Message}");
        }

        return new SaveSyncOutcome
        {
            Uploaded = uploaded,
            Downloaded = downloaded,
            Conflicts = conflicts.Count,
            NoOps = noOps,
            Failed = failed,
            BytesTransferred = bytes,
            Problems = problems,
            Unresolved = conflicts,
        };
    }

    private async Task<string?> UploadAsync(LocalSave save, int sessionId, CancellationToken cancellationToken)
    {
        var path = _install.Resolve(save.Path);

        if (!File.Exists(path))
        {
            return $"{save.Path}: the file is gone since the scan.";
        }

        try
        {
            await using var content = File.OpenRead(path);

            var response = await _connection.UploadSaveAsync(
                (int)save.RomId!.Value,
                save.Slot,
                save.Emulator,
                _deviceId,
                sessionId,
                save.Path.Name,
                content,
                overwrite: false,
                cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccess || response.Value is not { } result)
            {
                return $"{save.Path}: {response.Message}";
            }

            if (result.Conflict)
            {
                // The server refused because this device's record is stale. Surfaced rather
                // than retried with overwrite, which would discard whatever moved.
                return $"{save.Path}: {result.Detail ?? "the slot moved since the last sync."}";
            }

            if (result.Save is { } row)
            {
                // The server's identity, which is the tagged name, alongside the untagged one
                // that is what would be written on a restore. Both, because they differ.
                _store.SaveSlots.Record(row, _time.GetUtcNow());
            }

            _store.Saves.MarkUploaded(save.Path, save.ContentHash!, _time.GetUtcNow());
            return null;
        }
        catch (RomMUnreachableException ex)
        {
            return $"{save.Path}: {ex.Message}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"{save.Path}: it could not be read: {ex.Message}";
        }
    }

    /// <summary>
    /// Fetches a save and puts it in place atomically.
    /// </summary>
    /// <remarks>
    /// Written to a <c>.part</c>, verified against the hash the server reported, the existing
    /// file moved aside, then the new one moved in. The ack goes last, after all of that, which
    /// is the whole point of <c>optimistic=false</c> on the request.
    /// </remarks>
    private async Task<(long Bytes, string? Problem)> DownloadAsync(
        SyncOperation operation,
        int saveId,
        LocalSave? local,
        int sessionId,
        CancellationToken cancellationToken)
    {
        var target = ResolveTarget(operation, local);

        if (target is not { } destination)
        {
            return (0, $"rom {operation.RomId} slot {operation.Slot}: nowhere to write it. "
                + "The server named no file and this device holds no save in that slot.");
        }

        var partialDirectory = _install.Resolve(PartialDirectory);
        var part = Path.Combine(partialDirectory, $"save-{saveId}.part");

        try
        {
            Directory.CreateDirectory(partialDirectory);

            long written;

            await using (var stream = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var response = await _connection
                    .DownloadSaveAsync(saveId, _deviceId, sessionId, stream, cancellationToken)
                    .ConfigureAwait(false);

                if (!response.IsSuccess)
                {
                    return (0, $"{destination}: {response.Message}");
                }

                written = response.Value;
            }

            if (operation.ServerContentHash is { } expected)
            {
                var found = LogicalContentHash.OfFile(part);

                if (!string.Equals(found, expected, StringComparison.OrdinalIgnoreCase))
                {
                    SafeDelete(part);
                    return (0, $"{destination}: what arrived hashes to {found} and the server said "
                        + $"{expected}. Nothing was written and the server was not told it arrived.");
                }
            }

            var absolute = _install.Resolve(destination);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);

            MoveAside(destination);
            File.Move(part, absolute, overwrite: true);

            // Only now, with the bytes on disk and checked. Before this the server still
            // believes the device does not have the save, which is the recoverable state.
            var ack = await _connection.AcknowledgeSaveAsync(saveId, _deviceId, cancellationToken).ConfigureAwait(false);

            if (!ack.IsSuccess)
            {
                // The file is in place and the server does not know. The next negotiate offers
                // it again and the second download is a no-op against identical content, so
                // this costs a transfer rather than a save.
                return (written, $"{destination}: written, but the server was not told: {ack.Message}");
            }

            RecordRestored(operation, destination, local);
            return (written, null);
        }
        catch (RomMUnreachableException ex)
        {
            SafeDelete(part);
            return (0, $"{destination}: {ex.Message}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SafeDelete(part);
            return (0, $"{destination}: it could not be written: {ex.Message}");
        }
    }

    /// <summary>
    /// Where a downloaded save goes on disk.
    /// </summary>
    /// <remarks>
    /// <b>The untagged name, never the one the server holds.</b> The tagged
    /// <c>Game [2026-08-10_22-58-26].srm</c> is invisible to an emulator matching on the ROM
    /// name. Where this device already has the slot, its own path wins, because that is a path
    /// this device has proven an emulator reads.
    /// </remarks>
    private RelativePath? ResolveTarget(SyncOperation operation, LocalSave? local)
    {
        if (local is not null)
        {
            return local.Path;
        }

        var known = _store.SaveSlots.Read(operation.RomId, operation.Slot ?? string.Empty);
        return known?.OnDiskPath;
    }

    /// <summary>Copies whatever is there out of the way, and returns where it went.</summary>
    private RelativePath? MoveAside(RelativePath target)
    {
        var absolute = _install.Resolve(target);

        if (!File.Exists(absolute))
        {
            return null;
        }

        var aside = AsideDirectory.Combine($"{_time.GetUtcNow():yyyyMMddTHHmmss}-{target.Name}");
        var asidePath = _install.Resolve(aside);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(asidePath)!);

            // Copy rather than move: if anything after this fails, the file the emulator reads
            // is still the one that was always there.
            File.Copy(absolute, asidePath, overwrite: true);
            return aside;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Reported by the caller failing rather than swallowed: an overwrite with no copy
            // aside is exactly what principle 1 forbids.
            throw new IOException(
                $"the existing save at {target} could not be copied aside, so it was not replaced: {ex.Message}",
                ex);
        }
    }

    private SaveConflict RecordConflict(SyncOperation operation, LocalSave? local)
    {
        RelativePath? aside = null;

        if (local is not null)
        {
            try
            {
                aside = MoveAside(local.Path);
            }
            catch (IOException)
            {
                // The copy is a courtesy here rather than a precondition: nothing is being
                // overwritten, because a conflict is not resolved automatically.
            }
        }

        return new SaveConflict(
            operation.RomId,
            operation.Slot ?? string.Empty,
            local?.Path ?? default,
            aside,
            local?.ContentHash,
            operation.ServerContentHash,
            operation.ServerUpdatedAt,
            operation.Reason ?? "both this device and the server changed this slot since the last sync.");
    }

    /// <summary>
    /// Records a restored save as being in step with the server.
    /// </summary>
    /// <remarks>
    /// Written with <c>uploaded_content_hash</c> equal to what is now on disk, because both
    /// sides hold the same bytes: without that the next scan would read the file as unsent and
    /// offer it straight back up, and eviction would refuse the game forever.
    /// </remarks>
    private void RecordRestored(SyncOperation operation, RelativePath destination, LocalSave? previous)
    {
        var now = _time.GetUtcNow();
        var absolute = _install.Resolve(destination);
        var info = new FileInfo(absolute);
        var hash = LogicalContentHash.OfFile(absolute);

        // saves/<system>/..., which the schema's CHECK already guarantees is the shape.
        var segments = destination.Value.Split('/');

        _store.Saves.Record(
            new LocalSave
            {
                Path = destination,
                System = previous?.System ?? (segments.Length > 1 ? segments[1] : "unknown"),
                Emulator = previous?.Emulator ?? operation.Emulator ?? SaveShapes.Bundled.LooseEmulator,
                ShapeClass = previous?.ShapeClass ?? SaveShapeClass.A,
                Slot = operation.Slot ?? previous?.Slot ?? string.Empty,
                RomId = operation.RomId,
                RomPath = previous?.RomPath,
                ContentHash = hash,
                SizeBytes = info.Length,
                FileMtimeUtc = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
                UploadedContentHash = hash,
                UploadedAtUtc = now,
            },
            now);
    }

    private static void SafeDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // One stale partial file, which the next attempt truncates anyway.
        }
    }
}
