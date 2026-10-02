---
summary: The certification record for `n64`: each row's standing at the floor, and the system's own steps.
read-when: Before certifying a `n64` row, or when asked whether a `n64` row works.
---

# n64

Nintendo 64. RetroBat calls the folder `n64`, which is what this folder is named after.

**All nine rows `n64` declares are certified**, at RomM `5.3.1` and RetroBat 8.2.1, all nine
steps, each row's Controller Pak option driven at step 6. Eight on 2026-09-27:

- `libretro`/`mupen64plus_next`, **the row a stock install gives a user**, selected with no
  override, and `libretro`/`parallel_n64`
- `mupen64` (Rosalie's Mupen GUI), `simple64`, `project64`, `ares`/`Nintendo64`, and `bizhawk`
  under `Ares64` and `Mupen64Plus`

and **`gopher64` on 2026-09-29**, in [its own file](gopher64.md). It keeps its battery saves only in its
own `emulators/gopher64/portable_data/data/saves/`, which RetroBat neither points into `saves/n64/`
nor mirrors (RB-341), so RomMBat reads that folder where it is, through a battery rule anchored at
the RetroBat root (#239). The folder is reported upstream as
[emulatorlauncher#1389](https://github.com/RetroBat-Official/emulatorlauncher/issues/1389).

**Every row needed something except the two `libretro` cores.** A battery rule for ares, RMG and
simple64 together, and Project64; `n64` added to BizHawk's rule; a state declaration for ares,
simple64 and Project64; and two new shapes, a battery save named with an emulator's own title and
an md5 of the ROM, and a directory per game (RB-336 to RB-341). RetroBat lists no firmware for
`n64`, and every row boots with none.

## Where each row stands

**Every certified row was driven at the floor, RomM `5.3.1` and RetroBat 8.2.1.** Nothing is owed.

| File                       | What it holds                                                                |
| -------------------------- | ---------------------------------------------------------------------------- |
| This file                  | Steps 1, 2 and 3, which are the system's; what the first boots wrote         |
| [rows.md](rows.md)         | How each row is selected, and steps 4 to 9 on the eight driven on 2026-09-27 |
| [gopher64.md](gopher64.md) | `gopher64`, certified 2026-09-29                                             |
| [facts.md](facts.md)       | The measured facts about `n64`'s emulators, with RB- IDs                     |

## The install this was measured on

|            |                                                                                           |
| ---------- | ----------------------------------------------------------------------------------------- |
| RetroBat   | `8.2.1-stable-win64`, the supported floor                                                 |
| RomM       | `5.3.1`, the floor, read back by `status` as Supported                                    |
| Root       | `R:\RetroBat`                                                                             |
| Client     | the `n64-certification` branch, deployed as each rule landed                              |
| Budget     | `none`                                                                                    |
| Test games | Legend of Zelda, The - Ocarina of Time (USA), rom 225805; Mario Kart 64 (USA), rom 157714 |

**Ocarina of Time saves to 32 KB of cartridge SRAM**, and Mario Kart 64 to a 512 B EEPROM plus a
ghost on the Controller Pak, which is what step 6 drives. Neither carries an `<emulator>` pin in
`gamelist.xml`. **Four emulators were not installed**, `mupen64`, `simple64`, `project64` and
`gopher64`; with the maintainer's agreement RetroBat's installer put each in place on its first
launch (160.6 MB, 120.7 MB, 10.7 MB and 61.8 MB).

**The server already held Ocarina of Time's save**, `autosave` slot, 296,960 B, from another client
on 2026-08-07. It was restored first as the seed, so the stock row continued it rather than
conflicting with it.

**Each row after the first was seeded from the save the one before made**, converted where the
format differs (RB-338): the `libretro` image's SRAM region as it is for RMG, simple64 and
Project64, word-swapped for ares, BizHawk `Ares64` and gopher64, and placed at `0x40800` of BizHawk
`Mupen64Plus`'s own image. The maintainer then saved in the game on every row, so every save
the row files measure is one that emulator wrote. States the maintainer could not see land were made from
the agent's session through `emulatorLauncher` and `keybd_event`; those launches run no hook and
record no session, and each is named in its row's file.

## The set

|          |                                             |
| -------- | ------------------------------------------- |
| Name     | n64 certification                           |
| Scope    | `smart_collection 15`                       |
| Policy   | no game cap, no size cap, ordered by recent |
| Resolves | 147 games, 2 GB, into `n64`                 |

The sync fetched 147 ROMs (2 GB) and 580 media files (624 MB), and wrote one gamelist with 147
entries, exit 0.

## Steps 1, 2 and 3, for every row

### 1. Mapping

Resolved at layer **`fs_slug`**: `RomM's fs_slug 'n64' is already a folder in this install`.
`n64-unofficial` resolves to `n64` from the bundled table.

### 2. Multi-file games and extensions

From the live `es_systems.cfg`:

```text
.v64 .z64 .n64 .wad .zip .7z .decomp
```

**147 of 147 resolve and nothing is excluded or unlisted.** Every ROM is a `.zip` holding one
`.z64`, so `n64` has no multi-file shape to settle, and `.zip` holding a `.z64` was observed to
launch on all nine rows.

### 3. BIOS

```console
$ rommbat-agent bios n64
RetroBat requires no BIOS for n64.
```

**Exit 0, and step 3 passes by recording so.** The family's only firmware is `n64dd`'s three
`IPL` files, for disk-drive games, and nothing of the kind was anywhere in the tree when **all nine
rows booted Ocarina of Time to its title screen**, each launched through `emulatorLauncher` for
about 30 seconds.

## What the boot launches wrote

**Not steps 4 or 5.** No one saved; every file was moved to `R:\rommbat-evidence\n64\boot\` before a
flush could send it.

| Row                     | Wrote for Ocarina of Time                                                                |
| ----------------------- | ---------------------------------------------------------------------------------------- |
| `libretro`, both cores  | loose `<rom>.srm`, 296,960 B, EEPROM, four paks, SRAM and FlashRAM in one image          |
| `mupen64`, `simple64`   | `sram/Legend of Zelda, The - Ocarina o-5BD1FE10.sra`, 32,768 B, one file for both        |
| `project64`             | `project64/THE LEGEND OF ZELDA-AA3911F5.../THE LEGEND OF ZELDA.sra`, 16 B                |
| `ares`/`Nintendo64`     | `ares/Nintendo 64/<rom>.ram`, 32,768 B, when ended with `Esc`                            |
| `bizhawk`/`Ares64`      | `bizhawk/<rom>.SaveRAM`, 32,768 B, byte-identical to ares's `.ram`                       |
| `bizhawk`/`Mupen64Plus` | `bizhawk/<rom>.SaveRAM`, 296,960 B, SRAM at `0x40800`                                    |
| `gopher64`              | `emulators/gopher64/portable_data/data/saves/THE LEGEND OF ZELDA-<sha256>.sra`, 32,768 B |

Mario Kart 64 under ares wrote `<rom>.eeprom`, 512 B, and `<rom>.pak`, 32,768 B, and under Project64
`MARIOKART64.eep` and `MARIOKART64_Cont_1.mpk`. RMG's name is the first 32 characters of
mupen64plus's GoodName for the ROM, not the file: Mario Kart 64 (USA) wrote
`Mario Kart 64 (U) [!]-3A67D998.eep`, the hex being the first 8 of the `.z64`'s md5.

## What this file will not claim

- **Nothing about `n64` under any build but these**: RetroBat 8.2.1 and RomM `5.3.1`.
- **Nothing about a FlashRAM game.** Neither test game uses one, so no row's `.fla` or `.flash` was
  seen, and no rule carries one.
- **Nothing about Project64's slots 1 to 9**, which RetroBat's configuration does not reach; the
  declaration reads `.pj<n>.zip` from the name format in `Project64.exe`, not from a file.
- **Nothing about a second Controller Pak port**, or Project64's `_Cont_2` to `_Cont_4`, which its
  rule leaves unclaimed.
- **Nothing about a `.n64` or `.v64` ROM.** The library is `.z64` throughout, and the md5s in RMG's
  and Project64's names are of the ROM in the byte order they read it.
