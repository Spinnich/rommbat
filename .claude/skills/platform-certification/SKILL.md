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
the [wave table](waves.md#wave-order) rather than one.

## Map

This file holds the unit, when to certify, the checklist and what a floor move owes. The rest is
in topic files beside it:

| Section                                                                                  | File                   |
| ---------------------------------------------------------------------------------------- | ---------------------- |
| [When to run this](passes.md#when-to-run-this): what each certified system's pass taught | [passes.md](passes.md) |
| [Wave order](waves.md#wave-order): the waves, the four state families and the slot keys  | [waves.md](waves.md)   |

## When to run this

**The gate is open.** Every pass needs a person launching real games, through the gamepad UI
rather than a terminal, which would make a long job longer. A game has been driven from
EmulationStation and back through the hooks. The waves finish against an M8 package, which is what a user installs.
That launch certified nothing: it is one of nine points on one row.

**Steps 4, 5 and 6 do not wait**, because they are the ones where being wrong destroys data
rather than costing a re-download. A change to save logic owes a hands-on pass of the shape it
touches, driven through EmulationStation and back on every emulator the system offers and every
save option that writes it (`pre-pr-verification`). That is not a certification and must not be recorded as one, but "the tests pass" and
"an emulator wrote this and RomMBat handled it" are different claims and only the second one
is evidence.

## Checklist

Record results in `docs/platforms/<system>/`: `index.md` holds the system's own steps and each
row's standing at the floor, and a file per emulator or group of rows holds the rest. Each row's
status also goes in `data/certification.json`, every row `es_systems.cfg` declares for the system
(`untested`, with no floor, for one not driven; a note on one not certified), and the guide's platform table is
regenerated from it (`wiki/README.md`). A system's first certified row also owes its guide page,
`wiki/platforms/<system>.md` from `_template.md`, for a player; a later row corrects it. All nine,
or it is not certified. Steps 1, 2, 3, 7, 8 and 9 are largely per system and can be carried
across emulators with a note (step 2 not where emulators disagree about a playlist); **steps 4, 5 and 6 have to be redone per emulator.**

1. Folder mapping resolves, and the resolution layer is recorded.
2. **Multi-disc and multi-file games land correctly.** Record which shapes the library holds
   for this system and, for each, where it lands and that it launches from ES. The known shapes:
   several `.chd` plus an `.m3u`; several `.bin`/`.cue` sets, perhaps with an `.m3u`; update and
   DLC files that belong in other folders; and RomM's subfolder structure inside a game folder,
   most of which is ignored. Which parts are needed is the finding, not an assumption. Until a
   shape is settled here it stays excluded (`excluded_multi_file`, `excluded_folder`), and
   settling it is what unlocks it for this platform. Read both states: `excluded_folder` holds
   every folder-held ROM RomM does not flag multi-file, including a folder of several files, so
   a game with its update files can sit there rather than under `excluded_multi_file`. A system whose library is single files
   throughout passes this step by recording so.

   **Unlocking a system is one entry in `data/retrobat/multi_file.json`, made from this step's
   measurement.** `psx` was first (`docs/platforms/psx/`): drive every row on a set held as one
   RomM rom, confirm what ES lists for the layout with `/systems/<system>/games` and a screenshot,
   and record which rows read the playlist. A row that cannot is recorded, not waited on: RetroBat's
   launcher hands both BizHawk `psx` cores disc 1 whatever the layout (RB-314). Test on a set
   held as **one rom per release**, since that is the layout RomMBat designs for; a library holding
   each disc as its own rom has nothing multi-file to unlock, and regrouping one set in RomM is the
   fix, as it was for `psx`.

   **The extension gates nothing, so this step is not an extension check.**
   `<extension>` is a per-system union across every emulator the system declares (`nes` lists
   `.wad`, which is not a NES container at all; one `psx` emulator reads `.chd` and another does
   not), so it cannot say what a given `(emulator, core)` opens. RomMBat syncs every member and
   reports the ones the list omits as unlisted in ES. Capture the list in the record, note any
   unlisted count the resolve reports, and do not manufacture a file to test either direction.

   **`.m3u` support is per emulator**, so this step can differ between rows of one system: PCSX2
   cannot use a playlist although `ps2` lists `.m3u`. Where it does, record it per emulator.

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

   **Boot every row with the whole family's firmware out, not just the system's list.** A row can
   read a file RetroBat lists under a sibling system: on `gb`, `bsnes` needs `sgb`'s `SGB1.sfc`
   and `GBHawk` needs `gbc`'s boot ROM for a Color-flagged cartridge, and neither was on `gb`'s
   list (RB-293). Such a file goes into `tools/build-bios-manifest.py`'s supplement for the
   system, copied from the sibling's entry, so `bios <system>` fetches it.

   **Expect the list to cover the default emulator only.** By the RetroBat team's account, relayed
   by the maintainer on 2026-09-24, that is by design. So a row RetroBat does not run by default
   can need a file no list names, and the supplement has nothing to copy. On `snes` the default,
   `libretro`/`snes9x`, runs a DSP-1 cartridge without firmware and the list is empty, while
   `mesen-s`, Mesen and jgenesis refuse one without `dsp1b.rom` (RB-317). Record where each
   such row reads the file, since it need not be `bios\`: Mesen reads its own `Firmware\` folder
   and jgenesis a config key RetroBat never sets.

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

   **Drive a state made after RB-258's fix, never one uploaded before it.** RomM links a
   screenshot by filename, and a state uploaded before the fix carries a screenshot name RomM's
   lookup misses (RB-258). An unchanged state is never re-sent,
   so an older one stays unlinked. A null link on a fresh state is a new finding, not a
   recurrence of an old one.

   **All nine `nes` rows have passed it**, and the method is worth copying. Make the state in a real session, delete it and its `.png` from the tree, and
   run `saves restore <rom id>` and then `--apply`: the preview names the screenshot it would
   bring back, and the apply is what proves the whole path. **Delete, never move aside and put
   back.** Even a preview scans the tree before it finds anything, so a file moved out for one
   loses its `local_state` or `local_save` row, and moving it back does not restore the row. A
   state then re-uploads (states upsert, so no duplicate); a slotted save loses its baseline and
   can come back as a conflict. The `--apply` writes the row back. **Compare the returned image's bytes,
   not its name or its arrival**, because RomM can answer a libretro slot with another slot's
   image, which is not a link to this state. Make more than one state in the session and pick
   the one whose image is unique for the comparison, because two states on the same frame share an
   image and cannot tell a real link from a wrong one. `docs/platforms/nes/` has the worked
   pass.

   **Under `libretro` the state slot is RetroArch's, not EmulationStation's.** RetroArch runs with
   `savestate_auto_index` on and continues from the highest slot already in the core's state
   directory, so `-state_slot <n>` on the `emulatorLauncher.log` line does not decide the suffix:
   `mesen` was launched with `-state_slot 5` and wrote slots 1 and 2 (RB-261). Read the slot
   from the `Saving state` lines in `es_launch_stdout.log`, or from the file on disk.

   **Under `bizhawk` it is the other way round, and the screenshot is inside the state.** ES's
   `-state_slot` becomes EmuHawk's current slot and the pad's save key always writes there, so a
   second slot needs a keyboard: `Ctrl+F1` to `Ctrl+F10` save to a slot outright (RB-269).
   Those keys do not cross RDP. From a session on the RetroBat machine, start `emulatorLauncher`
   with the row's `-system`, `-emulator`, `-core` and `-rom` and send the key with `keybd_event`
   and its hardware scan code, because EmuHawk reads DirectInput and ignores `SendKeys`. Never
   start EmuHawk directly: `emulatorLauncher` does the mirror into `saves/`, and a state made
   without it is removed at the next launch (RB-270). BizHawk writes no `.png` at all, whatever
   `es_savestates.cfg` declares. The frame is `Framebuffer.bmp` inside the `.State` zip, so open it
   to check the two slots differ, and an md5-equal restore of the state carries it (RB-268).

   **Check the game's `<emulator>` in `gamelist.xml` before driving a row on it.** A per-game pin
   overrides `<system>.emulator` and leaves no trace in `es_settings.cfg`, and on the `nes` install
   eight games are pinned to other rows. Pick an unpinned game whose battery
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
   rows played back to back can each be matched to its launch; `--all-sessions` lists the whole
   window of up to 50, which a system of more rows than ten needs. A token stored with `--protect` needs
   `--passphrase` on that run, or the block says it could not read.

   **Both of the ways to answer this step by hand have a trap, and they are why the block says
   what it says.** `GET /api/play-sessions` is scoped to the authenticated user, so
   another account's token answers `200` with zero rows, and `?device_id=` given the local
   `client_device_identifier` rather than the id `status` prints on the `romm device` line does
   the same. Both read exactly like a session that was never written, which is why an empty
   answer settles nothing either way. `docs/contributing/testing.md` covers the owner token, which
   is the route where the paired token cannot be used.

9. **Re-sync is a clean no-op**: zero uploads, zero downloads, no gamelist churn. This is
   the strongest single signal that slots, cursors and mapping are all correct.

## When the floor moves

**A record is a measurement of the RomM and RetroBat builds it names, and a floor move does not
carry it forward by itself.** Nor does it void it. The PR that moves a floor owes a mapping of the
move onto the nine steps, in its description, and the re-run of the steps it touches. Each record's
"Where each row stands", in its `index.md`, then says which steps were re-run at the new floor and
when, which carry, and which are owed. The mapping itself stays in the PR:

- **A step is touched** when the move changes code or bundled data that step exercises (the
  diff since the previous floor under `src/` and `data/`), or when a fact in `docs/upstream/`
  records the adoption's upstream change on its path. A changelog line nobody measured counts as
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
runs, and the fixtures kept from each pass are designed in
[rollout.md](../../../docs/design/rollout.md#keeping-certifications-current) and tracked in #216. Until they exist, a touched step is re-run by hand.

This is the middle of three options #187 weighed. Re-running all nine on every move grows with
the wave rollout for steps nothing changed, and never re-running leaves a record attesting to a
server the client refuses at startup.
