using System.Diagnostics.CodeAnalysis;

namespace RomMBat.Core.Paths;

/// <summary>
/// A path relative to the RetroBat root, normalised to forward slashes.
/// </summary>
/// <remarks>
/// This is the only path shape any persisted record ever holds. RetroBat is portable, so
/// the drive letter changes: an install that moved G: to D: to K: during M0 probe 7 must
/// treat that as a non-event. Construction rejects anything rooted, drive-relative, UNC, or
/// carrying a <c>..</c> segment that would climb out of the tree, so an absolute path
/// cannot reach the database through a typed API.
/// <para>
/// Forward slashes are stored because they round-trip through SQLite, JSON and XML without
/// escaping, and Windows accepts them everywhere. <see cref="RetroBatInstall.Resolve(RelativePath)"/> is
/// the one place they become a real path.
/// </para>
/// </remarks>
public readonly struct RelativePath : IEquatable<RelativePath>, IComparable<RelativePath>
{
    private readonly string? _value;

    private RelativePath(string value) => _value = value;

    /// <summary>The normalised path, forward-slashed, with no leading or trailing slash.</summary>
    public string Value => _value ?? string.Empty;

    /// <summary>True when this holds a real path rather than the default value.</summary>
    public bool HasValue => !string.IsNullOrEmpty(_value);

    /// <summary>The last segment, which for a file is its name.</summary>
    public string Name
    {
        get
        {
            var value = Value;
            var slash = value.LastIndexOf('/');
            return slash < 0 ? value : value[(slash + 1)..];
        }
    }

    /// <summary>
    /// Normalises and validates a relative path.
    /// </summary>
    /// <returns>False when the value is absolute, escapes the root, or is empty.</returns>
    public static bool TryCreate(string? value, out RelativePath path)
    {
        path = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Replace('\\', '/').Trim();

        // Catches C:\x, C:x, /x, \x, \\server\share and \\?\C:\x in one go on Windows, and
        // is checked before normalisation so nothing can be smuggled past it.
        if (Path.IsPathRooted(candidate) || candidate.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        // A colon anywhere else means a drive-relative path or an alternate data stream.
        if (candidate.Contains(':', StringComparison.Ordinal))
        {
            return false;
        }

        var segments = new List<string>();
        foreach (var segment in candidate.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            // Never resolved away: ".." would climb out of the tree, and a stored path that
            // escapes the root is exactly the thing this type exists to prevent.
            if (segment == "..")
            {
                return false;
            }

            segments.Add(segment);
        }

        if (segments.Count == 0)
        {
            return false;
        }

        path = new RelativePath(string.Join('/', segments));
        return true;
    }

    /// <summary>Normalises and validates a relative path, throwing when it is not one.</summary>
    /// <exception cref="ArgumentException">The value is absolute, escapes the root, or is empty.</exception>
    public static RelativePath Create(string value)
    {
        if (!TryCreate(value, out var path))
        {
            throw new ArgumentException(
                $"'{value}' is not a path relative to the RetroBat root. "
                    + "No stored path may be absolute, drive-relative, UNC, or contain '..'.",
                nameof(value));
        }

        return path;
    }

    /// <summary>Appends a segment or subpath.</summary>
    public RelativePath Combine(string relative) => Create($"{Value}/{relative}");

    /// <summary>Paths compare case-insensitively, because Windows filesystems do.</summary>
    public bool Equals(RelativePath other) =>
        string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public int CompareTo(RelativePath other) =>
        string.Compare(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is RelativePath other && Equals(other);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    public override string ToString() => Value;

    public static bool operator ==(RelativePath left, RelativePath right) => left.Equals(right);

    public static bool operator !=(RelativePath left, RelativePath right) => !left.Equals(right);

    public static bool operator <(RelativePath left, RelativePath right) => left.CompareTo(right) < 0;

    public static bool operator <=(RelativePath left, RelativePath right) => left.CompareTo(right) <= 0;

    public static bool operator >(RelativePath left, RelativePath right) => left.CompareTo(right) > 0;

    public static bool operator >=(RelativePath left, RelativePath right) => left.CompareTo(right) >= 0;
}
