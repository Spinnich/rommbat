using RomM.Client;

namespace RomMBat.Core.Diagnostics;

/// <summary>
/// Version compatibility with the RetroBat install.
/// </summary>
/// <remarks>
/// Read from <c>system/version.info</c>, which RB-377 confirmed is the only version
/// file in the tree: there is no <c>build.ini</c>. The value carries a channel and an
/// architecture suffix (<c>8.3.0-stable-win64</c>), so it has to be split before it can be
/// compared. <see cref="ProductVersion"/> does that, and deliberately ignores the suffix
/// rather than reading it as a semantic-versioning prerelease, which would rank every
/// stock install below its own release number.
/// </remarks>
public static class RetroBatVersion
{
    /// <summary>Minimum supported RetroBat version.</summary>
    /// <remarks>
    /// Tracks the newest RetroBat stable rather than the oldest one that happens to work:
    /// RomMBat adopts a new stable within one release and moves the floor with it, so the
    /// behavior the measurements describe is the behavior of the install in front of the
    /// user. A range would carry both sides of every upstream fix, the workaround and the fix.
    /// </remarks>
    public static ProductVersion Minimum { get; } = ProductVersion.Parse("8.3.0");

    /// <summary>
    /// Newest RetroBat this release has been exercised against. The release's row in the guide's
    /// compatibility table, wiki/reference/compatibility.md, names it when the release is cut.
    /// </summary>
    public static ProductVersion LastTested { get; } = ProductVersion.Parse("8.3.0");

    /// <summary>Checks a <c>system/version.info</c> string against the supported range.</summary>
    public static CompatibilityCheck Check(string? reported) =>
        CompatibilityCheck.Evaluate("RetroBat", reported, Minimum, LastTested);
}
