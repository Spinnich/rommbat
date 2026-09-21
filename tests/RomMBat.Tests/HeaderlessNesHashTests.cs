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
        var body = "prg then chr"u8.ToArray();
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
        var body = "prg then chr"u8.ToArray();
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
    public void A_file_without_the_ines_magic_is_not_hashed()
    {
        var bogus = Path.Combine(_root, "Not A Rom.nes");
        File.WriteAllText(bogus, "rom");

        Assert.Null(HeaderlessNesHash.Of(bogus));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static byte[] Header(byte flags6)
    {
        var header = new byte[16];
        "NES"u8.CopyTo(header);
        header[6] = flags6;
        return header;
    }

#pragma warning disable CA5351 // The name mednafen gives the file.
    private static string Md5(byte[] bytes) => Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();
#pragma warning restore CA5351
}
