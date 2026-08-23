using RomMBat.Core.Paths;
using RomMBat.Core.Store;

namespace RomMBat.Core.Content;

/// <summary>Which ROM a file named after one belongs to.</summary>
/// <remarks>
/// <b>The folder is half the key, not decoration.</b> Contra, Aladdin, Tetris and Batman all
/// exist on several systems, which is the ordinary state of a multi-system library, and a
/// stem-only index gives <c>saves/snes/Contra.srm</c> to the NES ROM. That mis-attributes the
/// save on upload and puts two rows on one <c>(rom_id, slot)</c>.
/// <para>
/// A file whose own folder holds no ROM of that name is left unattributed rather than falling
/// back to a match in some other system. Guessing across systems is the failure this key exists
/// to prevent, and it was a review finding on M6 stage 1 rather than a hypothetical.
/// </para>
/// <para>
/// Shared by battery-save discovery and save-state discovery because both attribute the same
/// way: the file is named after the ROM file, inside its system's folder. Two copies of this
/// rule would be two chances for one of them to be relaxed.
/// </para>
/// </remarks>
public sealed class RomIndex
{
    private readonly Dictionary<string, (long RomId, RelativePath Path)> _byFolderAndStem;
    private readonly Dictionary<string, List<(long RomId, RelativePath Path)>> _byFolder;

    private RomIndex(
        Dictionary<string, (long, RelativePath)> index,
        Dictionary<string, List<(long RomId, RelativePath Path)>> byFolder)
    {
        _byFolderAndStem = index;
        _byFolder = byFolder;
    }

    /// <summary>Builds the index from what is on disk.</summary>
    /// <remarks>
    /// Two dictionaries off one pass, because the two lookups ask different questions.
    /// <c>(folder, stem)</c> answers "which ROM is this file named after" in one hit, and folder
    /// alone answers "every ROM in this system", which the Game ID route needs and which a
    /// prefix scan of the first dictionary used to cost O(systems x total ROMs) per scan.
    /// </remarks>
    public static RomIndex Build(LocalStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        var index = new Dictionary<string, (long, RelativePath)>(StringComparer.OrdinalIgnoreCase);
        var byFolder = new Dictionary<string, List<(long RomId, RelativePath Path)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in store.Files.List())
        {
            if (file.Kind != LocalFileKind.Rom || file.RomId is not { } romId || file.Folder is not { } folder)
            {
                continue;
            }

            // First wins, and within one folder a collision needs two ROMs with the same stem
            // and different extensions, where either is as good an answer as the other.
            if (!index.TryAdd(Key(folder, Path.GetFileNameWithoutExtension(file.FileName)), (romId, file.Path)))
            {
                // Kept out of the folder list too, so the two lookups answer over one set of
                // ROMs rather than the reverse lookup seeing rows the forward one dropped.
                continue;
            }

            if (!byFolder.TryGetValue(folder, out var inFolder))
            {
                inFolder = [];
                byFolder[folder] = inFolder;
            }

            inFolder.Add((romId, file.Path));
        }

        foreach (var inFolder in byFolder.Values)
        {
            // Sorted once at build rather than on every call, since InFolder's order is what
            // makes a Game ID binding the same one twice over an unchanged tree.
            inFolder.Sort((left, right) =>
                string.CompareOrdinal(left.Path.Value, right.Path.Value));
        }

        return new RomIndex(index, byFolder);
    }

    /// <summary>The ROM a name in a folder refers to, or null when that folder holds no such ROM.</summary>
    public (long RomId, RelativePath Path)? Find(string folder, string stem) =>
        _byFolderAndStem.TryGetValue(Key(folder, stem), out var found) ? found : null;

    /// <summary>
    /// Every ROM in one folder, which is what a reverse lookup has to walk.
    /// </summary>
    /// <remarks>
    /// The Game ID route asks "which ROM carries this code", and a code cannot be turned back
    /// into a filename, so the only way round is to read every ROM in the system once and index
    /// what comes out. That is 178 files of 256 bytes on the largest measured system.
    /// <para>
    /// A dictionary hit rather than a prefix scan of the whole index. This is called once per
    /// system from <c>GameIdAttributor</c>, so the scan cost O(systems x total ROMs) per pass
    /// rather than O(total ROMs), which is fine at every size measured and not obviously fine
    /// at a hundred thousand ROMs.
    /// </para>
    /// </remarks>
    public IEnumerable<(long RomId, RelativePath Path)> InFolder(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        return _byFolder.TryGetValue(folder, out var inFolder) ? inFolder : [];
    }

    private static string Key(string folder, string stem) => $"{folder}/{stem}";
}
