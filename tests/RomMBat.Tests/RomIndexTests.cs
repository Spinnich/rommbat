using RomMBat.Core.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The two lookups <see cref="RomIndex"/> serves, and the one set of ROMs they answer over.
/// </summary>
/// <remarks>
/// <c>InFolder</c> used to be a prefix scan of the <c>(folder, stem)</c> dictionary and is now
/// a second dictionary built in the same pass, so these pin the contract the two share: the
/// folder is half the key in both directions, a stem collision resolves the same way in both,
/// and the reverse lookup's order is stable.
/// </remarks>
public sealed class RomIndexTests : IDisposable
{
    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();
    private readonly LocalStore _store;

    public RomIndexTests() => _store = LocalStore.Open(_tree.Install());

    public void Dispose()
    {
        _store.Dispose();
        _tree.Dispose();
    }

    [Fact]
    public void A_folder_answers_only_its_own_roms()
    {
        // Contra exists on both, which is the ordinary state of a multi-system library and the
        // reason the folder is half the key rather than decoration.
        Add(1, "snes", "Contra III (USA).sfc");
        Add(2, "nes", "Contra (USA).nes");
        Add(3, "snes", "Tetris (USA).sfc");

        var index = RomIndex.Build(_store);

        Assert.Equal(
            ["roms/snes/Contra III (USA).sfc", "roms/snes/Tetris (USA).sfc"],
            index.InFolder("snes").Select(entry => entry.Path.Value));

        Assert.Equal(
            ["roms/nes/Contra (USA).nes"],
            index.InFolder("nes").Select(entry => entry.Path.Value));

        Assert.Empty(index.InFolder("megadrive"));
    }

    [Fact]
    public void The_folder_is_matched_whole_and_case_insensitively()
    {
        // A prefix scan keyed on "folder/" made this true by accident. Asserted so a folder
        // whose name starts like another cannot start answering for it.
        Add(1, "snes", "Tetris (USA).sfc");
        Add(2, "snesmsu1", "Tetris (USA).sfc");

        var index = RomIndex.Build(_store);

        Assert.Equal(
            ["roms/snes/Tetris (USA).sfc"],
            index.InFolder("SNES").Select(entry => entry.Path.Value));
    }

    [Fact]
    public void A_stem_collision_resolves_the_same_way_in_both_directions()
    {
        // First wins in the forward lookup, and the row it dropped must not come back through
        // the reverse one: the two would then disagree about which ROM a name means.
        Add(1, "snes", "Tetris (USA).sfc");
        Add(2, "snes", "Tetris (USA).zip");

        var index = RomIndex.Build(_store);

        Assert.Equal(1, index.Find("snes", "Tetris (USA)")!.Value.RomId);
        Assert.Equal([1L], index.InFolder("snes").Select(entry => entry.RomId));
    }

    [Fact]
    public void The_reverse_lookup_is_ordered_by_path_so_a_binding_is_the_same_one_twice()
    {
        // The Game ID route walks this and takes the first ROM whose header carries the code,
        // so an unstable order is an unstable binding over an unchanged tree.
        Add(3, "psp", "Zone of the Enders (Europe).cso");
        Add(1, "psp", "3rd Birthday, The (Europe).cso");
        Add(2, "psp", "Metal Gear Solid (Europe).cso");

        Assert.Equal(
            [
                "roms/psp/3rd Birthday, The (Europe).cso",
                "roms/psp/Metal Gear Solid (Europe).cso",
                "roms/psp/Zone of the Enders (Europe).cso",
            ],
            RomIndex.Build(_store).InFolder("psp").Select(entry => entry.Path.Value));
    }

    private void Add(int romId, string folder, string fileName) =>
        _store.Files.Record(new LocalFile
        {
            Path = RelativePath.Create($"roms/{folder}/{fileName}"),
            Folder = folder,
            RomId = romId,
            Kind = LocalFileKind.Rom,
            FileName = fileName,
            SizeBytes = 1024,
        });
}
