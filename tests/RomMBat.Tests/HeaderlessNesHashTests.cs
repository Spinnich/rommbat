using System.IO.Compression;
using System.Security.Cryptography;
using RomMBat.Core.Content;
using Xunit;

namespace RomMBat.Tests;

public sealed class HeaderlessNesHashTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("rommbat-nes-hash-").FullName;

    [Fact]
    public void A_zipped_rom_hashes_the_nes_inside_less_its_header()
    {
        var body = Body("prg then chr");
        var zip = Path.Combine(_root, "Game (USA).zip");

        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        using (var stream = archive.CreateEntry("Game (USA).nes").Open())
        {
            stream.Write(Header(flags6: 0));
            stream.Write(body);
        }

        Assert.Equal(Md5(body), HeaderlessNesHash.Of(zip));
    }

    [Fact]
    public void A_bare_nes_file_hashes_the_same_way()
    {
        var body = Body("prg then chr");
        var nes = Path.Combine(_root, "Game (USA).nes");
        File.WriteAllBytes(nes, [.. Header(flags6: 0), .. body]);

        Assert.Equal(Md5(body), HeaderlessNesHash.Of(nes));
    }

    [Fact]
    public void A_rom_with_a_trainer_is_not_guessed_at()
    {
        // Nothing measured says whether mednafen hashes the 512-byte trainer, so no name is
        // offered rather than one mednafen may not look for.
        var nes = Path.Combine(_root, "Trainer (USA).nes");
        File.WriteAllBytes(nes, [.. Header(flags6: 0x04), .. new byte[600]]);

        Assert.Null(HeaderlessNesHash.Of(nes));
    }

    [Fact]
    public void A_nes2_header_with_byte_9_clear_hashes_like_ines()
    {
        // Every ROM on the measured install carries this header, the three measured among them.
        var body = Body("prg then chr");
        var nes = Path.Combine(_root, "Game (USA).nes");
        File.WriteAllBytes(nes, [.. Header(flags6: 0, flags7: 0x08), .. body]);

        Assert.Equal(Md5(body), HeaderlessNesHash.Of(nes));
    }

    [Fact]
    public void A_nes2_header_with_size_bits_in_byte_9_is_not_guessed_at()
    {
        var nes = Path.Combine(_root, "Big (USA).nes");
        File.WriteAllBytes(nes, [.. Header(flags6: 0, flags7: 0x08, byte9: 0x01), .. Body("prg then chr")]);

        Assert.Null(HeaderlessNesHash.Of(nes));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void A_rom_longer_or_shorter_than_its_header_declares_is_not_guessed_at(int delta)
    {
        // Whether mednafen hashes padding past the declared banks is unmeasured.
        var body = Body("prg then chr");
        var nes = Path.Combine(_root, "Overdump (USA).nes");
        File.WriteAllBytes(nes, [.. Header(flags6: 0), .. body.AsSpan(0, body.Length + Math.Min(delta, 0)), .. new byte[Math.Max(delta, 0)]]);

        Assert.Null(HeaderlessNesHash.Of(nes));
    }

    [Fact]
    public void A_header_declaring_no_prg_is_not_guessed_at()
    {
        var nes = Path.Combine(_root, "Empty (USA).nes");
        File.WriteAllBytes(nes, [.. Header(flags6: 0, prg: 0, chr: 0)]);

        Assert.Null(HeaderlessNesHash.Of(nes));
    }

    [Fact]
    public void A_file_without_the_ines_magic_is_not_hashed()
    {
        var bogus = Path.Combine(_root, "Not A Rom.nes");
        File.WriteAllText(bogus, "rom");

        Assert.Null(HeaderlessNesHash.Of(bogus));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>One PRG bank and one CHR bank, the lengths <see cref="Header"/> declares by default.</summary>
    private static byte[] Body(string seed)
    {
        var body = new byte[16384 + 8192];
        System.Text.Encoding.ASCII.GetBytes(seed).CopyTo(body, 0);
        return body;
    }

    private static byte[] Header(byte flags6, byte prg = 1, byte chr = 1, byte flags7 = 0, byte byte9 = 0)
    {
        var header = new byte[16];
        header[4] = prg;
        header[5] = chr;
        header[7] = flags7;
        header[9] = byte9;
        "NES"u8.CopyTo(header);
        header[6] = flags6;
        return header;
    }

#pragma warning disable CA5351 // The name mednafen gives the file.
    private static string Md5(byte[] bytes) => Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();
#pragma warning restore CA5351
}
