using RomM.Client;
using RomMBat.Core.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.Store;

namespace RomMBat.Core.Sync;

/// <summary>What resolving one conflict did.</summary>
public sealed record ConflictResolutionOutcome(bool Resolved, string Message)
{
    /// <summary>
    /// Nothing was tried, because the game is being played.
    /// </summary>
    /// <remarks>
    /// Distinct from a failure for the reason <see cref="ConflictOutcomeState.Busy"/> is: the
    /// conflict is still open, nothing on either side moved, and asking again once the game is
    /// closed works. See <see cref="InFlightGuard"/>.
    /// </remarks>
    public bool IsDeferred { get; init; }

    public static ConflictResolutionOutcome Failed(string message) => new(false, message);

    public static ConflictResolutionOutcome Deferred(string message) =>
        new(false, message) { IsDeferred = true };
}

/// <summary>
/// Carries out the choice a user made about a conflicted slot.
/// </summary>
/// <remarks>
/// <b>A conflict comes back down as one <i>the user resolves</i>.</b> Detection copies the local
/// file aside and records the conflict; this is where the decision lands (#31).
/// <para>
/// <b><c>overwrite=true</c> is used here and nowhere else.</b> A conflict means this device's
/// sync record is stale for the slot, so an ordinary upload is refused with a 409. Retrying with
/// overwrite is what gets past that refusal, and it is correct only once a person has chosen
/// which side to keep. That is exactly why the flush never does it automatically: uploading
/// unasked would make the local side newest and told every other device to take it,
/// resolving the conflict silently in favour of whoever synced last.
/// </para>
/// <para>
/// <b>What it does not do is replace the server's row in place.</b> Measured on the live
/// instance: a keep-local on a psp class C unit left save id 187 standing and created id 193
/// beside it, one second after the local record was written. So <c>overwrite</c> means
/// "supersede", not "overwrite", and the server keeps the previous row as history.
/// </para>
/// <para>
/// <b>Nothing is discarded either way.</b> Keeping the server's copy runs the same verified
/// restore an ordinary download does, and the local side it replaces stays under
/// <c>replaced/</c>: the copy taken when the conflict was first seen, and a fresh one when the
/// save moved since. Keeping the local copy leaves the server's previous row in the slot's
/// history, which <c>autocleanup_limit=10</c> bounds. Confirmed against the live instance rather
/// than assumed: two rows for the slot afterwards, the older untouched.
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
    private readonly InFlightGuard _inFlight;

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
        _inFlight = new InFlightGuard(install, store);
    }

    /// <summary>
    /// Finishes a keep-server resolution for a bundled unit.
    /// </summary>
    /// <remarks>
    /// Everything that differs from the single-file path is here rather than branched through
    /// it, because the two verify differently and mixing them is what produced a resolution that
    /// could never succeed. The restore itself is the same helper the ordinary sync path uses,
    /// so the staging, the copy-aside and the rollback cannot drift between the two callers.
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
            now,
            saveId == conflict.ServerSaveId ? conflict.ServerHash : null);

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
        // download path records it: the save id a later download is compared against is the
        // row that just came down.
        _store.SaveSlots.RecordRestored(
            conflict.RomId,
            conflict.Slot,
            saveId,
            conflict.ServerHash,
            conflict.ServerUpdatedAt,
            now);

        _store.SaveConflicts.Resolve(conflict.RomId, conflict.Slot, ConflictResolution.KeepServer, now);
        var kept = Release(conflict, restored.CopiedAside);

        var warning = ack.IsSuccess
            ? string.Empty
            : $" The server was not told it arrived: {ack.Message}";

        return new ConflictResolutionOutcome(
            true,
            $"Took the server's copy into {unitRow.Path}/{unitRow.UnitKey}, "
                + $"{restored.Entries.Count} files.{kept}{warning}");
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
        var save = FindLocal(conflict);

        if (save is null || !IsOnDisk(save))
        {
            if (SaveSync.NotASave(conflict.ServerHash) is not null)
            {
                return CloseWithNothingToKeep(conflict, ConflictResolution.KeepLocal);
            }

            return ConflictResolutionOutcome.Failed(
                save is null
                    ? $"This device no longer holds a save in slot {conflict.Slot}, so there is "
                        + "nothing local to keep. Take the server's copy instead."
                    : $"{save.Path} is gone, so there is nothing to send. Take the server's copy instead.");
        }

        var path = _install.Resolve(save.Path);
        var isUnit = save.ShapeClass == RetroBat.SaveShapeClass.C;

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
                    $"{save.Path}/{save.UnitKey} is gone, so there is nothing to send. Take the "
                        + "server's copy instead.");
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

    /// <summary>The local save a conflict is about, or null when this device holds none.</summary>
    /// <remarks>
    /// By slot first, then by the file the conflict named. A conflict recorded because another
    /// slot's download would have landed on this device's save (#205) is keyed on the slot the
    /// server offered, which this device holds no row for: its local side is the file, and
    /// without this fallback the only way out of such a conflict would be taking the server's
    /// copy.
    /// </remarks>
    private LocalSave? FindLocal(SaveConflictRecord conflict) =>
        _store.Saves.List(conflict.RomId)
            .FirstOrDefault(row => string.Equals(row.Slot, conflict.Slot, StringComparison.Ordinal))
        ?? _store.Saves.List(conflict.RomId)
            .FirstOrDefault(row => row.Path == conflict.LocalPath);

    /// <remarks>
    /// A class C row is on disk when its unit is, not its container: the container is shared and
    /// outlives every unit in it.
    /// </remarks>
    private bool IsOnDisk(LocalSave save) =>
        save.ShapeClass == RetroBat.SaveShapeClass.C
            ? SaveUnitTransfer.Find(_units, save) is not null
            : File.Exists(_install.Resolve(save.Path));

    /// <summary>
    /// Closes a conflict neither side of which holds a save, whichever side was asked for.
    /// </summary>
    /// <remarks>
    /// <b>Otherwise it could never close.</b> Keeping the local side needs a file to send, and
    /// keeping the server's refuses a copy that is not a save, so a conflict whose local file was
    /// removed against a server <c>null</c> was reported on every flush with no answer that
    /// worked (RB-276). The copy set aside when it was recorded is left where it is, since
    /// it may be the only trace of the local side, and the message names it.
    /// </remarks>
    private ConflictResolutionOutcome CloseWithNothingToKeep(
        SaveConflictRecord conflict,
        ConflictResolution resolution)
    {
        _store.SaveConflicts.Resolve(conflict.RomId, conflict.Slot, resolution, _time.GetUtcNow());

        var copy = conflict.LocalCopyPath is { } kept && File.Exists(_install.Resolve(kept))
            ? $" The copy taken when it was recorded is still at {kept}."
            : string.Empty;

        return new ConflictResolutionOutcome(
            true,
            $"Closed the conflict with nothing written: this device no longer holds a save in slot "
                + $"{conflict.Slot}, and the server's copy is not a save.{copy}");
    }

    private static ConflictResolutionOutcome NotASaveKept(string reason) =>
        ConflictResolutionOutcome.Failed(
            $"Nothing was written, because {reason} The conflict is still open, and keeping this "
                + "device's copy settles it.");

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

        // The same question a download asks, on the route docs/architecture/projects.md pairs with it: this
        // writes the server's copy into the tree, and the class C half swaps unit members into a
        // container a running emulator holds open. Asked before the transfer, so a deferral
        // costs nothing on the wire either.
        if (_inFlight.Check((int)conflict.RomId, destination) is { CanWrite: false } verdict)
        {
            return ConflictResolutionOutcome.Deferred(
                $"Nothing was written, because {verdict.Reason}. The conflict is still open and "
                    + "still says the same thing. Close the game, then resolve it again.");
        }

        // A conflict can still carry one: the server's own conflict action and a 409 record it,
        // because the local side is real and keeping it is the answer.
        if (SaveSync.NotASave(conflict.ServerHash) is { } refused)
        {
            return FindLocal(conflict) is { } local && IsOnDisk(local)
                ? NotASaveKept(refused)
                : CloseWithNothingToKeep(conflict, ConflictResolution.KeepServer);
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

            if (new FileInfo(part).Length == SaveSync.NullPayload.Length
                && SaveSync.NotASave(LogicalContentHash.OfFile(part)) is { } arrived)
            {
                File.Delete(part);

                return FindLocal(conflict) is { } local && IsOnDisk(local)
                    ? NotASaveKept(arrived)
                    : CloseWithNothingToKeep(conflict, ConflictResolution.KeepServer);
            }

            var unitRow = _store.Saves.List(conflict.RomId)
                .FirstOrDefault(row =>
                    row.ShapeClass == RetroBat.SaveShapeClass.C
                    && string.Equals(row.Slot, conflict.Slot, StringComparison.Ordinal));

            if (unitRow is not null)
            {
                // <b>A bundled save is not verified the way a file is.</b> The server's
                // content_hash for an archive is a digest over its entries, not the MD5 of the
                // bytes. Driven: the first real PSP conflict refused itself with "what arrived
                // hashes to 0391c0a9 and the conflict recorded 174b2e82", and nothing was written.
                //
                // The shared restore checks the entry digest and every entry's CRC, and swaps
                // the unit in with the previous members copied aside. Per member rather than
                // whole, since the container is shared, and rolled back if it fails partway.
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
            var aside = CopyAsideIfMoved(conflict, destination);
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

            // The save just taken is the slot's server identity now, as FinishUnitAsync records
            // for a unit. Left alone, save_slot names the copy this device kept before (#157).
            _store.SaveSlots.RecordRestored(
                conflict.RomId,
                conflict.Slot,
                saveId,
                conflict.ServerHash,
                conflict.ServerUpdatedAt,
                now);

            _store.SaveConflicts.Resolve(conflict.RomId, conflict.Slot, ConflictResolution.KeepServer, now);
            var kept = Release(conflict, aside);

            var warning = ack.IsSuccess
                ? string.Empty
                : $" The server was not told it arrived: {ack.Message}";

            return new ConflictResolutionOutcome(
                true,
                $"Took the server's copy into {destination}.{kept}{warning}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Delete(part);
            return ConflictResolutionOutcome.Failed($"{destination}: it could not be written: {ex.Message}");
        }
        catch (Exception ex) when (ex is SaveUnitMismatchException or InvalidDataException)
        {
            // A unit that is not the save offered, or an archive that would not unpack. The live
            // tree is untouched, since the restore checks both before replacing anything.
            Delete(part);
            return ConflictResolutionOutcome.Failed($"{destination}: {ex.Message}. Nothing was written.");
        }
    }

    /// <summary>
    /// Copies the local save aside before the server's copy replaces it, unless the conflict's
    /// copy already holds the same bytes.
    /// </summary>
    /// <remarks>
    /// The conflict's copy is taken once, when the conflict is first seen, so anything played
    /// since is on no copy. It may also never have been taken, or been deleted by hand. Either
    /// way the overwrite needs a copy of what is there now, and fails rather than going ahead
    /// without one, as a download does.
    /// </remarks>
    private RelativePath? CopyAsideIfMoved(SaveConflictRecord conflict, RelativePath destination)
    {
        var absolute = _install.Resolve(destination);

        if (!File.Exists(absolute))
        {
            return null;
        }

        if (conflict.LocalCopyPath is { } copy
            && File.Exists(_install.Resolve(copy))
            && string.Equals(
                LogicalContentHash.OfFile(_install.Resolve(copy)),
                LogicalContentHash.OfFile(absolute),
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Suffixed rather than overwritten when the name is taken: a decision in the same second
        // as the flush that found the conflict would otherwise replace the conflict's own copy.
        var stamp = $"{_time.GetUtcNow():yyyyMMddTHHmmss}";
        var aside = SaveSync.AsideDirectory.Combine($"{stamp}-{destination.Name}");

        for (var n = 2; File.Exists(_install.Resolve(aside)); n++)
        {
            aside = SaveSync.AsideDirectory.Combine($"{stamp}-{n}-{destination.Name}");
        }

        try
        {
            var asidePath = _install.Resolve(aside);
            Directory.CreateDirectory(Path.GetDirectoryName(asidePath)!);
            File.Copy(absolute, asidePath);
            return aside;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException(
                $"the existing save could not be copied aside, so it was not replaced: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Lets go of the conflict's copy after a keep-server, leaving the file where it is, and
    /// says where this device's side now lives.
    /// </summary>
    /// <remarks>
    /// <b>Kept, because nowhere else holds it.</b> The slot was in conflict, so the local side
    /// never reached RomM, and the server's copy has just replaced it here. Like a download's
    /// copy aside it stays under <c>replaced/</c> and nothing prunes it. The pointer is cleared
    /// all the same, so a slot that conflicts again takes a copy of its own rather than
    /// inheriting this one (#326).
    /// </remarks>
    private string Release(SaveConflictRecord conflict, RelativePath? fresh)
    {
        _store.SaveConflicts.ForgetCopy(conflict.RomId, conflict.Slot);

        var kept = new[] { fresh, conflict.LocalCopyPath }
            .OfType<RelativePath>()
            .Distinct()
            .Where(path => File.Exists(_install.Resolve(path)) || Directory.Exists(_install.Resolve(path)))
            .Select(path => path.Value)
            .ToList();

        return kept.Count switch
        {
            0 => string.Empty,
            1 => $" This device's save is kept at {kept[0]}.",
            _ => $" This device's save is kept at {kept[0]}, and as it was when the conflict was "
                + $"found at {kept[1]}.",
        };
    }

    /// <summary>
    /// Removes the copy taken aside after a keep-local, now that the slot is back in step.
    /// </summary>
    /// <remarks>
    /// The rule is "keep the previous copy aside <b>until the next successful sync</b>". After a
    /// keep-local the copy holds the side that was kept and sent, and the server's side stays in
    /// the slot's history, so nothing is lost by removing it. A keep-server keeps its copy
    /// instead: see <see cref="Release"/>.
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

        // Forgotten before the delete, which can fail partway through a unit's members: a pointer
        // left to what remains would be reused by the slot's next conflict in place of a copy.
        _store.SaveConflicts.ForgetCopy(conflict.RomId, conflict.Slot);

        try
        {
            var path = _install.Resolve(copy);

            // A class C unit's copy is a directory of its members.
            var existed = File.Exists(path) || Directory.Exists(path);

            if (File.Exists(path))
            {
                File.Delete(path);
            }
            else if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }

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

    /// <summary>The system a save path belongs to, which <see cref="RetroBat.SaveShapes.SystemOf"/> reads.</summary>
    private static string Segment(RelativePath path) =>
        RetroBat.SaveShapes.Bundled.SystemOf(path) ?? "unknown";
}
