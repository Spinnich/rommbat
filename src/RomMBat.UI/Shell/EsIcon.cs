using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace RomMBat.UI.Shell;

/// <summary>One shape of an icon, as path markup, either filled or stroked.</summary>
/// <param name="Data">Avalonia path markup, nonzero fill rule, in the icon's 64-unit box.</param>
/// <param name="Filled">True to fill the shape, false to stroke its outline.</param>
/// <param name="StrokeWidth">How wide the outline is, when it is stroked.</param>
internal sealed record IconShape(string Data, bool Filled, double StrokeWidth);

/// <summary>
/// Reads EmulationStation's help icons, <c>resources/help/*.svg</c>, into shapes.
/// </summary>
/// <remarks>
/// <b>A reader of the few elements those files use, rather than an SVG package.</b> The one
/// built for Avalonia 12 moves SkiaSharp and HarfBuzzSharp a major version past what Avalonia
/// ships against, and ES's icons are circles, polygons, lines and plain paths in a 64-unit box,
/// drawn in one color that ES replaces with the theme's tint. Color is ignored for that reason:
/// a shape is filled or stroked, and the shell paints it.
/// <para>
/// <b>A file using anything else is refused whole</b>, and the hint falls back to the drawn
/// glyph. A transform or a clip skipped would draw the icon wrong, which is worse than drawing
/// RomMBat's own. Holds no Avalonia types, so the suite reads it without a window.
/// </para>
/// </remarks>
internal static class EsIcon
{
    /// <summary>The side of the box every help icon is drawn in.</summary>
    public const double Size = 64;

    private static readonly HashSet<string> Ignored = new(StringComparer.Ordinal)
    {
        "metadata", "defs", "namedview", "title", "desc",
    };

    private static readonly HashSet<string> Unsupported = new(StringComparer.Ordinal)
    {
        "transform", "clip-path", "mask", "filter",
    };

    /// <summary>The shapes in the file, or null when it is missing, unreadable or uses something unsupported.</summary>
    public static IReadOnlyList<IconShape>? Read(string path)
    {
        try
        {
            return File.Exists(path) ? Parse(File.ReadAllText(path)) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The shapes in an SVG document, in drawing order, or null when it cannot be drawn faithfully.</summary>
    public static IReadOnlyList<IconShape>? Parse(string svg)
    {
        XDocument document;

        try
        {
            document = XDocument.Parse(svg);
        }
        catch (XmlException)
        {
            return null;
        }

        if (document.Root is not { Name.LocalName: "svg" } root
            || !IsDefaultBox(root.Attribute("viewBox")?.Value))
        {
            return null;
        }

        var shapes = new List<IconShape>();
        return Walk(root, shapes) && shapes.Count > 0 ? shapes : null;
    }

    private static bool IsDefaultBox(string? viewBox) =>
        viewBox is null || Numbers(viewBox) is [0, 0, Size, Size];

    private static bool Walk(XElement parent, List<IconShape> shapes)
    {
        foreach (var element in parent.Elements())
        {
            var name = element.Name.LocalName;

            if (Ignored.Contains(name))
            {
                continue;
            }

            if (element.Attributes().Any(attribute => Unsupported.Contains(attribute.Name.LocalName)))
            {
                return false;
            }

            if (name == "g")
            {
                if (!Walk(element, shapes))
                {
                    return false;
                }

                continue;
            }

            var data = name switch
            {
                "path" => element.Attribute("d")?.Value,
                "circle" => Ellipse(element, "r", "r"),
                "ellipse" => Ellipse(element, "rx", "ry"),
                "polygon" => Points(element, close: true),
                "polyline" => Points(element, close: false),
                "line" => Line(element),
                _ => null,
            };

            if (data is null)
            {
                return false;
            }

            // Nonzero, SVG's default. Avalonia's markup defaults to even-odd, which punches a
            // hole wherever two subpaths of one shape overlap.
            data = "F1 " + data;

            // A line has no inside, so SVG never fills one whatever its fill says.
            if (name != "line" && Paint(element, "fill", "#000") is not "none")
            {
                shapes.Add(new IconShape(data, Filled: true, StrokeWidth: 0));
            }

            if (Paint(element, "stroke", "none") is not "none")
            {
                var width = Paint(element, "stroke-width", "1");
                shapes.Add(new IconShape(
                    data,
                    Filled: false,
                    StrokeWidth: double.TryParse(width, NumberStyles.Float, CultureInfo.InvariantCulture, out var w) ? w : 1));
            }
        }

        return true;
    }

    /// <summary>A presentation property from the element, its <c>style</c>, or the nearest group that sets it.</summary>
    private static string Paint(XElement element, string property, string fallback)
    {
        for (var node = element; node is not null; node = node.Parent)
        {
            if (node.Attribute("style")?.Value is { } style)
            {
                foreach (var declaration in style.Split(';'))
                {
                    var parts = declaration.Split(':', 2);

                    if (parts.Length == 2 && parts[0].Trim() == property)
                    {
                        return parts[1].Trim();
                    }
                }
            }

            if (node.Attribute(property)?.Value is { } value)
            {
                return value.Trim();
            }
        }

        return fallback;
    }

    private static string? Ellipse(XElement element, string rxName, string ryName)
    {
        if (!TryNumber(element, "cx", 0, out var cx) || !TryNumber(element, "cy", 0, out var cy)
            || !TryNumber(element, rxName, null, out var rx) || !TryNumber(element, ryName, null, out var ry))
        {
            return null;
        }

        // Two half arcs, because one arc from a point back to itself draws nothing.
        return Invariant($"M{cx - rx},{cy} A{rx},{ry} 0 1 0 {cx + rx},{cy} A{rx},{ry} 0 1 0 {cx - rx},{cy} Z");
    }

    private static string? Line(XElement element) =>
        TryNumber(element, "x1", 0, out var x1) && TryNumber(element, "y1", 0, out var y1)
            && TryNumber(element, "x2", 0, out var x2) && TryNumber(element, "y2", 0, out var y2)
            ? Invariant($"M{x1},{y1} L{x2},{y2}")
            : null;

    private static string? Points(XElement element, bool close)
    {
        var numbers = Numbers(element.Attribute("points")?.Value ?? string.Empty);

        if (numbers is null || numbers.Count < 4 || numbers.Count % 2 != 0)
        {
            return null;
        }

        var markup = new StringBuilder();

        for (var index = 0; index < numbers.Count; index += 2)
        {
            markup.Append(index == 0 ? "M" : " L")
                .Append(numbers[index].ToString(CultureInfo.InvariantCulture))
                .Append(',')
                .Append(numbers[index + 1].ToString(CultureInfo.InvariantCulture));
        }

        return close ? markup.Append(" Z").ToString() : markup.ToString();
    }

    private static List<double>? Numbers(string text)
    {
        var numbers = new List<double>();

        foreach (var part in text.Split([' ', ',', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                return null;
            }

            numbers.Add(number);
        }

        return numbers;
    }

    private static bool TryNumber(XElement element, string name, double? fallback, out double value)
    {
        if (element.Attribute(name)?.Value is not { } text)
        {
            value = fallback ?? 0;
            return fallback is not null;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
