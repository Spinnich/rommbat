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

    public long BytesTransferred { get; init; }

    public IReadOnlyList<string> Problems { get; init; } = [];

    public bool IsNoOp => Downloaded == 0 && Adopted == 0 && Failed == 0;

    public string Summary
    {
        get
        {
            if (Downloaded == 0 && Adopted == 0 && Blocked == 0 && Failed == 0)
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
    public async Task<MediaSyncOutcome> ApplyAsync(
        IReadOnlyCollection<int> romIds,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(romIds);

        var kinds = MediaPolicy.Read(_store.Settings);
        var downloaded = 0;
        var present = 0;
        var adopted = 0;
        var missing = 0;
        var blocked = 0;
        var failed = 0;
        var bytes = 0L;
        var problems = new List<string>();

        var budget = _store.Settings.GetInt64(SettingStore.ContentMaxBytes);
        var floor = _store.Settings.GetInt64(SettingStore.FreeSpaceFloorBytes)
            ?? SettingStore.DefaultFreeSpaceFloorBytes;
        var managed = _store.Files.List().Where(file => file.Origin == FileOrigin.Synced).Sum(file => file.SizeBytes);
        var freeRoom = Math.Max(0, _limits.AvailableFreeBytes - floor);
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

            var rom = romFiles[0];

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
                    target = TargetFor(rom.Folder, rom.FileName, kind, resource.Extension);
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
                    RecordAdopted(romId, kind, target, rom.Folder);
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
                    failed++;
                    problems.Add($"{metadata.Name}: {problem}");
                    continue;
                }

                Record(romId, kind, target, rom.Folder, result.Bytes);
                downloaded++;
                bytes += result.Bytes;
                managed += result.Bytes;
                freeRoom = Math.Max(0, freeRoom - result.Bytes);
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
            BytesTransferred = bytes,
            Problems = problems,
        };
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

    private async Task<(long Bytes, string? Problem)> FetchAsync(
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
                return (0, response.Message);
            }

            // Renamed only once the whole body has landed, so EmulationStation never sees a
            // half-written image and caches a broken texture for it.
            File.Move(partial, absolute, overwrite: true);
            return (response.Value!.BytesWritten, null);
        }
        catch (RomMUnreachableException ex)
        {
            SafeDelete(partial);
            return (0, ex.Message);
        }
        catch (PathTooLongException)
        {
            SafeDelete(partial);
            return (0, "the path to it is longer than this machine allows.");
        }
        catch (IOException ex)
        {
            SafeDelete(partial);
            return (0, $"it could not be written: {ex.Message}");
        }
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
