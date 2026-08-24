using System.Text;
using System.Xml;
using System.Xml.Linq;
using RomMBat.Core.Paths;

namespace RomMBat.Core.RetroBat;

/// <summary>Which of EmulationStation's three element groups a setting is written under.</summary>
/// <remarks>
/// The group is part of the file's shape rather than of the value: a real install's file holds
/// 42 <c>bool</c>, 3 <c>int</c> and 215 <c>string</c> entries, and ES sorts alphabetically
/// **within** each group when it rewrites. Emulator options are strings, including the ones
/// whose values read as numbers (<c>ps2.internalresolution</c> is <c>"4"</c>).
/// </remarks>
public enum EsSettingGroup
{
    /// <summary>A <c>&lt;string&gt;</c> element, which every emulator option is.</summary>
    Text,

    /// <summary>A <c>&lt;bool&gt;</c> element.</summary>
    Bool,

    /// <summary>An <c>&lt;int&gt;</c> element. Three of them on a real install.</summary>
    Number,
}

/// <summary>One setting as it sits in the file.</summary>
public sealed record EsSetting(string Name, EsSettingGroup Group, string Value);

/// <summary>
/// <c>es_settings.cfg</c>, the only durable place to configure an emulator.
/// </summary>
/// <remarks>
/// <b>Emulator INIs are regenerated at every launch</b> by <c>emulatorlauncher</c>
/// (<c>Pcsx2.Generator.cs</c> and its siblings write theirs from ES options), so editing one is
/// not merely against the rules, it is silently undone on the next boot. This file is the lever.
/// The precedence <c>emulatorlauncher</c> applies is
/// <c>global.&lt;key&gt;</c>, then <c>&lt;system&gt;.&lt;key&gt;</c>, then
/// <c>&lt;system&gt;["&lt;rom filename&gt;"].&lt;key&gt;</c>, each beating the one before it.
/// <para>
/// <b>ES owns this file and RomMBat is the second writer, so merge and never clobber.</b> M0
/// measured what that has to survive, and the results are gentler than the gamelist's:
/// </para>
/// <list type="bullet">
/// <item>ES rewrites the file <b>only when a setting changed that session</b>. A start-and-quit,
/// and even a session that launched a game, left it untouched to the second.</item>
/// <item>When it does rewrite, it <b>keeps keys it cannot understand</b>, a deliberate nonsense
/// key included. So the hazard is ordinary two-writer contention, not ES eating the override.</item>
/// <item>It <b>prunes any setting whose value equals its own default</b>, measured on
/// <c>Language</c>. A custom key has no default to match, but this is why
/// <see cref="Value"/> returning null must never be read as the user having reverted
/// something. Absence and revert are different states and this file cannot tell them apart.</item>
/// </list>
/// <para>
/// Rendered tab-indented with LF endings, no BOM, and a bare <c>&lt;?xml version="1.0"?&gt;</c>,
/// which is byte for byte what ES writes. A file that passes through both processes then
/// changes as little as possible, which is what makes a no-churn assertion mean anything.
/// </para>
/// </remarks>
public sealed class EsSettingsFile
{
    private const string Declaration = "<?xml version=\"1.0\"?>";

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly XElement _root;

    private EsSettingsFile(XElement root) => _root = root;

    /// <summary>Where the file lives, relative to the RetroBat root.</summary>
    /// <remarks>
    /// Relative because the install is portable and the drive letter changes. Resolved through
    /// <see cref="RetroBatInstall.Resolve(RelativePath)"/> at the point of use and never stored.
    /// </remarks>
    public static RelativePath Location { get; } =
        RelativePath.Create("emulationstation/.emulationstation/es_settings.cfg");

    /// <summary>Every setting in the file, in the order it appears.</summary>
    public IEnumerable<EsSetting> Settings =>
        _root.Elements()
            .Where(element => element.Attribute("name") is not null)
            .Select(element => new EsSetting(
                element.Attribute("name")!.Value,
                GroupOf(element.Name.LocalName),
                element.Attribute("value")?.Value ?? string.Empty));

    /// <summary>Reads the file, or an empty document when there is none.</summary>
    /// <remarks>
    /// A missing file is a real state on a fresh install and is not an error: ES writes it on
    /// its first exit that changes something. An unreadable one is, because writing a fresh
    /// document over a file that exists would discard every setting the user has.
    /// </remarks>
    public static EsSettingsFile Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            return new EsSettingsFile(new XElement("config"));
        }

        var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        return new EsSettingsFile(document.Root ?? new XElement("config"));
    }

    /// <summary>
    /// Builds the per-game override key, and refuses a name that would be ignored.
    /// </summary>
    /// <remarks>
    /// <b>The rom filename must carry its extension.</b> M0 drove both forms against a real
    /// launch: <c>ports["gong"].smooth</c> was ignored and <c>ports["gong.libretro"].smooth</c>
    /// took effect, differing in nothing else. Getting this wrong fails <b>silently</b>, with
    /// the emulator launching normally and carrying on writing to the shared container, so a
    /// stem is refused here rather than written and left to be discovered by a lost save.
    /// <para>
    /// The name comes from RomM's <c>fs_name</c>, which is the file as it lands on disk.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">The name carries no extension, or a quote.</exception>
    public static string PerGameKey(string system, string fsName, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(system);
        ArgumentException.ThrowIfNullOrWhiteSpace(fsName);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (string.IsNullOrEmpty(Path.GetExtension(fsName)))
        {
            throw new ArgumentException(
                $"'{fsName}' has no extension. A per-game key built from a stem is ignored silently "
                    + "by emulatorlauncher, so it must be built from the rom's fs_name.",
                nameof(fsName));
        }

        if (fsName.Contains('"', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"'{fsName}' contains a quote, which the per-game key form cannot express.",
                nameof(fsName));
        }

        return $"{system}[\"{fsName}\"].{key}";
    }

    /// <summary>The system-scoped key, which a per-game one outranks.</summary>
    public static string SystemKey(string system, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(system);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return $"{system}.{key}";
    }

    /// <summary>
    /// The value of a setting, or null when the file does not carry it.
    /// </summary>
    /// <remarks>
    /// <b>Null is not evidence of anything the user did.</b> ES prunes a setting whose value
    /// equals its own default, so a key can vanish from a file nobody edited. Anything that
    /// needs to know what was there before has to have written it down.
    /// </remarks>
    public string? Value(string name) => Find(name)?.Attribute("value")?.Value;

    /// <summary>True when the file carries the key at all, whatever its value.</summary>
    public bool Has(string name) => Find(name) is not null;

    /// <summary>
    /// Sets a value, adding the key when it is absent and leaving everything else alone.
    /// </summary>
    /// <remarks>
    /// An existing entry keeps its group and its position in the file, so a rewrite touches one
    /// attribute. A new one is appended, because ES re-sorts within groups on its own next
    /// rewrite and inventing a sort here would churn the file for nothing.
    /// </remarks>
    public void Set(string name, string value, EsSettingGroup group = EsSettingGroup.Text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);

        if (Find(name) is { } existing)
        {
            existing.SetAttributeValue("value", value);
            return;
        }

        _root.Add(new XElement(
            group switch
            {
                EsSettingGroup.Bool => "bool",
                EsSettingGroup.Number => "int",
                _ => "string",
            },
            new XAttribute("name", name),
            new XAttribute("value", value)));
    }

    /// <summary>Removes a key, and reports whether one was there.</summary>
    /// <remarks>
    /// This is what reverting to "the key was absent" means, and it is a different prior state
    /// from "the key held the stock value", which is why the conversion record stores which.
    /// </remarks>
    public bool Remove(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (Find(name) is not { } existing)
        {
            return false;
        }

        existing.Remove();
        return true;
    }

    /// <summary>Renders the document exactly as it would be written.</summary>
    public string Render()
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "\t",
            NewLineChars = "\n",
            OmitXmlDeclaration = true,
            Encoding = Utf8NoBom,
        };

        var builder = new StringBuilder();
        builder.Append(Declaration).Append('\n');

        using (var writer = XmlWriter.Create(builder, settings))
        {
            // Detached from any whitespace text nodes the load preserved, so the writer's own
            // indentation is what shapes the output rather than a mixture of the two.
            new XElement(
                _root.Name,
                _root.Attributes(),
                _root.Elements().Select(element => new XElement(element))).Save(writer);
        }

        builder.Append('\n');
        return builder.ToString();
    }

    /// <summary>
    /// Writes the file, and does not write when the bytes would be the same.
    /// </summary>
    /// <remarks>
    /// Temp file in the same directory plus a rename, so a power cut leaves either the old file
    /// or the new one and never half of either, and the comparison is against what is on disk
    /// rather than against anything remembered, which is the only version true after ES has had
    /// it. Skipping an identical write matters more here than for a gamelist: this file is one
    /// ES may be holding open, and not touching it at all is the safest of the outcomes.
    /// </remarks>
    /// <returns>True when bytes were written.</returns>
    public bool WriteIfChanged(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var rendered = Utf8NoBom.GetBytes(Render());

        if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(rendered))
        {
            return false;
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = path + ".rommbat-tmp";
        File.WriteAllBytes(temporary, rendered);
        File.Move(temporary, path, overwrite: true);
        return true;
    }

    /// <summary>
    /// Finds a setting by its exact name.
    /// </summary>
    /// <remarks>
    /// Ordinal rather than case-insensitive, because that is how <c>emulatorlauncher</c> reads
    /// them: a differently-cased key is a different setting to ES, and matching loosely here
    /// would have RomMBat overwrite one key while the emulator went on reading another.
    /// </remarks>
    private XElement? Find(string name) =>
        _root.Elements().FirstOrDefault(element =>
            string.Equals(element.Attribute("name")?.Value, name, StringComparison.Ordinal));

    private static EsSettingGroup GroupOf(string elementName) => elementName switch
    {
        "bool" => EsSettingGroup.Bool,
        "int" => EsSettingGroup.Number,
        _ => EsSettingGroup.Text,
    };
}
