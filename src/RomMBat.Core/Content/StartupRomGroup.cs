using RomMBat.Core.Paths;
using RomMBat.Core.Store;

namespace RomMBat.Core.Content;

/// <summary>Publishes a startup game only after every component has passed verification.</summary>
internal sealed class StartupRomGroup(
    RetroBatInstall install, LocalStore store, IReadOnlyList<ContentStep> steps)
{
    private readonly List<LocalFile> _kept = [];
    private readonly List<PendingFile> _pending = [];

    public bool Deferred { get; private set; }

    public string Defer(string name, string? reason = null)
    {
        Deferred = true;
        return $"existing '{name}' was left in place; review it and run sync with the emulator closed"
            + (reason is null ? "." : $" ({reason}).");
    }

    public void Keep(LocalFile file) => _kept.Add(file);

    public void Add(LocalFile file, string part, int fileId) => _pending.Add(new(file, part, fileId));

    public bool PlaylistIsComplete(RelativePath directory, string name, IEnumerable<string> members)
    {
        var playlist = directory.Combine(name);
        var available = members.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var staged = _pending.Find(file => file.Record.Path == playlist)?.Part;
        var entries = File.ReadLines(staged ?? install.Resolve(playlist))
            .Select(line => line.Trim().Replace('\\', '/'))
            .Where(line => line.Length > 0 && line[0] != '#')
            .Select(line => line.StartsWith("./", StringComparison.Ordinal) ? line[2..] : line)
            .ToList();
        return entries.Count > 0 && entries.All(available.Contains);
    }

    public void AddPlaylist(SyncSetMember member, RelativePath path, byte[] bytes, DateTimeOffset now) =>
        _pending.Add(new(new LocalFile
        {
            Path = path,
            Folder = member.Folder!,
            RomId = member.RomId,
            Kind = LocalFileKind.Rom,
            FileName = path.Name,
            SizeBytes = bytes.Length,
            VerifiedAt = now,
            VerifiedBy = VerifiedBy.Md5,
            Origin = FileOrigin.Synced,
        }, Part: null, FileId: 0, Playlist: bytes));

    public void Publish()
    {
        var targets = new HashSet<RelativePath>();
        foreach (var file in _kept.Select(file => file.Path).Concat(_pending.Select(file => file.Record.Path)))
        {
            if (!targets.Add(file))
            {
                throw new IOException($"more than one group member claims '{file.Name}'.");
            }
        }

        foreach (var file in _kept)
        {
            var info = new FileInfo(install.Resolve(file.Path));
            if (!info.Exists || info.Length != file.SizeBytes || info.LastWriteTimeUtc != file.ModifiedUtc)
            {
                throw new IOException($"'{file.Path.Name}' changed during the transfer.");
            }
        }

        foreach (var file in _pending)
        {
            var target = install.Resolve(file.Record.Path);
            if (Path.Exists(target))
            {
                throw new IOException($"'{file.Record.Path.Name}' appeared during the transfer.");
            }
        }

        // Also recheck discs that the planner found already installed.
        foreach (var step in steps.Where(step => step.Action == ContentAction.AlreadyPresent))
        {
            var info = new FileInfo(install.Resolve(step.TargetPath));
            if (!info.Exists || (step.ExistingSize is { } size && info.Length != size)
                || (step.ExistingModifiedUtc is { } time && info.LastWriteTimeUtc != time))
            {
                throw new IOException($"'{step.TargetPath.Name}' changed during the transfer.");
            }
        }

        var loose = FreshLooseDiscFolder();
        var targetFolder = loose is { } group ? install.Resolve(group)
            : steps is [{ Member.IsMultiFile: true }] && _pending.Count > 0
                ? install.Resolve(ContentPlanner.FolderFor(steps[0].Member)) : null;
        var packed = loose is null && targetFolder is not null && !Path.Exists(targetFolder);
        var stage = loose is { } freshDiscFolder
            ? install.Resolve(FlatDiscLayout.StageFor(freshDiscFolder))
            : packed ? install.Resolve(PackedRomStage.For(steps[0].Member.RomId)) : null;

        var moved = new List<PublishedFile>();
        var folderPublished = false;
        var stagingStarted = false;
        try
        {
            if (stage is not null && (Path.Exists(stage) || Path.Exists(targetFolder!)))
            {
                throw new IOException("The disc group directory is already occupied.");
            }

            if (loose is not null)
            {
                foreach (var file in _pending)
                {
                    var romId = file.Record.RomId!.Value;
                    var download = store.Downloads.Find(romId)
                        ?? throw new IOException("A verified disc has no transfer record.");
                    store.Downloads.Begin(download with { TargetPath = file.Record.Path });
                }
            }

            if (stage is not null)
            {
                Directory.CreateDirectory(stage);
                stagingStarted = true;
            }

            // Fresh groups are populated off-menu. Existing paths remain where the
            // emulator expects them, and startup repairs publish at most one loose disc.
            foreach (var file in _pending.OrderBy(file => file.Record.Kind != LocalFileKind.RomPart))
            {
                var target = stage is null ? install.Resolve(file.Record.Path) : Path.Combine(stage, file.Record.Path.Name);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                var stamp = MoveIntoPlace(file, target, packed);
                moved.Add(new PublishedFile(file, target, stamp));
            }

            if (stage is not null)
            {
                if (loose is not null)
                {
                    FlatDiscLayout.WriteManifest(stage, _pending.ToDictionary(
                        file => file.Record.RomId!.Value,
                        file => FlatDiscLayout.CreateReceipt(Path.Combine(stage, file.Record.Path.Name), file.Record)));
                    if (FlatDiscLayout.ExistingLooseFolders(install, steps[0].Member.Folder!).Contains(loose.Value))
                    {
                        throw new IOException("A loose disc appeared in the system folder during the transfer.");
                    }
                }

                if (packed)
                {
                    PackedRomStage.WriteManifest(stage, _pending.Select(file => new PackedRomStage.Entry(
                        file.FileId, file.Record.Path.Name,
                        FlatDiscLayout.CreateReceipt(Path.Combine(stage, file.Record.Path.Name), file.Record))));
                }

                Directory.CreateDirectory(Path.GetDirectoryName(targetFolder!)!);
                Directory.Move(stage, targetFolder!);
                folderPublished = true;
            }

            foreach (var file in moved)
            {
                var path = folderPublished ? install.Resolve(file.Pending.Record.Path) : file.Path;
                if (ReadStamp(path) != file.Stamp)
                {
                    throw new IOException($"'{file.Pending.Record.Path.Name}' changed before indexing.");
                }
            }

            Record(_pending);
            if (stage is not null)
            {
                FlatDiscLayout.RemoveManifest(targetFolder!);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        {
            if (folderPublished)
            {
                try
                {
                    var owned = moved.Select(file => install.Resolve(file.Pending.Record.Path))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    if (stage is not null)
                    {
                        owned.Add(FlatDiscLayout.ManifestPath(targetFolder!));
                    }

                    if (!owned.SetEquals(Directory.EnumerateFileSystemEntries(targetFolder!))
                        || moved.Any(file => ReadStamp(install.Resolve(file.Pending.Record.Path)) != file.Stamp))
                    {
                        throw new IOException("The published directory changed before rollback.");
                    }

                    // Undo public visibility atomically before restoring individual partials.
                    Directory.Move(targetFolder!, stage!);
                }
                catch (Exception rollback) when (rollback is IOException or UnauthorizedAccessException)
                {
                    throw new IOException($"{ex.Message} Group rollback needs attention: {rollback.Message}", ex);
                }
            }

            var problems = new List<string>();
            foreach (var file in moved.AsEnumerable().Reverse())
            {
                var path = file.Path;
                try
                {
                    if (ReadStamp(path) != file.Stamp)
                    {
                        problems.Add($"'{path}' changed before rollback");
                    }
                    else if (file.Pending.Part is { } part)
                    {
                        File.Move(path, part, overwrite: false);
                    }
                    else
                    {
                        File.Delete(path);
                    }
                }
                catch (Exception rollback) when (rollback is IOException or UnauthorizedAccessException)
                {
                    problems.Add(rollback.Message);
                }
            }

            if (stage is not null && stagingStarted)
            {
                var folder = stage;
                try
                {
                    if (Directory.Exists(folder) && moved.Count > 0)
                    {
                        FlatDiscLayout.RemoveManifest(folder);
                    }

                    if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
                    {
                        Directory.Delete(folder);
                    }
                }
                catch (Exception rollback) when (rollback is IOException or UnauthorizedAccessException)
                {
                    problems.Add(rollback.Message);
                }
            }

            if (problems.Count != 0)
            {
                throw new IOException($"{ex.Message} Group rollback needs attention: {string.Join("; ", problems)}", ex);
            }

            throw;
        }
    }

    private RelativePath? FreshLooseDiscFolder()
    {
        if (steps.Count < 2 || steps.Count != _pending.Count || steps.Any(step => !step.PublishAsDiscGroup))
        {
            return null;
        }

        var folder = FlatDiscLayout.FolderFor(steps[0].Member.Folder, steps[0].Member.FsName);
        return folder is { } group && steps.All(step => step.TargetPath == group.Combine(step.TargetPath.Name))
            ? group : null;
    }

    private static FileStamp? ReadStamp(string path)
    {
        var info = new FileInfo(path);
        return info.Exists ? new FileStamp(info.CreationTimeUtc, info.LastWriteTimeUtc, info.Length) : null;
    }

    private FileStamp MoveIntoPlace(PendingFile file, string target, bool stagedPackage)
    {
        if (file.Part is not null)
        {
            return MoveVerified(file.Part, target);
        }

        if (stagedPackage)
        {
            // This directory is off-menu and recovery can regenerate a torn playlist.
            using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                output.Write(file.Playlist!);
            }

            return ReadStamp(target) ?? throw new IOException("The staged playlist is missing.");
        }

        var temporary = install.Resolve(RetroBatInstall.PartialDirectory.Combine(Path.GetRandomFileName()));
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(temporary)!);
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                output.Write(file.Playlist!);
            }

            return MoveVerified(temporary, target);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static FileStamp MoveVerified(string source, string target)
    {
        var stamp = ReadStamp(source) ?? throw new FileNotFoundException("The staged group file is missing.", source);
        File.Move(source, target, overwrite: false);
        return stamp;
    }

    private void Record(IReadOnlyList<PendingFile> files) => store.InTransaction(() =>
    {
        foreach (var file in _kept)
        {
            store.Files.Record(file);
        }

        foreach (var file in files)
        {
            var record = file.Record;
            var absolute = install.Resolve(record.Path);
            var info = new FileInfo(absolute);
            store.Files.Record(record with
            {
                ModifiedUtc = info.LastWriteTimeUtc,
                Md5Hash = file.Playlist is not null ? ContentHasher.ComputeFileMd5(absolute) : record.Md5Hash,
            });

            if (file.Part is not null)
            {
                store.Downloads.Remove(record.RomId!.Value, file.FileId);
            }
        }
    });

    private sealed record FileStamp(DateTime Created, DateTime Modified, long Size);
    private sealed record PublishedFile(PendingFile Pending, string Path, FileStamp Stamp);
    private sealed record PendingFile(LocalFile Record, string? Part, int FileId, byte[]? Playlist = null);
}
