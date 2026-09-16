using RomM.Client;

namespace RomMBat.Core.Sync;

/// <summary>
/// Why work that needed the server did not all happen, ranked so the worst of several wins.
/// </summary>
/// <remarks>
/// <b>The ranking is the rule.</b> A run is <see cref="Unreachable"/> only when every failure in
/// it was, because that is the one cause that clears itself: a front end or a script that reads
/// it waits and tries again, and doing that against a token that will never work again loops
/// forever.
/// </remarks>
public enum FailureCause
{
    /// <summary>Nothing failed.</summary>
    None = 0,

    /// <summary>The server could not be reached. Waiting is the remedy.</summary>
    Unreachable = 1,

    /// <summary>
    /// The server answered with a failure, or what it sent could not be verified or written.
    /// </summary>
    Failed = 2,

    /// <summary>
    /// 401 or 403: the token no longer works, or was not granted what the call needs. Pairing
    /// again is the remedy for both.
    /// </summary>
    NotAuthorized = 3,
}

/// <summary>Classifying and combining <see cref="FailureCause"/>.</summary>
public static class FailureCauses
{
    /// <summary>What an answer from the server says about why the call failed.</summary>
    public static FailureCause Of(RomMResponseStatus status) => status switch
    {
        RomMResponseStatus.Ok => FailureCause.None,
        RomMResponseStatus.Unauthorized or RomMResponseStatus.Forbidden => FailureCause.NotAuthorized,
        _ => FailureCause.Failed,
    };

    /// <summary>The worse of two causes.</summary>
    public static FailureCause Worst(FailureCause first, FailureCause second) =>
        first > second ? first : second;
}
