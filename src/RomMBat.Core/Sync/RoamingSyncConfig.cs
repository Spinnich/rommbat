using System.Text.Json;
using System.Text.Json.Serialization;
using RomMBat.Core.Store;

namespace RomMBat.Core.Sync;

/// <summary>One sync set as it travels between devices.</summary>
public sealed record RoamingSyncSet
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("scope")]
    public required string Scope { get; init; }

    [JsonPropertyName("scope_value")]
    public required string ScopeValue { get; init; }

    [JsonPropertyName("max_games")]
    public int? MaxGames { get; init; }

    [JsonPropertyName("max_bytes")]
    public long? MaxBytes { get; init; }

    [JsonPropertyName("ordering")]
    public string Ordering { get; init; } = "name";

    [JsonPropertyName("eviction_policy")]
    public string EvictionPolicy { get; init; } = "keep_favourites";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = true;

    /// <summary>A RetroBat folder name. Never a path, so a drive letter cannot travel with it.</summary>
    [JsonPropertyName("folder")]
    public string? Folder { get; init; }
}

/// <summary>
/// What RomMBat keeps in <c>Device.sync_config</c> so a setup follows the user.
/// </summary>
/// <remarks>
/// Definitions only. Never membership, which is re-resolved on every sync, and never a path,
/// because the device this roams to has a different tree and possibly a different drive
/// letter. Folder names are the coarsest thing that travels, and they are meaningful on any
/// RetroBat install.
/// </remarks>
public sealed record RoamingSyncConfig
{
    /// <summary>The key RomMBat owns inside the shared <c>sync_config</c> dictionary.</summary>
    public const string Key = "rommbat";

    /// <summary>
    /// Used to read the document back. Writing goes through the connection's serializer, so
    /// setting a write option here would only claim something that does not happen.
    /// </summary>
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Bumped when the shape changes, so an older client can refuse rather than misread.</summary>
    [JsonPropertyName("version")]
    public int Version { get; init; } = 1;

    [JsonPropertyName("sets")]
    public IReadOnlyList<RoamingSyncSet> Sets { get; init; } = [];

    /// <summary>
    /// RomM <c>fs_slug</c> to RetroBat folder. The user's mapping choices, and only those.
    /// </summary>
    /// <remarks>
    /// Keyed by <c>fs_slug</c> rather than by slug, because RomM keeps only the former
    /// unique: a real 123-platform library carried 72 distinct slugs.
    /// </remarks>
    [JsonPropertyName("platform_overrides")]
    public IReadOnlyDictionary<string, string> PlatformOverrides { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; init; }

    /// <summary>Reads the current definitions out of the local store.</summary>
    public static RoamingSyncConfig FromStore(LocalStore store, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(store);

        return new RoamingSyncConfig
        {
            Sets =
            [
                .. store.SyncSets.List().Select(set => new RoamingSyncSet
                {
                    Name = set.Name,
                    Scope = SyncSetStore.ScopeText(set.Scope),
                    ScopeValue = set.ScopeValue,
                    MaxGames = set.MaxGames,
                    MaxBytes = set.MaxBytes,
                    Ordering = SyncSetStore.OrderingText(set.Ordering),
                    EvictionPolicy = set.EvictionPolicy,
                    Enabled = set.Enabled,
                    Folder = set.FolderOverride,
                }),
            ],
            PlatformOverrides = store.PlatformMap.Overrides(),
            UpdatedAt = now.ToUniversalTime().ToString("O"),
        };
    }

    /// <summary>
    /// Puts this document into a <c>sync_config</c> dictionary without disturbing the rest.
    /// </summary>
    /// <remarks>
    /// <c>sync_config</c> is free-form and shared with whatever else writes to this device, so
    /// the whole dictionary is read, one key is replaced, and the result is written back.
    /// Sending only our own key would delete everyone else's.
    /// </remarks>
    public IReadOnlyDictionary<string, object?> MergeInto(object? existingSyncConfig)
    {
        var merged = new Dictionary<string, object?>(StringComparer.Ordinal);

        if (existingSyncConfig is JsonElement { ValueKind: JsonValueKind.Object } element)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (!string.Equals(property.Name, Key, StringComparison.Ordinal))
                {
                    merged[property.Name] = property.Value.Clone();
                }
            }
        }

        merged[Key] = this;
        return merged;
    }

    /// <summary>Reads RomMBat's key back out of a device's <c>sync_config</c>, or null when absent.</summary>
    public static RoamingSyncConfig? Extract(object? syncConfig)
    {
        if (syncConfig is not JsonElement { ValueKind: JsonValueKind.Object } element
            || !element.TryGetProperty(Key, out var mine))
        {
            return null;
        }

        try
        {
            return mine.Deserialize<RoamingSyncConfig>(Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Turns a roaming set back into a local definition.</summary>
    public static SyncSetDefinition ToDefinition(RoamingSyncSet set, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(set);

        return new SyncSetDefinition
        {
            Name = set.Name,
            Scope = SyncSetStore.ParseScope(set.Scope),
            ScopeValue = set.ScopeValue,
            MaxGames = set.MaxGames,
            MaxBytes = set.MaxBytes,
            Ordering = SyncSetStore.ParseOrdering(set.Ordering),
            EvictionPolicy = set.EvictionPolicy,
            Enabled = set.Enabled,
            FolderOverride = set.Folder,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }
}
