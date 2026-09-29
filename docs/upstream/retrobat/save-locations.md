---
summary: The `saves/` tree on a real install: the four shapes, class C units, class D containers and their conversion options.
read-when: Before scanning `saves/`, attributing a save unit, or handling a shared container.
---

# RetroBat: save locations

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-361. The tree is `saves/<system>/<emulator>/`, and the loose level is mostly libretro's

Verified: RetroBat 8.2.0, 2026-08-08. How: inventoried every file under `saves/` on a real install with a substantial library.
Most standalone emulators write under an emulator-named subdirectory (`ps2/pcsx2`,
`dreamcast/flycast`, `saturn/kronos`, `3ds/azahar`, `wii/dolphin-emu`). Libretro battery saves
land loose at `saves/<system>/*.srm`, and so do a few standalone emulators', such as `mesen`
and `mednafen` on `nes` (RB-253, `save_rules.json`). Emulator-named folders also sit at the top
level beside the systems (RB-119), and `saves/dolphin/User/GC/SRAM.USA.raw` exists alongside
`saves/gamecube/dolphin-emu/User/GC/`. So a save path does not always begin with a system name.

| System                                                                       | Class       | Observed                                                                                            |
| ---------------------------------------------------------------------------- | ----------- | --------------------------------------------------------------------------------------------------- |
| `nes`, `snes`, `gb`, `gbc`, `gba`, `megadrive`, `n64`, `pcengine`, `sega32x` | A           | loose `.srm`, keyed by ROM filename                                                                 |
| `psx`                                                                        | A           | loose `.srm` under libretro; DuckStation writes per-set memory cards instead (`save_shapes.json`)   |
| `saturn`                                                                     | B           | `.bcr` (512 KB) and `.bkr` (32 KB), both for every game                                             |
| `megacd`                                                                     | B and D     | per-game `.brm` and `.srm`, plus a shared 512 KB `4Mbit_cart.brm` RAM cart (RB-121)                 |
| `mame`                                                                       | C           | `mame/nvram/<shortname>/`, 1,231 directories (RB-153)                                               |
| `psp`                                                                        | C           | `psp/SAVEDATA/<GAMEID>SYSDATA/` holding `PARAM.SFO` (RB-141, RB-144)                                |
| `ps3`                                                                        | C           | `ps3/rpcs3/dev_hdd0/home/00000001/savedata` and `dev_hdd0/savedata`, inside a 32,451-file data root |
| `gamecube`                                                                   | C, per file | `.gci` files in a shared folder, no per-game directory (RB-140)                                     |
| `wii`                                                                        | C           | the NAND tree at `wii/dolphin-emu/User/Wii/title/`, beside shared system state                      |
| `dreamcast`                                                                  | D           | shared VMUs under `flycast/vmu/`                                                                    |
| `xbox`                                                                       | D           | `eeprom.bin` (256 B) and `xbox_hdd.qcow2` (38 MB), one disk image for every game (RB-121)           |

`SaveScanner` finds saves through the rules in `save_rules.json` (battery saves, shared
containers) and the class C unit paths in `save_shapes.json`, never by position in the path.

## RB-119. Nine top-level folders under `saves/` are emulators, not systems

Verified: RetroBat 8.2.0, 2026-08-16. How: listed `saves/` on a real install and diffed it against the 243 systems its live `es_systems.cfg` declares.
The nine are `amiga`, `dolphin`, `gameandwatch`, `ghostship`, `loopy`, `mesen`, `pb`, `psxmame`
and `windows`. A first path segment is a system only when `es_systems.cfg` declares it.

## RB-120. The second path segment does not reliably name an emulator

Verified: RetroBat 8.2.0, 2026-08-16. How: classified every second-level directory under `saves/` on a real install.
`mame/artwork`, `mame/cfg`, `mame/ctrlr`, `n64/sram`, `n64/games`, `n64/sstates`, `psp/SYSTEM`,
`psp/Cheats`, `switch/user`, `switch/sdmc`, `rtcw/Main` and `dolphin/User` name no emulator.
Where states live the segment is emulator and core, so `saves/gbc/libretro.gambatte/` sits
beside `saves/gbc/*.srm`. Discovery cannot be positional at either level.

## RB-121. A loose file under `saves/<system>/` can be a shared container

Verified: RetroBat 8.2.0, 2026-08-16. How: classified every loose file under a system folder on a real install.
`xbox` keeps `eeprom.bin` and a 39,714,816 B `xbox_hdd.qcow2` loose at the system root, and both
are class D. `megacd` mixes classes at one level: per-game `.brm` and `.srm` beside the shared
`4Mbit_cart.brm`. So `save_rules.json`'s `shared_containers` names each one, and the scanner
excludes by that list rather than by position.

## RB-122. Every system `save_shapes.json` leaves unclassified holds saves

Verified: RetroBat 8.2.0, 2026-08-16. How: compared the file's `_unclassified` list and `shapes` against the systems holding content on a real install.
All 21 unclassified systems hold content. `ports` holds content too and appears in neither list.
So the bundled data falls short of a used tree in two ways, and a scan reports what no shape
covers rather than guessing a class for it.

## RB-123. The four class D options and `dolphin_sync_saves` are unset by default

Verified: RetroBat 8.2.0, 2026-08-16 and 2026-08-24. How: read `es_settings.cfg` on a real, heavily used install, 261 settings on the second reading.
None of `pcsx2_slot1_memory`, `duckstation_memcardtype`, `dolphin_slotA`, `flycast_vmupergame`
or `dolphin_sync_saves` appears, and no per-game `[&quot;` key of any kind does. `ps2.emulator`
is `pcsx2`. So the stock shape of each system is the one to build for, and a conversion is
something RomMBat detects and offers (RB-364).

## RB-364. The class D conversion options and their choices

Verified: RetroBat 8.2.0, 2026-08-08. How: read each option's choice list from `es_features.cfg`.

| Emulator    | Option                    | Choices                                                 | `save_shapes.json` sets |
| ----------- | ------------------------- | ------------------------------------------------------- | ----------------------- |
| DuckStation | `duckstation_memcardtype` | `PerGameTitle`, `Shared`, `PerGameFileTitle`, `PerGame` | nothing, stock is kept  |
| PCSX2       | `pcsx2_slot1_memory`      | `standard`, `folder`, `game`                            | `game`                  |
| Dolphin     | `dolphin_slotA`           | `8` (GCI folder), `1` (memory card)                     | `8`, the default        |
| Flycast     | `flycast_vmupergame`      | a `switchauto`, so off unless set                       | `on`                    |

DuckStation's stock `PerGameTitle` keys the card by `gamedb.yaml`'s `saveName` with the disc
marker removed, which binds a multi-disc set onto one card, so RomMBat leaves it alone.
`es_features.cfg` describes `dolphin_sync_saves` as syncing the dolphin and libretro-dolphin
folders. What it does is narrower, and is in the GameCube facts, RB-189.

## RB-43. Launching a PS2 game rewrites both shared memory cards

Verified: RetroBat 8.2.0, 2026-08-08. How: snapshotted `saves/ps2/` around a PCSX2 launch in which the game saved nothing.
`pcsx2/memcards/Mcd001.ps2` and `Mcd002.ps2`, 8,650,752 B each, were both rewritten. So a class
D container's mtime changes on every launch, and mtime never decides whether a save needs
uploading: every save RomMBat syncs is content-hashed.

## RB-125. A class A pass over a whole install costs half a second

Verified: RetroBat 8.2.0, 2026-08-16. How: timed a read of every loose file under every system folder on a real install.
37 loose files, 43.0 MB, 0.51 s. 38 MB of that is `xbox`'s class D disk image, which a scan
must not read (RB-121). MAME's whole `nvram` tree, for comparison, is 1,531 files and 8.0 s.

## RB-158. A class C scan of a whole tree costs about four seconds

Verified: RetroBat 8.2.0, 2026-08-17. How: timed a save scan over the whole `saves/` tree of the `K:` development install, hashing included.
4.1 s wall, 1,231 MAME `nvram` units and everything else included.

## RB-140. A class C unit is not a directory per game

Verified: RetroBat 8.2.0, 2026-08-17. How: listed every class C container on a real install.
`ps3` keeps `BLUS30109G6A383E91`, `BLUS30109G6A3B071C` and `BLUS30109S` for one title id, and
`BCUS98111-AUTOSAVE` beside `BCUS98111-USERDATA`. `psp` keeps `UCES01011` beside
`ULES01513SYSDATA`. `gamecube` has no per-game directory at all: `69-GXBE-game1.ssx.gci` and
`69-GXBE-settings.ssx.gci` are two files in a shared folder. So a unit is a `(container, key)`
pair, which is what migration `008` stores.

## RB-141. The unit key is a prefix of the directory name

Verified: RetroBat 8.2.0, 2026-08-17. How: joined each class C directory on a real install against the title ids its games carry.
`ULES01513SYSDATA` carries key `ULES01513`, and `BLUS30187GAMEDAT9ZLDR0F5K7M4000` carries
`BLUS30187`. Matching the whole segment finds nothing.

## RB-143. A ROM's header yields a Game ID for GameCube and Wii only

Verified: RetroBat 8.2.0, 2026-08-17. How: read the first bytes of every image in five systems on a real install.
`gamecube`, 178 `.rvz`: 100% readable at `0x58` with the version checked. `wii`, 40 `.rvz` and
13 `.wad`: 75.5%. `psp` (147 `.cso`, 7 `.chd`), `ps3` (23 `.dec.iso`) and `psx` (386 `.chd`):
0%. No constant offset reaches into a `.cso`, a `.chd` or an ISO9660 image. `RomGameId` serves
GameCube and Wii, and PSP attribution goes through the journal and the sidecar (RB-145).

## RB-144. `PARAM.SFO` adds nothing the directory name does not

Verified: RetroBat 8.2.0, 2026-08-17. How: parsed the `PARAM.SFO` of every `psp` save directory on a real install.
Its keys are `SAVEDATA_DIRECTORY`, which is the directory's own name, and `TITLE`, a human string
(`'echochrome'`, `'The 3rd Birthday'`). Parsing it buys a fuzzy title match, never an exact key,
so RomMBat does not read it.

## RB-145. A state's `.txt` sidecar joins a ROM to its native save key

Verified: RetroBat 8.2.0, 2026-08-17. How: read the sidecars beside `ppsspp` states on a real install and joined them to `SAVEDATA/`.
`ppsspp/3rd Birthday, The (Europe).txt` holds `ULES01513_1.00`. Its `ULES01513` prefix joins
`SAVEDATA/ULES01513SYSDATA`, and the stem resolves through `RomIndex`. The route needs no ROM
read and no observed launch, and it covers only games that have a state. It is one of
`GameIdAttributor`'s three routes, beside the launch journal and the ROM header (RB-143).

## RB-153. MAME's `nvram` directories are named by short name

Verified: RetroBat 8.2.0, 2026-08-17. How: listed `saves/mame/nvram/` on a real install against its `roms/mame`.
1,231 unit directories with well-formed short names (`1944`, `19xx`, `1on1gov`, `20pacgal`),
against 3 `.zip` files in `roms/mame`, so nothing joined. A MAME set names each archive after its
short name, so the basename is the key, but this library cannot demonstrate the join.

## RB-253. A system's save shape depends on the emulator

Verified: RetroBat 8.2.1, 2026-09-13. How: drove all nine `nes` rows from EmulationStation, confirming each emulator from `emulatorLauncher.log`.
Three libretro cores share one `saves/nes/<rom>.srm`, so switching core continues the same save,
measured on Kirby's Adventure under `nestopia` then `fceumm`. The other six rows write four other
shapes in their own subdirectories or under other extensions. So `nes` is class A on libretro and
on nothing else, and shape belongs to `(system, emulator)`, as `save_shapes.json`'s `_note` says.

## RB-254. BizHawk names a battery save after the game's display name

Verified: RetroBat 8.2.1, 2026-09-13. How: launched `StarTropics (USA).zip` under BizHawk and read what it wrote.
It wrote `saves/nes/bizhawk/StarTropics.SaveRAM`, dropping the region tag, which no ROM filename
yields. Its own state sidecar spells the convention out as `StarTropics.NesHawk`, so the mapping
is recoverable from the tree. `DisplayNameAttributor` joins it through that sidecar or a BizHawk
launch, and refuses a title two ROMs answer to.

## RB-139. BizHawk's display name can abbreviate a region tag

Verified: RetroBat 8.2.0, 2026-08-17. How: launched `mastersystem`'s Phantasy Star (Brazil) under BizHawk on the `K:` install, made a state, and read what it wrote.
`Phantasy Star (Brazil).zip` produced `bizhawk/Phantasy Star (B).SaveRAM`. So a BizHawk stem can
differ from the ROM's in more than a dropped tag, and joining goes through RB-254's routes, never
the filename.

## RB-404. A launch alone writes a battery save, and its content can pass for a real one

Verified: RetroBat 8.2.0, 2026-08-11. How: booted Phantasy Star (Brazil) under `libretro` `genesis_plus_gx` to its title screen, pressed nothing, and read `saves/mastersystem/` during the run and after exit.
A 65,536 B `<rom>.srm` appeared while the game ran and an 8,188 B one after a clean exit.
RetroBat's `retroarch.cfg` sets `autosave_interval = "10"`, so the file lands within seconds of
boot and survives a crash. It held the cartridge formatting its own backup RAM, legible ASCII
and 35 distinct byte values, so no size floor and no all-`0x00` or all-`0xFF` test separates it
from a player's save. Only a known earlier `content_hash` does, and a first save seen with no
baseline is not evidence of play.
