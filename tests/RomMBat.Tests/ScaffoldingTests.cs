using RomM.Client;
using RomMBat.Core;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Placeholder suite so the solution has a real test target before M1.
/// </summary>
/// <remarks>
/// These assert the declared compatibility baseline, which every release names and
/// which the README compatibility table has to agree with. Delete or absorb them once
/// there is behaviour worth covering.
/// </remarks>
public class ScaffoldingTests
{
    [Fact]
    public void Minimum_supported_RomM_version_is_5_1_0()
    {
        Assert.Equal(new Version(5, 1, 0), RomMServerVersion.Minimum);
    }

    [Fact]
    public void Minimum_supported_RetroBat_version_is_8_2()
    {
        Assert.Equal(new Version(8, 2), RetroBatRoot.MinimumVersion);
    }

    [Fact]
    public void RetroBat_root_is_identified_by_markers_relative_to_the_tree()
    {
        // Discovery walks up from AppContext.BaseDirectory looking for these. None may
        // be absolute, or the portable case breaks.
        Assert.NotEmpty(RetroBatRoot.Markers);
        Assert.All(RetroBatRoot.Markers, marker => Assert.False(Path.IsPathRooted(marker)));
    }
}
