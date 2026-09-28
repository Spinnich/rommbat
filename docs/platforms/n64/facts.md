---
summary: How each emulator RetroBat offers for `n64` behaves, measured, with RB- IDs.
read-when: Before certifying a `n64` row or changing how RomMBat handles a `n64` save, state or firmware.
---

# n64: emulator facts

Facts RomMBat relies on, one per heading. [The upstream reference](../../upstream/README.md) says what an entry holds and how IDs are kept.

## RB-336. One file for both, named with mupen64plus's title and an md5 prefix

Question: Where RMG and simple64 keep an `n64` battery save, and what names it

Measured: **One file for both, named with mupen64plus's title and an md5 prefix.** Both run with RetroBat's `SaveSRAMPath` `saves/n64/sram` and `SaveFilenameFormat 1`, and both wrote `sram/Legend of Zelda, The - Ocarina o-5BD1FE10.sra`, 32,768 B, word-swapped SRAM. The stem is the first 32 characters of the GoodName in mupen64plus's database, not the ROM file: Mario Kart 64 (USA) wrote `Mario Kart 64 (U) [!]-3A67D998.eep` and, with a memory pak, `.mpk`, 131,072 B, all four paks. The 8 hex digits are the start of the `.z64`'s md5. The file is joined to its ROM by the launch that wrote it, under one `mupen64` rule naming simple64 as a second writer. RMG keeps its states in `emulators/mupen64/Save/State/<title>.st<n>`, which the launcher mirrors to the declared `saves/n64/mupen64/<rom>.st<n>` and copies back on launch; simple64 writes `saves/n64/state/<title>.st<n>`, no mirror

## RB-337. A directory per game, named with the ROM header and an md5

Question: Where Project64 keeps an `n64` battery save and its states

Measured: **A directory per game, named with the ROM header and an md5.** `saves/n64/project64/THE LEGEND OF ZELDA-AA3911F5D5598E19E0183E15B6719C36/THE LEGEND OF ZELDA.sra`, the hex being the md5 of the ROM in 32-bit little-endian word order, upper-cased; Mario Kart 64 wrote `MARIOKART64.eep` and `MARIOKART64_Cont_1.mpk`, player 1's pak. The `.sra` is written only as far as the game has written, 16 B at boot. States go to `project64/sstates/<the same directory>/<its database's name>.pj.zip`, with no mirror and no screenshot. RetroBat's `Project64.sc3` binds F2 to save and F4 to load and no slot key, and ES's `-state_slot` is not passed on, so only slot 0 is reached; `Project64.exe` carries `.pj%d` for the others

## RB-338. One `.SaveRAM` named after the ROM file, in two formats neither reads from the other

Question: What BizHawk's two `n64` cores write, and whether they read each other's

Measured: **One `.SaveRAM` named after the ROM file, in two formats neither reads from the other.** `Ares64` writes raw big-endian SRAM, 32,768 B, byte-identical to ares's `.ram`, and appends the Controller Pak once one is set: 65,536 B for Ocarina of Time, 33,280 B for Mario Kart 64's EEPROM and pak. `Mupen64Plus` writes a 296,960 B image like the `libretro` cores' but with FlashRAM before the SRAM, which sits at `0x40800` rather than `0x20800`. On its first Mario Kart 64 launch `Mupen64Plus` replaced `Ares64`'s 33,280 B file with its own, so a device that changes core loses the other core's save locally, and the server's older row is what remains of it. The slot stays one, `bizhawk:battery`, as on `psx`

## RB-339. Which `n64` rows have a Controller Pak at RetroBat's default, and where it lives

Question: Which `n64` rows have a Controller Pak at RetroBat's default, and where it lives

Measured: **Four have none.** `libretro`/`parallel_n64` (`parallel-n64-pak1 = "none"`), RMG, and both BizHawk cores offer no pak until `parallel_pak1`, `mupen64_pak1` or `bizhawk_n64_pak1` is set, so a game saving only to the pak keeps nothing there. `mupen64plus_next`, simple64, Project64 and gopher64 have one; ares always does and offers no option. The pak lives with the battery save: inside the `.srm` for both `libretro` cores, which carried Mario Kart 64's ghost from one to the other; in the shared `.mpk` for RMG and simple64, likewise; in Project64's `_Cont_1.mpk`, ares's `<rom>.pak` and gopher64's `.mpk` alone; and inside BizHawk's `.SaveRAM`

## RB-340. What joins simple64's and Project64's states to a ROM

Question: What joins simple64's and Project64's states to a ROM

Measured: **The battery save's binding.** Neither stem is the ROM's: simple64 uses the `sram/` title, and Project64 a directory named as its battery directory and a file named from its own database. `es_savestates.supplement.xml` declares both with `titled_by`, naming the battery rule whose learned title stands in for `{{romfilename}}`, and Project64's with `per_game_directory`, where the title is the directory and the file keeps the name it was sent under. A state written before its game's first battery save is attributed on the pass after that save

## RB-341. Only in its own portable folder

Question: Where gopher64 keeps an `n64` battery save

Measured: **Only in its own portable folder.** `emulators/gopher64/portable_data/data/saves/<header>-<sha256 of the .z64>.sra`, 32,768 B big-endian, and for Mario Kart 64 `.eep`, 2,048 B, and `.mpk`, 131,072 B, the pak on at its default. RetroBat neither points it into `saves/n64/` nor mirrors it, while it does mirror `portable_data/data/states/` to the declared `saves/n64/gopher64/states/<rom>.state0` and copies a restored one back. RomMBat's `gopher64` rule reads the folder from the RetroBat root, the one directory outside `saves/` that `local_save`'s CHECK admits (#239), until RetroBat moves it, which emulatorlauncher#1389 asks; one state slot, `-state_slot` ignored
