namespace RomMBat.UI.Input;

/// <summary>What the user asked the interface to do.</summary>
/// <remarks>
/// <b>Deliberately not the button names.</b> <c>a</c> is a button and <c>Accept</c> is an
/// intention, and keeping them apart is what lets a screen be driven by a test with no
/// controller and no <c>es_input.cfg</c> in sight.
/// </remarks>
public enum NavAction
{
    Up,
    Down,
    Left,
    Right,

    /// <summary>
    /// Enter, commit or toggle. <b>Never adjust.</b>
    /// </summary>
    /// <remarks>
    /// Argosy treats this as non-negotiable and it is the rule most likely to be got wrong by
    /// writing the obvious thing first: accept on a list of choices opens the list, it does not
    /// step through it. Stepping is <see cref="Left"/> and <see cref="Right"/>.
    /// </remarks>
    Accept,

    /// <summary>Leave this screen. On the first screen, leave RomMBat.</summary>
    Back,

    /// <summary>The screen's own primary action, whatever it says in the footer.</summary>
    Start,

    /// <summary>
    /// The screen's secondary action, named in the footer wherever it exists.
    /// </summary>
    /// <remarks>
    /// Bound to <c>x</c>. Kept apart from <see cref="Accept"/> because a screen that needs a
    /// destructive or corrective action, backspace being the obvious one, must not put it on
    /// the button that also confirms.
    /// </remarks>
    Alternate,

    /// <summary>Previous page or previous section.</summary>
    PageUp,

    /// <summary>Next page or next section.</summary>
    PageDown,
}
