using System.Globalization;
using System.Text.Json;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;

namespace RomMBat.Core.Content;

/// <summary>Restart recovery for the atomic publication of one ROM's multi-file package.</summary>
internal static class PackedRomStage
{
    private const string Prefix = "pack-";

    public static RelativePath For(int romId) =>
        RetroBatInstall.PartialDirectory.Combine(Prefix + romId.ToString(CultureInfo.InvariantCulture));

    public static bool TryGetRomId(string name, out int romId)
    {
        romId = 0;
        return name.StartsWith(Prefix, StringComparison.Ordinal)
            && int.TryParse(name.AsSpan(Prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out romId)
            && romId > 0 && For(romId).Name == name;
    }

    internal sealed record Entry(int FileId, string Name, FlatDiscLayout.Receipt Receipt);

    public static void WriteManifest(string stage, IEnumerable<Entry> entries)
    {
        using var output = new FileStream(FlatDiscLayout.ManifestPath(stage), FileMode.CreateNew);
        JsonSerializer.Serialize(output, entries.ToArray());
    }

    // Both callers (startup and explicit sync) hold the library lease. Restore the usual
    // partials before planning so the transfer's Range and free-space accounting use them.
    public static void Recover(RetroBatInstall install, LocalStore store)
    {
        foreach (var group in store.Downloads.List()
            .Where(row => row.FileId > 0 && row.PartPath == ContentPlanner.PartFor(row.RomId, row.FileId))
            .GroupBy(row => row.RomId))
        {
            var rows = group.ToArray();
            var parts = rows[0].TargetPath.Value.Split('/');
            if (parts is not ["roms", _, _, _]
                || !RelativePath.TryCreate(string.Join('/', parts.Take(3)), out var folder)
                || rows.Any(row => row.TargetPath != folder.Combine(row.TargetPath.Name)))
            {
                continue;
            }

            var stage = install.Resolve(For(group.Key));
            var absolute = install.Resolve(folder);
            var playlist = MultiFileLayout.PlaylistNameFor(folder.Name);
            try
            {
                if (Directory.Exists(stage))
                {
                    RestorePartials(install, rows, stage, playlist);
                }
                else if (File.Exists(FlatDiscLayout.ManifestPath(absolute)))
                {
                    ReconcilePublished(install, store, rows, folder, playlist);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                // Leave obstructed files and any unknown content alone. The planner reports an
                // occupied stage; an already published package is left in its original folder.
            }
        }
    }

    public static long ResumableBytes(RetroBatInstall install, LocalStore store, int romId,
        RelativePath folder, IReadOnlyList<LocalFile> onDisk)
    {
        var installed = onDisk.Select(row => row.Path).ToHashSet();
        return store.Downloads.ForRom(romId).Where(row => row.FileId > 0
                && row.PartPath == ContentPlanner.PartFor(romId, row.FileId)
                && row.TargetPath == folder.Combine(row.TargetPath.Name) && !installed.Contains(row.TargetPath))
            .Sum(row =>
            {
                var part = new FileInfo(install.Resolve(row.PartPath));
                return part.Exists && row.ExpectedSize is > 0 and var size && part.Length <= size ? part.Length : 0;
            });
    }

    private static void RestorePartials(RetroBatInstall install, ContentDownload[] rows, string stage, string playlist)
    {
        if (File.GetAttributes(stage).HasFlag(FileAttributes.ReparsePoint))
        {
            return;
        }

        foreach (var row in rows)
        {
            var staged = Path.Combine(stage, row.TargetPath.Name);
            if (File.Exists(staged) && !File.GetAttributes(staged).HasFlag(FileAttributes.ReparsePoint))
            {
                File.Move(staged, install.Resolve(row.PartPath), overwrite: false);
            }
        }

        // The generated playlist has no download row. Rebuild it after verification.
        if (!rows.Any(row => string.Equals(row.TargetPath.Name, playlist, StringComparison.OrdinalIgnoreCase)))
        {
            File.Delete(Path.Combine(stage, playlist));
        }

        FlatDiscLayout.RemoveManifest(stage);
        if (!Directory.EnumerateFileSystemEntries(stage).Any())
        {
            Directory.Delete(stage);
        }
    }

    private static void ReconcilePublished(
        RetroBatInstall install, LocalStore store, ContentDownload[] rows, RelativePath folder, string playlist)
    {
        var directory = install.Resolve(folder);
        if (File.GetAttributes(directory).HasFlag(FileAttributes.ReparsePoint))
        {
            return;
        }

        var manifest = JsonSerializer.Deserialize<Entry[]>(File.ReadAllText(FlatDiscLayout.ManifestPath(directory)));
        if (manifest is null || manifest.Length is 0
            || manifest.Any(entry => entry is null || entry.Receipt is null)
            || manifest.Select(entry => entry.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.Length
            || !manifest.Any(entry => string.Equals(entry.Name, playlist, StringComparison.OrdinalIgnoreCase))
            || !rows.Select(row => row.FileId).ToHashSet().SetEquals(
                manifest.Where(entry => entry.FileId > 0).Select(entry => entry.FileId)))
        {
            return;
        }

        var records = new List<LocalFile>(manifest.Length);
        foreach (var entry in manifest)
        {
            var isPlaylist = string.Equals(entry.Name, playlist, StringComparison.OrdinalIgnoreCase);
            var transfer = rows.FirstOrDefault(row => row.FileId == entry.FileId && row.TargetPath.Name == entry.Name);
            if (transfer is null && (entry.FileId != 0 || !isPlaylist))
            {
                return;
            }

            var path = folder.Combine(entry.Name);
            var info = new FileInfo(install.Resolve(path));
            if (store.Files.Find(path) is not null || !info.Exists || info.Attributes.HasFlag(FileAttributes.ReparsePoint)
                || (transfer?.ExpectedSize is > 0 and var size && info.Length != size)
                || !ContentHasher.Matches(ContentHasher.ComputeFileMd5(info.FullName), entry.Receipt.FileHash))
            {
                return;
            }

            records.Add(new LocalFile
            {
                Path = path, RomId = rows[0].RomId, Folder = folder.Value.Split('/')[1], FileName = entry.Name,
                Kind = isPlaylist ? LocalFileKind.Rom : LocalFileKind.RomPart,
                Origin = FileOrigin.Synced, SizeBytes = info.Length, ModifiedUtc = info.LastWriteTimeUtc,
                Md5Hash = entry.Receipt.LogicalHash ?? entry.Receipt.FileHash, HashScope = entry.Receipt.Scope,
                VerifiedBy = entry.Receipt.VerifiedBy, VerifiedAt = transfer?.UpdatedAt ?? rows[0].UpdatedAt,
            });
        }

        store.InTransaction(() =>
        {
            foreach (var record in records)
            {
                store.Files.Record(record);
            }

            foreach (var row in rows)
            {
                store.Downloads.Remove(row.RomId, row.FileId);
            }
        });
        FlatDiscLayout.RemoveManifest(directory);
    }
}
