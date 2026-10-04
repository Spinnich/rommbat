---
summary: How each emulator RetroBat offers for `ps3` behaves, measured, with RB- IDs.
read-when: Before certifying a `ps3` row or changing how RomMBat handles a `ps3` save, state or firmware.
---

# ps3: emulator facts

Facts RomMBat relies on, one per heading. [The upstream reference](../../upstream/README.md) says what an entry holds and how IDs are kept.

## RB-124. The count is of the emulator's data root, not of saves

The claim being checked: RPCS3's 32,451 files make any recursive content hash a performance problem (plan)

Measurement says: **The count is of the emulator's data root, not of saves.** `saves/ps3/rpcs3` is 32,451 files and **52.87 GB**, hashing in **426 s warm and 512 s cold**, but that is `dev_hdd0` entire. The savedata subtree is **17 directories, 77 files, 16.3 MB, 0.06 s**. So the input is "scope the save unit in the shape definition", not "budget the hash"

## RB-142. RPCS3's data root hashes in 426.07 s and its savedata subtree in 0.06 s

Re-run rather than inherited: the data root is 32,451 files / 52,868.4 MB / **426.07 s**, its `dev_hdd0/home/*/savedata` subtree 77 files / 16.3 MB / **0.06 s**, MAME's whole `nvram` 1,531 files / 137.3 MB / 8.02 s
