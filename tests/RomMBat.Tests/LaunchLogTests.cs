using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Reading launch facts out of <c>emulatorLauncher.log</c>.
/// </summary>
/// <remarks>
/// Driven against <c>fixtures/emulatorLauncher.log</c>, which is twelve lines cut verbatim
/// from a real install's five months of history with only the user profile path replaced.
/// Every trap the M6 probe found has one line in it, so a parser regression fails here rather
/// than on someone's handheld.
/// </remarks>
public class LaunchLogTests
{
    private static string Fixture => Path.Combine(AppContext.BaseDirectory, "fixtures", "emulatorLauncher.log");

    [Fact]
    public void Only_the_lines_that_are_a_game_launch_are_read()
    {
        // 730 [Startup] lines against 424 launches on the real file. The discriminator is
        // -rom, not [Startup]: an -updatestores invocation is not a game.
        var records = Read(out _);

        Assert.Equal(6, records.Count);
        Assert.All(records, record => Assert.NotEqual(default, record.At));

        // Oldest first, which the reader guarantees rather than inherits. The fixture is
        // deliberately assembled by trap rather than chronologically, so a reader that merely
        // trusted file order would hand back the wrong last record and move the cursor
        // backwards. The real file is in order; nothing here depends on that.
        Assert.Equal(records.Select(record => record.At).Order(), records.Select(record => record.At));
    }

    [Fact]
    public void A_rom_path_from_a_previous_drive_letter_still_relativises()
    {
        // The finding that changes a design rather than a constant. 295 of 424 launches on the
        // measured install read D:\RetroBat and 129 read E:\RetroBat, one install, one log,
        // because the drive letter moved. Stripping the current root would discard 70% of it.
        var records = Read(out _);

        var fromOldLetter = Assert.Single(records, record => record.System == "nes");
        Assert.Equal("roms/nes/Jackal (USA).zip", fromOldLetter.RomPath?.Value);

        var fromCurrentLetter = Assert.Single(records, record => record.System == "gb");
        Assert.Equal(
            "roms/gb/Adventure Island II - Aliens in Paradise (USA, Europe).zip",
            fromCurrentLetter.RomPath?.Value);

        // And nothing rooted reached a record, which is rule 1 at this boundary.
        Assert.All(records, record => Assert.True(record.RomPath is null || record.RomPath.Value.HasValue));
    }

    [Fact]
    public void An_unquoted_rom_argument_carrying_spaces_and_commas_is_read_whole()
    {
        // One line in 424, and a -rom "([^"]+)" regex loses it silently.
        var records = Read(out _);

        var psp = Assert.Single(records, record => record.System == "psp");
        Assert.Equal("roms/psp/Patapon (Europe) (En,Fr,De,Es,It).cso", psp.RomPath?.Value);
        Assert.Equal("ppsspp", psp.Emulator);
    }

    [Fact]
    public void A_flag_written_after_the_rom_is_still_found()
    {
        // -core sits after -rom on 5 of 424 launches, so flags are scanned rather than read
        // positionally.
        var records = Read(out _);

        var snes = Assert.Single(records, record => record.System == "snes");
        Assert.Equal("libretro", snes.Emulator);
        Assert.Equal("snes9x", snes.Core);
        Assert.Equal("roms/snes/ActRaiser (USA).zip", snes.RomPath?.Value);
    }

    [Fact]
    public void A_present_but_empty_core_reads_as_absent_rather_than_as_the_next_flag()
    {
        // "-core  -rom ..." is how an empty core is written, and reading the next token would
        // make the core the string "-rom". Empty on 200 of 424 launches.
        var records = Read(out _);

        var psx = Assert.Single(records, record => record.System == "psx");
        Assert.Equal("duckstation", psx.Emulator);
        Assert.Null(psx.Core);
        Assert.Equal(
            "roms/psx/Metal Gear Solid (USA) (Disc 1) (Rev 1)/Metal Gear Solid (USA) (Disc 1) (Rev 1).cue",
            psx.RomPath?.Value);
    }

    [Fact]
    public void An_es_menu_launch_is_flagged_rather_than_taken_for_a_game()
    {
        // 27 of 424, and this is what stops RomMBat's own exit becoming a play session: the
        // menu launch that precedes that game-end is identifiable in the log.
        var records = Read(out _);

        var menu = Assert.Single(records, record => record.IsMenuLaunch);
        Assert.Equal("retrobat", menu.System);
        Assert.Equal("system/es_menu/eden.menu", menu.RomPath?.Value);
        Assert.All(records.Where(record => !record.IsMenuLaunch), record => Assert.NotEqual("retrobat", record.System));
    }

    [Fact]
    public void A_BOM_a_separator_a_stack_trace_and_a_failed_launch_are_all_skipped()
    {
        // Four line shapes that are not launches, all present in the fixture verbatim. The BOM
        // is the one that matters most: it sits on the first line of every rotation half, so a
        // parser that chokes on it loses the oldest launches rather than none.
        var log = new LaunchLog(new RetroBatInstall(Path.GetTempPath(), RootDiscoverySource.Explicit));

        Assert.Null(log.Parse("\uFEFF2026-06-11 08:29:01.906 [INFO]      ------------------------"));
        Assert.Null(log.Parse("   at System.IO.FileStream..ctor(String path, FileMode mode)"));
        Assert.Null(log.Parse("2026-06-17 17:30:19.594 [ERROR]     [Generator] Failed. path is null"));
        Assert.Null(log.Parse("2026-06-11 09:21:59.700 [INFO]      [Generator] Process exited with code 0"));
        Assert.Null(log.Parse(string.Empty));
    }

    [Fact]
    public void A_second_read_from_the_recorded_position_returns_nothing()
    {
        // The whole point of the cursor: a flush that runs on every game-end must not replay
        // five months of launches each time.
        var records = Read(out var log);
        var position = log.PositionAfter(records);

        Assert.Empty(log.Read(position));

        // And a position taken before the last launch returns exactly the tail.
        var earlier = new LaunchLogPosition(records[^3].At, records[^3].Signature, position.LiveSizeBytes);
        Assert.Equal(2, log.Read(earlier).Count);
    }

    [Fact]
    public void The_cursor_round_trips_through_the_store()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        using var store = LocalStore.Open(install);

        // Nothing read yet reads everything, which is what a first run wants: the history is
        // what attributes a save produced before RomMBat was installed.
        var fresh = store.LaunchCursor.Read();
        Assert.Null(fresh.At);
        Assert.Null(fresh.Signature);

        var position = new LaunchLogPosition(
            DateTimeOffset.Parse("2026-08-16T11:22:18Z", System.Globalization.CultureInfo.InvariantCulture),
            "abc123def4567890",
            503225);

        store.LaunchCursor.Write(position, DateTimeOffset.UnixEpoch);

        var read = store.LaunchCursor.Read();
        Assert.Equal(position.At, read.At);
        Assert.Equal(position.Signature, read.Signature);
        Assert.Equal(503225, read.LiveSizeBytes);
    }

    [Fact]
    public void A_rotation_is_noticed_by_the_live_file_getting_smaller()
    {
        // Rotation is a ~1 MiB size threshold and the two halves do not overlap, so the read
        // itself is already correct across one. This is what lets status say it happened
        // rather than leaving a gap in the history unexplained.
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        var live = install.Resolve(LaunchLog.LivePath);
        Directory.CreateDirectory(Path.GetDirectoryName(live)!);
        File.WriteAllText(live, new string('x', 4096));

        var log = new LaunchLog(install);
        var before = log.PositionAfter([]);

        Assert.Equal(4096, before.LiveSizeBytes);
        Assert.False(log.HasRotatedSince(before));

        File.Move(live, install.Resolve(LaunchLog.RotatedPath));
        File.WriteAllText(live, "fresh");

        Assert.True(log.HasRotatedSince(before));
    }

    [Fact]
    public void The_rotated_half_is_read_before_the_live_one()
    {
        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        Directory.CreateDirectory(Path.GetDirectoryName(install.Resolve(LaunchLog.LivePath))!);

        // Also the case the sort cannot fix on its own: the rotated half has to be opened at
        // all, and a reader that only looked at the live file would lose three weeks.
        const string Older = "2026-06-01 10:00:00.000 [INFO]      [Startup] \"D:\\RetroBat\\emulationstation"
            + "\\emulatorLauncher.exe\" -system nes -emulator libretro -rom \"D:\\RetroBat\\roms\\nes\\Old.zip\"";
        const string Newer = "2026-07-01 10:00:00.000 [INFO]      [Startup] \"E:\\RetroBat\\emulationstation"
            + "\\emulatorLauncher.exe\" -system snes -emulator libretro -rom \"E:\\RetroBat\\roms\\snes\\New.zip\"";

        File.WriteAllText(install.Resolve(LaunchLog.RotatedPath), Older + "\n");
        File.WriteAllText(install.Resolve(LaunchLog.LivePath), Newer + "\n");

        var records = new LaunchLog(install).Read();

        Assert.Equal(["roms/nes/Old.zip", "roms/snes/New.zip"], records.Select(r => r.RomPath?.Value));
    }

    private static IReadOnlyList<LaunchRecord> Read(out LaunchLog log)
    {
        // The fixture is placed as the live half, so the parser meets it exactly as it meets a
        // real file, BOM and all.
        var tree = TempRetroBatTree.Create();
        var install = tree.Install();
        var live = install.Resolve(LaunchLog.LivePath);

        Directory.CreateDirectory(Path.GetDirectoryName(live)!);
        File.Copy(Fixture, live, overwrite: true);

        log = new LaunchLog(install);
        return log.Read();
    }
}
