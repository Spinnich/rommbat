---
summary: The certification record for `snes`: the eight standalone rows.
read-when: When a result for one of these `snes` rows is needed, or before re-driving one.
---

# snes: The eight standalone rows

|            | Selected by                                    | Confirmed on the ES launch line     |
| ---------- | ---------------------------------------------- | ----------------------------------- |
| `mesen`    | `snes.emulator = mesen`                        | `-emulator mesen`, empty `-core`    |
| `mednafen` | `snes.emulator = mednafen`, `.core = snes`     | `-emulator mednafen -core snes`     |
| `snes9x`   | `snes.emulator = snes9x`                       | `-emulator snes9x`, empty `-core`   |
| `ares`     | `snes.emulator = ares`, `.core = SuperFamicom` | `-emulator ares -core SuperFamicom` |
| `BSNES`    | `snes.emulator = bizhawk`, `.core = BSNES`     | `-emulator bizhawk -core BSNES`     |
| `Faust`    | `snes.emulator = bizhawk`, `.core = Faust`     | `-emulator bizhawk -core Faust`     |
| `Snes9x`   | `snes.emulator = bizhawk`, `.core = Snes9x`    | `-emulator bizhawk -core Snes9x`    |
| `jgenesis` | `snes.emulator = jgenesis`                     | `-emulator jgenesis`, empty `-core` |

**Standalone Snes9x was not installed.** `emulators/snes9x/` held only `snes9x.conf`, and the first
launch under the row offered to install it; with the maintainer's agreement the agent accepted, and
RetroBat's installer put `snes9x-x64.exe` in place in about three seconds.

## Checklist for the eight

| #   | Result on every one of the eight                                                            |
| --- | ------------------------------------------------------------------------------------------- |
| 4   | **Pass, both directions**, every file at its own md5 after a restore                        |
| 5   | **Pass**, two slots each, where each emulator was found to write                            |
| 6   | **N/A**                                                                                     |
| 7   | **Pass**, carried: each was launched from ES on the synced ROM, art and description present |
| 8   | **Pass**, every session read back from RomM by `status`, under `recent:`                    |
| 9   | **Pass.** 276 present and verified, 1,075 media present, gamelist byte-identical, 0 sent    |

## 4. Battery saves on the eight

| Row        | File under `saves/snes/`                                             | Slot                                        | After the row |
| ---------- | -------------------------------------------------------------------- | ------------------------------------------- | ------------- |
| `mesen`    | `<rom>.srm`, the file the `libretro` cores share                     | `libretro:battery`                          | `05c99057...` |
| `mednafen` | `<rom>.srm` when one is there, else `<rom>.<md5>.srm`                | `libretro:battery`, else `mednafen:battery` | `425acf6b...` |
| `snes9x`   | `<rom>.srm`, the same file                                           | `libretro:battery`                          | `d12f5187...` |
| `ares`     | `ares/Super Famicom/<rom>.ram`, and `<rom>.dram` for a DSP cartridge | `ares:battery:ram`                          | `97929108...` |
| `BSNES`    | `bizhawk/<rom>.SaveRAM`, one file for all three cores                | `bizhawk:battery`                           | `71480f98...` |
| `Faust`    | the same file                                                        | `bizhawk:battery`                           | `74b45e5b...` |
| `Snes9x`   | the same file                                                        | `bizhawk:battery`                           | `cd78b59f...` |
| `jgenesis` | `jgenesis/sfc/<rom>.sav`                                             | `jgenesis:battery`                          | `b6cbf7aa...` |

**Mesen and Snes9x write the loose `.srm` the `libretro` cores share**, so they needed no rule, and
their saves upload as `libretro:battery`. Mesen's `.srm` above is the one after the agent's own
launch for slot 2, which rewrote it at boot. **mednafen read and saved into the plain `.srm`**, as
it adopted mGBA's `.sav` on `gb` and `gbc`; its hashed `<rom>.<md5>.srm`, written only when no
plain one is there, has its own rule, `mednafen:battery`, and a restore names it through
`MednafenRomHash`, both covered by tests and neither driven, since the maintainer's session never
wrote one. **BizHawk names its battery save after the ROM file on `snes`**, where on `gb` and `gbc`
it used its own title; the sidecar `<rom>.txt` reads `<rom>.BSNES.Compatibility`. The rule stays
`display name`, whose routes, the sidecar and the launch window, attribute either spelling.
**jgenesis keeps a `.sfc`'s save in `jgenesis/sfc/`**, named after the file inside the zip.

After each row its save and one state went out of the tree and came back through
`saves restore 200280 --apply`, `restored 1 save(s) and 1 state(s), failed 0`, exit 0, each at its
own md5.

## 5. States on the eight

| Row        | Directory under `saves/snes/` | Slots and who made them                       | Round-tripped               |
| ---------- | ----------------------------- | --------------------------------------------- | --------------------------- |
| `mesen`    | `mesen/SaveStates/`           | `_1.mss` in ES; `_2.mss` by the agent         | `_1.mss`, `c3711cf1...`     |
| `mednafen` | `mednafen/sstates/`           | `.<md5>.mc0` and `.mc1`, both in ES           | `.mc1`, `d2458808...`       |
| `snes9x`   | `snes9x/sstates/`             | `.000` and `.001`, both in ES                 | `.001`, `b47ed5be...`       |
| `ares`     | `ares/Super Famicom/`         | `.bs1` in ES; `.bs2` by the agent             | `.bs1`, `f19ac621...`       |
| `BSNES`    | `bizhawk/sstates/BSNES/`      | `QuickSave3` and `QuickSave4`, both in ES     | `QuickSave4`, `3c3f2311...` |
| `Faust`    | `bizhawk/sstates/Faust/`      | `QuickSave5` in ES; `QuickSave2` by the agent | `QuickSave5`, `6b2c0765...` |
| `Snes9x`   | `bizhawk/sstates/Snes9x/`     | `QuickSave6` and `QuickSave7`, both in ES     | `QuickSave7`, `2a12d622...` |
| `jgenesis` | `jgenesis/states/`            | `_0.jst` and `_1.jst`, both in ES             | `_1.jst`, `2ada4850...`     |

**`mesen`, `mednafen`, `snes9x` and `ares` declare no state directory**, and each wrote to the one
above, which the supplement now declares for `snes`. Snes9x's slots are `.000` to `.009`, Shift+F10
being slot 0, which the supplement writes as `.00{{slot0}}`. **ares keeps its states beside its
battery save** in `ares/Super Famicom/`, unlike `gbc`, where the two halves split. **jgenesis writes
its states to `emulators/jgenesis/states/sfc/`** under its `state_path = "EmulatorFolder"`, and
`emulatorLauncher` mirrors them into the declared `saves/snes/jgenesis/states/`, which is where
RomMBat reads them. Mesen and snes9x tag a state with their build, `v2.1.1+137ae7ce...` and `v1.63`.
BizHawk's frame is inside the state, and the two BSNES slots hold different `Framebuffer.bmp`s;
only the `libretro` rows write a screenshot `.png`.

**The agent's slots are title-screen states**: in each of its three launches the game was on the
title when the key went, having been given no input, and Mesen's `Ctrl+F1`, its load key, did not
bring slot 1 back before the save. They are real states on a different frame from the maintainer's,
and each went up in the flush after it.

**ares shows nothing when a state is saved or the slot changes**, so the maintainer could not tell
from the pad whether a key had landed. On disk `F2` saved slot 1 and `F7` did not step the slot. On
the next system the agent drives ares's states from its own session and checks the files, rather
than asking for keys pressed blind.

## 8. Sessions

| Row        | Journal, UTC         | Length |
| ---------- | -------------------- | ------ |
| `mesen`    | 12:18:15 to 12:19:05 | 50s    |
| `mednafen` | 12:24:09 to 12:24:55 | 46s    |
| `snes9x`   | 12:26:17 to 12:27:03 | 46s    |
| `ares`     | 12:28:12 to 12:29:04 | 51s    |
| `BSNES`    | 12:32:36 to 12:33:14 | 38s    |
| `Faust`    | 13:19:12 to 13:20:02 | 49s    |
| `Snes9x`   | 13:23:42 to 13:24:28 | 46s    |
| `jgenesis` | 13:29:26 to 13:30:25 | 59s    |

Every one is on the server, rom 200280, as are the seven `libretro` sessions above and a 4 s session
from 13:26:57, jgenesis's first launch, which closed on its own (below).
