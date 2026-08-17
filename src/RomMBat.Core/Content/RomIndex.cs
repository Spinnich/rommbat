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

    private RomIndex(Dictionary<string, (long, RelativePath)> index) => _byFolderAndStem = index;

    /// <summary>Builds the index from what is on disk.</summary>
    public static RomIndex Build(LocalStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        var index = new Dictionary<string, (long, RelativePath)>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in store.Files.List())
        {
            if (file.Kind != LocalFileKind.Rom || file.RomId is not { } romId || file.Folder is not { } folder)
            {
                continue;
            }

            // First wins, and within one folder a collision needs two ROMs with the same stem
            // and different extensions, where either is as good an answer as the other.
            index.TryAdd(Key(folder, Path.GetFileNameWithoutExtension(file.FileName)), (romId, file.Path));
        }

        return new RomIndex(index);
    }

    /// <summary>The ROM a name in a folder refers to, or null when that folder holds no such ROM.</summary>
    public (long RomId, RelativePath Path)? Find(string folder, string stem) =>
        _byFolderAndStem.TryGetValue(Key(folder, stem), out var found) ? found : null;

    private static string Key(string folder, string stem) => $"{folder}/{stem}";
}
