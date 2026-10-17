---
summary: How each emulator RetroBat offers for `pcengine` behaves, measured, with RB- IDs.
read-when: Before certifying a `pcengine` row or changing how RomMBat handles a `pcengine` save, state or firmware.
---

# pcengine: emulator facts

Facts RomMBat relies on, one per heading. [The upstream reference](../../upstream/README.md) says what an entry holds and how IDs are kept.

## RB-407. mednafen cannot load a PC Engine save of Mesen's, and will not start the game while one is there

Verified: RetroBat 8.2.1, 2026-10-02. Carried: RetroBat 8.3.1.1, 2026-10-17, 8.3.1.1's delta is ten RetroBat and fourteen emulatorlauncher commits (screen placement and Wiimote buttons across the generators, the doom and recomp ports systems, CHIHIRO's .bin extension and BIOS name, a QuiverLauncher padtokey entry, the order of emulator entries in `es_systems.cfg`); its only pcengine change is mesen moving ahead of ares in `es_systems.cfg`'s order, which moves no save, state or BIOS path. How: on `R:`, removed mednafen's hashed `.sav` for Populous (Japan) (En) and left the 2,048 B `<rom>.sav` Mesen had written, launched `-emulator mednafen -core pce` through `emulatorLauncher`, and read the error dialog from a screenshot and `emulators/mednafen/stdout.txt`.

On 8.2.1, mednafen 1.32.1, with Mesen's 2,048 B `Populous (Japan) (En).sav` beside the ROM and no hashed file, mednafen opened the plain name and stopped with a dialog: "Save game memory file ... is an incorrect size(2048 bytes). The correct size is 32768 bytes." The game never started. Its own hashed `<rom>.9d599a43d2c69738f3562f58aeff8828.sav` is 32,768 B, the same bytes the `libretro` cores and ares keep in `.srm` and `.ram`. Same class as **324** on `mastersystem` and **289** on `gba`
