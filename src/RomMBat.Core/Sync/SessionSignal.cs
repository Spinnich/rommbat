using RomMBat.Core.Paths;

namespace RomMBat.Core.Sync;

/// <summary>Install-local notifications that never become journal events.</summary>
public static class SessionSignal
{
    public const string LaunchExtension = ".launch";
    public const string AcknowledgementExtension = ".ack";

    internal static FileStream? BeginLaunch(RetroBatInstall install) => Create(install, LaunchExtension);

    internal static void AcknowledgementPending(RetroBatInstall install)
    {
        using var signal = Create(install, AcknowledgementExtension);
    }

    public static bool HasLaunchIntent(string spool)
    {
        try
        {
            return Directory.EnumerateFiles(spool, "*" + LaunchExtension).Any();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static FileStream? Create(RetroBatInstall install, string extension)
    {
        var directory = install.Resolve(SpoolDrain.Directory);
        try
        {
            Directory.CreateDirectory(directory);
            return new FileStream(Path.Combine(directory, Guid.NewGuid().ToString("N") + extension),
                FileMode.CreateNew, FileAccess.Write, FileShare.Read, 1, FileOptions.DeleteOnClose);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The launch still has a bounded lock wait, and acknowledgement work is durable.
            return null;
        }
    }
}
