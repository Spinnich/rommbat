using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using RomMBat.Core.Identity;
using RomMBat.UI.Input;
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
    private static readonly IBrush Warn = new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x7A));

    public static Control Build(IScreen screen) => screen switch
    {
        StatusViewModel status => Status(status),
        OnScreenKeyboard keyboard => Keyboard(keyboard),
        PairingViewModel pairing => Pairing(pairing),
        MessageScreen message => Message(message),
        ListScreen list => List(list),
        SetEditorViewModel editor => Editor(editor.Rows, editor.Cursor, editor.Problem),
        BudgetViewModel budget => Editor(budget.Rows, budget.Cursor, null),
        ResolveViewModel resolve => Resolve(resolve),
        _ => new TextBlock { Text = screen.Title, Foreground = Ink },
    };

    /// <summary>
    /// Where each face-button action sits on the pad, clockwise from the bottom.
    /// </summary>
    /// <remarks>
    /// <b>Position is the only thing every controller layout agrees on.</b> The bottom face
    /// button is A on an Xbox pad, Cross on a DualSense and B on a Switch Pro, so a footer that
    /// prints a letter is wrong on two of the three, and the live install has all three
    /// configured. EmulationStation draws a four-dot diamond with one dot filled for exactly
    /// this reason, and copying it costs nothing.
    /// <para>
    /// Read from <c>es_input.cfg</c>'s own names rather than from labels: <c>a</c> is the
    /// bottom button, <c>b</c> the right, <c>y</c> the left and <c>x</c> the top. The last two
    /// are the ones printed X and Y the other way round (finding 225).
    /// </para>
    /// </remarks>
    private static readonly (double X, double Y)[] Diamond =
    [
        (0.5, 1.0),
        (1.0, 0.5),
        (0.5, 0.0),
        (0.0, 0.5),
    ];

    private static int? FacePosition(NavAction action) => action switch
    {
        NavAction.Accept => 0,
        NavAction.Back => 1,
        NavAction.Alternate => 3,
        _ => null,
    };

    /// <summary>The shoulders and Start, which every layout does spell the same way.</summary>
    private static string ButtonWord(NavAction action) => action switch
    {
        NavAction.Start => "Start",
        NavAction.PageUp => "L1",
        NavAction.PageDown => "R1",
        _ => action.ToString(),
    };

    public static Control Hint(FooterHint hint)
    {
        ArgumentNullException.ThrowIfNull(hint);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        Control glyph = FacePosition(hint.Action) is { } filled
            ? FaceGlyph(filled)
            : new TextBlock { Text = ButtonWord(hint.Action), Foreground = Accent, FontSize = 17 };

        row.Children.Add(new Border
        {
            Background = Panel,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 3, 10, 3),
            Child = glyph,
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
        // The block is centred; the rows inside it are not. A label-and-value list read across
        // a room needs its labels to start on one line, and centring each row destroys that.
        var stack = new StackPanel
        {
            Spacing = 22,
            HorizontalAlignment = HorizontalAlignment.Center,
            MaxWidth = 900,
        };

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
        var stack = new StackPanel { Spacing = 18, HorizontalAlignment = HorizontalAlignment.Center };

        stack.Children.Add(new TextBlock
        {
            Text = keyboard.Prompt,
            Foreground = Muted,
            FontSize = 19,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        // What has been typed, in a box, so it reads as the thing being edited.
        stack.Children.Add(new Border
        {
            Background = Panel,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20, 14, 20, 14),
            MinWidth = 620,
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock
            {
                Text = keyboard.Text.Length == 0 ? " " : keyboard.Text,
                Foreground = Ink,
                FontSize = 30,
                FontFamily = new FontFamily("Consolas, monospace"),
                HorizontalAlignment = HorizontalAlignment.Center,
            },
        });

        // The refusal from Core, where the eye already is rather than at the top of the screen.
        if (keyboard.Problem is { } problem)
        {
            stack.Children.Add(new TextBlock
            {
                Text = problem,
                Foreground = Warn,
                FontSize = 17,
                MaxWidth = 700,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        var grid = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };

        for (var r = 0; r < keyboard.Keys.Count; r++)
        {
            var line = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            for (var c = 0; c < keyboard.Keys[r].Length; c++)
            {
                var selected = r == keyboard.CursorRow && c == keyboard.CursorColumn;

                line.Children.Add(new Border
                {
                    // Fill and ring only. The box is the same size selected or not, so the grid
                    // never shifts under the cursor.
                    Width = 62,
                    Height = 62,
                    Background = selected ? Accent : Panel,
                    BorderBrush = selected ? Ink : Panel,
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(8),
                    Child = new TextBlock
                    {
                        Text = keyboard.Keys[r][c].ToString(),
                        Foreground = selected ? Brushes.Black : Ink,
                        FontSize = 26,
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

    private static StackPanel Pairing(PairingViewModel pairing)
    {
        var stack = new StackPanel { Spacing = 20, HorizontalAlignment = HorizontalAlignment.Center };

        stack.Children.Add(new TextBlock
        {
            Text = pairing.Detail,
            Foreground = Ink,
            FontSize = 20,
            MaxWidth = 900,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        if (pairing.QrCode is { } qr)
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 44,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            row.Children.Add(Qr(qr));

            var side = new StackPanel { Spacing = 12, VerticalAlignment = VerticalAlignment.Center };

            side.Children.Add(Labelled("Address", pairing.VerificationUri?.ToString() ?? string.Empty, 18));
            side.Children.Add(Labelled("Code", pairing.DisplayCode ?? string.Empty, 40));

            if (pairing.Remaining is { } left)
            {
                side.Children.Add(Labelled(
                    "Expires in",
                    $"{(int)left.TotalMinutes}:{left.Seconds:00}",
                    22));
            }

            side.Children.Add(Labelled("Asks for", string.Join(", ", PairingViewModel.RequestedScopes), 15));

            row.Children.Add(side);
            stack.Children.Add(row);
        }

        if (pairing.Completion is { IsPaired: true } done)
        {
            stack.Children.Add(Labelled("Device", done.RomMDeviceId ?? "unknown", 18));
            stack.Children.Add(Labelled("Granted", string.Join(", ", done.Scopes.All), 15));

            // A feature quietly missing is worse than a late 403, so the narrowing is on screen
            // at the moment it happens rather than only later on the status screen.
            foreach (var (requirement, missing) in done.Scopes.Degradations)
            {
                stack.Children.Add(Labelled(
                    "Turned off",
                    $"{requirement.Name} (missing {string.Join(", ", missing)})",
                    15));
            }
        }

        return stack;
    }

    /// <summary>
    /// A list of rows with the cursor on one of them.
    /// </summary>
    /// <remarks>
    /// <b>An unavailable row is dimmed and keeps its reason.</b> Hiding it would be tidier and
    /// would teach the user nothing: the commonest case is a scope this pairing was not
    /// granted, which is fixable by pairing again, and a row that is simply absent says none
    /// of that.
    /// </remarks>
    private static StackPanel List(ListScreen list)
    {
        var stack = new StackPanel
        {
            Spacing = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            MaxWidth = 980,
        };

        if (list.Note is { } note)
        {
            stack.Children.Add(new TextBlock
            {
                Text = note,
                Foreground = Muted,
                FontSize = 17,
                MaxWidth = 900,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6),
            });
        }

        if (list.Rows.Count == 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = list.EmptyMessage ?? "Nothing here.",
                Foreground = Muted,
                FontSize = 20,
                MaxWidth = 760,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            });

            return stack;
        }

        // Windowed, because drawing every row does not scroll: the folder picker is about a
        // hundred systems on a real install and everything past the height of the display was
        // being drawn off it, with the cursor moving somewhere invisible.
        var window = ListWindow.Compute(list.Cursor, list.Rows.Count);

        if (window.Above > 0)
        {
            stack.Children.Add(More(window.Above, "above"));
        }

        for (var index = window.Start; index < window.Start + window.Count; index++)
        {
            stack.Children.Add(ListItem(list.Rows[index], index == list.Cursor));
        }

        if (window.Below > 0)
        {
            stack.Children.Add(More(window.Below, "below"));
        }

        return stack;
    }

    /// <summary>
    /// How much of the list is off screen, said rather than implied.
    /// </summary>
    /// <remarks>
    /// A window with nothing marking its edges reads as the whole list, which is worse than
    /// the bug it replaces: the user stops looking rather than keeps scrolling.
    /// </remarks>
    private static TextBlock More(int count, string direction) =>
        new()
        {
            Text = $"{count} more {direction}",
            Foreground = Muted,
            FontSize = 15,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

    private static Border ListItem(ListRow row, bool selected)
    {
        var lines = new StackPanel { Spacing = 3 };
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 18 };

        // Dimmed rather than removed, and both halves the same, so it reads as one unavailable
        // thing rather than as a row with a missing value.
        var ink = row.Available ? Ink : Muted;

        head.Children.Add(new TextBlock
        {
            Text = row.Label,
            Foreground = selected ? Brushes.Black : ink,
            FontSize = 21,
            MinWidth = 300,
        });

        if (row.Value is { } value)
        {
            head.Children.Add(new TextBlock
            {
                Text = value,
                Foreground = selected ? Brushes.Black : Muted,
                FontSize = 19,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        lines.Children.Add(head);

        if (row.Detail is { } detail)
        {
            lines.Children.Add(new TextBlock
            {
                Text = detail,
                Foreground = selected ? Brushes.Black : Muted,
                FontSize = 16,
                MaxWidth = 860,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        return new Border
        {
            // Fill and ring only. A row is the same size selected or not, so a held d-pad never
            // makes the list shift under the cursor.
            Background = selected ? Accent : Panel,
            BorderBrush = selected ? Ink : Panel,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18, 12, 18, 12),
            Child = lines,
        };
    }

    /// <summary>
    /// A form whose values are stepped rather than typed.
    /// </summary>
    /// <remarks>
    /// A steppable row is drawn with a chevron either side of its value, which is the
    /// affordance for "this moves with left and right" that needs neither words nor a button
    /// name.
    /// </remarks>
    private static StackPanel Editor(IReadOnlyList<EditorRow> rows, int cursor, string? problem)
    {
        var stack = new StackPanel
        {
            Spacing = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            MaxWidth = 980,
        };

        if (problem is { } text)
        {
            // Where the eye already is, rather than at the top of the screen.
            stack.Children.Add(new TextBlock
            {
                Text = text,
                Foreground = Warn,
                FontSize = 18,
                MaxWidth = 860,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6),
            });
        }

        for (var index = 0; index < rows.Count; index++)
        {
            stack.Children.Add(EditorItem(rows[index], index == cursor));
        }

        return stack;
    }

    private static Border EditorItem(EditorRow row, bool selected)
    {
        var lines = new StackPanel { Spacing = 3 };
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 18 };

        head.Children.Add(new TextBlock
        {
            Text = row.Label,
            Foreground = selected ? Brushes.Black : Ink,
            FontSize = 21,
            MinWidth = 300,
        });

        head.Children.Add(new TextBlock
        {
            Text = row.Steps ? $"‹  {row.Value}  ›" : row.Value,
            Foreground = selected ? Brushes.Black : Muted,
            FontSize = 19,
            VerticalAlignment = VerticalAlignment.Center,
        });

        lines.Children.Add(head);

        if (row.Detail is { } detail)
        {
            lines.Children.Add(new TextBlock
            {
                Text = detail,
                Foreground = selected ? Brushes.Black : Muted,
                FontSize = 16,
                MaxWidth = 860,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        return new Border
        {
            Background = selected ? Accent : Panel,
            BorderBrush = selected ? Ink : Panel,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18, 12, 18, 12),
            Child = lines,
        };
    }

    /// <summary>
    /// A resolve while it runs.
    /// </summary>
    /// <remarks>
    /// <b>The count is the point of the screen.</b> A platform resolve measured 8m 15s against
    /// a live instance, and one that cannot show movement is, from a sofa, the same screen as
    /// a hung one. The bar appears only once the server has said how big the scope is, because
    /// before that it would sit at zero and look stuck.
    /// </remarks>
    private static StackPanel Resolve(ResolveViewModel resolve)
    {
        var stack = new StackPanel
        {
            Spacing = 22,
            HorizontalAlignment = HorizontalAlignment.Center,
            MaxWidth = 900,
        };

        stack.Children.Add(new TextBlock
        {
            Text = resolve.Detail,
            Foreground = Ink,
            FontSize = 21,
            MaxWidth = 860,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        if (resolve.Counted is { } counted)
        {
            stack.Children.Add(new TextBlock
            {
                Text = counted,
                Foreground = Muted,
                FontSize = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        if (resolve.Progress?.Fraction is { } fraction)
        {
            stack.Children.Add(new Border
            {
                Background = Panel,
                CornerRadius = new CornerRadius(6),
                Height = 18,
                Width = 620,
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new Border
                {
                    Background = Accent,
                    CornerRadius = new CornerRadius(6),
                    Width = Math.Max(6, 620 * fraction),
                    HorizontalAlignment = HorizontalAlignment.Left,
                },
            });
        }

        return stack;
    }

    private static StackPanel Labelled(string label, string value, double size)
    {
        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(new TextBlock { Text = label.ToUpperInvariant(), Foreground = Accent, FontSize = 13 });
        stack.Children.Add(new TextBlock
        {
            Text = value,
            Foreground = Ink,
            FontSize = size,
            MaxWidth = 620,
            TextWrapping = TextWrapping.Wrap,
        });

        return stack;
    }

    /// <summary>
    /// Draws the QR as one path of dark squares.
    /// </summary>
    /// <remarks>
    /// White background and a real quiet zone, both of which a scanner needs. The matrix
    /// already carries the quiet zone, so the border here is only the visible frame.
    /// </remarks>
    private static Border Qr(QrMatrix matrix)
    {
        const int Module = 6;

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            for (var row = 0; row < matrix.Size; row++)
            {
                for (var column = 0; column < matrix.Size; column++)
                {
                    if (!matrix.IsDark(row, column))
                    {
                        continue;
                    }

                    var x = column * Module;
                    var y = row * Module;
                    context.BeginFigure(new Point(x, y), isFilled: true);
                    context.LineTo(new Point(x + Module, y));
                    context.LineTo(new Point(x + Module, y + Module));
                    context.LineTo(new Point(x, y + Module));
                    context.EndFigure(isClosed: true);
                }
            }
        }

        return new Border
        {
            Background = Brushes.White,
            Padding = new Thickness(10),
            CornerRadius = new CornerRadius(6),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new Avalonia.Controls.Shapes.Path
            {
                Data = geometry,
                Fill = Brushes.Black,
                Width = matrix.Size * Module,
                Height = matrix.Size * Module,
            },
        };
    }

    private static TextBlock Message(MessageScreen message) =>
        new()
        {
            Text = message.Message,
            Foreground = Ink,
            FontSize = 22,
            MaxWidth = 900,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

    /// <summary>
    /// Four dots in a diamond, with the one this action lives on filled.
    /// </summary>
    /// <remarks>
    /// <b>The three unfilled dots are the whole point and have to be visible.</b> They are what
    /// turns one lit dot into a <i>position</i>; without them the glyph is a blue speck that
    /// says nothing. Drawn as outlined rings rather than filled with <c>Panel</c>, which is
    /// within a few values of the footer's own background and disappeared on a television.
    /// </remarks>
    private static Canvas FaceGlyph(int filled)
    {
        const double Size = 26;
        const double Dot = 8;

        var canvas = new Canvas { Width = Size, Height = Size };

        for (var i = 0; i < Diamond.Length; i++)
        {
            var (x, y) = Diamond[i];
            var lit = i == filled;

            var dot = new Ellipse
            {
                Width = Dot,
                Height = Dot,
                Fill = lit ? Accent : Brushes.Transparent,
                Stroke = lit ? Accent : Muted,
                StrokeThickness = 1.5,
            };

            Canvas.SetLeft(dot, x * (Size - Dot));
            Canvas.SetTop(dot, y * (Size - Dot));
            canvas.Children.Add(dot);
        }

        return canvas;
    }
}
