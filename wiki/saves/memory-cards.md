# Memory cards and folder saves

Most games keep their save in one file named after the game, which RomMBat syncs as it is. Two
kinds of save need more: a folder of files per game, and a memory card shared by many games.

## Saves kept as a folder

Some emulators, such as PPSSPP for the PSP, keep each game's save as a folder named with the
game's own ID, such as `ULUS10057`, rather than the game's name. RomMBat sends the whole folder to
RomM as one save and brings it back the same way.

To know which game a folder belongs to, RomMBat asks RomM where the game's save lives, and reads
the game's ID from the game file, from RetroBat's record of what you launched, or from a save
state. If two of those disagree, it sends nothing rather than file one game's save under another,
and `rommbat-agent saves` lists both candidates. `rommbat-agent saves bind` tells it which game is
right, or makes it work the answer out again. See [Command line](../reference/cli.md).

A device receives a folder save once the emulator has made the folder it goes in. If it has not,
the flush says so: play any game for that system once on this device, then flush again.

## PlayStation 2 memory cards

A PlayStation 2 memory card holds saves for every game you have played on it, so there is no one
game to sync it as. RomMBat sets PCSX2's memory card to RetroBat's **FOLDER** choice instead, which
keeps each save in a folder of its own named after the game, such as `BASLUS-21050`. Each of those
folders then syncs like any other folder save, and RomM already knows which game each one belongs
to, so a save syncs the first time, before you have played the game on this computer.

RomMBat makes the change when you next quit EmulationStation, because EmulationStation overwrites
RetroBat's settings with its own copy when it closes. Until then the change is listed under
Pending settings on the This device screen, and `rommbat-agent saves` shows it too. It only does this on
a computer with a PlayStation 2 game synced, and it sets FOLDER over the choice for the whole
system. A game you gave its own card with the old `rommbat-agent saves convert` keeps that choice,
because a setting for one game outranks the one for the system. It keeps writing to its own card,
which no longer syncs. To bring it back, remove that game's memory card setting in
EmulationStation's game options.

**Saves on the old card stay on the old card.** A game you played before the change starts from an
empty card, because PCSX2 no longer reads the old one. RomMBat does not move them, and
`rommbat-agent saves` names the old card so you know it is there. The second memory card slot is
not part of the change.

Uninstalling RomMBat leaves the FOLDER choice in place, since your saves are in those folders and
changing it back would hide them from PCSX2.

A game whose RomM entry was never identified, such as a fan translation, has no folder name on
RomM. Its save syncs once you have played it on this computer, when RomMBat can tell from what you
launched which game wrote it.
