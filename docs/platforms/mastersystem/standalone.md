---
summary: The certification record for `mastersystem`: the five standalone rows.
read-when: When a result for one of these `mastersystem` rows is needed, or before re-driving one.
---

# mastersystem: The five standalone rows

|            | Selected by                                                | Confirmed on the ES launch line         |
| ---------- | ---------------------------------------------------------- | --------------------------------------- |
| `mesen`    | `mastersystem.emulator = mesen`                            | `-emulator mesen`, empty `-core`        |
| `mednafen` | `mastersystem.emulator = mednafen`, `.core = mastersystem` | `-emulator mednafen -core mastersystem` |
| `ares`     | `mastersystem.emulator = ares`, `.core = MasterSystem`     | `-emulator ares -core MasterSystem`     |
| `SMSHawk`  | `mastersystem.emulator = bizhawk`, `.core = SMSHawk`       | `-emulator bizhawk -core SMSHawk`       |
| `jgenesis` | `mastersystem.emulator = jgenesis`                         | `-emulator jgenesis`, empty `-core`     |

## Checklist for the five

| #   | Result on every one of the five                                                             |
| --- | ------------------------------------------------------------------------------------------- |
| 4   | **Pass, both directions**, every file at its own md5 after a restore                        |
| 5   | **Pass**, two slots each, where each emulator was found to write                            |
| 6   | **N/A**                                                                                     |
| 7   | **Pass**, carried: each was launched from ES on the synced ROM, art and description present |
| 8   | **Pass**, every session read back from RomM by `status`, under `recent:`                    |
| 9   | **Pass.** 153 present and verified, 599 media present, gamelist byte-identical, 0 sent      |

## 4. Battery saves on the five

| Row        | File under `saves/mastersystem/`             | Size     | Slot               | After the row |
| ---------- | -------------------------------------------- | -------- | ------------------ | ------------- |
| `mesen`    | `<rom>.sav`                                  | 8,192 B  | `mesen:battery`    | `2271877c...` |
| `mednafen` | `<rom>.d46e40bbb729ba233f171ad7bf6169f5.sav` | 32,768 B | `mednafen:battery` | `f898854e...` |
| `ares`     | `ares/Master System/<rom>.ram`               | 32,768 B | `ares:battery`     | `5f02b462...` |
| `SMSHawk`  | `bizhawk/Golden Axe Warrior (UE).SaveRAM`    | 8,192 B  | `bizhawk:battery`  | `772cd5c2...` |
| `jgenesis` | `jgenesis/sms/<rom>.sav`                     | 32,768 B | `jgenesis:battery` | `f898854e...` |

**Mesen keeps its own loose `.sav`**, where on `snes` it shared `libretro`'s `.srm`, and **rewrites it
only when the game changes the SRAM**: an agent boot with the seed in place left it untouched, md5
and timestamp. Its first session wrote nothing, and a second, 15:37:54Z to 15:38:51Z, rewrote it.
**mednafen will not start the game while Mesen's `.sav` is there**: it opens the plain `<rom>.sav`,
finds 8,192 B where it keeps 32 KB, and stops with "Error reading from opened file ... Unexpected
EOF" (RB-324). For its row the Mesen file was held out and mednafen's own hashed name seeded;
**the restore placed that hashed save under the name `MednafenRomHash` computes from the `.sms`**,
the first time that path was driven end to end. **BizHawk names the save after its own title**,
`Golden Axe Warrior (UE)`, and the sidecar beside its states reads `Golden Axe Warrior (UE).SMSHawk`,
which is how `saves` attributes it: `learned from the name sidecar beside a save state`. It rewrote
the file on exit with the bytes it loaded, and left a `.SaveRAM.bak` at launch. **ares writes on
exit**, as on `snes`.

After each row its save and one state went out of the tree and came back through
`saves restore 239603 --apply`, failed 0, exit 0, each at its own md5.

## 5. States on the five

| Row        | Directory under `saves/mastersystem/` | Slots and who made them                       | Round-tripped               |
| ---------- | ------------------------------------- | --------------------------------------------- | --------------------------- |
| `mesen`    | `mesen/SaveStates/`                   | `_1.mss` and `_2.mss`, both in ES             | `_2.mss`, `c66a9c5e...`     |
| `mednafen` | `mednafen/sstates/`                   | `.<md5>.mc0` and `.mc9`, both in ES           | `.mc9`, `addb179f...`       |
| `ares`     | `ares/Master System/`                 | `.bs1` and `.bs2`, both by the agent          | `.bs2`, `989bf265...`       |
| `SMSHawk`  | `bizhawk/sstates/SMSHawk/`            | `QuickSave4` in ES; `QuickSave2` by the agent | `QuickSave2`, `88a153f7...` |
| `jgenesis` | `jgenesis/states/`                    | `_0.jst` and `_1.jst`, both in ES             | `_1.jst`, `3298b2c1...`     |

**`mesen`, `mednafen` and `ares` declare no state directory**, and each wrote to the one above,
which the supplement now declares for `mastersystem`. **The maintainer's second mednafen slot was 9,
not 1**: the slot stepped down from 0 and wrapped, where the agent's `F7` in the probe stepped up to
slot 1. Both are valid slots and both synced. **ares keeps its states beside its battery save**, `F2`
saving and `F7` stepping the slot, which on `snes` it did not. BizHawk took ES's `-state_slot 4` for
the pad's key and `Ctrl+F2` wrote slot 2, and its two frames, `Framebuffer.bmp` inside each state,
differ (`c3b18e20...`, `af8b9ac2...`). jgenesis writes its states to
`emulators/jgenesis/states/sms/` and `emulatorLauncher` mirrors them into the declared directory, as
on the systems before. **None of the five writes a screenshot file**, which the preview says: `no
screenshot: the server links none to this state`.

## 8. Sessions

| Row        | Journal, UTC                                    | Length      |
| ---------- | ----------------------------------------------- | ----------- |
| `mesen`    | 15:28:16 to 15:29:04, then 15:37:54 to 15:38:51 | 48s, 57s    |
| `mednafen` | 15:48:20 to 15:49:51, then 15:55:19 to 15:55:47 | 1m 31s, 28s |
| `ares`     | 15:59:42 to 16:00:16                            | 33s         |
| `SMSHawk`  | 16:06:51 to 16:07:37                            | 45s         |
| `jgenesis` | 17:32:11 to 17:34:32                            | 2m 21s      |

Every one is on the server, rom 239603. mednafen's first is the launch that stopped on Mesen's save,
with the error on screen for the whole of it.
