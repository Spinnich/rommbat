---
summary: The certification record for `gba`: the two `libretro` rows that needed no code, `mgba` and `gpsp`.
read-when: When a result for one of these `gba` rows is needed, or before re-driving one.
---

# gba: `libretro`/`mgba` and `libretro`/`gpsp`

## `libretro`/`mgba`

|              |                                                                   |
| ------------ | ----------------------------------------------------------------- |
| Selected by  | **Nothing: RetroBat's default**                                   |
| Confirmed by | `-system gba -emulator libretro -core mgba` on the ES launch line |
| Result       | **Certified**                                                     |

| #   | Result                                                                                 |
| --- | -------------------------------------------------------------------------------------- |
| 4   | **Pass, both directions.** Class A, loose `<rom>.srm`, 131,072 B, `7d9fc2a6...`        |
| 5   | **Pass**, two slots, screenshot byte-checked                                           |
| 6   | **N/A.** `gba` is class A and the row wrote nothing but the game's own save            |
| 7   | **Pass.** Launched from ES after the sync, box art and description in `gamelist.xml`   |
| 8   | **Pass.** 12:08:58Z to 12:12:45Z, 3m 46s, rom 233631                                   |
| 9   | **Pass.** 201 present and verified, 759 media present, gamelist byte-identical, 0 sent |

**The maintainer played from the intro to Emerald's first save** and made two states, then left
ES; the detached `quit` pass exited 0 and `saves` showed the `.srm` and both states in step as
`libretro:battery`, `libretro:mgba:1` and `libretro:mgba:2`. **The save is real**, mixed bytes
against the boot write's uniform `0xFF`.

| Slot | State md5     | Size     | Screenshot md5 |
| ---- | ------------- | -------- | -------------- |
| 1    | `e4ba3d92...` | 11,780 B | `261753dd...`  |
| 2    | `be490aac...` | 28,272 B | `680147fd...`  |

**The declared `<directory>` is where the core wrote**, `saves/gba/libretro.mgba/`. The `.srm`
and slot 2's state and `.png` were moved out of the tree; the preview named the screenshot it
would bring back, and `saves restore 233631 --apply` answered `restored 1 save(s) and 1 state(s),
failed 0, 160 KB, with 1 screenshot(s)`, exit 0, **every file at its own md5**. The preview also
listed an older `autosave` row on the server, save 341 from 2026-09-19, written by another
client, and left it alone.

## `libretro`/`gpsp`

|              |                                                                   |
| ------------ | ----------------------------------------------------------------- |
| Selected by  | `gba.core = gpsp`, set with ES closed and removed afterwards      |
| Confirmed by | `-system gba -emulator libretro -core gpsp` on the ES launch line |
| Result       | **Certified**                                                     |

| #   | Result                                                                              |
| --- | ----------------------------------------------------------------------------------- |
| 4   | **Pass, both directions.** The shared `.srm`, 131,072 B, `bd917a0a...`              |
| 5   | **Pass**, two slots, screenshot byte-checked                                        |
| 6   | **N/A**                                                                             |
| 7   | **Pass**, carried                                                                   |
| 8   | **Pass.** 12:17:46Z to 12:19:48Z, 2m 1s, rom 233631                                 |
| 9   | **Pass.** Nothing to do, gamelist byte-identical, 55 states already in step, 0 sent |

**gpsp loaded the save mGBA made**, and the maintainer saved again in the game. The new `.srm`
differs from mGBA's in 62,543 bytes at the same size, which fits Emerald writing each save to
the other of its two save blocks, and went up as a new version of `libretro:battery`: the restore
preview listed it as the newest of three server saves, mGBA's save 390 and the older `autosave`
below it. So the two cores share one save, as the `megadrive` cores do (RB-277).

| Slot | State md5     | Size     | Screenshot md5 |
| ---- | ------------- | -------- | -------------- |
| 1    | `bd29de09...` | 28,230 B | `9dc85359...`  |
| 2    | `a8ca1732...` | 29,182 B | `5fc2d420...`  |

**The declared `<directory>` is where the core wrote**, `saves/gba/libretro.gpsp/`, beside mGBA's
states rather than over them. The `.srm` and slot 2's state and `.png` went out of the tree and
came back through `saves restore 233631 --apply`, `with 1 screenshot(s)`, exit 0, **every file at
its own md5**.
