using RomMBat.Core.RetroBat;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The <c>es_settings.cfg</c> writer, against a real ES-written file.
/// </summary>
/// <remarks>
/// <b>This is the first thing in the repository that writes into the user's RetroBat
/// configuration</b>, and being wrong here does not lose a save by mishandling it: it loses one
/// by pointing an emulator at a different container while the old one still holds the game.
/// So the round trip is asserted byte for byte rather than element by element.
/// <para>
/// The fixture is a live capture with its credentials replaced, produced by
/// <c>tools/m6-probes/m6-redact-es-settings.py</c>. <b>A real one holds plaintext
/// credentials</b>, which is why it cannot simply be copied in.
/// </para>
/// </remarks>
public class EsSettingsFileTests
{
    private static string Fixture => Path.Combine(AppContext.BaseDirectory, "fixtures", "es_settings.cfg");

    [Fact]
    public void A_real_file_renders_back_byte_for_byte()
    {
        // The strongest form of merge-not-clobber: 260 settings across three groups, tab
        // indentation, LF endings on Windows, a bare <?xml version="1.0"?> with no encoding,
        // and an &amp; in ScreenSaverGameInfo, all reproduced without being asked to.
        var expected = File.ReadAllBytes(Fixture);
        var rendered = System.Text.Encoding.UTF8.GetBytes(EsSettingsFile.Load(Fixture).Render());

        Assert.Equal(expected, rendered);
    }

    [Fact]
    public void Every_setting_survives_a_load_and_a_write_including_ones_nothing_understands()
    {
        var before = EsSettingsFile.Load(Fixture).Settings.ToList();

        // 42 bool, 3 int, 215 string on the captured install. The counts are asserted because
        // the group is part of the file's shape: ES sorts alphabetically within each group, so
        // a setting that changed group would move and read as churn.
        Assert.Equal(260, before.Count);
        Assert.Equal(42, before.Count(setting => setting.Group == EsSettingGroup.Bool));
        Assert.Equal(3, before.Count(setting => setting.Group == EsSettingGroup.Number));
        Assert.Equal(215, before.Count(setting => setting.Group == EsSettingGroup.Text));

        var file = EsSettingsFile.Load(Fixture);
        file.Set("ps2[\"Ape Escape 2 (USA).chd\"].pcsx2_slot1_memory", "game");

        var after = EsSettingsFile.Load(Fixture);
        var roundTripped = Reload(file).Settings.ToList();

        // Nothing lost, and nothing altered but the one key added.
        Assert.Equal(before.Count + 1, roundTripped.Count);
        foreach (var setting in before)
        {
            Assert.Contains(roundTripped, other => other == setting);
        }

        Assert.Equal(before.Count, after.Settings.Count());
    }

    [Fact]
    public void Es_own_quote_escaping_round_trips_through_the_per_game_form()
    {
        // ES writes the per-game key as ports[&quot;2048.libretro&quot;].smooth, measured in
        // M0. The key in memory carries bare quotes and the file carries the entities, and a
        // writer that got that backwards would produce a key emulatorlauncher cannot match.
        var file = EsSettingsFile.Load(Fixture);
        var key = EsSettingsFile.PerGameKey("ps2", "Ape Escape 2 (USA).chd", "pcsx2_slot1_memory");

        file.Set(key, "game");
        var rendered = file.Render();

        Assert.Contains("ps2[&quot;Ape Escape 2 (USA).chd&quot;].pcsx2_slot1_memory", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("ps2[\"Ape Escape 2 (USA).chd\"]", rendered, StringComparison.Ordinal);
        Assert.Equal("game", Reload(file).Value(key));
    }

    [Fact]
    public void An_ampersand_already_in_the_file_is_not_double_escaped()
    {
        // ScreenSaverGameInfo is 'start &amp; end' in the capture. A writer that read the
        // entity and re-escaped the '&' would turn it into '&amp;amp;' and change a setting it
        // was never asked to touch.
        var file = EsSettingsFile.Load(Fixture);

        Assert.Equal("start & end", file.Value("ScreenSaverGameInfo"));
        Assert.Contains("value=\"start &amp; end\"", file.Render(), StringComparison.Ordinal);
        Assert.DoesNotContain("&amp;amp;", file.Render(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_per_game_key_built_from_a_stem_is_refused_rather_than_written()
    {
        // M0 case E against case F: ports["gong"].smooth was ignored and
        // ports["gong.libretro"].smooth took effect, differing in nothing but the extension.
        // The failure is silent, so it has to be caught here rather than on the install.
        var thrown = Assert.Throws<ArgumentException>(
            () => EsSettingsFile.PerGameKey("ps2", "Ape Escape 2 (USA)", "pcsx2_slot1_memory"));

        Assert.Contains("no extension", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_per_game_key_carries_the_extension_and_outranks_the_system_key()
    {
        Assert.Equal(
            "ps2[\"Ape Escape 2 (USA).chd\"].pcsx2_slot1_memory",
            EsSettingsFile.PerGameKey("ps2", "Ape Escape 2 (USA).chd", "pcsx2_slot1_memory"));

        Assert.Equal("ps2.pcsx2_slot1_memory", EsSettingsFile.SystemKey("ps2", "pcsx2_slot1_memory"));
    }

    [Fact]
    public void An_override_written_for_one_rom_does_not_reach_another()
    {
        // M0 case D. The scoping is emulatorlauncher's and this only asserts the key shape,
        // but a writer that built one key for two roms would break the property the measurement
        // established, and nothing downstream would notice.
        var file = EsSettingsFile.Load(Fixture);
        var first = EsSettingsFile.PerGameKey("ps2", "Ape Escape 2 (USA).chd", "pcsx2_slot1_memory");
        var second = EsSettingsFile.PerGameKey("ps2", "Ape Escape 3 (USA).chd", "pcsx2_slot1_memory");

        file.Set(first, "game");
        var reloaded = Reload(file);

        Assert.NotEqual(first, second);
        Assert.Equal("game", reloaded.Value(first));
        Assert.Null(reloaded.Value(second));

        // And it does not reach the system scope either, which is the layer it outranks.
        Assert.Null(reloaded.Value(EsSettingsFile.SystemKey("ps2", "pcsx2_slot1_memory")));
    }

    [Fact]
    public void A_missing_key_is_reported_as_missing_and_never_as_a_revert()
    {
        // ES prunes any setting whose value equals its own default, measured on Language. So
        // absence has two causes that this file cannot tell apart, and the only correct answer
        // here is "not present". What decides a revert is the recorded prior state, not this.
        var file = EsSettingsFile.Load(Fixture);

        Assert.False(file.Has("ps2.pcsx2_slot1_memory"));
        Assert.Null(file.Value("ps2.pcsx2_slot1_memory"));

        file.Set("ps2.pcsx2_slot1_memory", "standard");
        Assert.True(file.Has("ps2.pcsx2_slot1_memory"));

        Assert.True(file.Remove("ps2.pcsx2_slot1_memory"));
        Assert.False(file.Has("ps2.pcsx2_slot1_memory"));
        Assert.False(file.Remove("ps2.pcsx2_slot1_memory"));
    }

    [Fact]
    public void Setting_an_existing_key_replaces_the_value_and_adds_no_second_entry()
    {
        var file = EsSettingsFile.Load(Fixture);
        var before = file.Settings.Count();

        file.Set("ps2.emulator", "pcsx2");
        file.Set("ps2.emulator", "libretro");

        var reloaded = Reload(file);
        Assert.Equal(before, reloaded.Settings.Count());
        Assert.Equal("libretro", reloaded.Value("ps2.emulator"));
        Assert.Single(reloaded.Settings, setting => setting.Name == "ps2.emulator");
    }

    [Fact]
    public void The_write_is_atomic_and_skipped_when_the_bytes_would_not_change()
    {
        using var temporary = new TempDirectory();
        var path = Path.Combine(temporary.Path, "es_settings.cfg");
        File.Copy(Fixture, path);

        var unchanged = EsSettingsFile.Load(path);
        Assert.False(unchanged.WriteIfChanged(path));

        var changed = EsSettingsFile.Load(path);
        changed.Set(EsSettingsFile.PerGameKey("ps2", "Ape Escape 2 (USA).chd", "pcsx2_slot1_memory"), "game");
        Assert.True(changed.WriteIfChanged(path));

        // No temp file left behind: the rename is the commit, so a leftover .rommbat-tmp means
        // the write did not go through the path it claims to.
        Assert.Empty(Directory.GetFiles(temporary.Path, "*.rommbat-tmp"));
        Assert.Single(Directory.GetFiles(temporary.Path));

        // And the file on disk really carries it, rather than only the object in hand.
        Assert.Equal("game", EsSettingsFile.Load(path).Value(
            EsSettingsFile.PerGameKey("ps2", "Ape Escape 2 (USA).chd", "pcsx2_slot1_memory")));
    }

    [Fact]
    public void A_missing_file_loads_as_empty_rather_than_throwing()
    {
        // A fresh install has none: ES writes it on its first exit that changes something.
        using var temporary = new TempDirectory();
        var path = Path.Combine(temporary.Path, "es_settings.cfg");

        var file = EsSettingsFile.Load(path);
        Assert.Empty(file.Settings);

        file.Set(EsSettingsFile.PerGameKey("ps2", "Game (USA).chd", "pcsx2_slot1_memory"), "game");
        Assert.True(file.WriteIfChanged(path));
        Assert.Single(EsSettingsFile.Load(path).Settings);
    }

    [Fact]
    public void The_location_is_relative_to_the_retrobat_root()
    {
        // Rule 1: the drive letter changes, so nothing persists the absolute path.
        Assert.Equal("emulationstation/.emulationstation/es_settings.cfg", EsSettingsFile.Location.Value);
    }

    /// <summary>Renders and re-reads, which is what a write followed by a load does.</summary>
    private static EsSettingsFile Reload(EsSettingsFile file)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, file.Render());
            return EsSettingsFile.Load(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory() => Directory.CreateDirectory(Path);

        public string Path { get; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rommbat-es-settings-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
