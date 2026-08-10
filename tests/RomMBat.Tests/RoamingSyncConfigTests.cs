using System.Text.Json;
using RomM.Client;
using RomM.Client.Catalog;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// What travels in <c>Device.sync_config</c>, and what must not.
/// </summary>
/// <remarks>
/// The device this roams to has a different tree and possibly a different drive letter, so
/// a path in here would be wrong on arrival rather than merely useless. Folder names are the
/// coarsest thing that means the same on every RetroBat install.
/// </remarks>
public class RoamingSyncConfigTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);

    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();
    private readonly LocalStore _store;

    public RoamingSyncConfigTests() => _store = LocalStore.Open(_tree.Install());

    public void Dispose()
    {
        _store.Dispose();
        _tree.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Definitions_and_overrides_travel_and_membership_does_not()
    {
        _store.SyncSets.Add(
            new SyncSetDefinition
            {
                Name = "My SNES favourites",
                Scope = CatalogScopeKind.Platform,
                ScopeValue = "6",
                MaxGames = 40,
                MaxBytes = 8L * 1024 * 1024 * 1024,
                Ordering = SetOrdering.SizeAscending,
            },
            Now);

        _store.PlatformMap.SetOverride("arcade", "fbneo", Now);

        var document = RoamingSyncConfig.FromStore(_store, Now);

        Assert.Single(document.Sets);
        Assert.Equal("platform", document.Sets[0].Scope);
        Assert.Equal(40, document.Sets[0].MaxGames);
        Assert.Equal("size_asc", document.Sets[0].Ordering);
        Assert.Equal("fbneo", document.PlatformOverrides["arcade"]);

        // Membership is re-resolved every sync, so it has no business roaming.
        var json = JsonSerializer.Serialize(document);
        Assert.DoesNotContain("rom_id", json, StringComparison.Ordinal);
        Assert.DoesNotContain("members", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Nothing_that_roams_is_a_path()
    {
        _store.SyncSets.Add(
            new SyncSetDefinition
            {
                Name = "arcade",
                Scope = CatalogScopeKind.Platform,
                ScopeValue = "2",
                FolderOverride = "fbneo",
            },
            Now);

        var json = JsonSerializer.Serialize(RoamingSyncConfig.FromStore(_store, Now));

        Assert.DoesNotContain(":\\", json, StringComparison.Ordinal);
        Assert.DoesNotContain("roms/", json, StringComparison.Ordinal);
        Assert.DoesNotContain(_tree.Install().RootPath, json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Merging_keeps_keys_this_client_does_not_own()
    {
        using var existing = JsonDocument.Parse(
            """
            {"rommbat": {"version": 0, "sets": []}, "someone_else": {"keep": "me"}, "top_level": 7}
            """);

        var merged = RoamingSyncConfig.FromStore(_store, Now).MergeInto(existing.RootElement);

        Assert.True(merged.ContainsKey("someone_else"));
        Assert.True(merged.ContainsKey("top_level"));
        Assert.IsType<RoamingSyncConfig>(merged[RoamingSyncConfig.Key]);
    }

    [Fact]
    public void A_roamed_definition_comes_back_as_a_local_one()
    {
        _store.SyncSets.Add(
            new SyncSetDefinition
            {
                Name = "roamed",
                Scope = CatalogScopeKind.SmartCollection,
                ScopeValue = "11",
                MaxBytes = 1024,
            },
            Now);

        var merged = RoamingSyncConfig.FromStore(_store, Now).MergeInto(null);
        using var wire = JsonDocument.Parse(JsonSerializer.Serialize(merged));

        var recovered = RoamingSyncConfig.Extract(wire.RootElement);

        Assert.NotNull(recovered);
        var definition = RoamingSyncConfig.ToDefinition(recovered.Sets[0], Now);

        Assert.Equal("roamed", definition.Name);
        Assert.Equal(CatalogScopeKind.SmartCollection, definition.Scope);
        Assert.Equal("11", definition.ScopeValue);
        Assert.Equal(1024, definition.MaxBytes);
    }

    [Fact]
    public void A_sync_config_without_our_key_reads_as_nothing_rather_than_failing()
    {
        using var foreign = JsonDocument.Parse("""{"someone_else": {"keep": "me"}}""");

        Assert.Null(RoamingSyncConfig.Extract(foreign.RootElement));
        Assert.Null(RoamingSyncConfig.Extract(null));
    }

    [Fact]
    public void A_collection_scope_is_refused_when_collections_read_was_not_granted()
    {
        var narrowed = new GrantedScopes([RomMScopes.RomsRead, RomMScopes.PlatformsRead]);

        Assert.False(narrowed.Allows(RomMFeature.CollectionSets));
        Assert.Contains(RomMScopes.CollectionsRead, narrowed.MissingFor(RomMFeature.CollectionSets));

        // Platform and filter scopes need nothing beyond the library feature, which is what
        // makes them the two that survive a narrowed grant.
        Assert.True(narrowed.Allows(RomMFeature.Library));
    }

    [Fact]
    public void A_saved_filter_round_trips_through_its_stored_json()
    {
        var filter = new CatalogFilter
        {
            SearchTerm = "mario",
            Genres = ["Platform"],
            Regions = ["USA", "Europe"],
            Favorite = true,
        };

        var recovered = CatalogFilterJson.Parse(CatalogFilterJson.Write(filter));

        Assert.Equal("mario", recovered.SearchTerm);
        Assert.Equal(["Platform"], recovered.Genres);
        Assert.Equal(["USA", "Europe"], recovered.Regions);
        Assert.True(recovered.Favorite);
        Assert.False(recovered.IsEmpty);
    }

    [Fact]
    public void An_unreadable_stored_filter_becomes_an_empty_one_rather_than_stopping_the_listing()
    {
        Assert.True(CatalogFilterJson.Parse("{not json").IsEmpty);
        Assert.True(CatalogFilterJson.Parse(null).IsEmpty);
    }
}
