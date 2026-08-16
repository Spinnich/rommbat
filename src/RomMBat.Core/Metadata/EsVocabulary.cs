using System.Diagnostics.CodeAnalysis;

namespace RomMBat.Core.Metadata;

/// <summary>
/// The two vocabularies EmulationStation uses that RomM does not.
/// </summary>
/// <remarks>
/// Both tables are derived from a real scraped RetroBat install rather than from a standard:
/// its 4,531 gamelist entries write six region codes (<c>us</c>, <c>jp</c>, <c>eu</c>,
/// <c>wr</c>, <c>br</c>, <c>kr</c>) and thirteen language codes, while RomM says
/// <c>USA</c>, <c>Japan</c>, <c>World</c> and <c>English</c>. RomM's own gamelist exporter
/// copies its values through untranslated, so a RomM-exported gamelist writes <c>USA</c>
/// where ES expects <c>us</c>; RomMBat translates.
/// <para>
/// <b>An unmapped value is dropped, not guessed.</b> Region codes are ScreenScraper's own and
/// follow no standard that could be extrapolated, so anything outside the observed six is
/// left out and the next region in the list is tried.
/// </para>
/// </remarks>
public static class EsVocabulary
{
    /// <summary>What a real install writes, keyed by what RomM calls it.</summary>
    private static readonly Dictionary<string, string> Regions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USA"] = "us",
        ["United States"] = "us",
        ["Japan"] = "jp",
        ["Europe"] = "eu",
        ["World"] = "wr",
        ["Brazil"] = "br",
        ["Korea"] = "kr",
        ["South Korea"] = "kr",
    };

    /// <summary>
    /// ISO 639-1, with the three exceptions a real install shows.
    /// </summary>
    /// <remarks>
    /// Ten of the thirteen observed codes are plain ISO 639-1 (<c>en</c>, <c>fr</c>,
    /// <c>es</c>, <c>de</c>, <c>it</c>, <c>pt</c>, <c>nl</c>, <c>ru</c>, <c>no</c>, <c>pl</c>),
    /// so the rest of the table extrapolates that. The three that do not follow it are
    /// Japanese as <c>jp</c> rather than <c>ja</c>, Korean as <c>kr</c> rather than <c>ko</c>,
    /// and Brazilian Portuguese as its own <c>br</c>.
    /// </remarks>
    private static readonly Dictionary<string, string> Languages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["English"] = "en",
        ["Japanese"] = "jp",
        ["French"] = "fr",
        ["Spanish"] = "es",
        ["German"] = "de",
        ["Italian"] = "it",
        ["Portuguese"] = "pt",
        ["Brazilian Portuguese"] = "br",
        ["Dutch"] = "nl",
        ["Korean"] = "kr",
        ["Russian"] = "ru",
        ["Norwegian"] = "no",
        ["Polish"] = "pl",
        ["Danish"] = "da",
        ["Swedish"] = "sv",
        ["Finnish"] = "fi",
        ["Chinese"] = "zh",
        ["Czech"] = "cs",
        ["Hungarian"] = "hu",
        ["Turkish"] = "tr",
        ["Greek"] = "el",
        ["Arabic"] = "ar",
        ["Hebrew"] = "he",
    };

    /// <summary>The region code for a RomM region name, or null when there is not one.</summary>
    public static bool TryRegion(string? name, [NotNullWhen(true)] out string? code)
    {
        code = null;
        return !string.IsNullOrWhiteSpace(name) && Regions.TryGetValue(name.Trim(), out code);
    }

    /// <summary>The language code for a RomM language name, or null when there is not one.</summary>
    public static bool TryLanguage(string? name, [NotNullWhen(true)] out string? code)
    {
        code = null;
        return !string.IsNullOrWhiteSpace(name) && Languages.TryGetValue(name.Trim(), out code);
    }

    /// <summary>
    /// The single region a gamelist entry carries.
    /// </summary>
    /// <remarks>
    /// <c>&lt;region&gt;</c> is single-valued in a real install while 246 of 5,000 sampled ROMs
    /// carry more than one, so the first that maps wins and the rest are dropped.
    /// </remarks>
    public static string? Region(IEnumerable<string>? regions)
    {
        foreach (var region in regions ?? [])
        {
            if (TryRegion(region, out var code))
            {
                return code;
            }
        }

        return null;
    }

    /// <summary>Every language that maps, comma-joined, which is the form a real install writes.</summary>
    public static string? LanguageList(IEnumerable<string>? languages)
    {
        var codes = new List<string>();
        foreach (var language in languages ?? [])
        {
            if (TryLanguage(language, out var code) && !codes.Contains(code, StringComparer.Ordinal))
            {
                codes.Add(code);
            }
        }

        return codes.Count == 0 ? null : string.Join(',', codes);
    }
}
