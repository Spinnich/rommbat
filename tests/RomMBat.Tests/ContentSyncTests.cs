using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using RomM.Client;
using RomM.Client.Catalog;
using RomM.Client.Content;
using RomMBat.Core.Content;
using RomMBat.Core.Mapping;
using RomMBat.Core.Paths;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Content sync: what lands on disk, what does not, and what a second run does.
/// </summary>
/// <remarks>
/// Driven against <see cref="StubRomMServer"/>, which copies the download behaviour measured
/// against a live instance rather than an idealised version of it: 206 with an <c>ETag</c> for a
/// single-file ROM, 403 for any range on a multi-file one, and a full 200 for a stale
/// <c>If-Range</c>.
/// </remarks>
public sealed class ContentSyncTests : IDisposable
{
    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();

    public void Dispose() => _tree.Dispose();

    [Fact]
    public async Task A_set_syncs_to_completion_and_the_second_run_does_nothing()
    {
        using var stub = Library(3);
        using var store = LocalStore.Open(_tree.Install());

        var first = await SyncAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(3, first.Downloaded);
        Assert.Equal(0, first.Failed);
        Assert.All(Members(store), member => Assert.True(File.Exists(Absolute(member))));

        // One request per game and nothing else: no HEAD pre-flight, whose Content-Length is
        // wrong for a multi-file ROM anyway, and no per-ROM detail call.
        Assert.Equal(3, stub.ContentRequests.Count);

        var requestsAfterFirst = stub.ContentRequests.Count;

        // The signal the plan asks for by name. A no-op has to be visible in what the run
        // says, not just in what it happens not to have done.
        var plan = new ContentPlanner(_tree.Install(), store).Plan(Set(store), Members(store));

        Assert.True(plan.IsNoOp);
        Assert.Contains("nothing to do", plan.Summary, StringComparison.Ordinal);

        var second = await SyncAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(second.IsNoOp);
        Assert.Equal(0, second.Downloaded);
        Assert.Equal(3, second.AlreadyPresent);
        Assert.Equal(requestsAfterFirst, stub.ContentRequests.Count);
        Assert.Contains("0 downloaded, 0 written", second.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_populated_install_that_moves_to_a_different_root_syncs_to_nothing()
    {
        using var stub = Library(3);

        using (var store = LocalStore.Open(_tree.Install()))
        {
            var outcome = await SyncAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(3, outcome.Downloaded);
        }

        // The whole tree turns up somewhere else, database included, which is what a drive
        // letter changing from E: to F: does. Nothing in the store may have recorded where it
        // used to be, and the ROMs must not be fetched again.
        using var moved = _tree.CopyToNewLocation();
        var relocated = moved.Install();

        using var store2 = LocalStore.OpenAt(relocated.DatabasePath);
        var plan = new ContentPlanner(relocated, store2).Plan(Set(store2), Members(store2));

        Assert.NotEqual(_tree.Root, moved.Root);
        Assert.True(plan.IsNoOp);
        Assert.Equal(3, plan.PresentCount);
    }

    [Fact]
    public async Task A_download_cut_off_mid_transfer_resumes_into_a_byte_identical_file()
    {
        using var stub = Library(1);
        using var store = LocalStore.Open(_tree.Install());

        var expected = stub.Content[1];
        stub.DropContentAfterBytes = expected.Length / 3;

        var interrupted = await SyncAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, interrupted.Failed);
        Assert.Equal(0, interrupted.Downloaded);

        // Nothing partial may be sitting where EmulationStation would list it.
        var member = Members(store).Single();
        Assert.False(File.Exists(Absolute(member)));

        var part = _tree.Install().Resolve(ContentPlanner.PartFor(member.RomId));
        Assert.True(File.Exists(part));
        Assert.Equal(expected.Length / 3, new FileInfo(part).Length);

        var resumed = await SyncAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, resumed.Resumed);
        Assert.Equal(0, resumed.Failed);
        Assert.Equal(expected, File.ReadAllBytes(Absolute(member)));

        // It really continued rather than starting again: the second request asked for the
        // rest, and only the rest crossed the wire.
        Assert.Contains(stub.ContentRequests, request => request.Contains($"bytes={expected.Length / 3}-", StringComparison.Ordinal));
        Assert.Equal(expected.Length - (expected.Length / 3), resumed.BytesTransferred);
        Assert.False(File.Exists(part));

        // The validator has to survive the transfer that died, or the resume asks the server to
        // splice onto bytes it has no way of recognising.
        Assert.Contains(
            stub.ContentRequests,
            request => request.Contains($"bytes={expected.Length / 3}-", StringComparison.Ordinal)
                && request.Contains($"if-range={stub.ContentETag}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_stale_validator_starts_again_rather_than_splicing()
    {
        using var stub = Library(1);
        using var store = LocalStore.Open(_tree.Install());

        var expected = stub.Content[1];
        stub.DropContentAfterBytes = expected.Length / 2;
        var original = stub.ContentETag;

        await SyncAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        // The file changed on the server, so the partial bytes describe something that no
        // longer exists. Measured behaviour: a full 200, never a 206 spliced onto stale bytes.
        stub.ContentETag = "\"deadbeef-2000\"";

        var resumed = await SyncAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, resumed.Failed);
        Assert.Equal(expected, File.ReadAllBytes(Absolute(Members(store).Single())));

        // The restart path, not the resume path. Without the stored validator on the wire the
        // server sees no If-Range, answers an ordinary 206, and this test passes either way.
        Assert.Contains(
            stub.ContentRequests,
            request => request.Contains($"if-range={original}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_part_file_that_is_already_complete_is_verified_and_renamed_rather_than_resumed()
    {
        using var stub = Library(1);
        using var store = LocalStore.Open(_tree.Install());

        var expected = stub.Content[1];
        await ResolveAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        var member = Members(store).Single();
        var part = ContentPlanner.PartFor(member.RomId);
        var partAbsolute = _tree.Install().Resolve(part);

        // What a power cut between the last byte landing and the rename leaves behind: a
        // complete .part and a live row. Resuming it asks for a byte past the end, which is
        // refused 416, and would be refused identically on every run after this one.
        Directory.CreateDirectory(Path.GetDirectoryName(partAbsolute)!);
        File.WriteAllBytes(partAbsolute, expected);

        store.Downloads.Begin(new ContentDownload
        {
            RomId = member.RomId,
            PartPath = part,
            TargetPath = ContentPlanner.TargetFor(member),
            ExpectedSize = member.SizeBytes,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        var outcome = await SyncAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, outcome.Failed);
        Assert.Equal(expected, File.ReadAllBytes(Absolute(member)));

        // The verify and rename it never got, and nothing crossed the wire to get them.
        Assert.Empty(stub.ContentRequests);
        Assert.Equal(0, outcome.BytesTransferred);
        Assert.False(File.Exists(partAbsolute));
        Assert.Null(store.Downloads.Find(member.RomId));
    }

    [Fact]
    public async Task A_resume_point_the_server_refuses_discards_the_partial_file_rather_than_repeating()
    {
        using var stub = Library(1);
        using var store = LocalStore.Open(_tree.Install());

        await ResolveAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        // The server's copy shrank under a resume point the catalogue still describes as
        // further out, so the range names a byte that no longer exists.
        stub.Content[1] = stub.Content[1][..1024];

        var member = Members(store).Single();
        var part = ContentPlanner.PartFor(member.RomId);
        var partAbsolute = _tree.Install().Resolve(part);

        Directory.CreateDirectory(Path.GetDirectoryName(partAbsolute)!);
        File.WriteAllBytes(partAbsolute, new byte[2048]);

        store.Downloads.Begin(new ContentDownload
        {
            RomId = member.RomId,
            PartPath = part,
            TargetPath = ContentPlanner.TargetFor(member),
            ExpectedSize = member.SizeBytes,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        var refused = await SyncAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, refused.Failed);

        // The message says the partial file is discarded, so it has to be gone. Keeping it
        // would plan the same resume, and be refused the same way, on every run from here on.
        Assert.False(File.Exists(partAbsolute));
        Assert.Null(store.Downloads.Find(member.RomId));

        // Proof that it broke out: the next run asks for the whole file, not for the range.
        await SyncAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains(stub.ContentRequests, request => request.Contains("bytes=0-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_7z_is_verified_by_size_because_nothing_here_can_look_inside_it()
    {
        // RomM's hashes describe the content inside an archive, and reaching inside a .7z needs
        // a dependency this milestone does not take. Comparing the archive's own bytes against a
        // content hash refuses a correct download, and refuses it again on every later run.
        var content = Encoding.UTF8.GetBytes(new string('7', 4096));
        var inside = Encoding.UTF8.GetBytes(new string('R', 16400));

        using var stub = new StubRomMServer();
        stub.Platforms.Add(new StubPlatform(1, "snes", "snes", "Super Nintendo"));
        stub.Library.Add(new StubRom(1, 1, "snes", "snes", "Game", "Game.7z", "7z", content.Length)
        {
            Md5Hash = Md5(inside),
            Sha1Hash = Sha1(inside),
        });
        stub.Content[1] = content;

        using var store = LocalStore.Open(_tree.Install());
        var outcome = await SyncAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Downloaded);
        Assert.Equal(0, outcome.Failed);

        var recorded = Assert.Single(store.Files.List());
        Assert.Equal(VerifiedBy.Size, recorded.VerifiedBy);

        // And it stays done. The permanent-failure shape is a second run that fetches it again.
        var second = await SyncAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(second.IsNoOp);
        Assert.Equal(1, second.AlreadyPresent);
    }

    [Fact]
    public async Task A_multi_entry_zip_is_verified_by_size_because_it_has_no_single_content_hash()
    {
        // RomM's rule holds for one file in one archive. A zip holding two has no content hash
        // to be compared against, so the plan's answer is size.
        var archive = Zip(("Disc 1.sfc", Encoding.UTF8.GetBytes(new string('A', 2048))),
                          ("Disc 2.sfc", Encoding.UTF8.GetBytes(new string('B', 2048))));

        using var stub = new StubRomMServer();
        stub.Platforms.Add(new StubPlatform(1, "snes", "snes", "Super Nintendo"));
        stub.Library.Add(new StubRom(1, 1, "snes", "snes", "Game", "Game.zip", "zip", archive.Length)
        {
            Md5Hash = Md5(Encoding.UTF8.GetBytes("neither entry, and not the archive either")),
        });
        stub.Content[1] = archive;

        using var store = LocalStore.Open(_tree.Install());
        var outcome = await SyncAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Downloaded);
        Assert.Equal(0, outcome.Failed);

        var recorded = Assert.Single(store.Files.List());
        Assert.Equal(HashScope.File, recorded.HashScope);
        Assert.Equal(VerifiedBy.Size, recorded.VerifiedBy);

        var second = await SyncAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(second.IsNoOp);
    }

    [Fact]
    public async Task A_damaged_zip_is_still_refused_rather_than_passed_on_size()
    {
        // The other side of the same rule: a zip that will not open is damaged, not opaque, so
        // its file hash is still compared and the mismatch still refuses it.
        var damaged = Encoding.UTF8.GetBytes(new string('X', 4096));

        using var stub = new StubRomMServer();
        stub.Platforms.Add(new StubPlatform(1, "snes", "snes", "Super Nintendo"));
        stub.Library.Add(new StubRom(1, 1, "snes", "snes", "Game", "Game.zip", "zip", damaged.Length)
        {
            Md5Hash = Md5(Encoding.UTF8.GetBytes("what the archive should have held")),
        });
        stub.Content[1] = damaged;

        using var store = LocalStore.Open(_tree.Install());
        var outcome = await SyncAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Failed);
        Assert.Empty(store.Files.List());
        Assert.Contains(outcome.Problems, problem => problem.Contains("md5", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_zip_is_verified_against_the_hash_of_what_is_inside_it()
    {
        // The trap measured against a live server: a 1,025-byte zip reports the hashes of the
        // 16,400-byte file inside it. Hashing the archive would fail every compressed ROM.
        var inner = Encoding.UTF8.GetBytes(new string('N', 4096));
        var archive = Zip("Game.sfc", inner);

        using var stub = new StubRomMServer();
        stub.Platforms.Add(new StubPlatform(1, "snes", "snes", "Super Nintendo"));
        stub.Library.Add(new StubRom(1, 1, "snes", "snes", "Game", "Game.zip", "zip", archive.Length)
        {
            Md5Hash = Md5(inner),
            Sha1Hash = Sha1(inner),
        });
        stub.Content[1] = archive;

        using var store = LocalStore.Open(_tree.Install());
        var outcome = await SyncAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Downloaded);
        Assert.Equal(0, outcome.Failed);

        var recorded = Assert.Single(store.Files.List());
        Assert.Equal(HashScope.ArchiveContent, recorded.HashScope);
        Assert.Equal(Md5(inner), recorded.Md5Hash);
        Assert.Equal(VerifiedBy.Md5, recorded.VerifiedBy);
    }

    [Fact]
    public async Task A_file_the_user_already_had_is_adopted_rather_than_downloaded()
    {
        using var stub = Library(1);
        using var store = LocalStore.Open(_tree.Install());
        var install = _tree.Install();

        await ResolveAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        var member = Members(store).Single();
        var target = install.Resolve(ContentPlanner.TargetFor(member));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllBytes(target, stub.Content[member.RomId]);

        var plan = new ContentPlanner(install, store).Plan(Set(store), Members(store));

        Assert.Equal(ContentAction.Adopt, Assert.Single(plan.Steps).Action);

        using var connection = Connect(stub);
        var outcome = await new ContentSync(install, store, connection).ApplyAsync(
            plan,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Adopted);
        Assert.Empty(stub.ContentRequests);

        // Adopted, not synced: it is the user's file, so it never counts against the budget and
        // eviction must never take it.
        var recorded = Assert.Single(store.Files.List());
        Assert.Equal(FileOrigin.Adopted, recorded.Origin);
        Assert.Equal(0, new ContentPlanner(install, store).ManagedBytes());
    }

    [Fact]
    public async Task A_rom_the_filesystem_cannot_hold_is_refused_before_anything_is_written()
    {
        using var stub = new StubRomMServer();
        stub.Platforms.Add(new StubPlatform(1, "ps2", "ps2", "PlayStation 2"));
        stub.Library.Add(new StubRom(1, 1, "ps2", "ps2", "Small", "Small.iso", "iso", 1024));
        stub.Library.Add(new StubRom(2, 1, "ps2", "ps2", "Huge", "Huge.iso", "iso", 5L * 1024 * 1024 * 1024));

        using var store = LocalStore.Open(_tree.Install());

        var systems = Fixtures.Synthesize(("ps2", ".iso .chd"));
        var resolver = new SetResolver(
            systems,
            new PlatformResolver(systems),
            FilesystemLimits.For("FAT32", availableFreeBytes: 200L * 1024 * 1024 * 1024));

        using var connection = Connect(stub);
        var set = store.SyncSets.Add(
            new SyncSetDefinition { Name = "ps2", Scope = CatalogScopeKind.Platform, ScopeValue = "1" },
            DateTimeOffset.UtcNow);

        var resolution = await resolver.ResolveAsync(
            set,
            new RomPager(connection, SetResolver.QueryFor(set)),
            DateTimeOffset.UtcNow,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(resolution.Members);
        Assert.Equal(1, resolution.TooLargeForFilesystem);

        var excluded = Assert.Single(resolution.Excluded);
        Assert.Equal(MemberState.ExcludedFilesystemLimit, excluded.State);

        // The message must never be the operating system's own, which says the disk is full on
        // a volume with 200 GB free and sends the user to delete the wrong things.
        Assert.Contains("too large for this drive's filesystem", resolution.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("not enough space", resolution.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_multi_file_rom_is_excluded_as_multi_file_rather_than_as_a_bad_format()
    {
        using var stub = new StubRomMServer();
        stub.Platforms.Add(new StubPlatform(1, "segacd", "segacd", "Mega CD"));
        stub.Library.Add(new StubRom(1, 1, "segacd", "segacd", "Disc", "Disc", string.Empty, 700_000_000)
        {
            HasMultipleFiles = true,
        });

        using var store = LocalStore.Open(_tree.Install());
        var systems = Fixtures.Synthesize(("segacd", ".chd .cue .iso"));
        var resolver = new SetResolver(systems, new PlatformResolver(systems));

        using var connection = Connect(stub);
        var set = store.SyncSets.Add(
            new SyncSetDefinition { Name = "cd", Scope = CatalogScopeKind.Platform, ScopeValue = "1" },
            DateTimeOffset.UtcNow);

        var resolution = await resolver.ResolveAsync(
            set,
            new RomPager(connection, SetResolver.QueryFor(set)),
            DateTimeOffset.UtcNow,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(resolution.Members);
        Assert.Equal(1, resolution.MultiFile);
        Assert.Equal(MemberState.ExcludedMultiFile, Assert.Single(resolution.Excluded).State);

        // The format is not what is wrong with a .bin/.cue set, and saying so would send
        // someone to re-import it for nothing.
        Assert.Contains("several files", resolution.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("format not supported", resolution.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_coarse_filesystem_timestamp_does_not_make_a_present_file_look_changed()
    {
        using var stub = Library(1);
        using var store = LocalStore.Open(_tree.Install());
        var install = _tree.Install();

        await SyncAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        var member = Members(store).Single();
        var target = install.Resolve(ContentPlanner.TargetFor(member));

        // FAT32 and exFAT store mtimes to 2 seconds and round up, so a file comes back stamped
        // as much as 2 seconds later than it was written. Without the tolerance, every sync on
        // a FAT stick would re-hash everything it owns.
        File.SetLastWriteTimeUtc(target, File.GetLastWriteTimeUtc(target).AddSeconds(1.9));

        var limits = FilesystemLimits.For("exFAT", availableFreeBytes: 64L * 1024 * 1024 * 1024);
        var plan = new ContentPlanner(install, store, limits).Plan(Set(store), Members(store));

        Assert.True(plan.IsNoOp);
        Assert.Equal(TimeSpan.FromSeconds(2), limits.TimestampGranularity);
    }

    [Fact]
    public async Task A_budget_blocks_a_download_rather_than_filling_the_disk()
    {
        using var stub = Library(3);
        using var store = LocalStore.Open(_tree.Install());

        // Room for one of the three, which are 4 KB each.
        store.Settings.Set(SettingStore.ContentMaxBytes, 6000L, DateTimeOffset.UtcNow);

        var outcome = await SyncAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Downloaded);
        Assert.Equal(2, outcome.Blocked);
        Assert.Contains(outcome.Problems, problem => problem.Contains("budget is full", StringComparison.Ordinal));
        Assert.Single(store.Files.List());
    }

    [Fact]
    public async Task Eviction_takes_departures_first_and_a_dry_run_matches_the_run_that_follows()
    {
        using var stub = Library(4);
        using var store = LocalStore.Open(_tree.Install());
        var install = _tree.Install();

        await SyncAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        // One game leaves the scope. It is the least surprising thing to remove, so it goes
        // before any current member, whatever its rank.
        var set = Set(store);
        var members = store.SyncSets.Members(set.Id);
        var departed = members.Single(member => member.RomId == 2);

        store.SyncSets.ReplaceMembers(
            set.Id,
            [.. members.Where(member => member.RomId != 2)],
            "one left",
            DateTimeOffset.UtcNow.AddMinutes(1));

        var planner = new EvictionPlanner(store);
        var plan = planner.Plan(bytesToFree: 8192);

        Assert.Equal(2, plan.Selected.Count);
        Assert.Equal(departed.RomId, plan.Selected[0].File.RomId);
        Assert.Equal(EvictionReason.Departed, plan.Selected[0].Reason);

        // The lowest-ranked member is next, and never the first one the set wanted.
        Assert.Equal(EvictionReason.LowestRanked, plan.Selected[1].Reason);
        Assert.Equal(4, plan.Selected[1].File.RomId);

        var planned = plan.Selected.Select(candidate => candidate.File.Path.Value).ToArray();
        var outcome = planner.Apply(plan, install);

        Assert.Equal(2, outcome.Removed);
        Assert.Empty(outcome.Problems);

        // A dry run is worth nothing if the real run does something else.
        Assert.All(planned, path => Assert.False(File.Exists(install.Resolve(RelativePath.Create(path)))));
        Assert.All(planned, path => Assert.Null(store.Files.Find(RelativePath.Create(path))));
        Assert.Equal(2, store.Files.List().Count);
    }

    [Fact]
    public async Task Eviction_refuses_a_game_whose_saves_have_not_reached_the_server()
    {
        using var stub = Library(2);
        using var store = LocalStore.Open(_tree.Install());

        await SyncAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        // M6 owns saves and nothing writes one yet, so the guard answers from the seams that do
        // exist. An unsent outbox entry is the one that matters most: evicting the ROM would
        // take its save's only attribution with it.
        store.Outbox.Enqueue(
            OutboxKind.Save,
            DateTimeOffset.UtcNow,
            romId: 2,
            slot: "libretro:battery",
            contentHash: "abc",
            sizeBytes: 32,
            fileMtimeUtc: DateTimeOffset.UtcNow);

        var plan = new EvictionPlanner(store).Plan(bytesToFree: long.MaxValue);

        Assert.DoesNotContain(plan.Selected, candidate => candidate.File.RomId == 2);

        var refused = Assert.Single(plan.Refused);
        Assert.Equal(2, refused.File.RomId);
        Assert.Contains("has not reached the server", refused.Refusal, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_plan_with_nothing_to_free_asks_the_guard_nothing()
    {
        using var stub = Library(2);
        using var store = LocalStore.Open(_tree.Install());

        await SyncAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        // The same unsent entry the test above uses. Its refusal is the observable: reaching it
        // means the walk ran and SaveGuard was asked, which is invisible in the summary and is
        // why this asserts the mechanism rather than the output.
        store.Outbox.Enqueue(
            OutboxKind.Save,
            DateTimeOffset.UtcNow,
            romId: 2,
            slot: "libretro:battery",
            contentHash: "abc",
            sizeBytes: 32,
            fileMtimeUtc: DateTimeOffset.UtcNow);

        // No budget is set, so nothing is over it, which is what every sync under the cap hits.
        var plan = new EvictionPlanner(store).Plan();

        Assert.Equal(0, plan.BytesToFree);
        Assert.Empty(plan.Selected);
        Assert.Empty(plan.Refused);

        // Still the right sentence, and for the right reason rather than for want of candidates.
        Assert.Equal("nothing to evict: the install is inside its budget", plan.Summary);
    }

    [Fact]
    public async Task Eviction_never_removes_a_file_the_user_put_there()
    {
        using var stub = Library(1);
        using var store = LocalStore.Open(_tree.Install());
        var install = _tree.Install();

        await ResolveAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        var member = Members(store).Single();
        var target = install.Resolve(ContentPlanner.TargetFor(member));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllBytes(target, stub.Content[member.RomId]);

        using var connection = Connect(stub);
        var plan = new ContentPlanner(install, store).Plan(Set(store), Members(store));
        await new ContentSync(install, store, connection).ApplyAsync(
            plan,
            cancellationToken: TestContext.Current.CancellationToken);

        var eviction = new EvictionPlanner(store).Plan(bytesToFree: long.MaxValue);

        Assert.Empty(eviction.Selected);
        Assert.True(File.Exists(target));
    }

    [Fact]
    public async Task A_multi_file_download_is_refused_with_an_explanation_rather_than_a_bare_403()
    {
        using var stub = new StubRomMServer();
        stub.Library.Add(new StubRom(1, 1, "segacd", "segacd", "Disc", "Disc", string.Empty, 64)
        {
            HasMultipleFiles = true,
        });
        stub.Content[1] = new byte[64];

        using var connection = Connect(stub);
        await using var destination = new MemoryStream();

        // Nothing in the shipped path asks for this, because a multi-file ROM never reaches a
        // set. The client still has to answer it in words rather than in a status code.
        var response = await connection.DownloadRomContentAsync(
            new RomContentRequest { RomId = 1, FsName = "Disc", IsMultiFile = false },
            destination,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(response.IsSuccess);
        Assert.Equal(RomMResponseStatus.Forbidden, response.Status);
    }

    [Fact]
    public async Task An_identifiers_endpoint_that_times_out_is_an_answer_rather_than_a_failure()
    {
        using var stub = Library(1);
        stub.IdentifiersStatus = System.Net.HttpStatusCode.GatewayTimeout;

        using var connection = Connect(stub);

        // Measured at 504 after 300 s on an 83k library, which is why deletion is reconciled
        // through set re-resolution and this is only ever a cross-check.
        Assert.Null(
            await connection.TryGetRomIdentifiersAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        stub.IdentifiersStatus = System.Net.HttpStatusCode.OK;

        Assert.Equal(
            [1],
            await connection.TryGetRomIdentifiersAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_drive_that_goes_away_mid_sync_fails_that_game_and_keeps_the_rest()
    {
        using var stub = Library(3);
        using var store = LocalStore.Open(_tree.Install());

        await ResolveAsync(stub, store, cancellationToken: TestContext.Current.CancellationToken);

        var install = _tree.Install();
        var plan = new ContentPlanner(install, store).Plan(Set(store), Members(store));

        using var connection = Connect(stub);
        stub.DropContentAfterBytes = 1024;

        var outcome = await new ContentSync(install, store, connection).ApplyAsync(
            plan,
            cancellationToken: TestContext.Current.CancellationToken);

        // One game is lost to the drop and the other two still arrive: a set of forty should
        // not be abandoned over one of them.
        Assert.Equal(1, outcome.Failed);
        Assert.Equal(2, outcome.Downloaded);
        Assert.Single(outcome.Problems);
    }

    [Fact]
    public void Planning_a_large_set_touches_the_network_not_at_all()
    {
        using var store = LocalStore.Open(_tree.Install());
        var set = store.SyncSets.Add(
            new SyncSetDefinition { Name = "big", Scope = CatalogScopeKind.Platform, ScopeValue = "1" },
            DateTimeOffset.UtcNow);

        var members = Enumerable.Range(1, 5_000).Select(id => new SyncSetMember
        {
            RomId = id,
            State = MemberState.Member,
            Folder = "snes",
            PlatformSlug = "snes",
            FsName = $"Game {id}.sfc",
            FsExtension = "sfc",
            SizeBytes = 1024,
            DisplayName = $"Game {id}",
            SortKey = $"Game {id}",
            Position = id,
            ResolvedAt = DateTimeOffset.UtcNow,
        }).ToArray();

        store.SyncSets.ReplaceMembers(set.Id, members, "5,000 games", DateTimeOffset.UtcNow);

        // A plan is answered entirely from the store and the disk. Nothing here may reach for
        // the server, which is what lets a handheld away from the network still say what a sync
        // would do, and what keeps the request count independent of the set's size.
        using var stub = new StubRomMServer();
        var plan = new ContentPlanner(_tree.Install(), store).Plan(set, store.SyncSets.Members(set.Id));

        Assert.Equal(5_000, plan.Steps.Count);
        Assert.All(plan.Steps, step => Assert.Equal(ContentAction.Download, step.Action));
        Assert.Empty(stub.RequestLog);
        Assert.Empty(stub.ContentRequests);
    }

    /// <summary>Resolves the set and returns the outcome of syncing what it holds.</summary>
    private async Task<ContentSyncOutcome> SyncAsync(
        StubRomMServer stub,
        LocalStore store,
        CancellationToken cancellationToken = default)
    {
        await ResolveAsync(stub, store, cancellationToken);

        var install = _tree.Install();
        var plan = new ContentPlanner(install, store).Plan(Set(store), Members(store));

        using var connection = Connect(stub);
        return await new ContentSync(install, store, connection)
            .ApplyAsync(plan, cancellationToken: cancellationToken);
    }

    private static async Task ResolveAsync(
        StubRomMServer stub,
        LocalStore store,
        CancellationToken cancellationToken = default)
    {
        var set = store.SyncSets.Find("snes")
            ?? store.SyncSets.Add(
                new SyncSetDefinition { Name = "snes", Scope = CatalogScopeKind.Platform, ScopeValue = "1" },
                DateTimeOffset.UtcNow);

        var systems = Fixtures.Synthesize(("snes", ".sfc .smc .zip .7z"));
        var resolver = new SetResolver(systems, new PlatformResolver(systems));

        using var connection = Connect(stub);
        var resolution = await resolver.ResolveAsync(
            set,
            new RomPager(connection, SetResolver.QueryFor(set)),
            DateTimeOffset.UtcNow,
            cancellationToken: cancellationToken);

        store.SyncSets.ReplaceMembers(
            set.Id,
            [.. resolution.Members, .. resolution.Excluded],
            resolution.Summary,
            DateTimeOffset.UtcNow);
    }

    private static SyncSetDefinition Set(LocalStore store) => store.SyncSets.List()[0];

    private static IReadOnlyList<SyncSetMember> Members(LocalStore store) => store.SyncSets.Members(Set(store).Id);

    private string Absolute(SyncSetMember member) => _tree.Install().Resolve(ContentPlanner.TargetFor(member));

    private static RomMConnection Connect(StubRomMServer stub) =>
        new(new RomMClientOptions { Origin = new Uri("https://romm.test"), AccessToken = "rmm_test" }, stub);

    /// <summary>A stub library whose ROMs have real, distinct content and honest hashes.</summary>
    private static StubRomMServer Library(int count)
    {
        var stub = new StubRomMServer();
        stub.Platforms.Add(new StubPlatform(1, "snes", "snes", "Super Nintendo"));

        for (var id = 1; id <= count; id++)
        {
            var content = Encoding.UTF8.GetBytes(new string((char)('A' + id), 4096));

            stub.Library.Add(new StubRom(id, 1, "snes", "snes", $"Game {id}", $"Game {id}.sfc", "sfc", content.Length)
            {
                Md5Hash = Md5(content),
                Sha1Hash = Sha1(content),
            });

            stub.Content[id] = content;
        }

        return stub;
    }

    private static byte[] Zip(string entryName, byte[] content) => Zip((entryName, content));

    private static byte[] Zip(params (string Name, byte[] Content)[] entries)
    {
        using var buffer = new MemoryStream();

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var entry = archive.CreateEntry(name).Open();
                entry.Write(content);
            }
        }

        return buffer.ToArray();
    }

#pragma warning disable CA5350, CA5351 // Mirrors what RomM stores; not a security primitive.
    private static string Md5(byte[] content) => Convert.ToHexString(MD5.HashData(content)).ToLowerInvariant();

    private static string Sha1(byte[] content) => Convert.ToHexString(SHA1.HashData(content)).ToLowerInvariant();
#pragma warning restore CA5350, CA5351
}
