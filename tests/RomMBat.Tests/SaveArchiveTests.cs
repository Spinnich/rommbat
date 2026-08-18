using System.IO.Compression;
using RomMBat.Core.Content;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Bundling a save unit, putting one back, and the identity that travels beside it.
/// </summary>
/// <remarks>
/// The archive is transport and the hash is identity. These tests keep the two apart, because
/// the failure that follows from confusing them is silent: RomMBat and Grout would disagree
/// forever about identical saves, and every sync of an unchanged directory would create a new
/// server row.
/// </remarks>
public class SaveArchiveTests
{
    [Fact]
    public void The_logical_hash_is_stable_across_two_runs_and_blind_to_how_the_archive_was_built()
    {
        // The two properties that make a replayed flush idempotent, asserted separately because
        // they fail for different reasons: run-to-run instability would come from enumeration
        // order, and archive sensitivity would come from folding the wrong bytes.
        var members = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["UCES01011/PARAM.SFO"] = "the parameters"u8.ToArray(),
            ["UCES01011/DATA.BIN"] = "the save itself"u8.ToArray(),
            ["UCES01011SYSDATA/SYSDATA.BIN"] = "system data"u8.ToArray(),
        };

        var first = Fold(members);
        var second = Fold(members);

        Assert.Equal(first, second);

        // Two archives of identical logical contents, built the way two implementations would.
        // Go's archive/zip and .NET's ZipArchive differ on exactly these three things.
        var one = Zip(members, CompressionLevel.Optimal, new DateTime(1980, 1, 1), reverse: false);
        var two = Zip(members, CompressionLevel.SmallestSize, new DateTime(2020, 6, 15), reverse: true);

        Assert.NotEqual(Convert.ToHexString(one), Convert.ToHexString(two));
        Assert.Equal(first, second);
    }

    [Fact]
    public void The_hash_moves_when_a_member_is_renamed_and_when_its_contents_change()
    {
        // Both halves matter. Contents alone would miss a game reorganising its savedata, and
        // names alone would miss the ordinary case of a save being written.
        var members = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["UCES01011/DATA.BIN"] = "one"u8.ToArray(),
        };

        var original = Fold(members);

        var renamed = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["UCES01011/OTHER.BIN"] = "one"u8.ToArray(),
        };

        var changed = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["UCES01011/DATA.BIN"] = "two"u8.ToArray(),
        };

        Assert.NotEqual(original, Fold(renamed));
        Assert.NotEqual(original, Fold(changed));
    }

    [Fact]
    public void A_unit_round_trips_through_pack_and_extract_unchanged()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        Write(tree, "saves/psp/SAVEDATA/UCES01011/PARAM.SFO", "the parameters");
        Write(tree, "saves/psp/SAVEDATA/UCES01011/DATA.BIN", "the save itself");
        Write(tree, "saves/psp/SAVEDATA/UCES01011SYSDATA/SYSDATA.BIN", "system data");

        var unit = Assert.Single(new SaveUnitScanner(install).Scan("psp"));
        var before = SaveArchive.HashOf(install, unit);

        using var buffer = new MemoryStream();
        SaveArchive.Pack(install, unit, buffer);
        buffer.Position = 0;

        var destination = Path.Combine(tree.Root, "extracted");
        var entries = SaveArchive.Extract(buffer, destination);

        Assert.Equal(
            ["UCES01011/DATA.BIN", "UCES01011/PARAM.SFO", "UCES01011SYSDATA/SYSDATA.BIN"],
            entries.Order(StringComparer.Ordinal));

        // The same fold over what came out as over what went in, which is the one comparison
        // the client can make on both sides of a restore.
        Assert.Equal(before, SaveArchive.HashOfExtracted(destination, entries));
    }

    [Fact]
    public void Packing_the_same_unchanged_unit_twice_produces_identical_bytes()
    {
        // Nothing compares these bytes, since RomM hashes an archive's contents rather than its
        // framing. It is still worth holding: a replayed flush then sends the same thing twice
        // instead of something new twice, which is what keeps a partial failure cheap.
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        Write(tree, "saves/mame/nvram/25pacman/eeprom", "one");
        Write(tree, "saves/mame/nvram/25pacman/flash", "two");

        var unit = Assert.Single(new SaveUnitScanner(install).Scan("mame"));

        using var first = new MemoryStream();
        using var second = new MemoryStream();

        SaveArchive.Pack(install, unit, first);
        SaveArchive.Pack(install, unit, second);

        Assert.Equal(Convert.ToHexString(first.ToArray()), Convert.ToHexString(second.ToArray()));
    }

    [Theory]
    [InlineData("../../../roms/snes/something.sfc")]
    [InlineData("..\\..\\evil.bin")]
    [InlineData("/etc/passwd")]
    [InlineData("C:/Windows/System32/evil.dll")]
    public void An_archive_entry_that_would_escape_its_destination_is_refused(string name)
    {
        // The entry names come off the wire, so they are untrusted. Refusing fails the whole
        // extraction rather than skipping the entry, because a partially-extracted save is not
        // a save, and nothing has touched the live tree by this point.
        using var tree = TempRetroBatTree.Create();

        using var buffer = new MemoryStream();

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(name);
            using var writer = entry.Open();
            writer.Write("payload"u8);
        }

        buffer.Position = 0;
        var destination = Path.Combine(tree.Root, "extracted");

        Assert.Throws<InvalidDataException>(() => SaveArchive.Extract(buffer, destination));
    }

    [Fact]
    public void A_corrupt_archive_fails_extraction_rather_than_landing_half_a_save()
    {
        // This is the check that replaces the byte hash for class C. RomM's archive digest is
        // computed over the contents by a function this client cannot reproduce, so the CRC that
        // extraction validates is what stands between a truncated download and a corrupt save.
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        Write(tree, "saves/mame/nvram/25pacman/eeprom", "a save with enough bytes to matter");

        var unit = Assert.Single(new SaveUnitScanner(install).Scan("mame"));

        using var buffer = new MemoryStream();
        SaveArchive.Pack(install, unit, buffer);

        var bytes = buffer.ToArray();

        // Corrupt the compressed payload, leaving the structure intact so the failure comes from
        // the CRC rather than from the archive being unreadable.
        bytes[^40] ^= 0xFF;

        using var corrupted = new MemoryStream(bytes);
        var destination = Path.Combine(tree.Root, "extracted");

        Assert.ThrowsAny<InvalidDataException>(() => SaveArchive.Extract(corrupted, destination));
    }

    private static string Fold(Dictionary<string, byte[]> members) =>
#pragma warning disable CA5351 // MD5, deliberately: RomM's content_hash is 32 characters.
        LogicalContentHash.Fold(members.Select(member => (
            member.Key,
            Convert.ToHexStringLower(System.Security.Cryptography.MD5.HashData(member.Value)))));
#pragma warning restore CA5351

    private static byte[] Zip(
        Dictionary<string, byte[]> members,
        CompressionLevel level,
        DateTime stamp,
        bool reverse)
    {
        using var buffer = new MemoryStream();

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            var names = members.Keys.Order(StringComparer.Ordinal).ToList();

            if (reverse)
            {
                names.Reverse();
            }

            foreach (var name in names)
            {
                var entry = archive.CreateEntry(name, level);
                entry.LastWriteTime = stamp;

                using var writer = entry.Open();
                writer.Write(members[name]);
            }
        }

        return buffer.ToArray();
    }

    private static void Write(TempRetroBatTree tree, string relativePath, string content)
    {
        var absolute = Path.Combine(tree.Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllText(absolute, content);
    }
}
