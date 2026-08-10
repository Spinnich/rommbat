using System.Globalization;
using RomM.Client;
using RomM.Client.Catalog;
using RomMBat.Core.Mapping;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Resolving a sync set: the caps, the ordering, the exclusions, and the store being able to
/// answer with the server switched off.
/// </summary>
/// <remarks>
/// The milestone's done-when is "my SNES favourites, max 40 games, 8 GB" resolving to an
/// exact list without the client ever holding the library, so that is the shape of most of
/// these.
/// </remarks>
public class SyncSetTests : IDisposable
{
    private static readonly Uri Origin = new("https://romm.invalid/");
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);

    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();
    private readonly LocalStore _store;

    public SyncSetTests() => _store = LocalStore.Open(_tree.Install());

    public void Dispose()
    {
        _store.Dispose();
        _tree.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void The_schema_is_at_the_version_this_build_expects()
    {
        Assert.Equal(2, LocalStore.ExpectedSchemaVersion);
        Assert.Equal(2, _store.SchemaVersion);
    }

    [Fact]
    public async Task Forty_snes_games_under_eight_gigabytes_resolve_to_an_exact_list()
    {
        using var stub = SnesLibrary(500);
        using var connection = Connect(stub);

        var set = Add(new SyncSetDefinition
        {
            Name = "My SNES favourites",
            Scope = CatalogScopeKind.Platform,
            ScopeValue = "1",
            MaxGames = 40,
            MaxBytes = 8L * 1024 * 1024 * 1024,
        });

        var resolution = await Resolve(set, connection);

        Assert.Equal(ResolutionOutcome.Resolved, resolution.Outcome);
        Assert.Equal(40, resolution.Members.Count);
        Assert.Equal(500, resolution.ScopeTotal);
        Assert.Equal(460, resolution.OverCount);

        // Ordered by name, so the first forty by sort key and nothing else.
        Assert.Equal("Game 0001", resolution.Members[0].DisplayName);
        Assert.Equal("Game 0040", resolution.Members[^1].DisplayName);
        Assert.Equal([.. Enumerable.Range(1, 40)], resolution.Members.Select(member => member.Position));
    }

    [Fact]
    public async Task The_resolved_list_reads_back_with_the_server_switched_off()
    {
        using var stub = SnesLibrary(100);
        using var connection = Connect(stub);

        var set = Add(new SyncSetDefinition
        {
            Name = "snes",
            Scope = CatalogScopeKind.Platform,
            ScopeValue = "1",
            MaxGames = 10,
        });

        var resolution = await Resolve(set, connection);
        _store.SyncSets.ReplaceMembers(set.Id, [.. resolution.Members, .. resolution.Excluded], resolution.Summary, Now);

        stub.IsReachable = false;

        var stored = _store.SyncSets.Members(set.Id);
        var (games, bytes) = _store.SyncSets.MemberTotals(set.Id);

        Assert.Equal(10, stored.Count);
        Assert.Equal(10, games);
        Assert.Equal(resolution.Bytes, bytes);
        Assert.Equal("Game 0001", stored[0].DisplayName);
        Assert.Equal("snes", stored[0].Folder);
        Assert.Equal("Game 0001.sfc", stored[0].FsName);
    }

    [Fact]
    public async Task A_byte_budget_keeps_the_best_ordered_subset_that_fits()
    {
        using var stub = new StubRomMServer();
        stub.Library.Add(new StubRom(1, 1, "snes", "snes", "A", "A.sfc", "sfc", 9_000));
        stub.Library.Add(new StubRom(2, 1, "snes", "snes", "B", "B.sfc", "sfc", 2_000));
        stub.Library.Add(new StubRom(3, 1, "snes", "snes", "C", "C.sfc", "sfc", 1_000));

        using var connection = Connect(stub);
        var set = Add(new SyncSetDefinition
        {
            Name = "budget",
            Scope = CatalogScopeKind.Platform,
            ScopeValue = "1",
            MaxBytes = 10_000,
        });

        var resolution = await Resolve(set, connection);

        // Greedy, not a knapsack: A wins on name order, B does not fit alongside it, C does.
        Assert.Equal(["A", "C"], resolution.Members.Select(member => member.DisplayName));
        Assert.Equal(10_000, resolution.Bytes);
        Assert.Equal(1, resolution.OverBytes);
    }

    [Fact]
    public async Task Ordering_by_size_ascending_fits_more_games_in_the_same_budget()
    {
        using var stub = new StubRomMServer();
        stub.Library.Add(new StubRom(1, 1, "snes", "snes", "A", "A.sfc", "sfc", 9_000));
        stub.Library.Add(new StubRom(2, 1, "snes", "snes", "B", "B.sfc", "sfc", 2_000));
        stub.Library.Add(new StubRom(3, 1, "snes", "snes", "C", "C.sfc", "sfc", 1_000));

        using var connection = Connect(stub);
        var set = Add(new SyncSetDefinition
        {
            Name = "smallest first",
            Scope = CatalogScopeKind.Platform,
            ScopeValue = "1",
            MaxBytes = 10_000,
            Ordering = SetOrdering.SizeAscending,
        });

        var resolution = await Resolve(set, connection);

        Assert.Equal(["C", "B"], resolution.Members.Select(member => member.DisplayName));
    }

    [Fact]
    public async Task The_result_does_not_depend_on_the_order_pages_arrive_in()
    {
        using var forward = new StubRomMServer();
        using var reverse = new StubRomMServer();

        var roms = new[]
        {
            new StubRom(1, 1, "snes", "snes", "Zelda", "Zelda.sfc", "sfc", 4_000),
            new StubRom(2, 1, "snes", "snes", "Actraiser", "Actraiser.sfc", "sfc", 3_000),
            new StubRom(3, 1, "snes", "snes", "Mario", "Mario.sfc", "sfc", 5_000),
        };

        foreach (var rom in roms)
        {
            forward.Library.Add(rom);
        }

        foreach (var rom in roms.Reverse())
        {
            reverse.Library.Add(rom);
        }

        var set = Add(new SyncSetDefinition
        {
            Name = "two",
            Scope = CatalogScopeKind.Platform,
            ScopeValue = "1",
            MaxGames = 2,
        });

        using var forwardConnection = Connect(forward);
        using var reverseConnection = Connect(reverse);

        var first = await Resolve(set, forwardConnection, pageSize: 1);
        var second = await Resolve(set, reverseConnection, pageSize: 1);

        Assert.Equal(["Actraiser", "Mario"], first.Members.Select(member => member.DisplayName));
        Assert.Equal(first.Members.Select(m => m.RomId), second.Members.Select(m => m.RomId));
    }

    [Fact]
    public async Task A_format_the_system_cannot_launch_is_excluded_and_counted()
    {
        using var stub = new StubRomMServer();
        stub.Library.Add(new StubRom(1, 1, "snes", "snes", "Good", "Good.sfc", "sfc", 1_000));
        stub.Library.Add(new StubRom(2, 1, "snes", "snes", "Disc", "Disc.chd", "chd", 1_000));
        stub.Library.Add(new StubRom(3, 1, "snes", "snes", "Also disc", "Also disc.chd", "chd", 1_000));

        using var connection = Connect(stub);
        var set = Add(new SyncSetDefinition { Name = "snes", Scope = CatalogScopeKind.Platform, ScopeValue = "1" });

        var resolution = await Resolve(set, connection);

        Assert.Single(resolution.Members);
        Assert.Equal(2, resolution.ExcludedExtensions["chd"]);
        Assert.Contains("format not supported by this system", resolution.Summary, StringComparison.Ordinal);
        Assert.Contains(".chd", resolution.Summary, StringComparison.Ordinal);

        // Exclusions are stored, so the reason survives to the next offline run.
        _store.SyncSets.ReplaceMembers(set.Id, [.. resolution.Members, .. resolution.Excluded], resolution.Summary, Now);
        var exclusions = _store.SyncSets.Exclusions(set.Id);

        Assert.Single(exclusions);
        Assert.Equal(MemberState.ExcludedExtension, exclusions[0].State);
        Assert.Equal(2, exclusions[0].Count);
        Assert.Equal(["chd"], exclusions[0].Extensions);
    }

    [Fact]
    public async Task A_rom_with_no_extension_is_excluded_and_reported_as_such()
    {
        using var stub = new StubRomMServer();
        stub.Library.Add(new StubRom(1, 1, "snes", "snes", "Nameless", "Nameless", string.Empty, 1_000));

        using var connection = Connect(stub);
        var set = Add(new SyncSetDefinition { Name = "snes", Scope = CatalogScopeKind.Platform, ScopeValue = "1" });

        var resolution = await Resolve(set, connection);

        // A real instance had 23 of these on one platform. Reporting the format as a bare dot
        // would read as a bug in RomMBat rather than a gap in the library.
        Assert.Empty(resolution.Members);
        Assert.Equal(1, resolution.ExcludedExtensions[SetResolver.NoExtension]);
        Assert.Contains("no extension", resolution.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("(.)", resolution.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_platform_with_no_folder_is_excluded_and_named()
    {
        using var stub = new StubRomMServer();
        stub.Library.Add(new StubRom(1, 9, "some-console-nobody-has", "nope", "Orphan", "Orphan.bin", "bin", 1_000));

        using var connection = Connect(stub);
        var set = Add(new SyncSetDefinition { Name = "orphans", Scope = CatalogScopeKind.Platform, ScopeValue = "9" });

        var resolution = await Resolve(set, connection);

        Assert.Empty(resolution.Members);
        Assert.Equal(1, resolution.UnmappedPlatforms["some-console-nobody-has"]);
        Assert.Contains("no RetroBat folder", resolution.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_arcade_set_refuses_to_guess_and_says_what_to_choose_from()
    {
        using var stub = new StubRomMServer();
        stub.Library.Add(new StubRom(1, 2, "arcade", "arcade", "Some Arcade Game", "sag.zip", "zip", 1_000));

        using var connection = Connect(stub);
        var set = Add(new SyncSetDefinition { Name = "arcade", Scope = CatalogScopeKind.Platform, ScopeValue = "2" });

        var resolution = await Resolve(set, connection);

        Assert.Equal(ResolutionOutcome.NeedsFolderChoice, resolution.Outcome);
        Assert.Contains("mame", resolution.Problem!, StringComparison.Ordinal);
        Assert.Empty(resolution.Members);
    }

    [Fact]
    public async Task An_arcade_set_resolves_once_the_folder_is_chosen_for_that_set()
    {
        using var stub = new StubRomMServer();
        stub.Library.Add(new StubRom(1, 2, "arcade", "arcade", "Some Arcade Game", "sag.zip", "zip", 1_000));

        using var connection = Connect(stub);
        var set = Add(new SyncSetDefinition
        {
            Name = "arcade",
            Scope = CatalogScopeKind.Platform,
            ScopeValue = "2",
            FolderOverride = "fbneo",
        });

        var resolution = await Resolve(set, connection);

        Assert.Equal(ResolutionOutcome.Resolved, resolution.Outcome);
        Assert.Equal("fbneo", resolution.Members[0].Folder);
    }

    [Fact]
    public async Task An_uncapped_scope_that_would_hold_the_library_is_refused()
    {
        using var stub = new StubRomMServer { TotalOverride = SetResolver.UncappedScopeLimit + 1 };
        stub.Library.Add(new StubRom(1, 1, "snes", "snes", "One", "One.sfc", "sfc", 1));

        using var connection = Connect(stub);
        var set = Add(new SyncSetDefinition { Name = "everything", Scope = CatalogScopeKind.Filter, ScopeValue = "{}" });

        var resolution = await Resolve(set, connection);

        Assert.Equal(ResolutionOutcome.Refused, resolution.Outcome);
        Assert.Contains("no game or size cap", resolution.Problem!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_member_that_left_the_scope_becomes_a_departure_rather_than_a_deletion()
    {
        using var stub = SnesLibrary(3);
        using var connection = Connect(stub);

        var set = Add(new SyncSetDefinition { Name = "drifting", Scope = CatalogScopeKind.Platform, ScopeValue = "1" });

        var first = await Resolve(set, connection);
        _store.SyncSets.ReplaceMembers(set.Id, [.. first.Members], first.Summary, Now);

        // The scope shrinks server-side, which is what a smart collection does on its own.
        stub.Library.RemoveAt(2);

        var second = await Resolve(set, connection);
        _store.SyncSets.ReplaceMembers(set.Id, [.. second.Members], second.Summary, Now.AddMinutes(1));

        Assert.Equal(2, _store.SyncSets.Members(set.Id).Count);

        var departed = _store.SyncSets.Members(set.Id, MemberState.Departed);
        Assert.Single(departed);
        Assert.Equal("Game 0003", departed[0].DisplayName);
        Assert.Null(departed[0].Position);
    }

    [Fact]
    public async Task An_interrupted_walk_records_where_to_resume()
    {
        using var stub = SnesLibrary(10);
        using var connection = Connect(stub);

        var set = Add(new SyncSetDefinition { Name = "snes", Scope = CatalogScopeKind.Platform, ScopeValue = "1" });
        var endpoint = $"roms:set:{set.Id}";

        _store.Cursors.BeginWalk(endpoint, Now);

        var pager = new RomPager(connection, SetResolver.QueryFor(set), pageSize: 4);
        await pager.NextAsync();
        _store.Cursors.RecordProgress(endpoint, pager.Offset, pager.Total, Now);

        var cursor = _store.Cursors.Read(endpoint);

        Assert.NotNull(cursor);
        Assert.True(cursor.HasWalkInFlight);
        Assert.Equal(4, cursor.ResumeOffset);
        Assert.Equal(10, cursor.WalkTotal);
        Assert.Null(cursor.UpdatedAfter);

        // Resuming continues from the recorded offset rather than restarting.
        var resumed = _store.Cursors.BeginWalk(endpoint, Now.AddMinutes(1));
        Assert.Equal(4, resumed.ResumeOffset);
    }

    [Fact]
    public void Completing_a_walk_advances_the_cursor_to_when_the_walk_started()
    {
        _store.Cursors.BeginWalk("roms", Now);
        _store.Cursors.CompleteWalk("roms", Now.AddMinutes(14));

        var cursor = _store.Cursors.Read("roms");

        Assert.NotNull(cursor);
        Assert.False(cursor.HasWalkInFlight);

        // The start, not the finish. Anything changed during those fourteen minutes is
        // picked up next time instead of falling into the gap.
        Assert.Equal(Now, cursor.UpdatedAfter);
    }

    [Fact]
    public void A_set_name_can_only_be_used_once()
    {
        Add(new SyncSetDefinition { Name = "snes", Scope = CatalogScopeKind.Platform, ScopeValue = "1" });

        Assert.Throws<SyncSetExistsException>(() =>
            Add(new SyncSetDefinition { Name = "snes", Scope = CatalogScopeKind.Platform, ScopeValue = "2" }));
    }

    [Fact]
    public void Removing_a_set_takes_its_membership_with_it()
    {
        var set = Add(new SyncSetDefinition { Name = "snes", Scope = CatalogScopeKind.Platform, ScopeValue = "1" });

        _store.SyncSets.ReplaceMembers(
            set.Id,
            [
                new SyncSetMember
                {
                    RomId = 1,
                    State = MemberState.Member,
                    Folder = "snes",
                    PlatformSlug = "snes",
                    FsName = "Game.sfc",
                    DisplayName = "Game",
                    SortKey = "Game",
                    Position = 1,
                },
            ],
            "1 game",
            Now);

        Assert.True(_store.SyncSets.Remove("snes"));
        Assert.Empty(_store.SyncSets.List());
    }

    private SyncSetDefinition Add(SyncSetDefinition definition) => _store.SyncSets.Add(definition, Now);

    private async Task<SetResolution> Resolve(SyncSetDefinition set, RomMConnection connection, int pageSize = 250)
    {
        var install = Fixtures.LoadEsSystems();
        var resolver = new SetResolver(install, new PlatformResolver(install, _store.PlatformMap.Overrides()));

        return await resolver.ResolveAsync(set, new RomPager(connection, SetResolver.QueryFor(set), pageSize), Now);
    }

    private static RomMConnection Connect(StubRomMServer stub) =>
        new(new RomMClientOptions { Origin = Origin, AccessToken = "rmm_test" }, stub);

    private static StubRomMServer SnesLibrary(int count)
    {
        var stub = new StubRomMServer();
        for (var id = 1; id <= count; id++)
        {
            stub.Library.Add(new StubRom(
                id,
                1,
                "snes",
                "snes",
                $"Game {id:0000}",
                $"Game {id:0000}.sfc",
                "sfc",
                1024L * 1024 * id));
        }

        return stub;
    }
}
