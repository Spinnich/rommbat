using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RomMBat.Core.RetroBat;

/// <summary>How the key that names a save unit is read off an entry.</summary>
public enum SaveUnitKeyKind
{
    /// <summary>Nothing this build understands, so the container is not read at all.</summary>
    Unknown,

    /// <summary>A directory whose name begins with a 4-letter, 5-digit title id.</summary>
    TitleId,

    /// <summary>A directory whose whole name is the key.</summary>
    Name,

    /// <summary>A <c>&lt;maker&gt;-&lt;code&gt;-&lt;internal&gt;.gci</c> file, keyed on the code.</summary>
    Gci,

    /// <summary>A directory of 8 hex characters decoding to a 4-character ASCII game code.</summary>
    HexAscii,
}

/// <summary>
/// One declared place a class C save unit can live.
/// </summary>
/// <remarks>
/// <b>A save unit is a (container, key) pair, and it is not a directory.</b> The plan's class
/// table called class C "a directory per game" and a real install refutes that on three systems
/// at once: <c>ps3</c> keeps <c>BLUS30109G6A383E91</c>, <c>BLUS30109G6A3B071C</c> and
/// <c>BLUS30109S</c> under one title id, <c>psp</c> keeps <c>UCES01011</c> beside
/// <c>ULES01513SYSDATA</c>, and <b><c>gamecube</c> has no per-game directory at all</b>: two
/// <c>.gci</c> files sharing a game code sit in one shared region folder. GameCube is what makes
/// the pair the only workable model.
/// <para>
/// <b>Scoping is the whole class C problem.</b> Hashing <c>saves/ps3/rpcs3</c> takes 426.07 s
/// over 52.87 GB because that is the emulator's entire data root; the savedata subtree a save
/// really is takes 0.06 s. So a container names the smallest thing that holds units, and
/// anything no container names is reported unknown rather than read.
/// </para>
/// </remarks>
/// <param name="Container">
/// Relative to <c>saves/</c>. A <c>*</c> segment matches exactly one directory, which is how
/// RPCS3's user id and Dolphin's region folder are covered without naming either.
/// </param>
/// <param name="Include">
/// When set, only this subpath of a matched member travels. Wii's NAND unit is the title
/// directory but only its <c>data/</c> is a save; its <c>content/title.tmd</c> is an installed
/// title's metadata and a title holding only that is a stub rather than a save.
/// </param>
public sealed record SaveUnitPath(
    string Container,
    string Emulator,
    SaveUnitKeyKind Key,
    string Slot,
    string? Include,
    string Evidence)
{
    private static readonly Regex TitleIdPattern = new(
        "^(?<key>[A-Za-z]{4}[0-9]{5})",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    // <makercode>-<gamecode>-<internal name>.gci. The anchored .gci is load-bearing: Dolphin
    // soft-deletes by appending .deleted, and a name ending .gci.deleted fails to match here
    // rather than being filtered out by a suffix list somewhere else.
    private static readonly Regex GciPattern = new(
        @"^[0-9A-Za-z]{2}-(?<key>[0-9A-Za-z]{4})-.+\.gci$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    /// <summary>True when a member is a file rather than a directory.</summary>
    public bool MembersAreFiles => Key == SaveUnitKeyKind.Gci;

    /// <summary>The container split into segments, for matching a <c>*</c> against a real tree.</summary>
    public string[] Segments => Container.Split('/', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// The unit key an entry name carries, or null when the entry is not part of any unit.
    /// </summary>
    /// <remarks>
    /// Returning null is the fail-closed answer and it is reached often: a <c>.gci.deleted</c>,
    /// a directory with no title-id prefix, and a NAND title id that is not a printable game
    /// code all land here, and none of them is touched afterwards.
    /// </remarks>
    public string? KeyOf(string entryName)
    {
        ArgumentNullException.ThrowIfNull(entryName);

        return Key switch
        {
            SaveUnitKeyKind.TitleId => Match(TitleIdPattern, entryName)?.ToUpperInvariant(),
            SaveUnitKeyKind.Name => string.IsNullOrWhiteSpace(entryName) ? null : entryName,
            SaveUnitKeyKind.Gci => Match(GciPattern, entryName)?.ToUpperInvariant(),
            SaveUnitKeyKind.HexAscii => DecodeHexAscii(entryName),
            _ => null,
        };
    }

    /// <summary>Reads a key kind, defaulting to <see cref="SaveUnitKeyKind.Unknown"/>.</summary>
    /// <remarks>
    /// An unrecognised kind makes the whole container unreadable rather than falling back to
    /// something permissive, so a future shape file this build does not understand reports its
    /// systems as unknown instead of walking them under the wrong rule.
    /// </remarks>
    public static SaveUnitKeyKind ParseKey(string? value) => value?.ToLowerInvariant() switch
    {
        "title_id" => SaveUnitKeyKind.TitleId,
        "name" => SaveUnitKeyKind.Name,
        "gci" => SaveUnitKeyKind.Gci,
        "hex_ascii" => SaveUnitKeyKind.HexAscii,
        _ => SaveUnitKeyKind.Unknown,
    };

    private static string? Match(Regex pattern, string value)
    {
        var match = pattern.Match(value);
        // Named rather than positional: RegexOptions.ExplicitCapture is on, which makes an
        // unnamed group non-capturing, so Groups[1] would be the empty string and every entry
        // in a container would collapse into one unit under one blank key.
        return match.Success ? match.Groups["key"].Value : null;
    }

    /// <summary>
    /// Decodes a Wii NAND title directory into the game code it stands for.
    /// </summary>
    /// <remarks>
    /// <c>52534245</c> is <c>RSBE</c>, which is the same code the <c>.rvz</c> header carries at
    /// <c>0x58</c>, so this is the one system where the save key and the ROM header agree by
    /// construction. Anything not decoding to four printable ASCII characters is refused, which
    /// keeps system titles and channels out.
    /// </remarks>
    private static string? DecodeHexAscii(string value)
    {
        if (value.Length != 8)
        {
            return null;
        }

        var decoded = new StringBuilder(4);

        for (var index = 0; index < 8; index += 2)
        {
            if (!byte.TryParse(
                    value.AsSpan(index, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out var octet)
                || octet is < 0x21 or > 0x7E)
            {
                return null;
            }

            decoded.Append((char)octet);
        }

        return decoded.ToString();
    }
}
