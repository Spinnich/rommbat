using RomM.Client;
using RomMBat.Core.Sets;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

public class CatalogScopeServiceTests
{
    [Fact]
    public async Task A_smart_collection_is_offered_without_the_count_the_server_stores_for_its_owner()
    {
        // Measured on 5.3.0-alpha.2: collection 29 listed rom_count 594 and paged back a total of
        // 0 for an account that had marked none of the owner's favourites.
        using var stub = new StubRomMServer();
        stub.SmartCollections.Add((29, "Favourites", 594));
        using var connection = new RomMConnection(
            new RomMClientOptions { Origin = new Uri("https://romm.test"), AccessToken = "rmm_test" },
            stub);

        var values = await new CatalogScopeService(connection)
            .ListAsync(RomM.Client.Catalog.CatalogScopeKind.SmartCollection, TestContext.Current.CancellationToken);

        Assert.False(values.IsRefused);
        var option = Assert.Single(values.Options);
        Assert.Equal("29", option.Value);
        Assert.Equal("Favourites", option.Label);
        Assert.Null(option.Detail);
    }
}
