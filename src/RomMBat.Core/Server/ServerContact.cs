using RomM.Client;
using RomMBat.Core.Store;

namespace RomMBat.Core.Server;

/// <summary>
/// What one reachability probe told us, after the clock bookkeeping was recorded.
/// </summary>
/// <param name="Probe">The heartbeat result and version verdict.</param>
/// <param name="Skew">
/// Device clock minus server clock, or null when the server sent no <c>Date</c> header.
/// </param>
public sealed record ServerContact(ServerProbe Probe, TimeSpan? Skew)
{
    /// <summary>True when the skew is large enough to warn about and offer a re-stamp.</summary>
    public bool IsSkewSuspicious => Skew.HasValue && ClockSkew.IsSuspicious(Skew.Value);

    /// <summary>True when the server version is below the minimum, or unreadable.</summary>
    public bool MustRefuse => Probe.Compatibility.MustRefuse;
}

/// <summary>
/// Probing the server and writing down what it said about the clock.
/// </summary>
/// <remarks>
/// Every path that reaches the server goes through here, so first successful contact is the
/// moment skew gets measured whichever command triggered it.
/// </remarks>
public static class ServerProbes
{
    /// <summary>
    /// Probes the server and records the contact, or returns null when it is unreachable.
    /// </summary>
    /// <remarks>
    /// Unreachable is not an error here. Every operation must work with the server down, so
    /// the caller gets a null and carries on with local state rather than an exception it
    /// would only have to swallow.
    /// </remarks>
    public static async Task<ServerContact?> TryContactAsync(
        RomMConnection connection,
        LocalStore store,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(store);

        var time = timeProvider ?? TimeProvider.System;

        ServerProbe probe;
        try
        {
            probe = await connection.ProbeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (RomMUnreachableException)
        {
            return null;
        }

        var skew = store.Clock.RecordContact(probe.ServerDate, time.GetUtcNow(), probe.RoundTrip);
        store.Device.TouchLastSeen(time.GetUtcNow());

        return new ServerContact(probe, skew);
    }
}
