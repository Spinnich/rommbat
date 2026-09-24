# snes

Super Nintendo Entertainment System / Super Famicom. RetroBat calls the folder `snes`, which is what
this file is named after.

**All fifteen rows `snes` declares are certified**, at RomM `5.3.0` and RetroBat 8.2.1 on
2026-09-24, all nine steps with step 6 N/A because `snes` has no class D:

- `libretro`/`snes9x`, **the row a stock install gives a user**, selected with no override
- `libretro`/`bsnes-jg`, `bsnes`, `bsnes_hd_beta`, `mednafen_snes`, `mesen-s` and `snes9x2005`
- `mesen`, `mednafen`/`snes`, `snes9x`, `ares`/`SuperFamicom`, `bizhawk`/`BSNES`, `Faust` and
  `Snes9x`, and `jgenesis`

**It certifies those fifteen rows and nothing wider.** The seven `libretro` rows needed nothing.
Of the eight standalone rows, Mesen and Snes9x share `libretro`'s loose `.srm` and needed only a
state declaration; the other six needed a battery rule for `snes`, and `mednafen` and `ares` a
state declaration as well. RetroBat lists no firmware for `snes`, and three rows refuse a DSP-1
cartridge without the chip's (below).

**This file is in five parts.** Steps 1, 2 and 3, which are the system's, with which rows need
firmware. What the first boots wrote. The seven `libretro` rows, then the eight standalone ones.
Last, what the pass turned up that is not a row.

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
| 6   | N/A         | Unchanged: `snes` has no class D                                                                                  |
| 7   | Carried     | The game list's query is unchanged; the Player changes are gamepad focus in RomM's own web UI                     |
| 8   | Carried     | `play_sessions.py` is byte-identical, and its handler changed only in how it counts rows                          |
| 9   | **Touched** | Always touched on a move. **Pass**, see below                                                                     |

**Both were re-run on a deploy of the adoption branch**, made by `tools/publish.ps1 -Deploy`, with
`status` reading the server as `5.3.1`, Supported. `platforms list` was identical either side of
the deploy, with `snes` resolved by `fs_slug` as before. `sync` answered `nothing to do: 276
games already present, 0 downloaded, 0 written` for `Spinnich's Super Nintendo Favorites`, and `gamelists: all 8 unchanged`,
with every `gamelist.xml` md5'd either side and identical, and a `flush` moved no save or state.
One re-sync covers every row, because none of them owns anything a sync touches that another does
not.

## The install this was measured on

|           |                                                             |
| --------- | ----------------------------------------------------------- |
| RetroBat  | `8.2.1-stable-win64`, the supported floor                   |
| RomM      | `5.3.0`, the floor then, read back by `status` as Supported |
| Root      | `R:\RetroBat`, found by walking up from the executable      |
| Store     | schema 18 of 18, WAL                                        |
| Client    | this branch, deployed twice, named below                    |
| Budget    | `none`, as for every system before it                       |
| Test game | Legend of Zelda, The - A Link to the Past (USA)             |

**The test game is a 1 MB LoROM cartridge with 8 KB of battery SRAM**, as jgenesis reports it,
and RomM rom 200280. It writes its save when a name is registered and on Save and Continue, and
**it writes its SRAM at boot too**, so a launch with nothing saved still changes the `.srm` and
uploads a new version. It carries no `<emulator>` pin in `gamelist.xml`. Super Mario Kart (USA),
a DSP-1 cartridge, was booted on every row for step 3 and not played.

**The client was deployed twice.** The first deploy carried this branch's `snes` battery rules and
state declarations, from the boot writes and state probes below, and every row was driven on it.
The second, during the `mednafen` row, added only the empty `.rtc` below, which no row's result
depends on.

**The server already held a `libretro:battery` save for Zelda**, written on 2026-09-01, and the
stock row's first flush recorded a conflict against it rather than overwrite either. The
maintainer chose `--keep-local`, which sent the new save as the slot's newest row and left the
September one standing, as the `save-sync` skill says it does.

**Every standalone row after the first was seeded from the save the one before made**, as on
`gba`, `gb` and `gbc`. The seven `libretro` cores, Mesen, mednafen and Snes9x all read one loose
`.srm` and needed no copying; ares's `.ram`, BizHawk's `.SaveRAM` and jgenesis's `.sav` were each
given the latest save before the row's launch. All are the same raw 8 KB, which every row's boot
write showed. The maintainer then continued the file and saved in the game on every row, so every
save measured below is one that emulator wrote.

**The maintainer played every row from ES over RDP.** Where a state key did not arrive, the agent
made the missing slot from its own session through `emulatorLauncher` and `keybd_event`; those
launches run no hooks and record no session, and each is named below.

## The set

|          |                                             |
| -------- | ------------------------------------------- |
| Name     | Spinnich's Super Nintendo Favorites         |
| Scope    | `smart_collection 12`                       |
| Policy   | no game cap, no size cap, ordered by recent |
| Resolves | 276 games, 330.6 MB, into `snes`            |

The sync fetched 276 ROMs and 1,075 media files (1,012.2 MB), and wrote one gamelist with 276
entries, exit 0.

## Steps 1, 2 and 3, for every row

### 1. Mapping

Resolved at layer **`fs_slug`**: RomM's `fs_slug` for the platform is literally `snes`, which is
already a folder in this install.

| `fs_slug`         | Resolved by | What `platforms list` says                                                         |
| ----------------- | ----------- | ---------------------------------------------------------------------------------- |
| `snes`            | `fs_slug`   | RomM's fs_slug 'snes' is already a folder in this install                          |
| `snes-unofficial` | `bundled`   | the bundled table offers `snes`, `snes-msu1` and picked the first this install has |

### 2. Multi-file games and extensions

From the live `es_systems.cfg`:

```text
.smc .fig .sfc .gd3 .gd7 .dx2 .bsx .swc .rom .wad .zip .7z .decomp
```

**276 of 276 resolve and nothing is excluded or unlisted.** Every ROM is a `.zip` holding one file,
so `snes` has no multi-disc or multi-file shape to settle. `.zip` holding a `.sfc` was observed to
launch on all fifteen rows. 275 zips hold a `.sfc`. **One holds a patch and no game**:
`Dark Law - Meaning of Death (Japan) [T-En by AGTP v1.00].zip` is a single 177,786 B `.bps`, a
translation patch for a ROM the library does not carry. It syncs like any other member, and what
is in RomM is the user's to keep (finding 320).

### 3. BIOS

```console
$ rommbat-agent bios snes
RetroBat requires no BIOS for snes.
```

**Exit 0, and step 3 passes by recording so.** RetroBat's manifest names nothing for `snes`, and
RomMBat adds nothing, because the supplement copies only an entry a sibling system lists.

## Which rows need firmware

**Three of fifteen refuse a DSP-1 cartridge, and nothing RetroBat lists would fetch the file.** Each
row was launched on Super Mario Kart (USA) with no coprocessor firmware anywhere in the tree:

| Row                                | With no firmware                                                                 |
| ---------------------------------- | -------------------------------------------------------------------------------- |
| `libretro`, six of the seven cores | Reaches the title screen                                                         |
| `libretro`/`mesen-s`               | **Refuses**: holds on the Nintendo logo, logging no firmware for DSP `dsp1b.rom` |
| `mesen`                            | **Refuses**: asks to select `dsp1b.rom`, 8,192 B                                 |
| `mednafen`/`snes`                  | Reaches the title screen                                                         |
| `snes9x`                           | Reaches the title screen                                                         |
| `ares`/`SuperFamicom`              | Reaches the title screen                                                         |
| `bizhawk`/`BSNES`                  | Warns "Couldn't find firmware SNES+DSP1b", and carries on                        |
| `bizhawk`/`Faust`, `Snes9x`        | Reach the title screen                                                           |
| `jgenesis`                         | **Refuses**: "Cannot load DSP-1 cartridge because DSP-1 ROM is not configured"   |

**The title screen does not exercise the DSP**, which the game uses in a race, so "reaches the
title" is all the eleven are claimed to do. RomM's own known-files list names the chip firmware for
`snes` (`dsp1b.data.rom`, `dsp1b.program.rom`, `st010.*`), in the split form bsnes and ares read,
while Mesen asks for a single `dsp1b.rom`. Zelda has no coprocessor and boots on all fifteen.
Finding 317.

**With the firmware, all four that refused or warned reach the title.** The split pair from the
maintainer's RetroBat 8.1.2 BIOS pack matched RomM's md5s, `d10f4468...` for the program and
`1e3f5686...` for the data, and `dsp1b.rom` was made by joining them, program first, 8,192 B,
`332273cc...`. The pack's `dsp1b.zip` holds a 10,240 B `dsp1b.bin`, MAME's and FBNeo's form, which
none of these rows reads.

| Row                  | Reads                          | From                                                                                       |
| -------------------- | ------------------------------ | ------------------------------------------------------------------------------------------ |
| `libretro`/`mesen-s` | `dsp1b.rom`                    | `bios\`, RetroArch's system directory                                                      |
| `bizhawk`/`BSNES`    | `SNES+DSP1b`                   | `bios\`, with no warning once the files are there                                          |
| `mesen`              | `dsp1b.rom`                    | `emulators\mesen\Firmware\`, not `bios\`; RetroBat puts none there                         |
| `jgenesis`           | whatever `dsp1_rom_path` names | a key in `jgenesis-config.toml` RetroBat's launcher never sets, and keeps when set by hand |

So on a stock install `mesen` and `jgenesis` refuse a DSP-1 game even with the firmware in
`bios\`. **By the RetroBat team's account, relayed by the maintainer on 2026-09-24, RetroBat's
firmware list covers only a system's default emulator, by design**, and `snes`'s default,
`libretro`/`snes9x`, runs the DSP without firmware, which is why `snes` lists none. The
maintainer's install keeps the three files in `bios\`, the copy in Mesen's folder and the added
key; the config as it was is `R:\rommbat-evidence\snes\dsp-with-fw\jgenesis-config.before.toml`.

## What the boot launches wrote

**Not steps 4 or 5.** No one saved in any of these launches; each ran about 25 seconds from
`emulatorLauncher` with no key sent. Every file below was moved to `R:\rommbat-evidence\snes\boot\`
before a flush could send it.

| Row                         | Wrote at boot, under `saves/snes/`                                         |
| --------------------------- | -------------------------------------------------------------------------- |
| `libretro`, all seven cores | `<rom>.srm`, 8,192 B                                                       |
| `libretro`/`mednafen_snes`  | and `<rom>.rtc`, 0 B                                                       |
| `mesen`, `snes9x`           | `<rom>.srm`, 8,192 B                                                       |
| `mednafen`/`snes`           | `<rom>.608c22b8ff930c62dc2de54bcd6eba72.srm`, 8,192 B                      |
| `ares`/`SuperFamicom`       | nothing when killed; ended with `Esc`, `ares/Super Famicom/<rom>.ram`      |
| `bizhawk`, all three cores  | `bizhawk/Legend of Zelda, The - A Link to the Past (USA).SaveRAM`, 8,192 B |
| `jgenesis`                  | `jgenesis/sfc/<rom>.sav`, 8,192 B                                          |

**`snes9x2005` and BizHawk's `Faust` and `Snes9x` wrote all zeros where the rest wrote Zelda's
boot pattern**, and still reached the title. **mednafen's md5 is of the whole `.sfc` inside the
zip**, and it writes a `.srm`, not the `.sav` its other systems' rule reads. On Super Mario Kart
ares also wrote `<rom>.dram`, 512 B, beside a 2,048 B `.ram`: the DSP's data RAM.

## The seven `libretro` rows

|                 | Selected by                                    | Confirmed on the ES launch line     |
| --------------- | ---------------------------------------------- | ----------------------------------- |
| `snes9x`        | **Nothing: RetroBat's default**                | `-emulator libretro -core snes9x`   |
| `bsnes-jg`      | `snes.emulator = libretro`, `.core = bsnes-jg` | `-core bsnes-jg -state_slot 3`      |
| `bsnes`         | `snes.core = bsnes`                            | `-core bsnes -state_slot 3`         |
| `bsnes_hd_beta` | `snes.core = bsnes_hd_beta`                    | `-core bsnes_hd_beta -state_slot 3` |
| `mednafen_snes` | `snes.core = mednafen_snes`                    | `-core mednafen_snes -state_slot 3` |
| `mesen-s`       | `snes.core = mesen-s`                          | `-core mesen-s -state_slot 3`       |
| `snes9x2005`    | `snes.core = snes9x2005`                       | `-core snes9x2005 -state_slot 3`    |

Each override was set with ES closed, and the `snes` keys were cleared when the pass ended. ES
rewrote `es_settings.cfg` on its own exit during the pass, re-indenting it, moving `LastSystem` and
adding `Language`; the copy taken first is `R:\rommbat-evidence\snes\es_settings.before.cfg`.

| #   | Result on every one of the seven                                                              |
| --- | --------------------------------------------------------------------------------------------- |
| 4   | **Pass, both directions.** Class A `.srm` as `libretro:battery`                               |
| 5   | **Pass**, two slots each, the screenshot byte-checked                                         |
| 6   | **N/A.** `snes` is class A                                                                    |
| 7   | **Pass.** Launched from ES after the sync, box art, marquee and description in `gamelist.xml` |
| 8   | **Pass**, every session read back from RomM by `status`, under `recent:`                      |
| 9   | **Pass.** 276 present and verified, 1,075 media present, gamelist byte-identical, 0 sent      |

| Core            | Session, UTC         | `.srm` after  | Slot 1 png    | Slot 2 state, png            |
| --------------- | -------------------- | ------------- | ------------- | ---------------------------- |
| `snes9x`        | 11:17:53 to 11:18:58 | `514e7309...` | `3ef0bcf6...` | `c012c9df...`, `af4d6ded...` |
| `bsnes-jg`      | 11:22:54 to 11:23:35 | `f07bde08...` | `3eb5cc10...` | `99d925c6...`, `7213c135...` |
| `bsnes`         | 11:24:51 to 11:26:22 | `c55ad6cd...` | `6b6c6fde...` | `7e1f072e...`, `481717e7...` |
| `bsnes_hd_beta` | 11:27:28 to 11:28:09 | `c2bce74f...` | `c99165dd...` | `58f71a2b...`, `b7f3ff86...` |
| `mednafen_snes` | 11:29:33 to 11:30:17 | `663ff2cf...` | `68c0ec7b...` | `e9b5ac91...`, `145f417d...` |
| `mesen-s`       | 11:31:26 to 11:32:04 | `4e060f14...` | `b3d34e1e...` | `2ea49d53...`, `488d22ee...` |
| `snes9x2005`    | 11:33:07 to 11:33:43 | `85a555e3...` | `69a6f07a...` | `a67305a3...`, `993d2299...` |

**The maintainer registered a name on the stock row**, saved with Save and Continue and made two
states; each later core continued the `.srm` the one before it left, saved again, and made two
states. **After each row its `.srm`, slot 2's state and slot 2's `.png` went out of the tree and
came back through `saves restore 200280 --apply`**, `restored 1 save(s) and 1 state(s), failed 0,
... with 1 screenshot(s)`, exit 0, every file at its own md5, and each returned image was that
slot's, distinct from slot 1's.

**The declared `<directory>` is where every core wrote**, `saves/snes/libretro.<core>/`. ES passed
`-state_slot 3` from the second row on, and RetroArch wrote slots 1 and 2 regardless, as finding
261 says. **`mednafen_snes` leaves an empty `<rom>.rtc` on every exit**, which RomMBat now passes
over without reporting (finding 319).

## The eight standalone rows

|            | Selected by                                    | Confirmed on the ES launch line     |
| ---------- | ---------------------------------------------- | ----------------------------------- |
| `mesen`    | `snes.emulator = mesen`                        | `-emulator mesen`, empty `-core`    |
| `mednafen` | `snes.emulator = mednafen`, `.core = snes`     | `-emulator mednafen -core snes`     |
| `snes9x`   | `snes.emulator = snes9x`                       | `-emulator snes9x`, empty `-core`   |
| `ares`     | `snes.emulator = ares`, `.core = SuperFamicom` | `-emulator ares -core SuperFamicom` |
| `BSNES`    | `snes.emulator = bizhawk`, `.core = BSNES`     | `-emulator bizhawk -core BSNES`     |
| `Faust`    | `snes.emulator = bizhawk`, `.core = Faust`     | `-emulator bizhawk -core Faust`     |
| `Snes9x`   | `snes.emulator = bizhawk`, `.core = Snes9x`    | `-emulator bizhawk -core Snes9x`    |
| `jgenesis` | `snes.emulator = jgenesis`                     | `-emulator jgenesis`, empty `-core` |

**Standalone Snes9x was not installed.** `emulators/snes9x/` held only `snes9x.conf`, and the first
launch under the row offered to install it; with the maintainer's agreement the agent accepted, and
RetroBat's installer put `snes9x-x64.exe` in place in about three seconds.

### Checklist for the eight

| #   | Result on every one of the eight                                                            |
| --- | ------------------------------------------------------------------------------------------- |
| 4   | **Pass, both directions**, every file at its own md5 after a restore                        |
| 5   | **Pass**, two slots each, where each emulator was found to write                            |
| 6   | **N/A**                                                                                     |
| 7   | **Pass**, carried: each was launched from ES on the synced ROM, art and description present |
| 8   | **Pass**, every session read back from RomM by `status`, under `recent:`                    |
| 9   | **Pass.** 276 present and verified, 1,075 media present, gamelist byte-identical, 0 sent    |

### 4. Battery saves on the eight

| Row        | File under `saves/snes/`                                             | Slot                                        | After the row |
| ---------- | -------------------------------------------------------------------- | ------------------------------------------- | ------------- |
| `mesen`    | `<rom>.srm`, the file the `libretro` cores share                     | `libretro:battery`                          | `05c99057...` |
| `mednafen` | `<rom>.srm` when one is there, else `<rom>.<md5>.srm`                | `libretro:battery`, else `mednafen:battery` | `425acf6b...` |
| `snes9x`   | `<rom>.srm`, the same file                                           | `libretro:battery`                          | `d12f5187...` |
| `ares`     | `ares/Super Famicom/<rom>.ram`, and `<rom>.dram` for a DSP cartridge | `ares:battery:ram`                          | `97929108...` |
| `BSNES`    | `bizhawk/<rom>.SaveRAM`, one file for all three cores                | `bizhawk:battery`                           | `71480f98...` |
| `Faust`    | the same file                                                        | `bizhawk:battery`                           | `74b45e5b...` |
| `Snes9x`   | the same file                                                        | `bizhawk:battery`                           | `cd78b59f...` |
| `jgenesis` | `jgenesis/sfc/<rom>.sav`                                             | `jgenesis:battery`                          | `b6cbf7aa...` |

**Mesen and Snes9x write the loose `.srm` the `libretro` cores share**, so they needed no rule, and
their saves upload as `libretro:battery`. Mesen's `.srm` above is the one after the agent's own
launch for slot 2, which rewrote it at boot. **mednafen read and saved into the plain `.srm`**, as
it adopted mGBA's `.sav` on `gb` and `gbc`; its hashed `<rom>.<md5>.srm`, written only when no
plain one is there, has its own rule, `mednafen:battery`, and a restore names it through
`MednafenRomHash`, both covered by tests and neither driven, since the maintainer's session never
wrote one. **BizHawk names its battery save after the ROM file on `snes`**, where on `gb` and `gbc`
it used its own title; the sidecar `<rom>.txt` reads `<rom>.BSNES.Compatibility`. The rule stays
`display name`, whose routes, the sidecar and the launch window, attribute either spelling.
**jgenesis keeps a `.sfc`'s save in `jgenesis/sfc/`**, named after the file inside the zip.

After each row its save and one state went out of the tree and came back through
`saves restore 200280 --apply`, `restored 1 save(s) and 1 state(s), failed 0`, exit 0, each at its
own md5.

### 5. States on the eight

| Row        | Directory under `saves/snes/` | Slots and who made them                       | Round-tripped               |
| ---------- | ----------------------------- | --------------------------------------------- | --------------------------- |
| `mesen`    | `mesen/SaveStates/`           | `_1.mss` in ES; `_2.mss` by the agent         | `_1.mss`, `c3711cf1...`     |
| `mednafen` | `mednafen/sstates/`           | `.<md5>.mc0` and `.mc1`, both in ES           | `.mc1`, `d2458808...`       |
| `snes9x`   | `snes9x/sstates/`             | `.000` and `.001`, both in ES                 | `.001`, `b47ed5be...`       |
| `ares`     | `ares/Super Famicom/`         | `.bs1` in ES; `.bs2` by the agent             | `.bs1`, `f19ac621...`       |
| `BSNES`    | `bizhawk/sstates/BSNES/`      | `QuickSave3` and `QuickSave4`, both in ES     | `QuickSave4`, `3c3f2311...` |
| `Faust`    | `bizhawk/sstates/Faust/`      | `QuickSave5` in ES; `QuickSave2` by the agent | `QuickSave5`, `6b2c0765...` |
| `Snes9x`   | `bizhawk/sstates/Snes9x/`     | `QuickSave6` and `QuickSave7`, both in ES     | `QuickSave7`, `2a12d622...` |
| `jgenesis` | `jgenesis/states/`            | `_0.jst` and `_1.jst`, both in ES             | `_1.jst`, `2ada4850...`     |

**`mesen`, `mednafen`, `snes9x` and `ares` declare no state directory**, and each wrote to the one
above, which the supplement now declares for `snes`. Snes9x's slots are `.000` to `.009`, Shift+F10
being slot 0, which the supplement writes as `.00{{slot0}}`. **ares keeps its states beside its
battery save** in `ares/Super Famicom/`, unlike `gbc`, where the two halves split. **jgenesis writes
its states to `emulators/jgenesis/states/sfc/`** under its `state_path = "EmulatorFolder"`, and
`emulatorLauncher` mirrors them into the declared `saves/snes/jgenesis/states/`, which is where
RomMBat reads them. Mesen and snes9x tag a state with their build, `v2.1.1+137ae7ce...` and `v1.63`.
BizHawk's frame is inside the state, and the two BSNES slots hold different `Framebuffer.bmp`s;
only the `libretro` rows write a screenshot `.png`.

**The agent's slots are title-screen states**: in each of its three launches the game was on the
title when the key went, having been given no input, and Mesen's `Ctrl+F1`, its load key, did not
bring slot 1 back before the save. They are real states on a different frame from the maintainer's,
and each went up in the flush after it.

**ares shows nothing when a state is saved or the slot changes**, so the maintainer could not tell
from the pad whether a key had landed. On disk `F2` saved slot 1 and `F7` did not step the slot. On
the next system the agent drives ares's states from its own session and checks the files, rather
than asking for keys pressed blind.

### 8. Sessions

| Row        | Journal, UTC         | Length |
| ---------- | -------------------- | ------ |
| `mesen`    | 12:18:15 to 12:19:05 | 50s    |
| `mednafen` | 12:24:09 to 12:24:55 | 46s    |
| `snes9x`   | 12:26:17 to 12:27:03 | 46s    |
| `ares`     | 12:28:12 to 12:29:04 | 51s    |
| `BSNES`    | 12:32:36 to 12:33:14 | 38s    |
| `Faust`    | 13:19:12 to 13:20:02 | 49s    |
| `Snes9x`   | 13:23:42 to 13:24:28 | 46s    |
| `jgenesis` | 13:29:26 to 13:30:25 | 59s    |

Every one is on the server, rom 200280, as are the seven `libretro` sessions above and a 4 s session
from 13:26:57, jgenesis's first launch, which closed on its own (below).

## What the pass turned up that is not a row

- **jgenesis closed once, 3.5 s into a launch from ES, with exit code 1**, and did not do so again.
  It could not be reproduced from `emulatorLauncher` with and without ES's controller arguments,
  from its own command line, or on the exact seed it was given, in a scratch folder with a copy of
  its config. The maintainer's next launch from ES ran normally. Exit code 1 is an error jgenesis
  returned, not a panic, and `emulatorLauncher` discards its stderr, so the reason was not seen.
  That launch was the first of the pass to report the pad as an Xbox 360 Controller. Finding 321.
- **A server state RomMBat cannot place is skipped on every restore**: `<rom>.state` under emulator
  `snes9x`, from before this pass, matches no declaration now that standalone Snes9x has one, and
  is reported and left alone, as it was before.
- **The first flush after each deploy refused one save that is not `snes`'s**: rom 189465 on
  `megadrive`, where the server holds the four bytes `null` another client wrote in July.
- **ES rewrote `gamelist.xml` on its own exit**, so step 9 compares against a copy taken after the
  last ES session: the re-sync left that copy byte-identical and said `gamelists: all 1 unchanged`.

## What this file will not claim

- **Nothing about `snes` under any build but these.** Every row was measured on RetroBat 8.2.1 and
  RomM `5.3.0`.
- **Nothing about another game.** Zelda is one LoROM cartridge with 8 KB of SRAM and no
  coprocessor. A game with a coprocessor, a real-time clock or a larger SRAM may be sized and named
  differently, and ares's `.dram` was seen on Super Mario Kart only.
- **Nothing about a DSP game past its title screen**, on any row.
- **Nothing about a `.smc`.** Every ROM in the set is a `.sfc`; a `.smc` can carry a 512-byte copier
  header, and mednafen's hashed name for one is left unnameable until one is driven.
- **Nothing about mednafen's hashed `.srm` driven through a restore**, which only the tests cover.
