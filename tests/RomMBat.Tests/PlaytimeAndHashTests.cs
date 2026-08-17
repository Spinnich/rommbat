using RomMBat.Core.Content;
using RomMBat.Core.Paths;
using RomMBat.Core.RetroBat;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Correlating play sessions, and the hash everything else rests on.
/// </summary>
public class PlaytimeAndHashTests
{
    [Fact]
    public void The_logical_content_hash_is_the_same_across_two_runs_and_two_enumeration_orders()
    {
        // The precondition for a replayed flush being free: identical content into one slot
        // reuses the server row, and that only holds if "identical" really is identical.
        // Defined over the contents rather than over an archive precisely so a zip library's
        // entry ordering, timestamps or compression level cannot move it.
        var entries = new[]
        {
            ("b/second.bin", "22222222222222222222222222222222"),
            ("a/first.bin", "11111111111111111111111111111111"),
            ("c.bin", "33333333333333333333333333333333"),
        };

        var forward = LogicalContentHash.Fold(entries);
        var reversed = LogicalContentHash.Fold(entries.Reverse());

        Assert.Equal(forward, reversed);
        Assert.Equal(forward, LogicalContentHash.Fold(entries));

        // Case is normalised, so two implementations disagreeing on hex casing still agree.
        Assert.Equal(
            forward,
            LogicalContentHash.Fold(entries.Select(entry => (entry.Item1, entry.Item2.ToUpperInvariant()))));

        // And a different tree really does hash differently.
        Assert.NotEqual(forward, LogicalContentHash.Fold(entries.Take(2)));
    }

    [Fact]
    public void Moving_a_file_within_a_directory_save_changes_its_hash()
    {
        // Paths are folded in, so a save whose files are the same bytes under different names
        // is a different save. That is what stops a rename reading as no change at all.
        Assert.NotEqual(
            LogicalContentHash.Fold([("a.bin", "11111111111111111111111111111111")]),
            LogicalContentHash.Fold([("b.bin", "11111111111111111111111111111111")]));
    }

    [Fact]
    public void An_orphan_game_end_is_discarded_rather_than_attributed_to_whatever_ran_last()
    {
        using var fixture = Journal.Create();

        // A real game, played and finished.
        fixture.Rom(42, "snes", "ActRaiser (USA).zip");
        fixture.Launch("2026-08-16T10:00:00Z", "snes", "libretro", "roms/snes/ActRaiser (USA).zip");
        fixture.Start("2026-08-16T10:00:01Z", "roms/snes/ActRaiser (USA).zip");
        fixture.End("2026-08-16T10:30:00Z");

        // Then a game-end with nothing behind it. A naive implementation gives this one to
        // ActRaiser and reports the user played it twice.
        fixture.End("2026-08-16T11:00:00Z");

        var outcome = fixture.Correlate();

        Assert.Equal(1, outcome.Sessions);
        Assert.Equal(1, outcome.Orphans);

        var queued = Assert.Single(fixture.Store.Outbox.Pending(kind: OutboxKind.PlaySession));
        Assert.Equal(42, queued.RomId);
    }

    [Fact]
    public void RomMBats_own_exit_does_not_become_a_play_session()
    {
        // RomMBat is launched from the ES menu, so quitting it fires game-end. The menu launch
        // is identifiable in the launch log rather than merely suspected, which is what makes
        // this a rule instead of a heuristic.
        using var fixture = Journal.Create();

        fixture.Rom(42, "snes", "ActRaiser (USA).zip");
        fixture.Launch("2026-08-16T10:00:00Z", "snes", "libretro", "roms/snes/ActRaiser (USA).zip");
        fixture.Start("2026-08-16T10:00:01Z", "roms/snes/ActRaiser (USA).zip");
        fixture.End("2026-08-16T10:30:00Z");

        // RomMBat itself, launched from the menu and quit.
        fixture.Launch("2026-08-16T10:45:00Z", "retrobat", null, "system/es_menu/rommbat.menu");
        fixture.End("2026-08-16T10:50:00Z");

        var outcome = fixture.Correlate();

        Assert.Equal(1, outcome.Sessions);
        Assert.Equal(1, outcome.MenuLaunches);
        Assert.Single(fixture.Store.Outbox.Pending(kind: OutboxKind.PlaySession));
    }

    [Fact]
    public void A_game_still_running_stays_open_rather_than_being_closed_or_discarded()
    {
        using var fixture = Journal.Create();

        fixture.Rom(42, "snes", "ActRaiser (USA).zip");
        fixture.Launch("2026-08-16T10:00:00Z", "snes", "libretro", "roms/snes/ActRaiser (USA).zip");
        fixture.Start("2026-08-16T10:00:01Z", "roms/snes/ActRaiser (USA).zip");

        var outcome = fixture.Correlate();

        Assert.Equal(0, outcome.Sessions);
        Assert.Equal(1, outcome.Unresolved);

        // Still open, which is also what stops eviction taking the game mid-session.
        Assert.Single(fixture.Store.Journal.Open());

        var guard = new SaveGuard(fixture.Store);
        Assert.False(guard.Check(42, RelativePath.Create("roms/snes/ActRaiser (USA).zip")).CanRemove);

        // And the end arriving later closes it into a real session.
        fixture.End("2026-08-16T10:30:00Z");
        Assert.Equal(1, fixture.Correlate().Sessions);
    }

    [Fact]
    public void A_session_whose_clock_went_backwards_is_dropped_rather_than_sent_to_be_refused()
    {
        // end_time must be strictly after start_time, enforced server-side with a 422. A
        // handheld whose RTC jumped mid-session produces exactly this.
        using var fixture = Journal.Create();

        fixture.Rom(42, "snes", "ActRaiser (USA).zip");
        fixture.Launch("2026-08-16T10:00:00Z", "snes", "libretro", "roms/snes/ActRaiser (USA).zip");
        fixture.Start("2026-08-16T10:30:00Z", "roms/snes/ActRaiser (USA).zip");
        fixture.End("2026-08-16T10:00:00Z");

        var outcome = fixture.Correlate();

        Assert.Equal(0, outcome.Sessions);
        Assert.Equal(1, outcome.Orphans);
        Assert.Empty(fixture.Store.Outbox.Pending(kind: OutboxKind.PlaySession));
    }

    [Fact]
    public void Correlating_twice_does_not_queue_the_same_session_twice()
    {
        using var fixture = Journal.Create();

        fixture.Rom(42, "snes", "ActRaiser (USA).zip");
        fixture.Launch("2026-08-16T10:00:00Z", "snes", "libretro", "roms/snes/ActRaiser (USA).zip");
        fixture.Start("2026-08-16T10:00:01Z", "roms/snes/ActRaiser (USA).zip");
        fixture.End("2026-08-16T10:30:00Z");

        Assert.Equal(1, fixture.Correlate().Sessions);
        Assert.True(fixture.Correlate().IsNoOp);
        Assert.Single(fixture.Store.Outbox.Pending(kind: OutboxKind.PlaySession));
    }

    [Fact]
    public void A_relocated_install_with_saves_present_rescans_to_the_same_answer()
    {
        // The portability regression, now with saves. A drive letter change must be a
        // non-event: nothing persisted may carry the old root, so the same tree at a new path
        // has to produce identical rows.
        using var original = TempRetroBatTree.Create();
        var install = original.Install();

        using (var store = LocalStore.Open(install))
        {
            Seed(install, store);
            var first = new SaveScanner(install, store).Scan();
            Assert.Equal(1, first.Found);
            Assert.Equal(1, first.Attributed);

            var states = new StateScanner(install, store, Fixtures.LoadSaveStates()).Scan();
            Assert.Equal(1, states.Found);
            Assert.Equal(1, states.Attributed);
            Assert.Equal(1, states.Screenshots);
        }

        using var moved = original.CopyToNewLocation();
        var movedInstall = moved.Install();

        Assert.NotEqual(install.RootPath, movedInstall.RootPath);

        using var relocated = LocalStore.Open(movedInstall);

        var before = relocated.Saves.List();
        var statesBefore = relocated.States.List();

        var outcome = new SaveScanner(movedInstall, relocated).Scan();
        var stateOutcome = new StateScanner(movedInstall, relocated, Fixtures.LoadSaveStates()).Scan();

        var after = relocated.Saves.List();
        var statesAfter = relocated.States.List();

        Assert.Equal(1, outcome.Found);
        Assert.Equal(0, outcome.Forgotten);
        Assert.Equal(
            before.Select(save => (save.Path.Value, save.Slot, save.ContentHash)),
            after.Select(save => (save.Path.Value, save.Slot, save.ContentHash)));

        // A state carries three paths rather than one, and a directory template that expands
        // through the system and the core, so it has three more chances to have captured a root.
        Assert.Equal(1, stateOutcome.Found);
        Assert.Equal(0, stateOutcome.Forgotten);
        Assert.Equal(
            statesBefore.Select(state => (state.Path.Value, state.Slot, state.ContentHash, state.ScreenshotPath)),
            statesAfter.Select(state => (state.Path.Value, state.Slot, state.ContentHash, state.ScreenshotPath)));

        // And nothing anywhere in the store holds the old root.
        Assert.All(after, save => Assert.DoesNotContain(":", save.Path.Value, StringComparison.Ordinal));

        Assert.All(statesAfter, state =>
        {
            Assert.DoesNotContain(":", state.Path.Value, StringComparison.Ordinal);
            Assert.DoesNotContain(":", state.ScreenshotPath!.Value.Value, StringComparison.Ordinal);
            Assert.DoesNotContain(":", state.RomPath!.Value.Value, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Every_path_this_milestone_constructs_is_relative_and_inside_the_tree()
    {
        // A static check over the paths M6 introduces, in the shape of the rule they exist
        // under: no absolute path is ever persisted, and each one lands where it says.
        RelativePath[] paths =
        [
            SaveScanner.SavesDirectory,
            SaveSync.PartialDirectory,
            SaveSync.AsideDirectory,
            SaveStateSchema.ConfigPath,
            SpoolDrain.Directory,
            TreeLock.Path,
            LaunchLog.LivePath,
            LaunchLog.RotatedPath,
            EsHooks.ScriptsDirectory,
            EsHooks.SourcePath,
            .. EsHooks.Events.Select(EsHooks.PathFor),
        ];

        using var tree = TempRetroBatTree.Create();
        var install = tree.Install();

        foreach (var path in paths)
        {
            Assert.True(path.HasValue);
            Assert.DoesNotContain(@"\", path.Value, StringComparison.Ordinal);
            Assert.DoesNotContain(":", path.Value, StringComparison.Ordinal);
            Assert.False(Path.IsPathRooted(path.Value));

            // And it resolves back inside the tree rather than escaping it.
            Assert.True(install.Contains(install.Resolve(path)));
        }
    }

    private static void Seed(RetroBatInstall install, LocalStore store)
    {
        var romPath = RelativePath.Create("roms/snes/ActRaiser (USA).zip");
        var romAbsolute = install.Resolve(romPath);
        Directory.CreateDirectory(Path.GetDirectoryName(romAbsolute)!);
        File.WriteAllText(romAbsolute, "rom");

        store.Files.Record(new LocalFile
        {
            Path = romPath,
            Folder = "snes",
            RomId = 42,
            Kind = LocalFileKind.Rom,
            FileName = "ActRaiser (USA).zip",
            SizeBytes = 3,
        });

        var savePath = install.Resolve(RelativePath.Create("saves/snes/ActRaiser (USA).srm"));
        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        File.WriteAllText(savePath, "progress");

        // A save state and its screenshot in the core-scoped directory beside the battery save,
        // which is the shape the tree really has: states and battery saves share the parent.
        var statePath = install.Resolve(
            RelativePath.Create("saves/snes/libretro.snes9x/ActRaiser (USA).state1"));
        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
        File.WriteAllText(statePath, "a state");
        File.WriteAllText(statePath + ".png", "a screenshot");
    }

    /// <summary>A store with a journal and a launch log a test can write into.</summary>
    private sealed class Journal : IDisposable
    {
        private readonly TempRetroBatTree _tree;
        private readonly List<string> _log = [];

        private Journal(TempRetroBatTree tree, RetroBatInstall install, LocalStore store)
        {
            _tree = tree;
            Install = install;
            Store = store;
        }

        public RetroBatInstall Install { get; }

        public LocalStore Store { get; }

        public static Journal Create()
        {
            var tree = TempRetroBatTree.Create();
            var install = tree.Install();
            return new Journal(tree, install, LocalStore.Open(install));
        }

        public void Rom(int romId, string folder, string fileName)
        {
            var path = RelativePath.Create($"roms/{folder}/{fileName}");
            var absolute = Install.Resolve(path);
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

        /// <summary>
        /// Appends a launch line in the shape the real launcher writes.
        /// </summary>
        /// <param name="at">
        /// The instant, in the same UTC form the hook timestamps use. Written to the log in
        /// <b>local</b> time, because that is what the launcher writes, so these tests describe
        /// one timeline rather than two and read the same in any zone.
        /// </param>
        public void Launch(string at, string system, string? emulator, string relativeRom)
        {
            var rom = Install.Resolve(RelativePath.Create(relativeRom));
            var emulatorFlag = emulator is null ? string.Empty : $" -emulator {emulator}";

            var timestamp = DateTimeOffset
                .Parse(at, System.Globalization.CultureInfo.InvariantCulture)
                .ToLocalTime()
                .ToString("yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);

            _log.Add(
                $"{timestamp} [INFO]      [Startup] \"{Install.RootPath}\\emulationstation\\emulatorLauncher.exe\""
                    + $" -system {system}{emulatorFlag} -rom \"{rom}\"");

            var path = Install.Resolve(LaunchLog.LivePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllLines(path, _log);
        }

        public void Start(string at, string relativeRom) =>
            Store.Journal.Append(
                JournalEvent.GameStart,
                DateTimeOffset.Parse(at, System.Globalization.CultureInfo.InvariantCulture),
                RelativePath.Create(relativeRom));

        public void End(string at) =>
            Store.Journal.Append(
                JournalEvent.GameEnd,
                DateTimeOffset.Parse(at, System.Globalization.CultureInfo.InvariantCulture));

        public CorrelationOutcome Correlate() => new PlaytimeCorrelator(Install, Store).Correlate();

        public void Dispose()
        {
            Store.Dispose();
            _tree.Dispose();
        }
    }
}
