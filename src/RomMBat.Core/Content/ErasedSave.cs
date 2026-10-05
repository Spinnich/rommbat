namespace RomMBat.Core.Content;

/// <summary>
/// Recognises a battery file that holds nothing but <c>0xFF</c>, which is save memory no game wrote.
/// </summary>
/// <remarks>
/// <b>ares writes one for every cartridge, battery or not.</b> Measured on <c>gamegear</c> under
/// <c>ares</c>/<c>GameGear</c> on 8.2.1: Sonic Chaos and Castle of Illusion, neither of which has
/// a battery, each left a 32,768 B <c>.ram</c> of <c>0xFF</c> on <c>Esc</c> (#453). Uploaded, every
/// game played under ares would gain a blank save on the server.
/// <para>
/// <b>It holds for any rule on any system</b>, by the maintainer's decision, because <c>0xFF</c> is
/// the erased state of SRAM, flash and EEPROM alike, so a save a game keeps never looks like this.
/// A game that erases its own save does leave one, and that wipe stays local: the accepted cost.
/// It is <see cref="Ps1MemoryCard"/>'s test for a format with no structure to read.
/// </para>
/// </remarks>
public static class ErasedSave
{
    private const byte Erased = 0xFF;

    /// <summary>True when the file is non-empty and every byte is <c>0xFF</c>.</summary>
    public static bool IsErased(string absolutePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);

        try
        {
            using var stream = File.OpenRead(absolutePath);
            if (stream.Length == 0)
            {
                return false;
            }

            // A written save stops the read at its first non-0xFF byte, usually in the first block.
            var buffer = new byte[64 * 1024];
            int read;
            while ((read = stream.Read(buffer)) > 0)
            {
                if (!IsErased(buffer.AsSpan(0, read)))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>True when the bytes are non-empty and every one is <c>0xFF</c>.</summary>
    public static bool IsErased(ReadOnlySpan<byte> contents) =>
        !contents.IsEmpty && !contents.ContainsAnyExcept(Erased);
}
