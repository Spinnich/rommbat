---
summary: The certification record for `gba`: the seven rows that needed code.
read-when: When a result for one of these `gba` rows is needed, or before re-driving one.
---

# gba: The seven rows that needed code

**All seven certified on 2026-09-22, on the deploy of this branch.** Each was driven first on the
`megadrive` build, where its save sat on disk unread; the branch's first flush sent **9 saves and
8 states**, everything those sessions had left unsyncable.

|              | Selected by                                       | Confirmed on the ES launch line         |
| ------------ | ------------------------------------------------- | --------------------------------------- |
| `mgba`       | `gba.emulator = mgba`, `.core = mgba`             | `-emulator mgba -core mgba`             |
| `mednafen`   | `gba.emulator = mednafen`, `.core = gba`          | `-emulator mednafen -core gba`          |
| `mesen`      | `gba.emulator = mesen`                            | `-emulator mesen`, empty `-core`        |
| `bizhawk`    | `gba.emulator = bizhawk`, `.core = mGBA`          | `-emulator bizhawk -core mGBA`          |
| `jgenesis`   | `gba.emulator = jgenesis`                         | `-emulator jgenesis`                    |
| mednafen_gba | `gba.emulator = libretro`, `.core = mednafen_gba` | `-emulator libretro -core mednafen_gba` |
| `ares`       | `gba.emulator = ares`, `.core = GameBoyAdvance`   | `-emulator ares -core GameBoyAdvance`   |

Each override was set with ES closed, and `es_settings.cfg` was put back from the copy taken
before the first, `R:\rommbat-evidence\gba\es_settings.before.cfg`, when the pass ended.

## Checklist for the seven

| #   | Result on every one of the seven                                                            |
| --- | ------------------------------------------------------------------------------------------- |
| 4   | **Pass, both directions**, every file at its own md5 after one restore                      |
| 5   | **Pass**, two slots each, where each emulator was found to write                            |
| 6   | **N/A**                                                                                     |
| 7   | **Pass**, carried: each was launched from ES on the synced ROM, art and description present |
| 8   | **Pass**, every session read back from RomM by `status`, under `recent:`                    |
| 9   | **Pass.** 201 present and verified, 759 media present, gamelist byte-identical, 0 sent      |

## 4. Battery saves on the seven

| Row          | File under `saves/gba/`                     | Size          | Slot                                | md5                          |
| ------------ | ------------------------------------------- | ------------- | ----------------------------------- | ---------------------------- |
| `mgba`       | `<rom>.sav`                                 | 131,088 B     | `mgba:battery`                      | `4444c841...`                |
| `mednafen`   | `<rom>.605b89b6....sav`                     | 131,072 B     | `mednafen:battery`                  | `c1345454...`                |
| `mesen`      | `<rom>.sav` and `<rom>.rtc`                 | 131,072, 19 B | `mgba:battery`, `mesen:battery:rtc` | `a7b096dc...`, `da65e7fe...` |
| `bizhawk`    | `bizhawk/<rom>.SaveRAM`                     | 131,088 B     | `bizhawk:battery`                   | `2e7801cb...`                |
| `jgenesis`   | `jgenesis/gba/<rom>.sav` and `.rtc`         | 131,072, 59 B | `jgenesis:battery:sav`, `:rtc`      | `61d7e3cf...`, `2a2ed802...` |
| mednafen_gba | `<rom>.zip#<rom>.605b89b6....sav`           | 131,072 B     | `libretro:battery:sav`              | `8d1ecd17...`                |
| `ares`       | `ares/Game Boy Advance/<rom>.flash`, `.rtc` | 131,072, 18 B | `ares:battery:flash`, `:rtc`        | `5db8042f...`, `7ba2e8d4...` |

All nine files went out of the tree with one state per row, and came back through one `saves
restore 233631 --apply`: `restored 9 save(s) and 7 state(s), failed 0, 1.5 MB, with 1
screenshot(s)`, exit 0, **all seventeen files at their own md5**. The preview named every path,
including the zip-member name rebuilt from the ROM and the hashed one.

**The loose `<rom>.sav` is one save for three emulators and uploads as `mgba:battery`**, by the
maintainer's ruling, as `nes` uploads its shared plain `.sav` as `mesen:battery`. Mesen's copy went
up first as that slot; mGBA's own, put back afterwards, went up as its next version, and the
restore brought back the newest, mGBA's. **Mesen reads mGBA's 131,088 B file**: launched on it,
Emerald continued from the save with no clock message, and on exit Mesen wrote the file back at
131,072 B, mGBA's flash byte for byte with the 16-byte footer dropped, `505a9dd2...`, and its own
`.rtc` beside it. That went up as the next `mgba:battery` version through the `quit` pass, with
nothing saved in the game. mednafen does not read mGBA's file: it refused it (below).

**mGBA standalone and BizHawk append 16 bytes of clock to the flash**, 131,088 B against 131,072.
Both read the 131,072 B seed and wrote their own size back. Mesen, jgenesis and ares keep the clock
in an `.rtc` instead, and each `.rtc` is its own class B slot, so the clock travels with the save.

**mednafen refused mGBA's file and cannot be given its own while it is there.** The first
mednafen session found the loose `.sav` mGBA had written and logged `Save game memory file ... is
an incorrect size(131088 bytes). The correct size is 65536 or 131072 bytes.`, and the game did not
load. mednafen opens the plain name whenever it exists (RB-273), so on a device where mGBA
standalone has run, mednafen cannot play Emerald until that file moves. The session that passed
ran with mGBA's file out of the tree and the 131,072 B seed under mednafen's hashed name. **The
game then said its internal battery had run dry**: mednafen does not carry the cartridge clock
through a save with none in it, where Mesen, seeded the same way, did not complain. RB-289.

**mednafen_gba ignores the `.srm` its two sibling cores share.** RetroArch logged `Redirecting save
file to ...srm` and then `Skipping SRAM load`, and the core kept its own file under mednafen's name
for RetroArch's `archive#member` path. So it cannot share `libretro:battery`, and by the
maintainer's ruling it takes `libretro:battery:sav`, class B's per-extension slot. A restore names
the file from the zip's one member and that member's md5. **The other two formats were driven
afterwards**, each booted once from `emulatorLauncher` with the real saves moved aside: a `.7z`
built with RetroBat's own `7za.exe` gave `<rom>.7z#<rom>.605b89b6....sav`, the same form and md5,
which the rule claims; a bare `.gba` gave `<rom>.605b89b6....sav`, **mednafen standalone's own
name**, hashed although a plain `<rom>.sav` was present, so on a library of bare `.gba` files the
two mednafens share one file and it uploads as `mednafen:battery`. A restore of
`libretro:battery:sav` reads the member out of a zip only, so for a `.7z` it is refused as
unnameable until RomMBat can read inside one (#221). Both boot writes were blank and were moved out of the tree before any flush. RB-290.

## 5. States on the seven

| Row          | Directory under `saves/gba/` | First                       | Second                      | Keys                     |
| ------------ | ---------------------------- | --------------------------- | --------------------------- | ------------------------ |
| `mgba`       | `mgba/sstates/`              | `.ss1`, `42c63fd6...`       | `.ss2`, `575a3683...`       | `Shift+F1`, `Shift+F2`   |
| `mednafen`   | `mednafen/sstates/`          | `.<md5>.mc0`, `39944e7f...` | `.<md5>.mc1`, `8830b0d3...` | `F2`, then `F7` and `F2` |
| `mesen`      | `mesen/SaveStates/`          | `_1.mss`, `e9d655e0...`     | `_2.mss`, `bf668503...`     | `Shift+F1`, `Shift+F2`   |
| `bizhawk`    | `bizhawk/sstates/mGBA/`      | `QuickSave3`, `d683652e...` | `QuickSave2`, `e4ab8604...` | the pad, then `Ctrl+F2`  |
| `jgenesis`   | `jgenesis/states/`           | `_0.jst`, `574f9b9f...`     | `_1.jst`, `2b860589...`     | the pad, then `F7`, `F2` |
| mednafen_gba | `libretro.mednafen_gba/`     | `state1`, `ecf90f36...`     | `state2`, `2da3ac7f...`     | the pad, twice           |
| `ares`       | `ares/Game Boy Advance/`     | `.bs1`, `73f17f68...`       | `.bs2`, `6d7b0d0f...`       | `F2`, then `F7` and `F2` |

**`mgba`, `mednafen`, `mesen` and `ares` declare no state directory**, and each wrote to the one
above, which the supplement now declares for `gba`: mGBA where RetroBat's `config.ini` sets
`savestatePath`, the other three as on `nes` and `megadrive`. `bizhawk` took ES's `-state_slot 3`
and the others did not (RB-269 and RB-275). `jgenesis` and `bizhawk` wrote in their own trees
and were mirrored into `saves/` with a `.txt` sidecar, which for BizHawk reads `Pokemon - Emerald
Version (USA, Europe).mGBA`: here it names the game after the ROM file, having no title of its own.

**Only mednafen_gba writes a screenshot**, `29beb94b...` for slot 1 and `5a2bc558...` for slot 2,
and the restore brought slot 2's back at its own md5. BizHawk's frame is inside the state,
`2ed26d0d...` and `70009970...` for its two slots. The rest have nothing to carry, and the preview
says so. ares states are a fixed 530,840 B, so only the md5 tells two apart, and ares again ignored
`WM_CLOSE` and was killed after its states were on disk.

## 8. Sessions

| Row          | Journal, UTC         | Length |
| ------------ | -------------------- | ------ |
| `mgba`       | 12:26:25 to 12:27:42 | 1m 17s |
| `mednafen`   | 12:33:42 to 12:35:53 | 2m 11s |
| `mesen`      | 12:39:37 to 12:40:50 | 1m 13s |
| `bizhawk`    | 12:42:55 to 12:44:40 | 1m 45s |
| `jgenesis`   | 12:46:48 to 12:47:56 | 1m 8s  |
| mednafen_gba | 12:49:46 to 12:50:39 | 53s    |
| `ares`       | 12:51:39 to 12:52:43 | 1m 4s  |

Every pair is correlated in the journal, every `quit` pass exited 0, and **every one is on the
server**: `status` lists the ten newest sessions under `recent:`, a line this pass added because it
printed only the newest, and the seven appear there at these times, rom 233631, alongside the
`nosgba` session at 12:58:48Z and the refused mednafen attempt at 12:30:51Z. One more, 13:32:28Z to
13:32:53Z, is the maintainer launching NO$GBA from RetroBat's own emulator menu to look for its
state key; that menu writes `gba.emulator` into `es_settings.cfg`, which is why a `nosgba` key
reappeared there after the file was put back.
