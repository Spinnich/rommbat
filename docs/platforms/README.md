# Platform certification records

One file per RetroBat system, named `<system>.md` after the folder name in `es_systems.cfg`,
**with a section per emulator inside it**.

**The unit is `(system, emulator, core)`, never the system alone.** Two emulators for one
console differ exactly where it costs a save: `psx` under libretro writes a plain `.srm` and is
class A, while `psx` under DuckStation writes a memory card named from an internal database
title and needs Game-ID attribution. State directories and filenames are per emulator, thirteen
of them, and two of the thirteen declare a directory the emulator does not write to. `libretro`
and `bizhawk` are core-scoped on top of that, so one game under two cores has independent state
sets. So "snes is certified" is not a claim; "`snes` under `libretro`/`snes9x` is certified"
is, and it says nothing about `snes` under `bizhawk`.

A pass is certified when all nine of these hold against a real install **at the current RomM
and RetroBat floor**. A floor move leaves a record owing the steps it touches, and the row is
not certified at the new floor until they pass; the `platform-certification` skill, "When the
floor moves", says how to map one. **A pass is not done at
eight of nine**, and it cannot be finished from a desk: step 7 requires actually launching a
game. Three of the nine can be staged ahead of time, which is a different claim and is below.

1. Folder mapping resolves, and the record names **which layer** resolved it.
2. Multi-disc and multi-file games land correctly: the shapes this library holds for the
   system are recorded, and each lands where the emulator reads it and launches from ES.
3. BIOS listed in `batocera-systems.json` resolved against RomM by md5; what RomM lacks is
   listed, and never fails the pass.
4. Save shape classified (A/B/C/D) **for this emulator**, and the battery save round-trips.
5. Save state round-trips including its screenshot, per this emulator's `es_savestates.cfg`
   entry, with the declared directory confirmed against where it really writes.
6. Where class D applies, the per-game memory card option is verified.
7. A game launches from EmulationStation after sync, with art and metadata.
8. Play session recorded and reaches RomM.
9. Re-sync is a clean no-op.

Steps 1, 2, 3, 7, 8 and 9 are largely per system and can be carried across emulators with a
note saying so. **Steps 4, 5 and 6 have to be redone per emulator**, and they are also the
three where being wrong destroys data rather than costing a re-download.

**Step 2 was an extension check until the extension stopped gating a sync.** Until 2026-09-20
it required a known-unsupported file to be excluded, which held `nes` open at eight of nine for
a property of the library rather than of the software; it then became "every ROM survives the
extension check". Now nothing is excluded on its extension at all: `<extension>` is a per-system
union across every emulator, so it cannot say what a given `(emulator, core)` opens, and members
it omits sync and are reported as unlisted in ES. What the step checks instead is the concern
the extension check was a poor proxy for, **multi-disc and multi-file placement**. Records
written before the change passed the old step 2 and carry that reading; the placement half is
owed on them when a record is next touched, and a single-file library passes it by saying so.

Load the `platform-certification` skill before starting. Record what failed as well as
what passed; a record that only lists successes is not evidence.

## Rollout order

Ordered by what each wave introduces, not by generation. Novelty first while the machinery is
still unproven, then the long tails that repeat a shape already established. Arcade is
deliberately last: it is the only wave needing the explicit folder-choice decision from M2, and
it carries romset-version coupling nothing else does.

**The systems are named in `es_systems.cfg`'s vocabulary**, which is the one record files are
named in. It is not RomM's and it is not the name on the box: Mega CD is `megacd`, WonderSwan
is `wswan`, WonderSwan Color is `wswanc`. Nintendo's DSi has a RomM slug (`nintendo-dsi`) and no
RetroBat folder, because melonDS runs DSi titles under `nds`, so it is out of scope here rather
than merely unscheduled.

| Wave | Systems                                                                                                  | n   | Why here                                                                          |
| ---- | -------------------------------------------------------------------------------------------------------- | --- | --------------------------------------------------------------------------------- |
| 1    | `nes`, `snes`, `gb`, `gbc`, `gba`, `megadrive`, `mastersystem`                                           | 7   | Class A saves observed, little or no BIOS, single files. Proves the spine         |
| 2    | `psx`, `pcengine`, `pcenginecd`, `megacd`, `saturn`, `n64`                                               | 6   | BIOS resolved by md5 and disc formats, plus class B (`saturn`) and BD (`megacd`)  |
| 3    | `ps2`, `gamecube`, `dreamcast`, `xbox`, `psp`, `wii`                                                     | 6   | The hard save shapes: memory cards, GCI folders, VMU, and the class C directories |
| 4    | `lynx`, `gamegear`, `wswan`, `wswanc`, `ngp`, `ngpc`, `atari2600`, `atari7800`, `virtualboy`, `pokemini` | 10  | Ten cheap rows that answer one question: is the class A fallback safe             |
| 5    | `atari5200`, `colecovision`, `intellivision`, `vectrex`, `channelf`, `arcadia`, `odyssey2`, `sg1000`     | 8   | Generation 2, small BIOS sets, every recommended core under libretro              |
| 6    | `fds`, `satellaview`, `sufami`, `sega32x`, `n64dd`, `supergrafx`                                         | 6   | `hardware=extension`: they share a parent system's tree, which nothing has tested |
| 7    | `3do`, `jaguar`, `jaguarcd`, `nds`                                                                       | 4   | Shape unclassified in all four, and `jaguar` carries the one non-libretro pick    |
| 8    | `neogeo`, `neogeocd`, `fbneo`, `mame`                                                                    | 4   | Arcade: romset-versioned naming, the ten-folder mapping question, 12 BIOS files   |

**The order is not derivable from `hardware=console`, and an earlier revision of this file said
it was.** That filter drops eleven systems the table already carried, because `gb`, `gbc`, `gba`,
`lynx`, `gamegear`, `ngp`, `ngpc`, `wswan`, `wswanc`, `nds` and `psp` are `hardware=portable` and
`fds`, `satellaview`, `sufami`, `sega32x`, `megacd` and `n64dd` are `hardware=extension`. A
manufacturer allowlist of Atari, Bandai, NEC, Nintendo, Sega, SNK and Sony drops the whole of
generation 2 on top of that, since RetroBat attributes those to Coleco, Emerson, Fairchild,
Mattel, MB and "Magnavox - Philips". `<manufacturer>`, `<hardware>` and `<release>` are worth
reading when a new system appears, but the wave a system belongs in is a judgement about what it
introduces, and it is hand-maintained.

### Every emulator and core a supported platform declares

**A wave certifies the whole matrix, not one recommended row per system.** Certifying a pick
tells a user nothing unless their install happens to run it, and the install decides that
through `<system>.emulator` and `<system>.core` in `es_settings.cfg`, which RomMBat neither
sets nor reads. So the unit stays `(system, emulator, core)` and the wave covers all of them.

This is more rows than it sounds and less work than the row count implies. Wave 1 is **81 rows
against 7 systems**:

| System         | Rows   | Can sync states |
| -------------- | ------ | --------------- |
| `nes`          | 9      | 9               |
| `snes`         | 15     | 15              |
| `gb`           | 14     | 14              |
| `gbc`          | 12     | 12              |
| `gba`          | 10     | 9               |
| `megadrive`    | 11     | 11              |
| `mastersystem` | 10     | 10              |
| **Total**      | **81** | **80**          |

`nes` is 9 of 9, `megadrive` 11 of 11, `gba` 9 of 10, `gb` 14 of 14, `gbc` 12 of 12, `snes` 15 of 15 and `mastersystem` 10 of 10 because RomMBat's bundled supplement
declares the emulators `es_savestates.cfg` leaves out there: `mednafen`, `mesen` and `ares` on
`nes`, `mednafen`, `ares` and `kega-fusion`'s three rows on `megadrive`, and `mgba`, `mednafen`,
`mesen` and `ares` on `gba`, the same four on `gb` and on `gbc`, `mednafen`, `mesen`, `snes9x` and `ares` on `snes`, and `mednafen`, `mesen`, `ares` and both `kega-fusion` rows on `mastersystem`. `nosgba` writes states nowhere RomMBat was shown. Every other system counts
`es_savestates.cfg` alone.

**Steps 1, 2, 3, 7, 8 and 9 are per system and carry across the rows with a note.** Only 4, 5
and 6 are redone per row, and they collapse into four families rather than 81 separate shapes:

- **libretro, 30 of the 81.** One `es_savestates.cfg` entry, `{{system}}/libretro.{{core}}`,
  differing only in a path segment, and class A loose `.srm` throughout. This is the family that
  makes the matrix affordable, and it is the one that most needs driving: finding 134 measured
  two libretro cores writing an identical `state1` filename, which became two server rows only
  because the uploaded name carries the core.
- **bizhawk, 14.** One entry, core-scoped as `{{system}}/bizhawk/sstates/{{core}}`.
- **jgenesis, 7.** One entry, not core-scoped.
- **30 rows that declare no save-state directory at all**, which is `mednafen`, `mesen`, `ares`,
  `snes9x`, `mgba`, `nosgba` and `kega-fusion` across these seven systems. For those, step 5 is
  a recorded declaration rather than a test: the emulator can still be certified on the other
  eight steps, and the record says state sync is outside what RomMBat offers for that row.

  **Declaring no directory does not mean writing no state**, and an earlier revision of this
  section assumed it did. Driven on `nes`: `mednafen`, `mesen` and `ares` each wrote a real save
  state into a directory they name themselves, which `StateScanner` never reads because it works
  from `es_savestates.cfg` alone. So step 5 for these rows is not "there is nothing to sync", it is
  **"there is something to sync and RomMBat cannot see it"**, and the record has to say which.
  Issue #150. That is a third of wave 1 resting on the wrong reading, so re-check it per row rather
  than carrying this bullet forward.

  `saves` says so, which is the reporting half: a directory whose emulator the file does not name
  is reported under `no_state_declaration` rather than folded into the row that promises save
  states sync. **The fix is per system**: `data/retrobat/es_savestates.supplement.xml` declares
  `mednafen`, `mesen` and `ares` on `nes` and `mednafen`, `ares` and `kega-fusion` on
  `megadrive`, each where it was driven, and a row in this family anywhere else needs its own
  supplement entry and battery rule, from its own pass, before steps 4 and 5 can pass. **The
  layout is the emulator's per system, not per emulator**: ares keeps `nes` under `ares/Famicom/`
  and `megadrive` under `ares/Mega Drive/`, so the supplement carries one entry per system.

  **Check the declaration by emulator name, not by save-directory name**, before recording a row
  as declaring none. RetroBat does not spell the two the same way everywhere: Dolphin is declared
  as `dolphin` and writes its save tree to `dolphin-emu/`, and its save states are measured
  working (finding 971). `SaveScanner.DeclaredNames` carries the correspondence for the one row
  where it diverges, so a new row whose directory name is not in `es_savestates.cfg` needs that
  checked before the record says the emulator declares nothing.

**Name how the row was selected, every time.** A row driven under an `es_settings.cfg` override
is not the row a stock install gives a user, and a record that does not distinguish them is
claiming something it did not test. Confirm what actually ran from
`emulationstation/emulatorLauncher.log`, which logs the emulator and core per launch. Never
infer it from `retroarch.cfg`: finding 217 measured that the file describes only the last game
launched and is regenerated per launch.

**`<extension>` is a property of the system, not of the row.** The list in `es_systems.cfg` is a
union across every emulator the system declares, and **RetroBat publishes no per-`(emulator,
core)` extension data anywhere**: `es_features.cfg` mentions "extension" 28 times and every one
is an N64 controller pak. So record which extensions the certified core was **observed** to
launch, and treat the rest as declared and unproven rather than supported. A core refusing a
declared extension is a real result about that row, not a RomMBat defect: RomMBat does not
police formats, and which one a user keeps in RomM is theirs to choose.

### What a wave can be staged before anyone sits down

Steps 1, 2 and 3 need no emulator running, so they can be batched for a whole wave ahead of
time: the mapping layer, the `<extension>` list and the library's multi-file shapes, and
`rommbat-agent bios <system>` for all four BIOS states. That stages the record files with six
of nine steps open, and it means the wave's BIOS gaps are known before a controller is picked
up. Steps 4 through 9 cannot be staged and are the reason the rollout waited for M7.

### What the bundled data already answers, and what it does not

Read before planning a wave, because it decides which steps are formalities and which are the
work. All three counts are against the 51 systems above.

- **Mapping is a formality.** Every one of the 51 has a bundled slug, so step 1 resolves at
  layer 3 unless the install overrides it. `arcade` is the exception that fans out to `fbneo`
  and `mame`, and it is why wave 8 needs the explicit folder choice.
- **`save_shapes.json` covers 22 of the 51.** For the other 29 there is no entry, and
  `SaveScanner` falls back to treating a loose file under `saves/<system>/` as class A. That
  fallback is probably right for the generation 2 to 4 tail and it has never been measured, so
  step 4 in waves 4 through 7 either confirms it or finds the exception. A confirmation is worth
  recording: the file is generated, so a new entry comes from re-running the probe against an
  install that has the system populated, not from hand-editing.
- **BIOS is smaller than the system count suggests.** Sixteen of the 51 require nothing at all.
  Five (`mastersystem`, `vectrex`, `sega32x`, `ngp`, `ngpc`) have a requirement RetroBat names no
  hash for, in whole, so step 3 records that in those words and the pass certifies on the other
  eight. `neogeocd` is the outlier at 12 files, 10 of them hashless.
- **Artwork outweighs the ROMs on a retro platform, by a lot.** Measured across two whole
  platforms with three kinds each and no video: `atari2600` is 296.6 KB of ROM against 28 MB of
  artwork over 53 games, and `atari5200` is 757.5 KB against 46.8 MB over 76. A set whose budget
  was sized from the ROMs fills with ROMs, every game lands with no cover, and no later run
  repairs it. Step 7 checks for art, so a wave's sets need headroom or the step fails for a
  reason that is nothing to do with the platform.
- **Only 13 emulators declare a save-state directory**, so step 5 is bounded by
  `es_savestates.cfg` rather than by what RetroBat can launch. An alternate outside those 13
  (`mednafen`, `ares`, `mesen`, standalone `snes9x`, `kega-fusion`, `xemu`, `raine` and the rest)
  needs a supplement entry of RomMBat's own before step 5 can pass, which is how `mednafen`, `mesen`
  and `ares` were certified on `nes`, and `mednafen` and `ares` on `megadrive`. **Outside what RetroBat declares, not outside what the
  emulator writes**: three of those were driven and all three wrote states anyway, so step 5 records the
  path as well as the absence.

## Seventy-three rows are certified, and the gate is open

The framework had to work end to end on a single platform first, which is M1 through M6, and
every pass then needs a person at the machine launching real games, which is what M7's gamepad
UI is for. Both conditions are met: 7b landed, and a game was launched from EmulationStation
and came back out through the hooks. The waves finish against an M8 package.

**That one launch is not a certified row**, and `ps2` is not certified by it. The unit is
`(system, emulator, core)` and the checklist is nine points; a launch is one of them.

**Every row `nes` declares is certified**, at RomM `5.3.0-beta.1` and RetroBat 8.2.1 and carried
to `5.3.0` and then the `5.3.1` floor, all
three `libretro` cores first. `nestopia`, re-driven on 2026-09-20, was the first row anywhere to pass step 5, a save
state round-tripping with its screenshot, which had been blocked on findings 138, 256 and 258
since the checklist was written. `fceumm` and `mesen` followed on 2026-09-21, and `fceumm` is the
row a stock install gives a user, selected with no override. **`nes` under both `bizhawk` cores,
`NesHawk` and `quickerNES`, followed later on 2026-09-21**, once #151 carried BizHawk's battery
saves, and `jgenesis`, `mesen`, `mednafen` and `ares` last, once each had a battery rule and the
last three a state declaration in the bundled supplement. Steps 1, 2, 3, 4, 5, 7, 8 and 9 pass on
all nine; step 6 is N/A because `nes` has no class D.

**Seven of `megadrive`'s eleven rows are certified**, at RomM `5.3.0` and RetroBat 8.2.1 on
2026-09-21: the three `libretro` cores that boot the library, `genesis_plus_gx` being the stock
row, then `bizhawk`/`Genplus-gx`, `jgenesis`, `mednafen` and `ares`, once each had a megadrive
battery rule and the last two a state declaration. **Four were driven and are not certified**:
`libretro`/`fbneo` boots no game named by No-Intro, since FBNeo takes its driver from the file
name (finding 278), and the three `kega-fusion` rows fail step 4, because Kega Fusion writes its
battery saves outside `saves/` where RetroBat's `Fusion.ini` sends them (finding 283).

**Nine of `gba`'s ten rows are certified**, at RomM `5.3.0` and RetroBat 8.2.1 on 2026-09-22:
`libretro` under `mgba`, the stock row, `gpsp` and `mednafen_gba`, then `mgba` standalone,
`mednafen`, `mesen`, `bizhawk`/`mGBA`, `jgenesis` and `ares`, the last seven once each had a gba
battery rule and four a state declaration. **`nosgba` was driven and is not certified**: it loads a
zipped ROM only through a bare `.gba` beside it, which NO$GBA itself deletes, keeps its saves
outside `saves/`, and writes a state only where a Save As dialog is pointed (finding 286). Three rows refuse to boot without `gba_bios.bin` and seven do not
(finding 285), which the record tables.

**All fourteen of `gb`'s rows are certified**, at RomM `5.3.0` and RetroBat 8.2.1 on 2026-09-22:
the six `libretro` cores, `gambatte` being the stock row, with `mesen-s`, `bsnes`, `tgbdual`,
`DoubleCherryGB` and `sameboy`, then `mesen`, `mgba`, `mednafen`, `ares`, `bizhawk` under `Gambatte`,
`GBHawk` and `SameBoy`, and `jgenesis`, the standalone rows once each had a gb battery rule and four a
state declaration. Twelve rows boot without firmware. `libretro`/`bsnes` needs `SGB1.sfc` and
`bizhawk`/`GBHawk` the Color boot ROM for a Color-flagged cartridge, files RetroBat's list names
under `sgb` and `gbc`, so `bios gb` is supplemented with them (finding 293). A clock cartridge,
Pokemon Silver synced into `gb`, keeps its clock in a `.rtc` under the stock core, which syncs as
`libretro:battery:rtc` and was driven through a restore with the clock intact (finding 298).

**All twelve of `gbc`'s rows are certified**, at RomM `5.3.0` and RetroBat 8.2.1 on 2026-09-23:
the four `libretro` cores, `gambatte` being the stock row, with `tgbdual`, `sameboy` and
`DoubleCherryGB`, then `mesen`, `mgba`, `mednafen`, `ares`, `bizhawk` under `Gambatte`, `GBHawk` and
`SameBoy`, and `jgenesis`, the standalone rows once each had a gbc battery rule and four a state
declaration. Eleven rows boot without firmware, and `bizhawk`/`GBHawk` needs `gbc_bios.bin`, which
is on `gbc`'s own list (finding 299). Pokemon Crystal's clock round-trips on every row, and every change
of row that was checked lost it, because each keeps it in its own format and the loose `.rtc` is
one file name with four (finding 300).

**All fifteen of `snes`'s rows are certified**, at RomM `5.3.0` and RetroBat 8.2.1 on 2026-09-24:
the seven `libretro` cores, `snes9x` being the stock row, with `bsnes-jg`, `bsnes`, `bsnes_hd_beta`,
`mednafen_snes`, `mesen-s` and `snes9x2005`, then `mesen`, `mednafen`, `snes9x` standalone, `ares`,
`bizhawk` under `BSNES`, `Faust` and `Snes9x`, and `jgenesis`. Mesen and Snes9x share `libretro`'s
loose `.srm`; the other standalone rows needed a snes battery rule, and four rows a state declaration.
RetroBat lists no `snes` firmware, and three rows refuse a DSP-1 cartridge without the chip's, which
nothing RomMBat fetches (finding 317).

**Seven of `mastersystem`'s ten rows are certified**, at RomM `5.3.0` and RetroBat 8.2.1 on
2026-09-24: `libretro` under `genesis_plus_gx`, the stock row, and `picodrive`, then `mesen`,
`mednafen`, `ares`, `bizhawk`/`SMSHawk` and `jgenesis`, each standalone row with a `mastersystem`
battery rule and three with a state declaration. `libretro`/`fbneo` and both `kega-fusion` rows are
driven and not certified, for `megadrive`'s reasons (findings 325 and 283). `SMSHawk` refuses to
start without the US/EU BIOS, which RetroBat lists without a hash, so RomMBat cannot fetch it
(finding 322).

**All seventy-three are carried to the RomM `5.3.1` floor.** Steps 1 and 9 were re-run there on
2026-09-24, and the other seven carry because 5.3.1 changes no route they exercise; each record's
"The move to `5.3.1`" maps the steps.

**Wave 2 has begun with `psx`: all seven of its rows certified at RomM `5.3.1`** on 2026-09-26, the three `libretro` cores, DuckStation, standalone mednafen and both BizHawk cores, with every memory card type the rows expose driven at step 6. Each non-`libretro` row needed code: a battery rule for DuckStation's per-port cards, mednafen's cards and BizHawk's `.SaveRAM`, a state declaration for mednafen, and attribution of a disc set's files to the set; the non-default card types added swanstation's and mednafen_psx_hw's other cards and eleven declared shared cards, five of them observed (findings 328 to 335). Six of the seven refuse to boot without `psxonpsp660.bin`, which RomMBat fetches; mednafen emulates no card at RetroBat's default (330). [psx.md](psx.md) is the record.

**Read all eight as narrowly as they are written.** They certify eighty
`(system, emulator, core)` rows on one install, wave 1's seventy-three and `psx`'s seven, each at the
floors its record names. They certify none of those
emulators on any other system: every rule and declaration the non-`libretro` rows needed is scoped
to the systems it was measured on. [nes.md](nes.md), [megadrive.md](megadrive.md),
[gba.md](gba.md), [gb.md](gb.md), [gbc.md](gbc.md), [snes.md](snes.md), [mastersystem.md](mastersystem.md) and
[psx.md](psx.md) are the records, gaps included.

**One thing does not wait.** Steps 4, 5 and 6 are the data-loss steps, and M6 ships them across
three stages. Each stage owes one hands-on pass of the shape it added: one game, one emulator,
one real save or state, through EmulationStation and back. That is not a certification and must
not be filed as one, but "the tests pass" and "an emulator wrote this and RomMBat handled it"
are different claims, and only the second is evidence.

| M6 stage | The one shape to exercise by hand                                     | Done               |
| -------- | --------------------------------------------------------------------- | ------------------ |
| 2a       | A save state, across more than one emulator for one game              | **Yes**, see below |
| 2b       | A PPSSPP `SAVEDATA/` directory, and MAME `nvram/` if convenient       | **Yes**, see below |
| 2c       | A PS2 battery save after opting that game into a per-game memory card | **Yes**, see below |

**2c, done on `ps2` / Armored Core 3 (USA), PCSX2.** Not a certification: one game, one
system, steps 4, 6 and 9 only. Results are findings 182 to 188 in
`docs/retrobat-findings.md`.

| Step                                       | Result                                                                                  |
| ------------------------------------------ | --------------------------------------------------------------------------------------- |
| The shared card, before anything           | `Mcd001.ps2` holding **11 distinct games**, Armored Core 3 among them as `BASLUS-20435` |
| Converted                                  | `ps2["Armored Core 3 (USA).chd"].pcsx2_slot1_memory = game`, prior state `absent`       |
| Refused while EmulationStation was running | **yes**, exit 2, nothing written to the file                                            |
| PCSX2 wrote                                | `saves/ps2/pcsx2/memcards/Armored Core 3 (USA).ps2`, **one game's saves**, 4 entries    |
| The shared card afterwards                 | **untouched**, mtime and md5 both unchanged                                             |
| Discovered and attributed                  | class D, slot `pcsx2:battery`, by the stem through the existing `RomIndex`              |
| Uploaded                                   | save 179, server hash equal to the local one                                            |
| **The game loaded the restored save**      | **yes**                                                                                 |
| Eviction with an unsent card               | **refused**, and reported "still short" rather than claiming success                    |
| Eviction after a flush                     | offered, so the guard does not block spuriously                                         |
| Reverted                                   | key removed, 57 settings before and after, nothing else disturbed                       |
| Re-sync and re-flush                       | clean no-op, 0 downloaded, 0 written, gamelists unchanged                               |

**Three things this pass did not prove, and they are not small.**

- **The download side of class D is untested anywhere.** The card went up and came back only as
  far as "the server holds it". Nothing has ever written a class D save onto a device from the
  server, and the bundled-slot refusal in `SaveSync.DownloadAsync` branches on class C, so what
  a class D download does is unexercised rather than decided.
- **Only `(ps2, pcsx2)` was driven.** `dreamcast` and `psx` are refused by declaration with
  their measured reasons and neither refusal was exercised against a real emulator, and
  `folder`, PCSX2's third choice, is declared and unmeasured.
- **The ROM was adopted, then re-downloaded, and neither is the ordinary case for a converted
  game.** The first attempt failed verification against a stale server hash (finding 180),
  which is fixed on the instance now but shaped how this pass ran.

**2b, done on `psp` / Bust-A-Move - Deluxe (USA), PPSSPP.** Not a certification: one game, one
system, steps 4 and 9 only. Results are findings 154 to 159 in `docs/retrobat-findings.md`.

| Step                                  | Result                                                                      |
| ------------------------------------- | --------------------------------------------------------------------------- |
| PPSSPP wrote a save                   | `SAVEDATA/ULUS100570000/`, 4 files, 91,607 B, from the game's own save menu |
| The grammar scoped it                 | container `saves/psp/SAVEDATA`, key `ULUS10057` read as a prefix            |
| Attribution                           | route 1, the launch window, with no header and no sidecar available         |
| Upload                                | one archive, 87,559 B, returned as `ULUS10057 [2026-08-18_02-30-06].zip`    |
| Re-sync with no changes               | `no_op`, slot reports in step                                               |
| Both sides diverged                   | reported as a conflict, all four files copied aside                         |
| `saves resolve --keep-server`         | staged restore, 4 files swapped in                                          |
| **The game loaded the restored save** | **yes**                                                                     |

**Four defects came out of the pass**, none reachable from a test: a cached refusal that was
only an absence, a 409 reported as a failure rather than a conflict, a conflict that recorded no
copy aside, and a resolver whose verification could never pass for an archive. All four are
fixed and covered.

**Still not done for 2b.** MAME's short-name join is structurally sound and undemonstrated: the
measured install holds 1,231 nvram directories against 3 mame ROMs, so nothing joins. Wii ships
its grammar on tree structure alone with no game ever launched. And the conflict's server side
was synthetic, so the emulator-loads-it result rests on that plus the fold rather than on one
untouched round trip.

**2a, done on `mastersystem` / Phantasy Star (Brazil), four emulators.** Not a certification:
one game, one system, steps 4 and 5 only. Results are findings 134 to 139 in
`docs/retrobat-findings.md`.

| Emulator                     | On disk                                      | Slot                         | Landed |
| ---------------------------- | -------------------------------------------- | ---------------------------- | ------ |
| `libretro`/`genesis_plus_gx` | `libretro.genesis_plus_gx/….state1`          | `libretro:genesis_plus_gx:1` | yes    |
| `libretro`/`picodrive`       | `libretro.picodrive/….state1`                | `libretro:picodrive:1`       | yes    |
| `bizhawk`/`SMSHawk`          | `bizhawk/sstates/SMSHawk/….QuickSave2.State` | `bizhawk:SMSHawk:2`          | yes    |
| `jgenesis`                   | `jgenesis/states/…_0.jst`                    | `jgenesis::0`                | yes    |

The two libretro cores wrote the **identical** filename and became two server rows, which is
the collision the scoped upload name exists to prevent, proven rather than argued. What did
**not** work is the screenshot: uploaded, stored against the ROM, and not linked to the state.
See finding 138.
