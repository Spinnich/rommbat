using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The leak check's report, which has to name the test that leaked rather than the last one run.
/// </summary>
/// <remarks>
/// #288: a tree left holding <c>roms/snes/gamelist.xml</c> failed the run against
/// 1,595 unrelated tests and named none of them. Each tree here is deleted by the test that
/// leaks it, so the assembly's own check stays green.
/// </remarks>
public sealed class TempTreeLeakCheckTests : IDisposable
{
    // Made the way most suites make theirs, before the test method runs.
    private readonly TempRetroBatTree _field = TempRetroBatTree.Create();

    public void Dispose() => _field.Dispose();

    [Fact]
    public void A_tree_made_in_a_field_initialiser_still_knows_its_test()
    {
        Assert.Contains(
            nameof(A_tree_made_in_a_field_initialiser_still_knows_its_test),
            TempRetroBatTree.Explain(_field.Root),
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_tree_written_to_after_its_disposal_names_its_test_and_the_late_write()
    {
        var tree = TempRetroBatTree.Create();
        tree.Dispose();

        // What a loader the test never waited for does: GamelistDocument creates the folder.
        var late = Path.Combine(tree.Root, "roms", "snes", "gamelist.xml");
        Directory.CreateDirectory(Path.GetDirectoryName(late)!);
        File.WriteAllText(late, "<gameList />");

        try
        {
            var explained = TempRetroBatTree.Explain(tree.Root);

            Assert.Contains(nameof(A_tree_written_to_after_its_disposal_names_its_test_and_the_late_write), explained, StringComparison.Ordinal);
            Assert.Contains("written to again", explained, StringComparison.Ordinal);
            Assert.Equal("roms/snes/gamelist.xml", TempTreeLeakCheck.Describe(tree.Root));
        }
        finally
        {
            Directory.Delete(tree.Root, recursive: true);
        }
    }

    [Fact]
    public void A_tree_whose_file_was_open_at_disposal_says_so()
    {
        var tree = TempRetroBatTree.Create();
        var held = Path.Combine(tree.Root, "retrobat.ini");

        try
        {
            using (new FileStream(held, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                tree.Dispose();
            }

            var explained = TempRetroBatTree.Explain(tree.Root);

            Assert.Contains(nameof(A_tree_whose_file_was_open_at_disposal_says_so), explained, StringComparison.Ordinal);
            Assert.Contains("still open at disposal", explained, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tree.Root, recursive: true);
        }
    }

    [Fact]
    public void A_tree_never_disposed_says_so()
    {
        var tree = TempRetroBatTree.Create();

        try
        {
            Assert.Contains("never disposed", TempRetroBatTree.Explain(tree.Root), StringComparison.Ordinal);
        }
        finally
        {
            tree.Dispose();
        }
    }
}
