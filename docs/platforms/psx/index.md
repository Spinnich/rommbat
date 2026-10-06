---
summary: The certification record for `psx`: each row's standing at the floor, and the system's own steps.
read-when: Before certifying a `psx` row, or when asked whether a `psx` row works.
---

# psx

Sony PlayStation. RetroBat calls the folder `psx`, which is what this folder is named after.

**Certified at `5.3.1` on 2026-09-26: all seven rows**, every step, with step 6 N/A on the two BizHawk rows, which expose no memory card option. Each non-`libretro` row, and each non-default card type, needed code first; RB-328 to RB-335 are the record of what it took.

RetroBat 8.2.1 declares seven rows, each driven here at its default settings:

- `libretro`/`mednafen_psx_hw`, **the row a stock install gives a user**, `swanstation` and
  `pcsx_rearmed`
- `duckstation`
- `mednafen`/`psx`, which `es_systems.cfg` marks incompatible with `.chd`
- `bizhawk`/`Nymashock` and `Octoshock`, marked the same

## Where each row stands

**Every row holds at the floor, RomM `5.3.1` and RetroBat 8.2.1.** Steps 1 and 3 to 9 were driven
at `5.3.1` on 2026-09-25 and 2026-09-26, apart from step 7 on `mednafen` and both `bizhawk` rows,
which was checked on the multi-disc pass. That pass, step 2, was driven at `5.3.0`, and carries:
`GET /api/roms` and the code that places a multi-disc set did not change, and the `Metal Gear
Solid` set re-synced at `5.3.1` with nothing to do (#236). Step 7 on those three rows carries on
the same re-sync, which wrote no media and left `gamelist.xml` byte-identical. Nothing is owed.

| File                             | What it holds                                                                  |
| -------------------------------- | ------------------------------------------------------------------------------ |
| This file                        | Step 2, the multi-disc sets; steps 1 and 3 for every row; step 8 for every row |
| [libretro.md](libretro.md)       | The three `libretro` rows                                                      |
| [duckstation.md](duckstation.md) | `duckstation`                                                                  |
| [mednafen.md](mednafen.md)       | `mednafen`/`psx`                                                               |
| [bizhawk.md](bizhawk.md)         | The two `bizhawk` rows                                                         |
| [facts.md](facts.md)             | The measured facts about `psx`'s emulators, with RB- IDs                       |

## The install this was measured on

|           |                                                                                                                                                   |
| --------- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| RetroBat  | `8.2.1-stable-win64`, the supported floor                                                                                                         |
| RomM      | `5.3.0` for step 2, and step 7 on `mednafen` and both `bizhawk` rows; the certification pass below ran at `5.3.1`                                 |
| Root      | `R:\RetroBat`                                                                                                                                     |
| Test game | Metal Gear Solid, as two RomM roms: (USA) (Rev 1), rom 320306, two `.chd` and an `.m3u`; (USA), rom 320307, two `.bin`/`.cue` pairs and an `.m3u` |

**Both roms were regrouped in RomM for this pass**, one rom per release holding every disc, which
is the layout RomM recommends and the one RomMBat designs for. Before that the library held 0
multi-file `psx` rows of 9,196, every disc its own rom.

## 2. Multi-file games and extensions

**A disc set lands as RomM holds it: `roms/psx/<fs_name>/` with every disc and `<fs_name>.m3u`.**
EmulationStation lists that folder as one game and shows no disc (RB-310). The unlock is
`data/retrobat/multi_file.json`, and each disc is fetched on its own through `file_ids` so it
resumes and verifies against its own md5 (RB-315).

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
`.m3u` with the first disc's `.cue` before EmuHawk starts (RB-314), so no arrangement of files
reaches disc 2 from the game entry. That is RetroBat's behavior, reported upstream as
[emulatorlauncher#1391](https://github.com/RetroBat-Official/emulatorlauncher/issues/1391), and it is why
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
record: the media pass deleted every disc after it landed (RB-316).

## The certification pass, at `5.3.1`

**Driven on 2026-09-25 and 2026-09-26**, at RomM `5.3.1` and RetroBat 8.2.1, on a deploy of the
`psx-certification` branch. The test game is **Castlevania: Symphony of the Night (USA)**, rom
280632, one `.chd`, pulled through a filter set named `psx certification` (search "Symphony of
the Night", region USA) rather than the whole library. Its gamelist entry pins no emulator, and
`es_settings.cfg` sets no `psx.emulator`, so ES runs the stock row. **Creating the name writes
nothing to the card**: SotN first saves at the first save room in the castle, which takes a while
to reach, so later rows are seeded with this row's card rather than replayed.

**Step 1 passes for every row.** RomM's `psx` platform resolves to the `psx` folder on the
`fs_slug` layer: `RomM's fs_slug 'psx' is already a folder in this install`.

**Step 3.** RetroBat lists one file, `bios/psxonpsp660.bin`, md5 `c53ca590...`, and `bios psx`
reported it present. On 2026-09-26 it was moved out, with no other PS1 BIOS anywhere on the install,
and each row booted through `emulatorLauncher`: **six of seven refuse without it and only
`pcsx_rearmed` boots**, on its HLE BIOS. `bios psx --apply` then fetched it back from RomM at the
listed md5. Nothing is missing and nothing is hashless, so every row is certified with the file.

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

**Nothing for the certification.** Two things are known and recorded rather than owed: a restore fills only a slot no file holds, so a live card deleted beside a stale one of the same game restores nothing (RB-333); and a restored DuckStation state loads only from ES's save-state menu.
