using Microsoft.Data.Sqlite;
using RomMBat.Tests.Support;
using Xunit;

[assembly: AssemblyFixture(typeof(TempTreeLeakCheck))]

namespace RomMBat.Tests.Support;

/// <summary>
/// Fails the run when a <see cref="TempRetroBatTree"/> is still on disk after the last test.
/// </summary>
/// <remarks>
/// Teardown stays lenient so one open handle does not fail an unrelated test, and this is what
/// stops that leniency hiding a leak. The two causes found so far: a tree nobody disposed, and a
/// raw <see cref="SqliteConnection"/> left pooled, which keeps <c>rommbat.db</c> open after its
/// <c>using</c> ends. Open raw connections with <c>Pooling=False</c>, as the store does.
/// <para>
/// The leftovers are deleted before the failure is raised, so a leaking run still leaves the
/// temp directory clean.
/// </para>
/// </remarks>
public sealed class TempTreeLeakCheck : IDisposable
{
    private const int ReportedTrees = 3;

    public void Dispose()
    {
        var leaked = TempRetroBatTree.CreatedRoots.Where(Directory.Exists).Order(StringComparer.Ordinal).ToList();
        if (leaked.Count == 0)
        {
            return;
        }

        // The MTP adapter repeats a cleanup failure against every test in the run, so the message
        // names a few trees rather than all of them.
        var report = leaked.Take(ReportedTrees).Select(root => $"  {root}: {Describe(root)}").ToList();
        if (leaked.Count > ReportedTrees)
        {
            report.Add($"  and {leaked.Count - ReportedTrees} more");
        }

        SqliteConnection.ClearAllPools();
        foreach (var root in leaked)
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Reported below either way.
            }
        }

        throw new InvalidOperationException(
            $"{leaked.Count} temporary RetroBat tree(s) outlived the run. A tree was not disposed, or a "
                + "file inside it was still open when it was:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, report));
    }

    private static string Describe(string root)
    {
        var files = Directory
            .EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

        return files.Count == 0 ? "(no files)" : string.Join(", ", files);
    }
}
