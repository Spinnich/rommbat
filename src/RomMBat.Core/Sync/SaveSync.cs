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
/// is a corrupt save.
/// </para>
/// <para>
/// The copies under <c>emulators/rommbat/replaced/</c> are kept indefinitely and nothing prunes
/// them, so a slot that conflicts on every flush accumulates one dated copy per run. Pruning
/// belongs with the resolution command that ends the conflict, which is issue #31.
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
    private readonly SaveUnitScanner _units;

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
        _units = new SaveUnitScanner(install);
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

        // Slots are the pairing key, so two rows on one (rom_id, slot) have nothing to
        // negotiate between them: the server would be told about the slot twice and answer
        // once. Reported and skipped rather than thrown on, because discovering an attribution
        // fault must not take the rest of the library's saves down with it.
        var byKey = new Dictionary<(long RomId, string Slot), LocalSave>();
        var problems = new List<string>();
        var failed = 0;

        foreach (var save in saves)
        {
            if (byKey.TryAdd((save.RomId!.Value, save.Slot), save))
            {
                continue;
            }

            failed++;
            problems.Add(
                $"{save.Path}: slot {save.Slot} on rom {save.RomId} is already held by "
                    + $"{byKey[(save.RomId!.Value, save.Slot)].Path}, so it was not sent.");
        }

        var request = new NegotiateRequest(
            _deviceId,
            [.. byKey.Values.Select(save => new NegotiateSave(
                (int)save.RomId!.Value,
                NameFor(save),
                save.Slot,
                save.Emulator,
                WireHash(save),

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
            problems.Add(ex.Message);
            return new SaveSyncOutcome { Failed = failed + byKey.Count, Problems = problems };
        }

        if (!negotiated.IsSuccess || negotiated.Value is not { } result)
        {
            problems.Add(negotiated.Message ?? "negotiate failed");
            return new SaveSyncOutcome { Failed = failed + byKey.Count, Problems = problems };
        }

        var uploaded = 0;
        var downloaded = 0;
        var noOps = 0;
        var bytes = 0L;
        var conflicts = new List<SaveConflict>();

        // What each upload did, so siblings of one save can be reported together below.
        var sent = new List<(long RomId, string Slot, bool Ok)>();

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
                    var attempt = await UploadAsync(local, result.SessionId, cancellationToken)
                        .ConfigureAwait(false);

                    if (attempt.Conflicted)
                    {
                        // <b>A 409 is a conflict, not a failure, and this is where the two
                        // meet.</b> Negotiate decides from the hashes it was given and answers
                        // `upload` whenever the client's mtime is newer; the server then refuses
                        // if THIS device's sync record is stale, which is the case negotiate
                        // could not see. Driven on real hardware: a save changed on both sides
                        // negotiated as `upload` and came back 409, so without this the only
                        // route to a conflict would be the one negotiate happens to name, and a
                        // real divergence would report as a bare failure with nothing to settle.
                        //
                        // The operation carries save_id, server_content_hash and
                        // server_updated_at even on an `upload`, so the record is complete.
                        conflicts.Add(RecordConflict(operation, local));
                        sent.Add((local.RomId!.Value, local.Slot, false));
                    }
                    else if (attempt.Problem is { } uploadProblem)
                    {
                        failed++;
                        problems.Add(uploadProblem);
                        sent.Add((local.RomId!.Value, local.Slot, false));
                    }
                    else
                    {
                        uploaded++;
                        sent.Add((local.RomId!.Value, local.Slot, true));
                    }

                    break;

                case SyncAction.Download when operation.SaveId is { } saveId:
                    if (AlreadyHeld(operation, local))
                    {
                        noOps++;
                        break;
                    }

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

                // Guarded like Upload, and for a harder reason: save_conflict.local_path is NOT
                // NULL, so recording a conflict for a slot this device did not submit would fail
                // the CHECK and take the whole flush down with it. It falls to default instead,
                // which says exactly that.
                case SyncAction.Conflict when local is not null:
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

        problems.AddRange(DescribePartialBatches(sent));

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

    /// <summary>
    /// Names any save whose siblings did not all land, as one batch rather than as parts.
    /// </summary>
    /// <remarks>
    /// <b>This is what <c>outbox.batch_key</c> was for, delivered without the column.</b> Class
    /// B takes one slot per file, so saturn's <c>.bcr</c> and <c>.bkr</c> are two rows
    /// describing one save, and a flush that lands one and fails the other otherwise reports two
    /// independent results where each looks fine on its own.
    /// <para>
    /// The column stays unwritten, and migration 006's header, which expected class C to give it
    /// a second caller, is wrong about that: a class C unit is one (container, key) pair and
    /// bundles to one archive, one slot and one upload, so it never supplies a second row to
    /// tie. Class B's siblings are the only real batch, and <c>SaveSync</c> already holds them
    /// all in one map, so grouping here needs no queue and does not disturb the upload path
    /// stage 1 proved.
    /// </para>
    /// <para>
    /// Only partial batches are named. A batch that landed whole is the ordinary case and a
    /// batch that failed whole is already one message per file saying the same thing.
    /// </para>
    /// </remarks>
    private static IEnumerable<string> DescribePartialBatches(List<(long RomId, string Slot, bool Ok)> sent) =>
        sent
            .GroupBy(entry => (entry.RomId, Batch: BatchKeyFor(entry.Slot)))
            .Where(batch => batch.Count() > 1 && batch.Any(entry => entry.Ok) && batch.Any(entry => !entry.Ok))
            .Select(batch =>
                $"rom {batch.Key.RomId}: {batch.Count(entry => entry.Ok)} of {batch.Count()} files in "
                    + $"the {batch.Key.Batch} save reached the server. They are one save, so the "
                    + "next flush sends the rest; until then the server holds a partial one.");

    /// <summary>
    /// What ties two slots into one save.
    /// </summary>
    /// <remarks>
    /// Class B's slot is <c>{emulator}:battery:{ext}</c>, so dropping the extension leaves
    /// <c>{emulator}:battery</c>, which every file of one save shares and nothing else does.
    /// Any other slot is its own batch of one.
    /// </remarks>
    private static string BatchKeyFor(string slot)
    {
        var separator = slot.LastIndexOf(':');

        return separator > 0 && slot.AsSpan(0, separator).EndsWith(":battery", StringComparison.Ordinal)
            ? slot[..separator]
            : slot;
    }

    /// <summary>
    /// The hash to put on the wire for a save, which is not always the one on the row.
    /// </summary>
    /// <remarks>
    /// <b>Class C carries two hashes and sending the wrong one uploads forever.</b> Measured:
    /// RomM's <c>content_hash</c> is the MD5 of the bytes for a plain file, and for an archive
    /// it is a digest over the archive's <i>contents</i> computed by a function this client
    /// cannot reproduce. Eight candidate reconstructions matched none of the observed values.
    /// <para>
    /// So the logical fold is the <b>local change detector</b> and the digest the server
    /// returned on the last upload is the <b>wire value</b>. Driven against a live instance:
    /// sending the server's own digest answers <c>no_op (Content is identical)</c>, while
    /// sending the fold or the archive's MD5 answers <c>download (Server save is newer)</c>.
    /// </para>
    /// <para>
    /// A unit whose fold has moved since the upload is deliberately sent with the fold, which
    /// cannot match anything the server holds, so negotiate answers <c>upload</c>. That is the
    /// intended outcome and not a coincidence: the client already knows the contents changed,
    /// and the server has no way to be told so in its own vocabulary.
    /// </para>
    /// </remarks>
    private string? WireHash(LocalSave save)
    {
        if (save.ShapeClass != SaveShapeClass.C)
        {
            return save.ContentHash;
        }

        var unchanged = save.ContentHash is not null
            && string.Equals(save.ContentHash, save.UploadedContentHash, StringComparison.OrdinalIgnoreCase);

        if (!unchanged)
        {
            return save.ContentHash;
        }

        return _store.SaveSlots.Read(save.RomId!.Value, save.Slot)?.ServerContentHash ?? save.ContentHash;
    }

    /// <summary>The file name a save is negotiated and uploaded under.</summary>
    /// <remarks>
    /// For class A and B the file's own name. For class C the unit key plus <c>.zip</c>, so the
    /// untagged name the server hands back is the key itself: <c>UCES01011.zip</c> came back as
    /// <c>UCES01011 [2026-08-17_23-52-18].zip</c> with <c>file_name_no_tags</c> of
    /// <c>UCES01011</c>. Nothing depends on this matching, since negotiate pairs on the slot,
    /// but a name that means something is worth more than one that does not.
    /// </remarks>
    private static string NameFor(LocalSave save) =>
        save.ShapeClass == SaveShapeClass.C ? $"{save.UnitKey}.zip" : save.Path.Name;

    /// <summary>
    /// Sends one save, and says whether a refusal was a conflict or an ordinary failure.
    /// </summary>
    /// <remarks>
    /// The two are different events with different remedies. A failure is retried by the next
    /// flush and costs nothing; a conflict needs a person to choose a side, so it is persisted
    /// and reported rather than retried, and <c>saves resolve</c> is the only thing that ends it.
    /// </remarks>
    private async Task<(bool Conflicted, string? Problem)> UploadAsync(
        LocalSave save,
        int sessionId,
        CancellationToken cancellationToken)
    {
        var path = _install.Resolve(save.Path);
        var isUnit = save.ShapeClass == SaveShapeClass.C;

        if (isUnit ? !Directory.Exists(path) : !File.Exists(path))
        {
            return (false, $"{Describe(save)}: it is gone since the scan.");
        }

        string? bundle = null;

        try
        {
            Stream content;

            if (isUnit)
            {
                // Rebuilt from the tree rather than from the scan's record, so a unit that
                // gained a member between the scan and the flush goes up whole. The hash is
                // re-taken with it for the same reason.
                var unit = SaveUnitTransfer.Find(_units, save);

                if (unit is null)
                {
                    return (false, $"{Describe(save)}: it is gone since the scan.");
                }

                bundle = SaveUnitTransfer.Pack(_install, unit, _install.Resolve(PartialDirectory));
                content = File.OpenRead(bundle);
            }
            else
            {
                content = File.OpenRead(path);
            }

            await using var stream = content;

            var response = await _connection.UploadSaveAsync(
                (int)save.RomId!.Value,
                save.Slot,
                save.Emulator,
                _deviceId,
                sessionId,
                NameFor(save),
                stream,
                overwrite: false,
                cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccess || response.Value is not { } result)
            {
                return (false, $"{Describe(save)}: {response.Message}");
            }

            if (result.Conflict)
            {
                // The server refused because this device's record is stale. Never retried with
                // overwrite here, which would discard whatever moved; the caller records it as a
                // conflict and `saves resolve --keep-local` is the only caller of overwrite.
                return (true, null);
            }

            if (result.Save is { } row)
            {
                // The server's identity, which is the tagged name, alongside the untagged one.
                // For class C this row also carries the only value negotiate will accept back
                // as "unchanged", since the server's archive digest cannot be recomputed here.
                _store.SaveSlots.Record(row, _time.GetUtcNow());
            }

            // The logical fold, never the server's digest. This is the local record of what was
            // sent, and it is the value a later scan compares the tree against.
            _store.Saves.MarkUploaded(save.Path, save.UnitKey, save.ContentHash!, _time.GetUtcNow());
            return (false, null);
        }
        catch (RomMUnreachableException ex)
        {
            return (false, $"{Describe(save)}: {ex.Message}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (false, $"{Describe(save)}: it could not be read: {ex.Message}");
        }
        finally
        {
            if (bundle is not null)
            {
                SafeDelete(bundle);
            }
        }
    }

    /// <summary>
    /// True when the save the server is offering is one this device already has on disk.
    /// </summary>
    /// <remarks>
    /// <c>origin_device_id</c> names the uploader, so a save this device sent that is being
    /// offered back is bytes this device already holds. Skipped rather than fetched, which is
    /// the cheapest saving in the protocol and the reason the field is persisted at all.
    /// <para>
    /// <b>Two questions, and the shape decides which hash answers the first.</b> A class A or B
    /// save's <c>content_hash</c> is the MD5 of its bytes, which is exactly what the server
    /// holds for a plain file, so the local fold and the wire value are the same function and
    /// comparing them settles it. For a bundled unit they are two different functions by
    /// construction: the fold is over the unit's contents and the server's is a digest over the
    /// archive, measured as not reproducible client-side. They are never equal, so the original
    /// single comparison was always false for class C and the download always ran.
    /// </para>
    /// <para>
    /// So a bundled unit is asked in the server's own vocabulary instead, which needs both
    /// halves rather than one. The slot's recorded <c>server_content_hash</c> against the
    /// operation's says the server is offering back the save this device last exchanged, and
    /// <see cref="LocalSave.HasChangedSinceUpload"/> says the tree still holds what went up.
    /// Either alone would skip a download that was needed: the first cannot see a unit edited
    /// since, and the second cannot see the server moving on.
    /// </para>
    /// </remarks>
    private bool AlreadyHeld(SyncOperation operation, LocalSave? local)
    {
        if (local is null || operation.ServerContentHash is not { } offered)
        {
            return false;
        }

        var slot = operation.Slot ?? string.Empty;

        if (local.ShapeClass == SaveShapeClass.C)
        {
            // A null content hash is a unit something held open, which is never evidence that
            // the tree matches anything.
            if (local.ContentHash is null || local.IsUnsent || local.HasChangedSinceUpload)
            {
                return false;
            }

            var recorded = _store.SaveSlots.Read(operation.RomId, slot)?.ServerContentHash;

            return recorded is not null
                && string.Equals(recorded, offered, StringComparison.OrdinalIgnoreCase)
                && _store.SaveSlots.IsOwnUpload(operation.RomId, slot, _deviceId);
        }

        return local.ContentHash is { } held
            && string.Equals(held, offered, StringComparison.OrdinalIgnoreCase)
            && _store.SaveSlots.IsOwnUpload(operation.RomId, slot, _deviceId);
    }

    /// <summary>How a save is named in a message, since a class C row's path is a container.</summary>
    private static string Describe(LocalSave save) =>
        save.ShapeClass == SaveShapeClass.C ? $"{save.Path}/{save.UnitKey}" : save.Path.Value;

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

            if (local?.ShapeClass == SaveShapeClass.C)
            {
                return await RestoreUnitAsync(operation, saveId, local, sessionId, part, cancellationToken)
                    .ConfigureAwait(false);
            }

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
    /// Fetches a bundled unit, stages it whole, and then swaps its members in one at a time.
    /// </summary>
    /// <remarks>
    /// <b>A half-written directory save is a corrupt one</b>, so nothing touches the live tree
    /// until the whole unit is on disk and readable. <b>The swap that follows is not atomic</b>,
    /// since the container is shared and only the unit's own members may move; #38 has the
    /// consequence and why a whole-container swap is the wrong fix. The archive is fetched to a <c>.part</c>,
    /// extracted into a staging directory beside it, the existing members are copied aside under
    /// <c>replaced/</c>, and only then are the new ones moved in.
    /// <para>
    /// <b>What can and cannot be verified, stated because the difference matters.</b> A class A
    /// download is checked against <c>server_content_hash</c>, which is the MD5 of the bytes.
    /// For an archive that field is a digest over the contents computed by a function this
    /// client cannot reproduce, measured, so the same check is impossible and pretending
    /// otherwise would fail every restore. What is checked instead is real but weaker:
    /// extraction validates every entry's CRC, so a truncated or corrupted archive fails before
    /// anything is replaced, and an entry that would escape the container is refused outright.
    /// </para>
    /// <para>
    /// The previous copy is kept under <c>replaced/</c> until the next successful sync, which is
    /// the retention rule this plan has always been written against.
    /// </para>
    /// </remarks>
    private async Task<(long Bytes, string? Problem)> RestoreUnitAsync(
        SyncOperation operation,
        int saveId,
        LocalSave local,
        int sessionId,
        string part,
        CancellationToken cancellationToken)
    {
        try
        {
            long written;

            await using (var stream = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var response = await _connection
                    .DownloadSaveAsync(saveId, _deviceId, sessionId, stream, cancellationToken)
                    .ConfigureAwait(false);

                if (!response.IsSuccess)
                {
                    return (0, $"{Describe(local)}: {response.Message}");
                }

                written = response.Value;
            }

            var outcome = SaveUnitTransfer.Restore(
                _install,
                _units,
                local,
                part,
                _install.Resolve(PartialDirectory),
                AsideDirectory,
                _time.GetUtcNow());

            var ack = await _connection.AcknowledgeSaveAsync(saveId, _deviceId, cancellationToken).ConfigureAwait(false);

            if (!ack.IsSuccess)
            {
                // The unit is in place and the server does not know. The next negotiate offers it
                // again and the second restore lands identical content, so this costs a transfer
                // rather than a save.
                return (written, $"{Describe(local)}: written, but the server was not told: {ack.Message}");
            }

            // Against the fold of what actually landed, so the next scan sees a unit already in
            // step rather than one that needs sending straight back.
            _store.Saves.MarkUploaded(local.Path, local.UnitKey, outcome.ContentHash, _time.GetUtcNow());

            // And the slot's new server identity, which is the other half of being in step: the
            // wire hash for an unchanged unit is the server's digest, so a slot still holding
            // the pre-download one negotiates as `upload` for a unit that just came down.
            _store.SaveSlots.RecordRestored(
                operation.RomId,
                operation.Slot ?? local.Slot,
                saveId,
                operation.ServerContentHash,
                operation.ServerUpdatedAt,
                _time.GetUtcNow());

            return (written, null);
        }
        catch (RomMUnreachableException ex)
        {
            return (0, $"{Describe(local)}: {ex.Message}");
        }
        catch (InvalidDataException ex)
        {
            // A corrupt archive, or one naming an entry that would escape the container. The
            // live tree is untouched at this point, which is the whole shape of this method.
            return (0, $"{Describe(local)}: the archive could not be unpacked: {ex.Message}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (0, $"{Describe(local)}: it could not be written: {ex.Message}");
        }
        finally
        {
            SafeDelete(part);
        }
    }

    /// <summary>
    /// Where a downloaded save goes on disk.
    /// </summary>
    /// <remarks>
    /// <b>The untagged name, never the one the server holds.</b> The tagged
    /// <c>Game [2026-08-10_22-58-26].srm</c> is invisible to an emulator matching on the ROM
    /// name. Where this device already has the slot, its own path wins, because that is a path
    /// this device has proven an emulator reads. Otherwise the slot's recorded server identity
    /// supplies the name and the ROM's own folder supplies the directory, which is the restore
    /// of a save this device once had and no longer does.
    /// <para>
    /// A slot this device has never negotiated at all has no recorded identity, and the
    /// negotiate operation carries only the tagged name. Stripping that tag client-side is what
    /// the plan rules out, so such a slot still reports that it has nowhere to go, and closing
    /// that needs the live negotiate this branch did not drive.
    /// </para>
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

    /// <summary>
    /// Persists a conflict, and copies the local file aside the first time only.
    /// </summary>
    /// <remarks>
    /// <b>The copy is taken once per conflict, not once per flush.</b> Stage 1 copied on every
    /// pass, so a slot that conflicts and is never resolved gained one dated file under
    /// <c>replaced/</c> each time and nothing pruned them. The row read back after recording
    /// answers both halves of that: a standing conflict already points at its copy, and a slot
    /// the user settled whose server side has not moved is not open at all.
    /// <para>
    /// Keying on the copy rather than on the conflict being new is what makes a reopened conflict
    /// work. Resolving one prunes the copy, so a slot that conflicts again has no copy aside and
    /// needs a fresh one taken.
    /// </para>
    /// <para>
    /// Recording comes first and the copy second, so a copy that fails still leaves the conflict
    /// visible. The other way round, an unwritable <c>replaced/</c> would lose the record of the
    /// conflict as well as the copy.
    /// </para>
    /// </remarks>
    private SaveConflict RecordConflict(SyncOperation operation, LocalSave local)
    {
        var slot = operation.Slot ?? string.Empty;
        var reason = operation.Reason
            ?? "both this device and the server changed this slot since the last sync.";
        var now = _time.GetUtcNow();

        _store.SaveConflicts.Record(
            new SaveConflictRecord(
                operation.RomId,
                slot,
                local.Path,
                null,
                local.ContentHash,
                operation.ServerContentHash,
                operation.ServerUpdatedAt,
                operation.SaveId,
                reason,
                now,
                now,
                null,
                null),
            now);

        var stored = _store.SaveConflicts.Read(operation.RomId, slot);
        var aside = stored?.LocalCopyPath;

        if (aside is null && stored is { IsOpen: true })
        {
            try
            {
                // Dispatched on the shape, because a class C row's path is a container and
                // File.Exists is false for one: taking the single-file route there quietly
                // copied nothing and left the record promising a copy it did not have. Found on
                // real hardware, where the first PSP conflict recorded local_copy_path as null.
                aside = local.ShapeClass == SaveShapeClass.C
                    ? SaveUnitTransfer.CopyAside(_install, _units, local, AsideDirectory, _time.GetUtcNow())
                    : MoveAside(local.Path);

                if (aside is not null)
                {
                    _store.SaveConflicts.RecordCopy(operation.RomId, slot, aside.Value);
                }
            }
            catch (IOException)
            {
                // The copy is a courtesy here rather than a precondition: nothing is being
                // overwritten, because a conflict is never resolved automatically.
            }
        }

        return new SaveConflict(
            operation.RomId,
            slot,
            local.Path,
            aside,
            local.ContentHash,
            operation.ServerContentHash,
            operation.ServerUpdatedAt,
            reason);
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
