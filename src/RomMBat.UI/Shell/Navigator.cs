using RomMBat.UI.Input;

namespace RomMBat.UI.Shell;

/// <summary>
/// The screens that are open, and the one the user is looking at.
/// </summary>
/// <remarks>
/// <b>Back on the last screen leaves RomMBat.</b> There is nothing underneath it: the app is
/// opened from the EmulationStation menu and closing it returns the user to the front end they
/// came from, which is why exiting has to be reachable without ever finding a menu item for it.
/// <para>
/// <b>A change of screen carries nothing over.</b> Whatever is held when a screen opens has to
/// be released before it acts again, so one physical press is one action. Without it, backing
/// out of a screen while still holding the button pops and then immediately fires again on the
/// screen underneath, which on the root screen closes RomMBat.
/// </para>
/// </remarks>
public sealed class Navigator
{
    private readonly List<IScreen> _screens = [];
    private readonly NavRepeat _repeat;

    public Navigator(IScreen root, NavRepeat? repeat = null)
    {
        ArgumentNullException.ThrowIfNull(root);

        _screens.Add(root);
        _repeat = repeat ?? new NavRepeat();
    }

    /// <summary>The screen on top, which is the one being shown.</summary>
    public IScreen Current => _screens[^1];

    /// <summary>How deep the user is. One is the root.</summary>
    public int Depth => _screens.Count;

    /// <summary>True once the user has asked to leave.</summary>
    public bool HasExited { get; private set; }

    /// <summary>
    /// Raised when anything a view draws may have changed, so it can rebuild.
    /// </summary>
    /// <remarks>
    /// <b>Any handled action, not only a change of screen.</b> Typing on the on-screen keyboard
    /// moves a cursor and grows a string without navigating anywhere, so a view redrawn only on
    /// push and pop would show a keyboard that never responds. Redrawing per frame instead
    /// would rebuild the whole visual tree at the poll rate for nothing.
    /// </remarks>
    public event EventHandler? Changed;

    /// <summary>Gives one action to the current screen and acts on its answer.</summary>
    /// <returns>False once RomMBat should close.</returns>
    public bool Handle(NavAction action)
    {
        if (HasExited)
        {
            return false;
        }

        var command = Current.Handle(action);

        switch (command.Kind)
        {
            case ScreenCommandKind.Stay:
                break;

            case ScreenCommandKind.Push when command.Screen is { } screen:
                _screens.Add(screen);
                _repeat.CarryNothingOver();
                break;

            case ScreenCommandKind.Replace when command.Screen is { } replacement:
                (_screens[^1] as IDisposable)?.Dispose();
                _screens[^1] = replacement;
                _repeat.CarryNothingOver();
                break;

            case ScreenCommandKind.Pop when _screens.Count > 1:
                // A screen that started work owns stopping it. Pairing polls until told not to.
                for (var closing = 0; closing < Math.Max(1, command.Depth) && _screens.Count > 1; closing++)
                {
                    (_screens[^1] as IDisposable)?.Dispose();
                    _screens.RemoveAt(_screens.Count - 1);
                }

                _repeat.CarryNothingOver();

                // Whatever is underneath may have been overtaken while it was covered, and this
                // is the only moment that can happen without the screen being pressed. A set
                // created in the editor above left the list showing the sets from before.
                (_screens[^1] as IReturnAware)?.Returned();
                break;

            case ScreenCommandKind.Pop:
                // Back on the root screen is the way out, so a user who keeps pressing back
                // leaves rather than getting stuck on a screen with no visible exit.
                HasExited = true;
                break;

            case ScreenCommandKind.Exit:
                HasExited = true;
                break;

            default:
                break;
        }

        Changed?.Invoke(this, EventArgs.Empty);

        return !HasExited;
    }

    /// <summary>
    /// Treats everything currently held as belonging to whatever came before RomMBat.
    /// </summary>
    /// <remarks>
    /// The shell calls this once, with its first reading, because the button that opened
    /// RomMBat from the EmulationStation menu is usually still down when the first poll runs.
    /// </remarks>
    public void SuppressHeld(IReadOnlySet<string> held) => _repeat.SuppressHeld(held);

    /// <summary>Polls the pad and gives the current screen whatever it asked for.</summary>
    /// <returns>False once RomMBat should close.</returns>
    public bool Advance(IReadOnlySet<string> held, DateTimeOffset now)
    {
        foreach (var action in _repeat.Advance(held, now))
        {
            if (!Handle(action))
            {
                return false;
            }
        }

        return !HasExited;
    }
}
