using System.Text.Json;
using RomM.Client.Catalog;

namespace RomMBat.Core.Sync;

/// <summary>
/// Reads and writes the saved filter a <see cref="CatalogScopeKind.Filter"/> set stores.
/// </summary>
/// <remarks>
/// The filter goes into <c>sync_set.scope_value</c> as JSON, and the same JSON roams in
/// <c>Device.sync_config</c>, so this is the one place its shape is decided.
/// </remarks>
public static class CatalogFilterJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Serialises a filter for storage.</summary>
    public static string Write(CatalogFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return JsonSerializer.Serialize(filter, Options);
    }

    /// <summary>
    /// Reads a stored filter.
    /// </summary>
    /// <remarks>
    /// An unreadable value comes back as an empty filter rather than throwing. A set whose
    /// filter was corrupted should resolve to something visible and obviously wrong, not stop
    /// the whole sync from listing.
    /// </remarks>
    public static CatalogFilter Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new CatalogFilter();
        }

        try
        {
            return JsonSerializer.Deserialize<CatalogFilter>(json, Options) ?? new CatalogFilter();
        }
        catch (JsonException)
        {
            return new CatalogFilter();
        }
    }
}
