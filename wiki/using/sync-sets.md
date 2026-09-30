# Sync sets

## On the gamepad

From the status screen, **Start** opens the sets list, and the screen's secondary action opens
the disk budget. On the list, Start makes a new set and Accept opens the one under the cursor.
On a set, Accept changes its folder if it has one, **Start syncs it**, the third face button
resolves it without downloading anything, and the secondary action deletes it. On the list,
the secondary action syncs every set and the third resolves every set.

Caps and ordering step on Left and Right rather than being typed. Only a set's name and a
filter's search term open the on-screen keyboard, so nobody enters "8 GB" on a grid of letters.

Everything except resolving works with no server at all. Resolve on an unpaired install says
so immediately rather than waiting on a timeout.

The platform picker offers the platforms this install has heard of, which a `sync`, a
`platforms list` or a resolve fills in. On a fresh tree the picker says it is empty.

A resolve against a real library takes minutes. The screen shows a count that moves, and backing
out records where it stopped, so the next resolve continues from that offset. To test the
resolve screen repeatedly, use a small scope or a filter with a search term.

## The sync screen

Start on a set syncs it, or the secondary action on the list syncs every set. The screen shows
the pass it is in, the game it is on with a bar for that game's transfer, a running count, the
disk budget as it is spent, and problems as they arrive.

**Back stops and stays; a second Back leaves.** The stop removes the game it was in, and a
screen that closed on the press could never say what went. The resolve screen answers Back the
same way.

To exercise a stop, use a set with large files: 76 Atari 5200 games took 17 seconds end to end
against a live instance, faster than anyone presses a button. After a stop, nothing is
half-finished:

```powershell
dir D:\retrobat-test\emulators\rommbat\partial      # empty
dotnet run --project src/RomMBat.Agent -- status --root D:\retrobat-test
```

The game that was in progress is wholly gone, ROM and rows together, and every game that
finished before it is still there with its artwork and a gamelist entry.

## From the console

```powershell
dotnet run --project src/RomMBat.Agent -- sets add snes --scope platform --value snes --max-games 5 --root D:\retrobat-test
dotnet run --project src/RomMBat.Agent -- budget --max 2GB --root D:\retrobat-test
dotnet run --project src/RomMBat.Agent -- sync --dry-run --root D:\retrobat-test
dotnet run --project src/RomMBat.Agent -- sync --root D:\retrobat-test
```

Start with a small `--max-games` and a `--max-bytes` against a real library, because the
default is the whole platform. `sync --dry-run` prints the plan and writes nothing, and it
works offline, so it is the cheap way to see what a set would cost before it costs it.
