using System.Text.Json;

namespace RomMBat.Core.Sync;

/// <summary>
/// The rom ids a picked set holds, as they sit in <c>sync_set.scope_value</c>.
/// </summary>
/// <remarks>
/// <b>For this scope the id list is the definition</b>, exactly as a filter's JSON is, which is
/// why migration 014 adds no column. It is also what roams: <c>RoamingSyncConfig</c> already
/// carries <c>scope_value</c> verbatim, so a picked set travels with no change to that
/// document at all.
/// <para>
/// <b>An unreadable value reads as no games rather than throwing</b>, the same rule
/// <see cref="CatalogFilterJson"/> follows. A set whose scope was corrupted should list and be
/// obviously empty, not stop the sets screen from drawing.
/// </para>
/// </remarks>
public static class PickedScopeJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Serialises the ids, sorted and deduplicated.
    /// </summary>
    /// <remarks>
    /// Sorted so a picked set's stored value is a function of what it holds rather than of the
    /// order somebody pressed things in, which is what keeps a roaming push from rewriting the
    /// document every time a set is re-read. Deduplicated because picking a game twice is one
    /// game, and a repeated id would become a repeated member row the store's primary key
    /// would reject.
    /// </remarks>
    public static string Write(IEnumerable<int> romIds)
    {
        ArgumentNullException.ThrowIfNull(romIds);
        return JsonSerializer.Serialize(romIds.Distinct().Order().ToArray(), Options);
    }

    /// <summary>Reads a stored id list, or an empty one.</summary>
    public static IReadOnlyList<int> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<int[]>(json, Options) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
