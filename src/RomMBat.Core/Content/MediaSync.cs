using RomM.Client;
using RomM.Client.Content;
using RomMBat.Core.Metadata;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;

namespace RomMBat.Core.Content;

/// <summary>What a media pass did.</summary>
public sealed record MediaSyncOutcome
{
    public int Downloaded { get; init; }

    public int AlreadyPresent { get; init; }

    /// <summary>Files a user's own scraper had already written, which are recorded and never fetched.</summary>
    public int Adopted { get; init; }

    public int Missing { get; init; }

    public int Blocked { get; init; }

    public int Failed { get; init; }

    /// <summary>
    /// Files removed because their kind is no longer wanted.
    /// </summary>
    /// <remarks>
    /// Turning a kind off used to stop future downloads and nothing else, so the artwork already
    /// fetched stayed for ever with nothing able to reclaim it: measured on a real install, 1.09
    /// GB of video on one platform and 566 MB on another. The setting means the same thing in
    /// both directions now.
    /// </remarks>
    public int Removed { get; init; }

    public long BytesTransferred { get; init; }

    public IReadOnlyList<string> Problems { get; init; } = [];

    public bool IsNoOp => Downloaded == 0 && Adopted == 0 && Failed == 0 && Removed == 0;

    /// <summary>
    /// Two outcomes as one, for a caller that fetches artwork a game at a time.
    /// </summary>
    /// <remarks>
    /// The run still reports one media line, because a person watching a sync wants to know
    /// what the artwork cost rather than what it cost forty times.
    /// </remarks>
    public static MediaSyncOutcome Merge(MediaSyncOutcome first, MediaSyncOutcome second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        return new MediaSyncOutcome
        {
            Downloaded = first.Downloaded + second.Downloaded,
            AlreadyPresent = first.AlreadyPresent + second.AlreadyPresent,
            Adopted = first.Adopted + second.Adopted,
            Missing = first.Missing + second.Missing,
            Blocked = first.Blocked + second.Blocked,
            Failed = first.Failed + second.Failed,
            Removed = first.Removed + second.Removed,
            BytesTransferred = first.BytesTransferred + second.BytesTransferred,
            Problems = [.. first.Problems, .. second.Problems],
        };
    }

    public string Summary
    {
        get
        {
            if (Downloaded == 0 && Adopted == 0 && Blocked == 0 && Failed == 0 && Removed == 0)
            {
                return AlreadyPresent == 0
                    ? "no media to fetch"
                    : $"media: {AlreadyPresent} already present";
            }

            var parts = new List<string>();

            if (Downloaded > 0)
            {
                parts.Add($"{Downloaded} downloaded ({ByteSize.Format(BytesTransferred)})");
            }

            if (Adopted > 0)
            {
                parts.Add($"{Adopted} already on disk");
            }

            if (AlreadyPresent > 0)
            {
                parts.Add($"{AlreadyPresent} present");
            }

            if (Blocked > 0)
            {
                parts.Add($"{Blocked} blocked by the budget");
            }

            if (Removed > 0)
            {
                parts.Add($"{Removed} removed, no longer wanted");
            }

            if (Failed > 0)
            {
                parts.Add($"{Failed} failed");
            }

            return "media: " + string.Join(", ", parts);
        }
    }
}

/// <summary>
/// Fetches the artwork, video and manuals that make a gamelist worth having.
/// </summary>
/// <remarks>
/// <b>Media is not a rounding error against the ROMs it decorates.</b> At the measured
/// medians a game costs 525 KB of cover, 104 KB of thumbnail, 445 KB of marquee, 1.99 MB of
/// video and 2.45 MB of manual, so a hundred-game NES set is about 12.8 MB of ROMs and up to
/// 550 MB of media. It counts against the same two bounds ROMs do, and manuals are off by
/// default because they are the largest single kind and nothing in M4 needs them.
/// <para>
/// <b>A user's own scraper writes to exactly these names.</b> A file already at the target is
/// recorded as <see cref="FileOrigin.Adopted"/> and never overwritten or counted against the
/// budget, which is what keeps eviction from deleting artwork RomMBat did not create.
/// </para>
/// <para>
/// Nothing here resumes. The largest kind has a 2.45 MB median, so a failed transfer starts
/// again rather than carrying the machinery a 4 GB ROM needs.
/// </para>
/// </remarks>
public sealed class MediaSync
{
    private readonly RetroBatInstall _install;
    private readonly LocalStore _store;
    private readonly RomMConnection _connection;
    private readonly FilesystemLimits _limits;
    private readonly TimeProvider _time;

    public MediaSync(
        RetroBatInstall install,
        LocalStore store,
        RomMConnection connection,
        FilesystemLimits? limits = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(connection);

        _install = install;
        _store = store;
        _connection = connection;
        _limits = limits ?? FilesystemLimits.Inspect(install.RootPath);
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Where one kind of media for one game belongs.</summary>
    public static RelativePath TargetFor(string folder, string romFileName, MediaKind kind, string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        var fileName = MediaNaming.FileNameFor(romFileName, kind, extension);
        return RelativePath.Create($"roms/{folder}/{MediaNaming.FolderFor(kind)}/{fileName}");
    }

    /// <summary>
    /// Fetches the configured kinds for every game that is locally present.
    /// </summary>
    /// <param name="romIds">
    /// Which games to consider. The caller passes the ones a sync just landed, so a run does
    /// not re-walk the whole install.
    /// </param>
    /// <param name="reservedBytes">
    /// How many bytes of ROM the caller still intends to fetch in this run.
    /// </param>
    /// <remarks>
    /// <b>The reservation exists because artwork is now fetched a game at a time.</b> Room is
    /// <c>cap - managed</c>, and <c>managed</c> is read from <c>local_file</c> when the call is
    /// made. One call after every ROM had landed saw the true total; one call per game sees the
    /// budget as it stands after only that game's ROM, with every later ROM still to come, so
    /// early artwork spends what the plan had already earmarked. Measured against a live
    /// instance before this parameter existed: a 1 MB budget finished 703 KB over it, where the
    /// pass it replaced finished at 1023.3 KB of 1 MB.
    /// <para>
    /// <b>This is a reservation for ROMs, not for media, and the difference is why it can
    /// exist.</b> A ROM's size is on the member row and the plan already has it. Media has no
    /// size until it is fetched, because RomM publishes none on the rom row, which is exactly
    /// why the fix for #102 was to interleave rather than to reserve.
    /// </para>
    /// </remarks>
    public async Task<MediaSyncOutcome> ApplyAsync(
        IReadOnlyCollection<int> romIds,
        IProgress<string>? progress = null,
        long reservedBytes = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(romIds);

        var kinds = MediaPolicy.Read(_store.Settings, _install);
        var downloaded = 0;
        var present = 0;
        var adopted = 0;
        var missing = 0;
        var blocked = 0;
        var failed = 0;
        var removed = 0;
        var bytes = 0L;
        var problems = new List<string>();

        var budget = _store.Settings.GetInt64(SettingStore.ContentMaxBytes);
        var floor = _store.Settings.GetInt64(SettingStore.FreeSpaceFloorBytes)
            ?? SettingStore.DefaultFreeSpaceFloorBytes;
        // Recomputed from local_file on every call. Fine at tens of games, which is one query
        // per game; an install syncing thousands would want this hoisted into the caller and
        // carried across games instead.
        var managed = _store.Files.List().Where(file => file.Origin == FileOrigin.Synced).Sum(file => file.SizeBytes);

        // The ROMs still to come are spoken for, against both bounds. Without this the last
        // game in a plan finds its budget already spent on the first game's artwork.
        managed += reservedBytes;

        var freeRoom = Math.Max(0, _limits.AvailableFreeBytes - floor - reservedBytes);
        var budgetExhausted = false;

        foreach (var romId in romIds.Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var metadata = _store.Metadata.Find(romId);
            if (metadata is null)
            {
                continue;
            }

            // The game itself has to be here. Media for a ROM that was never downloaded would
            // be bytes for a gamelist entry that is never written.
            var romFiles = _store.Files.ForRom(romId, LocalFileKind.Rom);
            if (romFiles.Count == 0)
            {
                continue;
            }

            // Non-null by the schema's own CHECK: only firmware has no folder, and this
            // query asked for roms.
            var rom = romFiles[0];
            var folder = rom.Folder!;
            var forgotten = new List<MediaKind>();

            removed += Discard(romId, kinds);

            foreach (var kind in kinds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!metadata.MediaPaths.TryGetValue(kind, out var sourcePath))
                {
                    missing++;
                    continue;
                }

                var resource = new MediaResource { Kind = kind, SourcePath = sourcePath };
                RelativePath target;

                try
                {
                    target = TargetFor(folder, rom.FileName, kind, resource.Extension);
                }
                catch (ArgumentException ex)
                {
                    failed++;
                    problems.Add($"{metadata.Name}: its {kind} could not be given a file name ({ex.Message})");
                    continue;
                }

                var state = Inspect(romId, kind, target);

                if (state == MediaState.Present)
                {
                    present++;
                    continue;
                }

                if (state == MediaState.Adopt)
                {
                    RecordAdopted(romId, kind, target, folder);
                    adopted++;
                    continue;
                }

                if (budgetExhausted)
                {
                    blocked++;
                    continue;
                }

                var room = Room(budget, managed, freeRoom);
                if (room <= 0)
                {
                    budgetExhausted = true;
                    blocked++;
                    problems.Add(
                        budget is { } cap && managed >= cap
                            ? $"the {ByteSize.Format(cap)} budget is full, so no more artwork was fetched."
                            : $"this drive would drop below the {ByteSize.Format(floor)} of free space "
                                + "RomMBat is told to leave, so no more artwork was fetched.");
                    continue;
                }

                progress?.Report($"{metadata.Name}: {kind}");

                var result = await FetchAsync(resource, target, room, cancellationToken).ConfigureAwait(false);

                if (result.Problem is { } problem)
                {
                    if (result.Absent)
                    {
                        // The server advertises a path it does not serve. Forgetting the path
                        // turns this from a failure that is re-attempted on every sync into the
                        // ordinary Missing case, and a re-resolve puts it back the moment RomM
                        // starts serving it, because a resolve rewrites this row from the
                        // server. Measured on a live library: 39 of 40 megadrive games
                        // advertised a video that answered 404, so this was 39 wasted requests
                        // and 39 lines of noise on every run, for ever.
                        forgotten.Add(kind);
                        missing++;
                        continue;
                    }

                    failed++;
                    problems.Add($"{metadata.Name}: {problem}");
                    continue;
                }

                Record(romId, kind, target, folder, result.Bytes);
                downloaded++;
                bytes += result.Bytes;
                managed += result.Bytes;
                freeRoom = Math.Max(0, freeRoom - result.Bytes);
            }

            if (forgotten.Count > 0)
            {
                Forget(metadata, forgotten);
            }
        }

        return new MediaSyncOutcome
        {
            Downloaded = downloaded,
            AlreadyPresent = present,
            Adopted = adopted,
            Missing = missing,
            Blocked = blocked,
            Failed = failed,
            Removed = removed,
            BytesTransferred = bytes,
            Problems = problems,
        };
    }

    /// <summary>
    /// Takes back artwork of a kind this install no longer wants.
    /// </summary>
    /// <remarks>
    /// <b>Turning a kind off has to mean the same thing in both directions.</b> It used to stop
    /// future downloads and nothing else, so what had already been fetched stayed for ever with
    /// nothing able to reclaim it: eviction removes whole games under budget pressure and has no
    /// notion of a kind. Measured on a real install, that was 1.09 GB of video on one platform
    /// and 566 MB on another.
    /// <para>
    /// <b>Only <see cref="FileOrigin.Synced"/>.</b> A user's own scrape sits at exactly these
    /// names and is recorded as <see cref="FileOrigin.Adopted"/> precisely so that nothing here
    /// touches it. Same fence the sync rollback uses.
    /// </para>
    /// <para>
    /// The row goes with the bytes, and a file that will not delete keeps its row, because a row
    /// removed from under a file nothing tracks is unreachable by both the budget and eviction.
    /// </para>
    /// </remarks>
    private int Discard(int romId, IReadOnlyList<MediaKind> wanted)
    {
        var keep = wanted.Select(ToFileKind).ToHashSet();
        var gone = 0;

        foreach (var file in _store.Files.ForRom(romId))
        {
            if (file.Kind == LocalFileKind.Rom
                || file.Kind == LocalFileKind.Firmware
                || file.Origin != FileOrigin.Synced
                || keep.Contains(file.Kind))
            {
                continue;
            }

            var absolute = _install.Resolve(file.Path);

            try
            {
                File.Delete(absolute);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Windows refuses two ways and only one is an IOException. Left with its row.
                continue;
            }

            _store.Files.Remove(file.Path);
            gone++;
        }

        return gone;
    }

    /// <summary>How many bytes the next file may take before it breaks a bound.</summary>
    private static long Room(long? budget, long managed, long freeRoom) =>
        budget is { } cap ? Math.Min(cap - managed, freeRoom) : freeRoom;

    private enum MediaState
    {
        Fetch,
        Present,
        Adopt,
    }

    /// <summary>
    /// Whether this file needs fetching, is already ours, or is the user's.
    /// </summary>
    /// <remarks>
    /// A file at the target that RomMBat has no row for is the user's own scrape. It is
    /// recorded so the gamelist can reference it and the next run leaves it alone, and marked
    /// adopted so eviction never deletes it.
    /// </remarks>
    private MediaState Inspect(int romId, MediaKind kind, RelativePath target)
    {
        var absolute = _install.Resolve(target);
        var info = new FileInfo(absolute);

        if (!info.Exists)
        {
            return MediaState.Fetch;
        }

        var known = _store.Files.Find(target);

        if (known is not null
            && known.RomId == romId
            && known.Kind == ToFileKind(kind)
            && known.SizeBytes == info.Length)
        {
            return MediaState.Present;
        }

        // Present on disk, unrecorded or recorded differently. Either way it is not something
        // to overwrite: this exact name is what RetroBat's own scraper writes.
        return MediaState.Adopt;
    }

    private async Task<(long Bytes, string? Problem, bool Absent)> FetchAsync(
        MediaResource resource,
        RelativePath target,
        long room,
        CancellationToken cancellationToken)
    {
        var absolute = _install.Resolve(target);
        var partial = absolute + ".part";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);

            RomMResponse<MediaResult> response;

            await using (var destination = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                response = await _connection
                    .DownloadMediaAsync(resource, destination, maximumBytes: room, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }

            if (!response.IsSuccess)
            {
                SafeDelete(partial);

                // 404 only. Every other refusal is about this attempt rather than about the
                // asset existing, and forgetting a path over a transient server error would
                // silently stop fetching artwork that is there.
                return (0, response.Message, response.Status == RomMResponseStatus.NotFound);
            }

            // Renamed only once the whole body has landed, so EmulationStation never sees a
            // half-written image and caches a broken texture for it.
            File.Move(partial, absolute, overwrite: true);
            return (response.Value!.BytesWritten, null, false);
        }
        catch (RomMUnreachableException ex)
        {
            SafeDelete(partial);
            return (0, ex.Message, false);
        }
        catch (PathTooLongException)
        {
            SafeDelete(partial);
            return (0, "the path to it is longer than this machine allows.", false);
        }
        catch (IOException ex)
        {
            SafeDelete(partial);
            return (0, $"it could not be written: {ex.Message}", false);
        }
    }

    /// <summary>
    /// Drops paths the server advertises but does not serve.
    /// </summary>
    /// <remarks>
    /// <b>Forgetting rather than remembering, so nothing new has to be stored.</b> The
    /// alternative is a table of known-absent assets and a rule for when to expire it. A
    /// resolve rewrites <c>metadata</c> from the server wholesale, so dropping the path here
    /// makes "retry when RomM's row changes" fall out of the refresh that already exists.
    /// </remarks>
    private void Forget(GameMetadata metadata, IReadOnlyList<MediaKind> kinds)
    {
        var kept = metadata.MediaPaths
            .Where(pair => !kinds.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        _store.Metadata.Record(metadata with { MediaPaths = kept });
    }

    private void Record(int romId, MediaKind kind, RelativePath target, string folder, long bytes)
    {
        var info = new FileInfo(_install.Resolve(target));

        _store.Files.Record(new LocalFile
        {
            Path = target,
            Folder = folder,
            RomId = romId,
            Kind = ToFileKind(kind),
            FileName = target.Name,
            SizeBytes = info.Exists ? info.Length : bytes,
            ModifiedUtc = info.Exists ? info.LastWriteTimeUtc : null,
            VerifiedAt = _time.GetUtcNow(),

            // Size is the only check available: RomM publishes no hash for media, and there is
            // nothing to compare a cover against.
            VerifiedBy = VerifiedBy.Size,
            Origin = FileOrigin.Synced,
        });
    }

    private void RecordAdopted(int romId, MediaKind kind, RelativePath target, string folder)
    {
        var info = new FileInfo(_install.Resolve(target));

        _store.Files.Record(new LocalFile
        {
            Path = target,
            Folder = folder,
            RomId = romId,
            Kind = ToFileKind(kind),
            FileName = target.Name,
            SizeBytes = info.Length,
            ModifiedUtc = info.LastWriteTimeUtc,
            VerifiedAt = _time.GetUtcNow(),
            VerifiedBy = VerifiedBy.Size,

            // Never 'synced'. This is the user's artwork, it does not count against the budget,
            // and eviction must never remove it.
            Origin = FileOrigin.Adopted,
        });
    }

    internal static LocalFileKind ToFileKind(MediaKind kind) => kind switch
    {
        MediaKind.Image => LocalFileKind.Image,
        MediaKind.Thumbnail => LocalFileKind.Thumbnail,
        MediaKind.Marquee => LocalFileKind.Marquee,
        MediaKind.Video => LocalFileKind.Video,
        _ => LocalFileKind.Manual,
    };

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
