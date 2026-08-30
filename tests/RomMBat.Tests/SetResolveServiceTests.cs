using RomM.Client;
using RomM.Client.Catalog;
using RomMBat.Core;
using RomMBat.Core.Sets;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Resolving a set, and what happens when the person watching gives up.
/// </summary>
/// <remarks>
/// <b>Cancellation is the ordinary case here, not a failure path, and that is a measurement
/// rather than an opinion.</b> A platform-scoped resolve of 9,196 roms took 8 minutes 15
/// seconds against a live 5.2.0 instance. Nobody holds a controller for eight minutes, so the
/// resolve screen has a cancel on it, and a cancel that threw away the paging already done
/// would make the feature worse than not having it.
/// <para>
/// So a cancelled walk records its offset exactly as an unreachable server does, and the next
/// resolve continues from there. That is the whole difference between cancelling and losing
/// eight minutes.
/// </para>
/// </remarks>
public sealed class SetResolveServiceTests : IDisposable
{
    private static readonly Uri Origin = new("https://romm.invalid/");
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();
    private readonly InstallSession _session;

    public SetResolveServiceTests()
    {
        var location = Path.Combine(_tree.Root, "emulationstation", ".emulationstation", "es_systems.cfg");
        Directory.CreateDirectory(Path.GetDirectoryName(location)!);
        File.Copy(Fixtures.EsSystemsTemplate, location);

        _session = InstallSession.Open(_tree.Root).Session!;
    }

    public void Dispose()
    {
        _session.Dispose();
        _tree.Dispose();
    }

    [Fact]
    public async Task A_resolve_reports_how_far_through_the_scope_it_is()
    {
        using var stub = Library(600);
        using var connection = Connect(stub);
        var set = Set();

        var seen = new List<SetResolveProgress>();

        await new SetResolveService(_session, connection).ResolveAsync(
            [set],
            new Immediate<SetResolveProgress>(seen.Add),
            TestContext.Current.CancellationToken);

        // Per page, which at 250 a page over 600 rows is three reports. Per row would be a
        // report every few microseconds and per set would be one report at the end, which is
        // the same as none for a screen somebody is watching.
        Assert.Equal(3, seen.Count);
        Assert.Equal(600, seen[^1].Total);
        Assert.Equal(600, seen[^1].Scanned);

        // Monotonic, because a count that goes backwards on screen reads as a fault.
        Assert.Equal(seen.Select(p => p.Scanned).Order(), seen.Select(p => p.Scanned));
        Assert.All(seen, p => Assert.Equal("resume", p.SetName));
    }

    [Fact]
    public void Progress_has_no_fraction_until_the_first_page_says_how_big_the_scope_is()
    {
        // Shown as a bare count rather than a bar until then. A progress bar that sits at zero
        // because nothing has told it the denominator looks identical to one that is stuck.
        Assert.Null(new SetResolveProgress("s", 0, 0, 0).Fraction);
        Assert.Equal(0.5, new SetResolveProgress("s", 50, 100, 50).Fraction);
    }

    [Fact]
    public async Task Cancelling_a_walk_records_where_it_stopped_rather_than_throwing_the_pages_away()
    {
        using var stub = Library(600);
        using var connection = Connect(stub);
        var set = Set();

        using var cancel = new CancellationTokenSource();

        var thrown = await Assert.ThrowsAsync<SetResolveCancelledException>(() =>
            new SetResolveService(_session, connection).ResolveAsync(
                [set],
                // Cancel as soon as the first page lands, which is a person pressing back.
                new Immediate<SetResolveProgress>(_ => cancel.Cancel()),
                cancel.Token));

        var report = Assert.Single(thrown.Reports);

        Assert.Equal(ResolveState.Interrupted, report.State);
        Assert.Equal(250, report.Offset);

        // The cursor is what the next run reads, so this is the assertion that matters: the
        // exception carrying an offset would be no use if the store did not have it too.
        var cursor = _session.Store.Cursors.BeginWalk(SetResolveService.EndpointFor(set), Now);
        Assert.Equal(250, cursor.ResumeOffset);
    }

    [Fact]
    public async Task The_next_resolve_after_a_cancel_continues_instead_of_starting_again()
    {
        using var stub = Library(600);
        using var connection = Connect(stub);
        var set = Set();

        using var cancel = new CancellationTokenSource();

        await Assert.ThrowsAsync<SetResolveCancelledException>(() =>
            new SetResolveService(_session, connection).ResolveAsync(
                [set],
                new Immediate<SetResolveProgress>(_ => cancel.Cancel()),
                cancel.Token));

        var servedBefore = stub.RomPagesServed;

        var reports = await new SetResolveService(_session, connection).ResolveAsync(
            [set],
            progress: null,
            TestContext.Current.CancellationToken);

        var report = Assert.Single(reports);

        Assert.Equal(ResolveState.Resolved, report.State);

        // Two pages, not three. The first page is not fetched again, which is the eight
        // minutes this rule exists to protect.
        Assert.Equal(2, stub.RomPagesServed - servedBefore);
    }

    [Fact]
    public async Task A_cancelled_walk_does_not_retire_membership_because_half_a_walk_proves_nothing()
    {
        using var stub = Library(600);
        using var connection = Connect(stub);
        var set = Set();

        // A completed walk first, so there is a membership that a bad resume could destroy.
        await new SetResolveService(_session, connection).ResolveAsync(
            [set], progress: null, TestContext.Current.CancellationToken);

        var before = _session.Store.SyncSets.MemberTotals(set.Id);
        Assert.Equal(600, before.Games);

        using var cancel = new CancellationTokenSource();

        await Assert.ThrowsAsync<SetResolveCancelledException>(() =>
            new SetResolveService(_session, connection).ResolveAsync(
                [set],
                new Immediate<SetResolveProgress>(_ => cancel.Cancel()),
                cancel.Token));

        // Nothing departed. A segment of a walk is an accumulator, not a statement about what
        // the set holds, and treating one as evidence would make every cancel an eviction.
        Assert.Empty(_session.Store.SyncSets.Members(set.Id, MemberState.Departed));
    }

    [Fact]
    public async Task An_unreachable_server_is_interrupted_and_carries_the_reason()
    {
        using var stub = Library(600);
        stub.IsReachable = false;

        using var connection = Connect(stub);

        var reports = await new SetResolveService(_session, connection).ResolveAsync(
            [Set()], progress: null, TestContext.Current.CancellationToken);

        var report = Assert.Single(reports);

        // Offline is a working state. It is the same outcome a cancel produces, because the
        // recovery is the same: run it again and it continues.
        Assert.Equal(ResolveState.Interrupted, report.State);
        Assert.NotNull(report.Problem);
    }

    [Fact]
    public async Task A_page_that_fails_partway_keeps_what_it_read_and_records_the_offset()
    {
        using var stub = Library(600);
        stub.FailRomsAfterPages = 1;

        using var connection = Connect(stub);
        var set = Set();

        var reports = await new SetResolveService(_session, connection).ResolveAsync(
            [set], progress: null, TestContext.Current.CancellationToken);

        Assert.Equal(ResolveState.Interrupted, Assert.Single(reports).State);

        // The rows the first page found are kept, so resuming carries them rather than
        // re-reading them, and the cursor knows where to pick up.
        var cursor = _session.Store.Cursors.BeginWalk(SetResolveService.EndpointFor(set), Now);
        Assert.Equal(250, cursor.ResumeOffset);
    }

    private SyncSetDefinition Set() =>
        _session.Store.SyncSets.Add(
            new SyncSetDefinition
            {
                Name = "resume",
                Scope = CatalogScopeKind.Platform,
                ScopeValue = "1",
                MaxGames = 5000,
            },
            Now);

    private static RomMConnection Connect(StubRomMServer stub) =>
        new(new RomMClientOptions { Origin = Origin, AccessToken = "rmm_test" }, stub);

    private static StubRomMServer Library(int count)
    {
        var stub = new StubRomMServer();

        for (var id = 1; id <= count; id++)
        {
            stub.Library.Add(new StubRom(id, 1, "snes", "snes", $"Game {id:0000}", $"g{id}.sfc", "sfc", 1024));
        }

        return stub;
    }
}
