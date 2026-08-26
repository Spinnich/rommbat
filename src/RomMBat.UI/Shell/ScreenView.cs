using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using RomMBat.UI.Screens;

namespace RomMBat.UI.Shell;

/// <summary>
/// Draws a screen. Holds no state and decides nothing.
/// </summary>
/// <remarks>
/// <b>Every method here is a pure function of a screen.</b> Nothing is cached, nothing is
/// mutated and no control keeps a reference to a view model, so a redraw is always correct and
/// the screens stay free of Avalonia types.
/// <para>
/// <b>Focus never moves an element.</b> The selected key on the keyboard changes its fill and
/// gains a ring; it does not grow, shift or re-lay-out. A focus style that moves things makes a
/// gamepad grid feel unanchored, which is Argosy's convention and the right one.
/// </para>
/// </remarks>
internal static class ScreenView
{
    private static readonly IBrush Ink = Brushes.White;
    private static readonly IBrush Muted = new SolidColorBrush(Color.FromRgb(0x9A, 0xA3, 0xB2));
    private static readonly IBrush Accent = new SolidColorBrush(Color.FromRgb(0x6E, 0xA8, 0xFE));
    private static readonly IBrush Panel = new SolidColorBrush(Color.FromRgb(0x1B, 0x1F, 0x29));

    public static Control Build(IScreen screen) => screen switch
    {
        StatusViewModel status => Status(status),
        OnScreenKeyboard keyboard => Keyboard(keyboard),
        MessageScreen message => Message(message),
        _ => new TextBlock { Text = screen.Title, Foreground = Ink },
    };

    public static Control Hint(FooterHint hint)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        row.Children.Add(new Border
        {
            Background = Panel,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 3, 10, 3),
            Child = new TextBlock { Text = hint.Button, Foreground = Accent, FontSize = 17 },
        });

        row.Children.Add(new TextBlock
        {
            Text = hint.Label,
            Foreground = Muted,
            FontSize = 17,
            VerticalAlignment = VerticalAlignment.Center,
        });

        return row;
    }

    private static StackPanel Status(StatusViewModel status)
    {
        var stack = new StackPanel { Spacing = 22 };

        foreach (var section in status.Sections())
        {
            var panel = new StackPanel { Spacing = 6 };

            panel.Children.Add(new TextBlock
            {
                Text = section.Title.ToUpperInvariant(),
                Foreground = Accent,
                FontSize = 15,
                Margin = new Thickness(0, 0, 0, 4),
            });

            foreach (var row in section.Rows)
            {
                panel.Children.Add(Row(row));
            }

            stack.Children.Add(panel);
        }

        return stack;
    }

    private static StackPanel Row(StatusRow row)
    {
        var lines = new StackPanel { Spacing = 2 };
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };

        line.Children.Add(new TextBlock
        {
            Text = row.Label,
            Foreground = Muted,
            FontSize = 19,
            Width = 220,
        });

        line.Children.Add(new TextBlock { Text = row.Value, Foreground = Ink, FontSize = 19 });
        lines.Children.Add(line);

        if (row.Detail is { } detail)
        {
            lines.Children.Add(new TextBlock
            {
                Text = detail,
                Foreground = Muted,
                FontSize = 16,
                Margin = new Thickness(234, 0, 0, 6),
                TextWrapping = TextWrapping.Wrap,
            });
        }

        return lines;
    }

    private static StackPanel Keyboard(OnScreenKeyboard keyboard)
    {
        var stack = new StackPanel { Spacing = 20 };

        stack.Children.Add(new TextBlock
        {
            Text = keyboard.Prompt,
            Foreground = Muted,
            FontSize = 19,
        });

        // What has been typed, in a box, so it reads as the thing being edited.
        stack.Children.Add(new Border
        {
            Background = Panel,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18, 14, 18, 14),
            Child = new TextBlock
            {
                Text = keyboard.Text.Length == 0 ? " " : keyboard.Text,
                Foreground = Ink,
                FontSize = 30,
                FontFamily = new FontFamily("Consolas, monospace"),
            },
        });

        var grid = new StackPanel { Spacing = 8 };

        for (var r = 0; r < OnScreenKeyboard.Grid.Count; r++)
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

            for (var c = 0; c < OnScreenKeyboard.Grid[r].Length; c++)
            {
                var selected = r == keyboard.CursorRow && c == keyboard.CursorColumn;

                line.Children.Add(new Border
                {
                    // Fill and ring only. The box is the same size selected or not, so the grid
                    // never shifts under the cursor.
                    Width = 52,
                    Height = 52,
                    Background = selected ? Accent : Panel,
                    BorderBrush = selected ? Ink : Panel,
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(8),
                    Child = new TextBlock
                    {
                        Text = OnScreenKeyboard.Grid[r][c].ToString(),
                        Foreground = selected ? Brushes.Black : Ink,
                        FontSize = 24,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                });
            }

            grid.Children.Add(line);
        }

        stack.Children.Add(grid);
        return stack;
    }

    private static TextBlock Message(MessageScreen message) =>
        new TextBlock
        {
            Text = message.Message,
            Foreground = Ink,
            FontSize = 22,
            MaxWidth = 900,
            TextWrapping = TextWrapping.Wrap,
        };
}
