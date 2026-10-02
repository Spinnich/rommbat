---
summary: The certification record for `gb`: the eight standalone rows.
read-when: When a result for one of these `gb` rows is needed, or before re-driving one.
---

# gb: The eight standalone rows

**All eight certified on 2026-09-22, on the deploy of this branch.** Each was driven first on
`main`, where its save sat on disk unread; the branch's first flush sent **4 saves and 9 states**.

|            | Selected by                                 | Confirmed on the ES launch line     |
| ---------- | ------------------------------------------- | ----------------------------------- |
| `mesen`    | `gb.emulator = mesen`                       | `-emulator mesen`, empty `-core`    |
| `mgba`     | `gb.emulator = mgba`, `.core = mgba`        | `-emulator mgba -core mgba`         |
| `mednafen` | `gb.emulator = mednafen`, `.core = gb`      | `-emulator mednafen -core gb`       |
| `ares`     | `gb.emulator = ares`, `.core = GameBoy`     | `-emulator ares -core GameBoy`      |
| `Gambatte` | `gb.emulator = bizhawk`, `.core = Gambatte` | `-emulator bizhawk -core Gambatte`  |
| `GBHawk`   | `gb.emulator = bizhawk`, `.core = GBHawk`   | `-emulator bizhawk -core GBHawk`    |
| `SameBoy`  | `gb.emulator = bizhawk`, `.core = SameBoy`  | `-emulator bizhawk -core SameBoy`   |
| `jgenesis` | `gb.emulator = jgenesis`                    | `-emulator jgenesis`, empty `-core` |

## Checklist for the eight

| #   | Result on every one of the eight                                                            |
| --- | ------------------------------------------------------------------------------------------- |
| 4   | **Pass, both directions**, every file at its own md5 after one restore                      |
| 5   | **Pass**, two slots each, where each emulator was found to write                            |
| 6   | **N/A**                                                                                     |
| 7   | **Pass**, carried: each was launched from ES on the synced ROM, art and description present |
| 8   | **Pass**, every session read back from RomM by `status`, under `recent:`                    |
| 9   | **Pass.** 107 present and verified, 414 media present, gamelist byte-identical, 0 sent      |

## 4. Battery saves on the eight

| Row        | File under `saves/gb/`                                   | Slot               | md5 after the session |
| ---------- | -------------------------------------------------------- | ------------------ | --------------------- |
| `mesen`    | `<rom>.srm`                                              | `libretro:battery` | `4a8163a7...`         |
| `mgba`     | `<rom>.sav`                                              | `mgba:battery`     | `e6ab3d49...`         |
| `mednafen` | `<rom>.sav`, mGBA's file                                 | `mgba:battery`     | `88c8ad8d...`         |
| `ares`     | `ares/Game Boy/<rom>.ram`                                | `ares:battery`     | `32467e57...`         |
| `Gambatte` | `bizhawk/Pokemon - Yellow Version (USA, Europe).SaveRAM` | `bizhawk:battery`  | `a79ad54d...`         |
| `GBHawk`   | the same file                                            | `bizhawk:battery`  | `e6c75d11...`         |
| `SameBoy`  | the same file                                            | `bizhawk:battery`  | `311eb5bd...`         |
| `jgenesis` | `jgenesis/gb/<rom>.sav`                                  | `jgenesis:battery` | `f2ccd8db...`         |

Every file is 32,768 B, the cartridge's RAM with nothing appended. **Mesen writes the loose `.srm`
the `libretro` cores share**, so it needed no rule and uploads as `libretro:battery`; its round trip
was run on the file it left, `4a8163a7...`. **mednafen read and saved into mGBA's plain `.sav`**
rather than its hashed name, which it writes only when no plain one is there (RB-273), so the
two share `mgba:battery` as on `gba`. **BizHawk's three cores share one file named after BizHawk's
own title**, `Pokemon - Yellow Version (USA, Europe)`, where on `gba` its title matched the ROM file;
the state sidecar says so, and the `.SaveRAM.bak` beside it is BizHawk's copy of the previous save.

The four saves with their own rule and one state from each of the eight rows went out of the tree
and came back through one `saves restore 153392 --apply`: `restored 4 save(s) and 8 state(s),
failed 0, 528.1 KB`, exit 0, **all twelve files at their own md5**. Mesen's `.srm` went out and came
back the same way on its own.

## 5. States on the eight

| Row        | Directory under `saves/gb/` | First                       | Second                      | Keys                     |
| ---------- | --------------------------- | --------------------------- | --------------------------- | ------------------------ |
| `mesen`    | `mesen/SaveStates/`         | `_1.mss`, `49020cd0...`     | `_2.mss`, `c3d8e4fd...`     | `Shift+F1`, `Shift+F2`   |
| `mgba`     | `mgba/sstates/`             | `.ss1`, `18efd6db...`       | `.ss3`, `b272934d...`       | the maintainer's         |
| `mednafen` | `mednafen/sstates/`         | `.<md5>.mc0`, `8512ae63...` | `.<md5>.mc2`, `f274dc98...` | `F2`, then `F7` and `F2` |
| `ares`     | `ares/Game Boy/`            | `.bs1`, `cbad7ff0...`       | `.bs2`, `68f58647...`       | `F2`, then `F7` and `F2` |
| `Gambatte` | `bizhawk/sstates/Gambatte/` | `QuickSave3`, `5d78e1e7...` | `QuickSave2`, `fb977adf...` | the pad, then `Ctrl+F2`  |
| `GBHawk`   | `bizhawk/sstates/GBHawk/`   | `QuickSave4`, `d406e432...` | `QuickSave2`, `ef8d2c87...` | the pad, then `Ctrl+F2`  |
| `SameBoy`  | `bizhawk/sstates/SameBoy/`  | `QuickSave5`, `7f9dc209...` | `QuickSave2`, `87cbce88...` | the pad, then `Ctrl+F2`  |
| `jgenesis` | `jgenesis/states/`          | `_0.jst`, `0dd85f35...`     | `_1.jst`, `fa8be187...`     | the pad, then `F7`, `F2` |

**`mesen`, `mgba`, `mednafen` and `ares` declare no state directory**, and each wrote to the one
above, which the supplement now declares for `gb`: mGBA where RetroBat's `config.ini` sets
`savestatePath`, the other three as on `nes`, `megadrive` and `gba`. **ares names its directory
after its own name for the console**, `Game Boy`, beside `gba`'s `Game Boy Advance`. The md5 in
mednafen's names is of the whole `.gb` inside the zip, `d9290db8...`.

**BizHawk's pad key followed ES's `-state_slot`, which moved on each launch**, 3, 4 and 5 across the
three cores, as states accumulated; `Ctrl+F2` then wrote slot 2. BizHawk's frame is inside the state,
and the two slots' `Framebuffer.bmp` differ on every core. `jgenesis` and `bizhawk` write in their
own trees and are mirrored into `saves/` with a `.txt` sidecar. Only the `libretro` rows write a
screenshot `.png`; the rest have nothing to carry, and the preview says so.

## 8. Sessions

| Row        | Journal, UTC         | Length |
| ---------- | -------------------- | ------ |
| `mesen`    | 18:51:45 to 18:52:43 | 57s    |
| `mgba`     | 18:54:47 to 18:56:14 | 1m 27s |
| `mednafen` | 18:57:23 to 18:58:38 | 1m 15s |
| `ares`     | 18:59:47 to 19:00:47 | 59s    |
| `Gambatte` | 19:02:47 to 19:03:46 | 59s    |
| `GBHawk`   | 19:06:05 to 19:06:53 | 47s    |
| `SameBoy`  | 19:08:11 to 19:09:08 | 57s    |
| `jgenesis` | 19:10:23 to 19:11:24 | 1m 1s  |

Every pair is correlated in the journal, every `quit` pass exited 0, and every one is on the server,
rom 153392, as are the six `libretro` sessions in [libretro.md](libretro.md).
