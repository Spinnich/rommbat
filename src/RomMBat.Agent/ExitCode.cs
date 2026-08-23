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

    /// <summary>Not paired, or the stored token no longer works.</summary>
    public const int NotPaired = 4;

    /// <summary>The server could not be reached. Normal, not a fault.</summary>
    public const int Offline = 5;

    /// <summary>The user cancelled.</summary>
    public const int Cancelled = 6;

    /// <summary>Some of the work landed and some is still queued.</summary>
    public const int Partial = 7;

    /// <summary>Not implemented in this milestone. EX_SOFTWARE.</summary>
    public const int NotImplemented = 70;
}
