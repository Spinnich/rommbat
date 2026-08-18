using RomM.Client;
using RomMBat.Core.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.Store;

namespace RomMBat.Core.Sync;

/// <summary>What resolving one conflict did.</summary>
public sealed record ConflictResolutionOutcome(bool Resolved, string Message)
{
    public static ConflictResolutionOutcome Failed(string message) => new(false, message);
}

/// <summary>
/// Carries out the choice a user made about a conflicted slot.
/// </summary>
/// <remarks>
/// <b>This is the half of the milestone's "done when" that stage 1 could not carry.</b> The plan
/// ends on "the newer save comes back down as a conflict <i>the user resolves</i>", and stage 1
/// detected the conflict, copied the local file aside and had nowhere to put the decision. See
/// issue #31.
/// <para>
/// <b><c>overwrite=true</c> is used here and nowhere else.</b> A conflict means this device's
/// sync record is stale for the slot, so an ordinary upload is refused with a 409. Retrying with
/// overwrite replaces the server's row in place rather than appending, which is correct only
/// once a person has chosen to discard what was there, and is exactly why stage 1 declined to
/// do it automatically: appending would have made the local side newest and told every other
/// device to take it, resolving the conflict silently in favour of whoever synced last.
/// </para>
/// <para>
/// <b>Nothing is discarded either way.</b> Keeping the server's copy runs the same verified,
/// atomic restore an ordinary download does, and the local file it replaces was already copied
/// aside when the conflict was first seen. Keeping the local copy leaves the server's previous
/// row in the slot's history, which <c>autocleanup_limit=10</c> bounds.
/// </para>
/// </remarks>
public sealed class SaveConflictResolver
{
    private readonly RetroBatInstall _install;
    private readonly LocalStore _store;
    private readonly RomMConnection _connection;
    private readonly string _deviceId;
    private readonly TimeProvider _time;
    private readonly SaveUnitScanner _units;

    public SaveConflictResolver(
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
        _units = new SaveUnitScanner(install);
    }

    /// <summary>
    /// Finishes a keep-server resolution for a bundled unit.
    /// </summary>
    /// <remarks>
    /// Everything that differs from the single-file path is here rather than branched through
    /// it, because the two verify differently and mixing them is what produced a resolution that
    /// could never succeed. The restore itself is the same helper the ordinary sync path uses,
    /// so the atomicity and the copy-aside cannot drift between the two callers.
    /// </remarks>
    private async Task<ConflictResolutionOutcome> FinishUnitAsync(
        SaveConflictRecord conflict,
        LocalSave unitRow,
        int saveId,
        string part,
        CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();

        var restored = SaveUnitTransfer.Restore(
            _install,
            _units,
            unitRow,
            part,
            _install.Resolve(SaveSync.PartialDirectory),
            SaveSync.AsideDirectory,
            now);

        Delete(part);

        var ack = await _connection.AcknowledgeSaveAsync(saveId, _deviceId, cancellationToken)
            .ConfigureAwait(false);

        var refreshed = SaveUnitTransfer.Find(_units, unitRow);

        _store.Saves.Record(
            unitRow with
            {
                ContentHash = restored.ContentHash,
                SizeBytes = refreshed?.SizeBytes ?? unitRow.SizeBytes,
                FileMtimeUtc = refreshed?.NewestMtimeUtc ?? unitRow.FileMtimeUtc,

                // Both sides now hold the same contents, so the next scan must not read this as
                // unsent and offer it straight back up.
                UploadedContentHash = restored.ContentHash,
                UploadedAtUtc = now,
            },
            now);

        // The slot's new server identity travels with the unit, for the same reason the
        // download path records it: the wire hash for an unchanged bundled save is the server's
        // digest, and a slot left holding the old one negotiates as `upload` next flush.
        _store.SaveSlots.RecordRestored(
            conflict.RomId,
            conflict.Slot,
            saveId,
            conflict.ServerHash,
            conflict.ServerUpdatedAt,
            now);

        _store.SaveConflicts.Resolve(conflict.RomId, conflict.Slot, ConflictResolution.KeepServer, now);
        var pruned = Prune(conflict);

        var warning = ack.IsSuccess
            ? string.Empty
            : $" The server was not told it arrived: {ack.Message}";

        return new ConflictResolutionOutcome(
            true,
            $"Took the server's copy into {unitRow.Path}/{unitRow.UnitKey}, "
                + $"{restored.Entries.Count} files.{pruned}{warning}");
    }

    /// <summary>Resolves one slot the way the user asked.</summary>
    public async Task<ConflictResolutionOutcome> ResolveAsync(
        long romId,
        string slot,
        ConflictResolution resolution,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slot);

        if (_store.SaveConflicts.Read(romId, slot) is not { } conflict)
        {
            return ConflictResolutionOutcome.Failed(
                $"There is no conflict recorded for rom {romId} slot {slot}.");
        }

        if (!conflict.IsOpen)
        {
            return ConflictResolutionOutcome.Failed(
                $"That conflict was already resolved on {conflict.ResolvedAtUtc:u}.");
        }

        try
        {
            return resolution == ConflictResolution.KeepLocal
                ? await KeepLocalAsync(conflict, cancellationToken).ConfigureAwait(false)
                : await KeepServerAsync(conflict, cancellationToken).ConfigureAwait(false);
        }
        catch (RomMUnreachableException ex)
        {
            // The conflict stays open and stays recorded, so the decision can be made again
            // when the server is back rather than being half applied.
            return ConflictResolutionOutcome.Failed($"The server is not reachable: {ex.Message}");
        }
    }

    private async Task<ConflictResolutionOutcome> KeepLocalAsync(
        SaveConflictRecord conflict,
        CancellationToken cancellationToken)
    {
        var save = _store.Saves.List(conflict.RomId)
            .FirstOrDefault(row => string.Equals(row.Slot, conflict.Slot, StringComparison.Ordinal));

        if (save is null)
        {
            return ConflictResolutionOutcome.Failed(
                $"This device no longer holds a save in slot {conflict.Slot}, so there is nothing "
                    + "local to keep. Delete the conflict or take the server's copy instead.");
        }

        var path = _install.Resolve(save.Path);
        var isUnit = save.ShapeClass == RetroBat.SaveShapeClass.C;

        if (isUnit ? !Directory.Exists(path) : !File.Exists(path))
        {
            return ConflictResolutionOutcome.Failed($"{save.Path} is gone, so there is nothing to send.");
        }

        // <b>A class C row's path is a container, not a file.</b> Opening it as one failed with
        // "is gone, so there is nothing to send", which is both wrong and misleading, and the
        // hands-on pass hit it on the first PSP conflict.
        string? bundle = null;
        Stream content;
        var name = save.Path.Name;

        if (isUnit)
        {
            var unit = SaveUnitTransfer.Find(_units, save);

            if (unit is null)
            {
                return ConflictResolutionOutcome.Failed(
                    $"{save.Path}/{save.UnitKey} is gone, so there is nothing to send.");
            }

            bundle = SaveUnitTransfer.Pack(_install, unit, _install.Resolve(SaveSync.PartialDirectory));
            content = File.OpenRead(bundle);
            name = unit.UploadFileName;
        }
        else
        {
            content = File.OpenRead(path);
        }

        RomMResponse<SaveUploadResult> response;

        try
        {
            await using var stream = content;

            response = await _connection.UploadSaveAsync(
                (int)conflict.RomId,
                conflict.Slot,
                save.Emulator,
                _deviceId,
                sessionId: null,
                name,
                stream,

                // The one place this is true, and only because a person asked for it.
                overwrite: true,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (bundle is not null && File.Exists(bundle))
            {
                File.Delete(bundle);
            }
        }

        if (!response.IsSuccess || response.Value is not { } result)
        {
            return ConflictResolutionOutcome.Failed($"The upload failed: {response.Message}");
        }

        if (result.Conflict)
        {
            // An overwrite that still 409s means the slot moved again between the report and
            // the decision, so the user is choosing against something they never saw.
            return ConflictResolutionOutcome.Failed(
                "The slot moved again since this conflict was reported, so nothing was sent. "
                    + "Flush again to see what it is now.");
        }

        var now = _time.GetUtcNow();

        if (result.Save is { } row)
        {
            _store.SaveSlots.Record(row, now);
        }

        if (save.ContentHash is { } hash)
        {
            _store.Saves.MarkUploaded(save.Path, save.UnitKey, hash, now);
        }

        _store.SaveConflicts.Resolve(conflict.RomId, conflict.Slot, ConflictResolution.KeepLocal, now);
        var pruned = Prune(conflict);

        // Not "replaced the server's copy". overwrite=true gets past the 409 and does not
        // replace the row: the server tags a slotted upload with the current second and keys the
        // row on that name, so a decision taken later than the same second appends. The older
        // copy is still there, one row down, and autocleanup bounds the slot at ten.
        return new ConflictResolutionOutcome(
            true,
            $"Kept this device's {save.Path} and sent it as the newest copy in the slot." + pruned);
    }

    private async Task<ConflictResolutionOutcome> KeepServerAsync(
        SaveConflictRecord conflict,
        CancellationToken cancellationToken)
    {
        if (conflict.ServerSaveId is not { } saveId)
        {
            return ConflictResolutionOutcome.Failed(
                "The conflict was recorded without a server save id, so there is nothing to "
                    + "fetch. Flush again and resolve it from the new report.");
        }

        var target = conflict.LocalPath.HasValue
            ? conflict.LocalPath
            : _store.SaveSlots.Read(conflict.RomId, conflict.Slot)?.OnDiskPath;

        if (target is not { } destination)
        {
            return ConflictResolutionOutcome.Failed(
                "There is nowhere to write the server's copy: this device holds no save in that "
                    + "slot and the slot has no recorded name.");
        }

        var partialDirectory = _install.Resolve(SaveSync.PartialDirectory);
        var part = Path.Combine(partialDirectory, $"resolve-{saveId}.part");

        Directory.CreateDirectory(partialDirectory);

        try
        {
            await using (var stream = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var response = await _connection
                    .DownloadSaveAsync(saveId, _deviceId, null, stream, cancellationToken)
                    .ConfigureAwait(false);

                if (!response.IsSuccess)
                {
                    return ConflictResolutionOutcome.Failed($"The download failed: {response.Message}");
                }
            }

            var unitRow = _store.Saves.List(conflict.RomId)
                .FirstOrDefault(row =>
                    row.ShapeClass == RetroBat.SaveShapeClass.C
                    && string.Equals(row.Slot, conflict.Slot, StringComparison.Ordinal));

            if (unitRow is not null)
            {
                // <b>A bundled save cannot be verified the way a file is, and trying is a
                // guaranteed failure rather than a safety net.</b> The server's content_hash for
                // an archive is a digest over its contents by a function this client cannot
                // reproduce, so comparing it against the MD5 of the bytes never matches. Driven:
                // the first real PSP conflict refused itself with "what arrived hashes to
                // 0391c0a9 and the conflict recorded 174b2e82", and nothing was written.
                //
                // The shared restore verifies what can be verified, by CRC per entry, and swaps
                // the unit in atomically with the previous members copied aside.
                return await FinishUnitAsync(conflict, unitRow, saveId, part, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (conflict.ServerHash is { } expected)
            {
                var found = LogicalContentHash.OfFile(part);

                if (!string.Equals(found, expected, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(part);

                    return ConflictResolutionOutcome.Failed(
                        $"What arrived hashes to {found} and the conflict recorded {expected}. "
                            + "Nothing was written.");
                }
            }

            var absolute = _install.Resolve(destination);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.Move(part, absolute, overwrite: true);

            // Only now, with the bytes on disk and checked, exactly as an ordinary download
            // does it. Before this the server still believes the device does not have the save.
            var ack = await _connection.AcknowledgeSaveAsync(saveId, _deviceId, cancellationToken)
                .ConfigureAwait(false);

            var now = _time.GetUtcNow();
            var hash = LogicalContentHash.OfFile(absolute);
            var info = new FileInfo(absolute);
            var previous = _store.Saves.List(conflict.RomId)
                .FirstOrDefault(row => row.Path == destination);

            _store.Saves.Record(
                new LocalSave
                {
                    Path = destination,
                    System = previous?.System ?? Segment(destination),
                    Emulator = previous?.Emulator ?? RetroBat.SaveShapes.Bundled.LooseEmulator,
                    ShapeClass = previous?.ShapeClass ?? RetroBat.SaveShapeClass.A,
                    Slot = conflict.Slot,
                    RomId = conflict.RomId,
                    RomPath = previous?.RomPath,
                    ContentHash = hash,
                    SizeBytes = info.Length,
                    FileMtimeUtc = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),

                    // Both sides now hold the same bytes, so the next scan must not read this
                    // as unsent and offer it straight back up.
                    UploadedContentHash = hash,
                    UploadedAtUtc = now,
                },
                now);

            _store.SaveConflicts.Resolve(conflict.RomId, conflict.Slot, ConflictResolution.KeepServer, now);
            var pruned = Prune(conflict);

            var warning = ack.IsSuccess
                ? string.Empty
                : $" The server was not told it arrived: {ack.Message}";

            return new ConflictResolutionOutcome(
                true,
                $"Took the server's copy into {destination}.{pruned}{warning}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Delete(part);
            return ConflictResolutionOutcome.Failed($"{destination}: it could not be written: {ex.Message}");
        }
    }

    /// <summary>
    /// Removes the copy taken aside, now that the slot is back in step.
    /// </summary>
    /// <remarks>
    /// The plan's rule is "keep the previous copy aside <b>until the next successful sync</b>",
    /// and until this existed nothing was ever the next successful sync for a conflicted slot.
    /// <para>
    /// <b>The row itself stays, resolved.</b> Migration 007 keeps decided rows so <c>saves</c> can
    /// say what was chosen and so a slot that conflicts again is recognised as one already
    /// settled rather than as a brand new conflict taking another copy aside. Only the pointer to
    /// the pruned file is cleared.
    /// </para>
    /// </remarks>
    private string Prune(SaveConflictRecord conflict)
    {
        if (conflict.LocalCopyPath is not { } copy)
        {
            return string.Empty;
        }

        try
        {
            var path = _install.Resolve(copy);
            var existed = File.Exists(path);

            if (existed)
            {
                File.Delete(path);
            }

            _store.SaveConflicts.ForgetCopy(conflict.RomId, conflict.Slot);

            return existed ? $" The copy at {copy} was removed." : string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $" The copy at {copy} could not be removed: {ex.Message}";
        }
    }

    private static void Delete(string path)
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

    /// <summary>The system folder out of <c>saves/&lt;system&gt;/...</c>.</summary>
    private static string Segment(RelativePath path)
    {
        var segments = path.Value.Split('/');
        return segments.Length > 1 ? segments[1] : "unknown";
    }
}
