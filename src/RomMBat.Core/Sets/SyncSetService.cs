using System.Globalization;
using RomM.Client;
using RomM.Client.Catalog;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;

namespace RomMBat.Core.Sets;

/// <summary>Why a set operation was refused, and what an exit code should be made of it.</summary>
/// <remarks>
/// <b>These map one to one onto the agent's existing exit codes</b>, which is what lets the
/// printers stay printers. <see cref="MissingScope"/> is the only refusal about this pairing
/// rather than about the request, and it is the only one the agent answers with
/// <c>Refused</c>; the rest are <c>Usage</c>, because a wrapping script needs to tell a wrong
/// command line apart from an environment problem.
/// </remarks>
public enum SetRefusal
{
    /// <summary>It worked.</summary>
    None,

    /// <summary>The pairing lacks a scope this kind of set needs.</summary>
    MissingScope,

    /// <summary>A scope that needs a value was given none.</summary>
    MissingValue,

    /// <summary>The platform named is not one this install knows.</summary>
    UnknownPlatform,

    /// <summary>The folder override names no system in this install's <c>es_systems.cfg</c>.</summary>
    UnknownFolder,

    /// <summary>A set of that name already exists.</summary>
    NameTaken,

    /// <summary>No set of that name exists.</summary>
    NotFound,
}

/// <summary>The result of a set operation: the set, or why there is not one.</summary>
/// <param name="Problem">
/// The sentence stating the rule, with no remedy attached. Null exactly when
/// <paramref name="Refusal"/> is <see cref="SetRefusal.None"/>.
/// </param>
/// <param name="Value">
/// The offending input, when there is one, so a caller can name it in a remedy of its own
/// wording. The console says "run 'platforms list'"; a picker says "choose from the list".
/// </param>
public sealed record SetOutcome(
    SyncSetDefinition? Set,
    SetRefusal Refusal,
    string? Problem,
    string? Value = null)
{
    public bool IsRefused => Refusal != SetRefusal.None;

    internal static SetOutcome Ok(SyncSetDefinition set) => new(set, SetRefusal.None, null);

    internal static SetOutcome No(SetRefusal refusal, string problem, string? value = null) =>
        new(null, refusal, problem, value);
}

/// <summary>The sets a verb applies to: the one named, or all of them.</summary>
public sealed record SetSelection(IReadOnlyList<SyncSetDefinition> Sets, string? Problem)
{
    public bool IsEmpty => Sets.Count == 0;
}

/// <summary>One set as a list shows it: the definition plus what it currently resolves to.</summary>
/// <param name="Bytes">
/// What the games weigh according to RomM, which is the only figure available before a sync.
/// </param>
/// <param name="OnDiskBytes">
/// What this set actually occupies now, artwork included.
/// </param>
/// <remarks>
/// <b>Two figures because they answer different questions, and the gap between them is the
/// point.</b> <paramref name="Bytes"/> is ROMs only: RomM publishes no media size on the rom
/// row, so nothing can predict the artwork. Measured on two Atari platforms, artwork is 62 to
/// 94 times the ROM bytes, so a set reading "296.6 KB" was occupying 28 MB and had no way of
/// saying so.
/// </remarks>
public sealed record SetSummary(SyncSetDefinition Set, int Games, long Bytes, long OnDiskBytes = 0)
{
    /// <summary>The caps and ordering as a sentence.</summary>
    public string Policy => SyncSetService.DescribePolicy(Set);
}

/// <summary>Everything <c>sets show</c> knows about one set.</summary>
/// <param name="Bytes">What the games weigh according to RomM. See <see cref="SetSummary"/>.</param>
/// <param name="OnDiskBytes">What this set occupies now, artwork included.</param>
public sealed record SetDetail(
    SyncSetDefinition Set,
    int Games,
    long Bytes,
    IReadOnlyList<SyncSetMember> Members,
    IReadOnlyList<SyncSetMember> Departed,
    IReadOnlyList<ExclusionSummary> Exclusions,
    long OnDiskBytes = 0)
{
    public string Policy => SyncSetService.DescribePolicy(Set);
}

/// <summary>
/// A scope a picker may offer, and why it may not be pickable on this install.
/// </summary>
/// <remarks>
/// <b>Availability is Core's answer, not a screen's.</b> A picker that worked out for itself
/// which scopes need <c>collections.read</c> would be a second copy of the rule
/// <see cref="SyncSetService.Add"/> enforces, and the two would drift.
/// </remarks>
public sealed record ScopeOption(
    CatalogScopeKind Kind,
    string Label,
    bool Available,
    string? Unavailable);

/// <summary>A platform this install could scope a set by.</summary>
/// <param name="Folder">
/// Where its games would land, or null when nothing maps it. A null folder is not a reason to
/// hide the row: it is the thing the user has to fix, and arcade reaches it by design.
/// </param>
public sealed record PlatformOption(
    int PlatformId,
    string FsSlug,
    string Label,
    string? Folder,
    string? Note);

/// <summary>What a caller wants a new set to be.</summary>
/// <remarks>
/// <b><see cref="Filter"/> and <see cref="ScopeValue"/> are separate fields on purpose.</b> A
/// filter scope is built from fields rather than from a value, so a front end assembling one
/// has no value to supply and cannot trip #78. The agent still maps <c>--value</c> onto
/// <see cref="ScopeValue"/>, and a filter draft still ignores it, which is that defect exactly
/// and is preserved here rather than fixed: it is filed, and fixing it while passing would
/// change the agent's behaviour inside a refactor whose whole claim is that it did not.
/// </remarks>
public sealed record SetDraft
{
    public required string Name { get; init; }

    public required CatalogScopeKind Scope { get; init; }

    /// <summary>An <c>fs_slug</c> or a numeric RomM id for a platform, an id for a collection.</summary>
    public string? ScopeValue { get; init; }

    /// <summary>The fields a <see cref="CatalogScopeKind.Filter"/> scope is made of.</summary>
    public CatalogFilter? Filter { get; init; }

    public int? MaxGames { get; init; }

    public long? MaxBytes { get; init; }

    /// <summary>Defaults with <see cref="SyncSetStore.DefaultOrdering"/>, which is recent.</summary>
    public SetOrdering Ordering { get; init; } = SyncSetStore.DefaultOrdering;

    /// <summary>A RetroBat system name, never a path. Validated against the live config.</summary>
    public string? FolderOverride { get; init; }
}

/// <summary>What an edit changes. An unset property is left alone.</summary>
/// <remarks>
/// Scope and value are absent deliberately: changing what a set points at makes its recorded
/// membership a statement about a different question, and there is no migration from one to
/// the other short of a re-resolve. Removing and re-adding is the honest way to do that, and
/// it touches nothing on disk.
/// </remarks>
public sealed record SetEdit
{
    public bool ClearMaxGames { get; init; }

    public int? MaxGames { get; init; }

    public bool ClearMaxBytes { get; init; }

    public long? MaxBytes { get; init; }

    public SetOrdering? Ordering { get; init; }

    public bool ClearFolderOverride { get; init; }

    public string? FolderOverride { get; init; }

    /// <summary>
    /// A replacement filter, for a filter-scoped set. Null leaves it alone.
    /// </summary>
    /// <remarks>
    /// Ignored on any other scope, because a platform set's scope value is an identity rather
    /// than a query and changing it is what removing and re-adding is for.
    /// </remarks>
    public CatalogFilter? Filter { get; init; }
}

/// <summary>
/// Defining what this device syncs. Local, instant, and answerable with the server off.
/// </summary>
/// <remarks>
/// <b>This is the orchestration <c>SetsCommand</c> used to hold, with the console taken out.</b>
/// It decides and it does not report, the way <see cref="InstallSession"/> does: a refusal is a
/// value carrying the sentence that states the rule, and the caller decides whether that is a
/// line on stderr or a row on a screen.
/// <para>
/// <b>Nothing here takes <see cref="TreeLock"/>, and that is a decision.</b> Every write on
/// this type is a row in SQLite, which is in WAL mode, and the tree lock serialises writers of
/// <i>files in the tree</i>. Taking it to add a set definition would be the speculative acquire
/// that makes a concurrent <c>background quit</c> flush skip its upload and report success. A
/// test asserts that a set can be defined while a background pass holds the lock.
/// </para>
/// <para>
/// <b>Where a sentence lives.</b> A sentence that states a rule or a fact about the library is
/// Core's, because it would be the same on either front end. A sentence naming a subcommand or
/// a flag is the caller's, because it would be false on the other one. So this type says
/// "'gbaa' is not a platform this install knows" and never "run 'platforms list' first".
/// </para>
/// </remarks>
public sealed class SyncSetService
{
    /// <summary>
    /// The saved filter of a filter-scoped set, or an empty one for any other scope.
    /// </summary>
    /// <remarks>
    /// <b>That a filter set keeps its filter in <c>scope_value</c> is one fact and belongs in
    /// one place.</b> <see cref="SetResolver"/> knew it and the set editor was about to learn
    /// it separately, which is two readers of a storage decision neither of them makes.
    /// </remarks>
    public static CatalogFilter FilterOf(SyncSetDefinition set)
    {
        ArgumentNullException.ThrowIfNull(set);

        // Verified rather than assumed for the picked kind: its scope_value is an id array,
        // which would parse as an empty filter anyway, but "an empty filter" and "this scope
        // has no filter" have to be the same answer on purpose rather than by luck.
        return set.Scope == CatalogScopeKind.Filter
            ? CatalogFilterJson.Parse(set.ScopeValue)
            : new CatalogFilter();
    }

    private readonly InstallSession _session;

    public SyncSetService(InstallSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
    }

    /// <summary>Every set, with what it currently resolves to.</summary>
    public IReadOnlyList<SetSummary> List()
    {
        var store = _session.Store.SyncSets;

        return [.. store.List().Select(set =>
        {
            var (games, bytes) = store.MemberTotals(set.Id);
            return new SetSummary(set, games, bytes, OnDisk(set.Id));
        })];
    }

    /// <summary>
    /// Picks the sets a verb applies to: the one named, or all of them.
    /// </summary>
    /// <remarks>Shared with sync and evict, which take a set name the same way.</remarks>
    public SetSelection Select(string? name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            var named = _session.Store.SyncSets.Find(name);

            return named is not null
                ? new SetSelection([named], null)
                : new SetSelection([], $"No sync set named '{name}'.");
        }

        var all = _session.Store.SyncSets.List();

        return all.Count > 0
            ? new SetSelection(all, null)
            : new SetSelection([], "No sync sets defined.");
    }

    /// <summary>One set, its membership, what left it, and what it excluded.</summary>
    public SetDetail? Show(string name)
    {
        var store = _session.Store.SyncSets;
        var set = store.Find(name);

        if (set is null)
        {
            return null;
        }

        var (games, bytes) = store.MemberTotals(set.Id);

        return new SetDetail(
            set,
            games,
            bytes,
            store.Members(set.Id),
            store.Members(set.Id, MemberState.Departed),
            store.Exclusions(set.Id),
            OnDisk(set.Id));
    }

    /// <summary>
    /// What one set occupies on this device now, every kind of file included.
    /// </summary>
    /// <remarks>
    /// Counted over the set's current members, so a game that has left the set stops counting
    /// against it the moment it departs, which is what makes the figure answer "what is this
    /// set costing me" rather than "what did it ever cost me".
    /// <para>
    /// <b>Adopted files are counted here and not in the budget, deliberately.</b> The budget
    /// bounds what RomMBat downloaded, because counting a user's own library would put the app
    /// permanently over its cap. This figure answers a different question, which is how much of
    /// the drive the set is using, and the user's own ROM in that folder is using it too.
    /// </para>
    /// <para>
    /// <b>One aggregate query, where this was one per member per set.</b> <see cref="List"/>
    /// calls it for every set and a list screen re-runs its rows on every back-press, so
    /// returning to the sets list from a sync re-issued the lot. Both rules above survive the
    /// rewrite; only the loop went. See #111.
    /// </para>
    /// <para>
    /// <b>A subquery rather than an id list, because the obvious rewrite was measured and
    /// barely helped.</b> Passing the membership to a parameterised <c>IN</c> is 95 ms at
    /// 5,000 members against the loop's 111 ms, where the subquery is 1 ms.
    /// </para>
    /// </remarks>
    private long OnDisk(long setId) => _session.Store.Files.BytesForSet(setId);

    /// <summary>
    /// The scopes a set can be given here, and why any of them cannot.
    /// </summary>
    /// <remarks>
    /// Every kind is listed whether or not it is available. A picker that dropped the
    /// unavailable ones would leave a user who knows their RomM has collections concluding
    /// RomMBat cannot use them, where the reason is their own pairing and is fixable.
    /// </remarks>
    public IReadOnlyList<ScopeOption> Scopes()
    {
        var granted = _session.Store.Device.Read()?.Scopes ?? GrantedScopes.None;
        var allowed = granted.Allows(RomMFeature.CollectionSets);
        var unavailable = allowed ? null : CollectionRefusal();

        return
        [
            .. Enum.GetValues<CatalogScopeKind>().Select(kind =>
            {
                // Two different reasons a scope may not be pickable, and they are not
                // interchangeable. One is this pairing's grant, which the user can fix by
                // pairing again. The other is that RomMBat cannot list what the scope could
                // point at, which they cannot fix at all, and a scope offered without a way to
                // complete it is worse than one that is not offered.
                var missingGrant = RequiresCollections(kind) && !allowed;
                var cannotList = CatalogScopeService.WhyNotListable(kind);

                return new ScopeOption(
                    kind,
                    SyncSetStore.ScopeText(kind),
                    !missingGrant && cannotList is null,
                    missingGrant ? unavailable : cannotList);
            }),
        ];
    }

    /// <summary>
    /// The platforms this install has heard of, for a picker that cannot ask the user to type.
    /// </summary>
    /// <remarks>
    /// From <c>platform_map</c>, which is what <c>platforms list</c> reads, so a platform absent
    /// here is absent there too and the answer is to sync or browse once. Rows with no
    /// <see cref="PlatformOption.PlatformId"/> are dropped: a scope needs the numeric id the
    /// endpoint accepts, and a row without one cannot be turned into one.
    /// </remarks>
    public IReadOnlyList<PlatformOption> PlatformsKnownHere() =>
    [
        .. _session.Store.PlatformMap.List()
            .Where(row => row.PlatformId is not null)
            .OrderBy(row => row.Label, StringComparer.CurrentCultureIgnoreCase)
            .Select(row => new PlatformOption(
                row.PlatformId!.Value,
                row.FsSlug,
                row.Label,
                row.Folder,
                row.Explanation)),
    ];

    /// <summary>
    /// The system folders this install actually has, read live.
    /// </summary>
    /// <remarks>
    /// RetroBat is the authority on this and it is read from the running install rather than
    /// bundled, because RetroBat adds systems every release and users add their own.
    /// </remarks>
    public IReadOnlyList<string> FoldersKnownHere() =>
        [.. EsSystemsFile.Load(_session.Install).Folders.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)];

    /// <summary>Defines a set, or refuses with the sentence for it.</summary>
    public SetOutcome Add(SetDraft draft, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(draft);

        // A narrowed grant degrades by feature rather than failing later with a 403 in the
        // middle of a sync, so the refusal happens here, at the point of definition.
        if (RequiresCollections(draft.Scope)
            && !(_session.Store.Device.Read()?.Scopes ?? GrantedScopes.None).Allows(RomMFeature.CollectionSets))
        {
            return SetOutcome.No(SetRefusal.MissingScope, CollectionRefusal());
        }

        var value = draft.ScopeValue ?? string.Empty;

        if (draft.Scope != CatalogScopeKind.Filter && string.IsNullOrWhiteSpace(value))
        {
            return SetOutcome.No(
                SetRefusal.MissingValue,
                $"A {SyncSetStore.ScopeText(draft.Scope)} scope needs a value.");
        }

        // A platform scope takes the fs_slug the mapping table lists as readily as the numeric
        // id the API wants, because the fs_slug is the one a person has in front of them.
        // Resolved here so the stored scope stays the id the endpoint accepts.
        if (draft.Scope == CatalogScopeKind.Platform && !int.TryParse(value, out _))
        {
            if (_session.Store.PlatformMap.Find(value)?.PlatformId is not { } platformId)
            {
                return SetOutcome.No(
                    SetRefusal.UnknownPlatform,
                    $"'{value}' is not a platform this install knows.",
                    value);
            }

            value = platformId.ToString(CultureInfo.InvariantCulture);
        }

        if (draft.Scope == CatalogScopeKind.Filter)
        {
            value = CatalogFilterJson.Write(draft.Filter ?? new CatalogFilter());
        }

        if (draft.FolderOverride is { } folder && !EsSystemsFile.Load(_session.Install).HasFolder(folder))
        {
            return SetOutcome.No(
                SetRefusal.UnknownFolder,
                $"'{folder}' is not a system in this install's es_systems.cfg.",
                folder);
        }

        try
        {
            return SetOutcome.Ok(_session.Store.SyncSets.Add(
                new SyncSetDefinition
                {
                    Name = draft.Name,
                    Scope = draft.Scope,
                    ScopeValue = value,
                    MaxGames = draft.MaxGames,
                    MaxBytes = draft.MaxBytes,
                    Ordering = draft.Ordering,
                    FolderOverride = draft.FolderOverride,
                },
                now));
        }
        catch (SyncSetExistsException ex)
        {
            return SetOutcome.No(SetRefusal.NameTaken, ex.Message, draft.Name);
        }
    }

    /// <summary>
    /// Changes the caps, the ordering or the folder of a set that already exists.
    /// </summary>
    /// <remarks>
    /// The membership is left exactly as it was. A tighter cap does not retire rows here,
    /// because what a set holds is decided by a resolve and a cap changed between resolves is
    /// an intention rather than an outcome. The next resolve applies it.
    /// </remarks>
    public SetOutcome Edit(string name, SetEdit edit, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(edit);

        var set = _session.Store.SyncSets.Find(name);

        if (set is null)
        {
            return SetOutcome.No(SetRefusal.NotFound, $"No sync set named '{name}'.", name);
        }

        var folder = edit.ClearFolderOverride ? null : edit.FolderOverride ?? set.FolderOverride;

        if (folder is not null && !EsSystemsFile.Load(_session.Install).HasFolder(folder))
        {
            return SetOutcome.No(
                SetRefusal.UnknownFolder,
                $"'{folder}' is not a system in this install's es_systems.cfg.",
                folder);
        }

        var updated = set with
        {
            MaxGames = edit.ClearMaxGames ? null : edit.MaxGames ?? set.MaxGames,
            MaxBytes = edit.ClearMaxBytes ? null : edit.MaxBytes ?? set.MaxBytes,
            Ordering = edit.Ordering ?? set.Ordering,
            FolderOverride = folder,
        };

        _session.Store.SyncSets.UpdatePolicy(updated, now);

        // Written second and only when it changed, so an edit that touched the caps alone does
        // not throw away a resolve that is still valid.
        if (edit.Filter is { } filter && set.Scope == CatalogScopeKind.Filter)
        {
            var written = CatalogFilterJson.Write(filter);

            if (!string.Equals(written, set.ScopeValue, StringComparison.Ordinal))
            {
                _session.Store.SyncSets.UpdateFilter(set.Id, written, now);
            }
        }

        return SetOutcome.Ok(_session.Store.SyncSets.Find(name) ?? updated);
    }

    /// <summary>Forgets a set. Touches nothing on disk.</summary>
    public SetOutcome Remove(string name) =>
        _session.Store.SyncSets.Remove(name)
            ? new SetOutcome(null, SetRefusal.None, null, name)
            : SetOutcome.No(SetRefusal.NotFound, $"No sync set named '{name}'.", name);

    /// <summary>The caps and ordering as a sentence, which both front ends show verbatim.</summary>
    public static string DescribePolicy(SyncSetDefinition set)
    {
        ArgumentNullException.ThrowIfNull(set);

        var parts = new List<string>
        {
            set.MaxGames is { } games ? $"max {games} games" : "no game cap",
            set.MaxBytes is { } bytes ? $"max {ByteSize.Format(bytes)}" : "no size cap",
            $"ordered by {SyncSetStore.OrderingText(set.Ordering)}",
        };

        if (set.FolderOverride is { } folder)
        {
            parts.Add($"into {folder}");
        }

        return string.Join(", ", parts);
    }

    /// <summary>Why a member is not in the set, as a person would say it.</summary>
    public static string Describe(MemberState state) => state switch
    {
        MemberState.ExcludedExtension => "skipped, format not supported by this system",
        MemberState.ExcludedUnmapped => "skipped, no RetroBat folder for their platform",
        MemberState.ExcludedMultiFile => "skipped, held as several files which this version cannot sync yet",
        MemberState.ExcludedFilesystemLimit => "skipped, too large for this drive's filesystem",
        MemberState.ExcludedOverCount => "past the game cap",
        MemberState.ExcludedOverBytes => "past the byte budget",
        MemberState.Departed => "no longer in the scope",
        _ => "in the set",
    };

    internal static bool RequiresCollections(CatalogScopeKind scope) =>
        scope is CatalogScopeKind.Collection
            or CatalogScopeKind.SmartCollection
            or CatalogScopeKind.VirtualCollection;

    /// <summary>
    /// The refusal sentence, built from the requirement rather than written out.
    /// </summary>
    /// <remarks>
    /// Stating the rule and naming nothing about how to fix it. The remedy differs per front
    /// end: a console says which flags to use instead, a picker has already offered them.
    /// </remarks>
    private static string CollectionRefusal()
    {
        var requirement = GrantedScopes.Requirements.Single(r => r.Feature == RomMFeature.CollectionSets);

        return $"This pairing was not granted {string.Join(", ", requirement.RequiredScopes)}, "
            + $"so {requirement.WithoutIt.ToLowerInvariant()}.";
    }
}
