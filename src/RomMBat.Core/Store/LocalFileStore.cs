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

/// <summary>Where one ROM's files are on this device, and what they weigh together.</summary>
/// <param name="Folders">
/// Every <c>roms/</c> folder holding a copy of the game itself. More than one is legitimate:
/// <c>folder_override</c> is the only way an arcade set resolves, so a <c>mame</c>-overridden
/// platform set and an <c>fbneo</c>-overridden collection set drawn from the same platform put
/// every shared game in both, and both sets are then correct in EmulationStation.
/// </param>
/// <param name="Bytes">
/// Every file of every kind, so two folders genuinely count twice. They do occupy twice the
/// room, and making that visible is the point.
/// </param>
public sealed record RomPlacement(IReadOnlyList<string> Folders, long Bytes)
{
    public bool IsHere => Folders.Count > 0;
}

/// <summary>One game this device holds, as an offline browse lists it.</summary>
public sealed record InstalledGame(int RomId, string DisplayName, string PlatformSlug);

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

    /// <summary>
    /// What RomMBat downloaded, in bytes, without materialising a row.
    /// </summary>
    /// <remarks>
    /// <b>The figure the disk budget is arithmetic over.</b> Callers used to read it as
    /// <c>List().Where(origin == Synced).Sum(...)</c>, which pulls the whole table into memory
    /// to add one column: migration 013's header puts the live install at 5,268 rows, and
    /// interleaving artwork per game turned that from one scan per run into one per game.
    /// Same answer, one row of results. See #111.
    /// <para>
    /// Adopted files are excluded, for the same reason they never counted: a user's own library
    /// is not RomMBat's to bound. <c>SyncSetService.OnDisk</c> counts them because it answers a
    /// different question.
    /// </para>
    /// </remarks>
    public long SyncedBytes()
    {
        using var command = _connection.Command(
            "SELECT COALESCE(SUM(size_bytes), 0) FROM local_file WHERE origin = 'synced';");

        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// What every file belonging to one sync set's members occupies, of every kind.
    /// </summary>
    /// <remarks>
    /// <b>A subquery rather than a list of ids, and the difference was measured.</b> The obvious
    /// rewrite of the per-member loop is <see cref="BytesForRoms"/> over the membership, and at
    /// 5,000 members on the development machine that is <b>95 ms against the loop's 111 ms</b>:
    /// binding five thousand parameters costs very nearly what five thousand queries did. This
    /// form takes one parameter, answers the same 5,000 members in <b>1 ms</b>, and lets SQLite walk <c>ix_sync_set_member_state</c> and
    /// <c>ix_local_file_rom</c> itself. See #111, whose own suggested SQL is this shape.
    /// <para>
    /// <c>state = 'member'</c>, so a game that has left the set stops counting against it the
    /// moment it departs, which is what makes the figure answer "what is this set costing me"
    /// rather than "what did it ever cost me". Adopted files are counted: see
    /// <c>SyncSetService.OnDisk</c>.
    /// </para>
    /// </remarks>
    public long BytesForSet(long syncSetId)
    {
        using var command = _connection
            .Command(
                """
                SELECT COALESCE(SUM(size_bytes), 0)
                FROM local_file
                WHERE rom_id IN (
                  SELECT rom_id FROM sync_set_member WHERE sync_set_id = $setId AND state = 'member'
                );
                """)
            .With("$setId", syncSetId);

        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// What every file recorded for these ROMs occupies, of every kind.
    /// </summary>
    /// <remarks>
    /// <b>For a page of browse rows, which is tens of ids and not thousands.</b> A set's own
    /// total goes through <see cref="BytesForSet"/> instead: binding five thousand parameters
    /// measured at 95 ms against the per-member loop's 111 ms, so this shape does not scale
    /// and the subquery form does.
    /// <para>
    /// Adopted files are counted, deliberately, and the reason is on
    /// <c>SyncSetService.OnDisk</c>: this answers how much of the drive something is using, and
    /// the user's own ROM in that folder is using it too.
    /// </para>
    /// </remarks>
    public long BytesForRoms(IReadOnlyCollection<int> romIds)
    {
        ArgumentNullException.ThrowIfNull(romIds);

        if (romIds.Count == 0)
        {
            return 0;
        }

        // Parameterised one id at a time rather than joined into the text, because a set can
        // hold thousands and building SQL out of values is how an injection gets in even when
        // every value is an integer today.
        var names = romIds.Select((_, index) => "$r" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();

        using var command = _connection.Command(
            $"SELECT COALESCE(SUM(size_bytes), 0) FROM local_file WHERE rom_id IN ({string.Join(", ", names)});");

        var index = 0;
        foreach (var romId in romIds)
        {
            command.With(names[index++], romId);
        }

        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Where a ROM's files are on this device, and what they weigh, for a page of rows at once.
    /// </summary>
    /// <remarks>
    /// <b>One query for a page, because a browse row has to say whether the game is here and a
    /// page is fifty of them.</b> Asked per row it is the shape #111 was filed about, on the
    /// drawing thread, while somebody scrolls.
    /// <para>
    /// Only the game's own rows name a folder. Its artwork lives under that folder too, and
    /// listing that again would read as another copy of the game.
    /// </para>
    /// </remarks>
    public IReadOnlyDictionary<int, RomPlacement> PlacementFor(IReadOnlyCollection<int> romIds)
    {
        ArgumentNullException.ThrowIfNull(romIds);

        var placements = new Dictionary<int, RomPlacement>();

        if (romIds.Count == 0)
        {
            return placements;
        }

        var names = romIds
            .Select((_, index) => "$r" + index.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ToList();

        using var command = _connection.Command(
            $"""
            SELECT rom_id, folder, size_bytes, kind
            FROM local_file
            WHERE rom_id IN ({string.Join(", ", names)})
            ORDER BY folder, relative_path;
            """);

        var bound = 0;
        foreach (var romId in romIds)
        {
            command.With(names[bound++], romId);
        }

        using var reader = command.ExecuteReader();

        var folders = new Dictionary<int, List<string>>();
        var bytes = new Dictionary<int, long>();

        while (reader.Read())
        {
            var romId = (int)reader.GetInt64(0);
            var folder = reader.GetStringOrNull(1);

            bytes[romId] = bytes.GetValueOrDefault(romId) + reader.GetInt64(2);

            if (!string.Equals(reader.GetString(3), "rom", StringComparison.Ordinal) || folder is null)
            {
                continue;
            }

            var known = folders.TryGetValue(romId, out var existing) ? existing : folders[romId] = [];

            if (!known.Contains(folder, StringComparer.OrdinalIgnoreCase))
            {
                known.Add(folder);
            }
        }

        foreach (var (romId, total) in bytes)
        {
            placements[romId] = new RomPlacement(folders.GetValueOrDefault(romId, []), total);
        }

        return placements;
    }

    /// <summary>
    /// The games this device actually holds, as a browsable page.
    /// </summary>
    /// <remarks>
    /// <b>What browse falls back to with no server, and it is not a lesser view of the same
    /// thing.</b> M2's rule is that the catalog is never mirrored wholesale, so the offline
    /// browsable set is the locally present subset, which is what EmulationStation shows anyway.
    /// <para>
    /// Keyed on the Rom-kind rows rather than on membership, because the question is what is on
    /// the drive: a game whose set was deleted is still here, and so is a file the user put
    /// there themselves. The name comes from the membership when there is one, which is why it
    /// is a left join.
    /// </para>
    /// </remarks>
    public (int Total, IReadOnlyList<InstalledGame> Games) InstalledGames(
        string? folder,
        string? search,
        int limit,
        int offset)
    {
        var clauses = new List<string> { "f.kind = 'rom'", "f.rom_id IS NOT NULL" };

        if (!string.IsNullOrWhiteSpace(folder))
        {
            clauses.Add("f.folder = $folder COLLATE NOCASE");
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            clauses.Add("COALESCE(m.display_name, f.file_name) LIKE $search ESCAPE '~'");
        }

        var where = "WHERE " + string.Join(" AND ", clauses);
        const string From = "FROM local_file f LEFT JOIN sync_set_member m ON m.rom_id = f.rom_id";

        using var counting = _connection.Command($"SELECT COUNT(DISTINCT f.rom_id) {From} {where};");
        Bind(counting);

        var total = Convert.ToInt32(counting.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);

        // Grouped by rom, so one ROM in two folders is one row that names both, which is the
        // reading browse gives an online row too.
        using var command = _connection.Command(
            $"""
            SELECT f.rom_id,
                   MIN(COALESCE(m.display_name, f.file_name)),
                   MIN(COALESCE(m.platform_slug, f.folder)),
                   MIN(COALESCE(m.sort_key, f.file_name))
            {From}
            {where}
            GROUP BY f.rom_id
            ORDER BY 4 COLLATE NOCASE, f.rom_id
            LIMIT $limit OFFSET $offset;
            """);

        Bind(command);
        command.With("$limit", limit).With("$offset", offset);

        using var reader = command.ExecuteReader();
        var games = new List<InstalledGame>();

        while (reader.Read())
        {
            games.Add(new InstalledGame(
                (int)reader.GetInt64(0),
                reader.GetString(1),
                reader.GetStringOrNull(2) ?? string.Empty));
        }

        return (total, games);

        void Bind(SqliteCommand command)
        {
            if (!string.IsNullOrWhiteSpace(folder))
            {
                command.With("$folder", folder);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                // Escaped with a character no path or title carries, because a name holding %
                // or _ would otherwise widen the search rather than narrow it, and a real
                // library is full of both.
                command.With("$search", "%" + Escape(search) + "%");
            }
        }
    }

    /// <summary>Escapes LIKE's two wildcards, and the escape character itself first.</summary>
    private static string Escape(string term) => term
        .Replace("~", "~~", StringComparison.Ordinal)
        .Replace("%", "~%", StringComparison.Ordinal)
        .Replace("_", "~_", StringComparison.Ordinal);

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
