using RomM.Client.Generated;

namespace RomM.Client;

/// <summary>
/// What <c>GET /api/heartbeat</c> told us: the server is up, which version it is, and what
/// its clock says.
/// </summary>
/// <param name="ReportedVersion">The raw <c>SYSTEM.VERSION</c> string.</param>
/// <param name="Compatibility">Whether that version is supported.</param>
/// <param name="ServerDate">
/// The response <c>Date</c> header, which is the only clock reference RomMBat gets. Null
/// when the server omitted it.
/// </param>
/// <param name="RoundTrip">How long the probe took, for a skew estimate that accounts for latency.</param>
/// <param name="Heartbeat">The whole response, for feature gating later.</param>
public sealed record ServerProbe(
    string? ReportedVersion,
    CompatibilityCheck Compatibility,
    DateTimeOffset? ServerDate,
    TimeSpan RoundTrip,
    HeartbeatResponse? Heartbeat);
