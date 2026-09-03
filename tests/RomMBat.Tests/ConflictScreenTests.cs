using RomM.Client;
using RomMBat.Core;
using RomMBat.Core.Identity;
using RomMBat.Core.Metadata;
using RomMBat.Core.Paths;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;
using RomMBat.Tests.Support;
using RomMBat.UI.Input;
using RomMBat.UI.Screens;
using RomMBat.UI.Shell;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// Choosing a side from the couch, and the lock that has to be held while it happens.
/// </summary>
/// <remarks>
/// <b>The rule this class is really about is that the UI cannot take the tree lock.</b> Resolving
/// a class C conflict runs the same restore a flush does, and two of those at once leave a shared
/// container half swapped. The console took the lock in <c>saves resolve</c>; the interface may
/// never name <c>TreeLock</c>, which is asserted structurally against the built assembly. So the
/// rule moved into <see cref="ConflictResolutionService"/> and both front ends call it, and these
/// assert that the refusal survived the move.
/// </remarks>
public class ConflictScreenTests : IDisposable
{
    private static readonly Uri Origin = new("https://romm.invalid");

    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();
    private readonly InstallSession _session;

    public ConflictScreenTests()
    {
        _session = InstallSession.Open(_tree.Root).Session!;
        Pair();
    }

    public void Dispose()
    {
        _session.Dispose();
        _tree.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void An_install_with_no_conflicts_says_so_rather_than_showing_an_empty_screen()
    {
        var list = Assert.IsType<ListScreen>(ConflictScreens.List(_session));

        Assert.Empty(list.Rows);
        Assert.NotNull(list.EmptyMessage);

        // What a conflict is, in the words of somebody who has never had one, because this is
        // the state the screen is in almost always.
        Assert.Contains("both sides", list.EmptyMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_open_conflict_is_listed_and_its_detail_names_both_sides()
    {
        Seed();
        Name(7, "Chrono Trigger", "Chrono Trigger (USA).sfc");

        var list = Assert.IsType<ListScreen>(ConflictScreens.List(_session));
        var row = Assert.Single(list.Rows);

        // The game's name, not its id. The first version of this screen drew "Game 7", which a
        // hands-on pass called close to meaningless from the couch.
        Assert.Equal("Chrono Trigger", row.Label);

        // And the file name under it, for the reason browse measured in 7b-2c: a title alone
        // cannot be matched against what is on disk. The slot is there too, because four slots
        // on one game make four otherwise identical rows.
        Assert.Contains("Chrono Trigger (USA).sfc", row.Detail!, StringComparison.Ordinal);
        Assert.Contains("battery", row.Detail!, StringComparison.Ordinal);

        var navigator = new Navigator(list);
        navigator.Handle(NavAction.Accept);

        var detail = Assert.IsType<ListScreen>(navigator.Current);

        // A pane of facts, not a menu of two choices: every row is something to read before
        // deciding, so nothing is selected.
        Assert.True(detail.Reading);
        Assert.Equal(-1, detail.Cursor);

        Assert.Equal("Chrono Trigger", detail.Title);

        var labels = detail.Rows.Select(r => r.Label).ToList();

        Assert.Contains("Game", labels);
        Assert.Contains("Slot", labels);
        Assert.Contains("This device", labels);
        Assert.Contains("The server", labels);

        // Which game, on the screen the decision is made on, without needing a press.
        Assert.Equal(
            "Chrono Trigger (USA).sfc",
            detail.Rows.Single(r => r.Label == "Game").Value);

        // The thing a person most needs to know before choosing, said rather than left to be
        // inferred from the absence of a warning.
        var reassurance = detail.Rows.Single(r => r.Label == "Either way");
        Assert.Contains("nothing is deleted", reassurance.Value!, StringComparison.OrdinalIgnoreCase);

        // Two verbs, and neither of them is Accept: a screen that put a side on the button that
        // also confirms would make the commonest mispress the destructive one.
        var hints = detail.Hints.Select(hint => hint.Action).ToList();

        Assert.Contains(NavAction.Start, hints);
        Assert.Contains(NavAction.Alternate, hints);
        Assert.DoesNotContain(NavAction.Accept, hints);
    }

    [Fact]
    public void A_conflict_whose_game_was_never_recorded_falls_back_to_its_id()
    {
        // A real state rather than a defensive branch: a save outlives its ROM, because removing
        // a game never touches saves, and a device that has never synced that platform has no
        // metadata for it either. Showing the id is honest; showing nothing is not.
        Seed();

        var list = Assert.IsType<ListScreen>(ConflictScreens.List(_session));
        var row = Assert.Single(list.Rows);

        Assert.Equal("Game 7", row.Label);
        Assert.Contains("battery", row.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_resolution_is_refused_while_something_else_holds_the_tree()
    {
        Seed();

        // The lock a flush would hold. The UI cannot name TreeLock, which is the whole reason
        // the refusal lives in Core, so this test takes it on the UI's behalf from outside.
        using var held = TreeLock.TryAcquire(_session.Install);

        Assert.NotNull(held);

        var outcome = await new ConflictResolutionService(_session.Install, _session.Store)
            .ResolveAsync(
                7,
                "libretro:battery",
                ConflictResolution.KeepLocal,
                () => throw new InvalidOperationException(
                    "the lock is taken first, so nothing should have asked for a connection"),
                TestContext.Current.CancellationToken);

        Assert.Equal(ConflictOutcomeState.Busy, outcome.State);
        Assert.False(outcome.Resolved);
        Assert.Contains("Nothing was changed", outcome.Message, StringComparison.Ordinal);

        // Still open, so the list still offers it.
        Assert.Single(_session.Store.SaveConflicts.ListOpen());
    }

    [Fact]
    public async Task With_no_server_the_conflict_is_left_alone_and_said_to_be_left_alone()
    {
        Seed();

        var outcome = await new ConflictResolutionService(_session.Install, _session.Store)
            .ResolveAsync(
                7,
                "libretro:battery",
                ConflictResolution.KeepServer,
                () => null,
                TestContext.Current.CancellationToken);

        Assert.Equal(ConflictOutcomeState.Offline, outcome.State);

        // Offline is a working state, so this says what did not happen and that the conflict is
        // still there, rather than reading as a failure.
        Assert.Contains("still here", outcome.Message, StringComparison.Ordinal);
        Assert.Single(_session.Store.SaveConflicts.ListOpen());
    }

    [Fact]
    public void The_conflict_screens_name_no_face_button()
    {
        Seed();
        Name(7, "Chrono Trigger", "Chrono Trigger (USA).sfc");

        var list = Assert.IsType<ListScreen>(ConflictScreens.List(_session));
        var navigator = new Navigator(list);
        navigator.Handle(NavAction.Accept);

        var detail = Assert.IsType<ListScreen>(navigator.Current);

        foreach (var screen in new[] { list, detail })
        {
            var strings = screen.Rows
                .SelectMany(row => new[] { row.Label, row.Value, row.Detail })
                .Concat(screen.Hints.Select(hint => hint.Label))
                .Append(screen.Title)
                .Append(screen.EmptyMessage)
                .Where(text => text is not null);

            foreach (var text in strings)
            {
                Assert.DoesNotContain("press ", text!, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("button", text!, StringComparison.OrdinalIgnoreCase);
            }

            Assert.All(screen.Hints, hint => Assert.Contains(hint.Action, NavRepeat.Bound));
        }
    }

    /// <summary>One open conflict, written straight into the store.</summary>
    /// <remarks>
    /// Not through a sync. What these tests are about is the screen and the lock, and a
    /// negotiated conflict is <see cref="SaveConflictTests"/>'s subject with its own fixture.
    /// </remarks>
    private void Seed() =>
        _session.Store.SaveConflicts.Record(
            new SaveConflictRecord(
                7,
                "libretro:battery",
                RelativePath.Create("saves/snes/Chrono Trigger.srm"),
                RelativePath.Create("saves/snes/Chrono Trigger.srm.romm-local"),
                // Real md5 lengths: local_hash and server_hash both carry a CHECK on 32.
                new string('a', 32),
                new string('b', 32),
                DateTimeOffset.UtcNow.AddHours(-2),
                42,
                "Both sides changed since the last sync.",
                DateTimeOffset.UtcNow.AddHours(-1),
                DateTimeOffset.UtcNow,
                null,
                null),
            DateTimeOffset.UtcNow);

    /// <summary>What the metadata pass would have recorded for this game.</summary>
    private void Name(int romId, string name, string fsName) =>
        _session.Store.Metadata.Record(new GameMetadata
        {
            RomId = romId,
            Folder = "snes",
            FsName = fsName,
            Name = name,
        });

    private void Pair()
    {
        _session.Store.Device.EnsureIdentity(DeviceIdentity.ReadOrCreate(_session.Install));
        _session.Store.Device.SavePairing(
            new PairingResult(
                Origin,
                "device-1",
                "Handheld",
                new GrantedScopes(RomMScopes.Requested),
                TokenProtector.Protect("rmm_token", null, DateTimeOffset.UtcNow.AddYears(1))),
            DateTimeOffset.UtcNow);
    }
}
