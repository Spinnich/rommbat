using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Teardown must never be the thing that fails a test.
/// </summary>
/// <remarks>
/// <c>HookSpawnTests</c> starts a real detached pass, which holds the SQLite native library
/// open until it exits. When the tree is deleted a moment too early Windows refuses, and it
/// refuses in two different ways: a shared file gives <c>ERROR_SHARING_VIOLATION</c> and an
/// <see cref="IOException"/>, while a still-mapped image or a read-only file gives
/// <c>ERROR_ACCESS_DENIED</c> and an <see cref="UnauthorizedAccessException"/>. Catching only
/// the first turned a passing test red on main.
/// </remarks>
public sealed class TempTreeTeardownTests
{
    [Fact]
    public void A_file_that_cannot_be_deleted_does_not_fail_the_teardown()
    {
        var tree = TempRetroBatTree.Create();
        var stubborn = Path.Combine(tree.AppDirectory, "e_sqlite3.dll");
        File.WriteAllBytes(stubborn, [0x4D, 0x5A]);

        // ERROR_ACCESS_DENIED, the way a loaded native library reports it, without needing a
        // process to hold one open.
        File.SetAttributes(stubborn, FileAttributes.ReadOnly);

        try
        {
            tree.Dispose();
        }
        finally
        {
            if (File.Exists(stubborn))
            {
                File.SetAttributes(stubborn, FileAttributes.Normal);
                Directory.Delete(tree.Root, recursive: true);
            }
        }
    }

    [Fact]
    public void An_open_handle_does_not_fail_the_teardown_either()
    {
        var tree = TempRetroBatTree.Create();
        var held = Path.Combine(tree.AppDirectory, "held.db");

        using (new FileStream(held, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            tree.Dispose();
        }

        Directory.Delete(tree.Root, recursive: true);
    }
}
