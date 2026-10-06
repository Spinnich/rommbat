---
summary: The certification record for `gamegear`: each row's standing at the floor, and the system's own steps.
read-when: Before certifying a `gamegear` row, or when asked whether a `gamegear` row works.
---

# gamegear

Sega Game Gear. RetroBat calls the folder `gamegear`, which is what this folder is named after.

**Five of the seven rows `gamegear` declares are certified**, at RomM `5.3.1` and RetroBat 8.2.1 on
2026-10-05, all nine steps, with step 6 N/A because `gamegear` has no class D:

- `libretro`/`genesis_plus_gx`, **the row a stock install gives a user**, selected with no override
- `libretro`/`picodrive`
- `mednafen`/`gg`, `ares`/`GameGear` and `bizhawk`/`SMSHawk`

**Two are driven and not certified**, each for a reason outside RomMBat:

- `libretro`/`fbneo` **never boots this library**: FBNeo takes a console game's set from the file
  name, and every ROM here carries its No-Intro name (RB-412), as on `mastersystem`.
- `jgenesis` **plays a Game Gear cartridge to a black screen**: `emulatorLauncher` starts it with
  `--hardware MasterSystem` for every `gamegear` game, and no ES option changes that (RB-410). Its
  saves were measured from a run by hand in Game Gear mode, which is evidence, not certification
  ([standalone.md](standalone.md#jgenesis-driven-and-not-certifiable-at-this-floor)).

**It certifies those five rows and nothing wider.** The two `libretro` rows needed nothing. The three
standalone rows each needed a battery rule for `gamegear`, and `mednafen` and `ares` a state
declaration as well, from the boot launches below (#453). That change also made the scanner pass over
a battery file of nothing but `0xFF`, because ares writes one for every cartridge (RB-408).

## Where each row stands

**Every certified row holds at the floor, RomM `5.3.1` and RetroBat 8.2.1**, measured there on
2026-10-05. Nothing is owed.

| File                           | What it holds                                                                                                          |
| ------------------------------ | ---------------------------------------------------------------------------------------------------------------------- |
| This file                      | Steps 1, 2 and 3, which are the system's; what the first boots wrote, and how the test game saves; what else turned up |
| [libretro.md](libretro.md)     | The two `libretro` rows, and FBNeo                                                                                     |
| [standalone.md](standalone.md) | The three certified standalone rows, and jgenesis                                                                      |
| [facts.md](facts.md)           | The measured facts about `gamegear`'s emulators, with RB- IDs                                                          |

## The install this was measured on

|           |                                                                                  |
| --------- | -------------------------------------------------------------------------------- |
| RetroBat  | `8.2.1-stable-win64`, the supported floor                                        |
| RomM      | `5.3.1`, read back by `status` as Supported                                      |
| Root      | `R:\RetroBat`, found by walking up from the executable                           |
| Store     | schema 21 of 21, WAL                                                             |
| Client    | `main` at `cc167a2`, carrying #453's rules, deployed before the first ES session |
| Budget    | unlimited, 2 GB free-space floor                                                 |
| Test game | Defenders of Oasis (USA, Europe)                                                 |

**The test game is a 512 KB cartridge with battery SRAM**, RomM rom 272855, the `.gg` inside the zip
hashing to `8430050c...`. **It autosaves as it is played**, so every row's save reached the file
within seconds of play, with no save point to walk to. It carries no `<emulator>` pin in
`gamelist.xml`, and no game in the set does. The server held no save or state for it before the pass.

**Every row after the first was seeded from the save the one before made**: the two `libretro` cores
share one `.srm`; mednafen's hashed `.sav`, ares's `.ram`, BizHawk's `.SaveRAM` and jgenesis's `.sav`
were each given the latest save before the row's launch, cut to 8 KB for BizHawk or padded with
`0xFF` to 32 KB for jgenesis. **The game's own Continue resumed where the previous row stopped on every
row**, so each emulator read the last one's progress, which `mastersystem`'s test game could not
show. Each row then wrote its own progress: 65 to 84 bytes changed against its seed.

**The maintainer played every row from ES**, choosing it in the game's own emulator option, which
`emulatorLauncher.log` confirmed on each launch. The agent made the states on ares and BizHawk from its
own session, and ran jgenesis by hand in Game Gear mode for the maintainer to play; those launches run
no hooks and record no session.

## The set

|          |                                             |
| -------- | ------------------------------------------- |
| Name     | gamegear certification                      |
| Scope    | `platform 446`                              |
| Policy   | no game cap, no size cap, ordered by recent |
| Resolves | 546 games, 90.9 MB, into `gamegear`         |

The sync fetched 546 ROMs and 2,046 media files (1.1 GB), and wrote one gamelist with 546 entries,
exit 0.

## Steps 1, 2 and 3, for every row

### 1. Mapping

Resolved at layer **`fs_slug`**: RomM's `fs_slug` for the platform is literally `gamegear`, which is
already a folder in this install.

| `fs_slug`             | Resolved by | What `platforms list` says                                    |
| --------------------- | ----------- | ------------------------------------------------------------- |
| `gamegear`            | `fs_slug`   | RomM's fs_slug 'gamegear' is already a folder in this install |
| `gamegear-unofficial` | `bundled`   | From the bundled table                                        |

### 2. Multi-file games and extensions

From the live `es_systems.cfg`:

```text
.gg .bin .zip .7z
```

**546 of 546 resolve and nothing is excluded or unlisted.** Every ROM is a `.zip` holding one file:
527 hold a `.gg` and **19 hold a `.sms`, Game Gear cartridges that run in Master System mode**. So
`gamegear` has no multi-disc or multi-file shape to settle. Castle of Illusion Starring Mickey Mouse
(USA, Europe, Brazil) (En), one of the nineteen, was launched on every row: Genesis Plus GX,
mednafen, ares and jgenesis play it at the Master System's frame, BizHawk plays it cropped to a Game
Gear screen, PicoDrive plays it cropped and in the wrong colors (RB-411), and FBNeo refuses it for its
name.

### 3. BIOS

```console
$ rommbat-agent bios gamegear
RetroBat requires no BIOS for gamegear.
```

**Exit 0, and RetroBat lists nothing to fetch**, which is step 3 passing. **No row needs firmware.**
Each was booted on the test game with no Game Gear or Master System BIOS anywhere in the tree, the
two Master System files this install keeps for `mastersystem`'s SMSHawk moved out for the boots:

| Row                                          | With no firmware                           |
| -------------------------------------------- | ------------------------------------------ |
| `libretro`/`genesis_plus_gx` and `picodrive` | Reaches the intro                          |
| `libretro`/`fbneo`                           | "Romset is unknown", for the name (RB-412) |
| `mednafen`, `ares`, `bizhawk`/`SMSHawk`      | Reach the intro                            |
| `jgenesis`                                   | Runs, to a black screen (RB-410)           |

**SMSHawk starts a Game Gear game with no firmware**, where on `mastersystem` it refuses (RB-322).

## What the boot launches wrote

**Not steps 4 or 5.** No one saved in these launches; each ran about 25 seconds from
`emulatorLauncher` with no key sent, and every file was moved to `R:\rommbat-evidence\gamegear\boot\`
before a flush could send it. RB-409 holds the detail.

| Row                          | Wrote at boot                                             |
| ---------------------------- | --------------------------------------------------------- |
| `libretro`/`genesis_plus_gx` | `<rom>.srm`, 285 B, trimmed at the last used byte         |
| `libretro`/`picodrive`       | `<rom>.srm`, 32,768 B                                     |
| `mednafen`/`gg`              | `<rom>.8430050c60db46b3887cf7d7cf2f206f.sav`, 32,768 B    |
| `ares`/`GameGear`            | `ares/Game Gear/<rom>.ram`, 32,768 B, on `Esc`            |
| `bizhawk`/`SMSHawk`          | `bizhawk/Defenders of Oasis (UE).SaveRAM`, 8,192 B        |
| `jgenesis`                   | `jgenesis/gg/<rom>.sav`, 32,768 B, the upper 24 KB `0x00` |

Every one holds the game's header from `0x100`, its ASCII `Backup Ver0.84` at `0x10E`. **ares also writes 32,768 B of `0xFF`
for a cartridge with no battery**, as Sonic Chaos and Castle of Illusion showed, which the scanner now
passes over as erased (RB-408). **mednafen's md5 is of the whole `.gg` inside the zip.**

## What the pass turned up that is not a row

- **The server already held a save for the Virtual Console dump**, rom 272854, `Defenders of Oasis
(USA, Europe) (Virtual Console).srm`: 285 B, `c57a4b80...`, byte-identical to the boot write
  Genesis Plus GX makes for the test game. Some other client booted that dump and uploaded its boot
  write, and the first ES session's start flush placed it, since this device held nothing for that
  rom. It is not the test game and was left alone.
- **ES passed `-core genesis_plus_gx` with `-emulator jgenesis`**, a leftover of the per-game core
  setting, which jgenesis ignores.
- **Every flush refused the same three saves that are not `gamegear`'s**: a superseded `psx` card, a
  `gamecube` directory save with no folder yet, and rom 189465 on `megadrive`, where the server holds
  the four bytes `null` (RB-276).
- **ES rewrote `gamelist.xml` on its own exit**, so step 9 compares against a copy taken after the last
  ES session: the re-sync left that copy byte-identical and said `gamelists: all 1 unchanged`.

## What this file will not claim

- **Nothing about `gamegear` under any build but these.** Every row was measured on RetroBat 8.2.1
  and RomM `5.3.1`.
- **Nothing about another game.** Defenders of Oasis is one cartridge whose save fits in 3,840 B; the
  sizes in the row files are each emulator's SRAM window, and another game's may differ.
- **Nothing about a `.sms` cartridge's save.** None of the nineteen has a battery, and
  `MednafenRomHash` answers only for a `.gg` on `gamegear`.
- **Nothing about jgenesis as RetroBat launches it**, beyond that it runs the game to a black screen.
