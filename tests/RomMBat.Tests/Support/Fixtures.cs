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

    /// <summary>RetroBat's own list of system folder names.</summary>
    public static string SystemsNames => Path("systems_names.lst");

    /// <summary>The bundled platform map, as shipped.</summary>
    public static string PlatformsJson => Path("platforms.json");

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
