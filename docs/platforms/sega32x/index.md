---
summary: The certification record for `sega32x`: each row's standing at the floor, and the system's own steps.
read-when: Before certifying a `sega32x` row, or when asked whether a `sega32x` row works.
---

# sega32x

Sega 32X. RetroBat calls the folder `sega32x`, which is what this folder is named after. It is
`hardware=extension` with `<group>megadrive</group>`, but keeps its own `roms\sega32x` and
`saves\sega32x`.

**Four of the five rows `sega32x` declares are certified**, at RomM `5.3.1` and RetroBat 8.2.1 on
2026-10-05, all nine steps, with step 6 N/A because `sega32x` has no class D:

- `libretro`/`picodrive`, **the row a stock install gives a user**, selected with no override
- `ares`/`Mega32X`, `bizhawk`/`PicoDrive` and `jgenesis`

**`kega-fusion`/`sega32x` is driven and not certified**: its battery saves land in
`emulators/kega-fusion/`, which RomMBat does not read, as on `megadrive` (RB-283), and its states in
the parent's `saves/megadrive/kega-fusion/` (RB-416).

**ares and BizHawk keep no serial EEPROM**, so the two Acclaim cartridges in the library, NBA Jam TE
and NFL Quarterback Club, keep no save under either (RB-415). An SRAM cartridge round-trips on both.
That is the emulators, and the rows are certified on the SRAM they do keep.

**It certifies those four rows and nothing wider.** `libretro`/`picodrive` needed nothing. The three
standalone rows needed a battery rule each for `sega32x`, and ares a state declaration, from the
boot launches below (#464).

## Where each row stands

**Every certified row holds at the floor, RomM `5.3.1` and RetroBat 8.2.1**, measured there on
2026-10-05. Nothing is owed.

| File                           | What it holds                                                                                 |
| ------------------------------ | --------------------------------------------------------------------------------------------- |
| This file                      | Steps 1, 2 and 3, which are the system's; what the first boots wrote; how the test games save |
| [libretro.md](libretro.md)     | `libretro`/`picodrive`                                                                        |
| [standalone.md](standalone.md) | ares, BizHawk and jgenesis, and Kega Fusion                                                   |
| [facts.md](facts.md)           | The measured facts about `sega32x`'s emulators, with RB- IDs                                  |

## The install this was measured on

|            |                                                                                     |
| ---------- | ----------------------------------------------------------------------------------- |
| RetroBat   | `8.2.1-stable-win64`, the supported floor                                           |
| RomM       | `5.3.1`, read back by `status` as Supported                                         |
| Root       | `D:\retrobat-agent`, the agent tree, given by `--root`                              |
| Store      | schema 21 of 21, WAL                                                                |
| Client     | `main` at `84d963d`, carrying #464's rules, deployed before the first ES session    |
| Emulators  | ares, Kega Fusion, BizHawk and jgenesis installed by ES on their first launch       |
| Test games | Knuckles' Chaotix (Japan, USA) (En), NBA Jam - Tournament Edition (World), and Doom |

**The library is read from each cartridge's header**, the `RA` tag at `0x1B0`: seven of the 50
games declare SRAM and two, NBA Jam TE and NFL Quarterback Club, Acclaim's serial EEPROM (`RA e840`).

| Game                                 | Rom    | Medium                         | Why it is in the set                    |
| ------------------------------------ | ------ | ------------------------------ | --------------------------------------- |
| Knuckles' Chaotix (Japan, USA) (En)  | 209635 | SRAM, 1 KB, odd bytes (`f820`) | The SRAM round trip on every row        |
| NBA Jam - Tournament Edition (World) | 209644 | serial EEPROM, 256 B (`e840`)  | The second medium                       |
| Doom (Japan, USA) (En)               | 209630 | none                           | What a row writes for no battery at all |

**Chaotix saves when it reaches the hub and when a slot is deleted**, not when a new game starts
in a slot: a new game in slot 2, played for a minute, left both ares's and BizHawk's file
byte-identical to the seed. **Deleting a slot from the data select writes at once**, which made it
the save each later row was tested on. **NBA Jam writes its EEPROM only when it formats a blank one
at boot**: entering initials left PicoDrive's file at its 12:51 md5 across two later sessions.

**Each later row was seeded from the maintainer's `libretro` save**, which reached the hub: BizHawk
takes PicoDrive's 1,024 B layout as is, and ares and jgenesis its 512 odd bytes. **Every row showed
slot 1's progress**, then deleting slot 1 changed the same 20 bytes, in the slot and its copy at
`0x200`, so the three files after the delete are one save in two layouts. None of the games carries
an `<emulator>` pin from the library; ES wrote one for each choice the maintainer made, and the agent
set the rest in `gamelist.xml` with ES closed. The server held no save or state for any of the three
before the pass.

## The set

|          |                                                                     |
| -------- | ------------------------------------------------------------------- |
| Name     | Picked on RomMBat agent tree                                        |
| Scope    | `picked`, the three games added with `game install`                 |
| Policy   | no game cap, no size cap, ordered by recent                         |
| Resolves | 7 games across the tree's systems, 5.8 MB, the three into `sega32x` |

The sync fetched the three ROMs and 12 media files, and wrote one gamelist with three entries.

## Steps 1, 2 and 3, for every row

### 1. Mapping

**RomM holds two platforms with the slug `sega32`**, and both land in `sega32x`:

| `fs_slug`            | Games | Resolved by | What `platforms list` says                                   |
| -------------------- | ----- | ----------- | ------------------------------------------------------------ |
| `sega32x`            | 50    | `fs_slug`   | RomM's fs_slug 'sega32x' is already a folder in this install |
| `sega32x-unofficial` | 321   | `bundled`   | From the bundled table                                       |

### 2. Multi-file games and extensions

From the live `es_systems.cfg`:

```text
.32x .smd .bin .md .zip .7z
```

**All 50 games on `sega32x` are a `.zip` holding one `.32x`**, 2 to 4 MB, so `sega32x` has no
multi-disc or multi-file shape to settle. The six Sega CD 32X games are not in the library.

### 3. BIOS

```console
$ rommbat-agent bios sega32x
3 RetroBat names no hash for

  sega32x
    no hash to check         bios/32X_G_BIOS.BIN
    no hash to check         bios/32X_M_BIOS.BIN
    no hash to check         bios/32X_S_BIOS.BIN
```

**RetroBat names no hash for any of `sega32x`'s three files**, so step 3 certifies on the other eight
steps. RomM holds no firmware on either 32X platform. **No row needs firmware**: with no 32X or Mega
Drive BIOS in the tree, every row booted all three games, Kega Fusion with its three
`32X_*_BIOS` paths in `Fusion.ini` pointing at files not present.

## What the boot launches wrote

**Not steps 4 or 5.** Each launch ran about 30 seconds from `emulatorLauncher` with no key sent, and
every file was deleted before the first ES session. RB-413 and RB-414 hold the detail.

| Row                     | Chaotix                                    | NBA Jam                                      | Doom             |
| ----------------------- | ------------------------------------------ | -------------------------------------------- | ---------------- |
| `libretro`/`picodrive`  | nothing                                    | `<rom>.srm`, 8,192 B, the 256 B format first | nothing          |
| `ares`/`Mega32X`        | `ares/Mega 32X/<rom>.ram`, 512 B, `0xFF`   | `<rom>.eeprom`, 512 B, `0xFF`                | nothing          |
| `kega-fusion`/`sega32x` | nothing                                    | `emulators/kega-fusion/<rom>.srm`, 256 B     | nothing          |
| `bizhawk`/`PicoDrive`   | `bizhawk/<title>.SaveRAM`, 1,024 B, `0x00` | 8,192 B, `0x00`                              | 16,384 B, `0x00` |
| `jgenesis`              | nothing                                    | `jgenesis/32x/<rom>.sav`, 256 B, the format  | nothing          |

## What the pass turned up that is not a row

- **Four emulators were not on the agent tree**: ES installed ares, Kega Fusion, BizHawk and jgenesis
  on their first launch, each after a modal the maintainer answered.
- **ares and BizHawk need no `0xFF` or `0x00` rule for EEPROM**: ares's blank file is passed over as
  erased, and BizHawk's zeroed one uploads, by the maintainer's ruling (RB-414).
- **ES rewrote `gamelist.xml` on its own exit**, so step 9 compares against a copy taken after the last
  ES session.

## What this file will not claim

- **Nothing about `sega32x` under any build but these.** Every row was measured on RetroBat 8.2.1
  and RomM `5.3.1`.
- **Nothing about another game's save size.** Chaotix keeps 512 B in a 1 KB odd-byte window, and the
  sizes in the row files are each emulator's file for it.
- **Nothing about the Sega CD 32X games**, which the library does not hold.
