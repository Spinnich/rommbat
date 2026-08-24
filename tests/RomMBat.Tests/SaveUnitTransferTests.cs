using System.IO.Compression;
using RomMBat.Core.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Putting a class C unit back, and what happens when that fails partway.
/// </summary>
/// <remarks>
/// The container is shared, so the swap is per member rather than a whole-directory rename:
/// <c>saves/psp/SAVEDATA</c> holds every PSP game on the install. That makes a partial failure
/// reachable, which is what these are about.
/// </remarks>
public sealed class SaveUnitTransferTests : IDisposable
{
    private const string Container = "saves/psp/SAVEDATA";

    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();

    public void Dispose() => _tree.Dispose();

    [Fact]
    public void A_restore_that_fails_partway_puts_the_unit_back_rather_than_leaving_it_mixed()
    {
        // The move loop touched the live tree one member at a time, so a failure partway left
        // some new members and some old ones in the container, which an emulator may read as
        // corrupt. Nothing was lost, since replaced/ holds the previous members, but recovery
        // was manual and nothing said so.
        //
        // The failure is forced the way a real one arrives: the archive names a member whose
        // parent directory cannot be created, because a file already sits at that path. A file
        // directly in the container is not a member of any unit, so it survives the scan and
        // blocks only the second move.
        var install = _tree.Install();

        Write("ULES01513SYSDATA/DATA.BIN", "what this device had");
        Write("ULES01513SYSDATA/SAVE1.BIN", "and this too");
        Write("ULES01513NEW", "a file exactly where a directory has to go");

        var part = PackArchive(
            ("ULES01513SYSDATA/DATA.BIN", "what the server sent"),
            ("ULES01513SYSDATA/SAVE1.BIN", "and this from the server"),
            ("ULES01513NEW/data.bin", "the member that cannot land"));

        var failure = Assert.Throws<IOException>(() => SaveUnitTransfer.Restore(
            install,
            new SaveUnitScanner(install),
            UnitRow(),
            part,
            install.Resolve(SaveSync.PartialDirectory),
            SaveSyncAside,
            DateTimeOffset.UnixEpoch));

        Assert.Contains("could not be put back", failure.Message, StringComparison.Ordinal);
        Assert.Contains("Nothing changed on disk", failure.Message, StringComparison.Ordinal);

        // Wholly as it was, not part new and part old.
        Assert.Equal("what this device had", Read("ULES01513SYSDATA/DATA.BIN"));
        Assert.Equal("and this too", Read("ULES01513SYSDATA/SAVE1.BIN"));

        // And nothing of the archive's is left behind in the shared container.
        Assert.False(Directory.Exists(Absolute("ULES01513NEW")));
        Assert.Equal("a file exactly where a directory has to go", Read("ULES01513NEW"));
    }

    [Fact]
    public void A_restore_that_succeeds_replaces_the_unit_whole()
    {
        // The other side of the same loop, so a rollback that fired when it should not fails
        // here rather than silently refusing every restore.
        var install = _tree.Install();

        Write("ULES01513SYSDATA/DATA.BIN", "what this device had");
        Write("ULES01513SYSDATA/SAVE1.BIN", "a slot another device deleted");

        var part = PackArchive(("ULES01513SYSDATA/DATA.BIN", "what the server sent"));

        var outcome = SaveUnitTransfer.Restore(
            install,
            new SaveUnitScanner(install),
            UnitRow(),
            part,
            install.Resolve(SaveSync.PartialDirectory),
            SaveSyncAside,
            DateTimeOffset.UnixEpoch);

        Assert.Equal("what the server sent", Read("ULES01513SYSDATA/DATA.BIN"));

        // A member the archive does not name is one the sending device deleted, so it goes.
        Assert.False(File.Exists(Absolute("ULES01513SYSDATA/SAVE1.BIN")));
        Assert.NotNull(outcome.CopiedAside);
    }

    private static RelativePath SaveSyncAside => SaveSync.AsideDirectory;

    private static LocalSave UnitRow() => new()
    {
        Path = RelativePath.Create(Container),
        UnitKey = "ULES01513",
        System = "psp",
        Emulator = "ppsspp",
        Slot = "ppsspp:savedata",
        ShapeClass = SaveShapeClass.C,
        SizeBytes = 1,
    };

    private string PackArchive(params (string Entry, string Content)[] entries)
    {
        var partial = _tree.Install().Resolve(SaveSync.PartialDirectory);
        Directory.CreateDirectory(partial);

        var path = Path.Combine(partial, "save-1.part");

        using (var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
        {
            foreach (var (entry, content) in entries)
            {
                using var writer = new StreamWriter(archive.CreateEntry(entry).Open());
                writer.Write(content);
            }
        }

        return path;
    }

    private string Absolute(string relativeToContainer) =>
        _tree.Install().Resolve($"{Container}/{relativeToContainer}");

    private string Read(string relativeToContainer) => File.ReadAllText(Absolute(relativeToContainer));

    private void Write(string relativeToContainer, string content)
    {
        var absolute = Absolute(relativeToContainer);

        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllText(absolute, content);
    }
}
