---
summary: How each emulator RetroBat offers for `pcengine` behaves, measured, with RB- IDs.
read-when: Before certifying a `pcengine` row or changing how RomMBat handles a `pcengine` save, state or firmware.
---

# pcengine: emulator facts

Facts RomMBat relies on, one per heading. [The upstream reference](../../upstream/README.md) says what an entry holds and how IDs are kept.

## RB-407. Not Mesen's, and it will not start the game while one is there

Question: Whether mednafen opens a PC Engine save another emulator wrote

Measured: **Not Mesen's, and it will not start the game while one is there.** On 8.2.1, mednafen 1.32.1, with Mesen's 2,048 B `Populous (Japan) (En).sav` beside the ROM and no hashed file, mednafen opened the plain name and stopped with a dialog: "Save game memory file ... is an incorrect size(2048 bytes). The correct size is 32768 bytes." The game never started. Its own hashed `<rom>.9d599a43d2c69738f3562f58aeff8828.sav` is 32,768 B, the same bytes the `libretro` cores and ares keep in `.srm` and `.ram`. Same class as **324** on `mastersystem` and **289** on `gba`
