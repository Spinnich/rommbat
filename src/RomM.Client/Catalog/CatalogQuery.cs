using System.Globalization;
using System.Text.Json.Serialization;

namespace RomM.Client.Catalog;

/// <summary>What a sync set is scoped to.</summary>
/// <remarks>
/// All five resolve the same way, by paging <c>GET /api/roms</c> with the scope as a query
/// parameter. None of them reads membership off a collection payload: <c>rom_ids</c> is a
/// full set on every collection response, and M0 probe 5 measured one collection at 715 KB
/// with no pagination available.
/// </remarks>
public enum CatalogScopeKind
{
    /// <summary>A hand-curated collection. Needs <c>collections.read</c>.</summary>
    Collection,

    /// <summary>A server-side saved search. Membership drifts, so it is re-resolved every sync.</summary>
    SmartCollection,

    /// <summary>A generated grouping such as a genre or a franchise.</summary>
    VirtualCollection,

    /// <summary>One RomM platform.</summary>
    Platform,

    /// <summary>A filter this client saved. Needs no collection scope.</summary>
    Filter,
}

/// <summary>How a resolved set is ordered before the caps are applied.</summary>
public enum SetOrdering
{
    /// <summary>By sort name, ascending. The default.</summary>
    Name,

    /// <summary>Smallest first, which fits the most games into a byte budget.</summary>
    SizeAscending,

    /// <summary>Largest first.</summary>
    SizeDescending,

    /// <summary>Most recently updated in RomM first.</summary>
    RecentlyUpdated,
}

/// <summary>The filter half of a <see cref="CatalogScopeKind.Filter"/> scope.</summary>
/// <remarks>
/// A deliberately small subset of what <c>GET /api/roms</c> accepts. Every field here
/// survives being stored, roamed through <c>Device.sync_config</c> and replayed against a
/// server that has never seen this device, which is not true of the server's own transient
/// filter state.
/// </remarks>
public sealed record CatalogFilter
{
    [JsonPropertyName("search_term")]
    public string? SearchTerm { get; init; }

    [JsonPropertyName("genres")]
    public IReadOnlyList<string> Genres { get; init; } = [];

    [JsonPropertyName("regions")]
    public IReadOnlyList<string> Regions { get; init; } = [];

    [JsonPropertyName("languages")]
    public IReadOnlyList<string> Languages { get; init; } = [];

    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get; init; } = [];

    [JsonPropertyName("franchises")]
    public IReadOnlyList<string> Franchises { get; init; } = [];

    /// <summary>Favourites are collection membership in RomM, and this is the filter form of it.</summary>
    [JsonPropertyName("favorite")]
    public bool? Favorite { get; init; }

    [JsonPropertyName("matched")]
    public bool? Matched { get; init; }

    /// <summary>True when nothing is set, which would match the whole library.</summary>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(SearchTerm)
        && Genres.Count == 0
        && Regions.Count == 0
        && Languages.Count == 0
        && Tags.Count == 0
        && Franchises.Count == 0
        && Favorite is null
        && Matched is null;
}

/// <summary>One query against <c>GET /api/roms</c>, ready to be paged.</summary>
public sealed record CatalogQuery
{
    /// <summary>What the query is scoped to.</summary>
    public required CatalogScopeKind Scope { get; init; }

    /// <summary>
    /// The scope's identifier: a collection, smart-collection or platform id, or a virtual
    /// collection's string id. Ignored for <see cref="CatalogScopeKind.Filter"/>.
    /// </summary>
    public string? ScopeId { get; init; }

    /// <summary>The saved filter, for a filter scope. Also applied on top of any other scope.</summary>
    public CatalogFilter? Filter { get; init; }

    /// <summary>A search term typed now, which narrows whatever the scope already selected.</summary>
    public string? SearchTerm { get; init; }

    /// <summary>
    /// Only ROMs updated after this instant.
    /// </summary>
    /// <remarks>
    /// The normal path. A full walk of 83k ROMs takes about 14 minutes at 250 per page, so
    /// it is a first-run or repair operation and this is what every other run does instead.
    /// </remarks>
    public DateTimeOffset? UpdatedAfter { get; init; }

    /// <summary>
    /// Walk order, which is not the same thing as the set's ordering policy.
    /// </summary>
    /// <remarks>
    /// Ascending id is what makes offset paging survive a library that changes underneath it:
    /// RomM hands out ascending ids, so a ROM added mid-walk lands past the cursor instead of
    /// shifting every later page by one. Deletions can still cause a skip, which is what M3's
    /// reconcile against <c>GET /api/roms/identifiers</c> is for.
    /// </remarks>
    public string OrderBy { get; init; } = "id";

    public string OrderDirection { get; init; } = "asc";

    /// <summary>Builds the query string for one page.</summary>
    /// <param name="withFilterValues">
    /// Turns the filter-value sidecar back on. Used once per session by the filter picker and
    /// never while paging: M0 probe 5 measured the sidecars at a flat 841 KB resent on every
    /// request, 65% of the body at the default page size.
    /// </param>
    public string ToQueryString(int limit, int offset, bool withFilterValues = false)
    {
        var parameters = new List<KeyValuePair<string, string>>
        {
            // Off on every page. They are whole-library index and filter metadata, not
            // per-page data, and the server resends them in full each time.
            new("with_char_index", "false"),
            new("with_rom_id_index", "false"),
            new("with_filter_values", withFilterValues ? "true" : "false"),

            // Kept on: it is an integer, it costs nothing, and it is the only way a resumable
            // walk knows how far it has left to go.
            new("with_total", "true"),

            // Opt-in and left off. Per-file detail is M3's problem, and it multiplies the body.
            new("with_files", "false"),
            new("limit", limit.ToString(CultureInfo.InvariantCulture)),
            new("offset", offset.ToString(CultureInfo.InvariantCulture)),
            new("order_by", OrderBy),
            new("order_dir", OrderDirection),
        };

        switch (Scope)
        {
            case CatalogScopeKind.Collection:
                Add(parameters, "collection_id", ScopeId);
                break;
            case CatalogScopeKind.SmartCollection:
                Add(parameters, "smart_collection_id", ScopeId);
                break;
            case CatalogScopeKind.VirtualCollection:
                Add(parameters, "virtual_collection_id", ScopeId);
                break;
            case CatalogScopeKind.Platform:
                Add(parameters, "platform_ids", ScopeId);
                break;
            case CatalogScopeKind.Filter:
            default:
                break;
        }

        if (Filter is { } filter)
        {
            Add(parameters, "search_term", filter.SearchTerm);
            AddAll(parameters, "genres", filter.Genres);
            AddAll(parameters, "regions", filter.Regions);
            AddAll(parameters, "languages", filter.Languages);
            AddAll(parameters, "tags", filter.Tags);
            AddAll(parameters, "franchises", filter.Franchises);
            Add(parameters, "favorite", filter.Favorite);
            Add(parameters, "matched", filter.Matched);
        }

        // A term typed now wins over one stored in the filter, so narrowing a saved set in
        // the browser does not require editing the set.
        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            parameters.RemoveAll(pair => pair.Key == "search_term");
            Add(parameters, "search_term", SearchTerm);
        }

        if (UpdatedAfter is { } updatedAfter)
        {
            Add(parameters, "updated_after", updatedAfter.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        }

        return string.Join(
            '&',
            parameters.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
    }

    private static void Add(List<KeyValuePair<string, string>> parameters, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parameters.Add(new KeyValuePair<string, string>(name, value));
        }
    }

    private static void Add(List<KeyValuePair<string, string>> parameters, string name, bool? value)
    {
        if (value is { } flag)
        {
            parameters.Add(new KeyValuePair<string, string>(name, flag ? "true" : "false"));
        }
    }

    // Repeated rather than comma-joined: the endpoint documents multiple values as repeating
    // the parameter, and a comma would be read as one value containing a comma.
    private static void AddAll(List<KeyValuePair<string, string>> parameters, string name, IReadOnlyList<string> values)
    {
        foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            parameters.Add(new KeyValuePair<string, string>(name, value));
        }
    }
}
