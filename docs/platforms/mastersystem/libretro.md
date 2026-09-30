---
summary: The certification record for `mastersystem`: the two `libretro` rows, and FBNeo.
read-when: When a result for one of these `mastersystem` rows is needed, or before re-driving one.
---

# mastersystem: The two `libretro` rows, and FBNeo

|                   | Selected by                                             | Confirmed on the ES launch line            |
| ----------------- | ------------------------------------------------------- | ------------------------------------------ |
| `genesis_plus_gx` | **Nothing: RetroBat's default**                         | `-emulator libretro -core genesis_plus_gx` |
| `picodrive`       | `mastersystem.emulator = libretro`, `.core = picodrive` | `-core picodrive -state_slot 4`            |
| `fbneo`           | the agent's launch                                      | `fbneo_libretro.dll ... --subsystem sms`   |

Each override was set with ES closed, and the `mastersystem` keys were cleared when the pass ended,
leaving `es_settings.cfg` as the copy taken first,
`R:\rommbat-evidence\mastersystem\es_settings.before.cfg`, apart from what ES itself rewrites.

| #   | `genesis_plus_gx`                                                      | `picodrive`                                       |
| --- | ---------------------------------------------------------------------- | ------------------------------------------------- |
| 4   | **Pass, both directions.** Class A `.srm`, 8,191 B                     | **Pass, both directions.** The same `.srm`, 32 KB |
| 5   | **Pass**, the screenshot byte-checked                                  | **Pass**, the screenshot byte-checked             |
| 6   | **N/A.** `mastersystem` is class A                                     | **N/A**                                           |
| 7   | **Pass.** Launched from ES after the sync, with box art                | **Pass**, carried                                 |
| 8   | **Pass.** 15:22:47Z to 15:23:27Z, 40s                                  | **Pass.** 15:25:52Z to 15:26:28Z, 36s             |
| 9   | **Pass.** 153 present and verified, 599 media, gamelist byte-identical | **Pass**                                          |

| Core              | `.srm` after  | States, slot: state, png                                                                          |
| ----------------- | ------------- | ------------------------------------------------------------------------------------------------- |
| `genesis_plus_gx` | `b98e4e38...` | 1: `b4436392...`, `d83b83a1...`; 2: `bf94637a...`, `d83b83a1...`; 3: `6a962a20...`, `5ff0f76a...` |
| `picodrive`       | `f898854e...` | 1: `8e6fa3e2...`, `3d44f154...`; 2: `68821f20...`, `b515b0a9...`                                  |

**The maintainer created the character on the stock row**, which is the one session that changed the
committed save, and made three states, the first two on the same frame. **Slot 3 was the one
compared**, since slots 1 and 2 share an image and cannot tell a real link from a wrong one. After
each row its `.srm` and one state with its `.png` went out of the tree and came back through
`saves restore 239603 --apply`, `restored 1 save(s) and 1 state(s), failed 0, ... with 1
screenshot(s)`, exit 0, every file at its own md5, the image that slot's own.

**PicoDrive read the file Genesis Plus GX wrote and wrote it back at 32 KB**: its 32,768 B are
Genesis Plus GX's 8,191 B followed by zeros, as on `megadrive`. **The declared `<directory>` is where
both cores wrote**, `saves/mastersystem/libretro.<core>/`, and RetroArch chose slots 1 onward from
`found_last_state_slot: #0` whatever ES passed (RB-261).

## `libretro`/`fbneo`: driven, and not certifiable on this library

**FBNeo shows "Romset is unknown" and never starts the game.** RetroBat launches it with
`--subsystem sms`, under which FBNeo takes the set from the file name. RetroBat's own
`bios/fba/FB Alpha (ClrMame Pro XML, Master System only).dat` names the test game's set `gaxewarr`,
CRC `c7ded988`, the same bytes. Started directly on two copies of the zip, outside the ROM tree:

| File                                                | What FBNeo did                                          |
| --------------------------------------------------- | ------------------------------------------------------- |
| `Golden Axe Warrior (USA, Europe, Brazil) (En).zip` | searched for no set at all, 640x480, the error screen   |
| `gaxewarr.zip`, the same bytes                      | `Romset found`, 256x192, the Master System's resolution |

So the row boots none of a No-Intro set, which is what RomM serves, and is **recorded as not
certifiable by the maintainer's ruling**, without an ES session. Nothing was written under `saves/`.
RB-325.
