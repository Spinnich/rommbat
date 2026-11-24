namespace RomMBat.Core.Content;

/// <summary>A save read refused by a sharing lock, as opposed to a missing file or denied access.</summary>
internal static class SaveFileLock
{
    public static bool IsSharingViolation(Exception exception) =>
        exception is IOException && (exception.HResult is unchecked((int)0x80070020) or unchecked((int)0x80070021)
            // Unix FileStream uses the native errno when its advisory flock is busy.
            || (OperatingSystem.IsMacOS() && exception.HResult == 35)
            || (OperatingSystem.IsLinux() && exception.HResult == 11));
}
