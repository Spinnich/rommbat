using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using RomMBat.Core.RetroBat;
using RomMBat.UI.Input;
using RomMBat.UI.Screens;

// Aliased because a Window has a Theme of its own, which the bare name would find first.
using EsTheme = RomMBat.UI.Shell.Theme;

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
    private readonly ContentControl _overlay = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly Border _scrim = new() { Background = EsTheme.Scrim, IsVisible = false };
    private readonly TextBlock _title = new();
    private readonly StackPanel _footer = new() { Orientation = Orientation.Horizontal, Spacing = 22 };
    private bool _primed;
    private GamepadAvailability? _lastAvailability;
    private ILiveScreen? _live;

    public ShellWindow(Navigator navigator, GamepadReader? gamepad, Action exit)
    {
        _navigator = navigator;
        _gamepad = gamepad;
        _exit = exit;

        Title = "RomMBat";
        WindowState = WindowState.FullScreen;
        WindowDecorations = WindowDecorations.None;
        Background = EsTheme.Background;

        // Inherited by every line the screens draw, so only the title and the help bar name
        // a face of their own.
        FontFamily = EsTheme.MenuFont;

        Content = Scaled(BuildChrome());
        Render();

        _navigator.Changed += (_, _) => Render();

        var timer = new DispatcherTimer { Interval = PollInterval };
        timer.Tick += (_, _) => Poll();
        timer.Start();

        // Focusable and focused on open, because a key event is routed to the focused element
        // and nothing else in this tree accepts focus: every control here is drawn rather than
        // interacted with, so with no focus target the handler below never runs at all.
        Focusable = true;
        Opened += (_, _) => Focus();

        // Tunnel only, and never Tunnel|Bubble: registering for both runs this handler twice
        // for one press, which is one physical press producing two actions. It cost an Escape
        // that popped a screen and then closed RomMBat in the same keystroke.
        AddHandler(
            KeyDownEvent,
            OnKey,
            Avalonia.Interactivity.RoutingStrategies.Tunnel,
            handledEventsToo: true);
    }

    /// <summary>The chrome at its design size, scaled to the display (<see cref="DesignSize"/>).</summary>
    private Viewbox Scaled(Grid chrome)
    {
        var (width, height) = DesignSize.Fit(DesignSize.Width, DesignSize.Height);
        chrome.Width = width;
        chrome.Height = height;

        SizeChanged += (_, e) =>
        {
            (chrome.Width, chrome.Height) = DesignSize.Fit(e.NewSize.Width, e.NewSize.Height);
        };

        return new Viewbox { Stretch = Stretch.Uniform, Child = chrome };
    }

    /// <summary>How wide the menu panel is: the widest list with a margin either side.</summary>
    private const double PanelWidth = 1100;

    /// <summary>
    /// EmulationStation's menu: a centered panel holding the title and the screen, over the
    /// theme's background, with the help bar along the bottom left (RB-425).
    /// </summary>
    /// <remarks>
    /// <b>The panel is a fixed size</b> where ES's fits its rows, because a screen here changes
    /// what it holds while it is open, and a panel that fitted it would grow and shrink as a
    /// sync ran or a page loaded (#490).
    /// </remarks>
    private Grid BuildChrome()
    {
        _title.FontFamily = EsTheme.TitleFont;
        _title.FontWeight = FontWeight.Bold;
        _title.FontSize = 34;
        _title.Foreground = EsTheme.Title;
        _title.Margin = new Thickness(24, 14, 24, 10);
        _title.HorizontalAlignment = HorizontalAlignment.Center;
        _title.TextTrimming = TextTrimming.CharacterEllipsis;

        _body.Margin = new Thickness(0);

        // Centered rather than pinned to the top. Most screens are far shorter than a
        // television, and left as-is the content sits in the upper third with a third of the
        // panel empty beneath it.
        _body.VerticalAlignment = VerticalAlignment.Center;

        var inside = new Grid();
        inside.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        inside.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        inside.RowDefinitions.Add(new RowDefinition(GridLength.Star));

        var rule = new Border { Height = 1, Background = EsTheme.Separator, Margin = new Thickness(0, 0, 0, 4) };
        var scroller = new ScrollViewer { Content = _body };

        Grid.SetRow(_title, 0);
        Grid.SetRow(rule, 1);
        Grid.SetRow(scroller, 2);
        inside.Children.Add(_title);
        inside.Children.Add(rule);
        inside.Children.Add(scroller);

        var panel = new Border
        {
            Background = EsTheme.MenuPanel,
            BorderBrush = EsTheme.MenuEdge,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Width = PanelWidth,
            Margin = new Thickness(0, 16, 0, 12),
            Padding = new Thickness(24, 0, 24, 8),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = inside,
        };

        _footer.HorizontalAlignment = HorizontalAlignment.Left;
        _footer.VerticalAlignment = VerticalAlignment.Center;

        // Fixed, so a screen with fewer hints, or none, does not resize the panel above it.
        _footer.Height = 40;

        var footerBar = new Border
        {
            Background = EsTheme.HelpBar,
            BorderBrush = EsTheme.Base,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(24, 6, 24, 6),
            Child = _footer,
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        Grid.SetRow(panel, 0);
        Grid.SetRow(footerBar, 1);

        // Over the panel, outside its layout, so opening a popup moves nothing underneath it.
        // The scrim darkens the panel behind it, as ES darkens what a menu opens over, or the
        // popup is the panel's own color on top of it and reads as part of it.
        Grid.SetRow(_scrim, 0);
        Grid.SetRow(_overlay, 0);

        grid.Children.Add(panel);
        grid.Children.Add(footerBar);
        grid.Children.Add(_scrim);
        grid.Children.Add(_overlay);

        return grid;
    }

    private void Poll()
    {
        var held = _gamepad?.Held() ?? (IReadOnlySet<string>)new HashSet<string>(StringComparer.Ordinal);

        // A controller arriving or leaving changes what is on screen without anyone pressing
        // anything, and it is the one change a user cannot prompt: with no pad there is no
        // input to redraw on, so a screen that only redraws on input would sit there saying
        // "no controller is connected" while they hold a working one.
        if (_gamepad?.Status.Availability is { } availability && availability != _lastAvailability)
        {
            _lastAvailability = availability;
            Render();
        }

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
            Key.Back => NavAction.PageUp,
            Key.Tab => NavAction.Alternate,
            Key.Q => NavAction.Extra,
            Key.PageUp => NavAction.PageUp,
            Key.PageDown => NavAction.PageDown,
            Key.F5 => NavAction.Start,
            Key.F6 => NavAction.Options,
            Key.Home => NavAction.PreviousGroup,
            Key.End => NavAction.NextGroup,
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

        // In capitals, as ES titles every menu (RB-423).
        _title.Text = screen.Title.ToUpperInvariant();
        _body.Content = ScreenView.Build(screen);
        _overlay.Content = ScreenView.Overlay(screen);
        _scrim.IsVisible = _overlay.Content is not null;

        _footer.Children.Clear();
        foreach (var hint in screen.Hints)
        {
            _footer.Children.Add(ScreenView.Hint(hint));
        }
    }
}
