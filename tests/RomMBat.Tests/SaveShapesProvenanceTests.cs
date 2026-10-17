using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Each <c>save_shapes.json</c> entry names the RetroBat build it was measured on: its own
/// <c>retrobat_version</c>, or the file's <c>_retrobat_version</c> when it has none.
/// </summary>
/// <remarks>
/// No generator stamps the file, and entries are measured one at a time on whatever build the
/// floor is then. An entry whose evidence cites a fact must name the build that fact was
/// <c>Verified:</c> on, so a shape measured on a newer build cannot sit under the file's
/// older default. A <c>Carried:</c> clause does not count, because carrying is not measuring.
/// </remarks>
public sealed partial class SaveShapesProvenanceTests
{
    private const string ShapesPath = "data/retrobat/save_shapes.json";

    [Fact]
    public void Every_stamp_has_the_form_version_info_carries()
    {
        var file = LoadShapes();
        Assert.Matches(StampPattern(), (string)file["_retrobat_version"]!);

        foreach (var (system, entry) in file["shapes"]!.AsObject())
        {
            if (entry!["retrobat_version"] is { } stamp)
            {
                Assert.True(StampPattern().IsMatch((string)stamp!), $"{system}: retrobat_version '{stamp}' is not the version.info form");
            }
        }
    }

    [Fact]
    public void An_entry_citing_a_fact_names_the_build_that_fact_was_measured_on()
    {
        var file = LoadShapes();
        var fallback = (string)file["_retrobat_version"]!;
        var measured = VerifiedBuilds();

        foreach (var (system, entry) in file["shapes"]!.AsObject())
        {
            var cited = FactId().Matches(entry!.ToJsonString()).Select(m => m.Value).Distinct().ToList();
            if (cited.Count == 0)
            {
                continue;
            }

            var missing = cited.Where(id => !measured.ContainsKey(id)).ToList();
            Assert.True(missing.Count == 0, $"{system}: cites {string.Join(", ", missing)}, which no doc stamps Verified:");

            var newest = cited.Select(id => measured[id]).Max()!;
            var stamp = (string?)entry["retrobat_version"] ?? fallback;
            Assert.True(
                Version.Parse(stamp.Split('-')[0]) == newest,
                $"{system}: measured on {newest} by {string.Join(", ", cited)}, but names {stamp}. Give the entry a retrobat_version.");
        }
    }

    private static JsonObject LoadShapes() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(GeneratedPage.RepoRoot, ShapesPath)))!.AsObject();

    /// <summary>Every RB- fact under <c>docs/</c>, to the build its <c>Verified:</c> line names.</summary>
    private static Dictionary<string, Version> VerifiedBuilds()
    {
        var builds = new Dictionary<string, Version>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(Path.Combine(GeneratedPage.RepoRoot, "docs"), "*.md", SearchOption.AllDirectories))
        {
            string? fact = null;
            foreach (var line in File.ReadLines(path))
            {
                if (line.StartsWith('#'))
                {
                    var heading = FactHeading().Match(line);
                    fact = heading.Success ? heading.Groups[1].Value : null;
                }
                else if (fact is not null && VerifiedLine().Match(line) is { Success: true } verified)
                {
                    builds[fact] = Version.Parse(verified.Groups[1].Value);
                    fact = null;
                }
            }
        }

        return builds;
    }

    [GeneratedRegex(@"^\d+\.\d+\.\d+-stable-win64$")]
    private static partial Regex StampPattern();

    [GeneratedRegex(@"RB-\d+")]
    private static partial Regex FactId();

    [GeneratedRegex(@"^#{2,} (RB-\d+)\.")]
    private static partial Regex FactHeading();

    [GeneratedRegex(@"^Verified: RetroBat (\d+\.\d+\.\d+)")]
    private static partial Regex VerifiedLine();
}
