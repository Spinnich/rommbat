---
summary: The certification record for `gamegear`: the two `libretro` rows, and FBNeo.
read-when: When a result for one of these `gamegear` rows is needed, or before re-driving one.
---

# gamegear: The two `libretro` rows, and FBNeo

|                   | Selected by                                   | Confirmed on the ES launch line            |
| ----------------- | --------------------------------------------- | ------------------------------------------ |
| `genesis_plus_gx` | **Nothing: RetroBat's default**               | `-emulator libretro -core genesis_plus_gx` |
| `picodrive`       | the game's emulator option in ES              | `-core picodrive -state_slot 3`            |
| `fbneo`           | the game's emulator option in ES              | `-core fbneo -state_slot 3`                |

| #   | `genesis_plus_gx`                                                       | `picodrive`                                       |
| --- | ----------------------------------------------------------------------- | ------------------------------------------------- |
| 4   | **Pass, both directions.** Class A `.srm`, 3,840 B                      | **Pass, both directions.** The same `.srm`, 32 KB |
| 5   | **Pass**, the screenshot byte-checked                                   | **Pass**, the screenshot byte-checked             |
| 6   | **N/A.** `gamegear` is class A                                          | **N/A**                                           |
| 7   | **Pass.** Launched from ES after the sync, with box art and description | **Pass**, carried                                 |
| 8   | **Pass.** 10:20:38Z to 10:22:14Z, 1m 36s                                | **Pass.** 10:23:43Z to 10:24:14Z, 31s             |
| 9   | **Pass.** 546 present and verified, 2,046 media, gamelist byte-identical | **Pass**                                          |

| Core              | `.srm` after  | States, slot: state, png                                            |
| ----------------- | ------------- | ------------------------------------------------------------------- |
| `genesis_plus_gx` | `0c136a7a...` | 1: `d55b0052...`, `ffecb041...`; 2: `3c9941f0...`, `68f18b0d...`     |
| `picodrive`       | `f374fdb8...` | 1: `85d3bfe8...`, `81f25fb6...`; 2: `43140b07...`, `ef10edc1...`     |

**The maintainer started the game on the stock row**, played a minute and made two states on
different screens, so each slot's image is its own. RetroArch's periodic flush wrote the full
65,536 B buffer while the game ran, and Genesis Plus GX trimmed the file to 3,840 B on exit: 2,588
bytes written from `0x200` on, beyond the boot write's header.

**PicoDrive read that file and wrote it back at 32 KB**: the game's Continue resumed where the stock
row stopped, 78 bytes within the first 3,840 changed with the new progress, and the rest of the
32,768 B is zero-padding, as on `mastersystem`. **The declared `<directory>` is where both cores
wrote**, `saves/gamegear/libretro.<core>/`, and RetroArch chose slots 1 and 2 whatever ES passed
(RB-261).

Each row's flush sent its `.srm` as `libretro:battery` and both states as
`libretro:<core>:<slot>`. At the end, `state1` of Genesis Plus GX and `state2` of PicoDrive, each with
its `.png`, were deleted and brought back by `saves restore 272855 --apply`, and the shared `.srm`
likewise: every file at its own md5, and each image its own slot's, not the other slot's.

## `libretro`/`fbneo`: driven, and not certifiable on this library

**FBNeo shows "Romset is unknown" and never starts the game**, from ES and from the agent's launch
alike, and writes nothing under `saves/`. It takes a console game's set from the file name, and every
ROM in the set carries its No-Intro name, so the row boots none of what RomM serves. Recorded as not
certifiable for `mastersystem`'s reason (RB-325), with `gamegear`'s own fact RB-412. The ES session,
10:45:45Z to 10:46:01Z, reached RomM as a 16 s session.
