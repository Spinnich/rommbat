namespace RomMBat.Core;

/// <summary>
/// Markers that identify a RetroBat installation root.
/// </summary>
/// <remarks>
/// Placeholder. Discovery itself lands in M1, after M0 experiment 4 confirms that
/// walking up from <c>AppContext.BaseDirectory</c> locates the root on both portable
/// and fixed installs. Registry and fixed-path lookups are a last-resort fallback,
/// never the primary path.
/// </remarks>
public static class RetroBatRoot
{
    /// <summary>
    /// Files and directories whose presence identifies a RetroBat root.
    /// </summary>
    public static readonly IReadOnlyList<string> Markers = ["retrobat.ini", "emulationstation", "roms"];

    /// <summary>
    /// Minimum supported RetroBat version. Below this, RomMBat refuses to run.
    /// </summary>
    public static readonly Version MinimumVersion = new(8, 2);
}
