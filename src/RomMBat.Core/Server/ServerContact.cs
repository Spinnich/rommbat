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

/// <summary>One reachability probe: the contact, or why there was none.</summary>
/// <param name="Contact">The contact, or null when there was no usable answer.</param>
/// <param name="Failure">Why there is no contact, or null when there is one.</param>
/// <param name="Answered">
/// True when something answered, which a caller reports differently from silence: the network
/// works and whatever is at the origin is not a RomM server this client can use.
/// </param>
public sealed record ServerContactAttempt(ServerContact? Contact, string? Failure, bool Answered);

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
    /// Probes the server and records the contact, or returns null when there is no usable answer.
    /// </summary>
    /// <remarks>
    /// Unreachable is not an error here. Every operation must work with the server down, so
    /// the caller gets a null and carries on with local state rather than an exception it
    /// would only have to swallow. <see cref="ContactAsync"/> says why, for a caller that shows it.
    /// </remarks>
    public static async Task<ServerContact?> TryContactAsync(
        RomMConnection connection,
        LocalStore store,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default) =>
        (await ContactAsync(connection, store, timeProvider, cancellationToken).ConfigureAwait(false)).Contact;

    /// <summary>
    /// Probes the server and records the contact, or says why there is none.
    /// </summary>
    /// <remarks>
    /// <b>An answer that is not a readable heartbeat is no contact, not a crash</b> (#211). A
    /// captive portal's login page, a reverse proxy's 502 while RomM restarts, or a newer RomM
    /// whose heartbeat moved all throw <see cref="RomMApiException"/> from the probe, and every
    /// caller treats that exactly as it treats an unreachable server. Nothing is recorded from
    /// such an answer: its <c>Date</c> header is not the server's clock.
    /// </remarks>
    public static async Task<ServerContactAttempt> ContactAsync(
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
        catch (RomMUnreachableException ex)
        {
            return new ServerContactAttempt(null, ex.Message, Answered: false);
        }
        catch (RomMApiException ex)
        {
            return new ServerContactAttempt(
                null,
                $"{connection.Options.Origin} answered, but not as a RomM server this client can read: {ex.Message}",
                Answered: true);
        }

        var skew = store.Clock.RecordContact(probe.ServerDate, time.GetUtcNow(), probe.RoundTrip);
        store.Device.TouchLastSeen(time.GetUtcNow());

        return new ServerContactAttempt(new ServerContact(probe, skew), null, Answered: true);
    }
}
