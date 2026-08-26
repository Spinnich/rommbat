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
}

/// <summary>A screen's answer to one action.</summary>
public readonly record struct ScreenCommand(ScreenCommandKind Kind, IScreen? Screen = null)
{
    public static ScreenCommand Stay => new(ScreenCommandKind.Stay);

    public static ScreenCommand Pop => new(ScreenCommandKind.Pop);

    public static ScreenCommand Exit => new(ScreenCommandKind.Exit);

    public static ScreenCommand Push(IScreen screen) => new(ScreenCommandKind.Push, screen);

    public static ScreenCommand Replace(IScreen screen) => new(ScreenCommandKind.Replace, screen);
}

/// <summary>
/// One line of the footer, and how readily it is dropped when there is no room.
/// </summary>
/// <param name="Priority">Lower goes last. Hints shed in a fixed order rather than wrapping.</param>
/// <remarks>
/// The fixed order is Argosy's convention and worth keeping: a footer that reflows or truncates
/// as the content changes makes the controls feel unreliable, where one that always drops the
/// same hints in the same order stays readable.
/// </remarks>
public sealed record FooterHint(string Button, string Label, int Priority = 0);

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
/// A screen that changes without being pressed, and needs redrawing when it does.
/// </summary>
/// <remarks>
/// <b>Only pairing needs this so far, and it needs it badly.</b> A countdown that does not tick
/// and an approval that never appears are the same screen as a hung one, from the couch.
/// <para>
/// <b>Raised from whatever thread did the work</b>, so the shell marshals it. Screens have no
/// business knowing which thread they are on.
/// </para>
/// </remarks>
public interface ILiveScreen
{
    /// <summary>Something worth redrawing has changed.</summary>
    event EventHandler? Invalidated;
}
