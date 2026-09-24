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
  file name, and every ROM here carries its No-Intro name (finding 325).
- `kega-fusion`/`auto` and `kega-fusion`/`mastersystem` **fail step 4**: Kega Fusion writes its
  battery save as `<rom>.ssm` into `emulators/kega-fusion/`, outside `saves/`, because RetroBat's
  template `Fusion.ini` sends it there (finding 283). Their states sync.

**It certifies those seven rows and nothing wider.** The two `libretro` rows needed nothing. The five
standalone rows each needed a battery rule for `mastersystem`, and `mesen`, `mednafen` and `ares` a
state declaration as well; `kega-fusion` got one too, so its states sync while its battery save
cannot. **`bizhawk`/`SMSHawk` is certified with the US/EU BIOS in `bios\`**, which it refuses to
start without and which RomMBat cannot fetch (below).

**This file is in five parts.** Steps 1, 2 and 3, which are the system's, with which rows need
firmware. What the first boots wrote, and how the test game lays out its save. The two `libretro`
rows and FBNeo, then the five standalone rows, then Kega Fusion. Last, what the pass turned up that
is not a row.

## The install this was measured on

|           |                                                                  |
| --------- | ---------------------------------------------------------------- |
| RetroBat  | `8.2.1-stable-win64`, the supported floor                        |
| RomM      | `5.3.0`, the supported floor, read back by `status` as Supported |
| Root      | `R:\RetroBat`, found by walking up from the executable           |
| Store     | schema 18 of 18, WAL                                             |
| Client    | this branch, deployed once, before the first ES session          |
| Budget    | `none`, as for every system before it                            |
| Test game | Golden Axe Warrior (USA, Europe, Brazil) (En)                    |

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
fetched and not in the library do not apply. `mastersystem` is one of the 28 systems with no
joinable entry.

## Which rows need firmware

**One of ten refuses to start without firmware.** Each row was launched on the test game with no
Master System BIOS anywhere in the tree:

| Row                                          | With no firmware                                                                 |
| -------------------------------------------- | -------------------------------------------------------------------------------- |
| `libretro`/`genesis_plus_gx` and `picodrive` | Reaches the intro                                                                |
| `libretro`/`fbneo`                           | "Romset is unknown", for the name (finding 325)                                  |
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
and Kega boots the cartridge without them. Finding 322.

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

**Four sizes for one cartridge's SRAM**, as on `megadrive` (finding 277): Genesis Plus GX and Kega
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
the game commits a save after the first character was not found. Finding 326.

**So after the stock row, step 4 rests on each emulator's own write, not on new progress.** Every
row below wrote its own file, which went up and came back at its own md5, which is what step 4 asks.
It does not show one emulator reading another's progress, since there was no new progress to read.
The maintainer chose to record it so rather than redrive three rows.

## The two `libretro` rows, and FBNeo

|                   | Selected by                                             | Confirmed on the ES launch line            |
| ----------------- | ------------------------------------------------------- | ------------------------------------------ |
| `genesis_plus_gx` | **Nothing: RetroBat's default**                         | `-emulator libretro -core genesis_plus_gx` |
| `picodrive`       | `mastersystem.emulator = libretro`, `.core = picodrive` | `-core picodrive -state_slot 4`            |
| `fbneo`           | the agent's launch                                      | `fbneo_libretro.dll ... --subsystem sms`   |

Each override was set with ES closed, and the `mastersystem` keys were cleared when the pass ended,
leaving `es_settings.cfg` as the copy taken first,
`R:\rommbat-evidence\mastersystem\es_settings.before.cfg`, apart from what ES itself rewrites.

| #   | `genesis_plus_gx`                                                      | `picodrive`                                       |
| --- | ---------------------------------------------------------------------- | ------------------------------------------------- |
| 4   | **Pass, both directions.** Class A `.srm`, 8,191 B                     | **Pass, both directions.** The same `.srm`, 32 KB |
| 5   | **Pass**, the screenshot byte-checked                                  | **Pass**, the screenshot byte-checked             |
| 6   | **N/A.** `mastersystem` is class A                                     | **N/A**                                           |
| 7   | **Pass.** Launched from ES after the sync, with box art                | **Pass**, carried                                 |
| 8   | **Pass.** 15:22:47Z to 15:23:27Z, 40s                                  | **Pass.** 15:25:52Z to 15:26:28Z, 36s             |
| 9   | **Pass.** 153 present and verified, 599 media, gamelist byte-identical | **Pass**                                          |

| Core              | `.srm` after  | States, slot: state, png                                                                          |
| ----------------- | ------------- | ------------------------------------------------------------------------------------------------- |
| `genesis_plus_gx` | `b98e4e38...` | 1: `b4436392...`, `d83b83a1...`; 2: `bf94637a...`, `d83b83a1...`; 3: `6a962a20...`, `5ff0f76a...` |
| `picodrive`       | `f898854e...` | 1: `8e6fa3e2...`, `3d44f154...`; 2: `68821f20...`, `b515b0a9...`                                  |

**The maintainer created the character on the stock row**, which is the one session that changed the
committed save, and made three states, the first two on the same frame. **Slot 3 was the one
compared**, since slots 1 and 2 share an image and cannot tell a real link from a wrong one. After
each row its `.srm` and one state with its `.png` went out of the tree and came back through
`saves restore 239603 --apply`, `restored 1 save(s) and 1 state(s), failed 0, ... with 1
screenshot(s)`, exit 0, every file at its own md5, the image that slot's own.

**PicoDrive read the file Genesis Plus GX wrote and wrote it back at 32 KB**: its 32,768 B are
Genesis Plus GX's 8,191 B followed by zeros, as on `megadrive`. **The declared `<directory>` is where
both cores wrote**, `saves/mastersystem/libretro.<core>/`, and RetroArch chose slots 1 onward from
`found_last_state_slot: #0` whatever ES passed (finding 261).

### `libretro`/`fbneo`: driven, and not certifiable on this library

**FBNeo shows "Romset is unknown" and never starts the game.** RetroBat launches it with
`--subsystem sms`, under which FBNeo takes the set from the file name. RetroBat's own
`bios/fba/FB Alpha (ClrMame Pro XML, Master System only).dat` names the test game's set `gaxewarr`,
CRC `c7ded988`, the same bytes. Started directly on two copies of the zip, outside the ROM tree:

| File                                                | What FBNeo did                                          |
| --------------------------------------------------- | ------------------------------------------------------- |
| `Golden Axe Warrior (USA, Europe, Brazil) (En).zip` | searched for no set at all, 640x480, the error screen   |
| `gaxewarr.zip`, the same bytes                      | `Romset found`, 256x192, the Master System's resolution |

So the row boots none of a No-Intro set, which is what RomM serves, and is **recorded as not
certifiable by the maintainer's ruling**, without an ES session. Nothing was written under `saves/`.
Finding 325.

## The five standalone rows

|            | Selected by                                                | Confirmed on the ES launch line         |
| ---------- | ---------------------------------------------------------- | --------------------------------------- |
| `mesen`    | `mastersystem.emulator = mesen`                            | `-emulator mesen`, empty `-core`        |
| `mednafen` | `mastersystem.emulator = mednafen`, `.core = mastersystem` | `-emulator mednafen -core mastersystem` |
| `ares`     | `mastersystem.emulator = ares`, `.core = MasterSystem`     | `-emulator ares -core MasterSystem`     |
| `SMSHawk`  | `mastersystem.emulator = bizhawk`, `.core = SMSHawk`       | `-emulator bizhawk -core SMSHawk`       |
| `jgenesis` | `mastersystem.emulator = jgenesis`                         | `-emulator jgenesis`, empty `-core`     |

### Checklist for the five

| #   | Result on every one of the five                                                             |
| --- | ------------------------------------------------------------------------------------------- |
| 4   | **Pass, both directions**, every file at its own md5 after a restore                        |
| 5   | **Pass**, two slots each, where each emulator was found to write                            |
| 6   | **N/A**                                                                                     |
| 7   | **Pass**, carried: each was launched from ES on the synced ROM, art and description present |
| 8   | **Pass**, every session read back from RomM by `status`, under `recent:`                    |
| 9   | **Pass.** 153 present and verified, 599 media present, gamelist byte-identical, 0 sent      |

### 4. Battery saves on the five

| Row        | File under `saves/mastersystem/`             | Size     | Slot               | After the row |
| ---------- | -------------------------------------------- | -------- | ------------------ | ------------- |
| `mesen`    | `<rom>.sav`                                  | 8,192 B  | `mesen:battery`    | `2271877c...` |
| `mednafen` | `<rom>.d46e40bbb729ba233f171ad7bf6169f5.sav` | 32,768 B | `mednafen:battery` | `f898854e...` |
| `ares`     | `ares/Master System/<rom>.ram`               | 32,768 B | `ares:battery`     | `5f02b462...` |
| `SMSHawk`  | `bizhawk/Golden Axe Warrior (UE).SaveRAM`    | 8,192 B  | `bizhawk:battery`  | `772cd5c2...` |
| `jgenesis` | `jgenesis/sms/<rom>.sav`                     | 32,768 B | `jgenesis:battery` | `f898854e...` |

**Mesen keeps its own loose `.sav`**, where on `snes` it shared `libretro`'s `.srm`, and **rewrites it
only when the game changes the SRAM**: an agent boot with the seed in place left it untouched, md5
and timestamp. Its first session wrote nothing, and a second, 15:37:54Z to 15:38:51Z, rewrote it.
**mednafen will not start the game while Mesen's `.sav` is there**: it opens the plain `<rom>.sav`,
finds 8,192 B where it keeps 32 KB, and stops with "Error reading from opened file ... Unexpected
EOF" (finding 324). For its row the Mesen file was held out and mednafen's own hashed name seeded;
**the restore placed that hashed save under the name `MednafenRomHash` computes from the `.sms`**,
the first time that path was driven end to end. **BizHawk names the save after its own title**,
`Golden Axe Warrior (UE)`, and the sidecar beside its states reads `Golden Axe Warrior (UE).SMSHawk`,
which is how `saves` attributes it: `learned from the name sidecar beside a save state`. It rewrote
the file on exit with the bytes it loaded, and left a `.SaveRAM.bak` at launch. **ares writes on
exit**, as on `snes`.

After each row its save and one state went out of the tree and came back through
`saves restore 239603 --apply`, failed 0, exit 0, each at its own md5.

### 5. States on the five

| Row        | Directory under `saves/mastersystem/` | Slots and who made them                       | Round-tripped               |
| ---------- | ------------------------------------- | --------------------------------------------- | --------------------------- |
| `mesen`    | `mesen/SaveStates/`                   | `_1.mss` and `_2.mss`, both in ES             | `_2.mss`, `c66a9c5e...`     |
| `mednafen` | `mednafen/sstates/`                   | `.<md5>.mc0` and `.mc9`, both in ES           | `.mc9`, `addb179f...`       |
| `ares`     | `ares/Master System/`                 | `.bs1` and `.bs2`, both by the agent          | `.bs2`, `989bf265...`       |
| `SMSHawk`  | `bizhawk/sstates/SMSHawk/`            | `QuickSave4` in ES; `QuickSave2` by the agent | `QuickSave2`, `88a153f7...` |
| `jgenesis` | `jgenesis/states/`                    | `_0.jst` and `_1.jst`, both in ES             | `_1.jst`, `3298b2c1...`     |

**`mesen`, `mednafen` and `ares` declare no state directory**, and each wrote to the one above,
which the supplement now declares for `mastersystem`. **The maintainer's second mednafen slot was 9,
not 1**: the slot stepped down from 0 and wrapped, where the agent's `F7` in the probe stepped up to
slot 1. Both are valid slots and both synced. **ares keeps its states beside its battery save**, `F2`
saving and `F7` stepping the slot, which on `snes` it did not. BizHawk took ES's `-state_slot 4` for
the pad's key and `Ctrl+F2` wrote slot 2, and its two frames, `Framebuffer.bmp` inside each state,
differ (`c3b18e20...`, `af8b9ac2...`). jgenesis writes its states to
`emulators/jgenesis/states/sms/` and `emulatorLauncher` mirrors them into the declared directory, as
on the systems before. **None of the five writes a screenshot file**, which the preview says: `no
screenshot: the server links none to this state`.

### 8. Sessions

| Row        | Journal, UTC                                    | Length      |
| ---------- | ----------------------------------------------- | ----------- |
| `mesen`    | 15:28:16 to 15:29:04, then 15:37:54 to 15:38:51 | 48s, 57s    |
| `mednafen` | 15:48:20 to 15:49:51, then 15:55:19 to 15:55:47 | 1m 31s, 28s |
| `ares`     | 15:59:42 to 16:00:16                            | 33s         |
| `SMSHawk`  | 16:06:51 to 16:07:37                            | 45s         |
| `jgenesis` | 17:32:11 to 17:34:32                            | 2m 21s      |

Every one is on the server, rom 239603. mednafen's first is the launch that stopped on Mesen's save,
with the error on screen for the whole of it.

## The two `kega-fusion` rows

**Driven, and not certified: step 4 cannot pass on RetroBat 8.2.1.** The other eight steps pass or
carry, step 6 being N/A.

|              | `kega-fusion`/`auto`                                  | `kega-fusion`/`mastersystem`             |
| ------------ | ----------------------------------------------------- | ---------------------------------------- |
| Selected by  | `mastersystem.emulator = kega-fusion`, `.core = auto` | the agent's launch, `-core mastersystem` |
| Confirmed by | `Fusion.exe -auto`                                    | `Fusion.exe -sms`                        |

| #   | Both rows                                                                                               |
| --- | ------------------------------------------------------------------------------------------------------- |
| 1-3 | **Pass**, carried                                                                                       |
| 4   | **Fail on 8.2.1.** The save lands in `emulators/kega-fusion/<rom>.ssm`, which RomMBat does not scan     |
| 5   | **Pass**, two slots each, round-tripped at their own md5                                                |
| 6   | **N/A**                                                                                                 |
| 7   | **Pass** on launch and art, for `auto` from ES                                                          |
| 8   | **Pass** for `auto`, 17:39:17Z to 17:40:26Z, 1m 8s. Carried to `mastersystem`, which the agent launched |
| 9   | **Pass**                                                                                                |

**Kega's Master System save is `emulators/kega-fusion/<rom>.ssm`**, 8,191 B, beside the `megadrive`
`.srm` from that pass: `Fusion.ini`'s one `SRMFiles` key serves every system. Seeded with the
`libretro` `.srm`, Kega rewrote it on exit on both rows. Its states go where `SMSStateFiles` sends
them, `saves/mastersystem/kega-fusion/<rom>.ss<slot>`, `.ss` where `megadrive` is `.gs`, 32,958 B,
`F5` saving and `F7` stepping the slot down from 0 to 9:

| Row            | `.ss0`        | `.ss9`        | Round-tripped |
| -------------- | ------------- | ------------- | ------------- |
| `auto`, in ES  | `4fb3becd...` | `b4bed337...` | `.ss9`        |
| `mastersystem` | `2960cd52...` | `585dcac1...` | `.ss0`        |

The supplement's new `kega-fusion` entry, scoped to `mastersystem`, is what lets them sync. No image
is written. `Fusion.ini` held `Joystick1Using=255` for player 1 before and after the `auto` session.

## What the pass turned up that is not a row

- **RomM's browser player rewrote the save mid-pass** (finding 327). During the Mesen session the
  maintainer launched the game in RomM's EmulatorJS by accident. At 15:28:26Z the server gained save
  496 in `libretro:battery`, with no device, named after PicoDrive's upload
  (`[2026-09-24_15-26-38]`) and holding 8,191 B, `b98e4e38...`: PicoDrive's 32 KB save as Genesis
  Plus GX, which EmulatorJS runs, trims it. The quit flush found the local `.srm` unchanged since it
  was last in step and the server's newer, took the server's, and kept the file it displaced in
  `emulators/rommbat/replaced/`. Same save data, and the right behaviour.
- **Every flush refused one save that is not `mastersystem`'s**: rom 189465 on `megadrive`, where
  the server holds the four bytes `null` another client wrote in July (finding 276).
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
