using RomMBat.Core.Paths;

namespace RomMBat.UI.Shell;

/// <summary>
/// The fonts and help icons the look borrows from the live install, found at point of use.
/// </summary>
/// <param name="MenuFont">Rows and body text, or null for the system default.</param>
/// <param name="TitleFont">The title, or null to draw the menu font in bold.</param>
/// <param name="HelpFont">The help bar's labels, or null to use the menu font.</param>
/// <param name="HelpIcons">The directory of ES's help icons, or null to draw RomMBat's own glyphs.</param>
/// <remarks>
/// <b>Absolute paths that live only as long as the window</b>, resolved from the root each
/// start and never stored (rule 1). Each is the first candidate present, so a tree without the
/// carbon theme falls back to ES's own font, and one without that to the system's.
/// </remarks>
internal sealed record ThemeFiles(string? MenuFont, string? TitleFont, string? HelpFont, string? HelpIcons)
{
    /// <summary>The carbon theme's fonts, which its <c>views/menu.xml</c> draws every menu in (RB-425).</summary>
    public static RelativePath CarbonFonts { get; } =
        RelativePath.Create("emulationstation/.emulationstation/themes/es-theme-carbon/art/fonts");

    /// <summary>ES's own resources: its fallback font and its help icons (RB-426).</summary>
    public static RelativePath EsResources { get; } = RelativePath.Create("emulationstation/resources");

    /// <summary>Nothing borrowed: the system font and the drawn glyphs.</summary>
    public static ThemeFiles None { get; } = new(null, null, null, null);

    public static ThemeFiles Find(RetroBatInstall? install)
    {
        if (install is null)
        {
            return None;
        }

        string? First(params RelativePath[] candidates) =>
            candidates.Select(install.Resolve).FirstOrDefault(File.Exists);

        // ES's condensed font is what its help bar shows under carbon: the theme's help
        // subset names Cabin by a path that resolves to nothing, and ES falls back (RB-426).
        var esFont = EsResources.Combine("opensans_hebrew_condensed_regular.ttf");
        var menu = First(CarbonFonts.Combine("Cabin-Regular.ttf"), esFont);
        var help = First(esFont) ?? menu;
        var icons = install.Resolve(EsResources.Combine("help"));

        return new ThemeFiles(
            menu,
            First(CarbonFonts.Combine("Cabin-Bold.ttf")),
            help,
            Directory.Exists(icons) ? icons : null);
    }
}
