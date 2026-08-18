namespace RomMBat.Core.Content;

/// <summary>What reading a ROM's head produced.</summary>
/// <param name="GameId">The code read, or null when none could be.</param>
/// <param name="Reason">
/// Why not, when <paramref name="GameId"/> is null. Always populated on failure, because a
/// container this cannot read is reported to the user rather than silently skipped.
/// </param>
public sealed record RomGameIdResult(string? GameId, string? Reason)
{
    public static RomGameIdResult Found(string gameId) => new(gameId, null);

    public static RomGameIdResult Refused(string reason) => new(null, reason);
}

/// <summary>
/// Reads a Game ID out of the head of a ROM, for the containers where one is there to read.
/// </summary>
/// <remarks>
/// <b>Measured, and the honest scope is much narrower than the plan assumed.</b> Every image in
/// five systems on a real install was head-read: <c>gamecube</c> is 178 <c>.rvz</c> and
/// <b>100%</b> readable, <c>wii</c> is 40 <c>.rvz</c> plus 13 <c>.wad</c> and <b>75.5%</b>, and
/// <c>psp</c> (147 <c>.cso</c>, 7 <c>.chd</c>), <c>ps3</c> (23 <c>.dec.iso</c>) and <c>psx</c>
/// (386 <c>.chd</c>) are <b>0%</b>. No constant offset reaches a compressed UMD image, a CHD or
/// an ISO9660 filesystem, where the identifier is a file inside rather than a field in a header.
/// <para>
/// <b>So this route serves GameCube and Wii and nothing else</b>, which happens to be where it
/// is irreplaceable: their save key <i>is</i> the game code, and a Wii NAND directory name
/// decodes to exactly what sits at <c>0x58</c>. The system this milestone's "done when" names is
/// PSP, which this route cannot touch at all; the journal and sidecar routes carry that.
/// </para>
/// <para>
/// <b>256 bytes, from the local file.</b> M3 established that a single-file ROM accepts a
/// bounded <c>Range</c>, so the same read works against the server for a ROM this device does
/// not hold, but nothing here downloads: a ROM that is not on disk is one whose saves are not
/// being attributed for eviction anyway.
/// </para>
/// </remarks>
public static class RomGameId
{
    /// <summary>Enough for every header this reads, and small enough to be free.</summary>
    public const int HeadBytes = 256;

    private static readonly byte[] WiiMagic = [0x5D, 0x1C, 0x9E, 0xA3];
    private static readonly byte[] GameCubeMagic = [0xC2, 0x33, 0x9F, 0x3D];

    /// <summary>Reads the head of a file and asks what it says.</summary>
    public static RomGameIdResult Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        byte[] head;

        try
        {
            using var stream = File.OpenRead(path);
            head = new byte[HeadBytes];
            var read = stream.ReadAtLeast(head, HeadBytes, throwOnEndOfStream: false);
            Array.Resize(ref head, read);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return RomGameIdResult.Refused($"it could not be read: {ex.Message}");
        }

        return Parse(head);
    }

    /// <summary>Reads a head already in hand, which is what a test and a ranged fetch both hold.</summary>
    public static RomGameIdResult Parse(ReadOnlySpan<byte> head)
    {
        if (head.Length < 0x5C)
        {
            return RomGameIdResult.Refused("it is shorter than any header this can read");
        }

        // A raw GameCube or Wii disc image carries the code at offset 0. Correct in principle
        // and never once exercised on a real library, which is exactly why the magic is checked
        // rather than assumed: without it, an .rvz read at offset 0 yields the literal bytes
        // "RVZ." and they pass a naive game-code shape test.
        if (IsGameCode(head[..4]))
        {
            if (head[0x18..0x1C].SequenceEqual(WiiMagic))
            {
                return RomGameIdResult.Found(Code(head[..4]));
            }

            if (head[0x1C..0x20].SequenceEqual(GameCubeMagic))
            {
                return RomGameIdResult.Found(Code(head[..4]));
            }
        }

        if (head[..3].SequenceEqual("RVZ"u8))
        {
            // The version follows the magic, and a later revision that moves the embedded disc
            // header moves this offset with it. Confirmed as version 1 on 218 real images.
            var version = BitConverter.ToUInt32(head[4..8]);

            if (version != 1)
            {
                return RomGameIdResult.Refused(
                    $"it is an RVZ container at format version {version}, and the offset this "
                        + "reads was only measured against version 1");
            }

            var code = head[0x58..0x5C];

            return IsGameCode(code)
                ? RomGameIdResult.Found(Code(code))
                : RomGameIdResult.Refused("it is an RVZ container whose 0x58 is not a game code");
        }

        // A WAD's header is a size then a type, and its title id lives inside the ticket behind
        // a certificate chain of variable length. 13 of 53 Wii images on a real install, and
        // reachable by no constant offset at all.
        if (BitConverter.ToUInt32([head[3], head[2], head[1], head[0]]) == 0x20
            && (head[4..6].SequenceEqual("Is"u8) || head[4..6].SequenceEqual("ib"u8)))
        {
            return RomGameIdResult.Refused(
                "it is a WAD, whose title id sits behind a variable-length certificate chain "
                    + "and cannot be read at a fixed offset");
        }

        if (head[..4].SequenceEqual("CISO"u8))
        {
            return RomGameIdResult.Refused(
                "it is a compressed UMD image, whose game id is inside PARAM.SFO in the "
                    + "filesystem rather than in a header");
        }

        if (head[..5].SequenceEqual("MComp"u8))
        {
            return RomGameIdResult.Refused("it is a CHD, which carries no header in the clear");
        }

        return RomGameIdResult.Refused("it carries no header this build recognises");
    }

    /// <summary>Four upper-case letters or digits, which is the disc game-code shape.</summary>
    private static bool IsGameCode(ReadOnlySpan<byte> value)
    {
        foreach (var octet in value)
        {
            if (octet is not ((>= (byte)'A' and <= (byte)'Z') or (>= (byte)'0' and <= (byte)'9')))
            {
                return false;
            }
        }

        return true;
    }

    private static string Code(ReadOnlySpan<byte> value) => System.Text.Encoding.ASCII.GetString(value);
}
