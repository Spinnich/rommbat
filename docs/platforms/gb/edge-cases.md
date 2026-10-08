---
summary: The `gb` edge-case pass, an MBC2 cartridge on all fourteen rows and a clock cartridge saved off the stock row, against the save rules.
read-when: Before relying on how a `gb` row stores an MBC2 or clock save, or when changing a `gb` battery rule.
---

# gb: edge cases

A targeted pass, not a re-certification: each row stays certified for what [index.md](index.md)
records. It covers the two cartridge kinds Pokemon Yellow, the certifying game, does not: MBC2's
512 half-bytes of RAM, and a real-time clock saved in game on every row rather than only the stock
one. **Every file either game wrote lands in a slot an existing `gb` rule reads**, but for
Silver's under `jgenesis/gbc/`, the gap [index.md](index.md#a-cartridge-with-a-clock-pokemon-silver-on-gb)
records, so no rule changed; what moved is two cross-row facts, below.

## Test games

| Edge case                  | Game                                                                                                                         | Why                                                          | First save                                  |
| -------------------------- | ---------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------ | ------------------------------------------- |
| MBC2 with battery          | Final Fantasy Adventure (USA), RomM rom 152928, filed under `gb`, unpinned                                                   | Header `0x06`, no Color or SGB flag, saves anywhere          | Select, Save, a slot, after the first fight |
| A clock, off the stock row | Pokemon Silver, RomM rom 275002, synced into `gb` as [index.md](index.md#a-cartridge-with-a-clock-pokemon-silver-on-gb) says | MBC3 with timer, the only clock title already driven on `gb` | Start, SAVE, from the bedroom               |

Golf (World), Kirby's Pinball Land and F-1 Race are also header `0x06` and save scores rather than
at a point the player picks.

## The install

The agent tree at RetroBat 8.2.1, paired as the approver account, with this branch deployed. The
`libretro` rows were launched through `emulatorLauncher` and driven by keyboard; the standalone
rows through `emulatorLauncher` with ES's player-1 arguments and a virtual pad, since their
keyboard maps did not reach the game. Each row was seeded from the file the row before it left, in
that row's own form, and then saved in game.

## Final Fantasy Adventure: MBC2

| Row                                               | File under `saves/gb/`                          | After an in-game save | Slot               |
| ------------------------------------------------- | ----------------------------------------------- | --------------------- | ------------------ |
| `libretro`/`gambatte`                             | `<rom>.srm`                                     | 8,192 B               | `libretro:battery` |
| `libretro`/`tgbdual`, `DoubleCherryGB`            | `<rom>.srm`, and the 4 B `<rom>.rtc` (RB-295)   | 8,192 B               | `libretro:battery` |
| `libretro`/`mesen-s`, `bsnes`, `sameboy`; `mesen` | `<rom>.srm`                                     | 512 B                 | `libretro:battery` |
| `mgba`/`mgba`                                     | `<rom>.sav`                                     | 256 B                 | `mgba:battery`     |
| `mednafen`/`gb`                                   | `<rom>.sav`                                     | 512 B                 | `mgba:battery`     |
| `ares`/`GameBoy`                                  | `ares/Game Boy/<rom>.ram`                       | 256 B                 | `ares:battery`     |
| `jgenesis`                                        | `jgenesis/gb/<rom>.sav`                         | 512 B                 | `jgenesis:battery` |
| `bizhawk`/`Gambatte`                              | `bizhawk/Final Fantasy Adventure (USA).SaveRAM` | 8,192 B               | `bizhawk:battery`  |
| `bizhawk`/`GBHawk`, `SameBoy`                     | the same `.SaveRAM`                             | 512 B                 | `bizhawk:battery`  |

The forms are in RB-428. Each row continued from the save the row before it left, given in its own
form where it keeps its own file; the rows sharing a file read each other's form there. The one
break is between the two rows sharing `<rom>.sav`: **`mednafen` refuses mGBA's 256 B file and the game does not
start** (RB-429). On Yellow it reads that file, so `refuses_plain` stays off for `gb` and the case
is tracked in #514 rather than held back.

## Pokemon Silver: the clock off the stock row

| Row                                    | Save after an in-game save                                        | Clock                      |
| -------------------------------------- | ----------------------------------------------------------------- | -------------------------- |
| `libretro`/`tgbdual`, `DoubleCherryGB` | `<rom>.srm` 32,768 B                                              | `<rom>.rtc` 4 B            |
| `libretro`/`sameboy`                   | `<rom>.srm` 32,768 B                                              | `<rom>.rtc` 32 B           |
| `libretro`/`bsnes`                     | `<rom>.srm` 32,816 B, the `.rtc` beside it left alone             | inside the save            |
| `libretro`/`mesen-s`                   | `<rom>.srm` 32,768 B, from a new game                             | none                       |
| `mesen`                                | `<rom>.srm` 32,768 B                                              | `<rom>.rtc` 13 B           |
| `mgba`, `mednafen`                     | `<rom>.sav` 32,816 B                                              | inside the save            |
| `ares`                                 | `ares/Game Boy/<rom>.ram` 32,768 B                                | `<rom>.rtc` 13 B beside it |
| `jgenesis`                             | `jgenesis/gbc/<rom>.sav` 32,768 B                                 | `<rom>.rtc` 38 B beside it |
| `bizhawk`/`Gambatte`                   | `bizhawk/Pokemon - Silver Version (USA, Europe).SaveRAM` 32,790 B | inside the save            |
| `bizhawk`/`GBHawk`                     | the same `.SaveRAM` at 32,768 B                                   | none                       |
| `bizhawk`/`SameBoy`                    | the same `.SaveRAM` at 32,816 B                                   | inside the save            |

The sizes and places match what the boot launches recorded (RB-298), now as saves. The RAM moved
between rows and the clock did not (RB-430): each row either showed a time unrelated to the one set
on the stock row or asked for it again. `mesen-s` never offered Continue on the stock row's save.
`jgenesis` kept it in `jgenesis/gbc/`, which the `gb` rule does not read, so those two files are
reported and not synced, as [index.md](index.md#a-cartridge-with-a-clock-pokemon-silver-on-gb)
records.

## What RomMBat read

`rommbat-agent saves --offline` on the tree afterwards attributed every file above to the slot in
the tables, `bizhawk`'s two `.SaveRAM` files to their games by the launch covering their write, and
reported the two `jgenesis/gbc/` files as a shape this release does not sync. Nothing else under
`saves/gb/` went unclaimed.

## What this file will not claim

- **Nothing about a round trip of these saves.** Every file lands in a slot whose round trip
  [libretro.md](libretro.md) and [standalone.md](standalone.md) record on Yellow, and size does
  not enter the protocol; no MBC2 or Silver save was restored from the server here.
- **Nothing about mGBA reading `mednafen`'s 512 B MBC2 file.** Three tries pressed Continue
  during the title's fade.
- **Nothing about another cartridge type.** MBC1, MBC3 without a clock, MBC5 with a rumble motor
  and the rest were not driven.
