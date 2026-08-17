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
expression and matching what is on disk reads the slot off the filename, and three of the four
famous traps in that file stop being questions: `libretro` declares no bounds, `bigpemu`
declares `001`/`999` against a two-digit `{{slot2d}}`, and whether `{{slot}}` renders empty at
slot zero becomes "accept zero digits". The same reversal on `<directory>` recovers the system
and the core from the tree, which is the only sound reading when neither level of the save tree
is positional. `desmume` still needs handling: nothing makes its `<image>` differ from its
`<file>`.

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
- **The declared `<directory>` is wrong for two of the twelve emulators launched.** `flycast`
  writes `dreamcast/reicast/states` against a declared `dreamcast/flycast/sstates`, and
  **`openmsx` writes to `bios/openmsx/savestates/`, a different top-level tree** from the
  declared `saves/msx1/openmsx`. An empty declared directory is never evidence that a game has
  no states; cross-check against the emulator's generated config.
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

## Class D is a configuration problem

PS1 and GameCube are **already per-game in a stock RetroBat** (`duckstation_memcardtype`
defaults to `PerGameTitle`; `dolphin_slotA` defaults to GCI folder), and both should be left
that way. Only PCSX2 defaults to a shared card, and `pcsx2_slot1_memory=game` names the card
after the ROM basename, which makes attribution trivial on a single-disc title.

Set these via `es_settings.cfg`, never an emulator INI. See `retrobat-layout`. The per-game
key is `<system>["<rom filename>"].<key>` and the **filename must keep its extension**; a
bare stem is ignored silently and the emulator keeps writing to the shared container.

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

Caveats, all user-visible: it mutates their config so it is opt-in and reversible;
switching strands existing saves inside the old container unless migrated; and per-game
cards break games that legitimately read a prequel's save.

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

2. **Read the ID from the ROM** (`PARAM.SFO`, disc headers) as a fallback for saves
   predating any observed launch.

## Hash contents, not the archive

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
- **Negotiate never volunteers a slot the client did not submit.** Measured: a device with a
  save on the server negotiated an empty `saves` array and got back no operations at all. It is
  full-state reconciliation over the set the client names, so a fresh device cannot discover its
  saves this way; that needs an inventory pass over `GET /api/saves?rom_id=`.
- Persist the `file_name` the server returns, not the one you sent. **Write a different name
  to disk**: `file_name_no_tags` + `file_extension`. A file called
  `Game [2026-08-10_22-58-26].srm` is invisible to the emulator, which matches on the rom
  name. The server returns the untagged stem, so no client-side regex is needed.
- **Download with `optimistic=false`, then ack.** The parameter defaults to true and records
  the device as current on the request rather than on receipt, so a transfer that dies
  mid-body leaves the server sure the device has a save it does not, and the next negotiate
  answers `no_op`. Send `POST /api/saves/{id}/downloaded` only after the bytes are written
  and verified. Same discipline as M3's `.part`: verify, then commit.
- **Decide retention.** `autocleanup` defaults to false and `autocleanup_limit` to 10.
  Without them a slot gains a row per genuine change forever, and the `keep_both` conflict
  default compounds it.
- Restores are atomic: extract to a temp directory beside the target, verify, swap, keep
  the previous copy until the next successful sync.
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
archive deterministic.

Play sessions replay safely too, and say so: `POST /api/play-sessions` returns a per-index
result array and marks a repeat `"status": "duplicate"`, so a partial flush is reconciled
exactly rather than inferred. It needs no open sync session.
