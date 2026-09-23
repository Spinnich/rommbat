using System.Runtime.CompilerServices;
using RomMBat.Core.Store;

namespace RomMBat.Tests.Support;

/// <summary>
/// Makes every store this suite opens cheap to create and to write, before any test runs.
/// </summary>
/// <remarks>
/// On a GitHub Windows runner the suite spent most of its eleven minutes waiting on the disk:
/// each fresh store ran every migration as its own committed transaction, and every commit was
/// flushed. A test database is thrown away, so neither buys anything here. Migrations still run
/// from scratch once, to build the seed, and in full for any test that writes an old-version file
/// before opening it.
/// <para>
/// <c>RomMBat.Agent.Tests</c> cannot see these switches and runs few enough stores not to need
/// them, so it opens every store with the shipped defaults.
/// </para>
/// </remarks>
internal static class ThrowawayStores
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        LocalStore.FlushCommits = false;

        var directory = Path.Combine(Path.GetTempPath(), "rommbat-tests");
        Directory.CreateDirectory(directory);
        var seed = Path.Combine(directory, $"seed-{Environment.ProcessId}-{Guid.NewGuid():N}.db");

        using (LocalStore.OpenAt(seed))
        {
        }

        LocalStore.SeedDatabase = seed;
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                File.Delete(seed);
            }
            catch (IOException)
            {
                // A leftover seed in the temp directory is harmless; failing the run over it is not.
            }
        };
    }
}
