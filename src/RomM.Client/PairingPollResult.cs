using RomM.Client.Generated;

namespace RomM.Client;

/// <summary>What one poll of <c>POST /api/auth/device/token</c> came back with.</summary>
public enum PairingOutcome
{
    /// <summary>Nobody has approved it yet. Keep polling at the interval.</summary>
    Pending,

    /// <summary>Polled inside the interval. Widen the gap and keep polling.</summary>
    SlowDown,

    /// <summary>Approved. A token was issued.</summary>
    Approved,

    /// <summary>Declined in the web UI. Do not keep polling.</summary>
    Denied,

    /// <summary>
    /// The code lapsed. Pending state is Redis-only with a hard 600 s TTL, so this is a
    /// normal end state and the answer is a fresh code, not a retry.
    /// </summary>
    Expired,

    /// <summary>Rate limited (60 polls/min/IP). Back off and keep polling.</summary>
    RateLimited,

    /// <summary>Anything else the server said.</summary>
    ServerError,
}

/// <summary>The outcome of one poll, plus the token when there is one.</summary>
public sealed record PairingPollResult(PairingOutcome Outcome, DeviceAuthTokenResponse? Token, string? Message)
{
    /// <summary>True when polling should stop, whether or not it succeeded.</summary>
    public bool IsTerminal => Outcome is not (PairingOutcome.Pending or PairingOutcome.SlowDown
        or PairingOutcome.RateLimited);

    internal static PairingPollResult Pending() => new(PairingOutcome.Pending, null, null);

    internal static PairingPollResult SlowDown() => new(PairingOutcome.SlowDown, null, null);

    internal static PairingPollResult Approved(DeviceAuthTokenResponse token) =>
        new(PairingOutcome.Approved, token, null);

    internal static PairingPollResult Failed(PairingOutcome outcome, string message) =>
        new(outcome, null, message);
}
