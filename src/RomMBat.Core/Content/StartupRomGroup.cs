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
            if (!File.Exists(install.Resolve(step.TargetPath)))
            {
                throw new IOException($"'{step.TargetPath.Name}' disappeared during the transfer.");
            }
        }

        if (steps.Count == 1 && steps[0].Member.IsMultiFile && _pending.Count > 0
            && !Path.Exists(install.Resolve(ContentPlanner.FolderFor(steps[0].Member))))
        {
            PublishNewFolder(install.Resolve(ContentPlanner.FolderFor(steps[0].Member)));
            Record(_pending);
            return;
        }

        // Flat discs and repairs keep their existing paths. They are all verified before the
        // first move. Individual moves are create-only, and the playlist always goes last.
        foreach (var file in _pending.OrderBy(file => file.Record.Kind != LocalFileKind.RomPart))
        {
            var target = install.Resolve(file.Record.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            MoveIntoPlace(file, target);
        }

        Record(_pending);
    }

    private void PublishNewFolder(string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var stage = install.Resolve(RetroBatInstall.PartialDirectory.Combine(Path.GetRandomFileName()));
        Directory.CreateDirectory(stage);
        try
        {
            foreach (var file in _pending)
            {
                MoveIntoPlace(file, Path.Combine(stage, file.Record.Path.Name));
            }

            Directory.Move(stage, target);
        }
        finally
        {
            // If publication lost a race, keep the verified downloads resumable.
            if (Directory.Exists(stage))
            {
                foreach (var file in _pending)
                {
                    var staged = Path.Combine(stage, file.Record.Path.Name);
                    if (file.Part is not null && File.Exists(staged))
                    {
                        File.Move(staged, file.Part, overwrite: false);
                    }
                }

                Directory.Delete(stage, recursive: true);
            }
        }
    }

    private void MoveIntoPlace(PendingFile file, string target)
    {
        if (file.Part is not null)
        {
            File.Move(file.Part, target, overwrite: false);
            return;
        }

        var temporary = install.Resolve(RetroBatInstall.PartialDirectory.Combine(Path.GetRandomFileName()));
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                output.Write(file.Playlist!);
            }

            File.Move(temporary, target, overwrite: false);
        }
        finally
        {
            File.Delete(temporary);
        }
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

    private sealed record PendingFile(LocalFile Record, string? Part, int FileId, byte[]? Playlist = null);
}
