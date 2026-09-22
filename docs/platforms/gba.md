# gba

Nintendo Game Boy Advance. RetroBat calls the folder `gba`, which is what this file is named
after.

**Two of ten rows are certified**, at RomM `5.3.0` and RetroBat 8.2.1 on 2026-09-22, all nine
steps with step 6 N/A: `libretro`/`mgba`, **the row a stock install gives a user**, selected with
no override, and `libretro`/`gpsp`. Steps 1, 2 and 3 pass for every row, and every row has been booted once from
`emulatorLauncher` with and without the BIOS. The other eight rows are open at steps 4 to 9.

`gba` declares ten rows:

- `libretro`/`mgba`, **the row a stock install gives a user**, since it is the first core listed
- `libretro`/`mednafen_gba` and `libretro`/`gpsp`
- `mgba`/`mgba`, `nosgba`, `mednafen`/`gba`, `ares`/`GameBoyAdvance`, `bizhawk`/`mGBA`,
  `jgenesis` and `mesen`

## The install this was measured on

|           |                                                                       |
| --------- | --------------------------------------------------------------------- |
| RetroBat  | `8.2.1-stable-win64`, the supported floor                             |
| RomM      | `5.3.0`, the supported floor, read back by `status` as Supported      |
| Root      | `R:\RetroBat`, found by walking up from the executable                |
| Store     | schema 16 of 16, WAL                                                  |
| Client    | the deploy `megadrive` was certified on, carrying no gba rules        |
| Budget    | `none`, as for `nes` and `megadrive`                                  |
| Test game | Pokemon - Emerald Version (USA, Europe), 16 MB, flash save and an RTC |

**`mgba` and `nosgba` were not on the install at the start.** Their folders held only RetroBat's
template configs. Each was installed by launching Emerald under it and answering ES's install
prompt, with `tools/m0-probes/probe2-install-emulator.ps1`: mGBA 51.9 MB, NO$GBA 0.4 MB. The
other eight rows' emulators were present.

**The test game has a real-time clock**, and two emulators keep it in a file beside the save
(below). Emerald carries no `<emulator>` pin in `gamelist.xml`.

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
was observed to launch on nine rows. **`nosgba` cannot open a `.zip`** and shows "Cartridge not
found" for every game in this library, which is the container rather than the extension check
(finding 286).

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
| `nosgba`                  | Boots a bare `.gba` to the intro; a `.zip` never loads             |
| `mednafen`/`gba`          | Boots to the intro                                                 |
| `bizhawk`/`mGBA`          | Boots to the intro                                                 |
| `ares`/`GameBoyAdvance`   | **Refuses**: "Game Boy Advance - BIOS (World) is required"         |
| `jgenesis`                | **Refuses**, exit 1: "No Game Boy Advance BIOS provided"           |
| `mesen`                   | **Refuses**: "This game requires a firmware file ... gba_bios.bin" |

**All three that refused boot once `bios --apply` puts it back**, so `emulatorLauncher` hands each
the file from `bios/` without anything RomMBat writes. Mesen copies it into
`emulators/mesen/Firmware/gba_bios.bin` on first use. So a gap report for `gba` is right for three
rows and overstated for seven, and the report cannot tell which because RomMBat does not read
`<system>.emulator`. Finding 285.

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
chip, flushed at boot. That is freegosy finding F20 on a second system, and the reason a first
save seen with no baseline is not evidence of play. All of them were moved to
`R:\rommbat-evidence\gba\` before any flush could send them. Finding 287.

**Three rows share the loose `<rom>.sav`**: `mgba` and `mesen` name it after the ROM, and
`mednafen` opens the plain name when it exists (finding 273). **Two keep the clock in an `.rtc`
beside the save**, `jgenesis` and `mesen`, which is a second file per save that no rule carries
today. **BizHawk's 16 extra bytes** are likely its RTC, inside the one file. **`libretro`/
`mednafen_gba` names its save after the zip and the file inside it**, `#` included, which no
`libretro` rule matches. Finding 288. Each needs a rule before its row can pass step 4, as the
`nes` and `megadrive` rows did.

## `libretro`/`mgba`

|              |                                                                   |
| ------------ | ----------------------------------------------------------------- |
| Selected by  | **Nothing: RetroBat's default**                                   |
| Confirmed by | `-system gba -emulator libretro -core mgba` on the ES launch line |
| Result       | **Certified**                                                     |

| #   | Result                                                                                 |
| --- | -------------------------------------------------------------------------------------- |
| 4   | **Pass, both directions.** Class A, loose `<rom>.srm`, 131,072 B, `7d9fc2a6...`        |
| 5   | **Pass**, two slots, screenshot byte-checked                                           |
| 6   | **N/A.** `gba` is class A and the row wrote nothing but the game's own save            |
| 7   | **Pass.** Launched from ES after the sync, box art and description in `gamelist.xml`   |
| 8   | **Pass.** 12:08:58Z to 12:12:45Z, 3m 46s, rom 233631                                   |
| 9   | **Pass.** 201 present and verified, 759 media present, gamelist byte-identical, 0 sent |

**The maintainer played from the intro to Emerald's first save** and made two states, then left
ES; the detached `quit` pass exited 0 and `saves` showed the `.srm` and both states in step as
`libretro:battery`, `libretro:mgba:1` and `libretro:mgba:2`. **The save is real**, mixed bytes
against the boot write's uniform `0xFF`.

| Slot | State md5     | Size     | Screenshot md5 |
| ---- | ------------- | -------- | -------------- |
| 1    | `e4ba3d92...` | 11,780 B | `261753dd...`  |
| 2    | `be490aac...` | 28,272 B | `680147fd...`  |

**The declared `<directory>` is where the core wrote**, `saves/gba/libretro.mgba/`. The `.srm`
and slot 2's state and `.png` were moved out of the tree; the preview named the screenshot it
would bring back, and `saves restore 233631 --apply` answered `restored 1 save(s) and 1 state(s),
failed 0, 160 KB, with 1 screenshot(s)`, exit 0, **every file at its own md5**. The preview also
listed an older `autosave` row on the server, save 341 from 2026-09-19, written by another
client, and left it alone.

## `libretro`/`gpsp`

|              |                                                                   |
| ------------ | ----------------------------------------------------------------- |
| Selected by  | `gba.core = gpsp`, set with ES closed and removed afterwards      |
| Confirmed by | `-system gba -emulator libretro -core gpsp` on the ES launch line |
| Result       | **Certified**                                                     |

| #   | Result                                                                              |
| --- | ----------------------------------------------------------------------------------- |
| 4   | **Pass, both directions.** The shared `.srm`, 131,072 B, `bd917a0a...`              |
| 5   | **Pass**, two slots, screenshot byte-checked                                        |
| 6   | **N/A**                                                                             |
| 7   | **Pass**, carried                                                                   |
| 8   | **Pass.** 12:17:46Z to 12:19:48Z, 2m 1s, rom 233631                                 |
| 9   | **Pass.** Nothing to do, gamelist byte-identical, 55 states already in step, 0 sent |

**gpsp loaded the save mGBA made**, and the maintainer saved again in the game. The new `.srm`
differs from mGBA's in 62,543 bytes at the same size, which fits Emerald writing each save to
the other of its two save blocks, and went up as a new version of `libretro:battery`: the restore
preview listed it as the newest of three server saves, mGBA's save 390 and the older `autosave`
below it. So the two cores share one save, as the `megadrive` cores do (finding 277).

| Slot | State md5     | Size     | Screenshot md5 |
| ---- | ------------- | -------- | -------------- |
| 1    | `bd29de09...` | 28,230 B | `9dc85359...`  |
| 2    | `a8ca1732...` | 29,182 B | `5fc2d420...`  |

**The declared `<directory>` is where the core wrote**, `saves/gba/libretro.gpsp/`, beside mGBA's
states rather than over them. The `.srm` and slot 2's state and `.png` went out of the tree and
came back through `saves restore 233631 --apply`, `with 1 screenshot(s)`, exit 0, **every file at
its own md5**.

## What is left

| Row                         | Needs before steps 4 and 5                                      |
| --------------------------- | --------------------------------------------------------------- |
| `libretro`/`mednafen_gba`   | A rule for its `.zip#...md5.sav` name                           |
| `mgba`, `mednafen`, `mesen` | Rules for the shared loose `.sav`, and a decision on the `.rtc` |
| `bizhawk`/`mGBA`            | Its battery rule scoped to `gba`; states are declared           |
| `jgenesis`                  | A rule for `jgenesis/gba/`, and the `.rtc`; states are declared |
| `ares`                      | A rule and a supplement entry, from a real save                 |
| `nosgba`                    | Nothing will help on a zipped library (finding 286)             |

`mgba`, `nosgba`, `mednafen`, `ares` and `mesen` declare no state directory, so each needs a
real state found in its own tree before step 5 means anything.
