---
summary: The four save shapes, attribution rules, the flush service, conflicts and save states as the code implements them.
read-when: Before changing save or state scanning, attribution, negotiation, conflicts or restores.
---

# Saves

RomM's `Save` is strictly one file with a `slot` and an MD5 `content_hash`. RetroBat
produces four different shapes, and squeezing them through that model is where this gets
hard.

| Class | Shape                                             | Handling                                                                  |
| ----- | ------------------------------------------------- | ------------------------------------------------------------------------- |
| A     | One file per game                                 | Direct 1:1 map to a `Save`. Slot `{emulator}:battery`                     |
| B     | Several files per game                            | One slot per file when the set is small and stable, otherwise bundle as C |
| C     | Several files under a container, keyed by Game ID | Bundle to a single archive; hash the **contents**, not the archive        |
| D     | One container shared by many games                | Convert to per-game via a RetroBat option, or report as unsyncable        |

**A class C unit is a `(container, key)` pair, not a directory.** Measured on a real install:
`ps3` keeps three directories under one title id, `psp`'s key is a **prefix** of the directory
name (`ULES01513SYSDATA`), and `gamecube` has no per-game directory at all, two `.gci` files
sharing a region folder with every other game on the system. So the path alone is not an
identity, which is what `local_save.unit_key` exists for. Containers are declared in
`data/retrobat/save_shapes.json` and never discovered: hashing an emulator's whole data root
took 426 s where the scoped subtree took 0.06 s.

**Whose a class A or B file is comes from a rule per `(system, emulator)`**, in
`data/retrobat/save_rules.json`: a directory under `saves/<system>/`, its extensions, and what
the stem joins on. gopher64's `n64` rule and Kega Fusion's `mastersystem` rule are the two whose
directory is relative to the RetroBat root instead (`from_root`), since RetroBat leaves those saves
in the emulator's own folder. A path there belongs to a system only when that rule also claims its
name, because Kega's folder holds the emulator and every system's `.srm` beside the `.ssm`. The emulator becomes the slot, so `SaveShapes` refuses at load a table where
two rules could claim one file, or one emulator has two rules on a system unless class B gives
each extension its own slot and no extension is in both. That is what keeps
mesen's loose `Crystalis (USA).sav` from landing in libretro's `libretro:battery` beside
libretro's own `.srm` (#152). Most rules join on the ROM file; mednafen's on `nes` joins on the ROM
file **and the md5 of its content less the iNES header**, which it appends only when the plain
name is free, so a plain `<rom>.sav` there is shared with mesen standalone and a restore refuses to
write a hashed one it would shadow. The flush guards the other direction: for a game ES launches
under mednafen, `RetroBat/LaunchEmulator` names it, and another emulator's save this device has never
held that would land on the plain name stays on the server (#235). `libretro`/`mednafen_gba` on `gba` joins on **the zip, the
file inside it and that file's md5**, `<rom>.zip#<member>.<md5>.sav`, under
`libretro:battery:sav`. Rules claiming one extension in one directory are asked narrowest first,
archive member, then hash, then plain, and two of one narrowness are refused. BizHawk's joins on **its own
title for the game** (`StarTropics.SaveRAM` for `StarTropics (USA).zip`), which
`Content/DisplayNameAttributor` learns from the state sidecar and the launch window and caches
in `game_id_binding` under the file name. A title two ROMs answer to fails closed, and a
download for such a slot is placed only where a title was learned (#151). A clock file beside a
save is class B: on `gb` the loose `.rtc` is `libretro`'s second slot, `libretro:battery:rtc`,
because a clock cartridge keeps its clock there under the stock core, and on `gba` it is Mesen's.
A rule can carry **stem suffixes**, one per memory card port, each with its own slot: DuckStation's
`<title>_1.mcd` and `_2.mcd` on `psx` are `duckstation:battery` and `duckstation:battery:2`, and the
title is the stem less the suffix. mednafen's `psx` hash is of no file but of every disc's table of
contents in playlist order, and a disc of a set answers for the set in `Content/RomIndex`, since
BizHawk names its `psx` files after disc 1. Where two files hold one `(rom_id, slot)`, as a changed
card type leaves, the flush sends the one written last and reports the other as superseded. A PS1
card with no save on it, which an emulator writes on exit regardless, is neither scanned nor
downloaded. On `n64` two more display-name rules join a file to its ROM through the launch window:
RMG's and simple64's shared `sram/<title>-<md5 prefix>.sra`, where a rule can name **other
emulators that write the same file**, and Project64's **directory per game**, where the title is the
directory and the file inside is named with the part before its last `-`. A rule's **title
pattern** keeps two display-name rules carrying one extension on one system from claiming each
other's binding keys.

**The flush is one Core service, not a subcommand.** `Sync/SaveFlushService` composes
`SpoolDrain`, `PlaytimeCorrelator`, `StateScanner`, `SaveScanner`, `OutboxFlush`, `SaveSync` and
`StateSync` and returns a `FlushReport`; `flush` and the sync screen are both printers over it.
Four properties of that pass are rules rather than implementation, and each has a test:

- **The tree lock is taken there and a failed acquire is `FlushState.Skipped`**, an outcome with
  its own sentence rather than an error. Two flushes overlap whenever somebody runs one beside a
  sync, and the second exits rather than waiting, because waiting would put a process to sleep
  inside the game-launch path. It is also what keeps a front end from ever naming `TreeLock`.
- **The local half always runs and only sending needs a link.** Draining, correlating and both
  scans answer from the tree, so a caller that could not authenticate passes no connection and
  still gets all of it.
- **States are scanned before saves and sent last.** The first is #64: the sidecar attribution
  route reads `local_state` and `SaveScanner` runs it, so scanning saves first leaves the route
  reading an empty table. The second is because states are the only part of the pass nobody has
  to act on.
- **No save is written for a game that is being played.** `Sync/InFlightGuard` reads the journal
  and the spool, and a download for a running game becomes `SaveSyncOutcome.Deferred` rather
  than a file: nothing is acknowledged, so the next negotiate offers the same save and the pass
  the `quit` hook spawns lands it. The alternative is what #155 measured, a download landing on
  a file the emulator holds open and being overwritten by the emulator's own copy on exit. The
  guard is per ROM, widened to a container the shape file declares as shared, so a `gamecube`
  launch defers another GameCube game's `.gci` and never an `nes` save. It is widened the same
  way for a file named after an emulator's title, which two ROMs of one system can share.

Three rules that are not obvious:

- **Slots are the pairing key.** Saves pair on `(rom_id, slot)`. A null slot means
  "archival manual upload", is excluded from pairing, and negotiates as `upload` forever,
  piling up duplicates. Always send a stable, non-null slot.
- **The server rewrites uploaded filenames** to `<name> [YYYY-MM-DD_HH-MM-SS]<ext>`.
  Persist the `file_name` from the response, never the one you sent. **Measured: it does not
  do this to a state**, which comes back exactly as sent.
- **Hash the contents, not the archive.** Zip output is implementation-dependent, so
  hashing the bytes would make RomMBat and Grout disagree forever on identical saves.
  Define `content_hash` over sorted relative paths plus each file's own hash. The archive
  is transport only.
- **The fold is RomM's own archive digest**, the md5 of `<entry>:<md5>` lines sorted by name
  (RB-303), so a bundled save carries one hash: it is the local change detector and the
  value negotiated. A restore is checked against the server's value over the archive's raw entry
  names, or the MD5 of the zip for a row written before RomM 5.2.0.
- **Negotiate falls back to `updated_at` wherever the hashes do not settle it, so one of its
  answers is overruled locally.** A `no_op` for a slot whose `content_hash` differs from
  `uploaded_content_hash` is uploaded, because that inequality is the client holding evidence the
  server lacks: otherwise a save put back from a backup never goes up and the flush says nothing.
  The exception, for every shape, is a head that already holds the local bytes: that save is
  recorded as sent, and the head is acknowledged first when it is not the row this device last
  exchanged, since the server refuses the next upload against a stale device record (RB-309,
  probe case M6). RB-259 has the server's other answers, from `s4-older-mtime.py`. A second guard answers an
  `upload` of bytes the server already holds as a no-op; negotiate settles that case on the hash
  itself (probe case M4), so the guard is cheap defence against a silent upload on every
  flush. #206.

  **A `download` over a local save the server has never seen is recorded as a conflict**, the
  same evidence read the other way. RB-259's probe case M3 answers `download` for a slot this
  device has no sync record for whatever it holds, so taken at its word it would replace a
  second device's offline progress on its first flush. An unsent save, or one changed since its
  upload, is kept unless its bytes equal what is offered. #211.

**A conflict is never resolved automatically.** Both sides are kept, the local file is copied
once into `emulators/rommbat/replaced/`, and the slot waits in `save_conflict` until
`saves resolve` picks a side. A conflict is keyed on the server row and not only on its digest,
because a slot returning to contents it once held is a different row carrying a decided hash.
`--keep-local` is the only thing that sends `overwrite=true`, which gets past the 409 and
**appends** rather than replacing: row identity is the server's own datetime-tagged filename at
one-second resolution, so no decision a person takes lands on the row it is overwriting. The
server's copy stays one row down, where negotiate no longer looks, since it pairs on the newest
row per slot alone (measured, not inferred); `autocleanup_limit=10` bounds the slot. **Until
something writes into it, or the row above it is deleted:** `PUT /api/saves/{id}` rewrites a row
in place and moves its `updated_at`, and deleting the row above leaves it the newest, so the
rejected copy can return to the head of the slot either way. The per-slot prune cannot do it,
because it deletes the oldest rows first (RM-11). A
download naming a save id lower than the one this device last recorded for the slot is therefore
recorded as a conflict rather than taken, which is RM-4's case E. `--keep-local` prunes the
copy, since the server keeps the other side. `--keep-server` keeps it, and takes a fresh one
first when a file save moved since the conflict was found, or always for a class C unit, whose
restore copies it aside: the local side never reached RomM, so
the copy under `replaced/` is the only place it survives. Nothing prunes that copy, as with a
download's (#326).

Save states look like the easier half, because `es_savestates.cfg` is a machine-readable
per-emulator schema of directory, filename, screenshot, autosave and slot bounds. Parse it; do
not hardcode. Two things make it less easy than it looks, both measured:

- **States are not in the negotiate protocol.** `POST /api/states` has no slot, no device and
  no conflict detection, and the row it returns carries **no `content_hash`**. So state sync is
  a best-effort push, "in step" is answerable only from a hash the device recorded itself, and
  the `{emulator}:{core}:{slot}` slot is a **local** identity that never goes on the wire.
- **The upsert keys on `(rom_id, file_name)` and the emulator is not part of it.** So the
  uploaded name is not the name on disk: it carries the emulator and core, or two libretro
  cores writing one filename for one game collapse into a single server row and the second
  silently wins. **A screenshot attaches to a state by name alone**, so it is uploaded as the
  state's upload name plus the image's own extension. The name can match another state's image
  as well (libretro slot 0's `.state.png` strips to every slot's name), so a restore follows
  only an image named after its own state. A restore reads the slot back out of the uploaded name
  through the template, because bizhawk, jgenesis and pcsx2 keep it in the stem rather than the
  extension. Discovery reverses the `<file>` and `<directory>` templates rather than
  expanding a slot range, which is what makes the four documented traps in that file mostly
  stop being traps.
- **Declaring no directory is not writing no state.** `mednafen`, `mesen` and `ares` have no
  entry and each writes real states on `nes` into a tree it names itself (#150).
  `StateScanner.LoadSchema` therefore reads the install's file with
  `data/retrobat/es_savestates.supplement.xml` beneath it: the same format plus a `systems`
  attribute limiting each entry to where it was measured, and a `{{romhash}}` token for mednafen's
  content hash, which a restore carries from the uploaded name. `titled_by` names a display-name
  battery rule whose learned title stands in for the ROM's stem, which simple64's `n64` states
  need, and `per_game_directory` puts each game's states in a directory of that title, keeping the
  file name the state was sent under, which Project64's need. An entry in the install's own file
  always wins. An emulator may have one supplement entry per system, because its layout is its
  own per system: ares writes `nes` under `ares/Famicom/` and `megadrive` under `ares/Mega Drive/`.

Attribution for classes C and D is a real problem, because these saves are keyed by Game ID.
Under `mame` the key is the ROM's own basename and the join is direct. Everywhere else three
routes are asked, **all of them rather than the first that answers**, because their agreement is
the only evidence a binding has: the launch window `emulatorLauncher.log` records, the `.txt`
sidecar RetroBat writes beside a save state, and the ROM header. The header route reaches
GameCube and Wii and nothing else, measured across five systems on a real library, so it
supplements the other two rather than backing them up.

**RomM is a fourth route**: it carries `title_id`, `save_target` and `save_target_layout` per
ROM, measured answering for the systems the header route reaches none of. It joins the three
rather than replacing any of them; `save-sync` holds the rules, including that `save_target` is computed from `title_id`,
that neither is unique per ROM, and that GameCube's has two shapes from 5.3.1.

**Disagreement fails closed, and an absence is not a disagreement.** Two routes naming different
games bind nothing and record the refusal, because picking a side uploads one game's save under
another's name and the cache would then make that permanent; `saves bind` is how a person settles
or clears one. Nothing answering at all is cached nowhere, since the usual cause is that the ROM
has not been synced yet and a stale refusal would outlive its own reason.
