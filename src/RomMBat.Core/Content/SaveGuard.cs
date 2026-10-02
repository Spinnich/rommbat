using Microsoft.Data.Sqlite;
using RomMBat.Core.Paths;
using RomMBat.Core.Store;
using RomMBat.Core.Sync;

namespace RomMBat.Core.Content;

/// <summary>Whether a ROM may be removed, and why not when it may not.</summary>
/// <param name="CanRemove">False means keep the file, whatever the budget says.</param>
/// <param name="Reason">What to tell the user. Null only when removal is allowed.</param>
public sealed record SaveGuardVerdict(bool CanRemove, string? Reason)
{
    public static SaveGuardVerdict Allowed { get; } = new(true, null);

    public static SaveGuardVerdict Refuse(string reason) => new(false, reason);
}

/// <summary>
/// Refuses to evict a game whose local saves have not reached the server.
/// </summary>
/// <remarks>
/// Deleting a ROM takes its save's only attribution with it, and that is not recoverable, so
/// every branch here fails closed.
/// <para>
/// Four questions:
/// </para>
/// <list type="bullet">
/// <item><c>outbox</c>: anything produced offline and not yet sent, keyed by ROM.</item>
/// <item><c>journal</c>: what the ES hooks append, keyed by the ROM's path. An entry that is
/// still <c>open</c> means a game was launched and nothing has yet worked out what it
/// wrote. A <c>game-start</c> older than the last <c>start</c> or <c>quit</c> is an orphan and
/// does not count, per <see cref="RunningGames"/>.</item>
/// <item><c>local_save</c>: a save file on disk whose <c>uploaded_content_hash</c> is null has
/// never reached the server, and one whose hash no longer matches the file has changed since it
/// did. Either way the bytes on disk are what would be lost.</item>
/// <item><c>local_state</c>: the same question about save states, which are save data by any
/// reading a user would recognise. A state is worthless once its ROM is gone, and a state that
/// has never gone up is not recoverable from anywhere.</item>
/// </list>
/// <para>
/// <b>Eviction also never touches a file RomMBat did not download.</b> That rule is a second
/// line, not the guard: a save produced while nothing was watching is visible here directly.
/// </para>
/// <para>
/// <b>The answer is only as wide as discovery.</b> Class C units are recorded into
/// <c>local_save</c> like any other save, so the <c>local_save</c> question counts them. A unit
/// that could not be attributed has a null <c>rom_id</c> and cannot match any ROM here; that is
/// not a hole this query can close, since the ROM being asked about is exactly what attribution
/// failed to name, and it is the case reported as unsyncable instead.
/// </para>
/// <para>
/// <b>A class D shared container is not guarded.</b> It has no <c>rom_id</c> to belong to by
/// definition, so it is reported rather than guarded.
/// </para>
/// <para>
/// <b>A new question here extends <c>EvictCommand</c>'s scan in the same commit.</b> Rows that
/// land in a table an existing question reads need nothing more, since the scan that refreshes
/// them is the same <c>SaveScanner</c> pass eviction already runs.
/// </para>
/// </remarks>
public sealed class SaveGuard
{
    private readonly LocalStore _store;

    public SaveGuard(LocalStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <summary>
    /// Asks whether a ROM's content can be removed.
    /// </summary>
    /// <param name="romId">The ROM being considered.</param>
    /// <param name="path">Its file, which is how the journal refers to it.</param>
    public SaveGuardVerdict Check(int romId, RelativePath? path = null)
    {
        try
        {
            if (CountUnsentOutboxEntries(romId) is var unsent && unsent > 0)
            {
                return SaveGuardVerdict.Refuse(
                    $"{unsent} save or play record for this game has not reached the server yet.");
            }

            if (path is { } file && HasOpenJournalEntry(file))
            {
                return SaveGuardVerdict.Refuse(
                    "this game was launched and what it wrote has not been worked out yet.");
            }

            if (CountUnsentSaves(romId) is var unsentSaves && unsentSaves > 0)
            {
                return SaveGuardVerdict.Refuse(
                    $"{unsentSaves} save file for this game on disk has not reached the server yet.");
            }

            if (CountUnsentStates(romId) is var unsentStates && unsentStates > 0)
            {
                return SaveGuardVerdict.Refuse(
                    $"{unsentStates} save state for this game on disk has not reached the server yet.");
            }

            return SaveGuardVerdict.Allowed;
        }
        catch (SqliteException ex)
        {
            // Fail closed. An unreadable store is not evidence that nothing is waiting, and the
            // cost of being wrong in the other direction is someone's save.
            return SaveGuardVerdict.Refuse(
                $"the local database could not be read, so it is not safe to say this game has no "
                    + $"unsaved work ({ex.Message}).");
        }
    }

    private int CountUnsentOutboxEntries(int romId)
    {
        using var command = _store.Connection
            .Command("SELECT COUNT(*) FROM outbox WHERE rom_id = $romId AND state <> 'sent';")
            .With("$romId", romId);

        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Save files on disk that have never gone up, or have changed since they did.
    /// </summary>
    /// <remarks>
    /// A save whose content hash could not be taken, because a running emulator held the file,
    /// is stored with a null hash and counts here. That is the fail-closed direction: refusing
    /// to evict a game that is very likely running is the correct outcome anyway.
    /// </remarks>
    private int CountUnsentSaves(int romId)
    {
        using var command = _store.Connection.Command(
            """
            SELECT COUNT(*)
            FROM local_save
            WHERE rom_id = $romId
              AND (uploaded_content_hash IS NULL
                   OR content_hash IS NULL
                   OR uploaded_content_hash <> content_hash);
            """)
            .With("$romId", romId);

        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Save states on disk that have never gone up, or have changed since they did.
    /// </summary>
    /// <remarks>
    /// The same query as <see cref="CountUnsentSaves"/> against the other table, and the same
    /// fail-closed rule: a state with no hash was held open by something, and refusing to evict
    /// a game that is very likely running is the correct answer regardless.
    /// <para>
    /// A state that could not be attributed to a ROM has a null <c>rom_id</c> and cannot match
    /// here. That is not a hole this query can close, since the ROM being asked about is exactly
    /// the thing an unattributed state failed to name; it is the case reported as unsyncable.
    /// </para>
    /// </remarks>
    private int CountUnsentStates(int romId)
    {
        using var command = _store.Connection.Command(
            """
            SELECT COUNT(*)
            FROM local_state
            WHERE rom_id = $romId
              AND (uploaded_content_hash IS NULL
                   OR content_hash IS NULL
                   OR uploaded_content_hash <> content_hash);
            """)
            .With("$romId", romId);

        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private bool HasOpenJournalEntry(RelativePath path)
    {
        // Only a game-start after the last start or quit can still be running. An older one is
        // an orphan from a power loss, which no flush closes, and would refuse this game forever.
        using var command = _store.Connection
            .Command(
                $"""
                SELECT 1
                FROM journal
                WHERE rom_relative_path = $path
                  AND state = 'open'
                  AND (event <> 'game-start' OR {RunningGames.AfterLastFrontEndEvent})
                LIMIT 1;
                """)
            .With("$path", path.Value);

        return command.ExecuteScalar() is not null;
    }
}
