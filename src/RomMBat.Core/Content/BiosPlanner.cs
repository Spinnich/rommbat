using RomM.Client.Catalog;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;

namespace RomMBat.Core.Content;

/// <summary>What a BIOS pass would do about one required file.</summary>
public enum BiosAction
{
    /// <summary>The right file is already there and RomMBat knows it.</summary>
    AlreadyPresent,

    /// <summary>The right file is there and nothing recorded it, so it is recorded rather than fetched.</summary>
    Adopt,

    /// <summary>RomM has these bytes and this install does not.</summary>
    Download,

    /// <summary>
    /// A different file is at the destination.
    /// </summary>
    /// <remarks>
    /// Never overwritten. A user's working BIOS beats RomMBat's idea of the correct one, and
    /// the report is how they find out the two disagree.
    /// </remarks>
    Mismatch,

    /// <summary>Nothing in the RomM library hashes to this, so the user has to find it elsewhere.</summary>
    MissingFromLibrary,

    /// <summary>Not on disk, and the server was not asked. What an offline pass says instead of guessing.</summary>
    NotChecked,

    /// <summary>
    /// RetroBat names no md5 for this file, so nothing can be said about it in either direction.
    /// </summary>
    /// <remarks>
    /// 172 of the 346 requirements, and 28 systems have nothing else. This is a fact about
    /// RetroBat's manifest and not a gap in the user's library, which is why it is its own
    /// state rather than a kind of missing.
    /// </remarks>
    Unverifiable,

    /// <summary>It would not fit inside the budget or the free-space floor.</summary>
    Blocked,
}

/// <summary>One required BIOS file, and what the pass would do about it.</summary>
public sealed record BiosStep
{
    public required BiosRequirement Requirement { get; init; }

    public required BiosAction Action { get; init; }

    /// <summary>The RomM record the md5 matched, when one did.</summary>
    public FirmwareRow? Match { get; init; }

    /// <summary>What is actually at the destination, when something is.</summary>
    public string? FoundMd5 { get; init; }

    public long BytesToTransfer { get; init; }

    public string? Reason { get; init; }

    /// <summary>Where this file belongs, relative to the RetroBat root.</summary>
    public RelativePath Path => Requirement.Path;

    /// <summary>True when a user has to do something about it.</summary>
    public bool NeedsAttention =>
        Action is BiosAction.Mismatch or BiosAction.MissingFromLibrary or BiosAction.Blocked;
}

/// <summary>What a BIOS pass would do, before anything is done.</summary>
public sealed record BiosPlan
{
    public IReadOnlyList<BiosStep> Steps { get; init; } = [];

    /// <summary>False when the server was never asked, which changes what a gap means.</summary>
    public bool CheckedAgainstServer { get; init; }

    /// <summary>The folders this plan covers, in the order they were asked for.</summary>
    public IReadOnlyList<string> Folders { get; init; } = [];

    public IEnumerable<BiosStep> Downloads => Steps.Where(step => step.Action == BiosAction.Download);

    /// <summary>How many distinct files would cross the network. One md5 can owe several writes.</summary>
    public int DownloadCount =>
        Downloads.Select(step => step.Requirement.Md5).Distinct(StringComparer.Ordinal).Count();

    /// <summary>What those downloads cost, counting each md5 once.</summary>
    public long BytesToTransfer =>
        Downloads
            .GroupBy(step => step.Requirement.Md5, StringComparer.Ordinal)
            .Sum(group => group.First().BytesToTransfer);

    public int Count(BiosAction action) => Steps.Count(step => step.Action == action);

    /// <summary>True when the pass would write nothing at all.</summary>
    public bool IsNoOp => Steps.All(step => step.Action is not (BiosAction.Download or BiosAction.Adopt));

    public string Summary
    {
        get
        {
            if (Steps.Count == 0)
            {
                return "no BIOS is required for these systems";
            }

            var parts = new List<string>();

            if (DownloadCount > 0)
            {
                parts.Add($"{DownloadCount} to fetch ({ByteSize.Format(BytesToTransfer)})");
            }

            Add(BiosAction.Adopt, "already on disk to adopt");
            Add(BiosAction.AlreadyPresent, "present");
            Add(BiosAction.Mismatch, "on disk but not what RetroBat expects");
            Add(BiosAction.MissingFromLibrary, "not in your RomM library");
            Add(BiosAction.NotChecked, "not on disk, and RomM was not asked");
            Add(BiosAction.Unverifiable, "RetroBat names no hash for");
            Add(BiosAction.Blocked, "blocked by the budget");

            return parts.Count == 0 ? "nothing to do" : string.Join(", ", parts);

            void Add(BiosAction action, string label)
            {
                var count = Count(action);
                if (count > 0)
                {
                    parts.Add($"{count} {label}");
                }
            }
        }
    }
}

/// <summary>
/// Works out which BIOS files a set of RetroBat systems needs, and where each one stands.
/// </summary>
/// <remarks>
/// <b>The join is md5 and nothing else.</b> Not the file name, because RomM serves files
/// under names RetroBat does not use, and not <c>is_verified</c>, because it is false on
/// files RetroBat requires: <c>psxonpsp660.bin</c>, without which no PS1 game runs at all,
/// is false on every copy of it. Both are one-line changes a later contributor will make in
/// good faith, so both are covered by tests carrying the real filenames.
/// <para>
/// <b>A requirement with no md5 is a third state.</b> It can be neither found in RomM nor
/// recognised on disk, so it is reported as unverifiable and never as missing: telling a user
/// to go and find a file RomMBat could not recognise if they already had it is worse than
/// saying nothing.
/// </para>
/// <para>
/// <b>Nothing here overwrites.</b> A file at a destination whose md5 disagrees is left exactly
/// where it is, because a working BIOS the user installed beats this table's idea of the right
/// one, and one whose md5 agrees is adopted rather than re-fetched. <c>bios/</c> holds
/// thousands of files that are not RomMBat's, emulator user data and save states among them.
/// </para>
/// </remarks>
public sealed class BiosPlanner
{
    private readonly RetroBatInstall _install;
    private readonly LocalStore _store;
    private readonly BiosManifest _manifest;
    private readonly FilesystemLimits _limits;

    public BiosPlanner(
        RetroBatInstall install,
        LocalStore store,
        BiosManifest? manifest = null,
        FilesystemLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(store);

        _install = install;
        _store = store;
        _manifest = manifest ?? BiosManifest.Bundled;
        _limits = limits ?? FilesystemLimits.Inspect(install.RootPath);
    }

    /// <summary>Where an in-flight firmware transfer is kept.</summary>
    /// <remarks>
    /// Under the app's own directory, never beside the target, so an interrupted write cannot
    /// leave a half-file in <c>bios/</c> for an emulator to load. Named from the md5, which is
    /// what the file is, and which several destinations share.
    /// </remarks>
    public static RelativePath PartFor(string md5)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(md5);
        return RetroBatInstall.AppDirectory.Combine($"partial/bios-{md5.Trim().ToLowerInvariant()}.part");
    }

    /// <summary>
    /// Indexes candidates by md5, taking one record per hash.
    /// </summary>
    /// <remarks>
    /// One md5 legitimately sits on several platform rows, 504 of 656 records on the library
    /// measured, because a user may file one system under more than one folder and put the
    /// firmware under each. Multiplicity is not ambiguity: any copy is the same bytes by
    /// definition of the join. A record flagged <c>missing_from_fs</c> loses to one that is
    /// not, because the server answers 500 for it rather than serving anything.
    /// </remarks>
    public static IReadOnlyDictionary<string, FirmwareRow> IndexByMd5(IEnumerable<PlatformRow> platforms)
    {
        ArgumentNullException.ThrowIfNull(platforms);

        var index = new Dictionary<string, FirmwareRow>(StringComparer.Ordinal);

        foreach (var row in platforms.SelectMany(platform => platform.Firmware))
        {
            if (row.NormalizedMd5 is not { } md5)
            {
                continue;
            }

            if (!index.TryGetValue(md5, out var existing) || (!row.MissingFromFs && existing.MissingFromFs))
            {
                index[md5] = row;
            }
        }

        return index;
    }

    /// <summary>Every RetroBat folder that has a BIOS requirement and holds ROMs RomMBat synced.</summary>
    public IReadOnlyList<string> FoldersNeedingBios() =>
        [.. _store.Files.List(kind: LocalFileKind.Rom)
            .Select(file => file.Folder!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(folder => _manifest.For(folder).Count > 0)
            .Order(StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// Works out what a BIOS pass over these folders would do.
    /// </summary>
    /// <param name="folders">
    /// The RetroBat folders to cover. Null covers every system the manifest names, which is
    /// what the whole-library report wants.
    /// </param>
    /// <param name="candidates">
    /// What RomM holds, indexed by md5, or null when the server was not asked. Null is not an
    /// error: the report still answers from the manifest and what is on disk, which is what
    /// makes it useful on a handheld away from the network.
    /// </param>
    public BiosPlan Plan(
        IEnumerable<string>? folders = null,
        IReadOnlyDictionary<string, FirmwareRow>? candidates = null)
    {
        var wanted = folders is null
            ? [.. _manifest.Folders.Order(StringComparer.OrdinalIgnoreCase)]
            : folders.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var budget = _store.Settings.GetInt64(SettingStore.ContentMaxBytes);
        var floor = _store.Settings.GetInt64(SettingStore.FreeSpaceFloorBytes)
            ?? SettingStore.DefaultFreeSpaceFloorBytes;

        var managed = _store.Files.List().Where(file => file.Origin == FileOrigin.Synced).Sum(file => file.SizeBytes);
        var free = _limits.AvailableFreeBytes;
        var steps = new List<BiosStep>();

        // One md5 can be required by several systems at several paths. The bytes cross the
        // network once, so only the first destination of a hash is charged for them.
        var charged = new HashSet<string>(StringComparer.Ordinal);

        foreach (var folder in wanted)
        {
            foreach (var requirement in _manifest.For(folder))
            {
                var step = Inspect(requirement, candidates);

                if (step.Action == BiosAction.Download && charged.Add(requirement.Md5!))
                {
                    if (budget is { } cap && managed + step.BytesToTransfer > cap)
                    {
                        steps.Add(step with
                        {
                            Action = BiosAction.Blocked,
                            BytesToTransfer = 0,
                            Reason = $"the {ByteSize.Format(cap)} budget is full. Raise it, or evict a game "
                                + "to make room. A system without its BIOS will not launch.",
                        });
                        continue;
                    }

                    if (free - step.BytesToTransfer < floor)
                    {
                        steps.Add(step with
                        {
                            Action = BiosAction.Blocked,
                            BytesToTransfer = 0,
                            Reason = $"this drive would drop below the {ByteSize.Format(floor)} of free space "
                                + "RomMBat is told to leave.",
                        });
                        continue;
                    }

                    managed += step.BytesToTransfer;
                    free -= step.BytesToTransfer;
                }

                steps.Add(step);
            }
        }

        return new BiosPlan
        {
            Steps = steps,
            CheckedAgainstServer = candidates is not null,
            Folders = wanted,
        };
    }

    /// <summary>Decides where one required file stands.</summary>
    private BiosStep Inspect(BiosRequirement requirement, IReadOnlyDictionary<string, FirmwareRow>? candidates)
    {
        var step = new BiosStep { Requirement = requirement, Action = BiosAction.NotChecked };
        var absolute = _install.Resolve(requirement.Path);
        var info = new FileInfo(absolute);

        if (requirement.Md5 is not { } wanted)
        {
            return step with
            {
                Action = BiosAction.Unverifiable,
                Reason = info.Exists
                    ? "a file is there, and RetroBat names no hash to check it against."
                    : "RetroBat names no hash for this file, so RomMBat cannot find it or recognise it.",
            };
        }

        if (info.Exists)
        {
            var known = _store.Files.Find(requirement.Path);

            // The cheap path, and the one a second pass takes: the inventory says this is the
            // file and the disk still agrees about its size.
            if (known is { Kind: LocalFileKind.Firmware } recorded
                && ContentHasher.Matches(recorded.Md5Hash, wanted)
                && recorded.SizeBytes == info.Length)
            {
                return step with { Action = BiosAction.AlreadyPresent };
            }

            var found = ContentHasher.ComputeFileMd5(absolute);

            if (ContentHasher.Matches(found, wanted))
            {
                return step with
                {
                    Action = BiosAction.Adopt,
                    FoundMd5 = found,
                    Reason = "already on disk and its content matches",
                };
            }

            return step with
            {
                Action = BiosAction.Mismatch,
                FoundMd5 = found,
                Reason = $"a different file is there ({found}). It is left exactly as it is: "
                    + "a BIOS that works for you beats the one RetroBat lists.",
            };
        }

        if (candidates is null)
        {
            return step with
            {
                Action = BiosAction.NotChecked,
                Reason = "not on disk. Run this again with the server reachable to see whether RomM has it.",
            };
        }

        if (candidates.TryGetValue(wanted, out var match) && !match.MissingFromFs)
        {
            return step with
            {
                Action = BiosAction.Download,
                Match = match,
                BytesToTransfer = match.SizeBytes,
            };
        }

        return step with
        {
            Action = BiosAction.MissingFromLibrary,
            Match = match,

            // A row whose file the server lost reads as "your library has it" everywhere else
            // in RomM, so it is worth saying which of the two this is.
            Reason = match is null
                ? null
                : "RomM has a record for it but no longer has the file itself.",
        };
    }
}
