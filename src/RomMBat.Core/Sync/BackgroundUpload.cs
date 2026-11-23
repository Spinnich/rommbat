using System.Globalization;
using RomMBat.Core.Paths;

namespace RomMBat.Core.Sync;

/// <summary>The time the most recent in-session save or state upload completed.</summary>
public static class BackgroundUpload
{
    private static RelativePath Location { get; } = RetroBatInstall.AppDirectory.Combine("last-upload.txt");

    public static DateTimeOffset? Read(RetroBatInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);

        try
        {
            using var stream = new FileStream(
                install.Resolve(Location), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return DateTimeOffset.TryParseExact(
                reader.ReadLine(), "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)
                ? at
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    internal static void Record(RetroBatInstall install, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(install);

        var path = install.Resolve(Location);
        var temporary = path + ".part";
        try
        {
            File.WriteAllText(temporary, at.ToString("O", CultureInfo.InvariantCulture));
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A status write must not turn a completed upload into a failed pass.
        }
    }
}
