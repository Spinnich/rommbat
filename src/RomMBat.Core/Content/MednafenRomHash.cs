using System.IO.Compression;
using System.Security.Cryptography;

namespace RomMBat.Core.Content;

/// <summary>
/// The md5 mednafen puts on the stem of a save it names with <c>%M</c>, per system.
/// </summary>
/// <remarks>
/// <b>What is hashed is the system's, not mednafen's.</b> On <c>nes</c> it is the ROM less its
/// 16-byte iNES header, which describes the cartridge rather than being on it
/// (<see cref="HeaderlessNesHash"/>). On <c>megadrive</c> it is the whole <c>.md</c>, measured on
/// <c>Sonic &amp; Knuckles + Sonic The Hedgehog 3 (USA) (Lock-on Combination).zip</c>: the file
/// inside hashes to <c>c5b1c655...</c>, and mednafen wrote
/// <c>&lt;rom&gt;.c5b1c655c19f462ade0ac4e17a844d10.sav</c>. On <c>gba</c> it is the whole <c>.gba</c>,
/// measured on Pokemon - Emerald Version (USA, Europe): <c>605b89b6...</c> on the standalone's
/// name and on mednafen_gba's, which carries the zip's member too (<see cref="ArchiveMemberOf"/>).
/// On <c>snes</c> it is the whole <c>.sfc</c>, measured on Legend of Zelda, The - A Link to the
/// Past (USA): <c>608c22b8...</c>, on a loose <c>.srm</c> rather than a <c>.sav</c>. On
/// <c>mastersystem</c> it is the whole <c>.sms</c>, measured on Golden Axe Warrior (USA, Europe,
/// Brazil) (En): <c>d46e40bb...</c>.
/// <para>
/// <b>Null for anything unmeasured</b>, so a restore reports the save as unnameable rather than
/// writing a file mednafen will not look for: every other system, and on <c>megadrive</c> any
/// format but a plain <c>.md</c>, since an interleaved <c>.smd</c> is decoded before it is hashed
/// and a <c>.bin</c> or <c>.gen</c> has not been driven. On <c>snes</c> only a <c>.sfc</c>, since a
/// <c>.smc</c> can carry a 512-byte copier header and none has been driven. On <c>mastersystem</c>
/// only a <c>.sms</c>, the one format the measured library holds.
/// </para>
/// </remarks>
public static class MednafenRomHash
{
    /// <summary>Hashes the ROM at this path the way mednafen does for this system.</summary>
    public static string? Of(string absolutePath, string system)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(system);

        return system.ToLowerInvariant() switch
        {
            "nes" => HeaderlessNesHash.Of(absolutePath),
            "megadrive" => WholeRom(absolutePath, ".md")?.Hash,
            "gba" => WholeRom(absolutePath, ".gba")?.Hash,
            "snes" => WholeRom(absolutePath, ".sfc")?.Hash,
            "mastersystem" => WholeRom(absolutePath, ".sms")?.Hash,
            _ => null,
        };
    }

    /// <summary>
    /// The stem of the one file inside a zipped ROM and its hash, which is what
    /// <c>libretro</c>/<c>mednafen_gba</c> names a save after, or null for anything else.
    /// </summary>
    /// <remarks>
    /// A ROM that is not a zip answers null. A <c>.7z</c> is named the same way but cannot be
    /// read here, and a bare <c>.gba</c> gets mednafen standalone's own hashed name, which is the
    /// <c>mednafen:battery</c> file rather than this slot's.
    /// </remarks>
    public static (string MemberStem, string Hash)? ArchiveMemberOf(string absolutePath, string system)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(system);

        return string.Equals(system, "gba", StringComparison.OrdinalIgnoreCase)
            && ContentHasher.LooksLikeZip(absolutePath)
            && WholeRom(absolutePath, ".gba") is { Member: { } member, Hash: var hash }
                ? (Path.GetFileNameWithoutExtension(member), hash)
                : null;
    }

    /// <summary>Hashes a ROM of one plain format, bare or as the only file in a zip.</summary>
    private static (string? Member, string Hash)? WholeRom(string absolutePath, string extension)
    {
        try
        {
            if (ContentHasher.LooksLikeZip(absolutePath))
            {
                using var archive = ZipFile.OpenRead(absolutePath);
                var entries = archive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name)).ToList();

                if (entries.Count != 1 || !Is(entries[0].Name, extension))
                {
                    return null;
                }

                using var content = entries[0].Open();
                return (entries[0].Name, Hash(content));
            }

            if (ContentHasher.LooksLikeOpaqueArchive(absolutePath) || !Is(absolutePath, extension))
            {
                return null;
            }

            using var file = File.OpenRead(absolutePath);
            return (null, Hash(file));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    private static bool Is(string name, string extension) =>
        string.Equals(Path.GetExtension(name), extension, StringComparison.OrdinalIgnoreCase);

#pragma warning disable CA5351 // MD5, deliberately: it is the name mednafen gives the file.
    private static string Hash(Stream stream) => Convert.ToHexStringLower(MD5.HashData(stream));
#pragma warning restore CA5351
}
