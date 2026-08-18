using System.Text.Json;
using RomM.Client;
using RomM.Client.Catalog;
using RomMBat.Core.Identity;
using RomMBat.Core.Mapping;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The catalog reads against a real RomM, driven headlessly.
/// </summary>
/// <remarks>
/// Skipped unless <c>ROMMBAT_TEST_SERVER</c> and <c>ROMMBAT_TEST_APPROVER_TOKEN</c> are both
/// set, so a clone with no server still runs green. Nothing here names an instance.
/// <para>
/// A device token is needed rather than the approver's, because the approver holds only
/// <c>me.read</c> and <c>me.write</c> and cannot read a library. So each test pairs, works,
/// and deletes the device it created, the same way <see cref="LivePairingTests"/> does.
/// </para>
/// <para>
/// Everything here is a read except the <c>sync_config</c> round trip, which writes to the
/// throwaway device this class created and to nothing else.
/// </para>
/// <para>
/// <b>One pairing for the whole class</b>, shared through a fixture.
/// <c>POST /api/auth/device/init</c> is rate limited to 10 per minute per IP, and a test
/// per pairing puts the live suite over that limit on its own.
/// </para>
/// </remarks>
public class LiveCatalogTests(LiveCatalogFixture fixture) : IClassFixture<LiveCatalogFixture>
{
    private const string NotConfigured =
        "Set ROMMBAT_TEST_SERVER and ROMMBAT_TEST_APPROVER_TOKEN to run the live tests.";

    private static bool IsConfigured => LiveCatalogFixture.IsConfigured;

    [Fact]
    public async Task A_page_of_roms_arrives_without_the_sidecars()
    {
        Assert.SkipUnless(IsConfigured, NotConfigured);

        var session = fixture.Session;
        var query = new CatalogQuery { Scope = CatalogScopeKind.Filter };

        var response = await session.Connection.GetRomPageAsync(query, limit: 5, offset: 0);

        Assert.SkipWhen(response.Status == RomMResponseStatus.Forbidden, "This account cannot read the library.");
        Assert.True(response.IsSuccess, response.Message);

        // The three costly sidecars are absent from the body, not merely unread. Checked on
        // the raw JSON because the slim row would drop them silently either way.
        using var raw = await session.RawAsync("api/roms?" + query.ToQueryString(limit: 5, offset: 0));

        Assert.False(raw.RootElement.TryGetProperty("rom_id_index", out var index) && index.GetArrayLength() > 0);
        Assert.False(raw.RootElement.TryGetProperty("char_index", out var chars) && chars.EnumerateObject().Any());
        Assert.True(raw.RootElement.TryGetProperty("total", out _), "with_total should still be on.");
    }

    [Fact]
    public async Task Order_by_id_is_accepted_and_pages_do_not_overlap()
    {
        Assert.SkipUnless(IsConfigured, NotConfigured);

        var session = fixture.Session;

        var pager = new RomPager(session.Connection, new CatalogQuery { Scope = CatalogScopeKind.Filter }, pageSize: 25);

        var first = await pager.NextAsync();
        Assert.SkipWhen(first.Status == RomMResponseStatus.Forbidden, "This account cannot read the library.");
        Assert.True(first.IsSuccess, first.Message);
        Assert.SkipWhen(first.Value!.Total < 30, "The library is too small to page.");

        var second = await pager.NextAsync();
        Assert.True(second.IsSuccess, second.Message);

        var firstIds = first.Value!.Items.Select(row => row.Id).ToList();
        var secondIds = second.Value!.Items.Select(row => row.Id).ToList();

        Assert.Empty(firstIds.Intersect(secondIds));
        Assert.Equal(firstIds.Order(), firstIds);
        Assert.True(secondIds[0] > firstIds[^1], "Ascending id order should make page two start after page one.");
    }

    [Fact]
    public async Task Real_platforms_resolve_through_the_chain_and_record_where_each_answer_came_from()
    {
        Assert.SkipUnless(IsConfigured, NotConfigured);

        var session = fixture.Session;

        var response = await session.Connection.ListPlatformsAsync();
        Assert.SkipWhen(response.Status == RomMResponseStatus.Forbidden, "This account cannot read platforms.");
        Assert.True(response.IsSuccess, response.Message);
        Assert.SkipWhen(response.Value!.Count == 0, "The instance has no platforms.");

        var install = Fixtures.LoadEsSystems();
        var resolver = new PlatformResolver(install);
        var now = DateTimeOffset.UtcNow;

        foreach (var platform in response.Value)
        {
            var resolution = resolver.Resolve(new RomMPlatform(
                platform.Id,
                platform.Slug,
                platform.FsSlug,
                platform.Label,
                platform.RomCount));

            session.Store.PlatformMap.Record(resolution, now);

            // An applied folder must exist. A suggestion must not be applied. Both are the
            // invariants that keep games out of folders EmulationStation never scans.
            if (resolution.IsApplied)
            {
                Assert.True(install.HasFolder(resolution.Folder), $"{platform.Slug} resolved to a missing folder.");
            }

            if (resolution.ResolvedBy == MappingSource.Normalized)
            {
                Assert.Null(resolution.Folder);
                Assert.NotNull(resolution.Suggestion);
            }
        }

        // Resolved rows only. This class shares one store, and a sibling test writes a user
        // override into the same table, so the unfiltered count passes or fails on test order.
        // The resolver here is built with no overrides, so nothing it produces can be a user
        // row and this stays "every platform the server returned made exactly one row, and
        // nothing else resolved".
        var rows = session.Store.PlatformMap.List()
            .Where(row => row.ResolvedBy != MappingSource.User)
            .ToList();

        Assert.Equal(response.Value.Count, rows.Count);
        Assert.All(rows, row => Assert.NotNull(row.Explanation));
    }

    [Fact]
    public async Task Sync_config_round_trips_and_keeps_keys_this_client_does_not_own()
    {
        Assert.SkipUnless(IsConfigured, NotConfigured);

        var session = fixture.Session;

        // This class shares one store, because POST /api/auth/device/init is rate limited to 10
        // per minute and pairing per test would exceed it alone. So a sibling test's sync set is
        // in here too, and Assert.Single below is only about the round trip if this test owns its
        // own input. Clearing rather than asserting on the named set: the count is what notices
        // the document gaining a set it should not have.
        foreach (var existing in session.Store.SyncSets.List())
        {
            session.Store.SyncSets.Remove(existing.Name);
        }

        session.Store.SyncSets.Add(
            new SyncSetDefinition
            {
                Name = "live round trip",
                Scope = CatalogScopeKind.Platform,
                ScopeValue = "1",
                MaxGames = 40,
                MaxBytes = 8L * 1024 * 1024 * 1024,
            },
            DateTimeOffset.UtcNow);

        session.Store.PlatformMap.SetOverride("arcade", "fbneo", DateTimeOffset.UtcNow);

        // Something another client might have left behind, which we must not delete.
        var foreign = new Dictionary<string, object?>
        {
            ["someone_else"] = new Dictionary<string, string> { ["keep"] = "me" },
        };

        var seeded = await session.Connection.UpdateDeviceSyncConfigAsync(session.DeviceId, foreign);
        Assert.SkipWhen(seeded.Status == RomMResponseStatus.Forbidden, "This token cannot write devices.");
        Assert.True(seeded.IsSuccess, seeded.Message);

        var before = await session.Connection.GetDeviceAsync(session.DeviceId);
        Assert.True(before.IsSuccess, before.Message);

        var document = RoamingSyncConfig.FromStore(session.Store, DateTimeOffset.UtcNow);
        var pushed = await session.Connection.UpdateDeviceSyncConfigAsync(
            session.DeviceId,
            document.MergeInto(before.Value!.Sync_config));

        Assert.True(pushed.IsSuccess, pushed.Message);

        var after = await session.Connection.GetDeviceAsync(session.DeviceId);
        Assert.True(after.IsSuccess, after.Message);

        var recovered = RoamingSyncConfig.Extract(after.Value!.Sync_config);

        Assert.NotNull(recovered);
        Assert.Single(recovered.Sets);
        Assert.Equal("live round trip", recovered.Sets[0].Name);
        Assert.Equal(40, recovered.Sets[0].MaxGames);
        Assert.Equal("fbneo", recovered.PlatformOverrides["arcade"]);

        // The other client's key survived the write, which is the whole point of merging.
        Assert.True(after.Value.Sync_config is JsonElement element
            && element.TryGetProperty("someone_else", out _));
    }

    [Fact]
    public async Task A_set_scoped_to_a_real_platform_resolves_to_an_exact_list()
    {
        Assert.SkipUnless(IsConfigured, NotConfigured);

        var session = fixture.Session;

        var platforms = await session.Connection.ListPlatformsAsync();
        Assert.SkipWhen(platforms.Status == RomMResponseStatus.Forbidden, "This account cannot read platforms.");
        Assert.True(platforms.IsSuccess, platforms.Message);

        var install = Fixtures.LoadEsSystems();
        var resolver = new PlatformResolver(install);

        var candidate = platforms.Value!
            .Where(platform => platform.RomCount > 0)
            .FirstOrDefault(platform => resolver
                .Resolve(new RomMPlatform(platform.Id, platform.Slug, platform.FsSlug, platform.Label))
                .IsApplied);

        Assert.SkipWhen(candidate is null, "No platform on this instance both has ROMs and maps to a folder.");

        var set = session.Store.SyncSets.Add(
            new SyncSetDefinition
            {
                Name = "live platform set",
                Scope = CatalogScopeKind.Platform,
                ScopeValue = candidate!.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                MaxGames = 5,
            },
            DateTimeOffset.UtcNow);

        var setResolver = new SetResolver(install, resolver);
        var pager = new RomPager(session.Connection, SetResolver.QueryFor(set), pageSize: 250);
        var resolution = await setResolver.ResolveAsync(set, pager, DateTimeOffset.UtcNow);

        Assert.Equal(ResolutionOutcome.Resolved, resolution.Outcome);
        Assert.True(resolution.Members.Count <= 5);
        Assert.All(resolution.Members, member => Assert.True(install.HasFolder(member.Folder)));

        session.Store.SyncSets.ReplaceMembers(
            set.Id,
            [.. resolution.Members, .. resolution.Excluded],
            resolution.Summary,
            DateTimeOffset.UtcNow);

        // The point of storing all that: the same answer with the server switched off.
        var stored = session.Store.SyncSets.Members(set.Id);
        Assert.Equal(resolution.Members.Count, stored.Count);
    }
}
