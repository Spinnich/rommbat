using System.Text.Json;
using RomM.Client;
using RomMBat.Core;
using RomMBat.Core.Diagnostics;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Version parsing, against the strings both products actually emit.
/// </summary>
/// <remarks>
/// Neither side sends a bare semantic version. RetroBat's <c>system/version.info</c> reads
/// <c>8.2.0-stable-win64</c>, and the RomM instance M0 measured against reported
/// <c>5.1.1-beta.1</c>. A three-numeric-component parse throws on both.
/// </remarks>
public class ProductVersionTests
{
    [Theory]
    [InlineData("8.2.0-stable-win64", 8, 2, 0, "stable-win64")]
    [InlineData("5.1.1-beta.1", 5, 1, 1, "beta.1")]
    [InlineData("5.1.0", 5, 1, 0, null)]
    [InlineData("8.2", 8, 2, -1, null)]
    public void Parses_the_numeric_core_and_keeps_the_suffix(
        string input,
        int major,
        int minor,
        int patch,
        string? suffix)
    {
        Assert.True(ProductVersion.TryParse(input, out var version));

        Assert.Equal(major, version.Components[0]);
        Assert.Equal(minor, version.Components[1]);
        Assert.Equal(suffix, version.Suffix);
        Assert.Equal(input, version.Raw);

        if (patch >= 0)
        {
            Assert.Equal(patch, version.Components[2]);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("stable")]
    [InlineData("v8.2.0")]
    [InlineData("8.x.0")]
    public void Refuses_anything_without_a_numeric_core(string? input)
    {
        Assert.False(ProductVersion.TryParse(input, out _));
        Assert.Throws<FormatException>(() => ProductVersion.Parse(input!));
    }

    [Fact]
    public void A_missing_component_counts_as_zero()
    {
        Assert.Equal(ProductVersion.Parse("8.2"), ProductVersion.Parse("8.2.0"));
        Assert.True(ProductVersion.Parse("8.2") >= ProductVersion.Parse("8.2.0"));
    }

    [Fact]
    public void The_suffix_never_lowers_the_version()
    {
        // Semantic versioning would rank 8.2.0-stable-win64 below 8.2.0 and refuse every
        // stock RetroBat install, because that suffix names a channel, not a prerelease.
        Assert.False(ProductVersion.Parse("8.2.0-stable-win64") < ProductVersion.Parse("8.2.0"));
        Assert.False(ProductVersion.Parse("5.1.1-beta.1") < ProductVersion.Parse("5.1.1"));
    }

    [Fact]
    public void Orders_by_numeric_components()
    {
        Assert.True(ProductVersion.Parse("5.1.1") > ProductVersion.Parse("5.1.0"));
        Assert.True(ProductVersion.Parse("5.2.0") > ProductVersion.Parse("5.1.99"));
        Assert.True(ProductVersion.Parse("10.0") > ProductVersion.Parse("9.9"));
    }

    [Fact]
    public void A_prerelease_suffix_on_a_supported_version_is_not_a_downgrade()
    {
        // The shape M0's instance reported. The suffix, not the number, used to be the trap.
        var check = RomMServerVersion.Check("5.2.0-beta.1");

        Assert.Equal(CompatibilityVerdict.Supported, check.Verdict);
        Assert.False(check.MustRefuse);
    }

    [Fact]
    public void The_pinned_schema_version_is_the_minimum_supported_one()
    {
        // Read out of the pin rather than restated, so moving the pin without moving the
        // floor fails here instead of shipping DTOs that describe a server we refuse.
        using var schema = JsonDocument.Parse(File.ReadAllText(Fixtures.PinnedOpenApi));
        var pinned = schema.RootElement.GetProperty("info").GetProperty("version").GetString();

        Assert.Equal(ProductVersion.Parse(pinned!), RomMServerVersion.Minimum);
        Assert.Equal(CompatibilityVerdict.Supported, RomMServerVersion.Check(pinned).Verdict);
    }

    [Fact]
    public void A_server_below_the_minimum_is_refused_and_the_message_names_both_versions()
    {
        var check = RomMServerVersion.Check("5.1.9");

        Assert.Equal(CompatibilityVerdict.TooOld, check.Verdict);
        Assert.True(check.MustRefuse);
        Assert.Contains("5.1.9", check.Message, StringComparison.Ordinal);
        Assert.Contains("5.2.0", check.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_previous_RomM_stable_is_refused_rather_than_warned_about()
    {
        // The floor tracks the newest stable, so the release before it is out of support.
        var check = RomMServerVersion.Check("5.1.0");

        Assert.Equal(CompatibilityVerdict.TooOld, check.Verdict);
        Assert.True(check.MustRefuse);
    }

    [Fact]
    public void A_server_above_the_tested_version_warns_and_continues()
    {
        var check = RomMServerVersion.Check("6.0.0");

        Assert.Equal(CompatibilityVerdict.Untested, check.Verdict);
        Assert.False(check.MustRefuse);
    }

    [Fact]
    public void An_unreadable_version_is_refused_rather_than_guessed()
    {
        var check = RomMServerVersion.Check("who knows");

        Assert.Equal(CompatibilityVerdict.Unreadable, check.Verdict);
        Assert.True(check.MustRefuse);
    }

    [Fact]
    public void The_stock_RetroBat_version_string_is_supported()
    {
        var check = RetroBatVersion.Check("8.2.1-stable-win64");

        Assert.Equal(CompatibilityVerdict.Supported, check.Verdict);
    }

    [Fact]
    public void RetroBat_below_the_floor_is_refused()
    {
        var check = RetroBatVersion.Check("8.1.0-stable-win64");

        Assert.Equal(CompatibilityVerdict.TooOld, check.Verdict);
        Assert.Contains("8.2.1", check.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_previous_stable_is_refused_rather_than_warned_about()
    {
        // The floor tracks the newest stable, so a patch release below it is out of support
        // and not merely untested. 8.2.0's Flycast state watcher read the wrong directory.
        var check = RetroBatVersion.Check("8.2.0-stable-win64");

        Assert.Equal(CompatibilityVerdict.TooOld, check.Verdict);
        Assert.True(check.MustRefuse);
    }

    [Fact]
    public void The_declared_minimums_match_the_README_compatibility_table()
    {
        Assert.Equal(ProductVersion.Parse("5.2.0"), RomMServerVersion.Minimum);
        Assert.Equal(ProductVersion.Parse("8.2.1"), RetroBatVersion.Minimum);
        Assert.Equal(new Version(8, 2, 1), RetroBatRoot.MinimumVersion);
    }
}
