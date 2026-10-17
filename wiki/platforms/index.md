# Platforms

<!-- Generated from data/certification.json by tests/RomMBat.Tests/PlatformSupportPageTests.cs.
     Edit the data and regenerate; an edit here fails the test. -->

RomMBat is tested one row at a time. A row is one emulator on one system, and one core where
the emulator has several, because two emulators for the same console keep their saves in
different places. RetroBat runs the default row unless you pick another emulator or core for
the system in its settings.

A certified row passed every check against a real RetroBat install: the game lands where the
emulator reads it, it launches with its artwork, and its saves, save states and playtime reach
RomM and come back. BIOS files come from your RomM library, so a game that needs one your
library lacks may not start, even on a certified row. A system that is not listed has not
been tested yet.

| System                                          | Certified rows | Default row                            |
| ----------------------------------------------- | -------------- | -------------------------------------- |
| [Nintendo Entertainment System - Famicom](#nes) | 9 of 9         | libretro / fceumm, certified           |
| [Megadrive - Genesis](#megadrive)               | 7 of 12        | libretro / genesis_plus_gx, certified  |
| [Game Boy Advance](#gba)                        | 9 of 10        | libretro / mgba, certified             |
| [Game Boy](#gb)                                 | 14 of 14       | libretro / gambatte, certified         |
| [Game Boy Color](#gbc)                          | 12 of 12       | libretro / gambatte, certified         |
| [Super Nintendo Entertainment System](#snes)    | 15 of 15       | libretro / snes9x, certified           |
| [Master System - Mark III](#mastersystem)       | 9 of 12        | libretro / genesis_plus_gx, certified  |
| [PlayStation](#psx)                             | 7 of 7         | libretro / mednafen_psx_hw, certified  |
| [Nintendo 64](#n64)                             | 9 of 9         | libretro / mupen64plus_next, certified |
| [Game Gear](#gamegear)                          | 5 of 9         | libretro / genesis_plus_gx, certified  |
| [32X](#sega32x)                                 | 4 of 6         | libretro / picodrive, certified        |

## Nintendo Entertainment System - Famicom {#nes}

RetroBat's `nes` system. Tested on RomM 5.4.0 and RetroBat 8.3.1.1. The [certification record](https://github.com/Spinnich/rommbat/tree/main/docs/platforms/nes/) has the detail. [Its page](nes.md) says which emulator to pick, what BIOS to supply and what will not work.

| Emulator | Core       | Status              |
| -------- | ---------- | ------------------- |
| libretro | fceumm     | Certified (default) |
| libretro | nestopia   | Certified           |
| libretro | mesen      | Certified           |
| mednafen | nes        | Certified           |
| mesen    |            | Certified           |
| jgenesis |            | Certified           |
| ares     | Famicom    | Certified           |
| bizhawk  | NesHawk    | Certified           |
| bizhawk  | quickerNES | Certified           |

## Megadrive - Genesis {#megadrive}

RetroBat's `megadrive` system. The [certification record](https://github.com/Spinnich/rommbat/tree/main/docs/platforms/megadrive/) has the detail. [Its page](megadrive.md) says which emulator to pick, what BIOS to supply and what will not work.

| Emulator    | Core                 | Status              | Tested on                    | Note                                                                                                                                                                                           |
| ----------- | -------------------- | ------------------- | ---------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| libretro    | genesis_plus_gx      | Certified (default) | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                                                                                                                                |
| libretro    | genesis_plus_gx_wide | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                                                                                                                                |
| libretro    | picodrive            | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                                                                                                                                |
| libretro    | fbneo                | Not certified       | RomM 5.4.0, RetroBat 8.3.0   | Boots no game named the No-Intro way, because FBNeo picks its driver from the file name                                                                                                        |
| jgenesis    |                      | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                                                                                                                                |
| blastem     |                      | Not tested yet      |                              |                                                                                                                                                                                                |
| mednafen    | megadrive            | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                                                                                                                                |
| ares        | MegaDrive            | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                                                                                                                                |
| kega-fusion | auto                 | Not certified       | RomM 5.4.0, RetroBat 8.2.1   | Wrote its battery saves outside saves/ on RetroBat 8.2.1; 8.3.0 writes the .srm loose in saves/megadrive/ and its states under kega-fusion/sstates/, which the supplement does not name (#519) |
| kega-fusion | megadrive            | Not certified       | RomM 5.4.0, RetroBat 8.2.1   | Wrote its battery saves outside saves/ on RetroBat 8.2.1; 8.3.0 writes the .srm loose in saves/megadrive/ and its states under kega-fusion/sstates/, which the supplement does not name (#519) |
| kega-fusion | genesis              | Not certified       | RomM 5.4.0, RetroBat 8.2.1   | Wrote its battery saves outside saves/ on RetroBat 8.2.1; 8.3.0 writes the .srm loose in saves/megadrive/ and its states under kega-fusion/sstates/, which the supplement does not name (#519) |
| bizhawk     | Genplus-gx           | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                                                                                                                                |

## Game Boy Advance {#gba}

RetroBat's `gba` system. The [certification record](https://github.com/Spinnich/rommbat/tree/main/docs/platforms/gba/) has the detail. [Its page](gba.md) says which emulator to pick, what BIOS to supply and what will not work.

| Emulator | Core           | Status              | Tested on                    | Note                                                                                                                        |
| -------- | -------------- | ------------------- | ---------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| libretro | mgba           | Certified (default) | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                                                             |
| libretro | mednafen_gba   | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                                                             |
| libretro | gpsp           | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                                                             |
| mgba     | mgba           | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                                                             |
| jgenesis |                | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                                                             |
| mesen    |                | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                                                             |
| nosgba   |                | Not certified       | RomM 5.4.0, RetroBat 8.2.1   | Could not load a zipped game, and kept its saves outside saves/, on RetroBat 8.2.1; 8.3.0 fixes both, not yet checked there |
| mednafen | gba            | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                                                             |
| ares     | GameBoyAdvance | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                                                             |
| bizhawk  | mGBA           | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                                                             |

## Game Boy {#gb}

RetroBat's `gb` system. Tested on RomM 5.4.0 and RetroBat 8.3.1.1. The [certification record](https://github.com/Spinnich/rommbat/tree/main/docs/platforms/gb/) has the detail. [Its page](gb.md) says which emulator to pick, what BIOS to supply and what will not work.

| Emulator | Core           | Status              |
| -------- | -------------- | ------------------- |
| libretro | gambatte       | Certified (default) |
| libretro | mesen-s        | Certified           |
| libretro | bsnes          | Certified           |
| libretro | tgbdual        | Certified           |
| libretro | sameboy        | Certified           |
| libretro | DoubleCherryGB | Certified           |
| mesen    |                | Certified           |
| jgenesis |                | Certified           |
| mgba     | mgba           | Certified           |
| mednafen | gb             | Certified           |
| ares     | GameBoy        | Certified           |
| bizhawk  | Gambatte       | Certified           |
| bizhawk  | GBHawk         | Certified           |
| bizhawk  | SameBoy        | Certified           |

## Game Boy Color {#gbc}

RetroBat's `gbc` system. Tested on RomM 5.4.0 and RetroBat 8.3.1.1. The [certification record](https://github.com/Spinnich/rommbat/tree/main/docs/platforms/gbc/) has the detail. [Its page](gbc.md) says which emulator to pick, what BIOS to supply and what will not work.

| Emulator | Core           | Status              |
| -------- | -------------- | ------------------- |
| libretro | gambatte       | Certified (default) |
| libretro | tgbdual        | Certified           |
| libretro | sameboy        | Certified           |
| libretro | DoubleCherryGB | Certified           |
| mesen    |                | Certified           |
| jgenesis |                | Certified           |
| mgba     | mgba           | Certified           |
| mednafen | gbc            | Certified           |
| ares     | GameBoyColor   | Certified           |
| bizhawk  | Gambatte       | Certified           |
| bizhawk  | GBHawk         | Certified           |
| bizhawk  | SameBoy        | Certified           |

## Super Nintendo Entertainment System {#snes}

RetroBat's `snes` system. Tested on RomM 5.4.0 and RetroBat 8.3.1.1. The [certification record](https://github.com/Spinnich/rommbat/tree/main/docs/platforms/snes/) has the detail. [Its page](snes.md) says which emulator to pick, what BIOS to supply and what will not work.

| Emulator | Core          | Status              |
| -------- | ------------- | ------------------- |
| libretro | snes9x        | Certified (default) |
| libretro | bsnes-jg      | Certified           |
| libretro | bsnes         | Certified           |
| libretro | bsnes_hd_beta | Certified           |
| libretro | mednafen_snes | Certified           |
| libretro | mesen-s       | Certified           |
| libretro | snes9x2005    | Certified           |
| mednafen | snes          | Certified           |
| mesen    |               | Certified           |
| snes9x   |               | Certified           |
| jgenesis |               | Certified           |
| ares     | SuperFamicom  | Certified           |
| bizhawk  | BSNES         | Certified           |
| bizhawk  | Faust         | Certified           |
| bizhawk  | Snes9x        | Certified           |

## Master System - Mark III {#mastersystem}

RetroBat's `mastersystem` system. The [certification record](https://github.com/Spinnich/rommbat/tree/main/docs/platforms/mastersystem/) has the detail. [Its page](mastersystem.md) says which emulator to pick, what BIOS to supply and what will not work.

| Emulator    | Core            | Status              | Tested on                    | Note                                                                                    |
| ----------- | --------------- | ------------------- | ---------------------------- | --------------------------------------------------------------------------------------- |
| libretro    | genesis_plus_gx | Certified (default) | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                         |
| libretro    | picodrive       | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                         |
| libretro    | gearsystem      | Not tested yet      |                              |                                                                                         |
| libretro    | fbneo           | Not certified       | RomM 5.4.0, RetroBat 8.3.0   | Boots no game named the No-Intro way, because FBNeo picks its driver from the file name |
| jgenesis    |                 | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                         |
| blastem     |                 | Not tested yet      |                              |                                                                                         |
| mednafen    | mastersystem    | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                         |
| mesen       |                 | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                         |
| ares        | MasterSystem    | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                         |
| kega-fusion | auto            | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                         |
| kega-fusion | mastersystem    | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                         |
| bizhawk     | SMSHawk         | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                         |

## PlayStation {#psx}

RetroBat's `psx` system. The [certification record](https://github.com/Spinnich/rommbat/tree/main/docs/platforms/psx/) has the detail. [Its page](psx.md) says which emulator to pick, what BIOS to supply and what will not work.

| Emulator    | Core            | Status              | Tested on                    |
| ----------- | --------------- | ------------------- | ---------------------------- |
| libretro    | mednafen_psx_hw | Certified (default) | RomM 5.4.0, RetroBat 8.3.1.1 |
| libretro    | pcsx_rearmed    | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |
| libretro    | swanstation     | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |
| duckstation |                 | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |
| mednafen    | psx             | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |
| bizhawk     | Nymashock       | Certified           | RomM 5.4.0, RetroBat 8.2.1   |
| bizhawk     | Octoshock       | Certified           | RomM 5.4.0, RetroBat 8.2.1   |

## Nintendo 64 {#n64}

RetroBat's `n64` system. The [certification record](https://github.com/Spinnich/rommbat/tree/main/docs/platforms/n64/) has the detail. [Its page](n64.md) says which emulator to pick, what BIOS to supply and what will not work.

| Emulator  | Core             | Status              | Tested on                    |
| --------- | ---------------- | ------------------- | ---------------------------- |
| libretro  | mupen64plus_next | Certified (default) | RomM 5.4.0, RetroBat 8.3.1.1 |
| libretro  | parallel_n64     | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |
| mupen64   |                  | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |
| gopher64  |                  | Certified           | RomM 5.4.0, RetroBat 8.2.1   |
| simple64  |                  | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |
| ares      | Nintendo64       | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |
| bizhawk   | Ares64           | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |
| bizhawk   | Mupen64Plus      | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |
| project64 |                  | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |

## Game Gear {#gamegear}

RetroBat's `gamegear` system. The [certification record](https://github.com/Spinnich/rommbat/tree/main/docs/platforms/gamegear/) has the detail. [Its page](gamegear.md) says which emulator to pick, what BIOS to supply and what will not work.

| Emulator | Core            | Status              | Tested on                    | Note                                                                                                                                                                                     |
| -------- | --------------- | ------------------- | ---------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| libretro | genesis_plus_gx | Certified (default) | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                                                                                                                          |
| libretro | picodrive       | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                                                                                                                          |
| libretro | gearsystem      | Not tested yet      |                              |                                                                                                                                                                                          |
| libretro | fbneo           | Not certified       | RomM 5.4.0, RetroBat 8.3.0   | Boots no game named the No-Intro way, because FBNeo picks its driver from the file name                                                                                                  |
| jgenesis |                 | Not certified       | RomM 5.4.0, RetroBat 8.2.1   | RetroBat 8.2.1 launched it as a Master System, so a Game Gear game played to a black screen; 8.3.0 launches it as a Game Gear and the intro plays in color; certification is owed (#522) |
| blastem  |                 | Not tested yet      |                              |                                                                                                                                                                                          |
| mednafen | gg              | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                                                                                                                          |
| ares     | GameGear        | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                                                                                                                          |
| bizhawk  | SMSHawk         | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                                                                                                                          |

## 32X {#sega32x}

RetroBat's `sega32x` system. The [certification record](https://github.com/Spinnich/rommbat/tree/main/docs/platforms/sega32x/) has the detail. [Its page](sega32x.md) says which emulator to pick, what BIOS to supply and what will not work.

| Emulator    | Core      | Status              | Tested on                    | Note                                                                                                                                                                                                       |
| ----------- | --------- | ------------------- | ---------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| libretro    | picodrive | Certified (default) | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                                                                                                                                            |
| jgenesis    |           | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                                                                                                                                            |
| blastem     |           | Not tested yet      |                              |                                                                                                                                                                                                            |
| ares        | Mega32X   | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                                                                                                                                            |
| kega-fusion | sega32x   | Not certified       | RomM 5.4.0, RetroBat 8.2.1   | Wrote its battery saves outside saves/, and its states into the Mega Drive's folder, on RetroBat 8.2.1; 8.3.0 writes both under saves/sega32x/, and the supplement does not name the states' folder (#519) |
| bizhawk     | PicoDrive | Certified           | RomM 5.4.0, RetroBat 8.3.1.1 |                                                                                                                                                                                                            |
