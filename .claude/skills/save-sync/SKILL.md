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

## Where the flush passes live

**One Core service, and both front ends are printers over it.** `Sync/SaveFlushService` composes
`SpoolDrain`, `PlaytimeCorrelator`, `StateScanner`, `SaveScanner`, `OutboxFlush`, `SaveSync` and
`StateSync` and returns a `FlushReport`. `flush` was 289 lines welded to `Console` until M7 stage
7b-2b; what is left in the agent is `--quiet`, the conflict block and the exit-code mapping.
**Add a pass to the service, never to a subcommand**, or the gamepad UI silently stops doing it.

Four properties of that pass are rules rather than implementation, and each has a test:

- **The tree lock is taken there and a failed acquire is `FlushState.Skipped`.** An outcome with
  its own sentence, never an exception and never a null report. Two flushes overlap whenever
  somebody runs one beside a sync, and the second exits rather than waiting, because waiting
  would put a process to sleep inside the game-launch path.
- **The local half always runs and only sending needs a link.** A caller that could not
  authenticate passes no connection and still gets the drain, the correlate and both scans.
- **States are scanned before saves** (#64) **and sent last.** The scan order is what lets the
  sidecar attribution route see `local_state`; the send order is because states are the only
  part of the pass nobody has to act on.
- **The connection is a parameter, not something the service opens.** Authenticating reads a
  passphrase off a command line and maps to an exit code Core cannot know. A caller that
  supplies a connection gets that one used for the whole pass, sends included: a screen that
  authenticated once must not have its flush quietly open a second connection to whatever the
  store calls the origin.

`FlushState` distinguishes `Done`, `Skipped`, `LocalOnly`, `NotPaired`, `Unreachable` and
`Partial`. **`Unreachable` is not reached in practice** and that is worth knowing before relying
on it: all three sending passes absorb `RomMUnreachableException` per item and report it, so a
server that goes away mid-flush ends the pass `Partial`. The outer catch is inherited from the
subcommand rather than designed.

**`Partial` means this run failed at something it attempted, and nothing else** (#148). A
session close the server refuses counts, though every transfer landed: a token without
`devices.write` fails it on every flush, and the line names the scope so the repeat has a remedy.
On `saves restore --apply` a row the find could not place does not count, since the run never
attempted it: such rows are mostly a standing property of the library, and on the measured `nes`
install 18 states scoped by core pinned every restore at 7 with `failed 0`. They are printed and
counted beside the result instead. The flush is different on one row and keeps it: an offered
bundled slot with no local unit is `Failed`, because it has a remedy, run the game once.

## Save states: parse, do not hardcode

`.emulationstation/es_savestates.cfg` gives directory, file, image, autosave templates and
slot bounds per emulator. See the `retrobat-layout` skill. Map `<image>` onto the optional
`screenshotFile`, and derive `{emulator}:{core}:{slot}` as the slot.

**Name the screenshot after the state's upload name, never after the image file.** RomM has no
link column: `State.screenshot` finds an image whose name, or name less extension, equals the
state's name or the state's name less extension, where "extension" is RomM's
`\.(([a-z]+\.)*\w+)$`. Scoping the image's own name put the group after `.state1` and never
matched for the five emulators whose `<image>` is `<file>.png`, while the seven whose `<image>`
replaces the extension happened to match; that mix was finding 138's "a third".
`<upload name><image extension>` matches for every declared emulator (ppsspp's image is `.jpg`).

**A match is not unique, so check the name that comes back.** RomM's pattern strips a run of
lowercase-letter extensions as one: `Game [libretro.snes9x].state.png` loses `.state.png`, so
libretro slot 0's image has the name-less-extension of every slot of that game and core. The
lookup ranks an image whose name less extension is the state's full name first, then takes the
highest id, so a slot with no image of its own is answered with slot 0's. An old-name slot 0
image, `Game.state [libretro.x].png`, likewise answers for the autosave
`Game.state [libretro.x].auto`. `StateSync.IsOwnScreenshot` accepts only an image named
`<state upload name>.<ext>`, or the exact earlier name of the state's own image (which linked
for the seven), on restore and when counting a dropped screenshot on push. `StubRomMServer.Binds` ports the filter and `ScreenshotFor` the
choice among a ROM's images, so a test cannot pass on a name the server would not link or on
another state's image. Finding 258.

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

### Removing a game names a class D container rather than vouching for it

**A shared container has no `rom_id` by definition, so `SaveGuard` cannot answer for it.** The
same is true of a class C unit whose attribution failed and left a null one. When a person
removes a game, the honest behaviour is to **name the container and let them decide**, never to
claim safety.

- **Nothing is deleted either way.** Removal walks `local_file`, whose seven kinds hold no
  saves, so the container survives whatever the screen says. What it cannot survive is the
  _attribution_: the ROM going takes with it the only thing that could ever say which game those
  bytes belong to, and that is not recoverable.
- **Scoped to the systems the removed games are in**, via `EvictionService.Unvouchable`. Naming
  every unattributed save on the install would be noise on a screen a person is reading in order
  to press a button.
- This is the hole #110 said it could not close, and it is closed by saying so rather than by
  pretending. A PS2 memory card is the case that exists.

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

### The other writer is usually the running emulator

**Nothing may write a save for a game that is in flight, and `Sync/InFlightGuard` is the only
thing that knows.** A download landing while the emulator holds the file is overwritten by the
emulator's own copy on exit, so the other device's save is gone and the write happened under an
open handle. Measured on this install (#155): ES's own launch write lands 1.6 to 4.9 s before the
`start` hook fires and the background pass then takes 5 to 11 s, so a user pressing A promptly
starts the emulator inside the window the download is still in.

**Guard the write, not the launch.** `game-start` and `game-end` stay inert, because they run in
the game-launch path and CLAUDE.md rule 4 is not negotiable; a launch that waits on a round trip
is worse than one that occasionally plays a stale save. So the deferral is the fix, and it is
cheap: nothing is acknowledged, so the next negotiate offers the same save again.

**Every route that writes a save into the tree asks, which is the set the tree lock already
groups.** The flush's download and `StateSync.RestoreAsync`, `saves restore --apply`, and
`saves resolve --keep-server`, whose class C half swaps unit members into a container the running
emulator holds open. A guard on the flush alone leaves the two routes a person reaches by hand
writing into a file being played, which is the same data loss on a slower path.

Five things about it that are decisions rather than detail:

- **Read the journal _and_ the spool.** The journal covers a game launched before the pass, since
  the flush drains and correlates before it downloads and `PlaytimeCorrelator` leaves an unmatched
  `game-start` open on purpose. The spool covers a game launched _during_ the pass, whose `.hook`
  file the drain has already gone past. Either alone misses half the window.
- **A stale `game-start` is bounded by a sequence, not a clock.** A machine that loses power
  mid-game leaves the row open forever, and the block has to lift on its own. The bound is the
  last `start` or `quit` row's `local_sequence`, because ES starting or exiting ends every game
  that was running, and sequence order survives a flat RTC where wall-clock order does not.
- **Per ROM, widened to a shared container.** One game being played must not stall a library
  sync, and a class A save is one file beside its own rom. But a container the shape file
  declares as shared is held by whichever game is running, so a `gamecube` launch defers another
  GameCube game's `.gci` in the same region folder. A file-shaped declaration matches only
  itself: a converted per-game `.ps2` card beside `Mcd001.ps2` is a different file.
- **A launch nobody can name is bounded by EmulationStation, because the spool has no sequence.**
  A record written by a newer hook is left on disk rather than deleted, so a newer agent recovers
  the play session it describes (#31), which also means the same file is read again on every
  pass. Nothing the front end launched outlives the front end, so `EmulationStationProcess` ends
  it: the install defers while ES is up and syncs on the pass the `quit` hook spawns. Without
  that, one unparseable file would stop every save the install ever downloads, permanently, which
  is the failure #31 exists to prevent. The check is injectable for the reason `SaveConverter`'s
  is: nothing is ever running on a build agent, so the branch is otherwise untestable.
- **A `game-end` pops the newest launch, whoever owns it, and that is safe only because of
  EmulationStation.** `game-end` carries no arguments, so `ApplySpool` removes the last entry
  unconditionally, the stack model `PlaytimeCorrelator` uses over the journal. `retrobat-layout`
  records that a **failed** launch fires `game-end` with no `game-start`, and that orphan would
  pop a different game that really is running and let a write land under its open handle. It
  cannot happen today because ES starts no second game while one runs, so an orphan never
  coexists with another launch in flight. **The argument is ES's, not RomMBat's**: a launch
  route that overlays a game on a running one, or an ES build that allows it, retires it
  silently. The fix then is to correlate the pop by rom path through the launch log, the way
  `PlaytimeCorrelator.MatchLaunch` finds a `game-end`'s launch, and drop a `game-end` that matches
  nothing. The correlator pops its own stack the same way and would want the same change (#165).

**Freshness before play is still not solved, and is still open on #155.** A save arriving while ES
sits idle for hours is picked up at the next ES start and not before, and nothing gates a launch
on the check having finished. The guard turns the data-loss ordering into a deferral; it does not
make the launch see a newer save.

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

Class A and B match by filename, **and which filename is a per-`(system, emulator)` rule, not a
global.** `save_rules.json`'s `battery_saves` gives each rule a directory under
`saves/<system>/`, its extensions and a `named_after`, and the emulator in it is the slot.
Loading refuses two rules claiming one extension in one directory, and one emulator with two
rules on a system, because either is two saves in one slot. The table replaced one extension
list plus one `loose_emulator`, which is the trap #152 recorded: adding mesen's loose `.sav`
would have given it `libretro:battery` and collided with libretro's `.srm` for the same ROM.
mesen, mednafen, jgenesis and ares are still reported, each waiting on a rule of its own.

**The grain is per emulator, decided** (`docs/PLAN.md`, 2026-09-21): libretro's cores share one
battery save, and no save migrates between emulators, even where the bytes happen to load. Do not
split a slot by core or merge two emulators' slots without a new decision.

**BizHawk names a battery save after its own title for the game** (`named_after: display
name`), so the filename join cannot match: `StarTropics (USA).zip` wrote
`bizhawk/StarTropics.SaveRAM`, and `Phantasy Star (Brazil).zip` wrote `Phantasy Star (B).SaveRAM`,
so no stripping rule recovers it. `DisplayNameAttributor` asks two routes every scan: the state
sidecar, which RetroBat writes as `<title>.<core>` (strip only the state's own core, since a title
can hold a dot), and the newest launch of the system **under the same emulator** covering the
mtime. The binding is cached in `game_id_binding` keyed on the **file name**
(`StarTropics.SaveRAM`), because that table's CHECK refuses `/` and `:` in a key.

- **The cache is an answer, not a short cut.** A title is not unique to a ROM, and if two
  regions share one, BizHawk keeps one file for both. Re-asking the routes is what lets a launch
  of the second ROM disagree with the binding the first taught, which fails closed as contested
  until `saves bind` settles it. A binding a person made is honoured as the settlement.
- **Two sidecars naming one title for two ROMs is contested too, not first-wins.** The class C
  sidecar index is first-wins because two ROMs sharing a game code are a revision pair; two ROMs
  sharing a title share a **file**.
- **A download is placed only under a learned title.** `ResolveTarget` builds
  `saves/<system>/<rule directory>/<title><ext>`; with no binding for that ROM, or two, the
  operation is failed with its remedy (run the game once under the emulator), in the flush and
  in the restore find alike. The server's tagged name is not used as a fallback: where RomM puts
  its timestamp tag in a name like `Dr. Mario.SaveRAM` is unmeasured.
- **The in-flight guard widens to any running game of the system** for a display-name file,
  because the ROM it is bound to is not the only one that can hold it open.

- **An unchanged file's mtime is not a write.** A restore writes now, so the newest launch
  before that mtime is a session that never touched the bytes: driven, an Ultima launch eight
  days earlier was credited with a restored `StarTropics.SaveRAM` and contested it. The launch
  route is skipped when `local_save` already holds the path with the same hash and a ROM, and a
  session ends at the next launch of anything, since ES runs one game at a time. Finding 265.
- **`save_slot`'s derived destination is the ROM's stem, which is wrong for this rule.** Once a
  slot has been sent, `SaveSlotStore` derived `saves/nes/StarTropics (USA).SaveRAM` and
  `ResolveTarget` took it before asking the rule. It now derives nothing for a slot a
  subdirectory rule owns. Finding 266.
- **BizHawk leaves `<title>.SaveRAM.bak` on exit**, the save the new one replaced. A rule
  declares its own `not_a_save_extensions` for that. Finding 264.

**Driven on both cores on 2026-09-21** (findings 262 to 266, `docs/platforms/nes.md`): upload,
restore into the file BizHawk loads, the in-flight deferral, the launch route alone, and the
Europe copy of StarTropics sharing the USA copy's file, contested and then settled by
`saves bind`. Whether `NesHawk` and `quickerNES` read each other's `.SaveRAM` is **not**
measured; they share the file and the `bizhawk:battery` slot.

Class C is keyed by **Game ID** (`UCUS98751`, a PS3
`TITLEID`, a GameCube disc ID). This design was built around **RomM storing no serial, title ID
or product code anywhere**, so that no API lookup existed to ask.

**That held at the 5.2.0 floor and stopped holding at 5.3.0**, which is now the floor. RomM
declares `title_id`, `save_target` and `save_target_layout` as ROM columns, and **measured on a
live library it answers for the systems this repo reads 0% of**: 3 of 4 psx `.chd`, 4 of 4 ps2
`.chd`, 4 of 4 psp `.cso`, 4 of 4 ps3, 3 of 3 each for 3ds, dreamcast, xbox and xbox360. The
route this design was built around not existing does exist. It is still a **fourth route, not a
replacement**: it is gated on `TITLE_ID_EXTRACTION_ENABLED`, it answers only for rows scanned
since the feature landed, and the rule below about asking every route is what it joins.

**Three facts about the field decide how to use it, and none are obvious from its name.**

- **`save_target` is computed from `title_id`, not equal to it.** Xbox `MS-100` has a
  `save_target` of `4D530064`, which is `ascii("MS")` plus `100` as a 16-bit hex number; ps2
  `SCUS-97472` becomes `BASCUS-97472`, the folder PCSX2 creates; 3ds splits the id in half and
  lower-cases the tail. **Read `save_target` with `save_target_layout`, never `title_id`**, when
  the question is where a save lives. `title_id` is what came out of the binary.
- **Where both routes answer they agree exactly.** Route 2 reads `head[0x58..0x5C]` as four ASCII
  bytes; RomM stores the same four hex encoded. All 1,601 distinct GameCube ids on a real library
  decode to printable `A-Z0-9` codes, `47553459` being `GU4Y`. So this route corroborates rather
  than competes, which is what the disagreement rule needs to be worth anything.
- **A serial is not unique per ROM and is not meant to be.** On a GameCube library scanned end to
  end, 167 ids are shared by 359 of 1,793 rows. A third of that is the library rather than the
  field: multi-disc releases stored as loose files are a row per disc, where one folder per game
  would be one row with several files, and folding them back leaves 101 groups over 222 rows
  (104 over 232 with the committed probe's fold, `r5-gamecube-title-ids.py`, finding 2).
  Both kinds are right. Disc 1 and Disc 2 share a memory card, and a revision does not move the
  player's save. **Plan for the larger number**, because a loose multi-disc library is ordinary
  and this client does not get to require otherwise. The first-wins rule below already covers it
  and already gives this as the reason. **Never use a serial to identify a ROM**; that is what
  the hash is for.

**The route runs both ways.** `PUT /roms/{id}/identity` takes the same triple from a client under
scope `roms.write`, described upstream as identity "a client extracted for a ROM that RomM cannot
read itself". Its extractor answers nothing for Switch (encrypted, left out deliberately), PSN
`.pkg` content, or a Vita `.zip`, and this repo reads GameCube at 100% and Wii at 75.5%, so the
two cover different ground in both directions. Writing back is not built and is not assumed; it
is recorded here so the next session does not re-derive that the endpoint exists.

See finding 2 of [romm-5.3-findings.md](../../../docs/romm-5.3-findings.md) and #168.

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
  row down until something writes into it in place (see "Other writers on the same slots"), and `autocleanup_limit=10` is what bounds the slot rather than the resolution bounding
  it at one. Never tell a user their copy replaced the server's. Measurement 160.
- An unregistered `device_id` is a **404**, not a request that quietly proceeds without a device.
  **Omitting it altogether is accepted**, and produces a save attributed to no device, which is
  how a second device is simulated on an install that has only one. So the 404 bounds
  impersonation, not participation. Measurement 162.
- 409 means the slot moved. Surface it; retry with `overwrite=true` only after resolution.
  **The body is a bare string** with no save id and no timestamps, so fetch the save row if
  you want to show the user what they are conflicting with. It fires when **this device's**
  sync record is stale, not when the save is newest overall.
- **Restore cannot be built on `negotiate`, and this is measured.** The server answers negotiate
  from its own per-device sync record, not from what the client claims. A save this device
  uploaded and acknowledged comes back `no_op`, "No changes since last sync", **even with the
  file deleted from the tree and even when the slot is claimed with `content_hash: null`**. So
  the one save a restore exists for is precisely the one negotiate will never offer.

  Two things that are easy to get backwards here. An **empty** negotiate does return work: 21
  operations on the measured install, each reading "Save exists on server but not on client".
  Every one of them was for a ROM **not** installed, which is the ordinary
  `skipped, for games not synced here` line and is correct. And enumerating absent slots into
  the request adds nothing, because the server was already volunteering everything it believed
  the device lacked.

  `saves restore` therefore walks `GET /api/saves` and filters locally. Unfiltered on purpose:
  the parameters are `rom_id`, `platform_id`, `device_id` and `slot`, none of which takes a
  list, and a save exists only where someone played, so one request returned 55 rows against a
  96,000-ROM library.

  **It is never automatic.** A save that reappears because a flush decided it should is
  indistinguishable from a bug to whoever deleted it deliberately, so finding is separate from
  restoring and `--apply` is required for either.

  **The find scans the tree before it reads `local_save`.** A row already held is not a
  candidate, and the store is only the tree as of the last scan, so a save deleted by hand stayed
  held and the first `saves restore` after the loss offered nothing. Measured on `nes`: the same
  command offered it once `saves` had run in between (#147). The scan lives in
  `SaveSync.FindRestorableAsync` rather than in the subcommand, states first for the #64 reason,
  so every caller of the find gets a true store. It costs a full save scan, the one `saves` pays
  on every run.

  **The find applies every guard the flush's download applies, at the same decision point.** A
  restore reaches `DownloadAsync` with a null local save, which is the same state an unsolicited
  negotiate download arrives in, so a rule written into the flush and not into the find is a rule
  the restore path does not have. The class C guard is the one that bites: with the ROM installed
  and no local unit, a `ppsspp:savedata` row resolves a target, passes `File.Exists` and takes the
  **class A** path, where a null `content_hash` skips verification entirely and the archive lands
  as `saves/psp/<stem>.zip`. Listing it in a preview is already a claim that it can be brought
  back, so it is named with a reason instead, the same reason the flush gives.

  **The write half holds `TreeLock` and refuses rather than skipping.** `saves restore --apply` is
  a third holder beside `saves resolve` and `evict`'s sweep, and for the same reason: it does
  `MoveAside`, `File.Move` and `Saves.Record` over the files a flush is concurrently hashing and
  uploading, and the ES `quit` hook spawns a detached `background quit` flush that nobody sees. A
  flush treats a held lock as success because somebody else is doing its work; nobody is doing a
  restore's, so a failed acquire is `Refused` with an exit code. The `partial/save-<id>.part`
  write is **not** the part that needs it: it is opened `FileShare.None`, which is what producers
  outside the lock rely on. The tree write is.

- **A server save can carry no slot at all**, written by a client that sets none, and
  `local_save.slot` is `CHECK`ed non-empty. Keying that as the empty string threw SQLite error
  19 **after the bytes were on disk**, leaving a save in the tree with no row behind it and
  aborting the rest of the batch. Derive a slot instead. **Null is not the only value that does
  this**: the CHECK refuses the empty string and whitespace the same way, `local_save.emulator`
  has its own, and `SaveScanner.SlotFor` throws on a blank emulator before a CHECK is reached, so
  the test is blankness rather than nullness on both columns. It reaches further than it looks:
  a restore covers **any** installed ROM, not only this device's own uploads, so the values
  arriving are other clients' free text. The derived value is provisional: the
  next scan re-keys a loose save to the loose emulator, measured as `fceumm:battery` becoming
  `libretro:battery`, which is the scanner being authoritative and costs one correction with no
  duplicate row.

- **States download too now, and nothing verifies them.** `StateSchema` carries no hash field of
  any kind, where a save carries `content_hash`, and states have no `/track`, no `/downloaded`
  and no negotiate participation. So a state arrives unverified and the command says so on the
  preview as well as after; that is the ceiling of the API rather than a shortcut. Finding 246.

  **A restored state is named after the ROM on disk, never after the server row.** RomM strips
  parenthesised groups as tags, so `Legend of Zelda, The (USA) (Rev 1) [libretro.nestopia].state1`
  reads back as `Legend of Zelda, The`. Writing the server's name puts a state where the emulator
  never looks, and it then reads as absent rather than as an error. Finding 247.

  **The slot is read out of the uploaded name, not the extension.** `StateSync.SentNameFor`
  strips the scope group back off `file_name`, the template matches what is left, and
  `SaveStateTemplate.FileFor` renders it onto the local ROM's stem. Taking only the extension
  worked for libretro, dolphin, gopher64 and mupen64, and placed nothing for the eight emulators
  that write the slot into the stem (`Game.QuickSave2.State`, `Game_0.jst`, `Game.01.p2s`) or a
  libretro autosave (`Game.state.auto`): every one reported "could not tell which slot it is".
  A name without the group, another client's, falls back to the extension. Finding 258.

  **A restore writes a `local_state` row, or the next flush sends back what it just fetched.**
  `RestoreAsync` records with `uploaded_content_hash` equal to what is now on disk, because both
  sides hold the same bytes. Without it the next scan reads the file as never sent, `NeedsUpload`
  is true, and the upsert on the server accepts every re-send without complaint. This hits every
  state that came from another device, because such a state never had a local row to begin with;
  the same-device case fails too as soon as any flush has run while the file was absent, since
  `ForgetMissing` drops the row. Proven by the no-op re-sync assertion: restore, scan, push, zero
  uploaded.

  **A restored state brings its screenshot only when the server links one** (#158). The find
  carries the id from the state row's `screenshot` field, and the write fetches
  `GET /api/screenshots/{id}/content` into the emulator's declared `<image>`, named from the ROM
  on disk through the template for the reason the state is. Best-effort, like the upload: a fetch
  that fails costs a line and the state still counts as restored, and an image already in the
  tree is left alone. **Both halves were RomMBat's.** A state whose image RomM stored and did not
  link reads `screenshot: null`, and there is nothing to follow, so that state comes back without
  one. Every libretro-shaped state uploaded before finding 258 is in that position and stays
  there, because an unchanged state is not re-sent. So does a
  linked one for an emulator whose `<image>` is its `<file>`, DeSmuME, which has nowhere to put
  it; the find keeps the id either way, so the preview says per row which of the three it is. `DetailedRomSchema.user_screenshots` might reach such an orphan
  by name, and that is unmeasured, so it is not built on.

  **The version rule is a statement here, not a comparison, and saying so is the whole of it.**
  `save-sync` and `PLAN.md` both say never silently restore a state made by a different emulator
  version. Neither side can perform that check: `ScopeOf` uploads `emulator[.core]` with no
  version, and `StateScanner.ReadEmulatorVersion` declines on every emulator measured, so the
  local column is null too. So the command prints the ceiling on the preview and before applying,
  and `RecordRestored` leaves `emulator_version` null rather than stamping the build that happens
  to be installed now. Stamping it would be the exact "a wrong version is worse than no version"
  case the scanner's own remarks warn about.

  **The state half of `saves restore` holds `TreeLock` and refuses, like the save half.** Same
  race, same reason: the ES `quit` hook spawns a detached `background quit` that holds the lock
  across `StateScanner.Scan()` over these same directories. A refusal on the save half also
  skips the state half, or a run that promised "nothing was changed" would still write states.
  A refusal on the state half alone is `Partial` rather than `Refused`, because the saves landed.

  **A failure reading `/api/states` must not take the save restore down with it.** They are
  independent reads. A token whose scopes do not cover the route, or a 500 from it, used to
  return `Offline` before a single save was written. It is now reported and carried, and an
  `--apply` that could not see the state half ends `Partial`. A state it can see and cannot place
  does not, for the rule under "Where the flush passes live".

  **The `<slot>` positional narrows saves only, and the help says so.** A state's slot lives in
  its file extension and shares no namespace with a save's key, so matching one against the
  other would be filtering on a coincidence.

- **A conflict is persisted, not printed.** It goes in `save_conflict` and outlives the flush
  that found it, the local file is copied aside **once per conflict rather than once per
  flush**, and `saves resolve <rom> <slot> --keep-local | --keep-server` ends it. There is no
  default side, because either default silently discards somebody's progress. `--keep-local` is
  the only caller of `overwrite=true` in the codebase; a 409 that survives it means the slot
  moved again between the report and the decision, so it is reported rather than forced. Both
  outcomes prune the copy aside.

  **`overwrite=true` supersedes, it does not replace in place.** Measured on the live instance in
  M7 stage 7b-3: a keep-local on a psp class C unit created a new save row and left the previous
  one standing, one second apart. Confirmed on a second shape, class A on `nes`: a keep-local sent
  save 212 and left 210 standing. The flag is what gets past the 409 an ordinary upload earns
  when this device's sync record is stale; it is not an instruction to the server to reuse the
  row. Anything reasoning about how many rows a slot has after a resolution has to expect two,
  subject to the one-second rule above: a decision taken later than the same second appends, and
  one taken inside it updates the row in place.

- **`ConflictResolutionService` takes `TreeLock`, and refuses rather than treating a held lock
  as done.** It runs the same `SaveUnitTransfer.Restore` a flush does, so two of them at once, or
  one racing `evict`'s sweep of `partial/`, leaves a shared container half swapped. Unlike a
  flush, where failing to acquire means another agent is already doing the work, a person asked
  for this one and silently returning `Ok` would read as having resolved it: it exits `Refused`
  and says why.

  **The rule is Core's, not `saves resolve`'s, and that is a boundary rather than tidiness.** M7
  stage 7b-3 gave the interface a conflict screen, and the UI never referencing `TreeLock` is
  asserted structurally against the built assembly: a flush treats a failed acquire as success,
  so a second caller taking the lock would make a concurrent `background quit` flush skip its
  upload and call it success. `saves resolve` is a shell over the service and keeps only its
  argument parsing and its exit-code mapping.

  **It takes a connection factory, not a connection, and the order is the point.** The lock is
  taken first, because a resolution that cannot run is not worth a round trip to the server. A
  caller handed in an already-open connection would have paid for it before finding out.
  `ConflictOutcomeState` separates `Busy` from `Failed` for the same reason: nothing was tried.

  **A null connection is `NotPaired`, never `Offline`, and the difference is a real install's
  only way out.** `InstallSession.Authenticate` decides it without touching the wire: the two
  returns carrying a null connection are a missing pairing row and a token that will not unlock.
  Calling that offline tells a person whose passphrase-protected store will not open to try
  again when they are back on the network, which will never be true, and it withheld the screen's
  pairing offer at the same time, since that offer was gated on `Failed`. A paired install with
  no `RomMDeviceId` is `NoDeviceId` for the same reason: its message is "Pair again", so it owes
  `ExitCode.NotPaired` rather than the `Partial` a default arm gave it. `Offline` is left for the
  front end that catches `RomMUnreachableException` round the call, which the service itself
  never raises.

- **`partial/unit-<guid>/` is live state for the length of a class C restore, and nothing holds
  a handle on it.** `SaveArchive.Extract` closes each entry's writer inside its own loop, so the
  staging directory sits unprotected across the hash, the copy aside, the `Remove` and the whole
  move loop. Anything that deletes under `partial/` must hold `TreeLock`. **A `FileShare.None`
  sentinel inside the directory is not a substitute**, measured rather than assumed:
  `Directory.Delete(recursive: true)` removes the siblings first and only then fails on the
  sentinel, so the staged members are gone regardless.
- **A server time shown beside a local one has to be read as UTC first.** RomM serialises
  `updated_at`, `server_updated_at`, `created_at`, `start_time` and `end_time` with no zone while
  storing UTC, so a plain `DateTimeOffset` is out by the machine's own offset and the conflict
  block shows the two sides on different clocks. `UtcTimestampConverter` is on every one of them;
  see the `romm-api` skill and finding 260. **Rows written before that fix carry the shifted value** in
  `save_slot.updated_at` and `save_conflict.server_updated_at`, and nothing rewrites them: they
  correct themselves when the slot is next negotiated or the conflict resolved, and they are
  display-only in the meantime, since ordering compares server rows only against each other.
- **Negotiate falls back to `updated_at` wherever the hashes do not settle it, so two of its
  answers have to be overruled here and a third is guarded against.** The repo's own rule is that
  mtime never decides whether a save changed, and this is the server applying that reasoning on
  the other side of the wire. `tools/romm-5.3-probes/s4-older-mtime.py` asks it directly, four
  cases, and is the instrument to re-run rather than reasoning from a flush (#206, finding 259).
  At the `5.3.0-beta.1` floor: M1 `no_op (No changes since last sync)`, M2 `upload`, M3
  `download (Server save is newer (no sync history))`, M4 `no_op (Content is identical)`.
  - **A `no_op` for a slot whose `content_hash` differs from `uploaded_content_hash` is
    uploaded.** That inequality is the client holding evidence the server lacks: this device has
    a change the server has never seen, whatever the timestamps say. The server answers `no_op`,
    "No changes since last sync", for content that differs whenever the local mtime is older than
    this device's own last upload, so a save restored from a backup, copied off another machine
    or extracted from an archive was never sent and the flush said nothing at all. Uploading is
    safe rather than merely better than silence: identical content into one slot reuses the row,
    and a stale device record still answers 409, which lands as a conflict.
  - **An `upload` of bytes the server already holds is a no-op without a round trip, and this
    one is defence rather than a live fix.** Finding 259 measured `1 up` on three consecutive
    flushes for save 336 on 5.3.0-alpha.3, an emulator rewriting a save with identical bytes
    moving the mtime and nothing else. **It does not reproduce at the floor**: M4 answers `no_op
(Content is identical)`, so the hash settles it server-side. Kept because it costs one
    comparison against a value the operation already carries, and because the failure it
    prevents is a silent upload on every flush forever.
  - **That guard must not ask who uploaded the row.** `AlreadyHeld` requires the slot to name
    this device, which is right for a download and wrong for an upload: whether the server holds
    these bytes has nothing to do with who put them there. `AlreadySent` is the upload-direction
    form and drops that test. The first cut shared `AlreadyHeld` and so missed its own headline
    case, caught on the install, where `Legend of Zelda, The (USA) (Rev 1).srm` holds
    `libretro:battery` as save 344 with a **null** `origin_device_id`, because that row came down
    rather than up. Every slot whose current row arrived from a peer or from RomM's browser
    player is in that state.
  - **A `download` over a local save the server has never seen is a conflict** (#211,
    `SaveSync.UnsentLocalWouldBeReplaced`). M3 is the case: a slot this device has no sync record
    for is answered "Server save is newer (no sync history)" whatever the device holds, so two
    devices playing one game offline, the ordinary case for a handheld, had the second one's
    save replaced on its first flush with no conflict, leaving only a copy under `replaced/` that
    nothing points to. The test is the `no_op` rule's, from the other side: an unsent save, or one
    changed since its upload, is evidence the server lacks. Identical bytes download as before.
    A class C unit is a conflict even then, because its fold never equals the server's digest.
    The restore find is untouched: it reaches the download only with no local save.
- **Negotiate returns a download for every save the device has no sync record for**, including
  slots the client did not submit. An **empty** `saves` array came back with 13 downloads across
  two ROMs, one never named by the client, and acking one dropped the next answer to 12. An
  earlier reading of this was backwards: a device that is already current for everything gets no
  operations, which is not the same as nothing being volunteered. **So negotiating with an empty
  array is the fresh-device inventory pass**, and no separate one over `GET /api/saves` is
  needed. A restore onto a device that never held the slot is an ordinary case, not a dead one,
  so **never return early because the local save list is empty**: that is the device with the
  strongest reason to pull. The target for such a slot comes from the ROM's own folder and stem,
  with only the extension read off the operation's tagged filename. **That target is usually a
  file another slot already keeps**, the loose class A save, and writing it was a silent overwrite
  that the next scan then uploaded into the other slot (#205, finding 259: RomM 5.3.0-alpha.3's
  browser files a fresh session under `autosave`, which lands on the `.srm` this device keeps as
  `libretro:battery`). So a download for a slot this device holds no row for, whose destination
  another slot's `local_save` holds, is **recorded as a conflict on the offered slot** and never
  written; `--keep-local` finds its local side by the conflict's path, since the offered slot has no
  row. **When the offered `content_hash` equals the holder's, it is a no-op instead**, or the save
  `--keep-local` just sent comes back offered and reopens the conflict it settled; a plain file hash
  is enough there because the bundled case below was refused first. **A bundled slot is the
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
- **Decide retention.** `autocleanup` defaults to false and `autocleanup_limit` to 10, and a
  writer that leaves them alone gained a row per genuine change forever up to 5.3.0-alpha.2,
  which the `keep_both` conflict default compounds. **This client is not that writer**: every
  save upload sends `autocleanup=true&autocleanup_limit=10`, so its slots have been bounded at
  10 since M6. **From alpha.3 the server prunes every slotted upload whatever the client asks**,
  keeping the newest by `updated_at` then `id` past the **tighter** of `MAX_SAVES_PER_SLOT` (env,
  50 by default, `0` disables) and the client's `autocleanup_limit`. So the cap here stays 10,
  and `MAX_SAVES_PER_SLOT` only ever bites on versions written by something that asks for no
  cleanup: a peer, or RomM's browser player. **Measured against such a writer, and safe as long
  as the upload 409 stays a conflict**: a version this device still names in `save_slot` can be
  deleted under it, negotiate then forgets the history and answers `upload` for an edited copy,
  while the upload guard still holds the device's record and refuses 409, which `SaveSync`
  records as a conflict. An unchanged copy downloads the newest. Finding 11 of
  `romm-5.3-findings.md`, `s3-slot-retention.py`.
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

## The slot is the key to everything, so a save without one is inert

`slot` is an **optional** query parameter on `POST /api/saves`, and a client that omits it
produces a row that RomMBat can neither reconcile nor collide with. Measured end to end on a live
5.2.0 instance:

- Negotiate keys on the slot, so a null-slot save is **never fetched** (#138). **Reported, and no
  slot is derived for it** (ruled): a derived slot may not match what the originating client
  would use, and two clients keying one save differently is worse than a save sitting visible.
  `saves` reads `GET /api/saves` when the install is paired and lists every blank-slot row for a
  ROM on this device, via `SaveSync.FindSlotlessAsync`. It is the one network read in that
  report, so `--offline` skips it and a failed read costs one line and not the exit code.
- It also **cannot conflict**. A save uploaded through RomM's own web UI, which sets no slot, left
  the device's record for `libretro:battery` current, and the next flush uploaded over it with no
  409 and no mention.
- It still **resolves to the same destination path** as the slotted rows for that ROM. So does a
  slot's own history. `saves restore` used to offer every one as its own restore, and applying
  them wrote one file repeatedly and kept whichever came last (#156). **The find now keeps the
  newest row per destination** by `updated_at` then save id, whatever its slot, and the preview
  names the rows it folded and says when a null-slot row and a slotted one share the file. It
  narrows by `<rom> <slot>` before folding, so asking for a slot by name gets that slot's newest
  row even where a newer null-slot row shares the file. **The flush had the same gap for slotted
  rows and it is closed separately**: a negotiated download for a slot this device never held,
  landing on another slot's file, is a conflict and not a write (#205).

So a null slot is not a save in a different slot, it is a save outside the protocol. Never treat
the absence of a conflict as evidence that the server holds nothing newer: it may hold something
newer that the protocol cannot see.

**Staging a conflict therefore takes a slotted upload**, `POST /api/saves?rom_id=<id>&slot=<slot>`,
with `device_id` omitted for the reasons in the protocol rules above, plus a local edit. Nothing
reachable from RomM's UI will do it.

## Every path that writes server bytes writes the slot

`save_slot` holds this device's picture of what the server has in a slot, and every path that
writes server bytes to disk owes it an update:

| Path                                                | Class | Records `save_slot` |
| --------------------------------------------------- | ----- | ------------------- |
| `SaveSync.RestoreUnitAsync`, download               | C     | yes                 |
| `SaveConflictResolver.FinishUnitAsync`, keep-server | C     | yes                 |
| `SaveSync.RecordRestored`, download                 | A     | yes                 |
| `SaveConflictResolver.KeepServerAsync`, keep-server | A     | yes                 |

**The class A rows said no until #157**, and it was measured before it was fixed: after a
negotiate-driven download of save 211 the row still read `save_id 209` with the pre-download hash
while 211's content sat on disk. It does not self-correct, because the local file is then in step
and the slot is never negotiated again. The server-side sync record **is** updated, which is why
nothing visibly broke.

**Fixing the download alone would have left keep-server broken.** They are two independent
writers of `local_save`, not one path with a caller: `KeepServerAsync` does not call the download.
A restore of a save the server holds **with no slot** records no slot identity, because that row
is outside the protocol and its save id would otherwise stand in for a slot it never belonged to.

**The recorded save id is now load-bearing**, which is why this was fixed rather than left
cosmetic: it is what recognises a superseded row returning to the head of its slot. See "Other
writers on the same slots" below.

**Copy aside before overwriting is honoured on the download path too**, not just on conflicts,
which is worth knowing before assuming a download is safe to make silent. A resolution prunes its
copy; a download's copy is currently never pruned. Since #211 a download only ever replaces
bytes this device already sent, so that copy duplicates the server rather than being the last
record of a save.

## Other writers on the same slots

RomM 5.3.0 adds two writers to the saves this protocol was measured against, and a third route
that looks like save transport and is not. Finding 3 and 4 of
[romm-5.3-findings.md](../../../docs/romm-5.3-findings.md) hold the evidence; these are the rules.

**`PUT /api/saves/{id}` rewrites a row in place, and a save id does not name its bytes.** It keeps
the id, the tagged `file_name` and the slot, changes `content_hash`, moves `updated_at`, and runs
no 409 check, no dedup and no device check. At 5.3.0-alpha.2 RomM's browser player sent it for
whichever save it loaded, at Save & Quit and, under 5.3.0's `emulatorjs.auto_save_sync`, **on every
save tick**. **At alpha.3 it no longer touches the save it loaded** (read, not measured; finding 11
of `romm-5.3-findings.md`): a session's first write `POST`s a new version with `overwrite=true`
into the loaded save's slot, or the newest slotted save's, which for a game this client syncs is
this client's slot, and later writes `PUT` only that new row. To this client that is a newer row in
its own slot, so `download` or `conflict`, and the table below is the alpha.2 writer.
**Still true at the beta.1 floor**, re-read there because the writer was rewritten around it
(finding 12): `preferredSlot` is byte-identical and still prefers the newest slotted save over
`autosave`, so the release notes' "ordinary play goes to the `autosave` slot" describes a game
with no slotted save and not one this client syncs. What is new is a screenshot on every save
version, which is inert here because only states carry one on this side. So
compare the hash wherever the question is "is this the save I had", which `save_conflict` already
does and must keep doing. Measured on 5.3.0-alpha.2 with `tools/romm-5.3-probes/s1-browser-save-writer.py`,
which replays the browser's own calls:

| Case                                                                          | Negotiate answers                                        |
| ----------------------------------------------------------------------------- | -------------------------------------------------------- |
| A. browser writes over this device's row, local unchanged                     | `download`, same save id, new hash                       |
| B. the same, local also changed                                               | `conflict`; an ordinary upload is 409                    |
| C. after keep-local, browser writes into the older row, this device synced it | `conflict` against the **older** row                     |
| E. the same, but the older row came from a peer this device never synced      | **`download`**, "Server save is newer (no sync history)" |
| D. no save loaded                                                             | one null-slot row, updated in place, never offered       |

**A superseded row does not stay one row down.** Negotiate pairs on the newest `updated_at` per
slot, and a PUT makes the row it touched the newest, so the copy a keep-local rejected comes back
to the head of the slot. Case E is the one that bites: taking the download would put the rejected
branch over the kept side and say nothing. **So a download naming a save id lower than the slot's
recorded one is recorded as a conflict instead** (`SaveSync.SupersededRowReturned`). Ids only grow,
so a lower id at the head of a slot is an in-place write or a deleted head row. Only the write was
measured to reach a download, and the refusal covers both without telling them apart. Case A, the
same id, is an ordinary download.

**Streaming V2 writes saves no slot can see.** Read at tag 5.3.0-alpha.2, not measured, because
the server measured has streaming off. A session's saves land as a **null-slot** row named
`<rom stem> [<emulator> <timestamp>].saves.zip`, one per pull, deduplicated by hash against every
save for the ROM. Negotiate never offers one (#138). `saves restore` would list one as restorable
to `saves/<system>/<stem>.zip`, because a null slot matches no declared unit, and `--apply` then
fails the hash check, since RomM's digest over an archive is not the MD5 of its bytes. It fails
closed, with a misleading preview.

**Streaming prunes states by emulator name, and the name is shared with this client.** Also read,
not measured. Each capture adds a state row, and past `STREAMING_STATE_HISTORY_LIMIT` (default 50)
the oldest are deleted from every state the user holds for that `(rom, emulator)`, not only the
ones streaming wrote. This client uploads PS2, GameCube and Xbox states under `pcsx2`, `dolphin`
and `xemu`, which are the names streaming uses, so a heavily streamed game deletes this device's
oldest server copies. The local file survives and is never re-sent, because "in step" is decided
from the hash this device recorded. `libretro.<core>` does not collide with streaming's `retroarch`.

**The browser writes states as new rows, and only a same-named manual upload rewrites one this
client holds** (#190, read at 5.3.0-alpha.2, finding 4). The player posts
`<rom> [<timestamp>].state` under the EJS core, and `auto_save_sync` does not touch states. The
console view posts `state.save` under `emulatorjs` every time, so the upsert rewrites one row per
ROM, but that name can never equal this client's `<stem> [<emulator>[.<core>]]<ext>`, and restore
reports both shapes unrestorable. A web-UI upload of a file carrying this client's exact name does
replace that row's bytes and clears its emulator. Nothing notices: `RunAsync` never reads the
server row, so the next local change overwrites it. Do not build a state conflict route on the
strength of this; no player write reaches it.

**Memory card endpoints are not a save transport.** Measured with `s2-memory-card-record.py`: a
card is scoped by `(user, emulator)` with **no ROM**, so it is a class D container by construction;
a version is the whole card, stored byte for byte and not deduplicated; a bare `SRAM.USA.raw` is
refused 400 while a zip holding one is accepted, so the server never checks the layout inside.
Read in source, upstream's Dolphin card is a zip of `.gci` files, the class C shape this client
already syncs per game, and its PCSX2 card is a folder card. Neither gives #82's raw card a home the server
would validate, and interop with a streaming card means rebuilding a whole card, which is two
writers on one container. The direction is the opposite one: steer emulators off shared cards.
PCSX2's `folder` choice is unmeasured here (#80).

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
