using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using RomMBat.Core.RetroBat;
using RomMBat.UI.Input;

namespace RomMBat.UI.Shell;

/// <summary>
/// The full-screen window, and the loop that feeds the controller into it.
/// </summary>
/// <remarks>
/// <b>This is the only class that knows about both Avalonia and the pad</b>, which is what
/// keeps every screen testable: the screens see <see cref="NavAction"/> and nothing else.
/// <para>
/// <b>The keyboard is a development convenience and is not a supported flow.</b> No primary
/// flow may require anything but a controller, and the keys mapped here exist so the UI can be
/// worked on at a desk without a pad plugged in. They are deliberately the obvious ones rather
/// than a second input system read from <c>es_input.cfg</c>'s keyboard section.
/// </para>
/// </remarks>
internal sealed class ShellWindow : Window
{
    /// <summary>
    /// How often the pad is read.
    /// </summary>
    /// <remarks>
    /// About 120 Hz, comfortably under the repeat interval so no press is missed between polls
    /// and well inside a frame. Reading the pad is a handful of memory reads after
    /// <c>SDL_JoystickUpdate</c>, so this is cheap.
    /// </remarks>
    private static TimeSpan PollInterval => TimeSpan.FromMilliseconds(8);

    private readonly Navigator _navigator;
    private readonly GamepadReader? _gamepad;
    private readonly Action _exit;
    private readonly ContentControl _body = new();
    private readonly TextBlock _title = new();
    private readonly StackPanel _footer = new() { Orientation = Orientation.Horizontal, Spacing = 28 };
    private bool _primed;
    private ILiveScreen? _live;

    public ShellWindow(Navigator navigator, GamepadReader? gamepad, Action exit)
    {
        _navigator = navigator;
        _gamepad = gamepad;
        _exit = exit;

        Title = "RomMBat";
        WindowState = WindowState.FullScreen;
        SystemDecorations = SystemDecorations.None;
        Background = new SolidColorBrush(Color.FromRgb(0x10, 0x12, 0x18));

        Content = BuildChrome();
        Render();

        _navigator.Changed += (_, _) => Render();

        var timer = new DispatcherTimer { Interval = PollInterval };
        timer.Tick += (_, _) => Poll();
        timer.Start();

        AddHandler(KeyDownEvent, OnKey, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    private Grid BuildChrome()
    {
        _title.FontSize = 34;
        _title.Foreground = Brushes.White;
        _title.Margin = new Thickness(48, 36, 48, 12);

        _body.Margin = new Thickness(48, 8, 48, 8);

        var footerBar = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x18, 0x1B, 0x24)),
            Padding = new Thickness(48, 18, 48, 18),
            Child = _footer,
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        Grid.SetRow(_title, 0);
        var scroller = new ScrollViewer { Content = _body };
        Grid.SetRow(scroller, 1);
        Grid.SetRow(footerBar, 2);

        grid.Children.Add(_title);
        grid.Children.Add(scroller);
        grid.Children.Add(footerBar);

        return grid;
    }

    private void Poll()
    {
        var held = _gamepad?.Held() ?? (IReadOnlySet<string>)new HashSet<string>(StringComparer.Ordinal);

        if (!_primed)
        {
            // The button that opened RomMBat from the ES menu is usually still down right now,
            // and it is not this app's to act on.
            _primed = true;
            _navigator.SuppressHeld(held);
            return;
        }

        if (!_navigator.Advance(held, DateTimeOffset.UtcNow))
        {
            _exit();
        }
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        var action = e.Key switch
        {
            Key.Up => (NavAction?)NavAction.Up,
            Key.Down => NavAction.Down,
            Key.Left => NavAction.Left,
            Key.Right => NavAction.Right,
            Key.Enter => NavAction.Accept,
            Key.Escape => NavAction.Back,
            Key.Back => NavAction.Alternate,
            Key.F5 => NavAction.Start,
            _ => null,
        };

        if (action is { } resolved && !_navigator.Handle(resolved))
        {
            _exit();
        }
    }

    /// <summary>Follows a screen that updates itself, and stops following the last one.</summary>
    private void Rewire(IScreen screen)
    {
        if (ReferenceEquals(screen, _live))
        {
            return;
        }

        if (_live is not null)
        {
            _live.Invalidated -= OnScreenInvalidated;
        }

        _live = screen as ILiveScreen;

        if (_live is not null)
        {
            _live.Invalidated += OnScreenInvalidated;
        }
    }

    // Raised from whatever thread did the work, so hop to the UI thread before touching controls.
    private void OnScreenInvalidated(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(Render);

    /// <summary>
    /// Rebuilds the visible screen.
    /// </summary>
    /// <remarks>
    /// Driven by the navigator whenever it handled an action, which is a few times a second at
    /// most, rather than by the poll timer. Typing moves a cursor without navigating anywhere,
    /// so redrawing only on push and pop would show a keyboard that never responds; redrawing
    /// per frame would rebuild the whole visual tree at 120 Hz for nothing.
    /// </remarks>
    private void Render()
    {
        var screen = _navigator.Current;
        Rewire(screen);

        _title.Text = screen.Title;
        _body.Content = ScreenView.Build(screen);

        _footer.Children.Clear();
        foreach (var hint in screen.Hints)
        {
            _footer.Children.Add(ScreenView.Hint(hint));
        }
    }
}
