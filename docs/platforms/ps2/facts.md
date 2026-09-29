---
summary: How each emulator RetroBat offers for `ps2` behaves, measured, with RB- IDs.
read-when: Before certifying a `ps2` row or changing how RomMBat handles a `ps2` save, state or firmware.
---

# ps2: emulator facts

Facts RomMBat relies on, one per heading. [The upstream reference](../../upstream/README.md) says what an entry holds and how IDs are kept.

## RB-172. 302 single-disc titles against 7 two-disc sets, with no `.m3u` anywhere and no per-game folders

The claim being checked: (not addressed) how a real PS2 library splits between single-disc and multi-disc

What was measured: **302 single-disc titles against 7 two-disc sets, with no `.m3u` anywhere and no per-game folders.** So loose sibling files are the only layout the conversion refusal has to read, and RetroBat's `ps2` listing `.m3u` in `<extension>` neither binds a set nor means anything by its absence, which matches its own wiki saying PCSX2 cannot use one

## RB-175. Both reproduced on PS2, having been measured on PS1

The claim being checked: A PS2 launch rewrites both shared memory cards, and an empty card is the same size as a real one (**the `save-sync` skill**, RB-43 and RB-328)

What was measured: **Both reproduced on PS2, having been measured on PS1.** `Mcd001.ps2` and `Mcd002.ps2` are each 8,650,752 B with identical mtimes to the second, and their byte histograms are **256 distinct values against 76**. So mtime cannot decide whether either changed, size cannot tell a formatted empty card from one holding a save, and only the contents separate them

## RB-176. PCSX2's is a third format: the serial plus a CRC

The claim being checked: A `.txt` sidecar holds the emulator's native basename, and DuckStation's holds a bare serial (**145, 168**)

What was measured: **PCSX2's is a third format: the serial plus a CRC.** `saves/ps2/pcsx2/Armored Core 3 (USA).txt` holds `SLUS-20435 (FDB4D261)`, and the two others on the install match its shape. So it is neither the bare serial DuckStation writes nor the underscore-joined form PPSSPP writes, and `GameIdAttributor.FromSidecar`'s first-underscore split leaves it whole rather than parsing it. Relevant to **#37**, and not on stage 2c's path, because a converted PS2 card is rom-named and attributes by filename

## RB-177. Whether the `K:` test stick can stand in for the real install on PS2

The claim being checked: (not addressed) whether the `K:` test stick can stand in for the real install on PS2

What was measured: **It cannot, today.** `K:\RetroBat` holds **no PS2 ROM, no PCSX2 binary** (`emulators/pcsx2` is `inis/` and `portable.ini` only) **and no PS2 BIOS**, against `E:`'s 319 ROMs, full PCSX2 and six `scph*` images. RetroBat downloads emulators on demand and an uninstalled one raises a modal dialog with no title and no timeout, so standing `K:` up is three steps with a known indefinite hang among them. **The two-writer probe needs none of that** and belongs on `K:`, which has EmulationStation and a real 56-setting file

## RB-182. `<rom stem>.ps2`, so the extension is replaced and not appended

The claim being checked: (not addressed) what a converted PCSX2 memory card is called

What was measured: **`<rom stem>.ps2`, so the extension is replaced and not appended.** Driven end to end: `ps2["Armored Core 3 (USA).chd"].pcsx2_slot1_memory=game` produced `saves/ps2/pcsx2/memcards/Armored Core 3 (USA).ps2`, 8,650,752 B. **Two naming rules are in play at once and confusing them is the whole trap**: the `es_settings.cfg` key must carry `.chd` or it is ignored silently (M0 cases E and F), while the card PCSX2 writes drops it. The consequence is the good one: the stem `Armored Core 3 (USA)` is exactly the `(folder, stem)` key class A attribution already uses, so a converted card resolves through the existing `RomIndex` with no new route, which is the claim the whole PS2 story rested on. It lands **three levels deep**, under `saves/ps2/pcsx2/memcards/`, where class A discovery only reads files loose directly under `saves/<system>/`

## RB-183. True while the game is using them, and a converted game leaves the shared card completely alone

The claim being checked: A PS2 launch rewrites both shared memory cards, so mtime cannot decide whether one changed (**175, the `save-sync` skill**)

What was measured: **True while the game is using them, and a converted game leaves the shared card completely alone.** After the converted launch, `Mcd001.ps2` was **untouched**: mtime still the moment it was copied, md5 unchanged at `d3334798…`. So the conversion really does redirect the writes, the stranded save really is stranded, and the warning's wording is honest rather than cautious. The new card holds **exactly one game's saves**, `SLUS-20435` with 4 entries, against `Mcd001`'s 11 games, which is the class D attribution problem solved and measured rather than argued

## RB-184. Slot 2 stays shared, exactly as the option name says, and it moved its mtime without changing a byte

The claim being checked: (not addressed) what `pcsx2_slot1_memory=game` does to console slot 2

What was measured: **Slot 2 stays shared, exactly as the option name says, and it moved its mtime without changing a byte.** `Mcd002.ps2` was written at `08:28:22.0017`, the same instant as the new card, and its md5 is unchanged at `96cebf28…`, 76 distinct byte values, no game serial in it. So it is F18's formatted-empty card reproduced on PS2, and it is a fresh demonstration on the converted path that **mtime moves while content does not**, which is why every save is content-hashed

## RB-186. It does, driven on hardware under real budget pressure, and it fails closed rather than pretending to succeed

The claim being checked: (not addressed) whether eviction really refuses a ROM whose converted card holds an unsent save

What was measured: **It does, driven on hardware under real budget pressure, and it fails closed rather than pretending to succeed.** With the budget 485.5 MB over and a save the player had just made, `evict` held Armored Core 3 back with `1 save file for this game on disk has not reached the server yet`, evicted two unrelated games, and reported **"2 held back (still short)"** rather than reporting success at freeing enough. After one flush the same command offered the same ROM for eviction, so both directions are proven and the guard is not blocking spuriously. The same flush also sent **1 play session**, so the hooks caught the launch alongside

## RB-187. It does, and the only thing that moves afterwards is ES's own bookkeeping

The claim being checked: (not addressed) whether reverting a conversion really restores the file

What was measured: **It does, and the only thing that moves afterwards is ES's own bookkeeping.** Against a byte copy taken before the conversion: **57 settings before, 57 after the revert, nothing added, nothing dropped, no `pcsx2_slot1_memory` and no RomMBat key left**. The file's md5 differs, and the whole difference is `LastSystem`, which ES rewrites to record where the user was in the UI. So a byte comparison is the wrong assertion for this file and a **setting-set comparison is the right one**, which is the same lesson M4 learned about `gamelist.xml`: compare what the writer owns, not the bytes a second writer also touches. The `absent` prior state was honoured, the key being removed rather than written at a stock value

## RB-188. It stays on disk, keeps its `local_save` row, and goes on syncing, which is the decision rather than an

The claim being checked: (not addressed) what happens to the per-game card after a revert

What was measured: **It stays on disk, keeps its `local_save` row, and goes on syncing, which is the decision rather than an oversight.** After the revert the row is still there, still class D, still in step, and the conversion record is deleted with no tombstone. So progress made while converted is never orphaned by un-converting, and the user is told plainly that the game will no longer read it. A re-sync afterwards is a clean no-op: `nothing to do`, 0 downloaded, 0 written, gamelists unchanged, and a flush moves nothing

## RB-376. Launching a PS2 game rewrites the shared memory cards on its own

Incidental, but it bears directly on class D. The PCSX2 run touched both shared cards without
the game being asked to save anything:

```text
*changed pcsx2\memcards\Mcd001.ps2   8650752 B
*changed pcsx2\memcards\Mcd002.ps2   8650752 B
```

**So a class-D container's mtime changes merely because a game was launched.** Any sync that
uses modification time to decide whether a shared card needs uploading will upload it after
every session. Content hashing is required, not optional, for class D.
