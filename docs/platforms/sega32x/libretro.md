---
summary: The certification record for `sega32x`: the `libretro`/`picodrive` row.
read-when: When a result for `sega32x` under `libretro` is needed, or before re-driving it.
---

# sega32x: `libretro`/`picodrive`

|              | `libretro`/`picodrive`                                                  |
| ------------ | ----------------------------------------------------------------------- |
| Selected by  | nothing: the stock row, no `sega32x.emulator` and no per-game override  |
| Confirmed by | `retroarch.exe ... -L picodrive_libretro.dll` in `emulatorLauncher.log` |

## Checklist

| #   | Result                                                                                               |
| --- | ---------------------------------------------------------------------------------------------------- |
| 1-3 | **Pass**, the system's ([index.md](index.md#steps-1-2-and-3-for-every-row))                          |
| 4   | **Pass, both directions**, class A: Chaotix's SRAM and NBA Jam's EEPROM up and back at their own md5 |
| 5   | **Pass**: two slots made in ES, one deleted and restored, state and screenshot md5-identical         |
| 6   | **N/A**                                                                                              |
| 7   | **Pass**: both games launched from ES on the synced ROM, art and description present                 |
| 8   | **Pass**: every session read back by `status` under `recent:`                                        |
| 9   | **Pass**: 0 downloaded, 0 written, `gamelists: all 10 unchanged`, gamelist byte-identical, 0 sent    |

## 4. Battery saves

| Game    | File under `saves/sega32x/` | Size    | md5           | Holds                                              |
| ------- | --------------------------- | ------- | ------------- | -------------------------------------------------- |
| Chaotix | `<rom>.srm`                 | 1,024 B | `06172308...` | slot 1 at the hub, the 512 B save in the odd bytes |
| NBA Jam | `<rom>.srm`                 | 8,192 B | `9cf09a17...` | the 256 B EEPROM format first, then zeros          |

**The maintainer played Chaotix to the hub**, about nine minutes, and the file reached its final
md5 on exit. RetroArch's 10 s `autosave_interval` writes the `.srm` 24 s after launch, before the game
has saved anything, so an early file is no evidence of a save. **NBA Jam's file is the boot format**,
written within seconds; entering initials in two later sessions left it at the same md5. Both went
up as `libretro:battery`, server saves 657 and 658, and read back `in step`. **Both were deleted and
`saves restore 209635 --apply` and `saves restore 209644 --apply` brought each back at its own md5**,
`restored 1 save(s)` and `restored 2 save(s) ... failed 0`, the second also returning ares's NBA Jam
file. Each is a loose `<rom>.srm`, class A, as `save_shapes.json` has recorded for `sega32x`.

**This file is the seed for every other row**, which reads it in its own layout
([index.md](index.md#the-install-this-was-measured-on)).

## 5. States

| Slot | File under `saves/sega32x/libretro.picodrive/` | Size      | Screenshot              |
| ---- | ---------------------------------------------- | --------- | ----------------------- |
| 4    | `<rom>.state4`                                 | 165,798 B | `856a2504...`, 14,757 B |
| 5    | `<rom>.state5`                                 | 167,329 B | `95650f89...`, 9,043 B  |

**The maintainer made both in ES, and RetroArch numbered them 4 and 5**, though the boot pass's
slots 1 and 3 had been deleted from the directory first, so read the slot from the file (RB-261). Both went
up with their screenshot linked. **`state5` and its `.png` were deleted and
`saves restore 209635 --apply` brought both back**, `d850a4ca...` and `95650f89...`, md5-identical,
with `restored 0 save(s) and 1 state(s), failed 0 ... with 1 screenshot(s)`. The two screenshots
differ, so the link is to this state's frame and no other.

## 8. Sessions

| Game    | Journal, UTC         | Length |
| ------- | -------------------- | ------ |
| NBA Jam | 16:50:18 to 16:51:21 | 1m 3s  |
| Chaotix | 16:51:40 to 17:01:14 | 9m 33s |
| NBA Jam | 17:06:38 to 17:07:44 | 1m 5s  |
| NBA Jam | 17:07:49 to 17:08:35 | 46s    |

The last two ran on this row because the game still carried its `libretro` pin; they are the two
sessions in which initials changed nothing.
