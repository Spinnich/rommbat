namespace RomMBat.Core.Content;

/// <summary>
/// Recognises a PlayStation memory card image that has been formatted and never written to.
/// </summary>
/// <remarks>
/// <b>An emulator writes one of these on exit whether or not the game saved.</b> Measured on
/// <c>psx</c> under <c>libretro</c>/<c>mednafen_psx_hw</c>: Castlevania: Symphony of the Night
/// booted to its name entry and quit left a 131,072 B loose <c>.srm</c> whose fifteen directory
/// frames were all free, and the flush uploaded it as that game's save. On a second device the
/// same boot write would go up as the newer save, so it is treated as holding nothing.
/// <para>
/// <b>Only a frame that was never used counts as free.</b> The BIOS marks a formatted frame
/// <c>0xA0</c> and a deleted one <c>0xA1</c> to <c>0xA3</c>, so a card whose saves were deleted in
/// the game still syncs, since deleting them was something the player did.
/// </para>
/// </remarks>
public static class Ps1MemoryCard
{
    /// <summary>The size of a raw card: sixteen 8 KB blocks.</summary>
    public const int CardBytes = 128 * 1024;

    private const int FrameBytes = 128;
    private const int DirectoryFrames = 15;
    private const byte NeverUsed = 0xA0;

    private static ReadOnlySpan<byte> Magic => "MC"u8;

    /// <summary>True when the file is a raw PS1 card with every directory frame never used.</summary>
    public static bool IsBlank(string absolutePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);

        try
        {
            // The length first, so the scan opens no file that cannot be a card.
            if (new FileInfo(absolutePath).Length != CardBytes)
            {
                return false;
            }

            using var stream = File.OpenRead(absolutePath);
            var header = new byte[FrameBytes * (DirectoryFrames + 1)];
            stream.ReadExactly(header);
            return IsBlank(header);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>True when the bytes open a card whose directory frames are all never used.</summary>
    /// <param name="header">At least the first sixteen 128-byte frames of the card.</param>
    public static bool IsBlank(ReadOnlySpan<byte> header)
    {
        if (header.Length < FrameBytes * (DirectoryFrames + 1) || !header.StartsWith(Magic))
        {
            return false;
        }

        for (var frame = 1; frame <= DirectoryFrames; frame++)
        {
            if (header[frame * FrameBytes] != NeverUsed)
            {
                return false;
            }
        }

        return true;
    }
}
