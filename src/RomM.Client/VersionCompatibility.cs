namespace RomM.Client;

/// <summary>
/// How a discovered version compares against what this release supports.
/// </summary>
public enum CompatibilityVerdict
{
    /// <summary>At or above the minimum, and no newer than the version this release was tested against.</summary>
    Supported,

    /// <summary>Above the tested version. Warn and continue; a newer server adding a field is not a failure.</summary>
    Untested,

    /// <summary>Below the minimum. Refuse.</summary>
    TooOld,

    /// <summary>Nothing parseable was found. Refuse, because guessing would be worse.</summary>
    Unreadable,
}

/// <summary>
/// The result of checking one product's version against this release's declared range.
/// </summary>
/// <param name="Product">Display name, "RomM" or "RetroBat".</param>
/// <param name="Verdict">What to do about it.</param>
/// <param name="Found">The parsed version, or null when nothing parseable was found.</param>
/// <param name="Minimum">The lowest version this release supports.</param>
/// <param name="LastTested">The highest version this release was tested against.</param>
/// <param name="Message">A message naming both versions, ready to show the user.</param>
public sealed record CompatibilityCheck(
    string Product,
    CompatibilityVerdict Verdict,
    ProductVersion? Found,
    ProductVersion Minimum,
    ProductVersion LastTested,
    string Message)
{
    /// <summary>True when RomMBat must refuse to continue.</summary>
    public bool MustRefuse => Verdict is CompatibilityVerdict.TooOld or CompatibilityVerdict.Unreadable;

    /// <summary>Runs the comparison and builds the message.</summary>
    public static CompatibilityCheck Evaluate(
        string product,
        string? reported,
        ProductVersion minimum,
        ProductVersion lastTested)
    {
        if (!ProductVersion.TryParse(reported, out var found))
        {
            var seen = string.IsNullOrWhiteSpace(reported) ? "nothing" : $"'{reported}'";
            return new CompatibilityCheck(
                product,
                CompatibilityVerdict.Unreadable,
                null,
                minimum,
                lastTested,
                $"Could not read a {product} version ({seen} reported). RomMBat needs {product} {minimum} or newer.");
        }

        if (found < minimum)
        {
            return new CompatibilityCheck(
                product,
                CompatibilityVerdict.TooOld,
                found,
                minimum,
                lastTested,
                $"{product} {found} is older than the minimum this RomMBat supports, {product} {minimum}. "
                    + $"Upgrade {product} to {minimum} or newer.");
        }

        if (found > lastTested)
        {
            return new CompatibilityCheck(
                product,
                CompatibilityVerdict.Untested,
                found,
                minimum,
                lastTested,
                $"{product} {found} is newer than the {product} {lastTested} this RomMBat was tested against. "
                    + "Continuing, but report anything that misbehaves.");
        }

        return new CompatibilityCheck(
            product,
            CompatibilityVerdict.Supported,
            found,
            minimum,
            lastTested,
            $"{product} {found} is supported.");
    }
}
