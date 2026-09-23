# psx

Sony PlayStation. RetroBat calls the folder `psx`, which is what this file is named after.

**Not certified.** This record holds step 2 only, taken first because a multi-disc game could not
be synced at all until it was settled. Steps 1 and 3 to 9 are owed for every row, and the memory
card options five of the rows expose are not driven yet.

RetroBat 8.2.1 declares seven rows, each driven here at its default settings:

- `libretro`/`mednafen_psx_hw`, **the row a stock install gives a user**, `swanstation` and
  `pcsx_rearmed`
- `duckstation`
- `mednafen`/`psx`, which `es_systems.cfg` marks incompatible with `.chd`
- `bizhawk`/`Nymashock` and `Octoshock`, marked the same

## The install this was measured on

|           |                                                                                                                                                   |
| --------- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| RetroBat  | `8.2.1-stable-win64`, the supported floor                                                                                                         |
| RomM      | `5.3.0`, the supported floor                                                                                                                      |
| Root      | `R:\RetroBat`                                                                                                                                     |
| Test game | Metal Gear Solid, as two RomM roms: (USA) (Rev 1), rom 320306, two `.chd` and an `.m3u`; (USA), rom 320307, two `.bin`/`.cue` pairs and an `.m3u` |

**Both roms were regrouped in RomM for this pass**, one rom per release holding every disc, which
is the layout RomM recommends and the one RomMBat designs for. Before that the library held 0
multi-file `psx` rows of 9,196, every disc its own rom.

## 2. Multi-file games and extensions

**A disc set lands as RomM holds it: `roms/psx/<fs_name>/` with every disc and `<fs_name>.m3u`.**
EmulationStation lists that folder as one game and shows no disc (finding 308). The unlock is
`data/retrobat/multi_file.json`, and each disc is fetched on its own through `file_ids` so it
resumes and verifies against its own md5 (finding 313).

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
`.m3u` with the first disc's `.cue` before EmuHawk starts (finding 312), so no arrangement of files
reaches disc 2 from the game entry. That is RetroBat's behaviour to report upstream, and it is why
the layout serves the five rows that read a playlist rather than waiting on a layout none could
find.

`<extension>` for `psx` on 8.2.1, read from the live `es_systems.cfg`: `.cue .img .mdf .pbp .toc
.cbn .m3u .ccd .chd .zip .7z .iso .cso .squashfs .decomp`. `.m3u` is in it, so ES lists the
playlist, and `.bin` is not, which is why the playlist names a `.bin` set's `.cue` files.

**Synced through RomMBat on 2026-09-23**, both roms from a RomM collection: the `.chd` set as two
`rom_part` rows and its playlist, the `.bin`/`.cue` set as four and its playlist, every member at
the size and md5 RomM lists, and the gamelist naming `./<fs_name>/<fs_name>.m3u` for each. The
second sync was 0 downloaded, 0 written. The first attempt found a defect, fixed before this
record: the media pass deleted every disc after it landed (finding 314).

## What is owed

Steps 1 and 3 to 9 for all seven rows, a hands-on sync of both roms through RomMBat recorded here,
and the memory card layouts `duckstation`, `swanstation`, `mednafen_psx_hw`, `pcsx_rearmed` and
`mednafen` expose.
