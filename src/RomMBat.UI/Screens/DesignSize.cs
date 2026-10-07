namespace RomMBat.UI.Screens;

/// <summary>
/// The size every screen is laid out for, and the canvas a display of another size is given.
/// </summary>
/// <remarks>
/// <b>Laid out once, at 1080p, and scaled.</b> The screens are fixed slots in pixels, which is
/// what stops them moving while they run (#490), and at 1280x720 they ran off the panel: the
/// root list was cut off under a scrollbar. EmulationStation scales its menus with the
/// display, so this does too.
/// <para>
/// The canvas keeps the display's shape rather than letterboxing it: the scale is the one that
/// fits 1920x1080 inside the display, and the canvas is the display divided by it, so a 16:10
/// screen gets a taller canvas instead of bars.
/// </para>
/// </remarks>
public static class DesignSize
{
    public const double Width = 1920;
    public const double Height = 1080;

    /// <summary>The canvas, in design pixels, for a display of the given size.</summary>
    public static (double Width, double Height) Fit(double width, double height)
    {
        if (width <= 0 || height <= 0 || double.IsNaN(width) || double.IsNaN(height))
        {
            return (Width, Height);
        }

        var scale = Math.Min(width / Width, height / Height);
        return (width / scale, height / scale);
    }
}
