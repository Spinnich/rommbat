using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using RomM.Client.Content;

namespace RomMBat.Core.RetroBat;

/// <summary>
/// Where media goes and what it is called, per RetroBat's own scraper convention.
/// </summary>
/// <remarks>
/// Read off a real scraped install rather than from memory: <c>images/&lt;stem&gt;-image.png</c>,
/// <c>images/&lt;stem&gt;-thumb.png</c>, <c>images/&lt;stem&gt;-marquee.png</c> (the marquee
/// lives under <c>images/</c>, not a folder of its own), <c>videos/&lt;stem&gt;-video.mp4</c>
/// and <c>manuals/&lt;stem&gt;-manual.pdf</c>, where the stem is the ROM file name without its
/// extension.
/// <para>
/// <b>These are exactly the names a user's own scrape writes</b>, which is why every file
/// RomMBat creates is recorded and nothing is ever deleted on the strength of its name alone.
/// </para>
/// </remarks>
public static class MediaNaming
{
    /// <summary>
    /// The longest a single path component may be.
    /// </summary>
    /// <remarks>
    /// The real ceiling for a constructed name, and not the one the plan expected. Measured:
    /// a 255-character file name writes and a 256-character one fails <c>IOException</c>,
    /// <b>with or without the <c>\\?\</c> prefix</b>, because it is a filesystem component
    /// limit rather than <c>MAX_PATH</c>. The same run wrote a 306-character total path
    /// without complaint.
    /// </remarks>
    public const int MaximumFileNameLength = 255;

    /// <summary>Characters Windows refuses in a name, plus the one it accepts and should not.</summary>
    /// <remarks>
    /// <c>&lt;</c>, <c>&gt;</c>, <c>"</c>, <c>|</c>, <c>?</c> and <c>*</c> fail loudly with an
    /// <c>IOException</c> and the separators fail with a <c>DirectoryNotFoundException</c>.
    /// <b><c>:</c> does not fail at all</b>: it opens an NTFS alternate data stream, so the
    /// write succeeds, the directory lists a file with the name truncated at the colon, and
    /// the file the gamelist points at is not there.
    /// </remarks>
    private static readonly char[] Forbidden = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    /// <summary>The subfolder each kind lives in, relative to the system's ROM folder.</summary>
    public static string FolderFor(MediaKind kind) => kind switch
    {
        MediaKind.Video => "videos",
        MediaKind.Manual => "manuals",
        _ => "images",
    };

    /// <summary>The suffix RetroBat's scraper appends before the extension.</summary>
    public static string SuffixFor(MediaKind kind) => kind switch
    {
        MediaKind.Image => "-image",
        MediaKind.Thumbnail => "-thumb",
        MediaKind.Marquee => "-marquee",
        MediaKind.Video => "-video",
        _ => "-manual",
    };

    /// <summary>The file name for one kind of media beside a ROM.</summary>
    /// <param name="romFileName">The ROM's file name, extension included.</param>
    /// <param name="kind">Which media.</param>
    /// <param name="extension">The extension the server served, with its dot, or empty.</param>
    public static string FileNameFor(string romFileName, MediaKind kind, string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(romFileName);

        var stem = StemOf(romFileName);
        var suffix = SuffixFor(kind);
        var tail = suffix + NormalizeExtension(extension);

        return Safe(stem, tail);
    }

    /// <summary>
    /// The ROM file name without its extension, which is what every media name is built on.
    /// </summary>
    /// <remarks>
    /// Only the last extension comes off. A real library holds
    /// <c>Game (USA) (Rev 1).xiso.iso</c>, and a scraper's stem for it is
    /// <c>Game (USA) (Rev 1).xiso</c>.
    /// </remarks>
    public static string StemOf(string romFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(romFileName);

        var dot = romFileName.LastIndexOf('.');
        return dot <= 0 ? romFileName : romFileName[..dot];
    }

    /// <summary>
    /// Makes a name Windows will accept, keeping <paramref name="tail"/> intact.
    /// </summary>
    /// <remarks>
    /// Two failures to head off, both of which happen at the write rather than before it.
    /// Forbidden characters become underscores, including the colon that would otherwise write
    /// an alternate data stream nobody can see. And a name over 255 characters is truncated
    /// with eight hex digits of the original's hash spliced in, because two long names that
    /// differ only past the cut would otherwise collide and the second game would show the
    /// first one's box art.
    /// </remarks>
    public static string Safe(string stem, string tail)
    {
        ArgumentNullException.ThrowIfNull(stem);
        ArgumentNullException.ThrowIfNull(tail);

        var cleaned = new StringBuilder(stem.Length);
        foreach (var character in stem)
        {
            cleaned.Append(character < 0x20 || Array.IndexOf(Forbidden, character) >= 0 ? '_' : character);
        }

        // Windows strips a trailing dot or space silently, so a name ending in one would not
        // be the name that landed, and the gamelist would point at nothing.
        var safe = cleaned.ToString().TrimEnd('.', ' ');

        if (safe.Length == 0)
        {
            safe = "_";
        }

        var full = safe + tail;
        if (full.Length <= MaximumFileNameLength)
        {
            return full;
        }

        var fingerprint = Fingerprint(stem);
        var room = MaximumFileNameLength - tail.Length - fingerprint.Length - 1;

        return room <= 0
            ? fingerprint + tail
            : string.Concat(safe.AsSpan(0, room), "-", fingerprint, tail);
    }

    /// <summary>Eight hex digits of the original name, so a truncation stays unique.</summary>
    private static string Fingerprint(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash.AsSpan(0, 4)).ToLowerInvariant();
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var trimmed = extension.Trim().ToLowerInvariant();
        var dotted = trimmed.StartsWith('.') ? trimmed : "." + trimmed;

        // An extension is a name component too, and one built from a path the server sent has
        // no guarantee about it.
        var cleaned = new StringBuilder(dotted.Length);
        foreach (var character in dotted)
        {
            if (character >= 0x20 && Array.IndexOf(Forbidden, character) < 0)
            {
                cleaned.Append(character);
            }
        }

        var result = cleaned.ToString();
        return result.Length is 0 or 1 ? string.Empty : result;
    }

    /// <summary>How a gamelist refers to a media file, relative to the system folder.</summary>
    public static string GamelistReference(MediaKind kind, string fileName) =>
        string.Create(CultureInfo.InvariantCulture, $"./{FolderFor(kind)}/{fileName}");
}
