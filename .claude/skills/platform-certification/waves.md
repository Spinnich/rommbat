# Certification waves

Part of the [platform-certification](SKILL.md) skill. Which systems are certified together, in what order, and what a wave costs.

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

**A wave certifies every emulator and core the system declares, not one recommended row.**
Certifying a pick tells a user nothing unless their install runs it, and the install decides
that through `<system>.emulator` and `<system>.core` in `es_settings.cfg`, which RomMBat neither
sets nor reads. Wave 1 is **81 rows against 7 systems**.

The row count is affordable because **only steps 4, 5 and 6 are per row**, and they collapse
into four families rather than 81 shapes: **libretro** (30 of wave 1's 81, one entry,
`{{system}}/libretro.{{core}}`, class A `.srm` throughout), **bizhawk** (14, core-scoped),
**jgenesis** (7, not core-scoped), and **30 rows that declare no state directory at all**
(`mednafen`, `mesen`, `ares`, `snes9x`, `mgba`, `nosgba`, `kega-fusion`).

**That fourth family is the expensive one, not the cheap one, and an earlier revision of this
section had it backwards.** It said step 5 there was a recorded declaration and the row still
certified on the other eight steps. Driving all nine `nes` rows showed otherwise: `mednafen`,
`mesen` and `ares` each wrote a real save state into a directory they name themselves, invisible
to `StateScanner` because it works from `es_savestates.cfg` alone. **Declaring no directory is not
writing no state.** Look in the emulator's own tree under `saves/<system>/` before recording step 5
for one of these rows, and record what you found there rather than what the file declares. On
`nes` and `megadrive` they are carried by `data/retrobat/es_savestates.supplement.xml`, each entry
scoped to the systems it was driven on, and ares with one entry per system because its directory
changes with it; a row in this family on another system needs its own entry from its own pass,
plus a battery rule, before it can pass steps 4 and 5.

**Find each emulator's slot keys before sitting down.** The pad's save key saves to the current
slot, and only `bizhawk` takes ES's `-state_slot` as that slot (RB-269). `jgenesis`, `mesen`,
`mednafen` and `ares` all step the slot on `F7` and save on `F2` with no modifier, which the agent
can send locally through `emulatorLauncher` when RDP eats them (RB-275). The keys are in
`es_padtokey.cfg` or the emulator's own config (`mednafen.cfg`, Mesen's `settings.json`). Kega
Fusion saves on `F5` and steps the slot down on `F7`, has no pad-to-key file, and needs its
controls remapped in its own menu before the pad plays (RB-284). On the maintainer's
RetroBat machine a Logitech LIGHTSPEED receiver takes DirectInput index 0, so every
DirectInput-indexed generator (Kega, mednafen, Snes9x, Mesen, PCSX2 and others) binds player 1
one pad too high until the floor carries emulatorlauncher#1376's fix (`docs/upstream/issues.md`).
A pad that does nothing there is that, not a failed row. **When a key's effect cannot be seen,
take a screenshot of the screen from the agent's session** rather than sending keys blind: a
blind Start on a title screen is as likely to land during a fade as on the menu.

The libretro family is the one that most needs driving rather than assumed: RB-134 measured
two cores writing an identical `state1` filename, which survived as two server rows only because
the uploaded name carries the core.

**Name how the row was selected, every time**, and confirm what ran from
`emulationstation/emulatorLauncher.log` rather than from configuration. A row driven under an
`es_settings.cfg` override is not the row a stock install gives a user. Never read
`retroarch.cfg` for this: RB-217 measured that it describes only the last game launched.

**`<extension>` belongs to the system, not the row.** It is a union across every emulator, and
RetroBat publishes no per-`(emulator, core)` extension data anywhere, so record what the
certified core was **observed** to launch and treat the rest as declared and unproven.

**Steps 1 and 3, and the inventory half of step 2, can be batched for a whole wave before
anyone sits down**, since none of them needs an emulator running. Staging a wave that way means its BIOS gaps are known before a
controller is picked up, and it leaves six of nine steps open per record. Steps 4 through 9
cannot be staged.

**The order is hand-maintained, and a `hardware=console` filter does not reproduce it.** That
filter drops `gb`, `gbc`, `gba`, `lynx`, `gamegear`, `ngp`, `ngpc`, `wswan`, `wswanc`, `nds` and
`psp`, which are `hardware=portable`, and `fds`, `satellaview`, `sufami`, `sega32x`, `megacd`
and `n64dd`, which are `hardware=extension`. A manufacturer allowlist of Atari, Bandai, NEC,
Nintendo, Sega, SNK and Sony drops all of generation 2 on top of that. Read `<manufacturer>`,
`<hardware>` and `<release>` when RetroBat adds a system, then decide the wave from what the
system introduces.
