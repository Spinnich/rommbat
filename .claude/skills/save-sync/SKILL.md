---
name: save-sync
description: Saves, save states, slots, the four save shapes, ROM attribution and archive hashing. Use for anything touching save or state sync, conflict handling, or per-game memory cards.
---

# Save sync

RomM's `Save` is **strictly one file**: `file_name`, `file_path`, `file_size_bytes`,
`content_hash` (MD5), `slot`, `emulator`. No directory or multi-file concept exists in the
API. Everything below is squeezed through that.

Grout is thin prior art: `sync/directory_saves.go` marks only `psp`, because Linux
handhelds run a narrow emulator set. RetroBat meets every case.

## Save states: parse, do not hardcode

`.emulationstation/es_savestates.cfg` gives directory, file, image, autosave templates and
slot bounds per emulator. See the `retrobat-layout` skill. Map `<image>` onto the optional
`screenshotFile`, and derive `{emulator}:{core}:{slot}` as the slot.

**That slot never leaves the device.** `POST /api/states` has no slot field, and the row it
returns carries no `content_hash` either, both confirmed live and in the pinned schema. So it
is a local pairing key, and "does this state still need sending" is answerable only from the
hash the device wrote down when it last sent one.

**Reverse the templates; do not expand a slot range.** Compiling `<file>` into an anchored
expression and matching what is on disk reads the slot off the filename, which answers
`libretro`'s trap, the one entry declaring no bounds, and settles a question that is not one of
the four: whether `{{slot}}` renders empty at slot zero becomes "accept zero digits".
**`bigpemu` is not answered**: it declares `001`/`999` against a two-digit `{{slot2d}}`, which
compiles to `\d{2}`. **That reads as a contradiction and is not one** (measurement 166, driven):
the bounds describe what **BigPEmu** writes in its own tree, three-digit and keyed by an internal
game id under `emulators/bigpemu/userdata/`, and the template describes **RetroBat's mirror**
under `saves/jaguar/bigpemu/`, two-digit and rom-named. Reading the declared path is right and
six real states came back as slots 1 to 6 with nothing reported. The edges are still reported and
never refused (#65): a `StateScanner` near-miss covers a name matching a `<file>` template except
for the width of its slot, and a slot outside the declared range. Only the slot widens, so the
`.txt` sidecar and the screenshots stay silent, confirmed against a real install's whole state
tree. A mirror name past slot 99 is what #34 now stands on, and reaching it needs ~94 saves of
one game.

**`bigpemu` is a third emulator whose native tree is not under `saves/`, and its battery save
never leaves it.** `game<ID>_eeprom.bigpeep` sits in `emulators/bigpemu/userdata/` with no
counterpart anywhere under `saves/jaguar/` (measurement 167), so a client reading only the
declared tree concludes the game has no battery save. That is the concrete reason `jaguar` stays
in `save_shapes.json`'s `_unclassified` list. Its `.txt` sidecar holds the same internal game id
its native filenames use (168), so it is the mapping between the two naming schemes, the same job
PPSSPP's `ULES01513_1.00` does. The same reversal on `<directory>` recovers the system and the
core from the tree, which answers `bizhawk`'s core scoping and is the only sound reading when
neither level of the save tree is positional. `desmume` still needs handling: nothing makes its
`<image>` differ from its `<file>`.

**The uploaded name is not the name on disk, and getting this wrong loses a state silently.**
Measured: the upsert keys on `(rom_id, file_name)` and the **emulator is not part of the key**,
so five posts of one name under five different emulator values reused a single row. `libretro`
declares `{{romfilename}}.state{{slot}}` and `gopher64` declares `{{romfilename}}.state{{slot0}}`,
which render identically for slots 1 to 9 and both serve `n64`; two libretro cores do the same
for one game. So upload as `<stem> [<emulator>[.<core>]]<ext>`, **unconditionally** rather than
only where a collision is possible: a conditional rule gives two devices two names for one
state, and two names is two rows.

**Suppress a zero-byte screenshot.** The server accepts one and stores it as a real screenshot
row, and RetroBat's mirror produces one by racing the emulator, so the client is the only place
that case gets caught.

**Read and write the declared directory, not the emulator's native one.** Several emulators
write states under their own naming and RetroBat mirrors them into the declared path a moment
later, live. Measured on PPSSPP: native `psp/PPSSPP_STATE/<GAMEID>_<ver>_<slot>.ppst` is
mirrored to `psp/ppsspp/<rom filename>_<slot>.ppst` about 120 ms after each save, and ES hands
the launcher the **declared** path via `-state_file`, which reaches the emulator as `--state=`.
So writing a downloaded state into the declared directory is what makes it loadable.

**The native location can be outside `saves/` altogether.** BizHawk writes
`emulators/bizhawk/sstates/<system>/<internal title>.<core>.QuickSave0.State` and openMSX
writes `bios/openmsx/savestates/<name>.oms`. For BizHawk only the mirror lands under `saves/`,
and deleting the native copy and relaunching rebuilt it from the ES-facing one, so the declared
path is authoritative in both directions. **openMSX's declared directory stayed empty**, so do
not assume every emulator is mirrored. Do not assume everything beside a state travels either:
BizHawk's `.State.rap` sibling is native-only and is not recreated on sync-in.

Four traps, all confirmed across the eleven emulators M0 drove:

- A **`.txt` sidecar** often sits beside the state holding the native basename
  (`UCES00995_1.00`, `SLUS-00404`, `GW7E69`). It is the mapping between the two naming schemes,
  and where it holds a serial it is the Game ID that directory-save attribution would otherwise
  read out of a ROM.

  **It is not emitted unconditionally, and an earlier reading here said it was.** Driven on a
  real install: `libretro` writes none at all, under either of two cores. `jgenesis` wrote one
  holding the plain rom filename, and `bizhawk` wrote `Phantasy Star (B).SMSHawk`, which is
  BizHawk's own truncated name plus the core. So its absence means nothing and its presence
  means nothing; only its **contents** are worth anything, and only sometimes.

- **`<image>` is absent more often than present**: missing outright for most emulators driven,
  and correct, zero-byte and missing across three runs of the same PPSSPP game. `screenshotFile`
  is best-effort everywhere; absent and empty are both normal and say nothing about the state.
- **The declared `<directory>` is wrong for one of the twelve emulators launched.**
  **`openmsx` writes to `bios/openmsx/savestates/`, a different top-level tree** from the
  declared `saves/msx1/openmsx`, and that is unfixed. `flycast` was the second until
  **RetroBat 8.2.1 fixed it** (`emulatorlauncher#1336`): it still writes
  `dreamcast/reicast/states` first, but the state is now mirrored into the declared
  `dreamcast/flycast/sstates` in the same millisecond, confirmed by hand over three runs, so
  Dreamcast states sync from the declaration like any other emulator's. An empty declared
  directory is never evidence that a game has no states; cross-check against the emulator's
  generated config.
- **Anchor the slot placeholder as a single digit when expanding a template.** DeSmuME declares
  `{{romfilename}}.ds{{slot0}}` and writes its battery save as `{{romfilename}}.dsv`, so a
  `<rom>.ds*` glob picks up the battery save as slot "v".

A declaration is not a promise that the emulator is usable. RetroBat downloads emulators on
demand, so six of the thirteen had no executable at all; installing one raises a **modal dialog
with no title and no timeout** that blocks the launch until answered. And `bizhawk` crashes in
RetroBat's controller generator unless the launcher is given `-core`. Check for the binary, and
do not promise state sync on the strength of the config alone.

Record emulator, core and version with every state, and never silently restore one made by
a different version. RetroBat's own wiki warns that states break across emulator updates.

## The four shapes

| Class | Shape                          | Examples                                                                 | Handling                                          |
| ----- | ------------------------------ | ------------------------------------------------------------------------ | ------------------------------------------------- |
| A     | One file per game              | RetroArch `.srm`/`.sav`/`.eep`                                           | Direct 1:1. Slot `{emulator}:battery`             |
| B     | Several files per game         | `.srm` + `.rtc`, ScummVM `.s00`-`.s99`                                   | Per-file slots when small and stable, else bundle |
| C     | Directory per game             | PPSSPP `SAVEDATA/<GAMEID>/`, RPCS3, Cemu, Citra, Wii NAND, MAME `nvram/` | Bundle to one archive                             |
| D     | Container shared by many games | PCSX2 `Mcd001.ps2`, Dreamcast VMU, megacd `4Mbit_cart.brm`, xbox HDD     | Convert to per-game, see below                    |

**The class says how many files move as a unit. It does not say how the key matches a name**,
and those are two axes, not one. A `unit_paths` entry carries `key` (`title_id`, `hex_ascii`,
`game code`, `rom stem`) saying what it keys on, and does not say whether the match is
**exact or a prefix**. PSP is the case that shows why it matters: `ULUS10064` is a prefix, so
`ULUS10064SYSDATA` belongs to the same unit, and the bundling only works because it was written
knowing that. A platform added later is where the omission bites. Argosy splits the same problem
into five explicit usages (`FOLDER_EXACT`, `FOLDER_PREFIX`, `FILE_EXACT`, `FILE_PREFIX`,
`FOLDER_SPLIT`), which is a match-rule taxonomy and not a rival to these four classes. When
adding a platform, state the match rule alongside the key. See
[argosy-findings.md](../../../docs/argosy-findings.md), A8.

**A save layout is chosen by `(system, emulator)`, never emulator alone.** RetroBat makes this
mostly structural, because its tree is `saves/<system>/<emulator>/` and `save_shapes.json` is
keyed by system folder, so `gamecube/dolphin-emu` and `wii/dolphin-emu` are already distinct.
Argosy, whose registry keyed on emulator, shipped two bugs from exactly this: a Wii row showing
GameCube's path, and a shared override key where a GameCube save path silently became the Wii
one. Our `shapes` map holds one class per system with `shape_depends_on_emulator` as the escape
hatch, which is the same relationship built the other way round; treat a multi-emulator system
as needing the per-emulator answer rather than as an exception. A9.

## Class D is a configuration problem

PS1 and GameCube are **already per-game in a stock RetroBat** (`duckstation_memcardtype`
defaults to `PerGameTitle`; `dolphin_slotA` defaults to GCI folder), and both should be left
that way. Only PCSX2 defaults to a shared card, and `pcsx2_slot1_memory=game` names the card
after the ROM stem, which makes attribution trivial on a single-disc title.

**GameCube can be moved the wrong way, and the menu makes it easy.** `dolphin_slotA` is
labelled **SAVE FORMAT** with two choices: `8`, the GCI folder that is class C, and `1`, one
shared raw `SRAM.<REGION>.raw` that is class D. So GameCube is class C only at the default, and
a user who picked the tidier-sounding option has a shared card RomMBat's class C scan finds
nothing in. **Slot B is already there**: RetroBat only ever writes `SlotB` when
`dolphin_microphone` is on, so it stays at Dolphin's stock relative default and a 16 MB
`saves/dolphin/User/GC/SRAM.<REGION>.raw` accumulates outside every declared container. Finding
193, and the same shape of trap as PCSX2's four menu entries.

Set these via `es_settings.cfg`, never an emulator INI. See `retrobat-layout`. The per-game
key is `<system>["<rom filename>"].<key>` and the **filename must keep its extension**; a
bare stem is ignored silently and the emulator keeps writing to the shared container.

## Somebody else may be writing to the same directory

**`dolphin_sync_saves` is the one measured case, and the repository described it wrongly for
four documents.** It is not a background schedule and it is not two emulator folders. It is
GameCube only, it runs once per launch inside `emulatorlauncher` before Dolphin starts, and it
reconciles `saves/gamecube/dolphin-emu/User/GC/<REGION>/` against a **`Card A/` subdirectory of
that same folder**. Newest wins by mtime, the loser is renamed `.old`, and every failure is
swallowed by a bare `catch`.

**The hazard is the one-sided branch, not the mtime comparison.** A save RomMBat restores is
written with the current time, so it always wins; that direction is safe. But a `.gci` sitting in
`Card A` with nothing beside it is copied **back out**, so a save RomMBat removed reappears
holding whatever `Card A` captured at some earlier launch. Driven on hardware: deleting the
region-root file and launching restored the _previous_ session's bytes, and the only trace was
`[INFO] GameCube saves have been synced.` Findings 190 and 191.

`Card A` is invisible to class C discovery, and that is correct rather than a bug:
`SaveUnitScanner` enumerates one level, so it can neither double-count the copies nor be fooled
by a `.gci.old`. It is also why RomMBat cannot see the resurrection coming, which is why
`DolphinSaveSync` exists.

**Detect and report, never act.** `DolphinSaveSync.Inspect` reads the key at es_settings.cfg's
own precedence and walks the three region folders, and the result becomes an
`UnsyncableReason.ManagedElsewhere` row. Two writers reconciling one directory by different
rules is how saves get lost, so RomMBat does not read `Card A`, does not upload it and does not
delete it. **Report when the option is off too**: turning it off deletes nothing, so the copies
outlive the setting and regain their effect the moment it comes back on.

**Never convert a multi-disc set, and never convert DuckStation at all.** A two-disc set
driven under stock `PerGameTitle` produced **one card for the set**:
`memcards/Metal Gear Solid (USA)_1.mcd` and `_2.mcd`, where the suffix is the console **slot**
and `_2` is an empty formatted card. The stem is `gamedb.yaml`'s `saveName` with the disc
marker removed, so it carries the region (`(USA)`) but not the disc and not the rom's
`(Rev 1)`. DuckStation binds a disc set through its own database, which is exactly what
`PerGameFileTitle` would throw away by keying on three separate filenames.

**The playlist is not what binds the set; the database is.** Final Fantasy VII, three discs
loose with no `.m3u` and launched as disc 1 alone, produced one `Final Fantasy VII (USA)_1.mcd`
resolving to all three serials. That is the layout a RomM sync creates, so stock is safe on it.

**But the card and the state are keyed differently, in the same session.** The card is per disc
**set**; the save state is `Final Fantasy VII (USA) (Disc 1)_01.sav`, named from the rom file
and therefore per **disc**. A `rom_id` can own one card and three states, so never assume one
save per game or one save per file. The mapping is many-to-many: 130 of the database's 698
disc-set stems keep a subtitle behind the disc marker
(`Biohazard 2 (Japan) (Disc 1) (Leon-hen)`), where set membership is not recoverable from the
card name.

The price of leaving it stock is that PS1 cards need Game-ID attribution rather than filename
attribution. RetroBat pays part of it already: a `.txt` beside the DuckStation save state holds
the bare serial (`SLUS-00594`, `SCUS-94163`).

PS2 has the same failure with no escape, because PCSX2 cannot bind discs at all. So conversion
is **per game**, which is what the `<system>["<rom>"]` form is for: convert single-disc titles,
leave sets alone, and say why.

**Driven end to end in M6 stage 2c, and the details are what make it work.** The card PCSX2
writes is `<rom stem>.ps2`: **the extension is replaced, not appended**, so the name is exactly
the `(folder, stem)` key class A attribution already uses and no new route is needed. Note the
asymmetry with the setting that causes it, because it is the trap: the `es_settings.cfg` key
must carry `.chd` or it is ignored silently, while the card it produces drops it. Both rules
are right and they point opposite ways.

It lands **three levels down**, `saves/ps2/pcsx2/memcards/`, where class A discovery only reads
files loose directly under `saves/<system>/`, so the container is **declared in
`save_shapes.json` and never discovered**. That declaration is also what the download side
needs: a converted card arriving for a device that has never run the game must go into the
container, and the class A rule would put it loose where PCSX2 never looks, quietly.

**Record it as class D, not class A.** One file per game is its shape; what it _is_ is a class D
system whose container was made per-game, and the row is the only place that stays true once
the setting is out of sight. Class D rows are forgettable like class A, keyed on the path: a row
left behind for a deleted card blocks eviction for that ROM forever.

**Discovery must not consult the conversion record.** A card named after a ROM in the declared
container is that ROM's save whether RomMBat set the option or the user did.

**What converting really costs, measured on a real card.** The shared `Mcd001.ps2` held saves
for **11 distinct games**, and after the conversion it was **untouched**: same mtime, same md5.
So the redirect is total and the stranded save really is stranded. Console **slot 2 stays
shared** (`slot1_memory` converts slot 1 only) and `Mcd002.ps2` moved its mtime without changing
a byte, which is one more reason nothing may trust mtime.

Caveats, all user-visible: it mutates their config so it is opt-in and reversible;
switching strands existing saves inside the old container unless migrated; and per-game
cards break games that legitimately read a prequel's save.

**"Auto" in the ES menu means the key is absent**, not that a value is set: `es_features.cfg`
declares three choices for `pcsx2_slot1_memory` and no `auto`, and ES synthesises AUTO for any
unset feature. Two things follow. Reverting has to restore **absence** rather than a plausible
stock value, or the user lands somewhere they never were, which is why the conversion record
stores absent and present-with-a-value as different states. And after a conversion the
system-scoped menu still reads Auto while the per-game key silently outranks it, so **the ES
menu shows no sign that a game has been converted**.

**Never write `es_settings.cfg` while EmulationStation is running.** It loads the file at
startup and serialises that model on every write, so a key that appears afterwards is
discarded, merged and atomic or not. See `retrobat-layout`. Refuse, say why, and re-read after
writing rather than trusting the rename.

**Never use mtime to decide whether a save changed, in any class.** Launching a PS2 game
rewrote both `Mcd001.ps2` and `Mcd002.ps2` with no in-game save at all, and a Dreamcast launch
rewrites the shared VMU the same way, so a mtime check uploads the container after every
session. Hash the content.

**Class A does it too, and no size floor catches it.** A Master System cart booted to its
title screen under libretro `genesis_plus_gx`, with no save key pressed and no progress made,
wrote an 8,188-byte `.srm` whose contents are the cart formatting its own backup RAM. 35
distinct byte values, legible ASCII: a minimum-upload-size check passes it and a blankness
check passes it. `autosave_interval = "10"` means it lands within seconds of boot, so waiting
for a clean exit protects nothing either. **The first save seen for a ROM with no local
baseline is not evidence that anything was played**, so it must not win a conflict on recency
alone.

**Dreamcast converts, but not into class A.** With `flycast_vmupergame=1` the new file is
`vmu/T40217N_vmu_save_A1.bin`, named for the **disc serial**, while the shared
`vmu_save_A1.bin` stops being written and both live in one directory. The rom filename appears
nowhere, so this is Game-ID attribution like class C. Only port 1 converts. PS1 lands in the
same place under its stock memory card mode, so identifier-keyed attribution is the normal case
for disc systems, not an exception two of them make.

**A save state is not always one file, and a save tree is not always portable.** A libretro
state comes with a real `.state1.png` screenshot beside it, which bundling has to keep
together. A multi-disc launch also leaves `saves/<system>/<playlist stem>.ldci`, RetroArch's
record of which disc was in the drive, and its `image_path` is an **absolute path with a drive
letter**. Syncing that verbatim restores a dangling pointer on any install at a different root,
so exclude it or rewrite it on restore.

## Attribution

Class A and B match by filename. Class C is keyed by **Game ID** (`UCUS98751`, a PS3
`TITLEID`, a GameCube disc ID), and **RomM stores no serial, title ID or product code
anywhere**, so no API lookup exists.

**Ask every route, not the first one that answers.** They are cheap next to the scan that
already ran, and their agreement is the only evidence a binding has. One exception comes before
all of them: under `mame` the key _is_ the ROM basename, so that join needs no route and is
never cached.

1. **Correlate with the launch journal.** A save directory touched inside a known launch
   window belongs to that rom. Cache the learned binding. This generalises to every odd case
   and needs no format parsing.

   **Do not source that window from the `game-start` hook's arguments.** The hook is never
   told the system, emulator or core, and a `.bat` hook does not even start when the display
   name contains a space, which is nearly every real rom (an `.exe` hook does; see
   `retrobat-layout`). Build the window from `emulationstation/emulatorLauncher.log`, which
   records rom path, system, emulator and core with a millisecond timestamp on every launch,
   and use the `game-end` hook as the trigger to go read it. See
   `docs/retrobat-findings.md` probes 1 and 7b.

2. **Read the ID from the ROM's header**, and know how little that reaches. Measured across
   every image in five systems on a real install: GameCube **100%** and Wii **75.5%** (a `.wad`
   has no disc header and its title id sits behind a variable-length certificate chain), and
   **0% of PSP, PS3 and PSX**, because no constant offset reaches a `.cso`, a `.chd` or an
   ISO9660 filesystem. Check the `.rvz` format version before trusting `0x58`. **`PARAM.SFO`
   adds nothing**: its `SAVEDATA_DIRECTORY` is the directory's own name and its `TITLE` is a
   human string. So this route serves the two systems whose save key _is_ the game code, and
   nothing else.

3. **Read it out of the save-state name sidecar**, which is free and reaches what route 2
   cannot. `ppsspp/3rd Birthday, The (Europe).txt` holds `ULES01513_1.00`, joining the key of
   `SAVEDATA/ULES01513SYSDATA` to a ROM filename the ordinary index resolves. It needs no ROM
   read and no observed launch, and it covers only games that have a state.

**Both indexes are first-wins, and the two must not diverge.** Within one route a key that two
ROMs answer to takes the first, because either is as good an answer as the other: for the header
that is a revision pair sharing a game code, and for the sidecar it is two states naming one
identifier. Both scans are ordered, by ROM path and by state path, so first is a stable answer
rather than whichever row the database returned last. The sidecar index was last-wins until the
sweep after stage 2b, which meant the two routes settled the same question by opposite rules.

**Disagreement fails closed.** Two routes naming different games binds nothing, records the
refusal so it is not recomputed every scan, and reports both candidates. Picking a side uploads
one game's save under another's name and the cache then makes it permanent. `saves bind` is how a
person settles or clears one.

**Cache a decision, never an absence.** "Both routes read something and they disagree" is worth
a row; "nothing had anything to say" is not, because the usual cause is that the ROM has not been
synced yet and a cached refusal outlives its own reason, leaving the unit unattributed behind a
row nothing clears. Recomputing costs one dictionary lookup against indexes the pass already
built. Measured on a real install, where a MAME `nvram/` tree with no ROMs beside it produced
1,231 of these in one scan.

**A save unit is a (container, key) pair, not a directory.** `ps3` keeps three directories under
one title id, `psp`'s key is a _prefix_ of the segment (`ULES01513SYSDATA`), and `gamecube` has
no per-game directory at all: two `.gci` files share a region folder with every other game. The
container is declared in `save_shapes.json` and never discovered, because hashing an emulator's
data root costs 426 s where the scoped subtree costs 0.06 s.

## Hash contents, not the archive

**RomM does the same thing, by a different function, so class C carries two hashes.** Its
`content_hash` is the MD5 of the bytes for a plain file and, for an archive, a digest over the
archive's _contents_: the same member rebuilt at another compression level and timestamp gives a
different zip and the same digest, and renaming the member changes it. That function is not
reproducible client-side. So the logical fold is the **local change detector** and the digest the
server returned on the last upload is the **wire value**; sending anything else answers
`download` forever. It also means a downloaded archive cannot be verified against
`server_content_hash` the way a plain file can, and the CRC that extraction validates is what
stands in for it.

**Which hash answers "have I already got this" follows from that split, and getting it wrong
costs a transfer.** The download skip that recognises this device's own upload compares the
local fold against the offered digest, which is the right comparison for class A and B and can
never be true for class C. A bundled unit is asked in the server's vocabulary instead: the
slot's recorded `server_content_hash` against the operation's says the server is offering back
what this device last exchanged, and `uploaded_content_hash` against the fold says the tree
still holds it. Both halves, because the first cannot see a unit edited since and the second
cannot see the server moving on.

Defining `content_hash` as the MD5 of zip bytes is a trap: Go's `archive/zip` and .NET's
`ZipArchive` differ in entry ordering, timestamps and compression, so RomMBat and Grout
would disagree on identical saves forever, and a library upgrade could do the same to
RomMBat alone. Hash the **logical contents**: sorted relative paths plus each file's own
hash, folded into one digest. The archive is transport only.

## Protocol rules

- Pair on `(rom_id, slot)`. Always send a stable, non-null slot.
- Send the **real local mtime** as `updated_at`, never the sync time.
- Compare on `content_hash` first, mtime second: **exFAT and FAT32 both quantise mtime to
  2 seconds and round up**, so a save can be stamped 2 s in the future and several saves
  written together share one timestamp. Mtimes are not bit-stable across filesystems.
- **`overwrite=true` does not replace a row in the slot, whatever it looks like.** A slotted
  upload is renamed with a `[YYYY-MM-DD_HH-MM-SS]` tag and the row is keyed on that name, so the
  clock decides: same second updates, a second later appends. `overwrite` only suppresses the 409
  checks and the identical-content dedup. So `--keep-local` appends, the server's copy stays one
  row down, and `autocleanup_limit=10` is what bounds the slot rather than the resolution bounding
  it at one. Never tell a user their copy replaced the server's. Measurement 160.
- An unregistered `device_id` is a **404**, not a request that quietly proceeds without a device.
  Measurement 162.
- 409 means the slot moved. Surface it; retry with `overwrite=true` only after resolution.
  **The body is a bare string** with no save id and no timestamps, so fetch the save row if
  you want to show the user what they are conflicting with. It fires when **this device's**
  sync record is stale, not when the save is newest overall.
- **A conflict is persisted, not printed.** It goes in `save_conflict` and outlives the flush
  that found it, the local file is copied aside **once per conflict rather than once per
  flush**, and `saves resolve <rom> <slot> --keep-local | --keep-server` ends it. There is no
  default side, because either default silently discards somebody's progress. `--keep-local` is
  the only caller of `overwrite=true` in the codebase; a 409 that survives it means the slot
  moved again between the report and the decision, so it is reported rather than forced. Both
  outcomes prune the copy aside.
- **`saves resolve` takes `TreeLock`, and refuses rather than treating a held lock as done.** It
  runs the same `SaveUnitTransfer.Restore` a flush does, so two of them at once, or one racing
  `evict`'s sweep of `partial/`, leaves a shared container half swapped. Unlike a flush, where
  failing to acquire means another agent is already doing the work, a person asked for this one
  and silently returning `Ok` would read as having resolved it: it exits `Refused` and says why.
- **`partial/unit-<guid>/` is live state for the length of a class C restore, and nothing holds
  a handle on it.** `SaveArchive.Extract` closes each entry's writer inside its own loop, so the
  staging directory sits unprotected across the hash, the copy aside, the `Remove` and the whole
  move loop. Anything that deletes under `partial/` must hold `TreeLock`. **A `FileShare.None`
  sentinel inside the directory is not a substitute**, measured rather than assumed:
  `Directory.Delete(recursive: true)` removes the siblings first and only then fails on the
  sentinel, so the staged members are gone regardless.
- **Negotiate returns a download for every save the device has no sync record for**, including
  slots the client did not submit. An **empty** `saves` array came back with 13 downloads across
  two ROMs, one never named by the client, and acking one dropped the next answer to 12. An
  earlier reading of this was backwards: a device that is already current for everything gets no
  operations, which is not the same as nothing being volunteered. **So negotiating with an empty
  array is the fresh-device inventory pass**, and no separate one over `GET /api/saves` is
  needed. A restore onto a device that never held the slot is an ordinary case, not a dead one,
  so **never return early because the local save list is empty**: that is the device with the
  strongest reason to pull. The target for such a slot comes from the ROM's own folder and stem,
  with only the extension read off the operation's tagged filename. **A bundled slot is the
  exception and is refused with a reason**, because a class C restore needs a container and a
  unit key and both come from a local unit this device does not have; recognise it from the
  shapes table, never from a `.zip` extension.
- **Unscoped negotiate means most of the answer is for games the device does not hold, and that
  is not a failure.** A device carrying a 10-game sync set out of a 500-save library is offered
  every one of them, and each has no local ROM, so no folder and no stem to build a target from.
  Counting those as failures gives a per-operation stderr line each and a `Partial` exit on every
  flush a partial-library device ever runs, which is what sync sets are for. It is one count in
  the summary, and it is kept out of `Problems` so a quiet hook-driven flush stays quiet. Check
  it **before** the bundled-slot refusal. Those two do not compete today, because
  `IsUnplaceableUnit` needs the ROM's folder to reach the shapes table and so answers false for
  an absent ROM: driven on a real install, a `ppsspp:savedata` operation for an unsynced game
  fell through it to "nowhere to write it". Deciding the absent ROM first keeps that from
  depending on the shape lookup's internals.
- Persist the `file_name` the server returns, not the one you sent, and **write a different name
  to disk**, because `Game [2026-08-10_22-58-26].srm` is invisible to an emulator matching on the
  rom name. **The name to write is the ROM's own stem plus `file_extension`, and it is not
  `file_name_no_tags`.** The server strips general tags rather than only its own timestamp: a real
  save came back as `Phantasy Star (Brazil) [2026-08-17_17-01-00].srm` with `file_name_no_tags` of
  `Phantasy Star`, because `(Brazil)` is part of the ROM's name. Writing that produces a file the
  emulator cannot see, which is the exact failure this rule exists to prevent. The ROM stem needs
  no regex: it is the `(folder, stem)` key class A attribution already uses, run backwards.
- **Download with `optimistic=false`, then ack.** The parameter defaults to true and records
  the device as current on the request rather than on receipt, so a transfer that dies
  mid-body leaves the server sure the device has a save it does not, and the next negotiate
  answers `no_op`. Send `POST /api/saves/{id}/downloaded` only after the bytes are written
  and verified. Same discipline as M3's `.part`: verify, then commit.
- **Decide retention.** `autocleanup` defaults to false and `autocleanup_limit` to 10.
  Without them a slot gains a row per genuine change forever, and the `keep_both` conflict
  default compounds it.
- Restores stage everything off to one side: extract to a temp directory beside the target, keep
  the previous copy until the next successful sync. **A class C swap is not one filesystem
  operation, and do not write that it is.** Members are removed and moved in one at a time,
  because a whole-container swap is the wrong fix: the container is shared, and
  `saves/psp/SAVEDATA` holds every PSP game on the install. It is **all-or-nothing anyway**: a
  failure partway is rolled back, the members the pass placed deleted and the ones it removed
  copied back from `replaced/`, so the unit ends up wholly new or wholly as it was. The one case
  that still leaves a mixed unit is a rollback that cannot finish, and the message says so by
  name and names the `replaced/` copy.
- **A class C restore whose contents already match the tree does not write the tree.** The fold
  of what arrived is computed before anything live is touched, so it is free. The transfer is
  not avoidable and the write is: a peer holding identical bytes carries a digest this device has
  never seen, so negotiate answers `download` and no local comparison can rule it out. The ack
  and the slot record still run, or the next negotiate answers `upload` for a unit in step.
- **A bundled restore replaces the unit, it does not merge into it.** Delete the members the
  archive does not name before moving the new ones in; they are under `replaced/` by then. The
  members the archive omits are the slots another device deleted, and leaving them makes the
  fold over the tree disagree with the fold over the archive, so the next scan reads the unit
  as changed and puts the merged copy back over the server's. Somebody who chose to discard the
  local side gets a merge instead, and it propagates.
- **Record the slot's server identity when a bundled restore lands**, not only the local fold.
  The wire hash for an unchanged class C unit is `server_content_hash`, since the server's digest
  over an archive cannot be recomputed client-side. A restore that leaves the slot holding the
  pre-download digest submits a hash the server no longer recognises, and negotiate answers
  `upload` for a unit that is already identical. Measured: the flush after a class C restore
  reported one upload, which the server then deduplicated into a row it already had.
- **A settled conflict is settled for one server row, not for one digest.** `content_hash` is
  over an archive's contents, so a slot returning to contents it held before carries a digest
  that was already decided while being a different row. Compare the save id too, or a real
  conflict is dropped: no row to list, `resolve` answering "already resolved", and the local
  write refused with a 409 on every flush with no way out.
- Never evict a ROM whose saves are still in the outbox.

## `device_id` is bookkeeping, never a filter

Both devices see the same save rows; nothing is isolated per device. What is per device is
the sync record, exposed as `device_syncs` on a save, and **that array is empty unless the
request carries `device_id`**. Empty therefore reads exactly like "nobody has ever synced
this", which is why it must not be read that way. With `device_id` set it lists every device
that has a record, the queried one first, and a device that has never synced is **absent**
rather than `is_current: false`. Treat a missing entry as the strongest reason to pull.
`origin_device_id` names the uploader, which is how a device recognises its own save
returning.

## Determinism is what makes replay safe

Identical content uploaded twice into one slot reuses the same row, which is what makes a
replayed flush idempotent. That only holds if the bytes are identical, so a bundled class-C
save must produce a **byte-identical archive** for unchanged contents. Freegosy writes a
timestamp file into every bundle specifically to defeat this dedup, and pays for it with a
new server row on every sync of an unchanged save. Hash the logical contents, and keep the
archive deterministic. **The dedup is off under `overwrite=true`**, measured, so it covers the
flush path and not `saves resolve --keep-local`: a repeated resolution makes a row.

Play sessions replay safely too, and say so: `POST /api/play-sessions` returns a per-index
result array and marks a repeat `"status": "duplicate"`, so a partial flush is reconciled
exactly rather than inferred. It needs no open sync session.
