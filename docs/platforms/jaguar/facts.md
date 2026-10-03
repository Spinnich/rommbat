---
summary: How each emulator RetroBat offers for `jaguar` behaves, measured, with RB- IDs.
read-when: Before certifying a `jaguar` row or changing how RomMBat handles a `jaguar` save, state or firmware.
---

# jaguar: emulator facts

Facts RomMBat relies on, one per heading. [The upstream reference](../../upstream/README.md) says what an entry holds and how IDs are kept.

## RB-166. Refuted. The two describe opposite sides of a mirror

Previously: `bigpemu` declares `001`/`999` against a two-digit `{{slot2d}}`, so its own file is internally inconsistent (**#34**, the `save-sync` skill)

The probe says: **Refuted. The two describe opposite sides of a mirror.** Driven on the real install: BigPEmu writes **three-digit** names in its own tree, `emulators/bigpemu/userdata/game4F7E323A69447A71_state001.bigpstate`, keyed by an internal game id; RetroBat mirrors each to `saves/jaguar/bigpemu/Rayman (World)_state01.bigpstate`, **two-digit and rom-named**, byte-identical in length. So `firstslot`/`lastslot` describe the emulator's native slot range and `<file>` describes the mirror, and neither contradicts the other. Six states driven through the gamepad overlay, slots 1 to 6; `SaveAutoIncr: 1` in `BigPEmuConfig.bigpcfg` is why they came out consecutively. RomMBat read all six correctly off the declared path. **Whether the mirror writes `_state100` past slot 99 is still unmeasured**, and reaching it needs ~94 more saves of one game

## RB-167. It does not. The Jaguar battery save never leaves the emulator's own tree

Previously: (not addressed) whether everything `bigpemu` writes reaches `saves/`

The probe says: **It does not. The Jaguar battery save never leaves the emulator's own tree.** `game4F7E323A69447A71_eeprom.bigpeep`, 128 bytes, sits in `emulators/bigpemu/userdata/` with no counterpart anywhere under `saves/jaguar/`. Same class of trap as openMSX's states landing in `bios/openmsx/savestates/`: a client reading only the declared tree concludes the game has no battery save. `jaguar` is in `save_shapes.json`'s `_unclassified` list and this is the concrete reason it has to stay there until the native path is modelled

## RB-168. `bigpemu` writes one, and its contents are the mapping between the two naming schemes

Previously: A `.txt` sidecar's presence and contents say nothing for emulators not yet driven (**145**, the `save-sync` skill)

The probe says: **`bigpemu` writes one, and its contents are the mapping between the two naming schemes.** `saves/jaguar/bigpemu/Rayman (World).txt` holds `game4F7E323A69447A71`, which is exactly the key BigPEmu's native filenames use. So it is the same shape as PPSSPP's `ULES01513_1.00`: not a console serial, but a stable per-game identifier that joins the mirror back to the native tree. It carries no underscore, so the first-underscore split in `GameIdAttributor.FromSidecar` returns it whole
