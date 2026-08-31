using System.Globalization;
using RomM.Client;
using RomM.Client.Catalog;

namespace RomMBat.Core.Sets;

/// <summary>
/// The filter facets RomMBat can store, which is fewer than RomM offers.
/// </summary>
/// <remarks>
/// RomM returns ten in <c>filter_values</c>. These five plus favourites are what
/// <see cref="CatalogFilter"/> persists, and a facet that cannot be saved is a picker that
/// forgets, so the others are not offered.
/// </remarks>
public static class FilterFacet
{
    public const string Genres = "Genres";

    public const string Regions = "Regions";

    public const string Languages = "Languages";

    public const string Tags = "Tags";

    public const string Franchises = "Franchises";

    /// <summary>Favourites are collection membership in RomM, so this is a yes or no.</summary>
    public const string Favourites = "Favourites only";

    /// <summary>The multi-select facets, in the order a picker offers them.</summary>
    public static IReadOnlyList<string> Multi { get; } = [Genres, Regions, Languages, Tags, Franchises];
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
    /// Only the facets <see cref="CatalogFilter"/> can persist are returned. RomM offers ten;
    /// the six here are the ones that survive being stored, roamed through
    /// <c>Device.sync_config</c> and replayed against a server that has never seen this device.
    /// Offering a facet that cannot be saved would be a picker that forgets.
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
                [FilterFacet.Regions] = Sorted(values.Regions),
                [FilterFacet.Languages] = Sorted(values.Languages),
                [FilterFacet.Tags] = Sorted(values.Tags),
                [FilterFacet.Franchises] = Sorted(values.Franchises),
            };
        }
        catch (RomMUnreachableException)
        {
            return empty;
        }
    }

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
