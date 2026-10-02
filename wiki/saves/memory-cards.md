# Memory cards and folder saves

Most games keep their save in one file named after the game, which RomMBat syncs as it is. Two
kinds of save need more: a folder of files per game, and a memory card shared by many games.

## Saves kept as a folder

Some emulators, such as PPSSPP for the PSP, keep each game's save as a folder named with the
game's own ID, such as `ULUS10057`, rather than the game's name. RomMBat sends the whole folder to
RomM as one save and brings it back the same way.

To know which game a folder belongs to, RomMBat reads the game's ID from the game file, from
RetroBat's record of what you launched, or from a save state. If two of those disagree, it sends
nothing rather than file one game's save under another, and `rommbat-agent saves` lists both
candidates. `rommbat-agent saves bind` tells it which game is right, or makes it work the answer
out again. See [Command line](../reference/cli.md).

A device that has never held a folder save for a game cannot receive one yet: play the game once
on this device first.

## Giving a PlayStation 2 game its own memory card

A PlayStation 2 memory card holds saves for every game you have played on it, so there is no one
game to sync it as. RomMBat does not sync a shared card. Instead, it can give one game a memory
card of its own, which then syncs like any other save.

RetroBat's FOLDER choice for the PlayStation 2 memory card is still a shared card: every game
writes into the one folder, `Mcdf01.ps2`, so RomMBat does not sync that either.

1. In [Find a game](../using/browse-and-install.md), open the game.
2. Press the top face button for Give it its own memory card.
3. Read what changes, then choose Queue it.
4. Quit EmulationStation. RomMBat makes the change as it closes.

RomMBat waits for EmulationStation to close because it writes the change into RetroBat's own
settings, and EmulationStation overwrites those with its own copy when it closes. Until then,
the change is listed under Queued changes on the main menu, where you can cancel it.

## Before you give a game its own card

**The game starts from an empty card.** Its saves on the shared card stay there, where it no longer
looks. RomMBat does not move them. The one exception is a card another computer already gave this
game: sync pulls it down even before you convert the game, and the game then starts from that card.
Also:

- A game on several discs is refused: each disc would get its own card, and the save would be
  lost when you change disc.
- A game that reads another game's save from the same card, such as a sequel importing a
  prequel's, no longer finds it.
- PlayStation (PS1) games are not offered. DuckStation's usual setting already keeps every disc
  of a game on one card, and changing it is what would break that.

To undo it, run `rommbat-agent saves convert <game number> --revert` with EmulationStation closed.
It puts the setting back exactly as it was.

## A card is here but the game does not use it

If another computer gave a game its own card, sync downloads that card to this one. PCSX2 only reads
it once this computer has done the same for the game, and until then the game reads the shared card
and shows no save. The flush summary counts these as `per-game memory card(s) waiting on a game that
is not converted`, and `rommbat-agent flush` names each game and the `saves convert <game number>
--apply` that fixes it. Nothing is lost: the card is on disk and the conversion is the whole fix.
