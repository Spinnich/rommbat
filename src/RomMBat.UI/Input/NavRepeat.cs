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
        (NavAction.PageUp, ["pageup"], true),
        (NavAction.PageDown, ["pagedown"], true),
    ];

    private readonly TimeSpan _delay;
    private readonly TimeSpan _interval;
    private readonly Dictionary<NavAction, DateTimeOffset> _downSince = [];
    private readonly Dictionary<NavAction, DateTimeOffset> _lastFired = [];

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
    /// Forgets what is held, so the next poll treats everything as a fresh press.
    /// </summary>
    /// <remarks>
    /// Called when a screen changes. Without it, a button still held when a screen opens is
    /// already down as far as this class is concerned, so the new screen never sees the press
    /// that arrived on it and the user presses twice.
    /// </remarks>
    public void Forget()
    {
        _downSince.Clear();
        _lastFired.Clear();
    }
}
