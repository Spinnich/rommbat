using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization;
using RomMBat.Core.Paths;

namespace RomMBat.Core.RetroBat;

/// <summary>One file RetroBat wants under <c>bios/</c>, and where it wants it.</summary>
/// <param name="System">The batocera system name the requirement came from.</param>
/// <param name="Folder">The RetroBat system folder, which is what everything else joins on.</param>
/// <param name="Md5">
/// The hash to join RomM's firmware on, or null when RetroBat names none.
/// </param>
/// <param name="Path">Where the file belongs, relative to the RetroBat root.</param>
public sealed record BiosRequirement(string System, string Folder, string? Md5, RelativePath Path)
{
    /// <summary>The file name RetroBat expects, which is not the name RomM serves.</summary>
    public string FileName => Path.Name;

    /// <summary>
    /// True when nothing can be said about this file in either direction.
    /// </summary>
    /// <remarks>
    /// 179 of the 353 entries, across 49 systems, and 28 systems have nothing else. Such a
    /// file can be neither found in RomM nor recognised on disk, so it is reported as
    /// unverifiable and never as missing from the user's library.
    /// </remarks>
    public bool IsUnverifiable => Md5 is null;
}

/// <summary>
/// <c>data/retrobat/bios.json</c>: RetroBat's own BIOS requirements, per system.
/// </summary>
/// <remarks>
/// <b>Bundled rather than read from the install, and that is measured rather than
/// preferred.</b> A real RetroBat 8.2 install contains no <c>batocera-systems.json</c>: the
/// data is a .NET string resource inside <c>emulationstation/batocera-systems.exe</c>, byte
/// for byte the vendored copy apart from a trailing newline. So the <c>es_systems.cfg</c>
/// precedent, read the live copy and treat the vendored one as a template, has nothing to
/// read, and reaching into an executable would bind RomMBat to one build's resource layout.
/// <para>
/// <b>The destination path is the key, and the md5 is not.</b> Six required md5s name more
/// than one destination (<c>coleco.rom</c>, <c>colecovision.rom</c> and
/// <c>openMSX/share/systemroms/coleco.rom</c> are the same bytes), while no destination ever
/// takes two md5s.
/// </para>
/// <para>
/// <b>Everything lands under <c>bios/</c>, and that is enforced here.</b> The seven manifest
/// entries that name <c>emulators/</c> paths are all hashless and therefore unreachable
/// through an md5 join, so refusing them costs nothing today and stops a future manifest
/// entry writing into an emulator's install directory.
/// </para>
/// </remarks>
public sealed class BiosManifest
{
    /// <summary>The one tree RomMBat writes firmware into.</summary>
    public const string RequiredPrefix = "bios/";

    private const string ResourceName = "RomMBat.Core.data.retrobat.bios.json";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly FrozenDictionary<string, IReadOnlyList<BiosRequirement>> _byFolder;

    private BiosManifest(
        FrozenDictionary<string, IReadOnlyList<BiosRequirement>> byFolder,
        IReadOnlyList<BiosRequirement> requirements,
        IReadOnlyList<string> rejected)
    {
        _byFolder = byFolder;
        Requirements = requirements;
        RejectedPaths = rejected;
    }

    /// <summary>The shipped manifest, read once.</summary>
    public static BiosManifest Bundled { get; } = LoadEmbedded();

    /// <summary>Every requirement of every system, in manifest order.</summary>
    public IReadOnlyList<BiosRequirement> Requirements { get; }

    /// <summary>
    /// Manifest paths refused for landing outside <c>bios/</c>, kept so a drift is visible.
    /// </summary>
    public IReadOnlyList<string> RejectedPaths { get; }

    /// <summary>Every RetroBat folder the manifest has requirements for.</summary>
    public IReadOnlyCollection<string> Folders => _byFolder.Keys;

    /// <summary>Reads a manifest from JSON. Tests use this; shipped code uses <see cref="Bundled"/>.</summary>
    public static BiosManifest Parse(Stream json)
    {
        ArgumentNullException.ThrowIfNull(json);

        var document = JsonSerializer.Deserialize<ManifestDocument>(json, SerializerOptions)
            ?? throw new InvalidOperationException("bios.json is empty.");

        var systems = new Dictionary<string, IReadOnlyList<BiosRequirement>>(StringComparer.OrdinalIgnoreCase);
        var requirements = new List<BiosRequirement>();
        var rejected = new List<string>();

        foreach (var (key, body) in document.Systems)
        {
            var folder = string.IsNullOrWhiteSpace(body.Folder) ? key : body.Folder;
            var files = new List<BiosRequirement>(body.Files.Count);

            foreach (var file in body.Files)
            {
                if (!IsInsideBios(file.Path) || !RelativePath.TryCreate(file.Path, out var path))
                {
                    rejected.Add(file.Path);
                    continue;
                }

                files.Add(new BiosRequirement(key, folder, Normalize(file.Md5), path));
            }

            // A system can share a folder with another (nothing does today), so the files
            // merge rather than the later system replacing the earlier one.
            systems[folder] = systems.TryGetValue(folder, out var existing)
                ? [.. existing, .. files]
                : files;

            requirements.AddRange(files);
        }

        return new BiosManifest(
            systems.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
            requirements,
            rejected);
    }

    /// <summary>What one RetroBat folder needs. Empty when the manifest names no BIOS for it.</summary>
    public IReadOnlyList<BiosRequirement> For(string? folder) =>
        !string.IsNullOrWhiteSpace(folder) && _byFolder.TryGetValue(folder, out var files) ? files : [];

    /// <summary>True when a manifest path is one RomMBat may write.</summary>
    /// <remarks>
    /// Case-insensitive because Windows filesystems are, and because the manifest spells one
    /// subtree <c>bios/openMSX/</c>.
    /// </remarks>
    public static bool IsInsideBios(string? path) =>
        path is not null
        && path.Replace('\\', '/').StartsWith(RequiredPrefix, StringComparison.OrdinalIgnoreCase);

    private static string? Normalize(string? md5) =>
        string.IsNullOrWhiteSpace(md5) ? null : md5.Trim().ToLowerInvariant();

    private static BiosManifest LoadEmbedded()
    {
        var assembly = typeof(BiosManifest).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"'{ResourceName}' is missing from the assembly. It is embedded by RomMBat.Core.csproj "
                    + "and generated by tools/build-bios-manifest.py.");

        return Parse(stream);
    }

    private sealed class ManifestDocument
    {
        [JsonPropertyName("systems")]
        public Dictionary<string, SystemDocument> Systems { get; init; } = [];
    }

    private sealed class SystemDocument
    {
        [JsonPropertyName("folder")]
        public string? Folder { get; init; }

        [JsonPropertyName("files")]
        public List<FileDocument> Files { get; init; } = [];
    }

    private sealed class FileDocument
    {
        [JsonPropertyName("md5")]
        public string? Md5 { get; init; }

        [JsonPropertyName("path")]
        public string Path { get; init; } = string.Empty;
    }
}
