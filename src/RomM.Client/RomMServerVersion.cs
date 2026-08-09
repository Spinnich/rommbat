namespace RomM.Client;

/// <summary>
/// Version compatibility with the RomM server.
/// </summary>
/// <remarks>
/// The version is read from <c>GET /api/heartbeat</c> (<c>SYSTEM.VERSION</c>) at startup.
/// Below <see cref="Minimum"/>, RomMBat refuses with a message naming both versions.
/// Above <see cref="LastTested"/>, it warns and continues. Features gate on version rather
/// than assuming, so a newer RomM adding a field does not break an older client.
/// </remarks>
public static class RomMServerVersion
{
    /// <summary>
    /// Minimum supported RomM version. The pinned OpenAPI schema comes from this version,
    /// so the generated DTOs describe the oldest server the client claims to work with.
    /// </summary>
    public static ProductVersion Minimum { get; } = ProductVersion.Parse("5.1.0");

    /// <summary>
    /// Newest RomM this release has been exercised against. Keep in step with the README
    /// compatibility table.
    /// </summary>
    public static ProductVersion LastTested { get; } = ProductVersion.Parse("5.1.1");

    /// <summary>Checks a reported <c>SYSTEM.VERSION</c> against the supported range.</summary>
    public static CompatibilityCheck Check(string? reported) =>
        CompatibilityCheck.Evaluate("RomM", reported, Minimum, LastTested);
}
