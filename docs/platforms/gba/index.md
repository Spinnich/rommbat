---
summary: The certification record for `gba`: each row's standing at the floor, and the system's own steps.
read-when: Before certifying a `gba` row, or when asked whether a `gba` row works.
---

# gba

Nintendo Game Boy Advance. RetroBat calls the folder `gba`, which is what this folder is named after.

**Nine of the ten rows `gba` declares are certified**, at RomM `5.3.0` and RetroBat 8.2.1 on
2026-09-22, all nine steps with step 6 N/A because `gba` has no class D:

- `libretro`/`mgba`, **the row a stock install gives a user**, selected with no override
- `libretro`/`gpsp` and `libretro`/`mednafen_gba`
- `mgba`/`mgba`, `mednafen`/`gba`, `mesen`, `bizhawk`/`mGBA`, `jgenesis` and
  `ares`/`GameBoyAdvance`

**One is driven and not certified.** `nosgba` cannot open a zip, because NO\$GBA unzips through an
external `PKUNZIP.EXE` it does not ship. It loads a `.gba` placed beside the zip instead, then
deletes it as its own temp file. It also keeps its saves compressed in `emulators/nosgba/BATTERY/`,
outside `saves/` (RB-286).

**It certifies those nine rows and nothing wider.** The two `libretro` rows that share the `.srm`
needed nothing new. The other seven needed a battery rule each, four a state declaration in
RomMBat's bundled supplement, and one of them a new way of naming a save, all scoped to `gba`.

## Where each row stands

**Every certified row holds at the floor, RomM `5.3.1` and RetroBat 8.2.1.** Steps 1 and 9 were
re-driven at `5.3.1` on 2026-09-24, in #236, which maps the nine steps. The other steps carry from
the drive at `5.3.0`, since nothing they exercise changed between the two. Nothing is owed.

| File                           | What it holds                                                                                                                                  |
| ------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| This file                      | Steps 1, 2 and 3, which are the system's, with which rows need the BIOS; what the first boots wrote; what the pass turned up that is not a row |
| [libretro.md](libretro.md)     | The two `libretro` rows that needed no code, `mgba` and `gpsp`                                                                                 |
| [seven-rows.md](seven-rows.md) | The seven rows that needed code                                                                                                                |
| [nosgba.md](nosgba.md)         | `nosgba`, driven and not certifiable                                                                                                           |
| [facts.md](facts.md)           | The measured facts about `gba`'s emulators, with RB- IDs                                                                                       |

## The install this was measured on

|           |                                                                       |
| --------- | --------------------------------------------------------------------- |
| RetroBat  | `8.2.1-stable-win64`, the supported floor                             |
| RomM      | `5.3.0`, read back by `status` as Supported                           |
| Root      | `R:\RetroBat`, found by walking up from the executable                |
| Store     | schema 16 of 16, WAL                                                  |
| Client    | two deploys, named below: the `megadrive` build, then this branch     |
| Budget    | `none`, as for `nes` and `megadrive`                                  |
| Test game | Pokemon - Emerald Version (USA, Europe), 16 MB, flash save and an RTC |

**`mgba` and `nosgba` were not on the install at the start.** Their folders held only RetroBat's
template configs. Each was installed by launching Emerald under it and answering ES's install
prompt, with `tools/m0-probes/probe2-install-emulator.ps1`: mGBA 51.9 MB, NO\$GBA 0.4 MB. The
other eight rows' emulators were present.

**The client was deployed twice, and which build a result was taken on is named.** The two
`libretro` rows that share the `.srm` were certified on the build `megadrive` was, which carries no
gba rules. The other seven were driven on it too, their saves sitting on disk unsyncable, and
certified on a deploy of this branch, whose first flush sent **9 saves and 8 states**.

**The test game has a real-time clock**, and three emulators keep it in a file beside the save.
Emerald carries no `<emulator>` pin in `gamelist.xml`.

**Every row after the first was seeded from the save the one before made**, by the maintainer's
ruling, rather than played through Emerald's intro again. The seed is the file each emulator
finds when it boots; the maintainer then saved in the game, so every save the row files measure is one
that emulator wrote. Two seeds were refused or changed by the emulator, and those are findings.

**The maintainer played over RDP, and the agent drove the states on the standalone rows** from
its session on the RetroBat machine, through `emulatorLauncher` with the row's arguments and keys
sent by `keybd_event`. Those launches skip ES, so they run no hooks and record no session.

## The set

|          |                                             |
| -------- | ------------------------------------------- |
| Name     | Spinnich's Game Boy Advance Favorites       |
| Scope    | `smart_collection 18`                       |
| Policy   | no game cap, no size cap, ordered by recent |
| Resolves | 201 games, 940.6 MB, into `gba`             |

The sync fetched the BIOS first, then 201 ROMs, 759 media files (729 MB), and wrote one gamelist
with 201 entries, exit 0.

## Steps 1, 2 and 3, for every row

### 1. Mapping

Resolved at layer **`fs_slug`**: RomM's `fs_slug` for the platform is literally `gba`, which is
already a folder in this install.

| `fs_slug`        | Resolved by | What `platforms list` says                                          |
| ---------------- | ----------- | ------------------------------------------------------------------- |
| `gba`            | `fs_slug`   | RomM's fs_slug 'gba' is already a folder in this install            |
| `gba-unofficial` | `bundled`   | the bundled table offers `gba`, `gba2players`, and picked the first |

### 2. Extensions

From the live `es_systems.cfg`:

```text
.gba .zip .7z
```

**201 of 201 resolve and nothing is excluded.** Every ROM is a `.zip` holding one `.gba`. `.zip`
was observed to launch on nine rows. **`nosgba` shows "Cartridge not found" for a `.zip`** unless
the bare `.gba` sits beside it, which is the container rather than the extension check (finding
286).

### 3. BIOS

```console
$ rommbat-agent bios gba
1 to fetch (16 KB)
  gba
    fetch 16 KB              bios/gba_bios.bin
$ rommbat-agent bios gba --apply
BIOS: 1 fetched (16 KB)
$ rommbat-agent bios gba
1 present
  gba: all 1 present
```

**Present at `a860e8c0b6d573d191e4ec7db1b1e4f6`**, the md5 RetroBat names, all three exit 0.
Nothing is missing from the library and nothing is hashless. The `sync` fetched it too, before
the ROMs; it was moved out of the tree for the boot test below and brought back by the apply.

## Which rows need the BIOS

**`batocera-systems.json` lists `gba_bios.bin` with no notion of optional, and it is optional on
seven of the ten rows.** RomMBat treats every entry as required, so it fetches the file whenever
RomM has it. By the maintainer's ruling that stays as it is, and this table records which rows
really need it. Each row was launched on Emerald with `bios/gba_bios.bin` out of the tree and no
copy anywhere else in it:

| Row                       | Without the BIOS                                                   |
| ------------------------- | ------------------------------------------------------------------ |
| `libretro`/`mgba`         | Boots to the intro                                                 |
| `libretro`/`mednafen_gba` | Boots to the intro                                                 |
| `libretro`/`gpsp`         | Boots to the intro                                                 |
| `mgba`/`mgba`             | Boots to the intro                                                 |
| `nosgba`                  | Boots a bare `.gba` to the intro                                   |
| `mednafen`/`gba`          | Boots to the intro                                                 |
| `bizhawk`/`mGBA`          | Boots to the intro                                                 |
| `ares`/`GameBoyAdvance`   | **Refuses**: "Game Boy Advance - BIOS (World) is required"         |
| `jgenesis`                | **Refuses**, exit 1: "No Game Boy Advance BIOS provided"           |
| `mesen`                   | **Refuses**: "This game requires a firmware file ... gba_bios.bin" |

**All three that refused boot once `bios --apply` puts it back**, so `emulatorLauncher` hands each
the file from `bios/` without anything RomMBat writes. Mesen copies it into
`emulators/mesen/Firmware/gba_bios.bin` on first use. So a gap report for `gba` is right for three
rows and overstated for seven, and the report cannot tell which because RomMBat does not read
`<system>.emulator`. RB-285.

## What the boot launches wrote

**Not steps 4 or 5.** No one saved in any of these launches; each ran about 25 seconds from
`emulatorLauncher` with no key sent. They are recorded because they show where each row puts a
battery save before one is made, and because every one of them wrote a file.

| Row                       | Wrote at boot, under `saves/gba/`                                       | Size      |
| ------------------------- | ----------------------------------------------------------------------- | --------- |
| `libretro`/`mgba`         | `<rom>.srm`                                                             | 131,072 B |
| `libretro`/`mednafen_gba` | `<rom>.zip#<rom>.605b89b67018abcea91e693a4dd25be3.sav`                  | 131,072 B |
| `libretro`/`gpsp`         | `<rom>.srm`, the file `mgba` wrote                                      | 131,072 B |
| `mgba`/`mgba`             | `<rom>.sav`                                                             | 131,072 B |
| `mednafen`/`gba`          | `<rom>.sav`, the file `mgba` wrote, plus `mednafen/backup/<rom>.<md5>/` | 131,072 B |
| `bizhawk`/`mGBA`          | `bizhawk/<rom>.SaveRAM`                                                 | 131,088 B |
| `jgenesis`                | `jgenesis/gba/<rom>.sav` and `jgenesis/gba/<rom>.rtc`                   | 131,072 B |
| `mesen`                   | `<rom>.sav` and `<rom>.rtc`                                             | 131,072 B |
| `ares`/`GameBoyAdvance`   | nothing: it ignored `WM_CLOSE` and was killed, as on `megadrive`        |           |
| `nosgba`                  | nothing under `saves/`; it has its own `emulators/nosgba/BATTERY/`      |           |

**Every 131,072 B file is the same 128 KB of `0xFF`**, md5 `41d2e2c0...`: an unwritten flash
chip, flushed at boot: the boot-time write of RB-404 on a second system, and the reason a first
save seen with no baseline is not evidence of play. All of them were moved to
`R:\rommbat-evidence\gba\` before any flush could send them. RB-287.

**Three rows share the loose `<rom>.sav`**: `mgba` and `mesen` name it after the ROM, and
`mednafen` opens the plain name when it exists (RB-273). **Two keep the clock in an `.rtc`
beside the save** here, `jgenesis` and `mesen`, and `ares` a third, measured later. **BizHawk's 16
extra bytes** are the clock inside the one file, as standalone mGBA's turned out to be.
**`libretro`/`mednafen_gba` names its save after the zip and the file inside it**, `#` included,
which no `libretro` rule matched. RB-288. Each needed a rule before its row could pass step 4,
as the `nes` and `megadrive` rows did.

## What the pass turned up that is not a row

- **Clock files change on every launch.** Mesen's `.rtc`, jgenesis's `.rtc` and BizHawk's
  `.SaveRAM` all changed on a launch in which nothing was saved, since each records the host time.
  So a session under one of those rows uploads a new version of that slot even when the game was
  not saved, which is small and correct. RB-291.
- **mednafen keeps rotating backups of a save it loads** in
  `saves/gba/mednafen/backup/<rom>.<md5>/`, `0.sav` to `2.sav` beside a one-byte counter `C.sav`.
  They are not saves, and `saves` lists the four files as `not in this release` under `gba`.
  RB-292.
- **Standalone mGBA is killed, not closed, by the pad's exit.** `es_padtokey.cfg` maps
  Hotkey+Start to `(%{KILL})` for mGBA, and a kill may beat mGBA's write of a save made just
  before. The maintainer waited a few seconds after saving and the save was intact.

## What this file will not claim

- **Nothing about `gba` under any build but these.** Every row was measured on RetroBat 8.2.1 and
  RomM `5.3.0`.
- **Nothing about another game.** Emerald is one 128 KB flash cartridge with a clock; a game with
  SRAM or EEPROM, or no clock, names and sizes its files differently on at least ares.
- **Nothing about mednafen_gba past the boot on a bare `.gba` or a `.7z`.** Each was booted once
  to read the name it writes; no save was made or restored on either.
