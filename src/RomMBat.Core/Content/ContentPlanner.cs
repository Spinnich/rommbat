using System.Globalization;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;

namespace RomMBat.Core.Content;

/// <summary>What a sync would do about one game.</summary>
public enum ContentAction
{
    /// <summary>The right file is already there. The whole point of the inventory.</summary>
    AlreadyPresent,

    /// <summary>A file already on disk is this game, so it is recorded rather than downloaded.</summary>
    Adopt,

    /// <summary>Nothing is there, so it has to be fetched.</summary>
    Download,

    /// <summary>Part of it is there, so the transfer continues rather than starting again.</summary>
    Resume,

    /// <summary>It would not fit, so nothing is attempted.</summary>
    Blocked,
}

/// <summary>One game, and what the sync would do about it.</summary>
public sealed record ContentStep
{
    public required SyncSetMember Member { get; init; }

    public required ContentAction Action { get; init; }

    /// <summary>Where the file belongs, relative to the RetroBat root.</summary>
    public required RelativePath TargetPath { get; init; }

    internal bool PublishAsDiscGroup { get; init; }

    internal long ResumableMemberBytes { get; init; }

    internal long? ExistingSize { get; init; }

    internal DateTime? ExistingModifiedUtc { get; init; }

    /// <summary>How many bytes would cross the network. Zero for anything already here.</summary>
    public long BytesToTransfer { get; init; }

    /// <summary>How many bytes of the file are already on disk, for a resume.</summary>
    public long BytesAlreadyOnDisk { get; init; }

    /// <summary>Why, when the action needs explaining.</summary>
    public string? Reason { get; init; }
}

/// <summary>What one sync of one set would do, before anything is done.</summary>
public sealed record ContentPlan
{
    public required SyncSetDefinition Set { get; init; }

    public IReadOnlyList<ContentStep> Steps { get; init; } = [];

    /// <summary>How much RomMBat's own content occupies now.</summary>
    public long BytesManagedNow { get; init; }

    /// <summary>The global cap, when one is set.</summary>
    public long? BudgetBytes { get; init; }

    /// <summary>How much free space the volume reports.</summary>
    public long FreeBytes { get; init; }

    public IEnumerable<ContentStep> Downloads =>
        Steps.Where(step => step.Action is ContentAction.Download or ContentAction.Resume);

    public IEnumerable<ContentStep> Blocked => Steps.Where(step => step.Action == ContentAction.Blocked);

    public long BytesToTransfer => Downloads.Sum(step => step.BytesToTransfer);

    public int PresentCount => Steps.Count(step => step.Action == ContentAction.AlreadyPresent);

    public int AdoptCount => Steps.Count(step => step.Action == ContentAction.Adopt);

    /// <summary>
    /// True when this sync would write nothing at all.
    /// </summary>
    /// <remarks>
    /// The signal the plan asks for by name: a second run of an unchanged set does no work, and
    /// says so, rather than quietly re-downloading and calling it success. An adoption still
    /// counts as work, because it writes a row.
    /// </remarks>
    public bool IsNoOp => Steps.All(step => step.Action == ContentAction.AlreadyPresent);

    /// <summary>One line for the console and for status.</summary>
    public string Summary
    {
        get
        {
            if (Steps.Count == 0)
            {
                return "nothing to sync: the set holds no games";
            }

            if (IsNoOp)
            {
                return $"nothing to do: all {PresentCount} games are already present and verified";
            }

            var parts = new List<string>();
            var downloads = Downloads.ToList();

            if (downloads.Count > 0)
            {
                var resumes = downloads.Count(step => step.Action == ContentAction.Resume);
                var text = $"{downloads.Count} to download ({ByteSize.Format(BytesToTransfer)})";
                parts.Add(resumes > 0 ? $"{text}, {resumes} resuming" : text);
            }

            if (AdoptCount > 0)
            {
                parts.Add($"{AdoptCount} already on disk to adopt");
            }

            if (PresentCount > 0)
            {
                parts.Add($"{PresentCount} present");
            }

            var blocked = Blocked.ToList();
            if (blocked.Count > 0)
            {
                parts.Add($"{blocked.Count} blocked ({ByteSize.Format(blocked.Sum(step => step.Member.SizeBytes))} over budget)");
            }

            return string.Join(", ", parts);
        }
    }

}

/// <summary>
/// Works out what a sync would do, without doing any of it.
/// </summary>
/// <remarks>
/// Everything expensive is avoided when it can be. A file whose recorded size and modification
/// time still match the disk is taken as present without being re-hashed, which is what keeps a
/// second sync of a 40-game set from reading 20 GB. A file that disagrees, or that was never
/// recorded, is hashed once and the answer is kept.
/// <para>
/// <b>The budget counts what RomMBat downloaded, not what is on the disk.</b> A user's own
/// library can be far larger than any budget, and counting it would leave the app permanently
/// over its cap, unable to fetch anything and unable to evict its way out, since it must never
/// delete a file it did not download. Free space is the bound that covers the disk as a whole,
/// and it is read live rather than derived.
/// </para>
/// </remarks>
public sealed class ContentPlanner
{
    private readonly RetroBatInstall _install;
    private readonly LocalStore _store;
    private readonly FilesystemLimits _limits;

    public ContentPlanner(RetroBatInstall install, LocalStore store, FilesystemLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(store);

        _install = install;
        _store = store;
        _limits = limits ?? FilesystemLimits.Inspect(install.RootPath);
    }

    /// <summary>Where a member's file belongs, or for a multi-file game where its playlist does.</summary>
    /// <remarks>
    /// A multi-file game lands in a folder named after the rom, which is RomM's own layout, and
    /// what EmulationStation launches is the playlist inside it: <c>roms/psx/Metal Gear Solid (USA)
    /// (Rev 1)/Metal Gear Solid (USA) (Rev 1).m3u</c>. ES lists that as one game and shows no disc.
    /// </remarks>
    public static RelativePath TargetFor(SyncSetMember member)
    {
        ArgumentNullException.ThrowIfNull(member);

        if (string.IsNullOrWhiteSpace(member.Folder))
        {
            throw new ArgumentException($"'{member.FsName}' has no resolved folder, so it has no target.", nameof(member));
        }

        return member.IsMultiFile
            ? RelativePath.Create($"roms/{member.Folder}/{member.FsName}/{MultiFileLayout.PlaylistNameFor(member.FsName)}")
            : RelativePath.Create($"roms/{member.Folder}/{member.FsName}");
    }

    /// <summary>The folder a multi-file game's members land in.</summary>
    public static RelativePath FolderFor(SyncSetMember member)
    {
        ArgumentNullException.ThrowIfNull(member);

        return RelativePath.Create($"roms/{member.Folder}/{member.FsName}");
    }

    /// <summary>Where an interrupted transfer is kept.</summary>
    /// <remarks>
    /// Under the app's own directory, never beside the target, so a power loss cannot leave a
    /// partial file in a folder EmulationStation scans and offers to launch. Named from the ROM
    /// id rather than the file name, which keeps it short and free of anything Windows refuses.
    /// </remarks>
    public static RelativePath PartFor(int romId) =>
        RetroBatInstall.PartialDirectory.Combine($"{romId.ToString(CultureInfo.InvariantCulture)}.part");

    /// <summary>Where an interrupted transfer of one member of a multi-file game is kept.</summary>
    public static RelativePath PartFor(int romId, int fileId) =>
        fileId == 0
            ? PartFor(romId)
            : RetroBatInstall.PartialDirectory.Combine(
                $"{romId.ToString(CultureInfo.InvariantCulture)}-{fileId.ToString(CultureInfo.InvariantCulture)}.part");

    /// <summary>Works out what syncing this set would do.</summary>
    public ContentPlan Plan(SyncSetDefinition set, IReadOnlyList<SyncSetMember> members) =>
        PlanCore(set, members, preserveExisting: false);

    internal ContentPlan PlanCore(
        SyncSetDefinition set, IReadOnlyList<SyncSetMember> members, bool preserveExisting)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(members);

        var budget = _store.Settings.GetInt64(SettingStore.ContentMaxBytes);
        var floor = _store.Settings.GetInt64(SettingStore.FreeSpaceFloorBytes)
            ?? SettingStore.DefaultFreeSpaceFloorBytes;

        var managed = ManagedBytes();
        var free = _limits.AvailableFreeBytes;
        var freshDiscFolders = preserveExisting ? FreshDiscFolders(members) : [];
        var steps = members.Where(member => !string.IsNullOrWhiteSpace(member.Folder))
            .Select(member => InspectCandidate(member, preserveExisting, freshDiscFolders))
            .ToArray();
        var projectedManaged = managed;
        var projectedFree = free;

        if (preserveExisting)
        {
            // Startup acts on whole games, including discs that are not adjacent in the set.
            // Charge both budgets only after the group is known to be eligible.
            foreach (var game in Enumerable.Range(0, steps.Length).GroupBy(index =>
                DiscSet.GameKey(steps[index].Member.Folder, steps[index].Member.FsName), StringComparer.OrdinalIgnoreCase))
            {
                var group = game.Select(index => steps[index]).ToArray();
                var transfers = game.Where(index => DownloadsContent(steps[index])).ToArray();
                if (transfers.Length == 0)
                {
                    continue;
                }

                var growth = transfers.Sum(index => ManagedBytesToAdd(steps[index], preserveExisting));
                var transferBytes = transfers.Sum(index => steps[index].BytesToTransfer);
                var refusal = group.Any(step => step.Action == ContentAction.Blocked)
                    ? "another file in this game group could not be synced"
                    : StartupGroupRefusal(group) ?? BudgetRefusal(growth, transferBytes);

                if (refusal is null)
                {
                    projectedManaged += growth;
                    projectedFree -= transferBytes;
                    continue;
                }

                foreach (var index in transfers)
                {
                    steps[index] = Block(steps[index], refusal);
                }
            }
        }
        else
        {
            // Explicit sync can repair individual files in an existing group.
            for (var index = 0; index < steps.Length; index++)
            {
                var step = steps[index];
                if (!DownloadsContent(step))
                {
                    continue;
                }

                var growth = ManagedBytesToAdd(step, preserveExisting);
                if (BudgetRefusal(growth, step.BytesToTransfer) is { } refusal)
                {
                    steps[index] = Block(step, refusal);
                    continue;
                }

                projectedManaged += growth;
                projectedFree -= step.BytesToTransfer;
            }
        }

        string? BudgetRefusal(long growth, long transfer)
        {
            if (budget is { } cap && projectedManaged + growth > cap)
            {
                return $"the {ByteSize.Format(cap)} budget is full. Raise it, or evict something to make room.";
            }

            return projectedFree - transfer < floor
                ? $"this drive would drop below the {ByteSize.Format(floor)} of free space RomMBat is told to leave."
                : null;
        }

        return new ContentPlan
        {
            Set = set,
            Steps = steps,
            BytesManagedNow = managed,
            BudgetBytes = budget,
            FreeBytes = free,
        };
    }

    private ContentStep InspectCandidate(SyncSetMember member, bool preserveExisting, HashSet<RelativePath> freshDiscFolders)
    {
        try
        {
            var step = Inspect(member, preserveExisting, freshDiscFolders);
            if (preserveExisting && !member.IsMultiFile && DownloadsContent(step) && Path.Exists(_install.Resolve(step.TargetPath)))
            {
                return Block(step, "existing ROM left in place; review it before running sync with the emulator closed");
            }

            return step;
        }
        catch (Exception ex) when (preserveExisting && ex is IOException or UnauthorizedAccessException)
        {
            return new ContentStep
            {
                Member = member,
                Action = ContentAction.Blocked,
                TargetPath = TargetFor(member),
                Reason = $"could not inspect the existing ROM; review it with the emulator closed ({ex.Message})",
            };
        }
    }

    internal static string? StartupGroupRefusal(IReadOnlyList<ContentStep> steps) =>
        steps.Any(step => !step.PublishAsDiscGroup) && steps.Count(DownloadsContent) > 1
            ? "multiple discs are missing from an existing group. Review the files and run an explicit sync with the emulator closed."
            : null;

    private static bool DownloadsContent(ContentStep step) => step.Action is ContentAction.Download or ContentAction.Resume;

    private static ContentStep Block(ContentStep step, string reason) =>
        step with { Action = ContentAction.Blocked, BytesToTransfer = 0, Reason = reason };

    internal static long ManagedBytesToAdd(ContentStep step, bool preserveExisting) =>
        preserveExisting && step.Member.IsMultiFile
            ? step.BytesToTransfer + step.ResumableMemberBytes : step.Member.SizeBytes;

    internal static string? PublicationBudgetProblem(LocalStore store, long growth)
    {
        var budget = store.Settings.GetInt64(SettingStore.ContentMaxBytes);
        return growth > 0 && budget is { } cap && growth > cap - store.Files.SyncedBytes()
            ? $"the {ByteSize.Format(cap)} managed content budget would be exceeded." : null;
    }

    /// <summary>How many bytes RomMBat's own downloads occupy.</summary>
    /// <remarks>
    /// Adopted files are excluded deliberately: they are the user's, they are never evicted, and
    /// counting them would put a large existing library permanently over budget.
    /// </remarks>
    public long ManagedBytes()
    {
        var total = 0L;
        foreach (var file in _store.Files.List(includeStale: true))
        {
            if (file.Origin == FileOrigin.Synced)
            {
                total += file.SizeBytes;
            }
        }

        return total;
    }

    /// <summary>
    /// Decides what a multi-file game needs, which is present or not as a whole.
    /// </summary>
    /// <remarks>
    /// Present means the playlist and every member recorded with it are on disk at their recorded
    /// sizes. Which members a game has is the server's to say, and only <see cref="ContentSync"/>
    /// asks, so anything short of that is a download and the transfer skips the members already
    /// there. The bytes counted are what is still missing, so the budget is not charged twice.
    /// </remarks>
    private ContentStep InspectSet(SyncSetMember member, bool preserveExisting)
    {
        var target = TargetFor(member);
        var step = new ContentStep
        {
            Member = member,
            Action = ContentAction.Download,
            TargetPath = target,
            BytesToTransfer = member.SizeBytes,
        };

        var rows = _store.Files.ForRom(member.RomId)
            .Where(file => file.Kind is LocalFileKind.Rom or LocalFileKind.RomPart)
            .ToList();

        var onDisk = rows
            .Where(file =>
            {
                var info = new FileInfo(_install.Resolve(file.Path));
                return info.Exists && info.Length == file.SizeBytes
                    && (!preserveExisting || (!file.Stale && info.LastWriteTimeUtc == file.ModifiedUtc));
            })
            .ToList();

        var playlist = onDisk.Any(file => file.Kind == LocalFileKind.Rom && file.Path.Equals(target));

        var current = !preserveExisting || member.RomUpdatedAt is not { } updated
            || onDisk.All(file => file.VerifiedAt >= updated);
        if (playlist && current && onDisk.Count == rows.Count && EveryDiscNamedIsHere(target, onDisk))
        {
            return step with { Action = ContentAction.AlreadyPresent, BytesToTransfer = 0 };
        }

        if (Path.Exists(_install.Resolve(PackedRomStage.For(member.RomId))))
        {
            return step with
            {
                Action = ContentAction.Blocked,
                BytesToTransfer = 0,
                Reason = "a previous package staging directory could not be recovered; review it before retrying",
            };
        }

        var held = onDisk.Where(file => file.Kind == LocalFileKind.RomPart).Sum(file => file.SizeBytes);

        var missing = Math.Max(0, member.SizeBytes - held);
        var resumable = Math.Min(missing, PackedRomStage.ResumableBytes(
            _install, _store, member.RomId, FolderFor(member), onDisk));

        return step with
        {
            BytesToTransfer = missing - resumable,
            ResumableMemberBytes = resumable,
            Reason = held + resumable > 0 ? $"{ByteSize.Format(held + resumable)} of its files already here" : null,
        };
    }

    /// <summary>
    /// True when every file the playlist names is on disk and recorded as this game's.
    /// </summary>
    /// <remarks>
    /// Rows alone cannot answer it: a disc whose file and row were both removed leaves a set
    /// whose remaining rows all agree with the disk, and a playlist naming a file that is gone.
    /// That is the state a media sweep left on a real install, and without this check no later
    /// sync would have repaired it.
    /// </remarks>
    private bool EveryDiscNamedIsHere(RelativePath playlist, IReadOnlyList<LocalFile> onDisk)
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(_install.Resolve(playlist));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        var folder = playlist.Value[..playlist.Value.LastIndexOf('/')];
        var held = onDisk.Select(file => file.Path.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var named = lines.Select(line => line.Trim()).Where(line => line.Length > 0).ToList();

        return named.Count > 0 && named.All(name => held.Contains($"{folder}/{name.Replace('\\', '/')}"));
    }

    /// <summary>Decides what one member needs, doing the least I/O that can answer.</summary>
    private HashSet<RelativePath> FreshDiscFolders(IReadOnlyList<SyncSetMember> members)
    {
        var candidates = members.Where(member => !member.IsMultiFile)
            .Select(member => (member.Folder, Group: FlatDiscLayout.FolderFor(member.Folder, member.FsName)))
            .Where(item => item.Group is not null)
            .GroupBy(item => item.Folder!, StringComparer.OrdinalIgnoreCase);
        var fresh = new HashSet<RelativePath>();
        foreach (var system in candidates)
        {
            var groups = system.GroupBy(item => item.Group!.Value).Where(group => group.Count() > 1).ToArray();
            if (groups.Length == 0)
            {
                continue;
            }

            try
            {
                var existing = FlatDiscLayout.ExistingLooseFolders(_install, system.Key).ToHashSet();
                fresh.UnionWith(groups.Select(group => group.Key)
                    .Where(group => !existing.Contains(group) && !Path.Exists(_install.Resolve(group))));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Without a readable system folder, multiple missing discs are deferred.
            }
        }

        return fresh;
    }

    private ContentStep Inspect(SyncSetMember member, bool preserveExisting, HashSet<RelativePath> freshDiscFolders)
    {
        if (member.IsMultiFile)
        {
            return InspectSet(member, preserveExisting);
        }

        var target = TargetFor(member);
        var publishAsGroup = false;
        if (FlatDiscLayout.FolderFor(member.Folder, member.FsName) is { } group)
        {
            var nested = group.Combine(target.Name);
            publishAsGroup = freshDiscFolders.Contains(group);
            if (publishAsGroup
                || !Path.Exists(_install.Resolve(target)) && Directory.Exists(_install.Resolve(group)))
            {
                target = nested;
            }
        }

        var absolute = _install.Resolve(target);
        var step = new ContentStep
        {
            Member = member,
            Action = ContentAction.Download,
            TargetPath = target,
            PublishAsDiscGroup = publishAsGroup,
            BytesToTransfer = member.SizeBytes,
        };

        var info = new FileInfo(absolute);

        if (!info.Exists)
        {
            // Nothing at the target. An interrupted transfer of it may still be waiting.
            var part = new FileInfo(_install.Resolve(PartFor(member.RomId)));
            var recorded = _store.Downloads.Find(member.RomId);

            if (recorded is not null && part.Exists && part.Length > 0)
            {
                return step with
                {
                    Action = ContentAction.Resume,
                    BytesToTransfer = Math.Max(0, member.SizeBytes - part.Length),
                    BytesAlreadyOnDisk = part.Length,
                    Reason = $"{ByteSize.Format(part.Length)} of "
                        + $"{ByteSize.Format(member.SizeBytes)} already fetched",
                };
            }

            return step;
        }

        var known = _store.Files.Find(target);

        // The cheap path, and the one a no-op re-sync takes: the inventory says this file is
        // this ROM, and the disk still agrees about its size and modification time.
        if (known is not null
            && known.RomId == member.RomId
            && !known.Stale
            && known.SizeBytes == info.Length
            && known.VerifiedBy != VerifiedBy.None
            && (!preserveExisting || (known.SizeBytes == member.SizeBytes
                && (string.IsNullOrWhiteSpace(member.Md5Hash) || known.VerifiedBy == VerifiedBy.Size
                    || ContentHasher.Matches(known.Md5Hash, member.Md5Hash))))
            && (known.ModifiedUtc is not { } recordedTime
                || _limits.AreTimestampsEquivalent(recordedTime, info.LastWriteTimeUtc)))
        {
            return step with
            {
                Action = ContentAction.AlreadyPresent, BytesToTransfer = 0,
                ExistingSize = info.Length, ExistingModifiedUtc = info.LastWriteTimeUtc,
            };
        }

        // Something is there and nothing vouches for it. Hashing is the only honest answer, and
        // the result is recorded so this costs once rather than every sync.
        var fingerprint = ContentHasher.Compute(absolute);

        if (ContentHasher.Matches(fingerprint.Md5, member.Md5Hash))
        {
            return step with
            {
                Action = ContentAction.Adopt,
                BytesToTransfer = 0,
                Reason = "already on disk and its content matches",
            };
        }

        // No hash to compare against is not the same as a mismatch, and neither is a hash that
        // cannot describe this file. A .7z is hashed as its own bytes while the server's hash
        // describes the content inside it, and a few rows carry no hash at all. Re-downloading
        // either on every sync would be worse than trusting a file of exactly the right length.
        //
        // There is no sha1 arm because sha1 is a second number the same server published rather
        // than an independent check, and RB-180 measured it being simply wrong on two ps2
        // rows. The rows with no hash at all are 0.6% of a real library's single-file ROMs (RB-257), and the answer
        // here is recorded as VerifiedBy.Size rather than passed off as a hash check.
        // Blank rather than null, because a member can be read back from a store or built by a
        // caller that never passed through the client's boundary, and a blank hash is absence
        // wearing a value.
        var nothingToCompare = string.IsNullOrWhiteSpace(member.Md5Hash);

        if (info.Length == member.SizeBytes && (nothingToCompare || !fingerprint.DescribesLibraryContent))
        {
            return step with
            {
                Action = ContentAction.Adopt,
                BytesToTransfer = 0,
                Reason = nothingToCompare
                    ? "already on disk at the right size, and the server has no hash to check it against"
                    : "already on disk at the right size, and this archive cannot be checked against its hash",
            };
        }

        return step with { Reason = "a different file is in its place" };
    }
}
