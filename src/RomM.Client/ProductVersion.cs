using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace RomM.Client;

/// <summary>
/// A dotted numeric version that may carry a trailing suffix, as both RomM and RetroBat emit.
/// </summary>
/// <remarks>
/// RetroBat reports <c>8.2.0-stable-win64</c> and RomM has been observed reporting
/// <c>5.1.1-beta.1</c>, so neither string parses as <see cref="Version"/> and neither is a
/// clean semantic version.
/// <para>
/// Ordering uses the numeric components only and ignores the suffix. Semantic versioning
/// would rank <c>8.2.0-stable-win64</c> below <c>8.2.0</c> and refuse every stock RetroBat
/// install, because RetroBat's suffix names a channel and an architecture rather than a
/// prerelease. The cost is that a genuine prerelease compares equal to its release, which
/// is the lenient direction and the safe one for a minimum-version gate.
/// </para>
/// </remarks>
public readonly struct ProductVersion : IEquatable<ProductVersion>, IComparable<ProductVersion>
{
    private readonly int[]? _components;

    private ProductVersion(string raw, int[] components, string? suffix)
    {
        Raw = raw;
        _components = components;
        Suffix = suffix;
    }

    /// <summary>The string this was parsed from, for display and error messages.</summary>
    public string Raw { get; }

    /// <summary>Everything after the first '-', or null when there was none.</summary>
    public string? Suffix { get; }

    /// <summary>The numeric components, most significant first. Never empty on a parsed value.</summary>
    public IReadOnlyList<int> Components => _components ?? [];

    /// <summary>True when this was produced by a successful parse.</summary>
    public bool IsParsed => _components is { Length: > 0 };

    /// <summary>
    /// Parses a version string, taking the numeric core from before the first '-'.
    /// </summary>
    public static bool TryParse(string? value, out ProductVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var raw = value.Trim();

        // Split on the first '-' only: RetroBat's suffix carries a second one (stable-win64).
        var dash = raw.IndexOf('-', StringComparison.Ordinal);
        var core = dash < 0 ? raw : raw[..dash];
        var suffix = dash < 0 ? null : raw[(dash + 1)..];

        var parts = core.Split('.');
        var components = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out var component))
            {
                return false;
            }

            components[i] = component;
        }

        version = new ProductVersion(raw, components, string.IsNullOrEmpty(suffix) ? null : suffix);
        return true;
    }

    /// <summary>Parses a version string, throwing when it is not one.</summary>
    /// <exception cref="FormatException">The value has no parseable numeric core.</exception>
    public static ProductVersion Parse(string value)
    {
        if (!TryParse(value, out var version))
        {
            throw new FormatException($"'{value}' is not a version this client can compare.");
        }

        return version;
    }

    /// <summary>Compares numeric components only; a missing component counts as zero.</summary>
    public int CompareTo(ProductVersion other)
    {
        var mine = Components;
        var theirs = other.Components;
        var length = Math.Max(mine.Count, theirs.Count);

        for (var i = 0; i < length; i++)
        {
            var a = i < mine.Count ? mine[i] : 0;
            var b = i < theirs.Count ? theirs[i] : 0;
            if (a != b)
            {
                return a.CompareTo(b);
            }
        }

        return 0;
    }

    public bool Equals(ProductVersion other) => CompareTo(other) == 0;

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is ProductVersion other && Equals(other);

    public override int GetHashCode()
    {
        var hash = default(HashCode);
        foreach (var component in Components)
        {
            hash.Add(component);
        }

        return hash.ToHashCode();
    }

    public override string ToString() => Raw ?? string.Empty;

    public static bool operator ==(ProductVersion left, ProductVersion right) => left.Equals(right);

    public static bool operator !=(ProductVersion left, ProductVersion right) => !left.Equals(right);

    public static bool operator <(ProductVersion left, ProductVersion right) => left.CompareTo(right) < 0;

    public static bool operator <=(ProductVersion left, ProductVersion right) => left.CompareTo(right) <= 0;

    public static bool operator >(ProductVersion left, ProductVersion right) => left.CompareTo(right) > 0;

    public static bool operator >=(ProductVersion left, ProductVersion right) => left.CompareTo(right) >= 0;
}
