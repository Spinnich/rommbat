# psx

Sony PlayStation. RetroBat calls the folder `psx`, which is what this file is named after.

**Not certified.** Step 2 was taken first, because a multi-disc game could not be synced at all
until it was settled. The certification pass is under way: step 1 holds for every row and the
stock row has passed steps 4, 5, 8 and 9. What is owed is listed at the end.

RetroBat 8.2.1 declares seven rows, each driven here at its default settings:

- `libretro`/`mednafen_psx_hw`, **the row a stock install gives a user**, `swanstation` and
  `pcsx_rearmed`
- `duckstation`
- `mednafen`/`psx`, which `es_systems.cfg` marks incompatible with `.chd`
- `bizhawk`/`Nymashock` and `Octoshock`, marked the same

## The move to `5.3.1`

**Step 2, the one step this record holds, carries.** `GET /api/roms` takes the same parameters,
`roms/files.py` changed only in typing, and nothing under `src/` that places a multi-disc set
moved (finding 14 of `docs/romm-5.3-findings.md`). The `Metal Gear Solid` set answered `nothing to
do: 2 games already present, 0 downloaded, 0 written` on a deploy of the adoption branch, with
`status` reading `5.3.1` as Supported. Steps 1 and 3 to 9 stay owed, now at `5.3.1`.

## The install this was measured on

|           |                                                                                                                                                   |
| --------- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| RetroBat  | `8.2.1-stable-win64`, the supported floor                                                                                                         |
| RomM      | `5.3.0`, the floor then                                                                                                                           |
| Root      | `R:\RetroBat`                                                                                                                                     |
| Test game | Metal Gear Solid, as two RomM roms: (USA) (Rev 1), rom 320306, two `.chd` and an `.m3u`; (USA), rom 320307, two `.bin`/`.cue` pairs and an `.m3u` |

**Both roms were regrouped in RomM for this pass**, one rom per release holding every disc, which
is the layout RomM recommends and the one RomMBat designs for. Before that the library held 0
multi-file `psx` rows of 9,196, every disc its own rom.

## 2. Multi-file games and extensions

**A disc set lands as RomM holds it: `roms/psx/<fs_name>/` with every disc and `<fs_name>.m3u`.**
EmulationStation lists that folder as one game and shows no disc (finding 310). The unlock is
`data/retrobat/multi_file.json`, and each disc is fetched on its own through `file_ids` so it
resumes and verifies against its own md5 (finding 315).

| Row                          | Reads the playlist                   | States named after                               |
| ---------------------------- | ------------------------------------ | ------------------------------------------------ |
| `libretro`/`mednafen_psx_hw` | yes, both discs, swap driven         | the playlist                                     |
| `libretro`/`swanstation`     | yes, both discs                      | the playlist                                     |
| `libretro`/`pcsx_rearmed`    | yes, both discs                      | the playlist                                     |
| `duckstation`                | yes                                  | disc serial natively, the playlist in the mirror |
| `mednafen`/`psx`             | yes, `.bin`/`.cue` set               | the playlist and one hash for the set            |
| `bizhawk`/`Nymashock`        | **no**: the launcher hands it disc 1 | disc 1's file                                    |
| `bizhawk`/`Octoshock`        | **no**: the launcher hands it disc 1 | disc 1's file                                    |

**The two BizHawk rows boot disc 1 only, whatever the layout.** `emulatorLauncher` replaces the
`.m3u` with the first disc's `.cue` before EmuHawk starts (finding 314), so no arrangement of files
reaches disc 2 from the game entry. That is RetroBat's behaviour to report upstream, and it is why
the layout serves the five rows that read a playlist rather than waiting on a layout none could
find.

`<extension>` for `psx` on 8.2.1, read from the live `es_systems.cfg`: `.cue .img .mdf .pbp .toc
.cbn .m3u .ccd .chd .zip .7z .iso .cso .squashfs .decomp`. `.m3u` is in it, so ES lists the
playlist, and `.bin` is not, which is why the playlist names a `.bin` set's `.cue` files.

**Synced through RomMBat on 2026-09-23**, both roms from a RomM collection: the `.chd` set as two
`rom_part` rows and its playlist, the `.bin`/`.cue` set as four and its playlist, every member at
the size and md5 RomM lists, and the gamelist naming `./<fs_name>/<fs_name>.m3u` for each. The
second sync was 0 downloaded, 0 written. Both games then launched from EmulationStation's PlayStation
list, each entry being its playlist. The first attempt found a defect, fixed before this
record: the media pass deleted every disc after it landed (finding 316).

## The certification pass, at `5.3.1`

**In progress, started 2026-09-25**, at RomM `5.3.1` and RetroBat 8.2.1, on a deploy of the
`psx-certification` branch. The test game is **Castlevania: Symphony of the Night (USA)**, rom
280632, one `.chd`, pulled through a filter set named `psx certification` (search "Symphony of
the Night", region USA) rather than the whole library. Its gamelist entry pins no emulator, and
`es_settings.cfg` sets no `psx.emulator`, so ES runs the stock row. **Creating the name writes
nothing to the card**: SotN first saves at the first save room in the castle, which takes a while
to reach, so later rows are seeded with this row's card rather than replayed.

**Step 1 passes for every row.** RomM's `psx` platform resolves to the `psx` folder on the
`fs_slug` layer: `RomM's fs_slug 'psx' is already a folder in this install`.

**Step 3, the inventory half.** RetroBat lists one file, `bios/psxonpsp660.bin`, md5
`c53ca590...`, and `bios psx` reports it present. Which rows refuse to boot without it is owed.

### `libretro`/`mednafen_psx_hw`

Selected by default and confirmed from `emulatorLauncher.log`: `-system psx -emulator libretro
-core mednafen_psx_hw`.

| #   | Result                                                                                                                                                                                                                                                                                                                                                            |
| --- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 2   | Carried from the multi-disc pass above                                                                                                                                                                                                                                                                                                                            |
| 3   | Owed: boot with `psxonpsp660.bin` moved out                                                                                                                                                                                                                                                                                                                       |
| 4   | **Pass, class A.** A loose `saves/psx/<rom>.srm`, a whole 131,072 B card, uploaded as save 512 with md5 `46b61dd3...` equal on both sides. Its one used frame is `0x51`, `BASLUS-00067DRAX00`, SotN's USA serial. Deleted from the tree, `saves restore 280632 --apply` brought it back byte for byte, choosing 512 over the empty card sent as 511 (finding 328) |
| 5   | **Pass.** Two states in `saves/psx/libretro.mednafen_psx_hw/`, the declared directory, slots 1 and 2 with screenshots of 118,638 B and 14,187 B. `state1` and its `.png` deleted and restored came back byte for byte, `93acce93...` and `0b3b025c...`, and the image differs from `state2.png`, so the link is to this state                                     |
| 6   | Owed: the shared card option                                                                                                                                                                                                                                                                                                                                      |
| 7   | Launched from ES with image, thumbnail, marquee, video and manual synced                                                                                                                                                                                                                                                                                          |
| 8   | **Pass.** `status` reads both sessions back against rom 280632: 10:44:49Z for 1m 45s, and 13:17:26Z for 11m 42s                                                                                                                                                                                                                                                   |
| 9   | **Pass.** After the restore, `flush` uploaded nothing for the game and `sync "psx certification" --dry-run` answered nothing to do                                                                                                                                                                                                                                |

**The Metal Gear Solid card was blank too.** Its 131,072 B `.srm`, sent as save 441 during the
multi-disc pass on 2026-09-23, has all fifteen frames never used. On the fixed build the scan
passes over it, and the count of loose saves fell from 65 to 64 with nothing else changed.

### `libretro`/`swanstation`

Selected with `psx.emulator` `libretro` and `psx.core` `swanstation` in `es_settings.cfg`, set with
ES closed, and confirmed from `emulatorLauncher.log`. ES passed `-state_slot 3`; RetroArch wrote
slots 1 and 2, as finding 261 describes. **It shares the loose `.srm` with the other `libretro`
cores**, so it was seeded by the card `mednafen_psx_hw` left, loaded it, and saved at the same
save room.

| #   | Result                                                                                                                                                                                                                                              |
| --- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 2   | Carried from the multi-disc pass above                                                                                                                                                                                                              |
| 3   | Owed: boot with `psxonpsp660.bin` moved out                                                                                                                                                                                                         |
| 4   | **Pass, class A.** The card changed in 392 bytes, all inside block 1, SotN's, and went up as save 513, `d822fc83...` on both sides. Deleted and restored, it came back byte for byte, the newest of saves 511, 512 and 513                          |
| 5   | **Pass.** Slots 1 and 2 in `saves/psx/libretro.swanstation/`, the declared directory. `state2` and its `.png` deleted and restored came back byte for byte, `9b386a94...` and `b7dd97e3...`, and the image differs from `state1.png`, `d75b4e1d...` |
| 6   | Owed: `swanstation_memcard1` `Shared`, `PerGame` and `PerGameTitle`                                                                                                                                                                                 |
| 7   | Carried: the same entry and media as the stock row                                                                                                                                                                                                  |
| 8   | Owed: read back with `status` at the end of the pass                                                                                                                                                                                                |
| 9   | **Pass.** After the restore, `flush` uploaded nothing and the sync preview answered nothing to do                                                                                                                                                   |

### `libretro`/`pcsx_rearmed`

Selected with `psx.core` `pcsx_rearmed`, set with ES closed, and confirmed from
`emulatorLauncher.log`. Seeded by swanstation's card in the shared loose `.srm`, loaded, and saved
at the same save room. ES passed `-state_slot 3` and RetroArch wrote slots 1 and 2.

| #   | Result                                                                                                                                                                                                                                               |
| --- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 2   | Carried from the multi-disc pass above                                                                                                                                                                                                               |
| 3   | Owed: boot with `psxonpsp660.bin` moved out                                                                                                                                                                                                          |
| 4   | **Pass, class A.** 424 bytes changed, all inside block 1, and went up as save 514, `46b9337c...` on both sides. Deleted and restored, it came back byte for byte, the newest of four server saves                                                    |
| 5   | **Pass.** Slots 1 and 2 in `saves/psx/libretro.pcsx_rearmed/`, the declared directory. `state1` and its `.png` deleted and restored came back byte for byte, `90e2673e...` and `4d847e47...`, and the image differs from `state2.png`, `36b6486e...` |
| 6   | Owed: `pcsx_rearmed_memcard2`                                                                                                                                                                                                                        |
| 7   | Carried: the same entry and media as the stock row                                                                                                                                                                                                   |
| 8   | Owed: read back with `status` at the end of the pass                                                                                                                                                                                                 |
| 9   | **Pass.** After the restore, `flush` uploaded nothing and the sync preview answered nothing to do                                                                                                                                                    |

### `duckstation`

Selected with `psx.emulator` `duckstation` and no `psx.core`, set with ES closed, and confirmed
from `emulatorLauncher.log`: `-system psx -emulator duckstation -core` with an empty core.
DuckStation keeps both ports at RetroBat's `PerGameTitle`, so its card for SotN is
`saves/psx/duckstation/memcards/<saveName>_1.mcd`, where `saveName` is `gamedb.yaml`'s for
`SLUS-00067`: `Castlevania - Symphony of the Night (USA)`, which happens to equal the rom's stem.
Seeded by copying `pcsx_rearmed`'s `.srm` there, since the raw card is the same 128 KB image; the
game loaded it and saved at the same save room. SotN never touched port 2, so no `_2.mcd` was
written. **Steps 4 and 5 needed code first** (finding 329): no rule covered `duckstation/memcards/`,
so the card was reported rather than synced until the `duckstation` battery rule landed.

| #   | Result                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| --- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 2   | Carried from the multi-disc pass above                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| 3   | Owed: boot with `psxonpsp660.bin` moved out                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| 4   | **Pass, class A per port.** 432 bytes changed, all inside block 1. Attributed to rom 280632 by the launch route, `learned_from` `journal`, and sent as save 515 in `duckstation:battery`, `42117720...` on both sides. Deleted and restored, it came back byte for byte into `memcards/`                                                                                                                                                                                                                                                               |
| 5   | **Byte half passes; the load half is owed.** DuckStation writes `emulators/duckstation/savestates/SLUS-00067_<slot>.sav`, keyed by serial, and keeps the state a new one replaced as `.bak`. `emulatorLauncher` mirrors all three into the declared `saves/psx/duckstation/<rom>_NN.sav` incrementally: `_01` is slot 1's `.bak`, `_02` slot 1 and `_03` slot 2, equal by md5. Each uploaded as `duckstation::1` to `::3`. `_02` deleted and restored came back byte for byte. No `.png` is written: DuckStation keeps the screenshot inside the state |
| 6   | Owed: `duckstation_memcardtype` `Shared`, `PerGameFileTitle` and `PerGame`                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| 7   | Carried: the same entry and media as the stock row                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| 8   | Owed: read back with `status` at the end of the pass                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| 9   | **Pass.** After the restore, `flush` uploaded nothing and the sync preview answered nothing to do                                                                                                                                                                                                                                                                                                                                                                                                                                                      |

## What is owed

Step 3's boot check and step 6 on every row, and steps 3 to 9 on the six rows after the stock
one. Step 6 drives every card mode the rows expose: DuckStation's `PerGameTitle`, `Shared`,
`PerGameFileTitle` and `PerGame`; `swanstation`'s `Shared`, `PerGame` and `PerGameTitle`;
`mednafen_psx_hw`'s shared card; `pcsx_rearmed`'s second card; and mednafen's card count.
