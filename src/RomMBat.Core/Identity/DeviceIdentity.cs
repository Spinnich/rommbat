using System.Globalization;
using RomMBat.Core.Paths;

namespace RomMBat.Core.Identity;

/// <summary>
/// The <c>client_device_identifier</c>: a GUID generated once and kept in the tree.
/// </summary>
/// <remarks>
/// This is the device's identity, and it is deliberately not the MAC address or the
/// hostname, so it travels with the drive. <c>POST /api/auth/device/approve</c> looks the
/// device up with <c>get_device_by_client_identifier</c> and records no host details at
/// all, which is what makes re-pairing after a move update the existing device instead of
/// creating a second one.
/// <para>
/// <c>POST /api/devices</c> must never be called with host fingerprint fields. Its dedup
/// matches on <c>mac_address</c> alone and would collide with a different RomM client that
/// happens to share a MAC or a DHCP lease.
/// </para>
/// <para>
/// It lives in a file rather than only in the database because it has to outlive the
/// database: a rebuilt store must not turn into a second device in the RomM UI.
/// </para>
/// </remarks>
public static class DeviceIdentity
{
    /// <summary>Reads the stored identifier, or null when this install has never had one.</summary>
    public static string? Read(RetroBatInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);

        var path = install.DeviceIdentityPath;
        if (!File.Exists(path))
        {
            return null;
        }

        var text = File.ReadAllText(path).Trim();
        return Guid.TryParse(text, out var identifier)
            ? identifier.ToString("D", CultureInfo.InvariantCulture)
            : null;
    }

    /// <summary>
    /// Reads the stored identifier, generating and writing one the first time.
    /// </summary>
    /// <remarks>
    /// A malformed or empty file is replaced rather than repaired. Every alternative loses
    /// the identity anyway, and a fresh GUID at least pairs cleanly; the cost is one extra
    /// device row in RomM, which the user can delete.
    /// </remarks>
    public static string ReadOrCreate(RetroBatInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);

        var existing = Read(install);
        if (existing is not null)
        {
            return existing;
        }

        install.EnsureAppDirectories();

        var identifier = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture);
        var path = install.DeviceIdentityPath;
        var temporary = path + ".part";

        // Write then rename, so a power loss never leaves a half-written identity behind.
        File.WriteAllText(temporary, identifier + Environment.NewLine);
        File.Move(temporary, path, overwrite: true);

        return identifier;
    }

    /// <summary>
    /// The default display name for the device in the RomM UI.
    /// </summary>
    /// <remarks>
    /// A label only. The machine name is not part of identity and is not sent as a
    /// fingerprint; it is here because "RomMBat" alone is useless in a device list.
    /// </remarks>
    public static string DefaultDeviceName() => $"RomMBat ({Environment.MachineName})";
}
