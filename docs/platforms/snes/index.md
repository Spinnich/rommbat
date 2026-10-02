---
summary: The certification record for `snes`: each row's standing at the floor, and the system's own steps.
read-when: Before certifying a `snes` row, or when asked whether a `snes` row works.
---

# snes

Super Nintendo Entertainment System / Super Famicom. RetroBat calls the folder `snes`, which is what
this folder is named after.

**All fifteen rows `snes` declares are certified**, at RomM `5.3.0` and RetroBat 8.2.1 on
2026-09-24, all nine steps with step 6 N/A because `snes` has no class D:

- `libretro`/`snes9x`, **the row a stock install gives a user**, selected with no override
- `libretro`/`bsnes-jg`, `bsnes`, `bsnes_hd_beta`, `mednafen_snes`, `mesen-s` and `snes9x2005`
- `mesen`, `mednafen`/`snes`, `snes9x`, `ares`/`SuperFamicom`, `bizhawk`/`BSNES`, `Faust` and
  `Snes9x`, and `jgenesis`

**It certifies those fifteen rows and nothing wider.** The seven `libretro` rows needed nothing.
Of the eight standalone rows, Mesen and Snes9x share `libretro`'s loose `.srm` and needed only a
state declaration; the other six needed a battery rule for `snes`, and `mednafen` and `ares` a
state declaration as well. RetroBat lists no firmware for `snes`, and three rows refuse a DSP-1
cartridge without the chip's (below).

## Where each row stands

**Every certified row holds at the floor, RomM `5.3.1` and RetroBat 8.2.1.** Steps 1 and 9 were
re-driven at `5.3.1` on 2026-09-24, in #236, which maps the nine steps. The other steps carry from
the drive at `5.3.0`, since nothing they exercise changed between the two. Nothing is owed.

| File                           | What it holds                                                                                                                                  |
| ------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| This file                      | Steps 1, 2 and 3, which are the system's, with which rows need firmware; what the first boots wrote; what the pass turned up that is not a row |
| [libretro.md](libretro.md)     | The seven `libretro` rows                                                                                                                      |
| [standalone.md](standalone.md) | The eight standalone rows                                                                                                                      |
| [facts.md](facts.md)           | The measured facts about `snes`'s emulators, with RB- IDs                                                                                      |

## The install this was measured on

|           |                                                        |
| --------- | ------------------------------------------------------ |
| RetroBat  | `8.2.1-stable-win64`, the supported floor              |
| RomM      | `5.3.0`, read back by `status` as Supported            |
| Root      | `R:\RetroBat`, found by walking up from the executable |
| Store     | schema 18 of 18, WAL                                   |
| Client    | this branch, deployed twice, named below               |
| Budget    | `none`, as for every system before it                  |
| Test game | Legend of Zelda, The - A Link to the Past (USA)        |

**The test game is a 1 MB LoROM cartridge with 8 KB of battery SRAM**, as jgenesis reports it,
and RomM rom 200280. It writes its save when a name is registered and on Save and Continue, and
**it writes its SRAM at boot too**, so a launch with nothing saved still changes the `.srm` and
uploads a new version. It carries no `<emulator>` pin in `gamelist.xml`. Super Mario Kart (USA),
a DSP-1 cartridge, was booted on every row for step 3 and not played.

**The client was deployed twice.** The first deploy carried this branch's `snes` battery rules and
state declarations, from the boot writes below and the state probes in
[standalone.md](standalone.md#5-states-on-the-eight), and every row was driven on it.
The second, during the `mednafen` row, added only the empty `.rtc` below, which no row's result
depends on.

**The server already held a `libretro:battery` save for Zelda**, written on 2026-09-01, and the
stock row's first flush recorded a conflict against it rather than overwrite either. The
maintainer chose `--keep-local`, which sent the new save as the slot's newest row and left the
September one standing, as the `save-sync` skill says it does.

**Every standalone row after the first was seeded from the save the one before made**, as on
`gba`, `gb` and `gbc`. The seven `libretro` cores, Mesen, mednafen and Snes9x all read one loose
`.srm` and needed no copying; ares's `.ram`, BizHawk's `.SaveRAM` and jgenesis's `.sav` were each
given the latest save before the row's launch. All are the same raw 8 KB, which every row's boot
write showed. The maintainer then continued the file and saved in the game on every row, so every
save the row files measure is one that emulator wrote.

**The maintainer played every row from ES over RDP.** Where a state key did not arrive, the agent
made the missing slot from its own session through `emulatorLauncher` and `keybd_event`; those
launches run no hooks and record no session, and each is named in its row's file.

## The set

|          |                                             |
| -------- | ------------------------------------------- |
| Name     | Spinnich's Super Nintendo Favorites         |
| Scope    | `smart_collection 12`                       |
| Policy   | no game cap, no size cap, ordered by recent |
| Resolves | 276 games, 330.6 MB, into `snes`            |

The sync fetched 276 ROMs and 1,075 media files (1,012.2 MB), and wrote one gamelist with 276
entries, exit 0.

## Steps 1, 2 and 3, for every row

### 1. Mapping

Resolved at layer **`fs_slug`**: RomM's `fs_slug` for the platform is literally `snes`, which is
already a folder in this install.

| `fs_slug`         | Resolved by | What `platforms list` says                                                         |
| ----------------- | ----------- | ---------------------------------------------------------------------------------- |
| `snes`            | `fs_slug`   | RomM's fs_slug 'snes' is already a folder in this install                          |
| `snes-unofficial` | `bundled`   | the bundled table offers `snes`, `snes-msu1` and picked the first this install has |

### 2. Multi-file games and extensions

From the live `es_systems.cfg`:

```text
.smc .fig .sfc .gd3 .gd7 .dx2 .bsx .swc .rom .wad .zip .7z .decomp
```

**276 of 276 resolve and nothing is excluded or unlisted.** Every ROM is a `.zip` holding one file,
so `snes` has no multi-disc or multi-file shape to settle. `.zip` holding a `.sfc` was observed to
launch on all fifteen rows. 275 zips hold a `.sfc`. **One holds a patch and no game**:
`Dark Law - Meaning of Death (Japan) [T-En by AGTP v1.00].zip` is a single 177,786 B `.bps`, a
translation patch for a ROM the library does not carry. It syncs like any other member, and what
is in RomM is the user's to keep (RB-320).

### 3. BIOS

```console
$ rommbat-agent bios snes
RetroBat requires no BIOS for snes.
```

**Exit 0, and step 3 passes by recording so.** RetroBat's manifest names nothing for `snes`, and
RomMBat adds nothing, because the supplement copies only an entry a sibling system lists.

## Which rows need firmware

**Three of fifteen refuse a DSP-1 cartridge, and nothing RetroBat lists would fetch the file.** Each
row was launched on Super Mario Kart (USA) with no coprocessor firmware anywhere in the tree:

| Row                                | With no firmware                                                                 |
| ---------------------------------- | -------------------------------------------------------------------------------- |
| `libretro`, six of the seven cores | Reaches the title screen                                                         |
| `libretro`/`mesen-s`               | **Refuses**: holds on the Nintendo logo, logging no firmware for DSP `dsp1b.rom` |
| `mesen`                            | **Refuses**: asks to select `dsp1b.rom`, 8,192 B                                 |
| `mednafen`/`snes`                  | Reaches the title screen                                                         |
| `snes9x`                           | Reaches the title screen                                                         |
| `ares`/`SuperFamicom`              | Reaches the title screen                                                         |
| `bizhawk`/`BSNES`                  | Warns "Couldn't find firmware SNES+DSP1b", and carries on                        |
| `bizhawk`/`Faust`, `Snes9x`        | Reach the title screen                                                           |
| `jgenesis`                         | **Refuses**: "Cannot load DSP-1 cartridge because DSP-1 ROM is not configured"   |

**The title screen does not exercise the DSP**, which the game uses in a race, so "reaches the
title" is all the eleven are claimed to do. RomM's own known-files list names the chip firmware for
`snes` (`dsp1b.data.rom`, `dsp1b.program.rom`, `st010.*`), in the split form bsnes and ares read,
while Mesen asks for a single `dsp1b.rom`. Zelda has no coprocessor and boots on all fifteen.
RB-317.

**With the firmware, all four that refused or warned reach the title.** The split pair from the
maintainer's RetroBat 8.1.2 BIOS pack matched RomM's md5s, `d10f4468...` for the program and
`1e3f5686...` for the data, and `dsp1b.rom` was made by joining them, program first, 8,192 B,
`332273cc...`. The pack's `dsp1b.zip` holds a 10,240 B `dsp1b.bin`, MAME's and FBNeo's form, which
none of these rows reads.

| Row                  | Reads                          | From                                                                                       |
| -------------------- | ------------------------------ | ------------------------------------------------------------------------------------------ |
| `libretro`/`mesen-s` | `dsp1b.rom`                    | `bios\`, RetroArch's system directory                                                      |
| `bizhawk`/`BSNES`    | `SNES+DSP1b`                   | `bios\`, with no warning once the files are there                                          |
| `mesen`              | `dsp1b.rom`                    | `emulators\mesen\Firmware\`, not `bios\`; RetroBat puts none there                         |
| `jgenesis`           | whatever `dsp1_rom_path` names | a key in `jgenesis-config.toml` RetroBat's launcher never sets, and keeps when set by hand |

So on a stock install `mesen` and `jgenesis` refuse a DSP-1 game even with the firmware in
`bios\`. **By the RetroBat team's account, relayed by the maintainer on 2026-09-24, RetroBat's
firmware list covers only a system's default emulator, by design**, and `snes`'s default,
`libretro`/`snes9x`, runs the DSP without firmware, which is why `snes` lists none. The
maintainer's install keeps the three files in `bios\`, the copy in Mesen's folder and the added
key; the config as it was is `R:\rommbat-evidence\snes\dsp-with-fw\jgenesis-config.before.toml`.

## What the boot launches wrote

**Not steps 4 or 5.** No one saved in any of these launches; each ran about 25 seconds from
`emulatorLauncher` with no key sent. Every file below was moved to `R:\rommbat-evidence\snes\boot\`
before a flush could send it.

| Row                         | Wrote at boot, under `saves/snes/`                                         |
| --------------------------- | -------------------------------------------------------------------------- |
| `libretro`, all seven cores | `<rom>.srm`, 8,192 B                                                       |
| `libretro`/`mednafen_snes`  | and `<rom>.rtc`, 0 B                                                       |
| `mesen`, `snes9x`           | `<rom>.srm`, 8,192 B                                                       |
| `mednafen`/`snes`           | `<rom>.608c22b8ff930c62dc2de54bcd6eba72.srm`, 8,192 B                      |
| `ares`/`SuperFamicom`       | nothing when killed; ended with `Esc`, `ares/Super Famicom/<rom>.ram`      |
| `bizhawk`, all three cores  | `bizhawk/Legend of Zelda, The - A Link to the Past (USA).SaveRAM`, 8,192 B |
| `jgenesis`                  | `jgenesis/sfc/<rom>.sav`, 8,192 B                                          |

**`snes9x2005` and BizHawk's `Faust` and `Snes9x` wrote all zeros where the rest wrote Zelda's
boot pattern**, and still reached the title. **mednafen's md5 is of the whole `.sfc` inside the
zip**, and it writes a `.srm`, not the `.sav` its other systems' rule reads. On Super Mario Kart
ares also wrote `<rom>.dram`, 512 B, beside a 2,048 B `.ram`: the DSP's data RAM.

## What the pass turned up that is not a row

- **jgenesis closed once, 3.5 s into a launch from ES, with exit code 1**, and did not do so again.
  It could not be reproduced from `emulatorLauncher` with and without ES's controller arguments,
  from its own command line, or on the exact seed it was given, in a scratch folder with a copy of
  its config. The maintainer's next launch from ES ran normally. Exit code 1 is an error jgenesis
  returned, not a panic, and `emulatorLauncher` discards its stderr, so the reason was not seen.
  That launch was the first of the pass to report the pad as an Xbox 360 Controller. RB-321.
- **A server state RomMBat cannot place is skipped on every restore**: `<rom>.state` under emulator
  `snes9x`, from before this pass, matches no declaration now that standalone Snes9x has one, and
  is reported and left alone, as it was before.
- **The first flush after each deploy refused one save that is not `snes`'s**: rom 189465 on
  `megadrive`, where the server holds the four bytes `null` another client wrote in July.
- **ES rewrote `gamelist.xml` on its own exit**, so step 9 compares against a copy taken after the
  last ES session: the re-sync left that copy byte-identical and said `gamelists: all 1 unchanged`.

## What this file will not claim

- **Nothing about `snes` under any build but these.** Every row was measured on RetroBat 8.2.1 and
  RomM `5.3.0`.
- **Nothing about another game.** Zelda is one LoROM cartridge with 8 KB of SRAM and no
  coprocessor. A game with a coprocessor, a real-time clock or a larger SRAM may be sized and named
  differently, and ares's `.dram` was seen on Super Mario Kart only.
- **Nothing about a DSP game past its title screen**, on any row.
- **Nothing about a `.smc`.** Every ROM in the set is a `.sfc`; a `.smc` can carry a 512-byte copier
  header, and mednafen's hashed name for one is left unnameable until one is driven.
- **Nothing about mednafen's hashed `.srm` driven through a restore**, which only the tests cover.
