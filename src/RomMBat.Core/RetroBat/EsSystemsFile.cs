using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Xml.Linq;
using RomMBat.Core.Paths;

namespace RomMBat.Core.RetroBat;

/// <summary>
/// One <c>&lt;system&gt;</c> from <c>es_systems.cfg</c> that owns a folder under <c>roms/</c>.
/// </summary>
/// <param name="Name">
/// The <c>&lt;name&gt;</c> element. A display key, <b>not</b> the folder and not unique:
/// the shipped 8.2.0 file has two systems both named <c>starship</c>.
/// </param>
/// <param name="Folder">
/// The last segment of <c>&lt;path&gt;</c>, which is the folder ROMs are written to and the
/// only key the platform map joins on.
/// </param>
public sealed record EsSystem(
    string Name,
    string Folder,
    RelativePath RomsPath,
    string? FullName,
    string? Manufacturer,
    string? Hardware,
    int? ReleaseYear,
    IReadOnlyList<string> Extensions)
{
    /// <summary>
    /// True when this system can launch a file with that extension.
    /// </summary>
    /// <remarks>
    /// Takes either form, with or without the leading dot, because <c>&lt;extension&gt;</c>
    /// carries the dot and RomM's <c>fs_extension</c> does not.
    /// </remarks>
    public bool Accepts(string? extension) =>
        !string.IsNullOrWhiteSpace(extension)
        && Extensions.Contains(NormalizeExtension(extension), StringComparer.OrdinalIgnoreCase);

    /// <summary>Lowercases an extension and drops any leading dot.</summary>
    public static string NormalizeExtension(string extension) =>
        extension.Trim().TrimStart('.').ToLowerInvariant();
}

/// <summary>A <c>&lt;system&gt;</c> that is not a sync target, and why.</summary>
public sealed record EsNonRomSystem(string Name, string DeclaredPath, string Reason);

/// <summary>
/// The parsed <c>es_systems.cfg</c>: which systems exist, and what each will launch.
/// </summary>
/// <remarks>
/// Read from the live install, never from the copy vendored in <c>reference/</c>. The
/// vendored file is the shipped template; the live one reflects that machine's actual
/// emulator configuration, and <c>&lt;extension&gt;</c> is a sync filter rather than a
/// display detail.
/// <para>
/// <b>The folder is <c>&lt;path&gt;</c>, not <c>&lt;name&gt;</c>.</b> They are different
/// vocabularies and five systems in the shipped 8.2.0 file disagree: <c>gw</c> writes to
/// <c>gameandwatch</c>, <c>powerbomberman</c> to <c>pb</c>, <c>casloopy</c> to <c>loopy</c>,
/// <c>Windows</c> to <c>windows</c>, and <c>starship</c> appears twice, once for
/// <c>ghostship</c> and once for <c>starship</c>. Keying anything on <c>&lt;name&gt;</c>
/// silently loses a system and mismatches four more.
/// </para>
/// </remarks>
public sealed class EsSystemsFile
{
    /// <summary>Where the live file sits inside the tree.</summary>
    public static RelativePath Location { get; } =
        RelativePath.Create("emulationstation/.emulationstation/es_systems.cfg");

    /// <summary>
    /// What <c>~</c> expands to in a <c>&lt;path&gt;</c>.
    /// </summary>
    /// <remarks>
    /// EmulationStation is started with its home at <c>&lt;root&gt;/emulationstation</c> and
    /// creates <c>.emulationstation</c> inside it, so the ubiquitous
    /// <c>~\..\roms\&lt;folder&gt;</c> resolves to <c>&lt;root&gt;/roms/&lt;folder&gt;</c>.
    /// </remarks>
    private static readonly string[] HomeSegments = ["emulationstation"];

    private readonly FrozenDictionary<string, EsSystem> _byFolder;

    private EsSystemsFile(IReadOnlyList<EsSystem> systems, IReadOnlyList<EsNonRomSystem> nonRomSystems)
    {
        Systems = systems;
        NonRomSystems = nonRomSystems;

        // Folders compare case-insensitively because Windows filesystems do, and because the
        // shipped file spells one system 'Windows' while its folder is 'windows'.
        _byFolder = systems
            .GroupBy(system => system.Folder, StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Every system that owns a folder under <c>roms/</c>, in file order.</summary>
    public IReadOnlyList<EsSystem> Systems { get; }

    /// <summary>
    /// Entries that declare no ROM folder, kept so <c>status</c> can explain a gap.
    /// </summary>
    /// <remarks>
    /// The shipped 8.2.0 file has five: <c>library</c>, <c>screenshots</c> and <c>kodi</c>
    /// point outside <c>roms/</c>, <c>retrobat</c> is the <c>system/es_menu</c> system that
    /// carries <c>.menu</c> entries, and <c>mess</c> declares an empty path. None of them is
    /// a sync target and none should reach the mapping surface.
    /// </remarks>
    public IReadOnlyList<EsNonRomSystem> NonRomSystems { get; }

    /// <summary>The folder names, for matching a RomM <c>fs_slug</c> against.</summary>
    public IReadOnlyCollection<string> Folders => _byFolder.Keys;

    /// <summary>Reads the live file from a located install.</summary>
    /// <exception cref="EsSystemsException">The file is missing or is not readable XML.</exception>
    public static EsSystemsFile Load(RetroBatInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);

        var path = install.Resolve(Location);
        if (!File.Exists(path))
        {
            throw new EsSystemsException(
                $"No es_systems.cfg at '{Location}'. RetroBat writes it on first run, so either "
                    + "this is not a RetroBat tree or EmulationStation has never started here.");
        }

        using var stream = File.OpenRead(path);
        return Parse(stream);
    }

    /// <summary>Parses the file from a stream.</summary>
    /// <exception cref="EsSystemsException">The content is not readable XML.</exception>
    public static EsSystemsFile Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        XDocument document;
        try
        {
            document = XDocument.Load(stream, LoadOptions.None);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new EsSystemsException($"es_systems.cfg is not readable XML: {ex.Message}", ex);
        }

        var systems = new List<EsSystem>();
        var nonRom = new List<EsNonRomSystem>();

        foreach (var element in document.Descendants("system"))
        {
            var name = Text(element, "name") ?? string.Empty;
            var declaredPath = Text(element, "path");

            if (string.IsNullOrWhiteSpace(declaredPath))
            {
                nonRom.Add(new EsNonRomSystem(name, string.Empty, "declares no path"));
                continue;
            }

            if (!TryResolveRomsFolder(declaredPath, out var romsPath, out var folder))
            {
                nonRom.Add(new EsNonRomSystem(name, declaredPath, "path is not a folder under roms/"));
                continue;
            }

            systems.Add(new EsSystem(
                name,
                folder,
                romsPath,
                Text(element, "fullname"),
                Text(element, "manufacturer"),
                Text(element, "hardware"),
                ParseYear(Text(element, "release")),
                ParseExtensions(Text(element, "extension"))));
        }

        return new EsSystemsFile(systems, nonRom);
    }

    /// <summary>Finds a system by its ROM folder, case-insensitively.</summary>
    public bool TryGetFolder(string? folder, [NotNullWhen(true)] out EsSystem? system)
    {
        system = null;
        return !string.IsNullOrWhiteSpace(folder) && _byFolder.TryGetValue(folder, out system);
    }

    /// <summary>True when the install has a folder by that name.</summary>
    public bool HasFolder(string? folder) => TryGetFolder(folder, out _);

    /// <summary>
    /// Turns a declared <c>&lt;path&gt;</c> into a folder under <c>roms/</c>.
    /// </summary>
    /// <remarks>
    /// Resolves <c>~</c> and any <c>..</c> itself, because <see cref="RelativePath"/>
    /// deliberately refuses both. Anything that lands outside <c>roms/</c>, or deeper than
    /// one level inside it, is not a sync target.
    /// </remarks>
    private static bool TryResolveRomsFolder(string declaredPath, out RelativePath romsPath, out string folder)
    {
        romsPath = default;
        folder = string.Empty;

        var resolved = new List<string>();

        foreach (var raw in declaredPath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var segment = raw.Trim();

            switch (segment)
            {
                case "" or ".":
                    continue;
                case "~":
                    resolved.AddRange(HomeSegments);
                    continue;
                case "..":
                    if (resolved.Count == 0)
                    {
                        // Climbs above the RetroBat root, so it is outside the tree entirely.
                        return false;
                    }

                    resolved.RemoveAt(resolved.Count - 1);
                    continue;
                default:
                    resolved.Add(segment);
                    continue;
            }
        }

        if (resolved.Count != 2 || !string.Equals(resolved[0], "roms", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        folder = resolved[1];
        return RelativePath.TryCreate(string.Join('/', resolved), out romsPath);
    }

    private static string? Text(XElement system, string name)
    {
        var value = system.Element(name)?.Value.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static int? ParseYear(string? release) =>
        int.TryParse(release, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year) ? year : null;

    private static IReadOnlyList<string> ParseExtensions(string? extensions) =>
        extensions is null
            ? []
            : [.. extensions
                .Split([' ', '\t', '\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries)
                .Select(EsSystem.NormalizeExtension)
                .Where(extension => extension.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)];
}

/// <summary>Thrown when <c>es_systems.cfg</c> cannot be read.</summary>
public sealed class EsSystemsException : Exception
{
    public EsSystemsException(string message)
        : base(message)
    {
    }

    public EsSystemsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public EsSystemsException()
        : base("es_systems.cfg could not be read.")
    {
    }
}
