using RomM.Client;
using RomMBat.Core.Sync;

namespace RomMBat.Agent;

/// <summary>
/// Process exit codes, so the ES hooks and any wrapping script can tell outcomes apart.
/// </summary>
/// <remarks>
/// Deliberately distinct: "the server is down" and "this build refuses to run against that
/// RetroBat" need different responses from whatever invoked the agent, and collapsing them
/// into a generic failure would hide that.
/// </remarks>
internal static class ExitCode
{
    /// <summary>Everything asked for happened.</summary>
    public const int Ok = 0;

    /// <summary>The command line was wrong.</summary>
    public const int Usage = 2;

    /// <summary>
    /// A precondition failed: no RetroBat root, a version below the minimum, or another agent
    /// holding the tree lock over work this command would have to write.
    /// </summary>
    public const int Refused = 3;

    /// <summary>
    /// Not paired, or the server refused the token (401) or found it lacks a scope the call
    /// needs (403). Pairing again fixes all three.
    /// </summary>
    public const int NotPaired = 4;

    /// <summary>The server could not be reached. Normal, not a fault.</summary>
    public const int Offline = 5;

    /// <summary>The user cancelled.</summary>
    public const int Cancelled = 6;

    /// <summary>
    /// The run failed at something it attempted, and the rest landed. Never a report about data
    /// no run can act on, such as a server row this install cannot place (#148). A flush whose
    /// session close is refused for a scope ends here rather than at <see cref="NotPaired"/>,
    /// because its transfers did land.
    /// </summary>
    public const int Partial = 7;

    /// <summary>
    /// The server answered and refused or failed the request, or the result could not be verified
    /// or written here. Not normal, and retrying unchanged may not help.
    /// </summary>
    public const int ServerError = 8;

    /// <summary>Not implemented in this milestone. EX_SOFTWARE.</summary>
    public const int NotImplemented = 70;

    /// <summary>
    /// The code for work that did not all happen.
    /// </summary>
    /// <remarks>
    /// <see cref="FailureCause.None"/> is <see cref="Offline"/>: it reaches here from a walk that
    /// stopped short with no failure recorded, which is resumable work rather than a fault.
    /// </remarks>
    public static int For(FailureCause cause) => cause switch
    {
        FailureCause.NotAuthorized => NotPaired,
        FailureCause.Failed => ServerError,
        _ => Offline,
    };

    /// <summary>The code for a call the server answered with a failure.</summary>
    public static int For(RomMResponseStatus status) => For(FailureCauses.Of(status));
}
