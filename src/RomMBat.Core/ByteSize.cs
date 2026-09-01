using System.Globalization;

namespace RomMBat.Core;

/// <summary>
/// Byte counts as a person reads them.
/// </summary>
/// <remarks>
/// One implementation, because M3 would otherwise have three: a set resolution, a content plan
/// and a filesystem refusal all report sizes, and two of them would end up disagreeing about
/// rounding in the same console output.
/// <para>
/// Binary units under decimal names, which is what a ROM library uses: a "4 GB" FAT32 ceiling
/// is 4,294,967,296 bytes, not 4,000,000,000, and reporting it any other way would make the
/// refusal message contradict the number it refused.
/// </para>
/// </remarks>
public static class ByteSize
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    /// <summary>Formats a byte count, to one decimal place where that adds anything.</summary>
    public static string Format(long bytes)
    {
        var unit = UnitFor(bytes);
        return string.Create(CultureInfo.InvariantCulture, $"{Scale(bytes, unit):0.#} {Units[unit]}");
    }

    /// <summary>
    /// A running total against the total it is heading for, both in the larger one's unit.
    /// </summary>
    /// <remarks>
    /// <b>The unit and the decimal place are fixed so the text does not change width.</b>
    /// Formatting each side on its own rescales the left one as it grows, KB to MB to GB, and
    /// <c>0.#</c> drops a decimal at every round number, so a progress line rebuilt eight times
    /// a second is a different length almost every time. Centred, that reads as the text
    /// vibrating: a hands-on pass on a set of small ROMs called it double vision. The
    /// destination is fixed for the whole run, so taking the unit from it makes the left side
    /// grow through a stable scale, and one forced decimal keeps the width constant within it.
    /// </remarks>
    public static string Progress(long soFar, long total)
    {
        var unit = UnitFor(total);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Scale(soFar, unit):0.0} of {Scale(total, unit):0.0} {Units[unit]}");
    }

    private static int UnitFor(long bytes)
    {
        double value = Math.Abs(bytes);
        var unit = 0;

        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit;
    }

    private static double Scale(long bytes, int unit)
    {
        double value = bytes;

        for (var step = 0; step < unit; step++)
        {
            value /= 1024;
        }

        return value;
    }

    /// <summary>
    /// Reads a size written the way people write one: <c>8GB</c>, <c>500 MB</c>, <c>1024</c>.
    /// </summary>
    /// <returns>The byte count, or null when the text is not a size.</returns>
    public static long? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim().ToUpperInvariant();
        var multiplier = 1L;

        foreach (var (suffix, scale) in new[] { ("TB", 1L << 40), ("GB", 1L << 30), ("MB", 1L << 20), ("KB", 1L << 10) })
        {
            if (text.EndsWith(suffix, StringComparison.Ordinal))
            {
                multiplier = scale;
                text = text[..^suffix.Length].Trim();
                break;
            }
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? (long)(number * multiplier)
            : null;
    }
}
