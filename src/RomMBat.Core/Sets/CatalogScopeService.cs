using System.Globalization;
using RomM.Client;
using RomM.Client.Catalog;

namespace RomMBat.Core.Sets;

/// <summary>
/// Every filter RomM offers, with the words a screen shows for each.
/// </summary>
/// <remarks>
/// <b>The API's names are the keys and these are only labels.</b>
/// <see cref="CatalogFilter.Facets"/> and <see cref="CatalogFilter.Properties"/> decide what
/// exists; this decides what it is called, which is a presentation concern that lives here
/// because both front ends need the same words.
/// <para>
/// This was five facets and two properties, chosen as the ones that survive being stored. They
/// all survive being stored, and a filter screen offering a third of what the server does is a
/// subset a person has to learn the edges of. So it is all of them now.
/// </para>
/// </remarks>
public static class FilterFacet
{
    public const string Genres = "Genres";

    public const string Franchises = "Franchises";

    public const string Collections = "Collections";

    public const string Companies = "Companies";

    public const string AgeRatings = "Age ratings";

    public const string Statuses = "Statuses";

    public const string Regions = "Regions";

    public const string Languages = "Languages";

    public const string PlayerCounts = "Player counts";

    public const string MetadataProviders = "Metadata providers";

    public const string Tags = "Tags";

    /// <summary>The multi-select facets, in the order RomM's own interface lists them.</summary>
    public static IReadOnlyList<string> Multi { get; } =
    [
        Genres,
        Franchises,
        Collections,
        Companies,
        AgeRatings,
        Statuses,
        Regions,
        Languages,
        PlayerCounts,
        MetadataProviders,
        Tags,
    ];

    /// <summary>The yes-or-no properties, in the order RomM's own interface lists them.</summary>
    public static IReadOnlyList<string> Properties { get; } =
    [
        "Matched",
        "Favourite",
        "Has versions",
        "Playable in browser",
        "Missing from disk",
        "Hash verified",
        "Has RetroAchievements",
        "Has saves",
        "Has save states",
        "Has soundtrack",
    ];

    /// <summary>
    /// The four whose answer depends on who is asking and when.
    /// </summary>
    /// <remarks>
    /// RomM answers these from its own bookkeeping rather than from the game, so a set
    /// carrying one resolves differently on another account or after a scan. Said on the
    /// screen rather than used to withhold them: a set is re-resolved on demand and is
    /// expected to move.
    /// </remarks>
    public static IReadOnlySet<string> DependOnTheServer { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "Favourite",
            "Missing from disk",
            "Has saves",
            "Has save states",
        };

    /// <summary>The API name behind a label, which is what goes on the wire and into storage.</summary>
    public static string KeyOf(string label) => label switch
    {
        Genres => "genres",
        Franchises => "franchises",
        Collections => "collections",
        Companies => "companies",
        AgeRatings => "age_ratings",
        Statuses => "statuses",
        Regions => "regions",
        Languages => "languages",
        PlayerCounts => "player_counts",
        MetadataProviders => "metadata_providers",
        Tags => "tags",
        "Matched" => "matched",
        "Favourite" => "favorite",
        "Has versions" => "duplicate",
        "Playable in browser" => "playable",
        "Missing from disk" => "missing",
        "Hash verified" => "verified",
        "Has RetroAchievements" => "has_ra",
        "Has saves" => "has_saves",
        "Has save states" => "has_states",
        "Has soundtrack" => "has_soundtrack",
        _ => string.Empty,
    };

    /// <summary>How a logic operator reads on a row, in words rather than as an enum name.</summary>
    public static string Says(FilterLogic logic) => logic switch
    {
        FilterLogic.All => "all of",
        FilterLogic.None => "none of",
        _ => "any of",
    };
}

/// <summary>One value a scope could take, as a picker shows it.</summary>
/// <param name="Value">What gets stored as the set's scope value.</param>
/// <param name="Detail">How big it is, so a person can tell two similarly named ones apart.</param>
public sealed record ScopeValueOption(string Value, string Label, string? Detail);

/// <summary>The values a scope can take, or why they could not be read.</summary>
public sealed record ScopeValues(IReadOnlyList<ScopeValueOption> Options, string? Problem)
{
    public bool IsRefused => Problem is not null;
}

/// <summary>
/// What a scope can point at, for a front end that cannot ask the user to type an id.
/// </summary>
/// <remarks>
/// <b>This exists because a scope that can be picked and then not completed is worse than one
/// that is not offered.</b> A hands-on pass reached exactly that: the scope picker offered the
/// three collection kinds, the editor had no row to set a value, and the only thing the screen
/// could say was that a value was needed.
/// <para>
/// <b>Listing collections is expensive and is only ever for naming.</b> M0 probe 5 measured a
/// single collection at 714.8 KB, 99% of it inlined cover-art paths, with no pagination. One
/// call when a picker opens is a fair price; reading membership off <c>rom_ids</c> is not, and
/// never happens: a set resolves by paging <c>GET /api/roms</c> like every other scope.
/// </para>
/// <para>
/// <b>Virtual collections are not offered, and the reason is a gap rather than a decision.</b>
/// That route requires a <c>type</c> parameter, answers 422 without one, and the pinned 5.2.0
/// schema declares it as a bare string with no enumeration. Nothing in this repository has
/// measured which values are valid, and inventing a list of likely ones is the same mistake as
/// the vendor-id table the input work threw out. It becomes available the day someone measures
/// it against a live instance.
/// </para>
/// </remarks>
public sealed class CatalogScopeService
{
    private readonly RomMConnection _connection;

    public CatalogScopeService(RomMConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
    }

    /// <summary>True when this kind's values can be listed at all.</summary>
    public static bool CanList(CatalogScopeKind kind) =>
        kind is CatalogScopeKind.Collection or CatalogScopeKind.SmartCollection;

    /// <summary>Why a kind cannot be listed, or null when it can.</summary>
    public static string? WhyNotListable(CatalogScopeKind kind) => kind switch
    {
        CatalogScopeKind.VirtualCollection =>
            "RomM does not publish which kinds of virtual collection exist, so RomMBat cannot "
                + "offer them without guessing.",
        _ => null,
    };

    /// <summary>Lists what a scope of this kind could point at.</summary>
    /// <remarks>
    /// A failure is a value with the reason in it, never a throw: an unreachable server while a
    /// picker is opening is an ordinary thing that should leave the screen navigable.
    /// </remarks>
    public async Task<ScopeValues> ListAsync(
        CatalogScopeKind kind,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotListable(kind) is { } why)
        {
            return new ScopeValues([], why);
        }

        try
        {
            return kind switch
            {
                CatalogScopeKind.Collection => From(
                    await _connection.ListCollectionsAsync(cancellationToken).ConfigureAwait(false),
                    rows => rows.Select(row => new ScopeValueOption(
                        row.Id.ToString(CultureInfo.InvariantCulture),
                        row.Name,
                        Count(row.Rom_count)))),

                CatalogScopeKind.SmartCollection => From(
                    await _connection.ListSmartCollectionsAsync(cancellationToken).ConfigureAwait(false),
                    rows => rows.Select(row => new ScopeValueOption(
                        row.Id.ToString(CultureInfo.InvariantCulture),
                        row.Name,
                        Count(row.Rom_count)))),

                _ => new ScopeValues([], null),
            };
        }
        catch (RomMUnreachableException ex)
        {
            return new ScopeValues([], ex.Message);
        }
    }

    /// <summary>
    /// How many games each platform holds and how much room they take, by RomM platform id.
    /// </summary>
    /// <remarks>
    /// <b>Enrichment, never a requirement.</b> The platform picker is answerable offline from
    /// <c>platform_map</c>, and it stays that way: a caller that cannot reach the server shows
    /// the platforms with no counts rather than showing nothing. The local map has no room for
    /// these without a migration, and a count that is one sync stale is worse than one fetched
    /// when the picker opens.
    /// <para>
    /// One <c>GET /api/platforms</c>, measured at 424 KB and 0.40 s for a 123-platform library.
    /// <c>PlatformRow</c> rather than the generated DTO, whose <c>fs_size_bytes</c> is an
    /// <c>int32</c> and overflows on the first platform of a real library.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyDictionary<int, (int Games, long Bytes)>> ListPlatformFactsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _connection
                .ListPlatformsAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccess)
            {
                return new Dictionary<int, (int, long)>();
            }

            return response.Value!
                .GroupBy(row => row.Id)
                .ToDictionary(group => group.Key, group => (group.First().RomCount, group.First().SizeBytes));
        }
        catch (RomMUnreachableException)
        {
            return new Dictionary<int, (int, long)>();
        }
    }

    /// <summary>
    /// The values each filter facet can take, read from the library itself.
    /// </summary>
    /// <remarks>
    /// <b>The sidecar this repository turns off everywhere else, used for the one job it is
    /// for.</b> <c>with_filter_values</c> costs a flat 841 KB and is refused on every page of a
    /// walk; here it is a single request at <c>limit=1</c> when the filter editor opens, which
    /// is what <see cref="RomMConnection.GetFilterValuesAsync"/> was built for and what its
    /// comment has said since M2.
    /// <para>
    /// <b>Eleven facets, which is RomM's whole multi-select surface rather than a subset of
    /// it.</b> Nine come from the sidecar. <c>statuses</c> and <c>metadata_providers</c> are
    /// hardcoded below because the sidecar does not report them, and the two keys it does
    /// report that no query parameter accepts, <c>game_modes</c> and <c>platforms</c>, are
    /// dropped. Finding 237.
    /// </para>
    /// <para>
    /// This returned six until the last commit of stage 7b-2a, on the reasoning that they were
    /// the ones that survive being stored and roamed through <c>Device.sync_config</c>. They
    /// all survive, because a filter is one JSON column, so that was a constraint on nothing.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ListFilterValuesAsync(
        CancellationToken cancellationToken = default)
    {
        var empty = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        try
        {
            var response = await _connection
                .GetFilterValuesAsync(new CatalogQuery { Scope = CatalogScopeKind.Filter }, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccess || response.Value is not { } values)
            {
                return empty;
            }

            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                [FilterFacet.Genres] = Sorted(values.Genres),
                [FilterFacet.Franchises] = Sorted(values.Franchises),
                [FilterFacet.Collections] = Sorted(values.Collections),
                [FilterFacet.Companies] = Sorted(values.Companies),
                [FilterFacet.AgeRatings] = Sorted(values.AgeRatings),
                [FilterFacet.Regions] = Sorted(values.Regions),
                [FilterFacet.Languages] = Sorted(values.Languages),
                [FilterFacet.PlayerCounts] = Sorted(values.PlayerCounts),
                [FilterFacet.Tags] = Sorted(values.Tags),

                // Two the sidecar does not carry, so a picker for them would open on nothing.
                // Statuses are a fixed vocabulary the user assigns and metadata providers are
                // the scrapers RomM was built with; neither is derived from the library, which
                // is all filter_values reports.
                [FilterFacet.Statuses] = Statuses,
                [FilterFacet.MetadataProviders] = MetadataProviders,
            };
        }
        catch (RomMUnreachableException)
        {
            return empty;
        }
    }

    /// <summary>
    /// The statuses a user can set, which the filter sidecar does not report.
    /// </summary>
    /// <remarks>
    /// <c>filter_values</c> describes the library, and a status nobody has assigned yet is
    /// still one you can filter for. Straight off <c>RomUserStatus</c> in the pinned schema,
    /// which enumerates them; a live probe cannot corroborate it, because an unrecognised
    /// status returns zero rows rather than the whole library and so looks exactly like a real
    /// status nobody has used.
    /// </remarks>
    private static IReadOnlyList<string> Statuses { get; } =
        ["incomplete", "finished", "completed_100", "retired", "never_playing"];

    /// <summary>
    /// The metadata sources RomM will filter on, measured rather than derived.
    /// </summary>
    /// <remarks>
    /// <b>The pinned schema declares this parameter as a bare array of strings with no
    /// enumeration</b>, and the server <b>silently ignores</b> a value it does not know, which
    /// makes a wrong entry here worse than a missing one: the user picks a provider and is
    /// handed the whole library. So these were probed one at a time against a live 5.2.0
    /// instance, where a recognised value narrows the total and an unrecognised one leaves it
    /// alone. Finding 236.
    /// <para>
    /// Deriving them from <c>SimpleRomSchema</c>'s <c>*_id</c> fields would have been wrong:
    /// <c>sgdb</c> is one of those and the filter ignores it.
    /// </para>
    /// <para>
    /// Shown as RomM spells them. <c>ss</c> and <c>ra</c> are opaque, and a friendlier name
    /// would be one this repository made up for a value the server defines.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<string> MetadataProviders { get; } =
        ["igdb", "moby", "ss", "ra", "launchbox", "hasheous", "tgdb", "flashpoint", "hltb", "gamelist", "libretro"];

    private static IReadOnlyList<string> Sorted(IEnumerable<string>? values) =>
        [.. (values ?? []).Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(v => v, StringComparer.CurrentCultureIgnoreCase)];

    private static ScopeValues From<T>(
        RomMResponse<ICollection<T>> response,
        Func<IEnumerable<T>, IEnumerable<ScopeValueOption>> project) =>
        response.IsSuccess
            ? new ScopeValues(
                [.. project(response.Value!).OrderBy(option => option.Label, StringComparer.CurrentCultureIgnoreCase)],
                null)
            : new ScopeValues([], response.Message);

    private static string Count(int roms) =>
        roms == 1 ? "1 game" : string.Create(CultureInfo.CurrentCulture, $"{roms:N0} games");
}
