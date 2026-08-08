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
slot bounds per emulator. See the `retrobat-layout` skill. Derive the RomM slot as
`{emulator}:{core}:{slot}`. Map `<image>` onto the optional `screenshotFile`.

Record emulator, core and version with every state, and never silently restore one made by
a different version. RetroBat's own wiki warns that states break across emulator updates.

## The four shapes

| Class | Shape                          | Examples                                                                 | Handling                                          |
| ----- | ------------------------------ | ------------------------------------------------------------------------ | ------------------------------------------------- |
| A     | One file per game              | RetroArch `.srm`/`.sav`/`.eep`                                           | Direct 1:1. Slot `{emulator}:battery`             |
| B     | Several files per game         | `.srm` + `.rtc`, ScummVM `.s00`-`.s99`                                   | Per-file slots when small and stable, else bundle |
| C     | Directory per game             | PPSSPP `SAVEDATA/<GAMEID>/`, RPCS3, Cemu, Citra, Wii NAND, MAME `nvram/` | Bundle to one archive                             |
| D     | Container shared by many games | PCSX2 `Mcd001.ps2`, Dreamcast VMU                                        | Convert to per-game, see below                    |

## Class D is a configuration problem

PS1 and GameCube are **already per-game in a stock RetroBat** (`duckstation_memcardtype`
defaults to `PerGameTitle`; `dolphin_slotA` defaults to GCI folder). Only PCSX2 defaults to
a shared card, and `pcsx2_slot1_memory=game` names the card after the ROM basename, which
makes attribution trivial.

Set these via `es_settings.cfg`, never an emulator INI. See `retrobat-layout`.

Caveats, all user-visible: it mutates their config so it is opt-in and reversible;
switching strands existing saves inside the old container unless migrated; and per-game
cards break games that legitimately read a prequel's save.

## Attribution

Class A and B match by filename. Class C is keyed by **Game ID** (`UCUS98751`, a PS3
`TITLEID`, a GameCube disc ID), and **RomM stores no serial, title ID or product code
anywhere**, so no API lookup exists.

1. **Correlate with the game-start journal.** The hooks already record which ROM launched
   and when; a save directory touched in that window belongs to it. Cache the learned
   binding. This generalises to every odd case and needs no format parsing.
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
- Compare on `content_hash` first, mtime second: exFAT and FAT32 store coarser timestamps
  than NTFS and mtimes are not bit-stable across filesystems.
- 409 means the slot moved. Surface it; retry with `overwrite=true` only after resolution.
- Persist the `file_name` the server returns, not the one you sent.
- Restores are atomic: extract to a temp directory beside the target, verify, swap, keep
  the previous copy until the next successful sync.
- Never evict a ROM whose saves are still in the outbox.
