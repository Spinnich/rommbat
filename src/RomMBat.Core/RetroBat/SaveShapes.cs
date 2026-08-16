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
/// </param>
public sealed record SaveShape(
    string System,
    IReadOnlyList<SaveShapeClass> Classes,
    string Evidence,
    bool DependsOnEmulator)
{
    /// <summary>True when any declared class is one stage 1 can carry.</summary>
    public bool HasSyncableClass => Classes.Any(value => value is SaveShapeClass.A or SaveShapeClass.B);
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
                entry.Value.ShapeDependsOnEmulator),
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
