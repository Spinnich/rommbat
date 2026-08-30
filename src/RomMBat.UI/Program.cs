using Avalonia;

namespace RomMBat.UI;

/// <summary>
/// Entry point for the gamepad-navigable front end.
/// </summary>
/// <remarks>
/// <b>Launched from the EmulationStation menu with no arguments at all.</b> Measured on 8.2.1:
/// the launcher runs <c>[Running] ...\RomMBat.exe</c> with an empty command line, and an
/// ES-menu launch carries none of the <c>-p1*</c> controller arguments a game launch does. So
/// the only argument here is for running it at a desk, pointed at a throwaway tree.
/// <para>
/// <b>Presentation owns no logic.</b> Set resolution, mapping, conflict handling and the outbox
/// all live in <c>RomMBat.Core</c>, and a test asserts this assembly cannot even name the
/// <c>es_settings.cfg</c> writer.
/// </para>
/// </remarks>
internal static class Program
{
    /// <summary>A RetroBat root given on the command line, for development away from a real one.</summary>
    public static string? ExplicitRoot { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--root", StringComparison.Ordinal))
            {
                ExplicitRoot = args[i + 1];
            }
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Win32 and Skia by name, never <c>UsePlatformDetect</c>.
    /// </summary>
    /// <remarks>
    /// RomMBat ships win-x64 only. Detecting the platform would pull in backends this build can
    /// never use, and the package that carries them raises a vulnerability warning that
    /// <c>-warnaserror</c> turns into a failed build. Skia is what makes what a handheld shows
    /// independent of that machine's Windows Desktop stack.
    /// </remarks>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseWin32()
            .UseSkia()
            .LogToTrace();
}
