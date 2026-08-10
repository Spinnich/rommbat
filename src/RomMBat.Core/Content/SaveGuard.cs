using Microsoft.Data.Sqlite;
using RomMBat.Core.Paths;
using RomMBat.Core.Store;

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
/// <b>This is M3 reaching into M6's territory on purpose, and it fails closed.</b> Saves,
/// states and playtime are M6's, and nothing writes one yet, so the honest thing is to build
/// the check against the seams that exist today and to refuse whenever it cannot answer.
/// Deleting a ROM takes its save's only attribution with it, and that is not recoverable.
/// <para>
/// Two seams exist today, and both are already declared in migration 001:
/// </para>
/// <list type="bullet">
/// <item><c>outbox</c>: anything produced offline and not yet sent, keyed by ROM.</item>
/// <item><c>journal</c>: what the ES hooks append, keyed by the ROM's path. An entry that is
/// still <c>open</c> means a game was launched and nothing has yet worked out what it
/// wrote.</item>
/// </list>
/// <para>
/// <b>What M6 has to connect here.</b> When save shapes land, a game's save files become
/// discoverable from its system and ROM name, and this check has to grow a third question:
/// does a save file exist on disk that has never been uploaded. Until then a save produced
/// while nothing was watching is invisible to this guard, which is why the plan records it and
/// why eviction never touches a file RomMBat did not download.
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

    private bool HasOpenJournalEntry(RelativePath path)
    {
        using var command = _store.Connection
            .Command("SELECT 1 FROM journal WHERE rom_relative_path = $path AND state = 'open' LIMIT 1;")
            .With("$path", path.Value);

        return command.ExecuteScalar() is not null;
    }
}
