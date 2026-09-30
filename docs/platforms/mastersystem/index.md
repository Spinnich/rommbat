---
summary: The certification record for `mastersystem`: each row's standing at the floor, and the system's own steps.
read-when: Before certifying a `mastersystem` row, or when asked whether a `mastersystem` row works.
---

# mastersystem

Sega Master System / Mark III. RetroBat calls the folder `mastersystem`, which is what this file is
named after.

**Seven of the ten rows `mastersystem` declares are certified**, at RomM `5.3.0` and RetroBat 8.2.1
on 2026-09-24, all nine steps with step 6 N/A because `mastersystem` has no class D:

- `libretro`/`genesis_plus_gx`, **the row a stock install gives a user**, selected with no override
- `libretro`/`picodrive`
- `mesen`, `mednafen`/`mastersystem`, `ares`/`MasterSystem`, `bizhawk`/`SMSHawk` and `jgenesis`

**Three are driven and not certified**, as on `megadrive` and for the same reasons:

- `libretro`/`fbneo` **never boots this library.** FBNeo takes a Master System game's set from the
  file name, and every ROM here carries its No-Intro name (RB-325).
- `kega-fusion`/`auto` and `kega-fusion`/`mastersystem` **fail step 4**: Kega Fusion writes its
  battery save as `<rom>.ssm` into `emulators/kega-fusion/`, outside `saves/`, because RetroBat's
  template `Fusion.ini` sends it there (RB-283). Their states sync.

**It certifies those seven rows and nothing wider.** The two `libretro` rows needed nothing. The five
standalone rows each needed a battery rule for `mastersystem`, and `mesen`, `mednafen` and `ares` a
state declaration as well; `kega-fusion` got one too, so its states sync while its battery save
cannot. **`bizhawk`/`SMSHawk` is certified with the US/EU BIOS in `bios\`**, which it refuses to
start without and which RomMBat cannot fetch (below).

## Where each row stands

**Every certified row holds at the floor, RomM `5.3.1` and RetroBat 8.2.1.** Steps 1 and 9 were
re-driven at `5.3.1` on 2026-09-24. The other steps carry from the drive at `5.3.0`, since nothing
they exercise changed between the two. Nothing is owed.

| File                             | What it holds                                                                                                                                                                           |
| -------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| This file                        | Steps 1, 2 and 3, which are the system's, with which rows need firmware; what the first boots wrote, and how the test game lays out its save; what the pass turned up that is not a row |
| [libretro.md](libretro.md)       | The two `libretro` rows, and FBNeo                                                                                                                                                      |
| [standalone.md](standalone.md)   | The five standalone rows                                                                                                                                                                |
| [kega-fusion.md](kega-fusion.md) | The two `kega-fusion` rows                                                                                                                                                              |
| [facts.md](facts.md)             | The measured facts about `mastersystem`'s emulators, with RB- IDs                                                                                                                       |

## The install this was measured on

|           |                                                             |
| --------- | ----------------------------------------------------------- |
| RetroBat  | `8.2.1-stable-win64`, the supported floor                   |
| RomM      | `5.3.0`, the floor then, read back by `status` as Supported |
| Root      | `R:\RetroBat`, found by walking up from the executable      |
| Store     | schema 18 of 18, WAL                                        |
| Client    | this branch, deployed once, before the first ES session     |
| Budget    | `none`, as for every system before it                       |
| Test game | Golden Axe Warrior (USA, Europe, Brazil) (En)               |

**The test game is a 256 KB cartridge with battery SRAM**, RomM rom 239603, CRC32 `c7ded988`, the
`.sms` inside the zip hashing to `d46e40bb...`. It carries no `<emulator>` pin in `gamelist.xml`, and
no game in the set does. The server held no save or state for it before the pass.

**The client was deployed once**, carrying this branch's `mastersystem` battery rules and state
declarations, measured from the boot launches and state probes below before anyone sat down, and
every row was driven on it.

**Every row after the first was seeded from the save the one before made**, as on `gba` and later:
the two `libretro` cores share one loose `.srm` and needed no copying; Mesen's `.sav`, mednafen's
hashed `.sav`, ares's `.ram`, BizHawk's `.SaveRAM`, jgenesis's `.sav` and Kega's `.ssm` were each
given the latest save before the row's launch, cut or zero-padded to that emulator's own size.

**The maintainer played every row but one from ES over RDP**, and the agent made the states on
`ares` from its own session, since ares shows nothing when a state is saved, plus a second slot on
`bizhawk` and the whole `kega-fusion`/`mastersystem` row. Those launches run no hooks and record no
session, and each is named below.

## The set

|          |                                             |
| -------- | ------------------------------------------- |
| Name     | Smart collection 8                          |
| Scope    | `smart_collection 8`                        |
| Policy   | no game cap, no size cap, ordered by recent |
| Resolves | 153 games, 21.5 MB, into `mastersystem`     |

The sync fetched 153 ROMs and 599 media files (628.7 MB), and wrote one gamelist with 154 entries,
exit 0. The 154th is Phantasy Star (Brazil), installed on 2026-09-23 outside any set with a 32 KB
save already uploaded, and left alone.

## Steps 1, 2 and 3, for every row

### 1. Mapping

Resolved at layer **`fs_slug`**: RomM's `fs_slug` for the platform is literally `mastersystem`,
which is already a folder in this install.

| `fs_slug`                 | Resolved by | What `platforms list` says                                        |
| ------------------------- | ----------- | ----------------------------------------------------------------- |
| `mastersystem`            | `fs_slug`   | RomM's fs_slug 'mastersystem' is already a folder in this install |
| `mastersystem-unofficial` | `bundled`   | From the bundled table                                            |

### 2. Multi-file games and extensions

From the live `es_systems.cfg`:

```text
.bin .sms .wad .zip .7z
```

**153 of 153 resolve and nothing is excluded or unlisted.** Every ROM is a `.zip` holding one `.sms`,
so `mastersystem` has no multi-disc or multi-file shape to settle. A `.zip` holding a `.sms` was
observed to launch on every row except `fbneo`, which refuses it for its name rather than its
extension.

### 3. BIOS

```console
$ rommbat-agent bios mastersystem
2 RetroBat names no hash for

  mastersystem
    no hash to check         bios/[BIOS] Sega Master System (Japan) (v2.1).sms
    no hash to check         bios/[BIOS] Sega Master System (USA, Europe) (v1.3).sms
```

**Exit 0. Both files RetroBat lists are hashless**, so RomMBat can neither find them in RomM nor
recognise them on disk, and says so. That is the fourth of step 3's states for both; present,
fetched and not in the library do not apply. `mastersystem` is one of the 29 systems with no
joinable entry.

## Which rows need firmware

**One of ten refuses to start without firmware.** Each row was launched on the test game with no
Master System BIOS anywhere in the tree:

| Row                                          | With no firmware                                                                 |
| -------------------------------------------- | -------------------------------------------------------------------------------- |
| `libretro`/`genesis_plus_gx` and `picodrive` | Reaches the intro                                                                |
| `libretro`/`fbneo`                           | "Romset is unknown", for the name (RB-325)                                       |
| `mednafen`, `mesen`, `ares`, `jgenesis`      | Reach the intro                                                                  |
| `kega-fusion`, both cores                    | Reach the intro                                                                  |
| `bizhawk`/`SMSHawk`                          | **Refuses**: "No BIOS found. Open the firmware manager now?", then fails to load |

**SMSHawk looks the file up whatever its `UseBios` setting says**, which RetroBat leaves `False`.
With both files from the maintainer's RetroBat 8.2.1 BIOS pack in `bios\`, `840481177270...` for the
US/EU v1.3 and `24a519c53f67...` for the Japanese v2.1, 8,192 B each, it starts. `emulatorLauncher`
names both in BizHawk's config, as `SMS+Export` and `SMS+Japan`. **This export cartridge needs the
US/EU file**: with it alone SMSHawk starts, and with the Japanese file alone it refuses as before.

**Kega Fusion looks for different names.** RetroBat's `Fusion.ini` sets `SMSUSABIOS`, `SMSJAPBIOS`
and `SMSEURBIOS` to `bios_U.sms`, `bios_J.sms` and `bios_E.sms`, which RetroBat's list does not name,
and Kega boots the cartridge without them. RB-322.

So `bizhawk`/`SMSHawk` is certified with the US/EU file present, **which a user supplies**: RetroBat
names it without a hash, and RomMBat fetches only by md5. By the maintainer's decision the two files
stay in this install's `bios\`.

## What the boot launches wrote

**Not steps 4 or 5.** No one saved in any of these launches; each ran about 25 seconds from
`emulatorLauncher` with no key sent. Every file below was moved to
`R:\rommbat-evidence\mastersystem\boot\` before a flush could send it.

| Row                          | Wrote at boot                                                           |
| ---------------------------- | ----------------------------------------------------------------------- |
| `libretro`/`genesis_plus_gx` | `<rom>.srm`, 8,191 B, trimmed at the last used byte                     |
| `libretro`/`picodrive`       | `<rom>.srm`, 32,768 B                                                   |
| `mednafen`/`mastersystem`    | `<rom>.d46e40bbb729ba233f171ad7bf6169f5.sav`, 32,768 B                  |
| `mesen`                      | `<rom>.sav`, 8,192 B, different bytes on each fresh boot                |
| `ares`/`MasterSystem`        | `ares/Master System/<rom>.ram`, 32,768 B, on `Esc`                      |
| `bizhawk`/`SMSHawk`          | `bizhawk/Golden Axe Warrior (UE).SaveRAM`, 8,192 B, with the US/EU BIOS |
| `jgenesis`                   | `jgenesis/sms/<rom>.sav`, 32,768 B                                      |
| `kega-fusion`, both cores    | `emulators/kega-fusion/<rom>.ssm`, 8,191 B, outside `saves/`            |

**Four sizes for one cartridge's SRAM**, as on `megadrive` (RB-277): Genesis Plus GX and Kega
trim it at the last used byte, 8,191 B; Mesen and BizHawk keep 8 KB; PicoDrive, mednafen, ares and
jgenesis keep a 32 KB window. **mednafen's md5 is of the whole `.sms` inside the zip.**

### How Golden Axe Warrior keeps its save

**The game writes its committed save once, when a character is first created, and after that
rewrites only a working copy.** Across every file the pass produced:

| Offset            | Holds                                                                         |
| ----------------- | ----------------------------------------------------------------------------- |
| `0x0000`          | A header, `Golden Axe Warrior Ver 1.0`, present from the first boot           |
| `0x1000`-`0x11FF` | Value and complement byte pairs, written at the first character creation only |
| `0x1200`-`0x13FF` | A working area the game rebuilds from the pairs, and rewrites while it runs   |

The stock row's session changed `0x1000` onward from the boot write. **No later session changed
`0x1000`-`0x11FF`**, including one on `ares` where the maintainer started a new game and named a new
character. Mesen, ares and the others changed only the working area, and mednafen and jgenesis wrote
it back to the stock row's bytes, as a game repairing its copy from the committed save would. Where
the game commits a save after the first character was not found. RB-326.

**So after the stock row, step 4 rests on each emulator's own write, not on new progress.** Every
row below wrote its own file, which went up and came back at its own md5, which is what step 4 asks.
It does not show one emulator reading another's progress, since there was no new progress to read.
The maintainer chose to record it so rather than redrive three rows.

## What the pass turned up that is not a row

- **RomM's browser player rewrote the save mid-pass** (RB-327). During the Mesen session the
  maintainer launched the game in RomM's EmulatorJS by accident. At 15:28:26Z the server gained save
  496 in `libretro:battery`, with no device, named after PicoDrive's upload
  (`[2026-09-24_15-26-38]`) and holding 8,191 B, `b98e4e38...`: PicoDrive's 32 KB save as Genesis
  Plus GX, which EmulatorJS runs, trims it. The quit flush found the local `.srm` unchanged since it
  was last in step and the server's newer, took the server's, and kept the file it displaced in
  `emulators/rommbat/replaced/`. Same save data, and the right behaviour.
- **Every flush refused one save that is not `mastersystem`'s**: rom 189465 on `megadrive`, where
  the server holds the four bytes `null` another client wrote in July (RB-276).
- **ES rewrote `gamelist.xml` on its own exit**, so step 9 compares against a copy taken after the
  last ES session: the re-sync left that copy byte-identical and said `gamelists: all 1 unchanged`.

## What this file will not claim

- **Nothing about `mastersystem` under any build but these.** Every row was measured on RetroBat 8.2.1
  and RomM `5.3.0`.
- **Nothing about another game.** Golden Axe Warrior is one cartridge with an 8 KB save; the sizes
  above are its SRAM window's, and another game's differ.
- **Nothing about one row reading another's progress after the first character.** The game committed
  no new save after that, so the seeds carried the same committed save throughout.
- **Nothing about a Japanese cartridge under SMSHawk**, which reads `SMS+Japan` and was not driven.
- **Nothing about a `.bin` or a `.7z`.** Every ROM in the set is a zipped `.sms`, and
  `MednafenRomHash` answers only for a `.sms`.
