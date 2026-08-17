using RomMBat.Core.RetroBat;

namespace RomMBat.Tests.Support;

/// <summary>Where the checked-in fixtures are, once the build has copied them next to the tests.</summary>
internal static class Fixtures
{
    /// <summary>
    /// RetroBat 8.2.0's shipped <c>es_systems.cfg</c>.
    /// </summary>
    /// <remarks>
    /// The upstream template, linked from <c>reference/</c> rather than copied, and used here
    /// because it carries every parser trap a live file does: five systems whose
    /// <c>&lt;name&gt;</c> differs from their folder, one <c>&lt;name&gt;</c> used twice, four
    /// entries pointing outside <c>roms/</c> and one with no path at all.
    /// <para>
    /// Shipped code must still read the live file. This is a fixture, not a substitute.
    /// </para>
    /// </remarks>
    public static string EsSystemsTemplate => Path("es_systems.template.cfg");

    /// <summary>A live 8.2.0 install's parsed <c>es_systems.cfg</c>, from M0 probe 4.</summary>
    public static string LiveEsSystems => Path("es_systems.live.json");

    /// <summary>
    /// RetroBat 8.2.0's shipped <c>es_savestates.cfg</c>, which M0 measured byte-identical to
    /// the live copy.
    /// </summary>
    /// <remarks>
    /// Linked from <c>reference/</c> rather than copied, because every trap the state parser
    /// exists to survive is a property of these exact bytes: <c>libretro</c> declaring no slot
    /// bounds, <c>desmume</c> declaring <c>&lt;image&gt;</c> identical to <c>&lt;file&gt;</c>,
    /// <c>bigpemu</c>'s three-digit bounds against a two-digit template, and two entries whose
    /// directory is core-scoped.
    /// </remarks>
    public static string EsSaveStatesTemplate => Path("es_savestates.template.cfg");

    /// <summary>Parses the shipped save-state schema.</summary>
    public static SaveStateSchema LoadSaveStates()
    {
        using var stream = File.OpenRead(EsSaveStatesTemplate);
        return SaveStateSchema.Parse(stream);
    }

    /// <summary>RetroBat's own list of system folder names.</summary>
    public static string SystemsNames => Path("systems_names.lst");

    /// <summary>The bundled platform map, as shipped.</summary>
    public static string PlatformsJson => Path("platforms.json");

    /// <summary>The bundled BIOS requirements manifest, as shipped.</summary>
    public static string BiosJson => Path("bios.json");

    /// <summary>
    /// RetroBat's own <c>batocera-systems.json</c>, which <see cref="BiosJson"/> is generated from.
    /// </summary>
    /// <remarks>
    /// Linked from <c>reference/</c> so the generator's output is checked against the same
    /// bytes the plan's numbers were derived from. A real install has no such file: the data
    /// is a string resource inside <c>emulationstation/batocera-systems.exe</c>.
    /// </remarks>
    public static string BatoceraSystemsJson => Path("batocera-systems.json");

    /// <summary>Parses the bundled BIOS manifest from the file, rather than from the assembly.</summary>
    public static BiosManifest LoadBiosManifest()
    {
        using var stream = File.OpenRead(BiosJson);
        return BiosManifest.Parse(stream);
    }

    /// <summary>
    /// Four entries lifted verbatim from a real scraped install's <c>roms/gamegear/gamelist.xml</c>.
    /// </summary>
    /// <remarks>
    /// Not synthesized, because the point is what a file RomMBat did not write actually
    /// contains. Two of the four carry <c>playcount</c>, <c>lastplayed</c> and
    /// <c>gametime</c>, all four carry <c>scrap</c> with its two attributes, <c>md5</c>,
    /// <c>cheevosHash</c> and an <c>id</c> attribute on <c>&lt;game&gt;</c>, and one carries
    /// <c>cheevosId</c>. RomMBat owns none of those.
    /// </remarks>
    public static string GamegearGamelist => Path("gamegear-gamelist.xml");

    /// <summary>Parses the shipped template.</summary>
    public static EsSystemsFile LoadEsSystems()
    {
        using var stream = File.OpenRead(EsSystemsTemplate);
        return EsSystemsFile.Parse(stream);
    }

    /// <summary>Every RetroBat system folder name.</summary>
    public static IReadOnlyList<string> LoadSystemNames() =>
        [.. File.ReadLines(SystemsNames).Select(line => line.Trim()).Where(line => line.Length > 0)];

    /// <summary>Builds an <c>es_systems.cfg</c> in memory, for a test that needs a specific shape.</summary>
    public static EsSystemsFile Synthesize(params (string Folder, string Extensions)[] systems)
    {
        var xml = string.Concat(systems.Select(system => $"""
              <system>
                <name>{system.Folder}</name>
                <fullname>{system.Folder}</fullname>
                <path>~\..\roms\{system.Folder}</path>
                <extension>{system.Extensions}</extension>
              </system>
            """));

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"<systemList>{xml}</systemList>"));
        return EsSystemsFile.Parse(stream);
    }

    private static string Path(string name) =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "fixtures", name);
}
