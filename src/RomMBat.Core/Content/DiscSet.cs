using System.Globalization;
using System.Text.RegularExpressions;

namespace RomMBat.Core.Content;

/// <summary>A ROM filename that names itself as one disc of several.</summary>
/// <param name="BaseTitle">Everything before the marker, which is what the set shares.</param>
/// <param name="Number">The disc number as written.</param>
/// <param name="Tail">
/// Whatever follows the marker, minus the extension. Empty for most, and not for 53 of the
/// 202 disc files on the measured library: <c>(Rev 1)</c>, <c>(Unl)</c> and translation tags
/// all sit behind it. So the base is the text before the marker and never the whole stem with
/// the marker cut out of the middle.
/// </param>
public sealed record DiscMarker(string BaseTitle, int Number, string Tail);

/// <summary>
/// Whether a ROM is one disc of a set, which decides whether it may be converted.
/// </summary>
/// <remarks>
/// <b>PCSX2 cannot bind discs at all.</b> `pcsx2_slot1_memory=game` names the card after the
/// rom basename, so each disc of a set gets its own card and the save disappears at the disc
/// change, where the stock shared <c>Mcd001.ps2</c> would have carried it through. That is what
/// makes the class D conversion a per-game decision rather than a per-system one, and it is why
/// this class exists at all rather than the conversion simply applying to <c>ps2</c>.
/// <para>
/// <b>The refusal is on the marker alone, not on whether siblings are on disk.</b> A rule that
/// looked for siblings would convert a game whose other discs have not been synced yet and then
/// spring the trap when they arrive, which makes the safety of a conversion depend on the order
/// a library happens to be pulled in. Refusing every marked disc is scan-state independent, and
/// the cost of being wrong in this direction is only that a save stays on the shared card, where
/// it still works and is merely unattributable.
/// </para>
/// <para>
/// <b>Measured rather than guessed at.</b> Across the whole roms tree of a real install, 202
/// files carry a disc marker and every one of them writes it <c>(Disc N)</c> with N numeric,
/// spread over <c>psx</c>, <c>saturn</c>, <c>dreamcast</c>, <c>gamecube</c>, <c>3do</c> and
/// <c>ps2</c>. No <c>(Disk</c>, <c>(CD</c> or <c>(Side</c> appears. The other forms are matched
/// anyway, because recognising a marker this library does not use costs a conversion that was
/// never offered, and failing to recognise one costs a save.
/// </para>
/// <para>
/// On the same install <c>ps2</c> is 302 single-disc titles against 7 sets of two, with
/// <b>no <c>.m3u</c> anywhere and no per-game folders</b>, so loose sibling files are the only
/// layout the refusal has to read. RetroBat's <c>ps2</c> does list <c>.m3u</c> in
/// <c>&lt;extension&gt;</c>, but its own wiki says PCSX2 cannot use one, so a playlist is not
/// what binds a PS2 set and its absence proves nothing.
/// </para>
/// </remarks>
public static class DiscSet
{
    private static readonly Regex MarkerPattern = new(
        @"\((?:disc|disk|cd|side)\s*(?<number>\d+)\)",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    /// <summary>Reads the disc marker off a filename, or null when it carries none.</summary>
    /// <param name="fsName">The ROM's filename as it sits on disk, extension included.</param>
    public static DiscMarker? Parse(string fsName)
    {
        ArgumentNullException.ThrowIfNull(fsName);

        var stem = Path.GetFileNameWithoutExtension(fsName);
        var match = MarkerPattern.Match(stem);

        if (!match.Success
            || !int.TryParse(
                match.Groups["number"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var number))
        {
            return null;
        }

        return new DiscMarker(
            stem[..match.Index].TrimEnd(),
            number,
            stem[(match.Index + match.Length)..].Trim());
    }

    /// <summary>True when this ROM names itself as one disc of several.</summary>
    public static bool IsOneDiscOfASet(string fsName) => Parse(fsName) is not null;

    /// <summary>
    /// The other files in the same folder that belong to this ROM's set.
    /// </summary>
    /// <remarks>
    /// Only for telling the user what they are looking at: the refusal does not depend on this
    /// finding anything, and an empty answer for a marked disc means the rest of the set has
    /// not been synced rather than that there is no set.
    /// </remarks>
    public static IReadOnlyList<string> SiblingsOf(string fsName, IEnumerable<string> namesInFolder)
    {
        ArgumentNullException.ThrowIfNull(namesInFolder);

        if (Parse(fsName) is not { } marker)
        {
            return [];
        }

        return
        [
            .. namesInFolder
                .Where(name =>
                    !string.Equals(name, fsName, StringComparison.OrdinalIgnoreCase)
                    && Parse(name) is { } other
                    && string.Equals(other.BaseTitle, marker.BaseTitle, StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.OrdinalIgnoreCase),
        ];
    }
}
