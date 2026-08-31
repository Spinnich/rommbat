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
    public void Progress_through_a_resumed_walk_counts_from_where_it_resumed()
    {
        // The segment's own count starts at zero on a resume, because it has folded in nothing
        // yet, and reporting that sent the bar back to the start while the work already done
        // was real and recorded. Offset is what the cursor keeps and what a person means.
        var resumed = new SetResolveProgress("s", Scanned: 50, Total: 600, Offset: 550);

        Assert.Equal(550d / 600d, resumed.Fraction);
        Assert.NotEqual(50d / 600d, resumed.Fraction);
    }

    [Fact]
    public async Task A_resumed_walk_reports_progress_that_carries_on_rather_than_restarting()
    {
        using var stub = Library(600);
        using var connection = Connect(stub);
        var set = Set();

        using var cancel = new CancellationTokenSource();

        await Assert.ThrowsAsync<SetResolveCancelledException>(() =>
            new SetResolveService(_session, connection).ResolveAsync(
                [set],
                new Immediate<SetResolveProgress>(p =>
                {
                    if (p.Offset >= 250)
                    {
                        cancel.Cancel();
                    }
                }),
                cancel.Token));

        var seen = new List<SetResolveProgress>();

        await new SetResolveService(_session, connection).ResolveAsync(
            [set],
            new Immediate<SetResolveProgress>(seen.Add),
            TestContext.Current.CancellationToken);

        // What this covers, and what it does not. The offset was always right, so this asserts
        // the walk genuinely resumed rather than re-read from the start; the fix itself is that
        // Fraction and the displayed count are derived from the offset, which the record-level
        // test above pins exactly.
        Assert.NotEmpty(seen);
        Assert.True(
            seen[0].Offset >= 250,
            $"the resumed walk reported offset {seen[0].Offset}, so it did not resume");

        Assert.All(seen, p => Assert.True(p.Fraction >= 250d / 600d));

        // Monotonic across the resume, which is the property a person actually watches.
        Assert.Equal(seen.Select(p => p.Offset).Order(), seen.Select(p => p.Offset));
    }

    [Fact]
    public async Task A_walk_the_server_drops_keeps_the_membership_that_segment_found()
    {
        // #104. The three ways a walk can stop were not treated alike: a cancel and an HTTP
        // failure both break out of the page loop and arrive at Record with the accumulator
        // intact, and only RomMUnreachableException unwound the stack, taking the games found
        // so far with the frame. The offset still advanced, so the next walk resumed at the
        // right page with nothing carried and completed short, retiring everything before it.
        //
        // On a handheld that drops its wifi mid-walk this is the ordinary path, not the
        // unlucky one.
        using var stub = Library(600);
        using var connection = Connect(stub);
        var set = Set();

        await new SetResolveService(_session, connection).ResolveAsync(
            [set],
            new Immediate<SetResolveProgress>(progress =>
            {
                // The link goes after the first page lands, so one segment's worth of members
                // is in the accumulator when the next request throws.
                if (progress.Offset >= 250)
                {
                    stub.IsReachable = false;
                }
            }),
            TestContext.Current.CancellationToken);

        var afterDrop = _session.Store.SyncSets.Members(_session.Store.SyncSets.Find("resume")!.Id);

        Assert.True(
            afterDrop.Count >= 250,
            $"the dropped walk kept {afterDrop.Count} members, so the segment it found was lost");

        stub.IsReachable = true;

        await new SetResolveService(_session, connection).ResolveAsync(
            [set],
            new Immediate<SetResolveProgress>(_ => { }),
            TestContext.Current.CancellationToken);

        // Both segments, which is the whole claim. The second walk completes and its
        // completion sweep retires anything the set no longer holds, so a first segment that
        // was never written is a first segment permanently gone.
        var complete = _session.Store.SyncSets.Members(_session.Store.SyncSets.Find("resume")!.Id);

        Assert.Equal(600, complete.Count);
    }

    [Fact]
    public async Task A_resumed_walk_keeps_the_exclusions_the_first_segment_found()
    {
        // The shape a real collection has: most rows sync, some are refused for a reason worth
        // reporting. Measured on a live install, a resumed walk finished saying "1 skipped,
        // format not supported" where the complete walk had said 92, because an earlier
        // segment's exclusions are not carried and the completion sweep then retires them.
        using var stub = new StubRomMServer();

        for (var id = 1; id <= 600; id++)
        {
            // Every third rom carries an extension this system cannot launch.
            var supported = id % 3 != 0;

            stub.Library.Add(new StubRom(
                id,
                1,
                "snes",
                "snes",
                $"Game {id:0000}",
                supported ? $"g{id}.sfc" : $"g{id}.xyz",
                supported ? "sfc" : "xyz",
                1024));
        }

        using var connection = Connect(stub);

        var set = _session.Store.SyncSets.Add(
            new SyncSetDefinition
            {
                Name = "mixed",
                Scope = CatalogScopeKind.Platform,
                ScopeValue = "1",
            },
            Now);

        using var cancel = new CancellationTokenSource();

        await Assert.ThrowsAsync<SetResolveCancelledException>(() =>
            new SetResolveService(_session, connection).ResolveAsync(
                [set],
                new Immediate<SetResolveProgress>(p =>
                {
                    if (p.Offset >= 250)
                    {
                        cancel.Cancel();
                    }
                }),
                cancel.Token));

        await new SetResolveService(_session, connection).ResolveAsync(
            [set], progress: null, TestContext.Current.CancellationToken);

        var members = _session.Store.SyncSets.MemberTotals(set.Id).Games;
        var skipped = _session.Store.SyncSets
            .Exclusions(set.Id)
            .Where(e => e.State == MemberState.ExcludedExtension)
            .Sum(e => e.Count);

        // 400 launchable, 200 refused on format, whether or not anybody stopped it half way.
        Assert.Equal(400, members);
        Assert.Equal(200, skipped);
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
    public async Task A_cancelled_walk_keeps_the_games_it_had_already_found()
    {
        using var stub = Library(600);
        using var connection = Connect(stub);
        var set = Set();

        using var cancel = new CancellationTokenSource();

        await Assert.ThrowsAsync<SetResolveCancelledException>(() =>
            new SetResolveService(_session, connection).ResolveAsync(
                [set],
                // Cancel once two pages are in, so there is something to lose.
                new Immediate<SetResolveProgress>(progress =>
                {
                    if (progress.Scanned >= 500)
                    {
                        cancel.Cancel();
                    }
                }),
                cancel.Token));

        // The whole point of resuming. Recording the offset and dropping the rows found before
        // it means the next walk restarts its accumulator from nothing, so the finished set is
        // missing everything before the cancel: the offset survives and the work does not.
        var (games, _) = _session.Store.SyncSets.MemberTotals(set.Id);

        Assert.True(games > 0, "a cancelled walk kept none of the games it had already read");
        Assert.Equal(500, games);
    }

    [Fact]
    public async Task Resuming_after_a_cancel_ends_with_every_game_the_scope_holds()
    {
        using var stub = Library(600);
        using var connection = Connect(stub);
        var set = Set();

        await ResumeKeepsEverything(stub, connection, set);
    }

    [Fact]
    public async Task Resuming_an_uncapped_set_also_ends_with_every_game()
    {
        // The shape the interface makes. Per-set caps were dropped in this stage, so every set
        // created from a screen is uncapped, and the capped path is the only one the resume was
        // ever tested against.
        using var stub = Library(600);
        using var connection = Connect(stub);

        var set = _session.Store.SyncSets.Add(
            new SyncSetDefinition
            {
                Name = "uncapped",
                Scope = CatalogScopeKind.Platform,
                ScopeValue = "1",
            },
            Now);

        await ResumeKeepsEverything(stub, connection, set);
    }

    private async Task ResumeKeepsEverything(
        StubRomMServer stub,
        RomMConnection connection,
        SyncSetDefinition set)
    {
        _ = stub;

        using var cancel = new CancellationTokenSource();

        await Assert.ThrowsAsync<SetResolveCancelledException>(() =>
            new SetResolveService(_session, connection).ResolveAsync(
                [set],
                new Immediate<SetResolveProgress>(progress =>
                {
                    if (progress.Scanned >= 250)
                    {
                        cancel.Cancel();
                    }
                }),
                cancel.Token));

        var reports = await new SetResolveService(_session, connection).ResolveAsync(
            [set], progress: null, TestContext.Current.CancellationToken);

        // End to end, which is what a person actually checks: stop it, start it again, and the
        // set holds what it would have held if nobody had touched it.
        Assert.Equal(ResolveState.Resolved, Assert.Single(reports).State);
        Assert.Equal(600, _session.Store.SyncSets.MemberTotals(set.Id).Games);
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

    [Fact]
    public async Task A_cancelled_run_carries_its_reports_in_walk_order()
    {
        // What the resolve screen reads to name the set it stopped on. It took the first
        // report, so stopping during the third of three printed the first one's summary under
        // "Stopped." The order is the contract, and it was never asserted.
        using var stub = Library(600);
        using var connection = Connect(stub);

        var sets = new[] { Set("first"), Set("second"), Set("third") };

        using var cancel = new CancellationTokenSource();

        var thrown = await Assert.ThrowsAsync<SetResolveCancelledException>(() =>
            new SetResolveService(_session, connection).ResolveAsync(
                sets,
                new Immediate<SetResolveProgress>(p =>
                {
                    if (p.SetIndex == 3)
                    {
                        cancel.Cancel();
                    }
                }),
                cancel.Token));

        Assert.Equal(["first", "second", "third"], thrown.Reports.Select(r => r.SetName));

        // The one a person is looking at, and the one the screen names.
        Assert.Equal("third", thrown.Reports[^1].SetName);
        Assert.Equal(ResolveState.Interrupted, thrown.Reports[^1].State);
    }

    private SyncSetDefinition Set(string name = "resume") =>
        _session.Store.SyncSets.Add(
            new SyncSetDefinition
            {
                Name = name,
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
