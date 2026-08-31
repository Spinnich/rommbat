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

/// <summary>How several chosen values of one facet combine.</summary>
/// <remarks>
/// RomM's own three, named as its <c>*_logic</c> parameters spell them. The default is
/// <see cref="Any"/> on both sides, so a filter that never mentions logic behaves as it always
/// did.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<FilterLogic>))]
public enum FilterLogic
{
    /// <summary>OR. A game matching one of the values matches.</summary>
    [JsonStringEnumMemberName("any")]
    Any,

    /// <summary>AND. A game has to match every value.</summary>
    [JsonStringEnumMemberName("all")]
    All,

    /// <summary>NOT. A game matching any of the values is excluded.</summary>
    [JsonStringEnumMemberName("none")]
    None,
}

/// <summary>The filter half of a <see cref="CatalogScopeKind.Filter"/> scope.</summary>
/// <remarks>
/// <b>Every filter <c>GET /api/roms</c> accepts, which is what RomM's own interface offers.</b>
/// Eleven multi-selects each with a logic operator, ten yes-or-no properties, and a search
/// term. It began as five of the eleven and two of the ten, on the reasoning that those were
/// the ones that survive being stored; the rest survive equally well, and a filter screen that
/// silently offers a third of what the server does is a subset a person has to learn the edges
/// of.
/// <para>
/// <b>Four of the properties describe this user or this server rather than the game.</b>
/// <see cref="HasSaves"/>, <see cref="HasStates"/>, <see cref="Missing"/> and
/// <see cref="Favorite"/> are answered from RomM's own bookkeeping, so a set carrying one can
/// resolve to different games on a different account or after a scan. That is worth saying and
/// is not worth omitting them for: a set is re-resolved on demand and is expected to move.
/// </para>
/// <para>
/// <b>New fields are additive on purpose.</b> This is stored as JSON in
/// <c>sync_set.scope_value</c> and roamed through <c>Device.sync_config</c>, so a set written
/// by an older build simply lacks the newer keys and reads back with their defaults. Nothing
/// here may become required or change its JSON name.
/// </para>
/// </remarks>
public sealed record CatalogFilter
{
    /// <summary>The multi-select facets, keyed as <c>GET /api/roms</c> names them.</summary>
    /// <remarks>
    /// The API's own stems rather than labels, because these keys go on the wire and into
    /// stored JSON, where a display name would be a translation waiting to break.
    /// </remarks>
    public static IReadOnlyList<string> Facets { get; } =
    [
        "genres",
        "franchises",
        "collections",
        "companies",
        "age_ratings",
        "statuses",
        "regions",
        "languages",
        "player_counts",
        "metadata_providers",
        "tags",
    ];

    /// <summary>The yes-or-no properties, keyed as <c>GET /api/roms</c> names them.</summary>
    public static IReadOnlyList<string> Properties { get; } =
    [
        "matched",
        "favorite",
        "duplicate",
        "playable",
        "missing",
        "verified",
        "has_ra",
        "has_saves",
        "has_states",
        "has_soundtrack",
    ];

    [JsonPropertyName("search_term")]
    public string? SearchTerm { get; init; }

    [JsonPropertyName("genres")]
    public IReadOnlyList<string> Genres { get; init; } = [];

    [JsonPropertyName("franchises")]
    public IReadOnlyList<string> Franchises { get; init; } = [];

    [JsonPropertyName("collections")]
    public IReadOnlyList<string> Collections { get; init; } = [];

    [JsonPropertyName("companies")]
    public IReadOnlyList<string> Companies { get; init; } = [];

    [JsonPropertyName("age_ratings")]
    public IReadOnlyList<string> AgeRatings { get; init; } = [];

    /// <summary>Game status, which is set by the user rather than scraped.</summary>
    [JsonPropertyName("statuses")]
    public IReadOnlyList<string> Statuses { get; init; } = [];

    [JsonPropertyName("regions")]
    public IReadOnlyList<string> Regions { get; init; } = [];

    [JsonPropertyName("languages")]
    public IReadOnlyList<string> Languages { get; init; } = [];

    [JsonPropertyName("player_counts")]
    public IReadOnlyList<string> PlayerCounts { get; init; } = [];

    [JsonPropertyName("metadata_providers")]
    public IReadOnlyList<string> MetadataProviders { get; init; } = [];

    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// How each facet's chosen values combine, keyed as <see cref="Facets"/> is.
    /// </summary>
    /// <remarks>
    /// A map rather than eleven properties, because a facet absent from it means
    /// <see cref="FilterLogic.Any"/> and writing that out eleven times per set would put the
    /// default in the stored JSON where a later change to it could not reach.
    /// </remarks>
    [JsonPropertyName("logic")]
    public IReadOnlyDictionary<string, FilterLogic> Logic { get; init; } =
        new Dictionary<string, FilterLogic>(StringComparer.Ordinal);

    /// <summary>Whether the game is matched to a metadata source.</summary>
    [JsonPropertyName("matched")]
    public bool? Matched { get; init; }

    /// <summary>Favourites are collection membership in RomM, and this is the filter form of it.</summary>
    [JsonPropertyName("favorite")]
    public bool? Favorite { get; init; }

    /// <summary>Whether the game has more than one version. RomM's interface says "has versions".</summary>
    [JsonPropertyName("duplicate")]
    public bool? Duplicate { get; init; }

    [JsonPropertyName("playable")]
    public bool? Playable { get; init; }

    /// <summary>Whether RomM's own scan can no longer find the file.</summary>
    [JsonPropertyName("missing")]
    public bool? Missing { get; init; }

    /// <summary>Hash verified against a known-good database.</summary>
    [JsonPropertyName("verified")]
    public bool? Verified { get; init; }

    [JsonPropertyName("has_ra")]
    public bool? HasRetroAchievements { get; init; }

    [JsonPropertyName("has_saves")]
    public bool? HasSaves { get; init; }

    [JsonPropertyName("has_states")]
    public bool? HasStates { get; init; }

    [JsonPropertyName("has_soundtrack")]
    public bool? HasSoundtrack { get; init; }

    /// <summary>True when nothing is set, which would match the whole library.</summary>
    /// <remarks>
    /// Derived from <see cref="Facets"/> and <see cref="Properties"/> rather than listed by
    /// hand, so a facet added above cannot be forgotten here and leave a filter that matches
    /// everything looking like one that was set.
    /// </remarks>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(SearchTerm)
        && Facets.All(facet => ValuesFor(facet).Count == 0)
        && Properties.All(property => Property(property) is null);

    /// <summary>What one facet holds, by its API name.</summary>
    public IReadOnlyList<string> ValuesFor(string facet) => facet switch
    {
        "genres" => Genres,
        "franchises" => Franchises,
        "collections" => Collections,
        "companies" => Companies,
        "age_ratings" => AgeRatings,
        "statuses" => Statuses,
        "regions" => Regions,
        "languages" => Languages,
        "player_counts" => PlayerCounts,
        "metadata_providers" => MetadataProviders,
        "tags" => Tags,
        _ => [],
    };

    /// <summary>The same facet with different values.</summary>
    public CatalogFilter WithValues(string facet, IReadOnlyList<string> values) => facet switch
    {
        "genres" => this with { Genres = values },
        "franchises" => this with { Franchises = values },
        "collections" => this with { Collections = values },
        "companies" => this with { Companies = values },
        "age_ratings" => this with { AgeRatings = values },
        "statuses" => this with { Statuses = values },
        "regions" => this with { Regions = values },
        "languages" => this with { Languages = values },
        "player_counts" => this with { PlayerCounts = values },
        "metadata_providers" => this with { MetadataProviders = values },
        "tags" => this with { Tags = values },
        _ => this,
    };

    /// <summary>How one facet's values combine. Absent means <see cref="FilterLogic.Any"/>.</summary>
    public FilterLogic LogicFor(string facet) =>
        Logic.TryGetValue(facet, out var logic) ? logic : FilterLogic.Any;

    /// <summary>What one yes-or-no property is set to, by its API name.</summary>
    public bool? Property(string property) => property switch
    {
        "matched" => Matched,
        "favorite" => Favorite,
        "duplicate" => Duplicate,
        "playable" => Playable,
        "missing" => Missing,
        "verified" => Verified,
        "has_ra" => HasRetroAchievements,
        "has_saves" => HasSaves,
        "has_states" => HasStates,
        "has_soundtrack" => HasSoundtrack,
        _ => null,
    };

    /// <summary>The same filter with one property set or cleared.</summary>
    public CatalogFilter WithProperty(string property, bool? value) => property switch
    {
        "matched" => this with { Matched = value },
        "favorite" => this with { Favorite = value },
        "duplicate" => this with { Duplicate = value },
        "playable" => this with { Playable = value },
        "missing" => this with { Missing = value },
        "verified" => this with { Verified = value },
        "has_ra" => this with { HasRetroAchievements = value },
        "has_saves" => this with { HasSaves = value },
        "has_states" => this with { HasStates = value },
        "has_soundtrack" => this with { HasSoundtrack = value },
        _ => this,
    };
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
            // Off on every page. Whole-library index and filter metadata, not per-page data,
            // and the server resends them in full each time.
            new("with_char_index", "false"),

            // Follows the scope rather than being a constant, because the premise above only
            // holds unscoped. Under a scoping parameter the index spans the scope rather than
            // the library, and it is what lets the server serve a page by primary key instead
            // of OFFSET n LIMIT m over a sort with no covering index.
            //
            // Measured against a live 88,331-rom instance on 5.2.0 (argosy-findings A1):
            // scoped, turning it off costs six seconds a page to save 63 KiB; unscoped it costs
            // about 130 ms to save 600 KiB. Measured end to end here too: a 9,196-rom platform
            // scope walked in 8m 15s with it off, which is what a person waits through on the
            // first screen that resolves a set.
            new("with_rom_id_index", Scope == CatalogScopeKind.Filter ? "false" : "true"),
            new("with_filter_values", withFilterValues ? "true" : "false"),

            // Kept on: it is an integer, it costs nothing, and it is the only way a resumable
            // walk knows how far it has left to go. Load-bearing since RomM 5.2.0, which made
            // the response's `total` nullable: the server returns null when neither this nor
            // with_rom_id_index is set, and RomPage.Total is a non-nullable int, so turning
            // this off to save bytes throws on deserialisation rather than degrading.
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

            // Driven off the filter's own lists, so a facet added there reaches the wire
            // without a second edit here. The logic operator is sent only when it is not the
            // default, which keeps a plain filter's query string as short as it was.
            foreach (var facet in CatalogFilter.Facets)
            {
                var values = filter.ValuesFor(facet);
                AddAll(parameters, facet, values);

                if (values.Count > 0 && filter.LogicFor(facet) is var logic && logic != FilterLogic.Any)
                {
                    Add(parameters, facet + "_logic", logic.ToString().ToLowerInvariant());
                }
            }

            foreach (var property in CatalogFilter.Properties)
            {
                Add(parameters, property, filter.Property(property));
            }
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
