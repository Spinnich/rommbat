using Xunit;

namespace RomMBat.Tests.Support;

/// <summary>
/// A page in the repo whose text is built from code or data, held to what it would be built as.
/// </summary>
/// <remarks>
/// The committed page is read from the checkout, not linked into the output folder, because
/// regenerating has to write back to the same file. With <c>ROMMBAT_REGENERATE_DOCS=1</c> the
/// test writes the page and passes; without it a difference fails, so a help text or a
/// certification row changed without regenerating never reaches main.
/// <para>
/// Line endings are compared as LF, because <c>.gitattributes</c> checks Markdown out as LF on CI
/// and a Windows editor may still hand one back as CRLF.
/// </para>
/// </remarks>
internal static class GeneratedPage
{
    public const string RegenerateVariable = "ROMMBAT_REGENERATE_DOCS";

    public static void AssertCurrent(string repoRelativePath, string expected)
    {
        var path = Path.Combine(RepoRoot, repoRelativePath);
        expected = expected.ReplaceLineEndings("\n");

        if (Environment.GetEnvironmentVariable(RegenerateVariable) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, expected);
            return;
        }

        var committed = File.Exists(path) ? File.ReadAllText(path).ReplaceLineEndings("\n") : "";

        Assert.True(
            committed == expected,
            $"{repoRelativePath} is stale. Regenerate it with {RegenerateVariable}=1 dotnet test, and commit the result.");
    }

    /// <summary>The checkout this test binary was built from: the first folder up holding the solution.</summary>
    public static string RepoRoot { get; } = FindRepoRoot();

    private static string FindRepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RomMBat.sln")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"No RomMBat.sln above {AppContext.BaseDirectory}.");
    }
}
