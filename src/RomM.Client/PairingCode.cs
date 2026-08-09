namespace RomM.Client;

/// <summary>
/// The 8-character user code, and how to show it.
/// </summary>
/// <remarks>
/// The published docs call this 8 digits. It is not: the server draws from
/// <see cref="Alphabet"/>, which excludes I, L, O, 0 and 1 so nothing is ambiguous when
/// read off a screen and typed on a phone.
/// </remarks>
public static class PairingCode
{
    /// <summary>The server's <c>PAIR_ALPHABET</c>. Verbatim, including the exclusions.</summary>
    public const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    /// <summary>How many characters a code has.</summary>
    public const int Length = 8;

    /// <summary>
    /// Groups a code for display as <c>ABCD-EFGH</c>. The server runs
    /// <c>normalize_user_code</c>, which strips hyphens and spaces and uppercases, so the
    /// grouping costs nothing and makes the code far easier to read aloud.
    /// </summary>
    public static string Format(string userCode)
    {
        ArgumentNullException.ThrowIfNull(userCode);

        var normalized = Normalize(userCode);
        return normalized.Length == Length
            ? $"{normalized[..4]}-{normalized[4..]}"
            : normalized;
    }

    /// <summary>Applies the server's own normalisation: strip hyphens and spaces, uppercase.</summary>
    public static string Normalize(string userCode)
    {
        ArgumentNullException.ThrowIfNull(userCode);

        return userCode.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
    }

    /// <summary>True when the code is the right length and draws only on the server's alphabet.</summary>
    public static bool IsWellFormed(string? userCode)
    {
        if (string.IsNullOrEmpty(userCode))
        {
            return false;
        }

        var normalized = Normalize(userCode);
        return normalized.Length == Length
            && normalized.All(character => Alphabet.Contains(character, StringComparison.Ordinal));
    }
}
