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
/// Whether a ROM's filename names it as one disc of a set.
/// </summary>
/// <remarks>
/// <b>The marker is the one thing that binds two rows into one game</b>, because RomM holds a
/// loose disc set as a ROM per disc. <c>GameSync</c> groups a plan's steps by it, so a sync that
/// rolls a game back takes every disc of it.
/// <para>
/// <b>Measured rather than guessed at.</b> Across the whole roms tree of a real install, 202
/// files carry a disc marker and every one of them writes it <c>(Disc N)</c> with N numeric,
/// spread over <c>psx</c>, <c>saturn</c>, <c>dreamcast</c>, <c>gamecube</c>, <c>3do</c> and
/// <c>ps2</c>. No <c>(Disk</c>, <c>(CD</c> or <c>(Side</c> appears. The other forms are matched
/// anyway, since recognizing a marker nobody writes costs nothing.
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
}
