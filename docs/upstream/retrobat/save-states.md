---
summary: What `es_savestates.cfg` declares against what emulators write: directories, slots, screenshots and `.txt` sidecars.
read-when: Before reading `es_savestates.cfg`, deriving a state slot, or syncing a state's companions.
---

# RetroBat: save states

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-360. `es_savestates.cfg` declares 13 emulators, and its attributes carry four traps

Verified: RetroBat 8.2.0, 2026-08-08, and 8.2.1, 2026-08-25. How: diffed the live file against the copy vendored in `reference/`, on each build.
The live file is byte-identical to `reference/es_savestates.cfg`, and 8.2.1 left it unchanged.
Its 13 emulators are all of RetroBat's machine-readable state knowledge. `libretro` and `bizhawk`
are both core-scoped (`{{system}}/bizhawk/sstates/{{core}}`), so each core keeps its own state
set for a game.

| Emulator   | Trap                                                                                                                                                             |
| ---------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `libretro` | No `firstslot` or `lastslot` at all                                                                                                                              |
| `desmume`  | `<image>` and `<file>` are the same template, `{{romfilename}}.ds{{slot0}}`                                                                                      |
| `bigpemu`  | `firstslot="001"` and `lastslot="999"` against a two-digit `{{slot2d}}`. The bounds describe BigPEmu's native naming and the template RetroBat's mirror (RB-166) |
| 5 others   | No `autosave` or `incremental` attribute, so both are unknown rather than false                                                                                  |

`SaveStateSchema` reads a slot off the filename on disk and never expands the bounds, so a
missing bound costs nothing and a slot outside them is reported as a near miss rather than
refused. `StateScanner` sends no `<image>` whose name equals the state's. The file also ships a
commented-out `<core>` override and `<defaultCoreDirectory>`, which a user can enable.

## RB-216. The `libretro` state folder is `libretro.<core>`, set by the launcher

Verified: RetroBat 8.2.1, 2026-08-25. How: read the generated `retroarch.cfg` and the state folders already on disk, read-only.
`emulatorlauncher` writes all four RetroArch sort keys (`sort_savefiles_enable`,
`sort_savefiles_by_content_enable`, `sort_savestates_enable`,
`sort_savestates_by_content_enable`) as `"false"` on every launch and puts the core in
`savestate_directory` instead. Four cores on disk agree: `mastersystem/libretro.genesis_plus_gx`,
`mastersystem/libretro.picodrive`, `psx/libretro.mednafen_psx_hw` and `ports/libretro.2048`.
That is the `{{system}}/libretro.{{core}}` `es_savestates.cfg` declares, so RomMBat reads the
manifest and never the sort keys, whose absent default misplaces states on other front ends.

## RB-217. `retroarch.cfg` describes only the last game launched

Verified: RetroBat 8.2.1, 2026-08-25. How: read the generated `retroarch.cfg` after a `mastersystem` session.
It named `mastersystem` throughout, `savefile_directory` included, because the launcher
regenerates it per launch. So it is no description of the install, and RomMBat never reads it
for one. This is rule 2 seen from the reading side.

## RB-368. `<file>` holds for every emulator driven, and a manual save mirrors live

Verified: RetroBat 8.2.0, 2026-08-08. How: snapshotted the whole `saves/<system>` subtree around a real save state in each emulator.

| Emulator      | System   | `<file>` written | `<image>` | `.txt` sidecar holds         | Written |
| ------------- | -------- | ---------------- | --------- | ---------------------------- | ------- |
| `libretro`    | snes     | `.state1`        | 1,163 B   | none                         | live    |
| `ppsspp`      | psp      | `_0.ppst`        | racy      | `UCES00995_1.00`             | live    |
| `duckstation` | psx      | `_resume.sav`    | absent    | `SLUS-00404`                 | at exit |
| `pcsx2`       | ps2      | `.resume.p2s`    | 183 KB    | `SLUS-20265 (79646C72)`      | at exit |
| `dolphin`     | gamecube | `.s01`           | absent    | `GW7E69`                     | live    |
| `gopher64`    | n64      | `.state0`        | absent    | `TWINE-72E3E7B4...` (sha256) | live    |

Four more are in RB-370, and `flycast`, whose declared directory 8.2.0 left empty, is in
RB-343. The declared `<file>` was right for every emulator driven, so
`StateScanner` attributes a state by its `{{romfilename}}` stem with no Game ID. A manual save
reaches the declared directory about 120 ms after the keypress, while the emulator runs. An
autosave state, driven here through `<system>.autosave=1`, appears only at exit, and the
`autosave_file` and `autosave_image` templates both matched. `libretro` needs no mirror, because
the launcher points RetroArch at the declared path. The `<image>` is missing more often than
not, so RomMBat treats it as best-effort everywhere (RB-367).

## RB-371. openMSX writes its states under `bios/`, not the declared directory

Verified: RetroBat 8.2.0, 2026-08-08. How: installed openMSX on demand and made two real saves from its console.
The declaration is `saves/msx1/openmsx/<rom filename>_<slot>.oms`. openMSX wrote
`bios/openmsx/savestates/quicksave.oms` and a real 7,531 B `quicksave.png`, because RetroBat
puts its whole user-data directory under `bios/openmsx/`, and the declared directory stayed
empty. The saves took openMSX's default name rather than the `[guess_title]_0` RetroBat's
`kbhotkeys.tcl` binds to Alt+F2, so whether RetroBat mirrors a state saved under its own naming
is unmeasured. Nothing in RomMBat scans `bios/openmsx/`, so openMSX states are neither synced
nor reported, and the scan counts none of them. `StateScanner.WrongDeclaredDirectories` names
the trap for tests and gates nothing.

## RB-135. The slot a save-state key writes is neither 0 nor fixed

Verified: RetroBat 8.2.0, 2026-08-17. How: made a state on `mastersystem`/Phantasy Star (Brazil) under four emulators and read the names written.
`libretro` wrote `.state1` and BizHawk wrote `.QuickSave2.State`. Expanding
`firstslot..lastslot` would have found neither. RomMBat reads the slot off the filename, and
RB-261 and RB-269 say where each emulator takes it from.

## RB-261. RetroArch picks a `libretro` state's slot, not ES's `-state_slot`

Verified: RetroBat 8.2.1, 2026-09-21. How: launched `nes` under `libretro`/`mesen` with `-state_slot 5` and read `es_launch_stdout.log`.
Two states made in the session landed as `.state1` and `.state2`. RetroArch's log says why:
`found_last_state_slot: #0` against an empty `saves/nes/libretro.mesen/`, then `Saving state`
for each. The generated `retroarch.cfg` sets `savestate_auto_index = "true"`, which continues
from the highest slot on disk for that game and core. So RB-258's `nestopia` pass, `-state_slot 2`
and states 2 to 4, followed a slot 1 already on disk. Where ES's number comes from is
unmeasured, and nothing in `src/` reads `-state_slot`. For a `libretro` row, read the slot from
the `Saving state` lines or the file on disk.

## RB-269. ES's `-state_slot` does pick a `bizhawk` state's slot

Verified: RetroBat 8.2.1, 2026-09-21. How: launched `nes` under `bizhawk` with and without `-state_slot`, and read `config.ini` and the states written.
`emulatorLauncher` writes it into EmuHawk's `config.ini` as `SaveSlot`. `-state_slot 5` left
`SaveSlot: 5` and the pad's save key wrote `QuickSave5`; no `-state_slot` left `SaveSlot: 10`
and the same key wrote `QuickSave0`. The pad key is `F2` sent by pad-to-key, so it saves to the
current slot. In game, `Ctrl+F1` to `Ctrl+F10` save to slots 1 to 10, and `F6` and `F7` step
the slot. `Shift+F1` loads slot 1, so the save key other emulators use loads a state here. EmuHawk reads DirectInput, so a key sent by `WScript.Shell.SendKeys` is ignored and
one sent by `keybd_event` with the hardware scan code is not. For a `bizhawk` row, read the slot
from the file on disk.

## RB-367. PPSSPP's mirrored screenshot races the emulator

Verified: RetroBat 8.2.0, 2026-08-08. How: made three real PSP states and timed each file the watcher and PPSSPP wrote.

| Save                        | ES-facing `.jpg` | Native `.jpg` |
| --------------------------- | ---------------- | ------------- |
| 3rd Birthday (pre-existing) | 0 B              | 120,539 B     |
| Patapon, first run          | 37,927 B         | 37,927 B      |
| Patapon, second run         | absent           | 37,927 B      |

The watcher finished its copy at `.416` and PPSSPP wrote its screenshot at `.431`, so the
mirror catches the file whole, truncated or not at all. The `.ppst` was right every time.
`StateScanner` skips a missing or zero-byte `<image>` and sends the state without one. The
native `PPSSPP_STATE/` copy was right all three times, but finding it needs the `.txt` mapping.

## RB-268. BizHawk writes no `<image>`, and keeps the screenshot inside the state

Verified: RetroBat 8.2.1, 2026-09-21. How: made five states across `NesHawk` and `quickerNES` and unzipped them.
The declared `{{romfilename}}.QuickSave{{slot0}}.png` never appears, in the declared directory
or in `emulators/bizhawk/sstates/nes/`. A `.State` is a zip of `BizState 1.0`, `BizVersion.txt`,
`Core.bin.zst`, `SyncSettings.json` and `Framebuffer.bmp`, the 256x224 frame at the moment of
saving. So a state that round-trips brings its screenshot with it, RomM holds no screenshot row
for it, and the restore preview's `no screenshot: the server links none to this state` is right.

## RB-136. A `.txt` sidecar's presence means nothing, and only its content is useful

Verified: RetroBat 8.2.0, 2026-08-17. How: made a state on `mastersystem`/Phantasy Star (Brazil) under four emulators and read every file beside it.
`libretro` wrote no sidecar under either core. `jgenesis` wrote the plain ROM filename, and
`bizhawk` wrote `Phantasy Star (B).SMSHawk`, its own truncated title plus the core. Elsewhere it
holds the emulator's native name: PPSSPP's `UCES00995_1.00`, DuckStation's `SLUS-00404`,
Dolphin's `GW7E69` (RB-368). `StateScanner.ReadNativeName` keeps the content as a hint, where
a serial is the Game ID a directory save otherwise reads from a ROM header.

## RB-270. A BizHawk state saved outside `emulatorLauncher` is lost at the next ES launch

Verified: RetroBat 8.2.1, 2026-09-21. How: saved a state in EmuHawk started directly with `--lua`, then launched the same game through ES.
BizHawk writes natively to `emulators/bizhawk/sstates/<system>/`, outside `saves/` and not
core-scoped, and the launcher mirrors that to the declared core-scoped directory. EmuHawk started
directly wrote `Destiny of an Emperor.NesHawk.QuickSave2.State` natively and nothing reached
`saves/nes/bizhawk/sstates/NesHawk/`. The next launch through `emulatorLauncher` rebuilt the
native directory from the declared one, and that state was gone. Launched through the launcher,
the same save mirrored within the second, sidecar included. Its native `.State.rap` sibling is
never mirrored. So the declared path is authoritative in both directions, and RomMBat reads
only it.

## RB-134. Two `libretro` cores write the same state name for one ROM

Verified: RetroBat 8.2.0, 2026-08-17. How: made a state under `genesis_plus_gx` and `picodrive` on one `mastersystem` ROM and flushed both.
Both wrote `Phantasy Star (Brazil).state1`, 9,202 B and 6,282 B, in their own `libretro.<core>`
folders. They landed as two RomM rows because RomMBat puts the core in the uploaded name.
Without it one would replace the other.

## RB-251. An emulator with no `es_savestates.cfg` entry still writes save states

Verified: RetroBat 8.2.1, 2026-09-13. How: drove every emulator `nes` declares from ES and diffed `saves/nes/` around each state.
`mednafen` wrote `saves/nes/mednafen/sstates/<rom>.<md5>.mc0`, `mesen` wrote
`saves/nes/mesen/SaveStates/<rom>_1.mss` and `ares` wrote `saves/nes/ares/Famicom/<rom>.bs1`,
and nothing in the file hints at any of them. `StateScanner` finds states only through a
declaration, so RomMBat declares rows it has driven in
`data/retrobat/es_savestates.supplement.xml`. A state directory neither file declares is counted
as unsyncable and named under `no_state_declaration`.
