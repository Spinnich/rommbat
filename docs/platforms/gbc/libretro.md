---
summary: The certification record for `gbc`: the four `libretro` rows.
read-when: When a result for one of these `gbc` rows is needed, or before re-driving one.
---

# gbc: The four `libretro` rows

|                  | Selected by                                  | Confirmed on the ES launch line      |
| ---------------- | -------------------------------------------- | ------------------------------------ |
| `gambatte`       | **Nothing: RetroBat's default**              | `-emulator libretro -core gambatte`  |
| `tgbdual`        | `gbc.emulator = libretro`, `.core = tgbdual` | `-core tgbdual -state_slot 3`        |
| `sameboy`        | `gbc.core = sameboy`                         | `-core sameboy -state_slot 3`        |
| `DoubleCherryGB` | `gbc.core = DoubleCherryGB`                  | `-core DoubleCherryGB -state_slot 3` |

Each override was set with ES closed, and the `gbc` keys were cleared when the pass ended. ES
rewrote `es_settings.cfg` on its own exit during the pass, moving `LastSystem` to `gbc` and dropping
`Language`; the copy taken first is `R:\rommbat-evidence\gbc\es_settings.before.cfg`.

| #   | Result on every one of the four                                                                              |
| --- | ------------------------------------------------------------------------------------------------------------ |
| 4   | **Pass, both directions.** Class A `.srm` as `libretro:battery`, beside the `.rtc` as `libretro:battery:rtc` |
| 5   | **Pass**, two slots each, the screenshot byte-checked                                                        |
| 6   | **N/A.** `gbc` is class A                                                                                    |
| 7   | **Pass.** Launched from ES after the sync, box art, marquee and description in `gamelist.xml`                |
| 8   | **Pass**, every session read back from RomM by `status`, under `recent:`                                     |
| 9   | **Pass.** 83 present and verified, 307 media present, gamelist byte-identical, 0 sent                        |

| Core             | Session, UTC         | `.srm` after  | `.rtc` after        | Slot 1 png    | Slot 2 state, png            |
| ---------------- | -------------------- | ------------- | ------------------- | ------------- | ---------------------------- |
| `gambatte`       | 13:05:42 to 13:07:19 | `05dcc927...` | 8 B, `5f8f8410...`  | `b47b53a3...` | `5bc1a3ab...`, `1167691f...` |
| `tgbdual`        | 13:11:11 to 13:12:26 | `0d4fc8e0...` | 4 B, `c8018a2c...`  | `ad1437d6...` | `1a658125...`, `cea4f8c0...` |
| `sameboy`        | 13:18:01 to 13:18:53 | `2b97141d...` | 32 B, `d8ee6dda...` | `522faf4b...` | `6a365769...`, `7d41cea4...` |
| `DoubleCherryGB` | 13:20:09 to 13:20:56 | `688ff125...` | 4 B, `e0719a22...`  | `fe28069f...` | `51c75433...`, `784d10fa...` |

**The maintainer played the stock row from the intro to Crystal's first save**, setting the clock
to about 9 AM, and made two states, then left ES; each later core loaded the `.srm` the one before
it left, and the maintainer saved again. **After each row its `.srm`, `.rtc`, slot 2's state and
slot 2's `.png` went out of the tree and came back through `saves restore 274994 --apply`**,
`restored 2 save(s) and 1 state(s), failed 0, ... with 1 screenshot(s)`, exit 0, every file at its
own md5, and each returned image was that slot's, distinct from slot 1's. From the second row on,
the preview also listed the earlier cores' versions on the server and left them alone.

**The declared `<directory>` is where every core wrote**, `saves/gbc/libretro.<core>/`. ES passed
`-state_slot 3` from the second row on, and RetroArch wrote slots 1 and 2 regardless, as RB-261
says. **Every core writes the `.rtc` on exit as RAM type #1**, beside the `.srm` as type #0, and
the clock it holds is not the same thing on any two of them ([index.md](index.md#no-change-of-row-that-was-checked-kept-the-clock)).
