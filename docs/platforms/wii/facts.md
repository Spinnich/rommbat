---
summary: How each emulator RetroBat offers for `wii` behaves, measured, with RB- IDs.
read-when: Before certifying a `wii` row or changing how RomMBat handles a `wii` save, state or firmware.
---

# wii: emulator facts

Facts RomMBat relies on, one per heading. [The upstream reference](../../upstream/README.md) says what an entry holds and how IDs are kept.

## RB-146. A Wii disc game's save unit is its `title/00010000/<hex>/` tree, and the rest of the NAND is system state

Verified: RetroBat 8.2.0, 2026-08-17. Carried: RetroBat 8.3.1.1, 2026-10-17, 8.3.1.1's delta is ten RetroBat and fourteen emulatorlauncher commits (screen placement and Wiimote buttons across the generators, the doom and recomp ports systems, CHIHIRO's .bin extension and BIOS name, a QuiverLauncher padtokey entry, the order of emulator entries in es_systems.cfg); Dolphin.Generator.cs changes only its version check, and no wii save, NAND or BIOS path moved. How: recovered from the findings ledger, which recorded it during M6 stage 2b (1a5503b) on the 8.2.0 install its header names; the method is the text below.
`title/00010000/<hex>/` is the disc-game tree and the hex is the ASCII game code (`52534245` = `RSBE`), which joins exactly to what route 2 reads at `0x58`. `title/00000001/*` is system titles, and `shared2/`, `sys/` and `fst.bin` are system state. A title with `content/title.tmd` and no `data/` is an installed stub, not a save
