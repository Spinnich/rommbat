using RomMBat.Core.Sync;
using RomMBat.Core.Paths;
using RomM.Client;
using RomM.Client.Catalog;
using RomM.Client.Content;
using RomMBat.Core;
using RomMBat.Core.Content;
using RomMBat.Core.Metadata;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Sets;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The order a sync does its work in, asserted rather than described.
/// </summary>
/// <remarks>
/// <b>Three of these orderings are data-loss guards, and they are asserted through the seam.</b>
/// Observing statement order through a console means comparing the positions of two printed
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
/// <b>A game's artwork goes ahead of the next game's ROM.</b> As one pass after every ROM of
/// every set, media lets a budget that runs out strip the artwork off the whole library rather
/// than truncating its tail (#102). The media pass is gated on being online, so a runner
/// passing <c>SyncOptions(Offline: true)</c> never sees <see cref="SyncPass.Media"/> in the
/// observed sequence and stays green through exactly the change it exists to catch. Every
/// ordering claim about artwork below is therefore taken from a run <b>with a connection</b>.
/// </para>
/// <para>
/// <b>Passes do not occupy disjoint stretches of a run, so the sequence check is over first
/// occurrences.</b> Content and Media interleave by design. What may never happen is a pass
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
    public async Task Startup_sync_resolves_enabled_sets_without_replacing_an_existing_ROM()
    {
        using var stub = Library(2);
        using var connection = new RomMConnection(
            new RomMClientOptions { Origin = new Uri("http://stub.invalid"), AccessToken = "rmm_test" }, stub);
        var service = StartupService();
        var progress = new Immediate<SyncEvent>(_ => { });
        var token = TestContext.Current.CancellationToken;
        _session.Store.SyncSets.Add(new SyncSetDefinition
        {
            Name = "disabled", Scope = CatalogScopeKind.Platform, ScopeValue = "1", Enabled = false,
        }, Now);
        Assert.Empty((await service.RunAtStartupAsync(connection, progress, token)).Ran);
        Assert.Empty(stub.ContentRequests);

        var enabled = Set(games: 3);
        var existing = _session.Install.Resolve("roms/psx/Game 1.chd");
        Directory.CreateDirectory(Path.GetDirectoryName(existing)!);
        File.WriteAllText(existing, "existing local ROM");
        var config = _session.Install.Resolve("emulationstation/.emulationstation/es_systems.cfg");
        var original = File.ReadAllBytes(config);
        var report = await service.RunAtStartupAsync(connection, progress, token);

        Assert.Contains(SyncPass.Resolve, report.Ran);
        Assert.Contains(SyncPass.Content, report.Ran);
        Assert.DoesNotContain(SyncPass.Hooks, report.Ran);
        Assert.Equal("existing local ROM", File.ReadAllText(existing));
        Assert.True(File.Exists(_session.Install.Resolve("roms/psx/Game 2.chd")));
        Assert.False(File.Exists(_session.Install.Resolve("roms/psx/Game 3.chd")));
        Assert.Single(_session.Store.SyncSets.Members(enabled.Id, MemberState.Departed));
        Assert.Equal(original, File.ReadAllBytes(config));
    }

    [Fact]
    public async Task Startup_reserves_the_full_managed_size_of_a_resumed_rom_before_fetching_artwork()
    {
        using var stub = Library(2);
        using var connection = Connect(stub);
        Set(games: 2);
        var install = _session.Install;
        var part = ContentPlanner.PartFor(2);
        Directory.CreateDirectory(Path.GetDirectoryName(install.Resolve(part))!);
        File.WriteAllBytes(install.Resolve(part), stub.Content[2]);
        _session.Store.Downloads.Begin(new ContentDownload
        {
            RomId = 2, PartPath = part, TargetPath = RelativePath.Create("roms/psx/Game 2.chd"),
            ExpectedSize = 1024, UpdatedAt = Now,
        });
        _session.Store.Settings.Set(SettingStore.ContentMaxBytes, "2048", Now);

        await StartupService().RunAtStartupAsync(
            connection, new Immediate<SyncEvent>(_ => { }), TestContext.Current.CancellationToken);

        Assert.Equal(2048, _session.Store.Files.SyncedBytes());
        Assert.Equal(2, _session.Store.Files.List("psx", LocalFileKind.Rom).Count);
        Assert.DoesNotContain(_session.Store.Files.List(), file => file.Kind.IsMedia());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Packed_playlist_cannot_push_managed_content_over_the_cap(bool startup)
    {
        using var stub = PlayStationSet();
        using var connection = Connect(stub);
        var set = Set();
        _session.Store.Settings.Set(SettingStore.ContentMaxBytes, "10000", Now);
        var token = TestContext.Current.CancellationToken;
        var progress = new Immediate<SyncEvent>(_ => { });
        var service = StartupService();
        var report = startup
            ? await service.RunAtStartupAsync(connection, progress, token)
            : await service.RunAsync([set], new SyncOptions(), connection, progress, cancellationToken: token);

        Assert.True(_session.Store.Files.SyncedBytes() <= 10000);
        Assert.Equal(SyncState.Blocked, report.State);
        Assert.False(File.Exists(PlayStationFile("Game 1.m3u")));
        Assert.Equal(startup ? 2 : 0, _session.Store.Downloads.ForRom(1).Count);
    }

    [Fact]
    public async Task Startup_sync_installs_a_multifile_PlayStation_member()
    {
        using var stub = PlayStationSet();
        using var connection = Connect(stub);
        Set(games: 1);

        var report = await StartupService().RunAtStartupAsync(
            connection, new Immediate<SyncEvent>(_ => { }), TestContext.Current.CancellationToken);

        Assert.Equal(SyncState.Done, report.State);
        Assert.Equal(2, stub.ContentRequests.Count);
        Assert.Equal("Disc 1.chd\nDisc 2.chd", File.ReadAllText(PlayStationFile("Game 1.m3u")));
        Assert.Contains("Game 1/Game 1.m3u", File.ReadAllText(_session.Install.Resolve("roms/psx/gamelist.xml")));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Restart_recovers_a_packed_ROM_stopped_during_off_menu_staging(bool published, bool changed)
    {
        using var stub = PlayStationSet();
        using var connection = Connect(stub);
        var token = TestContext.Current.CancellationToken;
        Set();
        stub.DropMemberAfterBytes = (12, 1500);
        var progress = new Immediate<SyncEvent>(_ => { });
        Assert.Equal(SyncState.Incomplete,
            (await StartupService().RunAtStartupAsync(connection, progress, token)).State);

        var install = _session.Install;
        var downloads = _session.Store.Downloads.ForRom(1);
        var stage = install.Resolve(PackedRomStage.For(1));
        Directory.CreateDirectory(stage);
        var receipts = new List<PackedRomStage.Entry>();
        foreach (var disc in published ? downloads : downloads.Take(1))
        {
            var part = install.Resolve(disc.PartPath);
            if (published)
            {
                File.WriteAllBytes(part, Enumerable.Repeat((byte)(disc.FileId - 10), 5000).ToArray());
            }

            var staged = Path.Combine(stage, disc.TargetPath.Name);
            File.Move(part, staged);
            receipts.Add(PackageReceipt(disc.FileId, staged));
        }
        Assert.False(File.Exists(PlayStationFile("Disc 1.chd")));
        if (published)
        {
            var generated = Path.Combine(stage, "Game 1.m3u");
            File.WriteAllText(generated, "Disc 1.chd\nDisc 2.chd");
            receipts.Add(PackageReceipt(0, generated));
            PackedRomStage.WriteManifest(stage, receipts);
            Directory.CreateDirectory(install.Resolve("roms/psx"));
            Directory.Move(stage, install.Resolve("roms/psx/Game 1"));
            if (changed)
            {
                File.WriteAllBytes(PlayStationFile("Disc 1.chd"), new byte[5000]);
            }
        }

        using var reopened = InstallSession.Open(install.RootPath).Session!;
        var sync = new LibrarySyncService(reopened, _ => Task.FromResult(EsCallResult.Answered));
        var restarted = await sync.RunAtStartupAsync(connection, progress, token);
        if (changed)
        {
            Assert.Equal(SyncState.Blocked, restarted.State);
            Assert.Equal(new byte[5000], File.ReadAllBytes(PlayStationFile("Disc 1.chd")));
            Assert.Empty(reopened.Store.Files.ForRom(1));
            Assert.Equal(2, stub.ContentRequests.Count);
            return;
        }

        Assert.Equal(SyncState.Done, restarted.State);
        Assert.Equal(published ? 2 : 3, stub.ContentRequests.Count);
        if (!published)
        {
            Assert.Contains("bytes=1500-", stub.ContentRequests[^1], StringComparison.Ordinal);
        }

        Assert.False(Directory.Exists(stage));
        Assert.Equal("Disc 1.chd\nDisc 2.chd", File.ReadAllText(PlayStationFile("Game 1.m3u")));
        Assert.Empty(reopened.Store.Downloads.List());
        var indexed = reopened.Store.Files.ForRom(1).Where(file => file.Kind is LocalFileKind.Rom or LocalFileKind.RomPart);
        Assert.Equal(3, indexed.Count());
        Assert.All(indexed, file => Assert.Equal(FileOrigin.Synced, file.Origin));
    }

    private static PackedRomStage.Entry PackageReceipt(int fileId, string path)
    {
        var hash = ContentHasher.ComputeFileMd5(path);
        return new(fileId, Path.GetFileName(path), new(hash, hash, HashScope.File, VerifiedBy.Md5));
    }

    [Fact]
    public async Task Resumable_packed_members_get_free_space_credit_but_still_count_toward_the_budget()
    {
        using var stub = PlayStationSet();
        using var connection = Connect(stub);
        var token = TestContext.Current.CancellationToken;
        var set = Set();
        stub.DropMemberAfterBytes = (12, 1500);
        var progress = new Immediate<SyncEvent>(_ => { });
        Assert.Equal(SyncState.Incomplete,
            (await StartupService().RunAtStartupAsync(connection, progress, token)).State);
        var install = _session.Install;
        _session.Store.Settings.Set(SettingStore.FreeSpaceFloorBytes, "1000", Now);
        var planner = new ContentPlanner(install, _session.Store,
            FilesystemLimits.Unconstrained with { AvailableFreeBytes = 4500 });
        var members = _session.Store.SyncSets.Members(set.Id);
        var resumed = Assert.Single(planner.PlanCore(set, members, preserveExisting: true).Steps);
        Assert.Equal(ContentAction.Download, resumed.Action);
        Assert.Equal(3500, resumed.BytesToTransfer);

        _session.Store.Settings.Set(SettingStore.ContentMaxBytes, "9999", Now);
        var overCap = Assert.Single(planner.PlanCore(set, members, preserveExisting: true).Steps);
        Assert.Equal(ContentAction.Blocked, overCap.Action);
        Assert.Contains("budget is full", overCap.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Startup_keeps_interrupted_game_groups_out_of_the_menu_until_complete(bool flatDiscs)
    {
        using var stub = PlayStationSet(flatDiscs);
        using var connection = Connect(stub);
        var token = TestContext.Current.CancellationToken;
        Set();
        if (flatDiscs)
        {
            var requests = 0;
            stub.BeforeRomContent = _ =>
            {
                if (++requests == 2)
                {
                    stub.DropContentAfterBytes = 1500;
                }

                return Task.CompletedTask;
            };
        }
        else
        {
            stub.DropMemberAfterBytes = (12, 1500);
        }

        var events = new List<SyncEvent>();
        var progress = new Immediate<SyncEvent>(events.Add);
        var service = StartupService();
        var first = await service.RunAtStartupAsync(connection, progress, token);
        Assert.Equal(SyncState.Incomplete, first.State);
        Assert.Equal(0, Assert.Single(events.OfType<SetSynced>()).Outcome.Downloaded);
        Assert.Empty(_session.Store.Files.List("psx", LocalFileKind.Rom));
        var folder = _session.Install.Resolve("roms/psx");
        Assert.False(Directory.Exists(folder) && Directory.EnumerateFiles(folder, "*.chd", SearchOption.AllDirectories).Any());
        Assert.False(File.Exists(PlayStationFile("Game 1.m3u")));

        var second = await service.RunAtStartupAsync(connection, progress, token);
        Assert.Equal(SyncState.Done, second.State);
        Assert.Equal(3, stub.ContentRequests.Count);
        Assert.Contains("bytes=1500-", stub.ContentRequests[^1], StringComparison.Ordinal);
        Assert.Equal(2, Directory.EnumerateFiles(folder, "*.chd", SearchOption.AllDirectories).Count());
        Assert.Equal(flatDiscs ? 2 : 1, _session.Store.Files.List("psx", LocalFileKind.Rom).Count);

        await service.RunAtStartupAsync(connection, progress, token);
        Assert.Equal(3, stub.ContentRequests.Count);
    }

    [Fact]
    public async Task Fresh_flat_discs_publish_together_inside_a_single_visible_folder()
    {
        using var stub = PlayStationSet(flatDiscs: true);
        using var connection = Connect(stub);
        Set();
        var report = await StartupService().RunAtStartupAsync(connection,
            new Immediate<SyncEvent>(_ => { }), TestContext.Current.CancellationToken);

        Assert.Equal(SyncState.Done, report.State);
        var system = _session.Install.Resolve("roms/psx");
        Assert.Empty(Directory.GetFiles(system, "*.chd"));
        var directory = Assert.Single(Directory.GetDirectories(system), path => Directory.GetFiles(path, "*.chd").Length > 0);
        Assert.Equal(2, Directory.GetFiles(directory, "*.chd").Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Restart_recovers_flat_discs_staged_off_menu_or_published_before_indexing(bool published)
    {
        using var stub = PlayStationSet(flatDiscs: true);
        using var connection = Connect(stub);
        var token = TestContext.Current.CancellationToken;
        Set();
        var requested = 0;
        stub.BeforeRomContent = _ =>
        {
            if (++requested == 2)
            {
                stub.DropContentAfterBytes = 1500;
            }
            return Task.CompletedTask;
        };
        var progress = new Immediate<SyncEvent>(_ => { });
        Assert.Equal(SyncState.Incomplete, (await StartupService().RunAtStartupAsync(connection, progress, token)).State);
        var install = _session.Install;
        var covered = FlatDiscLayout.FolderFor("psx", "Game 1 (Disc 1).chd")!.Value;
        var stage = install.Resolve(FlatDiscLayout.StageFor(covered));
        var downloads = _session.Store.Downloads.List().ToArray();
        Directory.CreateDirectory(stage);
        foreach (var item in published ? downloads : downloads.Take(1))
        {
            var part = install.Resolve(item.PartPath);
            File.WriteAllBytes(part, stub.Content[item.RomId]);
            File.Move(part, Path.Combine(stage, item.TargetPath.Name));
        }

        var system = install.Resolve("roms/psx");
        Assert.False(Directory.Exists(system) && Directory.EnumerateFiles(system, "*.chd", SearchOption.AllDirectories).Any());
        var folder = install.Resolve(covered);
        if (published)
        {
            FlatDiscLayout.WriteManifest(stage, downloads.ToDictionary(item => item.RomId,
                item => new FlatDiscLayout.Receipt(
                    ContentHasher.ComputeFileMd5(Path.Combine(stage, item.TargetPath.Name)),
                    null, HashScope.File, VerifiedBy.Size)));
            Directory.CreateDirectory(system);
            Directory.Move(stage, folder);
            Assert.Equal(2, Directory.GetFiles(folder, "*.chd").Length);
        }

        using var reopened = InstallSession.Open(install.RootPath).Session!;
        var sync = new LibrarySyncService(reopened, _ => Task.FromResult(EsCallResult.Answered));
        Assert.Equal(SyncState.Done, (await sync.RunAtStartupAsync(connection, progress, token)).State);
        Assert.False(Directory.Exists(stage));
        Assert.Equal(2, Directory.GetFiles(folder, "*.chd").Length);
        Assert.Empty(reopened.Store.Downloads.List());
        Assert.All(reopened.Store.Files.List("psx", LocalFileKind.Rom), file => Assert.Equal(FileOrigin.Synced, file.Origin));
        Assert.Equal(published ? 2 : 3, stub.ContentRequests.Count);
        if (!published)
        {
            Assert.Contains("bytes=1500-", stub.ContentRequests[^1], StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Startup_then_manual_sync_and_set_removal_share_disc_paths_media_and_gamelist()
    {
        using var stub = PlayStationSet(flatDiscs: true);
        using var connection = Connect(stub);
        var set = Set();
        var token = TestContext.Current.CancellationToken;
        var install = _session.Install;
        var gamelist = install.Resolve("roms/psx/gamelist.xml");
        Directory.CreateDirectory(Path.GetDirectoryName(gamelist)!);
        File.WriteAllText(gamelist, """
            <gameList>
              <game><path>./Game 1 (Disc 1).chd</path><name>Old disc path</name></game>
              <game><path>./Personal.chd</path><name>Personal</name></game>
            </gameList>
            """);

        var service = StartupService();
        var progress = new Immediate<SyncEvent>(_ => { });
        Assert.Equal(SyncState.Done, (await service.RunAtStartupAsync(connection, progress, token)).State);
        var discs = _session.Store.Files.List("psx", LocalFileKind.Rom).ToArray();
        Assert.Equal(2, discs.Length);
        var indexed = File.ReadAllText(gamelist);
        Assert.DoesNotContain("./Game 1 (Disc 1).chd", indexed, StringComparison.Ordinal);
        Assert.Contains("./Game 1 [RomMBat discs]/Game 1 (Disc 1).chd", indexed, StringComparison.Ordinal);

        Assert.Equal(SyncState.Done, (await service.RunAsync([set], new SyncOptions(NoResolve: true),
            connection, progress, cancellationToken: token)).State);
        Assert.Equal(2, stub.ContentRequests.Count);
        Assert.Equal(discs, _session.Store.Files.List("psx", LocalFileKind.Rom).ToArray());
        var media = _session.Store.Files.List("psx").Where(file => file.Kind.IsMedia()).ToArray();
        Assert.NotEmpty(media);
        Assert.Equal(indexed, File.ReadAllText(gamelist));

        var eviction = new EvictionService(_session);
        var preview = eviction.PreviewRemoval([1, 2], releasing: [set.Id]);
        var removal = await eviction.ApplySetRemovalAsync(preview, set.Name, token);
        Assert.Null(removal.Refusal);
        Assert.Empty(removal.Evicted!.Problems);
        Assert.Null(_session.Store.SyncSets.Find(set.Name));
        Assert.All(discs.Concat(media), file => Assert.False(File.Exists(install.Resolve(file.Path))));
        Assert.DoesNotContain("Disc 1", File.ReadAllText(gamelist), StringComparison.Ordinal);
        Assert.DoesNotContain("Disc 2", File.ReadAllText(gamelist), StringComparison.Ordinal);
        Assert.Contains("Personal.chd", File.ReadAllText(gamelist), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Startup_never_publishes_part_of_a_group_when_budget_or_an_existing_unselected_disc_blocks_it(bool existing)
    {
        using var stub = PlayStationSet(flatDiscs: true);
        using var connection = Connect(stub);
        Set();
        var install = _session.Install;
        var existingDisc = install.Resolve("roms/psx/Game 1 (Disc 3).chd");
        if (existing)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(existingDisc)!);
            File.WriteAllText(existingDisc, "personal disc");
        }
        else
        {
            _session.Store.Settings.Set(SettingStore.ContentMaxBytes, "8000", Now);
        }

        var report = await StartupService().RunAtStartupAsync(connection, new Immediate<SyncEvent>(_ => { }),
            TestContext.Current.CancellationToken);

        Assert.Equal(SyncState.Blocked, report.State);
        Assert.Empty(stub.ContentRequests);
        Assert.Empty(_session.Store.Files.List("psx", LocalFileKind.Rom));
        if (existing)
        {
            Assert.Equal("personal disc", File.ReadAllText(existingDisc));
        }
    }

    [Fact]
    public async Task Startup_groups_loose_discs_selected_by_different_sets()
    {
        using var stub = PlayStationSet(flatDiscs: true);
        using var connection = Connect(stub);
        var first = Set();
        var second = _session.Store.SyncSets.Add(first with { Name = "second", ScopeValue = "2" }, Now);
        foreach (var (set, rom) in new[] { (first, stub.Library[0]), (second, stub.Library[1]) })
        {
            _session.Store.SyncSets.ReplaceMembers(set.Id, [new SyncSetMember
            {
                RomId = rom.Id, Folder = "psx", FsName = rom.FsName, DisplayName = rom.Name, SizeBytes = 5000,
                State = MemberState.Member, PlatformSlug = "psx", FsExtension = "chd", ResolvedAt = Now,
                SortKey = rom.FsName,
            }], "one disc", Now.AddSeconds(1), complete: true);
        }

        var report = await StartupService().RunAsync([first, second], new SyncOptions(NoResolve: true) { AtStartup = true },
            connection, new Immediate<SyncEvent>(_ => { }), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(SyncState.Done, report.State);
        Assert.Equal(2, stub.ContentRequests.Count);
        var folder = _session.Install.Resolve("roms/psx/Game 1 [RomMBat discs]");
        Assert.Equal(2, Directory.GetFiles(folder, "*.chd").Length);
    }

    [Fact]
    public async Task A_flat_disc_changed_during_the_next_download_blocks_publication()
    {
        using var stub = PlayStationSet(flatDiscs: true);
        using var connection = Connect(stub);
        Set();
        var first = RelativePath.Create("roms/psx/Game 1 (Disc 1).chd");
        var path = _session.Install.Resolve(first);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, stub.Content[1]);
        _session.Store.Files.Record(new LocalFile
        {
            Path = first, RomId = 1, Folder = "psx", FileName = first.Name, SizeBytes = stub.Content[1].Length,
            VerifiedBy = VerifiedBy.Size, ModifiedUtc = File.GetLastWriteTimeUtc(path),
        });
        stub.BeforeRomContent = _ =>
        {
            File.AppendAllText(path, "edited");
            return Task.CompletedTask;
        };

        var report = await StartupService().RunAtStartupAsync(connection,
            new Immediate<SyncEvent>(_ => { }), TestContext.Current.CancellationToken);

        Assert.Equal(SyncState.Blocked, report.State);
        Assert.False(File.Exists(_session.Install.Resolve("roms/psx/Game 1 (Disc 2).chd")));
        Assert.EndsWith("edited", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Startup_builds_a_missing_playlist_from_verified_discs_without_a_partial_directory()
    {
        using var stub = PlayStationSet();
        using var connection = Connect(stub);
        Set();
        foreach (var disc in stub.Library[0].Files!)
        {
            var path = PlayStationFile(disc.FileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, disc.Bytes);
        }

        Assert.False(Directory.Exists(_session.Install.Resolve(RetroBatInstall.PartialDirectory)));
        var report = await StartupService().RunAtStartupAsync(connection,
            new Immediate<SyncEvent>(_ => { }), TestContext.Current.CancellationToken);

        Assert.Equal(SyncState.Done, report.State);
        Assert.Empty(stub.ContentRequests);
        Assert.True(File.Exists(PlayStationFile("Game 1.m3u")));
    }

    [Fact]
    public void A_later_group_move_failure_reverts_only_this_publication()
    {
        var install = _session.Install;
        var first = RelativePath.Create("roms/psx/Disc 1.chd");
        var second = RelativePath.Create("roms/psx/occupied/Disc 2.chd");
        var other = install.Resolve("roms/psx/occupied");
        Directory.CreateDirectory(Path.GetDirectoryName(other)!);
        File.WriteAllText(other, "external file");

        var group = new StartupRomGroup(install, _session.Store, []);
        var firstPart = Stage(first, 1);
        var secondPart = Stage(second, 2);

        Assert.ThrowsAny<IOException>(group.Publish);
        Assert.False(File.Exists(install.Resolve(first)));
        Assert.Equal("external file", File.ReadAllText(other));
        Assert.Equal(first.Name, File.ReadAllText(firstPart));
        Assert.Equal(second.Name, File.ReadAllText(secondPart));
        Assert.Empty(_session.Store.Files.List("psx", LocalFileKind.Rom));

        string Stage(RelativePath target, int id)
        {
            var part = install.Resolve(ContentPlanner.PartFor(id));
            Directory.CreateDirectory(Path.GetDirectoryName(part)!);
            File.WriteAllText(part, target.Name);
            group.Add(new LocalFile
            {
                Path = target, RomId = id, Folder = "psx", FileName = target.Name,
            }, part, fileId: 0);
            return part;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_group_index_transaction_restores_verified_partials(bool flatDiscs)
    {
        using var stub = PlayStationSet(flatDiscs);
        using var connection = Connect(stub);
        var token = TestContext.Current.CancellationToken;
        Set();
        using var fault = _session.Store.Connection.CreateCommand();
        fault.CommandText = """
            CREATE TEMP TRIGGER fail_disc_index BEFORE INSERT ON local_file
            WHEN NEW.relative_path LIKE '%Disc 2%.chd' OR NEW.relative_path LIKE '%Disc 2).chd'
            BEGIN SELECT RAISE(ABORT, 'cannot index disc 2'); END;
            """;
        fault.ExecuteNonQuery();
        var service = StartupService();
        var progress = new Immediate<SyncEvent>(_ => { });
        Assert.Equal(SyncState.Blocked, (await service.RunAtStartupAsync(connection, progress, token)).State);
        var folder = _session.Install.Resolve("roms/psx");
        Assert.False(Directory.Exists(folder) && Directory.EnumerateFiles(folder, "*.chd", SearchOption.AllDirectories).Any());
        Assert.False(File.Exists(PlayStationFile("Game 1.m3u")));
        Assert.Empty(_session.Store.Files.List("psx"));
        Assert.Equal(2, _session.Store.Downloads.List().Count);

        fault.CommandText = "DROP TRIGGER fail_disc_index;";
        fault.ExecuteNonQuery();
        Assert.Equal(SyncState.Done, (await service.RunAtStartupAsync(connection, progress, token)).State);
        Assert.Equal(2, Directory.EnumerateFiles(folder, "*.chd", SearchOption.AllDirectories).Count());
        Assert.Equal(2, stub.ContentRequests.Count);
    }

    [Fact]
    public async Task Startup_does_not_publish_a_server_playlist_that_references_an_absent_disc()
    {
        using var stub = PlayStationSet();
        var rom = stub.Library[0];
        var playlist = "Disc 1.chd\nMissing disc.chd"u8.ToArray();
        stub.Library[0] = rom with
        {
            Files = [.. rom.Files, new StubRomFile(13, "Game 1.m3u", playlist)],
            SizeBytes = rom.SizeBytes + playlist.Length,
        };
        using var connection = Connect(stub);
        Set();

        var report = await StartupService().RunAtStartupAsync(
            connection, new Immediate<SyncEvent>(_ => { }), TestContext.Current.CancellationToken);

        Assert.Equal(SyncState.Incomplete, report.State);
        Assert.Equal(3, stub.ContentRequests.Count);
        Assert.False(File.Exists(PlayStationFile("Disc 1.chd")));
        Assert.False(File.Exists(PlayStationFile("Game 1.m3u")));
    }

    [Fact]
    public async Task Startup_defers_a_conflicting_group_and_keeps_the_personal_disc_and_playlist()
    {
        using var stub = PlayStationSet();
        using var connection = Connect(stub);
        var personalDisc = PlayStationFile("Disc 1.chd");
        var personalPlaylist = PlayStationFile("Game 1.m3u");
        Directory.CreateDirectory(Path.GetDirectoryName(personalDisc)!);
        var editedDisc = new byte[5000];
        File.WriteAllBytes(personalDisc, editedDisc);
        File.WriteAllText(personalPlaylist, "locally curated playlist");
        Set();

        // Even a live handle that denies writes must never be waited on or replaced.
        using var opened = File.Open(personalDisc, FileMode.Open, FileAccess.Read, FileShare.Read);
        var events = new List<SyncEvent>();
        var report = await StartupService().RunAtStartupAsync(
            connection, new Immediate<SyncEvent>(events.Add), TestContext.Current.CancellationToken);

        Assert.Equal(SyncState.Blocked, report.State);
        Assert.Contains("Disc 1.chd", Assert.Single(Assert.Single(events.OfType<SetSynced>()).Outcome.Problems));
        Assert.Equal(editedDisc, File.ReadAllBytes(personalDisc));
        Assert.Equal("locally curated playlist", File.ReadAllText(personalPlaylist));
        Assert.False(File.Exists(PlayStationFile("Disc 2.chd")));
        Assert.Empty(_session.Store.Files.List("psx", LocalFileKind.Rom));
    }

    [Fact]
    public async Task Startup_preserves_a_disc_that_appears_during_download_and_installs_the_next_game()
    {
        using var stub = PlayStationSet();
        using var connection = Connect(stub);
        var personal = PlayStationFile("Disc 2.chd");
        var unrelated = _session.Install.Resolve("roms/psx/Game 2.chd");
        stub.Library.Add(new StubRom(2, 1, "psx", "psx", "Game 2", "Game 2.chd", "chd", 1024));
        stub.Content[2] = new byte[1024];
        Set(games: 2);
        stub.BeforeRomContent = _ =>
        {
            if (stub.ContentRequests.Count == 1)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(personal)!);
                File.WriteAllText(personal, "personal disc");
            }

            return Task.CompletedTask;
        };

        var progress = new List<SyncEvent>();
        var report = await StartupService().RunAtStartupAsync(
            connection, new Immediate<SyncEvent>(progress.Add), TestContext.Current.CancellationToken);

        Assert.Equal(SyncState.Blocked, report.State);
        Assert.Contains("appeared during the transfer", Assert.Single(Assert.Single(progress.OfType<SetSynced>()).Outcome.Problems));
        Assert.Equal("personal disc", File.ReadAllText(personal));
        Assert.False(File.Exists(PlayStationFile("Disc 1.chd")));
        Assert.False(File.Exists(PlayStationFile("Game 1.m3u")));
        Assert.True(File.Exists(unrelated));
        var gamelist = File.ReadAllText(_session.Install.Resolve("roms/psx/gamelist.xml"));
        Assert.DoesNotContain("Game 1.m3u", gamelist, StringComparison.Ordinal);
        Assert.Contains("Game 2.chd", gamelist, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Startup_reports_existing_BIOS_without_replacing_it_or_stalling_ROM_work()
    {
        using var stub = Library(1);
        using var connection = Connect(stub);
        Set();
        var bios = _session.Install.Resolve("bios/scph5501.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(bios)!);
        File.WriteAllText(bios, "existing local firmware");
        var events = new List<SyncEvent>();

        using var openBios = File.Open(bios, FileMode.Open, FileAccess.Read, FileShare.Read);
        var report = await StartupService().RunAtStartupAsync(
            connection, new Immediate<SyncEvent>(events.Add), TestContext.Current.CancellationToken);

        Assert.Equal(SyncState.Blocked, report.State);
        Assert.Contains(events.OfType<BiosProblem>(), problem => problem.Message.Contains("scph5501.bin", StringComparison.Ordinal));
        Assert.Equal("existing local firmware", File.ReadAllText(bios));
        Assert.True(File.Exists(_session.Install.Resolve("roms/psx/Game 1.chd")));
    }

    [Fact]
    public async Task Startup_reports_a_new_remote_disc_version_without_replacing_the_installed_copy()
    {
        using var stub = PlayStationSet();
        using var connection = Connect(stub);
        var service = StartupService();
        var token = TestContext.Current.CancellationToken;
        var events = new List<SyncEvent>();
        var progress = new Immediate<SyncEvent>(events.Add);
        Set();
        await service.RunAtStartupAsync(connection, progress, token);
        var installed = PlayStationFile("Disc 2.chd");
        var original = File.ReadAllBytes(installed);

        var first = stub.Library[0];
        stub.Library[0] = first with
        {
            UpdatedAt = DateTimeOffset.UtcNow.AddDays(1).ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            Files = [first.Files[0], new StubRomFile(12, "Disc 2.chd", new byte[original.Length])],
        };
        events.Clear();

        var report = await service.RunAtStartupAsync(connection, progress, token);

        Assert.Equal(SyncState.Blocked, report.State);
        Assert.Contains("Disc 2.chd", Assert.Single(Assert.Single(events.OfType<SetSynced>()).Outcome.Problems));
        Assert.Equal(original, File.ReadAllBytes(installed));
        Assert.Equal(2, stub.ContentRequests.Count);
    }

    [Fact]
    public async Task Startup_repairs_a_missing_disc_without_replacing_the_existing_disc_or_playlist()
    {
        using var stub = PlayStationSet();
        using var connection = Connect(stub);
        var service = StartupService();
        var progress = new Immediate<SyncEvent>(_ => { });
        var token = TestContext.Current.CancellationToken;
        Set();

        await service.RunAtStartupAsync(connection, progress, token);
        var playlist = PlayStationFile("Game 1.m3u");
        var kept = PlayStationFile("Disc 1.chd");
        var missing = PlayStationFile("Disc 2.chd");
        var playlistTime = File.GetLastWriteTimeUtc(playlist);
        var keptTime = File.GetLastWriteTimeUtc(kept);
        File.Delete(missing);

        var report = await service.RunAtStartupAsync(connection, progress, token);

        Assert.Equal(SyncState.Done, report.State);
        Assert.Equal(3, stub.ContentRequests.Count);
        Assert.StartsWith("1#12 ", stub.ContentRequests[^1], StringComparison.Ordinal);
        Assert.True(File.Exists(missing));
        Assert.Equal(playlistTime, File.GetLastWriteTimeUtc(playlist));
        Assert.Equal(keptTime, File.GetLastWriteTimeUtc(kept));
    }

    [Theory]
    [InlineData(EsCallResult.Answered, EsCallResult.Answered, 1, 0)]
    [InlineData(EsCallResult.Failed, EsCallResult.Failed, 1, 0)]
    [InlineData(EsCallResult.NotRunning, EsCallResult.Answered, 2, 3)]
    [InlineData(EsCallResult.NotRunning, EsCallResult.NotRunning, 2, 3)]
    public async Task Startup_menu_reload_retries_an_absent_listener_once(
        EsCallResult first, EsCallResult last, int expectedCalls, int elapsedSeconds)
    {
        var clock = new TestTimeProvider(Now);
        var calls = 0;

        var result = await EmulationStationClient.ReloadAtStartupAsync(
            _ => Task.FromResult(++calls == 1 ? first : last), clock, TestContext.Current.CancellationToken);

        Assert.Equal(last, result);
        Assert.Equal(expectedCalls, calls);
        Assert.Equal(Now.AddSeconds(elapsedSeconds), clock.GetUtcNow());
    }

    [Fact]
    public async Task Startup_reloads_after_installing_the_first_ROM_even_if_the_gamelist_is_already_current()
    {
        using var stub = Library(1);
        using var connection = Connect(stub);
        Set();
        var calls = 0;
        var existing = _session.Install.Resolve("roms/psx/Game 1.chd");
        var service = new LibrarySyncService(_session, _ =>
        {
            calls++;
            Assert.True(File.Exists(existing));
            return Task.FromResult(EsCallResult.Answered);
        });
        var events = new List<SyncEvent>();
        var progress = new Immediate<SyncEvent>(events.Add);
        var token = TestContext.Current.CancellationToken;

        await service.RunAtStartupAsync(connection, progress, token);
        events.Clear();
        File.Delete(existing);
        await service.RunAtStartupAsync(connection, progress, token);

        var last = Assert.Single(events.OfType<GamelistsWritten>()).Outcome;
        Assert.Equal(0, last.Written);
        Assert.True(last.ReloadRequested);
        Assert.Equal(2, calls);

        await service.RunAtStartupAsync(connection, progress, token);
        Assert.Equal(2, calls);
    }

    private LibrarySyncService StartupService() =>
        new(_session, _ => Task.FromResult(EsCallResult.Answered));

    private string PlayStationFile(string name) => _session.Install.Resolve($"roms/psx/Game 1/{name}");

    private static StubRomMServer PlayStationSet(bool flatDiscs = false)
    {
        var stub = Library(flatDiscs ? 2 : 1);
        if (flatDiscs)
        {
            for (var disc = 1; disc <= 2; disc++)
            {
                stub.Library[disc - 1] = stub.Library[disc - 1] with
                {
                    FsName = $"Game 1 (Disc {disc}).chd",
                    SizeBytes = 5000,
                };
                stub.Content[disc] = Enumerable.Repeat((byte)disc, 5000).ToArray();
            }

            return stub;
        }

        stub.Library[0] = stub.Library[0] with
        {
            FsName = "Game 1",
            Extension = string.Empty,
            HasMultipleFiles = true,
            SizeBytes = 10000,
            Files = [new StubRomFile(11, "Disc 1.chd", Enumerable.Repeat((byte)1, 5000).ToArray()),
                new StubRomFile(12, "Disc 2.chd", Enumerable.Repeat((byte)2, 5000).ToArray())],
        };
        return stub;
    }

    private static RomMConnection Connect(StubRomMServer stub) =>
        new(new RomMClientOptions { Origin = new Uri("http://stub.invalid"), AccessToken = "rmm_test" }, stub);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Immediate_game_launch_gets_fresh_SRAM_while_startup_ROM_download_is_paused(bool multiFile)
    {
        using var stub = multiFile ? PlayStationSet() : Library(1);
        using var libraryConnection = new RomMConnection(
            new RomMClientOptions { Origin = new Uri("http://stub.invalid"), AccessToken = "rmm_test" }, stub);
        using var saveConnection = new RomMConnection(
            new RomMClientOptions { Origin = new Uri("http://stub.invalid"), AccessToken = "rmm_test" }, stub);
        var token = TestContext.Current.CancellationToken;
        var downloading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        stub.BeforeRomContent = async ct =>
        {
            if (!multiFile || stub.ContentRequests.Count == 1)
            {
                downloading.TrySetResult();
                await release.Task.WaitAsync(ct);
            }
        };

        var install = _session.Install;
        const string game = "Zelda II – Dönüş";
        var rom = RelativePath.Create($"roms/nes/{game}.nes");
        Directory.CreateDirectory(Path.GetDirectoryName(install.Resolve(rom))!);
        File.WriteAllText(install.Resolve(rom), "existing NES ROM");
        _session.Store.Files.Record(new LocalFile
        {
            Path = rom, Folder = "nes", RomId = 42, Kind = LocalFileKind.Rom, FileName = rom.Name, SizeBytes = 16,
        });
        var settings = install.Resolve("emulationstation/.emulationstation/es_settings.cfg");
        File.WriteAllText(settings, """<config><string name="nes.emulator" value="libretro" /></config>""");
        stub.Saves[100] = new StubRomMServer.StubSave
        {
            Id = 100, RomId = 42, Slot = "libretro:battery", Emulator = "libretro",
            FileNameNoTags = game, FileExtension = "srm", Bytes = "fresh from RomM"u8.ToArray(),
        };
        var save = install.Resolve($"saves/nes/{game}.srm");
        Directory.CreateDirectory(Path.GetDirectoryName(save)!);
        File.WriteAllText(save, "previously synced");
        new SaveScanner(install, _session.Store).Scan();
        var indexed = Assert.Single(_session.Store.Saves.List(42));
        _session.Store.Saves.MarkUploaded(indexed.Path, indexed.UnitKey, indexed.ContentHash!, Now);
        _session.Store.SaveSlots.RecordRestored(42, indexed.Slot, 99, indexed.ContentHash, Now, Now);
        Set();

        var startup = StartupService().RunAtStartupAsync(
            libraryConnection, new Immediate<SyncEvent>(_ => { }), token);
        try
        {
            await downloading.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
            if (multiFile)
            {
                Assert.False(File.Exists(PlayStationFile("Disc 1.chd")));
                Assert.False(File.Exists(PlayStationFile("Game 1.m3u")));
            }

            Spool.Write(install.RootPath, new SpoolRecord("game-start", Now, 1, [install.Resolve(rom)]));
            using var launched = InstallSession.Open(_tree.Root).Session!;
            var ready = await new SaveSync(install, launched.Store, saveConnection, "receiver")
                .PrepareLaunchAsync(rom, token);
            Assert.Equal("incoming save ready", ready);
            Assert.Equal("fresh from RomM", File.ReadAllText(save));
            Assert.False(startup.IsCompleted);
            Assert.Equal([100], stub.Acknowledged);
        }
        finally
        {
            release.TrySetResult();
            await startup;
        }
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
        // It is not a stretch of the run that begins after Content ends, which is what
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
    public async Task A_platform_whose_firmware_RomM_lacks_still_syncs_its_games()
    {
        // RetroBat's list has no optional flag and the emulator decides what it reads, so a
        // file RomM does not hold is reported and never holds the platform back.
        using var stub = Library(2);
        using var connection = new RomMConnection(
            new RomMClientOptions { Origin = new Uri("http://stub.invalid"), AccessToken = "rmm_test" },
            stub);
        BiosPlan? plan = null;

        await new LibrarySyncService(_session).RunAsync(
            [Set(games: 2)],
            new SyncOptions(NoResolve: true),
            connection,
            new Immediate<SyncEvent>(reported =>
            {
                if (reported is BiosPlanned planned)
                {
                    plan = planned.Plan;
                }
            }),
            _ => Task.CompletedTask,
            TestContext.Current.CancellationToken);

        Assert.NotNull(plan);
        Assert.True(plan.Count(BiosAction.MissingFromLibrary) > 0, "psx lists no firmware RomM lacks");
        Assert.Empty(stub.FirmwareRequests);
        Assert.True(File.Exists(Path.Combine(_tree.Root, "roms", "psx", "Game 1.chd")));
        Assert.True(File.Exists(Path.Combine(_tree.Root, "roms", "psx", "Game 2.chd")));
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
