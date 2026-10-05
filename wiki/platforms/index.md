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
| [Megadrive - Genesis](#megadrive)               | 7 of 11        | libretro / genesis_plus_gx, certified  |
| [Game Boy Advance](#gba)                        | 9 of 10        | libretro / mgba, certified             |
| [Game Boy](#gb)                                 | 14 of 14       | libretro / gambatte, certified         |
| [Game Boy Color](#gbc)                          | 12 of 12       | libretro / gambatte, certified         |
| [Super Nintendo Entertainment System](#snes)    | 15 of 15       | libretro / snes9x, certified           |
| [Master System - Mark III](#mastersystem)       | 9 of 10        | libretro / genesis_plus_gx, certified  |
| [PlayStation](#psx)                             | 7 of 7         | libretro / mednafen_psx_hw, certified  |
| [Nintendo 64](#n64)                             | 9 of 9         | libretro / mupen64plus_next, certified |
| [Game Gear](#gamegear)                          | 5 of 7         | libretro / genesis_plus_gx, certified  |
| [32X](#sega32x)                                 | 4 of 5         | libretro / picodrive, certified        |

## Nintendo Entertainment System - Famicom {#nes}

RetroBat's `nes` system. Tested on RomM 5.3.1 and RetroBat 8.2.1. The [certification record](https://github.com/Spinnich/rommbat/tree/main/docs/platforms/nes/) has the detail. [Its page](nes.md) says which emulator to pick, what BIOS to supply and what will not work.

| Emulator | Core       | Status              |
| -------- | ---------- | ------------------- |
| libretro | fceumm     | Certified (default) |
| libretro | nestopia   | Certified           |
| libretro | mesen      | Certified           |
| mednafen | nes        | Certified           |
| mesen    |            | Certified           |
| ares     | Famicom    | Certified           |
| bizhawk  | NesHawk    | Certified           |
| bizhawk  | quickerNES | Certified           |
| jgenesis |            | Certified           |

## Megadrive - Genesis {#megadrive}

RetroBat's `megadrive` system. Tested on RomM 5.3.1 and RetroBat 8.2.1. The [certification record](https://github.com/Spinnich/rommbat/tree/main/docs/platforms/megadrive/) has the detail. [Its page](megadrive.md) says which emulator to pick, what BIOS to supply and what will not work.

| Emulator    | Core                 | Status              | Note                                                                                    |
| ----------- | -------------------- | ------------------- | --------------------------------------------------------------------------------------- |
| libretro    | genesis_plus_gx      | Certified (default) |                                                                                         |
| libretro    | genesis_plus_gx_wide | Certified           |                                                                                         |
| libretro    | picodrive            | Certified           |                                                                                         |
| libretro    | fbneo                | Not certified       | Boots no game named the No-Intro way, because FBNeo picks its driver from the file name |
| mednafen    | megadrive            | Certified           |                                                                                         |
| ares        | MegaDrive            | Certified           |                                                                                         |
| kega-fusion | auto                 | Not certified       | Writes its battery saves outside saves/, where RomMBat does not look                    |
| kega-fusion | megadrive            | Not certified       | Writes its battery saves outside saves/, where RomMBat does not look                    |
| kega-fusion | genesis              | Not certified       | Writes its battery saves outside saves/, where RomMBat does not look                    |
| bizhawk     | Genplus-gx           | Certified           |                                                                                         |
| jgenesis    |                      | Certified           |                                                                                         |

## Game Boy Advance {#gba}

RetroBat's `gba` system. Tested on RomM 5.3.1 and RetroBat 8.2.1. The [certification record](https://github.com/Spinnich/rommbat/tree/main/docs/platforms/gba/) has the detail. [Its page](gba.md) says which emulator to pick, what BIOS to supply and what will not work.

| Emulator | Core           | Status              | Note                                                                                                |
| -------- | -------------- | ------------------- | --------------------------------------------------------------------------------------------------- |
| libretro | mgba           | Certified (default) |                                                                                                     |
| libretro | mednafen_gba   | Certified           |                                                                                                     |
| libretro | gpsp           | Certified           |                                                                                                     |
| mgba     | mgba           | Certified           |                                                                                                     |
| nosgba   |                | Not certified       | Loads a zipped game only through a loose copy it deletes itself, and keeps its saves outside saves/ |
| mednafen | gba            | Certified           |                                                                                                     |
| ares     | GameBoyAdvance | Certified           |                                                                                                     |
| bizhawk  | mGBA           | Certified           |                                                                                                     |
| jgenesis |                | Certified           |                                                                                                     |
| mesen    |                | Certified           |                                                                                                     |

## Game Boy {#gb}

RetroBat's `gb` system. Tested on RomM 5.3.1 and RetroBat 8.2.1. The [certification record](https://github.com/Spinnich/rommbat/tree/main/docs/platforms/gb/) has the detail. [Its page](gb.md) says which emulator to pick, what BIOS to supply and what will not work.

| Emulator | Core           | Status              |
| -------- | -------------- | ------------------- |
| libretro | gambatte       | Certified (default) |
| libretro | mesen-s        | Certified           |
| libretro | bsnes          | Certified           |
| libretro | tgbdual        | Certified           |
| libretro | sameboy        | Certified           |
| libretro | DoubleCherryGB | Certified           |
| mesen    |                | Certified           |
| mgba     | mgba           | Certified           |
| mednafen | gb             | Certified           |
| ares     | GameBoy        | Certified           |
| bizhawk  | Gambatte       | Certified           |
| bizhawk  | GBHawk         | Certified           |
| bizhawk  | SameBoy        | Certified           |
| jgenesis |                | Certified           |

## Game Boy Color {#gbc}

RetroBat's `gbc` system. Tested on RomM 5.3.1 and RetroBat 8.2.1. The [certification record](https://github.com/Spinnich/rommbat/tree/main/docs/platforms/gbc/) has the detail. [Its page](gbc.md) says which emulator to pick, what BIOS to supply and what will not work.

| Emulator | Core           | Status              |
| -------- | -------------- | ------------------- |
| libretro | gambatte       | Certified (default) |
| libretro | tgbdual        | Certified           |
| libretro | sameboy        | Certified           |
| libretro | DoubleCherryGB | Certified           |
| mesen    |                | Certified           |
| mgba     | mgba           | Certified           |
| mednafen | gbc            | Certified           |
| ares     | GameBoyColor   | Certified           |
| bizhawk  | Gambatte       | Certified           |
| bizhawk  | GBHawk         | Certified           |
| bizhawk  | SameBoy        | Certified           |
| jgenesis |                | Certified           |

## Super Nintendo Entertainment System {#snes}

RetroBat's `snes` system. Tested on RomM 5.3.1 and RetroBat 8.2.1. The [certification record](https://github.com/Spinnich/rommbat/tree/main/docs/platforms/snes/) has the detail. [Its page](snes.md) says which emulator to pick, what BIOS to supply and what will not work.

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
| ares     | SuperFamicom  | Certified           |
| bizhawk  | BSNES         | Certified           |
| bizhawk  | Faust         | Certified           |
| bizhawk  | Snes9x        | Certified           |
| jgenesis |               | Certified           |

## Master System - Mark III {#mastersystem}

RetroBat's `mastersystem` system. Tested on RomM 5.3.1 and RetroBat 8.2.1. The [certification record](https://github.com/Spinnich/rommbat/tree/main/docs/platforms/mastersystem/) has the detail. [Its page](mastersystem.md) says which emulator to pick, what BIOS to supply and what will not work.

| Emulator    | Core            | Status              | Note                                                                                    |
| ----------- | --------------- | ------------------- | --------------------------------------------------------------------------------------- |
| libretro    | genesis_plus_gx | Certified (default) |                                                                                         |
| libretro    | picodrive       | Certified           |                                                                                         |
| libretro    | fbneo           | Not certified       | Boots no game named the No-Intro way, because FBNeo picks its driver from the file name |
| mednafen    | mastersystem    | Certified           |                                                                                         |
| mesen       |                 | Certified           |                                                                                         |
| ares        | MasterSystem    | Certified           |                                                                                         |
| kega-fusion | auto            | Certified           |                                                                                         |
| kega-fusion | mastersystem    | Certified           |                                                                                         |
| bizhawk     | SMSHawk         | Certified           |                                                                                         |
| jgenesis    |                 | Certified           |                                                                                         |

## PlayStation {#psx}

RetroBat's `psx` system. Tested on RomM 5.3.1 and RetroBat 8.2.1. The [certification record](https://github.com/Spinnich/rommbat/tree/main/docs/platforms/psx/) has the detail. [Its page](psx.md) says which emulator to pick, what BIOS to supply and what will not work.

| Emulator    | Core            | Status              |
| ----------- | --------------- | ------------------- |
| libretro    | mednafen_psx_hw | Certified (default) |
| libretro    | swanstation     | Certified           |
| libretro    | pcsx_rearmed    | Certified           |
| duckstation |                 | Certified           |
| mednafen    | psx             | Certified           |
| bizhawk     | Nymashock       | Certified           |
| bizhawk     | Octoshock       | Certified           |

## Nintendo 64 {#n64}

RetroBat's `n64` system. Tested on RomM 5.3.1 and RetroBat 8.2.1. The [certification record](https://github.com/Spinnich/rommbat/tree/main/docs/platforms/n64/) has the detail. [Its page](n64.md) says which emulator to pick, what BIOS to supply and what will not work.

| Emulator  | Core             | Status              |
| --------- | ---------------- | ------------------- |
| libretro  | mupen64plus_next | Certified (default) |
| libretro  | parallel_n64     | Certified           |
| mupen64   |                  | Certified           |
| simple64  |                  | Certified           |
| ares      | Nintendo64       | Certified           |
| bizhawk   | Ares64           | Certified           |
| bizhawk   | Mupen64Plus      | Certified           |
| project64 |                  | Certified           |
| gopher64  |                  | Certified           |

## Game Gear {#gamegear}

RetroBat's `gamegear` system. Tested on RomM 5.3.1 and RetroBat 8.2.1. The [certification record](https://github.com/Spinnich/rommbat/tree/main/docs/platforms/gamegear/) has the detail. [Its page](gamegear.md) says which emulator to pick, what BIOS to supply and what will not work.

| Emulator | Core            | Status              | Note                                                                                    |
| -------- | --------------- | ------------------- | --------------------------------------------------------------------------------------- |
| libretro | genesis_plus_gx | Certified (default) |                                                                                         |
| libretro | picodrive       | Certified           |                                                                                         |
| libretro | fbneo           | Not certified       | Boots no game named the No-Intro way, because FBNeo picks its driver from the file name |
| mednafen | gg              | Certified           |                                                                                         |
| ares     | GameGear        | Certified           |                                                                                         |
| bizhawk  | SMSHawk         | Certified           |                                                                                         |
| jgenesis |                 | Not certified       | RetroBat launches it as a Master System, so a Game Gear game plays to a black screen    |

## 32X {#sega32x}

RetroBat's `sega32x` system. Tested on RomM 5.3.1 and RetroBat 8.2.1. The [certification record](https://github.com/Spinnich/rommbat/tree/main/docs/platforms/sega32x/) has the detail. [Its page](sega32x.md) says which emulator to pick, what BIOS to supply and what will not work.

| Emulator    | Core      | Status              | Note                                                                                 |
| ----------- | --------- | ------------------- | ------------------------------------------------------------------------------------ |
| libretro    | picodrive | Certified (default) |                                                                                      |
| ares        | Mega32X   | Certified           |                                                                                      |
| kega-fusion | sega32x   | Not certified       | Writes its battery saves outside saves/, and its states into the Mega Drive's folder |
| bizhawk     | PicoDrive | Certified           |                                                                                      |
| jgenesis    |           | Certified           |                                                                                      |
