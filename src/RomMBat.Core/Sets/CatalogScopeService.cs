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
/// that is not offered.</b> A collection kind with no way to choose its value leaves the editor
/// able to say only that a value is needed.
/// <para>
/// <b>Listing collections is expensive and is only ever for naming.</b> RB-355 measured a
/// single collection at 714.8 KB, 99% of it inlined cover-art paths, with no pagination. One
/// call when a picker opens is a fair price; reading membership off <c>rom_ids</c> is not, and
/// never happens: a set resolves by paging <c>GET /api/roms</c> like every other scope.
/// </para>
/// <para>
/// <b>Virtual collections are not a scope.</b> That route requires a <c>type</c> parameter,
/// answers 422 without one, and the pinned schema declares it as a bare string with no
/// enumeration, so the values could only be guessed.
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

    /// <summary>Lists what a scope of this kind could point at.</summary>
    /// <remarks>
    /// A failure is a value with the reason in it, never a throw: an unreachable server while a
    /// picker is opening is an ordinary thing that should leave the screen navigable.
    /// </remarks>
    public async Task<ScopeValues> ListAsync(
        CatalogScopeKind kind,
        CancellationToken cancellationToken = default)
    {
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

                // No count. `rom_count` is stored and computed as the owner, while paging applies
                // the criteria as the caller, so a public collection filtering on `favorite`
                // lists another account's favorites and resolves to what this one has marked.
                // Measured: 29 of 29 advertised between 6 and 594 and paged back 0.
                CatalogScopeKind.SmartCollection => From(
                    await _connection.ListSmartCollectionsAsync(cancellationToken).ConfigureAwait(false),
                    rows => rows.Select(row => new ScopeValueOption(
                        row.Id.ToString(CultureInfo.InvariantCulture),
                        row.Name,
                        null))),

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
