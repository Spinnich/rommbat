using System.Collections.Frozen;

namespace RomM.Client;

/// <summary>A capability RomMBat offers, and the scopes it needs to offer it.</summary>
public enum RomMFeature
{
    /// <summary>Browsing and downloading ROMs. Without it nothing works.</summary>
    Library,

    /// <summary>Collection, smart-collection and virtual-collection sync sets.</summary>
    CollectionSets,

    /// <summary>BIOS and firmware sync.</summary>
    Firmware,

    /// <summary>Pulling saves and states down from RomM.</summary>
    SavePull,

    /// <summary>Pushing saves and states up to RomM.</summary>
    SavePush,

    /// <summary>Device identity, sync negotiation and roaming sync config.</summary>
    DeviceSync,

    /// <summary>Play sessions, last-played and favourites.</summary>
    Playtime,
}

/// <summary>What one feature needs, and what the user loses without it.</summary>
public sealed record FeatureRequirement(
    RomMFeature Feature,
    string Name,
    IReadOnlyList<string> RequiredScopes,
    string WithoutIt);

/// <summary>
/// The scopes a pairing actually granted, and what they turn on.
/// </summary>
/// <remarks>
/// The approving user picks <c>approved_scopes</c> in the web UI and can narrow them.
/// Everything that needs a scope asks here first, so a narrow grant disables a feature and
/// says so, rather than surfacing as a 403 in the middle of a sync.
/// </remarks>
public sealed class GrantedScopes
{
    private readonly FrozenSet<string> _granted;

    public GrantedScopes(IEnumerable<string> granted) =>
        _granted = granted.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>An empty grant, which is what an unpaired install has.</summary>
    public static GrantedScopes None { get; } = new([]);

    /// <summary>The granted scopes, sorted, as they should be persisted and displayed.</summary>
    public IReadOnlyList<string> All => [.. _granted.Order(StringComparer.Ordinal)];

    /// <summary>Everything RomMBat asked for and did not get.</summary>
    public IReadOnlyList<string> NotGranted =>
        [.. RomMScopes.Requested.Where(scope => !_granted.Contains(scope))];

    /// <summary>
    /// Granted scopes RomMBat never asked for. A token carrying one of these is over-scoped;
    /// see the README scopes table.
    /// </summary>
    public IReadOnlyList<string> OverGranted =>
        [.. _granted.Where(scope => !RomMScopes.Requested.Contains(scope)).Order(StringComparer.Ordinal)];

    /// <summary>Every feature and what it needs.</summary>
    public static IReadOnlyList<FeatureRequirement> Requirements { get; } =
    [
        new(
            RomMFeature.Library,
            "Browse and download ROMs",
            [RomMScopes.RomsRead, RomMScopes.PlatformsRead],
            "Nothing works: no browsing, no platform mapping, no content sync"),
        new(
            RomMFeature.CollectionSets,
            "Collection sync sets",
            [RomMScopes.CollectionsRead],
            "Sync sets can only be scoped by platform or by a saved filter"),
        new(
            RomMFeature.Firmware,
            "BIOS sync",
            [RomMScopes.FirmwareRead],
            "BIOS files must be copied into bios/ by hand"),
        new(
            RomMFeature.SavePull,
            "Pull saves and states",
            [RomMScopes.AssetsRead],
            "Push-only: saves made elsewhere never reach this device"),
        new(
            RomMFeature.SavePush,
            "Push saves and states",
            [RomMScopes.AssetsWrite],
            "Pull-only: local saves stay local"),
        new(
            RomMFeature.DeviceSync,
            "Device identity and sync negotiation",
            [RomMScopes.DevicesRead, RomMScopes.DevicesWrite],
            "No save sync at all, and sync-set definitions do not roam"),
        new(
            RomMFeature.Playtime,
            "Play sessions and favourites",
            [RomMScopes.RomsUserRead, RomMScopes.RomsUserWrite],
            "No playtime tracking"),
    ];

    /// <summary>True when this exact scope was granted.</summary>
    public bool Has(string scope) => _granted.Contains(scope);

    /// <summary>True when every scope the feature needs was granted.</summary>
    public bool Allows(RomMFeature feature) => MissingFor(feature).Count == 0;

    /// <summary>The scopes this feature needs and did not get.</summary>
    public IReadOnlyList<string> MissingFor(RomMFeature feature)
    {
        var requirement = Requirements.Single(r => r.Feature == feature);
        return [.. requirement.RequiredScopes.Where(scope => !_granted.Contains(scope))];
    }

    /// <summary>Every feature that is off, ready to show the user after a narrowed grant.</summary>
    public IReadOnlyList<(FeatureRequirement Requirement, IReadOnlyList<string> Missing)> Degradations =>
    [
        .. Requirements
            .Select(requirement => (requirement, Missing: MissingFor(requirement.Feature)))
            .Where(pair => pair.Missing.Count > 0),
    ];
}
