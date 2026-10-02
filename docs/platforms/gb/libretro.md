---
summary: The certification record for `gb`: the six `libretro` rows.
read-when: When a result for one of these `gb` rows is needed, or before re-driving one.
---

# gb: The six `libretro` rows

|                  | Selected by                                 | Confirmed on the ES launch line      |
| ---------------- | ------------------------------------------- | ------------------------------------ |
| `gambatte`       | **Nothing: RetroBat's default**             | `-emulator libretro -core gambatte`  |
| `mesen-s`        | `gb.emulator = libretro`, `.core = mesen-s` | `-core mesen-s -state_slot 3`        |
| `bsnes`          | `gb.core = bsnes`                           | `-core bsnes -state_slot 3`          |
| `tgbdual`        | `gb.core = tgbdual`                         | `-core tgbdual -state_slot 3`        |
| `DoubleCherryGB` | `gb.core = DoubleCherryGB`                  | `-core DoubleCherryGB -state_slot 3` |
| `sameboy`        | `gb.core = sameboy`                         | `-core sameboy -state_slot 3`        |

Each override was set with ES closed, and the `gb` keys were cleared when the pass ended; they then
matched the copy taken before the first, `R:\rommbat-evidence\gb\es_settings.before.cfg`.

| #   | Result on every one of the six                                                                    |
| --- | ------------------------------------------------------------------------------------------------- |
| 4   | **Pass, both directions.** Class A, the shared loose `<rom>.srm`, 32,768 B, as `libretro:battery` |
| 5   | **Pass**, two slots each, the screenshot byte-checked                                             |
| 6   | **N/A.** `gb` is class A                                                                          |
| 7   | **Pass.** Launched from ES after the sync, box art, marquee and description in `gamelist.xml`     |
| 8   | **Pass**, every session read back from RomM by `status`, under `recent:`                          |
| 9   | **Pass.** 107 present and verified, 414 media present, gamelist byte-identical, 0 sent            |

| Core             | Session, UTC         | `.srm` after  | Slot 1 state, png            | Slot 2 state, png            |
| ---------------- | -------------------- | ------------- | ---------------------------- | ---------------------------- |
| `gambatte`       | 18:32:20 to 18:34:01 | `0867d776...` | `ffbbb6d1...`, `aa9c25f7...` | `8cfd1650...`, `658479cd...` |
| `mesen-s`        | 18:37:01 to 18:38:06 | `91d956f9...` | `72ade192...`, `fcbe8508...` | `e26eadd2...`, `bb9939a2...` |
| `bsnes`          | 18:39:32 to 18:40:30 | `96e80b02...` | `59601523...`, `f30b193d...` | `0e2272a1...`, `7b70f46a...` |
| `tgbdual`        | 18:41:52 to 18:42:43 | `96e0dd8c...` | `9448e44b...`, `bf3fbefe...` | `6b41aeee...`, `83715a98...` |
| `DoubleCherryGB` | 18:44:13 to 18:44:56 | `f911a868...` | `9a929864...`, `8522cdaf...` | `63a35a91...`, `a67ed996...` |
| `sameboy`        | 18:46:33 to 18:47:24 | `135981ed...` | `01c417ce...`, `80aa7fe5...` | `073465aa...`, `06e37bed...` |

**The maintainer played the stock row from the intro to Yellow's first save** and made two states,
then left ES; each later core loaded the `.srm` the one before it left, and the maintainer saved
again. **Every row's `.srm` and slot 2's state and `.png` went out of the tree and came back through
`saves restore 153392 --apply`**, `restored 1 save(s) and 1 state(s), failed 0, ... with 1
screenshot(s)`, exit 0, every file at its own md5, and each returned image was that slot's, distinct
from slot 1's. The first preview listed an older save on the server, save 191 from 2026-09-01,
written by another client, and left it alone.

**The declared `<directory>` is where every core wrote**, `saves/gb/libretro.<core>/`. ES passed
`-state_slot 3` from the second row on, and RetroArch wrote slots 1 and 2 regardless, as RB-261
says.

**`bsnes` keeps the save itself.** RetroArch logged `Content loading skipped. Implementation will
load it on its own` and `Skipping SRAM load`, and no SRAM write on exit, yet the core read the seed
and wrote the `.srm` back with the maintainer's save in it.

**Three cores write a `.rtc` beside the `.srm` even for Yellow**, which has no clock; on a clock
cartridge `gambatte` writes one too, and it syncs as `libretro:battery:rtc` ([index.md](index.md#a-cartridge-with-a-clock-pokemon-silver-on-gb)).
