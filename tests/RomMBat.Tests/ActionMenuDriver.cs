using RomMBat.UI.Input;
using RomMBat.UI.Screens;
using RomMBat.UI.Shell;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Picking from a screen's actions menu the way a controller does: Start, then down to the
/// action by its label, then the confirm button.
/// </summary>
/// <remarks>
/// By label, for the reason <see cref="RootMenuDriver"/> gives: a test that pressed Down twice
/// would keep passing when the actions were reordered.
/// </remarks>
internal static class ActionMenuDriver
{
    public static void Choose(Navigator navigator, string label)
    {
        ArgumentNullException.ThrowIfNull(navigator);

        navigator.Handle(NavAction.Start);
        var menu = Assert.IsType<ActionMenuScreen>(navigator.Current);

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
            $"'{menu.Underneath.Title}' offers no available action '{label}'. It has: "
                + string.Join(", ", menu.Rows.Select(row => row.Label)));
    }
}
