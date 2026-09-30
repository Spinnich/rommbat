---
summary: The certification record for `gbc`: each row's standing at the floor, and the system's own steps.
read-when: Before certifying a `gbc` row, or when asked whether a `gbc` row works.
---

# gbc

Nintendo Game Boy Color. RetroBat calls the folder `gbc`, which is what this folder is named after.

**All twelve rows `gbc` declares are certified**, at RomM `5.3.0` and RetroBat 8.2.1 on
2026-09-23, all nine steps with step 6 N/A because `gbc` has no class D:

- `libretro`/`gambatte`, **the row a stock install gives a user**, selected with no override
- `libretro`/`tgbdual`, `sameboy` and `DoubleCherryGB`
- `mesen`, `mgba`/`mgba`, `mednafen`/`gbc`, `ares`/`GameBoyColor`, `bizhawk`/`Gambatte`, `GBHawk`
  and `SameBoy`, and `jgenesis`

**It certifies those twelve rows and nothing wider.** The four `libretro` rows needed only the
loose `.rtc` rule widened from `gb`. The eight standalone rows needed a battery rule each for
`gbc`, where `mesen`'s was already `libretro`'s, and four of them a state declaration in RomMBat's
bundled supplement. One row reads firmware, and RetroBat's `gbc` list names it.

## Where each row stands

**Every certified row holds at the floor, RomM `5.3.1` and RetroBat 8.2.1.** Steps 1 and 9 were
re-driven at `5.3.1` on 2026-09-24, in #236, which maps the nine steps. The other steps carry from
the drive at `5.3.0`, since nothing they exercise changed between the two. Nothing is owed.

| File                           | What it holds                                                                                                                                                                                                     |
| ------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| This file                      | Steps 1, 2 and 3, which are the system's, with which rows need firmware; what the first boots wrote; the cartridge clock, which no change of row that was checked kept; what the pass turned up that is not a row |
| [libretro.md](libretro.md)     | The four `libretro` rows                                                                                                                                                                                          |
| [standalone.md](standalone.md) | The eight standalone rows                                                                                                                                                                                         |
| [facts.md](facts.md)           | The measured facts about `gbc`'s emulators, with RB- IDs                                                                                                                                                          |

## The install this was measured on

|           |                                                             |
| --------- | ----------------------------------------------------------- |
| RetroBat  | `8.2.1-stable-win64`, the supported floor                   |
| RomM      | `5.3.0`, the floor then, read back by `status` as Supported |
| Root      | `R:\RetroBat`, found by walking up from the executable      |
| Store     | schema 17 of 17, WAL                                        |
| Client    | this branch, deployed twice, named below                    |
| Budget    | `none`, as for every system before it                       |
| Test game | Pokemon - Crystal Version (USA, Europe) (Rev 1)             |

**The test game is a 2 MB MBC3 cartridge with a clock and 32 KB of battery RAM**, header cartridge
type `0x10` (MBC3 with timer, RAM and battery) and Color flag `0xC0`, so it runs on a Color only.
Its save is written the moment the player picks Save from the menu in the first room. It carries
no `<emulator>` pin in `gamelist.xml`, and is RomM rom 274994.

**The client was deployed twice, and which build a result was taken on is named.** The first
deploy was `main` at `d43f4d6` with the loose `.rtc` rule widened to `gbc`, so the stock row's
clock would sync; the four `libretro` rows were certified on it. The eight standalone rows were
driven on it too, their saves sitting on disk unsynced, and certified on a second deploy carrying
this branch's `gbc` rules, whose first flush sent **6 saves and 10 states**.

**Every row after the first was seeded from the save the one before made**, as on `gba` and `gb`.
The four `libretro` cores and Mesen share one file and needed no copying. For the others the seed
was put where each emulator looks before its launch, and the maintainer then saved in the game, so
every save measured below is one that emulator wrote. Every row offered Continue on its seed.

**The maintainer played over RDP, and the agent drove every state on the standalone rows** from its
session on the RetroBat machine, through `emulatorLauncher` with the row's arguments and keys sent
by `keybd_event`. Those launches skip ES, so they run no hooks and record no session.

## The set

|          |                                             |
| -------- | ------------------------------------------- |
| Name     | Spinnich's Game Boy Color Favorites         |
| Scope    | `smart_collection 19`                       |
| Policy   | no game cap, no size cap, ordered by recent |
| Resolves | 83 games, 49.7 MB, into `gbc`               |

The sync found the BIOS present, then fetched 83 ROMs and 307 media files (273.8 MB), and wrote
one gamelist with 83 entries, exit 0.

## Steps 1, 2 and 3, for every row

### 1. Mapping

Resolved at layer **`fs_slug`**: RomM's `fs_slug` for the platform is literally `gbc`, which is
already a folder in this install.

| `fs_slug`        | Resolved by | What `platforms list` says                                                         |
| ---------------- | ----------- | ---------------------------------------------------------------------------------- |
| `gbc`            | `fs_slug`   | RomM's fs_slug 'gbc' is already a folder in this install                           |
| `gbc-unofficial` | `bundled`   | the bundled table offers `gbc`, `sgb`, `sgb-msu1`, `gbc2players`, picked the first |

### 2. Multi-file games and extensions

From the live `es_systems.cfg`:

```text
.gbc .zip .7z
```

**83 of 83 resolve and nothing is excluded or unlisted.** Every ROM is a `.zip` holding one `.gbc`,
so `gbc` has no multi-disc or multi-file shape to settle. `.zip` was observed to launch on all
twelve rows.

### 3. BIOS

```console
$ rommbat-agent bios gbc
1 present
  gbc: all 1 present
```

**One file, present at the md5 RetroBat names, exit 0.** RetroBat's manifest lists
`gbc_bios.bin` at `dbfce9db...`, and RomMBat adds nothing for `gbc`. Nothing is missing from the
library and nothing is hashless.

With the whole Game Boy family's firmware moved out of the tree (`gb_bios.bin`, `gbc_bios.bin` and
the four Super Game Boy files), `bios gbc` reported `1 to fetch (2.3 KB)` and `bios gbc --apply`
answered `BIOS: 1 fetched (2.3 KB)`, exit 0; `bios gb --apply` brought back the other five. All six
were back at the md5s they left with.

## Which rows need firmware

**One of twelve, and RetroBat's `gbc` list names it.** Each row was launched on Crystal with no
Game Boy family firmware anywhere in the tree:

| Row                        | With no firmware                                         |
| -------------------------- | -------------------------------------------------------- |
| `libretro`, all four cores | Boots to the intro                                       |
| `mesen`                    | Boots to the intro                                       |
| `mgba`/`mgba`              | Boots to the intro                                       |
| `mednafen`/`gbc`           | Boots to the intro                                       |
| `ares`/`GameBoyColor`      | Boots to the intro                                       |
| `bizhawk`/`Gambatte`       | Boots to the intro                                       |
| `bizhawk`/`GBHawk`         | **Refuses**: "Couldn't find required firmware GBC+World" |
| `bizhawk`/`SameBoy`        | Boots to the intro                                       |
| `jgenesis`                 | Boots to the intro                                       |

With `gbc_bios.bin` back, GBHawk boots Crystal to the intro. It is the file `bios gbc` fetches, so
`gbc` needs no supplement, where `gb` needed five files from its siblings (RB-293). RB-299.

## What the boot launches wrote

**Not steps 4 or 5.** No one saved in any of these launches; each ran about 25 seconds from
`emulatorLauncher` with no key sent. Every file below was moved to `R:\rommbat-evidence\gbc\boot\`
before a flush could send it.

| Row                         | Wrote at boot, under `saves/gbc/`                                           |
| --------------------------- | --------------------------------------------------------------------------- |
| `libretro`/`gambatte`       | `<rom>.srm`, 32,768 B, and `<rom>.rtc`, 8 B                                 |
| `libretro`/`tgbdual`        | `<rom>.srm` and `<rom>.rtc`, 4 B                                            |
| `libretro`/`sameboy`        | `<rom>.srm` and `<rom>.rtc`, 32 B                                           |
| `libretro`/`DoubleCherryGB` | `<rom>.srm` and `<rom>.rtc`, 4 B                                            |
| `mesen`                     | `<rom>.srm` and `<rom>.rtc`, 13 B                                           |
| `mgba`/`mgba`               | `<rom>.sav`, 32,816 B: the RAM and a 48 B clock footer                      |
| `mednafen`/`gbc`            | `<rom>.301899b8087289a6436b0a241fbbb474.sav`, 32,816 B                      |
| `ares`/`GameBoyColor`       | nothing: still running 15 s after `WM_CLOSE`, it was killed                 |
| `bizhawk`/`Gambatte`        | `bizhawk/Pokemon - Crystal Version (USA, Europe) (Rev A).SaveRAM`, 32,790 B |
| `bizhawk`/`GBHawk`          | nothing without `gbc_bios.bin`; the same file, 32,768 B, with it            |
| `bizhawk`/`SameBoy`         | the same file, 32,816 B                                                     |
| `jgenesis`                  | `jgenesis/gbc/<rom>.sav`, 32,768 B, and `<rom>.rtc`, 38 B                   |

**ares writes on exit, so a launch that is killed writes nothing.** A second boot, ended with
`Esc`, its `QuitEmulator` key in `settings.bml`, wrote `ares/Game Boy/<rom>.ram`, 32,768 B, and
`<rom>.rtc`, 13 B, which is how its directory was found before a seed was placed. A later launch
whose `Esc` went to another window closed on `WM_CLOSE` alone and wrote the same pair, so ares
does not always ignore `WM_CLOSE`, as `gb`'s record has it; it can take longer than 15 s.

## No change of row that was checked kept the clock

**Every row keeps Crystal's clock, and no two keep it the same way.** Each round trip above carried
the clock file at its own md5, so a device that stays on one row keeps its clock through RomMBat.
Moving the save between rows lost it on every move that was checked, and two were not:

| Row                                    | Where the clock is                                                 | Seeded from the row before, the game showed |
| -------------------------------------- | ------------------------------------------------------------------ | ------------------------------------------- |
| `libretro`/`gambatte`                  | `<rom>.rtc`, 8 B: the Unix time at which the game clock reads zero | the clock the maintainer set, about 9 AM    |
| `libretro`/`tgbdual`, `DoubleCherryGB` | `<rom>.rtc`, 4 B: the host time at exit                            | a wrong time, about 10 PM, with no prompt   |
| `libretro`/`sameboy`                   | `<rom>.rtc`, 32 B                                                  | a wrong time, about 4:22 AM, with no prompt |
| `mesen`                                | `<rom>.rtc`, 13 B                                                  | not noted                                   |
| `mgba`, `mednafen`                     | a 48 B footer on the `.sav`                                        | not noted                                   |
| `ares`                                 | `ares/Game Boy/<rom>.rtc`, 13 B                                    | **asked to set the clock**                  |
| `bizhawk`/`Gambatte`                   | a 22 B footer on the `.SaveRAM`                                    | **asked to set the clock**                  |
| `bizhawk`/`GBHawk`                     | nowhere: 32,768 B and no clock file                                | a wrong time, with no prompt                |
| `bizhawk`/`SameBoy`                    | a 48 B footer on the `.SaveRAM`                                    | **asked to set the clock**                  |
| `jgenesis`                             | `jgenesis/gbc/<rom>.rtc`, 38 B                                     | **asked to set the clock**                  |

**The loose `.rtc` is one file name with four formats.** RetroArch writes it for every core as RAM
type #1, and the stock core's 8 B base time, `tgbdual`'s and `DoubleCherryGB`'s 4 B host time,
`sameboy`'s 32 B record and Mesen's 13 B differ, and `tgbdual` and `sameboy` each showed a wrong
time for the file the row before left. What Mesen made of it was not noted. It syncs as one slot,
`libretro:battery:rtc`, because on disk it is one file. **Switching core on one machine can lose
the clock with no RomMBat involved**, and RomMBat carries whichever file the last row left. By the
maintainer's ruling that is recorded and not worked around. Where a seed was placed without the
clock (ares, BizHawk, jgenesis, each given the 32 KB RAM alone) the game found no clock and asked
for one, which says only that they were given none. mednafen read and saved into mGBA's own `.sav`,
both 32,816 B with a 48 B footer, and is the move most likely to have kept the clock; nobody looked.
RB-300.

## What the pass turned up that is not a row

- **ES rewrote `gamelist.xml` on its own exit**, moving Crystal's entry to the end with `playcount`,
  `lastplayed` and `gametime`, which is why step 9 compares against a copy taken after the last ES
  session: the re-sync left that copy byte-identical and said `gamelists: all 1 unchanged`.
- **The first flush on the second deploy refused one save that is not `gbc`'s**: rom 189465 on
  `megadrive`, where the server holds the four bytes `null` another client wrote in July.
- **mGBA, mednafen's shared `.sav`, Mesen, ares and jgenesis rewrite the clock on a launch with
  nothing saved**, so each such launch uploads a small new version, as on `gba` (RB-291).

## What this file will not claim

- **Nothing about `gbc` under any build but these.** Every row was measured on RetroBat 8.2.1 and
  RomM `5.3.0`.
- **Nothing about another game.** Crystal is one MBC3 cartridge with a clock and 32 KB of RAM. A
  game with no clock, or an MBC5 with rumble, may be sized and named differently.
- **Nothing about a clock carried across rows.** Only the same row reading its own file back was
  measured to keep the time.
- **Nothing about a `gb` game in `gbc`.** Every ROM in the set is a `.gbc`; a `.gb` placed there is
  what `jgenesis/gb/` would be for, and no rule reads it on `gbc`.

## Carried back to `gb`

**ares on `gb` keeps a clock cartridge's clock beside its save**, measured on 2026-09-23 with a copy
of Silver placed in `roms/gb` for one launch under `ares`/`GameBoy`: `ares/Game Boy/<rom>.ram`,
32,768 B, and `<rom>.rtc`, 13 B, the pair it writes on `gbc`. `gb`'s ares rule read only the `.ram`,
so the clock would not have synced. It now has its own class B rule, `ares:battery:rtc`, and the
`.ram` keeps `ares:battery`, so nothing uploaded under it moves; a flush on the new build sent
nothing. The copy and what it wrote were removed after the launch, and the round trip is carried
from the `gbc` row, which syncs the same two files. RB-302.

**jgenesis on `gb` needs no clock rule.** It names its directory from the file inside the zip, and
the clock cartridges known here are Color titles, a `.gbc`, even where they run on a mono Game Boy,
and a mono-only one may not exist; none of the catalog's `gb` Pokemon titles has a clock. So such a
clock lands in `jgenesis/gbc/`, the gap `gb/` records, and `jgenesis/gb/<rom>.rtc` was not seen.
Only the Pokemon titles' headers were read.
