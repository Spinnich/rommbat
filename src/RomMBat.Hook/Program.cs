using RomMBat.Core.Paths;
using RomMBat.Core.Sync;

// EmulationStation gives a hook the game's arguments positionally and never says which event
// it is serving, so the event is the name of the folder the hook was installed into.
//
// Everything this process does is: work out the event, work out the root, write one file,
// rename it, and for two of the four events start a detached agent. It opens no socket
// itself, touches no database and takes no lock, so there is nothing here that a kill can
// leave half done. The three types it uses are compiled from RomMBat.Core rather than
// referenced, because four copies of this file are installed and a reference would bring the
// store and the API client into every one.
//
// CLAUDE.md rule 4 is narrowed by that spawn, not bent. The rule forbids a hook touching the
// network and gives its reason in the next sentence: hooks run inside the game-launch path.
// game-start and game-end do, and they spawn nothing at all. start fires when
// EmulationStation starts and quit when it exits, and neither is in that path. Which events
// those are lives on SpoolRecord, so this file and the agent cannot disagree about it and a
// test can assert the boundary rather than a comment claiming it.
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

    // The record first, always. A spawn that fails must not cost the play session, and the
    // pass being started is the one that reads what was just written.
    Spool.Write(root, new SpoolRecord(hookEvent, DateTimeOffset.UtcNow, Environment.ProcessId, args));

    if (SpoolRecord.SpawnsBackgroundPass(hookEvent))
    {
        StartBackgroundPass(root, hookEvent);
    }

    return 0;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
{
    // A pulled stick or a read-only volume. Failing a launch over a missed play session is the
    // wrong trade, and EmulationStation ignores the exit code anyway.
    return 1;
}

// Starts the agent and does not wait for it. Detached on purpose: the hook's whole contract is
// that it returns in milliseconds, and the pass it starts polls for EmulationStation to exit.
static void StartBackgroundPass(string root, string hookEvent)
{
    var agent = Path.Combine(root, SpoolRecord.AgentRelativePath.Replace('/', Path.DirectorySeparatorChar));

    if (!File.Exists(agent))
    {
        // An install with hooks and no agent, which happens between an uninstall and a
        // reinstall. The spool file is already written and waits for whoever turns up.
        return;
    }

    try
    {
        // CreateNoWindow is load-bearing rather than cosmetic. The agent is a console app and
        // EmulationStation is full screen, so without it a console flashes over the front end
        // at every boot and every quit. UseShellExecute must be false for it to have any
        // effect, and false is also what keeps this from going through the shell at all.
        using var started = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(agent)
        {
            ArgumentList = { "background", hookEvent },
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(agent)!,
        });
    }
    catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
    {
        // Nothing is owed here. The spool file is on disk, and the next start, quit or sync
        // drains it.
    }
}
