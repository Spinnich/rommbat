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
        SetEditorViewModel editor => Editor(editor.Rows, editor.Cursor, editor.Window, editor.Problem),
        BudgetViewModel budget => Editor(budget.Rows, budget.Cursor, budget.Window, null),
        ResolveViewModel resolve => Resolve(resolve),
        SyncViewModel sync => Sync(sync),

        // The same body a ListScreen draws, given the same four things. Browse is a list with a
        // pager behind it rather than a different picture, and a second copy of this would be
        // the file 7b-1 already named as the one most likely to grow worst.
        BrowseViewModel browse => List(
            browse.Rows,
            browse.Cursor,
            browse.Window,
            reading: true,
            note: browse.Note,
            isLoading: browse.IsLoading,
            loadingMessage: "Asking RomM.",
            empty: "Nothing matched. Search for something else, or widen the platform."),

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
        NavAction.Extra => 2,
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

        Control glyph = hint.IsDirectional
            ? PadGlyph()
            : FacePosition(hint.Action) is { } filled
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

        stack.Children.Add(KeyboardGrid(keyboard));
        return stack;
    }

    /// <summary>
    /// The keys, on a real grid because they span cells.
    /// </summary>
    /// <remarks>
    /// Every cell is the same size and every key is a whole number of them, which is what makes
    /// the accept key two rows tall and the space bar seven columns wide without a second
    /// layout pass. The screen owns the spans; this only places them.
    /// </remarks>
    private static Grid KeyboardGrid(OnScreenKeyboard keyboard)
    {
        const double CellWidth = 66;
        const double CellHeight = 58;
        const double Gap = 6;

        var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Center };

        for (var column = 0; column < OnScreenKeyboard.Columns; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(CellWidth, GridUnitType.Pixel));
        }

        for (var row = 0; row < OnScreenKeyboard.Rows; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition(CellHeight, GridUnitType.Pixel));
        }

        foreach (var key in keyboard.Keys)
        {
            var selected = key == keyboard.Selected;
            var face = keyboard.Face(key);

            // Shift and the layer key are modes, so they say whether they are on. Upstream
            // colours them for the same reason, and a mode nobody can see is the thing the
            // original flat grid was built to avoid.
            var engaged = (key.Kind == KeyKind.Shift && keyboard.IsShifted)
                || (key.Kind == KeyKind.Layer && keyboard.IsAlted);

            var cell = new Border
            {
                // Margin rather than size, so the box is identical selected or not and the grid
                // never shifts under the cursor.
                Margin = new Thickness(Gap / 2),
                Background = selected ? Accent : engaged ? Warn : Panel,
                BorderBrush = selected ? Ink : Panel,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                Child = new TextBlock
                {
                    Text = face,
                    Foreground = selected || engaged ? Brushes.Black : Ink,
                    FontSize = face.Length <= 3 ? 26 : 15,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };

            Grid.SetRow(cell, key.Row);
            Grid.SetColumn(cell, key.Column);
            Grid.SetRowSpan(cell, key.Height);
            Grid.SetColumnSpan(cell, key.Width);

            grid.Children.Add(cell);
        }

        return grid;
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
    private static StackPanel List(ListScreen list) => List(
        list.Rows,
        list.Cursor,
        list.Window,
        list.Reading,
        list.Note?.Invoke(),
        list.IsLoading,
        list.LoadingMessage,
        list.LoadProblem ?? list.EmptyMessage);

    /// <summary>
    /// The list body, given only what it draws.
    /// </summary>
    /// <remarks>
    /// Split out when browse arrived, because browse is a list with a pager behind it rather
    /// than a different picture, and it is not a <see cref="ListScreen"/>: it holds one page and
    /// moves by fetching, where a <c>ListScreen</c> has all its rows the moment it opens. Both
    /// arms draw through here, so the windowing, the two edge markers and the fixed width cannot
    /// be got right in one and wrong in the other, which is the failure shape this file has
    /// produced three times.
    /// </remarks>
    private static StackPanel List(
        IReadOnlyList<ListRow> rows,
        int cursor,
        ListView window,
        bool reading,
        string? note,
        bool isLoading,
        string? loadingMessage,
        string? empty)
    {
        var stack = new StackPanel
        {
            Spacing = ListWindow.RowSpacing,
            HorizontalAlignment = HorizontalAlignment.Center,

            // Fixed, not a maximum. A stack that sizes to its content is as wide as the widest
            // row currently drawn, and the drawn rows change as the window scrolls, so the
            // whole block grew and shrank under the cursor. Fixing the height was only half of
            // it and the half nobody noticed.
            Width = ListWidth,
        };

        if (note is not null)
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

        if (isLoading)
        {
            stack.Children.Add(new TextBlock
            {
                Text = loadingMessage ?? "Working.",
                Foreground = Muted,
                FontSize = 20,
                MaxWidth = 760,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            });

            return stack;
        }

        if (rows.Count == 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = empty ?? "Nothing here.",
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
        // being drawn off it, with the cursor moving somewhere invisible. The screen decides
        // the window; this only draws it.
        //
        // Both markers always, empty when there is nothing to say. Adding and removing them as
        // the cursor reaches an end changed the height of the whole block, and the block is
        // centred, so the list visibly resized and shifted while being scrolled.
        stack.Children.Add(More(window.Above, "above"));

        for (var index = window.Start; index < window.Start + window.Count; index++)
        {
            stack.Children.Add(ListItem(rows[index], index == cursor, reading));
        }

        stack.Children.Add(More(window.Below, "below"));

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
            Text = count > 0 ? $"{count} more {direction}" : string.Empty,
            Foreground = Muted,
            FontSize = 15,

            // Reserved whether or not it says anything, so the block does not change height as
            // the cursor reaches an end.
            Height = 20,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

    private static Border ListItem(ListRow row, bool selected, bool reading = false)
    {
        var lines = new StackPanel { Spacing = 3 };

        // A grid rather than a horizontal stack, so the value sits at the right edge of every
        // row instead of wherever its label happens to end. With a fixed row width that puts
        // the second column on one line down the list, which is what makes it readable across
        // a room, and it is the same reason the status screen pins its label column.
        var head = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            ],
        };

        // Dimmed rather than removed, and both halves the same, so it reads as one unavailable
        // thing rather than as a row with a missing value.
        var ink = row.Available ? Ink : Muted;

        var label = new TextBlock
        {
            Text = row.Label,
            Foreground = selected ? Brushes.Black : ink,
            FontSize = 21,

            // A name longer than the row is trimmed rather than allowed to widen it. The live
            // platform list has names from "Bally Astrocade" to "Bandai - WonderSwan Color
            // (Unofficial)".
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };

        Grid.SetColumn(label, 0);
        head.Children.Add(label);

        if (row.Value is { } value)
        {
            var right = new TextBlock
            {
                Text = value,
                Foreground = selected ? Brushes.Black : Muted,
                FontSize = 19,
                Margin = new Thickness(18, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };

            Grid.SetColumn(right, 1);
            head.Children.Add(right);
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

                // On a reading list the detail is the row, and it is a whole sentence rather
                // than a label, so it is given room for three wrapped lines and clipped past
                // them. Left to wrap freely it decides the row's height, and rows of differing
                // height inside a fixed window make the block grow and shrink while it is
                // being scrolled, which is the thing the fixed row height exists to stop.
                Height = reading ? ReadingDetailHeight : double.NaN,
                TextTrimming = reading ? TextTrimming.CharacterEllipsis : TextTrimming.None,
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

            // Every row the same height whether or not it carries a second line. A window of a
            // fixed number of rows whose heights differ is a block whose height changes as the
            // cursor moves through it, which is what made scrolling feel like zooming.
            //
            // Fixed rather than a minimum on a reading list, where the detail is a sentence
            // that wraps: a minimum is only a floor, so a two-line row and a three-line one are
            // still different heights.
            MinHeight = reading ? ReadingRowHeight : RowHeight,
            Height = reading ? ReadingRowHeight : double.NaN,
            Child = lines,
        };
    }

    /// <summary>
    /// One list row, tall enough for a label and a detail line.
    /// </summary>
    /// <remarks>
    /// Uniform on purpose, and declared beside the capacity that counts them: the window draws
    /// a fixed number of rows, so how tall one is decides how many fit.
    /// </remarks>
    private const double RowHeight = ListWindow.RowHeight;

    private const double ReadingRowHeight = ListWindow.ReadingRowHeight;

    /// <summary>Three wrapped lines of detail, which is what makes a reading row its height.</summary>
    private const double ReadingDetailHeight = 66;

    /// <summary>
    /// How wide a list is, fixed so it cannot breathe as the window scrolls.
    /// </summary>
    /// <remarks>
    /// Wide enough for the longest platform name the live install carries with its folder
    /// beside it, and narrow enough to leave margins on a 720p display.
    /// </remarks>
    private const double ListWidth = 980;

    /// <summary>
    /// A form whose values are stepped rather than typed.
    /// </summary>
    /// <remarks>
    /// A steppable row is drawn with a chevron either side of its value, which is the
    /// affordance for "this moves with left and right" that needs neither words nor a button
    /// name.
    /// </remarks>
    private static StackPanel Editor(
        IReadOnlyList<EditorRow> rows,
        int cursor,
        ListView window,
        string? problem)
    {
        var stack = new StackPanel
        {
            Spacing = 14,
            HorizontalAlignment = HorizontalAlignment.Center,

            // Fixed, not a maximum, and this is the second screen to learn it. A maximum makes
            // the block as wide as its widest drawn row, and the drawn rows change as the
            // window scrolls, so the whole thing grows and shrinks under the cursor. The list
            // was fixed for that in round three; this screen only started scrolling later, and
            // inherited the bug the moment it did.
            Width = ListWidth,
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

        // Both markers always, empty when there is nothing to say, because the block is centred
        // and adding one as the cursor reaches an end resizes the whole thing under the thumb.
        stack.Children.Add(More(window.Above, "above"));

        for (var index = window.Start; index < window.Start + window.Count; index++)
        {
            stack.Children.Add(EditorItem(rows[index], index == cursor));
        }

        stack.Children.Add(More(window.Below, "below"));

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

        if (resolve.Outcome is { } finished)
        {
            stack.Children.Add(Outcome(finished));
        }

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

        if (resolve.Progressing is { } where)
        {
            stack.Children.Add(new TextBlock
            {
                Text = where,
                Foreground = Ink,
                FontSize = 21,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

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
                Width = SyncColumn,
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new Border
                {
                    Background = Accent,
                    CornerRadius = new CornerRadius(6),
                    Width = Math.Max(6, SyncColumn * fraction),
                    HorizontalAlignment = HorizontalAlignment.Left,
                },
            });
        }

        return stack;
    }

    /// <summary>
    /// The fixed width every line of the sync screen is laid out in.
    /// </summary>
    /// <remarks>
    /// <b>A centred <c>TextBlock</c> is as wide as its text, so it re-centres whenever the text
    /// changes width.</b> This screen rebuilds up to eight times a second and almost every line
    /// on it is a number, so each redraw nudged the whole column sideways. A hands-on pass on a
    /// set of small ROMs called it double vision. Giving every volatile line the bar's own width
    /// and centring the text inside it makes the box still and lets only the glyphs change.
    /// </remarks>
    private const double SyncColumn = 620;

    /// <summary>
    /// A sync, which is the busiest screen here and the only one that spends the user's disk.
    /// </summary>
    /// <remarks>
    /// <b>Fixed fields that update in place, plus problems that accumulate.</b> A live tail of
    /// forty games in three minutes is unreadable from a sofa and the count already says how
    /// many went by; what cannot be reconstructed afterwards is what failed, so that is what
    /// is kept on screen.
    /// <para>
    /// <b>Read once, into a local.</b> The value is published from whatever thread is doing the
    /// transfer, so reading the property twice while building this could draw a game name from
    /// one moment beside a count from another.
    /// </para>
    /// </remarks>
    private static StackPanel Sync(SyncViewModel sync)
    {
        var state = sync.State;

        var stack = new StackPanel
        {
            Spacing = 18,
            HorizontalAlignment = HorizontalAlignment.Center,
            MaxWidth = 900,
        };

        if (state.Outcome is { } finished)
        {
            stack.Children.Add(Outcome(finished));
        }

        stack.Children.Add(new TextBlock
        {
            Text = state.Detail,
            Foreground = Ink,
            FontSize = 21,
            MaxWidth = 860,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        if (state.Pass is { } pass)
        {
            stack.Children.Add(new TextBlock
            {
                Text = pass,
                Foreground = Accent,
                FontSize = 15,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        if (state.Game is { } game)
        {
            stack.Children.Add(new TextBlock
            {
                Text = game,
                Foreground = Ink,
                FontSize = 24,
                Width = SyncColumn,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        // The game's own progress as text, not a second bar. On a set of small ROMs a per-game
        // bar fills and empties several times a second, which a hands-on pass reported as
        // flashing rather than as progress.
        if (state.GameProgress is { } inGame)
        {
            stack.Children.Add(new TextBlock
            {
                Text = inGame,
                Foreground = Muted,
                FontSize = 15,
                Width = SyncColumn,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        if (state.Counted is { } counted)
        {
            stack.Children.Add(new TextBlock
            {
                Text = counted,
                Foreground = Muted,
                FontSize = 21,
                Width = SyncColumn,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        // One bar, for the run, measured in bytes. Games are not the same size, so a bar over
        // the count of them moves in lurches that mean nothing: forty cartridges and one disc
        // are both "1 of 2".
        if (state.Fraction is { } fraction)
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

        if (state.Transferred is { } transferred)
        {
            // Two anchored halves rather than one centred line. The rate and the total change
            // independently, and a single centred string moves both of them whenever either
            // changes width. Here the transferred count grows leftwards from a fixed edge and
            // the rate grows rightwards from another, so neither pushes the other.
            var line = new Grid
            {
                Width = SyncColumn,
                HorizontalAlignment = HorizontalAlignment.Center,
                ColumnDefinitions = new ColumnDefinitions("*,*"),
            };

            var moved = new TextBlock
            {
                Text = transferred,
                Foreground = Muted,
                FontSize = 15,
                TextAlignment = TextAlignment.Right,
                Margin = new Thickness(0, 0, 18, 0),
            };

            Grid.SetColumn(moved, 0);
            line.Children.Add(moved);

            if (state.Speed is { } speed)
            {
                var rate = new TextBlock
                {
                    Text = speed,
                    Foreground = Muted,
                    FontSize = 15,
                    TextAlignment = TextAlignment.Left,
                    Margin = new Thickness(18, 0, 0, 0),
                };

                Grid.SetColumn(rate, 1);
                line.Children.Add(rate);
            }

            stack.Children.Add(line);
        }

        // On this screen because this is where it is being spent.
        if (state.Budget is { } budget)
        {
            stack.Children.Add(new TextBlock
            {
                Text = $"Disk used  {budget}",
                Foreground = Muted,
                FontSize = 15,
                Width = SyncColumn,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        // Beside the budget, because that is what took them, and with no offer to fix it.
        if (state.Held is { } held)
        {
            stack.Children.Add(new TextBlock
            {
                Text = held,
                Foreground = Muted,
                FontSize = 15,
                Width = SyncColumn,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        if (state.Problems.Count > 0)
        {
            stack.Children.Add(Problems(state.Problems));
        }

        return stack;
    }

    /// <summary>
    /// What went wrong, oldest first, bounded to what fits.
    /// </summary>
    /// <remarks>
    /// <b>The newest are kept when there are too many.</b> A run that fails every game produces
    /// one line each, and the first six of forty identical sentences are the least useful six:
    /// the count says how many there were and the tail says what was happening most recently.
    /// <para>
    /// <b>The rest are reachable, which they were not.</b> A hands-on pass hit twenty-seven
    /// problems and could read six, with no press that offered the other twenty-one. The count
    /// is <see cref="SyncViewModel.ProblemsShown"/> so the footer's offer and this cut cannot
    /// drift apart.
    /// </para>
    /// </remarks>
    private static StackPanel Problems(IReadOnlyList<string> problems)
    {
        var stack = new StackPanel { Spacing = 6, MaxWidth = 860 };

        stack.Children.Add(new TextBlock
        {
            Text = problems.Count == 1 ? "PROBLEM" : $"PROBLEMS ({problems.Count})",
            Foreground = Accent,
            FontSize = 13,
        });

        foreach (var problem in problems.Skip(Math.Max(0, problems.Count - SyncViewModel.ProblemsShown)))
        {
            stack.Children.Add(new TextBlock
            {
                Text = problem,
                Foreground = Muted,
                FontSize = 15,
                MaxWidth = 860,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        return stack;
    }

    /// <summary>
    /// That the work on this screen has stopped happening, said in as many words.
    /// </summary>
    /// <remarks>
    /// <b>A finished progress bar and a stalled one are the same picture.</b> A hands-on pass
    /// sat on a resolve at 107 of 107 under a full bar and could not tell whether the last game
    /// had hung. Drawn in the accent colour above the sentence, in the same treatment the
    /// problems heading already uses, so it reads as a label on the screen rather than as one
    /// more line of detail. The word itself comes from the view model, because which one
    /// applies is a fact about the outcome.
    /// </remarks>
    private static TextBlock Outcome(string word) =>
        new()
        {
            Text = word.ToUpperInvariant(),
            Foreground = Accent,
            FontSize = 15,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

    /// <summary>A screen whose only content is one sentence about work in progress.</summary>
    private static StackPanel Working(string detail)
    {
        var stack = new StackPanel
        {
            Spacing = 22,
            HorizontalAlignment = HorizontalAlignment.Center,
            MaxWidth = 900,
        };

        stack.Children.Add(new TextBlock
        {
            Text = detail,
            Foreground = Ink,
            FontSize = 21,
            MaxWidth = 860,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

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
    /// <summary>
    /// A cross, for a hint that means every direction rather than one of them.
    /// </summary>
    /// <remarks>
    /// A shape rather than four lit dots, because four lit dots is what a face-button glyph
    /// with nothing selected would look like and the two must not be confusable.
    /// </remarks>
    private static Canvas PadGlyph()
    {
        const double Size = 26;
        const double Arm = 9;

        var canvas = new Canvas { Width = Size, Height = Size };

        foreach (var (width, height) in ((double, double)[])[(Arm, Size), (Size, Arm)])
        {
            var bar = new Rectangle
            {
                Width = width,
                Height = height,
                Fill = Accent,
                RadiusX = 2,
                RadiusY = 2,
            };

            Canvas.SetLeft(bar, (Size - width) / 2);
            Canvas.SetTop(bar, (Size - height) / 2);
            canvas.Children.Add(bar);
        }

        return canvas;
    }

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
