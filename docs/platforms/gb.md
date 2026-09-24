# gb

Nintendo Game Boy. RetroBat calls the folder `gb`, which is what this file is named after.

**All fourteen rows `gb` declares are certified**, at RomM `5.3.0` and RetroBat 8.2.1 on
2026-09-22, all nine steps with step 6 N/A because `gb` has no class D:

- `libretro`/`gambatte`, **the row a stock install gives a user**, selected with no override
- `libretro`/`mesen-s`, `bsnes`, `tgbdual`, `DoubleCherryGB` and `sameboy`
- `mesen`, `mgba`/`mgba`, `mednafen`/`gb`, `ares`/`GameBoy`, `bizhawk`/`Gambatte`, `GBHawk` and
  `SameBoy`, and `jgenesis`

**It certifies those fourteen rows and nothing wider.** The six `libretro` rows needed nothing new.
The eight standalone rows needed a battery rule each for `gb`, where `mesen`'s was already
`libretro`'s, and four of them a state declaration in RomMBat's bundled supplement. Two rows read
firmware RetroBat's list files under other systems, which `bios gb` now fetches.

**This file is in seven parts.** Steps 1, 2 and 3, which are the system's, with which rows need
firmware. What the first boots wrote. The six `libretro` rows, then the eight standalone ones. Then
Pokemon Silver, brought in for a clock cartridge. Last, what the pass turned up that is not a row,
and the upstream reports it drafted.

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
| 6   | N/A         | Unchanged: `gb` has no class D                                                                                    |
| 7   | Carried     | The game list's query is unchanged; the Player changes are gamepad focus in RomM's own web UI                     |
| 8   | Carried     | `play_sessions.py` is byte-identical, and its handler changed only in how it counts rows                          |
| 9   | **Touched** | Always touched on a move. **Pass**, see below                                                                     |

**Both were re-run on a deploy of the adoption branch**, made by `tools/publish.ps1 -Deploy`, with
`status` reading the server as `5.3.1`, Supported. `platforms list` was identical either side of
the deploy, with `gb` resolved by `fs_slug` as before. `sync` answered `nothing to do: 107
games already present, 0 downloaded, 0 written` for `Spinnich's Game Boy Favorites`, and `gamelists: all 8 unchanged`,
with every `gamelist.xml` md5'd either side and identical, and a `flush` moved no save or state.
One re-sync covers every row, because none of them owns anything a sync touches that another does
not.

## The install this was measured on

|           |                                                                                     |
| --------- | ----------------------------------------------------------------------------------- |
| RetroBat  | `8.2.1-stable-win64`, the supported floor                                           |
| RomM      | `5.3.0`, the floor then, read back by `status` as Supported                         |
| Root      | `R:\RetroBat`, found by walking up from the executable                              |
| Store     | schema 16 of 16, WAL                                                                |
| Client    | two deploys, named below: `main` at `b150ff2`, then this branch                     |
| Budget    | `none`, as for `nes`, `megadrive` and `gba`                                         |
| Test game | Pokemon - Yellow Version - Special Pikachu Edition (USA, Europe) (CGB+SGB Enhanced) |

**The test game is a 1 MB MBC5 cartridge with 32 KB of battery RAM and no clock**, header cartridge
type `0x1B`, with the Color flag `0x80` and the SGB flag set. Its save is written the moment the
player picks Save from the menu in the first room. It carries no `<emulator>` pin in
`gamelist.xml`, and is RomM rom 153392.

**The client was deployed twice, and which build a result was taken on is named.** The six
`libretro` rows were certified on `main` at `b150ff2`, which carries no `gb` rules. The eight
standalone rows were driven on it too, their saves and states sitting on disk unsynced, and
certified on a deploy of this branch, whose first flush sent **4 saves and 9 states**.

**Every row after the first was seeded from the save the one before made**, as on `gba`. The six
`libretro` cores and Mesen share one file and needed no copying. For the others the seed was put
where each emulator looks before its launch, and the maintainer then saved in the game, so every
save measured below is one that emulator wrote. Every row offered Continue on the seed.

**The maintainer played over RDP, and the agent drove the second state on the standalone rows**
from its session on the RetroBat machine, through `emulatorLauncher` with the row's arguments and
keys sent by `keybd_event`. Those launches skip ES, so they run no hooks and record no session.

## The set

|          |                                             |
| -------- | ------------------------------------------- |
| Name     | Spinnich's Game Boy Favorites               |
| Scope    | `smart_collection 6`                        |
| Policy   | no game cap, no size cap, ordered by recent |
| Resolves | 107 games, 14.1 MB, into `gb`               |

The sync fetched the BIOS first, then 107 ROMs, 414 media files (331.2 MB), and wrote one gamelist
with 107 entries, exit 0.

## Steps 1, 2 and 3, for every row

### 1. Mapping

Resolved at layer **`fs_slug`**: RomM's `fs_slug` for the platform is literally `gb`, which is
already a folder in this install.

| `fs_slug`       | Resolved by | What `platforms list` says                                        |
| --------------- | ----------- | ----------------------------------------------------------------- |
| `gb`            | `fs_slug`   | RomM's fs_slug 'gb' is already a folder in this install           |
| `gb-unofficial` | `bundled`   | the bundled table offers `gb`, `gb2players`, and picked the first |

### 2. Multi-file games and extensions

From the live `es_systems.cfg`:

```text
.gb .zip .7z
```

**107 of 107 resolve and nothing is excluded or unlisted.** Every ROM is a `.zip` holding one `.gb`,
so `gb` has no multi-disc or multi-file shape to settle. `.zip` was observed to launch on all
fourteen rows.

### 3. BIOS

```console
$ rommbat-agent bios gb
6 present
  gb: all 6 present
```

**Six files, all present at the md5s RetroBat names, exit 0.** RetroBat's manifest lists one,
`gb_bios.bin` at `32fbbd84...`. The other five are RomMBat's supplement for `gb`, each copied from
the system RetroBat files it under with RetroBat's md5, because a `gb` row was measured reading it:
the four Super Game Boy files from `sgb` and `gbc_bios.bin` from `gbc`. RetroBat's own Game Boy
wiki page lists `gb_bios.bin` and the four SGB files, and so does this RomM's Game Boy firmware
folder. Nothing is missing from the library and nothing is hashless.

Before the supplement existed, `bios gb --apply` fetched `gb_bios.bin` alone, and `bios sgb
--apply` and `bios gbc --apply` fetched the rest. After it, the five were moved out of the tree and
`bios gb --apply` answered `BIOS: 5 fetched (770.8 KB), 1 present`, every file back at its md5.

## Which rows need firmware

**RetroBat's list names only the mono boot ROM for `gb`, and twelve of fourteen rows need nothing
at all.** Each row was launched on Yellow with no Game Boy family boot ROM anywhere in the tree:

| Row                         | With no boot ROM                                                                   |
| --------------------------- | ---------------------------------------------------------------------------------- |
| `libretro`/`gambatte`       | Boots to the intro, in mono                                                        |
| `libretro`/`mesen-s`        | Boots to the intro                                                                 |
| `libretro`/`bsnes`          | **Refuses**: RetroArch logs `Failed to load content`                               |
| `libretro`/`tgbdual`        | Boots to the intro                                                                 |
| `libretro`/`sameboy`        | Boots, having looked for `bios\dmg_boot.bin` and fallen back to its own boot image |
| `libretro`/`DoubleCherryGB` | Boots to the intro                                                                 |
| `mesen`                     | Boots to the intro                                                                 |
| `mgba`/`mgba`               | Boots to the intro                                                                 |
| `mednafen`/`gb`             | Loads the ROM with no firmware complaint; the fullscreen window did not capture    |
| `ares`/`GameBoy`            | Boots to the intro                                                                 |
| `bizhawk`/`Gambatte`        | Boots to the intro                                                                 |
| `bizhawk`/`GBHawk`          | **Refuses**: "Couldn't find required firmware GBC+World"                           |
| `bizhawk`/`SameBoy`         | Boots to the intro                                                                 |
| `jgenesis`                  | Boots to the intro                                                                 |

**`bsnes` runs a Game Boy cartridge as a Super Game Boy**, so it reads `SGB1.sfc`, the SGB
cartridge, which RetroBat lists under `sgb`. With it, Yellow boots in its SGB border.

**GBHawk reads the boot ROM for the mode it picks, and picks Color for a Color-flagged
cartridge.** With `gb_bios.bin` present and `gbc_bios.bin` not, it still refused Yellow. On Tetris
(World) (Rev 1), which carries neither flag, it booted with `gbc_bios.bin` gone and refused with
"Couldn't find required firmware GB+World" once `gb_bios.bin` went too. BizHawk has a `ConsoleMode`
setting that could force mono, and RetroBat offers no ES option for it, so a stock install needs
the Color boot ROM for every Color-flagged cartridge in the `gb` folder. Two of the 107 carry the
flag.

**By the maintainer's ruling, `bios gb` fetches all five**, through a supplement in
`tools/build-bios-manifest.py` that copies each entry from RetroBat's own list for its system and
fails the build if RetroBat drops it there or adds it to `gb` itself. Finding 293.

## What the boot launches wrote

**Not steps 4 or 5.** No one saved in any of these launches; each ran about 25 to 30 seconds from
`emulatorLauncher` with no key sent. Every file below was moved to `R:\rommbat-evidence\gb\boot\`
before a flush could send it.

| Row                         | Wrote at boot, under `saves/gb/`                         | Content                                          |
| --------------------------- | -------------------------------------------------------- | ------------------------------------------------ |
| `libretro`/`gambatte`       | `<rom>.srm`, 32,768 B                                    | `0xFF` throughout                                |
| `libretro`/`mesen-s`        | `<rom>.srm`, 32,768 B                                    | Random, all 256 byte values                      |
| `libretro`/`bsnes`          | nothing without `SGB1.sfc`; `<rom>.srm` with it          | `0xFF` throughout                                |
| `libretro`/`tgbdual`        | `<rom>.srm` and a 4 B `<rom>.rtc`                        | Mostly `0x00`, with sprite scratch               |
| `libretro`/`sameboy`        | `<rom>.srm` and a 32 B `<rom>.rtc`                       | `0xFF` throughout                                |
| `libretro`/`DoubleCherryGB` | `<rom>.srm` and a 4 B `<rom>.rtc`                        | Mostly `0x00`, with sprite scratch               |
| `mesen`                     | `<rom>.srm`, 32,768 B                                    | Random                                           |
| `mgba`/`mgba`               | `<rom>.sav`, 32,768 B                                    | `0xFF` throughout                                |
| `mednafen`/`gb`             | `<rom>.d9290db87b1f0a23b89f99ee4469e34b.sav`             | `0xFF` throughout                                |
| `ares`/`GameBoy`            | nothing: it ignored `WM_CLOSE` and was killed            |                                                  |
| `bizhawk`, all three cores  | `bizhawk/Pokemon - Yellow Version (USA, Europe).SaveRAM` | `0xFF` throughout, GBHawk only with its boot ROM |
| `jgenesis`                  | `jgenesis/gb/<rom>.sav`, 32,768 B                        | `0xFF` throughout                                |

**A boot write on `gb` is not recognisably blank.** On `gba` every one was uniform `0xFF`. Here
Mesen randomises uninitialised RAM, and Pokemon's first generation decompresses sprites through
cartridge RAM, so a boot can leave real-looking bytes. Finding 287 already says only a baseline
separates a boot write from a save; `gb` shows a fill test would not even be a heuristic. Finding 294.

## The six `libretro` rows

|                  | Selected by                                 | Confirmed on the ES launch line      |
| ---------------- | ------------------------------------------- | ------------------------------------ |
| `gambatte`       | **Nothing: RetroBat's default**             | `-emulator libretro -core gambatte`  |
| `mesen-s`        | `gb.emulator = libretro`, `.core = mesen-s` | `-core mesen-s -state_slot 3`        |
| `bsnes`          | `gb.core = bsnes`                           | `-core bsnes -state_slot 3`          |
| `tgbdual`        | `gb.core = tgbdual`                         | `-core tgbdual -state_slot 3`        |
| `DoubleCherryGB` | `gb.core = DoubleCherryGB`                  | `-core DoubleCherryGB -state_slot 3` |
| `sameboy`        | `gb.core = sameboy`                         | `-core sameboy -state_slot 3`        |

Each override was set with ES closed, and the `gb` keys were cleared when the pass ended; they then
matched the copy taken before the first, `R:\rommbat-evidence\gb\es_settings.before.cfg`.

| #   | Result on every one of the six                                                                    |
| --- | ------------------------------------------------------------------------------------------------- |
| 4   | **Pass, both directions.** Class A, the shared loose `<rom>.srm`, 32,768 B, as `libretro:battery` |
| 5   | **Pass**, two slots each, the screenshot byte-checked                                             |
| 6   | **N/A.** `gb` is class A                                                                          |
| 7   | **Pass.** Launched from ES after the sync, box art, marquee and description in `gamelist.xml`     |
| 8   | **Pass**, every session read back from RomM by `status`, under `recent:`                          |
| 9   | **Pass.** 107 present and verified, 414 media present, gamelist byte-identical, 0 sent            |

| Core             | Session, UTC         | `.srm` after  | Slot 1 state, png            | Slot 2 state, png            |
| ---------------- | -------------------- | ------------- | ---------------------------- | ---------------------------- |
| `gambatte`       | 18:32:20 to 18:34:01 | `0867d776...` | `ffbbb6d1...`, `aa9c25f7...` | `8cfd1650...`, `658479cd...` |
| `mesen-s`        | 18:37:01 to 18:38:06 | `91d956f9...` | `72ade192...`, `fcbe8508...` | `e26eadd2...`, `bb9939a2...` |
| `bsnes`          | 18:39:32 to 18:40:30 | `96e80b02...` | `59601523...`, `f30b193d...` | `0e2272a1...`, `7b70f46a...` |
| `tgbdual`        | 18:41:52 to 18:42:43 | `96e0dd8c...` | `9448e44b...`, `bf3fbefe...` | `6b41aeee...`, `83715a98...` |
| `DoubleCherryGB` | 18:44:13 to 18:44:56 | `f911a868...` | `9a929864...`, `8522cdaf...` | `63a35a91...`, `a67ed996...` |
| `sameboy`        | 18:46:33 to 18:47:24 | `135981ed...` | `01c417ce...`, `80aa7fe5...` | `073465aa...`, `06e37bed...` |

**The maintainer played the stock row from the intro to Yellow's first save** and made two states,
then left ES; each later core loaded the `.srm` the one before it left, and the maintainer saved
again. **Every row's `.srm` and slot 2's state and `.png` went out of the tree and came back through
`saves restore 153392 --apply`**, `restored 1 save(s) and 1 state(s), failed 0, ... with 1
screenshot(s)`, exit 0, every file at its own md5, and each returned image was that slot's, distinct
from slot 1's. The first preview listed an older save on the server, save 191 from 2026-09-01,
written by another client, and left it alone.

**The declared `<directory>` is where every core wrote**, `saves/gb/libretro.<core>/`. ES passed
`-state_slot 3` from the second row on, and RetroArch wrote slots 1 and 2 regardless, as finding 261
says.

**`bsnes` keeps the save itself.** RetroArch logged `Content loading skipped. Implementation will
load it on its own` and `Skipping SRAM load`, and no SRAM write on exit, yet the core read the seed
and wrote the `.srm` back with the maintainer's save in it.

**Three cores write a `.rtc` beside the `.srm` even for Yellow**, which has no clock; on a clock
cartridge `gambatte` writes one too, and it syncs as `libretro:battery:rtc` (below).

## The eight standalone rows

**All eight certified on 2026-09-22, on the deploy of this branch.** Each was driven first on
`main`, where its save sat on disk unread; the branch's first flush sent **4 saves and 9 states**.

|            | Selected by                                 | Confirmed on the ES launch line     |
| ---------- | ------------------------------------------- | ----------------------------------- |
| `mesen`    | `gb.emulator = mesen`                       | `-emulator mesen`, empty `-core`    |
| `mgba`     | `gb.emulator = mgba`, `.core = mgba`        | `-emulator mgba -core mgba`         |
| `mednafen` | `gb.emulator = mednafen`, `.core = gb`      | `-emulator mednafen -core gb`       |
| `ares`     | `gb.emulator = ares`, `.core = GameBoy`     | `-emulator ares -core GameBoy`      |
| `Gambatte` | `gb.emulator = bizhawk`, `.core = Gambatte` | `-emulator bizhawk -core Gambatte`  |
| `GBHawk`   | `gb.emulator = bizhawk`, `.core = GBHawk`   | `-emulator bizhawk -core GBHawk`    |
| `SameBoy`  | `gb.emulator = bizhawk`, `.core = SameBoy`  | `-emulator bizhawk -core SameBoy`   |
| `jgenesis` | `gb.emulator = jgenesis`                    | `-emulator jgenesis`, empty `-core` |

### Checklist for the eight

| #   | Result on every one of the eight                                                            |
| --- | ------------------------------------------------------------------------------------------- |
| 4   | **Pass, both directions**, every file at its own md5 after one restore                      |
| 5   | **Pass**, two slots each, where each emulator was found to write                            |
| 6   | **N/A**                                                                                     |
| 7   | **Pass**, carried: each was launched from ES on the synced ROM, art and description present |
| 8   | **Pass**, every session read back from RomM by `status`, under `recent:`                    |
| 9   | **Pass.** 107 present and verified, 414 media present, gamelist byte-identical, 0 sent      |

### 4. Battery saves on the eight

| Row        | File under `saves/gb/`                                   | Slot               | md5 after the session |
| ---------- | -------------------------------------------------------- | ------------------ | --------------------- |
| `mesen`    | `<rom>.srm`                                              | `libretro:battery` | `4a8163a7...`         |
| `mgba`     | `<rom>.sav`                                              | `mgba:battery`     | `e6ab3d49...`         |
| `mednafen` | `<rom>.sav`, mGBA's file                                 | `mgba:battery`     | `88c8ad8d...`         |
| `ares`     | `ares/Game Boy/<rom>.ram`                                | `ares:battery`     | `32467e57...`         |
| `Gambatte` | `bizhawk/Pokemon - Yellow Version (USA, Europe).SaveRAM` | `bizhawk:battery`  | `a79ad54d...`         |
| `GBHawk`   | the same file                                            | `bizhawk:battery`  | `e6c75d11...`         |
| `SameBoy`  | the same file                                            | `bizhawk:battery`  | `311eb5bd...`         |
| `jgenesis` | `jgenesis/gb/<rom>.sav`                                  | `jgenesis:battery` | `f2ccd8db...`         |

Every file is 32,768 B, the cartridge's RAM with nothing appended. **Mesen writes the loose `.srm`
the `libretro` cores share**, so it needed no rule and uploads as `libretro:battery`; its round trip
was run on the file it left, `4a8163a7...`. **mednafen read and saved into mGBA's plain `.sav`**
rather than its hashed name, which it writes only when no plain one is there (finding 273), so the
two share `mgba:battery` as on `gba`. **BizHawk's three cores share one file named after BizHawk's
own title**, `Pokemon - Yellow Version (USA, Europe)`, where on `gba` its title matched the ROM file;
the state sidecar says so, and the `.SaveRAM.bak` beside it is BizHawk's copy of the previous save.

The four saves with their own rule and one state from each of the eight rows went out of the tree
and came back through one `saves restore 153392 --apply`: `restored 4 save(s) and 8 state(s),
failed 0, 528.1 KB`, exit 0, **all twelve files at their own md5**. Mesen's `.srm` went out and came
back the same way on its own.

### 5. States on the eight

| Row        | Directory under `saves/gb/` | First                       | Second                      | Keys                     |
| ---------- | --------------------------- | --------------------------- | --------------------------- | ------------------------ |
| `mesen`    | `mesen/SaveStates/`         | `_1.mss`, `49020cd0...`     | `_2.mss`, `c3d8e4fd...`     | `Shift+F1`, `Shift+F2`   |
| `mgba`     | `mgba/sstates/`             | `.ss1`, `18efd6db...`       | `.ss3`, `b272934d...`       | the maintainer's         |
| `mednafen` | `mednafen/sstates/`         | `.<md5>.mc0`, `8512ae63...` | `.<md5>.mc2`, `f274dc98...` | `F2`, then `F7` and `F2` |
| `ares`     | `ares/Game Boy/`            | `.bs1`, `cbad7ff0...`       | `.bs2`, `68f58647...`       | `F2`, then `F7` and `F2` |
| `Gambatte` | `bizhawk/sstates/Gambatte/` | `QuickSave3`, `5d78e1e7...` | `QuickSave2`, `fb977adf...` | the pad, then `Ctrl+F2`  |
| `GBHawk`   | `bizhawk/sstates/GBHawk/`   | `QuickSave4`, `d406e432...` | `QuickSave2`, `ef8d2c87...` | the pad, then `Ctrl+F2`  |
| `SameBoy`  | `bizhawk/sstates/SameBoy/`  | `QuickSave5`, `7f9dc209...` | `QuickSave2`, `87cbce88...` | the pad, then `Ctrl+F2`  |
| `jgenesis` | `jgenesis/states/`          | `_0.jst`, `0dd85f35...`     | `_1.jst`, `fa8be187...`     | the pad, then `F7`, `F2` |

**`mesen`, `mgba`, `mednafen` and `ares` declare no state directory**, and each wrote to the one
above, which the supplement now declares for `gb`: mGBA where RetroBat's `config.ini` sets
`savestatePath`, the other three as on `nes`, `megadrive` and `gba`. **ares names its directory
after its own name for the console**, `Game Boy`, beside `gba`'s `Game Boy Advance`. The md5 in
mednafen's names is of the whole `.gb` inside the zip, `d9290db8...`.

**BizHawk's pad key followed ES's `-state_slot`, which moved on each launch**, 3, 4 and 5 across the
three cores, as states accumulated; `Ctrl+F2` then wrote slot 2. BizHawk's frame is inside the state,
and the two slots' `Framebuffer.bmp` differ on every core. `jgenesis` and `bizhawk` write in their
own trees and are mirrored into `saves/` with a `.txt` sidecar. Only the `libretro` rows write a
screenshot `.png`; the rest have nothing to carry, and the preview says so.

### 8. Sessions

| Row        | Journal, UTC         | Length |
| ---------- | -------------------- | ------ |
| `mesen`    | 18:51:45 to 18:52:43 | 57s    |
| `mgba`     | 18:54:47 to 18:56:14 | 1m 27s |
| `mednafen` | 18:57:23 to 18:58:38 | 1m 15s |
| `ares`     | 18:59:47 to 19:00:47 | 59s    |
| `Gambatte` | 19:02:47 to 19:03:46 | 59s    |
| `GBHawk`   | 19:06:05 to 19:06:53 | 47s    |
| `SameBoy`  | 19:08:11 to 19:09:08 | 57s    |
| `jgenesis` | 19:10:23 to 19:11:24 | 1m 1s  |

Every pair is correlated in the journal, every `quit` pass exited 0, and every one is on the server,
rom 153392, as are the six `libretro` sessions above.

## A cartridge with a clock: Pokemon Silver on `gb`

**No game in the set has a clock, so one was brought in.** Pokemon - Silver Version (USA, Europe)
(SGB Enhanced) (GB Compatible), RomM rom 275002, header type `0x10` (MBC3 with timer, RAM and
battery) and 32 KB of RAM, is filed under `gbc` in RomM. A filter set with `--folder gb`, `gb clock
probe`, resolved to it alone and synced it into `roms/gb`, the `.gbc` inside a zip. It was booted
once under every row with nothing saved, and every file it wrote was moved out of the tree:

| Row                                                         | Where the clock went                                        |
| ----------------------------------------------------------- | ----------------------------------------------------------- |
| `libretro`/`gambatte`                                       | **`<rom>.rtc`, 8 B**, where Yellow got none                 |
| `libretro`/`sameboy`                                        | `<rom>.rtc`, 32 B                                           |
| `libretro`/`tgbdual`, `DoubleCherryGB`                      | `<rom>.rtc`, 4 B                                            |
| `mesen`                                                     | **`<rom>.rtc`, 13 B**, where Yellow got none                |
| `jgenesis`                                                  | **`jgenesis/gbc/<rom>.rtc`, 38 B**, beside a `.sav`         |
| `libretro`/`bsnes`, `mgba`, `mednafen`, `bizhawk`/`SameBoy` | Inside the save, 32,816 B                                   |
| `bizhawk`/`Gambatte`                                        | Inside the save, 32,790 B                                   |
| `libretro`/`mesen-s`, `bizhawk`/`GBHawk`                    | Nowhere: 32,768 B and no clock file                         |
| `ares`                                                      | `ares/Game Boy/<rom>.rtc`, 13 B, measured by the `gbc` pass |

**The stock row keeps the clock only in the `.rtc`**, so by the maintainer's ruling the loose `.rtc`
is a save on `gb`: `libretro:battery:rtc`, class B beside the `.srm`, a file the `libretro` cores and
Mesen share as they share the `.srm`. `gambatte`'s 8 B is the Unix time at which the game clock
reads zero.

**Driven on the stock row, and the clock round-trips.** The maintainer started a new game, set the
clock, saved and made a state. The `quit` pass sent the `.srm` as `libretro:battery` and the `.rtc`
as `libretro:battery:rtc`, and the session, 19:55:42Z to 19:57:42Z, is on the server. The `.srm`,
`.rtc`, state and screenshot went out of the tree and came back through `saves restore 275002
--apply`, `restored 2 save(s) and 1 state(s), failed 0, 39.3 KB, with 1 screenshot(s)`, each at its
own md5. Relaunched, the game showed the clock about five minutes on, the time since the save, and
Continue loaded it. That launch saved nothing, left the `.rtc` unchanged and changed the `.srm` in
335 bytes, 334 of them in the first 8 KB bank, which Pokemon uses as scratch, so it went up as a new
version. Both sets then re-synced as a clean no-op. Finding 298.

**`jgenesis` names its directory from the file inside the zip**: `jgenesis/gbc/` for Silver's
`.gbc`, `jgenesis/gb/` for Yellow's `.gb`. The `gb` rule reads `jgenesis/gb/` only, and the loader
refuses a second jgenesis `.sav` rule on one system, since a restore could not tell which directory
a slot belongs in without reading the ROM. By the maintainer's ruling that stays a recorded gap:
such a save is reported and not synced, and the `gbc` pass declares `jgenesis/gbc/` where it is the
normal case. The clock cartridges known here are Color titles, so on `gb` a clock lands in that
gap, and `jgenesis/gb/` has not been seen to hold one.

**ares's clock on `gb` is its own slot, `ares:battery:rtc`**, added by the `gbc` pass after a copy of
Silver under `ares`/`GameBoy` wrote `ares/Game Boy/<rom>.rtc` beside the `.ram`; the `.ram` keeps
`ares:battery` (`gbc.md`, finding 302).

## What the pass turned up that is not a row

- **On a cartridge with no clock, three cores' `.rtc` is only the host time.** `tgbdual`,
  `DoubleCherryGB` and `sameboy` write `<rom>.rtc` for every game, 4 B of Unix time for the first two
  and 32 B for `sameboy`, which read the 4 B form without complaint. It is still a save, for the
  clock cartridge above, so on Yellow those cores upload a few bytes of new version per launch.
  Finding 295.
- **The first flush after the sync pulled down a save.** The `start` pass that ran before the first
  launch wrote `Super Mario Land 2 - 6 Golden Coins (USA, Europe) (Rev 2).srm`, 8,192 B, the server's
  save for a game the sync had just placed, written by another client. That is RomMBat's download
  path working, recorded because it is the first file to appear under `saves/gb/` on this install.
- **Mesen rewrites the `.srm` on exit with nothing saved**, byte for byte the same, so a launch
  moves its mtime without changing it, and nothing is sent.
- **ares closes on the pad's hotkey and ignored `WM_CLOSE` here**, which is how the agent's launches
  end it, so those were killed after their states were on disk. The `gbc` pass saw it close on
  `WM_CLOSE` alone, later than the 15 s these launches allowed.
- **A pad `Ctrl+F2` for EmuHawk needs the keys held.** `keybd_event` with 120 ms between press and
  release never reached it; 400 ms did.

## Drafted for upstream, not filed

By the maintainer's ruling these are drafts, for the maintainer to file with RetroBat if they
choose.

**`batocera-systems.json` lists one file for `gb` where the wiki lists five.** RetroBat's Game Boy
wiki page lists `gb_bios.bin`, `sgb_boot.bin`, `sgb2_boot.bin`, `SGB1.sfc` and `SGB2.sfc`, and says
the SGB files are needed by `mesen-s`. The manifest `emulatorLauncher` ships lists only
`gb_bios.bin` under `gb`, and the other four under `sgb`. Measured on 8.2.1, `libretro`/`bsnes`
cannot load a `.gb` without `SGB1.sfc`, while `mesen-s` boots without any of them.

**GBHawk needs the Color boot ROM for Color-flagged `gb` games, and nothing lists or configures
it.** `bizhawk`/`GBHawk` runs a cartridge whose header flags Color support in Color mode and refuses
it with "Couldn't find required firmware GBC+World" unless `gbc_bios.bin` is in `bios/`.
`gbc_bios.bin` is listed under `gbc` only, and `es_features.cfg` offers no option for BizHawk's
`ConsoleMode` on GBHawk, which would let such a game run in mono on `gb_bios.bin`.

## What this file will not claim

- **Nothing about `gb` under any build but these.** Every row was measured on RetroBat 8.2.1 and
  RomM `5.3.0`.
- **Nothing about another game.** Yellow is one MBC5 cartridge with 32 KB of RAM and no clock. A
  game with an MBC2's 512 half-bytes, or MBC3 with a clock, may be sized and named differently.
- **Nothing about a clock cartridge's clock on any row but the stock one.** Silver was booted under
  all fourteen and driven only under `libretro`/`gambatte`. Under `jgenesis` its saves sit in
  `jgenesis/gbc/`, which the `gb` rule does not read, so they are reported and not synced.
- **Nothing about GBHawk on the two Color-flagged games without `gbc_bios.bin`.** It refuses them,
  and `bios gb` now fetches the file.
- **Nothing about headers beyond the 107.** RomM holds 1,776 `gb` games; only the set's headers and
  the catalog's Pokemon titles were read.
