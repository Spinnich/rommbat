---
summary: What RetroBat ships for firmware and who owns `bios/`.
read-when: Before fetching or placing firmware.
---

# RetroBat: BIOS

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-379. The firmware manifest is a string resource inside `batocera-systems.exe`, not a file

Verified: RetroBat 8.3.0, 2026-10-14. Carried: RetroBat 8.3.1.1, 2026-10-17, 8.3.1.1's delta is ten RetroBat and fourteen emulatorlauncher commits (screen placement and Wiimote buttons across the generators, the doom and recomp ports systems, CHIHIRO's .bin extension and BIOS name, a QuiverLauncher padtokey entry, the order of emulator entries in `es_systems.cfg`) and EmulationStation's window holds no commit on events, settings keys or the HTTP API on Windows; its one BIOS change is chihiro's ic10_g24lc64 becoming ic10_g24lc64.bin in batocera-systems.json, and no BIOS folder, name or hash the fact names moved. How: searched the install for the file name, then compared the executable's embedded resource with `reference/batocera-systems.json`.
No file named `batocera-systems.json` exists anywhere in an install. The data is a .NET string
resource named `batocera_systems` inside `emulationstation/batocera-systems.exe`, at offset 7,250
of the 51,200-byte executable, and matches the vendored copy byte for byte apart from its
trailing newline: 101 systems and 360 entries, 186 of them with an empty md5. With no live copy
to read, RomMBat bundles the manifest at `data/retrobat/bios.json` rather than reading a
build-specific resource layout out of the executable at runtime.

## RB-380. `bios/` is a shared tree, and almost none of it is firmware

Verified: RetroBat 8.2.0, 2026-08-16. Carried: RetroBat 8.3.1.1, 2026-10-17, 8.3.1.1's delta is ten RetroBat and fourteen emulatorlauncher commits (screen placement and Wiimote buttons across the generators, the doom and recomp ports systems, CHIHIRO's .bin extension and BIOS name, a QuiverLauncher padtokey entry, the order of emulator entries in `es_systems.cfg`) and EmulationStation's window holds no commit on events, settings keys or the HTTP API on Windows; its one BIOS change is chihiro's ic10_g24lc64 becoming ic10_g24lc64.bin in batocera-systems.json, and no BIOS folder, name or hash the fact names moved. How: walked `bios/` on an install RomMBat had not yet written to, and hashed every path the manifest names.
Before RomMBat writes anything, `bios/` holds 4,683 files and 373 MB, nearly all of it emulator
data: `dolphin-emu/` 2,508 files, `mame/` 858, `nxengine/` 436, `Machines/` 296, `scummvm/` 208,
`PPSSPP/` 167, `dinothawr/` 144, 6 flat at `bios/` and 60 across 15 more subfolders. Three files
sat at a path the manifest names with the md5 it names, and none at a named path with a
different md5. `mame/hash/` holds 776 software-list XML files, whose manifest entries carry no
md5, and openMSX keeps its whole user-data directory here, save states included (RB-371).
RomMBat therefore never overwrites or deletes anything under `bios/` that it did not download.
