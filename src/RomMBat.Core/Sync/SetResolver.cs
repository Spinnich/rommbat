using System.Globalization;
using RomM.Client;
using RomM.Client.Catalog;
using RomMBat.Core.Content;
using RomMBat.Core.Mapping;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;

namespace RomMBat.Core.Sync;

/// <summary>How a resolution ended.</summary>
public enum ResolutionOutcome
{
    /// <summary>The whole scope was walked and the set is up to date.</summary>
    Resolved,

    /// <summary>The walk stopped early. The offset is recorded and the next run continues it.</summary>
    Interrupted,

    /// <summary>The set names a platform that needs a folder chosen, and none was.</summary>
    NeedsFolderChoice,

    /// <summary>The scope is unbounded and too large to resolve without a cap.</summary>
    Refused,
}

/// <summary>What one resolution produced.</summary>
public sealed record SetResolution
{
    public required SyncSetDefinition Set { get; init; }

    public required ResolutionOutcome Outcome { get; init; }

    /// <summary>The games in the set, in the set's own order.</summary>
    public IReadOnlyList<SyncSetMember> Members { get; init; } = [];

    /// <summary>Games the scope matched but this install cannot use, with the reason on each.</summary>
    public IReadOnlyList<SyncSetMember> Excluded { get; init; } = [];

    /// <summary>How many rows the server said the scope matches.</summary>
    public int ScopeTotal { get; init; }

    /// <summary>How many rows were actually read.</summary>
    public int Scanned { get; init; }

    /// <summary>Bytes the members add up to.</summary>
    public long Bytes { get; init; }

    /// <summary>Candidates dropped because the set is full. Counted, not listed: a cap doing its job is not a fault.</summary>
    public int OverCount { get; init; }

    /// <summary>Candidates dropped because the set's byte budget is full.</summary>
    public int OverBytes { get; init; }

    /// <summary>Excluded extensions and how many games each cost, for the message the user reads.</summary>
    public IReadOnlyDictionary<string, int> ExcludedExtensions { get; init; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Platforms with no RetroBat folder, and how many games each cost.</summary>
    public IReadOnlyDictionary<string, int> UnmappedPlatforms { get; init; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Games RomM holds as several files, which v1 does not sync.</summary>
    public int MultiFile { get; init; }

    /// <summary>Games the target filesystem cannot hold, which today means over 4 GB on FAT32.</summary>
    public int TooLargeForFilesystem { get; init; }

    /// <summary>Folders the members land in. Two RomM platforms can share one.</summary>
    public IReadOnlyList<string> Folders { get; init; } = [];

    /// <summary>Why the resolution stopped, when it did not finish.</summary>
    public string? Problem { get; init; }

    /// <summary>One line, stored on the set so <c>status</c> can report it with no server.</summary>
    public string Summary { get; init; } = string.Empty;
}

/// <summary>
/// Turns a sync set's scope into the list of games it currently resolves to.
/// </summary>
/// <remarks>
/// <b>Every set is re-resolved on every sync.</b> Smart-collection membership is a
/// server-side saved search and drifts without anyone touching the set, so a stored
/// membership is a cache of the last answer and never the answer.
/// <para>
/// <b>Memory is bounded by the set, never by the library.</b> One page of 250 rows is in
/// flight at a time and nothing accumulates the catalog. What is held is a buffer of the
/// ordering-best candidates, capped at <c>max_games</c> for a set with only a game cap and
/// at <see cref="UncappedScopeLimit"/> otherwise, so a resolve holds the same ceiling
/// whether the library has 5,000 ROMs or 83,000.
/// </para>
/// <para>
/// <b>The caps are greedy, not a knapsack.</b> The buffer is walked in the set's own order
/// and a candidate is taken when it still fits, so the result is the best-ordered subset
/// that fits. It does not search for a combination that would pack the budget more tightly.
/// Both halves are order-independent by construction: the ordering-best N of a stream does
/// not depend on the stream's order, and the pass over the buffer sees one fixed order.
/// </para>
/// </remarks>
public sealed class SetResolver
{
    /// <summary>
    /// How large an uncapped scope may be before it is refused.
    /// </summary>
    /// <remarks>
    /// A set with no <c>max_games</c> and no <c>max_bytes</c> holds one row per matching ROM,
    /// so a filter scope that matches everything would hold the whole library after all. The
    /// answer is a cap or a narrower scope, and saying so beats quietly using a gigabyte.
    /// </remarks>
    public const int UncappedScopeLimit = 50_000;

    /// <summary>
    /// Stands in for a ROM whose <c>fs_extension</c> is empty.
    /// </summary>
    /// <remarks>
    /// A real library has these: 23 of one instance's PS2 entries carry no extension at all.
    /// They are still excluded and still counted, but reporting a bare dot as the offending
    /// format reads as a bug rather than a fact.
    /// </remarks>
    public const string NoExtension = "(none)";

    private readonly EsSystemsFile _install;
    private readonly PlatformResolver _platforms;
    private readonly FilesystemLimits _limits;
    private readonly Dictionary<int, PlatformResolution> _resolutionCache = [];

    /// <param name="limits">
    /// What the target volume can hold. Supplied here rather than at download time because a
    /// ROM FAT32 cannot store is not a member of the set: counting it towards the budget and
    /// then refusing it would leave a set that never reaches its own target.
    /// </param>
    public SetResolver(EsSystemsFile install, PlatformResolver platforms, FilesystemLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(platforms);

        _install = install;
        _platforms = platforms;
        _limits = limits ?? FilesystemLimits.Unconstrained;
    }

    /// <summary>Builds the query one set resolves through.</summary>
    public static CatalogQuery QueryFor(SyncSetDefinition set)
    {
        ArgumentNullException.ThrowIfNull(set);

        return set.Scope == CatalogScopeKind.Filter
            ? new CatalogQuery { Scope = set.Scope, Filter = CatalogFilterJson.Parse(set.ScopeValue) }
            : new CatalogQuery { Scope = set.Scope, ScopeId = set.ScopeValue };
    }

    /// <summary>
    /// Walks the scope and works out what the set contains now.
    /// </summary>
    /// <param name="pager">
    /// Supplied rather than built here so a caller can start it at a recorded offset and
    /// continue an interrupted walk.
    /// </param>
    /// <param name="resolvedAt">
    /// The walk's start. Every row it produces is stamped with it, including the rows an
    /// earlier segment of the same walk already wrote.
    /// </param>
    /// <param name="carried">
    /// What earlier segments of this same walk resolved to, read back from the store. The
    /// caps apply to the whole walk rather than to each segment, and selection state does not
    /// survive the process, so a resumed run has to be handed back what it had.
    /// </param>
    public async Task<SetResolution> ResolveAsync(
        SyncSetDefinition set,
        RomPager pager,
        DateTimeOffset resolvedAt,
        IReadOnlyList<SyncSetMember>? carried = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(pager);

        var selector = new BoundedSelection(set);
        var excluded = new List<SyncSetMember>();
        var extensionCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var unmappedCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var scanned = 0;
        var multiFile = 0;
        var tooLarge = 0;
        RomMResponse<RomPage>? failure = null;

        foreach (var row in carried ?? [])
        {
            switch (Carry(row, selector, excluded, extensionCounts, unmappedCounts))
            {
                case MemberState.ExcludedMultiFile:
                    multiFile++;
                    break;
                case MemberState.ExcludedFilesystemLimit:
                    tooLarge++;
                    break;
                default:
                    break;
            }
        }

        while (!pager.IsComplete)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = await pager.NextAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccess)
            {
                failure = response;
                break;
            }

            var page = response.Value!;

            if (!set.MaxGames.HasValue && !set.MaxBytes.HasValue && page.Total > UncappedScopeLimit)
            {
                return Refused(set, page.Total);
            }

            foreach (var row in page.Items)
            {
                scanned++;

                var resolution = ResolveFolder(set, row);

                if (resolution.NeedsChoice)
                {
                    return NeedsChoice(set, row, resolution.Candidates);
                }

                if (resolution.Folder is null)
                {
                    Count(unmappedCounts, row.PlatformSlug);
                    excluded.Add(Member(row, null, MemberState.ExcludedUnmapped, resolvedAt));
                    continue;
                }

                // Checked before the extension, because a multi-file ROM has no extension to
                // judge and reporting it as an unsupported format would send someone to fix
                // the wrong thing.
                if (row.HasMultipleFiles)
                {
                    multiFile++;
                    excluded.Add(Member(row, resolution.Folder, MemberState.ExcludedMultiFile, resolvedAt));
                    continue;
                }

                if (!Accepts(resolution.Folder, row.FsExtension))
                {
                    Count(extensionCounts, string.IsNullOrWhiteSpace(row.FsExtension) ? NoExtension : row.FsExtension);
                    excluded.Add(Member(row, resolution.Folder, MemberState.ExcludedExtension, resolvedAt));
                    continue;
                }

                if (!_limits.CanHold(row.SizeBytes))
                {
                    tooLarge++;
                    excluded.Add(Member(row, resolution.Folder, MemberState.ExcludedFilesystemLimit, resolvedAt));
                    continue;
                }

                selector.Offer(Member(row, resolution.Folder, MemberState.Member, resolvedAt));
            }
        }

        var selected = selector.Drain();
        var members = new List<SyncSetMember>(selected.Count);
        for (var index = 0; index < selected.Count; index++)
        {
            members.Add(selected[index] with { Position = index + 1, ResolvedAt = resolvedAt });
        }

        var outcome = failure is not null || !pager.IsComplete
            ? ResolutionOutcome.Interrupted
            : ResolutionOutcome.Resolved;

        var resolutionResult = new SetResolution
        {
            Set = set,
            Outcome = outcome,
            Members = members,
            Excluded = excluded,
            ScopeTotal = pager.Total ?? scanned,
            Scanned = scanned,
            Bytes = members.Sum(member => member.SizeBytes),
            OverCount = selector.OverCount,
            OverBytes = selector.OverBytes,
            ExcludedExtensions = extensionCounts,
            UnmappedPlatforms = unmappedCounts,
            MultiFile = multiFile,
            TooLargeForFilesystem = tooLarge,
            Folders = [.. members.Select(member => member.Folder!).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)],
            Problem = failure?.Message,
        };

        return resolutionResult with { Summary = Describe(resolutionResult) };
    }

    /// <summary>One line describing a resolution, for <c>status</c> and for the set row.</summary>
    public static string Describe(SetResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);

        var parts = new List<string>
        {
            $"{resolution.Members.Count} games, {ByteSize.Format(resolution.Bytes)}",
        };

        if (resolution.Folders.Count > 0)
        {
            parts.Add("into " + string.Join(", ", resolution.Folders));
        }

        var skippedExtensions = resolution.ExcludedExtensions.Values.Sum();
        if (skippedExtensions > 0)
        {
            var extensions = resolution.ExcludedExtensions
                .OrderByDescending(pair => pair.Value)
                .Select(pair => string.Equals(pair.Key, NoExtension, StringComparison.Ordinal)
                    ? "no extension"
                    : "." + pair.Key);

            parts.Add($"{skippedExtensions} skipped, format not supported by this system ({string.Join(", ", extensions)})");
        }

        var skippedUnmapped = resolution.UnmappedPlatforms.Values.Sum();
        if (skippedUnmapped > 0)
        {
            parts.Add($"{skippedUnmapped} skipped, no RetroBat folder for {string.Join(", ", resolution.UnmappedPlatforms.Keys.Order(StringComparer.Ordinal))}");
        }

        // Named as multi-file rather than as a format problem: the format is not what is wrong
        // with a .bin/.cue set, and saying so would send someone to re-import it for nothing.
        if (resolution.MultiFile > 0)
        {
            parts.Add($"{resolution.MultiFile} skipped, held as several files which this version cannot sync yet");
        }

        if (resolution.TooLargeForFilesystem > 0)
        {
            parts.Add($"{resolution.TooLargeForFilesystem} skipped, too large for this drive's filesystem");
        }

        if (resolution.OverCount > 0)
        {
            parts.Add($"{resolution.OverCount} past the game cap");
        }

        if (resolution.OverBytes > 0)
        {
            parts.Add($"{resolution.OverBytes} past the byte budget");
        }

        if (resolution.Outcome == ResolutionOutcome.Interrupted)
        {
            parts.Add("walk interrupted, will resume");
        }

        return string.Join("; ", parts);
    }

    private (string? Folder, bool NeedsChoice, IReadOnlyList<string> Candidates) ResolveFolder(
        SyncSetDefinition set,
        RomRow row)
    {
        // The set's own choice outranks the install-wide map, because it is the narrower
        // statement of the same decision. It is also the only way an arcade set resolves.
        if (!string.IsNullOrWhiteSpace(set.FolderOverride))
        {
            return (set.FolderOverride, false, []);
        }

        if (!_resolutionCache.TryGetValue(row.PlatformId, out var resolution))
        {
            resolution = _platforms.Resolve(
                new RomMPlatform(row.PlatformId, row.PlatformSlug, row.PlatformFsSlug, row.PlatformDisplayName));

            _resolutionCache[row.PlatformId] = resolution;
        }

        return (resolution.Folder, resolution.RequiresExplicitChoice && resolution.Folder is null, resolution.Candidates);
    }

    private bool Accepts(string folder, string? extension) =>
        _install.TryGetFolder(folder, out var system) && system.Accepts(extension);

    /// <summary>Puts a row an earlier segment of this walk produced back where it was.</summary>
    /// <returns>The state it was carried as, so the caller can keep its counts.</returns>
    private static MemberState Carry(
        SyncSetMember row,
        BoundedSelection selector,
        List<SyncSetMember> excluded,
        Dictionary<string, int> extensionCounts,
        Dictionary<string, int> unmappedCounts)
    {
        switch (row.State)
        {
            case MemberState.Member:
                selector.Offer(row);
                break;

            case MemberState.ExcludedExtension:
                Count(extensionCounts, string.IsNullOrWhiteSpace(row.FsExtension) ? NoExtension : row.FsExtension);
                excluded.Add(row);
                break;

            case MemberState.ExcludedUnmapped:
                Count(unmappedCounts, row.PlatformSlug);
                excluded.Add(row);
                break;

            case MemberState.ExcludedMultiFile:
            case MemberState.ExcludedFilesystemLimit:
                excluded.Add(row);
                break;

            default:
                break;
        }

        return row.State;
    }

    private static SyncSetMember Member(RomRow row, string? folder, MemberState state, DateTimeOffset resolvedAt) =>
        new()
        {
            RomId = row.Id,
            State = state,
            Folder = folder,
            PlatformSlug = row.PlatformSlug,
            FsName = row.FsName,
            FsExtension = row.FsExtension,
            SizeBytes = row.SizeBytes,

            // Carried so a later sync can decide a file on disk is already the right one with
            // the server switched off. Both describe the uncompressed content.
            Md5Hash = row.Md5Hash,
            Sha1Hash = row.Sha1Hash,
            DisplayName = row.DisplayName,
            SortKey = row.SortKey,
            RomUpdatedAt = row.UpdatedAtUtc,
            ResolvedAt = resolvedAt,
        };

    private static void Count(Dictionary<string, int> counts, string key) =>
        counts[key] = counts.TryGetValue(key, out var existing) ? existing + 1 : 1;

    private static SetResolution Refused(SyncSetDefinition set, int total) => new()
    {
        Set = set,
        Outcome = ResolutionOutcome.Refused,
        ScopeTotal = total,
        Problem =
            $"'{set.Name}' matches {total:N0} games and has no game or size cap, so resolving it would hold the "
                + "whole library. Give it a cap, or narrow the scope.",
        Summary = $"refused: {total:N0} games with no cap",
    };

    private static SetResolution NeedsChoice(SyncSetDefinition set, RomRow row, IReadOnlyList<string> candidates) => new()
    {
        Set = set,
        Outcome = ResolutionOutcome.NeedsFolderChoice,
        Problem =
            $"'{row.PlatformSlug}' does not map to one folder on its own. Which one is right depends on the romset "
                + $"the files came from. Choose one for this set: {string.Join(", ", candidates)}.",
        Summary = $"needs a folder chosen for {row.PlatformSlug}",
    };

    /// <summary>
    /// Keeps the ordering-best candidates that fit inside the set's caps.
    /// </summary>
    /// <remarks>
    /// Two stages, because one alone cannot be both bounded and independent of the order the
    /// pages arrive in. A worst-first priority queue holds the ordering-best candidates and
    /// nothing else, which is what lets a 40-game set be resolved out of an 83,000 ROM
    /// library without ever holding it. Then one pass over that buffer, in the set's order,
    /// takes each candidate that still fits.
    /// <para>
    /// Dropping the ordering-worst as the budget is exceeded, without the second stage, is
    /// not the same answer: a late ordering-best candidate evicts small ones that would have
    /// fitted alongside it, and the result then depends on the order the ROMs happened to be
    /// imported in.
    /// </para>
    /// <para>
    /// The buffer is <c>max_games</c> when that is the only cap, because the answer cannot
    /// then reach past the ordering-best <c>max_games</c> candidates. A byte budget can reach
    /// further, since a candidate the budget turns away lets a later one in, so the buffer
    /// falls back to <see cref="UncappedScopeLimit"/>: the ceiling an uncapped scope is
    /// refused above, and the most this holds regardless of library size.
    /// </para>
    /// </remarks>
    private sealed class BoundedSelection
    {
        private readonly SyncSetDefinition _set;
        private readonly PriorityQueue<SyncSetMember, SyncSetMember> _buffer;
        private readonly int _limit;

        public BoundedSelection(SyncSetDefinition set)
        {
            _set = set;
            _limit = set.MaxBytes.HasValue ? UncappedScopeLimit : set.MaxGames ?? UncappedScopeLimit;

            // Inverted, so the queue hands back the item the set wants least.
            var order = SetOrder(set.Ordering);
            _buffer = new PriorityQueue<SyncSetMember, SyncSetMember>(
                Comparer<SyncSetMember>.Create((left, right) => order.Compare(right, left)));
        }

        public int OverCount { get; private set; }

        public int OverBytes { get; private set; }

        public void Offer(SyncSetMember candidate)
        {
            // A ROM larger than the whole budget can never be selected, whatever it displaces.
            if (_set.MaxBytes is { } maxBytes && candidate.SizeBytes > maxBytes)
            {
                OverBytes++;
                return;
            }

            _buffer.Enqueue(candidate, candidate);

            if (_buffer.Count > _limit)
            {
                _buffer.Dequeue();
                Turned();
            }
        }

        public List<SyncSetMember> Drain()
        {
            var ordered = new List<SyncSetMember>(_buffer.Count);
            while (_buffer.TryDequeue(out var candidate, out _))
            {
                ordered.Add(candidate);
            }

            // Drained worst first, so reversing leaves the buffer in the set's own order.
            ordered.Reverse();

            var kept = new List<SyncSetMember>(ordered.Count);
            var bytes = 0L;

            foreach (var candidate in ordered)
            {
                if (_set.MaxGames is { } maxGames && kept.Count >= maxGames)
                {
                    OverCount++;
                    continue;
                }

                if (_set.MaxBytes is { } maxBytes && bytes + candidate.SizeBytes > maxBytes)
                {
                    OverBytes++;
                    continue;
                }

                kept.Add(candidate);
                bytes += candidate.SizeBytes;
            }

            return kept;
        }

        /// <summary>
        /// Counts a candidate the buffer had no room for, against whichever cap set its size.
        /// </summary>
        /// <remarks>
        /// A set with neither cap never gets here: a scope that large is refused before the
        /// first page is read.
        /// </remarks>
        private void Turned()
        {
            if (_set.MaxBytes.HasValue)
            {
                OverBytes++;
            }
            else
            {
                OverCount++;
            }
        }

        /// <summary>The set's own order: negative means the left one comes first.</summary>
        private static Comparer<SyncSetMember> SetOrder(SetOrdering ordering) =>
            Comparer<SyncSetMember>.Create((left, right) => ordering switch
            {
                SetOrdering.SizeAscending => Then(left.SizeBytes.CompareTo(right.SizeBytes), left, right),
                SetOrdering.SizeDescending => Then(right.SizeBytes.CompareTo(left.SizeBytes), left, right),
                SetOrdering.RecentlyUpdated => Then(Updated(right).CompareTo(Updated(left)), left, right),
                _ => ByName(left, right),
            });

        private static int Then(int primary, SyncSetMember left, SyncSetMember right) =>
            primary != 0 ? primary : ByName(left, right);

        // Falls through to the rom id so the order is total. Without it, two games with the
        // same name and size would swap places between runs and churn the membership.
        private static int ByName(SyncSetMember left, SyncSetMember right)
        {
            var byName = string.Compare(left.SortKey, right.SortKey, StringComparison.OrdinalIgnoreCase);
            return byName != 0 ? byName : left.RomId.CompareTo(right.RomId);
        }

        private static DateTimeOffset Updated(SyncSetMember candidate) =>
            candidate.RomUpdatedAt ?? DateTimeOffset.MinValue;
    }
}
