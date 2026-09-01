using RomMBat.Core;
using RomMBat.Core.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// A <c>local_file</c> row whose bytes are gone, which the budget was counting forever.
/// </summary>
/// <remarks>
/// Measured on the live install during #109's hands-on pass and filed as #113: <b>5,512 of
/// 5,932 rows pointed at files that were not there, claiming 18.22 GiB against 1.41 GiB of real
/// content.</b> An 8 GB cap on that install read as permanently 10 GB over, so every sync
/// blocked every game with 334 problems, none of which pointed at the cause.
/// </remarks>
public sealed class InventorySweepTests : IDisposable
{
    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();
    private readonly InstallSession _session;

    public InventorySweepTests()
    {
        _session = InstallSession.Open(_tree.Root).Session!;
    }

    public void Dispose()
    {
        _session.Dispose();
        _tree.Dispose();
    }

    [Fact]
    public void A_row_whose_file_is_gone_is_counted_and_named()
    {
        Row("roms/snes/here.sfc", 1_000, onDisk: true);
        Row("roms/snes/gone.sfc", 4_000, onDisk: false);
        Row("roms/fbneo/gone.zip", 8_000, onDisk: false);

        var report = new InventorySweep(_session.Install, _session.Store).Plan();

        Assert.Equal(3, report.Rows);
        Assert.Equal(2, report.Missing.Count);
        Assert.Equal(12_000, report.MissingBytes);

        // Grouped worst first, so a person can recognise "I cleared that folder by hand"
        // rather than being handed one number they have no way to place.
        Assert.Equal("fbneo", report.Folders[0].Folder);
    }

    [Fact]
    public void A_clean_install_says_so_rather_than_offering_a_repair()
    {
        Row("roms/snes/here.sfc", 1_000, onDisk: true);

        var report = new InventorySweep(_session.Install, _session.Store).Plan();

        Assert.True(report.IsClean);
        Assert.Contains("all present", report.Summary, StringComparison.Ordinal);
    }

    /// <summary>
    /// The repair takes the rows and stops the budget counting bytes that are not there.
    /// </summary>
    /// <remarks>
    /// Safe by the rollback's own argument: a row must never outlive its bytes, and the row is
    /// the claim that is wrong. The next sync re-downloads, which is what would have happened
    /// anyway, because <c>ContentPlanner</c> checks the filesystem.
    /// </remarks>
    [Fact]
    public void Repairing_stops_the_budget_counting_what_is_not_there()
    {
        Row("roms/snes/here.sfc", 1_000, onDisk: true);
        Row("roms/snes/gone.sfc", 4_000, onDisk: false);

        Assert.Equal(5_000, _session.Store.Files.SyncedBytes());

        var sweep = new InventorySweep(_session.Install, _session.Store);
        var repaired = sweep.Apply(sweep.Plan());

        Assert.Equal(1, repaired.Removed);
        Assert.Equal(4_000, repaired.BytesReclaimed);
        Assert.Equal(1_000, _session.Store.Files.SyncedBytes());

        // The file that is there keeps its row, or the next sync re-downloads and re-verifies a
        // library that was already correct.
        Assert.NotNull(_session.Store.Files.Find(RelativePath.Create("roms/snes/here.sfc")));
    }

    /// <summary>
    /// A file that came back between the preview and the repair keeps its row.
    /// </summary>
    /// <remarks>
    /// Re-checked rather than trusted, the same rule <c>EvictionPlanner.Apply</c> follows: a
    /// report can be shown to a person, sat on, and applied later, and a sync in between may
    /// have put the file back. Removing its row then would cost a re-download of a file that is
    /// already correct.
    /// </remarks>
    [Fact]
    public void A_file_that_came_back_before_the_repair_keeps_its_row()
    {
        Row("roms/snes/gone.sfc", 4_000, onDisk: false);

        var sweep = new InventorySweep(_session.Install, _session.Store);
        var report = sweep.Plan();

        Assert.Single(report.Missing);

        var absolute = Path.Combine(_tree.Root, "roms", "snes", "gone.sfc");
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllBytes(absolute, new byte[4_000]);

        var repaired = sweep.Apply(report);

        Assert.Equal(0, repaired.Removed);
        Assert.Equal(1, repaired.Returned);
        Assert.NotNull(_session.Store.Files.Find(RelativePath.Create("roms/snes/gone.sfc")));
    }

    /// <summary>
    /// A tree in which nothing recorded is found is distrusted rather than emptied.
    /// </summary>
    /// <remarks>
    /// <b>Written because a probe disproved the argument this class was first shipped with.</b>
    /// The claim was that an unplugged drive cannot reach the sweep, because a tree that does
    /// not open has no session behind it. True, and not enough: a tree carrying retrobat.ini,
    /// system/version.info and the database but no roms/ opens perfectly and reports every row
    /// missing. A copied install, a restored backup and a roms/ on a second volume all reach it,
    /// and a repair there costs a re-download of the entire library.
    /// <para>
    /// The state this is actually for looks nothing like it: on the install that motivated #113,
    /// 5,512 of 5,932 rows were missing and 420 were still there.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_tree_where_nothing_recorded_is_found_is_never_repaired()
    {
        Row("roms/snes/one.sfc", 1_000, onDisk: false);
        Row("roms/snes/two.sfc", 2_000, onDisk: false);

        var sweep = new InventorySweep(_session.Install, _session.Store);
        var report = sweep.Plan();

        Assert.True(report.NothingFound);
        Assert.Contains("does not look like the tree", report.Summary, StringComparison.Ordinal);

        var repaired = sweep.Apply(report);

        Assert.Equal(0, repaired.Removed);
        Assert.Equal(3_000, _session.Store.Files.SyncedBytes());
    }

    /// <summary>One surviving file is enough to trust the tree, which is the live install's shape.</summary>
    [Fact]
    public void One_file_still_there_makes_the_rest_repairable()
    {
        Row("roms/snes/here.sfc", 1_000, onDisk: true);
        Row("roms/snes/one.sfc", 2_000, onDisk: false);
        Row("roms/snes/two.sfc", 4_000, onDisk: false);

        var sweep = new InventorySweep(_session.Install, _session.Store);
        var report = sweep.Plan();

        Assert.False(report.NothingFound);
        Assert.Equal(2, sweep.Apply(report).Removed);
    }

    private void Row(string relative, long bytes, bool onDisk)
    {
        if (onDisk)
        {
            var absolute = Path.Combine(_tree.Root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllBytes(absolute, new byte[bytes]);
        }

        var path = RelativePath.Create(relative);

        _session.Store.Files.Record(new LocalFile
        {
            Path = path,
            Folder = relative.Split('/')[1],
            RomId = Math.Abs(relative.GetHashCode(StringComparison.Ordinal)) % 100_000,
            Kind = LocalFileKind.Rom,
            FileName = path.Name,
            SizeBytes = bytes,
            Origin = FileOrigin.Synced,
        });
    }
}
