---
summary: The certification record for `megadrive`: the four `libretro` rows.
read-when: When a result for one of these `megadrive` rows is needed, or before re-driving one.
---

# megadrive: The four `libretro` rows

|              | `genesis_plus_gx`                          | `genesis_plus_gx_wide`                  | `picodrive`                  | `fbneo`                                 |
| ------------ | ------------------------------------------ | --------------------------------------- | ---------------------------- | --------------------------------------- |
| Selected by  | **Nothing: RetroBat's default**            | `megadrive.core = genesis_plus_gx_wide` | `megadrive.core = picodrive` | `megadrive.core = fbneo`                |
| Confirmed by | `-emulator libretro -core genesis_plus_gx` | `genesis_plus_gx_wide_libretro.dll`     | `-core picodrive`            | `fbneo_libretro.dll ... --subsystem md` |
| Result       | **Certified**                              | **Certified**                           | **Certified**                | **Driven, not certifiable here**        |

**The stock row names its core on the launch line.** With neither key set, ES filled in
`-emulator libretro -core genesis_plus_gx`, the first `es_systems.cfg` lists, and the three
overrides were set in `es_settings.cfg` with ES closed and removed afterwards, which is how the
install was found and left.

## Checklist for the three certified `libretro` rows

| #   | `genesis_plus_gx`                                     | `genesis_plus_gx_wide`                       | `picodrive`                                         |
| --- | ----------------------------------------------------- | -------------------------------------------- | --------------------------------------------------- |
| 4   | **Pass, both directions.** Class A, `.srm`, 980 B     | **Pass, both directions.** The shared `.srm` | **Pass, both directions.** The shared `.srm`, 16 KB |
| 5   | **Pass**, screenshot byte-checked                     | **Pass**, screenshot byte-checked            | **Pass**, screenshot byte-checked                   |
| 7   | **Pass.** Box art and description on screen           | **Pass**, carried                            | **Pass**, carried                                   |
| 8   | **Pass.** 22:59:42Z to 23:00:38Z, 56s                 | **Pass.** 23:06:01Z to 23:06:36Z, 34s        | **Pass.** 23:53:02Z to 23:54:08Z, 1m 5s             |
| 9   | **Pass.** 0 downloaded, 0 written, gamelist identical | **Pass**                                     | **Pass**                                            |

## 4. Battery save on the three

All three write `saves/megadrive/<rom>.srm`, and each session picked a data-select slot the one
before had not, so each shows the next reading the last.

| Row                    | Before        | After         | Size     | Server rows at the restore |
| ---------------------- | ------------- | ------------- | -------- | -------------------------- |
| `genesis_plus_gx`      | `ed4db2dc...` | `22103c18...` | 980 B    | newest of 2                |
| `genesis_plus_gx_wide` | `22103c18...` | `b60c4918...` | 980 B    | newest of 3                |
| `picodrive`            | `b60c4918...` | `6ba79e41...` | 16,384 B | newest of 4                |

Each went up as a new version of `libretro:battery` through the detached `quit` pass, which
exited 0 every time, was moved out of the tree with its slot 2 state, and came back through
`saves restore 203767 --apply` at its own md5, exit 0.

**`picodrive` read the file the other two wrote and wrote it back sixteen times the size.** The
earlier slots showed as used on its data-select screen. Its 16,384 B file matches the 980 B one in
all but the 12 bytes of the new slot, and is zero from byte 980 on: Genesis Plus GX trims the
file at the last used byte (979), and PicoDrive keeps the whole SRAM window. So the three cores
share one save across two sizes, each switch uploads a new version, and that is right, since the
bytes change. RB-277.

## 5. States on the three

| Row                    | Slot | State md5     | Screenshot md5 |
| ---------------------- | ---- | ------------- | -------------- |
| `genesis_plus_gx`      | 1    | `60fe8ead...` | `92a12709...`  |
| `genesis_plus_gx`      | 2    | `89f0541d...` | `ea0da9e3...`  |
| `genesis_plus_gx_wide` | 1    | `548d62db...` | `191561da...`  |
| `genesis_plus_gx_wide` | 2    | `66e91029...` | `ccbde430...`  |
| `picodrive`            | 1    | `3a3987dc...` | `24e9858f...`  |
| `picodrive`            | 2    | `3d12e9c7...` | `b91e1fad...`  |

**The declared `<directory>` is where each core wrote**, `saves/megadrive/libretro.<core>/`, and
RetroArch's log shows it choosing the slot, `found_last_state_slot: #0` against an empty
directory, as RB-261 describes. On each row slot 2's state and `.png` were moved out with the
`.srm`; the preview named the screenshot it would bring back, the apply answered `with 1
screenshot(s)`, and **every file came back at its own md5**, slot 2's image differing from slot
1's on every row.

## `libretro`/`fbneo`: driven, and not certifiable on this library

**FBNeo showed its own "Unknown Romset" screen and never started the game.** RetroBat launched it
as `fbneo_libretro.dll "<rom>.zip" --subsystem md`, and under that subsystem FBNeo takes the
driver name from the file name: `md_` plus the stem. No-Intro's name is no FBNeo driver.

**The name is the whole barrier**, measured outside the ROM tree with RetroArch started directly
on two copies of one ROM, Sonic The Hedgehog (USA, Europe):

| File                                   | What FBNeo did                                                      |
| -------------------------------------- | ------------------------------------------------------------------- |
| `Sonic The Hedgehog (USA, Europe).zip` | searched for no romset at all, 640x480, the "Unknown Romset" screen |
| `sonic.zip`, the same bytes            | `Romset found`, 320x224, the Mega Drive's resolution: the game ran  |

So this row cannot boot any of the 252 games, and it is not a RomMBat result: FBNeo's set names
are its own dat's, and RomM hands out the library's names. **Recorded as not certifiable on a
No-Intro library, by the maintainer's ruling**, rather than as a failure owed a fix. The one
lock-on driver in this core that could be the test game is `md_sks3`, a two-ROM lock-on set rather
than No-Intro's combined file. Nothing was written under `saves/`; the ES launch left two empty
directories, `saves/megadrive/fbneo/` and `saves/megadrive/libretro.fbneo/`. RB-278.
