# Your first sync

A sync puts games on this device. You choose which ones by making a sync set: a platform, a
collection, or a search of your RomM library. This page walks through one small set from start
to finish. [Sync sets](../using/sync-sets.md) covers everything else they can do.

## Before you start

Set a disk limit if this drive holds other things. Choose Disk space on the main menu, step
Limit RomMBat to with Left and Right, then choose Save at the bottom. Without a limit, RomMBat fills
the drive until only the amount under Always leave free remains.
[Disk space](../using/disk-budget.md) explains both numbers.

**On a new install, the platform list starts empty.** Nothing on the controller fills it yet
([#325](https://github.com/Spinnich/rommbat/issues/325)). Before you make a platform sync set,
or install a single game from Browse library, run this once from a terminal in your RetroBat folder
while the server is reachable:

```powershell
emulators\rommbat\rommbat-agent.exe platforms list
```

A collection or a filter sync set does not need it, because RomMBat asks RomM for those directly.

## Make a sync set

1. On the main menu, choose Sync sets.
2. Press Start for the menu, and choose New set.
3. On What it holds, press Accept and choose a platform, one of your collections, or a saved
   filter.
4. Choose the platform or collection. A filter asks for a name, and then for what to match.
5. Choose Create set, the last row.

RomMBat names a platform or collection set after what you chose, so you type nothing. It then
asks RomM which games are in the set straight away, which on a large library can take a few
minutes. Press Back and choose Stop to stop early: what it found so far is kept, and the next
check carries on from there. When it finishes, press Accept to reach the set.

## Sync it

On the new set, press the left face button for Sync now, or choose it from the Start menu. The sync screen shows which step it is on, the game it
is downloading with a progress bar, how many are done, how much of your disk limit is used, and
any problems as they happen. In order, a sync:

1. sends any saves and playtime waiting to go up;
2. asks RomM which games are in the set now;
3. fetches the BIOS files RetroBat lists for those systems, when your RomM library has them;
4. downloads the games;
5. fetches their artwork and writes EmulationStation's game lists;
6. tells EmulationStation to reload, so the games appear.

The first sync also adds RomMBat to EmulationStation's menu and sets up the hooks that send your
saves and playtime back.

When it finishes, the title changes to Synced and the footer to Done. A sync that stopped or could not finish says so in its title instead. Press Accept to leave. If
some games could not come down, the screen says why, and syncing again picks up where it left
off.

## Stopping part way

Press Back during a sync, and choose Stop, to stop it. Keep syncing is selected first, so a
stray press changes nothing. The game being downloaded at that moment is removed completely,
and every game that finished before it stays, with its artwork and its place in
EmulationStation's list. Nothing is left half-done. Press Accept to leave the screen.

## Play

Go back to EmulationStation. Your games are in their system's list, with artwork. Saves you make
go back to RomM when you next open or quit EmulationStation, which [How saves
sync](../saves/index.md) explains.
