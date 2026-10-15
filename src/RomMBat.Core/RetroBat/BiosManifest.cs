using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using RomMBat.Core.Paths;

namespace RomMBat.Core.RetroBat;

/// <summary>One file a slot accepts, and what ranks it against the others.</summary>
/// <param name="Md5">The hash RomM's firmware is joined on.</param>
/// <param name="Size">The size a source states, or null.</param>
/// <param name="Priority">
/// The emulator's own preference among files it reads, lower first, or null when it states none.
/// </param>
/// <param name="Regions">retrobios's region words: <c>north-america</c>, <c>europe</c>, <c>japan</c>, <c>world</c> and others.</param>
/// <param name="From">
/// Which source names the hash: <c>retrobat</c>, <c>profile:&lt;name&gt;</c>, <c>bizhawk</c>,
/// <c>catalog</c> or <c>romm</c>.
/// </param>
public sealed record BiosAccepted(
    string Md5,
    long? Size,
    int? Priority,
    IReadOnlyList<string> Regions,
    IReadOnlyList<string> From)
{
    /// <summary>The source that names RetroBat's own list.</summary>
    public const string RetroBat = "retrobat";

    /// <summary>True when RetroBat's list names this hash, which ranks it first.</summary>
    /// <remarks>ES's BIOS check reports the file missing for any other hash.</remarks>
    public bool IsRetroBats => From.Contains(RetroBat, StringComparer.Ordinal);
}

/// <summary>One member a reader lists inside a firmware zip.</summary>
public sealed record BiosZipMember(string Name, uint Crc32, long? Size);

/// <summary>What one or more readers require inside a zip, every member of it.</summary>
public sealed record BiosZipContents(IReadOnlyList<string> Readers, IReadOnlyList<BiosZipMember> Files);

/// <summary>One firmware slot: a destination, the files accepted there, and who reads it.</summary>
/// <param name="Folder">The RetroBat system folder, which is what everything else joins on.</param>
/// <param name="Path">Where the file belongs, relative to the RetroBat root.</param>
/// <param name="Accepted">Every file accepted here by md5, best ranked first.</param>
/// <param name="Readers">
/// The <c>emulator/core</c> rows that read it, and <c>retrobat</c> where RetroBat's own list
/// names it.
/// </param>
/// <param name="Required">True when any reader cannot run without it.</param>
/// <param name="Supplement">
/// Why RomMBat adds a file RetroBat's list names under another system, or null.
/// </param>
public sealed record BiosRequirement(
    string Folder,
    RelativePath Path,
    IReadOnlyList<BiosAccepted> Accepted,
    IReadOnlyList<string> Readers,
    bool Required,
    string? Supplement = null)
{
    /// <summary>Other names a reader accepts the same file under, in the same directory.</summary>
    public IReadOnlyList<RelativePath> Aliases { get; init; } = [];

    /// <summary>
    /// What readers require inside the file, for a zip, which is how a zip is identified (RM-28).
    /// </summary>
    public IReadOnlyList<BiosZipContents> Contents { get; init; } = [];

    /// <summary>The file name the emulators expect, which is not the name RomM serves.</summary>
    public string FileName => Path.Name;

    /// <summary>True for a zip, which is fetched by name and verified by what is inside it.</summary>
    public bool IsZip => FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when nothing can be said about this file in either direction.
    /// </summary>
    /// <remarks>
    /// A slot RetroBat names without a hash that no other source identifies. It can be neither
    /// found in RomM nor recognized on disk, so it is reported as unverifiable and never as
    /// missing from the user's library.
    /// </remarks>
    public bool IsUnverifiable => Accepted.Count == 0 && Contents.Count == 0;

    /// <summary>The md5 RetroBat's own list names here, if it names one.</summary>
    public string? RetroBatMd5 => Accepted.FirstOrDefault(accepted => accepted.IsRetroBats)?.Md5;

    /// <summary>The accepted entry for an md5, or null.</summary>
    public BiosAccepted? Accepts(string? md5) =>
        md5 is null ? null : Accepted.FirstOrDefault(accepted => string.Equals(accepted.Md5, md5, StringComparison.OrdinalIgnoreCase));
}

/// <summary>A file a system's emulators read that RomMBat never fetches, and why.</summary>
public sealed record BiosReported(string Folder, string Path, string Why, IReadOnlyList<string> Readers);

/// <summary>
/// <c>data/retrobat/bios.json</c>: every firmware slot a RetroBat system's emulators read.
/// </summary>
/// <remarks>
/// <b>Two authorities, split by question.</b> RetroBat's <c>batocera-systems.json</c> says where
/// a file goes and, for the default emulator, which hash; retrobios's emulator profiles say
/// which files and hashes every other emulator RetroBat ships accepts.
/// <c>tools/build-bios-manifest.py</c> joins them into slots, and
/// <c>docs/design/decisions/firmware-from-emulator-evidence.md</c> records why.
/// <para>
/// <b>Bundled rather than read from the install, and that is measured rather than
/// preferred.</b> A real RetroBat install contains no <c>batocera-systems.json</c>: the data is
/// a .NET string resource inside <c>emulationstation/batocera-systems.exe</c> (RB-379).
/// </para>
/// <para>
/// <b>The destination path is the key.</b> One md5 can owe several destinations
/// (<c>coleco.rom</c>, <c>colecovision.rom</c> and <c>openMSX/share/systemroms/coleco.rom</c>
/// are the same bytes), and one destination can accept several md5s, which a plan ranks.
/// </para>
/// <para>
/// <b>Everything lands under <c>bios/</c>, and that is enforced here.</b> The generator already
/// reports every path outside it rather than slotting it; refusing one here as well stops a
/// future manifest writing into an emulator's install directory.
/// </para>
/// </remarks>
public sealed class BiosManifest
{
    /// <summary>The one tree RomMBat writes firmware into.</summary>
    public const string RequiredPrefix = "bios/";

    /// <summary>The schema this reader understands.</summary>
    public const int Schema = 2;

    private const string ResourceName = "RomMBat.Core.data.retrobat.bios.json";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly FrozenDictionary<string, IReadOnlyList<BiosRequirement>> _byFolder;
    private readonly FrozenDictionary<string, IReadOnlyList<BiosReported>> _reported;

    private BiosManifest(
        FrozenDictionary<string, IReadOnlyList<BiosRequirement>> byFolder,
        FrozenDictionary<string, IReadOnlyList<BiosReported>> reported,
        IReadOnlyList<BiosRequirement> requirements,
        IReadOnlyList<string> rejected)
    {
        _byFolder = byFolder;
        _reported = reported;
        Requirements = requirements;
        RejectedPaths = rejected;
    }

    /// <summary>The shipped manifest, read once.</summary>
    public static BiosManifest Bundled { get; } = LoadEmbedded();

    /// <summary>Every slot of every system, in manifest order.</summary>
    public IReadOnlyList<BiosRequirement> Requirements { get; }

    /// <summary>
    /// Manifest paths refused for landing outside <c>bios/</c>, kept so a drift is visible.
    /// </summary>
    public IReadOnlyList<string> RejectedPaths { get; }

    /// <summary>Every RetroBat folder the manifest has a slot for.</summary>
    public IReadOnlyCollection<string> Folders => _byFolder.Keys;

    /// <summary>Reads a manifest from JSON. Tests use this; shipped code uses <see cref="Bundled"/>.</summary>
    public static BiosManifest Parse(Stream json)
    {
        ArgumentNullException.ThrowIfNull(json);

        var document = JsonSerializer.Deserialize<ManifestDocument>(json, SerializerOptions)
            ?? throw new InvalidOperationException("bios.json is empty.");

        if (document.Schema is { } schema && schema != Schema)
        {
            throw new InvalidOperationException($"bios.json is schema {schema}, and this build reads schema {Schema}.");
        }

        var systems = new Dictionary<string, IReadOnlyList<BiosRequirement>>(StringComparer.OrdinalIgnoreCase);
        var reported = new Dictionary<string, IReadOnlyList<BiosReported>>(StringComparer.OrdinalIgnoreCase);
        var requirements = new List<BiosRequirement>();
        var rejected = new List<string>();

        foreach (var (folder, body) in document.Systems)
        {
            var slots = new List<BiosRequirement>(body.Slots.Count);

            foreach (var slot in body.Slots)
            {
                if (!IsInsideBios(slot.Path) || !RelativePath.TryCreate(slot.Path, out var path))
                {
                    rejected.Add(slot.Path);
                    continue;
                }

                slots.Add(new BiosRequirement(
                    folder,
                    path,
                    [.. slot.Accepted.Select(Accepted)],
                    slot.Readers,
                    slot.Required,
                    Blank(slot.Supplement))
                {
                    Aliases = Paths(slot.Aliases),
                    Contents = [.. slot.Contents.Select(group => new BiosZipContents(
                        group.Readers,
                        [.. group.Files.Select(member => new BiosZipMember(member.Name, Crc(member.Crc32), member.Size))]))],
                });
            }

            if (slots.Count > 0)
            {
                systems[folder] = slots;
                requirements.AddRange(slots);
            }

            if (body.Reported.Count > 0)
            {
                reported[folder] = [.. body.Reported.Select(entry => new BiosReported(folder, entry.Path, entry.Why, entry.Readers))];
            }
        }

        return new BiosManifest(
            systems.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
            reported.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
            requirements,
            rejected);
    }

    /// <summary>What one RetroBat folder's emulators read. Empty when the manifest names nothing for it.</summary>
    public IReadOnlyList<BiosRequirement> For(string? folder) =>
        !string.IsNullOrWhiteSpace(folder) && _byFolder.TryGetValue(folder, out var files) ? files : [];

    /// <summary>What one folder's emulators read that RomMBat never fetches, and why.</summary>
    public IReadOnlyList<BiosReported> ReportedFor(string? folder) =>
        !string.IsNullOrWhiteSpace(folder) && _reported.TryGetValue(folder, out var files) ? files : [];

    /// <summary>True when a manifest path is one RomMBat may write.</summary>
    /// <remarks>
    /// Case-insensitive because Windows filesystems are, and because the manifest spells one
    /// subtree <c>bios/openMSX/</c>.
    /// </remarks>
    public static bool IsInsideBios(string? path) =>
        path is not null
        && path.Replace('\\', '/').StartsWith(RequiredPrefix, StringComparison.OrdinalIgnoreCase);

    private static BiosAccepted Accepted(AcceptedDocument accepted) => new(
        accepted.Md5.Trim().ToLowerInvariant(),
        accepted.Size,
        accepted.Priority,
        accepted.Region,
        accepted.From);

    private static List<RelativePath> Paths(IEnumerable<string> values)
    {
        var paths = new List<RelativePath>();

        foreach (var value in values)
        {
            if (IsInsideBios(value) && RelativePath.TryCreate(value, out var path))
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    private static uint Crc(string value) => uint.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

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
        [JsonPropertyName("_schema")]
        public int? Schema { get; init; }

        [JsonPropertyName("systems")]
        public Dictionary<string, SystemDocument> Systems { get; init; } = [];
    }

    private sealed class SystemDocument
    {
        [JsonPropertyName("slots")]
        public List<SlotDocument> Slots { get; init; } = [];

        [JsonPropertyName("reported")]
        public List<ReportedDocument> Reported { get; init; } = [];
    }

    private sealed class SlotDocument
    {
        [JsonPropertyName("path")]
        public string Path { get; init; } = string.Empty;

        [JsonPropertyName("accepted")]
        public List<AcceptedDocument> Accepted { get; init; } = [];

        [JsonPropertyName("readers")]
        public List<string> Readers { get; init; } = [];

        [JsonPropertyName("required")]
        public bool Required { get; init; }

        [JsonPropertyName("aliases")]
        public List<string> Aliases { get; init; } = [];

        [JsonPropertyName("supplement")]
        public string? Supplement { get; init; }

        [JsonPropertyName("contents")]
        public List<ContentsDocument> Contents { get; init; } = [];
    }

    private sealed class AcceptedDocument
    {
        [JsonPropertyName("md5")]
        public string Md5 { get; init; } = string.Empty;

        [JsonPropertyName("size")]
        public long? Size { get; init; }

        [JsonPropertyName("priority")]
        public int? Priority { get; init; }

        [JsonPropertyName("region")]
        public List<string> Region { get; init; } = [];

        [JsonPropertyName("from")]
        public List<string> From { get; init; } = [];
    }

    private sealed class ContentsDocument
    {
        [JsonPropertyName("readers")]
        public List<string> Readers { get; init; } = [];

        [JsonPropertyName("files")]
        public List<MemberDocument> Files { get; init; } = [];
    }

    private sealed class MemberDocument
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("crc32")]
        public string Crc32 { get; init; } = "0";

        [JsonPropertyName("size")]
        public long? Size { get; init; }
    }

    private sealed class ReportedDocument
    {
        [JsonPropertyName("path")]
        public string Path { get; init; } = string.Empty;

        [JsonPropertyName("why")]
        public string Why { get; init; } = string.Empty;

        [JsonPropertyName("readers")]
        public List<string> Readers { get; init; } = [];
    }
}
