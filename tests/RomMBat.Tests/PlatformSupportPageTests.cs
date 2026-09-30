using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The guide's platform table is <c>data/certification.json</c>, and the file names the rows
/// RetroBat declares.
/// </summary>
/// <remarks>
/// Certification is per <c>(system, emulator, core)</c>, and a hand-kept status table drifts from
/// the records it summarises. The rows are data, the page is built from them, and a system in the
/// file must carry every row the vendored <c>es_systems.cfg</c> declares for it, in its order, so
/// a refresh that adds a core surfaces here as a row nobody has tested rather than going unseen.
/// </remarks>
public sealed class PlatformSupportPageTests
{
    public const string DataPath = "data/certification.json";
    public const string PagePath = "wiki/platforms/index.md";
    private const string RecordsUrl = "https://github.com/Spinnich/rommbat/tree/main/docs/platforms/";

    private static readonly Dictionary<string, string> StatusText = new(StringComparer.Ordinal)
    {
        ["certified"] = "Certified",
        ["not-certified"] = "Not certified",
        ["untested"] = "Not tested yet",
    };

    [Fact]
    public void Every_row_has_a_known_status_and_a_not_certified_row_says_why()
    {
        foreach (var row in LoadRows())
        {
            Assert.True(StatusText.ContainsKey(row.Status), $"{row}: unknown status '{row.Status}'");
            Assert.False(row.Status == "not-certified" && string.IsNullOrWhiteSpace(row.Note), $"{row}: not certified with no note");
            Assert.False(string.IsNullOrWhiteSpace(row.RomM) || string.IsNullOrWhiteSpace(row.RetroBat), $"{row}: no floor");
        }
    }

    [Fact]
    public void A_system_in_the_file_has_a_record_and_every_row_es_systems_declares_in_its_order()
    {
        var declared = DeclaredRows();

        foreach (var system in LoadRows().GroupBy(row => row.System))
        {
            Assert.True(
                File.Exists(Path.Combine(GeneratedPage.RepoRoot, "docs", "platforms", system.Key, "index.md")),
                $"{system.Key} has no record at docs/platforms/{system.Key}/index.md");
            Assert.True(declared.ContainsKey(system.Key), $"es_systems.cfg declares no system named {system.Key}");
            Assert.Equal(declared[system.Key], system.Select(row => (row.Emulator, row.Core)).ToList());
        }
    }

    [Fact]
    public void The_platform_page_is_built_from_the_certification_data()
    {
        GeneratedPage.AssertCurrent(PagePath, Render(LoadRows(), FullNames()));
    }

    private static string Render(IReadOnlyList<CertificationRow> rows, IReadOnlyDictionary<string, string> fullNames)
    {
        var systems = rows.GroupBy(row => row.System).ToList();
        var page = new StringBuilder();

        page.Append("# Platforms\n\n");
        page.Append("<!-- Generated from data/certification.json by tests/RomMBat.Tests/PlatformSupportPageTests.cs.\n");
        page.Append("     Edit the data and regenerate; an edit here fails the test. -->\n\n");
        page.Append("RomMBat is tested one row at a time. A row is one emulator on one system, and one core where\n");
        page.Append("the emulator has several, because two emulators for the same console keep their saves in\n");
        page.Append("different places. RetroBat runs the default row unless you pick another emulator or core for\n");
        page.Append("the system in its settings.\n\n");
        page.Append("A certified row passed every check against a real RetroBat install: the game lands where the\n");
        page.Append("emulator reads it, its BIOS arrives, it launches with its artwork, and its saves, save states\n");
        page.Append("and playtime reach RomM and come back. A system that is not listed has not been tested yet.\n\n");

        page.Append(Table(
            ["System", "Certified rows", "Default row"],
            systems.Select(system => new[]
            {
                $"[{Name(system.Key, fullNames)}](#{system.Key})",
                $"{system.Count(row => row.Status == "certified")} of {system.Count()}",
                $"{RowName(system.First())}, {StatusText[system.First().Status].ToLowerInvariant()}",
            })));

        foreach (var system in systems)
        {
            var floors = system.Select(row => (row.RomM, row.RetroBat)).Distinct().ToList();

            page.Append("\n## " + Name(system.Key, fullNames) + " {#" + system.Key + "}\n\n");
            page.Append("RetroBat's `" + system.Key + "` system.");
            if (floors.Count == 1)
            {
                page.Append(" Tested on RomM " + floors[0].RomM + " and RetroBat " + floors[0].RetroBat + ".");
            }

            page.Append(" The [certification record](" + RecordsUrl + system.Key + "/) has the detail.\n\n");

            // A column every row leaves empty is left out: the floor when all rows share it, the
            // note when none needs one.
            var perRowFloor = floors.Count > 1;
            var notes = system.Any(row => row.Note is not null);

            var header = new List<string> { "Emulator", "Core", "Status" };
            if (perRowFloor)
            {
                header.Add("Tested on");
            }

            if (notes)
            {
                header.Add("Note");
            }

            page.Append(Table([.. header], system.Select((row, index) =>
            {
                var cells = new List<string> { row.Emulator, row.Core ?? "", StatusText[row.Status] + (index == 0 ? " (default)" : "") };
                if (perRowFloor)
                {
                    cells.Add("RomM " + row.RomM + ", RetroBat " + row.RetroBat);
                }

                if (notes)
                {
                    cells.Add(row.Note ?? "");
                }

                return cells.ToArray();
            })));
        }

        return page.ToString();
    }

    private static string Name(string system, IReadOnlyDictionary<string, string> fullNames) =>
        fullNames.TryGetValue(system, out var fullName) ? fullName : system;

    private static string RowName(CertificationRow row) =>
        row.Core is null ? row.Emulator : $"{row.Emulator} / {row.Core}";

    /// <summary>A table padded the way Prettier pads one, so <c>trunk fmt</c> leaves it alone.</summary>
    private static string Table(string[] header, IEnumerable<string[]> body)
    {
        var rows = body.Select(cells => cells.Select(Escape).ToArray()).ToList();
        var widths = header
            .Select((cell, column) => Math.Max(3, rows.Select(row => row[column].Length).Append(cell.Length).Max()))
            .ToArray();

        var table = new StringBuilder();
        table.Append(Line(header, widths));
        table.Append(Line(widths.Select(width => new string('-', width)).ToArray(), widths));
        foreach (var row in rows)
        {
            table.Append(Line(row, widths));
        }

        return table.ToString();

        static string Line(string[] cells, int[] widths) =>
            "| " + string.Join(" | ", cells.Select((cell, column) => cell.PadRight(widths[column]))) + " |\n";

        static string Escape(string cell) => cell.Replace("|", "\\|", StringComparison.Ordinal);
    }

    private static List<CertificationRow> LoadRows()
    {
        var json = File.ReadAllText(Path.Combine(GeneratedPage.RepoRoot, DataPath));
        var file = JsonSerializer.Deserialize<CertificationFile>(json)
            ?? throw new InvalidOperationException($"{DataPath} is empty.");
        return file.Rows;
    }

    /// <summary>Each system's rows as the vendored <c>es_systems.cfg</c> declares them, in file order.</summary>
    /// <remarks>
    /// An emulator's cores are either under <c>&lt;cores&gt;</c> or one bare <c>&lt;core&gt;</c>
    /// (<c>mednafen</c> on <c>nes</c> is the second), and one with neither is a row of its own.
    /// </remarks>
    private static Dictionary<string, List<(string Emulator, string? Core)>> DeclaredRows() =>
        Systems().ToDictionary(
            system => (string)system.Element("name")!,
            system => system.Element("emulators")?.Elements("emulator")
                .SelectMany(emulator =>
                {
                    var cores = emulator.Descendants("core").Select(core => (string?)core.Value).ToList();
                    return (cores.Count == 0 ? new List<string?> { null } : cores).Select(core => ((string)emulator.Attribute("name")!, core));
                })
                .ToList() ?? []);

    private static Dictionary<string, string> FullNames() =>
        Systems()
            .Where(system => system.Element("fullname") is not null)
            .ToDictionary(system => (string)system.Element("name")!, system => (string)system.Element("fullname")!);

    private static IEnumerable<XElement> Systems() =>
        XDocument.Load(Fixtures.EsSystemsTemplate).Root!.Elements("system")
            .GroupBy(system => (string?)system.Element("name"))
            .Where(group => group.Key is not null)
            .Select(group => group.First());

    private sealed record CertificationFile([property: JsonPropertyName("rows")] List<CertificationRow> Rows);

    private sealed record CertificationRow(
        [property: JsonPropertyName("system")] string System,
        [property: JsonPropertyName("emulator")] string Emulator,
        [property: JsonPropertyName("core")] string? Core,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("romm")] string RomM,
        [property: JsonPropertyName("retrobat")] string RetroBat,
        [property: JsonPropertyName("note")] string? Note = null);
}
