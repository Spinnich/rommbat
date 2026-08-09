using RomM.Client.Generated;

namespace RomM.Client;

/// <summary>
/// A pairing request the server has issued but nobody has approved yet.
/// </summary>
/// <remarks>
/// The pending state lives only in Redis with a hard 600 s TTL, so this is short-lived by
/// design and the deadline is real. Show the countdown and offer a one-key restart.
/// </remarks>
public sealed class PairingSession
{
    internal PairingSession(
        DeviceAuthInitResponse response,
        DeviceAuthInitPayload payload,
        Uri origin,
        DateTimeOffset issuedAt)
    {
        DeviceName = payload.Name;
        ClientDeviceIdentifier = payload.Client_device_identifier;
        DeviceCode = response.Device_code;
        UserCode = response.User_code;
        DisplayCode = PairingCode.Format(response.User_code);
        VerificationUri = RomMConnection.JoinOrigin(origin, response.Verification_path_complete);
        IssuedAt = issuedAt;
        ExpiresAt = issuedAt.AddSeconds(response.Expires_in);
        Interval = TimeSpan.FromSeconds(Math.Max(1, response.Interval));
    }

    /// <summary>The label this device will carry in the RomM device list.</summary>
    public string DeviceName { get; }

    /// <summary>The GUID this pairing was started with, which is what makes it an update.</summary>
    public string ClientDeviceIdentifier { get; }

    /// <summary>
    /// The polling secret. Never display it, never log it whole; the server logs only its
    /// first 8 characters and this client logs none of it.
    /// </summary>
    public string DeviceCode { get; }

    /// <summary>The 8-character code as the server issued it.</summary>
    public string UserCode { get; }

    /// <summary>The same code grouped as <c>ABCD-EFGH</c> for reading aloud.</summary>
    public string DisplayCode { get; }

    /// <summary>
    /// The configured origin joined with <c>verification_path_complete</c>. This is what the
    /// QR encodes; the server returns a relative path on purpose and stays origin-agnostic.
    /// </summary>
    public Uri VerificationUri { get; }

    public DateTimeOffset IssuedAt { get; }

    public DateTimeOffset ExpiresAt { get; }

    /// <summary>The server's requested polling interval, at least one second.</summary>
    public TimeSpan Interval { get; }

    /// <summary>How long is left before the code lapses, floored at zero.</summary>
    public TimeSpan RemainingAt(DateTimeOffset now)
    {
        var remaining = ExpiresAt - now;
        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }
}

/// <summary>Reported before each poll, so a caller can redraw a countdown.</summary>
/// <param name="Attempt">1 for the first poll.</param>
/// <param name="Remaining">How long the code has left.</param>
/// <param name="Interval">The interval currently in force, which grows on <c>slow_down</c>.</param>
/// <param name="LastOutcome">What the previous poll said, or null before the first.</param>
public sealed record PairingProgress(
    int Attempt,
    TimeSpan Remaining,
    TimeSpan Interval,
    PairingOutcome? LastOutcome);

/// <summary>
/// Drives the device pairing flow: start a request, then poll until it resolves.
/// </summary>
public sealed class DevicePairing
{
    /// <summary>RFC 8628's convention, and what the server's per-code pacing expects.</summary>
    private static readonly TimeSpan SlowDownStep = TimeSpan.FromSeconds(5);

    /// <summary>Backoff after a 429. The token endpoint allows 60 polls/min/IP.</summary>
    private static readonly TimeSpan RateLimitBackoff = TimeSpan.FromSeconds(30);

    private readonly RomMConnection _connection;
    private readonly TimeProvider _time;

    public DevicePairing(RomMConnection connection, TimeProvider? timeProvider = null)
    {
        _connection = connection;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Builds the init payload. <paramref name="clientDeviceIdentifier"/> is the GUID stored
    /// in the RetroBat tree, never a MAC address or a hostname: it is what makes re-pairing
    /// update the existing device instead of creating a second one.
    /// </summary>
    public static DeviceAuthInitPayload BuildPayload(
        string clientDeviceIdentifier,
        string deviceName,
        string clientVersion,
        string platform = "windows") => new()
        {
            Client_device_identifier = clientDeviceIdentifier,
            Name = deviceName,
            Client = "RomMBat",
            Platform = platform,
            Client_version = clientVersion,
            Requested_scopes = [.. RomMScopes.Requested],
        };

    /// <summary>Starts a pairing request and returns the code and QR target to display.</summary>
    /// <exception cref="RomMUnreachableException">The server did not answer.</exception>
    /// <exception cref="RomMApiException">The server rejected the request, for example a 429.</exception>
    public async Task<PairingSession> BeginAsync(
        DeviceAuthInitPayload payload,
        CancellationToken cancellationToken = default)
    {
        var response = await _connection.BeginPairingAsync(payload, cancellationToken).ConfigureAwait(false);
        return new PairingSession(response, payload, _connection.Options.Origin, _time.GetUtcNow());
    }

    /// <summary>
    /// Polls until the request is approved, declined, expired, or the caller cancels.
    /// </summary>
    /// <remarks>
    /// The first poll happens immediately: the server's per-code pacing has nothing to
    /// compare against yet, and a code approved before the first sleep should not cost the
    /// user an interval. Subsequent polls wait, and <c>slow_down</c> widens the wait rather
    /// than being treated as a failure.
    /// <para>
    /// A dropped network does not end the flow. The server is unreachable is a normal state,
    /// so the loop keeps polling until the code's own deadline passes.
    /// </para>
    /// </remarks>
    public async Task<PairingPollResult> AwaitApprovalAsync(
        PairingSession session,
        IProgress<PairingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var interval = session.Interval;
        var attempt = 0;
        PairingOutcome? last = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var now = _time.GetUtcNow();
            if (now >= session.ExpiresAt)
            {
                return PairingPollResult.Failed(
                    PairingOutcome.Expired,
                    "The pairing code expired before it was approved. Start again for a new one.");
            }

            attempt++;
            progress?.Report(new PairingProgress(attempt, session.RemainingAt(now), interval, last));

            PairingPollResult result;
            try
            {
                result = await _connection.PollPairingAsync(session.DeviceCode, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (RomMUnreachableException)
            {
                // Losing the network mid-pairing is not a failure of the pairing. Keep trying
                // until the code's own deadline decides.
                result = PairingPollResult.Failed(PairingOutcome.ServerError, "The server is not reachable.");
                await DelayAsync(interval, session, cancellationToken).ConfigureAwait(false);
                last = PairingOutcome.Pending;
                continue;
            }

            last = result.Outcome;

            switch (result.Outcome)
            {
                case PairingOutcome.Approved:
                case PairingOutcome.Denied:
                case PairingOutcome.Expired:
                    return result;

                case PairingOutcome.SlowDown:
                    interval += SlowDownStep;
                    break;

                case PairingOutcome.RateLimited:
                    interval = RateLimitBackoff;
                    break;

                case PairingOutcome.ServerError:
                    return result;

                case PairingOutcome.Pending:
                default:
                    break;
            }

            await DelayAsync(interval, session, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Waits the interval, but never past the code's deadline.</summary>
    private Task DelayAsync(TimeSpan interval, PairingSession session, CancellationToken cancellationToken)
    {
        var remaining = session.RemainingAt(_time.GetUtcNow());
        var wait = interval < remaining ? interval : remaining;
        return wait <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(wait, _time, cancellationToken);
    }
}
