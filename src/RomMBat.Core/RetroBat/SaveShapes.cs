using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RomMBat.Core.RetroBat;

/// <summary>How a system's saves are laid out on disk.</summary>
public enum SaveShapeClass
{
    /// <summary>No shape definition covers this system, so nothing may be assumed.</summary>
    Unknown,

    /// <summary>One file per game. Direct 1:1 onto a RomM save.</summary>
    A,

    /// <summary>Several files per game. One slot per file.</summary>
    B,

    /// <summary>A directory per game, keyed by an internal game ID. Stage 2.</summary>
    C,

    /// <summary>One container shared by many games, so it has no rom to belong to.</summary>
    D,
}

/// <summary>How a system's shared container can be made per-game, where it can.</summary>
/// <param name="Option">The <c>es_settings.cfg</c> key, e.g. <c>pcsx2_slot1_memory</c>.</param>
/// <param name="SetTo">The value to write. Null where the declaration says not to convert.</param>
/// <param name="KeysBy">
/// What the converted container is named after, and <b>the discriminator that decides whether
/// this release can offer the conversion at all</b>. <c>rom basename</c> means the result drops
/// into ordinary filename attribution; <c>disc serial</c> and <c>game code</c> mean it comes out
/// identifier-keyed and needs the Game-ID routes, which is a different piece of work.
/// </param>
/// <param name="Apply">
/// False where the measured answer is to leave the stock setting alone. <c>psx</c> is the
/// worked case: stock <c>PerGameTitle</c> binds a multi-disc set through DuckStation's own
/// database, and the conversion that looks like an improvement is the regression.
/// </param>
public sealed record PerGameConversion(string Option, string? SetTo, string KeysBy, bool Apply, string Note)
{
    /// <summary>
    /// True when converting produces a container named after the ROM file.
    /// </summary>
    /// <remarks>
    /// The only shape this release converts, because it is the only one whose result is
    /// attributable by the filename index that already exists. Anything identifier-keyed is
    /// reported with its reason rather than half-supported.
    /// </remarks>
    public bool YieldsRomNamedContainer =>
        Apply
        && SetTo is not null
        && string.Equals(KeysBy, "rom basename", StringComparison.OrdinalIgnoreCase);
}

/// <summary>What is known about one system's saves.</summary>
/// <param name="Classes">
/// Usually one. <c>megacd</c> is declared <c>BD</c>, per-game <c>.brm</c> and <c>.srm</c>
/// beside a shared <c>4Mbit_cart.brm</c>, so a system can be two classes at once and the file
/// decides which applies.
/// </param>
/// <param name="DependsOnEmulator">
/// True where the shape is a property of <c>(system, emulator)</c> rather than of the system.
/// <c>psx</c> is the worked example: libretro writes a loose <c>.srm</c> and DuckStation
/// writes a database-named memory card, and they share nothing.
/// <para>
/// <b>Nothing branches on this yet, and nothing needs to.</b> Discovery is path-based rather
/// than shape-based, so both halves of <c>psx</c> already come out right without consulting it:
/// a loose <c>.srm</c> is libretro's by <see cref="SaveShapes.LooseEmulator"/>, and
/// <c>saves/psx/duckstation/memcards/</c> is a subdirectory and is reported as a shape this
/// release does not carry. The flag exists for the stage that reads a memory card, where the
/// class alone stops being enough to know what a file is.
/// </para>
/// </param>
/// <param name="UnitPaths">
/// Where this system's class C save units live. Empty for every class A, B and D system, and
/// empty for a class C system whose layout has not been measured, which is what makes an
/// unmeasured tree report as unknown rather than get walked under a guessed rule.
/// </param>
/// <param name="Conversion">
/// How this system's shared container can be made per-game, or null where no lever exists.
/// Present for the four systems that declare one, which is not the same as four this release
/// converts: see <see cref="PerGameConversion.YieldsRomNamedContainer"/>.
/// </param>
public sealed record SaveShape(
    string System,
    IReadOnlyList<SaveShapeClass> Classes,
    string Evidence,
    bool DependsOnEmulator,
    IReadOnlyList<SaveUnitPath> UnitPaths,
    PerGameConversion? Conversion)
{
    /// <summary>True when any declared class is a battery shape this build carries.</summary>
    public bool HasSyncableClass => Classes.Any(value => value is SaveShapeClass.A or SaveShapeClass.B);

    /// <summary>True when this system declares class C and somewhere to look for it.</summary>
    /// <remarks>
    /// Both halves are needed. A system declared class C with no measured container is exactly
    /// the case that must report rather than guess: the cost of picking a plausible directory
    /// is hashing an emulator's whole data root, which was measured at 426.07 s.
    /// </remarks>
    public bool HasUnitPaths => Classes.Contains(SaveShapeClass.C) && UnitPaths.Count > 0;
}

/// <summary>
/// The bundled description of where saves live and which files are which.
/// </summary>
/// <remarks>
/// <b>Bundled, and the reasoning is not M5's even though the outcome matches.</b> M5 bundled
/// the BIOS manifest because a real install contains no readable copy of it. Here there is no
/// live file at all describing battery-save shapes: <c>es_savestates.cfg</c> covers states and
/// nothing covers the rest. So the shapes are shipped.
/// <para>
/// <b>But the tree they describe belongs to the user's emulators, not to RomMBat</b>, so a
/// shape is a claim to check against disk and never an authority over it. Anything the shapes
/// do not name is reported as unknown, never guessed at and never touched. That is the same
/// fail-closed rule <c>SaveGuard</c> already sets, applied one level earlier.
/// </para>
/// <para>
/// <b>Two files, because they answer different questions.</b> <c>save_shapes.json</c> says
/// what class a system is, generated during M0 from a real install. <c>save_rules.json</c>
/// says which files under <c>saves/</c> are that class, which the class alone cannot: megacd's
/// shared <c>4Mbit_cart.brm</c> sits beside per-game <c>.brm</c> files at the same level and
/// only the name separates them, and xbox's two class-D files are loose under the system
/// folder where class A normally lives.
/// </para>
/// </remarks>
public sealed class SaveShapes
{
    private const string ShapesResource = "RomMBat.Core.data.retrobat.save_shapes.json";
    private const string RulesResource = "RomMBat.Core.data.retrobat.save_rules.json";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly FrozenDictionary<string, SaveShape> _shapes;
    private readonly FrozenSet<string> _batteryExtensions;
    private readonly FrozenDictionary<string, FrozenDictionary<string, string>> _sharedContainers;
    private readonly FrozenSet<string> _notASaveExtensions;

    private SaveShapes(
        FrozenDictionary<string, SaveShape> shapes,
        FrozenSet<string> batteryExtensions,
        FrozenDictionary<string, FrozenDictionary<string, string>> sharedContainers,
        FrozenSet<string> notASaveExtensions,
        string looseEmulator,
        IReadOnlyList<string> unclassified)
    {
        _shapes = shapes;
        _batteryExtensions = batteryExtensions;
        _sharedContainers = sharedContainers;
        _notASaveExtensions = notASaveExtensions;
        LooseEmulator = looseEmulator;
        Unclassified = unclassified;
    }

    /// <summary>The shipped tables, read once.</summary>
    public static SaveShapes Bundled { get; } = LoadEmbedded();

    /// <summary>
    /// The emulator that wrote a file sitting loose directly under <c>saves/&lt;system&gt;/</c>.
    /// </summary>
    /// <remarks>
    /// A structural fact rather than a guess, and it is what makes the slot for a class A save
    /// stable. Every standalone emulator gets its own <c>saves/&lt;system&gt;/&lt;emulator&gt;/</c>
    /// subdirectory and libretro's own state directory is
    /// <c>saves/&lt;system&gt;/libretro.&lt;core&gt;/</c>, so the loose level holds libretro
    /// battery saves and nothing else. Checked across saturn, megacd, psx, gb and twelve more.
    /// </remarks>
    public string LooseEmulator { get; }

    /// <summary>
    /// Systems M0 could not classify, tracked so the number cannot silently grow.
    /// </summary>
    /// <remarks>
    /// 21 of them, and all 21 hold content on the measured install, so this is a real gap in
    /// coverage rather than a list of systems nobody uses.
    /// </remarks>
    public IReadOnlyList<string> Unclassified { get; }

    /// <summary>How many systems carry a shape at all.</summary>
    public int Count => _shapes.Count;

    /// <summary>What is known about a system, or null when nothing is.</summary>
    public SaveShape? For(string system) =>
        _shapes.TryGetValue(system, out var shape) ? shape : null;

    /// <summary>True when the extension is one a loose battery save carries.</summary>
    public bool IsBatteryExtension(string extension) =>
        _batteryExtensions.Contains(extension.ToLowerInvariant());

    /// <summary>
    /// True when the file is something RetroBat or RetroArch writes that is not a save.
    /// </summary>
    /// <remarks>
    /// The <c>.ldci</c> is the one that matters: RetroArch's record of which disc is in the
    /// drive, whose <c>image_path</c> is an absolute path with a drive letter. Relaying it
    /// through RomM restores a dangling pointer on any install at a different root, so the
    /// save tree is treated as untrusted for portability rather than copied verbatim.
    /// </remarks>
    public bool IsNotASave(string extension) =>
        _notASaveExtensions.Contains(extension.ToLowerInvariant());

    /// <summary>
    /// Why a path is a shared container, or null when it is not one.
    /// </summary>
    /// <param name="system">The RetroBat system folder.</param>
    /// <param name="relativeToSystem">The path under it, forward-slashed.</param>
    public string? SharedContainerReason(string system, string relativeToSystem)
    {
        if (!_sharedContainers.TryGetValue(system, out var containers))
        {
            return null;
        }

        return containers.TryGetValue(relativeToSystem, out var reason) ? reason : null;
    }

    /// <summary>Every shared container declared, whether or not this install holds it.</summary>
    public int SharedContainerCount => _sharedContainers.Sum(entry => entry.Value.Count);

    /// <summary>
    /// Every shared container declared for a system, as a path under <c>saves/&lt;system&gt;/</c>.
    /// </summary>
    /// <remarks>
    /// <b>Seven of the ten declarations name a path with a separator in it</b>
    /// (<c>pcsx2/memcards/Mcd001.ps2</c>, the four Dreamcast VMUs, Kronos's backup RAM), and
    /// <see cref="SharedContainerReason"/>'s only caller asked it with a bare loose filename, so
    /// those seven could never match. The three that could are exactly the three that sit loose
    /// under the system folder. Enumerating them is what lets a container in a subdirectory be
    /// reported as the shared container it is rather than swept into a count of files nothing
    /// carries.
    /// </remarks>
    public IEnumerable<KeyValuePair<string, string>> SharedContainersFor(string system) =>
        _sharedContainers.TryGetValue(system, out var containers)
            ? containers
            : [];

    private static SaveShapes LoadEmbedded()
    {
        var assembly = typeof(SaveShapes).Assembly;

        var shapes = JsonSerializer.Deserialize<ShapesDocument>(Read(assembly, ShapesResource), SerializerOptions)
            ?? throw new InvalidOperationException("The bundled save_shapes.json could not be read.");
        var rules = JsonSerializer.Deserialize<RulesDocument>(Read(assembly, RulesResource), SerializerOptions)
            ?? throw new InvalidOperationException("The bundled save_rules.json could not be read.");

        var parsed = shapes.Shapes.ToFrozenDictionary(
            entry => entry.Key,
            entry => new SaveShape(
                entry.Key,
                ParseClasses(entry.Value.Class),
                entry.Value.Evidence ?? string.Empty,
                entry.Value.ShapeDependsOnEmulator,
                ParseUnitPaths(entry.Value.UnitPaths),
                ParseConversion(entry.Value.Conversion)),
            StringComparer.OrdinalIgnoreCase);

        return new SaveShapes(
            parsed,
            rules.BatteryExtensions.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
            rules.SharedContainers.ToFrozenDictionary(
                entry => entry.Key,
                entry => entry.Value.ToFrozenDictionary(
                    inner => inner.Key,
                    inner => inner.Value,
                    StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase),
            rules.NotASaveExtensions.Keys.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
            rules.LooseEmulator,
            shapes.Unclassified);
    }

    /// <summary>
    /// Reads a class string, which is usually one letter and sometimes two.
    /// </summary>
    /// <remarks>
    /// <c>megacd</c> is <c>BD</c>. An unrecognised letter becomes
    /// <see cref="SaveShapeClass.Unknown"/> rather than being dropped, so a future class this
    /// build does not know is reported as unsyncable instead of silently treated as class A.
    /// </remarks>
    private static IReadOnlyList<SaveShapeClass> ParseClasses(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? [SaveShapeClass.Unknown]
            : [.. value.Select(letter => letter switch
            {
                'A' or 'a' => SaveShapeClass.A,
                'B' or 'b' => SaveShapeClass.B,
                'C' or 'c' => SaveShapeClass.C,
                'D' or 'd' => SaveShapeClass.D,
                _ => SaveShapeClass.Unknown,
            })];

    /// <summary>
    /// Reads the declared class C containers, dropping any this build cannot act on.
    /// </summary>
    /// <remarks>
    /// A container with no path, no emulator, no slot or an unrecognised key kind is dropped
    /// rather than defaulted. Every default available here is a guess about where to read
    /// someone's saves from, and the shipped alternative is reporting the system as unknown.
    /// </remarks>
    private static IReadOnlyList<SaveUnitPath> ParseUnitPaths(List<UnitPathEntry> entries) =>
    [
        .. entries
            .Select(entry => (entry, key: SaveUnitPath.ParseKey(entry.Key)))
            .Where(parsed =>
                parsed.key != SaveUnitKeyKind.Unknown
                && !string.IsNullOrWhiteSpace(parsed.entry.Container)
                && !string.IsNullOrWhiteSpace(parsed.entry.Emulator)
                && !string.IsNullOrWhiteSpace(parsed.entry.Slot))
            .Select(parsed => new SaveUnitPath(
                parsed.entry.Container!.Replace('\\', '/').Trim('/'),
                parsed.entry.Emulator!,
                parsed.key,
                parsed.entry.Slot!,
                string.IsNullOrWhiteSpace(parsed.entry.Include) ? null : parsed.entry.Include,
                parsed.entry.Evidence ?? string.Empty)),
    ];

    /// <summary>
    /// Reads a declared conversion, dropping one with no option to write.
    /// </summary>
    /// <remarks>
    /// An entry with no <c>option</c> names no key, so there is nothing to set and nothing to
    /// put back. Dropped rather than defaulted, for the same reason a container with an
    /// unrecognised key kind is: every default available here is a guess about someone's
    /// configuration.
    /// </remarks>
    private static PerGameConversion? ParseConversion(ConversionEntry? entry) =>
        entry is null || string.IsNullOrWhiteSpace(entry.Option)
            ? null
            : new PerGameConversion(
                entry.Option,
                string.IsNullOrWhiteSpace(entry.SetTo) ? null : entry.SetTo,
                entry.KeysBy ?? string.Empty,
                entry.Apply ?? true,
                entry.Note ?? string.Empty);

    private static string Read(System.Reflection.Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Bundled resource '{name}' is missing from the assembly.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed record ShapesDocument
    {
        [JsonPropertyName("shapes")]
        public Dictionary<string, ShapeEntry> Shapes { get; init; } = [];

        [JsonPropertyName("_unclassified")]
        public List<string> Unclassified { get; init; } = [];
    }

    private sealed record ShapeEntry
    {
        [JsonPropertyName("class")]
        public string? Class { get; init; }

        [JsonPropertyName("evidence")]
        public string? Evidence { get; init; }

        [JsonPropertyName("shape_depends_on_emulator")]
        public bool ShapeDependsOnEmulator { get; init; }

        [JsonPropertyName("unit_paths")]
        public List<UnitPathEntry> UnitPaths { get; init; } = [];

        [JsonPropertyName("per_game_conversion")]
        public ConversionEntry? Conversion { get; init; }
    }

    private sealed record ConversionEntry
    {
        [JsonPropertyName("option")]
        public string? Option { get; init; }

        [JsonPropertyName("set_to")]
        public string? SetTo { get; init; }

        [JsonPropertyName("keys_by")]
        public string? KeysBy { get; init; }

        /// <summary>Absent means apply, because only the refusals are declared explicitly.</summary>
        [JsonPropertyName("apply")]
        public bool? Apply { get; init; }

        [JsonPropertyName("note")]
        public string? Note { get; init; }
    }

    private sealed record UnitPathEntry
    {
        [JsonPropertyName("container")]
        public string? Container { get; init; }

        [JsonPropertyName("emulator")]
        public string? Emulator { get; init; }

        [JsonPropertyName("key")]
        public string? Key { get; init; }

        [JsonPropertyName("slot")]
        public string? Slot { get; init; }

        [JsonPropertyName("include")]
        public string? Include { get; init; }

        [JsonPropertyName("evidence")]
        public string? Evidence { get; init; }
    }

    private sealed record RulesDocument
    {
        [JsonPropertyName("loose_emulator")]
        public string LooseEmulator { get; init; } = "libretro";

        [JsonPropertyName("battery_extensions")]
        public List<string> BatteryExtensions { get; init; } = [];

        [JsonPropertyName("not_a_save_extensions")]
        public Dictionary<string, string> NotASaveExtensions { get; init; } = [];

        [JsonPropertyName("shared_containers")]
        public Dictionary<string, Dictionary<string, string>> SharedContainers { get; init; } = [];
    }
}
