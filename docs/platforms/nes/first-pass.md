---
summary: The certification record for `nes`: the eight rows after `nestopia` as first driven, on steps 4 and 5 only.
read-when: When a result for one of these `nes` rows is needed, or before re-driving one.
---

# nes: The other eight rows

`nes` declares **nine** `(emulator, core)` rows, and `libretro`/`nestopia`,
in [libretro-nestopia.md](libretro-nestopia.md), is one of them. All nine have
now been driven by hand on this install: a real player battery save and a save state in each,
launched from EmulationStation, with the emulator confirmed from `emulatorLauncher.log` rather
than from configuration.

**This did not certify them.** Steps 4 and 5 are driven for all nine and steps 1, 3, 7, 8 and 9
carry across from that row. Six of the nine could not sync what they wrote. All eight are
certified on passes of their own, in [libretro.md](libretro.md), [bizhawk.md](bizhawk.md) and
[standalone.md](standalone.md); where a sentence below says a row cannot sync something, those
files hold the current result.

## How each row was selected

EmulationStation keeps a per-game emulator choice in **`gamelist.xml`**, as `<emulator>` and
`<core>` children of the `<game>` element, and not in `es_settings.cfg`. That was established by
setting one game through ES's own menu and diffing: `es_settings.cfg` came back byte-identical.

**Fourteen `nes["<rom>.zip"].emulator` keys written into `es_settings.cfg` were ignored.** They
survived ES's startup and exit rewrites untouched and were never read: five launches made under
them all ran the system-level `nes.emulator` / `nes.core` pair, `libretro` / `nestopia`. The
`<system>["<rom>"].<key>` chain is `emulatorlauncher`'s, and it covers feature keys; which emulator
runs is resolved by ES before `emulatorlauncher` exists. The skill now says so.

**Two rows declare no core and inherited `-core nestopia` from the system key.** `mesen` standalone
and `jgenesis` were both launched that way and both ignored it, running `Mesen.exe` and
`jgenesis-cli.exe`. For an emulator that declares no core, `-core` in the log is noise.

**`mesen` standalone is not the `mesen` libretro core**, and the two are separate rows that share a
name. They were driven on different games and wrote to different places, which is the clearest way
to see that they are not one thing.

## Where each row actually stores a save

Measured, not declared. Every battery save below was checked for content rather than existence:
the smallest is 499 non-zero bytes over 52 distinct values, where a file a core writes at boot is
uniform.

| Row                    | Battery save                               | Save state                                                    |
| ---------------------- | ------------------------------------------ | ------------------------------------------------------------- |
| `libretro`/`fceumm`    | `saves/nes/<rom>.srm`                      | `saves/nes/libretro.fceumm/<rom>.state1` + `.png`             |
| `libretro`/`mesen`     | `saves/nes/<rom>.srm`                      | `saves/nes/libretro.mesen/<rom>.state1` + `.png`              |
| `libretro`/`nestopia`  | `saves/nes/<rom>.srm`                      | `saves/nes/libretro.nestopia/<rom>.state1` + `.png`           |
| `mednafen`/`nes`       | `saves/nes/<rom>.<md5>.sav`                | `saves/nes/mednafen/sstates/<rom>.<md5>.mc0`                  |
| `mesen` standalone     | `saves/nes/<rom>.sav`                      | `saves/nes/mesen/SaveStates/<rom>_1.mss`                      |
| `ares`/`Famicom`       | `saves/nes/ares/Famicom/<rom>.ram`         | `saves/nes/ares/Famicom/<rom>.bs1`                            |
| `bizhawk`/`NesHawk`    | `saves/nes/bizhawk/<display name>.SaveRAM` | `saves/nes/bizhawk/sstates/NesHawk/<rom>.QuickSave0.State`    |
| `bizhawk`/`quickerNES` | `saves/nes/bizhawk/<display name>.SaveRAM` | `saves/nes/bizhawk/sstates/quickerNES/<rom>.QuickSave0.State` |
| `jgenesis`             | `saves/nes/jgenesis/nes/<rom>.sav`         | `saves/nes/jgenesis/states/<rom>_0.jst`                       |

`<md5>` is of the ROM: Final Fantasy came out as `24ae5edf8375162f91a6846d3202e3d6`, which is the
`.nes` less its 16-byte iNES header, and mednafen adds it only when `<rom>.sav` does not already
exist (RB-273 and RB-274).

**The screenshot half of that column was recorded for the three `libretro` rows only, and the gap
is not cosmetic here.** `es_savestates.cfg` declares an `<image>` for `bizhawk`
(`{{romfilename}}.QuickSave{{slot0}}.png`) and for `jgenesis` (`{{romfilename}}_{{slot0}}.png`), so
whether those five rows wrote one is a measurable fact this pass did not capture. The other three
declare no entry at all, so there is no `<image>` template to check them against and anything they
wrote would be in their own tree, unread for the same reason their states are. Which rows write one
decides how wide the remaining gap is, and it needs another hands-on pass. It is no longer _this
platform's_ open gap, because the three `libretro` rows closed it on 2026-09-20 and 2026-09-21; it
is what the other six rows owe. **Since measured on all six**: BizHawk writes the frame inside the
state and no `.png` (RB-268), and `jgenesis`, `mesen`, `mednafen` and `ares` write no image at
all (RB-275).

**So `nes` is class A on `libretro` and on nothing else.** `save_shapes.json` gives the system one
entry, `class A`, `provenance: observed`, evidence `loose .srm per rom, libretro`, and the evidence
string was already telling the truth: six of the nine rows write something else entirely, in four
different shapes. The file's own `_note` says shape is a property of `(system, emulator)`; this is
that note measured on one platform across every emulator it declares, and it is the first time the
claim has been checked rather than repeated.

**Only the three libretro rows share one battery file.** All three write
`saves/nes/<rom>.srm`, so switching core continues the same save: Kirby's Adventure was played
under `nestopia`, then under `fceumm`, and the second session carried on from the first and
rewrote the same 8,192 bytes. Across emulator families they are separate files in incompatible
formats, so the same game under `mesen` standalone and under `libretro` holds two unrelated saves
and neither sees the other. **`mesen` standalone and `mednafen` are the exception**, found later:
mednafen reads a plain `<rom>.sav` whenever one exists, so the two share it (RB-273).

## What RomMBat does with them

| Row                   | Battery save      | Save state          |
| --------------------- | ----------------- | ------------------- |
| the three `libretro`  | synced, class A   | synced, core-scoped |
| `bizhawk`, both cores | synced, certified | synced, certified   |
| `jgenesis`            | synced, certified | synced, certified   |
| `ares`                | synced, certified | synced, certified   |
| `mednafen`            | synced, certified | synced, certified   |
| `mesen` standalone    | synced, certified | synced, certified   |

**The table is as it stands now, and what follows is how it stood after the first pass**, when the
last four rows read "deferred", "no shape claims it" and "invisible". Each of those is now carried
by a battery rule in `save_rules.json`, and the three "invisible" states by the bundled
`es_savestates.supplement.xml`.

**"Synced" was upload only for `bizhawk` and `jgenesis`.** A restore could not place either
row's state, because both keep the slot in the stem and the restore read it from the extension.
The restore preview listed all three as "could not tell which slot it is". Fixed with RB-258,
and driven since on both `bizhawk` rows and on `jgenesis`, each restoring two slots to their own
names.

**Those two battery states are different things, and the distinction decides what to build next.**
`UnsyncableReason` separates them where the report's wording does not:

- **Deferred** is `NotInThisVersion`, defined as "The shape is understood and this build does not
  carry it. Stage 2's list." These are the emulators' own subdirectories under `saves/nes/`:
  `jgenesis/nes/`, and `bizhawk/` until #151. Understood, planned, not carried. `ares/Famicom/` is the same
  deferral and reports under `NoStateDeclaration` instead, because it holds a save state as well
  and that is the row that does not promise states sync.
- **No shape claims it** is `UnknownShape`. `mednafen` and `mesen` write their battery save **loose
  under `saves/nes/`**, beside the `.srm` files, which is structurally class A already. The only
  thing rejecting them is the extension: `save_rules.json` recognises `.bcr`, `.bkr`, `.brm` and
  `.srm`, and these two are `.sav`.

**The cheap-looking fix was a trap.** `save_rules.json` hard-wired `loose_emulator` to
`libretro`, so adding `.sav` to the extension list alone would have given mesen's
`Crystalis (USA).sav` the slot `libretro:battery` for that ROM and collided with libretro's own
`Crystalis (USA).srm`, which already syncs in that slot. This install holds both, because that game
was driven under `nestopia` and under `mesen` standalone. **#151 replaced the two globals** with a
`battery_saves` table of one rule per `(system, emulator)`, and loading refuses a table in which
two rules could claim one file, so `mesen` and `mednafen` each need a rule of their own and can no
longer be carried by accident. **Both have one since**, and the two share `.sav` at the loose
level only because mednafen's rule names a content hash on the stem, which the loader allows for
exactly one of a pair (RB-273).

The battery column is a **reported** limitation and the right behaviour for this release. Nothing
is dropped silently. The pass that drove these nine rows read one `saves` row covering all five
directories: "ares, bizhawk, jgenesis, mednafen, mesen hold shared containers or a shape no
declaration covers. This release syncs the battery saves loose under `saves/nes/` (class A), the
save states beside them, and the directory saves the shape definition names."

**That last clause was false for three of the five, and that was the defect.** `mednafen`, `mesen`
and `ares` declare no `es_savestates.cfg` entry, and `StateScanner` finds states only from that
file, so the states they wrote are not scanned, not uploaded and not restorable. They were
mentioned, and that is the part to be precise about: `CountFiles` excludes only declared state
directories, so these three undeclared ones were counted and named by the same `AddSubdirectories`
row. A person with a save state was not told there was none. They were told the directory held
something unsyncable, under a sentence promising that "the save states beside them" sync, with the
state files folded into a count whose reason is about battery saves and shared containers.

Issue #150 split that row in two, on whether `es_savestates.cfg` names the emulator at all. `nes`
reported `bizhawk, jgenesis` under the sentence above (`jgenesis` alone since #151 carried
BizHawk's battery save), and `ares, mednafen, mesen` under a
`no_state_declaration` row that repeats the shape half and ends "a save state written under these
is found only where RetroBat mirrors it into a declared path, and is otherwise not scanned, not
uploaded and not restorable". **Which is a correct message, not a fix.** The states are still
invisible, and making them syncable needs a bundled supplement carrying directory, filename and
slot per row. **That supplement now exists for `nes`**, `data/retrobat/es_savestates.supplement.xml`,
and all three rows' states sync and restore through it. The reach past this platform is unchanged: 30 of wave 1's 81 rows are in that
family, and `docs/platforms/README.md` had been reading "declares no directory" as "writes no
state".

The split is untested against a real install. It was driven against the `nes` tree this pass
measured, reproduced as a fixture, and the shipped `es_savestates.cfg`; nobody has re-run `saves`
on the machine that produced the states.

Two smaller findings from the same pass:

- **BizHawk names a battery save after the display name**, dropping the region tag:
  `StarTropics (USA).zip` produced `bizhawk/StarTropics.SaveRAM`. Its own state sidecar spells the
  convention out, `StarTropics.NesHawk`, so the join key exists. Issue #151, which now carries it:
  the title is joined through that sidecar or a BizHawk launch, and a title two ROMs answer to is
  refused rather than given to either. Driven on both cores on 2026-09-21; see "BizHawk battery
  saves, driven" below.
- **`saves` groups two unsyncable files under `nes/libretro`**, a directory that does not exist,
  and names no filenames. Issue #152.

## BizHawk battery saves, driven

Driven from EmulationStation on 2026-09-21 against the build carrying #151, with the maintainer
at the controller. **This is the one hands-on pass a save-logic change owes, not a certification
of either `bizhawk` row**: steps other than the battery save were not re-run. RB-262 to RB-266.

| Pass                                               | What happened                                                                                                                                                                                                      |
| -------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| The two saves already on disk, one per core        | Both attributed through their sidecars (`StarTropics.NesHawk`, and Ultima's under `quickerNES`), uploaded, and a second flush a no-op                                                                              |
| Restore, then play                                 | `StarTropics.SaveRAM` restored under its title with the original hash, and **Continue in BizHawk showed the saved progress**. BizHawk's rewrite on exit went up under the USA ROM                                  |
| Restore while another `nes` game runs              | Ultima's restore deferred with the display-name reason while StarTropics ran, and landed with its original hash once it quit                                                                                       |
| No learned title                                   | With the binding forgotten, the restore refused with "Run the game once under bizhawk" and wrote nothing                                                                                                           |
| No save state, `NesHawk`                           | Zelda wrote `Legend of Zelda, The.SaveRAM`; the launch route alone bound it and it went up                                                                                                                         |
| In-game save, `quickerNES`                         | Ultima's changed bytes went up under the same slot and ROM, then a no-op                                                                                                                                           |
| Europe copy of a game already saved in the USA one | **One file for both**: the Europe copy read the USA progress and wrote a new character into `StarTropics.SaveRAM`. Contested, nothing uploaded; `saves bind` settled it to USA and a restore put the USA save back |

Three defects surfaced here that the suite had not caught, and each now has a test: a slot this
device had already sent restored to the ROM's stem rather than the title (RB-266), a restored
file was credited to whichever BizHawk session came last and contested (265), and BizHawk's
`.SaveRAM.bak` was reported as an unknown shape (264).

**Since measured**: `NesHawk` and `quickerNES` read each other's `.SaveRAM`. Zelda's file,
registered under `NesHawk`, showed on the file-select screen under `quickerNES`, which wrote a
second name beside it (RB-271, in "`bizhawk`/`NesHawk` and `bizhawk`/`quickerNES`").

## The hooks carried the whole session

Seventeen launches across eight emulators, with nobody at a terminal, and every `start` and `quit`
pass in `background.log` exited 0. The states were already uploaded by the `quit` hook's detached
pass before a flush was run by hand, which is what the arrangement is for. One line of that log
lost its first eighteen characters, which is issue #153.
