---
summary: How each emulator RetroBat offers for `wii` behaves, measured, with RB- IDs.
read-when: Before certifying a `wii` row or changing how RomMBat handles a `wii` save, state or firmware.
---

# wii: emulator facts

Facts RomMBat relies on, one per heading. [The upstream reference](../../upstream/README.md) says what an entry holds and how IDs are kept.

## RB-146. Decided from data

Previously: Wii's NAND "is not all attributable" and what counts as a save unit is open (plan)

Measurement says: **Decided from data.** `title/00010000/<hex>/` is the disc-game tree and the hex is the ASCII game code (`52534245` = `RSBE`), which joins exactly to what route 2 reads at `0x58`. `title/00000001/*` is system titles, and `shared2/`, `sys/` and `fst.bin` are system state. A title with `content/title.tmd` and no `data/` is an installed stub, not a save
