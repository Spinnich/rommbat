using System.Globalization;
using RomM.Client;
using RomM.Client.Catalog;

namespace RomMBat.Core.Sets;

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
