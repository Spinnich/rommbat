# RomMBat architecture

How the code is laid out, how the pieces talk to each other, and what the local schema
holds.

[docs/PLAN.md](PLAN.md) is the design of record and says **why**. This file says **where**,
and is the one to read before adding a class. Where the two disagree, the plan wins and
this file needs fixing.

> [!NOTE]
>
> Written at scaffolding time, ahead of the code, and amended as milestones land.
> `docs/retrobat-findings.md` records the measurements that corrected it, and supersedes
> any number quoted here.

---

## 1. The shape of it

```text
                       RomM server  (may be unreachable)
                             |
                   HTTPS, Authorization: Bearer rmm_...
                             |
     +-----------------------+------------------------+
     |           RomM.Client  (net10 library)         |
     |   generated from /openapi.json + hand-written  |
     |   pairing, resumable download, negotiation     |
     +-----------------------+------------------------+
                             |
     +-----------------------+------------------------+
     |                  RomMBat.Core                   |
     |   SQLite: local file index, hashes, cursors,    |
     |   sync sets, OUTBOX (saves/states/sessions)     |
     |   RetroBat root discovery, es_systems reader,   |
     |   platform + save-dir maps, gamelist merger     |
     +------+--------------------------+---------------+
            |                          |
 +----------v-----------+   +----------v---------------+
 |  rommbat-agent.exe   |   |      RomMBat.exe         |
 |  ES hooks: journal   |   |  full-screen gamepad UI  |
 |  only, no network    |   |  pair, sync sets, browse |
 |  flush: when online  |   |  online browse is paged  |
 +----------------------+   +--------------------------+
```

The dependency direction is strict and one-way:

```text
RomMBat.Agent ─┐
               ├─> RomMBat.Core ─> RomM.Client
RomMBat.UI ────┘
```

`RomM.Client` knows nothing about RetroBat. `RomMBat.Core` knows nothing about the console
or the UI. Neither executable talks to the API except through Core. If a type needs to go
the other way, the design is wrong.

---

## 2. Projects

### `src/RomM.Client`

The RomM API, and nothing else. No disk, no SQLite, no RetroBat.

- **DTOs are generated** from `/openapi.json` (served at the root, not under `/api`),
  pinned to a known RomM version and checked in. The published docs have drifted from the
  server, so the backend is the contract. **NSwag**, contracts only: it emits plain POCOs
  with `System.Text.Json` attributes and no runtime package of its own, where Kiota would
  generate a request-builder API over `Microsoft.Kiota.Abstractions` that owns the
  `HttpClient`. Owning the handler is not negotiable here; see the connect timeout below.
  The pin, the normalisation step it needs, and how to move it are in
  [`src/RomM.Client/openapi/README.md`](../src/RomM.Client/openapi/README.md).
- **Everything else is hand-written** over a client-owned handler: the device pairing poll
  loop, resumable downloads, multipart save upload, sync negotiation.
- Every call takes a `CancellationToken`, and **`SocketsHttpHandler.ConnectTimeout` is set
  explicitly on every handler**, because nothing sets it by default and an absent host on
  the local subnet otherwise stalls for 21 seconds (M0 probe 6b). 2 s is the interactive
  budget. `HttpClient.Timeout` is set too, for a different reason: it bounds the body, so it
  cannot be the reachability lever.
- **A timeout and a user cancellation are the same exception type.** Both surface as
  `TaskCanceledException` and differ only in the inner exception, so every failure goes
  through `RomMTransportErrors.Classify` rather than a bare `catch`. A naive catch reports
  every offline server as a user action.
- **Never throws on 401.** An expired or revoked token is an expected state, not an
  exception, and it must never cost data. Authenticated calls return `RomMResponse<T>`
  carrying `Unauthorized` or `Forbidden`; only transport failures throw. See §6.

Prior art to mine, not copy wholesale: the Playnite plugin's `Models/RomM/*` for DTO
shapes and `Downloads/DownloadQueueController.cs` for the queue. Its `RomMRegisterDevice`
carries `mac_address` and `hostname`, which is right for a fixed desktop and **wrong
here**; see §5.

### `src/RomMBat.Core`

Local state, plus everything that knows RetroBat's disk layout. The largest project and
the one with all the interesting invariants.

| Area             | Responsibility                                                                                                                                                                |
| ---------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Root discovery   | Walk up from `AppContext.BaseDirectory` to a marker (`retrobat.ini`, `emulationstation/`, `roms/`). Registry and fixed-path lookups are a last-resort fallback, never primary |
| Path resolution  | The single place a relative stored path becomes an absolute one. Nothing else concatenates a root                                                                             |
| Local store      | SQLite: file index, sync sets, outbox, cursors, learned bindings                                                                                                              |
| RetroBat readers | `es_systems.cfg` (folders and `<extension>`), `es_savestates.cfg` (state schema), `es_features.cfg` (per-game options), `system/version.info` (version)                       |
| RetroBat writers | `gamelist.xml` and `es_settings.cfg`, both merge-not-clobber and atomic                                                                                                       |
| Mapping          | Platform resolution chain, save-directory map, save-shape classification                                                                                                      |
| Sync             | Set resolution, disk budget and eviction, negotiation state machine, outbox flush                                                                                             |

### `src/RomMBat.Agent`

Console executable, published as `rommbat-agent.exe`. Short-lived: one pass, then exit.
There is no daemon, because a portable install cannot register a service or a scheduled
task.

| Subcommand   | Network       | Notes                                                                                 |
| ------------ | ------------- | ------------------------------------------------------------------------------------- |
| `pair`       | yes           | Device pairing. The M1 pairing surface until the UI lands in M7                       |
| `sync`       | yes           | Flush first, then resolve sets, BIOS, content, media, gamelists, and scan saves       |
| `bios`       | only if asked | Report what RetroBat requires under `bios/`, and fetch it with `--apply`              |
| `hooks`      | **never**     | `status`, `install`, `uninstall` the four EmulationStation event hooks                |
| `saves`      | only if asked | What is on disk, what went up, what cannot and why, and what is waiting on a decision |
| `game-start` | **never**     | Append a start record and exit                                                        |
| `game-end`   | **never**     | Close the record. Read the launch facts from `emulatorLauncher.log`, exit             |
| `flush`      | yes           | One pass over everything waiting, then exit. The local half works with no server      |
| `status`     | only if asked | Report local state; probes the server unless `--offline`. For support and for scripts |

All of these are implemented. `saves resolve <rom> <slot> --keep-local | --keep-server` is the
one subcommand that needs the network and a decision from a person, and the only caller of
`overwrite=true` anywhere in the codebase. `saves bind <system> <game id> <rom id>`, and
`--forget`, are the local-only pair that settle or clear a Game-ID binding; nothing else writes
one by hand.

**Nothing invokes `flush` except `sync` and a person typing it.** The hooks write a spool file
and exit without starting a process, and the UI that would drive one is M7. Having a hook spawn
an agent puts an 11 MB process start inside the game-launch path, and that cost has to be
measured on a real install before it is added; the measurement is still outstanding.

`game-start` and `game-end` run inside the game launch path. They must not open a socket and
must not wait on a lock. M0 measured that ES spawns them **fire-and-forget**, so they do not
delay the launch (30 ms from hook to launcher, against an 8 s hook), but they **do run
concurrently**, with each other and across events.

**`game-start` does fire, and the hooks ship as an executable.** An earlier reading here
said ES never fires `game-start` for a name containing a space. M0 probe 7b overturned it:
ES fires the event and logs `executing:` for every script in the folder, and the failure was
**per interpreter**, not per event. A `.bat` never starts once any argument is quoted,
because the `batfile` association is `cmd /c "%1" %*`; a `.ps1` never starts once the name
contains a parenthesis, because ES builds `powershell <script> <args>` with no `-File`. An
`.exe` received all three arguments intact on a real No-Intro name, and on the second host
in probe 7 an `.exe` was the **only** form that ran at all.

So the hooks are the agent executable, and `game-start` is usable. Two things still hold.
`game-end` also fires with **no** preceding `game-start`, including for ES-menu launches and
for launches that failed, so an orphan `game-end` is normal rather than a fault. And the
hook is never told the system, emulator or core, so
**`emulationstation/emulatorLauncher.log` remains the source for the launch facts**. See
`docs/retrobat-findings.md` probes 1 and 7b.

Concurrent invocations are safe: the flush takes a lock file in the tree and a second
process exits rather than queueing. The lock is mandatory, not defensive, because concurrent
hook execution is the normal case.

### `src/RomMBat.UI`

Full-screen, gamepad-navigable, published as `RomMBat.exe`, registered with
EmulationStation through `system/es_menu/*.menu`.

**The UI framework is deliberately undecided and is chosen in M7.** Avalonia
(cross-platform, gamepad-friendly) and WPF (Windows-only, matches RetroBat's own tooling)
are both live. Until then the project is a framework-free placeholder, and no UI framework
package is referenced anywhere in the tree.

That decision is cheap to defer precisely because presentation owns no logic. Set
resolution, mapping, conflict handling and the outbox all live in Core, and the UI
project holds views and view models over them. If something in the UI project cannot be
tested without a window, it is in the wrong project.

No primary flow may require a mouse.

### `tests/RomMBat.Tests`

xUnit, covering Core and Client.

### `tests/RomMBat.Agent.Tests`

xUnit, covering the Agent's subcommands. Its own project rather than a reference added to
the one above, because the Agent is an `Exe` carrying an `app.manifest` and pulling that
into the existing test host would put a Windows manifest behind every unit test in the
repo. `TempRetroBatTree` is linked from `RomMBat.Tests` rather than copied, so both suites
agree on what a RetroBat tree looks like.

**The commands are where milestones meet**, each wiring a planner to a sync to a store to
an exit code, and that is the layer a defect survives a full green suite in. One did:
`BiosCommand` and `SyncCommand` both returned before constructing `BiosSync` when nothing
needed downloading, which made `BiosAction.Adopt` unreachable from either entry point, and
a user who had copied their BIOS in by hand would have been told "N already on disk to
adopt" forever with no row ever written. The planner was covered, the sync was covered, and
the gate between them was neither.

Fixtures come from a real install and are checked in under `tests/**/fixtures/`, byte
exact and excluded from linting. Save-shape and mapping logic without a fixture is not
finished.

The highest-value suite is the **offline simulation**: drive the whole client against a
stubbed handler that can be switched to "unreachable" mid-operation, and assert that every
operation either completes locally or queues, and that a later flush is idempotent under
replay and partial failure. It exists as `OfflineSimulationTests` over
`Support/StubRomMServer`, whose unreachable mode throws exactly what `SocketsHttpHandler`
throws on a connect timeout, because that shape is the thing the code has to tell apart from
a user cancellation.

Anything needing a live RomM calls `Assert.SkipUnless` on environment variables, so a clone
with no server still runs green. Those tests drive the **real** pairing flow headlessly:
`GET /api/auth/device/pending/{user_code}` and `POST /api/auth/device/approve` are ordinary
protected routes, so `Support/ApprovingUser` holds a pre-made token and plays the approving
user. That harness lives in the test project on purpose. Putting approval or token injection
into the shipped client would give it a second auth-adjacent surface, and the whole point of
pairing being the only path is that there is exactly one.

**That token is not a RomMBat token.** `/approve` and `/deny` are `[Scope.ME_WRITE]` routes,
and `me.write` is a scope RomMBat itself never requests, so the harness token needs
`me.read` plus `me.write` and nothing else. Its **account** separately needs the full device
scope set, because `allowed_scopes` is computed from the account's permissions rather than
the token's. See DEVELOPER_SETUP.md section 3.

---

## 3. Reference data and bundled tables

Two different things, easy to confuse.

**`reference/`** vendors upstream files so the numbers in the plan are reproducible
offline. It is an audit trail, not a runtime input. Never hand-edit it, and never resolve
a drift by updating the expected number.

**`data/retrobat/`** holds tables RomMBat actually ships and reads at runtime:

| File                    | Shape                                                | Derived from                                                                             |
| ----------------------- | ---------------------------------------------------- | ---------------------------------------------------------------------------------------- |
| `platforms.json`        | RomM slug to an **ordered list** of RetroBat folders | Seeded from RomM's `config.batocera-retrobat.yml`, corrected against `systems_names.lst` |
| `save_directories.json` | RomM slug to emulator save subdirectories            | M0 experiment 2, in Grout's shape                                                        |
| `save_shapes.json`      | RetroBat system to save class A/B/C/D                | M0 experiment 2                                                                          |

Every one of these is a **seed, not an authority**. The live install always wins: read
`es_systems.cfg` from the actual tree, because RetroBat adds systems every release and
users add custom ones.

---

## 4. The local store

SQLite, inside the RetroBat tree at `emulators/rommbat/rommbat.db`. Settled in M1: every
table below exists from schema version 1, including the ones only later milestones write to,
so each milestone has somewhere honest to write from the moment it starts. Seven have been
added since, by migrations whose headers state what shape could not carry the work. The schema lives
in [`src/RomMBat.Core/Store/Migrations/`](../src/RomMBat.Core/Store/Migrations/).

| Table              | Holds                                                                                                                              |
| ------------------ | ---------------------------------------------------------------------------------------------------------------------------------- |
| `device`           | Singleton: the `client_device_identifier` GUID, server origin, RomM `device_id`, granted scopes, the token                         |
| `local_sequence`   | Singleton: the monotonic counter the outbox and journal share                                                                      |
| `local_file`       | Relative path, resolved folder, `rom_id`, **what kind of file it is**, size, md5/sha1/crc, mtime, last verified, synced or adopted |
| `sync_set`         | Name, scope kind and parameters, policy (max games, max bytes, ordering, eviction)                                                 |
| `sync_set_member`  | Resolved membership per set, with departed members kept so drift between runs is visible                                           |
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
| `content_download` | One interrupted transfer per ROM: its `.part`, its target, the expected length and the validator to resume against                 |
| `sync_cursor`      | Per-endpoint cursors and `updated_after` watermarks                                                                                |
| `clock`            | Singleton: last observed server `Date`, measured skew, round trip, last successful contact                                         |

### No column ever holds an absolute path

Everything is relative to the RetroBat root and resolved at the point of use, because a
drive letter changing from `E:` to `F:` must be a non-event. M0 probe 7 moved a stick
G: to D: to K: across two machines, so this is measured rather than theoretical.

An earlier draft of this document promised a **static check that fails the build**. That is
not what was built, and this is what replaced it, because a Roslyn analyser can only see
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

### How the schema is versioned

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

### Why the sequence number exists

A handheld with a flat RTC produces timestamps that lose every conflict. Each outbox entry
carries a monotonic local sequence number alongside its wall clock, drawn from a counter the
journal shares, so entries in the two are orderable against each other. On first successful
contact, local time is compared against the server response `Date` header; past
`ClockSkew.WarnThreshold` (30 s) RomMBat warns and offers to re-stamp the outbox. Ordering
survives a wrong clock; correctness of the wall clock does not have to be assumed.

Any check for "this timestamp is in the future" carries at least
`ClockSkew.FilesystemTimestampTolerance` (2 s). M0 probe 7 measured FAT32 **and exFAT**
storing mtimes to 2 seconds and rounding **up**, so a freshly written save is legitimately
stamped ahead of the clock that wrote it, and without the tolerance every FAT install would
look like it had a broken clock.

### Why the journal is separate from the outbox

The journal is what the hooks write, on the game-launch path, under a hard time budget.
It is append-only and dumb. The outbox is what the flush reads, and entries land there
after correlation and hashing, which are too slow to do inside a launch. Keeping them
apart is what lets the hook stay honest about its budget.

---

## 5. Identity

Device identity follows the drive, not the host, and the backend has a trap in it.

`POST /api/devices` dedups via `get_device_by_fingerprint`, which matches on
**`mac_address` alone**, then falls back to `ip_address + platform`, then
`hostname + platform`. For a drive that moves between machines that is actively wrong: the
same install would fingerprint differently on each host, and could collide onto a
_different_ RomM client that happens to share a MAC or a DHCP lease.

The pairing path does the right thing already. `POST /api/auth/device/approve` looks the
device up with `get_device_by_client_identifier(user_id, client_device_identifier)` and
records no `ip_address`, `mac_address` or `hostname` at all.

So:

- Generate a GUID **once**, store it in the tree, send it as `client_device_identifier`
  on `POST /api/auth/device/init`.
- Let pairing own device creation. **Never call `POST /api/devices` with host fingerprint
  fields.**
- Re-pairing with the same identifier updates the existing device rather than duplicating
  it, which is what makes "move the drive to another PC" a non-event.

The GUID lives in **`emulators/rommbat/device.id`**, a plain text file, and is mirrored into
the `device` table. The file is the authority on purpose: identity has to outlive the
database, or a rebuilt store would turn into a second device in the RomM UI.

Sync-set definitions persist to the free-form `Device.sync_config` dict via
`PUT /api/devices/{id}`, so a reimaged or re-paired device gets its configuration back and
the config is visible from the RomM web UI.

### The token at rest

**DPAPI is unavailable.** `DataProtectionScope.CurrentUser` binds the ciphertext to one user
profile on one machine and `LocalMachine` binds it to that machine, so either makes the drive
undecryptable on the next PC. M0 probe 7 moved a stick between two machines under two
different Windows users, which is precisely the case that has to keep working.

So the honest position is that **on a portable install the token is only as protected as the
drive**, and that is what the docs say rather than implying otherwise. The mitigations are
the ones RomM's own guidance recommends:

| Default                                                               | Optional                                              |
| --------------------------------------------------------------------- | ----------------------------------------------------- |
| Stored as written inside the tree, with a scoped and expiring token   | AES-GCM under a PBKDF2-SHA256 key from a passphrase   |
| Re-pairing is cheap, so a lost drive is revoked rather than recovered | `--protect` on `pair`; the passphrase is never stored |

The passphrase is a real trade, not a free win: a passphrase-protected install cannot flush
its outbox unattended, because nothing can decrypt the token without someone typing it. The
KDF iteration count is stored with the ciphertext so it can be raised without stranding an
existing database.

---

## 6. Being offline is the normal case

Not an error path. The network is an enrichment, probed with a short-timeout
`GET /api/heartbeat`, never assumed.

| Situation               | Behaviour                                                                                                                               |
| ----------------------- | --------------------------------------------------------------------------------------------------------------------------------------- |
| Server unreachable      | Every operation completes locally or queues. Browse falls back to the local subset and says so                                          |
| Mid-download disconnect | `.part` file survives; the next run resumes with `Range`                                                                                |
| Days offline            | A bigger negotiate payload, nothing more. The protocol is full-state reconciliation                                                     |
| Failed flush            | Replay is safe: sessions dedup on truncated-to-the-second timestamps, saves on `content_hash` within a slot                             |
| Wrong device clock      | Sequence numbers preserve ordering; skew is detected against the server `Date` header                                                   |
| 401                     | Expected. Keep the database and the outbox intact, drop to the pairing screen, resume the flush after re-pairing on the same identifier |
| Conflict                | Normal, not exceptional. Default to `keep_both`, never silently overwrite, always copy aside before any overwrite                       |
| Partial write           | Write `.part`, verify, rename. A half-written file is never visible under its final name                                                |

Retries lean on the server's idempotency rather than on an invented ack protocol.

---

## 7. Writing into someone else's tree

RomMBat writes into a directory RetroBat also owns, and two of those files are rewritten
by EmulationStation on exit. Every writer therefore follows the same discipline: **read,
merge only the fields RomMBat owns, write atomically via temp file plus rename, and never
clobber.**

| File                         | Who else writes it                                      | Rule                                                                            |
| ---------------------------- | ------------------------------------------------------- | ------------------------------------------------------------------------------- |
| `roms/<system>/gamelist.xml` | ES writes back favourite, playcount, lastplayed, hidden | Merge. Only locally present ROMs. Keyed by **resolved folder**, not by platform |
| `es_settings.cfg`            | ES rewrites on exit                                     | Merge. Opt-in and reversible. Write while ES is idle                            |
| `scripts/<event>/*.bat`      | RetroBat ships its own                                  | Append idempotently, never replace. Uninstall cleanly                           |
| Emulator INIs                | `emulatorlauncher` regenerates them every launch        | **Never write these.** Write the RetroBat option instead                        |

Gamelists key by **resolved folder** because the platform mapping is many-to-many: `snes`
and `sfam` can both resolve to `snes`, and several arcade platforms into `mame`. One
gamelist per platform would have the second write clobber the first.

The `es_settings.cfg` precedence chain, from `Program.cs:384-388`:

```text
es_settings.cfg  ->  global.<key>  ->  <system>.<key>  ->  <system>["<rom filename>"].<key>
```

That last form is a genuine per-game override, and it is the lever that turns shared
memory cards into per-game ones without touching an emulator config.

---

## 8. Two authorities that are easy to get backwards

**File extensions come from RetroBat.** RomM will happily hold a file the target system
cannot launch, and syncing it produces the worst failure this app has: a game that appears
in EmulationStation, looks correct, and dies on launch. The `<extension>` list in the live
`es_systems.cfg` is a **sync filter**, applied before anything is downloaded, and
exclusions are shown to the user rather than hidden.

**Firmware requirements come from RetroBat too.** `batocera-systems.json` gives 353 BIOS
entries across 99 systems as `{md5, file}`, with the exact destination path. Join it
against RomM's firmware records on **md5 only**: filenames differ between the two projects,
and RomM's `is_verified` is false on files RetroBat requires, `psxonpsp660.bin` among them,
so on a real library filtering on it discards 6 of the 49 required hashes that library
holds. A further 93 of the 156 have no RomM record at all, which is a gap and not a
flag. BIOS is fetched
**before** that platform's ROMs, because a platform without its BIOS is dead weight in the
gallery.

Two shapes follow from measuring it. **RetroBat does not ship that file**, only a copy of it
inside `batocera-systems.exe`, so the manifest is bundled at `data/retrobat/bios.json` rather
than read from the install. And **179 of the 353 entries carry no md5**, so a BIOS report has
three states rather than two: matched, missing from the library, and unverifiable because
RetroBat names no hash. `bios/` is otherwise a tree RomMBat does not own, holding thousands of
files of emulator user data, so nothing there is overwritten or deleted.

---

## 9. Saves

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
- **So a bundled save carries two hashes and they are never compared.** RomM digests an
  archive's contents too, by a function this client cannot reproduce, so the logical fold is
  the local change detector and the digest the server returned on the last upload is the value
  that goes back on the wire. Sending the fold instead answers `download` forever.

**A conflict is never resolved automatically.** Both sides are kept, the local file is copied
once into `emulators/rommbat/replaced/`, and the slot waits in `save_conflict` until
`saves resolve` picks a side. A conflict is keyed on the server row and not only on its digest,
because a slot returning to contents it once held is a different row carrying a decided hash.
`--keep-local` is the only thing that sends `overwrite=true`, which gets past the 409 and
**appends** rather than replacing: row identity is the server's own datetime-tagged filename at
one-second resolution, so no decision a person takes lands on the row it is overwriting. The
server's copy stays one row down, where negotiate no longer looks, since it pairs on the newest
row per slot alone (measured, not inferred); `autocleanup_limit=10` bounds the slot. Resolving either way
prunes the copy, which is what makes the plan's "keep the previous copy
until the next successful sync" true rather than aspirational.

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
  silently wins. Discovery reverses the `<file>` and `<directory>` templates rather than
  expanding a slot range, which is what makes the four documented traps in that file mostly
  stop being traps.

Attribution for classes C and D is a real problem, because these saves are keyed by Game ID
and RomM stores no serial, title id or product code anywhere. Under `mame` the key is the ROM's
own basename and the join is direct. Everywhere else three routes are asked, **all of them
rather than the first that answers**, because their agreement is the only evidence a binding
has: the launch window `emulatorLauncher.log` records, the `.txt` sidecar RetroBat writes beside
a save state, and the ROM header. The header route reaches GameCube and Wii and nothing else,
measured across five systems on a real library, so it supplements the other two rather than
backing them up.

**Disagreement fails closed, and an absence is not a disagreement.** Two routes naming different
games bind nothing and record the refusal, because picking a side uploads one game's save under
another's name and the cache would then make that permanent; `saves bind` is how a person settles
or clears one. Nothing answering at all is cached nowhere, since the usual cause is that the ROM
has not been synced yet and a stale refusal would outlive its own reason.

---

## 10. Adding something

| You are adding                                   | Start in                                   | Load the skill           |
| ------------------------------------------------ | ------------------------------------------ | ------------------------ |
| An API call                                      | `RomM.Client`                              | `romm-api`               |
| A table or column                                | `Store/Migrations/NNN-*.sql`, never 001    | `offline-and-portable`   |
| Anything reading or writing the RetroBat tree    | `RomMBat.Core`                             | `retrobat-layout`        |
| A platform mapping fix                           | `data/retrobat/platforms.json` plus a test | `platform-mapping`       |
| Save or state handling                           | `RomMBat.Core`                             | `save-sync`              |
| Anything touching paths, the outbox or the clock | `RomMBat.Core`                             | `offline-and-portable`   |
| A new supported platform                         | `docs/platforms/<system>.md`               | `platform-certification` |
| Wrapping up any change                           |                                            | `pre-pr-verification`    |

Ask three questions before writing the class:

1. Does it persist a path? Then it persists a relative one.
2. Does it run on the game-launch path? Then it does not open a socket.
3. Does it need the server? Then it needs a defined behaviour when the server is gone.
