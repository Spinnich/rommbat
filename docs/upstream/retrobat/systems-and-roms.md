---
summary: What `es_systems.cfg` declares, how system names relate to folders, and how RetroBat libraries name their discs.
read-when: Before reading `es_systems.cfg`, mapping a system to its folder, or parsing a disc marker.
---

# RetroBat: systems and ROMs

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-67. The live `es_systems.cfg` declares the shipped template's 244 systems and 240 ROM folders

Verified: RetroBat 8.3.1.1, 2026-10-17. How: parsed the live file on the agent tree and the shipped template in the pristine 8.3.1.1 install, and matched their `<path>` folders against `systems_names.lst`.
Both files hold 244 `<system>` elements, and both own the same 240 folders under `roms/` that
`systems_names.lst` names. Every system has a non-empty `<extension>`, 340 distinct across the
file. RomMBat still reads the live file and never the copy in `reference/`, because a user's
emulator choices can change it (rule 3). `reference/verify.py` asserts that `systems_names.lst`
holds 240 names, and `EsSystemsFileTests` that the shipped file owns 240 folders and differs
from a live capture only where a release renamed or added one.

## RB-68. `<name>` is not the folder

Verified: RetroBat 8.2.0, 2026-08-10, 8.2.1, 2026-09-27, and 8.3.0, 2026-10-14. Carried: RetroBat 8.3.1.1, 2026-10-17, 8.3.1.1's delta is ten RetroBat and fourteen emulatorlauncher commits (screen placement and Wiimote buttons across the generators, the doom and recomp ports systems, CHIHIRO's .bin extension and BIOS name, a QuiverLauncher padtokey entry, the order of emulator entries in `es_systems.cfg`) and EmulationStation's window holds no commit on events, settings keys or the HTTP API on Windows; systems_names.lst loses n64recomp and prboom and gains recomp, and no extension list the fact names changed but CHIHIRO's. How: compared each `<name>` with the last segment of its `<path>`.
Five systems disagree: `gw`/`gameandwatch`, `powerbomberman`/`pb`, `casloopy`/`loopy` and
`Windows`/`windows`, and `starship` is the `<name>` of both `ghostship` and `starship`. Keying on
`<name>` loses one system and mismatches four more, so `EsSystemsFile` takes the folder from
`<path>`.

## RB-69. Four systems are not sync targets

Verified: RetroBat 8.2.0, 2026-08-10, 8.2.1, 2026-09-27, and 8.3.0, 2026-10-14. Carried: RetroBat 8.3.1.1, 2026-10-17, 8.3.1.1's delta is ten RetroBat and fourteen emulatorlauncher commits (screen placement and Wiimote buttons across the generators, the doom and recomp ports systems, CHIHIRO's .bin extension and BIOS name, a QuiverLauncher padtokey entry, the order of emulator entries in `es_systems.cfg`) and EmulationStation's window holds no commit on events, settings keys or the HTTP API on Windows; systems_names.lst loses n64recomp and prboom and gains recomp, and no extension list the fact names changed but CHIHIRO's. How: resolved every `<path>` in the live file.
`library` and `screenshots` point outside `roms/`, `retrobat` is `system/es_menu`, which carries
the `.menu` entries, and `mess` declares no path at all. `EsSystemsFile` filters on the resolved
path, not on a list of names, and keeps the four as `NonRomSystems`, which nothing reads yet.

## RB-70. `arcade` and `kodi` are inside XML comments

Verified: RetroBat 8.3.1.1, 2026-10-17. How: counted `<system>` in the vendored `reference/es_systems.cfg` with a regex and with an XML parser, and listed the systems inside comments.
A regex over `<system>` finds 246, including both. An XML parser finds 244 and neither, which is
why `EsSystemsFile` loads the file with `XDocument`.

## RB-173. A disc marker is always `(Disc N)`, and text can follow it

Verified: RetroBat 8.2.0, 2026-08-24. Carried: RetroBat 8.3.1.1, 2026-10-17, 8.3.1.1's delta is ten RetroBat and fourteen emulatorlauncher commits (screen placement and Wiimote buttons across the generators, the doom and recomp ports systems, CHIHIRO's .bin extension and BIOS name, a QuiverLauncher padtokey entry, the order of emulator entries in `es_systems.cfg`) and EmulationStation's window holds no commit on events, settings keys or the HTTP API on Windows; systems_names.lst loses n64recomp and prboom and gains recomp, and no extension list the fact names changed but CHIHIRO's. How: swept every filename under a real install's `roms/`, read-only.
202 files carry a marker, across `psx`, `saturn`, `dreamcast`, `gamecube`, `3do` and `ps2`, and
every one is `(Disc N)` with N numeric. No `(Disk`, `(CD` or `(Side` appears. 53 of the 202
carry text after the marker, such as `(Rev 1)`, `(Unl)` and translation tags. So `DiscSet` takes a
set's base title from the text before the marker, never from the stem with the marker cut out,
and still matches the other forms.

## RB-431. An in-place upgrade to 8.3.0 renames `roms/gzdoom` to `roms/doom`, contents and all

Verified: RetroBat 8.3.0, 2026-10-14. Carried: RetroBat 8.3.1.1, 2026-10-17, 8.3.1.1's system/upgrade.xml adds one neveroverwrite line (quiverlauncher/settings.json) and no roms move, so the gzdoom-to-doom rename measured on 8.2.1 is untouched; how an in-place upgrade to 8.3.1.1 handles prboom and n64recomp is unmeasured. How: ran RetroBat's `es-update.exe` on an 8.2.1 tree holding one game under `gzdoom`, read `es-update.log`, then synced with the 8.3.0 build.
8.3.0 renames the `gzdoom` system to `doom`. `es-update.exe` applies the update archive's
`system/upgrade.xml`, which the setup.exe's payload does not carry: it moves `roms\doom` to
`roms\doom.backup`, then `roms\gzdoom` to `roms\doom`, with the game, `gamelist.xml` and media
inside. `saves\` is not moved. Once RomM's `doom` resolves to the new folder, a sync adopts the
moved file by content and downloads nothing, but the row under `gzdoom` stays tracked beside it
(#518).

## RB-438. 8.3.1.1 nests `play` and `pcsx2-16` inside `ps2`'s `libretro` emulator

Verified: RetroBat 8.3.1.1, 2026-10-17. How: read `system/templates/emulationstation/es_systems.cfg` and the live copy in the pristine 8.3.1.1 install, and parsed it with `EsSystemsFile`.
The `ps2` system's `libretro` element is not closed after its `<cores>`: `<emulator name="play"/>` and `<emulator name="pcsx2-16"/>` sit inside it, and the single `</emulator>` that follows closes `libretro`. `EsSystemsFile` reads an `<emulators>` element's direct children only, so it lists `pcsx2` and `libretro` for `ps2`, and that is harmless because the only reader of the list takes the first entry as the default (`LaunchEmulator`). Whether EmulationStation offers `play` and `pcsx2-16` in its menu was not observed. The launcher still starts both when handed `-emulator play` or `-emulator pcsx2-16` (RB-435). The certification data reads the emulators at any depth, so the two rows stay in `ps2`'s order.
