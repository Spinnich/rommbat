using RomM.Client;
using RomM.Client.Catalog;
using RomMBat.Core;
using RomMBat.Core.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.Sets;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The sixth scope kind: a set whose definition is the games in it.
/// </summary>
/// <remarks>
/// <b>A hand-picked set is a set.</b> That is the whole claim of this file, and it is why the
/// alternatives were refused: an id list inside a <c>filter</c> scope overloads one column with
/// two meanings, and an unmanaged download outside every set means storing "this orphan is
/// deliberate" and teaching the planner to recognise it, which is a set by another name with
/// none of a set's machinery.
/// </remarks>
public sealed class PickedSetTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();
    private readonly InstallSession _session;

    public PickedSetTests()
    {
        var location = Path.Combine(_tree.Root, "emulationstation", ".emulationstation", "es_systems.cfg");
        Directory.CreateDirectory(Path.GetDirectoryName(location)!);
        File.Copy(Fixtures.EsSystemsTemplate, location);

        _session = InstallSession.Open(_tree.Root).Session!;
        Map(1, "snes");
    }

    public void Dispose()
    {
        _session.Dispose();
        _tree.Dispose();
    }

    // ------------------------------------------------------------------ picking

    [Fact]
    public void The_first_pick_creates_the_set_and_writes_its_member_with_no_resolve()
    {
        var picked = new PickedSetService(_session);

        Assert.Null(picked.Find());

        var outcome = picked.Pick(Row(11, "Chrono Trigger"), Now);

        Assert.False(outcome.IsRefused);
        Assert.Equal(CatalogScopeKind.Picked, outcome.Set.Scope);
        Assert.Equal([11], PickedScopeJson.Parse(outcome.Set.ScopeValue));

        // Written from the browse row in hand. The page already carries every field the
        // membership wants, so nothing was asked of the server and nothing was resolved: a set
        // that had to be resolved before its first game landed would not be one press.
        var member = Assert.Single(_session.Store.SyncSets.Members(outcome.Set.Id));
        Assert.Equal(11, member.RomId);
        Assert.Equal("Chrono Trigger", member.DisplayName);
        Assert.Equal("snes", member.Folder);
        Assert.Equal("sfc", member.FsExtension);
        Assert.Equal(2_048, member.SizeBytes);

        // Stamped as resolved even though nothing was walked, because it is: the membership
        // written here is current as of this moment, and a set reading "last resolved never"
        // while holding exactly the right games would be the sets list telling a lie.
        Assert.Equal(Now, outcome.Set.LastResolvedAt);
        Assert.Contains("1 picked game", outcome.Set.LastResolutionSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void A_second_pick_joins_the_same_set_rather_than_making_another()
    {
        var picked = new PickedSetService(_session);

        picked.Pick(Row(11, "Chrono Trigger"), Now);
        picked.Pick(Row(12, "Super Metroid"), Now);

        var set = Assert.Single(_session.Store.SyncSets.List());

        Assert.Equal([11, 12], PickedScopeJson.Parse(set.ScopeValue));
        Assert.Equal(2, _session.Store.SyncSets.Members(set.Id).Count);
    }

    [Fact]
    public void Picking_a_game_twice_is_one_game()
    {
        var picked = new PickedSetService(_session);

        picked.Pick(Row(11, "Chrono Trigger"), Now);
        var second = picked.Pick(Row(11, "Chrono Trigger"), Now);

        Assert.True(second.AlreadyPicked);
        Assert.Equal([11], picked.Picks());
        Assert.Single(_session.Store.SyncSets.Members(second.Set.Id));
    }

    [Fact]
    public void Unpicking_takes_the_game_out_of_the_definition_and_the_membership()
    {
        var picked = new PickedSetService(_session);

        picked.Pick(Row(11, "Chrono Trigger"), Now);
        picked.Pick(Row(12, "Super Metroid"), Now);

        var set = picked.Unpick(11, Now);

        Assert.NotNull(set);
        Assert.Equal([12], PickedScopeJson.Parse(set.ScopeValue));
        Assert.Equal([12], _session.Store.SyncSets.Members(set.Id).Select(member => member.RomId));
    }

    /// <summary>
    /// A pick that could not become a member says why rather than writing one.
    /// </summary>
    /// <remarks>
    /// The rules a resolve applies, applied here. A pick that wrote a member the sync would then
    /// skip is a press that appears to work and never produces a game.
    /// </remarks>
    [Fact]
    public void A_game_whose_platform_has_no_folder_is_refused_with_the_reason()
    {
        var outcome = new PickedSetService(_session).Pick(
            Row(11, "Unmappable") with { PlatformSlug = "nowhere", PlatformFsSlug = "nowhere" },
            Now);

        Assert.True(outcome.IsRefused);
        Assert.Contains("no RetroBat folder", outcome.Problem, StringComparison.Ordinal);
        Assert.Empty(_session.Store.SyncSets.Members(outcome.Set.Id));
    }

    [Fact]
    public void A_multi_file_game_is_refused_as_multi_file_rather_than_as_a_format()
    {
        var outcome = new PickedSetService(_session).Pick(
            Row(11, "Two Discs") with { HasMultipleFiles = true },
            Now);

        Assert.True(outcome.IsRefused);
        Assert.Contains("several files", outcome.Problem, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ it is an ordinary set

    /// <summary>
    /// The planner never calls a picked game orphaned, which is the whole reason this is a set.
    /// </summary>
    /// <remarks>
    /// An unmanaged download outside every set would be the alternative shape, and this is the
    /// test that would fail under it: nothing would claim the game, so the first budget-driven
    /// eviction would take a game the user asked for by name.
    /// </remarks>
    [Fact]
    public void Eviction_never_calls_a_picked_game_orphaned()
    {
        var picked = new PickedSetService(_session);
        var outcome = picked.Pick(Row(11, "Chrono Trigger"), Now);

        SeedFile(11, "snes", "chrono.sfc", 2_048);

        var plan = new EvictionPlanner(_session.Store).Plan(bytesToFree: long.MaxValue);
        var candidate = Assert.Single(plan.Selected);

        Assert.NotEqual(EvictionReason.Orphaned, candidate.Reason);
        Assert.Equal(outcome.Set.Name, candidate.SetName);
    }

    [Fact]
    public void The_picked_scope_is_offered_and_not_pickable_with_the_reason_on_the_row()
    {
        var option = Assert.Single(
            new SyncSetService(_session).Scopes(),
            scope => scope.Kind == CatalogScopeKind.Picked);

        // Offered rather than hidden, because a user who has one wants to know what kind of
        // thing it is; not pickable, because there is nothing here for a value picker to list.
        Assert.False(option.Available);
        Assert.NotNull(option.Unavailable);
    }

    /// <summary>
    /// The pager refuses a picked scope rather than paging the library.
    /// </summary>
    /// <remarks>
    /// <c>GET /api/roms</c> takes no id-list parameter, so every scoping parameter is omitted
    /// for this kind and falling through would build a query that matches everything. A picked
    /// scope reaching the pager is a bug, and it fails where it happened.
    /// </remarks>
    [Fact]
    public void A_picked_scope_refuses_to_become_a_query()
    {
        var query = new CatalogQuery { Scope = CatalogScopeKind.Picked };

        var thrown = Assert.Throws<InvalidOperationException>(() => query.ToQueryString(50, 0));

        Assert.Contains("no id-list parameter", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_scope_kind_survives_the_store_in_both_directions()
    {
        Assert.Equal("picked", SyncSetStore.ScopeText(CatalogScopeKind.Picked));
        Assert.Equal(CatalogScopeKind.Picked, SyncSetStore.ParseScope("picked"));
    }

    /// <summary>A picked set has no filter, and that is verified rather than assumed.</summary>
    [Fact]
    public void A_picked_sets_filter_is_empty_rather_than_its_id_list_misread()
    {
        var set = new PickedSetService(_session).Pick(Row(11, "Chrono Trigger"), Now).Set;

        Assert.True(SyncSetService.FilterOf(set).IsEmpty);
    }

    // ------------------------------------------------------------------ roaming

    /// <summary>
    /// The ids go into <c>sync_config</c> and come back out, with no change to that document.
    /// </summary>
    /// <remarks>
    /// <c>RoamingSyncConfig</c> carries <c>scope_value</c> verbatim, which is why a picked set
    /// needed nothing added to it. The hydrate half is tested against a stubbed
    /// <c>GET /api/roms/{id}</c> below.
    /// </remarks>
    [Fact]
    public void A_picked_set_roams_as_an_id_array_and_comes_back()
    {
        var picked = new PickedSetService(_session);
        picked.Pick(Row(11, "Chrono Trigger"), Now);
        picked.Pick(Row(12, "Super Metroid"), Now);

        var document = RoamingSyncConfig.FromStore(_session.Store, Now);
        var roamed = Assert.Single(document.Sets);

        Assert.Equal("picked", roamed.Scope);
        Assert.Equal([11, 12], PickedScopeJson.Parse(roamed.ScopeValue));

        var back = RoamingSyncConfig.ToDefinition(roamed, Now);

        Assert.Equal(CatalogScopeKind.Picked, back.Scope);
        Assert.Equal([11, 12], PickedScopeJson.Parse(back.ScopeValue));
    }

    [Fact]
    public async Task A_roamed_picked_set_hydrates_by_fetching_each_id()
    {
        using var stub = new StubRomMServer();
        stub.Library.Add(new StubRom(11, 1, "snes", "snes", "Chrono Trigger", "chrono.sfc", "sfc", 2_048));
        stub.Library.Add(new StubRom(12, 1, "snes", "snes", "Super Metroid", "metroid.sfc", "sfc", 4_096));

        // As it arrives on a second device: ids in the definition, no membership behind them.
        var set = _session.Store.SyncSets.Add(
            new SyncSetDefinition
            {
                Name = "Picked on somewhere else",
                Scope = CatalogScopeKind.Picked,
                ScopeValue = PickedScopeJson.Write([11, 12, 99]),
            },
            Now);

        Assert.Empty(_session.Store.SyncSets.Members(set.Id));

        using var connection = new RomMConnection(
            new RomMClientOptions { Origin = new Uri("https://romm.invalid/"), AccessToken = "rmm_test" },
            stub);

        var reports = await new SetResolveService(_session, connection)
            .ResolveAsync([set], progress: null, TestContext.Current.CancellationToken);

        var report = Assert.Single(reports);

        Assert.Equal(ResolveState.Resolved, report.State);

        // 99 is not in the library. A game picked on another device and since deleted in RomM
        // is drift a re-resolve exists to notice, and one missing game must not cost the others.
        Assert.Contains("no longer in RomM", report.Summary, StringComparison.Ordinal);
        Assert.Equal([11, 12], _session.Store.SyncSets.Members(set.Id).Select(member => member.RomId).Order());
    }

    // ------------------------------------------------------------------ seeding

    private static RomRow Row(int id, string name) => new()
    {
        Id = id,
        PlatformId = 1,
        PlatformSlug = "snes",
        PlatformFsSlug = "snes",
        FsName = $"{name}.sfc",
        FsExtension = "sfc",
        SizeBytes = 2_048,
        Name = name,
    };

    private void Map(int platformId, string folder) =>
        _session.Store.PlatformMap.Record(
            new RomMBat.Core.Mapping.PlatformResolver(
                Fixtures.LoadEsSystems(),
                new Dictionary<string, string>())
                .Resolve(new RomMBat.Core.Mapping.RomMPlatform(platformId, folder, folder, folder)),
            Now);

    private void SeedFile(int romId, string folder, string fileName, long bytes)
    {
        var absolute = Path.Combine(_tree.Root, "roms", folder, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllBytes(absolute, new byte[bytes]);

        _session.Store.Files.Record(new LocalFile
        {
            Path = RelativePath.Create($"roms/{folder}/{fileName}"),
            Folder = folder,
            RomId = romId,
            Kind = LocalFileKind.Rom,
            FileName = fileName,
            SizeBytes = bytes,
            Origin = FileOrigin.Synced,
        });
    }
}
