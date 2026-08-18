using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;

namespace RomMBat.Core.Content;

/// <summary>
/// Finds class C save units by expanding the declared containers against the real tree.
/// </summary>
/// <remarks>
/// <b>Scoping is the whole problem, and it is measured rather than argued.</b> A logical
/// content hash over <c>saves/ps3/rpcs3</c> takes 426.07 s across 32,451 files and 52.87 GB,
/// because that is the emulator's entire data root with its installed games, firmware and
/// caches. The savedata subtree a save unit really lives in is 77 files, 16.3 MB and 0.06 s.
/// So <b>a shape that names an emulator's data root is the bug</b>, and this class never
/// discovers a container: it only expands one that was declared.
/// <para>
/// <b>Anything no container names is not read.</b> That is the same fail-closed rule
/// <see cref="SaveScanner"/> applies to the loose level, applied to the deep one, and it
/// matters more here because the cost of guessing is reading somebody's entire ROM library
/// off a slow disk and calling it a save.
/// </para>
/// </remarks>
public sealed class SaveUnitScanner
{
    private readonly RetroBatInstall _install;
    private readonly SaveShapes _shapes;

    public SaveUnitScanner(RetroBatInstall install, SaveShapes? shapes = null)
    {
        ArgumentNullException.ThrowIfNull(install);

        _install = install;
        _shapes = shapes ?? SaveShapes.Bundled;
    }

    /// <summary>Every unit under one system, or none when the system declares no container.</summary>
    public IReadOnlyList<SaveUnit> Scan(string system)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(system);

        var shape = _shapes.For(system);

        if (shape is null || !shape.HasUnitPaths)
        {
            return [];
        }

        var units = new List<SaveUnit>();

        foreach (var declared in shape.UnitPaths)
        {
            foreach (var container in Expand(declared))
            {
                units.AddRange(UnitsIn(system, declared, container));
            }
        }

        // Ordinal by container then key, so two runs over one tree produce one order and a
        // report reads the same twice.
        return [.. units.OrderBy(unit => unit.Container.Value, StringComparer.Ordinal)
            .ThenBy(unit => unit.Key, StringComparer.Ordinal)];
    }

    /// <summary>Every declared container that exists, with each <c>*</c> resolved.</summary>
    /// <remarks>
    /// One <c>*</c> matches exactly one directory. RPCS3's is a user id (<c>00000001</c>) and
    /// Dolphin's is a region (<c>USA</c>), and neither is worth naming in the shape file: what
    /// matters is that the segments around it are named, so the expansion cannot wander.
    /// </remarks>
    private List<RelativePath> Expand(SaveUnitPath declared)
    {
        var roots = new List<RelativePath> { SaveScanner.SavesDirectory };

        foreach (var segment in declared.Segments)
        {
            var next = new List<RelativePath>();

            foreach (var root in roots)
            {
                var absolute = _install.Resolve(root);

                if (!Directory.Exists(absolute))
                {
                    continue;
                }

                if (segment == "*")
                {
                    foreach (var child in SafeDirectories(absolute))
                    {
                        next.Add(root.Combine(Path.GetFileName(child)));
                    }

                    continue;
                }

                // Named rather than globbed, but matched case-insensitively, because a real
                // install carries SAVEDATA, PPSSPP_STATE and dolphin-emu side by side and
                // Windows does not care which case the shape file recorded.
                var match = SafeDirectories(absolute)
                    .FirstOrDefault(child => string.Equals(
                        Path.GetFileName(child),
                        segment,
                        StringComparison.OrdinalIgnoreCase));

                if (match is not null)
                {
                    next.Add(root.Combine(Path.GetFileName(match)));
                }
            }

            roots = next;
        }

        return roots;
    }

    private IEnumerable<SaveUnit> UnitsIn(string system, SaveUnitPath declared, RelativePath container)
    {
        var absolute = _install.Resolve(container);

        // Members are grouped rather than taken one at a time, which is the whole point: one
        // key can own several directories on ps3 and several files on gamecube.
        var byKey = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        var entries = declared.MembersAreFiles
            ? SafeFiles(absolute)
            : SafeDirectories(absolute);

        foreach (var entry in entries)
        {
            if (declared.KeyOf(Path.GetFileName(entry)) is not { } key)
            {
                // Not part of any unit: a .gci.deleted, a directory with no title-id prefix, a
                // NAND title that is not a printable game code. Left alone rather than reported
                // per file, because the container holds these by design.
                continue;
            }

            if (!byKey.TryGetValue(key, out var members))
            {
                members = [];
                byKey[key] = members;
            }

            members.Add(entry);
        }

        foreach (var (key, members) in byKey)
        {
            var files = new List<SaveUnitFile>();

            foreach (var member in members.Order(StringComparer.Ordinal))
            {
                files.AddRange(FilesOf(declared, absolute, member));
            }

            if (files.Count == 0)
            {
                // A unit that owns nothing is not a save. Wii reaches this for a title holding
                // only content/title.tmd, which is an installed title's metadata rather than
                // anything the game wrote, and uploading an empty archive for it would put a
                // save on the server that never existed.
                continue;
            }

            yield return new SaveUnit(
                system,
                container,
                key,
                declared.Emulator,
                declared.Slot,
                [.. files.OrderBy(file => file.ArchivePath, StringComparer.Ordinal)]);
        }
    }

    /// <summary>The files one member contributes, with the paths they carry in the archive.</summary>
    private IEnumerable<SaveUnitFile> FilesOf(SaveUnitPath declared, string container, string member)
    {
        if (declared.MembersAreFiles)
        {
            return Describe(container, [member]);
        }

        // Wii's unit is the title directory and only its data/ is a save: content/title.tmd is
        // the installed title's metadata, and syncing it would carry a title around rather than
        // a save. Every other system takes the member whole.
        var root = declared.Include is { } include
            ? Path.Combine(member, include.Replace('/', Path.DirectorySeparatorChar))
            : member;

        return Directory.Exists(root) ? Describe(container, SafeTree(root)) : [];
    }

    private IEnumerable<SaveUnitFile> Describe(string container, IEnumerable<string> files)
    {
        foreach (var file in files)
        {
            if (!_install.Contains(file))
            {
                continue;
            }

            FileInfo info;

            try
            {
                info = new FileInfo(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            yield return new SaveUnitFile(
                _install.Relativize(file),
                Path.GetRelativePath(container, file).Replace('\\', '/'),
                info.Length,
                new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero));
        }
    }

    private static IEnumerable<string> SafeDirectories(string path) => Safe(() => Directory.EnumerateDirectories(path));

    private static IEnumerable<string> SafeFiles(string path) => Safe(() => Directory.EnumerateFiles(path));

    private static IEnumerable<string> SafeTree(string path) =>
        Safe(() => Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories));

    /// <summary>
    /// Enumerates, materialised, and answers nothing rather than throwing.
    /// </summary>
    /// <remarks>
    /// Materialised because a lazy enumeration throws partway through the caller's loop, where
    /// the try/catch is not. A tree that cannot be read makes its units invisible, which leaves
    /// them unattributed and unuploaded, which is the direction that loses nothing.
    /// </remarks>
    private static IEnumerable<string> Safe(Func<IEnumerable<string>> enumerate)
    {
        try
        {
            return [.. enumerate().Order(StringComparer.Ordinal)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
