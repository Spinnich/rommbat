using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using RomM.Client;
using RomMBat.Core.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The BIOS pass: the md5 join, what it refuses to do, and what it says when it cannot help.
/// </summary>
/// <remarks>
/// <b>Every fixture here carries a real pair.</b> The file names RomM serves and the names
/// RetroBat wants are the ones measured on a live library
/// (<c>SegaCDBIOS9303.bin</c> for <c>bios_CD_U.bin</c>, <c>flash.bin</c> for
/// <c>dc_flash.bin</c>, <c>sega_100.bin</c> for <c>saturn_bios.bin</c>, <c>pcfxbios.bin</c>
/// for <c>pcfx.rom</c>, <c>bios.col</c> for <c>coleco.rom</c>), and <c>psxonpsp660.bin</c>
/// carries <c>is_verified: false</c> as it does on every copy of it in the wild. The hashes
/// are the stub content's own, because no test can mint bytes that hash to a chosen md5; the
/// names, the flags and the destinations are real, and they are what the two forbidden joins
/// would trip over.
/// </remarks>
public sealed class BiosSyncTests : IDisposable
{
    /// <summary>What RomM serves each file as, against what RetroBat wants it called.</summary>
    private static readonly (string Served, string Destination, bool Verified)[] MeasuredPairs =
    [
        ("SegaCDBIOS9303.bin", "bios/bios_CD_U.bin", false),
        ("flash.bin", "bios/dc_flash.bin", true),
        ("sega_100.bin", "bios/saturn_bios.bin", true),
        ("pcfxbios.bin", "bios/pcfx.rom", true),
        ("bios.col", "bios/coleco.rom", false),
        ("psxonpsp660.bin", "bios/psxonpsp660.bin", false),
    ];

    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();

    public void Dispose()
    {
        _tree.Dispose();
        GC.SuppressFinalize(this);
    }

    // ------------------------------------------------------------------ the join

    [Fact]
    public async Task The_join_is_md5_alone_so_a_different_name_and_a_false_flag_both_land()
    {
        // The one test that has to keep working. A filename join and an is_verified filter are
        // both one-line changes a later contributor will make in good faith, and either would
        // throw away files without which the emulator does not boot.
        using var stub = new StubRomMServer();
        var manifest = MeasuredLibrary(stub);

        using var store = LocalStore.Open(_tree.Install());
        var outcome = await SyncAsync(stub, store, manifest);

        Assert.Equal(6, outcome.Downloaded);
        Assert.Equal(0, outcome.Failed);

        foreach (var (served, destination, _) in MeasuredPairs)
        {
            var landed = Path.Combine(_tree.Root, destination.Replace('/', Path.DirectorySeparatorChar));

            Assert.True(File.Exists(landed), $"{destination} was not written, though RomM serves it as {served}");
            Assert.Equal(Content(served), File.ReadAllBytes(landed));
        }

        // Five of the six would have been lost by a name join and three by an is_verified
        // filter, so both counts are asserted rather than left to the file list above.
        Assert.Equal(
            5,
            MeasuredPairs.Count(pair => !string.Equals(
                pair.Served,
                pair.Destination.Split('/')[^1],
                StringComparison.OrdinalIgnoreCase)));

        Assert.Equal(3, MeasuredPairs.Count(pair => !pair.Verified));
    }

    [Fact]
    public async Task The_file_without_which_no_PS1_game_runs_is_fetched_though_RomM_calls_it_unverified()
    {
        using var stub = new StubRomMServer();
        var manifest = MeasuredLibrary(stub);

        using var store = LocalStore.Open(_tree.Install());
        await SyncAsync(stub, store, manifest);

        var recorded = store.Files.Find(RelativePath.Create("bios/psxonpsp660.bin"));

        Assert.NotNull(recorded);
        Assert.Equal(LocalFileKind.Firmware, recorded.Kind);
        Assert.Equal(VerifiedBy.Md5, recorded.VerifiedBy);

        var served = stub.Platforms.SelectMany(platform => platform.Firmware)
            .Single(firmware => firmware.FileName == "psxonpsp660.bin");

        Assert.False(served.IsVerified);
    }

    [Fact]
    public async Task A_record_the_server_has_lost_the_file_for_is_reported_missing_rather_than_fetched()
    {
        // missing_from_fs is 142 of 656 records on a real library, and the content route for
        // one answers 500 rather than 404. Trusting the row would promise a file and fail on it.
        var bytes = Content("sgb_boot.bin");

        using var stub = new StubRomMServer();
        stub.Platforms.Add(new StubPlatform(1, "gb", "gb", "Game Boy")
        {
            Firmware = [new StubFirmware(1, "sgb_boot.bin", bytes) { MissingFromFs = true }],
        });

        var manifest = Manifest(("gb", Md5(bytes), "bios/sgb_boot.bin"));

        using var store = LocalStore.Open(_tree.Install());
        var plan = await PlanAsync(stub, store, manifest);

        var step = Assert.Single(plan.Steps);
        Assert.Equal(BiosAction.MissingFromLibrary, step.Action);
        Assert.Contains("no longer has the file", step.Reason, StringComparison.Ordinal);
        Assert.Empty(stub.FirmwareRequests);
    }

    // ------------------------------------------------------------------ one hash, several paths

    [Fact]
    public async Task One_md5_owing_three_destinations_is_fetched_once_and_written_three_times()
    {
        // coleco.rom, colecovision.rom and openMSX/share/systemroms/coleco.rom are the same
        // bytes. Six required md5s are like this, and one of them wants a file in two trees.
        var bytes = Content("bios.col");
        var destinations = new[]
        {
            "bios/coleco.rom",
            "bios/colecovision.rom",
            "bios/openMSX/share/systemroms/coleco.rom",
        };

        using var stub = new StubRomMServer();
        stub.Platforms.Add(new StubPlatform(1, "colecovision", "colecovision", "ColecoVision")
        {
            Firmware = [new StubFirmware(1, "bios.col", bytes)],
        });

        var manifest = Manifest([.. destinations.Select(path => ("colecovision", Md5(bytes), path))]);

        using var store = LocalStore.Open(_tree.Install());
        var outcome = await SyncAsync(stub, store, manifest);

        Assert.Equal(1, outcome.Downloaded);
        Assert.Equal(3, outcome.Written);
        Assert.Single(stub.FirmwareRequests);

        foreach (var destination in destinations)
        {
            var landed = Path.Combine(_tree.Root, destination.Replace('/', Path.DirectorySeparatorChar));
            Assert.Equal(bytes, File.ReadAllBytes(landed));
            Assert.NotNull(store.Files.Find(RelativePath.Create(destination)));
        }
    }

    [Fact]
    public async Task A_destination_several_systems_require_is_written_once_and_never_onto_itself()
    {
        // The other direction from the test above, and five destinations in the bundled
        // manifest are like this: bios/syscard3.pce is required by pcengine, pcenginecd and
        // supergrafx, so the plan carries three steps for one file. Copying the second onto
        // the first throws, which would fail a pass in which the file actually landed.
        var bytes = Content("syscard3.pce");

        using var stub = new StubRomMServer();
        stub.Platforms.Add(new StubPlatform(1, "pcengine", "pcengine", "PC Engine")
        {
            Firmware = [new StubFirmware(1, "syscard3.pce", bytes)],
        });

        var manifest = Manifest(
            ("pcengine", Md5(bytes), "bios/syscard3.pce"),
            ("pcenginecd", Md5(bytes), "bios/syscard3.pce"),
            ("supergrafx", Md5(bytes), "bios/syscard3.pce"));

        using var store = LocalStore.Open(_tree.Install());
        var outcome = await SyncAsync(stub, store, manifest);

        Assert.Equal(0, outcome.Failed);
        Assert.Empty(outcome.Problems);
        Assert.Equal(1, outcome.Downloaded);
        Assert.Equal(1, outcome.Written);
        Assert.Single(stub.FirmwareRequests);

        Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(_tree.Root, "bios", "syscard3.pce")));
        Assert.Single(store.Files.List(kind: LocalFileKind.Firmware));

        // And the second pass says "present" once rather than once per system that wants it.
        var second = await SyncAsync(stub, store, manifest);
        Assert.True(second.IsNoOp);
        Assert.Equal(1, second.AlreadyPresent);
    }

    // ------------------------------------------------------------------ the third state

    [Fact]
    public async Task A_requirement_RetroBat_names_no_hash_for_is_its_own_state_and_never_missing()
    {
        // 172 of the 346 requirements, and 28 systems have nothing else. Reporting these as
        // "not in your library" would send a user looking for a file RomMBat could not
        // recognise if they already had it.
        using var stub = new StubRomMServer();
        stub.Platforms.Add(new StubPlatform(1, "mastersystem", "mastersystem", "Master System"));

        var manifest = Manifest(
            ("mastersystem", null, "bios/mame/hash/sms.xml"),
            ("mastersystem", null, "bios/mame/hash/sms1.xml"));

        using var store = LocalStore.Open(_tree.Install());
        var plan = await PlanAsync(stub, store, manifest);

        Assert.Equal(2, plan.Count(BiosAction.Unverifiable));
        Assert.Equal(0, plan.Count(BiosAction.MissingFromLibrary));
        Assert.Equal(0, plan.Count(BiosAction.Download));
        Assert.True(plan.IsNoOp);
    }

    // ------------------------------------------------------------------ what is already there

    [Fact]
    public async Task A_file_already_there_with_the_right_hash_is_adopted_and_never_fetched()
    {
        var bytes = Content("saturn_bios.bin");

        using var stub = new StubRomMServer();
        stub.Platforms.Add(new StubPlatform(1, "saturn", "saturn", "Saturn")
        {
            Firmware = [new StubFirmware(1, "sega_100.bin", bytes)],
        });

        var manifest = Manifest(("saturn", Md5(bytes), "bios/saturn_bios.bin"));
        Write("bios/saturn_bios.bin", bytes);

        using var store = LocalStore.Open(_tree.Install());
        var outcome = await SyncAsync(stub, store, manifest);

        Assert.Equal(1, outcome.Adopted);
        Assert.Equal(0, outcome.Downloaded);
        Assert.Empty(stub.FirmwareRequests);

        // Adopted, not synced: it is the user's file, it does not count against the budget, and
        // eviction must never remove it.
        var recorded = Assert.Single(store.Files.List(kind: LocalFileKind.Firmware));
        Assert.Equal(FileOrigin.Adopted, recorded.Origin);

        // And a second pass costs nothing at all.
        var second = await SyncAsync(stub, store, manifest);
        Assert.True(second.IsNoOp);
        Assert.Equal(1, second.AlreadyPresent);
    }

    [Fact]
    public async Task A_file_with_the_wrong_hash_is_warned_about_and_left_byte_identical()
    {
        // The plan's rule, and it is not a preference: a BIOS the user installed and plays with
        // beats this table's idea of the correct one, and the report is how they find out.
        var wanted = Content("dc_flash.bin");
        var theirs = Content("a working flash the user already had");

        using var stub = new StubRomMServer();
        stub.Platforms.Add(new StubPlatform(1, "dreamcast", "dreamcast", "Dreamcast")
        {
            Firmware = [new StubFirmware(1, "flash.bin", wanted)],
        });

        var manifest = Manifest(("dreamcast", Md5(wanted), "bios/dc_flash.bin"));
        var target = Write("bios/dc_flash.bin", theirs);

        using var store = LocalStore.Open(_tree.Install());
        var outcome = await SyncAsync(stub, store, manifest);

        Assert.Equal(0, outcome.Downloaded);
        Assert.Equal(theirs, File.ReadAllBytes(target));
        Assert.Empty(stub.FirmwareRequests);

        var problem = Assert.Single(outcome.Problems);
        Assert.Contains("bios/dc_flash.bin", problem, StringComparison.Ordinal);
        Assert.Contains("beats", problem, StringComparison.Ordinal);

        // Nothing is recorded for it either: it is not RomMBat's file and never becomes one.
        Assert.Empty(store.Files.List(kind: LocalFileKind.Firmware));
    }

    [Fact]
    public async Task A_download_that_arrives_wrong_is_discarded_rather_than_written()
    {
        // Verified against RetroBat's hash rather than RomM's. Firmware that fails to verify is
        // worse than absent: an emulator that loads it fails in ways that look like a bad ROM.
        var wanted = Content("pcfx.rom");

        using var stub = new StubRomMServer();
        stub.Platforms.Add(new StubPlatform(1, "pcfx", "pcfx", "PC-FX")
        {
            Firmware = [new StubFirmware(1, "pcfxbios.bin", wanted)],
        });

        stub.FirmwareBodyOverrides[1] = Content("something else entirely");

        var manifest = Manifest(("pcfx", Md5(wanted), "bios/pcfx.rom"));

        using var store = LocalStore.Open(_tree.Install());
        var outcome = await SyncAsync(stub, store, manifest);

        Assert.Equal(1, outcome.Failed);
        Assert.Equal(0, outcome.Written);
        Assert.False(File.Exists(Path.Combine(_tree.Root, "bios", "pcfx.rom")));

        // And no half-file is left where an emulator would find it, or anywhere else.
        Assert.Empty(store.Files.List(kind: LocalFileKind.Firmware));
        Assert.False(File.Exists(_tree.Install().Resolve(BiosPlanner.PartFor(Md5(wanted)))));
    }

    // ------------------------------------------------------------------ offline

    [Fact]
    public async Task The_gap_report_answers_with_the_server_unreachable()
    {
        // Principle 1, and the reason this feature is worth having on a handheld away from the
        // network: the manifest is bundled and what is on disk is local, so the question "what
        // does this system still need" never needs a server.
        var present = Content("bios_CD_U.bin");

        using var stub = new StubRomMServer();
        stub.Platforms.Add(new StubPlatform(1, "megacd", "megacd", "Mega CD")
        {
            Firmware = [new StubFirmware(1, "SegaCDBIOS9303.bin", present)],
        });

        var manifest = Manifest(
            ("megacd", Md5(present), "bios/bios_CD_U.bin"),
            ("megacd", Md5(Content("bios_CD_E.bin")), "bios/bios_CD_E.bin"),
            ("megacd", null, "bios/mame/hash/megacd.xml"));

        Write("bios/bios_CD_U.bin", present);

        using var store = LocalStore.Open(_tree.Install());

        stub.IsReachable = false;
        var plan = new BiosPlanner(_tree.Install(), store, manifest).Plan(["megacd"], candidates: null);

        Assert.False(plan.CheckedAgainstServer);
        Assert.Equal(1, plan.Count(BiosAction.Adopt));
        Assert.Equal(1, plan.Count(BiosAction.NotChecked));
        Assert.Equal(1, plan.Count(BiosAction.Unverifiable));

        // Never "missing from your library": with no server, nothing has been asked about it.
        Assert.Equal(0, plan.Count(BiosAction.MissingFromLibrary));

        await Task.CompletedTask;
    }

    // ------------------------------------------------------------------ the shared tree

    [Fact]
    public async Task Nothing_under_bios_that_RomMBat_did_not_download_is_ever_a_candidate_for_removal()
    {
        // bios/ holds 4,683 files on a real install before RomMBat writes anything: emulator
        // user data, MAME software lists, and openMSX's entire user directory including its save
        // states. RomMBat may delete only what it downloaded, and it downloads none of that.
        var fetched = Content("coleco.rom");

        using var stub = new StubRomMServer();
        stub.Platforms.Add(new StubPlatform(1, "colecovision", "colecovision", "ColecoVision")
        {
            Firmware = [new StubFirmware(1, "bios.col", fetched)],
        });

        var manifest = Manifest(("colecovision", Md5(fetched), "bios/coleco.rom"));

        var theirs = new[]
        {
            Write("bios/openmsx/savestates/quicksave.oms", Content("a real save state")),
            Write("bios/openmsx/share/machines/Boosted_MSX2_EN.xml", Content("openMSX user data")),
            Write("bios/mame/hash/adam_cart.xml", Content("a MAME software list")),
            Write("bios/dolphin-emu/Load/Textures/keep.png", Content("someone's texture pack")),
        };

        using var store = LocalStore.Open(_tree.Install());
        var outcome = await SyncAsync(stub, store, manifest);
        Assert.Equal(1, outcome.Downloaded);

        var install = _tree.Install();
        var planner = new EvictionPlanner(store);
        var plan = planner.Plan(bytesToFree: long.MaxValue);
        planner.Apply(plan, install);

        // The firmware row has no rom_id, which is what keeps eviction away from it, and the
        // files RomMBat never touched are still exactly where they were.
        Assert.Empty(plan.Selected);
        Assert.True(File.Exists(Path.Combine(_tree.Root, "bios", "coleco.rom")));
        Assert.All(theirs, path => Assert.True(File.Exists(path), $"{path} was removed"));
    }

    [Fact]
    public async Task Firmware_counts_against_the_budget_and_still_cannot_be_evicted()
    {
        var bytes = Content("saturn_bios.bin");

        using var stub = new StubRomMServer();
        stub.Platforms.Add(new StubPlatform(1, "saturn", "saturn", "Saturn")
        {
            Firmware = [new StubFirmware(1, "sega_100.bin", bytes)],
        });

        var manifest = Manifest(("saturn", Md5(bytes), "bios/saturn_bios.bin"));

        using var store = LocalStore.Open(_tree.Install());
        await SyncAsync(stub, store, manifest);

        // Counted, so status and budget tell the truth about what RomMBat put on the disk.
        Assert.Equal(bytes.Length, new ContentPlanner(_tree.Install(), store).ManagedBytes());

        // And still not evictable, however far over the budget the install is.
        store.Settings.Set(SettingStore.ContentMaxBytes, 1L, DateTimeOffset.UtcNow);
        var planner = new EvictionPlanner(store);

        Assert.True(planner.BytesOverBudget() > 0);
        Assert.Empty(planner.Plan().Selected);
    }

    [Fact]
    public async Task A_populated_install_that_moves_to_a_different_root_needs_no_BIOS_again()
    {
        var bytes = Content("psxonpsp660.bin");

        using var stub = new StubRomMServer();
        stub.Platforms.Add(new StubPlatform(1, "psx", "psx", "PlayStation")
        {
            Firmware = [new StubFirmware(1, "psxonpsp660.bin", bytes)],
        });

        var manifest = Manifest(("psx", Md5(bytes), "bios/psxonpsp660.bin"));

        using (var store = LocalStore.Open(_tree.Install()))
        {
            var outcome = await SyncAsync(stub, store, manifest);
            Assert.Equal(1, outcome.Downloaded);
        }

        using var moved = _tree.CopyToNewLocation();
        var relocated = moved.Install();

        using var store2 = LocalStore.OpenAt(relocated.DatabasePath);
        var plan = new BiosPlanner(relocated, store2, manifest).Plan(["psx"], Candidates(stub));

        Assert.NotEqual(_tree.Root, moved.Root);
        Assert.True(plan.IsNoOp);
        Assert.Equal(1, plan.Count(BiosAction.AlreadyPresent));
    }

    // ------------------------------------------------------------------ the budget

    [Fact]
    public async Task A_full_budget_blocks_the_fetch_and_says_what_that_costs()
    {
        var bytes = Content("saturn_bios.bin");

        using var stub = new StubRomMServer();
        stub.Platforms.Add(new StubPlatform(1, "saturn", "saturn", "Saturn")
        {
            Firmware = [new StubFirmware(1, "sega_100.bin", bytes)],
        });

        var manifest = Manifest(("saturn", Md5(bytes), "bios/saturn_bios.bin"));

        using var store = LocalStore.Open(_tree.Install());
        store.Settings.Set(SettingStore.ContentMaxBytes, 1L, DateTimeOffset.UtcNow);

        var plan = await PlanAsync(stub, store, manifest);
        var step = Assert.Single(plan.Steps);

        Assert.Equal(BiosAction.Blocked, step.Action);
        Assert.Contains("will not launch", step.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_full_budget_blocks_every_destination_of_the_hash_it_blocks()
    {
        // The transfer is what the budget refuses, so the other destinations of that hash have
        // nothing to be copied from. Blocking only the first would leave the rest marked for
        // download and fetch them, which is a full budget stopping nothing.
        var bytes = Content("saturn_bios.bin");

        using var stub = new StubRomMServer();
        stub.Platforms.Add(new StubPlatform(1, "saturn", "saturn", "Saturn")
        {
            Firmware = [new StubFirmware(1, "sega_100.bin", bytes)],
        });

        var manifest = Manifest(
            ("saturn", Md5(bytes), "bios/saturn_bios.bin"),
            ("saturn", Md5(bytes), "bios/kronos/saturn_bios.bin"));

        using var store = LocalStore.Open(_tree.Install());
        store.Settings.Set(SettingStore.ContentMaxBytes, 1L, DateTimeOffset.UtcNow);

        var plan = await PlanAsync(stub, store, manifest);

        Assert.Equal(2, plan.Count(BiosAction.Blocked));
        Assert.Equal(0, plan.Count(BiosAction.Download));
        Assert.Equal(0, plan.DownloadCount);
        Assert.Equal(0, plan.BytesToTransfer);
        Assert.True(plan.IsNoOp);

        // And nothing is fetched when the plan is carried out anyway.
        using var connection = Connect(stub);
        var outcome = await new BiosSync(_tree.Install(), store, connection).ApplyAsync(
            plan,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(outcome.IsNoOp);
        Assert.Empty(stub.FirmwareRequests);
    }

    [Fact]
    public async Task Every_destination_of_one_hash_is_charged_against_the_disk_budget()
    {
        // The bytes cross the network once, but they land three times, and content.max_bytes is
        // a disk budget: reserving one copy and writing three is what makes status lie.
        var bytes = Content("bios.col");
        var destinations = new[]
        {
            "bios/coleco.rom",
            "bios/colecovision.rom",
            "bios/openMSX/share/systemroms/coleco.rom",
        };

        using var stub = new StubRomMServer();
        stub.Platforms.Add(new StubPlatform(1, "colecovision", "colecovision", "ColecoVision")
        {
            Firmware = [new StubFirmware(1, "bios.col", bytes)],
        });

        var manifest = Manifest([.. destinations.Select(path => ("colecovision", Md5(bytes), path))]);

        using var store = LocalStore.Open(_tree.Install());

        // Room for two copies of it, and three are wanted.
        store.Settings.Set(SettingStore.ContentMaxBytes, (bytes.Length * 2L) + 1, DateTimeOffset.UtcNow);

        var plan = await PlanAsync(stub, store, manifest);

        Assert.Equal(2, plan.Count(BiosAction.Download));
        Assert.Equal(1, plan.Count(BiosAction.Blocked));

        var outcome = await SyncAsync(stub, store, manifest);

        // What the plan reserved is what the run put on the disk.
        Assert.Equal(2, outcome.Written);
        Assert.Equal(bytes.Length * 2L, new ContentPlanner(_tree.Install(), store).ManagedBytes());
    }

    // ------------------------------------------------------------------ adopting without a fetch

    [Fact]
    public async Task A_plan_that_only_adopts_is_still_a_pass_worth_running()
    {
        // 93 of the 156 required md5s are unknown to RomM, so a user who copied their BIOS in
        // by hand can easily have a plan with nothing to download and rows to write. Gating the
        // apply on DownloadCount would make the adopt half of the pass unreachable, and the
        // AlreadyPresent fast path needs the row this writes.
        var bytes = Content("bios_CD_U.bin");

        using var stub = new StubRomMServer();
        stub.Platforms.Add(new StubPlatform(1, "megacd", "megacd", "Mega CD"));

        var manifest = Manifest(("megacd", Md5(bytes), "bios/bios_CD_U.bin"));
        Write("bios/bios_CD_U.bin", bytes);

        using var store = LocalStore.Open(_tree.Install());
        var plan = await PlanAsync(stub, store, manifest);

        Assert.Equal(0, plan.DownloadCount);
        Assert.False(plan.IsNoOp);

        var outcome = await SyncAsync(stub, store, manifest);

        Assert.Equal(1, outcome.Adopted);

        var recorded = Assert.Single(store.Files.List(kind: LocalFileKind.Firmware));
        Assert.Equal(FileOrigin.Adopted, recorded.Origin);
    }

    [Fact]
    public async Task An_adoption_is_recorded_with_the_server_never_asked()
    {
        // Principle 1. Recognising a file already on the disk is local work, so a pass that can
        // only adopt does not need a connection to do it.
        var bytes = Content("psxonpsp660.bin");

        var manifest = Manifest(("psx", Md5(bytes), "bios/psxonpsp660.bin"));
        Write("bios/psxonpsp660.bin", bytes);

        using var store = LocalStore.Open(_tree.Install());
        var plan = new BiosPlanner(_tree.Install(), store, manifest).Plan(["psx"], candidates: null);

        Assert.False(plan.CheckedAgainstServer);
        Assert.False(plan.IsNoOp);

        var outcome = await new BiosSync(_tree.Install(), store, connection: null).ApplyAsync(
            plan,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Adopted);
        Assert.Equal(0, outcome.Failed);
        Assert.NotNull(store.Files.Find(RelativePath.Create("bios/psxonpsp660.bin")));
    }

    // ------------------------------------------------------------------ the window between plan and apply

    [Fact]
    public async Task A_file_that_appears_after_the_plan_was_built_is_reported_rather_than_overwritten()
    {
        // "Nothing is ever overwritten and nothing is ever deleted" is the class's own rule.
        // The planner checks the destination, but the check and the write are not atomic, and
        // whatever arrives in between is the user's.
        var wanted = Content("dc_flash.bin");
        var theirs = Content("a working flash the user dropped in mid-pass");

        using var stub = new StubRomMServer();
        stub.Platforms.Add(new StubPlatform(1, "dreamcast", "dreamcast", "Dreamcast")
        {
            Firmware = [new StubFirmware(1, "flash.bin", wanted)],
        });

        var manifest = Manifest(("dreamcast", Md5(wanted), "bios/dc_flash.bin"));

        using var store = LocalStore.Open(_tree.Install());
        var plan = await PlanAsync(stub, store, manifest);

        Assert.Equal(BiosAction.Download, Assert.Single(plan.Steps).Action);

        var target = Write("bios/dc_flash.bin", theirs);

        using var connection = Connect(stub);
        var outcome = await new BiosSync(_tree.Install(), store, connection).ApplyAsync(
            plan,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Failed);
        Assert.Equal(0, outcome.Written);
        Assert.Equal(theirs, File.ReadAllBytes(target));
        Assert.Empty(store.Files.List(kind: LocalFileKind.Firmware));

        // And the transfer that could not be landed leaves nothing behind.
        Assert.False(File.Exists(_tree.Install().Resolve(BiosPlanner.PartFor(Md5(wanted)))));
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>A stub carrying all six measured pairs, and the manifest that wants them.</summary>
    private static BiosManifest MeasuredLibrary(StubRomMServer stub)
    {
        var id = 1;
        var firmware = new List<StubFirmware>();
        var requirements = new List<(string System, string? Md5, string Path)>();

        foreach (var (served, destination, verified) in MeasuredPairs)
        {
            var bytes = Content(served);
            firmware.Add(new StubFirmware(id++, served, bytes) { IsVerified = verified });
            requirements.Add(("psx", Md5(bytes), destination));
        }

        // One platform carrying all of them, which is enough: the join never reads a platform.
        stub.Platforms.Add(new StubPlatform(1, "psx", "psx", "PlayStation") { Firmware = firmware });

        return Manifest([.. requirements]);
    }

    /// <summary>A manifest holding exactly these requirements, in the shipped file's shape.</summary>
    private static BiosManifest Manifest(params (string System, string? Md5, string Path)[] requirements)
    {
        var systems = requirements
            .GroupBy(requirement => requirement.System, StringComparer.Ordinal)
            .Select(group => $$"""
                "{{group.Key}}": {
                  "name": "{{group.Key}}",
                  "folder": "{{group.Key}}",
                  "files": [
                    {{string.Join(",\n    ", group.Select(file =>
                        $$"""{ "md5": {{(file.Md5 is null ? "null" : $"\"{file.Md5}\"")}}, "path": "{{file.Path}}" }"""))}}
                  ]
                }
                """);

        var json = $$"""{ "systems": { {{string.Join(",", systems)}} } }""";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return BiosManifest.Parse(stream);
    }

    private static IReadOnlyDictionary<string, RomM.Client.Catalog.FirmwareRow> Candidates(StubRomMServer stub) =>
        BiosPlanner.IndexByMd5(stub.Platforms.Select(platform => new RomM.Client.Catalog.PlatformRow
        {
            Id = platform.Id,
            Slug = platform.Slug,
            FsSlug = platform.FsSlug,
            Firmware = [.. platform.Firmware.Select(firmware => new RomM.Client.Catalog.FirmwareRow
            {
                Id = firmware.Id,
                FileName = firmware.FileName,
                SizeBytes = firmware.Bytes.Length,
                Md5Hash = firmware.Md5Hash,
                IsVerified = firmware.IsVerified,
                MissingFromFs = firmware.MissingFromFs,
            })],
        }));

    private async Task<BiosPlan> PlanAsync(StubRomMServer stub, LocalStore store, BiosManifest manifest)
    {
        using var connection = Connect(stub);
        var (candidates, problem) = await BiosCandidatesAsync(connection);

        Assert.Null(problem);

        return new BiosPlanner(_tree.Install(), store, manifest).Plan(manifest.Folders, candidates);
    }

    private async Task<BiosSyncOutcome> SyncAsync(StubRomMServer stub, LocalStore store, BiosManifest manifest)
    {
        var plan = await PlanAsync(stub, store, manifest);

        using var connection = Connect(stub);
        return await new BiosSync(_tree.Install(), store, connection).ApplyAsync(plan);
    }

    /// <summary>Reads the candidate index the way the agent does, in one request.</summary>
    private static async Task<(IReadOnlyDictionary<string, RomM.Client.Catalog.FirmwareRow>? Index, string? Problem)>
        BiosCandidatesAsync(RomMConnection connection)
    {
        var response = await connection.ListPlatformsAsync();
        return response.IsSuccess
            ? (BiosPlanner.IndexByMd5(response.Value!), null)
            : (null, response.Message);
    }

    private string Write(string relativePath, byte[] content)
    {
        var absolute = Path.Combine(_tree.Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllBytes(absolute, content);
        return absolute;
    }

    private static RomMConnection Connect(StubRomMServer stub) =>
        new(new RomMClientOptions { Origin = new Uri("https://romm.test"), AccessToken = "rmm_test" }, stub);

    /// <summary>Distinct, deterministic content for a name, so each file has its own hash.</summary>
    private static byte[] Content(string name) =>
        Encoding.UTF8.GetBytes(string.Create(
            CultureInfo.InvariantCulture,
            $"{name}:{new string('x', 64)}"));

#pragma warning disable CA5351 // MD5, deliberately: it is what RetroBat's manifest carries.
    private static string Md5(byte[] content) =>
        Convert.ToHexString(MD5.HashData(content)).ToLowerInvariant();
#pragma warning restore CA5351
}
