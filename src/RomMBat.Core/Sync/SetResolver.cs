using System.Globalization;
using RomM.Client;
using RomM.Client.Catalog;
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
/// flight at a time and nothing accumulates the catalog. What is held is the resolved set
/// itself, which is capped at <c>max_games</c> when there is one; an uncapped scope larger
/// than <see cref="UncappedScopeLimit"/> is refused rather than silently accumulated.
/// </para>
/// <para>
/// <b>The caps are greedy, not a knapsack.</b> Candidates are kept in the set's ordering and
/// the ordering-worst is dropped when a cap is exceeded, so the result is the best-ordered
/// subset that fits. It does not search for a combination that would pack the budget more
/// tightly, and it does not depend on the order pages arrive in.
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
    private readonly Dictionary<int, PlatformResolution> _resolutionCache = [];

    public SetResolver(EsSystemsFile install, PlatformResolver platforms)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(platforms);

        _install = install;
        _platforms = platforms;
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
    public async Task<SetResolution> ResolveAsync(
        SyncSetDefinition set,
        RomPager pager,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(pager);

        var selector = new BoundedSelection(set);
        var excluded = new List<SyncSetMember>();
        var extensionCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var unmappedCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var scanned = 0;
        RomMResponse<RomPage>? failure = null;

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
                    excluded.Add(Member(row, null, MemberState.ExcludedUnmapped, null, now));
                    continue;
                }

                if (!Accepts(resolution.Folder, row.FsExtension))
                {
                    Count(extensionCounts, string.IsNullOrWhiteSpace(row.FsExtension) ? NoExtension : row.FsExtension);
                    excluded.Add(Member(row, resolution.Folder, MemberState.ExcludedExtension, null, now));
                    continue;
                }

                selector.Offer(row, resolution.Folder);
            }
        }

        var selected = selector.Drain();
        var members = new List<SyncSetMember>(selected.Count);
        for (var index = 0; index < selected.Count; index++)
        {
            members.Add(Member(selected[index].Row, selected[index].Folder, MemberState.Member, index + 1, now));
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
            $"{resolution.Members.Count} games, {FormatBytes(resolution.Bytes)}",
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

    /// <summary>Bytes as something a person reads, in the units a ROM library uses.</summary>
    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{value:0.#} {units[unit]}");
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

    private static SyncSetMember Member(RomRow row, string? folder, MemberState state, int? position, DateTimeOffset now) =>
        new()
        {
            RomId = row.Id,
            State = state,
            Folder = folder,
            PlatformSlug = row.PlatformSlug,
            FsName = row.FsName,
            FsExtension = row.FsExtension,
            SizeBytes = row.SizeBytes,
            DisplayName = row.DisplayName,
            SortKey = row.SortKey,
            Position = position,
            ResolvedAt = now,
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
    /// A priority queue ordered worst-first, so exceeding a cap is one dequeue. That bounds
    /// what is held to the cap itself plus the candidate being considered, which is what lets
    /// a 40-game set be resolved out of an 83,000 ROM library without ever holding it.
    /// </remarks>
    private sealed class BoundedSelection
    {
        private readonly SyncSetDefinition _set;
        private readonly PriorityQueue<Candidate, Candidate> _kept;
        private long _bytes;

        public BoundedSelection(SyncSetDefinition set)
        {
            _set = set;

            // Inverted, so the queue hands back the item the set wants least.
            var order = SetOrder(set.Ordering);
            _kept = new PriorityQueue<Candidate, Candidate>(
                Comparer<Candidate>.Create((left, right) => order.Compare(right, left)));
        }

        public int OverCount { get; private set; }

        public int OverBytes { get; private set; }

        public void Offer(RomRow row, string folder)
        {
            var candidate = new Candidate(row, folder);
            _kept.Enqueue(candidate, candidate);
            _bytes += row.SizeBytes;

            while (_set.MaxGames is { } maxGames && _kept.Count > maxGames)
            {
                Drop();
                OverCount++;
            }

            while (_set.MaxBytes is { } maxBytes && _bytes > maxBytes && _kept.Count > 0)
            {
                Drop();
                OverBytes++;
            }
        }

        public List<Candidate> Drain()
        {
            var kept = new List<Candidate>(_kept.Count);
            while (_kept.TryDequeue(out var candidate, out _))
            {
                kept.Add(candidate);
            }

            // Drained worst first, so reversing leaves the set in its own order.
            kept.Reverse();
            return kept;
        }

        private void Drop() => _bytes -= _kept.Dequeue().Row.SizeBytes;

        /// <summary>The set's own order: negative means the left one comes first.</summary>
        private static Comparer<Candidate> SetOrder(SetOrdering ordering) =>
            Comparer<Candidate>.Create((left, right) => ordering switch
            {
                SetOrdering.SizeAscending => Then(left.Row.SizeBytes.CompareTo(right.Row.SizeBytes), left, right),
                SetOrdering.SizeDescending => Then(right.Row.SizeBytes.CompareTo(left.Row.SizeBytes), left, right),
                SetOrdering.RecentlyUpdated => Then(Updated(right).CompareTo(Updated(left)), left, right),
                _ => ByName(left, right),
            });

        private static int Then(int primary, Candidate left, Candidate right) =>
            primary != 0 ? primary : ByName(left, right);

        // Falls through to the rom id so the order is total. Without it, two games with the
        // same name and size would swap places between runs and churn the membership.
        private static int ByName(Candidate left, Candidate right)
        {
            var byName = string.Compare(left.Row.SortKey, right.Row.SortKey, StringComparison.OrdinalIgnoreCase);
            return byName != 0 ? byName : left.Row.Id.CompareTo(right.Row.Id);
        }

        private static DateTimeOffset Updated(Candidate candidate) =>
            candidate.Row.UpdatedAtUtc ?? DateTimeOffset.MinValue;
    }

    private readonly record struct Candidate(RomRow Row, string Folder);
}
