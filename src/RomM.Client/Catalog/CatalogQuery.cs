using System.Globalization;

namespace RomM.Client.Catalog;

/// <summary>What a sync set is scoped to.</summary>
/// <remarks>
/// <b>All three resolve the same way</b>, by paging <c>GET /api/roms</c> with the scope as a
/// query parameter. None of them reads membership off a collection payload: <c>rom_ids</c> is a
/// full set on every collection response, and RB-355 measured one collection at 715 KB with no
/// pagination available.
/// </remarks>
public enum CatalogScopeKind
{
    /// <summary>A hand-curated collection. Needs <c>collections.read</c>.</summary>
    Collection,

    /// <summary>A server-side saved search. Membership drifts, so it is re-resolved every sync.</summary>
    SmartCollection,

    /// <summary>One RomM platform.</summary>
    Platform,
}

/// <summary>How a resolved set is ordered before the caps are applied.</summary>
/// <remarks>
/// <b>Which member a set falls back to is not stated here.</b> That belongs to the layer that
/// creates sets, and naming it in a doc comment beside the enum is how it came to be declared
/// in three places and answered differently in two of them.
/// </remarks>
public enum SetOrdering
{
    /// <summary>By sort name, ascending.</summary>
    Name,

    /// <summary>Smallest first, which fits the most games into a byte budget.</summary>
    SizeAscending,

    /// <summary>Largest first.</summary>
    SizeDescending,

    /// <summary>Most recently updated in RomM first.</summary>
    RecentlyUpdated,
}

/// <summary>One query against <c>GET /api/roms</c>, ready to be paged.</summary>
public sealed record CatalogQuery
{
    /// <summary>What the query is scoped to.</summary>
    public required CatalogScopeKind Scope { get; init; }

    /// <summary>The scope's identifier: a collection, smart-collection or platform id.</summary>
    public string? ScopeId { get; init; }

    /// <summary>
    /// Only ROMs updated after this instant.
    /// </summary>
    /// <remarks>
    /// The normal path. A full walk of a 96k library takes minutes at 250 per page (RB-354), so
    /// it is a first-run or repair operation and this is what every other run does instead.
    /// </remarks>
    public DateTimeOffset? UpdatedAfter { get; init; }

    /// <summary>
    /// Walk order, which is not the same thing as the set's ordering policy.
    /// </summary>
    /// <remarks>
    /// Ascending id is what makes offset paging survive a library that changes underneath it:
    /// RomM hands out ascending ids, so a ROM added mid-walk lands past the cursor instead of
    /// shifting every later page by one. Deletions can still cause a skip, which the next
    /// resolve's completed walk reconciles.
    /// </remarks>
    public string OrderBy { get; init; } = "id";

    public string OrderDirection { get; init; } = "asc";

    /// <summary>Builds the query string for one page.</summary>
    /// <exception cref="InvalidOperationException">
    /// The scope is not one of the three. <b>Refused rather than paged</b>, because every scoping
    /// parameter would be omitted and the query would walk the entire library.
    /// </exception>
    public string ToQueryString(int limit, int offset)
    {
        var parameters = new List<KeyValuePair<string, string>>
        {
            // Off on every page. Whole-library index and filter metadata, not per-page data,
            // and the server resends them in full each time.
            new("with_char_index", "false"),

            // Off under every scope too. Every id the scope matches, resent on every page: 63 KiB
            // a page on a 9,194-ROM platform and 114 KiB on a 16,687-ROM virtual collection, at
            // no latency cost (RM-9, probe R2).
            new("with_rom_id_index", "false"),
            new("with_filter_values", "false"),

            // Kept on: it is the only way a resumable walk knows how far it has left to go. With
            // the index off it is a separate count rather than the index's length: inside the
            // noise of a scoped page and about 140 ms unscoped (RM-9). Load-bearing: the server
            // returns a null `total` when neither this nor with_rom_id_index is set, and
            // RomPage.Total is a non-nullable int, so turning this off to save bytes throws on
            // deserialization rather than degrading.
            new("with_total", "true"),

            // Opt-in and left off. Per-file detail is the content sync's to ask for, and it
            // multiplies the body.
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
            case CatalogScopeKind.Platform:
                Add(parameters, "platform_ids", ScopeId);
                break;
            default:
                throw new InvalidOperationException($"No query parameter scopes a {Scope} set.");
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
}
