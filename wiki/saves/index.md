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

When EmulationStation opens, RomMBat sends pending saves and syncs the sets enabled on this
device. It adds missing supported games, including multi-file and multi-disc games, along with
your selected artwork and available firmware. It verifies every part of a game before adding
the group to the menu. Fresh loose discs appear together in one folder. If a visible
loose-disc game is missing more than one disc, review the startup report and run Sync
with the emulator closed. Your existing ROMs, BIOS and artwork stay in place. If a ROM
or BIOS needs replacing, check the startup report in `emulators\rommbat\logs\background.log`
and review the file before running Sync with the emulator closed.

Closing a game triggers a send. Quitting EmulationStation gives pending work another chance
to go up. A save pass delayed by another pass or a running game retries the pending
game-end work when it can. If the server is unavailable, RomMBat retries while work is waiting.

On the verified Windows EmulationStation build shipped with RetroBat 8.3.1.1, choosing a game
managed by RomMBat checks that game's remote saves before the emulator starts. Automatic
restore at launch covers plain NES Libretro battery saves (`.srm`). Other managed games use
their regular save sync. If the check fails or times out, the game opens with the local save.
Installations without verified launch support keep the 30-second receive check for plain NES
Libretro saves while EmulationStation is open.

An open conflict or unverified local history keeps the local save in place. If only the
file's modification time changed and its bytes still match the last verified shared save,
the newer remote version can wait for the next launch. To bring back a previously removed
save, start with `rommbat-agent saves restore` to preview the restore.

## Away from the server

Saves and playtime stay on this device while RomM is unreachable. If short retries
run out, RomMBat checks for reconnection while pending work remains and sends it when
it can reach RomM again. Open Waiting to upload to see pending saves by game and slot, alongside
outbox entries. Saves found on disk are read-only in this list. Under This device, Status shows
the pending count and the most recent in-session upload and download.

## When a save is deleted in RomM

Deleting a save in RomM takes it out of the emulator's folder on the next sync, and the sync summary
says it was removed. A copy is kept in `emulators/rommbat/replaced/`, so nothing is lost for good.
A save you changed here since it went up is sent again instead of removed.

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
cannot read the save another emulator keeps under that name, Mesen's on the Master System and the
PC Engine, mGBA's on the Game Boy Advance and mGBA's on the Game Boy for a game with the smallest
save chip, such as Final Fantasy Adventure, RomMBat leaves that save on the server for a game that runs under
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
