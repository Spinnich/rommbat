using RomMBat.Core.Store;

namespace RomMBat.Core.Content;

/// <summary>
/// The order regions are preferred in when several accepted files compete for one slot.
/// </summary>
/// <remarks>
/// The third key of the ranked pick, after RetroBat's own md5 and the emulator's priority, so it
/// decides only between files neither of those separates. Words are the user's, matched to
/// retrobios's region vocabulary, where <c>north-america</c> is the USA.
/// </remarks>
public sealed class FirmwareRegionOrder
{
    /// <summary>What a pick uses when nothing is configured.</summary>
    public static readonly FirmwareRegionOrder Default = new(["usa", "europe", "japan", "world"]);

    /// <summary>The words a user can order, and the retrobios regions each one stands for.</summary>
    private static readonly Dictionary<string, string[]> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        ["usa"] = ["north-america", "usa", "us"],
        ["europe"] = ["europe"],
        ["japan"] = ["japan"],
        ["world"] = ["world"],
        ["korea"] = ["korea"],
        ["brazil"] = ["brazil"],
        ["asia"] = ["asia"],
        ["oceania"] = ["oceania", "australia"],
    };

    private FirmwareRegionOrder(IReadOnlyList<string> regions) => Regions = regions;

    /// <summary>The words, most preferred first.</summary>
    public IReadOnlyList<string> Regions { get; }

    /// <summary>Every word <see cref="TryParse"/> accepts.</summary>
    public static IReadOnlyCollection<string> Known => Words.Keys;

    /// <summary>The order configured on this install, or the default.</summary>
    public static FirmwareRegionOrder From(SettingStore settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return TryParse(settings.Get(SettingStore.FirmwareRegionOrder), out var order) ? order : Default;
    }

    /// <summary>Reads a comma-separated list of known words, each once.</summary>
    public static bool TryParse(string? value, out FirmwareRegionOrder order)
    {
        order = Default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var words = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(word => word.ToLowerInvariant())
            .ToList();

        if (words.Count == 0
            || words.Any(word => !Words.ContainsKey(word))
            || words.Distinct(StringComparer.Ordinal).Count() != words.Count)
        {
            return false;
        }

        order = new FirmwareRegionOrder(words);
        return true;
    }

    /// <summary>Where a file's regions place it: its best region's position, or past every one.</summary>
    public int Rank(IReadOnlyList<string> regions)
    {
        ArgumentNullException.ThrowIfNull(regions);

        for (var index = 0; index < Regions.Count; index++)
        {
            var stands = Words[Regions[index]];

            if (regions.Any(region => stands.Contains(region, StringComparer.OrdinalIgnoreCase)))
            {
                return index;
            }
        }

        return Regions.Count;
    }

    /// <summary>The setting's own form, as <see cref="TryParse"/> reads it.</summary>
    public override string ToString() => string.Join(",", Regions);

    /// <summary>For a person: <c>USA &gt; Europe &gt; Japan &gt; world</c>.</summary>
    public string Describe() => string.Join(" > ", Regions.Select(word => word switch
    {
        "usa" => "USA",
        "world" => "world",
        _ => char.ToUpperInvariant(word[0]) + word[1..],
    }));
}
