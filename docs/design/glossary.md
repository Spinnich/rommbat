---
summary: The precise meaning of the terms RomMBat's code, docs and skills use in a narrow sense.
read-when: A term such as row, shape, slot, set, floor or certified reads as if it means something specific.
---

# Glossary

Terms used in a narrower sense than everyday English. Where a term has a home that explains it
fully, the entry links there.

## Platforms and certification

**System.** A RetroBat system folder under `roms/`, such as `snes` or `psx`, as `es_systems.cfg`
names it. Not a RomM platform, which has its own slug.

**Platform.** A RomM platform, named by its slug. Mapping one to a system is the job of
`platform-mapping`.

**Row.** One `(system, emulator, core)` combination RetroBat offers, such as `snes` under
`libretro`/`snes9x`. The unit of certification. `core` is empty for an emulator that has no
cores.

**Certified.** A row whose nine checklist steps in `platform-certification` all pass at the
current floor. A row that was driven and does not pass is **recorded**, with its reason, which is
a result rather than a gap. "snes is certified" is not a claim; "`snes` under
`libretro`/`snes9x` is certified" is.

**Wave.** A group of systems certified together, in the order `platform-certification` gives
under "Wave order".

**Hands-on pass.** A person, or the agent driving the install, launching real games in RetroBat
to exercise a path. The test suite does not stand in for one.

**Floor.** The minimum RomM and RetroBat versions a release supports. Older is refused at
startup, newer warns. See `CLAUDE.md`, "Version floor".

## Content

**Sync set, or set.** A named, saved selection of games to mirror onto the device. Its
**scope** is one platform, collection, smart collection, virtual collection or saved filter
(`CatalogScopeKind`). A set is re-resolved on demand, so its membership moves with the library.
The gamepad UI calls the scope **What it holds**.

**Resolve.** Asking the server which games a set holds now. Reaches the network; a sync is a
resolve followed by downloads. The gamepad UI calls it **Check for changes**.

**Budget.** The install-wide cap on bytes RomMBat may place on the device. A sync skips each game
that would go over it and names it. Removing content to make room is always a separate decision a
person makes.

**Unlisted.** A file in a game whose extension the live `es_systems.cfg` does not list for its
system. Reported, never excluded (`CLAUDE.md` rule 3).

**Manifest.** The bundled BIOS manifest RomMBat builds from `batocera-systems.json`: what
firmware each system may read, joined to RomM by md5.

## Saves

**Save shape, or class.** How many files a game's save occupies, which decides how it moves.
**A**: one file per game. **B**: several files per game. **C**: a directory per game. **D**: one
container shared by many games, such as a memory card. A shape belongs to `(system, emulator)`,
never to the emulator alone. See `save-sync`, "The four shapes".

**Unit.** One class C save: everything under a container that carries one key, which on a real
install is often neither one directory nor one file (`SaveUnit`).

**Slot.** The name a save is filed under on the server, such as `libretro:battery`. Negotiation
keys on it, so a server save with no slot is outside the protocol: never fetched, never in
conflict. See `save-sync`, "The slot is the key to everything". The gamepad UI never shows one:
it reads `SaveSlotLabel.Describe`, such as "Battery save" or "Save state 3".

**Conflict.** A slot where this device and the server both changed since they last agreed.
Never resolved silently; a person keeps the local side or the server's.

**Attribution.** Working out which ROM a save belongs to. **Game-ID attribution** does it from
the title id inside the game (class C and some class D saves), and a **binding** caches the
answer per file name.

## Offline and the agent

**Spool.** One file per hook event, written by the hook into the tree and nothing else. The
hook cannot open the database, so this is how an event is recorded.

**Journal.** The table the spool drains into. Each entry is reconciled against
`emulatorLauncher.log`, and what it produces, such as a play session, goes to the outbox.

**Outbox.** Everything produced on the device that the server has not yet accepted: saves,
states and play sessions, each with its real local time and content hash. An entry the server
answers for and refuses is marked failed instead of retried. The gamepad UI calls it **Waiting to
upload**.

**Flush.** One pass that drains the spool and sends the outbox. Safe to replay, because the
server deduplicates what it has already seen.

**Background pass.** A flush started by the `start` or `quit` hook as a detached agent process.
`game-start` and `game-end` never start one (`CLAUDE.md` rule 4).

**Tree lock.** The lock file every writer of save files in the tree takes. A flush that cannot
get it exits, because another process is doing the work. The UI never takes it.

**Pending config.** An `es_settings.cfg` change queued in the store, because EmulationStation
discards a write made while it runs. `background quit` applies it once ES has exited. The
gamepad UI calls these **Pending settings**.
