using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RomMBat.Core.Paths;
using RomMBat.Core.Store;

namespace RomMBat.Core.Content;

/// <summary>Paths and restart recovery for loose discs published as one directory.</summary>
internal static class FlatDiscLayout
{
    private const string Suffix = " [RomMBat discs]";
    private const string ManifestName = ".rommbat-publication.json";

    public static RelativePath? FolderFor(string? system, string name) =>
        !string.IsNullOrWhiteSpace(system)
        && DiscSet.Parse(name) is { BaseTitle.Length: > 0 } marker
        && RelativePath.TryCreate($"roms/{system}/{marker.BaseTitle}{Suffix}", out var folder)
            ? folder : null;

    public static IEnumerable<RelativePath> ExistingLooseFolders(RetroBatInstall install, string system)
    {
        var root = install.Resolve(RelativePath.Create($"roms/{system}"));
        if (!Directory.Exists(root))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFileSystemEntries(root))
        {
            if (!Directory.Exists(path) && FolderFor(system, Path.GetFileName(path)) is { } group)
            {
                yield return group;
            }
        }
    }

    public static RelativePath StageFor(RelativePath folder)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(folder.Value.ToUpperInvariant())));
        return RetroBatInstall.PartialDirectory.Combine($"discs-{key}");
    }

    public static string ManifestPath(string directory) => Path.Combine(directory, ManifestName);

    internal sealed record Receipt(string FileHash, string? LogicalHash, HashScope Scope, VerifiedBy VerifiedBy);

    public static void WriteManifest(string stage, IReadOnlyDictionary<int, Receipt> receipts) =>
        File.WriteAllText(ManifestPath(stage), JsonSerializer.Serialize(receipts));

    public static Receipt CreateReceipt(string stagedPath, LocalFile record) => new(
        record.HashScope == HashScope.File && record.Md5Hash is { } hash
            ? hash : ContentHasher.ComputeFileMd5(stagedPath),
        record.Md5Hash, record.HashScope, record.VerifiedBy);

    public static void RemoveManifest(string directory)
    {
        try
        {
            File.Delete(ManifestPath(directory));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The index is already durable. The manifest can be retired next time.
        }
    }

    // Called under the library lease before planning, so the free-space check treats
    // files moved back from a stopped publication exactly like other verified partials.
    public static void Recover(RetroBatInstall install, LocalStore store)
    {
        var groups = store.Downloads.List().Where(download => download.FileId == 0)
            .Where(download => download.PartPath == ContentPlanner.PartFor(download.RomId)
                && FolderOfTarget(download.TargetPath) is not null)
            .GroupBy(download => FolderOfTarget(download.TargetPath)!.Value);

        foreach (var group in groups)
        {
            var stage = install.Resolve(StageFor(group.Key));
            var published = install.Resolve(group.Key);
            try
            {
                if (Directory.Exists(stage))
                {
                    foreach (var download in group)
                    {
                        var staged = Path.Combine(stage, download.TargetPath.Name);
                        var partial = install.Resolve(download.PartPath);
                        if (File.Exists(staged) && !Path.Exists(partial))
                        {
                            File.Move(staged, partial, overwrite: false);
                        }
                    }

                    RemoveManifest(stage);
                    if (!Directory.EnumerateFileSystemEntries(stage).Any())
                    {
                        Directory.Delete(stage);
                    }
                }
                else if (File.Exists(ManifestPath(published)))
                {
                    ReconcilePublished(install, store, published, group);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                // A locked or changed file is left alone. The ordinary content plan can
                // report that path, and must never adopt it as a verified publication.
            }
        }
    }

    private static RelativePath? FolderOfTarget(RelativePath path)
    {
        var parts = path.Value.Split('/');
        if (parts is ["roms", var system, _, var file]
            && FolderFor(system, file) is { } folder && folder.Combine(file) == path)
        {
            return folder;
        }

        return null;
    }

    private static void ReconcilePublished(RetroBatInstall install, LocalStore store, string directory,
        IEnumerable<ContentDownload> downloads)
    {
        var receipts = JsonSerializer.Deserialize<Dictionary<int, Receipt>>(File.ReadAllText(ManifestPath(directory)));
        if (receipts is null)
        {
            return;
        }

        var transfers = downloads.ToArray();
        if (receipts.Count != transfers.Length)
        {
            return;
        }

        var records = new List<LocalFile>();
        foreach (var download in transfers)
        {
            var path = download.TargetPath;
            var absolute = install.Resolve(path);
            if (!receipts.TryGetValue(download.RomId, out var receipt) || receipt is null || !File.Exists(absolute))
            {
                return;
            }

            var file = new FileInfo(absolute);
            if ((download.ExpectedSize is > 0 && file.Length != download.ExpectedSize)
                || !ContentHasher.Matches(ContentHasher.ComputeFileMd5(absolute), receipt.FileHash))
            {
                return;
            }

            records.Add(new LocalFile
            {
                Path = path, RomId = download.RomId, Folder = path.Value.Split('/')[1], Kind = LocalFileKind.Rom,
                FileName = path.Name, SizeBytes = file.Length, ModifiedUtc = file.LastWriteTimeUtc,
                Md5Hash = receipt.LogicalHash, HashScope = receipt.Scope, VerifiedBy = receipt.VerifiedBy,
                VerifiedAt = download.UpdatedAt, Origin = FileOrigin.Synced,
            });
        }

        store.InTransaction(() =>
        {
            foreach (var record in records)
            {
                store.Files.Record(record);
                store.Downloads.Remove(record.RomId!.Value);
            }
        });
        RemoveManifest(directory);
    }
}
