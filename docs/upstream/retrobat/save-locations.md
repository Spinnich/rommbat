---
summary: The `saves/` tree on a real install: the four shapes, class C units, class D containers and their conversion options.
read-when: Before scanning `saves/`, attributing a save unit, or handling a shared container.
---

# RetroBat: save locations

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-6. Add `megacd`'s shared `4Mbit_cart.brm` and `xbox`'s `eeprom.bin` + `xbox_hdd.qcow2`

Plan says: Class-D list is PCSX2 and Dreamcast VMU (L822)

Measurement says: Add `megacd`'s shared `4Mbit_cart.brm` and `xbox`'s `eeprom.bin` + `xbox_hdd.qcow2`

## RB-10. The tree is `saves/<system>/<emulator>/`, plus emulator-named folders at the top level

Plan says: Save directories are modelled per system (L815, M6 generally)

Measurement says: The tree is `saves/<system>/<emulator>/`, plus emulator-named folders at the top level

## RB-43. Detecting changes to a class-D shared container

Plan says: (not addressed) detecting changes to a class-D shared container

Measurement says: Launching a PS2 game rewrote both `Mcd001.ps2` and `Mcd002.ps2` with no in-game save, so **mtime is useless for class D** and content hashing is mandatory

## RB-119. Nine, against the 243 systems the live `es_systems.cfg` declares: `amiga`, `dolphin`, `gameandwatch`

Previously: Four top-level directories under `saves/` are emulator-named, not systems (probe 2)

Measurement says: **Nine**, against the 243 systems the live `es_systems.cfg` declares: `amiga`, `dolphin`, `gameandwatch`, `ghostship`, `loopy`, `mesen`, `pb`, `psxmame`, `windows`

## RB-120. Not reliably

Previously: The second segment is the emulator (plan M6, probe 2)

Measurement says: **Not reliably.** `mame/artwork`, `mame/cfg`, `mame/ctrlr`, `n64/sram`, `n64/games`, `n64/sstates`, `psp/SYSTEM`, `psp/Cheats`, `switch/user`, `switch/sdmc`, `rtcw/Main` and `dolphin/User` name no emulator. Where states live it is emulator-**and-core**, so `saves/gbc/libretro.gambatte/` sits beside `saves/gbc/*.srm`. Discovery cannot be positional in either level

## RB-121. `xbox` refutes it: `eeprom.bin` and a 39,714,816 B `xbox_hdd.qcow2` sit loose at the system root and both are

Previously: A loose file under `saves/<system>/` is a class A battery save (plan M6)

Measurement says: **`xbox` refutes it**: `eeprom.bin` and a 39,714,816 B `xbox_hdd.qcow2` sit loose at the system root and both are class D. And **`megacd` interleaves classes at one level**, per-game `.brm` and `.srm` beside the shared `4Mbit_cart.brm`, so excluding class D is a named-container list rather than a positional rule

## RB-122. Still 21, and all 21 hold content on the measured install

Previously: `save_shapes.json` leaves 21 systems `_unclassified` (F19)

Measurement says: Still 21, and **all 21 hold content on the measured install**. `ports` holds content and is absent from the file entirely, not even listed as unclassified. So the bundled data is short of the tree in two different ways

## RB-123. Unset on this install, as are all four class-D options (`duckstation_memcardtype`, `pcsx2_slot1_memory`

Previously: `dolphin_sync_saves` must be detected before trusting a location (RB-9c, and see 189 for what it actually does)

Measurement says: **Unset on this install**, as are all four class-D options (`duckstation_memcardtype`, `pcsx2_slot1_memory`, `flycast_vmupergame`, `dolphin_slotA`). Stock is the case to build for; the conversion hazards are stage 2's to detect

## RB-125. 37 loose files, 43.0 MB, 0.51 s

Previously: (not addressed) what a class A pass actually costs

Measurement says: **37 loose files, 43.0 MB, 0.51 s** across every system on a real install, and **38 MB of that is `xbox`'s class-D disk image** which it must not read. MAME's whole `nvram` tree, for comparison, is 1,531 files and 8.0 s

## RB-139. Whether an emulator's battery save keeps the ROM's name

Previously: (not addressed) whether an emulator's battery save keeps the ROM's name

Measurement says: **BizHawk truncates it**: `Phantasy Star (Brazil).zip` produced `bizhawk/Phantasy Star (B).SaveRAM`. It sits in a subdirectory so this release reports it rather than syncing it, but any future attribution by filename has to expect a truncated stem

## RB-140. Refuted, on three systems at once

Previously: Class C is "a directory per game" (plan, the class table)

Measurement says: **Refuted, on three systems at once.** `ps3` holds `BLUS30109G6A383E91`, `BLUS30109G6A3B071C` and `BLUS30109S` for one title id, and `BCUS98111-AUTOSAVE` beside `BCUS98111-USERDATA`. `psp` holds `UCES01011` and `ULES01513SYSDATA`. **`gamecube` has no per-game directory at all**: `69-GXBE-game1.ssx.gci` and `69-GXBE-settings.ssx.gci` are two files in a shared folder

## RB-141. It is a prefix of it

Previously: The unit key is the directory's name

Measurement says: **It is a prefix of it.** `ULES01513SYSDATA` carries key `ULES01513`, and `BLUS30187GAMEDAT9ZLDR0F5K7M4000` carries `BLUS30187`. Matching the whole segment finds nothing

## RB-143. It reaches nothing this stage needs

Previously: Reading the ID out of the ROM is the fallback route (plan, M6; F17)

Measurement says: **It reaches nothing this stage needs.** Every image in five systems, head read only: `gamecube` 178 `.rvz`, **100%** readable at `0x58` with the version checked; `wii` 40 `.rvz` + 13 `.wad`, **75.5%**; `psp` 147 `.cso` + 7 `.chd`, **0%**; `ps3` 23 `.dec.iso`, **0%**; `psx` 386 `.chd`, **0%**. No constant offset reaches a `.cso`, a `.chd` or an ISO9660 image

## RB-144. It yields nothing the directory name does not

Previously: `PARAM.SFO` yields the Game ID (start-m6-stage2b brief)

Measurement says: **It yields nothing the directory name does not.** Its keys are `SAVEDATA_DIRECTORY`, which is the directory's own name, and `TITLE`, a human string (`'echochrome'`, `'The 3rd Birthday'`). So parsing it buys a fuzzy title match, never an exact key

## RB-145. It is, and it is measured

Previously: The state `.txt` sidecar may be a cheaper third route (stage 2a ledger)

Measurement says: **It is, and it is measured.** `ppsspp/3rd Birthday, The (Europe).txt` holds `ULES01513_1.00`, whose `ULES01513` prefix joins `SAVEDATA/ULES01513SYSDATA`, while the stem resolves through `RomIndex`. It needs no ROM read and no observed launch, and it covers only games that have a state

## RB-153. Structurally sound and unprovable on this install

Previously: MAME's short name **is** the rom basename, so attribution is free (probe 2, plan)

Measurement says: **Structurally sound and unprovable on this install.** 1,231 `nvram` unit directories against 3 `.zip` files in `roms/mame`, so nothing joins. The names are well-formed MAME short names (`1944`, `19xx`, `1on1gov`, `20pacgal`) and a MAME set names each archive after one, but this library cannot demonstrate it

## RB-158. What a class C scan costs on a real tree

Previously: (not addressed) what a class C scan costs on a real tree

The pass says: **4.1 s wall** for the whole `K:` saves tree, 1,231 MAME nvram units and everything else, including hashing

## RB-171. Seven of ten never were

The claim being checked: The bundled `shared_containers` declarations are reachable (`save_rules.json`, `SaveDiscoveryTests`)

What was measured: **Seven of ten never were.** `SharedContainerReason` had one caller, asking with a bare filename from a non-recursive enumeration, so the seven declarations naming a path with a separator (`ps2/pcsx2/memcards/Mcd00{1,2}.ps2`, dreamcast's four VMUs, `saturn/kronos/bkram.bin`) could not match. The test covering it called the lookup table rather than the scanner, and passed. The shared PS2 cards were being counted inside an unread `pcsx2/` subdirectory instead of named

## RB-174. Still true, re-read on a heavily used install

The claim being checked: All four class D options and `dolphin_sync_saves` are unset on a stock install (**probe 2**)

What was measured: **Still true, re-read on a heavily used install.** None of `pcsx2_slot1_memory`, `duckstation_memcardtype`, `dolphin_slotA`, `flycast_vmupergame` or `dolphin_sync_saves` appears in the 261-setting file, and no per-game `[&quot;` key of any kind does. `ps2.emulator` is `pcsx2`, so `(ps2, pcsx2)` is the pair to build for

## RB-198. Whether a save this device uploaded is restored if it goes missing locally

The claim being checked: (not addressed) whether a save this device uploaded is restored if it goes missing locally

What was measured: **It is not.** With the GameCube unit deleted and its row forgotten, a flush planned no download and the region root stayed empty; the save had to be fetched from `/api/saves/{id}/content` by hand. So the two halves compound: RetroBat puts back a stale copy RomMBat cannot see, and the good copy on the server is unreachable to this device. This is the ledger's `IsOwnUpload` question, measured rather than inferred

## RB-253. No, and `nes` is the first system checked against every row rather than one

Question: A system's save shape holds across every emulator it declares (`save_shapes.json`, one row per system)

Measured: **No, and `nes` is the first system checked against every row rather than one.** Three `libretro` cores share one `saves/nes/<rom>.srm`, so switching core continues the same save, measured on Kirby's Adventure under `nestopia` then `fceumm`. The other six rows write four other shapes in their own subdirectories or under other extensions, so `nes` is class A on `libretro` and on nothing else. The file's `_note` already said shape is a property of `(system, emulator)`; this is that note measured

## RB-254. The display name, not the ROM filename

Question: (not addressed) what BizHawk names a battery save after

Measured: **The display name, not the ROM filename.** `StarTropics (USA).zip` produced `saves/nes/bizhawk/StarTropics.SaveRAM`, dropping the region tag, which is a join key no ROM filename yields. Its own state sidecar spells the convention out as `StarTropics.NesHawk`, so the mapping is recoverable from the tree (#151). **Now joined**, by `DisplayNameAttributor` through that sidecar or a BizHawk launch, refusing a title two ROMs answer to; driven on both cores in RB-262 to RB-266

## RB-361. What the real save tree contains

Inventoried from a live install with a substantial library
(`probe-output/saves_observed.json`). **The single biggest structural finding is that the
plan's mental model of the saves tree is wrong.**

**Saves are `saves/<system>/<emulator>/...`, not `saves/<system>/`.** Every system that uses
a standalone emulator gets an emulator-named subdirectory (`ps2/pcsx2`, `dreamcast/flycast`,
`saturn/kronos`, `3ds/azahar`, `wii/dolphin-emu`). Only libretro battery saves land loose at
`saves/<system>/*.srm`. Worse, there are also **emulator-named folders at the top level**
(`saves/dolphin/`, `saves/mesen/`, `saves/psxmame/`, `saves/amiga/`) that sit beside the
system folders rather than under them. `saves/dolphin/User/GC/SRAM.USA.raw` exists at the
same time as `saves/gamecube/dolphin-emu/User/GC/`. Any code that assumes a save path
begins with a system name will mis-attribute these.

Observed shapes, with the evidence:

| System                                                                       | Observed            | Notes                                                                                                                                                                                                                        |
| ---------------------------------------------------------------------------- | ------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `nes`, `snes`, `gb`, `gbc`, `gba`, `megadrive`, `n64`, `pcengine`, `sega32x` | **A**               | loose `.srm`, ROM-filename-keyed. The easy case, and it is the majority.                                                                                                                                                     |
| `psx`                                                                        | **A**               | loose `.srm`, so this install runs libretro for PS1, **not** DuckStation. The plan's claim that PS1 is already per-game via DuckStation's `PerGameTitle` default does not apply unless DuckStation is the selected emulator. |
| `saturn`                                                                     | **B**               | `.bcr` (512 KB) **and** `.bkr` (32 KB) per game, both present for every game.                                                                                                                                                |
| `megacd`                                                                     | **B and D at once** | per-game `.brm` + `.srm`, **plus a shared `4Mbit_cart.brm` (512 KB)** holding the RAM cart for all games. Class D, and not in the plan's class-D table.                                                                      |
| `mame`                                                                       | **C**               | `mame/nvram/<shortname>/`, **1231 directories**. Keyed by MAME short name, which _is_ the ROM basename, so attribution here is trivially solvable by filename, unlike the other class-C cases.                               |
| `psp`                                                                        | **C**               | `psp/SAVEDATA/<GAMEID>SYSDATA/` containing `PARAM.SFO`. Game-ID-keyed as predicted.                                                                                                                                          |
| `ps3`                                                                        | **C**               | `ps3/rpcs3/dev_hdd0/home/00000001/savedata` and `dev_hdd0/savedata`. **32451 files** under `ps3/rpcs3/`, which is a real performance constraint on any recursive hash.                                                       |
| `gamecube`                                                                   | **C, multi-file**   | see below                                                                                                                                                                                                                    |
| `wii`                                                                        | **C**               | full NAND tree at `wii/dolphin-emu/User/Wii/title/...`, alongside a lot of shared system state that is not per-game.                                                                                                         |
| `dreamcast`                                                                  | **D**               | see below                                                                                                                                                                                                                    |
| `xbox`                                                                       | **D**               | `xbox/eeprom.bin` (256 B) and `xbox/xbox_hdd.qcow2` (38 MB). A whole disk image, shared by every game.                                                                                                                       |

## RB-364. The class-D conversion options, read from `es_features.cfg`

All four options the plan names exist, and their choice lists are wider than assumed:

| Emulator    | Option                    | Choices                                                 | Best for sync            |
| ----------- | ------------------------- | ------------------------------------------------------- | ------------------------ |
| DuckStation | `duckstation_memcardtype` | `PerGameTitle`, `Shared`, `PerGameFileTitle`, `PerGame` | **`PerGameFileTitle`**   |
| PCSX2       | `pcsx2_slot1_memory`      | `standard`, `folder`, `game`                            | **`game`**               |
| Dolphin     | `dolphin_slotA`           | `8` (GCI folder), `1` (memory card)                     | **`8`**, already default |
| Flycast     | `flycast_vmupergame`      | switch, `switchauto` so unset by default                | **on**                   |

**The plan's DuckStation recommendation should change.** It treats the stock `PerGameTitle`
as good enough. `PerGameFileTitle` is strictly better for a sync client: it names the card
after the **rom file**, which is the key RomMBat already matches on, whereas `PerGameTitle`
uses DuckStation's internal database title, which need not equal the filename. Choosing
`PerGameFileTitle` collapses class D into ordinary class-A handling.

> **Superseded.** This recommendation was withdrawn once a real card was measured rather than
> reasoned about. `PerGameTitle` names the card from `gamedb.yaml`'s `saveName` with the disc
> marker removed, which is what binds a multi-disc set onto one card; keying by rom file splits
> the set and loses the save at the disc change. The conclusion above is right about the
> mechanism and wrong about which side of the trade to take. See
> [freegosy-findings.md](../../freegosy-findings.md), F18. The rest of this section still holds.

**One hazard found while reading these options:** `dolphin_sync_saves`, described as
"RetroBat will sync dolphin and libretro-dolphin saves folders". **That description is the
emulator's and it is misleading**, which RB-189 settles: it is GameCube only, it runs once
per launch inside emulatorlauncher rather than on a schedule, and it reconciles
`GC/<REGION>/` against a `Card A/` subdirectory of that same folder. Whether it is on has to be
detected, and so does the `Card A` it leaves behind, which outlives the setting.
