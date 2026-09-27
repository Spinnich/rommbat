---
summary: What `es_systems.cfg` declares, how system names relate to folders, and how RetroBat libraries name their discs.
read-when: Before reading `es_systems.cfg`, mapping a system to its folder, or parsing a disc marker.
---

# RetroBat: systems and ROMs

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-67. Wrong comparison

Previously: The live `es_systems.cfg` carries four systems upstream does not (probe 5, "Other observations")

Measurement says: **Wrong comparison.** That put the live file's 244 `<system>` elements next to `systems_names.lst`'s 240 folder **names**. The shipped template also has 244 active systems, and both files own exactly the same 240 folders under `roms/`. The live file matches upstream

## RB-68. `<name>` is not the folder

Previously: `es_systems.cfg` `<name>` identifies the system (plan L918-928, `retrobat-layout`)

Measurement says: **`<name>` is not the folder.** Five systems disagree in the shipped file: `gw`/`gameandwatch`, `powerbomberman`/`pb`, `casloopy`/`loopy`, `Windows`/`windows`, and `starship` is used **twice**, for `ghostship` and `starship`. Take the folder from `<path>`

## RB-69. Whether every `<system>` is a sync target

Previously: (not addressed) whether every `<system>` is a sync target

Measurement says: Four own no folder under `roms/` (`library`, `screenshots`, `kodi`, and `retrobat` at `system/es_menu`) and `mess` declares no path at all. Filter on the resolved path, not on a list of names

## RB-70. `arcade` and `kodi` appear in the shipped `es_systems.cfg`

Previously: (not addressed) `arcade` and `kodi` appear in the shipped `es_systems.cfg`

Measurement says: Both are inside XML comments. A regex over `<system>` finds them; an XML parser correctly does not

## RB-173. `(Disc N)`, always, N numeric, 202 files across `psx`, `saturn`, `dreamcast`, `gamecube`, `3do` and `ps2`

The claim being checked: (not addressed) how a disc marker is written

What was measured: **`(Disc N)`, always, N numeric, 202 files across `psx`, `saturn`, `dreamcast`, `gamecube`, `3do` and `ps2`.** No `(Disk`, `(CD` or `(Side` appears. **53 of the 202 carry text after the marker** (`(Rev 1)`, `(Unl)`, translation tags), the filename-side twin of the 130 subtitled `gamedb` stems in F18, so a set's base title is the text **before** the marker and never the stem with the marker cut out of its middle

## RB-402. Other observations

- The live `es_systems.cfg` **differs** from the copy vendored in `reference/`, as expected
  for a per-install generated file. It declares **244 systems and 1176 distinct file
  extensions**, and every single system has a non-empty `<extension>`. The vendored upstream
  copy declares **240**, so a live install carries four systems upstream does not. That gap
  is exactly why rule 3 exists: read extensions from the live file, never from a bundled
  table. `reference/verify.py` continues to assert 240 against the vendored file, which is
  correct and should not be changed to match the live number.
- `es_systems.cfg` uses `~\..\roms\<system>` for `<path>` and `%HOME%\emulatorLauncher.exe`
  in `<command>`, so RetroBat itself already avoids absolute paths in its primary config.
  That is a good sign for probe 7 but is not yet a full audit.
- The `game-start`, `game-end`, `quit`, `reboot`, `shutdown`, `sleep` and `wake` hook
  directories all exist and are **empty** in a stock install. Only `start/` and
  `update-gamelists/` ship a script, both `updatestores.bat`. Installing hooks is therefore
  a pure addition in the common case, but the append-don't-replace rule still matters for
  those two events.
