using System.Collections.Frozen;

namespace RomM.Client;

/// <summary>
/// The RomM OAuth scope strings, and what RomMBat asks for.
/// </summary>
/// <remarks>
/// The approving user can narrow the grant, and <c>POST /api/auth/device/token</c> returns
/// what was actually granted. Compare against <see cref="GrantedScopes"/> rather than
/// assuming, so a narrow grant turns off a feature instead of throwing a 403 at the user
/// later.
/// </remarks>
public static class RomMScopes
{
    public const string MeRead = "me.read";
    public const string MeWrite = "me.write";
    public const string RomsRead = "roms.read";
    public const string RomsWrite = "roms.write";
    public const string RomsUserRead = "roms.user.read";
    public const string RomsUserWrite = "roms.user.write";
    public const string PlatformsRead = "platforms.read";
    public const string PlatformsWrite = "platforms.write";
    public const string AssetsRead = "assets.read";
    public const string AssetsWrite = "assets.write";
    public const string DevicesRead = "devices.read";
    public const string DevicesWrite = "devices.write";
    public const string FirmwareRead = "firmware.read";
    public const string CollectionsRead = "collections.read";
    public const string UsersRead = "users.read";
    public const string UsersWrite = "users.write";
    public const string TasksRun = "tasks.run";
    public const string LogsRead = "logs.read";

    /// <summary>
    /// What <c>POST /api/auth/device/init</c> requests, in the order the approval screen
    /// shows them.
    /// </summary>
    public static IReadOnlyList<string> Requested { get; } =
    [
        MeRead,
        RomsRead,
        PlatformsRead,
        CollectionsRead,
        FirmwareRead,
        AssetsRead,
        AssetsWrite,
        DevicesRead,
        DevicesWrite,
        RomsUserRead,
        RomsUserWrite,
    ];

    /// <summary>
    /// Scopes RomMBat never needs. A token carrying one of these is over-scoped, which
    /// usually means an admin paired the device rather than a purpose-made account.
    /// </summary>
    public static IReadOnlySet<string> NeverNeeded { get; } = new[]
    {
        UsersRead,
        UsersWrite,
        RomsWrite,
        PlatformsWrite,
        TasksRun,
        LogsRead,
    }.ToFrozenSet(StringComparer.Ordinal);
}
