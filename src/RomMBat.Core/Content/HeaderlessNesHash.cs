using System.IO.Compression;
using System.Security.Cryptography;

namespace RomMBat.Core.Content;

/// <summary>
/// The md5 of an iNES ROM with its 16-byte header left off, which is what mednafen names a
/// <c>nes</c> save and state after.
/// </summary>
/// <remarks>
/// Measured on <c>Final Fantasy (USA).zip</c>: the whole <c>.nes</c> inside hashes to
/// <c>a475798c...</c>, which is what RomM and <see cref="ContentHasher"/> record, and the same
/// bytes after the header hash to <c>24ae5edf...</c>, which is the one mednafen wrote into
/// <c>Final Fantasy (USA).24ae5edf8375162f91a6846d3202e3d6.sav</c>.
/// <para>
/// <b>Null rather than a guess wherever the measurement does not reach.</b> A ROM carrying a
/// 512-byte trainer after the header, a file without the iNES magic, a zip holding anything but
/// one file, and any archive the base class library cannot open all answer null, so a restore
/// reports the save as unnameable instead of writing a file mednafen will not look for.
/// </para>
/// </remarks>
public static class HeaderlessNesHash
{
    private const int HeaderBytes = 16;

    private static ReadOnlySpan<byte> Magic => "NES"u8;

    /// <summary>Hashes a <c>.nes</c> file, or the one file inside a <c>.zip</c>.</summary>
    public static string? Of(string absolutePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);

        try
        {
            if (ContentHasher.LooksLikeZip(absolutePath))
            {
                using var archive = ZipFile.OpenRead(absolutePath);
                var entries = archive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name)).ToList();

                if (entries.Count != 1)
                {
                    return null;
                }

                using var content = entries[0].Open();
                return OfStream(content);
            }

            if (ContentHasher.LooksLikeOpaqueArchive(absolutePath))
            {
                return null;
            }

            using var file = File.OpenRead(absolutePath);
            return OfStream(file);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    private static string? OfStream(Stream stream)
    {
        Span<byte> header = stackalloc byte[HeaderBytes];

        if (stream.ReadAtLeast(header, HeaderBytes, throwOnEndOfStream: false) < HeaderBytes
            || !header[..4].SequenceEqual(Magic)
            || (header[6] & 0x04) != 0)
        {
            return null;
        }

#pragma warning disable CA5351 // MD5, deliberately: it is the name mednafen gives the file.
        using var md5 = MD5.Create();
#pragma warning restore CA5351

        return Convert.ToHexString(md5.ComputeHash(stream)).ToLowerInvariant();
    }
}
