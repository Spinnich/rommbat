namespace RomMBat.Core.Paths;

/// <summary>
/// What identifies a RetroBat root, and how to walk up to one.
/// </summary>
/// <remarks>
/// Its own file because <b>the ES hook compiles this source directly</b> rather than
/// referencing <c>RomMBat.Core</c>. Four copies of the hook are installed, one per event
/// folder, so it must not drag the store and the API client in with it; measured, the
/// difference is 12.8 MB per copy against 75.9 MB. Linking one file is what keeps the hook and
/// the agent from disagreeing about where the install is, which they would eventually do if
/// each had its own marker walk.
/// <para>
/// Nothing here reads the working directory. M0 probe 7b measured that it differs by hook
/// form, an <c>.exe</c> getting its own folder and a <c>.ps1</c> getting ES's home, so no
/// design may depend on it.
/// </para>
/// </remarks>
public static class RootMarkers
{
    /// <summary>
    /// Files and directories whose presence identifies a RetroBat root.
    /// </summary>
    /// <remarks>
    /// M0 probe 4 confirmed all three in a stock 8.2 tree, and confirmed there is no
    /// <c>build.ini</c> anywhere in it.
    /// </remarks>
    public static IReadOnlyList<string> All { get; } = ["retrobat.ini", "emulationstation", "roms"];

    /// <summary>
    /// True when the directory looks like a RetroBat root.
    /// </summary>
    /// <remarks>
    /// <c>retrobat.ini</c> alone is decisive. Without it both <c>emulationstation/</c> and
    /// <c>roms/</c> are required, because either alone is a common enough directory name to
    /// match partway up an unrelated tree.
    /// </remarks>
    public static bool IsRoot(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        if (File.Exists(Path.Combine(directory, "retrobat.ini")))
        {
            return true;
        }

        return Directory.Exists(Path.Combine(directory, "emulationstation"))
            && Directory.Exists(Path.Combine(directory, "roms"));
    }

    /// <summary>
    /// Walks up from a directory to the first one that is a root.
    /// </summary>
    /// <remarks>
    /// Walking to a marker rather than counting levels is what makes the hook's depth a
    /// non-issue. A hook sits at
    /// <c>&lt;root&gt;/emulationstation/.emulationstation/scripts/&lt;event&gt;/</c>, four
    /// levels down, where RetroBat's own <c>start/updatestores.bat</c> goes up three because
    /// it is calling <c>emulatorLauncher.exe</c> in <c>emulationstation/</c>. Counting gets
    /// that wrong quietly; a marker walk gets it right or misses.
    /// </remarks>
    /// <returns>Null when nothing above it looks like a RetroBat install.</returns>
    public static string? WalkUp(string? start)
    {
        if (string.IsNullOrWhiteSpace(start))
        {
            return null;
        }

        var directory = new DirectoryInfo(Path.GetFullPath(start));

        while (directory is not null)
        {
            if (IsRoot(directory.FullName))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
