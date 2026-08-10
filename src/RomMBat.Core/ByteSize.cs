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
        double value = bytes;
        var unit = 0;

        while (Math.Abs(value) >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{value:0.#} {Units[unit]}");
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
