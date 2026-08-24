using RomMBat.Core.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Opting one game into a per-game memory card, and taking it back out.
/// </summary>
/// <remarks>
/// The only code in RomMBat that changes the user's RetroBat configuration. Every test here is
/// a refusal or a reversal, because the happy path is one line and the failures are what cost a
/// save.
/// </remarks>
public class SaveConverterTests
{
    private const string Ps2Key = "pcsx2_slot1_memory";

    [Fact]
    public void Converting_writes_the_per_game_key_and_records_what_was_there_before()
    {
        using var fixture = ConvertTree.Create();
        fixture.AddRom(42, "ps2", "Armored Core 3 (USA).chd");

        var result = fixture.Converter().Convert(42);

        Assert.Equal(ConversionStatus.Converted, result.Status);
        Assert.Equal(
            "game",
            fixture.Settings().Value("ps2[\"Armored Core 3 (USA).chd\"].pcsx2_slot1_memory"));

        var recorded = Assert.Single(fixture.Store.SaveConversions.List());
        Assert.Equal(42, recorded.RomId);
        Assert.Equal("Armored Core 3 (USA).chd", recorded.FsName);
        Assert.Equal(Ps2Key, recorded.SettingKey);
        Assert.Equal("game", recorded.AppliedValue);

        // The key was not in the file, which is a different prior state from it holding the
        // stock value, and it is the one reverting has to reproduce.
        Assert.Equal(PriorSettingState.Absent, recorded.PriorState);
        Assert.Null(recorded.PriorValue);
    }

    [Fact]
    public void A_preview_writes_nothing_and_still_carries_the_warning()
    {
        using var fixture = ConvertTree.Create();
        fixture.AddRom(42, "ps2", "Armored Core 3 (USA).chd");

        var result = fixture.Converter().Preview(42);

        Assert.Equal(ConversionStatus.Ready, result.Status);
        Assert.Empty(fixture.Store.SaveConversions.List());
        Assert.False(fixture.Settings().Has("ps2[\"Armored Core 3 (USA).chd\"].pcsx2_slot1_memory"));

        // The user has to be told before the switch, not after: migration out of the shared
        // container is deliberately out of scope, so the stranded save is the cost of saying yes.
        Assert.NotNull(result.Warning);
        Assert.Contains("empty memory card", result.Warning, StringComparison.Ordinal);
        Assert.Contains("--revert", result.Warning, StringComparison.Ordinal);
    }

    [Fact]
    public void The_warning_names_the_shared_card_the_save_is_stranded_in_when_one_is_there()
    {
        using var fixture = ConvertTree.Create();
        fixture.AddRom(42, "ps2", "Armored Core 3 (USA).chd");
        fixture.AddSave("ps2", "pcsx2/memcards/Mcd001.ps2", "eleven games' saves");

        var result = fixture.Converter().Preview(42);

        Assert.Contains("Mcd001.ps2", result.Warning!, StringComparison.Ordinal);

        // And it must also say the thing a user never connects back to this decision.
        Assert.Contains("prequel", result.Warning!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_multi_disc_set_is_refused_with_the_reason_and_the_other_discs_named()
    {
        using var fixture = ConvertTree.Create();
        fixture.AddRom(43, "ps2", "Armored Core - Nexus (USA) (Disc 1) (Evolution).chd");
        fixture.AddRom(44, "ps2", "Armored Core - Nexus (USA) (Disc 2) (Revolution).chd");

        var result = fixture.Converter().Convert(43);

        Assert.Equal(ConversionStatus.Refused, result.Status);
        Assert.Contains("disc change", result.Detail, StringComparison.Ordinal);
        Assert.Contains("1 other disc of it", result.Detail, StringComparison.Ordinal);
        Assert.Empty(fixture.Store.SaveConversions.List());
        Assert.Empty(fixture.Settings().Settings);
    }

    [Fact]
    public void The_sibling_count_reads_as_English_when_there_is_more_than_one()
    {
        using var fixture = ConvertTree.Create();
        fixture.AddRom(46, "ps2", "Shadow Hearts - Covenant (USA) (Disc 1).chd");
        fixture.AddRom(47, "ps2", "Shadow Hearts - Covenant (USA) (Disc 2).chd");
        fixture.AddRom(48, "ps2", "Shadow Hearts - Covenant (USA) (Disc 3).chd");

        var result = fixture.Converter().Convert(46);

        Assert.Equal(ConversionStatus.Refused, result.Status);
        Assert.Contains("2 other discs of it", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_lone_disc_of_a_set_is_refused_too_even_with_no_sibling_on_disk()
    {
        // The refusal must not depend on what has been synced, or converting disc 1 today
        // springs the trap when disc 2 arrives tomorrow.
        using var fixture = ConvertTree.Create();
        fixture.AddRom(45, "ps2", "Xenosaga Episode II (USA) (Disc 1).chd");

        var result = fixture.Converter().Convert(45);

        Assert.Equal(ConversionStatus.Refused, result.Status);
        Assert.Contains("not on this device", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_system_the_measurement_says_to_leave_alone_is_refused_with_that_reason()
    {
        // psx declares apply:false. Stock PerGameTitle binds a multi-disc set through
        // DuckStation's own database, so the conversion that looks like an improvement is the
        // regression, and the declaration carries that reasoning rather than the code.
        using var fixture = ConvertTree.Create();
        fixture.AddRom(46, "psx", "Final Fantasy VII (USA) (Disc 1).chd");

        var result = fixture.Converter().Convert(46);

        Assert.Equal(ConversionStatus.Refused, result.Status);
        Assert.Contains("stock setting", result.Detail, StringComparison.Ordinal);
        Assert.Contains("PerGameTitle", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void An_identifier_keyed_conversion_is_refused_rather_than_half_supported()
    {
        // dreamcast converts, and the result is vmu/<SERIAL>_vmu_save_A1.bin, named for the disc
        // serial with the rom filename appearing nowhere. That needs the Game-ID routes, so it
        // is reported with its measured reason rather than offered and left unattributable.
        using var fixture = ConvertTree.Create();
        fixture.AddRom(47, "dreamcast", "Bangai-O (USA).chd");

        var result = fixture.Converter().Convert(47);

        Assert.Equal(ConversionStatus.Refused, result.Status);
        Assert.Contains("disc serial", result.Detail, StringComparison.Ordinal);
        Assert.Empty(fixture.Store.SaveConversions.List());
    }

    [Fact]
    public void A_system_with_no_declared_option_is_refused()
    {
        using var fixture = ConvertTree.Create();
        fixture.AddRom(48, "snes", "ActRaiser (USA).sfc");

        var result = fixture.Converter().Convert(48);

        Assert.Equal(ConversionStatus.Refused, result.Status);
        Assert.Contains("no per-game save option", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_rom_this_device_does_not_hold_is_refused()
    {
        using var fixture = ConvertTree.Create();

        var result = fixture.Converter().Convert(999);

        Assert.Equal(ConversionStatus.Refused, result.Status);
        Assert.Contains("no ROM with id 999", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_setting_RomMBat_did_not_write_is_not_taken_over()
    {
        // Presence is not authorship. ES adds keys on its own (finding 170), so the only sound
        // test is whether the value is one RomMBat wrote, and anything else is left alone.
        using var fixture = ConvertTree.Create();
        fixture.AddRom(42, "ps2", "Armored Core 3 (USA).chd");
        fixture.SetSetting("ps2[\"Armored Core 3 (USA).chd\"].pcsx2_slot1_memory", "folder");

        var result = fixture.Converter().Convert(42);

        Assert.Equal(ConversionStatus.Refused, result.Status);
        Assert.Contains("RomMBat did not write it", result.Detail, StringComparison.Ordinal);

        // And the user's value is still there, untouched.
        Assert.Equal(
            "folder",
            fixture.Settings().Value("ps2[\"Armored Core 3 (USA).chd\"].pcsx2_slot1_memory"));
    }

    [Fact]
    public void Converting_twice_is_a_no_op_rather_than_a_second_record()
    {
        using var fixture = ConvertTree.Create();
        fixture.AddRom(42, "ps2", "Armored Core 3 (USA).chd");

        Assert.Equal(ConversionStatus.Converted, fixture.Converter().Convert(42).Status);
        Assert.Equal(ConversionStatus.NoChange, fixture.Converter().Convert(42).Status);
        Assert.Single(fixture.Store.SaveConversions.List());
    }

    [Fact]
    public void Reverting_an_absent_prior_state_removes_the_key_entirely()
    {
        using var fixture = ConvertTree.Create();
        fixture.AddRom(42, "ps2", "Armored Core 3 (USA).chd");
        fixture.Converter().Convert(42);

        var result = fixture.Converter().Revert(42);

        Assert.Equal(ConversionStatus.Reverted, result.Status);
        Assert.Contains("was not in the file before", result.Detail, StringComparison.Ordinal);

        // Removed, not set to a stock value that was never there.
        Assert.False(fixture.Settings().Has("ps2[\"Armored Core 3 (USA).chd\"].pcsx2_slot1_memory"));
        Assert.Empty(fixture.Store.SaveConversions.List());
    }

    [Fact]
    public void Reverting_a_present_prior_state_restores_the_value_that_was_there()
    {
        // The other half of the pair, and the reason prior_state is two columns: restoring
        // "absent" over a key that held 'standard' leaves the user somewhere they never were.
        using var fixture = ConvertTree.Create();
        fixture.AddRom(42, "ps2", "Armored Core 3 (USA).chd");

        var key = "ps2[\"Armored Core 3 (USA).chd\"].pcsx2_slot1_memory";
        fixture.SetSetting(key, "standard");

        // RomMBat is allowed to take over a value it recognises as the stock one only when it
        // recorded writing it, so simulate the recorded case by converting from a prior record.
        fixture.Store.SaveConversions.Record(new SaveConversion
        {
            RomId = 42,
            System = "ps2",
            FsName = "Armored Core 3 (USA).chd",
            SettingKey = Ps2Key,
            AppliedValue = "standard",
            PriorState = PriorSettingState.Present,
            PriorValue = "standard",
            ConvertedAtUtc = DateTimeOffset.UtcNow,
        });

        var result = fixture.Converter().Revert(42);

        Assert.Equal(ConversionStatus.Reverted, result.Status);
        Assert.Equal("standard", fixture.Settings().Value(key));
        Assert.Empty(fixture.Store.SaveConversions.List());
    }

    [Fact]
    public void Reverting_something_never_converted_is_refused_rather_than_guessed()
    {
        using var fixture = ConvertTree.Create();
        fixture.AddRom(42, "ps2", "Armored Core 3 (USA).chd");

        var result = fixture.Converter().Revert(42);

        Assert.Equal(ConversionStatus.Refused, result.Status);
        Assert.Contains("no record", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Reverting_is_refused_when_somebody_changed_the_value_since()
    {
        using var fixture = ConvertTree.Create();
        fixture.AddRom(42, "ps2", "Armored Core 3 (USA).chd");
        fixture.Converter().Convert(42);

        fixture.SetSetting("ps2[\"Armored Core 3 (USA).chd\"].pcsx2_slot1_memory", "folder");

        var result = fixture.Converter().Revert(42);

        Assert.Equal(ConversionStatus.Refused, result.Status);
        Assert.Contains("changed it since", result.Detail, StringComparison.Ordinal);

        // The record survives, because the conversion is still in force as far as anyone knows.
        Assert.Single(fixture.Store.SaveConversions.List());
    }

    [Fact]
    public void A_conversion_preserves_every_other_setting_in_the_file()
    {
        using var fixture = ConvertTree.Create();
        fixture.AddRom(42, "ps2", "Armored Core 3 (USA).chd");
        fixture.SetSetting("ps2.emulator", "pcsx2");
        fixture.SetSetting("ThemeSet", "es-theme-carbon");
        fixture.SetSetting("some.key.ES.invented", "whatever");

        fixture.Converter().Convert(42);

        var after = fixture.Settings();
        Assert.Equal("pcsx2", after.Value("ps2.emulator"));
        Assert.Equal("es-theme-carbon", after.Value("ThemeSet"));
        Assert.Equal("whatever", after.Value("some.key.ES.invented"));

        // And it does not reach for the system scope, which the per-game key outranks.
        Assert.False(after.Has("ps2.pcsx2_slot1_memory"));
    }


    [Fact]
    public void No_EmulationStation_from_this_install_reads_as_not_running()
    {
        // The refusal path itself cannot be reached from a test, because it needs a real ES
        // process, and it is verified by hand instead: see docs/platforms/README.md. What is
        // testable is the half that decides which install a process belongs to, and that half
        // is the one that would produce a refusal the user cannot act on. A test tree has no ES
        // under it, and whatever is running elsewhere on the machine must not count.
        using var fixture = ConvertTree.Create();

        var verdict = EmulationStationProcess.Check(fixture.Install);

        Assert.False(verdict.IsRunning);
        Assert.Null(verdict.Detail);
    }


    [Fact]
    public void Moving_the_whole_install_leaves_the_conversion_and_its_card_exactly_as_they_were()
    {
        // The relocation check with an es_settings.cfg override present, which is the one the
        // brief names. RetroBat is portable and the drive letter changes, so a rescan after a
        // move has to be a clean no-op. The override is the new thing at risk: it names a ROM
        // filename, never a path, so nothing in it can go stale on a move.
        using var fixture = ConvertTree.Create();
        fixture.AddRom(191723, "ps2", "Armored Core 3 (USA).chd");
        fixture.AddSave("ps2", "pcsx2/memcards/Armored Core 3 (USA).ps2", "one game's card");

        Assert.Equal(ConversionStatus.Converted, fixture.Converter().Convert(191723).Status);

        var before = new SaveScanner(fixture.Install, fixture.Store).Scan();
        var savesBefore = fixture.Store.Saves.List()
            .Select(save => (save.Path.Value, save.ShapeClass, save.ContentHash, save.RomId))
            .ToList();
        var settingsBefore = fixture.Settings().Settings.ToList();
        var conversionBefore = Assert.Single(fixture.Store.SaveConversions.List());

        Assert.Equal(1, before.Found);

        using var moved = fixture.CopyToNewLocation();

        var outcome = new SaveScanner(moved.Install, moved.Store).Scan();

        // Nothing forgotten and nothing re-added, which is what a stored absolute path breaks.
        Assert.Equal(0, outcome.Forgotten);
        Assert.Equal(1, outcome.Found);
        Assert.Equal(1, outcome.Attributed);

        Assert.Equal(
            savesBefore,
            moved.Store.Saves.List()
                .Select(save => (save.Path.Value, save.ShapeClass, save.ContentHash, save.RomId)));

        // The override travelled as written, because its key is a rom filename and not a path.
        Assert.Equal(settingsBefore, moved.Settings().Settings.ToList());
        Assert.Equal(
            "game",
            moved.Settings().Value("ps2[\"Armored Core 3 (USA).chd\"].pcsx2_slot1_memory"));

        var conversionAfter = Assert.Single(moved.Store.SaveConversions.List());
        Assert.Equal(conversionBefore, conversionAfter);

        // And nothing anywhere mentions a drive letter.
        Assert.All(moved.Store.Saves.List(), save => Assert.False(Path.IsPathRooted(save.Path.Value)));
    }

    [Fact]
    public void Every_path_a_conversion_constructs_stays_relative_and_inside_the_tree()
    {
        // The two portability rules applied to what this stage constructs: the container path
        // the shape declares, and the es_settings.cfg location the writer resolves.
        using var fixture = ConvertTree.Create();
        fixture.AddRom(191723, "ps2", "Armored Core 3 (USA).chd");
        fixture.AddSave("ps2", "pcsx2/memcards/Armored Core 3 (USA).ps2", "a card");
        fixture.Converter().Convert(191723);
        new SaveScanner(fixture.Install, fixture.Store).Scan();

        var limits = FilesystemLimits.For("FAT32", availableFreeBytes: 64L * 1024 * 1024 * 1024);

        Assert.False(Path.IsPathRooted(EsSettingsFile.Location.Value));
        Assert.DoesNotContain("..", EsSettingsFile.Location.Value, StringComparison.Ordinal);
        Assert.True(fixture.Install.Contains(fixture.Install.Resolve(EsSettingsFile.Location)));

        foreach (var save in fixture.Store.Saves.List())
        {
            Assert.False(Path.IsPathRooted(save.Path.Value));
            Assert.StartsWith("saves/", save.Path.Value, StringComparison.Ordinal);
            Assert.DoesNotContain("..", save.Path.Value, StringComparison.Ordinal);
            Assert.True(fixture.Install.Contains(fixture.Install.Resolve(save.Path)));
            Assert.True(limits.CanHold(save.SizeBytes));
        }

        // The stored conversion names a rom filename, never a path, so a move cannot stale it.
        var conversion = Assert.Single(fixture.Store.SaveConversions.List());
        Assert.DoesNotContain('/', conversion.FsName);
        Assert.DoesNotContain('\\', conversion.FsName);
        Assert.DoesNotContain('/', conversion.System);
    }

    [Fact]
    public void Converting_and_reverting_need_no_server_at_all()
    {
        // Offline is a working state, and a conversion is a local operation by design: it
        // changes where an emulator will write, which is nobody's business but this device's.
        // The fixture holds no connection and none of this reaches for one.
        using var fixture = ConvertTree.Create();
        fixture.AddRom(191723, "ps2", "Armored Core 3 (USA).chd");

        Assert.Equal(ConversionStatus.Ready, fixture.Converter().Preview(191723).Status);
        Assert.Equal(ConversionStatus.Converted, fixture.Converter().Convert(191723).Status);
        Assert.Equal(ConversionStatus.NoChange, fixture.Converter().Convert(191723).Status);
        Assert.Equal(ConversionStatus.Reverted, fixture.Converter().Revert(191723).Status);

        // Idempotent under replay in both directions: a repeated revert refuses rather than
        // half-restoring something, and neither direction queues anything for a server.
        Assert.Equal(ConversionStatus.Refused, fixture.Converter().Revert(191723).Status);
        Assert.Empty(fixture.Store.SaveConversions.List());
        Assert.Equal(0, fixture.Store.Outbox.PendingCount());
    }

    private sealed class ConvertTree : IDisposable
    {
        private readonly TempRetroBatTree _tree;

        private ConvertTree(TempRetroBatTree tree, RetroBatInstall install, LocalStore store)
        {
            _tree = tree;
            Install = install;
            Store = store;
        }

        public RetroBatInstall Install { get; }

        public LocalStore Store { get; }

        public static ConvertTree Create()
        {
            var tree = TempRetroBatTree.Create();
            var install = tree.Install();
            return new ConvertTree(tree, install, LocalStore.Open(install));
        }

        /// <summary>The same tree at a different root, which is the drive-letter change.</summary>
        public ConvertTree CopyToNewLocation()
        {
            Store.Dispose();
            var moved = _tree.CopyToNewLocation();
            var install = moved.Install();
            return new ConvertTree(moved, install, LocalStore.OpenAt(install.DatabasePath));
        }

        public SaveConverter Converter() => new(Install, Store);

        public EsSettingsFile Settings() => EsSettingsFile.Load(Install.Resolve(EsSettingsFile.Location));

        public void SetSetting(string name, string value)
        {
            var path = Install.Resolve(EsSettingsFile.Location);
            var file = EsSettingsFile.Load(path);
            file.Set(name, value);
            file.WriteIfChanged(path);
        }

        public void AddRom(long romId, string folder, string fileName)
        {
            var path = RelativePath.Create($"roms/{folder}/{fileName}");
            var absolute = Install.Resolve(path);

            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllText(absolute, "rom bytes");

            Store.Files.Record(new LocalFile
            {
                Path = path,
                Folder = folder,
                RomId = (int)romId,
                Kind = LocalFileKind.Rom,
                FileName = fileName,
                SizeBytes = 9,
            });
        }

        public void AddSave(string system, string relative, string contents)
        {
            var absolute = Install.Resolve(RelativePath.Create($"saves/{system}/{relative}"));
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllText(absolute, contents);
        }

        public void Dispose()
        {
            Store.Dispose();
            _tree.Dispose();
        }
    }
}
