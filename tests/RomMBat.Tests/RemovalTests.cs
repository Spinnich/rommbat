using RomM.Client.Catalog;
using RomMBat.Core;
using RomMBat.Core.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Sets;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Taking a game back off the device, and the two rules that bound it.
/// </summary>
/// <remarks>
/// <b>This is the first thing RomMBat does that removes content the user asked for.</b>
/// <c>GameSync</c>'s rollback removes things too, but only what the run itself just wrote and
/// only to keep a game whole. Here a person names what goes, so the guards are different:
/// <see cref="SaveGuard"/> answers per game, a game another enabled set still claims is held
/// back, and nothing that removes content can reach a save at all, because <c>local_file</c>
/// has no save kind.
/// </remarks>
public sealed class RemovalTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();
    private readonly InstallSession _session;

    public RemovalTests()
    {
        _session = InstallSession.Open(_tree.Root).Session!;
    }

    public void Dispose()
    {
        _session.Dispose();
        _tree.Dispose();
    }

    // ------------------------------------------------------------------ the claim rule

    /// <summary>A game a second enabled set still claims is held back, and named.</summary>
    [Fact]
    public void Removing_one_set_holds_back_a_game_another_enabled_set_still_claims()
    {
        var keep = Set("Shared", 1);
        var drop = Set("Dropping", 2);

        Rom(1, "snes", "shared.sfc", 1_000);
        Rom(2, "snes", "only.sfc", 1_000);

        Members(keep, 1);
        Members(drop, 1, 2);

        var report = new EvictionService(_session).PreviewRemoval(Members(drop), releasing: [drop.Id]);

        Assert.Equal([2], report.Plan.Selected.Select(candidate => candidate.File.RomId));

        var held = Assert.Single(report.Plan.Refused);
        Assert.Equal(1, held.File.RomId);
        Assert.Contains("still in 'Shared'", held.Refusal, StringComparison.Ordinal);
    }

    /// <summary>And it goes once the set that claimed it is gone.</summary>
    /// <remarks>
    /// The other direction of the same rule, because a held-back game that is held back forever
    /// is the failure mode the rule creates if it is written once and never released.
    /// </remarks>
    [Fact]
    public void The_same_game_is_removable_once_the_other_set_is_gone()
    {
        var keep = Set("Shared", 1);
        var drop = Set("Dropping", 2);

        Rom(1, "snes", "shared.sfc", 1_000);
        Members(keep, 1);
        Members(drop, 1);

        Assert.Single(new EvictionService(_session).PreviewRemoval(Members(drop), releasing: [drop.Id]).Plan.Refused);

        _session.Store.SyncSets.Remove(keep.Name);

        var after = new EvictionService(_session).PreviewRemoval(Members(drop), releasing: [drop.Id]);

        Assert.Empty(after.Plan.Refused);
        Assert.Equal([1], after.Plan.Selected.Select(candidate => candidate.File.RomId));
    }

    /// <summary>A disabled set makes no claim, which is what "enabled" in the rule means.</summary>
    [Fact]
    public void A_disabled_set_does_not_hold_a_game_back()
    {
        var keep = Set("Shared", 1, enabled: false);
        var drop = Set("Dropping", 2);

        Rom(1, "snes", "shared.sfc", 1_000);
        Members(keep, 1);
        Members(drop, 1);

        var report = new EvictionService(_session).PreviewRemoval(Members(drop), releasing: [drop.Id]);

        Assert.Empty(report.Plan.Refused);
        Assert.Single(report.Plan.Selected);
    }

    // ------------------------------------------------------------------ saves

    /// <summary>
    /// Removal never touches a save, and the guarantee is schema-level rather than careful code.
    /// </summary>
    /// <remarks>
    /// <c>local_file</c>'s seven kinds are <c>rom</c>, <c>image</c>, <c>thumbnail</c>,
    /// <c>marquee</c>, <c>video</c>, <c>manual</c> and <c>firmware</c>, enforced by a
    /// <c>CHECK</c>. Saves live in <c>local_save</c> and <c>local_state</c>. Anything that
    /// removes content walks <c>local_file</c>, so it cannot reach one.
    /// </remarks>
    [Fact]
    public async Task Removing_a_game_leaves_its_save_and_its_state_where_they_are()
    {
        var set = Set("Dropping", 1);
        Rom(1, "snes", "game.sfc", 1_000);
        Members(set, 1);

        var save = Save(1, "saves/snes/game.srm");
        var state = State(1, "saves/snes/game.state1");

        var service = new EvictionService(_session);

        // Previewed once so the scans inside it run, then marked up the way a flush would.
        // A save whose recorded hash no longer matches the file is exactly what SaveGuard
        // refuses on, and seeding a hash the scanner then overwrites would test the refusal
        // instead of the removal.
        service.PreviewRemoval(Members(set), releasing: [set.Id]);
        Flushed();

        var report = service.PreviewRemoval(Members(set), releasing: [set.Id]);

        Assert.Empty(report.Plan.Refused);
        await service.ApplyAsync(report, TestContext.Current.CancellationToken);

        Assert.False(File.Exists(Path.Combine(_tree.Root, "roms", "snes", "game.sfc")));
        Assert.True(File.Exists(save));
        Assert.True(File.Exists(state));
        Assert.Single(_session.Store.Saves.List(romId: 1));
        Assert.Single(_session.Store.States.List(romId: 1));
    }

    /// <summary>Marks every scanned save and state as having reached the server.</summary>
    private void Flushed()
    {
        foreach (var save in _session.Store.Saves.List())
        {
            _session.Store.Saves.MarkUploaded(save.Path, save.UnitKey, save.ContentHash!, Now);
        }

        foreach (var state in _session.Store.States.List())
        {
            _session.Store.States.MarkUploaded(state.Path, 1, state.Path.Name, state.ContentHash!, Now);
        }
    }

    // ------------------------------------------------------------------ seeding

    private SyncSetDefinition Set(string name, int platformId, bool enabled = true) =>
        _session.Store.SyncSets.Add(
            new SyncSetDefinition
            {
                Name = name,
                Scope = CatalogScopeKind.Platform,
                ScopeValue = platformId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Enabled = enabled,
            },
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
                    SizeBytes = 1_000,
                    DisplayName = $"Game {romId}",
                    SortKey = $"game {romId}",
                    Position = index + 1,
                    ResolvedAt = Now,
                }),
            ],
            $"{romIds.Length} games",
            Now);

    private IReadOnlyList<int> Members(SyncSetDefinition set) =>
        [.. _session.Store.SyncSets.Members(set.Id).Select(member => member.RomId)];

    private void Rom(int romId, string folder, string fileName, long bytes) =>
        Write(romId, folder, fileName, bytes, LocalFileKind.Rom);

    private void Media(int romId, string folder, string fileName, long bytes) =>
        Write(romId, folder, fileName, bytes, LocalFileKind.Image);

    private void Write(int romId, string folder, string fileName, long bytes, LocalFileKind kind)
    {
        var relative = RelativePath.Create($"roms/{folder}/{fileName}");
        var absolute = Path.Combine(_tree.Root, "roms", folder, fileName);

        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllBytes(absolute, new byte[bytes]);

        _session.Store.Files.Record(new LocalFile
        {
            Path = relative,
            Folder = folder,
            RomId = romId,
            Kind = kind,
            FileName = fileName,
            SizeBytes = bytes,
            Origin = FileOrigin.Synced,
        });
    }

    /// <summary>A save on disk that has already reached the server, so it does not refuse.</summary>
    private string Save(int romId, string relative)
    {
        var absolute = Path.Combine(_tree.Root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllBytes(absolute, [1, 2, 3]);

        _session.Store.Saves.Record(
            new LocalSave
            {
                Path = RelativePath.Create(relative),
                RomId = romId,
                System = "snes",
                Emulator = "snes9x",
                ShapeClass = SaveShapeClass.A,
                Slot = "auto",
                ContentHash = new string('a', 32),
                UploadedContentHash = new string('a', 32),
                SizeBytes = 3,
            },
            Now);

        return absolute;
    }

    private string State(int romId, string relative)
    {
        var absolute = Path.Combine(_tree.Root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllBytes(absolute, [4, 5, 6]);

        _session.Store.States.Record(
            new LocalState
            {
                Path = RelativePath.Create(relative),
                RomId = romId,
                System = "snes",
                Emulator = "snes9x",
                Slot = "1",
                ContentHash = new string('b', 32),
                UploadedContentHash = new string('b', 32),
                SizeBytes = 3,
            },
            Now);

        return absolute;
    }
}
