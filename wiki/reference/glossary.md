# Glossary

The words this guide uses in a particular sense.

**BIOS, or firmware.** A file copied from an original console that some emulators need before a
game will start. See [BIOS and firmware](../using/bios.md).

**Conflict.** A save that changed on this device and on another one since they last agreed.
RomMBat keeps both until you choose. See [Conflicts](../saves/conflicts.md).

**Disk limit.** How much of the drive RomMBat's downloads may take up, across all your sync sets.
See [Disk space](../using/disk-budget.md).

**EmulationStation.** The menu RetroBat opens into, where you pick a system and a game.

**Emulator and core.** The program that runs a game. Some emulators, such as RetroArch
(`libretro`), run several engines called cores, and RetroBat lets you choose one per system.

**Folder save.** A save an emulator keeps as a folder of files per game, such as a PSP game's
save data. See [Memory cards and folder saves](../saves/memory-cards.md#saves-kept-as-a-folder).

**Game list.** The `gamelist.xml` file in each system folder, which holds the names, details and
artwork paths EmulationStation shows. See [Artwork and game lists](../using/media-and-gamelists.md).

**Hooks.** Small programs RetroBat runs when EmulationStation opens or closes and when a game
starts or ends. RomMBat's hooks are how saves and playtime go up without you doing anything.

**Pairing.** Connecting this device to your RomM server by approving a code in RomM. See
[Pairing](../getting-started/pairing.md).

**Permission.** What you allow this device to do in RomM when you approve it, such as reading
games or sending saves. RomM calls these scopes.

**Platform.** A console as RomM names it, such as Super Nintendo. Each platform's games go into
one system folder.

**Query.** Asking RomM which games a sync set holds, without downloading any.

**Row.** One emulator on one system, and one core where the emulator has several, such as `snes`
under `libretro`/`snes9x`. RomMBat is tested one row at a time. See
[Platforms](../platforms/index.md).

**Save state.** A snapshot of the whole game at one moment, which the emulator makes when you ask
it to. See [Save states](../saves/states.md).

**Save, or in-game save.** What a game writes itself when you save, such as a cartridge's battery
save or a memory card.

**Sync.** Bringing this device in step with your sync sets: sending saves and playtime, then
downloading games, artwork and BIOS.

**Sync set.** A saved choice of games to keep on this device: a platform, a collection, a search,
or games you picked. See [Sync sets](../using/sync-sets.md).

**System folder.** A folder under RetroBat's `roms` folder, such as `roms\snes`, where one
system's games live.
