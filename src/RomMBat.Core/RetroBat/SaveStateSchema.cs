using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using RomMBat.Core.Paths;

namespace RomMBat.Core.RetroBat;

/// <summary>Which placeholder an emulator uses for the slot number.</summary>
/// <remarks>
/// Three exist and they are not interchangeable. <c>{{slot2d}}</c> is exactly two digits,
/// <c>{{slot0}}</c> exactly one, and <c>{{slot}}</c> is the free form only <c>libretro</c> uses.
/// The width is what stops a glob picking up a neighbour: DeSmuME declares
/// <c>{{romfilename}}.ds{{slot0}}</c> and writes its <b>battery</b> save as
/// <c>{{romfilename}}.dsv</c>, so a one-digit anchor is the difference between finding one save
/// state and uploading a battery save as slot "v".
/// </remarks>
public enum SlotToken
{
    /// <summary>No slot in the template at all, which is the autosave case.</summary>
    None,

    /// <summary><c>{{slot}}</c>, free width.</summary>
    Free,

    /// <summary><c>{{slot0}}</c>, exactly one digit.</summary>
    OneDigit,

    /// <summary><c>{{slot2d}}</c>, exactly two digits.</summary>
    TwoDigit,
}

/// <summary>
/// One <c>&lt;emulator&gt;</c> from <c>es_savestates.cfg</c>.
/// </summary>
/// <param name="Directory">
/// The declared template. <b>Declared is not written.</b> Two of the twelve emulators driven on a
/// real install write somewhere else entirely: <c>flycast</c> writes
/// <c>saves/dreamcast/reicast/states/</c> against a declared
/// <c>{{system}}/flycast/sstates</c>, and <c>openmsx</c> writes <c>bios/openmsx/savestates/</c>,
/// a different top-level tree from the declared <c>saves/msx1/openmsx</c>. Both declared
/// directories exist and are empty, so an empty declared directory means "you are looking in the
/// wrong place" and never "this game has no states".
/// </param>
/// <param name="Image">
/// The screenshot template, which maps onto RomM's optional <c>screenshotFile</c>.
/// <b>DeSmuME declares this identical to <see cref="File"/></b>, so a caller that expands both
/// and uploads what it finds uploads the state itself as its own preview. Compare the two
/// expansions before sending anything.
/// </param>
/// <param name="FirstSlot">
/// Declared bounds, absent on <c>libretro</c>. They are never the source of the slot: this
/// parser reads a slot off a filename on disk rather than expanding a range, so a missing bound
/// costs nothing.
/// <para>
/// <b><c>bigpemu</c>'s <c>001</c>/<c>999</c> against a two-digit template is not resolved by
/// that, and saying it was is what #34 is about.</b> The compiled <c>{{slot2d}}</c> expression
/// is <c>(?&lt;slot&gt;\d{2})</c>, so reading the slot off disk covers 00 to 99 and misses the
/// declared range at both ends. A three-digit name matches nowhere and is not synced;
/// <c>_state00</c> matches, is read as slot 0 below the declared floor, and is synced. Both were
/// silent, and <see cref="SaveStateTemplate.NearMiss"/> is what ended that: they are reported
/// and neither is refused, because the file on disk is evidence and the declaration is only a
/// claim. Whether BigPEmu writes a three-digit name is still unmeasured, since it is reachable
/// only through its own gamepad overlay and no Jaguar launch has been driven.
/// </para>
/// </param>
public sealed record SaveStateEmulator(
    string Name,
    string Directory,
    string File,
    string? Image,
    string? AutosaveFile,
    string? AutosaveImage,
    string? FirstSlot,
    string? LastSlot,
    IReadOnlyDictionary<string, SaveStateCore> Cores)
{
    /// <summary>True when the same game has independent state sets per core.</summary>
    /// <remarks>
    /// <c>libretro</c> (<c>{{system}}/libretro.{{core}}</c>) and <c>bizhawk</c>
    /// (<c>{{system}}/bizhawk/sstates/{{core}}</c>) both are, which is why a state's identity has
    /// to carry the core and not just the emulator.
    /// </remarks>
    public bool IsCoreScoped => Directory.Contains("{{core}}", StringComparison.Ordinal);

    /// <summary>Which slot placeholder <see cref="File"/> uses.</summary>
    public SlotToken Slot => SlotTokenOf(File);

    /// <summary>The declared bounds as numbers, where they parse.</summary>
    /// <remarks>
    /// <c>bigpemu</c> declares <c>firstslot="001"</c> and <c>lastslot="999"</c> while its
    /// template is two-digit <c>{{slot2d}}</c>, so its upper bound cannot be written by its own
    /// filename rule. The bounds are read as integers regardless of zero padding, and a slot
    /// outside them is reported rather than refused, because the file on disk is evidence and
    /// the declaration is only a claim. <see cref="SaveStateTemplate.NearMiss"/> is the caller
    /// that does the reporting.
    /// </remarks>
    public (int? First, int? Last) Bounds =>
        (ParseBound(FirstSlot), ParseBound(LastSlot));

    private static int? ParseBound(string? value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    internal static SlotToken SlotTokenOf(string template) =>
        template.Contains("{{slot2d}}", StringComparison.Ordinal) ? SlotToken.TwoDigit
        : template.Contains("{{slot0}}", StringComparison.Ordinal) ? SlotToken.OneDigit
        : template.Contains("{{slot}}", StringComparison.Ordinal) ? SlotToken.Free
        : SlotToken.None;
}

/// <summary>
/// A <c>&lt;core&gt;</c> child, which ships commented out and must still be tolerated.
/// </summary>
/// <remarks>
/// The shipped file carries the mechanism only as a sample inside an XML comment, so nothing
/// reads one today. A user can uncomment it, and the sample shows it overriding both the system
/// and the directory (<c>&lt;core name="fceumm" system="nes" directory="{{system}}"/&gt;</c>)
/// as well as disabling a core outright, so all three are carried.
/// </remarks>
public sealed record SaveStateCore(string Name, bool Enabled, string? System, string? Directory);

/// <summary>
/// The parsed <c>es_savestates.cfg</c>: where each emulator's save states live and what they are
/// called.
/// </summary>
/// <remarks>
/// <b>Parsed, never hardcoded</b>, and read from the live install so a user's edits count. The
/// shipped 8.2 file declares 13 emulators, which is what bounds state sync: not the 243 systems
/// <c>es_systems.cfg</c> declares.
/// <para>
/// <b>Discovery reverses the template rather than expanding a slot range.</b> Compiling
/// <see cref="SaveStateEmulator.File"/> into an anchored expression and matching it against what
/// is on disk reads the slot off the filename, which answers <c>libretro</c>'s trap, the one
/// entry declaring no bounds at all: nothing has to invent a default. It also settles a question
/// that is not one of the four this file is known for, whether <c>{{slot}}</c> renders as an
/// empty string at slot zero.
/// </para>
/// <para>
/// <b>It does not answer <c>bigpemu</c>'s.</b> Its declared range is <c>001</c> to <c>999</c>
/// and its template is two-digit, so reading the slot off disk answers 00 to 99 and misses that
/// declaration at both ends: a three-digit name matches no expression and is not synced, and
/// <c>_state00</c> is read as slot 0 below the declared floor and is. Neither is worked around,
/// because whether the emulator writes either name is unmeasured, and both are now reported by
/// <see cref="SaveStateTemplate.NearMiss"/> rather than passed over in silence. See #34 and #65.
/// </para>
/// </remarks>
public sealed class SaveStateSchema
{
    private readonly Dictionary<string, SaveStateEmulator> _emulators;

    private SaveStateSchema(Dictionary<string, SaveStateEmulator> emulators) => _emulators = emulators;

    /// <summary>Where the file lives, relative to the RetroBat root.</summary>
    public static RelativePath ConfigPath { get; } =
        RelativePath.Create("emulationstation/.emulationstation/es_savestates.cfg");

    /// <summary>Every declared emulator, in file order.</summary>
    public IReadOnlyCollection<SaveStateEmulator> Emulators => _emulators.Values;

    /// <summary>One emulator by name, case-insensitively.</summary>
    public SaveStateEmulator? For(string? emulator) =>
        emulator is not null && _emulators.TryGetValue(emulator, out var found) ? found : null;

    /// <summary>
    /// Which emulator, system and core a directory under <c>saves/</c> belongs to, if any.
    /// </summary>
    /// <remarks>
    /// <b>The reverse of the <c>&lt;directory&gt;</c> template, and shared rather than copied.</b>
    /// State discovery uses it to find directories worth scanning, and battery-save discovery
    /// uses it to know which subdirectories are already accounted for. Two implementations would
    /// be two chances for the two passes to disagree about what a directory is, and a
    /// disagreement there shows up as a file reported unsyncable while it is being synced.
    /// </remarks>
    /// <param name="relativeToSaves">A directory path relative to <c>saves/</c>, forward slashes.</param>
    public SaveStateDirectory? MatchDirectory(string relativeToSaves)
    {
        if (string.IsNullOrWhiteSpace(relativeToSaves))
        {
            return null;
        }

        var normalized = relativeToSaves.Replace('\\', '/').Trim('/');

        foreach (var emulator in _emulators.Values)
        {
            if (DirectoryPattern(emulator.Directory) is not { } pattern)
            {
                continue;
            }

            if (pattern.Match(normalized) is not { Success: true } match)
            {
                continue;
            }

            var core = match.Groups["core"].Success ? match.Groups["core"].Value : null;

            // A core the user turned off through the <core> mechanism is not somewhere
            // RetroBat will be writing, so it is not a state directory either.
            if (core is not null
                && emulator.Cores.TryGetValue(core, out var declared)
                && !declared.Enabled)
            {
                continue;
            }

            return new SaveStateDirectory(emulator, match.Groups["system"].Value, core);
        }

        return null;
    }

    /// <summary>
    /// Compiles a <c>&lt;directory&gt;</c> template into an expression recovering system and core.
    /// </summary>
    /// <remarks>
    /// Both captures refuse a separator, so the template's own segment boundaries decide where
    /// the system ends. Everything outside a placeholder is escaped, so no character of a real
    /// template is read as an expression.
    /// </remarks>
    private static Regex? DirectoryPattern(string template)
    {
        if (!template.Contains("{{system}}", StringComparison.Ordinal))
        {
            return null;
        }

        var pattern = string.Join(
            @"(?<system>[^/]+)",
            template
                .Trim('/')
                .Split("{{system}}", StringSplitOptions.None)
                .Select(part => string.Join(
                    @"(?<core>[^/]+)",
                    part.Split("{{core}}", StringSplitOptions.None).Select(Regex.Escape))));

        return new Regex("^" + pattern + "$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }

    /// <summary>Reads the file at a path, or null when it is not there.</summary>
    public static SaveStateSchema? Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!System.IO.File.Exists(path))
        {
            return null;
        }

        using var stream = System.IO.File.OpenRead(path);
        return Parse(stream);
    }

    /// <summary>Parses the document.</summary>
    /// <remarks>
    /// An <c>&lt;emulator&gt;</c> with no <c>&lt;file&gt;</c> is dropped rather than defaulted:
    /// with no filename rule there is nothing to recognise a state by, and inventing one is how
    /// a client uploads a file that is not a save state.
    /// </remarks>
    public static SaveStateSchema Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var document = XDocument.Load(stream);
        var emulators = new Dictionary<string, SaveStateEmulator>(StringComparer.OrdinalIgnoreCase);

        foreach (var element in document.Root?.Elements("emulator") ?? [])
        {
            var name = (string?)element.Attribute("name");
            var directory = Text(element, "directory");
            var file = Text(element, "file");

            if (string.IsNullOrWhiteSpace(name)
                || string.IsNullOrWhiteSpace(directory)
                || string.IsNullOrWhiteSpace(file))
            {
                continue;
            }

            emulators[name] = new SaveStateEmulator(
                name,
                directory,
                file,
                Text(element, "image"),
                Text(element, "autosave_file"),
                Text(element, "autosave_image"),
                (string?)element.Attribute("firstslot"),
                (string?)element.Attribute("lastslot"),
                ReadCores(element));
        }

        return new SaveStateSchema(emulators);
    }

    private static Dictionary<string, SaveStateCore> ReadCores(XElement emulator)
    {
        var cores = new Dictionary<string, SaveStateCore>(StringComparer.OrdinalIgnoreCase);

        foreach (var element in emulator.Elements("core"))
        {
            if ((string?)element.Attribute("name") is not { Length: > 0 } name)
            {
                continue;
            }

            // Absent means enabled: the sample only ever writes the attribute to turn one off.
            var enabled = (string?)element.Attribute("enabled") is not { } value
                || !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);

            cores[name] = new SaveStateCore(
                name,
                enabled,
                (string?)element.Attribute("system"),
                (string?)element.Attribute("directory"));
        }

        return cores;
    }

    private static string? Text(XElement parent, string name) =>
        parent.Element(name)?.Value is { Length: > 0 } value ? value.Trim() : null;
}

/// <summary>A directory under <c>saves/</c> that an emulator's state template claims.</summary>
/// <param name="Core">Null where the emulator is not core-scoped, never empty.</param>
public sealed record SaveStateDirectory(SaveStateEmulator Emulator, string System, string? Core);

/// <summary>
/// One expansion of an emulator's templates for a given system and core.
/// </summary>
/// <remarks>
/// Holds the directory the states are expected under and an expression that recognises one and
/// hands back the ROM stem and the slot. Built once per (emulator, system, core) rather than per
/// file.
/// </remarks>
public sealed partial class SaveStateTemplate
{
    private readonly Regex _file;
    private readonly Regex? _autosave;
    private readonly Regex? _anyWidthSlot;

    private SaveStateTemplate(
        SaveStateEmulator emulator,
        string system,
        string? core,
        RelativePath directory,
        Regex file,
        Regex? autosave,
        Regex? anyWidthSlot)
    {
        Emulator = emulator;
        System = system;
        Core = core;
        Directory = directory;
        _file = file;
        _autosave = autosave;
        _anyWidthSlot = anyWidthSlot;
    }

    public SaveStateEmulator Emulator { get; }

    public string System { get; }

    /// <summary>The core, where the emulator is core-scoped. Null otherwise, never empty.</summary>
    public string? Core { get; }

    /// <summary>The directory the templates expand to, relative to the RetroBat root.</summary>
    public RelativePath Directory { get; }

    /// <summary>
    /// Expands the directory and compiles the filename rules for one (system, core).
    /// </summary>
    /// <remarks>
    /// Returns null when the directory template needs a core and none was given, because
    /// <c>saves/snes/libretro./</c> is not a directory any emulator writes and guessing past a
    /// missing core would invent one.
    /// </remarks>
    public static SaveStateTemplate? Create(SaveStateEmulator emulator, string system, string? core)
    {
        ArgumentNullException.ThrowIfNull(emulator);
        ArgumentException.ThrowIfNullOrWhiteSpace(system);

        if (emulator.IsCoreScoped && string.IsNullOrWhiteSpace(core))
        {
            return null;
        }

        var expanded = Expand(emulator.Directory, system, core);

        // The declared directory is relative to saves/ in every shipped entry, and openmsx's
        // real location under bios/ is a known exception this does not attempt to model: it is
        // reported rather than read, because reading the wrong tree is worse than reading none.
        if (!RelativePath.TryCreate("saves/" + expanded.Trim('/'), out var directory))
        {
            return null;
        }

        var file = Compile(emulator.File, system, core);

        if (file is null)
        {
            return null;
        }

        var autosave = emulator.AutosaveFile is { } template ? Compile(template, system, core) : null;

        // Only a fixed-width token can be near-missed: {{slot}} already accepts any number of
        // digits, so there is no wider reading of it to compare against.
        var anyWidthSlot = emulator.Slot is SlotToken.TwoDigit or SlotToken.OneDigit
            ? Compile(emulator.File, system, core, anySlotWidth: true)
            : null;

        return new SaveStateTemplate(emulator, system, core, directory, file, autosave, anyWidthSlot);
    }

    /// <summary>
    /// Recognises a filename as a save state and says which ROM and slot it belongs to.
    /// </summary>
    /// <remarks>
    /// <b>The autosave rule is tried first.</b> <c>libretro</c> declares
    /// <c>{{romfilename}}.state{{slot}}</c> beside <c>{{romfilename}}.state.auto</c>, and a free
    /// slot expression matching zero digits would take <c>Game.state.auto</c> for a ROM called
    /// <c>Game.state</c> at slot zero.
    /// </remarks>
    public SaveStateMatch? Match(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (_autosave?.Match(fileName) is { Success: true } auto)
        {
            return new SaveStateMatch(auto.Groups["stem"].Value, Slot: null, IsAutosave: true, SlotText: string.Empty);
        }

        if (_file.Match(fileName) is not { Success: true } match)
        {
            return null;
        }

        var digits = match.Groups["slot"].Value;

        // A free-width token renders nothing at all for the first slot on some emulators, which
        // is why an empty capture is slot zero rather than a failure to parse.
        var slot = digits.Length == 0
            ? 0
            : int.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);

        return new SaveStateMatch(match.Groups["stem"].Value, slot, IsAutosave: false, digits);
    }

    /// <summary>
    /// A name this emulator very nearly wrote, or null when there is nothing to say about it.
    /// </summary>
    /// <remarks>
    /// <b>Two shapes, and neither is a file the scanner should be silent about.</b> A name that
    /// matches the template except for the width of its slot is a state this emulator really
    /// wrote and this client cannot read; <c>bigpemu</c> declares <c>firstslot="001"</c> and
    /// <c>lastslot="999"</c> against a two-digit <c>{{slot2d}}</c>, so 100 to 999 is
    /// unrepresentable and a three-digit name matched nothing and was dropped along with the
    /// <c>.txt</c> sidecars (#34). A slot outside the declared bounds is the other end of the
    /// same declaration: <c>_state00</c> reads as slot 0, below <c>bigpemu</c>'s floor, and was
    /// accepted in silence because <see cref="SaveStateEmulator.Bounds"/> had no caller (#65).
    /// <para>
    /// <b>Reported, never refused.</b> The file on disk is evidence and the declaration is only
    /// a claim, so an out-of-bounds slot is still recorded and still uploaded. What changes is
    /// that it is no longer invisible.
    /// </para>
    /// <para>
    /// Only the slot widens. Everything else in the expression stays anchored and escaped, so a
    /// <c>.txt</c> sidecar or a <c>.png</c> screenshot beside a state still matches nothing and
    /// is still passed over without a word, which is the rule this deliberately does not relax.
    /// </para>
    /// </remarks>
    public SaveStateNearMiss? NearMiss(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (_autosave?.IsMatch(fileName) == true)
        {
            return null;
        }

        var (first, last) = Emulator.Bounds;

        if (Match(fileName) is { IsAutosave: false, Slot: { } slot })
        {
            return (first is { } floor && slot < floor) || (last is { } ceiling && slot > ceiling)
                ? new SaveStateNearMiss(
                    fileName,
                    NearMissKind.SlotOutsideDeclaredBounds,
                    $"slot {slot} is outside the {first?.ToString(CultureInfo.InvariantCulture) ?? "?"}"
                        + $" to {last?.ToString(CultureInfo.InvariantCulture) ?? "?"} range "
                        + $"{Emulator.Name} declares. Synced anyway, since the file is evidence "
                        + "and the declaration is only a claim.")
                : null;
        }

        if (_anyWidthSlot?.Match(fileName) is not { Success: true } wide)
        {
            return null;
        }

        return new SaveStateNearMiss(
            fileName,
            NearMissKind.SlotWidth,
            $"{Emulator.Name} names slots {first?.ToString(CultureInfo.InvariantCulture) ?? "?"} to "
                + $"{last?.ToString(CultureInfo.InvariantCulture) ?? "?"} and its own filename rule "
                + $"'{Emulator.File}' cannot write slot {wide.Groups["slot"].Value}. Not synced, and "
                + "RetroBat's own es_savestates.cfg is what disagrees with itself here.");
    }

    /// <summary>The screenshot beside a state, when the emulator declares a distinct one.</summary>
    /// <remarks>
    /// <b>Null when <c>&lt;image&gt;</c> is the same template as <c>&lt;file&gt;</c></b>, which
    /// is what DeSmuME declares. Returning the state's own path here is how a client uploads a
    /// save state as its own screenshot.
    /// <para>
    /// <b><c>{{slot}}</c> renders the digits that were on disk, not the slot re-formatted.</b>
    /// The free-width token accepts zero digits and reads them as slot zero, so rendering the
    /// parsed integer back would compute <c>Game.state0.png</c> for a state called
    /// <c>Game.state</c> and find nothing. The fixed-width tokens are formatted, because a
    /// one- or two-digit capture round-trips through an int without loss and the file and image
    /// templates are not obliged to use the same token.
    /// </para>
    /// </remarks>
    public string? ImageFor(SaveStateMatch match)
    {
        ArgumentNullException.ThrowIfNull(match);

        var template = match.IsAutosave ? Emulator.AutosaveImage : Emulator.Image;
        var fileTemplate = match.IsAutosave ? Emulator.AutosaveFile : Emulator.File;

        if (template is null || string.Equals(template, fileTemplate, StringComparison.Ordinal))
        {
            return null;
        }

        return Expand(template, System, Core)
            .Replace("{{romfilename}}", match.Stem, StringComparison.Ordinal)
            .Replace("{{slot2d}}", Format(match.Slot, SlotToken.TwoDigit), StringComparison.Ordinal)
            .Replace("{{slot0}}", Format(match.Slot, SlotToken.OneDigit), StringComparison.Ordinal)
            .Replace("{{slot}}", match.SlotText, StringComparison.Ordinal);
    }

    private static string Format(int? slot, SlotToken token) => slot is not { } value
        ? string.Empty
        : token == SlotToken.TwoDigit
            ? value.ToString("D2", CultureInfo.InvariantCulture)
            : value.ToString(CultureInfo.InvariantCulture);

    private static string Expand(string template, string system, string? core) =>
        template
            .Replace("{{system}}", system, StringComparison.Ordinal)
            .Replace("{{core}}", core ?? string.Empty, StringComparison.Ordinal);

    /// <summary>
    /// Turns a filename template into an anchored expression capturing the stem and the slot.
    /// </summary>
    /// <remarks>
    /// <b>The slot's width is taken from the token, not from what happens to be on disk.</b>
    /// <c>{{slot0}}</c> compiles to exactly one digit, so DeSmuME's <c>.ds{{slot0}}</c> matches
    /// <c>Game.ds1</c> and refuses <c>Game.dsv</c>, which is its battery save.
    /// <para>
    /// The stem is lazy and everything else is escaped, so a ROM whose own name contains the
    /// literal text of the suffix still resolves: the anchor at the end is what decides.
    /// </para>
    /// </remarks>
    private static Regex? Compile(string template, string system, string? core, bool anySlotWidth = false)
    {
        var expanded = Expand(template, system, core);

        if (!expanded.Contains("{{romfilename}}", StringComparison.Ordinal))
        {
            return null;
        }

        var slotToken = SaveStateEmulator.SlotTokenOf(expanded);

        var slotPattern = anySlotWidth && slotToken is SlotToken.TwoDigit or SlotToken.OneDigit
            ? @"(?<slot>\d+)"
            : slotToken switch
            {
                SlotToken.TwoDigit => @"(?<slot>\d{2})",
                SlotToken.OneDigit => @"(?<slot>\d)",
                SlotToken.Free => @"(?<slot>\d*)",
                _ => string.Empty,
            };

        var placeholder = slotToken switch
        {
            SlotToken.TwoDigit => "{{slot2d}}",
            SlotToken.OneDigit => "{{slot0}}",
            SlotToken.Free => "{{slot}}",
            _ => null,
        };

        // Split on the two placeholders and escape everything between them, so no character of a
        // real template is ever read as an expression.
        var pattern = string.Concat(
            "^",
            string.Join(
                @"(?<stem>.+?)",
                expanded
                    .Split("{{romfilename}}", StringSplitOptions.None)
                    .Select(part => placeholder is null
                        ? Regex.Escape(part)
                        : string.Join(
                            slotPattern,
                            part.Split(placeholder, StringSplitOptions.None).Select(Regex.Escape)))),
            "$");

        return new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }
}

/// <summary>Why a name in a state directory is worth mentioning.</summary>
public enum NearMissKind
{
    /// <summary>The name matches the template except for the width of its slot.</summary>
    SlotWidth,

    /// <summary>The name matches, and the slot falls outside the declared range.</summary>
    SlotOutsideDeclaredBounds,
}

/// <summary>One name a state directory holds that neither synced cleanly nor is a sidecar.</summary>
/// <remarks>
/// The alternative to this type is silence, which is what #34 and #65 are both about. A
/// three-digit <c>bigpemu</c> name matched nothing and was dropped with the screenshots, and a
/// slot below the declared floor was accepted without a word.
/// </remarks>
public sealed record SaveStateNearMiss(string FileName, NearMissKind Kind, string Detail);

/// <summary>What a filename turned out to be.</summary>
/// <param name="Stem">
/// The ROM's filename without its extension, which is what <c>{{romfilename}}</c> expands to.
/// Measured against the checked-in launch log: <c>Patapon (Europe) (En,Fr,De,Es,It).cso</c>
/// launched and produced <c>Patapon (Europe) (En,Fr,De,Es,It)_0.ppst</c>.
/// </param>
/// <param name="Slot">Null for an autosave, which has no slot of its own.</param>
/// <param name="SlotText">
/// The digits exactly as they appeared in the filename, which is not always
/// <see cref="Slot"/> rendered back: a free-width token matches zero digits and reads as slot
/// zero, so <c>Game.state</c> and <c>Game.state0</c> parse to the same slot and only this
/// distinguishes them. Empty for an autosave.
/// </param>
public sealed record SaveStateMatch(string Stem, int? Slot, bool IsAutosave, string SlotText)
{
    /// <summary>
    /// The slot a state pairs on locally, which never travels to the server.
    /// </summary>
    /// <remarks>
    /// <b><c>POST /api/states</c> has no slot field</b>, measured against the pinned schema and
    /// against a live instance, so this is a local identity only. The three-part shape is kept
    /// even when there is no core, so the string is parseable by position rather than by counting
    /// separators.
    /// </remarks>
    public string SlotKey(string emulator, string? core) =>
        $"{emulator}:{core ?? string.Empty}:{(IsAutosave ? "auto" : Slot!.Value.ToString(CultureInfo.InvariantCulture))}";
}
