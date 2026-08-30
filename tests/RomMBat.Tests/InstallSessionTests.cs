using RomMBat.Core;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The composition root, which both front ends open an install through.
/// </summary>
/// <remarks>
/// <b>It lives in Core so there is one version gate rather than two.</b> These assertions are
/// about the shape that makes that safe: every refusal is a value carrying the words for it,
/// so nothing here needs a console, and the UI can render a refusal as a screen without
/// re-deciding anything.
/// </remarks>
public class InstallSessionTests
{
    [Fact]
    public void A_supported_install_opens_with_its_tree_and_its_store()
    {
        using var tree = TempRetroBatTree.Create();

        var opened = InstallSession.Open(tree.Root);
        using var session = opened.Session;

        Assert.True(opened.IsOpen);
        Assert.Equal(InstallRefusal.None, opened.Refusal);
        Assert.Null(opened.Message);
        Assert.Null(opened.Warning);
        Assert.NotNull(session);
        Assert.NotNull(session!.Store);
        Assert.Equal(tree.Root, session.Install.RootPath);
    }

    [Fact]
    public void A_directory_that_is_not_a_RetroBat_tree_is_refused_with_words_rather_than_an_exception()
    {
        var empty = Directory.CreateTempSubdirectory("rommbat-not-retrobat");

        try
        {
            var opened = InstallSession.Open(empty.FullName);

            Assert.False(opened.IsOpen);
            Assert.Equal(InstallRefusal.NotFound, opened.Refusal);
            Assert.False(string.IsNullOrWhiteSpace(opened.Message));
        }
        finally
        {
            empty.Delete(recursive: true);
        }
    }

    [Fact]
    public void A_RetroBat_below_the_floor_is_refused_before_a_database_is_ever_created()
    {
        using var tree = TempRetroBatTree.Create(version: "7.0.0-stable-win64");

        var opened = InstallSession.Open(tree.Root);

        Assert.False(opened.IsOpen);
        Assert.Equal(InstallRefusal.Version, opened.Refusal);
        Assert.False(string.IsNullOrWhiteSpace(opened.Message));

        // The ordering is the point, not an implementation detail: a build that refuses to run
        // against this RetroBat must not leave a store behind in the user's tree.
        Assert.Empty(Directory.GetFiles(
            Path.Combine(tree.Root, "emulators", "rommbat"),
            "rommbat.db*"));
    }

    [Fact]
    public void A_supplied_origin_wins_and_a_non_http_one_is_refused()
    {
        using var tree = TempRetroBatTree.Create();
        using var session = InstallSession.Open(tree.Root).Session!;

        Assert.Equal(
            new Uri("https://example.invalid/"),
            session.ResolveOrigin("https://example.invalid/").Origin);

        var refused = session.ResolveOrigin("ftp://example.invalid/");

        Assert.Null(refused.Origin);
        Assert.Contains("http", refused.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void With_nothing_supplied_and_nothing_stored_the_answer_names_the_flag_to_pass()
    {
        using var tree = TempRetroBatTree.Create();
        using var session = InstallSession.Open(tree.Root).Session!;

        var choice = session.ResolveOrigin(null);

        Assert.Null(choice.Origin);
        Assert.Contains("--server", choice.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unpaired_install_is_not_paired_rather_than_broken()
    {
        using var tree = TempRetroBatTree.Create();
        using var session = InstallSession.Open(tree.Root).Session!;

        var attempt = session.Authenticate();

        // The distinction the UI needs: this is the pairing screen, not an error screen. An
        // expiring token is the recommended default, so arriving here is ordinary.
        Assert.Null(attempt.Connection);
        Assert.True(attempt.NotPaired);
        Assert.False(string.IsNullOrWhiteSpace(attempt.Problem));
    }
}
