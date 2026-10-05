---
summary: How each emulator RetroBat offers for `sega32x` behaves, measured, with RB- IDs.
read-when: Before certifying a `sega32x` row or changing how RomMBat handles a `sega32x` save, state or firmware.
---

# sega32x: emulator facts

Facts RomMBat relies on, one per heading. [The upstream reference](../../upstream/README.md) says what an entry holds and how IDs are kept.

## RB-413. Each standalone `sega32x` row keeps its battery save and states in its own place

Verified: RetroBat 8.2.1, 2026-10-05. How: booted Knuckles' Chaotix (Japan, USA) (En), an SRAM cartridge, NBA Jam - Tournament Edition (World), a serial EEPROM, and Doom (Japan, USA) (En), with no battery, under each row through `emulatorLauncher` with no 32X BIOS in `bios\`, saved states on `F2`, `F7`, `F2`, read the tree.
Every row boots all three without firmware. ares, core `Mega32X`, keeps `ares/Mega 32X/<rom>.ram` for SRAM and `<rom>.eeprom` for EEPROM, 512 B each, written on `Esc`, and writes its states to the parent's `ares/Mega Drive/`, `.bs1` and `.bs2` of 1,010,843 B each. It left nothing for Doom. BizHawk's PicoDrive writes `bizhawk/<title>.SaveRAM`, named after its own title, `Knuckles' Chaotix (32X) (JU) [!]`. jgenesis writes `jgenesis/32x/<rom>.sav`, 256 B for NBA Jam, and its states to `emulators/jgenesis/states/32x/`, which `emulatorLauncher` mirrors into `jgenesis/states/` on exit. PicoDrive under libretro keeps NBA Jam's 256 B EEPROM image at the head of an 8,192 B loose `.srm`, the rest zeros, and jgenesis's and Kega Fusion's files hold the same 256 B

## RB-414. BizHawk writes a zeroed `sega32x` battery file at boot for every cartridge, battery or not

Verified: RetroBat 8.2.1, 2026-10-05. How: launched each game under `bizhawk`/`PicoDrive` through `emulatorLauncher`, closed the window, read the file.
Knuckles' Chaotix left a 1,024 B `.SaveRAM`, NBA Jam 8,192 B and Doom, which has no battery, 16,384 B, every byte `0x00`. NBA Jam's game initialises its EEPROM at boot, as the other rows' files show, and BizHawk's file stays zero. `ErasedSave` passes over `0xFF` only, so by the maintainer's decision these upload as saves

## RB-415. ares and BizHawk do not keep a `sega32x` serial EEPROM, while their SRAM saves round-trip

Verified: RetroBat 8.2.1, 2026-10-05. How: with the EEPROM file deleted, launched NBA Jam - Tournament Edition (World) under `ares`/`Mega32X` and `bizhawk`/`PicoDrive` through `emulatorLauncher`, ran 60 s at the title, quit on `Esc` or by closing the window, read the file.
The game formats a blank 24C02 at boot: PicoDrive under libretro, jgenesis and Kega Fusion each wrote the same 256 B within seconds, holding `Snake 1995` at `0x08`. ares wrote 512 B of `0xFF`, which the scanner passes over as erased, and BizHawk 8,192 B of `0x00`. Seeded with PicoDrive's 256 B and run from ES, ares rewrote the file on exit byte-identical to the seed. On Knuckles' Chaotix, an SRAM cartridge, both rows read the save another row made and wrote the game's change back, so an Acclaim EEPROM cartridge (NBA Jam TE, NFL Quarterback Club) keeps no save under either row, and an SRAM one does

## RB-416. Kega Fusion splits a `sega32x` game across two folders, neither under `saves/sega32x/`

Verified: RetroBat 8.2.1, 2026-10-05. How: launched Knuckles' Chaotix (Japan, USA) (En) and NBA Jam - Tournament Edition (World) under `kega-fusion` through `emulatorLauncher`, `Fusion.exe -32x`, saved states on `F5`, `F7`, `F5`, read the tree and `saves`.
RetroBat's `Fusion.ini` sends every system's battery save to `SxMFiles=.\..\..\emulators\kega-fusion` and the 32X's states to `StateFiles=.\..\..\saves\megadrive\kega-fusion`, the parent's folder, and `emulatorLauncher` rewrote neither. NBA Jam's 256 B EEPROM landed as `emulators/kega-fusion/<rom>.srm`, which no rule reads (RB-283). The states landed as `megadrive/kega-fusion/<rom>.gs0` and `.gs9`, 676,671 B each, where the `megadrive` state entry finds them and `saves` reports them as `no rom`, since no `megadrive` game has the name. Kega booted all three games with the three `32X_*_BIOS` paths in `Fusion.ini` pointing at files not present
