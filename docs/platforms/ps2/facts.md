---
summary: How each emulator RetroBat offers for `ps2` behaves, measured, with RB- IDs.
read-when: Before certifying a `ps2` row or changing how RomMBat handles a `ps2` save, state or firmware.
---

# ps2: emulator facts

Facts RomMBat relies on, one per heading. [The upstream reference](../../upstream/README.md) says what an entry holds and how IDs are kept.

## RB-172. A real PS2 library is 302 single-disc titles against 7 two-disc sets, with no `.m3u` anywhere and no per-game folders

Verified: RetroBat 8.2.0, 2026-08-24. Carried: RetroBat 8.3.0, 2026-10-14, it counts the user's PS2 library, and ps2's `<extension>` still lists .m3u, gaining only .decomp in 8.2.1. How: recovered from the findings ledger, which recorded it during M6 stage 2c (ad01b2d) on the 8.2.0 install its header names; the method is the text below.
So loose sibling files are the only layout the conversion refusal has to read, and RetroBat's `ps2` listing `.m3u` in `<extension>` neither binds a set nor means anything by its absence, which matches its own wiki saying PCSX2 cannot use one

## RB-175. A PS2 card's size and mtime cannot say whether it holds a save

Verified: RetroBat 8.3.0, 2026-10-16. How: hashed and counted the byte values of `saves/ps2/pcsx2/memcards/` around Burnout 3 launches under PCSX2 2.8.2 on the agent tree's 8.3.0 install, before and after it saved a profile to its own card.
PCSX2 creates `Mcd001.ps2` and `Mcd002.ps2` unformatted, 8,650,752 B of one byte value. After Burnout 3 formatted its own card and saved, that card was still 8,650,752 B, now 250 distinct byte values, and its mtime matched the untouched `Mcd002.ps2` to the tenth of a millisecond. So mtime cannot decide whether a card changed, size cannot tell an empty card from one holding a save, and only the contents separate them (RB-43, RB-328).

## RB-176. PCSX2's sidecar is a third format: the serial plus a CRC

Verified: RetroBat 8.3.0, 2026-10-15. How: saved a state in Armored Core 3 under PCSX2 2.8.2 with `F2` and read the files RetroBat mirrored into `saves/ps2/pcsx2/`.
`saves/ps2/pcsx2/Armored Core 3 (USA).txt` holds `SLUS-20435 (FDB4D261)`, beside the mirrored `Armored Core 3 (USA).01.p2s`, while PCSX2's own copy is `emulators/pcsx2/sstates/SLUS-20435 (FDB4D261).01.p2s`. So it is neither the bare serial DuckStation writes nor the underscore-joined form PPSSPP writes, and `GameIdAttributor.FromSidecar`'s first-underscore split leaves it whole rather than parsing it. It is not on the per-game memory card's path, because a converted PS2 card is rom-named and attributes by filename (RB-182).

## RB-177. The `K:` test stick cannot stand in for the real install on PS2

Verified: RetroBat 8.2.0, 2026-08-24. Carried: RetroBat 8.3.0, 2026-10-14, it describes what the K: test stick held, not a RetroBat behavior. How: recovered from the findings ledger, which recorded it during M6 stage 2c (ad01b2d) on the 8.2.0 install its header names; the method is the text below.
`K:\RetroBat` holds **no PS2 ROM, no PCSX2 binary** (`emulators/pcsx2` is `inis/` and `portable.ini` only) **and no PS2 BIOS**, against `E:`'s 319 ROMs, full PCSX2 and six `scph*` images. RetroBat downloads emulators on demand and an uninstalled one raises a modal dialog with no title and no timeout, so standing `K:` up is three steps with a known indefinite hang among them. **The two-writer probe needs none of that** and belongs on `K:`, which has EmulationStation and a real 56-setting file

## RB-182. A converted PCSX2 memory card is `<rom stem>.ps2`, so the extension is replaced and not appended

Verified: RetroBat 8.3.0, 2026-10-15. How: ran `saves convert` on Armored Core 3 on the agent tree's 8.3.0 install, booted it under PCSX2 2.8.2, and read `PCSX2.ini` and `memcards/`.
`ps2["Armored Core 3 (USA).chd"].pcsx2_slot1_memory=game` makes `emulatorlauncher` write `Slot1_Filename = Armored Core 3 (USA).ps2`, and PCSX2 creates `saves/ps2/pcsx2/memcards/Armored Core 3 (USA).ps2`, 8,650,752 B. **Two naming rules are in play at once and confusing them is the whole trap**: the `es_settings.cfg` key must carry `.chd` or it is ignored silently (RB-358), while the card PCSX2 writes drops it. The stem is exactly the `(folder, stem)` key class A attribution already uses, so a converted card resolves through the existing `RomIndex` with no new route. It lands **three levels deep**, under `saves/ps2/pcsx2/memcards/`, where class A discovery only reads files loose directly under `saves/<system>/`.

## RB-183. A converted game leaves the shared card completely alone, and its own card holds only its saves

Verified: RetroBat 8.3.0, 2026-10-16. How: converted Burnout 3 on the agent tree's 8.3.0 install, saved a profile under PCSX2 2.8.2, and hashed `memcards/` before and after.
Across the converted launch `Mcd001.ps2` kept its mtime and md5, so the conversion redirects the writes and a save left on the shared card is stranded there, as the warning says. The new card, `Burnout 3 - Takedown (USA, Canada).ps2`, holds one serial, `BASLUS-21050`, the game's own: one game's saves on one card, which is the class D attribution problem solved rather than argued.

## RB-184. `pcsx2_slot1_memory=game` leaves slot 2 shared, and moves its mtime without changing a byte

Verified: RetroBat 8.3.0, 2026-10-16. How: hashed `memcards/` around converted launches of Armored Core 3 and Burnout 3 under PCSX2 2.8.2 on the agent tree's 8.3.0 install.
`Slot2_Filename` stays `Mcd002.ps2`, and each converted launch wrote it at the same instant as the game's own card, to the tenth of a millisecond, with its md5 unchanged: one byte value, the unformatted card PCSX2 creates, which no game formatted. So on the converted path too **mtime moves while content does not**, which is why every save is content-hashed.

## RB-186. Eviction refuses a ROM whose converted card holds an unsent save, and fails closed when that leaves it short

Verified: RetroBat 8.2.0, 2026-08-24. Carried: RetroBat 8.3.0, 2026-10-14, it measures RomMBat's eviction guard; where the converted card lands is RB-182's. How: recovered from the findings ledger, which recorded it during M6 stage 2c's hands-on pass (1f37d5b) on the 8.2.0 install its header names; the method is the text below.
Driven on hardware under real budget pressure. With the budget 485.5 MB over and a save the player had just made, `evict` held Armored Core 3 back with `1 save file for this game on disk has not reached the server yet`, evicted two unrelated games, and reported **"2 held back (still short)"** rather than reporting success at freeing enough. After one flush the same command offered the same ROM for eviction, so both directions are proven and the guard is not blocking spuriously. The same flush also sent **1 play session**, so the hooks caught the launch alongside

## RB-187. Reverting a conversion restores `es_settings.cfg`, and only ES's own bookkeeping moves afterwards

Verified: RetroBat 8.2.0, 2026-08-24. Carried: RetroBat 8.3.0, 2026-10-14, Settings.cpp changes defaults only (MaxVRAM in 8.2.1; ScraperMediaFilter, IndexDirectoryDepth, AntiAliasing and RightStickVolume in 8.3.0), not when the file is loaded or written, so ES still rewrites only its own bookkeeping. How: recovered from the findings ledger, which recorded it during M6 stage 2c's hands-on pass (1f37d5b) on the 8.2.0 install its header names; the method is the text below.
Against a byte copy taken before the conversion: **57 settings before, 57 after the revert, nothing added, nothing dropped, no `pcsx2_slot1_memory` and no RomMBat key left**. The file's md5 differs, and the whole difference is `LastSystem`, which ES rewrites to record where the user was in the UI. So a byte comparison is the wrong assertion for this file and a **setting-set comparison is the right one**, which is the same lesson `gamelist.xml` teaches: compare what the writer owns, not the bytes a second writer also touches. The `absent` prior state was honored, the key being removed rather than written at a stock value

## RB-188. After a revert the per-game card stays on disk, keeps its `local_save` row, and goes on syncing, by decision

Verified: RetroBat 8.2.0, 2026-08-24. Carried: RetroBat 8.3.0, 2026-10-14, it measures RomMBat keeping a reverted card's row, which no RetroBat change reaches. How: recovered from the findings ledger, which recorded it during M6 stage 2c's hands-on pass (1f37d5b) on the 8.2.0 install its header names; the method is the text below.
The row is still there, still class D, still in step, and the conversion record is deleted with no tombstone. So progress made while converted is never orphaned by un-converting, and the user is told plainly that the game will no longer read it. A re-sync afterwards is a clean no-op: `nothing to do`, 0 downloaded, 0 written, gamelists unchanged, and a flush moves nothing

## RB-376. Launching a PS2 game rewrites the shared memory cards on its own

Verified: RetroBat 8.3.0, 2026-10-15. How: hashed `saves/ps2/` around two Armored Core 3 launches under PCSX2 2.8.2 on the agent tree's 8.3.0 install in which nothing was saved.
Both shared cards were written when the game closed, without the game being asked to save:

```text
pcsx2\memcards\Mcd001.ps2   8650752 B   15:13:54.7840 -> 15:15:29.8625   md5 unchanged
pcsx2\memcards\Mcd002.ps2   8650752 B   15:13:54.7845 -> 15:15:29.8625   md5 unchanged
```

**So a class-D container's mtime changes merely because a game was launched.** Any sync that
uses modification time to decide whether a shared card needs uploading will upload it after
every session. Content hashing is required, not optional, for class D.

## RB-406. `pcsx2_slot1_memory=folder` makes slot 1 one shared directory, split per save by PS2 serial

Verified: RetroBat 8.3.0, 2026-10-16. How: set `ps2.pcsx2_slot1_memory=folder` on the agent tree's 8.3.0 install, booted Armored Core 3 and then saved a Burnout 3 profile under PCSX2 2.8.2, and read `PCSX2.ini` and `memcards/`.
`emulatorlauncher` writes `Slot1_Filename = Mcdf01.ps2`, and PCSX2 makes `memcards/Mcdf01.ps2/` a
directory holding a `_pcsx2_superblock`, 0 B until a game formats the card and 8,192 B after.
`McdFolderAutoManage = true` comes from RetroBat's `PCSX2.ini` template. Each save is a directory
named from the save's own name, which starts with the serial: Burnout 3's profile is
`BASLUS-21050/`, a 61,772 B `BASLUS-21050` beside `icon.sys`, `view.ico` and `_pcsx2_index`. So
it is a shared container, one fixed name that every game writes to. Only the units inside it are
per save, and they are keyed by serial, not by the ROM stem. `Mcd001.ps2` stopped changing, and
slot 2 is still the file `Mcd002.ps2`, so `saves` reports both cards on one row and names each
file. RomMBat declares `Mcdf01.ps2` a shared container. Syncing it would take a route from serial
to ROM that RomMBat does not have.
