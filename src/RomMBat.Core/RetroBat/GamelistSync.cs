using System.Globalization;
using RomM.Client.Content;
using RomMBat.Core.Content;
using RomMBat.Core.Metadata;
using RomMBat.Core.Paths;
using RomMBat.Core.Store;

namespace RomMBat.Core.RetroBat;

/// <summary>What writing one folder's gamelist did.</summary>
public sealed record GamelistFolderResult
{
    public required string Folder { get; init; }

    public int Entries { get; init; }

    public int Added { get; init; }

    public int Updated { get; init; }

    public int Removed { get; init; }

    /// <summary>Entries in the file that RomMBat does not own and left alone.</summary>
    public int Foreign { get; init; }

    public bool Wrote { get; init; }

    /// <summary>Set when the folder was skipped, and why.</summary>
    public string? Problem { get; init; }
}

/// <summary>What a whole gamelist pass did.</summary>
public sealed record GamelistSyncOutcome
{
    public IReadOnlyList<GamelistFolderResult> Folders { get; init; } = [];

    /// <summary>What happened when EmulationStation was asked to re-read.</summary>
    public EsCallResult Reload { get; init; } = EsCallResult.NotRunning;

    /// <summary>Folders past the navigability threshold, with their entry counts.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    public int Written => Folders.Count(folder => folder.Wrote);

    public bool IsNoOp => Written == 0 && Folders.All(folder => folder.Problem is null);

    public string Summary
    {
        get
        {
            if (Folders.Count == 0)
            {
                return "no gamelist to write: nothing is on disk yet";
            }

            if (IsNoOp)
            {
                return $"gamelists: all {Folders.Count} unchanged";
            }

            var parts = new List<string>();
            var added = Folders.Sum(folder => folder.Added);
            var updated = Folders.Sum(folder => folder.Updated);
            var removed = Folders.Sum(folder => folder.Removed);

            if (added > 0)
            {
                parts.Add($"{added} added");
            }

            if (updated > 0)
            {
                parts.Add($"{updated} updated");
            }

            if (removed > 0)
            {
                parts.Add($"{removed} removed");
            }

            var problems = Folders.Count(folder => folder.Problem is not null);
            if (problems > 0)
            {
                parts.Add($"{problems} skipped");
            }

            var text = parts.Count == 0 ? "rewritten" : string.Join(", ", parts);
            return $"gamelists: {Written} of {Folders.Count} written, {text}";
        }
    }
}

/// <summary>
/// Writes one <c>gamelist.xml</c> per RetroBat folder, then tells EmulationStation.
/// </summary>
/// <remarks>
/// <b>Keyed by resolved folder, never by platform.</b> The mapping is many-to-many:
/// <c>snes</c> and <c>sfam</c> both resolve to <c>snes</c>, and <c>arcade</c> fans out to ten
/// folders. One gamelist per platform would have the second write clobber the first, which is
/// the single failure this grouping exists to prevent.
/// <para>
/// <b>Works with the server unreachable.</b> Everything written comes from the local store:
/// the file rows say what is on disk, and <c>rom_metadata</c> says what to say about it. That
/// is what lets an eviction rewrite a folder's gamelist on a handheld that has been off the
/// network for a week.
/// </para>
/// <para>
/// <b>Ownership decides what may be removed.</b> An entry is RomMBat's when its path is
/// <c>./</c> plus an <c>fs_name</c> recorded for that folder. Anything else was written by
/// the user's own scraper and is left exactly where it is, even when the file behind it is
/// gone.
/// </para>
/// </remarks>
public sealed class GamelistSync
{
    /// <summary>
    /// How many entries in one folder before a sync says something.
    /// </summary>
    /// <remarks>
    /// <b>A threshold, not a cap, and the difference is measured.</b> EmulationStation lists
    /// ROM files it has no gamelist entry for, so dropping entries would hide no games and
    /// only strip their art: the user scrolls past exactly as many tiles, now blank. What
    /// bounds navigability is the sync set's own <c>max_games</c>. ES itself is not the
    /// constraint either, having loaded 100,000 entries in 2.07 s.
    /// </remarks>
    public const int DefaultWarnEntries = 1000;

    /// <summary>Overrides <see cref="DefaultWarnEntries"/>. Set to 0 to say nothing.</summary>
    public const string WarnEntriesSetting = "gamelist.warn_entries";

    private readonly RetroBatInstall _install;
    private readonly LocalStore _store;

    public GamelistSync(RetroBatInstall install, LocalStore store)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(store);

        _install = install;
        _store = store;
    }

    /// <summary>Where a folder's gamelist lives.</summary>
    public static RelativePath PathFor(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        return RelativePath.Create($"roms/{folder}/gamelist.xml");
    }

    /// <summary>
    /// Writes every folder that has locally present ROMs, then reloads.
    /// </summary>
    /// <param name="folders">
    /// Which folders to write, or null for every folder that holds a ROM. A sync passes the
    /// folders it touched so an install with forty systems does not rewrite thirty-nine of
    /// them to change nothing.
    /// </param>
    /// <param name="emulationStation">
    /// Null skips the reload entirely, which is what a dry run wants. Otherwise the reload is
    /// best-effort: ES is usually not running and that is not a failure.
    /// </param>
    public async Task<GamelistSyncOutcome> ApplyAsync(
        IEnumerable<string>? folders = null,
        EmulationStationClient? emulationStation = null,
        CancellationToken cancellationToken = default)
    {
        var targets = (folders ?? FoldersWithRoms())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var results = new List<GamelistFolderResult>(targets.Count);
        var warnings = new List<string>();
        var threshold = _store.Settings.GetInt64(WarnEntriesSetting) ?? DefaultWarnEntries;

        foreach (var folder in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = Write(folder);
            results.Add(result);

            if (threshold > 0 && result.Entries > threshold)
            {
                warnings.Add(
                    $"{folder}: {result.Entries.ToString("N0", CultureInfo.InvariantCulture)} games with metadata, "
                        + $"past the {threshold:N0} this install is told to mention. Nothing was left out: "
                        + "EmulationStation lists the ROM files either way, so lowering the set's game cap is "
                        + "what makes the list shorter.");
            }
        }

        var reload = EsCallResult.NotRunning;

        // Only when something changed. Asking ES to rescan when nothing moved costs it a
        // second of work at 100,000 entries and achieves nothing.
        if (emulationStation is not null && results.Any(folder => folder.Wrote))
        {
            reload = await emulationStation.ReloadGamesAsync(cancellationToken).ConfigureAwait(false);
        }

        return new GamelistSyncOutcome { Folders = results, Reload = reload, Warnings = warnings };
    }

    /// <summary>Every folder holding at least one ROM RomMBat knows about.</summary>
    public IReadOnlyList<string> FoldersWithRoms() =>
        [.. _store.Files.List(kind: LocalFileKind.Rom)
            .Select(file => file.Folder)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)];

    /// <summary>Merges one folder's gamelist and writes it when the bytes would differ.</summary>
    public GamelistFolderResult Write(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        var path = _install.Resolve(PathFor(folder));

        GamelistDocument document;
        try
        {
            document = GamelistDocument.Load(path);
        }
        catch (GamelistParseException ex)
        {
            // Left alone deliberately. The file holds metadata a user may have scraped or
            // edited by hand, and rewriting what could not be parsed would destroy it.
            return new GamelistFolderResult { Folder = folder, Problem = ex.Message };
        }

        var present = _store.Files.List(folder, LocalFileKind.Rom)
            .Where(file => file.RomId is not null)
            .ToList();

        var metadata = _store.Metadata.ForRoms(present.Select(file => file.RomId!.Value));
        var media = MediaFor(folder);

        var added = 0;
        var updated = 0;

        foreach (var file in present)
        {
            var entryPath = "./" + file.FileName;
            var existed = document.Contains(entryPath);
            var game = metadata.GetValueOrDefault(file.RomId!.Value);

            var entry = new GamelistEntry(
                entryPath,
                Fields(game, file.FileName, media.GetValueOrDefault(file.RomId!.Value)));

            if (document.Apply(entry))
            {
                if (existed)
                {
                    updated++;
                }
                else
                {
                    added++;
                }
            }
        }

        var removed = RemoveDeparted(document, folder, present);
        var wrote = document.WriteIfChanged(path);

        // Whatever is left that RomMBat neither wrote nor knows about: entries a user's own
        // scraper put there, which are carried through untouched and counted so a run can say
        // so rather than leaving the user to wonder.
        var ours = present.Select(file => "./" + file.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var foreign = document.Paths.Count(entry => !ours.Contains(entry));

        return new GamelistFolderResult
        {
            Folder = folder,
            Entries = document.Count,
            Added = added,
            Updated = updated,
            Removed = removed,
            Foreign = foreign,
            Wrote = wrote,
        };
    }

    /// <summary>
    /// Drops entries for games RomMBat used to have here and no longer does.
    /// </summary>
    /// <remarks>
    /// A stale entry is inert rather than harmful, since ES does not list a game whose file is
    /// missing, but it does survive ES's own rewrite and would sit there forever. Only entries
    /// RomMBat owns are considered: an entry a user scraped for a ROM they later moved is
    /// theirs, not ours.
    /// </remarks>
    private int RemoveDeparted(GamelistDocument document, string folder, IReadOnlyList<LocalFile> present)
    {
        var here = present
            .Select(file => "./" + file.FileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var removed = 0;

        foreach (var owned in _store.Metadata.ForFolder(folder))
        {
            if (!here.Contains(owned.GamelistPath) && document.Remove(owned.GamelistPath))
            {
                removed++;
            }
        }

        return removed;
    }

    /// <summary>The gamelist references for one folder's media, keyed by ROM.</summary>
    private Dictionary<int, Dictionary<MediaKind, string>> MediaFor(string folder)
    {
        var byRom = new Dictionary<int, Dictionary<MediaKind, string>>();

        foreach (var file in _store.Files.List(folder))
        {
            if (file.RomId is not { } romId || file.Kind == LocalFileKind.Rom)
            {
                continue;
            }

            var kind = ToMediaKind(file.Kind);
            if (kind is null)
            {
                continue;
            }

            if (!byRom.TryGetValue(romId, out var kinds))
            {
                kinds = [];
                byRom[romId] = kinds;
            }

            kinds[kind.Value] = MediaNaming.GamelistReference(kind.Value, file.FileName);
        }

        return byRom;
    }

    /// <summary>
    /// The elements RomMBat owns, in the order a new entry writes them.
    /// </summary>
    /// <remarks>
    /// <b>This list is the allowlist, and it is the whole of it.</b> Everything else in the
    /// file belongs to somebody else: ES writes <c>playcount</c>, <c>lastplayed</c> and
    /// <c>gametime</c>, its scraper writes <c>scrap</c>, <c>cheevosHash</c>, <c>cheevosId</c>,
    /// <c>md5</c>, <c>crc32</c>, <c>arcadesystemname</c> and <c>multidisk</c>, and
    /// <c>&lt;game&gt;</c> can carry <c>id</c> and <c>source</c> attributes.
    /// <para>
    /// A null value removes the element rather than writing an empty one, so a game whose
    /// rating disappeared upstream stops claiming one instead of claiming zero.
    /// </para>
    /// <para>
    /// There is no <c>publisher</c>, and its absence is the point: see
    /// <see cref="GameMetadata.Developer"/>.
    /// </para>
    /// </remarks>
    private static List<KeyValuePair<string, string?>> Fields(
        GameMetadata? metadata,
        string fileName,
        IReadOnlyDictionary<MediaKind, string>? media) =>
        [
            new("name", metadata?.Name ?? MediaNaming.StemOf(fileName)),
            new("desc", metadata?.Description),
            new("image", Media(media, MediaKind.Image)),
            new("thumbnail", Media(media, MediaKind.Thumbnail)),
            new("marquee", Media(media, MediaKind.Marquee)),
            new("video", Media(media, MediaKind.Video)),
            new("manual", Media(media, MediaKind.Manual)),
            new("developer", metadata?.Developer),
            new("genre", metadata?.Genre),
            new("family", metadata?.Family),
            new("players", metadata?.Players),
            new("lang", metadata?.Languages),
            new("region", metadata?.Region),
            new("releasedate", metadata?.ReleaseDate),
            new("rating", metadata?.Rating),
        ];

    private static string? Media(IReadOnlyDictionary<MediaKind, string>? media, MediaKind kind) =>
        media is not null && media.TryGetValue(kind, out var reference) ? reference : null;

    private static MediaKind? ToMediaKind(LocalFileKind kind) => kind switch
    {
        LocalFileKind.Image => MediaKind.Image,
        LocalFileKind.Thumbnail => MediaKind.Thumbnail,
        LocalFileKind.Marquee => MediaKind.Marquee,
        LocalFileKind.Video => MediaKind.Video,
        LocalFileKind.Manual => MediaKind.Manual,
        _ => null,
    };
}
