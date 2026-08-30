using RomM.Client.Catalog;
using RomMBat.Core;
using RomMBat.Core.Sets;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The order a sync makes its passes in, asserted rather than described.
/// </summary>
/// <remarks>
/// <b>Two of these orderings are data-loss guards and neither was checkable before the seam.</b>
/// The ordering used to be statement order inside a 200-line method that printed as it went,
/// so the only way to observe it was to redirect a console and compare the positions of two
/// strings, which couples the rule to how the report happens to be formatted and says nothing
/// about the path the interface takes through the same code.
/// <para>
/// <b>The flush goes first.</b> It is what brings <c>local_save</c> up to date, and eviction
/// inside the same run asks <c>local_save</c> whether a game's saves are safely up. Flushing
/// afterwards answers that from the previous run, and the failure is a ROM removed while the
/// save it just wrote is still unsent, which is permanent.
/// </para>
/// <para>
/// <b>BIOS goes ahead of every ROM.</b> A platform synced without its firmware is dead weight:
/// the games appear in EmulationStation, look right, and die on launch. Fetching firmware after
/// the ROMs leaves exactly that state behind on any run that was interrupted, and interrupted
/// is the normal case for a handheld.
/// </para>
/// </remarks>
public sealed class LibrarySyncOrderTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();
    private readonly InstallSession _session;

    public LibrarySyncOrderTests()
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
    public void The_declared_order_puts_the_flush_first_and_the_firmware_ahead_of_the_roms()
    {
        var order = LibrarySyncService.Order.ToList();

        // Read off the declaration. The run below asserts that a real pass agrees with it, so
        // these two together are what fails when somebody exchanges two statements.
        Assert.True(order.IndexOf(SyncPass.Flush) < order.IndexOf(SyncPass.Bios));
        Assert.True(order.IndexOf(SyncPass.Flush) < order.IndexOf(SyncPass.Content));
        Assert.True(order.IndexOf(SyncPass.Bios) < order.IndexOf(SyncPass.Content));
        Assert.True(order.IndexOf(SyncPass.Resolve) < order.IndexOf(SyncPass.Content));
        Assert.True(order.IndexOf(SyncPass.Content) < order.IndexOf(SyncPass.Gamelists));
    }

    [Fact]
    public async Task Every_pass_is_declared_so_nothing_can_be_reported_that_the_order_does_not_name()
    {
        var (_, seen, _) = await RunAsync();

        // The other half of the assertion below. Without this, a pass added and reported but
        // left out of Order would sail past the sequence check, because a subsequence of a
        // list it is not in cannot contradict it.
        Assert.All(seen, pass => Assert.Contains(pass, LibrarySyncService.Order));
    }

    [Fact]
    public async Task A_real_run_reports_its_passes_in_the_declared_order()
    {
        var (_, seen, _) = await RunAsync();

        // The observed sequence has to be a subsequence of the declaration: a run skips passes
        // it has no work for, but it may never reorder the ones it does run.
        var expected = LibrarySyncService.Order.Where(seen.Contains).ToList();

        Assert.Equal(expected, seen);
    }

    [Fact]
    public async Task The_flush_runs_before_any_pass_that_reads_what_it_wrote()
    {
        var (_, seen, flushedAt) = await RunAsync();

        Assert.True(flushedAt >= 0, "the flush delegate was never called");

        // Positional rather than by name: the flush is a delegate the caller supplies, so what
        // is asserted is that the service called it before it went on, not that some string
        // was printed first.
        var before = seen.Take(flushedAt).ToList();
        var after = seen.Skip(flushedAt).ToList();

        Assert.DoesNotContain(SyncPass.Bios, before);
        Assert.DoesNotContain(SyncPass.Content, before);
        Assert.DoesNotContain(SyncPass.Gamelists, before);

        Assert.Contains(SyncPass.Bios, after);
        Assert.Contains(SyncPass.Content, after);
    }

    [Fact]
    public async Task A_dry_run_installs_nothing_and_flushes_nothing()
    {
        var flushed = false;

        await new LibrarySyncService(_session).RunAsync(
            [Set()],
            new SyncOptions(DryRun: true, Offline: true),
            connection: null,
            new Immediate<SyncEvent>(_ => { }),
            _ =>
            {
                flushed = true;
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        // A dry run writes nothing at all, which is what a dry run means. The flush writes.
        Assert.False(flushed);
    }

    /// <summary>Runs an offline sync and records what it reported, in order.</summary>
    /// <returns>
    /// The report, the passes seen, and how many had been seen when the flush delegate ran.
    /// </returns>
    private async Task<(SyncReport Report, List<SyncPass> Seen, int FlushedAt)> RunAsync()
    {
        var seen = new List<SyncPass>();
        var flushedAt = -1;

        var report = await new LibrarySyncService(_session).RunAsync(
            [Set()],
            // Offline rather than dry: a dry run skips the hooks, the menu and the flush, which
            // are three of the passes whose position is the thing being asserted.
            new SyncOptions(Offline: true),
            connection: null,
            new Immediate<SyncEvent>(report =>
            {
                if (seen.Count == 0 || seen[^1] != report.Pass)
                {
                    seen.Add(report.Pass);
                }
            }),
            _ =>
            {
                flushedAt = seen.Count;
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        return (report, seen, flushedAt);
    }

    /// <summary>
    /// A set with one resolved member, which is what makes the BIOS pass have anything to say.
    /// </summary>
    /// <remarks>
    /// The folder is <c>psx</c> because the pass reports nothing when no folder in the sync has
    /// a firmware requirement, and a set with no members has no folders at all. Without a
    /// member this asserts the ordering of a run that never reached the pass being ordered.
    /// </remarks>
    private SyncSetDefinition Set()
    {
        var set = _session.Store.SyncSets.Add(
            new SyncSetDefinition
            {
                Name = "ordering",
                Scope = CatalogScopeKind.Platform,
                ScopeValue = "1",
            },
            Now);

        _session.Store.SyncSets.ReplaceMembers(
            set.Id,
            [
                new SyncSetMember
                {
                    RomId = 1,
                    State = MemberState.Member,
                    Folder = "psx",
                    PlatformSlug = "psx",
                    FsName = "Game.chd",
                    FsExtension = "chd",
                    SizeBytes = 1024,
                    DisplayName = "Game",
                    SortKey = "game",
                    Position = 1,
                    ResolvedAt = Now,
                },
            ],
            "1 game",
            Now,
            complete: true);

        return _session.Store.SyncSets.Find("ordering")!;
    }
}
