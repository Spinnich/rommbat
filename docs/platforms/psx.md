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
| 8   | **Pass.** `status` reads this row's ES session back against its rom; see the table below                                                                                                                                                            |
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
| 8   | **Pass.** `status` reads this row's ES session back against its rom; see the table below                                                                                                                                                             |
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

| #   | Result                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| --- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 2   | Carried from the multi-disc pass above                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| 3   | Owed: boot with `psxonpsp660.bin` moved out                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| 4   | **Pass, class A per port.** 432 bytes changed, all inside block 1. Attributed to rom 280632 by the launch route, `learned_from` `journal`, and sent as save 515 in `duckstation:battery`, `42117720...` on both sides. Deleted and restored, it came back byte for byte into `memcards/`                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      |
| 5   | **Pass.** DuckStation writes `emulators/duckstation/savestates/SLUS-00067_<slot>.sav`, keyed by serial, and keeps the state a new one replaced as `.bak`. `emulatorLauncher` mirrors all three into the declared `saves/psx/duckstation/<rom>_NN.sav` incrementally: `_01` is slot 1's `.bak`, `_02` slot 1 and `_03` slot 2, equal by md5, uploaded as `duckstation::1` to `::3`. `_02` deleted and restored came back byte for byte. **The mirror goes one way**: with DuckStation's own SotN states moved aside, its in-game load found none, since a plain launch copies nothing back. ES's save-state menu is the way back: it passes `-state_file` with the declared path and the launcher hands that file to DuckStation as `-statefile`, leaving a copy natively as `SLUS-00067_01.sav`. The restored `_02`, launched that way through `emulatorLauncher`, opened in play in the castle. DuckStation warned that memory card 1 in the state did not match the card on disk and simulated a replug, its own check against a card written after the state; the game carried on. No `.png` is written: DuckStation keeps the screenshot inside the state |
| 6   | Owed: `duckstation_memcardtype` `Shared`, `PerGameFileTitle` and `PerGame`                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| 7   | Carried: the same entry and media as the stock row                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| 8   | **Pass.** `status` reads this row's ES session back against its rom; see the table below                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      |
| 9   | **Pass.** After the restore, `flush` uploaded nothing and the sync preview answered nothing to do                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |

### `mednafen`/`psx`

Driven on **Metal Gear Solid (USA)**, rom 320307, the `.bin`/`.cue` set, since `es_systems.cfg`
marks this row incompatible with `.chd` and SotN is one. Selected with `psx.emulator` `mednafen`
and `psx.core` `psx`, set with ES closed, and confirmed from `emulatorLauncher.log`: `-rom` names
the set's `.m3u`. The save was made through Mei Ling on the codec, 140.96, which the game offers
only after the opening.

**At RetroBat's default this row has no memory card** (finding 330). With
`mednafen_psx_memcards` unset, `emulatorLauncher` writes `psx.input.port1.memcard 0` through
`port8` into `mednafen.cfg`, and the game reports no card. Set to `2` in `es_settings.cfg`, the
launcher wrote ports 1 and 2 as `1`. **The row is certified with `psx.mednafen_psx_memcards` set
to `2`**, which is mednafen's own default of a card in each of the first two ports. Steps 4 and 5
needed code first (finding 331).

| #   | Result                                                                                                                                                                                                                                                                                                                                                                                                   |
| --- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 2   | Carried from the multi-disc pass above                                                                                                                                                                                                                                                                                                                                                                   |
| 3   | Owed: boot with `psxonpsp660.bin` moved out                                                                                                                                                                                                                                                                                                                                                              |
| 4   | **Pass, class A per port.** A loose `<rom>.2f876f49....0.mcr` for port 1 holding the save, one frame `0x51` named `BASLUS-00594...`, and `.1.mcr` for port 2, blank and so passed over. Sent as save 516 in `mednafen:battery`, attributed to rom 320307 by the `.m3u`'s stem. Deleted and restored, it came back byte for byte, `6ef89967...`, under the name mednafen opens                            |
| 5   | **Pass.** No `es_savestates.cfg` entry covers the row; mednafen writes `mednafen/sstates/<rom>.<hash>.mc<n>`, which the bundled supplement now declares for `psx`. Two states, `mc0` and `mc1`, uploaded against rom 320307. `mc1` deleted and restored came back byte for byte, `dbb638ff...`, into the directory mednafen reads its states from, with no mirror between. mednafen writes no screenshot |
| 6   | Owed: `mednafen_psx_memcards` `1`, `3` and `4`; `0` is the default above                                                                                                                                                                                                                                                                                                                                 |
| 7   | Carried: MGS's entry and media were checked in the multi-disc pass                                                                                                                                                                                                                                                                                                                                       |
| 8   | **Pass.** `status` reads this row's ES session back against its rom; see the table below                                                                                                                                                                                                                                                                                                                 |
| 9   | **Pass.** After the restore, `flush` uploaded nothing and `sync "Metal Gear Solid" --dry-run` answered nothing to do                                                                                                                                                                                                                                                                                     |

### `bizhawk`/`Nymashock`

Driven on Metal Gear Solid (USA), rom 320307, for the same `.chd` reason as mednafen. Selected with
`psx.emulator` `bizhawk` and `psx.core` `Nymashock`, set with ES closed, and confirmed from
`emulatorLauncher.log`; the launcher hands EmuHawk disc 1's `.cue` (finding 314). Seeded by copying
mednafen's port-1 card to `bizhawk/Metal Gear Solid (USA) (Disc 1) (v1.0).SaveRAM`, the name
BizHawk's own title gives it, since Nymashock keeps one raw 131,072 B card; the game found the save
and a new one was made through the codec. Steps 4 and 5 needed code first (finding 332).

| #   | Result                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| --- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 2   | Carried from the multi-disc pass above: disc 1 only                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| 3   | Owed: boot with `psxonpsp660.bin` moved out                                                                                                                                                                                                                                                                                                                                                                                                                                                      |
| 4   | **Pass, class A.** The `.SaveRAM` changed in blocks 0 and 1, the save's file counter moving from `G0006` to `G0007`. The BizHawk rule now covers `psx`; the card was bound to rom 320307 through the state's sidecar and went up in `bizhawk:battery`. Deleted and restored, it came back byte for byte, `e6bdb931...`, into `bizhawk/`                                                                                                                                                          |
| 5   | **Pass, after a fix.** One state, `QuickSave0`, from the pad at ES's slot; EmuHawk writes `emulators/bizhawk/sstates/psx/<title>.Nymashock.QuickSave0.State` and the launcher mirrors it to `saves/psx/bizhawk/sstates/Nymashock/<disc 1>.QuickSave0.State`. The first restore named it after the `.m3u`, `Metal Gear Solid (USA).QuickSave0.State`, with the right bytes. Fixed, it came back under disc 1's name byte for byte, `1c31d1d0...`. The frame is `Framebuffer.bmp` inside the state |
| 6   | N/A: BizHawk exposes no memory card option                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| 7   | Carried: MGS's entry and media were checked in the multi-disc pass                                                                                                                                                                                                                                                                                                                                                                                                                               |
| 8   | **Pass.** `status` reads this row's ES session back against its rom; see the table below                                                                                                                                                                                                                                                                                                                                                                                                         |
| 9   | **Pass.** After the second restore, `flush` uploaded nothing and the sync preview answered nothing to do                                                                                                                                                                                                                                                                                                                                                                                         |

### `bizhawk`/`Octoshock`

Selected with `psx.core` `Octoshock`, set with ES closed, and confirmed from `emulatorLauncher.log`.
It shares Nymashock's `.SaveRAM` and **read Nymashock's 131,072 B card**: the game found the save,
and the codec save moved the counter to `G0008`. Octoshock wrote the file back at 262,144 B, the card
followed by 128 KB that holds no card, as it did on 2026-09-23. The previous file went to
`.SaveRAM.bak`, which the rule passes over.

| #   | Result                                                                                                                                                                                               |
| --- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 2   | Carried from the multi-disc pass above: disc 1 only                                                                                                                                                  |
| 3   | Owed: boot with `psxonpsp660.bin` moved out                                                                                                                                                          |
| 4   | **Pass, class A.** Sent as save 518 in `bizhawk:battery`, one slot with Nymashock's. Deleted and restored, it came back byte for byte, `a5110bae...`, the newest of saves 517 and 518                |
| 5   | **Pass.** `QuickSave0` mirrored to `saves/psx/bizhawk/sstates/Octoshock/<disc 1>.QuickSave0.State`, 1,069,352 B. Deleted and restored, it came back under disc 1's name byte for byte, `6cdaac6a...` |
| 6   | N/A: BizHawk exposes no memory card option                                                                                                                                                           |
| 7   | Carried: MGS's entry and media were checked in the multi-disc pass                                                                                                                                   |
| 8   | **Pass.** `status` reads this row's ES session back against its rom; see the table below                                                                                                             |
| 9   | **Pass.** After the restore, `flush` uploaded nothing and the sync preview answered nothing to do                                                                                                    |

### Step 8, every row

`rommbat-agent status` on 2026-09-26 read these sessions back from RomM for this device, each
matched to its launch in `emulatorLauncher.log` (local time, UTC-4). Launches made from the agent's
session through `emulatorLauncher` directly run no ES hook and record no session, which is correct.

| Row                          | Sessions (UTC)                                                | Rom    |
| ---------------------------- | ------------------------------------------------------------- | ------ |
| `libretro`/`mednafen_psx_hw` | 2026-09-25 10:44:49Z, 1m 45s; 13:17:26Z, 11m 42s              | 280632 |
| `libretro`/`swanstation`     | 2026-09-25 13:41:04Z, 1m 16s                                  | 280632 |
| `libretro`/`pcsx_rearmed`    | 2026-09-25 13:44:36Z, 1m 2s                                   | 280632 |
| `duckstation`                | 2026-09-25 13:49:02Z, 1m 40s; 2026-09-26 10:18:54Z, 10:39:43Z | 280632 |
| `mednafen`/`psx`             | 2026-09-26 10:46:10Z, 10:50:02Z, 10:58:53Z                    | 320307 |
| `bizhawk`/`Nymashock`        | 2026-09-26 11:21:30Z, 1m 44s                                  | 320307 |
| `bizhawk`/`Octoshock`        | 2026-09-26 11:32:19Z, 1m 48s                                  | 320307 |

## What is owed

**Two steps across the rows, and nothing else.** Steps 1, 2, 4, 5, 7, 8 and 9 pass on all seven.

- **Step 3's boot check on every row:** boot with `psxonpsp660.bin` moved out and record which
  refuse. Standalone mednafen's legacy-BIOS option names an SCPH file RetroBat's list does not.
- **Step 6 on the five rows with card options:** DuckStation's `Shared`, `PerGameFileTitle` and
  `PerGame`; `swanstation`'s `Shared`, `PerGame` and `PerGameTitle`; `mednafen_psx_hw`'s shared card;
  `pcsx_rearmed`'s second card; and mednafen's card counts `1`, `3` and `4`. A shared card needs two
  games saving to it to show whether RomMBat keeps it off both.
