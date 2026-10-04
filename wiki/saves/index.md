# How saves sync

RomMBat sends your saves, save states and playtime to RomM, so another device paired to the same
account can carry on where you left off. You do not have to do anything for it to happen.

## What goes up

- In-game saves, the ones a game makes itself, such as a battery save on a cartridge.
- Save states, with their screenshot. See [Save states](states.md).
- Saves an emulator keeps as a folder per game, such as a PSP game's save data.
- A PlayStation 2 game's memory card, once you have given that game its own card. See
  [Memory cards](memory-cards.md).
- How long you played. See [Playtime](playtime.md).

## When it happens

RomMBat syncs saves each time you open EmulationStation and each time you quit it, and again at
the start of every sync. It runs in the background, with no window, and never while a game is
starting. Nothing is sent while you play: it goes up the next time you quit EmulationStation.

A save another device made comes down when you open EmulationStation. If you have already
started that game by then, RomMBat does not write under it: the save waits and lands the next
time you quit EmulationStation. A save that reaches RomM while EmulationStation is sitting open
comes down the next time you open it
([#155](https://github.com/Spinnich/rommbat/issues/155)).

## Away from the server

**Playing offline loses nothing.** Saves and playtime wait on this device and go up the next time
it can reach RomM. The main menu's Outbox row counts what is still waiting.

## When both sides changed

If a save changed here and on another device since they last agreed, RomMBat keeps both and asks
you which one to use. Nothing is overwritten meanwhile. See [Conflicts](conflicts.md).

## Changing emulator

Each emulator keeps its saves in its own place and its own format, and RomMBat syncs each one as
it is, without converting it. So when you switch a system to another emulator, your progress
comes with you only if the new emulator reads the same save file as the old one. Otherwise the
game starts from whatever save the new emulator has, and your old save is usually still there
when you switch back. A few emulators use the same file name as another but a different format,
so one refuses the other's save or writes over it. Each system's page under
[Platforms](../platforms/index.md) says which of its emulators share a save, and which clash.

The standalone Mednafen opens a save under the game's plain file name before its own. Where it
cannot read the save another emulator keeps under that name, Mesen's on the Master System and
mGBA's on the Game Boy Advance, RomMBat leaves that save on the server for a game that runs under
Mednafen on this computer. The sync summary counts it as `left on the server, for an emulator
other than the one this device runs`, and it arrives on the next sync after you switch the game to
the emulator that wrote it.

To change a system's emulator, open Game settings in EmulationStation's main menu, then Per
system advanced configuration, and pick the system.

## What does not sync

Some saves cannot be tied to one game, such as a memory card every game shares. RomMBat never
guesses: it leaves them where they are and reports them, with the reason. To see that report,
run `rommbat-agent saves` in a terminal. It lists what is on this device, what has gone up, what
cannot go up and why, and what is waiting on you.

Which emulators have had their saves tested is on [Platforms](../platforms/index.md).

## When something looks stuck

The background sync writes what it did to `emulators\rommbat\logs\background.log`, since it has
no window. `rommbat-agent flush` runs the same pass on the spot and shows what it does.
