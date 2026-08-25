namespace RomMBat.UI;

/// <summary>
/// Entry point for the gamepad-navigable front end.
/// </summary>
/// <remarks>
/// The framework is Avalonia, settled in M7 stage 7a, and no package is referenced yet, so
/// this is still a placeholder. Nothing here should ever hold logic: set resolution, mapping,
/// conflict handling and the outbox live in RomMBat.Core.
/// </remarks>
internal static class Program
{
    private static int Main()
    {
        Console.Error.WriteLine(
            "RomMBat: the gamepad UI is not implemented yet. "
                + "It arrives in M7 stage 7b, on Avalonia. See docs/PLAN.md.");
        return 70; // EX_SOFTWARE
    }
}
