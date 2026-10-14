---
summary: What `es_systems.cfg` declares, how system names relate to folders, and how RetroBat libraries name their discs.
read-when: Before reading `es_systems.cfg`, mapping a system to its folder, or parsing a disc marker.
---

# RetroBat: systems and ROMs

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-67. The live `es_systems.cfg` declares the shipped template's 245 systems and 241 ROM folders

Verified: RetroBat 8.3.0, 2026-10-14. How: parsed the live file and the shipped template, and matched their `<path>` folders against `systems_names.lst`.
Both files hold 245 `<system>` elements, and both own the same 241 folders under `roms/` that
`systems_names.lst` names. Every system has a non-empty `<extension>`, 340 distinct across the
file. RomMBat still reads the live file and never the copy in `reference/`, because a user's
emulator choices can change it (rule 3). `reference/verify.py` asserts that `systems_names.lst`
holds 241 names, and `EsSystemsFileTests` that the shipped file's folders are exactly those.

## RB-68. `<name>` is not the folder

Verified: RetroBat 8.2.0, 2026-08-10, 8.2.1, 2026-09-27, and 8.3.0, 2026-10-14. How: compared each `<name>` with the last segment of its `<path>`.
Five systems disagree: `gw`/`gameandwatch`, `powerbomberman`/`pb`, `casloopy`/`loopy` and
`Windows`/`windows`, and `starship` is the `<name>` of both `ghostship` and `starship`. Keying on
`<name>` loses one system and mismatches four more, so `EsSystemsFile` takes the folder from
`<path>`.

## RB-69. Four systems are not sync targets

Verified: RetroBat 8.2.0, 2026-08-10, 8.2.1, 2026-09-27, and 8.3.0, 2026-10-14. How: resolved every `<path>` in the live file.
`library` and `screenshots` point outside `roms/`, `retrobat` is `system/es_menu`, which carries
the `.menu` entries, and `mess` declares no path at all. `EsSystemsFile` filters on the resolved
path, not on a list of names, and keeps the four as `NonRomSystems`, which nothing reads yet.

## RB-70. `arcade` and `kodi` are inside XML comments

Verified: RetroBat 8.3.0, 2026-10-14. How: counted `<system>` with a regex and with an XML parser.
A regex over `<system>` finds 247, including both. An XML parser finds 245 and neither, which is
why `EsSystemsFile` loads the file with `XDocument`.

## RB-173. A disc marker is always `(Disc N)`, and text can follow it

Verified: RetroBat 8.2.0, 2026-08-24. How: swept every filename under a real install's `roms/`, read-only.
202 files carry a marker, across `psx`, `saturn`, `dreamcast`, `gamecube`, `3do` and `ps2`, and
every one is `(Disc N)` with N numeric. No `(Disk`, `(CD` or `(Side` appears. 53 of the 202
carry text after the marker, such as `(Rev 1)`, `(Unl)` and translation tags. So `DiscSet` takes a
set's base title from the text before the marker, never from the stem with the marker cut out,
and still matches the other forms.

## RB-431. An in-place upgrade to 8.3.0 renames `roms/gzdoom` to `roms/doom`, contents and all

Verified: RetroBat 8.3.0, 2026-10-14. How: ran RetroBat's `es-update.exe` on an 8.2.1 tree holding one game under `gzdoom`, read `es-update.log`, then synced with the 8.3.0 build.
8.3.0 renames the `gzdoom` system to `doom`. `es-update.exe` applies the update archive's
`system/upgrade.xml`, which the setup.exe's payload does not carry: it moves `roms\doom` to
`roms\doom.backup`, then `roms\gzdoom` to `roms\doom`, with the game, `gamelist.xml` and media
inside. `saves\` is not moved. Once RomM's `doom` resolves to the new folder, a sync adopts the
moved file by content and downloads nothing, but the row under `gzdoom` stays tracked beside it
(#518).
