using RomMBat.Core.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Working out which ROM a directory save belongs to, and refusing to guess when it cannot.
/// </summary>
/// <remarks>
/// The fail-closed direction is the whole point. A wrong binding uploads one game's save under
/// another game's name, and the cache then makes the mistake permanent, so every test here that
/// asserts a refusal is asserting the more important half.
/// </remarks>
public class GameIdAttributionTests
{
    [Fact]
    public void An_rvz_game_code_is_read_at_0x58_with_the_version_checked()
    {
        // Confirmed on 218 real images, all format version 1: gamecube 178 of 178 and wii 40 of
        // 40 .rvz. The version check is not decoration, since a later revision that moves the
        // embedded disc header moves this offset with it.
        Assert.Equal("GW7E", RomGameId.Parse(Rvz("GW7E", version: 1)).GameId);
        Assert.Equal("RUUE", RomGameId.Parse(Rvz("RUUE", version: 1)).GameId);

        var newer = RomGameId.Parse(Rvz("GW7E", version: 2));

        Assert.Null(newer.GameId);
        Assert.Contains("format version 2", newer.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void A_wad_is_refused_rather_than_misread()
    {
        // 13 of 53 Wii images on a real install. Its title id sits inside the ticket behind a
        // certificate chain of variable length, so no constant offset reaches it, and the
        // dangerous outcome is not "no answer" but "a confident wrong answer".
        var head = new byte[256];
        head[3] = 0x20;
        head[4] = (byte)'I';
        head[5] = (byte)'s';

        var read = RomGameId.Parse(head);

        Assert.Null(read.GameId);
        Assert.Contains("WAD", read.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void An_rvz_read_at_offset_zero_would_have_taken_the_magic_for_a_game_code()
    {
        // The trap F17 names. A reader that handles only raw .iso resolves nothing on a real
        // library and would read the literal bytes "RVZ." as a code, so the raw-image path
        // checks the disc magic rather than the shape of the first four bytes.
        var head = Rvz("GW7E", version: 1);

        Assert.Equal("RVZ"u8.ToArray(), head[..4]);
        Assert.Equal("GW7E", RomGameId.Parse(head).GameId);
    }

    [Theory]
    [InlineData("CISO", "compressed UMD")]
    [InlineData("MCom", "CHD")]
    public void A_container_with_no_header_in_the_clear_is_refused_with_its_reason(string magic, string expected)
    {
        // psp is 147 .cso and 7 .chd and psx is 386 .chd, all measured at 0% readable. Saying
        // which container it is, rather than "unattributed", is what tells a user this is a
        // property of their library rather than a fault.
        var head = new byte[256];
        System.Text.Encoding.ASCII.GetBytes(magic).CopyTo(head, 0);
        head[4] = (byte)'p';

        var read = RomGameId.Parse(head);

        Assert.Null(read.GameId);
        Assert.Contains(expected, read.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void A_launch_covering_when_the_unit_was_written_attributes_it()
    {
        using var fixture = new AttributionFixture();
        fixture.AddRom("wii", "Wii Sports (USA).rvz", romId: 41);

        var unit = fixture.WriteWiiUnit("52534245", written: fixture.Now.AddMinutes(-5));

        var attributor = fixture.Attributor(
            Launch(fixture.Now.AddMinutes(-10), "wii", "roms/wii/Wii Sports (USA).rvz"));

        var attributed = attributor.Attribute(unit);

        Assert.Equal(41, attributed.RomId);
        Assert.Equal(BindingSource.Journal, attributed.Source);
        Assert.Contains("was running when", attributed.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_launch_of_another_system_does_not_attribute_a_unit()
    {
        // The system is half the key, for the same reason it is half of RomIndex's: Contra and
        // Tetris exist on several systems and a save must never cross one.
        using var fixture = new AttributionFixture();
        fixture.AddRom("mastersystem", "Phantasy Star (Brazil).zip", romId: 7);

        var unit = fixture.WriteWiiUnit("52534245", written: fixture.Now.AddMinutes(-5));

        var attributed = fixture
            .Attributor(Launch(fixture.Now.AddMinutes(-10), "mastersystem", "roms/mastersystem/Phantasy Star (Brazil).zip"))
            .Attribute(unit);

        Assert.Null(attributed.RomId);
    }

    [Fact]
    public void Two_launches_covering_one_window_are_refused_rather_than_guessed_between()
    {
        // exFAT and FAT32 both quantise mtime to two seconds and round up, so two launches this
        // close cannot be separated by when a file says it was written. Picking the later one
        // would upload one game's save under the other's name.
        using var fixture = new AttributionFixture();
        fixture.AddRom("wii", "Wii Sports (USA).rvz", romId: 41);
        fixture.AddRom("wii", "Wii Play (USA).rvz", romId: 42);

        var unit = fixture.WriteWiiUnit("52534245", written: fixture.Now);

        var attributed = fixture
            .Attributor(
                Launch(fixture.Now.AddSeconds(-3), "wii", "roms/wii/Wii Sports (USA).rvz"),
                Launch(fixture.Now.AddSeconds(-1), "wii", "roms/wii/Wii Play (USA).rvz"))
            .Attribute(unit);

        Assert.Null(attributed.RomId);
        Assert.Contains("no route could say", attributed.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void An_es_menu_launch_never_attributes_a_save()
    {
        // RomMBat's own exit is an es_menu launch, and 27 of 424 launches on a real install
        // were. Attributing a save to one is how a user gets a save against a game they never
        // played.
        using var fixture = new AttributionFixture();
        var unit = fixture.WriteWiiUnit("52534245", written: fixture.Now);

        var menu = new LaunchRecord(
            fixture.Now.AddMinutes(-1),
            RelativePath.Create("system/es_menu/start.exe"),
            "wii",
            "libretro",
            null,
            IsMenuLaunch: true,
            "menu");

        Assert.Null(fixture.Attributor(menu).Attribute(unit).RomId);
    }

    [Fact]
    public void A_learned_binding_is_reused_without_reading_the_rom_again()
    {
        // The cache is what makes an odd case cost one lookup rather than one per scan. Proven
        // by deleting the ROM after the first pass: nothing can read a header off a file that
        // is gone, so a second success can only have come from the binding.
        using var fixture = new AttributionFixture();
        var romPath = fixture.AddRom("wii", "Wii Sports (USA).rvz", romId: 41, gameCode: "RSBE");

        var unit = fixture.WriteWiiUnit("52534245", written: fixture.Now);

        var first = fixture.Attributor().Attribute(unit);

        Assert.Equal(41, first.RomId);
        Assert.Equal(BindingSource.RomHeader, first.Source);

        File.Delete(fixture.Install.Resolve(romPath));

        var second = fixture.Attributor().Attribute(unit);

        Assert.Equal(41, second.RomId);
        Assert.Equal(BindingSource.RomHeader, second.Source);
    }

    [Fact]
    public void A_wrong_binding_can_be_corrected_and_the_correction_sticks()
    {
        // "How is a wrong binding unlearned" is the question the cache creates, and it has to
        // have an answer or the first mistake is permanent.
        using var fixture = new AttributionFixture();
        fixture.AddRom("wii", "Wii Sports (USA).rvz", romId: 41, gameCode: "RSBE");

        var unit = fixture.WriteWiiUnit("52534245", written: fixture.Now);

        Assert.Equal(41, fixture.Attributor().Attribute(unit).RomId);

        fixture.Store.GameIdBindings.Record(new GameIdBinding(
            "wii",
            "RSBE",
            99,
            RelativePath.Create("roms/wii/Something Else.rvz"),
            BindingSource.User,
            "corrected by hand",
            fixture.Now));

        var corrected = fixture.Attributor().Attribute(unit);

        Assert.Equal(99, corrected.RomId);
        Assert.Equal(BindingSource.User, corrected.Source);

        // And forgetting one lets the routes run again from scratch.
        Assert.True(fixture.Store.GameIdBindings.Forget("wii", "RSBE"));
        Assert.Equal(41, fixture.Attributor().Attribute(unit).RomId);
    }

    [Fact]
    public void Nothing_resolving_a_unit_is_not_cached_so_the_rom_arriving_later_still_attributes_it()
    {
        // Found on a real install, not by reasoning: a MAME nvram tree with no MAME ROMs beside
        // it produced 1,231 unattributable units in one scan, and caching each refusal would
        // have meant a later sync bringing those ROMs in left every save still unattributed
        // behind a stale row nothing clears.
        //
        // A refusal is only worth remembering when it is a decision. "Nothing had anything to
        // say" is an absence, and it must not outlive its own reason.
        using var fixture = new AttributionFixture();

        fixture.WriteFile("saves/mame/nvram/25pacman/eeprom", "nvram nobody can place yet");

        var unit = Assert.Single(new SaveUnitScanner(fixture.Install).Scan("mame"));
        var first = fixture.Attributor().Attribute(unit);

        Assert.Null(first.RomId);
        Assert.Equal(AttributionOutcome.NotFound, first.Outcome);

        // The important half: nothing was written down.
        Assert.Empty(fixture.Store.GameIdBindings.List());

        // The ROM arrives on a later sync, and the same unit now attributes with no
        // intervention and nothing to forget.
        fixture.AddRom("mame", "25pacman.zip", romId: 8);

        var second = fixture.Attributor().Attribute(unit);

        Assert.Equal(8, second.RomId);
    }

    [Fact]
    public void Two_sidecars_naming_one_identifier_resolve_first_wins_like_the_header_route_does()
    {
        // The sidecar index took last-wins where the ROM-header index takes first-wins, so the
        // two routes answered the same question by opposite rules and which ROM won depended on
        // where a state sorted in local_state. The scan is ordered by relative path, so first is
        // a stable answer; last was stable too, and inconsistent with the route beside it.
        using var fixture = new AttributionFixture();

        // Game codes that are not RSBE, so the header route has nothing to say and this is a
        // test about the sidecar index rather than about the fail-closed rule.
        var play = fixture.AddRom("wii", "Wii Play (USA).rvz", romId: 42, gameCode: "RZTE");
        var sports = fixture.AddRom("wii", "Wii Sports (USA).rvz", romId: 41, gameCode: "RSPE");

        // Both states claim the same native identifier. Their paths carry the ROM stems, so
        // Wii Play sorts first.
        fixture.AddStateSidecar("wii", play, romId: 42, native: "RSBE_1.00");
        fixture.AddStateSidecar("wii", sports, romId: 41, native: "RSBE_1.00");

        var unit = fixture.WriteWiiUnit("52534245", written: fixture.Now);
        var attributed = fixture.Attributor().Attribute(unit);

        Assert.Equal(AttributionOutcome.Resolved, attributed.Outcome);
        Assert.Equal(42, attributed.RomId);
        Assert.Equal(BindingSource.Sidecar, attributed.Source);
    }

    [Fact]
    public void A_contested_key_is_cached_because_that_one_is_a_decision()
    {
        // The other side of the same rule. Both routes read something real and disagree, so
        // re-deriving it every scan would re-report it every scan without ever changing the
        // answer, and only a person can settle it.
        using var fixture = new AttributionFixture();
        fixture.AddRom("wii", "Wii Sports (USA).rvz", romId: 41, gameCode: "RSBE");
        var other = fixture.AddRom("wii", "Wii Play (USA).rvz", romId: 42, gameCode: "RZTE");
        fixture.AddStateSidecar("wii", other, romId: 42, native: "RSBE_1.00");

        var unit = fixture.WriteWiiUnit("52534245", written: fixture.Now);
        var attributed = fixture.Attributor().Attribute(unit);

        Assert.Equal(AttributionOutcome.Contested, attributed.Outcome);

        var stored = Assert.Single(fixture.Store.GameIdBindings.List());

        Assert.False(stored.IsResolved);
    }

    [Fact]
    public void Two_routes_disagreeing_refuses_and_records_that_it_refused()
    {
        // Both routes are right about what they read and they name different games, which is
        // exactly the case where guessing costs a save. The refusal is stored so the same work
        // is not redone and re-reported on every scan.
        using var fixture = new AttributionFixture();
        fixture.AddRom("wii", "Wii Sports (USA).rvz", romId: 41, gameCode: "RSBE");
        var other = fixture.AddRom("wii", "Wii Play (USA).rvz", romId: 42, gameCode: "RZTE");

        // The sidecar says RSBE belongs to Wii Play; the header says it belongs to Wii Sports.
        fixture.AddStateSidecar("wii", other, romId: 42, native: "RSBE_1.00");

        var unit = fixture.WriteWiiUnit("52534245", written: fixture.Now);
        var attributed = fixture.Attributor().Attribute(unit);

        Assert.Null(attributed.RomId);
        Assert.Contains("two routes disagree", attributed.Detail, StringComparison.Ordinal);
        Assert.Contains("saves bind", attributed.Detail, StringComparison.Ordinal);

        var stored = fixture.Store.GameIdBindings.Find("wii", "RSBE");

        Assert.NotNull(stored);
        Assert.False(stored.IsResolved);
        Assert.Contains("two routes disagree", stored.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_contested_row_records_that_nothing_taught_it_rather_than_naming_a_route()
    {
        // The row asserts a provenance, and `saves` prints detail rather than the source for an
        // unresolved one, so a wrong value here is both invisible and durable. Migration 008 is
        // the authority: a reviewer deciding whether to keep a binding needs to know what it
        // rests on, and this one rests on nothing.
        using var fixture = new AttributionFixture();
        fixture.AddRom("wii", "Wii Sports (USA).rvz", romId: 41, gameCode: "RSBE");
        var other = fixture.AddRom("wii", "Wii Play (USA).rvz", romId: 42, gameCode: "RZTE");

        // A sidecar and a ROM header disagreeing, with no launch involved at all.
        fixture.AddStateSidecar("wii", other, romId: 42, native: "RSBE_1.00");

        var unit = fixture.WriteWiiUnit("52534245", written: fixture.Now);

        Assert.Equal(AttributionOutcome.Contested, fixture.Attributor().Attribute(unit).Outcome);

        var stored = Assert.Single(fixture.Store.GameIdBindings.List());

        Assert.Equal(BindingSource.Contested, stored.LearnedFrom);
        Assert.False(stored.IsResolved);
    }

    [Fact]
    public void A_binding_typed_in_lower_case_is_the_one_the_scan_reads()
    {
        // `saves bind psp ules01513 42` types what a person read off a report; the attributor
        // only ever looks up ULES01513, because SaveUnitPath.KeyOf upper-cases every key it
        // reads off the tree. Without NOCASE on the column the insert succeeded, the command
        // said the next scan would use it, and nothing ever did.
        using var fixture = new AttributionFixture();
        var rom = fixture.AddRom("psp", "3rd Birthday, The (Europe).cso", romId: 42);

        fixture.Store.GameIdBindings.Record(new GameIdBinding(
            "psp",
            "ules01513",
            42,
            rom,
            BindingSource.User,
            "bound by hand",
            fixture.Now));

        var found = fixture.Store.GameIdBindings.Find("PSP", "ULES01513");

        Assert.NotNull(found);
        Assert.Equal(42, found.RomId);

        // And the same key does not become a second row, whichever case it arrives in.
        fixture.Store.GameIdBindings.Record(new GameIdBinding(
            "psp",
            "ULES01513",
            43,
            rom,
            BindingSource.User,
            "bound again",
            fixture.Now));

        Assert.Equal(43, Assert.Single(fixture.Store.GameIdBindings.List()).RomId);
        Assert.True(fixture.Store.GameIdBindings.Forget("psp", "ules01513"));
    }

    [Fact]
    public void A_mame_short_name_resolves_by_filename_and_learns_nothing()
    {
        // The friendly case, and the reason MAME proves bundling without any attribution at
        // all. Nothing was learned, so nothing is cached: a binding row here would be a fact
        // about the ROM index rather than about a Game ID.
        using var fixture = new AttributionFixture();
        fixture.AddRom("mame", "25pacman.zip", romId: 8);

        fixture.WriteFile("saves/mame/nvram/25pacman/eeprom", "nvram");

        var unit = Assert.Single(new SaveUnitScanner(fixture.Install).Scan("mame"));
        var attributed = fixture.Attributor().Attribute(unit);

        Assert.Equal(8, attributed.RomId);
        Assert.Null(attributed.Source);
        Assert.Empty(fixture.Store.GameIdBindings.List());
    }

    /// <summary>An <c>.rvz</c> head: the magic, a format version, and a code at 0x58.</summary>
    private static byte[] Rvz(string code, uint version)
    {
        var head = new byte[256];
        "RVZ"u8.ToArray().CopyTo(head, 0);
        BitConverter.GetBytes(version).CopyTo(head, 4);
        System.Text.Encoding.ASCII.GetBytes(code).CopyTo(head, 0x58);
        return head;
    }

    private static LaunchRecord Launch(DateTimeOffset at, string system, string romPath) =>
        new(at, RelativePath.Create(romPath), system, "dolphin", null, IsMenuLaunch: false, $"{at:O}|{romPath}");

    private sealed class AttributionFixture : IDisposable
    {
        private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();

        public AttributionFixture()
        {
            Install = _tree.Install();
            Store = LocalStore.Open(Install);
        }

        public DateTimeOffset Now { get; } = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

        public RetroBatInstall Install { get; }

        public LocalStore Store { get; }

        public RelativePath AddRom(string folder, string fileName, long romId, string? gameCode = null)
        {
            var path = RelativePath.Create($"roms/{folder}/{fileName}");
            var absolute = Install.Resolve(path);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);

            if (gameCode is not null)
            {
                var head = new byte[256];
                "RVZ"u8.ToArray().CopyTo(head, 0);
                BitConverter.GetBytes(1u).CopyTo(head, 4);
                System.Text.Encoding.ASCII.GetBytes(gameCode).CopyTo(head, 0x58);
                File.WriteAllBytes(absolute, head);
            }
            else
            {
                File.WriteAllText(absolute, "a rom");
            }

            Store.Files.Record(new LocalFile
            {
                Path = path,
                Kind = LocalFileKind.Rom,
                RomId = (int)romId,
                Folder = folder,
                FileName = fileName,
                SizeBytes = new FileInfo(absolute).Length,
            });

            return path;
        }

        public void AddStateSidecar(string system, RelativePath romPath, long romId, string native)
        {
            var statePath = RelativePath.Create($"saves/{system}/dolphin/{Path.GetFileNameWithoutExtension(romPath.Value)}.state1");
            WriteFile(statePath.Value, "a state");

            Store.States.Record(
                new LocalState
                {
                    Path = statePath,
                    System = system,
                    Emulator = "dolphin",
                    Slot = "dolphin::1",
                    RomId = (int)romId,
                    RomPath = romPath,
                    ContentHash = new string('a', 32),
                    SizeBytes = 7,
                    NativeName = native,
                    UploadedFileName = $"{Path.GetFileNameWithoutExtension(romPath.Value)} [dolphin].state1",
                },
                Now);
        }

        public SaveUnit WriteWiiUnit(string hexCode, DateTimeOffset written)
        {
            var path = $"saves/wii/dolphin-emu/User/Wii/title/00010000/{hexCode}/data/save.bin";
            WriteFile(path, "the save");
            File.SetLastWriteTimeUtc(Install.Resolve(path), written.UtcDateTime);

            return Assert.Single(new SaveUnitScanner(Install).Scan("wii"));
        }

        public void WriteFile(string relativePath, string content)
        {
            var absolute = Install.Resolve(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllText(absolute, content);
        }

        public GameIdAttributor Attributor(params LaunchRecord[] launches) =>
            new(Install, Store, RomIndex.Build(Store), launches, new TestTimeProvider(Now));

        public void Dispose()
        {
            Store.Dispose();
            _tree.Dispose();
        }
    }
}
