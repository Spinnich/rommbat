---
summary: The SQLite store: its tables, the migrations, how paths are kept relative, and why the sequence number and journal exist.
read-when: Before adding a migration, a table or a column, or touching the store's connection or ordering.
---

# The local store

**One connection, gated.** Every store class shares a single `SqliteConnection`, which is not
thread-safe, and access is serialised inside the process by a re-entrant gate taken when a
command is created and released when it is disposed. **Closing the connection takes that same
gate, and is the second place that takes it.** The race is real because a sync writes from a
background thread for minutes while the drawing thread reads the same connection on every
redraw. A command must therefore be created and disposed on one thread, which every store method
satisfies by being synchronous. `LocalStore.Dispose` closes under the gate for the same reason:
closing outside it would pull the connection from under a background reader still inside it.

SQLite, inside the RetroBat tree at `emulators/rommbat/rommbat.db`. The schema lives in
[`src/RomMBat.Core/Store/Migrations/`](../../src/RomMBat.Core/Store/Migrations/), one script per
version, and each script's header states what shape the schema before it could not carry. The
tables, as the latest migration leaves them:

| Table              | Holds                                                                                                                              |
| ------------------ | ---------------------------------------------------------------------------------------------------------------------------------- |
| `device`           | Singleton: the `client_device_identifier` GUID, server origin, RomM `device_id`, granted scopes, the token                         |
| `local_sequence`   | Singleton: the monotonic counter the outbox and journal share                                                                      |
| `local_file`       | Relative path, resolved folder, `rom_id`, **what kind of file it is**, size, md5, mtime, last verified, synced/adopted, stale flag |
| `sync_set`         | Name, scope kind and parameters, policy (max games, max bytes, ordering, eviction)                                                 |
| `sync_set_member`  | Resolved membership per set, with departed members kept so drift between runs is visible, and whether RomM serves each as one file |
| `platform_map`     | Resolved folder per RomM platform, and **which layer resolved it**                                                                 |
| `outbox`           | Pending saves, states and play sessions, with real local mtime, content hash and a monotonic sequence number                       |
| `journal`          | Hook events, correlated later against `emulatorLauncher.log`                                                                       |
| `launch_cursor`    | Singleton: how far `emulatorLauncher.log` has been read, as a timestamp rather than an offset, because the file rotates            |
| `local_save`       | One row per save unit on disk, identified by `(relative_path, unit_key)`, with its logical content hash and the last uploaded one  |
| `local_state`      | One row per save state, with its emulator, core, version, screenshot and the name it was uploaded under                            |
| `save_slot`        | The server-side identity of each `(rom_id, slot)`: `save_id`, both filenames, the server hash, the uploading device                |
| `save_conflict`    | Slots where both sides moved, outliving the flush that found them, until a person picks a side                                     |
| `unsyncable`       | What was found under `saves/` and is not being synced, with a reason a user can act on. Rewritten every scan                       |
| `game_id_binding`  | Learned Game ID to `rom_id` bindings for class C and D attribution, with the route that taught each one, or a recorded refusal     |
| `rom_metadata`     | Per selected ROM: the gamelist fields, already converted, and where its media lives on the server                                  |
| `setting`          | Install-wide values the sync-set definitions do not carry: the disk budget, the free-space floor, the media policy                 |
| `content_download` | One interrupted transfer per ROM, or per member of a multi-file one: its `.part`, target, expected length and resume validator     |
| `sync_cursor`      | Per-endpoint cursors and `updated_after` watermarks                                                                                |
| `clock`            | Singleton: last observed server `Date`, measured skew, round trip, last successful contact                                         |
| `save_conversion`  | Which `(system, rom)` RomMBat opted into a per-game save container, what it set, and **what was there before**                     |
| `pending_config`   | Configuration changes waiting for EmulationStation to close, and how each one turned out once applied                              |

`local_save.relative_path` is under `saves/` apart from two folders where an emulator keeps a
save RetroBat does not mirror: gopher64's `n64` battery saves in
`emulators/gopher64/portable_data/data/saves/`, and Kega Fusion's `mastersystem` `.ssm` directly
in `emulators/kega-fusion/`, admitting nothing else there because the same folder holds the
emulator.

## No column ever holds an absolute path

Everything is relative to the RetroBat root and resolved at the point of use, because a
drive letter changing from `E:` to `F:` must be a non-event. RB-391 moved a stick
G: to D: to K: across two machines, so this is measured rather than theoretical.

Three layers hold that, and **none is a static check**, because a Roslyn analyser can only see
literals while the real risk is a runtime value:

1. **A type, not a convention.** `RomMBat.Core.Paths.RelativePath` is the only path shape any
   store API accepts. It rejects rooted, drive-qualified, UNC and `..`-escaping values at
   construction, so an absolute path cannot reach the database through a typed call.
2. **A `CHECK` constraint on every path column**, spelled out in the migration rather than
   generated, so it is visible in the schema an operator can read with `.schema`. This is the
   layer that holds for raw SQL, a hand-edited database, or a future migration that forgets.
3. **A test that binds the two together.** `LocalStoreTests` drives the same table of
   rejected values through both, so the type and the constraint cannot drift apart, and CI
   builds with `-warnaserror` and runs it.

`RetroBatInstall.Resolve` is the single place a stored path becomes an absolute one, and
`RetroBatInstall.Relativize` is the single place an absolute one becomes storable. The
boundary that forces the second is the ES hooks, which receive an absolute rom path in `$1`.

## How the schema is versioned

**SQLite's own `PRAGMA user_version`**, with an append-only list of embedded SQL scripts
applied in order. Each runs inside one transaction that also bumps the version, so an
interrupted upgrade is a no-op rather than a half-applied schema, which matters when the
database is on a stick that can be pulled.

Not EF Core: the schema is hand-written SQL carrying real invariants and nothing here needs
a change tracker or a design-time toolchain. Not a migrations table either: `user_version`
is a single integer in the file header that updates atomically with the transaction that
earned it.

**A database written by a newer RomMBat is refused, not opened.** On a portable drive that
is a real case, not a defensive one: the stick may have been used with a newer build on
another PC.

## Why the sequence number exists

A handheld with a flat RTC produces timestamps that lose every conflict. Each outbox entry
carries a monotonic local sequence number alongside its wall clock, drawn from a counter the
journal shares, so entries in the two are orderable against each other. On first successful
contact, local time is compared against the server response `Date` header; past
`ClockSkew.WarnThreshold` (30 s) RomMBat warns and offers to re-stamp the outbox. Ordering
survives a wrong clock; correctness of the wall clock does not have to be assumed.

Any check for "this timestamp is in the future" carries at least
`ClockSkew.FilesystemTimestampTolerance` (2 s). RB-393 measured FAT32 **and exFAT**
storing mtimes to 2 seconds and rounding **up**, so a freshly written save is legitimately
stamped ahead of the clock that wrote it, and without the tolerance every FAT install would
look like it had a broken clock.

## Why the journal is separate from the outbox

The journal is the raw record of the hook events, one row each, append-only and dumb. The
hook never writes it: on the game-launch path, under a hard time budget, it writes one spool
file and touches no database, and the flush's first step turns each file into a journal row.
The outbox is what the flush reads, and entries land there after correlation and hashing,
which are too slow to do inside a launch. Keeping all three apart is what lets the hook stay
honest about its budget: its whole job is a file write and a rename.
