using RomMBat.Core.Paths;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// What counts as a game being in flight, and what that stops being written.
/// </summary>
/// <remarks>
/// The ordering issue #155 measured is the reason this exists: a download landing while an
/// emulator holds the file is overwritten by the emulator's own copy on exit, so the other
/// device's save is gone. Every test here is one of the two halves of that, either "is a game
/// running" or "is this file the running game's".
/// </remarks>
public class InFlightGuardTests
{
    [Fact]
    public void Nothing_running_lets_a_save_through()
    {
        using var fixture = GuardFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World).zip");

        var verdict = fixture.Check(7, "saves/gb/Tetris (World).srm");

        Assert.True(verdict.CanWrite);
        Assert.Null(verdict.Reason);
    }

    [Fact]
    public void A_game_end_that_has_not_been_correlated_yet_still_lets_its_save_through()
    {
        // The journal row is closed by correlation, which a flush runs before it downloads. This
        // asserts the ordinary sequence rather than the guard: a game played and finished must
        // not keep its own save out on the pass that follows.
        using var fixture = GuardFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World).zip");
        fixture.Launch(7);
        fixture.GameEnd();
        fixture.Correlate();

        Assert.True(fixture.Check(7, "saves/gb/Tetris (World).srm").CanWrite);
    }

    [Fact]
    public void The_running_games_own_save_is_deferred()
    {
        using var fixture = GuardFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World).zip");
        fixture.Launch(7);

        var verdict = fixture.Check(7, "saves/gb/Tetris (World).srm");

        Assert.False(verdict.CanWrite);
        Assert.Contains("this game is running", verdict.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void Another_games_save_goes_through_while_one_is_running()
    {
        // The narrow half of the rule. One game being played must not stop a library sync: a
        // class A save is one file beside its own rom and no running emulator has it open.
        using var fixture = GuardFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World).zip");
        fixture.AddGame(42, "snes", "ActRaiser (USA).zip");
        fixture.Launch(7);

        Assert.True(fixture.Check(42, "saves/snes/ActRaiser (USA).srm").CanWrite);
    }

    [Fact]
    public void Every_disc_of_a_multi_disc_set_counts_as_the_same_game()
    {
        // One rom id, several files. Launching disc 2 has to defer the save the set shares, and
        // matching on the launched path alone would answer that only disc 1 is in use.
        using var fixture = GuardFixture.Create();
        fixture.AddGame(7, "psx", "Final Fantasy VII (Disc 1).chd");
        fixture.AddGame(7, "psx", "Final Fantasy VII (Disc 2).chd");
        fixture.Launch(7, "roms/psx/Final Fantasy VII (Disc 2).chd");

        Assert.False(fixture.Check(7, "saves/psx/Final Fantasy VII (Disc 1).srm").CanWrite);
    }

    [Fact]
    public void A_running_game_nobody_can_name_defers_every_save()
    {
        // Fail closed, the rule SaveGuard set. The hook is handed an absolute path and records
        // none when it lies outside the tree, which is exactly the case that most needs
        // explaining, so it must not read as no game running.
        using var fixture = GuardFixture.Create();
        fixture.AddGame(42, "snes", "ActRaiser (USA).zip");
        fixture.Store.Journal.Append(JournalEvent.GameStart, fixture.Now, romPath: null);

        var verdict = fixture.Check(42, "saves/snes/ActRaiser (USA).srm");

        Assert.False(verdict.CanWrite);
        Assert.Contains("was not told which one", verdict.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_start_after_a_game_start_ends_it()
    {
        // The bound on a stale row, and the reason it is a sequence and not a clock. A machine
        // that lost power mid-game leaves a game-start open forever, and without this the game
        // would never receive another save.
        using var fixture = GuardFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World).zip");
        fixture.Launch(7);
        Assert.False(fixture.Check(7, "saves/gb/Tetris (World).srm").CanWrite);

        // EmulationStation started again, so whatever was running is not.
        fixture.Store.Journal.Append(JournalEvent.Start, fixture.Now);

        Assert.True(fixture.Check(7, "saves/gb/Tetris (World).srm").CanWrite);
    }

    [Fact]
    public void A_launch_spooled_since_the_drain_is_seen()
    {
        // The window the issue measured. ES is interactive 1.6 to 4.9 s before the start hook
        // fires and the background pass takes 5 to 11 s, so a game launched during the pass has
        // written a .hook file the drain has already gone past. A guard reading only the journal
        // is blind to precisely the ordering that loses the save.
        using var fixture = GuardFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World).zip");
        fixture.SpoolHook("game-start", fixture.Absolute("roms/gb/Tetris (World).zip"));

        var verdict = fixture.Check(7, "saves/gb/Tetris (World).srm");

        Assert.False(verdict.CanWrite);
        Assert.Contains("this game is running", verdict.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_spooled_game_end_closes_a_spooled_launch()
    {
        using var fixture = GuardFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World).zip");
        fixture.SpoolHook("game-start", fixture.Absolute("roms/gb/Tetris (World).zip"));
        fixture.SpoolHook("game-end");

        Assert.True(fixture.Check(7, "saves/gb/Tetris (World).srm").CanWrite);
    }

    [Fact]
    public void A_spooled_quit_ends_a_launch_the_journal_still_holds_open()
    {
        using var fixture = GuardFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World).zip");
        fixture.Launch(7);
        fixture.SpoolHook("quit");

        Assert.True(fixture.Check(7, "saves/gb/Tetris (World).srm").CanWrite);
    }

    [Fact]
    public void A_record_from_a_newer_hook_defers_rather_than_being_passed_over()
    {
        // The drain leaves one of these for a newer agent, which is right for a play session and
        // wrong here: a record this build cannot read is not evidence that no game is running.
        using var fixture = GuardFixture.Create();
        fixture.AddGame(7, "gb", "Tetris (World).zip");
        fixture.SpoolRaw("rommbat-hook-99\nevent=game-start\n");

        Assert.False(fixture.Check(7, "saves/gb/Tetris (World).srm").CanWrite);
    }

    [Fact]
    public void A_shared_unit_container_is_deferred_for_a_sibling_game_on_the_same_system()
    {
        // The widened half. Dolphin's GameCube region directory holds every game's .gci side by
        // side, so swapping another game's members into it is a write under the handle the
        // running game has open. SaveSync's own download path says the container is shared.
        using var fixture = GuardFixture.Create();
        fixture.AddGame(1, "gamecube", "Melee.iso");
        fixture.AddGame(2, "gamecube", "Wind Waker.iso");
        fixture.Launch(1);

        var verdict = fixture.Check(2, "saves/gamecube/dolphin-emu/User/GC/USA/01-GZLE-data.gci");

        Assert.False(verdict.CanWrite);
        Assert.Contains("shared by every gamecube game", verdict.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_shared_container_is_not_deferred_for_a_game_on_another_system()
    {
        using var fixture = GuardFixture.Create();
        fixture.AddGame(1, "gb", "Tetris (World).zip");
        fixture.AddGame(2, "gamecube", "Wind Waker.iso");
        fixture.Launch(1);

        Assert.True(fixture.Check(2, "saves/gamecube/dolphin-emu/User/GC/USA/01-GZLE-data.gci").CanWrite);
    }

    [Fact]
    public void A_loose_save_beside_a_shared_container_goes_through()
    {
        // The declaration names ps2's default memory cards as files, not as a directory, so a
        // per-game card written beside one is a different file and nothing holds it. Deferring
        // on the folder instead would hold back every converted ps2 card for one running game.
        using var fixture = GuardFixture.Create();
        fixture.AddGame(1, "ps2", "Ico.iso");
        fixture.AddGame(2, "ps2", "Okami.iso");
        fixture.Launch(1);

        Assert.True(fixture.Check(2, "saves/ps2/pcsx2/memcards/Okami.ps2").CanWrite);
    }

    private sealed class GuardFixture : IDisposable
    {
        private readonly TempRetroBatTree _tree;
        private readonly RomMBat.Core.Paths.RetroBatInstall _install;
        private int _spooled;

        private GuardFixture(TempRetroBatTree tree, RomMBat.Core.Paths.RetroBatInstall install, LocalStore store)
        {
            _tree = tree;
            _install = install;
            Store = store;
        }

        public LocalStore Store { get; }

        public DateTimeOffset Now { get; } = new(2026, 9, 13, 11, 22, 0, TimeSpan.Zero);

        public static GuardFixture Create()
        {
            var tree = TempRetroBatTree.Create();
            var install = tree.Install();
            return new GuardFixture(tree, install, LocalStore.Open(install));
        }

        public string Absolute(string relative) => _install.Resolve(RelativePath.Create(relative));

        /// <summary>Indexes a rom and puts the file on disk.</summary>
        public void AddGame(int romId, string folder, string fileName)
        {
            var path = RelativePath.Create($"roms/{folder}/{fileName}");
            var absolute = _install.Resolve(path);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllText(absolute, "rom");

            Store.Files.Record(new LocalFile
            {
                Path = path,
                Folder = folder,
                RomId = romId,
                Kind = LocalFileKind.Rom,
                FileName = fileName,
                SizeBytes = 3,
            });
        }

        /// <summary>A game-start with no game-end, which is a game still running.</summary>
        public void Launch(int romId, string? romPath = null)
        {
            var path = romPath is null
                ? Store.Files.List().First(file => file.RomId == romId).Path
                : RelativePath.Create(romPath);

            Store.Journal.Append(JournalEvent.GameStart, Now, path, path.Name, path.Name);
        }

        public void GameEnd() => Store.Journal.Append(JournalEvent.GameEnd, Now.AddMinutes(30));

        public void Correlate() => new PlaytimeCorrelator(_install, Store).Correlate();

        /// <summary>Writes a spool record the way a hook does, undrained.</summary>
        public void SpoolHook(string hookEvent, params string[] arguments) =>
            SpoolRaw(new SpoolRecord(hookEvent, Now.AddSeconds(++_spooled), 4242, arguments).Render());

        public void SpoolRaw(string contents)
        {
            var directory = Path.Combine(_tree.Root, Spool.RelativeDirectory.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(directory);

            // Name order is time order, which is what the guard's reduction relies on.
            var stem = $"{Now.AddSeconds(++_spooled):yyyyMMddTHHmmssfffffff}-4242-{_spooled:D4}";
            File.WriteAllText(Path.Combine(directory, stem + Spool.Extension), contents);
        }

        public InFlightVerdict Check(int romId, string target) =>
            new InFlightGuard(_install, Store).Check(romId, RelativePath.Create(target));

        public void Dispose()
        {
            Store.Dispose();
            _tree.Dispose();
        }
    }
}
