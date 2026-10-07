using RomMBat.UI.Input;

namespace RomMBat.UI.Shell;

/// <summary>What a screen wants the shell to do next.</summary>
public enum ScreenCommandKind
{
    /// <summary>Stay here. The screen handled it, or ignored it.</summary>
    Stay,

    /// <summary>Open another screen on top of this one.</summary>
    Push,

    /// <summary>Close this screen and go back.</summary>
    Pop,

    /// <summary>
    /// Swap this screen for another one.
    /// </summary>
    /// <remarks>
    /// A step in a sequence rather than a detour: the on-screen keyboard hands off to pairing
    /// and has no business staying underneath it, because back from pairing means "I did not
    /// want to pair" and not "let me retype the address".
    /// </remarks>
    Replace,

    /// <summary>Leave RomMBat entirely.</summary>
    Exit,

    /// <summary>
    /// Close this popup, then do what <see cref="ScreenCommand.Follow"/> asks of the screen
    /// underneath.
    /// </summary>
    /// <remarks>
    /// <b>For the actions menu, whose choices belong to the screen it covers.</b> An action
    /// written as "open the editor" means open it over the set, not over the menu, so the menu
    /// leaves first and the action runs against what is then on top, and backing out of what it
    /// opened lands on the set rather than on the menu again.
    /// </remarks>
    Dismiss,
}

/// <summary>A screen's answer to one action.</summary>
/// <param name="Depth">
/// How many screens a pop closes. More than one when the screen underneath has been made
/// meaningless by what just happened: deleting a set leaves its detail screen describing
/// something that no longer exists, so the confirmation and the detail go together.
/// </param>
/// <param name="Follow">What a <see cref="ScreenCommandKind.Dismiss"/> runs once the popup has gone.</param>
public readonly record struct ScreenCommand(
    ScreenCommandKind Kind,
    IScreen? Screen = null,
    int Depth = 1,
    IScreen? Then = null,
    Func<ScreenCommand>? Follow = null)
{
    /// <summary>Closes this popup and runs <paramref name="follow"/> on the screen it covered.</summary>
    public static ScreenCommand Dismiss(Func<ScreenCommand> follow) =>
        new(ScreenCommandKind.Dismiss, Follow: follow);

    public static ScreenCommand Stay => new(ScreenCommandKind.Stay);

    public static ScreenCommand Pop => new(ScreenCommandKind.Pop);

    /// <summary>Closes this screen and the ones under it that it invalidated.</summary>
    public static ScreenCommand PopMany(int depth) => new(ScreenCommandKind.Pop, null, depth);

    /// <summary>
    /// Swaps this screen for one, then opens another over it.
    /// </summary>
    /// <remarks>
    /// For a step that both finishes and starts something. Creating a set lands on that set and
    /// begins resolving it, and the set has to be underneath rather than beside it, or backing
    /// out of the resolve would reach the list and skip the thing just made.
    /// </remarks>
    public static ScreenCommand ReplaceThenOpen(IScreen replacement, IScreen opened) =>
        new(ScreenCommandKind.Replace, replacement, 1, opened);

    public static ScreenCommand Exit => new(ScreenCommandKind.Exit);

    public static ScreenCommand Push(IScreen screen) => new(ScreenCommandKind.Push, screen);

    public static ScreenCommand Replace(IScreen screen) => new(ScreenCommandKind.Replace, screen);
}

/// <summary>
/// One line of the footer.
/// </summary>
/// <param name="Action">
/// What the hint promises, rather than what to call the button. A screen cannot name a button
/// here, deliberately: <c>es_input.cfg</c>'s <c>x</c> is the button printed Y and its <c>y</c>
/// is the one printed X, so a screen free to write "X" writes the wrong one, which is exactly
/// what RB-225 was. The renderer owns the glyph and there is one place to be wrong.
/// </param>
/// <remarks>
/// <b>Every hint a screen offers is drawn, in the order it is listed.</b> This record carried a
/// <c>Priority</c> for shedding hints on a narrow screen, which nothing implemented and every
/// screen set: a comment describing a behavior the code does not have is worse than the missing
/// behavior, because the next reader trusts it. No screen offers more than five, so if a footer
/// ever has too many for a panel, the shed goes in <c>ShellWindow</c> where the
/// widths are known, and the order it drops them in is Argosy's convention and worth keeping:
/// a footer that reflows as the content changes makes the controls feel unreliable.
/// </remarks>
public sealed record FooterHint(NavAction Action, string Label)
{
    /// <summary>True when the hint stands for all four directions rather than the one named.</summary>
    public bool IsDirectional { get; private init; }

    /// <summary>
    /// A hint for moving about, drawn as a pad rather than as one direction.
    /// </summary>
    /// <remarks>
    /// It carries <see cref="NavAction.Up"/> so the rule that a hint names a bound action still
    /// holds; the renderer draws what it means. EmulationStation's own footer says MOVE CURSOR
    /// the same way, and the keyboard is the first screen here where which way to move is not
    /// obvious from the content.
    /// </remarks>
    public static FooterHint Move(string label) => new(NavAction.Up, label) { IsDirectional = true };
}

/// <summary>
/// A screen, as the shell sees it.
/// </summary>
/// <remarks>
/// <b>No Avalonia anywhere in this interface, deliberately.</b> A screen is a thing that has a
/// title, some hints, and an opinion about what each action does; rendering it is a separate
/// concern that a test never needs. That is what lets every screen in this app be walked end to
/// end with the gamepad map alone and no window.
/// <para>
/// <b>Screens hold no logic beyond navigation.</b> Anything that has to decide something about
/// the user's library, saves or configuration asks Core. If a screen needs an answer Core
/// cannot give, the fix is an API on Core with a test.
/// </para>
/// </remarks>
public interface IScreen
{
    /// <summary>Shown in the header.</summary>
    string Title { get; }

    /// <summary>What the footer offers, most important first.</summary>
    IReadOnlyList<FooterHint> Hints { get; }

    /// <summary>Responds to one action.</summary>
    ScreenCommand Handle(NavAction action);
}

/// <summary>
/// One thing a screen can do, listed in its actions menu.
/// </summary>
/// <param name="Label">What the action does, in the screen's own words. Never a button name.</param>
/// <param name="Run">
/// What it does, answered as if the screen itself had been pressed: a push opens over this
/// screen, a pop closes it.
/// </param>
/// <remarks>
/// <b>Start opens the menu and never commits anything itself</b>, which is EmulationStation's
/// model: Start is MENU there, and the bottom button picks inside it (RB-423). A press of the
/// button ES uses to open a menu must never save, create or install.
/// </remarks>
public sealed record ScreenAction(string Label, Func<ScreenCommand> Run)
{
    /// <summary>
    /// The face button that runs this without opening the menu, or null for menu only.
    /// </summary>
    /// <remarks>
    /// <see cref="NavAction.Alternate"/> or <see cref="NavAction.Extra"/>, or
    /// <see cref="NavAction.Options"/> for a list's view options, which Select opens in ES
    /// (RB-422). A verb keeps the same one on every screen it appears on, so a thumb that
    /// learned Sync on one screen finds it on the next. Destructive actions take none: they are two presses into a menu,
    /// never one press beside the confirm button.
    /// </remarks>
    public NavAction? Shortcut { get; init; }

    /// <summary>Why this cannot be done now, or null when it can.</summary>
    /// <remarks>
    /// Shown dimmed with its reason rather than left out, as an unavailable <c>ListRow</c> is:
    /// an action that disappears teaches nothing about how to get it back.
    /// </remarks>
    public string? Unavailable { get; init; }

    public bool Available => Unavailable is null;

    /// <summary>
    /// The footer hints a screen's actions earn: each available shortcut, then the menu.
    /// </summary>
    /// <remarks>
    /// Derived from the same list the menu and the shortcuts are, so the footer cannot offer
    /// a button the menu lacks.
    /// </remarks>
    public static IReadOnlyList<FooterHint> Hints(IReadOnlyList<ScreenAction> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        if (actions.Count == 0)
        {
            return [];
        }

        return
        [
            .. actions
                .Where(action => action.Available && action.Shortcut is not null)
                .Select(action => new FooterHint(action.Shortcut!.Value, action.Label)),
            new FooterHint(NavAction.Start, "Menu"),
        ];
    }
}

/// <summary>A screen whose verbs are listed in an actions menu that Start opens.</summary>
/// <remarks>
/// The navigator handles Start and the shortcuts for such a screen before the screen sees
/// them, so no screen can bind its own verb to Start again.
/// </remarks>
public interface IActionScreen
{
    /// <summary>Every action the screen offers now, in menu order.</summary>
    IReadOnlyList<ScreenAction> Actions { get; }
}

/// <summary>
/// A popup drawn over the screen it was opened from.
/// </summary>
/// <remarks>
/// The screen underneath stays visible, dimmed, as EmulationStation draws its menus and its
/// message box over the list they came from (RB-423, RB-424), so a person can still see what
/// the question is about.
/// </remarks>
public interface IPopupScreen
{
    /// <summary>The screen drawn dimmed behind the popup, or null to draw it alone.</summary>
    IScreen? Underneath { get; }
}

/// <summary>
/// A screen drawn as a windowed list of rows.
/// </summary>
/// <remarks>
/// <b>Because the number of rows and the height of one are the same decision, and it belongs
/// in one file.</b> With the count in a view model and the height in the renderer, a screen
/// can compute a window of <see cref="Screens.ListWindow.Capacity"/> and be drawn at the
/// reading height, overflowing the display by exactly the margin
/// <see cref="Screens.ListWindow.ReadingCapacity"/> exists to avoid. A rule enforced at one
/// instance rather than at its class is reintroduced by the next screen.
/// <para>
/// A screen answers <see cref="Reading"/> once and <c>ListWindow.CapacityFor</c> follows from
/// it, so the renderer asks rather than deciding. A pane of facts is not drawn at a uniform
/// height either: its block is bounded by <c>ListWindow.ContentBudget</c>, so there is
/// no second number left to disagree with the first.
/// </para>
/// <para>
/// No Avalonia here either. <see cref="Window"/> is arithmetic a test can assert on, which is
/// the whole reason the windowing left the renderer in the first place.
/// </para>
/// </remarks>
public interface IWindowedScreen
{
    /// <summary>The rows to draw, which for a paged screen is one page of them.</summary>
    IReadOnlyList<Screens.ListRow> Rows { get; }

    /// <summary>Which row is selected, or -1 when there are none.</summary>
    int Cursor { get; }

    /// <summary>Which slice is on screen.</summary>
    Screens.ListView Window { get; }

    /// <summary>
    /// True when every row is text to read rather than a choice, and is drawn taller for it.
    /// </summary>
    bool Reading { get; }
}

/// <summary>
/// A screen that has to rebuild when it becomes current again.
/// </summary>
/// <remarks>
/// <b>Because a screen above it can write.</b> Creating a set left the list underneath showing
/// the sets from before, and it corrected itself only when the whole screen was rebuilt by
/// leaving and coming back. The navigator raises this on whatever a pop lands on, which is the
/// only moment a screen already on the stack can have been overtaken without being pressed. A
/// replacement is freshly constructed and has nothing stale to re-read, so it is not raised
/// there.
/// <para>
/// Distinct from <see cref="ILiveScreen"/>, which is about work the screen itself started.
/// This is about work somebody else finished.
/// </para>
/// </remarks>
public interface IReturnAware
{
    /// <summary>Something above this screen closed. Re-read anything that may have changed.</summary>
    void Returned();
}

/// <summary>
/// A screen that changes without being pressed, and needs redrawing when it does.
/// </summary>
/// <remarks>
/// <b>Only pairing needs this so far, and it needs it badly.</b> A countdown that does not tick
/// and an approval that never appears are the same screen as a hung one, from the couch.
/// <para>
/// <b>Raised from whatever thread did the work</b>, so the shell marshals it. Screens have no
/// business knowing which thread they are on.
/// </para>
/// <para>
/// <b>Followed while it is drawn, not only while it is on top.</b> A screen dimmed behind a
/// popup still redraws the stack when it changes, through <see cref="LiveFollow"/>.
/// </para>
/// </remarks>
public interface ILiveScreen
{
    /// <summary>Something worth redrawing has changed.</summary>
    event EventHandler? Invalidated;
}
