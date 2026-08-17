using RomMBat.Core.Paths;
using RomMBat.Core.Store;

namespace RomMBat.Core.Sync;

/// <summary>What a drain pass did.</summary>
/// <param name="Ingested">Records turned into journal rows.</param>
/// <param name="Malformed">Files that were not a record in this format at all. Deleted.</param>
/// <param name="Unreadable">
/// Files in this format at a revision this build cannot read, written by a newer hook.
/// <b>Left on disk</b>, so updating the agent recovers them rather than finding them gone.
/// </param>
/// <param name="Abandoned">Half-written <c>.tmp</c> files cleaned up.</param>
public sealed record SpoolDrainOutcome(int Ingested, int Malformed, int Unreadable, int Abandoned)
{
    public static SpoolDrainOutcome Empty { get; } = new(0, 0, 0, 0);

    public bool IsNoOp => Ingested == 0 && Malformed == 0 && Unreadable == 0 && Abandoned == 0;
}

/// <summary>
/// Turns what the hooks spooled into journal rows.
/// </summary>
/// <remarks>
/// <b>This is the only place a hook's absolute rom path is relativised</b>, which is the
/// boundary CLAUDE.md rule 1 names as mandatory work. The hook writes its arguments verbatim,
/// because it does not know the ROM index and has no business guessing; the drain has the root
/// and can do it properly.
/// <para>
/// <b>Draining is idempotent under replay.</b> A record becomes a journal row and its file is
/// deleted in that order, so a crash between the two replays one record. That is why the
/// journal is keyed on a fresh sequence rather than on the file name: a duplicated row is
/// reconciled to the same play session and deduped server-side on its truncated timestamps,
/// while a lost row is a lost session.
/// </para>
/// </remarks>
public sealed class SpoolDrain
{
    /// <summary>The spool directory, kept in step with the hook's own copy of the constant.</summary>
    public static RelativePath Directory { get; } = RelativePath.Create(Spool.RelativeDirectory);

    /// <summary>The extension a committed record carries.</summary>
    public const string Extension = Spool.Extension;

    /// <summary>
    /// How long a <c>.tmp</c> is left alone before it is treated as abandoned.
    /// </summary>
    /// <remarks>
    /// Generous on purpose. A hook that is mid-write when the drain runs is the ordinary
    /// concurrent case, and deleting its file would lose the record it is about to commit.
    /// </remarks>
    public static TimeSpan AbandonedAfter { get; } = TimeSpan.FromMinutes(10);

    private readonly RetroBatInstall _install;
    private readonly LocalStore _store;
    private readonly TimeProvider _time;

    public SpoolDrain(RetroBatInstall install, LocalStore store, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(store);

        _install = install;
        _store = store;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Reads every committed record into the journal and removes it.</summary>
    public SpoolDrainOutcome Drain()
    {
        var directory = _install.Resolve(Directory);
        if (!System.IO.Directory.Exists(directory))
        {
            return SpoolDrainOutcome.Empty;
        }

        var ingested = 0;
        var malformed = 0;
        var unreadable = 0;
        var abandoned = 0;
        var now = _time.GetUtcNow();

        // Name order is time order: the hook stems its file with a sortable UTC timestamp.
        foreach (var file in System.IO.Directory.GetFiles(directory, "*" + Extension).Order(StringComparer.Ordinal))
        {
            string text;

            try
            {
                text = File.ReadAllText(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Still being written, or the stick went away. Left for the next pass.
                continue;
            }

            var record = SpoolRecord.Parse(text);

            if (record is null)
            {
                // A record whose first line names this format at a revision this build cannot
                // read is left where it is, not deleted. An agent older than the installed hook
                // would otherwise discard every event it found, permanently, and report only a
                // count of them. Leaving it costs one file until the agent catches up; deleting
                // it costs the play sessions it described. Issue #31.
                if (SpoolRecord.IsFromNewerBuild(text))
                {
                    unreadable++;
                    continue;
                }

                malformed++;
                Remove(file);
                continue;
            }

            Ingest(record);
            ingested++;
            Remove(file);
        }

        foreach (var file in System.IO.Directory.GetFiles(directory, "*.tmp"))
        {
            try
            {
                if (now - File.GetLastWriteTimeUtc(file) > AbandonedAfter)
                {
                    Remove(file);
                    abandoned++;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Next pass.
            }
        }

        return new SpoolDrainOutcome(ingested, malformed, unreadable, abandoned);
    }

    private void Ingest(SpoolRecord record)
    {
        var journalEvent = record.Event switch
        {
            "start" => JournalEvent.Start,
            "game-start" => JournalEvent.GameStart,
            "game-end" => JournalEvent.GameEnd,
            "quit" => JournalEvent.Quit,
            _ => (JournalEvent?)null,
        };

        if (journalEvent is not { } parsed)
        {
            return;
        }

        // $1 absolute rom path, $2 basename, $3 gamelist display name. Batocera documents $3
        // as the system; in RetroBat it is the display name and the system is not passed at
        // all, which is why the launch log exists in this design. $4 and $5 arrive empty.
        var romArgument = record.Arguments.Count > 0 ? record.Arguments[0] : null;
        var basename = record.Arguments.Count > 1 ? record.Arguments[1] : null;
        var displayName = record.Arguments.Count > 2 ? record.Arguments[2] : null;

        RelativePath? romPath = null;

        if (!string.IsNullOrWhiteSpace(romArgument) && _install.Contains(romArgument))
        {
            romPath = _install.Relativize(romArgument);
        }

        _store.Journal.Append(
            parsed,
            record.At,
            romPath,
            basename,
            displayName,
            record.ProcessId == 0 ? null : record.ProcessId);
    }

    private static void Remove(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A record already ingested whose file survives is replayed next pass, which the
            // journal tolerates by design.
        }
    }
}
