namespace RomMBat.UI;

/// <summary>
/// Entry point for the gamepad-navigable front end.
/// </summary>
/// <remarks>
/// The UI framework is chosen in M7, so this is a placeholder with no framework
/// dependency. Nothing here should ever hold logic: set resolution, mapping, conflict
/// handling and the outbox live in RomMBat.Core.
/// </remarks>
internal static class Program
{
    private static int Main()
    {
        Console.Error.WriteLine(
            "RomMBat: the gamepad UI is not implemented yet. "
                + "The framework choice (Avalonia or WPF) is deferred to M7. See docs/PLAN.md.");
        return 70; // EX_SOFTWARE
    }
}
