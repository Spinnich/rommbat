using System.Text.Json;
using RomMBat.Core.Mapping;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The mapping regression: the bundled table has to stay true to RetroBat's own list, and
/// the chain has to keep a guess distinguishable from a choice.
/// </summary>
/// <remarks>
/// This is what stands between an editable mapping feature and a lookup table that quietly
/// writes ROMs into folders EmulationStation never scans.
/// </remarks>
public class PlatformMappingTests
{
    [Fact]
    public void Every_bundled_folder_is_a_real_retrobat_system()
    {
        var known = Fixtures.LoadSystemNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(File.ReadAllText(Fixtures.PlatformsJson));

        var broken = new List<string>();
        foreach (var platform in document.RootElement.GetProperty("platforms").EnumerateObject())
        {
            foreach (var folder in platform.Value.EnumerateArray())
            {
                var name = folder.GetString()!;
                if (!known.Contains(name))
                {
                    broken.Add($"{platform.Name} -> {name}");
                }
            }
        }

        Assert.Empty(broken);
    }

    [Fact]
    public void The_stale_seed_entries_were_corrected_rather_than_carried_over()
    {
        var map = BundledPlatformMap.Bundled;

        // The seed keys these as astrocde, bbc, ps and segacd; RetroBat's own list says
        // astrocade, bbcmicro, psx and megacd.
        Assert.Equal(["astrocade"], map.Candidates("astrocade"));
        Assert.Equal(["bbcmicro"], map.Candidates("bbcmicro"));
        Assert.Equal(["psx"], map.Candidates("psx"));
        Assert.Equal(["megacd"], map.Candidates("segacd"));
    }

    [Fact]
    public void Multi_folder_slugs_resolve_deterministically_against_a_real_install()
    {
        var install = Fixtures.LoadEsSystems();
        var resolver = new PlatformResolver(install);

        // amiga names three folders, and the same one has to win every time.
        var first = resolver.Resolve(new RomMPlatform(1, "amiga", null, "Amiga"));
        var second = resolver.Resolve(new RomMPlatform(1, "amiga", null, "Amiga"));

        Assert.Equal("amiga500", first.Folder);
        Assert.Equal(first.Folder, second.Folder);
        Assert.Equal(MappingSource.Bundled, first.ResolvedBy);
        Assert.Equal(["amiga500", "amiga1200", "amiga4000"], first.Candidates);
    }

    [Fact]
    public void The_first_candidate_the_install_actually_has_wins()
    {
        // An install with only the 1200, which is the case the ordered list exists for.
        var install = Fixtures.Synthesize(("amiga1200", ".adf .zip"));
        var resolver = new PlatformResolver(install);

        var resolution = resolver.Resolve(new RomMPlatform(1, "amiga", null, "Amiga"));

        Assert.Equal("amiga1200", resolution.Folder);
        Assert.Equal(MappingSource.Bundled, resolution.ResolvedBy);
    }

    /// <summary>
    /// Arcade still refuses to guess, but only when there is something to guess about.
    /// </summary>
    /// <remarks>
    /// <b>This assertion was inverted in M7 stage 7b-2a, on a hands-on finding, and the reason
    /// is worth reading before inverting it back.</b> It used to pass <c>fs_slug: "mame"</c>
    /// and require a refusal, which put the arcade check ahead of the fs_slug match. But
    /// <c>docs/PLAN.md</c>'s M2 orders the chain the other way, "try this before any table",
    /// and the arcade rule comes from that table.
    /// <para>
    /// What it cost on a live install: RomM's "Arcade (FinalBurn Neo)" carries
    /// <c>slug: arcade</c> and <c>fs_slug: fbneo</c>, RetroBat has an <c>fbneo</c> system and a
    /// <c>roms/fbneo</c> directory, and resolving a collection that merely contained one arcade
    /// game stopped halfway to demand a per-set folder choice that the library had already
    /// answered by naming the folder.
    /// </para>
    /// <para>
    /// The refusal is kept for the case it exists for: an arcade slug whose fs_slug names
    /// nothing this install has. Arcade rom names are romset-versioned, so choosing among ten
    /// folders with no evidence puts files where the emulator cannot read them.
    /// </para>
    /// </remarks>
    [Fact]
    public void Arcade_refuses_to_guess_when_the_fs_slug_names_no_folder()
    {
        var install = Fixtures.LoadEsSystems();
        var resolver = new PlatformResolver(install);

        var resolution = resolver.Resolve(new RomMPlatform(1, "arcade", "arcade", "Arcade"));

        Assert.Null(resolution.Folder);
        Assert.True(resolution.RequiresExplicitChoice);
        Assert.Contains("mame", resolution.Candidates);
        Assert.Contains("romset", resolution.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Arcade_resolves_once_somebody_chooses()
    {
        var install = Fixtures.LoadEsSystems();
        // Keyed by fs_slug, which is what RomM keeps unique and what the override table holds.
        var resolver = new PlatformResolver(install, new Dictionary<string, string> { ["arcade"] = "fbneo" });

        var resolution = resolver.Resolve(new RomMPlatform(1, "arcade", "arcade", "Arcade"));

        Assert.Equal("fbneo", resolution.Folder);
        Assert.Equal(MappingSource.User, resolution.ResolvedBy);
    }

    [Fact]
    public void Fs_slug_beats_the_bundled_table()
    {
        var install = Fixtures.LoadEsSystems();
        var resolver = new PlatformResolver(install);

        // The bundled table would send 'amiga' to amiga500. A Batocera-shaped library that
        // already calls it amiga1200 is a better answer than any table.
        var resolution = resolver.Resolve(new RomMPlatform(1, "amiga", "amiga1200", "Amiga"));

        Assert.Equal("amiga1200", resolution.Folder);
        Assert.Equal(MappingSource.FsSlug, resolution.ResolvedBy);
    }

    [Fact]
    public void A_user_override_beats_everything()
    {
        var install = Fixtures.LoadEsSystems();
        var resolver = new PlatformResolver(install, new Dictionary<string, string> { ["snes"] = "snes-msu1" });

        var resolution = resolver.Resolve(new RomMPlatform(1, "snes", "snes", "Super Nintendo"));

        Assert.Equal("snes-msu1", resolution.Folder);
        Assert.Equal(MappingSource.User, resolution.ResolvedBy);
        Assert.False(resolution.FolderMissingFromInstall);
    }

    [Fact]
    public void An_override_naming_a_folder_the_install_lacks_is_flagged_not_silently_used()
    {
        var install = Fixtures.Synthesize(("snes", ".sfc"));
        var resolver = new PlatformResolver(install, new Dictionary<string, string> { ["snes"] = "not-a-system" });

        var resolution = resolver.Resolve(new RomMPlatform(1, "snes", "snes", "Super Nintendo"));

        Assert.True(resolution.FolderMissingFromInstall);
        Assert.Contains("EmulationStation never scans", resolution.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void A_normalized_match_is_offered_and_not_applied()
    {
        var install = Fixtures.LoadEsSystems();
        var resolver = new PlatformResolver(install);

        // RetroBat calls it ti99, RomM calls it ti-99. Close enough to suggest, not close
        // enough to write games into a folder without being asked.
        var resolution = resolver.Resolve(new RomMPlatform(1, "ti-99", null, "TI-99"));

        Assert.Equal(MappingSource.Normalized, resolution.ResolvedBy);
        Assert.Equal("ti99", resolution.Suggestion);
        Assert.Null(resolution.Folder);
        Assert.False(resolution.IsApplied);
    }

    [Fact]
    public void A_platform_with_no_retrobat_folder_is_unmapped_and_explained()
    {
        var install = Fixtures.LoadEsSystems();
        var resolver = new PlatformResolver(install);

        var resolution = resolver.Resolve(new RomMPlatform(1, "some-console-nobody-has", null, "Nobody's"));

        Assert.Equal(MappingSource.Unmapped, resolution.ResolvedBy);
        Assert.Null(resolution.Folder);
        Assert.Null(resolution.Suggestion);
        Assert.Contains("skipped", resolution.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Retrobat_only_systems_never_reach_the_mapping_surface()
    {
        var install = Fixtures.LoadEsSystems();
        var resolver = new PlatformResolver(install);

        // Ports and storefronts have no RomM platform by design. The chain runs per RomM
        // platform, so these can only appear if something enumerated folders instead, which
        // is the mistake this asserts against.
        foreach (var folder in new[] { "cavestory", "devilutionx", "steam", "gog", "epic", "amazon" })
        {
            Assert.True(install.HasFolder(folder));
        }

        var mapped = resolver.ResolveAll([new RomMPlatform(1, "snes", "snes", "Super Nintendo")]);

        Assert.Single(mapped);
        Assert.Equal("snes", mapped[0].Slug);
    }

    [Fact]
    public void Two_platforms_can_share_one_folder()
    {
        var install = Fixtures.LoadEsSystems();
        var resolver = new PlatformResolver(
            install,
            new Dictionary<string, string> { ["sfam"] = "snes" });

        var snes = resolver.Resolve(new RomMPlatform(1, "snes", "snes", "Super Nintendo"));
        var sfam = resolver.Resolve(new RomMPlatform(2, "sfam", "sfam", "Super Famicom"));

        Assert.Equal("snes", snes.Folder);
        Assert.Equal("snes", sfam.Folder);
    }

    [Fact]
    public void Two_platforms_sharing_one_slug_stay_two_platforms()
    {
        var install = Fixtures.LoadEsSystems();

        // Measured on a real 123-platform instance: 72 distinct slugs, because every system
        // has an "-unofficial" twin carrying the same slug. Keyed by slug, one of these two
        // would overwrite the other and become unmappable.
        var resolver = new PlatformResolver(install, new Dictionary<string, string> { ["gb-unofficial"] = "gb2players" });

        var official = resolver.Resolve(new RomMPlatform(271, "gb", "gb", "Nintendo - Game Boy"));
        var unofficial = resolver.Resolve(new RomMPlatform(299, "gb", "gb-unofficial", "Nintendo - Game Boy (Unofficial)"));

        Assert.NotEqual(official.FsSlug, unofficial.FsSlug);
        Assert.Equal(official.Slug, unofficial.Slug);
        Assert.Equal("gb", official.Folder);
        Assert.Equal("gb2players", unofficial.Folder);
        Assert.Equal(MappingSource.User, unofficial.ResolvedBy);
    }

    [Fact]
    public void Recording_a_resolution_over_an_override_leaves_one_row_and_it_stays_the_choice()
    {
        using var tree = TempRetroBatTree.Create();
        using var store = LocalStore.Open(tree.Install());
        var now = new DateTimeOffset(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);

        store.PlatformMap.SetOverride("arcade", "fbneo", now);

        // The resolver knows nothing of the override, which is the case a re-resolve after a
        // RetroBat upgrade hits: it is built from the install alone and its answer is a guess.
        var resolver = new PlatformResolver(Fixtures.LoadEsSystems());
        store.PlatformMap.Record(resolver.Resolve(new RomMPlatform(1, "arcade", "arcade", "Arcade")), now);

        // One row, not two. A choice and a guess for the same platform share a key, so the
        // guess cannot be counted separately from the choice it did not overwrite.
        var row = Assert.Single(store.PlatformMap.List());

        Assert.Equal("arcade", row.FsSlug);
        Assert.Equal(MappingSource.User, row.ResolvedBy);
        Assert.Equal("fbneo", row.Folder);
    }

    [Theory]
    [InlineData("action-max", "actionmax")]
    [InlineData("TI-99", "ti99")]
    [InlineData("Sega_CD", "segacd")]
    public void Normalization_strips_case_and_punctuation(string input, string expected) =>
        Assert.Equal(expected, PlatformResolver.Normalize(input));

    [Fact]
    public void An_arcade_platform_whose_fs_slug_already_names_a_folder_needs_no_choice()
    {
        // Found on a live install. RomM's "Arcade (FinalBurn Neo)" carries slug 'arcade' and
        // fs_slug 'fbneo', and RetroBat has an fbneo system with a roms/fbneo directory. The
        // arcade check keyed on the slug and ran before the fs_slug match, so a platform whose
        // folder was already named refused to resolve and demanded a per-set choice, halfway
        // through resolving a collection that merely happened to contain an arcade game.
        //
        // An arcade slug is ambiguous because ten folders could be right. An fs_slug that
        // already names one of them is not ambiguous: the person filing the library answered
        // the question by naming the folder.
        var install = Fixtures.LoadEsSystems();
        var resolver = new PlatformResolver(install, new Dictionary<string, string>());

        var resolution = resolver.Resolve(
            new RomMPlatform(9, "arcade", "fbneo", "Arcade (FinalBurn Neo)"));

        Assert.Equal("fbneo", resolution.Folder);
        Assert.False(resolution.RequiresExplicitChoice);
        Assert.Equal(MappingSource.FsSlug, resolution.ResolvedBy);
    }

}
