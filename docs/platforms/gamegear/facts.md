---
summary: How each emulator RetroBat offers for `gamegear` behaves, measured, with RB- IDs.
read-when: Before certifying a `gamegear` row or changing how RomMBat handles a `gamegear` save, state or firmware.
---

# gamegear: emulator facts

Facts RomMBat relies on, one per heading. [The upstream reference](../../upstream/README.md) says what an entry holds and how IDs are kept.

## RB-408. ares writes a `gamegear` battery file for every cartridge, all `0xFF` when the cartridge has no battery

Verified: RetroBat 8.2.1, 2026-10-04. How: launched each game under `ares`/`GameGear` through `emulatorLauncher`, quit on `Esc`, read the file.
Sonic Chaos (USA, Europe, Brazil) (En) and Castle of Illusion Starring Mickey Mouse (USA, Europe, Brazil) (En), neither of which has a battery, each left `ares/Game Gear/<rom>.ram`, 32,768 B, every byte `0xFF`. No other row wrote anything for either. Uploaded, every game played under ares would gain a blank save on the server, so the scanner passes over a battery file that is entirely `0xFF`, under any rule on any system, and a restore treats one as absent and refuses one the server holds

## RB-409. Each standalone `gamegear` row keeps its battery save and states in its own place

Verified: RetroBat 8.2.1, 2026-10-04. How: booted Defenders of Oasis (USA, Europe) under each row through `emulatorLauncher` with no firmware in `bios\`, saved states on `F2`, `F7`, `F2`, read the tree.
mednafen 1.32.1, core `gg`, writes a loose `<rom>.<md5>.sav`, 32,768 B, on exit, the md5 of the whole `.gg` inside the zip, with states at `mednafen/sstates/<rom>.<md5>.mc<n>`. ares keeps `ares/Game Gear/<rom>.ram`, 32,768 B, beside `.bs1` and `.bs2`, 58,231 B each. BizHawk's SMSHawk starts with no firmware, unlike on `mastersystem` (**322**), and writes `bizhawk/Defenders of Oasis (UE).SaveRAM`, 8,192 B, named after its own title. jgenesis writes `jgenesis/gg/<rom>.sav`, 32,768 B. Genesis Plus GX trims its `.srm` to 285 B and PicoDrive keeps 32,768 B, as on `mastersystem` (**323**). Every file holds the game's header from `0x100`, its ASCII `Backup Ver0.84` at `0x10E`

## RB-410. RetroBat launches jgenesis on `gamegear` as a Master System, so a Game Gear cartridge plays to a black screen

Verified: RetroBat 8.2.1, 2026-10-05. How: launched Defenders of Oasis (USA, Europe) under `jgenesis` from ES and through `emulatorLauncher`, then ran `jgenesis-cli` by hand with each `--hardware`.
`emulatorLauncher` starts `jgenesis-cli.exe -f <rom> --hardware MasterSystem` for every `gamegear` game, and the window title reads `sms - <rom>`. The game runs, autosaving its `Backup Ver0.84` header and later progress, but the screen stays black, since a Game Gear cartridge sets its palette through Game Gear registers. Run by hand with `--hardware GameGear`, the title reads `gg - <rom>` and the intro plays in colour. Its help says it otherwise picks the hardware from the file extension, which was not driven on a zip. No ES feature sets the flag: `es_features.cfg` gives jgenesis shaders, vsync, filtering, prescaling and scanlines only. So the row is driven and not certifiable at this floor; reported as [emulatorlauncher#1398](../../upstream/issues.md#tracked). Run by hand it reads another emulator's save, writes `jgenesis/gg/<rom>.sav` and keeps its states in `emulators/jgenesis/states/gg/`, which only `emulatorLauncher` mirrors into `saves/`

## RB-411. PicoDrive runs a Master System cartridge in the `gamegear` folder as a Game Gear game, in the wrong colours

Verified: RetroBat 8.2.1, 2026-10-04. How: launched Castle of Illusion Starring Mickey Mouse (USA, Europe, Brazil) (En), a zipped `.sms`, under each row through `emulatorLauncher` and read the screen.
Nineteen of the library's 546 zips hold a `.sms`: Game Gear cartridges that run in Master System mode. Genesis Plus GX, mednafen, ares and jgenesis show the intro at the Master System's full frame and colours, and BizHawk's SMSHawk shows it cropped to a Game Gear screen in the right colours. PicoDrive crops it to a Game Gear screen and draws it in blue and purple where the others show green, though the game runs and takes input. No such cartridge has a battery, so nothing is saved either way

## RB-412. `libretro`/`fbneo` does not boot a `gamegear` library named by No-Intro, as on `mastersystem` (325)

Verified: RetroBat 8.2.1, 2026-10-05. How: launched Defenders of Oasis (USA, Europe) under `libretro`/`fbneo` from ES and through `emulatorLauncher`.
FBNeo shows "Romset is unknown" and never starts the game, since it takes a console game's set from the file name. It wrote nothing under `saves/`
