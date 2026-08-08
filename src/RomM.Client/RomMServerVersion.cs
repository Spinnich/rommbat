namespace RomM.Client;

/// <summary>
/// Version compatibility with the RomM server.
/// </summary>
/// <remarks>
/// The version is read from <c>GET /api/heartbeat</c> (<c>SYSTEM.VERSION</c>) at startup.
/// Below <see cref="Minimum"/>, RomMBat refuses with a message naming both versions.
/// Above but untested, it warns and continues. Features gate on version rather than
/// assuming, so a newer RomM adding a field does not break an older client.
/// </remarks>
public static class RomMServerVersion
{
    /// <summary>
    /// Minimum supported RomM version.
    /// </summary>
    public static readonly Version Minimum = new(5, 1, 0);
}
