---
summary: How each emulator RetroBat offers for `gamegear` behaves, measured, with RB- IDs.
read-when: Before certifying a `gamegear` row or changing how RomMBat handles a `gamegear` save, state or firmware.
---

# gamegear: emulator facts

Facts RomMBat relies on, one per heading. [The upstream reference](../../upstream/README.md) says what an entry holds and how IDs are kept.

## RB-408. ares writes a `gamegear` battery file for every cartridge, all `0xFF` when the cartridge has no battery

Verified: RetroBat 8.3.0, 2026-10-14. How: launched Castle of Illusion Starring Mickey Mouse (USA, Europe, Brazil) (En), which has no battery, under `ares`/`GameGear` through `emulatorLauncher`, quit on `Esc`, read the file.
Castle of Illusion Starring Mickey Mouse (USA, Europe, Brazil) (En), which has no battery, left `ares/Game Gear/<rom>.ram`, 32,768 B, every byte `0xFF`. Uploaded, every game played under ares would gain a blank save on the server, so the scanner passes over a battery file that is entirely `0xFF`, under any rule on any system, and a restore treats one as absent and refuses one the server holds

## RB-409. Each standalone `gamegear` row keeps its battery save and states in its own place

Verified: RetroBat 8.3.0, 2026-10-14. How: booted Defenders of Oasis (USA, Europe) under each row through `emulatorLauncher` with no firmware in `bios\`, saved states on `F2`, `F7`, `F2`, read the tree.
mednafen 1.32.1, core `gg`, writes a loose `<rom>.<md5>.sav`, 32,768 B, on exit, the md5 of the whole `.gg` inside the zip, with states at `mednafen/sstates/<rom>.<md5>.mc<n>`. ares keeps `ares/Game Gear/<rom>.ram`, 32,768 B, beside `.bs1` and `.bs2`, 58,231 B each. BizHawk's SMSHawk starts with no firmware, unlike on `mastersystem` (**322**), and writes `bizhawk/Defenders of Oasis (UE).SaveRAM`, 8,192 B, named after its own title. jgenesis writes `jgenesis/gg/<rom>.sav`, 32,768 B. Genesis Plus GX trims its `.srm` to 285 B and PicoDrive keeps 32,768 B, as on `mastersystem` (**323**). Every file holds the game's header from `0x100`, its ASCII `Backup Ver0.84` at `0x10E`

## RB-410. RetroBat 8.3.0 launches jgenesis on `gamegear` as a Game Gear, and the cartridge plays in color

Verified: RetroBat 8.3.0, 2026-10-14. How: launched Defenders of Oasis (USA, Europe) under `jgenesis` through `emulatorLauncher`, read its log and a screenshot, then saved states on `F2`, `F7`, `F2`.
`emulatorLauncher.log` reads `jgenesis-cli.exe -f <rom> --hardware GameGear`, where 8.2.1 passed `MasterSystem` and the screen stayed black (emulatorlauncher#1398, `37e1b4ed`). The intro plays in color, the battery save is `jgenesis/gg/<rom>.sav`, 32,768 B with `Backup Ver0.84` at `0x10E`, and the states `_0.jst` and `_1.jst` land in `emulators/jgenesis/states/gg/` and are mirrored on exit into `saves/gamegear/jgenesis/states/` beside a `.txt` holding the game's name. The row boots; it is not yet certified (#522)

## RB-411. PicoDrive and jgenesis do not play a Master System cartridge in the `gamegear` folder as the other rows do

Verified: RetroBat 8.3.0, 2026-10-14. How: launched Castle of Illusion Starring Mickey Mouse (USA, Europe, Brazil) (En), a zipped `.sms`, under each of the six rows that boot the library through `emulatorLauncher` and read the screen 30 s in.
Some of the library's zips hold a `.sms`: Game Gear cartridges that run in Master System mode. Genesis Plus GX and ares show the story text on black, mednafen shows it at the Master System's full frame, and BizHawk's SMSHawk crops it to a Game Gear screen. PicoDrive showed solid blue with black bars where the others showed text, and jgenesis, which 8.3.0 starts as Game Gear hardware (RB-410), a black screen with blue bars, so neither shows the game's own colors at 30 s. No such cartridge has a battery, so nothing is saved either way

## RB-412. `libretro`/`fbneo` does not boot a `gamegear` library named by No-Intro, as on `mastersystem` (325)

Verified: RetroBat 8.3.0, 2026-10-14. How: launched Defenders of Oasis (USA, Europe) under `libretro`/`fbneo` through `emulatorLauncher`.
FBNeo shows "Romset is unknown" and never starts the game, since it takes a console game's set from the file name. It wrote nothing under `saves/`
