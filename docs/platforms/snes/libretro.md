---
summary: The certification record for `snes`: the seven `libretro` rows.
read-when: When a result for one of these `snes` rows is needed, or before re-driving one.
---

# snes: The seven `libretro` rows

|                 | Selected by                                    | Confirmed on the ES launch line     |
| --------------- | ---------------------------------------------- | ----------------------------------- |
| `snes9x`        | **Nothing: RetroBat's default**                | `-emulator libretro -core snes9x`   |
| `bsnes-jg`      | `snes.emulator = libretro`, `.core = bsnes-jg` | `-core bsnes-jg -state_slot 3`      |
| `bsnes`         | `snes.core = bsnes`                            | `-core bsnes -state_slot 3`         |
| `bsnes_hd_beta` | `snes.core = bsnes_hd_beta`                    | `-core bsnes_hd_beta -state_slot 3` |
| `mednafen_snes` | `snes.core = mednafen_snes`                    | `-core mednafen_snes -state_slot 3` |
| `mesen-s`       | `snes.core = mesen-s`                          | `-core mesen-s -state_slot 3`       |
| `snes9x2005`    | `snes.core = snes9x2005`                       | `-core snes9x2005 -state_slot 3`    |

Each override was set with ES closed, and the `snes` keys were cleared when the pass ended. ES
rewrote `es_settings.cfg` on its own exit during the pass, re-indenting it, moving `LastSystem` and
adding `Language`; the copy taken first is `R:\rommbat-evidence\snes\es_settings.before.cfg`.

| #   | Result on every one of the seven                                                              |
| --- | --------------------------------------------------------------------------------------------- |
| 4   | **Pass, both directions.** Class A `.srm` as `libretro:battery`                               |
| 5   | **Pass**, two slots each, the screenshot byte-checked                                         |
| 6   | **N/A.** `snes` is class A                                                                    |
| 7   | **Pass.** Launched from ES after the sync, box art, marquee and description in `gamelist.xml` |
| 8   | **Pass**, every session read back from RomM by `status`, under `recent:`                      |
| 9   | **Pass.** 276 present and verified, 1,075 media present, gamelist byte-identical, 0 sent      |

| Core            | Session, UTC         | `.srm` after  | Slot 1 png    | Slot 2 state, png            |
| --------------- | -------------------- | ------------- | ------------- | ---------------------------- |
| `snes9x`        | 11:17:53 to 11:18:58 | `514e7309...` | `3ef0bcf6...` | `c012c9df...`, `af4d6ded...` |
| `bsnes-jg`      | 11:22:54 to 11:23:35 | `f07bde08...` | `3eb5cc10...` | `99d925c6...`, `7213c135...` |
| `bsnes`         | 11:24:51 to 11:26:22 | `c55ad6cd...` | `6b6c6fde...` | `7e1f072e...`, `481717e7...` |
| `bsnes_hd_beta` | 11:27:28 to 11:28:09 | `c2bce74f...` | `c99165dd...` | `58f71a2b...`, `b7f3ff86...` |
| `mednafen_snes` | 11:29:33 to 11:30:17 | `663ff2cf...` | `68c0ec7b...` | `e9b5ac91...`, `145f417d...` |
| `mesen-s`       | 11:31:26 to 11:32:04 | `4e060f14...` | `b3d34e1e...` | `2ea49d53...`, `488d22ee...` |
| `snes9x2005`    | 11:33:07 to 11:33:43 | `85a555e3...` | `69a6f07a...` | `a67305a3...`, `993d2299...` |

**The maintainer registered a name on the stock row**, saved with Save and Continue and made two
states; each later core continued the `.srm` the one before it left, saved again, and made two
states. **After each row its `.srm`, slot 2's state and slot 2's `.png` went out of the tree and
came back through `saves restore 200280 --apply`**, `restored 1 save(s) and 1 state(s), failed 0,
... with 1 screenshot(s)`, exit 0, every file at its own md5, and each returned image was that
slot's, distinct from slot 1's.

**The declared `<directory>` is where every core wrote**, `saves/snes/libretro.<core>/`. ES passed
`-state_slot 3` from the second row on, and RetroArch wrote slots 1 and 2 regardless, as finding
261 says. **`mednafen_snes` leaves an empty `<rom>.rtc` on every exit**, which RomMBat now passes
over without reporting (RB-319).
