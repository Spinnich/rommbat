using System.Globalization;
using System.Net;
using RomM.Client;
using RomM.Client.Catalog;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Paged browsing: the sidecars, the page size, and what happens when a walk is cut short.
/// </summary>
/// <remarks>
/// M0 probe 5 measured the three costly sidecar flags at a flat 841 KB resent on every
/// request, 65% of the response body at the default page size. Walking 83k ROMs with them
/// on would resend about 1.4 GB of identical data, so their absence is asserted on the wire
/// rather than trusted.
/// </remarks>
public class CatalogPagingTests
{
    private static readonly Uri Origin = new("https://romm.invalid/");

    [Fact]
    public void Every_page_request_turns_the_costly_sidecars_off()
    {
        var query = new CatalogQuery { Scope = CatalogScopeKind.Platform, ScopeId = "6" };

        var built = query.ToQueryString(limit: 250, offset: 500);

        Assert.Contains("with_char_index=false", built, StringComparison.Ordinal);
        Assert.Contains("with_filter_values=false", built, StringComparison.Ordinal);
        Assert.Contains("with_files=false", built, StringComparison.Ordinal);
    }

    /// <summary>
    /// The rom id index follows the scope, because its cost inverts with one.
    /// </summary>
    /// <remarks>
    /// Scoped, the index spans the scope rather than the library and is what lets the server
    /// answer by primary key: measured at six seconds a page to save 63 KiB with it off.
    /// Unscoped it is the whole library and costs about 130 ms to save 600 KiB. Sending
    /// <c>false</c> for both was #88, and it made a 9,196-rom platform resolve take 8m 15s
    /// against a live instance.
    /// </remarks>
    [Theory]
    [InlineData(CatalogScopeKind.Platform, "true")]
    [InlineData(CatalogScopeKind.Collection, "true")]
    [InlineData(CatalogScopeKind.SmartCollection, "true")]
    [InlineData(CatalogScopeKind.VirtualCollection, "true")]
    [InlineData(CatalogScopeKind.Filter, "false")]
    public void The_rom_id_index_is_on_for_a_scoped_walk_and_off_for_an_unscoped_one(
        CatalogScopeKind scope,
        string expected)
    {
        var built = new CatalogQuery { Scope = scope, ScopeId = "6" }.ToQueryString(limit: 250, offset: 0);

        Assert.Contains($"with_rom_id_index={expected}", built, StringComparison.Ordinal);
    }

    [Fact]
    public void With_total_stays_on_because_it_costs_nothing_and_bounds_the_walk()
    {
        var built = new CatalogQuery { Scope = CatalogScopeKind.Filter }.ToQueryString(limit: 250, offset: 0);

        Assert.Contains("with_total=true", built, StringComparison.Ordinal);
    }

    [Fact]
    public void The_filter_value_sidecar_is_opt_in_and_separate()
    {
        var query = new CatalogQuery { Scope = CatalogScopeKind.Filter };

        Assert.Contains("with_filter_values=false", query.ToQueryString(250, 0), StringComparison.Ordinal);
        Assert.Contains("with_filter_values=true", query.ToQueryString(1, 0, withFilterValues: true), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CatalogScopeKind.Collection, "collection_id=12")]
    [InlineData(CatalogScopeKind.SmartCollection, "smart_collection_id=12")]
    [InlineData(CatalogScopeKind.VirtualCollection, "virtual_collection_id=12")]
    [InlineData(CatalogScopeKind.Platform, "platform_ids=12")]
    public void Each_scope_becomes_its_own_query_parameter(CatalogScopeKind scope, string expected)
    {
        var built = new CatalogQuery { Scope = scope, ScopeId = "12" }.ToQueryString(250, 0);

        Assert.Contains(expected, built, StringComparison.Ordinal);
    }

    [Fact]
    public void Multi_valued_filters_repeat_the_parameter_rather_than_joining_with_commas()
    {
        var query = new CatalogQuery
        {
            Scope = CatalogScopeKind.Filter,
            Filter = new CatalogFilter { Genres = ["Shooter", "Platform"] },
        };

        var built = query.ToQueryString(250, 0);

        Assert.Contains("genres=Shooter", built, StringComparison.Ordinal);
        Assert.Contains("genres=Platform", built, StringComparison.Ordinal);
        Assert.DoesNotContain("Shooter%2CPlatform", built, StringComparison.Ordinal);
    }

    [Fact]
    public void The_walk_is_ordered_by_ascending_id_so_inserts_land_past_the_cursor()
    {
        var built = new CatalogQuery { Scope = CatalogScopeKind.Filter }.ToQueryString(250, 0);

        Assert.Contains("order_by=id", built, StringComparison.Ordinal);
        Assert.Contains("order_dir=asc", built, StringComparison.Ordinal);
    }

    [Fact]
    public void Updated_after_is_sent_in_utc_round_trip_form()
    {
        var query = new CatalogQuery
        {
            Scope = CatalogScopeKind.Filter,
            UpdatedAfter = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.FromHours(2)),
        };

        var built = query.ToQueryString(250, 0);

        Assert.Contains("updated_after=2026-03-04T03%3A06%3A07.0000000%2B00%3A00", built, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_walk_pages_to_the_end_without_holding_more_than_one_page()
    {
        using var stub = Library(1_000);
        using var connection = Connect(stub);

        var pager = new RomPager(connection, Platform(1), pageSize: 250);
        var seen = 0;

        while (!pager.IsComplete)
        {
            var response = await pager.NextAsync(TestContext.Current.CancellationToken);
            Assert.True(response.IsSuccess);
            Assert.True(response.Value!.Items.Count <= 250);
            seen += response.Value.Items.Count;
        }

        Assert.Equal(1_000, seen);
        Assert.Equal(1_000, pager.Total);
        Assert.Equal(4, pager.Pages);
    }

    [Fact]
    public async Task An_interrupted_walk_resumes_at_the_recorded_offset()
    {
        using var stub = Library(1_000);
        using var connection = Connect(stub);

        var first = new RomPager(connection, Platform(1), pageSize: 250);
        await first.NextAsync(TestContext.Current.CancellationToken);
        await first.NextAsync(TestContext.Current.CancellationToken);

        Assert.Equal(500, first.Offset);

        var resumed = new RomPager(connection, Platform(1), pageSize: 250, startOffset: first.Offset);
        var response = await resumed.NextAsync(TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccess);
        Assert.Equal(501, response.Value!.Items[0].Id);
    }

    [Fact]
    public async Task A_failed_page_leaves_the_offset_alone_so_nothing_is_skipped()
    {
        using var stub = Library(1_000);
        using var connection = Connect(stub);

        var pager = new RomPager(connection, Platform(1), pageSize: 250);
        await pager.NextAsync(TestContext.Current.CancellationToken);

        stub.NextRomsStatus = HttpStatusCode.Unauthorized;
        var failed = await pager.NextAsync(TestContext.Current.CancellationToken);

        Assert.True(failed.NeedsRepairing);
        Assert.Equal(250, pager.Offset);

        var recovered = await pager.NextAsync(TestContext.Current.CancellationToken);

        Assert.True(recovered.IsSuccess);
        Assert.Equal(251, recovered.Value!.Items[0].Id);
    }

    [Fact]
    public async Task A_rom_larger_than_two_gigabytes_survives_deserialisation()
    {
        using var stub = new StubRomMServer();

        // 4 GB, the size the generated SimpleRomSchema cannot hold: the pinned schema
        // declares fs_size_bytes as a bare integer, so NSwag emitted an int32.
        stub.Library.Add(new StubRom(1, 1, "ps2", "ps2", "Big Game", "Big Game.iso", "iso", 4_294_967_296L));

        using var connection = Connect(stub);
        var pager = new RomPager(connection, Platform(1), pageSize: 250);

        var response = await pager.NextAsync(TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccess);
        Assert.Equal(4_294_967_296L, response.Value!.Items[0].SizeBytes);
    }

    [Fact]
    public async Task An_unreachable_server_mid_walk_throws_the_transport_error_not_a_cancellation()
    {
        using var stub = Library(1_000);
        using var connection = Connect(stub);

        var pager = new RomPager(connection, Platform(1), pageSize: 250);
        await pager.NextAsync(TestContext.Current.CancellationToken);

        stub.IsReachable = false;

        await Assert.ThrowsAsync<RomMUnreachableException>(
            () => pager.NextAsync(TestContext.Current.CancellationToken));

        // The offset is untouched, so the next run picks up exactly where this one stopped.
        Assert.Equal(250, pager.Offset);
    }

    private static CatalogQuery Platform(int id) => new()
    {
        Scope = CatalogScopeKind.Platform,
        ScopeId = id.ToString(CultureInfo.InvariantCulture),
    };

    private static RomMConnection Connect(StubRomMServer stub) =>
        new(new RomMClientOptions { Origin = Origin, AccessToken = "rmm_test" }, stub);

    private static StubRomMServer Library(int count)
    {
        var stub = new StubRomMServer();
        for (var id = 1; id <= count; id++)
        {
            stub.Library.Add(new StubRom(id, 1, "snes", "snes", $"Game {id:0000}", $"Game {id:0000}.sfc", "sfc", 1024 * id));
        }

        return stub;
    }
}
