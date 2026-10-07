using RomMBat.Core;
using RomMBat.Core.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using RomMBat.UI.Input;
using RomMBat.UI.Screens;
using RomMBat.UI.Shell;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The file check on the disk screen, and what it reads once its repair closes.
/// </summary>
/// <remarks>
/// <b>Driven through a navigator, because the defect was in the pop.</b> The check planned once
/// and the repair was pushed over it, so Done landed on "Not on the drive: 60,000 files" with
/// the repair still offered, after every one of them had been forgotten (#505).
/// </remarks>
public sealed class InventoryScreenTests : IDisposable
{
    private const string ForgetLabel = "Forget the files that are not there";

    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();
    private readonly InstallSession _session;

    public InventoryScreenTests()
    {
        _session = InstallSession.Open(_tree.Root).Session!;
    }

    public void Dispose()
    {
        _session.Dispose();
        _tree.Dispose();
    }

    [Fact]
    public async Task Done_on_a_finished_repair_lands_on_a_check_that_reads_clean()
    {
        Row("roms/snes/here.sfc", onDisk: true);

        for (var index = 0; index < 3; index++)
        {
            Row($"roms/snes/gone-{index}.sfc", onDisk: false);
        }

        var check = (ListScreen)InventoryScreens.Check(_session);
        var navigator = new Navigator(check);
        await Settled(check);

        Assert.Contains(check.Rows, row => row.Label == "Not on the drive");

        navigator.Handle(NavAction.Accept);
        var repair = Assert.IsType<ListScreen>(navigator.Current);
        await Settled(repair);

        navigator.Handle(NavAction.Accept);
        Assert.Same(check, navigator.Current);
        await Settled(check);

        Assert.Equal("1 files", Assert.Single(check.Rows, row => row.Label == "Recorded").Value);
        Assert.DoesNotContain(check.Rows, row => row.Label == "Not on the drive");
        Assert.DoesNotContain(check.Hints, hint => hint.Label == ForgetLabel);

        navigator.Handle(NavAction.Back);
    }

    [Fact]
    public async Task Stop_on_a_repair_lands_on_a_check_that_counts_what_is_left()
    {
        // Enough rows that the stop usually lands part way. When the repair wins the race, Back
        // is Done, and the check must read the store as it is now either way. A stop that is
        // certain to land on running work is pinned in ControlGrammarTests.
        for (var index = 0; index < 2_000; index++)
        {
            Row($"roms/snes/gone-{index:0000}.sfc", onDisk: false);
        }

        Row("roms/snes/here.sfc", onDisk: true);

        var check = (ListScreen)InventoryScreens.Check(_session);
        var navigator = new Navigator(check);
        await Settled(check);

        navigator.Handle(NavAction.Accept);
        var repair = Assert.IsType<ListScreen>(navigator.Current);

        navigator.Handle(NavAction.Back);
        if (navigator.Current is ConfirmScreen)
        {
            navigator.Handle(NavAction.Left);
            navigator.Handle(NavAction.Accept);
        }

        // Planning again from the moment the pop lands, rather than drawing the old report.
        Assert.Same(check, navigator.Current);
        Assert.True(check.IsLoading);
        Assert.DoesNotContain(check.Hints, hint => hint.Label == ForgetLabel);

        await Settled(repair);
        await Settled(check);

        var now = new InventorySweep(_session.Install, _session.Store).Plan();
        Assert.Equal($"{now.Rows:N0} files", Assert.Single(check.Rows, row => row.Label == "Recorded").Value);

        if (now.IsClean)
        {
            Assert.DoesNotContain(check.Rows, row => row.Label == "Not on the drive");
        }
        else
        {
            Assert.StartsWith(
                $"{now.Missing.Count:N0} files",
                Assert.Single(check.Rows, row => row.Label == "Not on the drive").Value,
                StringComparison.Ordinal);
        }

        navigator.Handle(NavAction.Back);
    }

    /// <remarks>
    /// The run rather than <see cref="ListScreen.IsLoading"/>, which clears a moment before the
    /// rows are read again.
    /// </remarks>
    private static async Task Settled(ListScreen screen)
    {
        await screen.Loaded.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.False(screen.IsLoading);
    }

    private void Row(string relative, bool onDisk)
    {
        if (onDisk)
        {
            var absolute = Path.Combine(_tree.Root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllBytes(absolute, new byte[16]);
        }

        var path = RelativePath.Create(relative);

        _session.Store.Files.Record(new LocalFile
        {
            Path = path,
            Folder = relative.Split('/')[1],
            RomId = Math.Abs(relative.GetHashCode(StringComparison.Ordinal)) % 100_000,
            Kind = LocalFileKind.Rom,
            FileName = path.Name,
            SizeBytes = 16,
            Origin = FileOrigin.Synced,
        });
    }
}
