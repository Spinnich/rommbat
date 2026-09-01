using Microsoft.Data.Sqlite;
using RomMBat.Core.Paths;

namespace RomMBat.Core.Store;

/// <summary>How a file came to be here.</summary>
public enum FileOrigin
{
    /// <summary>RomMBat downloaded it.</summary>
    Synced,

    /// <summary>It was already on disk and was matched to a ROM rather than re-downloaded.</summary>
    Adopted,
}

/// <summary>What a recorded hash describes.</summary>
public enum HashScope
{
    /// <summary>The bytes of the file itself.</summary>
    File,

    /// <summary>
    /// The single file inside a one-entry archive.
    /// </summary>
    /// <remarks>
    /// What RomM stores, and therefore the only thing worth comparing against. A 1,025-byte
    /// <c>.zip</c> reports the hashes of the 16,400-byte <c>.nes</c> inside it.
    /// </remarks>
    ArchiveContent,
}

/// <summary>What a recorded file is.</summary>
/// <remarks>
/// Media shares its <see cref="LocalFile.RomId"/> with the ROM it decorates, so one ROM can
/// have six rows. Everything that used to assume one row per ROM now says which kind it
/// means.
/// </remarks>
public enum LocalFileKind
{
    /// <summary>The game itself. What every row was before M4.</summary>
    Rom,

    /// <summary>The large cover, which the gamelist calls <c>image</c>.</summary>
    Image,

    Thumbnail,

    Marquee,

    Video,

    Manual,

    /// <summary>
    /// A BIOS file under <c>bios/</c>, which belongs to a system rather than to a ROM.
    /// </summary>
    /// <remarks>
    /// The one kind with no <see cref="LocalFile.RomId"/> and no <see cref="LocalFile.Folder"/>,
    /// enforced by a CHECK: <c>bios/coleco.rom</c> is required by three systems at three
    /// paths, so any single folder written on the row would be a lie. Having no
    /// <c>rom_id</c> is also what keeps eviction away from it, since eviction only ever
    /// considers rows that have one.
    /// </remarks>
    Firmware,
}

/// <summary>Which check a file last passed.</summary>
public enum VerifiedBy
{
    /// <summary>Nothing has been checked.</summary>
    None,

    /// <summary>Length only, which is all that is possible when the server has no hash.</summary>
    Size,

    Md5,

    Sha1,
}

/// <summary>One file RomMBat knows about inside the RetroBat tree.</summary>
public sealed record LocalFile
{
    public long Id { get; init; }

    /// <summary>Where it is, relative to the RetroBat root. Never absolute, on pain of a CHECK.</summary>
    public required RelativePath Path { get; init; }

    /// <summary>
    /// The <c>roms/</c> folder it lives in, which is what everything downstream groups by,
    /// and null for firmware.
    /// </summary>
    public string? Folder { get; init; }

    /// <summary>The ROM it is, or belongs to, or null for firmware.</summary>
    public int? RomId { get; init; }

    /// <summary>Whether this is the game, one of the five media files beside it, or firmware.</summary>
    public LocalFileKind Kind { get; init; } = LocalFileKind.Rom;

    public required string FileName { get; init; }

    public long SizeBytes { get; init; }

    public string? Md5Hash { get; init; }

    /// <summary>What the hash describes.</summary>
    public HashScope HashScope { get; init; } = HashScope.File;

    /// <summary>
    /// The file's own modification time.
    /// </summary>
    /// <remarks>
    /// A tiebreak and never a comparison. FAT32 and exFAT both store this to 2 seconds and
    /// round <b>up</b>, so a file can carry a timestamp later than the clock that wrote it and
    /// two files written in one window are not orderable at all.
    /// </remarks>
    public DateTimeOffset? ModifiedUtc { get; init; }

    public DateTimeOffset? VerifiedAt { get; init; }

    public VerifiedBy VerifiedBy { get; init; } = VerifiedBy.None;

    public FileOrigin Origin { get; init; } = FileOrigin.Synced;
}

/// <summary>
/// The inventory of what is on disk, which is what makes a second sync a no-op.
/// </summary>
/// <remarks>
/// Every path here is relative to the RetroBat root, so the whole inventory survives the
/// drive letter changing. Nothing in this class ever sees an absolute path:
/// <see cref="RetroBatInstall.Resolve(RelativePath)"/> is the only place one is built, at the
/// moment of use.
/// </remarks>
public sealed class LocalFileStore
{
    private const string SelectColumns = """
        SELECT id, relative_path, folder, rom_id, file_name, size_bytes, md5_hash,
               hash_scope, mtime_utc, verified_at, verified_by, origin, kind
        FROM local_file
        """;

    private readonly SqliteConnection _connection;

    internal LocalFileStore(SqliteConnection connection) => _connection = connection;

    /// <summary>Records a file, replacing whatever was known about that path.</summary>
    public LocalFile Record(LocalFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        using var command = _connection.Command(
            """
            INSERT INTO local_file (
              relative_path, folder, rom_id, file_name, size_bytes, md5_hash,
              hash_scope, mtime_utc, verified_at, verified_by, origin, kind
            )
            VALUES (
              $path, $folder, $romId, $fileName, $size, $md5,
              $scope, $mtime, $verifiedAt, $verifiedBy, $origin, $kind
            )
            ON CONFLICT (relative_path) DO UPDATE SET
              folder      = excluded.folder,
              rom_id      = excluded.rom_id,
              kind        = excluded.kind,
              file_name   = excluded.file_name,
              size_bytes  = excluded.size_bytes,
              md5_hash    = excluded.md5_hash,
              hash_scope  = excluded.hash_scope,
              mtime_utc   = excluded.mtime_utc,
              verified_at = excluded.verified_at,
              verified_by = excluded.verified_by,
              origin      = excluded.origin
            RETURNING id;
            """)
            .With("$path", file.Path.Value)
            .With("$folder", SqliteValues.OrNull(file.Folder))
            .With("$romId", SqliteValues.OrNull(file.RomId))
            .With("$fileName", file.FileName)
            .With("$size", file.SizeBytes)
            .With("$md5", SqliteValues.OrNull(Normalize(file.Md5Hash)))
            .With("$scope", ScopeText(file.HashScope))
            .With("$mtime", SqliteValues.ToTextOrNull(file.ModifiedUtc))
            .With("$verifiedAt", SqliteValues.ToTextOrNull(file.VerifiedAt))
            .With("$verifiedBy", VerifiedText(file.VerifiedBy))
            .With("$origin", OriginText(file.Origin))
            .With("$kind", KindText(file.Kind));

        return file with { Id = Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) };
    }

    /// <summary>The file at a path, or null.</summary>
    public LocalFile? Find(RelativePath path)
    {
        using var command = _connection
            .Command($"{SelectColumns} WHERE relative_path = $path;")
            .With("$path", path.Value);

        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    /// <summary>
    /// Every file recorded for a ROM, the game and its media alike.
    /// </summary>
    /// <remarks>
    /// Since M4 this is normally several rows, so a caller that means the game itself has to
    /// say so with <paramref name="kind"/>. More than one row of the same kind means the same
    /// ROM in two folders.
    /// </remarks>
    public IReadOnlyList<LocalFile> ForRom(int romId, LocalFileKind? kind = null)
    {
        var filter = kind is null ? string.Empty : " AND kind = $kind";
        using var command = _connection
            .Command($"{SelectColumns} WHERE rom_id = $romId{filter} ORDER BY relative_path;")
            .With("$romId", romId);

        if (kind is { } wanted)
        {
            command.With("$kind", KindText(wanted));
        }

        using var reader = command.ExecuteReader();
        return ReadAll(reader);
    }

    /// <summary>
    /// Files whose content hashes to this, for adoption.
    /// </summary>
    /// <remarks>
    /// Matched on md5 because that is what <c>GET /api/roms/by-hash</c> takes and what most
    /// of a real library carries. The comparison is case-insensitive: RomM lower-cases its
    /// hashes and nothing guarantees another writer did.
    /// </remarks>
    public IReadOnlyList<LocalFile> ByMd5(string md5)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(md5);

        using var command = _connection
            .Command($"{SelectColumns} WHERE md5_hash = $md5;")
            .With("$md5", Normalize(md5)!);

        using var reader = command.ExecuteReader();
        return ReadAll(reader);
    }

    /// <summary>Everything in one folder, or everything when no folder is named.</summary>
    public IReadOnlyList<LocalFile> List(string? folder = null, LocalFileKind? kind = null)
    {
        var clauses = new List<string>();
        if (folder is not null)
        {
            clauses.Add("folder = $folder COLLATE NOCASE");
        }

        if (kind is not null)
        {
            clauses.Add("kind = $kind");
        }

        var where = clauses.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", clauses);
        using var command = _connection.Command($"{SelectColumns}{where} ORDER BY relative_path;");

        if (folder is not null)
        {
            command.With("$folder", folder);
        }

        if (kind is { } wanted)
        {
            command.With("$kind", KindText(wanted));
        }

        using var reader = command.ExecuteReader();
        return ReadAll(reader);
    }

    /// <summary>How many files RomMBat holds and how many bytes they occupy.</summary>
    public (int Files, long Bytes) Totals(string? folder = null, LocalFileKind? kind = null)
    {
        var clauses = new List<string>();
        if (folder is not null)
        {
            clauses.Add("folder = $folder COLLATE NOCASE");
        }

        if (kind is not null)
        {
            clauses.Add("kind = $kind");
        }

        var where = clauses.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", clauses);
        using var command = _connection.Command(
            $"SELECT COUNT(*), COALESCE(SUM(size_bytes), 0) FROM local_file{where};");

        if (folder is not null)
        {
            command.With("$folder", folder);
        }

        if (kind is { } wanted)
        {
            command.With("$kind", KindText(wanted));
        }

        using var reader = command.ExecuteReader();
        return reader.Read() ? ((int)reader.GetInt64(0), reader.GetInt64(1)) : (0, 0);
    }

    /// <summary>Forgets a file. Returns false when there was nothing recorded there.</summary>
    public bool Remove(RelativePath path)
    {
        using var command = _connection
            .Command("DELETE FROM local_file WHERE relative_path = $path;")
            .With("$path", path.Value);

        return command.ExecuteNonQuery() > 0;
    }

    internal static string KindText(LocalFileKind kind) => kind switch
    {
        LocalFileKind.Image => "image",
        LocalFileKind.Thumbnail => "thumbnail",
        LocalFileKind.Marquee => "marquee",
        LocalFileKind.Video => "video",
        LocalFileKind.Manual => "manual",
        LocalFileKind.Firmware => "firmware",
        _ => "rom",
    };

    internal static LocalFileKind ParseKind(string? text) => text switch
    {
        "image" => LocalFileKind.Image,
        "thumbnail" => LocalFileKind.Thumbnail,
        "marquee" => LocalFileKind.Marquee,
        "video" => LocalFileKind.Video,
        "manual" => LocalFileKind.Manual,
        "firmware" => LocalFileKind.Firmware,
        _ => LocalFileKind.Rom,
    };

    internal static string OriginText(FileOrigin origin) => origin switch
    {
        FileOrigin.Adopted => "adopted",
        _ => "synced",
    };

    internal static FileOrigin ParseOrigin(string? text) =>
        string.Equals(text, "adopted", StringComparison.Ordinal) ? FileOrigin.Adopted : FileOrigin.Synced;

    internal static string ScopeText(HashScope scope) => scope switch
    {
        HashScope.ArchiveContent => "archive_content",
        _ => "file",
    };

    internal static HashScope ParseScope(string? text) =>
        string.Equals(text, "archive_content", StringComparison.Ordinal) ? HashScope.ArchiveContent : HashScope.File;

    internal static string VerifiedText(VerifiedBy verified) => verified switch
    {
        VerifiedBy.Md5 => "md5",
        VerifiedBy.Sha1 => "sha1",
        VerifiedBy.Size => "size",
        _ => "none",
    };

    internal static VerifiedBy ParseVerified(string? text) => text switch
    {
        "md5" => VerifiedBy.Md5,
        "sha1" => VerifiedBy.Sha1,
        "size" => VerifiedBy.Size,
        _ => VerifiedBy.None,
    };

    /// <summary>Hashes are stored lower-case, so a comparison never has to care.</summary>
    private static string? Normalize(string? hash) =>
        string.IsNullOrWhiteSpace(hash) ? null : hash.Trim().ToLowerInvariant();

    private static List<LocalFile> ReadAll(SqliteDataReader reader)
    {
        var files = new List<LocalFile>();
        while (reader.Read())
        {
            files.Add(Read(reader));
        }

        return files;
    }

    private static LocalFile Read(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        Path = RelativePath.Create(reader.GetString(1)),
        Folder = reader.GetStringOrNull(2),
        RomId = (int?)reader.GetInt64OrNull(3),
        FileName = reader.GetString(4),
        SizeBytes = reader.GetInt64(5),
        Md5Hash = reader.GetStringOrNull(6),
        HashScope = ParseScope(reader.GetStringOrNull(7)),
        ModifiedUtc = reader.GetTimestampOrNull(8),
        VerifiedAt = reader.GetTimestampOrNull(9),
        VerifiedBy = ParseVerified(reader.GetStringOrNull(10)),
        Origin = ParseOrigin(reader.GetStringOrNull(11)),
        Kind = ParseKind(reader.GetStringOrNull(12)),
    };
}
