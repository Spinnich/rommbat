using RomM.Client;
using RomM.Client.Catalog;
using RomM.Client.Content;
using RomMBat.Core;
using RomMBat.Core.Metadata;
using RomMBat.Core.Sets;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The order a sync does its work in, asserted rather than described.
/// </summary>
/// <remarks>
/// <b>Three of these orderings are data-loss guards and none was checkable before the seam.</b>
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
/// <para>
/// <b>A game's artwork goes ahead of the next game's ROM, and this file used to say the
/// opposite by omission.</b> Media was one pass after every ROM of every set, so a budget that
/// ran out stripped the artwork off the whole library rather than truncating its tail (#102).
/// The rule inverted here, and the test that was meant to guard the ordering could not see it:
/// its runner passed <c>SyncOptions(Offline: true)</c>, and the media pass is gated on being
/// online, so <see cref="SyncPass.Media"/> had never once appeared in the observed sequence.
/// It would have stayed green through the change it existed to catch. Every ordering claim
/// about artwork below is therefore taken from a run <b>with a connection</b>.
/// </para>
/// <para>
/// <b>Passes no longer occupy disjoint stretches of a run, so the sequence check is over first
/// occurrences.</b> Content and Media interleave by design now. What may never happen is a pass
/// <i>starting</i> out of turn.
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

        // Media is declared after Content because that is where its one summary is reported.
        // It is no longer a stretch of the run that begins after Content ends, which is what
        // the interleave assertion below is for.
        Assert.True(order.IndexOf(SyncPass.Content) < order.IndexOf(SyncPass.Media));
        Assert.True(order.IndexOf(SyncPass.Media) < order.IndexOf(SyncPass.Gamelists));
    }

    [Fact]
    public async Task Every_pass_is_declared_so_nothing_can_be_reported_that_the_order_does_not_name()
    {
        var (_, seen, _) = await RunOfflineAsync();

        // The other half of the assertion below. Without this, a pass added and reported but
        // left out of Order would sail past the sequence check, because a subsequence of a
        // list it is not in cannot contradict it.
        Assert.All(seen, pass => Assert.Contains(pass, LibrarySyncService.Order));
    }

    [Fact]
    public async Task Each_pass_starts_in_the_declared_order_even_though_artwork_runs_inside_the_content_pass()
    {
        using var stub = Library(2);
        var (_, seen) = await RunOnlineAsync(stub);

        // First occurrences, not the raw sequence: Content and Media alternate by design now,
        // and the rule that survives that is that no pass may begin out of turn.
        var starts = seen.Distinct().ToList();
        var expected = LibrarySyncService.Order.Where(starts.Contains).ToList();

        Assert.Equal(expected, starts);

        // Named rather than left to the list comparison, because these two are the data-loss
        // guards and a reader should not have to reconstruct them from an enum's declaration.
        Assert.Contains(SyncPass.Media, starts);
        Assert.True(starts.IndexOf(SyncPass.Bios) < starts.IndexOf(SyncPass.Content));
        Assert.True(starts.IndexOf(SyncPass.Content) < starts.IndexOf(SyncPass.Media));
    }

    [Fact]
    public async Task A_games_artwork_is_fetched_before_the_next_games_rom()
    {
        // The whole of #102, as an ordering. Before this, every ROM of every set was fetched
        // and only then was any artwork, so a budget that ran out during the ROMs left the
        // entire library in EmulationStation with no covers, and no later run repaired it
        // because nothing frees space by itself. Interleaved, a full budget truncates the tail
        // of the library instead of stripping the artwork off all of it.
        using var stub = Library(2);
        await RunOnlineAsync(stub);

        var firstRom = stub.RequestLog.ToList().FindIndex(IsRomContentFor(1));
        var firstArt = stub.RequestLog.ToList().FindIndex(IsAssetFor(1));
        var secondRom = stub.RequestLog.ToList().FindIndex(IsRomContentFor(2));
        var secondArt = stub.RequestLog.ToList().FindIndex(IsAssetFor(2));

        Assert.True(firstRom >= 0, "game one's ROM was never fetched, so nothing was ordered");
        Assert.True(firstArt >= 0, "game one's artwork was never fetched, so nothing was ordered");
        Assert.True(secondRom >= 0, "game two's ROM was never fetched, so nothing was ordered");
        Assert.True(secondArt >= 0, "game two's artwork was never fetched, so nothing was ordered");

        // A game's own ROM comes before its own artwork: artwork for a ROM that never landed
        // would be bytes for a gamelist entry that is never written.
        Assert.True(firstRom < firstArt, "game one's artwork was fetched before its own ROM");
        Assert.True(secondRom < secondArt, "game two's artwork was fetched before its own ROM");

        // And the inversion this branch exists for.
        Assert.True(
            firstArt < secondRom,
            "game one's artwork was fetched after game two's ROM, which is the pre-#102 order");
    }

    [Fact]
    public async Task The_flush_runs_before_any_pass_that_reads_what_it_wrote()
    {
        var (_, seen, flushedAt) = await RunOfflineAsync();

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

    private static Predicate<string> IsRomContentFor(int romId) =>
        path => path.Contains($"/api/roms/{romId}/content", StringComparison.Ordinal);

    private static Predicate<string> IsAssetFor(int romId) =>
        path => path.StartsWith($"/assets/romm/resources/roms/1/{romId}/", StringComparison.Ordinal);

    /// <summary>Runs an offline sync and records what it reported, in order.</summary>
    /// <returns>
    /// The report, the passes seen, and how many had been seen when the flush delegate ran.
    /// </returns>
    private async Task<(SyncReport Report, List<SyncPass> Seen, int FlushedAt)> RunOfflineAsync()
    {
        var seen = new List<SyncPass>();
        var flushedAt = -1;

        var report = await new LibrarySyncService(_session).RunAsync(
            [Set()],
            // Offline rather than dry: a dry run skips the hooks, the menu and the flush, which
            // are three of the passes whose position is the thing being asserted. Nothing about
            // artwork can be asserted from here, which is what RunOnlineAsync is for.
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
    /// Runs a real sync against the stub, which is the only way the media pass is reached.
    /// </summary>
    /// <remarks>
    /// <c>NoResolve</c>, with the membership and the metadata seeded directly: what is under
    /// test is the order the passes run in, and paging a catalog to get there would make a
    /// failure in <c>SetResolver</c> read as an ordering defect.
    /// </remarks>
    private async Task<(SyncReport Report, List<SyncPass> Seen)> RunOnlineAsync(StubRomMServer stub)
    {
        var set = Set(games: 2);
        var seen = new List<SyncPass>();

        using var connection = new RomMConnection(
            new RomMClientOptions { Origin = new Uri("http://stub.invalid"), AccessToken = "rmm_test" },
            stub);

        var report = await new LibrarySyncService(_session).RunAsync(
            [set],
            new SyncOptions(NoResolve: true),
            connection,
            new Immediate<SyncEvent>(reported => seen.Add(reported.Pass)),
            _ => Task.CompletedTask,
            TestContext.Current.CancellationToken);

        return (report, seen);
    }

    /// <summary>A library of games that all carry artwork, so the media pass has work.</summary>
    private static StubRomMServer Library(int count)
    {
        var stub = new StubRomMServer();
        stub.Platforms.Add(new StubPlatform(1, "psx", "psx", "PlayStation"));

        for (var id = 1; id <= count; id++)
        {
            stub.Library.Add(new StubRom(id, 1, "psx", "psx", $"Game {id}", $"Game {id}.chd", "chd", 1024)
            {
                Metadata = new StubRomMetadata(),
            });

            stub.Content[id] = new byte[1024];

            foreach (var kind in new[] { "cover/big.png", "cover/small.png", "logo/logo.png" })
            {
                stub.Media[$"/assets/romm/resources/roms/1/{id}/{kind}"] = new byte[64];
            }
        }

        return stub;
    }

    /// <summary>
    /// A set with resolved members, which is what makes the BIOS pass have anything to say.
    /// </summary>
    /// <remarks>
    /// The folder is <c>psx</c> because the pass reports nothing when no folder in the sync has
    /// a firmware requirement, and a set with no members has no folders at all. Without a
    /// member this asserts the ordering of a run that never reached the pass being ordered.
    /// </remarks>
    private SyncSetDefinition Set(int games = 1)
    {
        var set = _session.Store.SyncSets.Find("ordering") ?? _session.Store.SyncSets.Add(
            new SyncSetDefinition
            {
                Name = "ordering",
                Scope = CatalogScopeKind.Platform,
                ScopeValue = "1",
            },
            Now);

        var members = Enumerable.Range(1, games).Select(id => new SyncSetMember
        {
            RomId = id,
            State = MemberState.Member,
            Folder = "psx",
            PlatformSlug = "psx",
            FsName = $"Game {id}.chd",
            FsExtension = "chd",
            SizeBytes = 1024,
            DisplayName = $"Game {id}",
            SortKey = $"game {id}",
            Position = id,
            ResolvedAt = Now,
        }).ToList();

        _session.Store.SyncSets.ReplaceMembers(set.Id, [.. members], $"{games} games", Now, complete: true);

        foreach (var member in members)
        {
            // Media only ever runs for a game the store has metadata for, and metadata is
            // written by a resolve. Seeded here because this run is NoResolve.
            _session.Store.Metadata.Record(new GameMetadata
            {
                RomId = member.RomId,
                Folder = "psx",
                FsName = member.FsName,
                Name = member.DisplayName,
                MediaPaths = new Dictionary<MediaKind, string>
                {
                    [MediaKind.Image] = $"/assets/romm/resources/roms/1/{member.RomId}/cover/big.png",
                    [MediaKind.Thumbnail] = $"/assets/romm/resources/roms/1/{member.RomId}/cover/small.png",
                },
            });
        }

        return _session.Store.SyncSets.Find("ordering")!;
    }
}
