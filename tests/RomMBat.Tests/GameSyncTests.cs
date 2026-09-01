using RomM.Client;
using RomM.Client.Catalog;
using RomM.Client.Content;
using RomMBat.Core;
using RomMBat.Core.Content;
using RomMBat.Core.Metadata;
using RomMBat.Core.Paths;
using RomMBat.Core.Sets;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The invariant a sync is built around, and the three fences that bound it.
/// </summary>
/// <remarks>
/// <b>A sync leaves every game either wholly present, with its gamelist entry and whatever
/// artwork the server actually had for it, or wholly absent.</b> Whether it ran to the end, was
/// stopped, or lost the server.
/// <para>
/// <b>"With its artwork" cannot mean every configured kind, and the tests say what it does
/// mean.</b> Nothing guarantees the server has it: the RomM administrator may not have scraped
/// that kind, and the upstream source (ScreenScraper, IGDB) may never have held it for that
/// game. <see cref="MediaSyncOutcome.Missing"/> counts exactly that, no run can fix it, and a
/// rule demanding every kind would declare most real libraries permanently broken. What the
/// invariant forbids is the systematic stripping #102 caused, where artwork the server did have
/// went unfetched because the ROMs had eaten the budget first.
/// </para>
/// <para>
/// <b>Removing content is <c>evict</c>'s job and that rule is not weakened here.</b> What the
/// rollback takes is what this very run placed seconds ago, on a game that is not finished.
/// Each of the three fences that make that true has a test below, and each of those tests is
/// written so that removing the fence breaks it.
/// </para>
/// </remarks>
public sealed class GameSyncTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();
    private readonly InstallSession _session;

    public GameSyncTests()
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

    // ------------------------------------------------------------------ what a game is

    [Fact]
    public void The_discs_of_one_title_are_one_game_and_everything_else_is_a_game_of_its_own()
    {
        // SetResolver refuses multi-file ROMs outright, so a disc marker is the only thing that
        // binds two rows into one game and DiscSet already reads it. Nothing new had to learn
        // what a game is.
        var plan = PlanFor(
            (1, "Final Fantasy VII (Disc 1).chd"),
            (2, "Final Fantasy VII (Disc 2).chd"),
            (3, "Final Fantasy VII (Disc 3).chd"),
            (4, "Vagrant Story.chd"));

        var games = GameSync.Group(plan);

        Assert.Equal(2, games.Count);
        Assert.Equal("Final Fantasy VII", games[0].Title);
        Assert.Equal([1, 2, 3], games[0].RomIds);
        Assert.Equal([4], games[1].RomIds);
    }

    [Fact]
    public void A_title_with_no_marker_is_never_folded_into_one_that_has_it()
    {
        // "Game" and "Game (Disc 1)" share a prefix and are two different games. Keying the
        // unmarked one on its own file name is what keeps them apart.
        var plan = PlanFor((1, "Game.chd"), (2, "Game (Disc 1).chd"), (3, "Game (Disc 2).chd"));

        var games = GameSync.Group(plan);

        Assert.Equal(2, games.Count);
        Assert.Equal([1], games[0].RomIds);
        Assert.Equal([2, 3], games[1].RomIds);
    }

    // ------------------------------------------------------------------ the invariant

    [Fact]
    public async Task A_disc_set_stopped_between_its_discs_leaves_the_whole_game_absent()
    {
        // The invariant's sharpest case. Disc one has committed and disc two has not started,
        // so without the rollback the tree holds half a game: EmulationStation lists it, it
        // launches, and it dies at the disc change.
        using var stub = Library((1, "Title (Disc 1).chd"), (2, "Title (Disc 2).chd"), (3, "Other.chd"));

        var outcome = await RunAsync(stub, StopBefore(romId: 2));

        Assert.True(outcome.Stopped);
        Assert.Equal(1, outcome.RolledBack);

        // Wholly absent: no bytes, no rows, for either disc. Nothing was adopted in this
        // fixture, so the stronger claim holds and is made.
        AssertGone(1, "Title (Disc 1).chd");
        AssertGone(2, "Title (Disc 2).chd");
        Assert.Empty(_session.Store.Files.ForRom(1));
        Assert.Empty(_session.Store.Files.ForRom(2));

        // And nothing was fetched past the stop, so the run really did end there.
        Assert.False(File.Exists(RomPath("Other.chd")));
    }

    [Fact]
    public async Task A_disc_that_fails_takes_the_disc_that_landed_with_it()
    {
        // The same rule with nobody pressing anything, which is the case ruled onto this branch
        // before it was written. ContentSync's "a failure is per game, not per run" is what
        // makes a half-set the ordinary outcome rather than the unlucky one.
        using var stub = Library((1, "Title (Disc 1).chd"), (2, "Title (Disc 2).chd"));

        // Right id, wrong length. ContentSync verifies against the size the server declared and
        // refuses, which is a per-game failure rather than a transport error.
        stub.Content[2] = new byte[16];

        var outcome = await RunAsync(stub);

        Assert.False(outcome.Stopped);
        Assert.Equal(1, outcome.RolledBack);
        Assert.Equal(1, outcome.Content.Failed);

        AssertGone(1, "Title (Disc 1).chd");
        AssertGone(2, "Title (Disc 2).chd");
    }

    [Fact]
    public async Task A_single_file_game_that_fails_needs_no_rollback_because_it_committed_nothing()
    {
        // The other half of the same claim, and the reason the ruling costs nothing in the
        // common case: a transfer that never verifies is never renamed into roms/, so there is
        // nothing to take back and no row to remove.
        using var stub = Library((1, "Solo.chd"));
        stub.Content[1] = new byte[16];

        var outcome = await RunAsync(stub);

        Assert.Equal(1, outcome.Content.Failed);
        Assert.Equal(0, outcome.RolledBack);
        AssertGone(1, "Solo.chd");
    }

    [Fact]
    public async Task A_transfer_the_server_dropped_keeps_its_partial_even_though_the_game_is_rolled_back()
    {
        // The rollback and the resume are two rules that meet here, and only one of them is
        // about the user pressing anything. A 929 MB image that loses the LAN at 800 MB must
        // still be rolled back, because half a game on disk is the thing the invariant forbids,
        // and must still be resumable, because the whole reason a .part is written is that the
        // next run continues from it. Deleting it here would silently cost the 800 MB, and
        // nothing would say so: no file was removed, so no GameRolledBack event fires.
        //
        // The size-mismatch tests above cannot catch this. ContentSync deletes the partial
        // itself on a verification failure, so both paths look identical from outside.
        using var stub = Library((1, "Title (Disc 1).chd"), (2, "Title (Disc 2).chd"));
        stub.DropContentAfterBytes = 400;

        var outcome = await RunAsync(stub);

        Assert.False(outcome.Stopped);
        Assert.Equal(1, outcome.Content.Failed);
        Assert.Equal(1, outcome.RolledBack);

        // The disc that landed comes off, which is the invariant unchanged.
        AssertGone(2, "Title (Disc 2).chd");

        // And the disc that died keeps what a resume needs. Not on disk under roms/, because
        // it never committed.
        Assert.False(File.Exists(RomPath("Title (Disc 1).chd")));

        var part = _session.Install.Resolve(ContentPlanner.PartFor(1));
        Assert.True(File.Exists(part), "the partial the next run would continue from was deleted");
        Assert.Equal(400, new FileInfo(part).Length);
        Assert.NotNull(_session.Store.Downloads.Find(1));
    }

    [Fact]
    public async Task A_transfer_that_never_received_a_byte_leaves_no_partial_to_resume_from()
    {
        // The other side of the rule above, found by the hands-on pass. ContentSync opens the
        // .part before it makes the request, so a response that carries no body still leaves an
        // empty file and a download row. Measured on a live install: one RomM answering 502 for
        // three seconds left 155 empty partials and 155 rows, none of them resumable and all of
        // them something a person has to make sense of.
        //
        // Bytes are what make a partial worth keeping. Nothing in it, nothing to keep.
        using var stub = Library((1, "Solo.chd"));
        stub.DropContentAfterBytes = 0;

        var outcome = await RunAsync(stub);

        Assert.Equal(1, outcome.Content.Failed);
        Assert.False(File.Exists(RomPath("Solo.chd")));

        Assert.False(
            File.Exists(_session.Install.Resolve(ContentPlanner.PartFor(1))),
            "an empty partial was kept, which no resume can use");
        Assert.Null(_session.Store.Downloads.Find(1));
    }

    [Fact]
    public async Task A_rollback_takes_a_row_whose_folder_has_already_gone()
    {
        // Found by a hands-on pass, not by reading. File.Delete returns quietly for a missing
        // file and throws DirectoryNotFoundException for a missing folder, and that derives from
        // IOException, so the catch written for "a media player has this file open" was also
        // catching "there is nothing here at all". The row was kept for bytes that do not exist,
        // which is the inverse of the rule it serves, and a problem was reported about a file
        // the user no longer has.
        //
        // Reached on the live install because roms/ps2/images/ had been cleaned up under its
        // rows, so a stopped ps2 game left its artwork rows behind for ever.
        using var stub = Library((1, "Title.chd"), (2, "Other.chd"));

        var orphan = RelativePath.Create("roms/psx/images/Title-image.png");
        _session.Store.Files.Record(new LocalFile
        {
            Path = orphan,
            Folder = "psx",
            RomId = 1,
            Kind = LocalFileKind.Image,
            FileName = orphan.Name,
            SizeBytes = 64,
            VerifiedAt = Now,
            VerifiedBy = VerifiedBy.Size,
            Origin = FileOrigin.Synced,
        });

        // The folder itself, not just the file. That is the whole case.
        Assert.False(Directory.Exists(Path.GetDirectoryName(_session.Install.Resolve(orphan))));

        var outcome = await RunAsync(stub, StopBefore(romId: 1));

        Assert.True(outcome.Stopped);
        Assert.DoesNotContain(_session.Store.Files.ForRom(1), file => file.Path == orphan);
        Assert.Empty(outcome.RollbackProblems);
    }

    [Fact]
    public async Task Games_that_finished_before_the_stop_keep_their_files_their_rows_and_their_artwork()
    {
        // The other half of the invariant, and the one a person notices: a stop must not undo
        // work that was already done. Only the game in progress goes.
        using var stub = Library((1, "First.chd"), (2, "Second (Disc 1).chd"), (3, "Second (Disc 2).chd"));

        var outcome = await RunAsync(stub, StopBefore(romId: 3));

        Assert.True(outcome.Stopped);

        // Wholly present: the ROM, its row, and the artwork the server had for it.
        Assert.True(File.Exists(RomPath("First.chd")));

        var rows = _session.Store.Files.ForRom(1);
        Assert.Contains(rows, row => row.Kind == LocalFileKind.Rom);
        Assert.Contains(rows, row => row.Kind == LocalFileKind.Image);
        Assert.Equal(1, outcome.Media.Downloaded > 0 ? 1 : 0);

        // And the game that was in progress is wholly absent, both discs.
        AssertGone(2, "Second (Disc 1).chd");
        AssertGone(3, "Second (Disc 2).chd");
    }

    [Fact]
    public async Task Interleaved_artwork_never_spends_the_budget_the_remaining_roms_are_owed()
    {
        // Found by a live probe, not by reading, and it is the cost of the interleave rather
        // than a slip. Room is `cap - managed`, and `managed` is read from local_file when the
        // call is made. One media pass after every ROM had landed saw the true total; one pass
        // per game sees the budget as it stands after only that game's ROM, with every later
        // ROM still to come. Early artwork then spends what the plan had already earmarked.
        //
        // Measured against a live instance before the reservation existed: a 1 MB budget
        // finished 703 KB over, where the pass it replaced finished at 1023.3 KB of 1 MB.
        using var stub = Library((1, "First.chd"), (2, "Second.chd"), (3, "Third.chd"));

        foreach (var romId in new[] { 1, 2, 3 })
        {
            stub.Media[$"/assets/romm/resources/roms/1/{romId}/cover/big.png"] = new byte[2048];
        }

        // Three 1 KB ROMs, 2 KB of artwork each, and a cap with 1000 bytes to spare over the
        // ROMs. The numbers are chosen so the first game's artwork alone would take more than
        // the budget has left once the other two ROMs are counted: without the reservation the
        // first game sees 3,048 bytes free, spends 2,048 of it, and the two ROMs behind it push
        // the run 1,048 bytes past the cap.
        var cap = (3 * 1024L) + 1000;
        _session.Store.Settings.Set(SettingStore.ContentMaxBytes, cap, Now);

        var outcome = await RunAsync(stub);

        var managed = _session.Store.Files.List()
            .Where(file => file.Origin == FileOrigin.Synced)
            .Sum(file => file.SizeBytes);

        Assert.True(
            managed <= cap,
            $"the run took {managed} bytes against a {cap} byte budget, so the artwork spent "
                + "what the ROMs after it were owed");

        // And the ROMs still all landed, which is the half the reservation is protecting.
        Assert.Equal(3, outcome.Content.Downloaded);
    }

    // ------------------------------------------------------------------ the three fences

    [Fact]
    public async Task An_adopted_file_survives_a_rollback_because_it_is_the_users_own()
    {
        // Fence one. Adopted means the user's own ROM or their own scrape: it does not count
        // against the budget and eviction may never delete it, and a rollback is not a licence
        // to. MediaSync writes exactly this row for artwork a user's own scraper left behind.
        using var stub = Library((1, "Title (Disc 1).chd"), (2, "Title (Disc 2).chd"));
        stub.Content[2] = new byte[16];

        var scraped = Adopt(romId: 1, "roms/psx/images/Title (Disc 1)-image.png");

        var outcome = await RunAsync(stub);

        Assert.Equal(1, outcome.RolledBack);
        AssertGone(1, "Title (Disc 1).chd");

        // Untouched, bytes and row, even though it hangs off the rom id that was rolled back.
        Assert.True(File.Exists(_session.Install.Resolve(scraped)));
        Assert.NotNull(_session.Store.Files.Find(scraped));
    }

    [Fact]
    public async Task A_game_that_was_on_disk_before_the_run_is_not_this_runs_to_remove()
    {
        // Fence two. A step that entered as AlreadyPresent describes a file the sync found
        // rather than one it wrote, and a run that fails afterwards has not made it any less
        // present than it already was.
        using var stub = Library((1, "Title (Disc 1).chd"), (2, "Title (Disc 2).chd"));

        // Disc one from a previous run: on disk, recorded, verified.
        Place(romId: 1, "Title (Disc 1).chd");
        stub.Content[2] = new byte[16];

        var plan = PlanFor((1, "Title (Disc 1).chd"), (2, "Title (Disc 2).chd"));

        Assert.Equal(ContentAction.AlreadyPresent, plan.Steps[0].Action);

        var outcome = await ApplyAsync(stub, plan);

        Assert.Equal(1, outcome.Content.Failed);

        // Still there. The run did not place it and does not take it away.
        Assert.True(File.Exists(RomPath("Title (Disc 1).chd")));
        Assert.NotEmpty(_session.Store.Files.ForRom(1));
    }

    [Fact]
    public async Task A_row_never_outlives_its_bytes_so_a_file_that_cannot_go_keeps_its_row()
    {
        // Fence three, which is ContentSync's own rule read backwards: neither the file nor the
        // row may outlive the other. Removing the row from under a file that would not delete
        // leaves bytes nothing tracks, which neither the budget nor eviction can ever reach
        // again. Reported instead, by name.
        using var stub = Library((1, "Title (Disc 1).chd"), (2, "Title (Disc 2).chd"));
        stub.Content[2] = new byte[16];

        var plan = PlanFor((1, "Title (Disc 1).chd"), (2, "Title (Disc 2).chd"));

        // Held open with no sharing, which is how Windows refuses a delete. The rollback runs
        // while this handle is alive.
        var landed = RomPath("Title (Disc 1).chd");
        GameSyncOutcome outcome;

        // try/finally rather than a bare dispose after the assertions: a handle left open on a
        // file inside the temp tree outlives the test, and TempRetroBatTree's own teardown
        // swallows the refusal it then meets, so the leak would be silent.
        try
        {
            using var connection = Connect(stub);
            var sync = new GameSync(_session.Install, _session.Store, connection);
            var events = new List<SyncEvent>();

            var apply = sync.ApplyAsync(
                plan,
                new Immediate<SyncEvent>(reported =>
                {
                    events.Add(reported);

                    // Taken the moment disc one is committed and before disc two is reached,
                    // so the handle is held across the rollback rather than around the run.
                    if (reported is ContentProgressed starting
                        && starting.Progress.Progress is null
                        && starting.Progress.Step.Member.RomId == 2
                        && _held is null)
                    {
                        _held = new FileStream(landed, FileMode.Open, FileAccess.Read, FileShare.None);
                    }
                }),
                TestContext.Current.CancellationToken);

            outcome = await apply;
        }
        finally
        {
            _held?.Dispose();
            _held = null;
        }

        Assert.Equal(1, outcome.RolledBack);

        // The file could not go, so its row stayed with it and the reason was reported rather
        // than swallowed.
        Assert.NotEmpty(outcome.RollbackProblems);
        Assert.Contains(outcome.RollbackProblems, problem => problem.Contains("Title", StringComparison.Ordinal));
        Assert.True(File.Exists(landed));
        Assert.NotEmpty(_session.Store.Files.ForRom(1));
    }

    private FileStream? _held;

    // ------------------------------------------------------------------ fixture

    /// <summary>
    /// Nothing this run wrote for that game is left, bytes or rows.
    /// </summary>
    /// <remarks>
    /// Scoped to <see cref="FileOrigin.Synced"/> deliberately. An adopted row is the user's own
    /// file and surviving is the whole of fence one, so a helper that demanded the rom id have
    /// no rows at all would make the fence unassertable and would fail the test that proves it.
    /// </remarks>
    private void AssertGone(int romId, string fileName)
    {
        Assert.False(File.Exists(RomPath(fileName)), $"{fileName} is still on disk");
        Assert.DoesNotContain(_session.Store.Files.ForRom(romId), file => file.Origin == FileOrigin.Synced);
        Assert.Null(_session.Store.Downloads.Find(romId));
        Assert.False(
            File.Exists(_session.Install.Resolve(ContentPlanner.PartFor(romId))),
            $"the partial transfer for rom {romId} is still under partial/");
    }

    private string RomPath(string fileName) =>
        _session.Install.Resolve(RelativePath.Create($"roms/psx/{fileName}"));

    /// <summary>A file this run did not write, recorded as the user's own.</summary>
    private RelativePath Adopt(int romId, string relative)
    {
        var path = RelativePath.Create(relative);
        var absolute = _session.Install.Resolve(path);

        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllBytes(absolute, new byte[64]);

        _session.Store.Files.Record(new LocalFile
        {
            Path = path,
            Folder = "psx",
            RomId = romId,
            Kind = LocalFileKind.Image,
            FileName = path.Name,
            SizeBytes = 64,
            VerifiedAt = Now,
            VerifiedBy = VerifiedBy.Size,
            Origin = FileOrigin.Adopted,
        });

        return path;
    }

    /// <summary>A ROM a previous run left behind, on disk and recorded.</summary>
    private void Place(int romId, string fileName)
    {
        var path = RelativePath.Create($"roms/psx/{fileName}");
        var absolute = _session.Install.Resolve(path);

        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllBytes(absolute, new byte[1024]);

        _session.Store.Files.Record(new LocalFile
        {
            Path = path,
            Folder = "psx",
            RomId = romId,
            Kind = LocalFileKind.Rom,
            FileName = fileName,
            SizeBytes = 1024,
            ModifiedUtc = new FileInfo(absolute).LastWriteTimeUtc,
            VerifiedAt = Now,
            VerifiedBy = VerifiedBy.Size,
            Origin = FileOrigin.Synced,
        });
    }

    /// <summary>Cancels the run as the named game is reached, before its transfer starts.</summary>
    private static Func<CancellationTokenSource, SyncEvent, bool> StopBefore(int romId) =>
        (_, reported) => reported is ContentProgressed reported0
            && reported0.Progress.Progress is null
            && reported0.Progress.Step.Member.RomId == romId;

    private static RomMConnection Connect(StubRomMServer stub) =>
        new(new RomMClientOptions { Origin = new Uri("http://stub.invalid"), AccessToken = "rmm_test" }, stub);

    private Task<GameSyncOutcome> RunAsync(
        StubRomMServer stub,
        Func<CancellationTokenSource, SyncEvent, bool>? stopWhen = null)
    {
        var names = stub.Library.Select(rom => (rom.Id, rom.FsName)).ToArray();
        return ApplyAsync(stub, PlanFor(names), stopWhen);
    }

    private async Task<GameSyncOutcome> ApplyAsync(
        StubRomMServer stub,
        ContentPlan plan,
        Func<CancellationTokenSource, SyncEvent, bool>? stopWhen = null)
    {
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);

        using var connection = Connect(stub);

        return await new GameSync(_session.Install, _session.Store, connection).ApplyAsync(
            plan,
            new Immediate<SyncEvent>(reported =>
            {
                if (stopWhen is not null && stopWhen(stopping, reported))
                {
                    stopping.Cancel();
                }
            }),
            stopping.Token);
    }

    /// <summary>A library whose games all carry artwork, so the media half is exercised too.</summary>
    private static StubRomMServer Library(params (int RomId, string FsName)[] games)
    {
        var stub = new StubRomMServer();
        stub.Platforms.Add(new StubPlatform(1, "psx", "psx", "PlayStation"));

        foreach (var (romId, fsName) in games)
        {
            stub.Library.Add(new StubRom(
                romId,
                1,
                "psx",
                "psx",
                Path.GetFileNameWithoutExtension(fsName),
                fsName,
                "chd",
                1024)
            {
                Metadata = new StubRomMetadata(),
            });

            stub.Content[romId] = new byte[1024];
            stub.Media[$"/assets/romm/resources/roms/1/{romId}/cover/big.png"] = new byte[64];
        }

        return stub;
    }

    /// <summary>The plan a sync of these games would make, from a real planner.</summary>
    private ContentPlan PlanFor(params (int RomId, string FsName)[] games)
    {
        var set = _session.Store.SyncSets.Find("invariant") ?? _session.Store.SyncSets.Add(
            new SyncSetDefinition { Name = "invariant", Scope = CatalogScopeKind.Platform, ScopeValue = "1" },
            Now);

        var members = games.Select((game, index) => new SyncSetMember
        {
            RomId = game.RomId,
            State = MemberState.Member,
            Folder = "psx",
            PlatformSlug = "psx",
            FsName = game.FsName,
            FsExtension = "chd",
            SizeBytes = 1024,
            DisplayName = Path.GetFileNameWithoutExtension(game.FsName),
            SortKey = game.FsName.ToLowerInvariant(),
            Position = index + 1,
            ResolvedAt = Now,
        }).ToList();

        _session.Store.SyncSets.ReplaceMembers(set.Id, [.. members], $"{games.Length} games", Now, complete: true);

        foreach (var member in members)
        {
            _session.Store.Metadata.Record(new GameMetadata
            {
                RomId = member.RomId,
                Folder = "psx",
                FsName = member.FsName,
                Name = member.DisplayName,
                MediaPaths = new Dictionary<MediaKind, string>
                {
                    [MediaKind.Image] = $"/assets/romm/resources/roms/1/{member.RomId}/cover/big.png",
                },
            });
        }

        return new ContentPlanner(_session.Install, _session.Store).Plan(
            _session.Store.SyncSets.Find("invariant")!,
            members);
    }
}
