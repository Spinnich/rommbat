using System.IO.Compression;
using System.Security.Cryptography;
using RomMBat.Core.Content;
using Xunit;

namespace RomMBat.Tests;

public sealed class MednafenRomHashTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("rommbat-mednafen-hash-").FullName;

    [Fact]
    public void A_zipped_megadrive_rom_hashes_the_whole_md_inside()
    {
        // Measured on Sonic & Knuckles + Sonic 3: the .md's own md5 is the one on mednafen's name.
        var body = Body("SEGA MEGA DRIVE");
        var zip = Zip("Game (USA).zip", ("Game (USA).md", body));

        Assert.Equal(Md5(body), MednafenRomHash.Of(zip, "megadrive"));
    }

    [Fact]
    public void A_bare_md_file_hashes_the_same_way()
    {
        var body = Body("SEGA GENESIS");
        var md = Path.Combine(_root, "Game (USA).md");
        File.WriteAllBytes(md, body);

        Assert.Equal(Md5(body), MednafenRomHash.Of(md, "megadrive"));
    }

    [Theory]
    [InlineData("Game (USA).smd")]
    [InlineData("Game (USA).bin")]
    [InlineData("Game (USA).gen")]
    public void A_megadrive_format_nobody_measured_answers_null(string entry)
    {
        // An .smd is interleaved and decoded before mednafen hashes it; .bin and .gen are undriven.
        Assert.Null(MednafenRomHash.Of(Zip("Game (USA).zip", (entry, Body("x"))), "megadrive"));
    }

    [Fact]
    public void A_megadrive_zip_holding_two_files_answers_null()
    {
        var zip = Zip("Game (USA).zip", ("Game (USA).md", Body("a")), ("Patch.bin", Body("b")));

        Assert.Null(MednafenRomHash.Of(zip, "megadrive"));
    }

    [Fact]
    public void Nes_keeps_the_headerless_hash_and_other_systems_answer_null()
    {
        var body = new byte[16384];
        var nes = Path.Combine(_root, "Game (USA).nes");
        File.WriteAllBytes(nes, [.. "NES"u8, 1, 0, .. new byte[10], .. body]);

        Assert.Equal(HeaderlessNesHash.Of(nes), MednafenRomHash.Of(nes, "nes"));
        Assert.Equal(Md5(body), MednafenRomHash.Of(nes, "nes"));
        Assert.Null(MednafenRomHash.Of(nes, "pcengine"));
    }

    [Fact]
    public void A_zipped_snes_rom_hashes_the_whole_sfc_inside()
    {
        // Measured on Legend of Zelda, The - A Link to the Past (USA): the .sfc's own md5 is the
        // one on mednafen's .srm and on its states.
        var body = Body("THE LEGEND OF ZELDA");
        var zip = Zip("Game (USA).zip", ("Game (USA).sfc", body));

        Assert.Equal(Md5(body), MednafenRomHash.Of(zip, "snes"));
    }

    [Fact]
    public void An_smc_answers_null_because_it_can_carry_a_copier_header_nobody_measured()
    {
        Assert.Null(MednafenRomHash.Of(Zip("Game (USA).zip", ("Game (USA).smc", Body("x"))), "snes"));
    }

    [Fact]
    public void A_zipped_gba_rom_hashes_the_whole_gba_and_names_its_member()
    {
        // Measured on Pokemon - Emerald Version (USA, Europe): the .gba's own md5 is on both
        // mednafen's name and mednafen_gba's, which carries the member's stem after a '#'.
        var body = Body("POKEMON EMER");
        var zip = Zip("Game (USA).zip", ("Game (USA, Europe).gba", body));

        Assert.Equal(Md5(body), MednafenRomHash.Of(zip, "gba"));
        Assert.Equal(("Game (USA, Europe)", Md5(body)), MednafenRomHash.ArchiveMemberOf(zip, "gba"));
    }

    [Fact]
    public void An_archive_member_is_named_only_for_a_zipped_gba_rom()
    {
        var body = Body("POKEMON EMER");
        var gba = Path.Combine(_root, "Game (USA).gba");
        File.WriteAllBytes(gba, body);

        // A bare .gba's save is mednafen standalone's hashed name, not this slot's.
        Assert.Equal(Md5(body), MednafenRomHash.Of(gba, "gba"));
        Assert.Null(MednafenRomHash.ArchiveMemberOf(gba, "gba"));
        Assert.Null(MednafenRomHash.ArchiveMemberOf(Zip("Game (USA).zip", ("Game (USA).md", body)), "megadrive"));
        Assert.Null(MednafenRomHash.ArchiveMemberOf(Zip("Two.zip", ("A.gba", body), ("B.gba", body)), "gba"));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Zip(string name, params (string Entry, byte[] Body)[] entries)
    {
        var path = Path.Combine(_root, name);

        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);

        foreach (var (entry, body) in entries)
        {
            using var stream = archive.CreateEntry(entry).Open();
            stream.Write(body);
        }

        return path;
    }

    private static byte[] Body(string seed)
    {
        var body = new byte[4096];
        System.Text.Encoding.ASCII.GetBytes(seed).CopyTo(body, 0x100);
        return body;
    }

#pragma warning disable CA5351 // The name mednafen gives the file.
    private static string Md5(byte[] bytes) => Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();
#pragma warning restore CA5351
}
