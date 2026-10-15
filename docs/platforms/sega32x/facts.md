---
summary: How each emulator RetroBat offers for `sega32x` behaves, measured, with RB- IDs.
read-when: Before certifying a `sega32x` row or changing how RomMBat handles a `sega32x` save, state or firmware.
---

# sega32x: emulator facts

Facts RomMBat relies on, one per heading. [The upstream reference](../../upstream/README.md) says what an entry holds and how IDs are kept.

## RB-413. Each standalone `sega32x` row keeps its battery save and states in its own place

Verified: RetroBat 8.3.0, 2026-10-14. How: booted Knuckles' Chaotix (Japan, USA) (En), an SRAM cartridge, NBA Jam - Tournament Edition (World), a serial EEPROM, and Doom (Japan, USA) (En), with no battery, under each row through `emulatorLauncher` with no 32X BIOS in `bios\`, saved states on `F2`, `F7`, `F2` and on `F5`, `F7`, `F5` for Kega Fusion, read the tree; PicoDrive under libretro ran 60 s on NBA Jam.
Every row boots all three without firmware. ares, core `Mega32X`, keeps `ares/Mega 32X/<rom>.ram` for SRAM and `<rom>.eeprom` for EEPROM, 512 B each, written on `Esc`, and writes its states to the parent's `ares/Mega Drive/`, `.bs1` and `.bs2`, 1,010,843 B for Chaotix, 1,010,865 B for NBA Jam and 1,010,331 B for Doom. It left nothing for Doom. BizHawk's PicoDrive writes `bizhawk/<title>.SaveRAM`, named after its own title, `Knuckles' Chaotix (32X) (JU) [!]`. jgenesis writes `jgenesis/32x/<rom>.sav`, 256 B for NBA Jam, and its states to `emulators/jgenesis/states/32x/`, which `emulatorLauncher` mirrors into `jgenesis/states/` on exit. PicoDrive under libretro keeps NBA Jam's 256 B EEPROM image at the head of an 8,192 B loose `.srm`, the rest zeros, and jgenesis's and Kega Fusion's files hold the same 256 B

## RB-414. BizHawk writes a zeroed `sega32x` battery file at boot for every cartridge, battery or not

Verified: RetroBat 8.3.0, 2026-10-14. How: launched each game under `bizhawk`/`PicoDrive` through `emulatorLauncher`, closed the window, read the file.
Knuckles' Chaotix left a 1,024 B `.SaveRAM`, NBA Jam 8,192 B and Doom, which has no battery, 16,384 B, every byte `0x00`. NBA Jam's game initializes its EEPROM at boot, as the other rows' files show, and BizHawk's file stays zero. `ErasedSave` passes over `0xFF` only, so by the maintainer's decision these upload as saves

## RB-415. ares and BizHawk do not keep a `sega32x` serial EEPROM

Verified: RetroBat 8.3.0, 2026-10-14. How: with the EEPROM file deleted, launched NBA Jam - Tournament Edition (World) under `ares`/`Mega32X` and `bizhawk`/`PicoDrive` through `emulatorLauncher`, ran 60 s at the title, closed each and read the file; then seeded ares with PicoDrive's 256 B and ran it 40 s.
The game formats a blank 24C02 at boot: PicoDrive under libretro, jgenesis and Kega Fusion each wrote the same 256 B within seconds, holding `Snake 1995` at `0x08`. ares wrote 512 B of `0xFF`, which the scanner passes over as erased, and BizHawk 8,192 B of `0x00`. Seeded with PicoDrive's 256 B and run through `emulatorLauncher` for 40 s, ares kept the seed in the first 256 B and wrote the file back on exit as 512 B, the rest `0xFF`, so an Acclaim EEPROM cartridge (NBA Jam TE, NFL Quarterback Club) keeps no save under either row

## RB-416. Kega Fusion writes a `sega32x` game's save loose in `saves/sega32x/` and its states under `saves/sega32x/kega-fusion/sstates/`, where only the `.srm` is read

Verified: RetroBat 8.3.0, 2026-10-14. How: launched Knuckles' Chaotix (Japan, USA) (En), NBA Jam - Tournament Edition (World) and Doom (Japan, USA) (En) under `kega-fusion` through `emulatorLauncher`, `Fusion.exe -32x`, saved states on `F5`, `F7`, `F5`, read `Fusion.ini`, the tree and `saves`.
Each launch rewrites `Fusion.ini` with `SRMFiles=<root>\saves\sega32x` and `StateFiles=<root>\saves\sega32x\kega-fusion\sstates` (emulatorlauncher#1390), where 8.2.1 left the template's `emulators\kega-fusion` and `saves\megadrive\kega-fusion`. NBA Jam's 256 B EEPROM landed as `saves/sega32x/<rom>.srm`, the same bytes jgenesis writes, which the loose `libretro` rule reads as `libretro:battery`, the file PicoDrive's 8,192 B image shares, and the states as `sega32x/kega-fusion/sstates/<rom>.gs0` and `.gs9`, 676,671 B each. `saves` reports the six state files as `no save-state directory is declared here`, since the supplement's `kega-fusion` entry still names the parent's folder (#519). Chaotix and Doom wrote no battery file. Kega booted all three games with no 32X BIOS in `bios\`

## RB-432. ares and BizHawk round-trip a `sega32x` SRAM save another row made

Verified: RetroBat 8.2.1, 2026-10-05. How: with a save made on Knuckles' Chaotix under another row, launched it under `ares`/`Mega32X` and `bizhawk`/`PicoDrive` through `emulatorLauncher`, saved in the game, read the file.
Both rows read the save another row made and wrote the game's change back, so an SRAM cartridge keeps its save under either row where an Acclaim EEPROM one does not (RB-415)
