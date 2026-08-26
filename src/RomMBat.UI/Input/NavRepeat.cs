using RomMBat.Core.RetroBat;

namespace RomMBat.UI.Input;

/// <summary>
/// Turns "which inputs are held right now" into "what the user just asked for".
/// </summary>
/// <remarks>
/// <b>The split this class exists to keep.</b> <see cref="GamepadReader"/> decides what a
/// press <i>means</i>, from the user's own <c>es_input.cfg</c>. This decides what a held
/// meaning <i>does</i> over time, which is edge detection and auto-repeat, and is a
/// presentation concern with no business in Core.
/// <para>
/// <b>No timer and no clock of its own</b>, so it is driven by a test as easily as by a
/// render loop: the caller passes the time. Every screen in this UI is reachable from
/// <see cref="Advance"/> alone, which is what makes "navigable by gamepad" an assertion
/// rather than a claim.
/// </para>
/// <para>
/// <b>Only directions repeat.</b> Holding <c>a</c> must not activate a thing twice, and
/// holding <c>b</c> must not walk back through two screens; a long list, on the other hand, is
/// unusable if every row needs its own press.
/// </para>
/// </remarks>
public sealed class NavRepeat
{
    /// <summary>How long a direction must be held before it starts repeating.</summary>
    public static TimeSpan DefaultDelay => TimeSpan.FromMilliseconds(400);

    /// <summary>How often it repeats after that.</summary>
    public static TimeSpan DefaultInterval => TimeSpan.FromMilliseconds(90);

    /// <summary>
    /// Which EmulationStation input names produce each action.
    /// </summary>
    /// <remarks>
    /// The stick's four directions sit beside the d-pad's rather than replacing them, because
    /// a pad has both and a user reaches for either. The synthesised <c>joystick1down</c> and
    /// <c>joystick1right</c> come from <see cref="GamepadReader"/>, since <c>es_input.cfg</c>
    /// records only one direction per axis.
    /// </remarks>
    private static readonly (NavAction Action, string[] Names, bool Repeats)[] Bindings =
    [
        (NavAction.Up, ["up", "joystick1up"], true),
        (NavAction.Down, ["down", "joystick1down"], true),
        (NavAction.Left, ["left", "joystick1left"], true),
        (NavAction.Right, ["right", "joystick1right"], true),
        (NavAction.Accept, ["a"], false),
        (NavAction.Back, ["b"], false),
        (NavAction.Start, ["start"], false),
        // Bound to EmulationStation's "y", which is the button an Xbox-layout pad prints X on.
        // Measured, not assumed: on the 8BitDo the file maps x to SDL button 3 and y to button
        // 2, so ES's names for the left and top face buttons are the other way round from the
        // labels printed on the pad. Binding ES's "x" put backspace on the physical Y while the
        // footer said X. Repeats, so holding it clears a mistyped URL rather than asking for one
        // press per character.
        (NavAction.Alternate, ["y"], true),
        (NavAction.PageUp, ["pageup"], true),
        (NavAction.PageDown, ["pagedown"], true),
    ];

    private readonly TimeSpan _delay;
    private readonly TimeSpan _interval;
    private readonly Dictionary<NavAction, DateTimeOffset> _downSince = [];
    private readonly Dictionary<NavAction, DateTimeOffset> _lastFired = [];
    private readonly HashSet<NavAction> _suppressed = [];

    public NavRepeat(TimeSpan? delay = null, TimeSpan? interval = null)
    {
        _delay = delay ?? DefaultDelay;
        _interval = interval ?? DefaultInterval;
    }

    /// <summary>
    /// Reports what the currently held inputs ask for at this moment.
    /// </summary>
    /// <param name="held">Every input name held, from <see cref="GamepadReader.Held"/>.</param>
    /// <param name="now">The caller's clock, so a test needs no real time to pass.</param>
    /// <returns>
    /// The actions that fired on this call. Empty is the ordinary answer: a held direction
    /// fires once, then nothing until the repeat is due.
    /// </returns>
    public IReadOnlyList<NavAction> Advance(IReadOnlySet<string> held, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(held);

        var fired = new List<NavAction>();

        foreach (var (action, names, repeats) in Bindings)
        {
            var isDown = names.Any(held.Contains);

            if (!isDown)
            {
                _downSince.Remove(action);
                _lastFired.Remove(action);

                // Released at last, so it counts again from here.
                _suppressed.Remove(action);
                continue;
            }

            if (_suppressed.Contains(action))
            {
                continue;
            }

            if (!_downSince.TryGetValue(action, out var since))
            {
                // The press itself, which every action gets.
                _downSince[action] = now;
                _lastFired[action] = now;
                fired.Add(action);
                continue;
            }

            if (!repeats || now - since < _delay)
            {
                continue;
            }

            if (now - _lastFired[action] >= _interval)
            {
                _lastFired[action] = now;
                fired.Add(action);
            }
        }

        return fired;
    }

    /// <summary>
    /// Marks everything currently held as not this session's to act on, until it is released.
    /// </summary>
    /// <remarks>
    /// <b>Called once, by the shell, on its first poll.</b> RomMBat is opened from the
    /// EmulationStation menu by pressing A, and that first poll happens while the button is very
    /// often still down. Without this the press that launched the app is consumed by the app's
    /// own first screen, which was observed doing exactly that: a pad held at launch walked
    /// straight back out of the root screen and closed RomMBat before it drew.
    /// <para>
    /// Explicit rather than implicit on the first <see cref="Advance"/>, because a general
    /// class that silently treats its first call differently is a trap for the next caller.
    /// </para>
    /// </remarks>
    public void SuppressHeld(IReadOnlySet<string> held)
    {
        ArgumentNullException.ThrowIfNull(held);

        foreach (var (action, names, _) in Bindings)
        {
            if (names.Any(held.Contains))
            {
                _suppressed.Add(action);
            }
        }
    }

    /// <summary>
    /// Carries nothing across a change of screen: whatever is held now has to be released
    /// before it acts again.
    /// </summary>
    /// <remarks>
    /// <b>One physical press is one action, always.</b> An earlier version did the opposite and
    /// treated anything still held as a fresh press for the new screen, reasoning that a screen
    /// opening under a finger should still see it. That is wrong, and wrong in a way found by
    /// using it rather than by testing it: back out of a screen while still holding B and the
    /// pop is followed immediately by a second Back on the screen underneath, which on the root
    /// screen closes RomMBat. The same shape would have typed a character on the on-screen
    /// keyboard from the very press that opened it.
    /// <para>
    /// Same rule as <see cref="SuppressHeld"/>, applied at a screen boundary rather than at
    /// startup: an action belongs to the screen that was showing when the button went down.
    /// </para>
    /// </remarks>
    public void CarryNothingOver()
    {
        foreach (var action in _downSince.Keys)
        {
            _suppressed.Add(action);
        }

        _downSince.Clear();
        _lastFired.Clear();
    }
}
