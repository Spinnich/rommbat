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

## Map

This file holds the flush and the shapes. The rest is in topic files beside it, by section:

| Section                                                                                                                                           | File                                 |
| ------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------ |
| [Save states: parse, do not hardcode](states.md#save-states-parse-do-not-hardcode)                                                                | [states.md](states.md)               |
| [Somebody else may be writing to the same directory](other-writers.md#somebody-else-may-be-writing-to-the-same-directory)                         | [other-writers.md](other-writers.md) |
| [The other writer is usually the running emulator](other-writers.md#the-other-writer-is-usually-the-running-emulator)                             | [other-writers.md](other-writers.md) |
| [Other writers on the same slots](other-writers.md#other-writers-on-the-same-slots)                                                               | [other-writers.md](other-writers.md) |
| [Attribution](attribution.md#attribution)                                                                                                         | [attribution.md](attribution.md)     |
| [Hash contents, not the archive](attribution.md#hash-contents-not-the-archive)                                                                    | [attribution.md](attribution.md)     |
| [Protocol rules](protocol.md#protocol-rules)                                                                                                      | [protocol.md](protocol.md)           |
| [`device_id` is bookkeeping, never a filter](protocol.md#device_id-is-bookkeeping-never-a-filter)                                                 | [protocol.md](protocol.md)           |
| [The slot is the key to everything, so a save without one is inert](protocol.md#the-slot-is-the-key-to-everything-so-a-save-without-one-is-inert) | [protocol.md](protocol.md)           |
| [Every path that writes server bytes writes the slot](protocol.md#every-path-that-writes-server-bytes-writes-the-slot)                            | [protocol.md](protocol.md)           |
| [Determinism is what makes replay safe](protocol.md#determinism-is-what-makes-replay-safe)                                                        | [protocol.md](protocol.md)           |

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
