using RomMBat.Core;
using RomMBat.Core.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// One ROM in two RetroBat folders, which is legitimate and used to be fatal.
/// </summary>
/// <remarks>
/// Its own file because it is a claim about the planner rather than about removal: the same
/// state takes out <c>evict</c>, the budget screen and the eviction pass inside every sync,
/// none of which this stage otherwise touches.
/// </remarks>
public sealed class TwoFolderEvictionTests : IDisposable
{
    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();
    private readonly InstallSession _session;

    public TwoFolderEvictionTests()
    {
        _session = InstallSession.Open(_tree.Root).Session!;
    }

    public void Dispose()
    {
        _session.Dispose();
        _tree.Dispose();
    }

    // ------------------------------------------------------- one ROM in two folders

    /// <summary>
    /// Two Rom-kind rows for one <c>rom_id</c> are legitimate, and used to take out the planner.
    /// </summary>
    /// <remarks>
    /// <b>Reached by ordinary configuration, not by corruption.</b> <c>folder_override</c> is
    /// how an arcade set resolves at all, so a <c>mame</c>-overridden platform set and an
    /// <c>fbneo</c>-overridden collection set drawn from that same platform put every shared
    /// game in both folders, and <b>both sets are then correct in EmulationStation</b>.
    /// Remapping a platform between two syncs reaches the same state with no override.
    /// <para>
    /// <c>Candidates()</c> keyed its ROM lookup with <c>ToDictionary(file =&gt; file.RomId)</c>,
    /// which throws on the second row and takes out <c>evict</c>, the budget screen and the
    /// eviction inside every sync. The comment above it said keying on <c>rom_id</c> alone
    /// "would throw on the second" and then fixed only the media case.
    /// </para>
    /// </remarks>
    [Fact]
    public void One_rom_in_two_folders_is_two_candidates_rather_than_a_crash()
    {
        Rom(1, "fbneo", "mslug.zip", 3_000);
        Rom(1, "mame", "mslug.zip", 3_000);
        Media(1, "fbneo", "mslug-image.png", 400);
        Media(1, "mame", "mslug-image.png", 400);

        var plan = new EvictionPlanner(_session.Store).Plan(bytesToFree: long.MaxValue);

        // Each copy evicts on its own merits, because each folder's gamelist names its own file
        // and removing one must not leave the other set's list pointing outside its folder.
        Assert.Equal(2, plan.Selected.Count);
        Assert.Equal(
            ["fbneo", "mame"],
            plan.Selected.Select(candidate => candidate.File.Folder).Order(StringComparer.Ordinal));

        // The media follows its own copy. Attaching all of it to one candidate would have the
        // first removal delete the second folder's cover.
        Assert.All(plan.Selected, candidate => Assert.Single(candidate.Media));
        Assert.All(
            plan.Selected,
            candidate => Assert.Equal(candidate.File.Folder, candidate.Media[0].Folder));

        // The bytes genuinely double and the budget is right to count them twice. What was
        // wrong was that nobody could see why.
        Assert.Equal(6_800, plan.BytesFreed);
    }

    private void Rom(int romId, string folder, string fileName, long bytes) =>
        Write(romId, folder, fileName, bytes, LocalFileKind.Rom);

    private void Media(int romId, string folder, string fileName, long bytes) =>
        Write(romId, folder, fileName, bytes, LocalFileKind.Image);

    private void Write(int romId, string folder, string fileName, long bytes, LocalFileKind kind)
    {
        var absolute = Path.Combine(_tree.Root, "roms", folder, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllBytes(absolute, new byte[bytes]);

        _session.Store.Files.Record(new LocalFile
        {
            Path = RelativePath.Create($"roms/{folder}/{fileName}"),
            Folder = folder,
            RomId = romId,
            Kind = kind,
            FileName = fileName,
            SizeBytes = bytes,
            Origin = FileOrigin.Synced,
        });
    }
}
