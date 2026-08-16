using RomMBat.Core.Paths;
using RomMBat.Core.Sync;

// EmulationStation gives a hook the game's arguments positionally and never says which event
// it is serving, so the event is the name of the folder the hook was installed into.
//
// Everything this process does is: work out the event, work out the root, write one file,
// rename it, exit. It opens no socket (CLAUDE.md rule 4), touches no database and takes no
// lock, so there is nothing here that a kill can leave half done. The three types it uses are
// compiled from RomMBat.Core rather than referenced, because four copies of this file are
// installed and a reference would bring the store and the API client into every one.
try
{
    var directory = AppContext.BaseDirectory;

    if (SpoolRecord.EventFromDirectory(directory) is not { } hookEvent)
    {
        // Installed somewhere that names no event. A record with no event would become a
        // journal entry nothing can interpret, so nothing is written.
        return 2;
    }

    if (RootMarkers.WalkUp(directory) is not { } root)
    {
        // Nowhere legitimate to write. The spool lives inside the tree like everything else,
        // and guessing a location outside it would break the portable-install rule.
        return 3;
    }

    Spool.Write(root, new SpoolRecord(hookEvent, DateTimeOffset.UtcNow, Environment.ProcessId, args));
    return 0;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
{
    // A pulled stick or a read-only volume. Failing a launch over a missed play session is the
    // wrong trade, and EmulationStation ignores the exit code anyway.
    return 1;
}
