using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;

namespace RomMBat.Core.Content;

/// <summary>
/// One class C save unit: everything under a container that carries one key.
/// </summary>
/// <remarks>
/// <b>A unit is a (container, key) pair rather than a directory</b>, because on a real install
/// it is routinely neither one directory nor one file. <c>ps3</c> keeps
/// <c>BLUS30109G6A383E91</c>, <c>BLUS30109G6A3B071C</c> and <c>BLUS30109S</c> for one title id;
/// <c>psp</c> keeps <c>UCES01011</c> beside <c>ULES01513SYSDATA</c>; and <c>gamecube</c> keeps
/// <c>69-GXBE-game1.ssx.gci</c> and <c>69-GXBE-settings.ssx.gci</c> as two files in a folder
/// shared with every other game, so no directory exists that could be the unit at all.
/// </remarks>
/// <param name="Container">
/// The declared container with any <c>*</c> already expanded against the real tree, so this is
/// a path that exists. Stored relative to the RetroBat root and never rooted.
/// </param>
/// <param name="Key">
/// What names the unit inside its container: a title id, a MAME short name, a GameCube game
/// code, or a Wii NAND code. For <c>mame</c> it is also the ROM basename, which is why that
/// system needs no Game-ID attribution at all.
/// </param>
/// <param name="Files">
/// Every file the unit owns, with the path each will carry inside the archive, which is
/// relative to <see cref="Container"/>. Sorted ordinally, so the hash and the archive agree.
/// </param>
public sealed record SaveUnit(
    string System,
    RelativePath Container,
    string Key,
    string Emulator,
    string Slot,
    IReadOnlyList<SaveUnitFile> Files)
{
    /// <summary>Bytes across the whole unit.</summary>
    public long SizeBytes => Files.Sum(file => file.SizeBytes);

    /// <summary>The newest member's mtime, which is what goes on the wire as `updated_at`.</summary>
    /// <remarks>
    /// The newest rather than the container's own, because a directory's mtime moves for
    /// reasons that have nothing to do with a save and does not move for a rewrite in place.
    /// It only ever breaks ties: the content hash decides whether anything changed, since
    /// exFAT and FAT32 both quantise to 2 seconds and round up.
    /// </remarks>
    public DateTimeOffset? NewestMtimeUtc => Files.Count == 0 ? null : Files.Max(file => file.MtimeUtc);

    /// <summary>The name a bundled unit is uploaded under.</summary>
    /// <remarks>
    /// The key plus <c>.zip</c>, so the untagged name the server hands back is the key itself.
    /// Measured: <c>UCES01011.zip</c> came back as <c>UCES01011 [2026-08-17_23-52-18].zip</c>
    /// with <c>file_name_no_tags</c> of <c>UCES01011</c>. Nothing else about this name is load
    /// bearing, since the negotiate match is on the slot rather than the name.
    /// </remarks>
    public string UploadFileName => $"{Key}.zip";
}

/// <summary>One file inside a save unit.</summary>
/// <param name="ArchivePath">
/// Where it sits relative to the unit's container, forward-slashed. This is both the archive
/// entry name and what the logical content hash folds, so the two cannot disagree.
/// </param>
public sealed record SaveUnitFile(
    RelativePath Path,
    string ArchivePath,
    long SizeBytes,
    DateTimeOffset MtimeUtc);
