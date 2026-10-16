using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
/// keeps every screen testable: the screens see no Avalonia type and no coordinate, only a
/// <see cref="NavAction"/>, the index of an item pointed at, or text typed.
/// <para>
/// <b>The pad, the keyboard and the mouse are all supported, and the pad is first.</b> A key
/// means what <c>es_input.cfg</c>'s keyboard entry says it does, so it is the key EmulationStation
/// uses (<see cref="KeyboardBindings"/>), and it reaches the screens as the same held names a pad
/// produces, so repeat and one-press-one-action behave identically. The mouse points at the
/// items a screen tagged, and a click is pointing followed by Accept.
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
    private readonly KeyboardBindings _keys;
    private readonly HashSet<Key> _keysDown = [];
    private Point? _pointer;
    private int _pointedAt = -1;
    private IScreen? _pointedScreen;
    private double _wheel;
    private bool _keyboardLast;
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
    private readonly LiveFollow _live;

    public ShellWindow(Navigator navigator, GamepadReader? gamepad, Action exit, KeyboardBindings? keys = null)
    {
        _navigator = navigator;
        _gamepad = gamepad;
        _keys = keys ?? KeyboardBindings.From(null);

        // Hidden until the mouse moves, because the window is shown on a television where a
        // pointer in the middle of the panel is clutter.
        ShowPointer(false);
        _exit = exit;

        // Raised from whatever thread did the work, so hop to the UI thread before touching controls.
        _live = new LiveFollow(() => Dispatcher.UIThread.Post(Render));

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

        AddHandler(KeyUpEvent, OnKeyUp, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(TextInputEvent, OnText, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerMovedEvent, OnPointerMoved, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, OnPointerPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerWheelChangedEvent, OnWheel, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);

        // A key released while another window had focus would otherwise stay held for good.
        Deactivated += (_, _) => _keysDown.Clear();
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

    private readonly Border _panel = new();

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

        var panel = _panel;
        panel.Background = EsTheme.MenuPanel;
        panel.BorderBrush = EsTheme.MenuEdge;
        panel.BorderThickness = new Thickness(1);
        panel.CornerRadius = new CornerRadius(8);
        panel.Width = PanelWidth;
        panel.Margin = new Thickness(0, 16, 0, 12);
        panel.Padding = new Thickness(24, 0, 24, 8);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.Child = inside;

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
        var held = new HashSet<string>(_gamepad?.Held() ?? new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);

        if (held.Count > 0)
        {
            SetKeyboardLast(false);
            ShowPointer(false);
        }

        // Not while typing: the letters that are buttons in ES are letters here.
        if (_navigator.Current is not ITextEntryScreen)
        {
            held.UnionWith(_keys.Held(_keysDown));
        }

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
        SetKeyboardLast(true);
        ShowPointer(false);

        if (_navigator.Current is ITextEntryScreen)
        {
            // The grid is a pad's way of typing. A physical keyboard types directly: letters
            // arrive as text, and only the keys that are not letters are actions.
            NavAction? typing = e.Key switch
            {
                Key.Enter => NavAction.Start,
                Key.Escape => NavAction.Back,
                Key.Back or Key.Delete => NavAction.PageUp,
                Key.Up => NavAction.Up,
                Key.Down => NavAction.Down,
                Key.Left => NavAction.Left,
                Key.Right => NavAction.Right,
                _ => null,
            };

            if (typing is { } typed)
            {
                e.Handled = true;
                Act(_navigator.Handle(typed));
            }

            return;
        }

        if (_keys.Names(e.Key).Count > 0)
        {
            // Held until released, and read on the next poll with everything else held.
            _keysDown.Add(e.Key);
            e.Handled = true;
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e) => _keysDown.Remove(e.Key);

    private void OnText(object? sender, TextInputEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Text) && !e.Text.Any(char.IsControl))
        {
            _navigator.Type(e.Text);
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var position = e.GetPosition(this);

        // Only real movement: a redraw under a still pointer raises a move, and showing the
        // pointer for it would undo hiding it on a key press.
        if (_pointer == position)
        {
            return;
        }

        var first = _pointer is null;
        _pointer = position;

        if (first)
        {
            return;
        }

        ShowPointer(true);

        if (TargetAt(position) is PointerTarget target)
        {
            // Once per item, so moving about inside one row does not rebuild the screen.
            if (target.Index != _pointedAt)
            {
                _pointedAt = target.Index;
                Act(_navigator.Point(target.Index));
            }
        }
        else
        {
            _pointedAt = -1;
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(this).Properties;
        ShowPointer(true);

        if (properties.IsRightButtonPressed)
        {
            e.Handled = true;
            Act(_navigator.Handle(NavAction.Back));
            return;
        }

        // The first press of a double click has already acted, and a second would choose
        // whatever the screen it opened draws under the pointer.
        if (!properties.IsLeftButtonPressed || e.ClickCount > 1)
        {
            return;
        }

        e.Handled = true;

        switch (TargetAt(e.GetPosition(this)))
        {
            case PointerTarget target:
                Act(_navigator.Click(target.Index));
                break;

            case HintTarget hint:
                Act(_navigator.Handle(hint.Action));
                break;

            case ScrimTarget:
                // Outside a popup, which is the way out of it that costs nothing.
                Act(_navigator.Handle(NavAction.Back));
                break;

            default:
                break;
        }
    }

    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        ShowPointer(true);

        // A touchpad sends many small deltas for one swipe, and a wheel one whole notch each:
        // a step per whole notch accumulated, so a swipe is not a long run of rows.
        _wheel += e.Delta.Y;

        while (Math.Abs(_wheel) >= 1)
        {
            var notch = Math.Sign(_wheel);
            _wheel -= notch;
            Act(_navigator.Wheel(notch));
        }

        e.Handled = true;
    }

    private void Act(bool stillOpen)
    {
        if (!stillOpen)
        {
            _exit();
        }
    }

    private sealed record ScrimTarget;

    /// <summary>What a drawn item under the pointer stands for, or null.</summary>
    private object? TargetAt(Point position)
    {
        if (this.InputHitTest(position) is not Visual hit)
        {
            return null;
        }

        foreach (var visual in hit.GetSelfAndVisualAncestors())
        {
            if (ReferenceEquals(visual, _scrim) && _scrim.IsVisible)
            {
                return new ScrimTarget();
            }

            if (visual is Control { Tag: PointerTarget or HintTarget } control)
            {
                return control.Tag;
            }
        }

        return null;
    }

    /// <summary>
    /// The key to draw on a footer hint, or null to draw the pad's glyph.
    /// </summary>
    /// <remarks>
    /// On a text screen the letters type, so the keys that act are the ones <see cref="OnKey"/>
    /// names there and not the ones <c>es_input.cfg</c> binds.
    /// </remarks>
    private string? KeyLabel(NavAction action) => _navigator.Current is ITextEntryScreen
        ? action switch
        {
            NavAction.Start => "Enter",
            NavAction.Back => "Esc",
            NavAction.PageUp => "Backspace",
            NavAction.PageDown => "Space",
            _ => null,
        }
        : _keys.KeyFor(action);

    private void ShowPointer(bool visible) =>
        Cursor = visible ? null : new Cursor(StandardCursorType.None);

    /// <summary>Redraws the footer when the hand changes between the pad and the keyboard.</summary>
    private void SetKeyboardLast(bool keyboard)
    {
        if (_keyboardLast == keyboard)
        {
            return;
        }

        _keyboardLast = keyboard;
        Render();
    }

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

        // What was under the pointer belonged to the last screen, not to a redraw of this one.
        if (!ReferenceEquals(screen, _pointedScreen))
        {
            _pointedScreen = screen;
            _pointedAt = -1;
        }

        _live.Follow(screen);

        // In capitals, as ES titles every menu (RB-423).
        _title.Text = screen.Title.ToUpperInvariant();
        _body.Content = ScreenView.Build(screen);
        _overlay.Content = ScreenView.Overlay(screen);
        _scrim.IsVisible = _overlay.Content is not null;

        _footer.Children.Clear();
        foreach (var hint in screen.Hints)
        {
            var key = _keyboardLast && !hint.IsDirectional ? KeyLabel(hint.Action) : null;
            var control = ScreenView.Hint(hint, key);

            // Clickable, as ES's help bar is, except the directional hint, which names no one action.
            if (control is Panel panel && !hint.IsDirectional)
            {
                panel.Background = Brushes.Transparent;
                panel.Tag = new HintTarget(hint.Action);
            }

            _footer.Children.Add(control);
        }
    }
}
