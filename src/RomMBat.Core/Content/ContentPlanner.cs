using System.Globalization;
using RomMBat.Core.Paths;
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
                return "nothing to sync: the set resolves to no games";
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

    /// <summary>Where a member's file belongs.</summary>
    public static RelativePath TargetFor(SyncSetMember member)
    {
        ArgumentNullException.ThrowIfNull(member);

        if (string.IsNullOrWhiteSpace(member.Folder))
        {
            throw new ArgumentException($"'{member.FsName}' has no resolved folder, so it has no target.", nameof(member));
        }

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

    /// <summary>Works out what syncing this set would do.</summary>
    public ContentPlan Plan(SyncSetDefinition set, IReadOnlyList<SyncSetMember> members)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(members);

        var budget = _store.Settings.GetInt64(SettingStore.ContentMaxBytes);
        var floor = _store.Settings.GetInt64(SettingStore.FreeSpaceFloorBytes)
            ?? SettingStore.DefaultFreeSpaceFloorBytes;

        var managed = ManagedBytes();
        var free = _limits.AvailableFreeBytes;
        var steps = new List<ContentStep>(members.Count);

        // Tracked across the loop so each candidate is judged against what the ones before it
        // would already have taken.
        var projectedManaged = managed;
        var projectedFree = free;

        foreach (var member in members.Where(member => !string.IsNullOrWhiteSpace(member.Folder)))
        {
            var step = Inspect(member);

            if (step.Action is ContentAction.Download or ContentAction.Resume)
            {
                var wouldAdd = member.SizeBytes;

                if (budget is { } cap && projectedManaged + wouldAdd > cap)
                {
                    steps.Add(step with
                    {
                        Action = ContentAction.Blocked,
                        BytesToTransfer = 0,
                        Reason = $"the {ByteSize.Format(cap)} budget is full. "
                            + "Raise it, or evict something to make room.",
                    });
                    continue;
                }

                if (projectedFree - step.BytesToTransfer < floor)
                {
                    steps.Add(step with
                    {
                        Action = ContentAction.Blocked,
                        BytesToTransfer = 0,
                        Reason = $"this drive would drop below the {ByteSize.Format(floor)} "
                            + "of free space RomMBat is told to leave.",
                    });
                    continue;
                }

                projectedManaged += wouldAdd;
                projectedFree -= step.BytesToTransfer;
            }

            steps.Add(step);
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

    /// <summary>How many bytes RomMBat's own downloads occupy.</summary>
    /// <remarks>
    /// Adopted files are excluded deliberately: they are the user's, they are never evicted, and
    /// counting them would put a large existing library permanently over budget.
    /// </remarks>
    public long ManagedBytes()
    {
        var total = 0L;
        foreach (var file in _store.Files.List())
        {
            if (file.Origin == FileOrigin.Synced)
            {
                total += file.SizeBytes;
            }
        }

        return total;
    }

    /// <summary>Decides what one member needs, doing the least I/O that can answer.</summary>
    private ContentStep Inspect(SyncSetMember member)
    {
        var target = TargetFor(member);
        var absolute = _install.Resolve(target);
        var step = new ContentStep
        {
            Member = member,
            Action = ContentAction.Download,
            TargetPath = target,
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
            && known.SizeBytes == info.Length
            && known.VerifiedBy != VerifiedBy.None
            && (known.ModifiedUtc is not { } recordedTime
                || _limits.AreTimestampsEquivalent(recordedTime, info.LastWriteTimeUtc)))
        {
            return step with { Action = ContentAction.AlreadyPresent, BytesToTransfer = 0 };
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
        // Measured, so the "few rows" is not a guess: 1,612 of 1,616 rom rows sampled across
        // three platforms of a live library carry an md5, and the four that do not carry no
        // sha1 either, which is why there is no sha1 arm here any more.
        var nothingToCompare = member.Md5Hash is null;

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
