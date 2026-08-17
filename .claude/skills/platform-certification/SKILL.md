---
name: platform-certification
description: Certifying a RetroBat system end to end before claiming it works. Use when adding support for a platform or when asked whether a platform is done.
---

# Platform certification

Once the framework works end to end on one platform, stop building horizontally and certify
platforms one at a time. Each surfaces its own edge cases; certifying in isolation keeps
them from arriving as one intermixed pile.

**The unit is `(system, emulator, core)`, never the system alone and never an aggregate.**
"RetroArch works" is unverifiable, and so is "snes works". Two emulators for one console
differ exactly where it costs a save:

- **Save shape is a property of `(system, emulator)`.** `psx` under libretro writes plain
  `saves/psx/*.srm` and is class A; `psx` under DuckStation writes a memory card named from
  its internal database title and needs Game-ID attribution. `save_shapes.json` carries a
  `DependsOnEmulator` flag for this.
- **State directories and filenames are per emulator**, thirteen of them in
  `es_savestates.cfg`, and **two of the thirteen declare a directory the emulator does not
  write to**. Which two is not derivable from the system.
- **`libretro` and `bizhawk` are core-scoped** (`{{system}}/libretro.{{core}}`,
  `{{system}}/bizhawk/sstates/{{core}}`), so one game under two cores has independent state
  sets. That is the third element of the triple.
- **BIOS follows the emulator too**: `batocera-systems.json` keys firmware on the system, and
  the emulator decides which of it is consulted.

So a certified row names the emulator and the core. Expect two to four passes per system in
the wave table below rather than one.

## When to run this

**The wave rollout starts after M7.** Every pass needs a person launching real games, and
doing that through a terminal instead of the gamepad UI makes a long job longer. The waves
finish against an M8 package, which is what a user installs.

**Steps 4, 5 and 6 do not wait**, because they are the ones where being wrong destroys data
rather than costing a re-download. Each M6 stage owes one hands-on pass of the save shape it
added: one game, one emulator, one real save or state, driven through EmulationStation and
back. That is not a certification and must not be recorded as one, but "the tests pass" and
"an emulator wrote this and RomMBat handled it" are different claims and only the second one
is evidence.

## Checklist

Record results in `docs/platforms/<system>.md`, one section per `(emulator, core)`. All nine,
or it is not certified. Steps 1, 2, 3, 7, 8 and 9 are largely per system and can be carried
across emulators with a note; **steps 4, 5 and 6 have to be redone per emulator.**

1. Folder mapping resolves, and the resolution layer is recorded.
2. `<extension>` list captured from the live `es_systems.cfg`; a known-unsupported file is
   correctly excluded from the sync set and reported.
3. Required BIOS resolved against RomM **by md5**; gaps listed with expected filename and hash.
   Run `rommbat-agent bios <system>` for the report and `bios <system> --apply` to fetch, and
   record all four states rather than a pass or fail: present, fetched, not in the library, and
   the ones RetroBat names no hash for. A system whose whole requirement is hashless (28 of the
   99 are) is certified on the other eight steps, and step 3 says so in those words.
4. Save shape classified A/B/C/D **for this emulator**, and a battery save round-trips.
5. A save state round-trips including its screenshot, per this emulator's `es_savestates.cfg`
   entry, and **the declared `<directory>` is confirmed to be where the emulator really
   writes**. An empty declared directory means you are looking in the wrong place, never that
   the game has no states: `flycast` and `openmsx` both declare one they do not use.
6. Where class D applies, the per-game memory card option is verified via `es_settings.cfg`.
7. A game launches from EmulationStation after sync, with art and metadata present.
8. A play session is recorded and reaches RomM.
9. **Re-sync is a clean no-op**: zero uploads, zero downloads, no gamelist churn. This is
   the strongest single signal that slots, cursors and mapping are all correct.

## Wave order

| Wave | Systems                                                                                      | Why here                                                                            |
| ---- | -------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| 1    | `nes`, `snes`, `gb`, `gbc`, `gba`, `megadrive`, `mastersystem`                               | Class A saves, no BIOS, single files. Proves the spine                              |
| 2    | `n64`, `psx`, `saturn`, `segacd`, `pcengine`, `pcenginecd`                                   | Introduces BIOS and disc formats                                                    |
| 3    | `ps2`, `gamecube`, `dreamcast`, `xbox`                                                       | The hard save shapes                                                                |
| 4    | `neogeo`, `neogeocd`, `fbneo`                                                                | Arcade: romset-versioned naming, the fan-out question, 12 BIOS files for `neogeocd` |
| 5    | `wonderswan`, `wonderswancolor`, `ngp`, `ngpc`, `lynx`, `gamegear`, `atari2600`, `atari7800` | Long tail, mostly class A                                                           |

Arcade is last on purpose: it is the only wave needing the explicit folder-choice decision
and the only one coupled to romset versions.

The order is derivable rather than hand-maintained: `es_systems.cfg` carries
`<manufacturer>`, `<hardware>` and `<release>`, so filtering to `hardware=console` for
Atari, Bandai, NEC, Nintendo, Sega, SNK and Sony and sorting by year reproduces this list
and stays correct as RetroBat adds systems.
