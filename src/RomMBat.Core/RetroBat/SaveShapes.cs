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

/// <summary>What a battery save's file stem joins on to find its ROM.</summary>
public enum BatteryNaming
{
    /// <summary>The ROM file's own stem, which the <c>(folder, stem)</c> index answers.</summary>
    RomFile,

    /// <summary>
    /// The emulator's own title for the game, which no index answers and has to be learned.
    /// BizHawk is the measured case: <c>StarTropics (USA).zip</c> produced
    /// <c>StarTropics.SaveRAM</c>, and <c>Phantasy Star (Brazil).zip</c> produced
    /// <c>Phantasy Star (B).SaveRAM</c>, so no rule over the ROM name recovers it.
    /// </summary>
    DisplayName,
}

/// <summary>Which files in one directory are one emulator's battery saves.</summary>
/// <param name="Emulator">Who writes them, which is also the first half of the slot.</param>
/// <param name="Directory">
/// Relative to <c>saves/&lt;system&gt;/</c>, forward-slashed. Empty is the loose level.
/// </param>
/// <param name="Extensions">Lower-cased, with the dot.</param>
/// <param name="Class">
/// The class the files are, or null to take it from the system's declaration. Null only makes
/// sense at the loose level, where megacd's per-game <c>.brm</c> is class B and nes's
/// <c>.srm</c> is class A under one rule.
/// </param>
/// <param name="Systems">The systems it applies to, or null for every system with a shape.</param>
/// <param name="NotASave">
/// Extensions the emulator writes beside its saves that are not saves, lower-cased. BizHawk
/// leaves <c>StarTropics.SaveRAM.bak</c>, its copy of the save a new one replaced, and counting
/// it told a reader the directory held a shape nothing covers.
/// </param>
public sealed record BatteryRule(
    string Emulator,
    string Directory,
    FrozenSet<string> Extensions,
    BatteryNaming NamedAfter,
    SaveShapeClass? Class,
    FrozenSet<string>? Systems,
    string Evidence,
    FrozenSet<string> NotASave)
{
    /// <summary>True when the rule reads the files loose directly under the system folder.</summary>
    public bool IsLoose => Directory.Length == 0;

    public bool AppliesTo(string system) => Systems is null || Systems.Contains(system);

    public bool Carries(string extension) => Extensions.Contains(extension.ToLowerInvariant());

    /// <summary>
    /// True when a binding key is one of this rule's file names, which is how a display-name save
    /// is bound: <c>saves bind nes StarTropics.SaveRAM &lt;rom id&gt;</c>.
    /// </summary>
    /// <remarks>
    /// The file name rather than a path, because <c>game_id_binding</c> refuses a key holding a
    /// separator. The extension is what keeps it apart from a class C key under the same system,
    /// which is a bare identifier such as <c>ULES01513</c>.
    /// </remarks>
    public bool IsBindingKey(string key) =>
        Path.GetFileNameWithoutExtension(key).Length > 0 && Carries(Path.GetExtension(key));

    /// <summary>True when a slot is one this rule's saves are uploaded under.</summary>
    public bool OwnsSlot(string? slot) =>
        slot is not null
        && (string.Equals(slot, $"{Emulator}:battery", StringComparison.OrdinalIgnoreCase)
            || slot.StartsWith($"{Emulator}:battery:", StringComparison.OrdinalIgnoreCase));

    internal bool Overlaps(BatteryRule other) =>
        Systems is null || other.Systems is null || Systems.Overlaps(other.Systems);
}

/// <summary>How a system's shared container can be made per-game, where it can.</summary>
/// <param name="Option">The <c>es_settings.cfg</c> key, e.g. <c>pcsx2_slot1_memory</c>.</param>
/// <param name="SetTo">The value to write. Null where the declaration says not to convert.</param>
/// <param name="KeysBy">
/// What the converted container is named after, and <b>the discriminator that decides whether
/// this release can offer the conversion at all</b>. <c>rom stem</c> means the result drops
/// into ordinary filename attribution; <c>disc serial</c> and <c>game code</c> mean it comes out
/// identifier-keyed and needs the Game-ID routes, which is a different piece of work.
/// </param>
/// <param name="Apply">
/// False where the measured answer is to leave the stock setting alone. <c>psx</c> is the
/// worked case: stock <c>PerGameTitle</c> binds a multi-disc set through DuckStation's own
/// database, and the conversion that looks like an improvement is the regression.
/// </param>
/// <param name="Container">
/// Where the converted container lands, relative to <c>saves/&lt;system&gt;/</c>. Null where the
/// layout has not been measured, which is what keeps an unmeasured tree reported rather than
/// walked under a guessed rule.
/// </param>
/// <param name="Extension">The converted container's extension, which replaces the ROM's.</param>
/// <param name="Emulator">Who writes it, for the slot and the report.</param>
/// <param name="Slot">The slot suffix, so the pair is <c>{Emulator}:{Slot}</c>.</param>
public sealed record PerGameConversion(
    string Option,
    string? SetTo,
    string KeysBy,
    bool Apply,
    string Note,
    string? Container = null,
    string? Extension = null,
    string? Emulator = null,
    string? Slot = null)
{
    /// <summary>True when the converted container's location and naming are both known.</summary>
    /// <remarks>
    /// Both halves, for the reason <see cref="SaveShape.HasUnitPaths"/> needs both: a
    /// conversion whose result has never been seen on disk must be discovered by measurement
    /// rather than by guessing at a plausible directory.
    /// </remarks>
    public bool IsDiscoverable =>
        YieldsRomNamedContainer
        && !string.IsNullOrWhiteSpace(Container)
        && !string.IsNullOrWhiteSpace(Extension)
        && !string.IsNullOrWhiteSpace(Emulator)
        && !string.IsNullOrWhiteSpace(Slot);

    /// <summary>True when converting produces a container named after the ROM file.</summary>
    /// <remarks>
    /// The only shape this release converts, because it is the only one whose result is
    /// attributable by the filename index that already exists. Anything identifier-keyed is
    /// reported with its reason rather than half-supported.
    /// </remarks>
    public bool YieldsRomNamedContainer =>
        Apply
        && SetTo is not null
        && string.Equals(KeysBy, "rom stem", StringComparison.OrdinalIgnoreCase);
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
/// a loose <c>.srm</c> is libretro's by its <see cref="BatteryRule"/>, and
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
    private readonly IReadOnlyList<BatteryRule> _batteryRules;
    private readonly FrozenDictionary<string, FrozenDictionary<string, string>> _sharedContainers;
    private readonly FrozenSet<string> _notASaveExtensions;

    private SaveShapes(
        FrozenDictionary<string, SaveShape> shapes,
        IReadOnlyList<BatteryRule> batteryRules,
        FrozenDictionary<string, FrozenDictionary<string, string>> sharedContainers,
        FrozenSet<string> notASaveExtensions,
        IReadOnlyList<string> unclassified)
    {
        _shapes = shapes;
        _batteryRules = batteryRules;
        _sharedContainers = sharedContainers;
        _notASaveExtensions = notASaveExtensions;
        Unclassified = unclassified;

        LooseEmulator = batteryRules.FirstOrDefault(rule => rule.IsLoose && rule.Systems is null)?.Emulator
            ?? throw new InvalidOperationException(
                "save_rules.json declares no battery rule for the loose level of every system.");
    }

    /// <summary>The shipped tables, read once.</summary>
    public static SaveShapes Bundled { get; } = LoadEmbedded();

    /// <summary>
    /// The emulator whose rule covers the loose level of every system, which is libretro.
    /// </summary>
    /// <remarks>
    /// A fallback for a restored save whose row and server copy both name no emulator, and
    /// nothing else. Which emulator wrote a file is <see cref="BatteryRuleFor"/>'s answer, keyed
    /// on the system, the directory and the extension: a loose <c>.sav</c> on <c>nes</c> is
    /// mesen standalone's or mednafen's, and taking this value for it would give it
    /// <c>libretro:battery</c> and collide with libretro's own <c>.srm</c> for the same ROM (#152).
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

    /// <summary>
    /// The rule that claims a file with this extension in this directory, or null when none does.
    /// </summary>
    /// <param name="directory">Relative to <c>saves/&lt;system&gt;/</c>; empty for the loose level.</param>
    /// <remarks>
    /// <b>At most one can answer, and loading refuses a table where two could.</b> One extension
    /// list and one loose emulator for every system was the shape this replaced, and it is why a
    /// second emulator's loose save could not be carried: adding mesen's <c>.sav</c> to the list
    /// would have given it libretro's slot (#152).
    /// </remarks>
    public BatteryRule? BatteryRuleFor(string system, string directory, string extension) =>
        _batteryRules.FirstOrDefault(rule =>
            rule.AppliesTo(system)
            && string.Equals(rule.Directory, directory, StringComparison.OrdinalIgnoreCase)
            && rule.Carries(extension));

    /// <summary>The rules for an emulator's own subdirectory under a system, in table order.</summary>
    public IEnumerable<BatteryRule> BatteryRulesBelow(string system) =>
        _batteryRules.Where(rule => !rule.IsLoose && rule.AppliesTo(system));

    /// <summary>The subdirectory rule whose saves go up under this slot, or null.</summary>
    /// <remarks>
    /// Loose rules are left out, because a loose save's destination is the ROM's own folder and
    /// stem and needs no rule to find it.
    /// </remarks>
    public BatteryRule? BatteryRuleForSlot(string system, string? slot) =>
        BatteryRulesBelow(system).FirstOrDefault(rule => rule.OwnsSlot(slot));

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

        return Parse(Read(assembly, ShapesResource), Read(assembly, RulesResource));
    }

    /// <summary>Reads both tables, refusing a battery table in which two rules could answer.</summary>
    internal static SaveShapes Parse(string shapesJson, string rulesJson)
    {
        var shapes = JsonSerializer.Deserialize<ShapesDocument>(shapesJson, SerializerOptions)
            ?? throw new InvalidOperationException("The bundled save_shapes.json could not be read.");
        var rules = JsonSerializer.Deserialize<RulesDocument>(rulesJson, SerializerOptions)
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
            ParseBatteryRules(rules.BatterySaves),
            rules.SharedContainers.ToFrozenDictionary(
                entry => entry.Key,
                entry => entry.Value.ToFrozenDictionary(
                    inner => inner.Key,
                    inner => inner.Value,
                    StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase),
            rules.NotASaveExtensions.Keys.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
            shapes.Unclassified);
    }

    /// <summary>
    /// Reads the battery rules, and refuses a table in which a file or a slot has two owners.
    /// </summary>
    /// <remarks>
    /// <b>Two checks, and each is a collision this table exists to make impossible.</b> Two rules
    /// claiming one extension in one directory of one system would give a file two emulators, and
    /// two rules naming one emulator on one system would put two files in one slot. Both fail at
    /// load, which every test run does, rather than surfacing as one save quietly replacing another.
    /// </remarks>
    private static List<BatteryRule> ParseBatteryRules(List<BatteryRuleEntry> entries)
    {
        var parsed = entries
            .Select(entry => new BatteryRule(
                Blank(entry.Emulator) ?? throw new InvalidOperationException(
                    "A battery rule in save_rules.json names no emulator."),
                (entry.Directory ?? string.Empty).Replace('\\', '/').Trim('/'),
                entry.Extensions.Select(extension => extension.ToLowerInvariant()).ToFrozenSet(StringComparer.Ordinal),
                ParseNaming(entry.NamedAfter),
                Blank(entry.Class) is { } shapeClass ? ParseClasses(shapeClass)[0] : null,
                entry.Systems?.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                entry.Evidence ?? string.Empty,
                entry.NotASave.Keys.Select(extension => extension.ToLowerInvariant()).ToFrozenSet(StringComparer.Ordinal)))
            .ToList();

        for (var i = 0; i < parsed.Count; i++)
        {
            for (var j = i + 1; j < parsed.Count; j++)
            {
                var (first, second) = (parsed[i], parsed[j]);

                if (!first.Overlaps(second))
                {
                    continue;
                }

                if (string.Equals(first.Emulator, second.Emulator, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"save_rules.json gives {first.Emulator} two battery rules on one system, "
                            + $"so both would upload under {first.Emulator}:battery.");
                }

                if (string.Equals(first.Directory, second.Directory, StringComparison.OrdinalIgnoreCase)
                    && first.Extensions.Overlaps(second.Extensions))
                {
                    throw new InvalidOperationException(
                        $"save_rules.json lets {first.Emulator} and {second.Emulator} both claim "
                            + $"{string.Join(", ", first.Extensions.Intersect(second.Extensions))} "
                            + $"in saves/<system>/{first.Directory}.");
                }
            }
        }

        return parsed;
    }

    private static BatteryNaming ParseNaming(string? value) => value switch
    {
        "rom file" => BatteryNaming.RomFile,
        "display name" => BatteryNaming.DisplayName,
        _ => throw new InvalidOperationException(
            $"save_rules.json names a battery save after '{value}', which this build cannot join."),
    };

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
                entry.Note ?? string.Empty,
                Blank(entry.Container?.Replace('\\', '/').Trim('/')),
                Blank(entry.Extension),
                Blank(entry.Emulator),
                Blank(entry.Slot));

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

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

        [JsonPropertyName("container")]
        public string? Container { get; init; }

        [JsonPropertyName("extension")]
        public string? Extension { get; init; }

        [JsonPropertyName("emulator")]
        public string? Emulator { get; init; }

        [JsonPropertyName("slot")]
        public string? Slot { get; init; }
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
        [JsonPropertyName("battery_saves")]
        public List<BatteryRuleEntry> BatterySaves { get; init; } = [];

        [JsonPropertyName("not_a_save_extensions")]
        public Dictionary<string, string> NotASaveExtensions { get; init; } = [];

        [JsonPropertyName("shared_containers")]
        public Dictionary<string, Dictionary<string, string>> SharedContainers { get; init; } = [];
    }

    private sealed record BatteryRuleEntry
    {
        [JsonPropertyName("emulator")]
        public string? Emulator { get; init; }

        [JsonPropertyName("systems")]
        public List<string>? Systems { get; init; }

        [JsonPropertyName("directory")]
        public string? Directory { get; init; }

        [JsonPropertyName("extensions")]
        public List<string> Extensions { get; init; } = [];

        [JsonPropertyName("named_after")]
        public string? NamedAfter { get; init; }

        [JsonPropertyName("class")]
        public string? Class { get; init; }

        [JsonPropertyName("evidence")]
        public string? Evidence { get; init; }

        [JsonPropertyName("not_a_save_extensions")]
        public Dictionary<string, string> NotASave { get; init; } = [];
    }
}
