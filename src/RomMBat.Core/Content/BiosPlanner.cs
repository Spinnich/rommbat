using RomM.Client.Catalog;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;

namespace RomMBat.Core.Content;

/// <summary>What a BIOS pass would do about one slot.</summary>
public enum BiosAction
{
    /// <summary>An accepted file is already there and RomMBat knows it.</summary>
    AlreadyPresent,

    /// <summary>An accepted file is there and nothing recorded it, so it is recorded rather than fetched.</summary>
    Adopt,

    /// <summary>RomM has an accepted file and this install does not.</summary>
    Download,

    /// <summary>
    /// RomMBat's own file is there, unchanged, and RomM now has a better-ranked accepted one.
    /// </summary>
    /// <remarks>
    /// The one deliberate exception to never overwriting under <c>bios/</c>, and only for a file
    /// RomMBat downloaded itself. A file the user placed or RomMBat adopted keeps its place and
    /// carries <see cref="BiosStep.Better"/> instead.
    /// </remarks>
    Upgrade,

    /// <summary>
    /// A file is at the destination that the slot does not accept.
    /// </summary>
    /// <remarks>
    /// Never overwritten. A user's working BIOS beats RomMBat's idea of the correct one, and
    /// the report is how they find out the two disagree.
    /// </remarks>
    Mismatch,

    /// <summary>A zip is at the destination whose members satisfy no reader. Left exactly as it is.</summary>
    ContentsMismatch,

    /// <summary>
    /// RomM has several accepted files the ranking cannot separate, so none is fetched.
    /// </summary>
    /// <remarks>Reported rather than guessed: <see cref="BiosStep.Rivals"/> names them.</remarks>
    Ambiguous,

    /// <summary>Nothing in the RomM library is accepted here, so the user has to find it elsewhere.</summary>
    MissingFromLibrary,

    /// <summary>Not on disk, and the server was not asked. What an offline pass says instead of guessing.</summary>
    NotChecked,

    /// <summary>
    /// No source identifies this file, so nothing can be said about it in either direction.
    /// </summary>
    /// <remarks>
    /// A fact about the sources and not a gap in the user's library, which is why it is its own
    /// state rather than a kind of missing.
    /// </remarks>
    Unverifiable,

    /// <summary>It would not fit inside the budget or the free-space floor.</summary>
    Blocked,
}

/// <summary>One firmware slot, and what the pass would do about it.</summary>
public sealed record BiosStep
{
    public required BiosRequirement Requirement { get; init; }

    public required BiosAction Action { get; init; }

    /// <summary>The RomM record to fetch, or the one a gap is about.</summary>
    public FirmwareRow? Match { get; init; }

    /// <summary>
    /// The accepted file the pick chose, whose md5 the download is checked against. Null for a
    /// zip fetched by name, which is checked by its members instead.
    /// </summary>
    public BiosAccepted? Chosen { get; init; }

    /// <summary>The md5 of what is on disk, where the step read it.</summary>
    public string? Found { get; init; }

    /// <summary>
    /// A better-ranked file RomM holds, for a slot whose file RomMBat will not replace.
    /// </summary>
    public FirmwareRow? Better { get; init; }

    /// <summary>For <see cref="BiosAction.Ambiguous"/>: the records the ranking could not separate.</summary>
    public IReadOnlyList<FirmwareRow> Rivals { get; init; } = [];

    /// <summary>
    /// A library record under the slot's exact name, when nothing identifies the slot.
    /// </summary>
    /// <remarks>Pointed at, never fetched: with no identity there is nothing to compare.</remarks>
    public FirmwareRow? SameName { get; init; }

    public long BytesToTransfer { get; init; }

    public string? Reason { get; init; }

    /// <summary>Where this file belongs, relative to the RetroBat root.</summary>
    public RelativePath Path => Requirement.Path;

    /// <summary>Which bytes cross the network, so a transfer several slots owe is made once.</summary>
    public string? TransferKey => Match?.NormalizedMd5 ?? Chosen?.Md5;

    /// <summary>True for a step that fetches something.</summary>
    public bool Transfers => Action is BiosAction.Download or BiosAction.Upgrade;
}

/// <summary>What a BIOS pass would do, before anything is done.</summary>
public sealed record BiosPlan
{
    public IReadOnlyList<BiosStep> Steps { get; init; } = [];

    /// <summary>False when the server was never asked, which changes what a gap means.</summary>
    public bool CheckedAgainstServer { get; init; }

    /// <summary>The order a tie between regions was broken in.</summary>
    public FirmwareRegionOrder RegionOrder { get; init; } = FirmwareRegionOrder.Default;

    public IEnumerable<BiosStep> Downloads => Steps.Where(step => step.Transfers);

    /// <summary>How many distinct files would cross the network. One file can owe several writes.</summary>
    public int DownloadCount =>
        Downloads.Select(step => step.TransferKey).Distinct(StringComparer.Ordinal).Count();

    /// <summary>What those downloads cost, counting each file once.</summary>
    public long BytesToTransfer =>
        Downloads
            .GroupBy(step => step.TransferKey, StringComparer.Ordinal)
            .Sum(group => group.First().BytesToTransfer);

    public int Count(BiosAction action) => Steps.Count(step => step.Action == action);

    /// <summary>True when the pass would write nothing at all.</summary>
    public bool IsNoOp => Steps.All(step => step.Action is not (BiosAction.Download or BiosAction.Adopt or BiosAction.Upgrade));

    public string Summary
    {
        get
        {
            if (Steps.Count == 0)
            {
                return "no firmware is listed for these systems";
            }

            var parts = new List<string>();

            // One size for every transfer, fresh and upgrade alike, since BytesToTransfer counts both.
            var transfers = new List<string>();
            var fetches = Distinct(BiosAction.Download);
            var upgrades = Distinct(BiosAction.Upgrade);

            if (fetches > 0)
            {
                transfers.Add($"{fetches} to fetch");
            }

            if (upgrades > 0)
            {
                transfers.Add($"{upgrades} to upgrade");
            }

            if (transfers.Count > 0)
            {
                parts.Add($"{string.Join(" and ", transfers)} ({ByteSize.Format(BytesToTransfer)})");
            }

            Add(BiosAction.Adopt, "already on disk to adopt");
            Add(BiosAction.AlreadyPresent, "present");
            Add(BiosAction.Mismatch, "on disk but not a file any reader accepts");
            Add(BiosAction.ContentsMismatch, "zips on disk whose contents no reader accepts");
            Add(BiosAction.Ambiguous, "with several equally ranked files in RomM");
            Add(BiosAction.MissingFromLibrary, "not in your RomM library");
            Add(BiosAction.NotChecked, "not on disk, and RomM was not asked");
            Add(BiosAction.Unverifiable, "no source identifies");
            Add(BiosAction.Blocked, "blocked by the budget");

            var better = Steps.Count(step => step.Better is not null);
            if (better > 0)
            {
                parts.Add($"{better} where RomM has a better-ranked file");
            }

            return parts.Count == 0 ? "nothing to do" : string.Join(", ", parts);

            int Distinct(BiosAction action) =>
                Downloads.Where(step => step.Action == action).Select(step => step.TransferKey).Distinct(StringComparer.Ordinal).Count();

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
/// Works out which firmware a set of RetroBat systems' emulators read, and where each slot stands.
/// </summary>
/// <remarks>
/// <b>A single file is joined on md5 and nothing else.</b> Not the file name, because RomM serves
/// files under names RetroBat does not use, and not <c>is_verified</c>, because it is false on
/// files RetroBat requires: <c>psxonpsp660.bin</c>, without which no PS1 game runs at all, is
/// false on every copy of it. The manifest already holds every hash a source accepts.
/// <para>
/// <b>Several accepted files can compete for one slot, and the pick is ranked</b>: RetroBat's own
/// md5 first, since its ES BIOS check reports the file missing otherwise, then the emulator's
/// priority, then <see cref="FirmwareRegionOrder"/>. A tie left after all three is
/// <see cref="BiosAction.Ambiguous"/>: reported, never guessed.
/// </para>
/// <para>
/// <b>A zip is fetched by name and verified by its members.</b> Its container md5 depends on how
/// it was packed (RM-28), so where no accepted md5 is in RomM, a same-named zip is fetched and
/// lands only if <see cref="BiosZip"/> finds a reader it satisfies.
/// </para>
/// <para>
/// <b>Nothing here overwrites a file RomMBat did not write.</b> A file at a destination that the
/// slot does not accept is left exactly where it is, and an accepted one is adopted rather than
/// re-fetched. The only replacement is <see cref="BiosAction.Upgrade"/>, of RomMBat's own file,
/// unchanged since it landed, by a better-ranked one. <c>bios/</c> holds thousands of files that
/// are not RomMBat's, emulator user data and save states among them.
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
    /// leave a half-file in <c>bios/</c> for an emulator to load. Named from the md5 RomM gives
    /// the file, which several destinations share.
    /// </remarks>
    public static RelativePath PartFor(string md5)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(md5);
        return RetroBatInstall.PartialDirectory.Combine($"bios-{md5.Trim().ToLowerInvariant()}.part");
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

    /// <summary>
    /// Every RetroBat folder RomMBat has synced a ROM into, whatever its firmware needs.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="FoldersNeedingBios"/> so a caller can tell an install that has
    /// synced nothing from one whose systems simply need no firmware, which is the ordinary case
    /// for many libraries and reads as the first if the two are collapsed.
    /// </remarks>
    public IReadOnlyList<string> SyncedFolders() =>
        [.. _store.Files.List(kind: LocalFileKind.Rom)
            .Select(file => file.Folder!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)];

    /// <summary>Every RetroBat folder that has a firmware slot and holds ROMs RomMBat synced.</summary>
    public IReadOnlyList<string> FoldersNeedingBios() =>
        [.. SyncedFolders().Where(folder => _manifest.For(folder).Count > 0)];

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

        var order = FirmwareRegionOrder.From(_store.Settings);
        var budget = _store.Settings.GetInt64(SettingStore.ContentMaxBytes);
        var floor = _store.Settings.GetInt64(SettingStore.FreeSpaceFloorBytes)
            ?? SettingStore.DefaultFreeSpaceFloorBytes;

        var managed = _store.Files.List(includeStale: true).Where(file => file.Origin == FileOrigin.Synced).Sum(file => file.SizeBytes);
        var free = _limits.AvailableFreeBytes;
        var steps = new List<BiosStep>();

        // Two counters, because the two costs are not the same. The bytes cross the network
        // once per file however many systems want them, but they land once per destination and
        // the budget is a disk budget: a hash owing three paths puts three files on the disk.
        var charged = new HashSet<string>(StringComparer.Ordinal);
        var placed = new HashSet<RelativePath>();

        // A transfer that will not happen cannot be copied to its other destinations either, so
        // the first refusal decides them all rather than each one deciding alone.
        var refused = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var folder in wanted)
        {
            foreach (var requirement in _manifest.For(folder))
            {
                var step = Inspect(requirement, candidates, order);

                if (step.Transfers)
                {
                    var key = step.TransferKey!;

                    if (refused.TryGetValue(key, out var earlier))
                    {
                        steps.Add(Block(step, earlier));
                        continue;
                    }

                    var transfers = !charged.Contains(key);

                    // An upgrade replaces a file the budget already counts, so only the growth is new.
                    var replaced = step.Action == BiosAction.Upgrade ? new FileInfo(_install.Resolve(step.Path)).Length : 0;
                    var disk = placed.Contains(requirement.Path) ? 0 : Math.Max(0, step.BytesToTransfer - replaced);

                    if (budget is { } cap && managed + disk > cap)
                    {
                        var reason = $"the {ByteSize.Format(cap)} budget is full. Raise it, or evict a game "
                            + "to make room. A system without its BIOS will not launch.";

                        Refuse(key, reason, transfers);
                        steps.Add(Block(step, reason));
                        continue;
                    }

                    if (free - disk < floor)
                    {
                        var reason = $"this drive would drop below the {ByteSize.Format(floor)} of free space "
                            + "RomMBat is told to leave.";

                        Refuse(key, reason, transfers);
                        steps.Add(Block(step, reason));
                        continue;
                    }

                    managed += disk;
                    free -= disk;
                    charged.Add(key);
                    placed.Add(requirement.Path);
                }

                steps.Add(step);
            }
        }

        // Only a refusal of the transfer itself carries to its other destinations. One
        // destination blocked after the bytes were already paid for blocks nothing else.
        void Refuse(string key, string reason, bool transfers)
        {
            if (transfers)
            {
                refused[key] = reason;
            }
        }

        return new BiosPlan
        {
            Steps = steps,
            CheckedAgainstServer = candidates is not null,
            RegionOrder = order,
        };
    }

    /// <summary>
    /// Ranks the accepted files RomM can serve, best first, and says whether the best is a tie.
    /// </summary>
    public static (IReadOnlyList<(BiosAccepted Accepted, FirmwareRow Row)> Ranked, bool Tied) Rank(
        BiosRequirement requirement,
        IReadOnlyDictionary<string, FirmwareRow> candidates,
        FirmwareRegionOrder order)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(order);

        var ranked = requirement.Accepted
            .Select(accepted => (Accepted: accepted, Row: candidates.TryGetValue(accepted.Md5, out var row) ? row : null))
            .Where(pair => pair.Row is { IsFetchable: true })
            .Select(pair => (pair.Accepted, Row: pair.Row!))
            .OrderBy(pair => Key(pair.Accepted, order))
            .ToList();

        var tied = ranked.Count > 1 && Key(ranked[0].Accepted, order) == Key(ranked[1].Accepted, order);
        return (ranked, tied);
    }

    private static (int RetroBat, int Priority, int Region) Key(BiosAccepted accepted, FirmwareRegionOrder order) =>
        (accepted.IsRetroBats ? 0 : 1, accepted.Priority ?? int.MaxValue, order.Rank(accepted.Regions));

    private static BiosStep Block(BiosStep step, string reason) => step with
    {
        Action = BiosAction.Blocked,
        BytesToTransfer = 0,
        Reason = reason,
    };

    /// <summary>Decides where one slot stands.</summary>
    private BiosStep Inspect(
        BiosRequirement requirement,
        IReadOnlyDictionary<string, FirmwareRow>? candidates,
        FirmwareRegionOrder order)
    {
        var step = new BiosStep { Requirement = requirement, Action = BiosAction.NotChecked };
        var absolute = _install.Resolve(requirement.Path);
        var info = new FileInfo(absolute);

        if (info.Exists)
        {
            return OnDisk(step, absolute, info, candidates, order);
        }

        // A reader that takes the file under another name has it already.
        foreach (var alias in requirement.Aliases)
        {
            var other = _install.Resolve(alias);
            if (File.Exists(other) && requirement.Accepts(ContentHasher.ComputeFileMd5(other)) is not null)
            {
                return step with
                {
                    Action = BiosAction.AlreadyPresent,
                    Reason = $"present as {alias.Name}, a name its reader accepts",
                };
            }
        }

        if (requirement.IsUnverifiable)
        {
            // A lead, never a join: the state stays Unverifiable and nothing is fetched.
            if (candidates is not null && SameNamed(requirement, candidates) is { } named)
            {
                return step with
                {
                    Action = BiosAction.Unverifiable,
                    SameName = named,
                    Reason = $"RomM has a {named.FileName} ({named.NormalizedMd5}). No source names a hash "
                        + "to say whether it is this file, so it is not fetched.",
                };
            }

            return step with
            {
                Action = BiosAction.Unverifiable,
                Reason = "no source names a hash for this file, so RomMBat cannot find it or recognize it.",
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

        var (ranked, tied) = Rank(requirement, candidates, order);

        if (tied)
        {
            var best = ranked.TakeWhile(pair => Key(pair.Accepted, order) == Key(ranked[0].Accepted, order)).ToList();

            return step with
            {
                Action = BiosAction.Ambiguous,
                Rivals = [.. best.Select(pair => pair.Row)],
                Reason = $"RomM has {best.Count} files this slot accepts that nothing ranks apart "
                    + $"({string.Join(", ", best.Select(pair => pair.Row.FileName))}), so none is fetched.",
            };
        }

        if (ranked.Count > 0)
        {
            var (chosen, row) = ranked[0];

            return step with
            {
                Action = BiosAction.Download,
                Match = row,
                Chosen = chosen,
                BytesToTransfer = row.SizeBytes,
            };
        }

        if (requirement.IsZip && SameNamed(requirement, candidates) is { } zip)
        {
            if (requirement.Contents.Count > 0)
            {
                return step with
                {
                    Action = BiosAction.Download,
                    Match = zip,
                    BytesToTransfer = zip.SizeBytes,
                    Reason = $"fetched by name, and landed only if its members satisfy a reader",
                };
            }

            return step with
            {
                Action = BiosAction.MissingFromLibrary,
                SameName = zip,
                Reason = $"RomM has a {zip.FileName} built differently ({zip.NormalizedMd5}). No reader lists "
                    + "what is inside it, so it is not fetched.",
            };
        }

        var lost = requirement.Accepted
            .Select(accepted => candidates.TryGetValue(accepted.Md5, out var row) ? row : null)
            .FirstOrDefault(row => row is not null);

        return step with
        {
            Action = BiosAction.MissingFromLibrary,
            Match = lost,

            // A row whose file the server lost reads as "your library has it" everywhere else
            // in RomM, so it is worth saying which of the two this is.
            Reason = lost is null
                ? null
                : "RomM has a record for it but no longer has the file itself.",
        };
    }

    /// <summary>Decides where a slot stands when something is already at its path.</summary>
    private BiosStep OnDisk(
        BiosStep step,
        string absolute,
        FileInfo info,
        IReadOnlyDictionary<string, FirmwareRow>? candidates,
        FirmwareRegionOrder order)
    {
        var requirement = step.Requirement;

        if (requirement.IsUnverifiable)
        {
            return step with
            {
                Action = BiosAction.Unverifiable,
                Reason = "a file is there, and no source names a hash to check it against.",
            };
        }

        var known = _store.Files.Find(requirement.Path);

        // The cheap path, and the one a second pass takes: the inventory says this is the file
        // and the disk still agrees about its size. A zip was checked by its members when it was
        // recorded, so its recorded md5 stands for that check.
        if (known is { Kind: LocalFileKind.Firmware } recorded
            && recorded.SizeBytes == info.Length
            && (requirement.Accepts(recorded.Md5Hash) is not null || (requirement.Contents.Count > 0 && recorded.Md5Hash is not null)))
        {
            return Better(step with { Action = BiosAction.AlreadyPresent, Found = recorded.Md5Hash }, recorded, candidates, order);
        }

        var found = ContentHasher.ComputeFileMd5(absolute);

        if (requirement.Accepts(found) is not null)
        {
            return Better(
                step with { Action = BiosAction.Adopt, Found = found, Reason = "already on disk and its content matches" },
                recorded: null,
                candidates,
                order);
        }

        if (requirement.Contents.Count > 0)
        {
            var satisfied = BiosZip.Satisfied(absolute, requirement.Contents);

            if (satisfied is { Count: > 0 })
            {
                return step with
                {
                    Action = BiosAction.Adopt,
                    Found = found,
                    Reason = $"already on disk, and its members satisfy {string.Join(", ", satisfied)}",
                };
            }

            return step with
            {
                Action = BiosAction.ContentsMismatch,
                Found = found,
                Reason = "a zip is there whose members satisfy none of its readers. It is left exactly as it is.",
            };
        }

        return step with
        {
            Action = BiosAction.Mismatch,
            Found = found,
            Reason = $"a different file is there ({found}). It is left exactly as it is: "
                + "a BIOS that works for you beats the one the sources list.",
        };
    }

    /// <summary>
    /// Turns a present file into an upgrade or a note when RomM now has a better-ranked one.
    /// </summary>
    /// <remarks>
    /// Only RomMBat's own download, still the bytes it landed, is replaced. Anything the user put
    /// there, or RomMBat adopted, keeps its place and the report names what RomM has instead.
    /// </remarks>
    private BiosStep Better(
        BiosStep step,
        LocalFile? recorded,
        IReadOnlyDictionary<string, FirmwareRow>? candidates,
        FirmwareRegionOrder order)
    {
        if (candidates is null || step.Requirement.Accepts(step.Found) is not { } current)
        {
            return step;
        }

        var (ranked, tied) = Rank(step.Requirement, candidates, order);

        if (ranked.Count == 0 || tied || ranked[0].Accepted.Md5 == current.Md5 || Key(ranked[0].Accepted, order).CompareTo(Key(current, order)) >= 0)
        {
            return step;
        }

        var (chosen, row) = ranked[0];

        if (recorded is { Origin: FileOrigin.Synced }
            && ContentHasher.Matches(ContentHasher.ComputeFileMd5(_install.Resolve(step.Path)), recorded.Md5Hash))
        {
            return step with
            {
                Action = BiosAction.Upgrade,
                Match = row,
                Chosen = chosen,
                BytesToTransfer = row.SizeBytes,
                Reason = $"RomMBat's own {step.Requirement.FileName} is replaced by a better-ranked file RomM now has ({row.FileName})",
            };
        }

        return step with
        {
            Better = row,
            Reason = $"RomM has a better-ranked file for this slot ({row.FileName}, {row.NormalizedMd5}). "
                + "The one here was not downloaded by RomMBat, so it stays.",
        };
    }

    /// <summary>A fetchable library record under the slot's exact name, the lowest id first.</summary>
    private static FirmwareRow? SameNamed(
        BiosRequirement requirement,
        IReadOnlyDictionary<string, FirmwareRow> candidates)
    {
        return candidates.Values
            .Where(row => row.IsFetchable
                && string.Equals(row.FileName, requirement.FileName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(row => row.Id)
            .FirstOrDefault();
    }
}
