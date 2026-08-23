using RomM.Client.Catalog;
using RomMBat.Core.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Reclaiming <c>partial/</c>, which neither the budget nor the free-space floor can see.
/// </summary>
/// <remarks>
/// Every assertion here is about the mechanism rather than the summary, because the defect this
/// closes is invisible in any report: the bytes were on disk, counted by nothing, and reachable
/// by nothing.
/// </remarks>
public sealed class PartialSweepTests : IDisposable
{
    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();

    public void Dispose() => _tree.Dispose();

    [Fact]
    public void A_rom_transfer_a_set_still_wants_is_kept_and_one_nothing_wants_goes()
    {
        // Keyed on set membership, not on age. An interrupted transfer waiting to resume looks
        // exactly like an orphan on disk, so the only thing that separates them is whether an
        // enabled set still claims the game.
        using var store = LocalStore.Open(_tree.Install());

        var set = store.SyncSets.Add(
            new SyncSetDefinition { Name = "kept", Scope = CatalogScopeKind.Platform, ScopeValue = "snes" },
            DateTimeOffset.UtcNow);

        store.SyncSets.ReplaceMembers(set.Id, [Member(7)], "one game", DateTimeOffset.UtcNow);

        Write("7.part", "half a rom this set still wants");
        Write("9.part", "half a rom no set has heard of");

        var plan = new PartialSweep(_tree.Install(), store).Plan();

        var candidate = Assert.Single(plan.Candidates);
        Assert.Equal("9.part", candidate.Name);
        Assert.Equal(PartialReason.Unclaimed, candidate.Reason);

        new PartialSweep(_tree.Install(), store).Apply(plan);

        Assert.True(File.Exists(Resolve("7.part")), "a transfer its set still wants was removed");
        Assert.False(File.Exists(Resolve("9.part")));
    }

    [Fact]
    public void A_rom_transfer_mid_flight_is_kept_even_with_no_content_download_row()
    {
        // The row is written on commit, so a transfer that has not committed has none. Keying on
        // the row rather than on membership would delete the file a resume is waiting for.
        using var store = LocalStore.Open(_tree.Install());

        var set = store.SyncSets.Add(
            new SyncSetDefinition { Name = "kept", Scope = CatalogScopeKind.Platform, ScopeValue = "snes" },
            DateTimeOffset.UtcNow);

        store.SyncSets.ReplaceMembers(set.Id, [Member(7)], "one game", DateTimeOffset.UtcNow);
        Write("7.part", "resuming");

        Assert.Null(store.Downloads.Find(7));
        Assert.Empty(new PartialSweep(_tree.Install(), store).Plan().Candidates);
    }

    [Fact]
    public void The_four_producers_that_never_resume_are_all_abandoned_once_left_behind()
    {
        // None of these opens for resume, and each deletes its own file in a finally, so
        // anything of theirs still here is from a pass that died. The staging directory counts
        // too: it is where a class C restore extracts before touching the live tree.
        using var store = LocalStore.Open(_tree.Install());

        Write("bios-0123456789abcdef0123456789abcdef.part", "half a bios");
        Write("save-42.part", "half a save");
        Write("resolve-42.part", "half a resolution");
        Write("unit-deadbeef.zip", "half a unit");
        WriteDirectory("unit-deadbeef", "SAVEDATA/GAME.DAT", "staged member");

        var plan = new PartialSweep(_tree.Install(), store).Plan();

        Assert.Equal(5, plan.Candidates.Count);
        Assert.All(plan.Candidates, candidate => Assert.Equal(PartialReason.Abandoned, candidate.Reason));
        Assert.Contains(plan.Candidates, candidate => candidate.IsDirectory);

        var outcome = new PartialSweep(_tree.Install(), store).Apply(plan);

        Assert.Equal(5, outcome.Removed);
        Assert.Empty(outcome.Problems);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_tree.Install().Resolve(PartialSweep.Directory)));
    }

    [Fact]
    public void A_name_no_producer_writes_is_left_alone()
    {
        // Deleting on the strength of "it is in a directory we own" is how a sweep destroys
        // something it was never asked to judge.
        using var store = LocalStore.Open(_tree.Install());

        Write("notes.txt", "someone put this here");
        Write("12abc.part", "not a rom id, and int.TryParse over a prefix would say it was");

        Assert.Empty(new PartialSweep(_tree.Install(), store).Plan().Candidates);
        Assert.True(File.Exists(Resolve("notes.txt")));
        Assert.True(File.Exists(Resolve("12abc.part")));
    }

    [Fact]
    public void A_transfer_in_flight_survives_because_the_filesystem_refuses_the_delete()
    {
        // The second line, for the producers that run outside the tree lock. sync and bios hold
        // their partial with FileShare.None while writing, so a sweep racing one cannot take the
        // file out from under it. Losing that race costs a transfer, not data.
        using var store = LocalStore.Open(_tree.Install());

        Write("save-42.part", "being written right now");

        var sweep = new PartialSweep(_tree.Install(), store);
        var plan = sweep.Plan();

        using (new FileStream(Resolve("save-42.part"), FileMode.Open, FileAccess.Write, FileShare.None))
        {
            var outcome = sweep.Apply(plan);

            Assert.Equal(0, outcome.Removed);
            Assert.Contains("save-42.part", Assert.Single(outcome.Problems), StringComparison.Ordinal);
        }

        Assert.True(File.Exists(Resolve("save-42.part")));
    }

    [Fact]
    public void Every_path_the_sweep_carries_is_relative_to_the_root()
    {
        // Rule 1. This walks the filesystem, which is exactly where an absolute path gets into
        // something that outlives the drive letter.
        using var store = LocalStore.Open(_tree.Install());

        Write("save-42.part", "half a save");

        var candidate = Assert.Single(new PartialSweep(_tree.Install(), store).Plan().Candidates);

        Assert.Equal("emulators/rommbat/partial/save-42.part", candidate.Path.Value);
        Assert.DoesNotContain(":", candidate.Path.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Nothing_is_removed_while_another_agent_holds_the_tree_lock()
    {
        // A class C restore extracts into partial/unit-<guid>/ and holds no handle on it:
        // SaveArchive closes each entry's writer inside its own loop. A recursive delete landing
        // in that window succeeds, and the restore then fails partway through its moves with the
        // container half swapped. A sentinel file inside the directory does not close it, since
        // a recursive delete takes the siblings before it reaches the sentinel. The lock does.
        using var store = LocalStore.Open(_tree.Install());

        WriteDirectory("unit-0f1e2d3c4b5a69788796a5b4c3d2e1f0", "SAVEDATA/GAME.DAT", "being restored");

        var sweep = new PartialSweep(_tree.Install(), store);
        var plan = sweep.Plan();

        Assert.Single(plan.Candidates);

        using (TreeLock.TryAcquire(_tree.Install()))
        {
            var outcome = sweep.Apply(plan);

            Assert.True(outcome.Skipped);
            Assert.Equal(0, outcome.Removed);
        }

        Assert.True(
            Directory.Exists(Resolve("unit-0f1e2d3c4b5a69788796a5b4c3d2e1f0")),
            "a restore's staging directory was removed under it");

        // And it goes on the next pass, once the lock is free.
        Assert.Equal(1, sweep.Apply(plan).Removed);
    }

    private static SyncSetMember Member(int romId) => new()
    {
        RomId = romId,
        State = MemberState.Member,
        Folder = "snes",
        PlatformSlug = "snes",
        FsName = $"Game {romId}.sfc",
        FsExtension = "sfc",
        DisplayName = $"Game {romId}",
        SortKey = $"game {romId}",
        Position = 1,
    };

    private string Resolve(string name) =>
        _tree.Install().Resolve(PartialSweep.Directory.Combine(name));

    private void Write(string name, string contents)
    {
        var path = Resolve(name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }

    private void WriteDirectory(string name, string member, string contents)
    {
        var path = Path.Combine(Resolve(name), member.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }
}
