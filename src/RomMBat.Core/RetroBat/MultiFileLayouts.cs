using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RomMBat.Core.RetroBat;

/// <summary>How one system's multi-file games land on disk.</summary>
/// <param name="System">The RetroBat folder, as <c>es_systems.cfg</c>'s <c>&lt;path&gt;</c> names it.</param>
/// <param name="DiscExtensions">
/// Which members a generated playlist lists. On a <c>.bin</c>/<c>.cue</c> set that is the
/// <c>.cue</c> files and never the <c>.bin</c> they point at.
/// </param>
public sealed record MultiFileLayout(string System, IReadOnlyList<string> DiscExtensions)
{
    /// <summary>
    /// The playlist's file name for a set: the set's own folder name plus <c>.m3u</c>.
    /// </summary>
    /// <remarks>
    /// The shape RomM itself writes when a set is regrouped, and the one RetroBat's
    /// EmulationStation lists as a single game: a folder holding a playlist named after the
    /// folder collapses into one entry and hides its discs.
    /// </remarks>
    public static string PlaylistNameFor(string fsName) => fsName + ".m3u";

    private static readonly string[] SheetExtensions = [".cue", ".ccd"];

    /// <summary>Whether a member is a disc a playlist should name.</summary>
    public bool IsDisc(string fileName) =>
        DiscExtensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase);

    /// <summary>The members a generated playlist names, in playlist order.</summary>
    /// <remarks>
    /// A sheet and the image it points at can both carry a disc extension (<c>.cue</c> and
    /// <c>.img</c>, or CloneCD's <c>.ccd</c> and <c>.img</c>), and naming both would show the
    /// cores one disc twice. An image that shares its stem with a sheet in the set is the sheet's,
    /// so only the sheet is named.
    /// </remarks>
    public IReadOnlyList<string> DiscsOf(IEnumerable<string> fileNames)
    {
        var discs = fileNames.Where(IsDisc).ToList();

        var sheetStems = discs
            .Where(name => SheetExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase))
            .Select(Path.GetFileNameWithoutExtension)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return discs
            .Where(name => SheetExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase)
                || !sheetStems.Contains(Path.GetFileNameWithoutExtension(name)))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

/// <summary>
/// The systems whose multi-file RomM roms are synced, which is none until a certification says so.
/// </summary>
/// <remarks>
/// <b>Unlocked per system, by a hands-on pass, never by default.</b> A multi-file rom is
/// excluded everywhere else, because how RetroBat wants one laid out is a fact about each system's
/// emulators and not about the files: on <c>psx</c> five rows read a playlist and both BizHawk
/// rows are handed disc 1 by <c>emulatorLauncher</c>, and <c>ps2</c>'s PCSX2 cannot use a
/// playlist at all. The table is <c>data/retrobat/multi_file.json</c>, and each entry carries the
/// evidence that unlocked it.
/// </remarks>
public sealed class MultiFileLayouts
{
    private const string Resource = "RomMBat.Core.data.retrobat.multi_file.json";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly FrozenDictionary<string, MultiFileLayout> _systems;

    private MultiFileLayouts(FrozenDictionary<string, MultiFileLayout> systems) => _systems = systems;

    /// <summary>The shipped table, read once.</summary>
    public static MultiFileLayouts Bundled { get; } = LoadEmbedded();

    /// <summary>A table naming nothing, for callers that must not unlock any system.</summary>
    public static MultiFileLayouts None { get; } = new(FrozenDictionary<string, MultiFileLayout>.Empty);

    /// <summary>The systems the table unlocks.</summary>
    public IEnumerable<string> Systems => _systems.Keys;

    /// <summary>The layout for a system, or null when its multi-file roms stay excluded.</summary>
    public MultiFileLayout? For(string? system) =>
        system is not null && _systems.TryGetValue(system, out var layout) ? layout : null;

    /// <summary>Reads a table from JSON, refusing anything it cannot honour.</summary>
    public static MultiFileLayouts Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        var file = JsonSerializer.Deserialize<FileShape>(json, SerializerOptions)
            ?? throw new InvalidDataException("multi_file.json is empty.");

        var systems = new Dictionary<string, MultiFileLayout>(StringComparer.OrdinalIgnoreCase);

        foreach (var (system, entry) in file.Systems ?? [])
        {
            // One layout and one playlist format exist, because one has been measured. A value
            // this build does not know is refused rather than read as the one it does, which
            // would land a set in a shape nobody drove.
            if (!string.Equals(entry.Layout, "folder", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"multi_file.json gives {system} the layout '{entry.Layout}'; only 'folder' is implemented.");
            }

            if (!string.Equals(entry.Playlist, "m3u", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"multi_file.json gives {system} the playlist '{entry.Playlist}'; only 'm3u' is implemented.");
            }

            var extensions = entry.DiscExtensions ?? [];
            if (extensions.Count == 0 || extensions.Any(extension => !extension.StartsWith('.')))
            {
                throw new InvalidDataException(
                    $"multi_file.json gives {system} no disc extensions, or one without its leading dot.");
            }

            systems[system] = new MultiFileLayout(system, extensions);
        }

        return new MultiFileLayouts(systems.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase));
    }

    private static MultiFileLayouts LoadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(Resource)
            ?? throw new InvalidOperationException($"The embedded resource {Resource} is missing.");
        using var reader = new StreamReader(stream);

        return Parse(reader.ReadToEnd());
    }

    private sealed record FileShape
    {
        public Dictionary<string, EntryShape>? Systems { get; init; }
    }

    private sealed record EntryShape
    {
        public string? Layout { get; init; }

        public string? Playlist { get; init; }

        [JsonPropertyName("disc_extensions")]
        public List<string>? DiscExtensions { get; init; }
    }
}
