using RomMBat.UI.Input;
using RomMBat.UI.Shell;

namespace RomMBat.UI.Screens;

/// <summary>
/// Every action the screen underneath offers, opened by Start as EmulationStation's menu is.
/// </summary>
/// <remarks>
/// <b>The bottom button picks and Start or the right button closes</b>, which is ES's own
/// MAIN MENU (RB-423). A pick closes the menu before the action runs, through
/// <see cref="ScreenCommandKind.Dismiss"/>, so what it opens lands over the screen the action
/// belongs to.
/// <para>
/// The rows are a <see cref="ListScreen"/>'s, so an unavailable action is dimmed with its reason
/// and the cursor skips it, exactly as a list row is.
/// </para>
/// </remarks>
public sealed class ActionMenuScreen : IScreen, IWindowedScreen, IPopupScreen, IDisposable
{
    private readonly ListScreen _list;

    public ActionMenuScreen(IScreen underneath, IReadOnlyList<ScreenAction> actions)
    {
        ArgumentNullException.ThrowIfNull(underneath);
        ArgumentNullException.ThrowIfNull(actions);

        Underneath = underneath;
        Actions = actions;

        _list = new ListScreen(
            underneath.Title,
            [.. actions.Select(action => new ListRow(action.Label, null, action.Unavailable, action.Available))],
            index => ScreenCommand.Dismiss(actions[index].Run),
            acceptLabel: "Select",
            backLabel: "Close")
        {
            Verbs = (action, _) => action == NavAction.Start ? ScreenCommand.Pop : null,
        };
    }

    public IScreen Underneath { get; }

    IScreen? IPopupScreen.Underneath => Underneath;

    public IReadOnlyList<ScreenAction> Actions { get; }

    /// <summary>The covered screen's own title, so the header does not change under the menu.</summary>
    public string Title => Underneath.Title;

    public IReadOnlyList<FooterHint> Hints => _list.Hints;

    public IReadOnlyList<ListRow> Rows => _list.Rows;

    public int Cursor => _list.Cursor;

    public ListView Window => _list.Window;

    public bool Reading => false;

    public ScreenCommand Handle(NavAction action) => _list.Handle(action);

    public void Dispose() => _list.Dispose();
}
