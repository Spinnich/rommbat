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
mednafen 1.32.1, core `gg`, writes a loose `<rom>.<md5>.sav`, 32,768 B, on exit, the md5 of the whole `.gg` inside the zip, with states at `mednafen/sstates/<rom>.<md5>.mc<n>`. ares keeps `ares/Game Gear/<rom>.ram`, 32,768 B, beside `.bs1` and `.bs2`, 58,231 B each. BizHawk's SMSHawk starts with no firmware, unlike on `mastersystem` (**322**), and writes `bizhawk/Defenders of Oasis (UE).SaveRAM`, 8,192 B, named after its own title. jgenesis writes `jgenesis/gg/<rom>.sav`, 32,768 B. Genesis Plus GX trims its `.srm` to 285 B and PicoDrive keeps 32,768 B, as on `mastersystem` (**323**). Every file holds the game's `Backup Ver0.84` header at `0x100`
