using System.IO.Compression;
using RomMBat.Core.RetroBat;

namespace RomMBat.Core.Content;

/// <summary>
/// Whether a firmware zip holds what a reader requires inside it.
/// </summary>
/// <remarks>
/// <b>A zip is identified by its members, never its container md5.</b> RomM and RetroBat both
/// hash the container (RM-28), so two builds of the same members disagree. Each reader's profile
/// states every member's crc32 and size, which is what MAME's own source checks.
/// <para>
/// <b>One reader satisfied is enough.</b> MAME and FBNeo list different members for the same
/// <c>neogeo.zip</c>, and a set that boots one of them is worth landing; a zip that satisfies
/// none is discarded and reported.
/// </para>
/// </remarks>
public static class BiosZip
{
    /// <summary>The readers whose every listed member the zip holds, or empty.</summary>
    /// <returns>Null when the file is not a readable zip.</returns>
    public static IReadOnlyList<string>? Satisfied(string path, IReadOnlyList<BiosZipContents> contents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(contents);

        try
        {
            using var archive = ZipFile.OpenRead(path);

            var members = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in archive.Entries)
            {
                members.TryAdd(entry.FullName.Replace('\\', '/'), entry);
                members.TryAdd(entry.Name, entry);
            }

            return [.. contents
                .Where(group => group.Files.All(wanted =>
                    members.TryGetValue(wanted.Name, out var entry)
                    && entry.Crc32 == wanted.Crc32
                    && (wanted.Size is not { } size || entry.Length == size)))
                .SelectMany(group => group.Readers)
                .Distinct(StringComparer.Ordinal)];
        }
        catch (InvalidDataException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
