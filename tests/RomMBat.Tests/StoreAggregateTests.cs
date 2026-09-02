using System.Diagnostics;
using RomM.Client.Catalog;
using RomMBat.Core;
using RomMBat.Core.Paths;
using RomMBat.Core.Sets;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The two aggregates that replaced per-game store reads, and the rules they had to keep.
/// </summary>
/// <remarks>
/// <b>#111 is a change of cost, not of behaviour</b>, so what these assert is that the answers
/// are the same ones the loops gave. Each includes the case that would be easiest to get wrong
/// by writing the SQL from the summary instead of from the code: adopted files count towards
/// what a set occupies and not towards the budget, and those are two different queries.
/// </remarks>
public sealed class StoreAggregateTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();
    private readonly InstallSession _session;

    public StoreAggregateTests()
    {
        _session = InstallSession.Open(_tree.Root).Session!;
    }

    public void Dispose()
    {
        _session.Dispose();
        _tree.Dispose();
    }

    [Fact]
    public void What_a_set_occupies_counts_every_kind_and_the_users_own_files_too()
    {
        var set = Set("mixed");

        Row(1, LocalFileKind.Rom, 2_048, FileOrigin.Synced);
        Row(1, LocalFileKind.Image, 512, FileOrigin.Synced);

        // The user's own ROM in that folder is using the drive too, which is why OnDisk counts
        // it and the budget does not.
        Row(2, LocalFileKind.Rom, 4_096, FileOrigin.Adopted);

        // Not in the set, so not counted against it.
        Row(3, LocalFileKind.Rom, 8_192, FileOrigin.Synced);

        Members(set, 1, 2);

        var summary = Assert.Single(new SyncSetService(_session).List());

        Assert.Equal(2_048 + 512 + 4_096, summary.OnDiskBytes);
    }

    [Fact]
    public void The_budget_figure_counts_only_what_RomMBat_downloaded()
    {
        Row(1, LocalFileKind.Rom, 2_048, FileOrigin.Synced);
        Row(1, LocalFileKind.Image, 512, FileOrigin.Synced);
        Row(2, LocalFileKind.Rom, 4_096, FileOrigin.Adopted);

        // Counting the user's own library would put the app permanently over its cap.
        Assert.Equal(2_560, _session.Store.Files.SyncedBytes());
    }

    [Fact]
    public void A_set_with_no_members_occupies_nothing_rather_than_everything()
    {
        // The empty-collection case, which an IN () built by string concatenation turns into
        // either a syntax error or a clause that matches every row.
        Row(1, LocalFileKind.Rom, 2_048, FileOrigin.Synced);

        Assert.Equal(0, _session.Store.Files.BytesForRoms([]));
    }

    /// <summary>
    /// The size the issue named, timed, so the claim is a measurement rather than an argument.
    /// </summary>
    /// <remarks>
    /// Two thousand members rather than the 9,196 the platform scope measured, because a test
    /// that seeds nine thousand rows costs the suite more than the assertion is worth. The
    /// shape is what matters: the old form was one query per member per set on the drawing
    /// thread, each taking and releasing the store gate.
    /// <para>
    /// Measured separately at 5,000 members on the development machine: the per-member loop
    /// 111 ms, a parameterised <c>IN</c> over the same ids 95 ms, and the subquery this now
    /// uses 1 ms. The middle number is why the obvious rewrite was not the one taken.
    /// </para>
    /// </remarks>
    [Fact]
    public void Listing_a_large_set_stays_inside_the_budget_a_screen_has()
    {
        var set = Set("large");

        for (var romId = 1; romId <= 2_000; romId++)
        {
            Row(romId, LocalFileKind.Rom, 1_024, FileOrigin.Synced);
        }

        Members(set, [.. Enumerable.Range(1, 2_000)]);

        var service = new SyncSetService(_session);
        service.List();

        var clock = Stopwatch.StartNew();
        var listed = service.List();
        clock.Stop();

        Assert.Equal(2_000 * 1_024L, listed[0].OnDiskBytes);
        Assert.True(
            clock.ElapsedMilliseconds < 2_000,
            $"listing 2,000 members took {clock.ElapsedMilliseconds} ms");
    }

    private SyncSetDefinition Set(string name) =>
        _session.Store.SyncSets.Add(
            new SyncSetDefinition { Name = name, Scope = CatalogScopeKind.Platform, ScopeValue = "1" },
            Now);

    private void Members(SyncSetDefinition set, params int[] romIds) =>
        _session.Store.SyncSets.ReplaceMembers(
            set.Id,
            [
                .. romIds.Select((romId, index) => new SyncSetMember
                {
                    RomId = romId,
                    State = MemberState.Member,
                    Folder = "snes",
                    PlatformSlug = "snes",
                    FsName = $"rom{romId}.sfc",
                    FsExtension = "sfc",
                    SizeBytes = 1_024,
                    DisplayName = $"Game {romId}",
                    SortKey = $"game {romId}",
                    Position = index + 1,
                    ResolvedAt = Now,
                }),
            ],
            $"{romIds.Length} games",
            Now);

    private void Row(int romId, LocalFileKind kind, long bytes, FileOrigin origin) =>
        _session.Store.Files.Record(new LocalFile
        {
            Path = RelativePath.Create($"roms/snes/{romId}-{kind}.bin"),
            Folder = "snes",
            RomId = romId,
            Kind = kind,
            FileName = $"{romId}-{kind}.bin",
            SizeBytes = bytes,
            Origin = origin,
        });
}
