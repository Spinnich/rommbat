using RomM.Client;
using RomMBat.Core.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// A conflict outliving the flush that found it, and a person deciding it.
/// </summary>
/// <remarks>
/// <b>This is the half of the milestone's "done when" that stage 1 could not carry.</b> The plan
/// ends on "the newer save comes back down as a conflict <i>the user resolves</i>", and until
/// this existed the conflict lived on an in-memory list that <c>flush</c> printed once. Issue
/// #31.
/// </remarks>
public class SaveConflictTests
{
    private static readonly Uri Origin = new("https://romm.invalid");
    private const string DeviceId = "device-under-test";
    private const string Slot = "libretro:battery";

    [Fact]
    public async Task A_conflict_survives_the_flush_that_found_it()
    {
        using var fixture = ConflictFixture.Create();
        await fixture.ConflictAsync();

        var conflict = Assert.Single(fixture.Store.SaveConflicts.ListOpen());

        Assert.Equal(7, conflict.RomId);
        Assert.Equal(Slot, conflict.Slot);
        Assert.True(conflict.IsOpen);
        Assert.NotEqual(conflict.LocalHash, conflict.ServerHash);
        Assert.NotNull(conflict.ServerSaveId);

        // The copy taken aside is pointed at, so the user has somewhere to look after the
        // console output has scrolled away.
        Assert.NotNull(conflict.LocalCopyPath);
        Assert.Equal("what this device did", File.ReadAllText(fixture.Resolve(conflict.LocalCopyPath.Value.Value)));
    }

    [Fact]
    public async Task Conflicting_again_does_not_write_a_second_copy_aside()
    {
        // Stage 1 copied on every pass, so a slot that conflicted and was never resolved gained
        // one dated file under replaced/ per flush and nothing pruned them.
        using var fixture = ConflictFixture.Create();

        await fixture.ConflictAsync();
        await fixture.ConflictAsync();
        await fixture.ConflictAsync();

        var replaced = fixture.Resolve(SaveSync.AsideDirectory.Value);

        Assert.Single(Directory.GetFiles(replaced));
        Assert.Single(fixture.Store.SaveConflicts.ListOpen());
    }

    [Fact]
    public async Task Re_observing_a_conflict_does_not_reset_how_long_it_has_stood()
    {
        using var fixture = ConflictFixture.Create();
        await fixture.ConflictAsync();

        var first = Assert.Single(fixture.Store.SaveConflicts.ListOpen()).FirstSeenAtUtc;

        fixture.Advance(TimeSpan.FromDays(3));
        await fixture.ConflictAsync();

        var again = Assert.Single(fixture.Store.SaveConflicts.ListOpen());

        Assert.Equal(first, again.FirstSeenAtUtc);
        Assert.True(again.LastSeenAtUtc > first);
    }

    [Fact]
    public async Task Keeping_the_local_side_overwrites_the_server_and_prunes_the_copy()
    {
        using var fixture = ConflictFixture.Create();
        await fixture.ConflictAsync();

        var copy = Assert.Single(fixture.Store.SaveConflicts.ListOpen()).LocalCopyPath!.Value;

        // The one place overwrite=true is sent, and only because a person asked. The stub
        // refuses an ordinary upload on this slot exactly as a stale device record does.
        fixture.Stub.ConflictOnUpload.Add((7, Slot));

        var outcome = await fixture.ResolveAsync(ConflictResolution.KeepLocal);

        Assert.True(outcome.Resolved, outcome.Message);

        // The server now holds this device's bytes, in the row that was already there rather
        // than in a second one beside it.
        var stored = Assert.Single(fixture.Stub.Saves.Values);
        Assert.Equal("what this device did", System.Text.Encoding.UTF8.GetString(stored.Bytes));

        // And the local file is untouched, which is the point of keeping it.
        Assert.Equal("what this device did", File.ReadAllText(fixture.Resolve("saves/gb/Tetris (World).srm")));

        // "Keep the previous copy aside until the next successful sync" finally has a next
        // successful sync to happen at.
        Assert.False(File.Exists(fixture.Resolve(copy.Value)));
        Assert.Empty(fixture.Store.SaveConflicts.ListOpen());

        // In step, so the next scan does not offer it straight back up.
        Assert.False(Assert.Single(fixture.Store.Saves.List()).IsUnsent);
    }

    [Fact]
    public async Task A_decided_conflict_keeps_its_row_and_stops_pointing_at_the_pruned_copy()
    {
        // Migration 007 keeps decided rows so `saves` can say what was chosen, and so a slot that
        // conflicts again is recognised as one already settled. Pruning the copy aside used to
        // delete the row with it, microseconds after the resolution was written.
        using var fixture = ConflictFixture.Create();
        await fixture.ConflictAsync();

        Assert.True((await fixture.ResolveAsync(ConflictResolution.KeepLocal)).Resolved);

        var decided = Assert.Single(fixture.Store.SaveConflicts.List());

        Assert.False(decided.IsOpen);
        Assert.Equal(ConflictResolution.KeepLocal, decided.Resolution);
        Assert.NotNull(decided.ResolvedAtUtc);

        // The file is gone, so the pointer to it goes too rather than outliving it.
        Assert.Null(decided.LocalCopyPath);
    }

    [Fact]
    public async Task Deciding_a_conflict_is_not_undone_by_the_next_flush_finding_the_same_slot()
    {
        using var fixture = ConflictFixture.Create();
        await fixture.ConflictAsync();

        Assert.True((await fixture.ResolveAsync(ConflictResolution.KeepServer)).Resolved);

        // The server side has not moved since the decision, so re-reporting it would make the
        // resolve command useless and would take a second copy aside.
        fixture.Advance(TimeSpan.FromHours(1));
        await fixture.ConflictAsync();

        Assert.Empty(fixture.Store.SaveConflicts.ListOpen());
        Assert.Empty(Directory.GetFiles(fixture.Resolve(SaveSync.AsideDirectory.Value)));
    }

    [Fact]
    public async Task A_slot_that_moves_again_after_a_decision_reopens_with_a_fresh_copy_aside()
    {
        using var fixture = ConflictFixture.Create();
        await fixture.ConflictAsync();

        Assert.True((await fixture.ResolveAsync(ConflictResolution.KeepServer)).Resolved);

        // Somebody else changed the slot after the decision, so the decision was about two sides
        // that no longer exist and the user has to be asked again.
        fixture.Stub.Saves[100] = fixture.Stub.Saves[100] with
        {
            Bytes = System.Text.Encoding.UTF8.GetBytes("what a third device did"),
        };

        fixture.Advance(TimeSpan.FromHours(1));
        await fixture.ConflictAsync();

        var reopened = Assert.Single(fixture.Store.SaveConflicts.ListOpen());

        Assert.True(reopened.IsOpen);

        // The copy taken for the first conflict was pruned when it was decided, so this one needs
        // its own rather than inheriting a path to a deleted file.
        Assert.NotNull(reopened.LocalCopyPath);
        Assert.True(File.Exists(fixture.Resolve(reopened.LocalCopyPath.Value.Value)));
    }

    [Fact]
    public async Task Keeping_the_server_side_writes_it_atomically_and_acks_after_the_bytes_land()
    {
        using var fixture = ConflictFixture.Create();
        await fixture.ConflictAsync();

        var outcome = await fixture.ResolveAsync(ConflictResolution.KeepServer);

        Assert.True(outcome.Resolved, outcome.Message);

        Assert.Equal(
            "what the other device did",
            File.ReadAllText(fixture.Resolve("saves/gb/Tetris (World).srm")));

        // The ack is the same discipline as an ordinary download: after the bytes are written
        // and checked, never on the response headers.
        Assert.Contains(100, fixture.Stub.Acknowledged);
        Assert.Empty(fixture.Stub.OptimisticDownloads);

        // Recorded as in step with the server, so the next scan does not read the restored file
        // as an unsent local change and push it straight back.
        var save = Assert.Single(fixture.Store.Saves.List());
        Assert.False(save.IsUnsent);
        Assert.False(save.HasChangedSinceUpload);

        Assert.Empty(fixture.Store.SaveConflicts.ListOpen());
    }

    [Fact]
    public async Task A_download_that_does_not_match_the_recorded_hash_writes_nothing()
    {
        using var fixture = ConflictFixture.Create();
        await fixture.ConflictAsync();

        // What a corrupted transfer looks like from here: the bytes arrive and do not match the
        // hash the conflict was recorded with.
        fixture.Stub.HashLie = "ffffffffffffffffffffffffffffffff";
        await fixture.ConflictAsync();

        var outcome = await fixture.ResolveAsync(ConflictResolution.KeepServer);

        Assert.False(outcome.Resolved);
        Assert.Contains("hashes to", outcome.Message, StringComparison.Ordinal);

        // The local file is exactly as it was, and the conflict is still open.
        Assert.Equal("what this device did", File.ReadAllText(fixture.Resolve("saves/gb/Tetris (World).srm")));
        Assert.Single(fixture.Store.SaveConflicts.ListOpen());
        Assert.Empty(fixture.Stub.Acknowledged);
    }

    [Fact]
    public async Task Resolving_a_slot_that_is_not_conflicted_does_nothing_and_says_so()
    {
        using var fixture = ConflictFixture.Create();

        var outcome = await fixture.ResolveAsync(ConflictResolution.KeepLocal);

        Assert.False(outcome.Resolved);
        Assert.Contains("no conflict recorded", outcome.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resolving_the_same_conflict_twice_is_refused_rather_than_repeated()
    {
        using var fixture = ConflictFixture.Create();
        await fixture.ConflictAsync();

        Assert.True((await fixture.ResolveAsync(ConflictResolution.KeepServer)).Resolved);

        var second = await fixture.ResolveAsync(ConflictResolution.KeepLocal);

        Assert.False(second.Resolved);

        // Refused because the row says it was decided, not because the row is gone. The two read
        // the same from the caller and only one of them lets a user see what they chose.
        Assert.Contains("already resolved", second.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_new_save_in_a_settled_slot_reopens_it_rather_than_stranding_it()
    {
        // A bundled save's content_hash is over the archive's contents, so a slot that returns to
        // contents it held before carries a digest that was already settled while being a
        // different row. Comparing the digest alone dropped that conflict on the floor: nothing
        // stored, `saves` listing nothing, `saves resolve` answering "already resolved", and
        // every flush still counting it while the local write was refused forever. Driven on
        // hardware, one device deleting a PSP save slot and another putting it back.
        using var fixture = ConflictFixture.Create();
        await fixture.ConflictAsync();

        Assert.True((await fixture.ResolveAsync(ConflictResolution.KeepServer)).Resolved);
        Assert.Empty(fixture.Store.SaveConflicts.ListOpen());

        // The same contents arrive as a new row, and this device has written since.
        File.WriteAllText(fixture.Resolve("saves/gb/Tetris (World).srm"), "written after deciding");
        fixture.Advance(TimeSpan.FromMinutes(5));
        fixture.ReplaceServerSave(id: 101);
        await fixture.ConflictAsync();

        var reopened = Assert.Single(fixture.Store.SaveConflicts.ListOpen());

        Assert.Equal(101, reopened.ServerSaveId);
        Assert.Null(reopened.Resolution);

        // And it is settleable, which is the point: a conflict nothing can end is worse than one
        // that was never reported.
        Assert.True((await fixture.ResolveAsync(ConflictResolution.KeepServer)).Resolved);
    }

    [Fact]
    public async Task An_unreachable_server_leaves_the_conflict_open_rather_than_half_applied()
    {
        using var fixture = ConflictFixture.Create();
        await fixture.ConflictAsync();

        fixture.Stub.IsReachable = false;

        var outcome = await fixture.ResolveAsync(ConflictResolution.KeepLocal);

        Assert.False(outcome.Resolved);
        Assert.Contains("not reachable", outcome.Message, StringComparison.Ordinal);

        // Still open, so the decision can be made again when the server is back.
        Assert.Single(fixture.Store.SaveConflicts.ListOpen());
        Assert.Equal("what this device did", File.ReadAllText(fixture.Resolve("saves/gb/Tetris (World).srm")));
    }

    [Fact]
    public async Task A_slot_that_moves_again_before_the_decision_is_reported_rather_than_forced()
    {
        using var fixture = ConflictFixture.Create();
        await fixture.ConflictAsync();

        // 409 even with overwrite=true, which is the server saying the slot moved between the
        // report the user read and the choice they made.
        fixture.Stub.RefuseOverwrite.Add((7, Slot));
        fixture.Stub.ConflictOnUpload.Add((7, Slot));

        var outcome = await fixture.ResolveAsync(ConflictResolution.KeepLocal);

        Assert.False(outcome.Resolved);
        Assert.Contains("moved again", outcome.Message, StringComparison.Ordinal);
        Assert.Single(fixture.Store.SaveConflicts.ListOpen());
    }

    /// <summary>A conflicted slot, and the two ways out of it.</summary>
    private sealed class ConflictFixture : IDisposable
    {
        private readonly TempRetroBatTree _tree;
        private readonly RomMConnection _connection;
        private readonly TestTimeProvider _time;

        private ConflictFixture(
            TempRetroBatTree tree,
            RetroBatInstall install,
            LocalStore store,
            StubRomMServer stub,
            TestTimeProvider time)
        {
            _tree = tree;
            _time = time;
            Install = install;
            Store = store;
            Stub = stub;
            _connection = new RomMConnection(
                new RomMClientOptions { Origin = Origin, AccessToken = "rmm_test" },
                stub);
        }

        public RetroBatInstall Install { get; }

        public LocalStore Store { get; }

        public StubRomMServer Stub { get; }

        public static ConflictFixture Create()
        {
            var tree = TempRetroBatTree.Create();
            var install = tree.Install();
            var stub = new StubRomMServer { ServerDate = new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero) };
            var time = new TestTimeProvider(new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero));

            var fixture = new ConflictFixture(tree, install, LocalStore.Open(install), stub, time);
            fixture.Seed();
            return fixture;
        }

        public string Resolve(string relative) => Install.Resolve(RelativePath.Create(relative));

        public void Advance(TimeSpan by) => _time.Advance(by);

        /// <summary>Runs a sync that negotiates this slot as a conflict.</summary>
        public async Task ConflictAsync()
        {
            Stub.NegotiateActions[(7, Slot)] = "conflict";

            await new SaveSync(Install, Store, _connection, DeviceId, _time).RunAsync();
        }

        public Task<ConflictResolutionOutcome> ResolveAsync(ConflictResolution resolution) =>
            new SaveConflictResolver(Install, Store, _connection, DeviceId, _time)
                .ResolveAsync(7, Slot, resolution);

        /// <summary>Puts the same contents in the slot under a new id, as another device would.</summary>
        public void ReplaceServerSave(int id)
        {
            var current = Stub.Saves.Values.Single();

            Stub.Saves.Clear();
            Stub.Saves[id] = current with { Id = id, OriginDeviceId = "some-other-device" };
        }

        private void Seed()
        {
            var romPath = RelativePath.Create("roms/gb/Tetris (World).zip");
            var romAbsolute = Install.Resolve(romPath);
            Directory.CreateDirectory(Path.GetDirectoryName(romAbsolute)!);
            File.WriteAllText(romAbsolute, "rom");

            Store.Files.Record(new LocalFile
            {
                Path = romPath,
                Folder = "gb",
                RomId = 7,
                Kind = LocalFileKind.Rom,
                FileName = "Tetris (World).zip",
                SizeBytes = 3,
            });

            var savePath = Install.Resolve(RelativePath.Create("saves/gb/Tetris (World).srm"));
            Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
            File.WriteAllText(savePath, "what this device did");

            new SaveScanner(Install, Store).Scan();

            Stub.Saves[100] = new StubRomMServer.StubSave
            {
                Id = 100,
                RomId = 7,
                Slot = Slot,
                Emulator = "libretro",
                Bytes = System.Text.Encoding.UTF8.GetBytes("what the other device did"),
                FileNameNoTags = "Tetris (World)",
                FileExtension = "srm",
                OriginDeviceId = "some-other-device",
                UpdatedAt = Stub.ServerDate ?? DateTimeOffset.UnixEpoch,
            };
        }

        public void Dispose()
        {
            _connection.Dispose();
            Store.Dispose();
            _tree.Dispose();
        }
    }
}
