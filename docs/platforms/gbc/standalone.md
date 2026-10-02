---
summary: The certification record for `gbc`: the eight standalone rows.
read-when: When a result for one of these `gbc` rows is needed, or before re-driving one.
---

# gbc: The eight standalone rows

**All eight certified on 2026-09-23, on the second deploy.** Each was driven first on the first,
where its save sat on disk unread; the second's first flush sent **6 saves and 10 states**.

|            | Selected by                                   | Confirmed on the ES launch line     |
| ---------- | --------------------------------------------- | ----------------------------------- |
| `mesen`    | `gbc.emulator = mesen`                        | `-emulator mesen`, empty `-core`    |
| `mgba`     | `gbc.emulator = mgba`, `.core = mgba`         | `-emulator mgba -core mgba`         |
| `mednafen` | `gbc.emulator = mednafen`, `.core = gbc`      | `-emulator mednafen -core gbc`      |
| `ares`     | `gbc.emulator = ares`, `.core = GameBoyColor` | `-emulator ares -core GameBoyColor` |
| `Gambatte` | `gbc.emulator = bizhawk`, `.core = Gambatte`  | `-emulator bizhawk -core Gambatte`  |
| `GBHawk`   | `gbc.emulator = bizhawk`, `.core = GBHawk`    | `-emulator bizhawk -core GBHawk`    |
| `SameBoy`  | `gbc.emulator = bizhawk`, `.core = SameBoy`   | `-emulator bizhawk -core SameBoy`   |
| `jgenesis` | `gbc.emulator = jgenesis`                     | `-emulator jgenesis`, empty `-core` |

## Checklist for the eight

| #   | Result on every one of the eight                                                            |
| --- | ------------------------------------------------------------------------------------------- |
| 4   | **Pass, both directions**, every file at its own md5 after one restore                      |
| 5   | **Pass**, two slots each, where each emulator was found to write                            |
| 6   | **N/A**                                                                                     |
| 7   | **Pass**, carried: each was launched from ES on the synced ROM, art and description present |
| 8   | **Pass**, every session read back from RomM by `status`, under `recent:`                    |
| 9   | **Pass.** 83 present and verified, 307 media present, gamelist byte-identical, 0 sent       |

## 4. Battery saves on the eight

| Row        | Files under `saves/gbc/`                                                    | Slots                                          |
| ---------- | --------------------------------------------------------------------------- | ---------------------------------------------- |
| `mesen`    | `<rom>.srm`, 32,768 B, and `<rom>.rtc`, 13 B                                | `libretro:battery`, `libretro:battery:rtc`     |
| `mgba`     | `<rom>.sav`, 32,816 B, the clock in a 48 B footer                           | `mgba:battery`                                 |
| `mednafen` | `<rom>.sav`, mGBA's file, 32,816 B                                          | `mgba:battery`                                 |
| `ares`     | `ares/Game Boy/<rom>.ram`, 32,768 B, and `<rom>.rtc`, 13 B                  | `ares:battery:ram`, `ares:battery:rtc`         |
| `Gambatte` | `bizhawk/Pokemon - Crystal Version (USA, Europe) (Rev A).SaveRAM`, 32,790 B | `bizhawk:battery`                              |
| `GBHawk`   | the same file, 32,768 B with no clock                                       | `bizhawk:battery`                              |
| `SameBoy`  | the same file, 32,816 B                                                     | `bizhawk:battery`                              |
| `jgenesis` | `jgenesis/gbc/<rom>.sav`, 32,768 B, and `<rom>.rtc`, 38 B                   | `jgenesis:battery:sav`, `jgenesis:battery:rtc` |

**Mesen writes the loose `.srm` and `.rtc` the `libretro` cores share**, so it needed no rule, and
it rewrites the `.rtc` on a launch with nothing saved. **mednafen read and saved into mGBA's plain
`.sav`** rather than its hashed name, as on `gb` (RB-273). **ares keeps its battery save in
`Game Boy`, `gb`'s directory name, and its states in `Game Boy Color`**, so the two halves of one
row sit in two trees. **BizHawk's three cores share one file named after BizHawk's own title**,
`(Rev A)` where the ROM file says `(Rev 1)`, learned from the state sidecar
`Pokemon - Crystal Version (USA, Europe) (Rev A).Gambatte`; each core rewrites it at its own size,
and the `.SaveRAM.bak` beside it is BizHawk's copy of the previous save. `jgenesis` keeps a `.gbc`
ROM's saves in `jgenesis/gbc/`, which `gb`'s record left for this pass to declare.

The six saves with their own rule and one state from each of the eight rows went out of the tree
and came back through one `saves restore 274994 --apply`: `restored 6 save(s) and 8 state(s),
failed 0, 562.4 KB`, exit 0, **all fourteen files at their own md5**. Mesen's `.srm` and `.rtc` went
out and came back the same way on their own, `restored 2 save(s) and 0 state(s), failed 0`.

## 5. States on the eight

| Row        | Directory under `saves/gbc/` | First                       | Second                      | Keys                     |
| ---------- | ---------------------------- | --------------------------- | --------------------------- | ------------------------ |
| `mesen`    | `mesen/SaveStates/`          | `_1.mss`, `6aeaf1fa...`     | `_2.mss`, `69d42a50...`     | `Shift+F1`, `Shift+F2`   |
| `mgba`     | `mgba/sstates/`              | `.ss1`, `fc7fac89...`       | `.ss2`, `6c884d72...`       | `Shift+F1`, `Shift+F2`   |
| `mednafen` | `mednafen/sstates/`          | `.<md5>.mc0`, `d414ff85...` | `.<md5>.mc1`, `53d5eb0f...` | `F2`, then `F7` and `F2` |
| `ares`     | `ares/Game Boy Color/`       | `.bs1`, `13ab6aef...`       | `.bs2`, `0352b419...`       | `F2`, then `F7` and `F2` |
| `Gambatte` | `bizhawk/sstates/Gambatte/`  | `QuickSave4`, `5afd4f67...` | `QuickSave2`, `127dc204...` | `Ctrl+F4`, `Ctrl+F2`     |
| `GBHawk`   | `bizhawk/sstates/GBHawk/`    | `QuickSave4`, `fa8ce222...` | `QuickSave2`, `6d8227ca...` | `Ctrl+F4`, `Ctrl+F2`     |
| `SameBoy`  | `bizhawk/sstates/SameBoy/`   | `QuickSave4`, `7ffe1369...` | `QuickSave2`, `a12342e2...` | `Ctrl+F4`, `Ctrl+F2`     |
| `jgenesis` | `jgenesis/states/`           | `_0.jst`, `e21bf5e4...`     | `_1.jst`, `8212e9a5...`     | `F2`, then `F7` and `F2` |

**`mesen`, `mgba`, `mednafen` and `ares` declare no state directory**, and each wrote to the one
above, which the supplement now declares for `gbc`. The md5 in mednafen's names is of the whole
`.gbc` inside the zip, `301899b8...`. **BizHawk names its states after the ROM file**, `(Rev 1)`,
while its battery save carries its own title. BizHawk's frame is inside the state; only the
`libretro` rows write a screenshot `.png`, and the rest have nothing to carry.

**On this machine `Ctrl+F1` never reached EmuHawk as a save**, from `keybd_event` with EmuHawk in the
foreground and the keys held 400 ms, and neither did a plain `F2`; `Ctrl+F2` and `Ctrl+F4` saved
every time, so the three BizHawk rows took slots 4 and 2. `config.ini` binds `Save State 1` to
`Ctrl+F1`, so this is the key's delivery and not BizHawk's slot.

## 8. Sessions

| Row        | Journal, UTC         | Length |
| ---------- | -------------------- | ------ |
| `mesen`    | 13:22:06 to 13:22:46 | 40s    |
| `mgba`     | 13:28:25 to 13:28:59 | 33s    |
| `mednafen` | 13:31:19 to 13:32:03 | 44s    |
| `ares`     | 13:34:05 to 13:35:30 | 1m 24s |
| `Gambatte` | 13:37:45 to 13:38:48 | 1m 2s  |
| `GBHawk`   | 13:44:59 to 13:45:43 | 43s    |
| `SameBoy`  | 13:47:47 to 13:48:45 | 57s    |
| `jgenesis` | 13:50:49 to 13:51:47 | 57s    |

Every one is on the server, rom 274994, as are the four `libretro` sessions in [libretro.md](libretro.md).
