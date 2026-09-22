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

**All nine `nes` rows are through, and the first is the model for the rest.** `nes` under
`libretro`/`nestopia` on 2026-09-20, then the other two `libretro` cores, both `bizhawk` cores,
`jgenesis`, `mesen`, `mednafen` and `ares` on 2026-09-21, at RomM `5.3.0-beta.1` and RetroBat
8.2.1, all nine steps with step 6 N/A, and carried to the `5.3.0` floor with step 9 re-run. The two later `libretro` passes took under an hour between
them, which is what a row costs once steps 1, 2 and 3 carry. The last four needed code first: a
battery rule each and, for three, a state declaration in RomMBat's bundled supplement, which is
what a row outside `es_savestates.cfg` will need on every other system too. Read
`docs/platforms/nes.md` before starting a pass: it is the only worked example of the whole
checklist, and it carries the two traps that cost the most time, the screenshot byte check at
step 5 and the RomM-side device id at step 8. That pass is what opened #208, and `status` now
reads the sessions back, so step 8 no longer needs the token that record describes.

**`megadrive` is second: seven of its eleven rows certified at `5.3.0` on 2026-09-21**, and
`docs/platforms/megadrive.md` is the model for a system whose matrix does not all pass. The three
`libretro` cores that boot the library, `bizhawk`, `jgenesis`, `mednafen` and `ares`, the last four
on a build carrying megadrive rules. `libretro`/`fbneo` and the three `kega-fusion` rows were
driven and are recorded as not certifiable, each with its reason, which is a result and not a
gap. The whole system took one evening, eleven ES sessions and the agent's keyboard launches.

**`gba` is third: nine of its ten rows certified at `5.3.0` on 2026-09-22**, in one morning,
because **each row after the first was seeded with the save the one before made** rather than
played through the intro again: copy the save to where the next emulator looks, launch, save in
the game, so the file measured is still that emulator's. A seed an emulator refuses is a finding,
not a failed pass: mednafen refused mGBA's 131,088 B file (finding 289). **Put an override's
`gba.emulator` in `es_settings.cfg` only with ES closed**, and restore the file from a copy taken
first. **A clock file is a second save file**, class B, and changes on every launch (finding 291).
**An emulator can delete what you place in `roms/`**: NO$GBA took a bare `.gba` put beside its zip
for its own unzip output and deleted it (finding 286), so check a hand-placed file is still there
before each launch. Blame `emulatorLauncher` only once the emulator run by hand keeps the file.

**Three things `megadrive` taught that transfer.** An emulator lays out its tree per system, not per
emulator: `jgenesis` and `ares` name their save directory after their own name for the console
(`jgenesis/md`, `ares/Mega Drive`), so a rule measured on `nes` says nothing about the next
system's path. An emulator can write outside `saves/`: Kega Fusion's battery saves go where
RetroBat's `Fusion.ini` sends them, `emulators/kega-fusion/`, and that is a RetroBat defect to
report rather than a tree to start scanning (finding 283). And a core can refuse the library for
its names: FBNeo takes a console game's driver from the file name, so it boots nothing named by
No-Intro (finding 278). **An emulator absent from `emulators/`** is installed by ES on the first
launch under it, so check the folder holds an executable before planning its rows.

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
2. `<extension>` list captured from the live `es_systems.cfg`, and **every ROM the set resolves
   survives the extension check**. Nothing is excluded that the user asked for.

   **The step used to ask for the opposite and it was testing the wrong direction.** It required
   a known-unsupported file to be excluded and reported. Wrongly downloading one costs bytes and
   a game that does not appear, because EmulationStation filters by `<extension>` itself; wrongly
   **excluding** one silently drops a game the user asked for, with no error and no line in the
   report worth questioning. `<extension>` is a per-system union across every emulator the system
   declares (`nes` lists `.wad`, which is not a NES container at all), so the filter cannot be
   precise per `(emulator, core)` and over-rejection is the likelier error of the two.

   So record the list, record that the resolve kept everything, and do not manufacture a file to
   reject. A platform whose library is one format throughout passes this step rather than being
   held open by it.

   **Where the exclusion half still earns a look is a library with mixed formats arising
   naturally**, which is wave 2: `psx` and the CD systems carry `.chd`, `.cue`, `.bin` and `.m3u`
   in one set, and over-filtering there drops real games. That is also where multi-disc and
   multi-file placement has to be settled, which is the concern this step is a poor proxy for.

3. The BIOS RetroBat lists, resolved against RomM **by md5**; what RomM lacks listed with
   expected filename and hash. Run `rommbat-agent bios <system>` for the report and
   `bios <system> --apply` to fetch, and record all four states rather than a pass or fail:
   present, fetched, not in the library, and the ones RetroBat names no hash for. A system whose
   whole list is hashless (28 of the 99 are) is certified on the other eight steps, and step 3
   says so in those words.

   **A missing file never fails this step or holds a platform back.** RetroBat's list is what
   RomMBat fetches, not what a platform needs to run: `batocera-systems.json` has no optional
   flag, it keys firmware on the system, and the emulator decides which files it reads. RomMBat
   fetches every entry RomM holds, reports the rest, and leaves the verdict to the emulator. On
   `gba` three of ten rows refuse to boot without `gba_bios.bin` and seven HLE it (finding
   285). So boot each row once with the file moved out of the tree, record which refuse, then
   put it back with `bios <system> --apply`, which doubles as the fetch this step asks for. A
   row that refuses without a file is certified with it, and the record names the file.

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

   **Drive a state made after finding 258's fix, never one uploaded before it.** RomM links a
   screenshot by filename, and RomMBat named it so that no libretro-shaped state ever linked,
   while its restore could not place a state for any emulator that keeps the slot in the stem.
   Between them no row could pass step 5 until 2026-09-20, and both were RomMBat's
   (`docs/retrobat-findings.md` findings 138, 256 and 258). An unchanged state is never re-sent,
   so an older one stays unlinked. A null link on a fresh state is a new finding, not a
   recurrence of an old one.

   **All nine `nes` rows have passed it**, and the method is worth copying. Make the state in a real session, delete it and its `.png` from the tree, and
   run `saves restore <rom id>` and then `--apply`: the preview names the screenshot it would
   bring back, and the apply is what proves the whole path. **Compare the returned image's bytes,
   not its name or its arrival**, because RomM can answer a libretro slot with another slot's
   image, which is not a link to this state. Make more than one state in the session and pick
   the one whose image is unique for the comparison, because two states on the same frame share an
   image and cannot tell a real link from a wrong one. `docs/platforms/nes.md` has the worked
   pass.

   **Under `libretro` the state slot is RetroArch's, not EmulationStation's.** RetroArch runs with
   `savestate_auto_index` on and continues from the highest slot already in the core's state
   directory, so `-state_slot <n>` on the `emulatorLauncher.log` line does not decide the suffix:
   `mesen` was launched with `-state_slot 5` and wrote slots 1 and 2 (finding 261). Read the slot
   from the `Saving state` lines in `es_launch_stdout.log`, or from the file on disk.

   **Under `bizhawk` it is the other way round, and the screenshot is inside the state.** ES's
   `-state_slot` becomes EmuHawk's current slot and the pad's save key always writes there, so a
   second slot needs a keyboard: `Ctrl+F1` to `Ctrl+F10` save to a slot outright (finding 269).
   Those keys do not cross RDP. From a session on the RetroBat machine, start `emulatorLauncher`
   with the row's `-system`, `-emulator`, `-core` and `-rom` and send the key with `keybd_event`
   and its hardware scan code, because EmuHawk reads DirectInput and ignores `SendKeys`. Never
   start EmuHawk directly: `emulatorLauncher` does the mirror into `saves/`, and a state made
   without it is removed at the next launch (finding 270). BizHawk writes no `.png` at all, whatever
   `es_savestates.cfg` declares. The frame is `Framebuffer.bmp` inside the `.State` zip, so open it
   to check the two slots differ, and an md5-equal restore of the state carries it (finding 268).

   **Check the game's `<emulator>` in `gamelist.xml` before driving a row on it.** A per-game pin
   overrides `<system>.emulator` and leaves no trace in `es_settings.cfg`, and on the `nes` install
   eight games are pinned to other rows from the first pass. Pick an unpinned game whose battery
   save is quick to make: Zelda writes one the moment a name is registered.

6. Where class D applies, the per-game memory card option is verified via `es_settings.cfg`.
7. A game launches from EmulationStation after sync, with art and metadata present.

   **Give the set a budget with real headroom, or step 7 fails for a reason that is not the
   platform's.** Artwork is not a rounding error on retro systems: measured across two whole
   platforms with three kinds each and no video at all, `atari2600`'s 53 games are 296.6 KB of
   ROM against 28 MB of artwork, and `atari5200`'s 76 games are 757.5 KB against 46.8 MB. That
   is 94x and 62x. A budget sized from the ROMs is filled by the ROMs, every game lands with no
   cover, and **no later run repairs it, because nothing frees space by itself**. Media is fetched
   per game so a budget that runs out truncates the tail rather than stripping the artwork off
   everything, which is #102, but interleaving concentrates the same bytes and does not conjure
   more. A step 7 failure on a tight budget is a budget setting and must not be recorded as a
   platform result.

8. A play session is recorded and reaches RomM.

   **`rommbat-agent status` settles this step**, under its `Playtime` block: with the server
   reachable it reads `GET /api/play-sessions` back for this device and prints the count, the
   last session's start, end and length, and the ten newest under `recent:` (#208), so a run of
   rows played back to back can each be matched to its launch. A token stored with `--protect` needs
   `--passphrase` on that run, or the block says it could not read.

   **Both of the ways this step used to be answered by hand have a trap, and they are why the
   block says what it says.** `GET /api/play-sessions` is scoped to the authenticated user, so
   another account's token answers `200` with zero rows, and `?device_id=` given the local
   `client_device_identifier` rather than the id `status` prints on the `romm device` line does
   the same. Both read exactly like a session that was never written, which is why an empty
   answer settles nothing either way. `DEVELOPER_SETUP.md` covers the read-only token, which is
   still the route where the paired token cannot be used.

9. **Re-sync is a clean no-op**: zero uploads, zero downloads, no gamelist churn. This is
   the strongest single signal that slots, cursors and mapping are all correct.

## When the floor moves

**A record is a measurement of the RomM and RetroBat builds it names, and a floor move does not
carry it forward by itself.** Nor does it void it. The PR that moves a floor owes every record in
`docs/platforms/` a mapping of the move onto the nine steps, and the re-run of the steps it touches:

- **A step is touched** when the move changes code or bundled data that step exercises (the
  diff since the previous floor under `src/` and `data/`), or when the findings doc for the
  adoption records an upstream change on its path. A changelog line nobody measured counts as
  touching, because the point is to find out.
- **Step 9 is always touched.** It is the cheapest step and the one that catches a change nobody
  mapped: new fields on a row show up as gamelist churn or a re-download.
- **Every other step says why it carries over**, in one line naming what did not change. "Not
  obviously broken" is not a reason.
- **A touched step not yet re-run is owed, not passed.** The record says so against the new
  floor, and the row is not certified there until it passes, whatever it held on the build it
  was measured on. Leave the original result and its version in place beside the owed line.

**A scripted replay counts as a re-run, for a row already certified by hand.** A harness that
launches the row through `emulatorLauncher` on the real install, drives its states and reads back
what the emulator wrote is evidence, not a test suite standing in for it, so its pass clears a
touched step. It never clears a row's first certification, a new emulator or core, step 6, or a
multi-disc set; those stay hands-on. The harness, the row fingerprint that decides which rows it
runs, and the fixtures kept from each pass are designed in `docs/PLAN.md`, "Keeping
certifications current", and tracked in #216. Until they exist, a touched step is re-run by hand.

This is the middle of three options #187 weighed. Re-running all nine on every move grows with
the wave rollout for steps nothing changed, and never re-running leaves a record attesting to a
server the client refuses at startup.

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
slot, and only `bizhawk` takes ES's `-state_slot` as that slot (finding 269). `jgenesis`, `mesen`,
`mednafen` and `ares` all step the slot on `F7` and save on `F2` with no modifier, which the agent
can send locally through `emulatorLauncher` when RDP eats them (finding 275). The keys are in
`es_padtokey.cfg` or the emulator's own config (`mednafen.cfg`, Mesen's `settings.json`). Kega
Fusion saves on `F5` and steps the slot down on `F7`, has no pad-to-key file, and needs its
controls remapped in its own menu before the pad plays (finding 284). **When a key's effect cannot be seen,
take a screenshot of the screen from the agent's session** rather than sending keys blind: a
blind Start on a title screen is as likely to land during a fade as on the menu.

The libretro family is the one that most needs driving rather than assumed: finding 134 measured
two cores writing an identical `state1` filename, which survived as two server rows only because
the uploaded name carries the core.

**Name how the row was selected, every time**, and confirm what ran from
`emulationstation/emulatorLauncher.log` rather than from configuration. A row driven under an
`es_settings.cfg` override is not the row a stock install gives a user. Never read
`retroarch.cfg` for this: finding 217 measured that it describes only the last game launched.

**`<extension>` belongs to the system, not the row.** It is a union across every emulator, and
RetroBat publishes no per-`(emulator, core)` extension data anywhere, so record what the
certified core was **observed** to launch and treat the rest as declared and unproven.

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
