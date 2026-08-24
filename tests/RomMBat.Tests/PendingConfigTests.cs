using Microsoft.Data.Sqlite;
using RomMBat.Core.Store;
using RomMBat.Tests.Support;
using Xunit;

namespace RomMBat.Tests;

/// <summary>
/// The queue of configuration changes waiting for EmulationStation to be gone.
/// </summary>
/// <remarks>
/// It exists because the UI is launched from the ES menu and therefore always runs under a
/// live ES, which is the one condition under which <c>es_settings.cfg</c> cannot be written.
/// The thing these tests protect is the half nobody watches: the change is applied by
/// <c>background quit</c> when no interface is running, so the record of what happened is the
/// only account of it a person will ever see.
/// </remarks>
public sealed class PendingConfigTests : IDisposable
{
    private readonly TempRetroBatTree _tree = TempRetroBatTree.Create();
    private readonly LocalStore _store;

    public PendingConfigTests() => _store = LocalStore.Open(_tree.Install());

    public void Dispose()
    {
        _store.Dispose();
        _tree.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void A_queued_change_round_trips_with_everything_needed_to_apply_it_later()
    {
        var at = new DateTimeOffset(2026, 8, 24, 21, 4, 0, TimeSpan.Zero);

        var id = _store.PendingConfig.Queue(Request(reason: "a per-game memory card for Armored Core 3", at: at));

        var outstanding = Assert.Single(_store.PendingConfig.ListOutstanding());

        Assert.Equal(id, outstanding.Id);
        Assert.Equal(4242, outstanding.RomId);
        Assert.Equal("ps2", outstanding.System);
        Assert.Equal("Armored Core 3 (USA).iso", outstanding.FsName);
        Assert.Equal("pcsx2_slot1_memory", outstanding.SettingKey);
        Assert.Equal(DesiredSettingState.Set, outstanding.DesiredState);
        Assert.Equal("game", outstanding.DesiredValue);
        Assert.Equal("a per-game memory card for Armored Core 3", outstanding.Reason);
        Assert.Equal(at, outstanding.QueuedAtUtc);
        Assert.True(outstanding.IsOutstanding);
        Assert.Null(outstanding.Result);
    }

    [Fact]
    public void Applying_leaves_a_result_something_else_can_read_afterwards()
    {
        // The point of the whole table. Nothing is running when background quit drains this:
        // the UI exited before the quit hook fired. If the row vanished on success, the next
        // session could not tell an applied change from a cancelled one or from one that was
        // never queued at all.
        var id = _store.PendingConfig.Queue(Request());

        _store.PendingConfig.RecordResult(
            id,
            PendingConfigResult.Applied,
            "set ps2[\"Armored Core 3 (USA).iso\"].pcsx2_slot1_memory = game",
            new DateTimeOffset(2026, 8, 24, 22, 0, 0, TimeSpan.Zero));

        Assert.Empty(_store.PendingConfig.ListOutstanding());

        var finished = Assert.Single(_store.PendingConfig.ListFinished());
        Assert.Equal(PendingConfigResult.Applied, finished.Result);
        Assert.Contains("pcsx2_slot1_memory = game", finished.Detail, StringComparison.Ordinal);
        Assert.False(finished.IsOutstanding);
    }

    [Fact]
    public void A_refusal_is_kept_as_carefully_as_a_success()
    {
        // A refused change is the case a user most needs told about, because from the ES menu
        // it looks exactly like nothing happening.
        var id = _store.PendingConfig.Queue(Request());

        _store.PendingConfig.RecordResult(
            id,
            PendingConfigResult.Refused,
            "the key is already set to 'folder' and RomMBat did not write it.",
            DateTimeOffset.UtcNow);

        var finished = Assert.Single(_store.PendingConfig.ListFinished());
        Assert.Equal(PendingConfigResult.Refused, finished.Result);
        Assert.Contains("RomMBat did not write it", finished.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Cancelling_an_unapplied_change_takes_it_out_and_leaves_no_trace()
    {
        // The one case that deletes rather than recording an outcome: nothing was written, so
        // there is nothing a later reader could want.
        _store.PendingConfig.Queue(Request());

        Assert.True(_store.PendingConfig.Cancel("ps2", "Armored Core 3 (USA).iso", "pcsx2_slot1_memory"));

        Assert.Empty(_store.PendingConfig.ListOutstanding());
        Assert.Empty(_store.PendingConfig.ListFinished());
    }

    [Fact]
    public void Cancelling_never_reaches_a_change_that_has_already_been_applied()
    {
        // Once it is on disk, cancelling it is not a thing the queue can do. Reverting is, and
        // that is a new queued change rather than the removal of an old one.
        var id = _store.PendingConfig.Queue(Request());
        _store.PendingConfig.RecordResult(id, PendingConfigResult.Applied, "set", DateTimeOffset.UtcNow);

        Assert.False(_store.PendingConfig.Cancel("ps2", "Armored Core 3 (USA).iso", "pcsx2_slot1_memory"));
        Assert.Single(_store.PendingConfig.ListFinished());
    }

    [Fact]
    public void Queueing_the_same_target_twice_replaces_rather_than_stacks()
    {
        // A user changing their mind. Two contradictory pending rows on one key would apply in
        // an order nothing defines.
        _store.PendingConfig.Queue(Request());

        _store.PendingConfig.Queue(Request() with
        {
            DesiredState = DesiredSettingState.Remove,
            DesiredValue = null,
            Reason = "back to the shared card after all",
        });

        var outstanding = Assert.Single(_store.PendingConfig.ListOutstanding());
        Assert.Equal(DesiredSettingState.Remove, outstanding.DesiredState);
        Assert.Null(outstanding.DesiredValue);
    }

    [Fact]
    public void A_finished_change_does_not_block_a_new_one_for_the_same_game()
    {
        // Convert, apply, then revert: the history row and the new pending row live on the same
        // triple at the same time, which is the case a state column on save_conversion could
        // not have carried.
        var first = _store.PendingConfig.Queue(Request());
        _store.PendingConfig.RecordResult(first, PendingConfigResult.Applied, "set", DateTimeOffset.UtcNow);

        _store.PendingConfig.Queue(Request() with
        {
            DesiredState = DesiredSettingState.Remove,
            DesiredValue = null,
            Reason = "put it back",
        });

        Assert.Single(_store.PendingConfig.ListOutstanding());
        Assert.Single(_store.PendingConfig.ListFinished());
    }

    [Fact]
    public void Removing_a_key_and_writing_an_empty_one_are_not_the_same_request()
    {
        // Finding 170: "the key was absent" and "the key held the stock value" are different
        // files to restore, so a null value is refused rather than read as a removal.
        Assert.Throws<ArgumentException>(() => _store.PendingConfig.Queue(Request() with
        {
            DesiredState = DesiredSettingState.Set,
            DesiredValue = null,
        }));

        Assert.Throws<ArgumentException>(() => _store.PendingConfig.Queue(Request() with
        {
            DesiredState = DesiredSettingState.Remove,
            DesiredValue = "game",
        }));
    }

    [Fact]
    public void A_row_cannot_be_half_finished()
    {
        // A result with no timestamp, or a timestamp with no result, is a row no reader can
        // classify as outstanding or done. Enforced by the schema rather than by the store, so
        // a second writer cannot get it wrong.
        Assert.Throws<SqliteException>(() => Insert("applied_at_utc", "'2026-01-01T00:00:00Z'"));
        Assert.Throws<SqliteException>(() => Insert("result", "'applied'"));
    }

    [Fact]
    public void A_setting_key_that_is_really_a_scoped_key_is_refused()
    {
        // The column holds the bare option. The full ps2["Game.iso"].pcsx2_slot1_memory form is
        // derivable from the other three columns, and storing it too would let them disagree.
        using var command = _store.Connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO pending_config (rom_id, system, fs_name, setting_key, desired_state,
                                        desired_value, reason, queued_at_utc)
            VALUES (1, 'ps2', 'Game.iso', $key, 'set', 'game', 'x', '2026-01-01T00:00:00Z');
            """;
        command.Parameters.AddWithValue("$key", "ps2[\"Game.iso\"].pcsx2_slot1_memory");

        Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
    }

    [Fact]
    public void A_rom_filename_with_no_extension_is_refused()
    {
        // A per-game key built from a stem is ignored silently by emulatorlauncher, and the
        // emulator then writes to the shared container with nothing to show anything went
        // wrong. That is the bug that costs a save, so it is refused at write time.
        Assert.Throws<SqliteException>(() => _store.PendingConfig.Queue(Request() with
        {
            FsName = "Armored Core 3 (USA)",
        }));
    }

    private void Insert(string column, string value)
    {
        using var command = _store.Connection.CreateCommand();

        // The column name comes from a constant in this file, never from input.
        command.CommandText =
            $"""
            INSERT INTO pending_config (rom_id, system, fs_name, setting_key, desired_state,
                                        desired_value, reason, queued_at_utc, {column})
            VALUES (1, 'ps2', 'Game.iso', 'pcsx2_slot1_memory', 'set', 'game', 'x',
                    '2026-01-01T00:00:00Z', {value});
            """;

        command.ExecuteNonQuery();
    }

    private static PendingConfigRequest Request(
        string reason = "a per-game memory card",
        DateTimeOffset? at = null) => new()
        {
            RomId = 4242,
            System = "ps2",
            FsName = "Armored Core 3 (USA).iso",
            SettingKey = "pcsx2_slot1_memory",
            DesiredState = DesiredSettingState.Set,
            DesiredValue = "game",
            Reason = reason,
            QueuedAtUtc = at ?? new DateTimeOffset(2026, 8, 24, 21, 0, 0, TimeSpan.Zero),
        };
}
