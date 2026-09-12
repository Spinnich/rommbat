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

**The gate opened with M7 stage 7b, and it is open now.** Every pass needs a person launching
real games, and doing that through a terminal instead of the gamepad UI makes a long job longer.
7b landed and a game was driven from EmulationStation and back through the hooks, which is what
the gate was waiting on. The waves finish against an M8 package, which is what a user installs.
That launch certified nothing: it is one of nine points on one row.

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

   Three answers, not two, and the difference matters when a system name is mistyped.
   `RetroBat requires no BIOS for <system>` is a real system with nothing to fetch and counts as
   step 3 passing. A name the install's `es_systems.cfg` does not declare is refused with a
   non-zero exit and is a typo, never a pass.

4. Save shape classified A/B/C/D **for this emulator**, and a battery save round-trips.
5. A save state round-trips including its screenshot, per this emulator's `es_savestates.cfg`
   entry, and **the declared `<directory>` is confirmed to be where the emulator really
   writes**. An empty declared directory means you are looking in the wrong place, never that
   the game has no states: `openmsx` declares one it does not use, writing
   `bios/openmsx/savestates/` instead. `flycast` was the second until RetroBat 8.2.1 fixed
   `emulatorlauncher#1336`; on the supported floor its declared `flycast/sstates` is
   populated and is the one to read, so confirm it rather than expecting it to be empty.
6. Where class D applies, the per-game memory card option is verified via `es_settings.cfg`.
7. A game launches from EmulationStation after sync, with art and metadata present.
8. A play session is recorded and reaches RomM.
9. **Re-sync is a clean no-op**: zero uploads, zero downloads, no gamelist churn. This is
   the strongest single signal that slots, cursors and mapping are all correct.

## Wave order

Named in `es_systems.cfg`'s vocabulary, which is what a record file is named after: Mega CD is
`megacd`, WonderSwan is `wswan`, WonderSwan Color is `wswanc`. Nintendo's DSi is out of scope
rather than unscheduled, because RetroBat declares no `dsi` system.

| Wave | Systems                                                                                                  | n   | Why here                                                                          |
| ---- | -------------------------------------------------------------------------------------------------------- | --- | --------------------------------------------------------------------------------- |
| 1    | `nes`, `snes`, `gb`, `gbc`, `gba`, `megadrive`, `mastersystem`                                           | 7   | Class A saves observed, little or no BIOS, single files. Proves the spine         |
| 2    | `psx`, `pcengine`, `pcenginecd`, `megacd`, `saturn`, `n64`                                               | 6   | BIOS resolved by md5 and disc formats, plus class B (`saturn`) and BD (`megacd`)  |
| 3    | `ps2`, `gamecube`, `dreamcast`, `xbox`, `psp`, `wii`                                                     | 6   | The hard save shapes: memory cards, GCI folders, VMU, and the class C directories |
| 4    | `lynx`, `gamegear`, `wswan`, `wswanc`, `ngp`, `ngpc`, `atari2600`, `atari7800`, `virtualboy`, `pokemini` | 10  | Ten cheap rows that answer one question: is the class A fallback safe             |
| 5    | `atari5200`, `colecovision`, `intellivision`, `vectrex`, `channelf`, `arcadia`, `odyssey2`, `sg1000`     | 8   | Generation 2, small BIOS sets, every recommended core under libretro              |
| 6    | `fds`, `satellaview`, `sufami`, `sega32x`, `n64dd`, `supergrafx`                                         | 6   | `hardware=extension`: they share a parent system's tree, which nothing has tested |
| 7    | `3do`, `jaguar`, `jaguarcd`, `nds`                                                                       | 4   | Shape unclassified in all four, and `jaguar` carries the one non-libretro pick    |
| 8    | `neogeo`, `neogeocd`, `fbneo`, `mame`                                                                    | 4   | Arcade: romset-versioned naming, the fan-out question, 12 BIOS files              |

Arcade is last on purpose: it is the only wave needing the explicit folder-choice decision
and the only one coupled to romset versions.

**The first pass through a wave is one row per system**, on that system's recommended emulator
and core. Alternates are a second pass, except where the shape is emulator-dependent and the
alternate is the point, which `save_shapes.json`'s `DependsOnEmulator` flag names.

**Steps 1, 2 and 3 can be batched for a whole wave before anyone sits down**, since none of them
needs an emulator running. Staging a wave that way means its BIOS gaps are known before a
controller is picked up, and it leaves six of nine steps open per record. Steps 4 through 9
cannot be staged.

**The order is hand-maintained, and a `hardware=console` filter does not reproduce it.** That
filter drops `gb`, `gbc`, `gba`, `lynx`, `gamegear`, `ngp`, `ngpc`, `wswan`, `wswanc`, `nds` and
`psp`, which are `hardware=portable`, and `fds`, `satellaview`, `sufami`, `sega32x`, `megacd`
and `n64dd`, which are `hardware=extension`. A manufacturer allowlist of Atari, Bandai, NEC,
Nintendo, Sega, SNK and Sony drops all of generation 2 on top of that. Read `<manufacturer>`,
`<hardware>` and `<release>` when RetroBat adds a system, then decide the wave from what the
system introduces.
