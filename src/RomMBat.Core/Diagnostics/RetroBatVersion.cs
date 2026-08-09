using RomM.Client;

namespace RomMBat.Core.Diagnostics;

/// <summary>
/// Version compatibility with the RetroBat install.
/// </summary>
/// <remarks>
/// Read from <c>system/version.info</c>, which M0 probe 4 confirmed is the only version
/// file in the tree: there is no <c>build.ini</c>. The value carries a channel and an
/// architecture suffix (<c>8.2.0-stable-win64</c>), so it has to be split before it can be
/// compared. <see cref="ProductVersion"/> does that, and deliberately ignores the suffix
/// rather than reading it as a semantic-versioning prerelease, which would rank every
/// stock install below its own release number.
/// </remarks>
public static class RetroBatVersion
{
    /// <summary>Minimum supported RetroBat version.</summary>
    public static ProductVersion Minimum { get; } = ProductVersion.Parse("8.2");

    /// <summary>
    /// Newest RetroBat this release has been exercised against. Keep in step with the README
    /// compatibility table.
    /// </summary>
    public static ProductVersion LastTested { get; } = ProductVersion.Parse("8.2.0");

    /// <summary>Checks a <c>system/version.info</c> string against the supported range.</summary>
    public static CompatibilityCheck Check(string? reported) =>
        CompatibilityCheck.Evaluate("RetroBat", reported, Minimum, LastTested);
}
