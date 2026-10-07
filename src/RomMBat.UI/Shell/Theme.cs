using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Fonts;

namespace RomMBat.UI.Shell;

/// <summary>
/// Every color and font the shell draws with: EmulationStation's menus in RetroBat's default
/// carbon theme, blue colorset.
/// </summary>
/// <remarks>
/// <b>Copied from the theme's own files rather than picked</b>, so RomMBat reads as one more
/// ES menu (RB-425): <c>views/menu.xml</c> for the panel, text and selector, and
/// <c>subsets/colorsets/blue.xml</c> for the variables it names. Fonts are read from the live
/// install by <see cref="Load"/>; a tree without them draws in the system font.
/// </remarks>
internal static class Theme
{
    /// <summary>The colorset's <c>backgroundColor</c>.</summary>
    public static IBrush Background { get; } = Solid(0x051222);

    /// <summary><c>menuBackground</c>'s color.</summary>
    public static IBrush MenuPanel { get; } = Solid(0x242424);

    /// <summary>The panel's edge, where ES draws its frame's shadow.</summary>
    public static IBrush MenuEdge { get; } = Solid(0x0D0E0E);

    /// <summary><c>menutext</c>'s color: every row, and secondary text.</summary>
    public static IBrush Text { get; } = Solid(0x969696);

    /// <summary>
    /// Secondary text and anything unavailable, a step under <c>menutext</c>.
    /// </summary>
    /// <remarks>ES dims a disabled row by its alpha; this is that, over the panel.</remarks>
    public static IBrush Dim { get; } = Solid(0x6E6E6E);

    /// <summary><c>menutext</c>'s <c>selectedColor</c>, and the color of anything read first.</summary>
    public static IBrush Selected { get; } = Solid(0xFFFFFF);

    /// <summary><c>menutitle</c>'s color.</summary>
    public static IBrush Title { get; } = Solid(0xFAFAFA);

    /// <summary>The colorset's <c>groupColor</c>: group headings and small labels.</summary>
    public static IBrush Group { get; } = Solid(0x5178C3);

    /// <summary>The colorset's <c>baseColor</c>: help icons, bars and anything lit.</summary>
    public static IBrush Base { get; } = Solid(0x3675CA);

    /// <summary>The help bar's labels.</summary>
    public static IBrush HelpText { get; } = Solid(0x7D7D7D);

    /// <summary>The strip under the help bar.</summary>
    public static IBrush HelpBar { get; } = new SolidColorBrush(Color.FromArgb(0xC0, 0x04, 0x04, 0x04));

    /// <summary>Laid over the screen a popup opens from.</summary>
    public static IBrush Scrim { get; } = new SolidColorBrush(Color.FromArgb(0x90, 0x00, 0x00, 0x00));

    /// <summary><c>menugrid</c>'s row separator.</summary>
    public static IBrush Separator { get; } = new SolidColorBrush(Color.FromArgb(0x10, 0xFF, 0xFF, 0xFF));

    /// <summary>A key, button or bar track at rest, a step lighter than the panel.</summary>
    public static IBrush Well { get; } = Solid(0x303030);

    /// <summary>A refusal or a problem. Carbon has no warning color, so this is RomMBat's own.</summary>
    public static IBrush Warn { get; } = Solid(0xFFA57A);

    /// <summary>
    /// The selection: <c>selectorColor</c> fading to <c>selectorColorEnd</c>, left to right.
    /// </summary>
    public static IBrush Selector { get; } = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.FromRgb(0x36, 0x75, 0xCA), 0),
            new GradientStop(Color.FromRgb(0x00, 0x20, 0x5B), 1),
        },
    };

    /// <summary>Rows and body text: carbon's Cabin, else ES's own font, else the system's.</summary>
    public static FontFamily MenuFont { get; private set; } = FontFamily.Default;

    /// <summary>The title, drawn bold whichever family it is.</summary>
    public static FontFamily TitleFont { get; private set; } = FontFamily.Default;

    /// <summary>The help bar's labels.</summary>
    public static FontFamily HelpFont { get; private set; } = FontFamily.Default;

    /// <summary>Where ES's help icons are, or null to draw RomMBat's glyphs.</summary>
    public static string? HelpIcons { get; private set; }

    /// <summary>
    /// Loads the fonts <paramref name="files"/> names, once, before the window is built.
    /// </summary>
    /// <remarks>
    /// Through a font collection of the files' bytes, because Avalonia reads fonts from
    /// resources or the system and these are neither. A file that will not load is skipped,
    /// and that face falls back to the next.
    /// </remarks>
    public static void Load(ThemeFiles files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var collection = new InstallFonts();
        FontManager.Current.AddFontCollection(collection);

        MenuFont = collection.Add(files.MenuFont) ?? FontFamily.Default;
        TitleFont = collection.Add(files.TitleFont) ?? MenuFont;
        HelpFont = collection.Add(files.HelpFont) ?? MenuFont;
        HelpIcons = files.HelpIcons;
    }

    private static SolidColorBrush Solid(uint rgb) => new(Color.FromUInt32(0xFF000000 | rgb));

    /// <summary>The install's fonts, registered under one key so a family name can find them.</summary>
    private sealed class InstallFonts : FontCollectionBase
    {
        public override Uri Key { get; } = new("fonts:RomMBatInstall");

        public FontFamily? Add(string? path)
        {
            if (path is null)
            {
                return null;
            }

            try
            {
                // Copied into memory so the file is not held open for the life of the window.
                var stream = new MemoryStream(File.ReadAllBytes(path));

                // Named as one "key#family" string. The constructor taking the key as a base URI
                // builds a family the collection never matches, and every face silently became
                // Segoe UI.
                return TryAddGlyphTypeface(stream, out var typeface)
                    ? new FontFamily($"{Key}#{typeface.FamilyName}")
                    : null;
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
    }
}
