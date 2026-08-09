# RomMBat architecture

How the code is laid out, how the pieces talk to each other, and what the local schema
holds.

[docs/PLAN.md](PLAN.md) is the design of record and says **why**. This file says **where**,
and is the one to read before adding a class. Where the two disagree, the plan wins and
this file needs fixing.

> [!NOTE]
>
> Written at scaffolding time, ahead of the code. Sections marked **(proposed)** are
> design intent that M0 can still overturn; sections without that marker are settled by
> the plan. `docs/retrobat-findings.md` amends both once M0 lands.

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

- **DTOs are generated** from the instance's `/openapi.json` (served at the root, not
  under `/api`), pinned to a known RomM version and checked in. The published docs have
  drifted from the server, so the backend is the contract.
- **Hand-written where codegen does badly:** the device pairing poll loop, resumable
  downloads, multipart save upload, sync negotiation.
- Every call takes a `CancellationToken` and a short connect timeout. The budget comes
  from M0 experiment 6, which measures how long an unreachable LAN address takes to fail.
- **Never throws on 401.** An expired or revoked token is an expected state, not an
  exception, and it must never cost data. See §6.

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

| Subcommand   | Network   | Notes                                                                                    |
| ------------ | --------- | ---------------------------------------------------------------------------------------- |
| `pair`       | yes       | Device pairing, for headless setup                                                       |
| `sync`       | yes       | Resolve sets, pull content, media and BIOS                                               |
| `game-start` | **never** | Append a start record and exit. Best-effort only: ES does not fire it for most real roms |
| `game-end`   | **never** | The real trigger. Read the launch from `emulatorLauncher.log`, close the record, exit    |
| `flush`      | yes       | Drain the outbox if the server is reachable                                              |
| `status`     | no        | Report local state, for support and for scripts                                          |

`game-start` and `game-end` run inside the game launch path. They must not open a socket and
must not wait on a lock. M0 measured that ES spawns them **fire-and-forget**, so they do not
delay the launch (30 ms from hook to launcher, against an 8 s hook), but they **do run
concurrently**, with each other and across events.

**`game-start` cannot be relied on.** ES never fires it when the gamelist `<name>` contains
a space, which covers nearly every real rom. `game-end` fires reliably, including on crash.
So the journal is built the other way round: **`game-end` triggers, and
`emulationstation/emulatorLauncher.log` supplies the rom path, system, emulator and core**
that the hook arguments omit. See `docs/retrobat-findings.md` probe 1.

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

xUnit, one project for now, covering Core and Client. Split into `<Project>.Tests` when
that stops being comfortable; nothing depends on the current shape.

Fixtures come from a real install and are checked in under `tests/**/fixtures/`, byte
exact and excluded from linting. Save-shape and mapping logic without a fixture is not
finished.

The highest-value suite is the **offline simulation**: drive the whole client against a
stubbed handler that can be switched to "unreachable" mid-operation, and assert that every
operation either completes locally or queues, and that a later flush is idempotent under
replay and partial failure.

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

SQLite, inside the RetroBat tree. **(proposed)** shape, settled properly in M1.

One rule governs the whole schema: **no column ever holds an absolute path.** Everything
is relative to the RetroBat root and resolved at the point of use. There is a static check
that fails the build if an absolute path reaches the database, and it exists because a
drive letter changing from `E:` to `F:` must be a non-event.

| Table             | Holds                                                                                                           |
| ----------------- | --------------------------------------------------------------------------------------------------------------- |
| `device`          | The `client_device_identifier` GUID, the RomM `device_id`, granted scopes, server origin                        |
| `local_file`      | Relative path, resolved folder, `rom_id`, size, md5/sha1, mtime, last verified                                  |
| `sync_set`        | Name, scope kind and parameters, policy (max games, max bytes, ordering, eviction)                              |
| `sync_set_member` | Resolved membership per set, so drift between runs is visible                                                   |
| `platform_map`    | Resolved folder per RomM platform, and **which layer resolved it**                                              |
| `outbox`          | Pending saves, states and play sessions, with real local mtime, content hash and a monotonic sequence number    |
| `journal`         | Launch records: `game-end` hook events joined to `emulatorLauncher.log` lines, plus any `game-start` that fired |
| `game_id_binding` | Learned Game ID to `rom_id` bindings, for class C and D attribution                                             |
| `sync_cursor`     | Per-endpoint cursors and `updated_after` watermarks                                                             |
| `clock`           | Last observed server `Date`, measured skew, last successful contact                                             |

### Why the sequence number exists

A handheld with a flat RTC produces timestamps that lose every conflict. Each outbox entry
carries a monotonic local sequence number alongside its wall clock. On first successful
contact, local time is compared against the server response `Date` header; if skew exceeds
a threshold, RomMBat warns and offers to re-stamp the outbox. Ordering survives a wrong
clock; correctness of the wall clock does not have to be assumed.

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

Sync-set definitions persist to the free-form `Device.sync_config` dict via
`PUT /api/devices/{id}`, so a reimaged or re-paired device gets its configuration back and
the config is visible from the RomM web UI.

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
against `GET /api/firmware` on **md5 only**: filenames differ between the two projects,
and RomM's `is_verified` misses 94 of the 157 hashes RetroBat requires. BIOS is fetched
**before** that platform's ROMs, because a platform without its BIOS is dead weight in the
gallery.

---

## 9. Saves

RomM's `Save` is strictly one file with a `slot` and an MD5 `content_hash`. RetroBat
produces four different shapes, and squeezing them through that model is where this gets
hard.

| Class | Shape                              | Handling                                                                  |
| ----- | ---------------------------------- | ------------------------------------------------------------------------- |
| A     | One file per game                  | Direct 1:1 map to a `Save`. Slot `{emulator}:battery`                     |
| B     | Several files per game             | One slot per file when the set is small and stable, otherwise bundle as C |
| C     | Directory per game                 | Bundle to a single archive; hash the **contents**, not the archive        |
| D     | One container shared by many games | Convert to per-game via a RetroBat option, or report as unsyncable        |

Three rules that are not obvious:

- **Slots are the pairing key.** Saves pair on `(rom_id, slot)`. A null slot means
  "archival manual upload", is excluded from pairing, and negotiates as `upload` forever,
  piling up duplicates. Always send a stable, non-null slot.
- **The server rewrites uploaded filenames** to `<name> [YYYY-MM-DD_HH-MM-SS]<ext>`.
  Persist the `file_name` from the response, never the one you sent.
- **Hash the contents, not the archive.** Zip output is implementation-dependent, so
  hashing the bytes would make RomMBat and Grout disagree forever on identical saves.
  Define `content_hash` over sorted relative paths plus each file's own hash. The archive
  is transport only.

Save states are the easier half, because `es_savestates.cfg` is a machine-readable
per-emulator schema of directory, filename, screenshot, autosave and slot bounds. Parse
it; do not hardcode. Note that states are **not** part of the negotiate protocol:
`POST /api/states` has no slot, device or conflict detection, so state sync is
best-effort push, tracked locally, and the UI says so.

Attribution for classes C and D is a real problem, because directory saves are keyed by
Game ID and RomM stores no serial or title ID anywhere. The primary route is correlating
with the `game-start` journal that already exists, caching the learned binding; reading
the ID out of the ROM is the fallback.

---

## 10. Adding something

| You are adding                                   | Start in                                   | Load the skill           |
| ------------------------------------------------ | ------------------------------------------ | ------------------------ |
| An API call                                      | `RomM.Client`                              | `romm-api`               |
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
