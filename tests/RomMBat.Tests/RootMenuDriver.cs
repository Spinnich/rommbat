using RomMBat.UI.Input;
using RomMBat.UI.Screens;
using RomMBat.UI.Shell;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Driving the root menu the way a controller does.
/// </summary>
/// <remarks>
/// <b>By row label, never by index.</b> Stage 7b-3 turned the root's four button-verbs into
/// eight rows, and a test that pressed Down four times would keep passing when the rows were
/// reordered or when one of them was mislabelled, which is exactly what the tests using this are
/// there to catch. Naming the row means the assertion fails when the row a person would look for
/// is not the row that opens.
/// <para>
/// Only <see cref="NavAction"/>s go in, because the claim these tests make is that every screen
/// is reachable with the gamepad map alone.
/// </para>
/// </remarks>
internal static class RootMenuDriver
{
    /// <summary>Moves to the row with this label and opens it.</summary>
    /// <remarks>
    /// Down only. The cursor wraps, so every row is reachable going one way, and a walk that
    /// could go either way would not notice a row that had become unavailable and was skipped.
    /// </remarks>
    public static void Open(Navigator navigator, string label)
    {
        ArgumentNullException.ThrowIfNull(navigator);

        var menu = Assert.IsType<ListScreen>(navigator.Current);

        // One pass of the list and no more. Without the bound, a label that is not there or a
        // row the cursor skips spins forever rather than failing.
        for (var step = 0; step < menu.Rows.Count; step++)
        {
            if (menu.Rows[menu.Cursor].Label == label)
            {
                navigator.Handle(NavAction.Accept);
                return;
            }

            navigator.Handle(NavAction.Down);
        }

        Assert.Fail(
            $"The root menu has no selectable row labelled '{label}'. It has: "
                + string.Join(", ", menu.Rows.Select(row => row.Label)));
    }
}
