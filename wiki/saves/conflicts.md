# Conflicts

A conflict is a save that changed on this device and on another one since the two last agreed.
Say you played offline on a handheld, then played the same game on your desktop before the
handheld reconnected. Neither save is more right than the other, so RomMBat does not choose.

Nothing is overwritten while a conflict is open. Both saves are kept, and that save stops syncing
until you decide. The main menu's Conflicts row counts how many are waiting.

## Choose a side

1. Choose Conflicts on the main menu, and open the save.
2. The screen shows when the conflict was found, the save on this device, and the save on the
   server with when it last changed.
3. Press Start to keep this device's save, or the left face button to keep the server's.
4. Confirm.

Keeping this device's save sends it to RomM, and every other device takes it from there. RomM
keeps the save it had as an earlier version, so that side is not lost.

**Keeping the server's save replaces this device's.** RomMBat downloads the server's save, checks
it and puts it in place, and then deletes the copy of this device's save it set aside when it
found the conflict. That save never reached RomM, so after this choice it is gone. The conflict
screen currently says that copy is kept, which is wrong
([#326](https://github.com/Spinnich/rommbat/issues/326)).

Choosing needs the server. Keeping the server's save is refused while that game is open, so quit
the game first; the conflict waits.

## From a terminal

`rommbat-agent saves` lists open conflicts, and `rommbat-agent saves resolve` settles one with
`--keep-local` or `--keep-server`. See [Command line](../reference/cli.md).
