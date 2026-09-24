# gbc

Nintendo Game Boy Color. RetroBat calls the folder `gbc`, which is what this file is named after.

**All twelve rows `gbc` declares are certified**, at RomM `5.3.0` and RetroBat 8.2.1 on
2026-09-23, all nine steps with step 6 N/A because `gbc` has no class D:

- `libretro`/`gambatte`, **the row a stock install gives a user**, selected with no override
- `libretro`/`tgbdual`, `sameboy` and `DoubleCherryGB`
- `mesen`, `mgba`/`mgba`, `mednafen`/`gbc`, `ares`/`GameBoyColor`, `bizhawk`/`Gambatte`, `GBHawk`
  and `SameBoy`, and `jgenesis`

**It certifies those twelve rows and nothing wider.** The four `libretro` rows needed only the
loose `.rtc` rule widened from `gb`. The eight standalone rows needed a battery rule each for
`gbc`, where `mesen`'s was already `libretro`'s, and four of them a state declaration in RomMBat's
bundled supplement. One row reads firmware, and RetroBat's `gbc` list names it.

**This file is in six parts.** Steps 1, 2 and 3, which are the system's, with which rows need
firmware. What the first boots wrote. The four `libretro` rows, then the eight standalone ones.
Then the cartridge clock, which no change of row that was checked kept. Last, what the pass turned up
that is not a row.

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
| 6   | N/A         | Unchanged: `gbc` has no class D                                                                                   |
| 7   | Carried     | The game list's query is unchanged; the Player changes are gamepad focus in RomM's own web UI                     |
| 8   | Carried     | `play_sessions.py` is byte-identical, and its handler changed only in how it counts rows                          |
| 9   | **Touched** | Always touched on a move. **Pass**, see below                                                                     |

**Both were re-run on a deploy of the adoption branch**, made by `tools/publish.ps1 -Deploy`, with
`status` reading the server as `5.3.1`, Supported. `platforms list` was identical either side of
the deploy, with `gbc` resolved by `fs_slug` as before. `sync` answered `nothing to do: 83
games already present, 0 downloaded, 0 written` for `Spinnich's Game Boy Color Favorites`, and `gamelists: all 8 unchanged`,
with every `gamelist.xml` md5'd either side and identical, and a `flush` moved no save or state.
One re-sync covers every row, because none of them owns anything a sync touches that another does
not.

## The install this was measured on

|           |                                                             |
| --------- | ----------------------------------------------------------- |
| RetroBat  | `8.2.1-stable-win64`, the supported floor                   |
| RomM      | `5.3.0`, the floor then, read back by `status` as Supported |
| Root      | `R:\RetroBat`, found by walking up from the executable      |
| Store     | schema 17 of 17, WAL                                        |
| Client    | this branch, deployed twice, named below                    |
| Budget    | `none`, as for every system before it                       |
| Test game | Pokemon - Crystal Version (USA, Europe) (Rev 1)             |

**The test game is a 2 MB MBC3 cartridge with a clock and 32 KB of battery RAM**, header cartridge
type `0x10` (MBC3 with timer, RAM and battery) and Color flag `0xC0`, so it runs on a Color only.
Its save is written the moment the player picks Save from the menu in the first room. It carries
no `<emulator>` pin in `gamelist.xml`, and is RomM rom 274994.

**The client was deployed twice, and which build a result was taken on is named.** The first
deploy was `main` at `d43f4d6` with the loose `.rtc` rule widened to `gbc`, so the stock row's
clock would sync; the four `libretro` rows were certified on it. The eight standalone rows were
driven on it too, their saves sitting on disk unsynced, and certified on a second deploy carrying
this branch's `gbc` rules, whose first flush sent **6 saves and 10 states**.

**Every row after the first was seeded from the save the one before made**, as on `gba` and `gb`.
The four `libretro` cores and Mesen share one file and needed no copying. For the others the seed
was put where each emulator looks before its launch, and the maintainer then saved in the game, so
every save measured below is one that emulator wrote. Every row offered Continue on its seed.

**The maintainer played over RDP, and the agent drove every state on the standalone rows** from its
session on the RetroBat machine, through `emulatorLauncher` with the row's arguments and keys sent
by `keybd_event`. Those launches skip ES, so they run no hooks and record no session.

## The set

|          |                                             |
| -------- | ------------------------------------------- |
| Name     | Spinnich's Game Boy Color Favorites         |
| Scope    | `smart_collection 19`                       |
| Policy   | no game cap, no size cap, ordered by recent |
| Resolves | 83 games, 49.7 MB, into `gbc`               |

The sync found the BIOS present, then fetched 83 ROMs and 307 media files (273.8 MB), and wrote
one gamelist with 83 entries, exit 0.

## Steps 1, 2 and 3, for every row

### 1. Mapping

Resolved at layer **`fs_slug`**: RomM's `fs_slug` for the platform is literally `gbc`, which is
already a folder in this install.

| `fs_slug`        | Resolved by | What `platforms list` says                                                         |
| ---------------- | ----------- | ---------------------------------------------------------------------------------- |
| `gbc`            | `fs_slug`   | RomM's fs_slug 'gbc' is already a folder in this install                           |
| `gbc-unofficial` | `bundled`   | the bundled table offers `gbc`, `sgb`, `sgb-msu1`, `gbc2players`, picked the first |

### 2. Multi-file games and extensions

From the live `es_systems.cfg`:

```text
.gbc .zip .7z
```

**83 of 83 resolve and nothing is excluded or unlisted.** Every ROM is a `.zip` holding one `.gbc`,
so `gbc` has no multi-disc or multi-file shape to settle. `.zip` was observed to launch on all
twelve rows.

### 3. BIOS

```console
$ rommbat-agent bios gbc
1 present
  gbc: all 1 present
```

**One file, present at the md5 RetroBat names, exit 0.** RetroBat's manifest lists
`gbc_bios.bin` at `dbfce9db...`, and RomMBat adds nothing for `gbc`. Nothing is missing from the
library and nothing is hashless.

With the whole Game Boy family's firmware moved out of the tree (`gb_bios.bin`, `gbc_bios.bin` and
the four Super Game Boy files), `bios gbc` reported `1 to fetch (2.3 KB)` and `bios gbc --apply`
answered `BIOS: 1 fetched (2.3 KB)`, exit 0; `bios gb --apply` brought back the other five. All six
were back at the md5s they left with.

## Which rows need firmware

**One of twelve, and RetroBat's `gbc` list names it.** Each row was launched on Crystal with no
Game Boy family firmware anywhere in the tree:

| Row                        | With no firmware                                         |
| -------------------------- | -------------------------------------------------------- |
| `libretro`, all four cores | Boots to the intro                                       |
| `mesen`                    | Boots to the intro                                       |
| `mgba`/`mgba`              | Boots to the intro                                       |
| `mednafen`/`gbc`           | Boots to the intro                                       |
| `ares`/`GameBoyColor`      | Boots to the intro                                       |
| `bizhawk`/`Gambatte`       | Boots to the intro                                       |
| `bizhawk`/`GBHawk`         | **Refuses**: "Couldn't find required firmware GBC+World" |
| `bizhawk`/`SameBoy`        | Boots to the intro                                       |
| `jgenesis`                 | Boots to the intro                                       |

With `gbc_bios.bin` back, GBHawk boots Crystal to the intro. It is the file `bios gbc` fetches, so
`gbc` needs no supplement, where `gb` needed five files from its siblings (finding 293). Finding 299.

## What the boot launches wrote

**Not steps 4 or 5.** No one saved in any of these launches; each ran about 25 seconds from
`emulatorLauncher` with no key sent. Every file below was moved to `R:\rommbat-evidence\gbc\boot\`
before a flush could send it.

| Row                         | Wrote at boot, under `saves/gbc/`                                           |
| --------------------------- | --------------------------------------------------------------------------- |
| `libretro`/`gambatte`       | `<rom>.srm`, 32,768 B, and `<rom>.rtc`, 8 B                                 |
| `libretro`/`tgbdual`        | `<rom>.srm` and `<rom>.rtc`, 4 B                                            |
| `libretro`/`sameboy`        | `<rom>.srm` and `<rom>.rtc`, 32 B                                           |
| `libretro`/`DoubleCherryGB` | `<rom>.srm` and `<rom>.rtc`, 4 B                                            |
| `mesen`                     | `<rom>.srm` and `<rom>.rtc`, 13 B                                           |
| `mgba`/`mgba`               | `<rom>.sav`, 32,816 B: the RAM and a 48 B clock footer                      |
| `mednafen`/`gbc`            | `<rom>.301899b8087289a6436b0a241fbbb474.sav`, 32,816 B                      |
| `ares`/`GameBoyColor`       | nothing: still running 15 s after `WM_CLOSE`, it was killed                 |
| `bizhawk`/`Gambatte`        | `bizhawk/Pokemon - Crystal Version (USA, Europe) (Rev A).SaveRAM`, 32,790 B |
| `bizhawk`/`GBHawk`          | nothing without `gbc_bios.bin`; the same file, 32,768 B, with it            |
| `bizhawk`/`SameBoy`         | the same file, 32,816 B                                                     |
| `jgenesis`                  | `jgenesis/gbc/<rom>.sav`, 32,768 B, and `<rom>.rtc`, 38 B                   |

**ares writes on exit, so a launch that is killed writes nothing.** A second boot, ended with
`Esc`, its `QuitEmulator` key in `settings.bml`, wrote `ares/Game Boy/<rom>.ram`, 32,768 B, and
`<rom>.rtc`, 13 B, which is how its directory was found before a seed was placed. A later launch
whose `Esc` went to another window closed on `WM_CLOSE` alone and wrote the same pair, so ares
does not always ignore `WM_CLOSE`, as `gb`'s record has it; it can take longer than 15 s.

## The four `libretro` rows

|                  | Selected by                                  | Confirmed on the ES launch line      |
| ---------------- | -------------------------------------------- | ------------------------------------ |
| `gambatte`       | **Nothing: RetroBat's default**              | `-emulator libretro -core gambatte`  |
| `tgbdual`        | `gbc.emulator = libretro`, `.core = tgbdual` | `-core tgbdual -state_slot 3`        |
| `sameboy`        | `gbc.core = sameboy`                         | `-core sameboy -state_slot 3`        |
| `DoubleCherryGB` | `gbc.core = DoubleCherryGB`                  | `-core DoubleCherryGB -state_slot 3` |

Each override was set with ES closed, and the `gbc` keys were cleared when the pass ended. ES
rewrote `es_settings.cfg` on its own exit during the pass, moving `LastSystem` to `gbc` and dropping
`Language`; the copy taken first is `R:\rommbat-evidence\gbc\es_settings.before.cfg`.

| #   | Result on every one of the four                                                                              |
| --- | ------------------------------------------------------------------------------------------------------------ |
| 4   | **Pass, both directions.** Class A `.srm` as `libretro:battery`, beside the `.rtc` as `libretro:battery:rtc` |
| 5   | **Pass**, two slots each, the screenshot byte-checked                                                        |
| 6   | **N/A.** `gbc` is class A                                                                                    |
| 7   | **Pass.** Launched from ES after the sync, box art, marquee and description in `gamelist.xml`                |
| 8   | **Pass**, every session read back from RomM by `status`, under `recent:`                                     |
| 9   | **Pass.** 83 present and verified, 307 media present, gamelist byte-identical, 0 sent                        |

| Core             | Session, UTC         | `.srm` after  | `.rtc` after        | Slot 1 png    | Slot 2 state, png            |
| ---------------- | -------------------- | ------------- | ------------------- | ------------- | ---------------------------- |
| `gambatte`       | 13:05:42 to 13:07:19 | `05dcc927...` | 8 B, `5f8f8410...`  | `b47b53a3...` | `5bc1a3ab...`, `1167691f...` |
| `tgbdual`        | 13:11:11 to 13:12:26 | `0d4fc8e0...` | 4 B, `c8018a2c...`  | `ad1437d6...` | `1a658125...`, `cea4f8c0...` |
| `sameboy`        | 13:18:01 to 13:18:53 | `2b97141d...` | 32 B, `d8ee6dda...` | `522faf4b...` | `6a365769...`, `7d41cea4...` |
| `DoubleCherryGB` | 13:20:09 to 13:20:56 | `688ff125...` | 4 B, `e0719a22...`  | `fe28069f...` | `51c75433...`, `784d10fa...` |

**The maintainer played the stock row from the intro to Crystal's first save**, setting the clock
to about 9 AM, and made two states, then left ES; each later core loaded the `.srm` the one before
it left, and the maintainer saved again. **After each row its `.srm`, `.rtc`, slot 2's state and
slot 2's `.png` went out of the tree and came back through `saves restore 274994 --apply`**,
`restored 2 save(s) and 1 state(s), failed 0, ... with 1 screenshot(s)`, exit 0, every file at its
own md5, and each returned image was that slot's, distinct from slot 1's. From the second row on,
the preview also listed the earlier cores' versions on the server and left them alone.

**The declared `<directory>` is where every core wrote**, `saves/gbc/libretro.<core>/`. ES passed
`-state_slot 3` from the second row on, and RetroArch wrote slots 1 and 2 regardless, as finding 261
says. **Every core writes the `.rtc` on exit as RAM type #1**, beside the `.srm` as type #0, and
the clock it holds is not the same thing on any two of them (below).

## The eight standalone rows

**All eight certified on 2026-09-23, on the second deploy.** Each was driven first on the first,
where its save sat on disk unread; the second's first flush sent **6 saves and 10 states**.

|            | Selected by                                   | Confirmed on the ES launch line     |
| ---------- | --------------------------------------------- | ----------------------------------- |
| `mesen`    | `gbc.emulator = mesen`                        | `-emulator mesen`, empty `-core`    |
| `mgba`     | `gbc.emulator = mgba`, `.core = mgba`         | `-emulator mgba -core mgba`         |
| `mednafen` | `gbc.emulator = mednafen`, `.core = gbc`      | `-emulator mednafen -core gbc`      |
| `ares`     | `gbc.emulator = ares`, `.core = GameBoyColor` | `-emulator ares -core GameBoyColor` |
| `Gambatte` | `gbc.emulator = bizhawk`, `.core = Gambatte`  | `-emulator bizhawk -core Gambatte`  |
| `GBHawk`   | `gbc.emulator = bizhawk`, `.core = GBHawk`    | `-emulator bizhawk -core GBHawk`    |
| `SameBoy`  | `gbc.emulator = bizhawk`, `.core = SameBoy`   | `-emulator bizhawk -core SameBoy`   |
| `jgenesis` | `gbc.emulator = jgenesis`                     | `-emulator jgenesis`, empty `-core` |

### Checklist for the eight

| #   | Result on every one of the eight                                                            |
| --- | ------------------------------------------------------------------------------------------- |
| 4   | **Pass, both directions**, every file at its own md5 after one restore                      |
| 5   | **Pass**, two slots each, where each emulator was found to write                            |
| 6   | **N/A**                                                                                     |
| 7   | **Pass**, carried: each was launched from ES on the synced ROM, art and description present |
| 8   | **Pass**, every session read back from RomM by `status`, under `recent:`                    |
| 9   | **Pass.** 83 present and verified, 307 media present, gamelist byte-identical, 0 sent       |

### 4. Battery saves on the eight

| Row        | Files under `saves/gbc/`                                                    | Slots                                          |
| ---------- | --------------------------------------------------------------------------- | ---------------------------------------------- |
| `mesen`    | `<rom>.srm`, 32,768 B, and `<rom>.rtc`, 13 B                                | `libretro:battery`, `libretro:battery:rtc`     |
| `mgba`     | `<rom>.sav`, 32,816 B, the clock in a 48 B footer                           | `mgba:battery`                                 |
| `mednafen` | `<rom>.sav`, mGBA's file, 32,816 B                                          | `mgba:battery`                                 |
| `ares`     | `ares/Game Boy/<rom>.ram`, 32,768 B, and `<rom>.rtc`, 13 B                  | `ares:battery:ram`, `ares:battery:rtc`         |
| `Gambatte` | `bizhawk/Pokemon - Crystal Version (USA, Europe) (Rev A).SaveRAM`, 32,790 B | `bizhawk:battery`                              |
| `GBHawk`   | the same file, 32,768 B with no clock                                       | `bizhawk:battery`                              |
| `SameBoy`  | the same file, 32,816 B                                                     | `bizhawk:battery`                              |
| `jgenesis` | `jgenesis/gbc/<rom>.sav`, 32,768 B, and `<rom>.rtc`, 38 B                   | `jgenesis:battery:sav`, `jgenesis:battery:rtc` |

**Mesen writes the loose `.srm` and `.rtc` the `libretro` cores share**, so it needed no rule, and
it rewrites the `.rtc` on a launch with nothing saved. **mednafen read and saved into mGBA's plain
`.sav`** rather than its hashed name, as on `gb` (finding 273). **ares keeps its battery save in
`Game Boy`, `gb`'s directory name, and its states in `Game Boy Color`**, so the two halves of one
row sit in two trees. **BizHawk's three cores share one file named after BizHawk's own title**,
`(Rev A)` where the ROM file says `(Rev 1)`, learned from the state sidecar
`Pokemon - Crystal Version (USA, Europe) (Rev A).Gambatte`; each core rewrites it at its own size,
and the `.SaveRAM.bak` beside it is BizHawk's copy of the previous save. `jgenesis` keeps a `.gbc`
ROM's saves in `jgenesis/gbc/`, which `gb`'s record left for this pass to declare.

The six saves with their own rule and one state from each of the eight rows went out of the tree
and came back through one `saves restore 274994 --apply`: `restored 6 save(s) and 8 state(s),
failed 0, 562.4 KB`, exit 0, **all fourteen files at their own md5**. Mesen's `.srm` and `.rtc` went
out and came back the same way on their own, `restored 2 save(s) and 0 state(s), failed 0`.

### 5. States on the eight

| Row        | Directory under `saves/gbc/` | First                       | Second                      | Keys                     |
| ---------- | ---------------------------- | --------------------------- | --------------------------- | ------------------------ |
| `mesen`    | `mesen/SaveStates/`          | `_1.mss`, `6aeaf1fa...`     | `_2.mss`, `69d42a50...`     | `Shift+F1`, `Shift+F2`   |
| `mgba`     | `mgba/sstates/`              | `.ss1`, `fc7fac89...`       | `.ss2`, `6c884d72...`       | `Shift+F1`, `Shift+F2`   |
| `mednafen` | `mednafen/sstates/`          | `.<md5>.mc0`, `d414ff85...` | `.<md5>.mc1`, `53d5eb0f...` | `F2`, then `F7` and `F2` |
| `ares`     | `ares/Game Boy Color/`       | `.bs1`, `13ab6aef...`       | `.bs2`, `0352b419...`       | `F2`, then `F7` and `F2` |
| `Gambatte` | `bizhawk/sstates/Gambatte/`  | `QuickSave4`, `5afd4f67...` | `QuickSave2`, `127dc204...` | `Ctrl+F4`, `Ctrl+F2`     |
| `GBHawk`   | `bizhawk/sstates/GBHawk/`    | `QuickSave4`, `fa8ce222...` | `QuickSave2`, `6d8227ca...` | `Ctrl+F4`, `Ctrl+F2`     |
| `SameBoy`  | `bizhawk/sstates/SameBoy/`   | `QuickSave4`, `7ffe1369...` | `QuickSave2`, `a12342e2...` | `Ctrl+F4`, `Ctrl+F2`     |
| `jgenesis` | `jgenesis/states/`           | `_0.jst`, `e21bf5e4...`     | `_1.jst`, `8212e9a5...`     | `F2`, then `F7` and `F2` |

**`mesen`, `mgba`, `mednafen` and `ares` declare no state directory**, and each wrote to the one
above, which the supplement now declares for `gbc`. The md5 in mednafen's names is of the whole
`.gbc` inside the zip, `301899b8...`. **BizHawk names its states after the ROM file**, `(Rev 1)`,
while its battery save carries its own title. BizHawk's frame is inside the state; only the
`libretro` rows write a screenshot `.png`, and the rest have nothing to carry.

**On this machine `Ctrl+F1` never reached EmuHawk as a save**, from `keybd_event` with EmuHawk in the
foreground and the keys held 400 ms, and neither did a plain `F2`; `Ctrl+F2` and `Ctrl+F4` saved
every time, so the three BizHawk rows took slots 4 and 2. `config.ini` binds `Save State 1` to
`Ctrl+F1`, so this is the key's delivery and not BizHawk's slot.

### 8. Sessions

| Row        | Journal, UTC         | Length |
| ---------- | -------------------- | ------ |
| `mesen`    | 13:22:06 to 13:22:46 | 40s    |
| `mgba`     | 13:28:25 to 13:28:59 | 33s    |
| `mednafen` | 13:31:19 to 13:32:03 | 44s    |
| `ares`     | 13:34:05 to 13:35:30 | 1m 24s |
| `Gambatte` | 13:37:45 to 13:38:48 | 1m 2s  |
| `GBHawk`   | 13:44:59 to 13:45:43 | 43s    |
| `SameBoy`  | 13:47:47 to 13:48:45 | 57s    |
| `jgenesis` | 13:50:49 to 13:51:47 | 57s    |

Every one is on the server, rom 274994, as are the four `libretro` sessions above.

## No change of row that was checked kept the clock

**Every row keeps Crystal's clock, and no two keep it the same way.** Each round trip above carried
the clock file at its own md5, so a device that stays on one row keeps its clock through RomMBat.
Moving the save between rows lost it on every move that was checked, and two were not:

| Row                                    | Where the clock is                                                 | Seeded from the row before, the game showed |
| -------------------------------------- | ------------------------------------------------------------------ | ------------------------------------------- |
| `libretro`/`gambatte`                  | `<rom>.rtc`, 8 B: the Unix time at which the game clock reads zero | the clock the maintainer set, about 9 AM    |
| `libretro`/`tgbdual`, `DoubleCherryGB` | `<rom>.rtc`, 4 B: the host time at exit                            | a wrong time, about 10 PM, with no prompt   |
| `libretro`/`sameboy`                   | `<rom>.rtc`, 32 B                                                  | a wrong time, about 4:22 AM, with no prompt |
| `mesen`                                | `<rom>.rtc`, 13 B                                                  | not noted                                   |
| `mgba`, `mednafen`                     | a 48 B footer on the `.sav`                                        | not noted                                   |
| `ares`                                 | `ares/Game Boy/<rom>.rtc`, 13 B                                    | **asked to set the clock**                  |
| `bizhawk`/`Gambatte`                   | a 22 B footer on the `.SaveRAM`                                    | **asked to set the clock**                  |
| `bizhawk`/`GBHawk`                     | nowhere: 32,768 B and no clock file                                | a wrong time, with no prompt                |
| `bizhawk`/`SameBoy`                    | a 48 B footer on the `.SaveRAM`                                    | **asked to set the clock**                  |
| `jgenesis`                             | `jgenesis/gbc/<rom>.rtc`, 38 B                                     | **asked to set the clock**                  |

**The loose `.rtc` is one file name with four formats.** RetroArch writes it for every core as RAM
type #1, and the stock core's 8 B base time, `tgbdual`'s and `DoubleCherryGB`'s 4 B host time,
`sameboy`'s 32 B record and Mesen's 13 B differ, and `tgbdual` and `sameboy` each showed a wrong
time for the file the row before left. What Mesen made of it was not noted. It syncs as one slot,
`libretro:battery:rtc`, because on disk it is one file. **Switching core on one machine can lose
the clock with no RomMBat involved**, and RomMBat carries whichever file the last row left. By the
maintainer's ruling that is recorded and not worked around. Where a seed was placed without the
clock (ares, BizHawk, jgenesis, each given the 32 KB RAM alone) the game found no clock and asked
for one, which says only that they were given none. mednafen read and saved into mGBA's own `.sav`,
both 32,816 B with a 48 B footer, and is the move most likely to have kept the clock; nobody looked.
Finding 300.

## What the pass turned up that is not a row

- **ES rewrote `gamelist.xml` on its own exit**, moving Crystal's entry to the end with `playcount`,
  `lastplayed` and `gametime`, which is why step 9 compares against a copy taken after the last ES
  session: the re-sync left that copy byte-identical and said `gamelists: all 1 unchanged`.
- **The first flush on the second deploy refused one save that is not `gbc`'s**: rom 189465 on
  `megadrive`, where the server holds the four bytes `null` another client wrote in July.
- **mGBA, mednafen's shared `.sav`, Mesen, ares and jgenesis rewrite the clock on a launch with
  nothing saved**, so each such launch uploads a small new version, as on `gba` (finding 291).

## What this file will not claim

- **Nothing about `gbc` under any build but these.** Every row was measured on RetroBat 8.2.1 and
  RomM `5.3.0`.
- **Nothing about another game.** Crystal is one MBC3 cartridge with a clock and 32 KB of RAM. A
  game with no clock, or an MBC5 with rumble, may be sized and named differently.
- **Nothing about a clock carried across rows.** Only the same row reading its own file back was
  measured to keep the time.
- **Nothing about a `gb` game in `gbc`.** Every ROM in the set is a `.gbc`; a `.gb` placed there is
  what `jgenesis/gb/` would be for, and no rule reads it on `gbc`.

## Carried back to `gb`

**ares on `gb` keeps a clock cartridge's clock beside its save**, measured on 2026-09-23 with a copy
of Silver placed in `roms/gb` for one launch under `ares`/`GameBoy`: `ares/Game Boy/<rom>.ram`,
32,768 B, and `<rom>.rtc`, 13 B, the pair it writes on `gbc`. `gb`'s ares rule read only the `.ram`,
so the clock would not have synced. It now has its own class B rule, `ares:battery:rtc`, and the
`.ram` keeps `ares:battery`, so nothing uploaded under it moves; a flush on the new build sent
nothing. The copy and what it wrote were removed after the launch, and the round trip is carried
from the `gbc` row, which syncs the same two files. Finding 302.

**jgenesis on `gb` needs no clock rule.** It names its directory from the file inside the zip, and
the clock cartridges known here are Color titles, a `.gbc`, even where they run on a mono Game Boy,
and a mono-only one may not exist; none of the catalog's `gb` Pokemon titles has a clock. So such a
clock lands in `jgenesis/gbc/`, the gap `gb.md` records, and `jgenesis/gb/<rom>.rtc` was not seen.
Only the Pokemon titles' headers were read.
