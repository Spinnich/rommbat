# gba

Nintendo Game Boy Advance. RetroBat calls the folder `gba`, which is what this file is named
after.

**Nine of the ten rows `gba` declares are certified**, at RomM `5.3.0` and RetroBat 8.2.1 on
2026-09-22, all nine steps with step 6 N/A because `gba` has no class D:

- `libretro`/`mgba`, **the row a stock install gives a user**, selected with no override
- `libretro`/`gpsp` and `libretro`/`mednafen_gba`
- `mgba`/`mgba`, `mednafen`/`gba`, `mesen`, `bizhawk`/`mGBA`, `jgenesis` and
  `ares`/`GameBoyAdvance`

**One is driven and not certified.** `nosgba` cannot open a zip, because NO$GBA unzips through an
external `PKUNZIP.EXE` it does not ship. It loads a `.gba` placed beside the zip instead, then
deletes it as its own temp file. It also keeps its saves compressed in `emulators/nosgba/BATTERY/`,
outside `saves/` (finding 286).

**It certifies those nine rows and nothing wider.** The two `libretro` rows that share the `.srm`
needed nothing new. The other seven needed a battery rule each, four a state declaration in
RomMBat's bundled supplement, and one of them a new way of naming a save, all scoped to `gba`.

**This file is in six parts.** Steps 1, 2 and 3, which are the system's, with which rows need the
BIOS. What the first boots wrote. The two `libretro` rows that needed no code, then the seven that
did, then `nosgba`. Last, what the pass turned up that is not a row.

## The move to `5.3.1`

**It touches steps 1 and 9, and both were re-run on 2026-09-24.** Mapped from finding 14 of
`docs/romm-5.3-findings.md`, 161 upstream commits with no schema change, and from this repo's
`src/` and `data/` diff across the move, which is the two version constants and
`PlatformMapStore.Record`'s case-only rekey. It applies to every row alike, because nothing a
single row exercises moved.

| #   | At `5.3.1`  | Why                                                                                                               |
| --- | ----------- | ----------------------------------------------------------------------------------------------------------------- |
| 1   | **Touched** | RomM #4676 lets an `fs_slug` change case and `platform_map` now rekeys for it. **Pass**, see below                |
| 2   | Carried     | `GET /api/roms` takes the same parameters, and `roms/files.py` changed only in typing                             |
| 3   | Carried     | `endpoints/firmware.py` is byte-identical, `firmware_handler.py` changed only in typing, and `data/` is unchanged |
| 4   | Carried     | `saves.py` and `sync.py` are byte-identical, and `s4-older-mtime.py` answers all six cases as at `5.3.0`          |
| 5   | Carried     | `states.py` is byte-identical, and `screenshots_handler.py` changed only in a type annotation                     |
| 6   | N/A         | Unchanged: `gba` has no class D                                                                                   |
| 7   | Carried     | The game list's query is unchanged; the Player changes are gamepad focus in RomM's own web UI                     |
| 8   | Carried     | `play_sessions.py` is byte-identical, and its handler changed only in how it counts rows                          |
| 9   | **Touched** | Always touched on a move. **Pass**, see below                                                                     |

**Both were re-run on a deploy of the adoption branch**, made by `tools/publish.ps1 -Deploy`, with
`status` reading the server as `5.3.1`, Supported. `platforms list` was identical either side of
the deploy, with `gba` resolved by `fs_slug` as before. `sync` answered `nothing to do: 201
games already present, 0 downloaded, 0 written` for `Spinnich's Game Boy Advance Favorites`, and `gamelists: all 8 unchanged`,
with every `gamelist.xml` md5'd either side and identical, and a `flush` moved no save or state.
One re-sync covers every row, because none of them owns anything a sync touches that another does
not.

## The install this was measured on

|           |                                                                       |
| --------- | --------------------------------------------------------------------- |
| RetroBat  | `8.2.1-stable-win64`, the supported floor                             |
| RomM      | `5.3.0`, the floor then, read back by `status` as Supported           |
| Root      | `R:\RetroBat`, found by walking up from the executable                |
| Store     | schema 16 of 16, WAL                                                  |
| Client    | two deploys, named below: the `megadrive` build, then this branch     |
| Budget    | `none`, as for `nes` and `megadrive`                                  |
| Test game | Pokemon - Emerald Version (USA, Europe), 16 MB, flash save and an RTC |

**`mgba` and `nosgba` were not on the install at the start.** Their folders held only RetroBat's
template configs. Each was installed by launching Emerald under it and answering ES's install
prompt, with `tools/m0-probes/probe2-install-emulator.ps1`: mGBA 51.9 MB, NO$GBA 0.4 MB. The
other eight rows' emulators were present.

**The client was deployed twice, and which build a result was taken on is named.** The two
`libretro` rows that share the `.srm` were certified on the build `megadrive` was, which carries no
gba rules. The other seven were driven on it too, their saves sitting on disk unsyncable, and
certified on a deploy of this branch, whose first flush sent **9 saves and 8 states**.

**The test game has a real-time clock**, and three emulators keep it in a file beside the save.
Emerald carries no `<emulator>` pin in `gamelist.xml`.

**Every row after the first was seeded from the save the one before made**, by the maintainer's
ruling, rather than played through Emerald's intro again. The seed is the file each emulator
finds when it boots; the maintainer then saved in the game, so every save measured below is one
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
beside the save** here, `jgenesis` and `mesen`, and `ares` a third, measured later. **BizHawk's 16
extra bytes** are the clock inside the one file, as standalone mGBA's turned out to be.
**`libretro`/`mednafen_gba` names its save after the zip and the file inside it**, `#` included,
which no `libretro` rule matched. Finding 288. Each needed a rule before its row could pass step 4,
as the `nes` and `megadrive` rows did.

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

## The seven rows that needed code

**All seven certified on 2026-09-22, on the deploy of this branch.** Each was driven first on the
`megadrive` build, where its save sat on disk unread; the branch's first flush sent **9 saves and
8 states**, everything those sessions had left unsyncable.

|              | Selected by                                       | Confirmed on the ES launch line         |
| ------------ | ------------------------------------------------- | --------------------------------------- |
| `mgba`       | `gba.emulator = mgba`, `.core = mgba`             | `-emulator mgba -core mgba`             |
| `mednafen`   | `gba.emulator = mednafen`, `.core = gba`          | `-emulator mednafen -core gba`          |
| `mesen`      | `gba.emulator = mesen`                            | `-emulator mesen`, empty `-core`        |
| `bizhawk`    | `gba.emulator = bizhawk`, `.core = mGBA`          | `-emulator bizhawk -core mGBA`          |
| `jgenesis`   | `gba.emulator = jgenesis`                         | `-emulator jgenesis`                    |
| mednafen_gba | `gba.emulator = libretro`, `.core = mednafen_gba` | `-emulator libretro -core mednafen_gba` |
| `ares`       | `gba.emulator = ares`, `.core = GameBoyAdvance`   | `-emulator ares -core GameBoyAdvance`   |

Each override was set with ES closed, and `es_settings.cfg` was put back from the copy taken
before the first, `R:\rommbat-evidence\gba\es_settings.before.cfg`, when the pass ended.

### Checklist for the seven

| #   | Result on every one of the seven                                                            |
| --- | ------------------------------------------------------------------------------------------- |
| 4   | **Pass, both directions**, every file at its own md5 after one restore                      |
| 5   | **Pass**, two slots each, where each emulator was found to write                            |
| 6   | **N/A**                                                                                     |
| 7   | **Pass**, carried: each was launched from ES on the synced ROM, art and description present |
| 8   | **Pass**, every session read back from RomM by `status`, under `recent:`                    |
| 9   | **Pass.** 201 present and verified, 759 media present, gamelist byte-identical, 0 sent      |

### 4. Battery saves on the seven

| Row          | File under `saves/gba/`                     | Size          | Slot                                | md5                          |
| ------------ | ------------------------------------------- | ------------- | ----------------------------------- | ---------------------------- |
| `mgba`       | `<rom>.sav`                                 | 131,088 B     | `mgba:battery`                      | `4444c841...`                |
| `mednafen`   | `<rom>.605b89b6....sav`                     | 131,072 B     | `mednafen:battery`                  | `c1345454...`                |
| `mesen`      | `<rom>.sav` and `<rom>.rtc`                 | 131,072, 19 B | `mgba:battery`, `mesen:battery:rtc` | `a7b096dc...`, `da65e7fe...` |
| `bizhawk`    | `bizhawk/<rom>.SaveRAM`                     | 131,088 B     | `bizhawk:battery`                   | `2e7801cb...`                |
| `jgenesis`   | `jgenesis/gba/<rom>.sav` and `.rtc`         | 131,072, 59 B | `jgenesis:battery:sav`, `:rtc`      | `61d7e3cf...`, `2a2ed802...` |
| mednafen_gba | `<rom>.zip#<rom>.605b89b6....sav`           | 131,072 B     | `libretro:battery:sav`              | `8d1ecd17...`                |
| `ares`       | `ares/Game Boy Advance/<rom>.flash`, `.rtc` | 131,072, 18 B | `ares:battery:flash`, `:rtc`        | `5db8042f...`, `7ba2e8d4...` |

All nine files went out of the tree with one state per row, and came back through one `saves
restore 233631 --apply`: `restored 9 save(s) and 7 state(s), failed 0, 1.5 MB, with 1
screenshot(s)`, exit 0, **all seventeen files at their own md5**. The preview named every path,
including the zip-member name rebuilt from the ROM and the hashed one.

**The loose `<rom>.sav` is one save for three emulators and uploads as `mgba:battery`**, by the
maintainer's ruling, as `nes` uploads its shared plain `.sav` as `mesen:battery`. Mesen's copy went
up first as that slot; mGBA's own, put back afterwards, went up as its next version, and the
restore brought back the newest, mGBA's. **Mesen reads mGBA's 131,088 B file**: launched on it,
Emerald continued from the save with no clock message, and on exit Mesen wrote the file back at
131,072 B, mGBA's flash byte for byte with the 16-byte footer dropped, `505a9dd2...`, and its own
`.rtc` beside it. That went up as the next `mgba:battery` version through the `quit` pass, with
nothing saved in the game. mednafen does not read mGBA's file: it refused it (below).

**mGBA standalone and BizHawk append 16 bytes of clock to the flash**, 131,088 B against 131,072.
Both read the 131,072 B seed and wrote their own size back. Mesen, jgenesis and ares keep the clock
in an `.rtc` instead, and each `.rtc` is its own class B slot, so the clock travels with the save.

**mednafen refused mGBA's file and cannot be given its own while it is there.** The first
mednafen session found the loose `.sav` mGBA had written and logged `Save game memory file ... is
an incorrect size(131088 bytes). The correct size is 65536 or 131072 bytes.`, and the game did not
load. mednafen opens the plain name whenever it exists (finding 273), so on a device where mGBA
standalone has run, mednafen cannot play Emerald until that file moves. The session that passed
ran with mGBA's file out of the tree and the 131,072 B seed under mednafen's hashed name. **The
game then said its internal battery had run dry**: mednafen does not carry the cartridge clock
through a save with none in it, where Mesen, seeded the same way, did not complain. Finding 289.

**mednafen_gba ignores the `.srm` its two sibling cores share.** RetroArch logged `Redirecting save
file to ...srm` and then `Skipping SRAM load`, and the core kept its own file under mednafen's name
for RetroArch's `archive#member` path. So it cannot share `libretro:battery`, and by the
maintainer's ruling it takes `libretro:battery:sav`, class B's per-extension slot. A restore names
the file from the zip's one member and that member's md5. **The other two formats were driven
afterwards**, each booted once from `emulatorLauncher` with the real saves moved aside: a `.7z`
built with RetroBat's own `7za.exe` gave `<rom>.7z#<rom>.605b89b6....sav`, the same form and md5,
which the rule claims; a bare `.gba` gave `<rom>.605b89b6....sav`, **mednafen standalone's own
name**, hashed although a plain `<rom>.sav` was present, so on a library of bare `.gba` files the
two mednafens share one file and it uploads as `mednafen:battery`. A restore of
`libretro:battery:sav` reads the member out of a zip only, so for a `.7z` it is refused as
unnameable until RomMBat can read inside one (#221). Both boot writes were blank and were moved out of the tree before any flush. Finding 290.

### 5. States on the seven

| Row          | Directory under `saves/gba/` | First                       | Second                      | Keys                     |
| ------------ | ---------------------------- | --------------------------- | --------------------------- | ------------------------ |
| `mgba`       | `mgba/sstates/`              | `.ss1`, `42c63fd6...`       | `.ss2`, `575a3683...`       | `Shift+F1`, `Shift+F2`   |
| `mednafen`   | `mednafen/sstates/`          | `.<md5>.mc0`, `39944e7f...` | `.<md5>.mc1`, `8830b0d3...` | `F2`, then `F7` and `F2` |
| `mesen`      | `mesen/SaveStates/`          | `_1.mss`, `e9d655e0...`     | `_2.mss`, `bf668503...`     | `Shift+F1`, `Shift+F2`   |
| `bizhawk`    | `bizhawk/sstates/mGBA/`      | `QuickSave3`, `d683652e...` | `QuickSave2`, `e4ab8604...` | the pad, then `Ctrl+F2`  |
| `jgenesis`   | `jgenesis/states/`           | `_0.jst`, `574f9b9f...`     | `_1.jst`, `2b860589...`     | the pad, then `F7`, `F2` |
| mednafen_gba | `libretro.mednafen_gba/`     | `state1`, `ecf90f36...`     | `state2`, `2da3ac7f...`     | the pad, twice           |
| `ares`       | `ares/Game Boy Advance/`     | `.bs1`, `73f17f68...`       | `.bs2`, `6d7b0d0f...`       | `F2`, then `F7` and `F2` |

**`mgba`, `mednafen`, `mesen` and `ares` declare no state directory**, and each wrote to the one
above, which the supplement now declares for `gba`: mGBA where RetroBat's `config.ini` sets
`savestatePath`, the other three as on `nes` and `megadrive`. `bizhawk` took ES's `-state_slot 3`
and the others did not (findings 269 and 275). `jgenesis` and `bizhawk` wrote in their own trees
and were mirrored into `saves/` with a `.txt` sidecar, which for BizHawk reads `Pokemon - Emerald
Version (USA, Europe).mGBA`: here it names the game after the ROM file, having no title of its own.

**Only mednafen_gba writes a screenshot**, `29beb94b...` for slot 1 and `5a2bc558...` for slot 2,
and the restore brought slot 2's back at its own md5. BizHawk's frame is inside the state,
`2ed26d0d...` and `70009970...` for its two slots. The rest have nothing to carry, and the preview
says so. ares states are a fixed 530,840 B, so only the md5 tells two apart, and ares again ignored
`WM_CLOSE` and was killed after its states were on disk.

### 8. Sessions

| Row          | Journal, UTC         | Length |
| ------------ | -------------------- | ------ |
| `mgba`       | 12:26:25 to 12:27:42 | 1m 17s |
| `mednafen`   | 12:33:42 to 12:35:53 | 2m 11s |
| `mesen`      | 12:39:37 to 12:40:50 | 1m 13s |
| `bizhawk`    | 12:42:55 to 12:44:40 | 1m 45s |
| `jgenesis`   | 12:46:48 to 12:47:56 | 1m 8s  |
| mednafen_gba | 12:49:46 to 12:50:39 | 53s    |
| `ares`       | 12:51:39 to 12:52:43 | 1m 4s  |

Every pair is correlated in the journal, every `quit` pass exited 0, and **every one is on the
server**: `status` lists the ten newest sessions under `recent:`, a line this pass added because it
printed only the newest, and the seven appear there at these times, rom 233631, alongside the
`nosgba` session at 12:58:48Z and the refused mednafen attempt at 12:30:51Z. One more, 13:32:28Z to
13:32:53Z, is the maintainer launching NO$GBA from RetroBat's own emulator menu to look for its
state key; that menu writes `gba.emulator` into `es_settings.cfg`, which is why a `nosgba` key
reappeared there after the file was put back.

## `nosgba`: driven, and not certifiable

|              |                                                                   |
| ------------ | ----------------------------------------------------------------- |
| Selected by  | `gba.emulator = nosgba`                                           |
| Confirmed by | `no$gba.exe /f "<rom>.zip"` on the ES launch line                 |
| Result       | **Not certifiable on this install**: steps 2, 4 and 5 cannot pass |

**NO$GBA reads a zipped ROM only through the bare `.gba` beside it.** Launched on the zip alone it
shows "Cartridge not found"; with `<rom>.gba` beside the zip the same launch boots. By the
maintainer's ruling that `.gba` was placed by hand, and the maintainer's ES session loaded the save
and played. **NO$GBA then deletes that `.gba` itself**: present before each of two launches and
gone after, so every launch after the first fails again. It unzips by running an external
`PKUNZIP.EXE` (its loader prints "LOADING PKUNZIP..."), which its folder does not hold, so it takes
a `.gba` named like the zip for PKUNZIP's output and deletes it as a temp file. Run by hand with
`emulatorLauncher` not involved, it deleted the file while still running. Handed a bare `.gba`
path, it keeps the file. `NosGbaGenerator` passes the zip through and never extracts it. A sync
cannot make this row work, since RomMBat places the ROM RomM serves and never the file inside it.
Reported upstream as
[emulatorlauncher#1377](https://github.com/RetroBat-Official/emulatorlauncher/issues/1377).

**Its saves live outside `saves/`**, in `emulators/nosgba/BATTERY/<rom>.SAV`, as Kega Fusion's do
on `megadrive` (finding 283). NO$GBA read the raw 131,072 B seed and wrote it back in its own
compressed format, 3,583 B headed `NocashGbaBackup`, and rewrote it on a later launch with no save
made. **No state can be synced**: `F8` is NO$GBA's Write Snapshot, and it opens a Save As dialog
in the user's `Documents` folder rather than writing anywhere fixed, so where a state lands is the
user's choice each time and no rule can find it. The dialog was cancelled. There is no pad mapping
for it either. **Its pad maps Start and Select differently from every other
row**, as the maintainer found: `NO$GBA.INI` numbers them 3 and 4, and `emulatorLauncher` writes no
mapping for it. Finding 286.

## What the pass turned up that is not a row

- **Clock files change on every launch.** Mesen's `.rtc`, jgenesis's `.rtc` and BizHawk's
  `.SaveRAM` all changed on a launch in which nothing was saved, since each records the host time.
  So a session under one of those rows uploads a new version of that slot even when the game was
  not saved, which is small and correct. Finding 291.
- **mednafen keeps rotating backups of a save it loads** in
  `saves/gba/mednafen/backup/<rom>.<md5>/`, `0.sav` to `2.sav` beside a one-byte counter `C.sav`.
  They are not saves, and `saves` lists the four files as `not in this release` under `gba`.
  Finding 292.
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
