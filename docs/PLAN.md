# RomMBat: a RomM ↔ RetroBat companion app

## Context

RetroBat is a Windows retro-gaming distro (EmulationStation + RetroArch + standalone
emulators) with no concept of a remote library. RomM is a self-hosted ROM manager that
already acts as the metadata and file authority for a collection. Today the only way to
get a RomM library onto a RetroBat box is to copy files by hand, and nothing carries
saves, states, or playtime back.

The goal is a companion app that makes RomM the authority and RetroBat the player: pull a
**chosen subset** of ROMs, metadata, media and BIOS down into RetroBat's native folder
layout, and push saves, states and play sessions back up, so the same collection stays
coherent across a RetroBat machine, the RomM web UI, and other RomM clients (Grout on
handhelds, Argosy on Android, the Playnite plugin on desktop).

This is a **new repository**. No changes to `rommapp/romm` are required for v1.

**Name:** RomMBat, a portmanteau of RomM and RetroBat that lands close to "wombat".
Mascot: a wombat. Suggested repo `rommbat`, agent binary `rommbat-agent.exe`,
UI `RomMBat.exe`.

### Decisions already made

| Decision     | Choice                                                                                                                                 |
| ------------ | -------------------------------------------------------------------------------------------------------------------------------------- |
| Architecture | Standalone companion app; integrate via RetroBat's existing folder and script seams                                                    |
| Stack        | C# / .NET 10 (LTS), published self-contained single-file win-x64                                                                       |
| v1 scope     | Full two-way sync (selective library pull + saves/states/playtime push)                                                                |
| UX           | Gamepad-navigable full-screen app launched from ES, plus a headless agent                                                              |
| Auth         | Device pairing only (`/api/auth/device/*`): scan a QR or type the 8-character code. No password entry, no token pasting, no other flow |
| Portability  | Portable-first: lives entirely inside the RetroBat tree, survives a drive-letter change and a move between machines                    |
| License      | GPL-3.0, matching the Playnite plugin and Argosy                                                                                       |

C# was chosen because RetroBat's own tooling (`emulatorlauncher`, `batocera-store`) is
C#, and because the RomM Playnite plugin's DTOs and download queue can be lifted almost
directly.

The runtime version is deliberately not tied to RetroBat's. Publishing self-contained
means RomMBat carries its own runtime, and the agent runs as a separate process that
never shares an assembly with RetroBat, so the only thing a shared version would buy is
source compatibility if code ever moves between the two projects. Against that,
self-contained publishing puts unpatched runtime CVEs inside the shipped binary rather
than on a machine the user can patch, which makes a supported runtime worth more than
the alignment. .NET 10 is LTS and supported to 14 November 2028; .NET 8 falls out of
support on 10 November 2026.

---

## Core principles

These four constraints cut across every milestone. They are not a late-stage polish
pass; they decide the data model, so build to them from M1.

### 1. Offline-first, network-optional

RomMBat will run on handheld Windows gaming PCs that are away from the RomM instance for
hours or days. **Every operation must work with the server unreachable, and reconcile
cleanly on reconnect.**

- The local SQLite database is the source of truth for local state. The network is an
  optional enrichment, probed with a short-timeout `GET /api/heartbeat`, never assumed.
- **ES hooks never touch the network.** `game-start` and `game-end` run inside the game
  launch path; they append to a durable local journal and exit in milliseconds. A
  background agent flushes the journal when the server is reachable.
- Everything produced offline (saves, states, play sessions) lands in an **outbox** with
  its real local mtime and content hash, not its sync time. A week offline is just a
  bigger `POST /api/sync/negotiate` payload; the protocol is full-state reconciliation,
  so it handles this natively as long as the timestamps are honest.
- Retries must be safe. Play sessions dedup server-side on truncated-to-the-second
  timestamps, and save uploads dedup on `content_hash` within a slot, so replaying a
  failed flush is idempotent. Lean on that instead of inventing an ack protocol.
- **Clock skew is a real failure mode.** A handheld with a flat RTC produces timestamps
  that lose every conflict. Record a monotonic local sequence number alongside wall
  clock; on first successful contact compare local time against the server response
  `Date` header, and if skew exceeds a threshold, warn and offer to re-stamp the outbox.
- Conflicts are normal offline, not exceptional. Default to `keep_both`, never silently
  overwrite, and always copy the local file aside before any overwrite.
- Partial downloads must survive power loss: write `.part`, verify, then rename.

### 2. Selective sync, because libraries reach 100,000+ games

Pulling everything is not an option; it would exhaust the RetroBat host. Separate the two
things that "sync" usually conflates:

- **Catalog** (metadata about ROMs) is _never_ mirrored wholesale. When online, browsing
  is a thin paged client over `GET /api/roms` with `search_term` and filters. When
  offline, the browsable set is the locally present subset, which is what ES shows
  anyway. Optionally cache catalog rows for selected sync sets only.
- **Content** (ROM, media and BIOS bytes) is strictly opt-in and bounded by a disk
  budget.

Guardrails that follow from this:

- Never call `GET /api/roms` without `with_char_index=false&with_filter_values=false&with_rom_id_index=false`.
  Each of those sidecars scans the whole library.
- **Never read `rom_ids` off a collection response.** `BaseCollectionSchema.rom_ids` is a
  full `set[int]` and it is present on the _list_ endpoint too, so `GET /api/collections`
  on a large instance returns every membership of every collection in one payload.
  Resolve membership by paging `GET /api/roms?collection_id=` (or
  `smart_collection_id=` / `virtual_collection_id=`) instead.
- Use the `/identifiers` endpoints (`/api/roms/identifiers` and friends) for deletion
  reconciliation rather than re-pulling full rows.
- `gamelist.xml` only ever contains locally present ROMs. A 100k-entry gamelist would
  make EmulationStation unusable.
- Warn before a set resolves to more than a configurable game count or byte size.

### 3. Curation, so the device shows what the user cares about

A 100k library is unnavigable from a couch with a gamepad. The organising abstraction is
a **Sync Set**: a named scope plus a policy.

- **Scope** can be a collection, a smart collection, a virtual collection, a platform, or
  a saved filter over any supported `GET /api/roms` query. Collections are the
  recommended default and the best first implementation, but they are deliberately _one_
  scope type rather than the only mechanism, so users who do not curate collections in
  RomM are not stranded. Useful ready-made filters include `favorite`, `last_played`,
  `has_saves`, `playable`, plus the multi-value `genres` / `franchises` / `companies` /
  `regions` / `player_counts` filters with their `any`/`all`/`none` logic operators.
- **Policy** covers: max games, max bytes, ordering (name, recently added, recently
  played), and eviction rules (keep favourites, keep the last N played, and **never evict
  a game with unflushed local saves**).
- Smart collections are re-evaluated server-side and their membership drifts, so
  re-resolve every set on every sync: new members are added, departed members become
  eviction candidates rather than immediate deletions.
- Persist set definitions into `Device.sync_config` (a free-form dict, writable via
  `PUT /api/devices/{id}`) so a reimaged or re-paired device gets its configuration back
  and the config is visible from the RomM UI.

### 4. Portable-first

RetroBat is designed to run portably, from a USB stick or external drive, moved between
machines. RomMBat must not be the component that breaks that. **The whole app, its
config, and its state live inside the RetroBat tree, and a portable install must survive
a drive-letter change and a move to a different PC.**

- **Nothing outside the tree.** No `%APPDATA%`, no `%LOCALAPPDATA%`, no registry keys, no
  Windows service, no scheduled task, no admin rights, no machine-wide .NET requirement.
  Everything lands under `RetroBat/plugins/rommbat/` (or wherever M0 experiment 4 says is
  idiomatic), including the SQLite database, logs, and the outbox.
- **Never persist an absolute path.** The local file index, sync-set definitions and
  outbox entries all store paths **relative to the RetroBat root**. Resolve to absolute
  only at the moment of use. A drive letter that shifts from `E:` to `F:` must be a
  non-event.
- **Find the root relative to the executable**, walking up from `AppContext.BaseDirectory`
  and confirming with a marker (`retrobat.ini`, `emulationstation/`, `roms/`). Registry
  and fixed-path lookups are a last-resort fallback for a fixed install, never the primary
  path. The ES hook `.bat` files use the same trick RetroBat's own scripts use:
  `%~dp0..\..\..\` relative to the hook's location, as seen in
  `.emulationstation/scripts/start/updatestores.bat`.
- **Device identity follows the drive, not the host.** This is the subtle one, and the
  backend has a trap in it. `POST /api/devices` dedups via
  `db_device_handler.get_device_by_fingerprint`, which matches on **`mac_address` alone**
  (ignoring platform), then falls back to `ip_address + platform`, then
  `hostname + platform`. For a drive that moves between machines that is actively wrong:
  the same install would fingerprint differently on each host, and could collide onto a
  _different_ RomM client that happens to share a MAC or a DHCP lease.

  The device-auth pairing path does the right thing already. `POST /api/auth/device/approve`
  looks the device up with `get_device_by_client_identifier(user_id, client_device_identifier)`
  and **never records `ip_address`, `mac_address` or `hostname` at all**. So: generate a
  GUID once, store it in the tree, send it as `client_device_identifier` on
  `POST /api/auth/device/init`, and let pairing own device creation. Do not call
  `POST /api/devices` with host fingerprint fields.

  Note this contradicts the Playnite plugin's `RomMRegisterDevice` model, which carries
  `mac_address` and `hostname`. That model is correct for a fixed desktop install and
  wrong here. Mine Playnite for DTO shapes, not for this decision.

- **The filesystem may not be NTFS.** A portable RetroBat often lives on exFAT or FAT32,
  which has two consequences that reach into the sync design:
  - **FAT32 cannot hold a file larger than 4 GB.** Plenty of PS2, GameCube and Wii images
    exceed that. Detect the filesystem, and when it is FAT32 either skip oversized ROMs
    with a clear explanation or refuse the sync set outright rather than failing mid-write.
  - **FAT and exFAT store coarser modification timestamps than NTFS** (FAT32 is
    2-second granularity). Any conflict logic that leans on mtime equality will produce
    both false matches and spurious conflicts. Treat `content_hash` as the primary
    comparison and mtime only as an ordering tiebreak, and never assume a round-tripped
    mtime comes back bit-identical.
  - No ACLs and no symlinks on FAT/exFAT, so neither can be part of any design.
- **Token at rest is a real exposure on a portable drive.** DPAPI is the usual answer on
  Windows and it is unavailable to us: `DataProtectionScope.CurrentUser` binds the
  ciphertext to a user profile on one machine and `LocalMachine` binds it to that machine,
  so either choice makes the drive undecryptable on the next PC. Be honest about the
  trade instead of pretending: on a portable install the token is only as protected as the
  drive. Mitigate by defaulting portable installs to a **scoped, expiring** token, offering
  an optional passphrase-derived key for users who want it, and making re-pairing cheap.
  This matches RomM's own guidance, which explicitly lists "infinite-expiry tokens in
  untrusted locations" as an anti-pattern for exactly the lost-or-handed-off device case.
- **No machine-level persistence means no background service.** The outbox flush is driven
  by ES lifecycle hooks (`start`, `game-end`, `quit`) and by the UI when it runs, not by
  anything registered with Windows. Design the agent as a short-lived process that does
  one pass and exits, not a daemon.
- **Windows path length.** A deep portable path plus long ROM names plus `images/`
  siblings can cross `MAX_PATH`. Use long-path-aware APIs and, where needed, `\\?\`
  prefixed paths.

---

## What already exists (do not rebuild)

### RomM already ships a companion-app protocol

All of this is on `rommapp/romm` master today. Verified against the local checkout at
`/home/dustin-minnich/romm`, not just the docs.

| Concern            | Endpoint / file                                                                                 |
| ------------------ | ----------------------------------------------------------------------------------------------- |
| Capability probe   | `GET /api/heartbeat` (unauthenticated, returns `SYSTEM.VERSION`)                                |
| Device pairing     | `backend/endpoints/device_auth.py`, RFC-8628 style                                              |
| Long-lived tokens  | `backend/endpoints/client_tokens.py` (`rmm_` + 64 hex, up to 25/user)                           |
| Device registry    | `backend/endpoints/device.py` (incl. the `sync_config` dict)                                    |
| Save sync protocol | `backend/endpoints/sync.py` (`/negotiate`, `/sessions/{id}/complete`)                           |
| Playtime           | `backend/endpoints/play_sessions.py`                                                            |
| Save/state I/O     | `backend/endpoints/saves.py`, `backend/endpoints/states.py`                                     |
| ES gamelist export | `backend/endpoints/export.py`, `backend/utils/gamelist_exporter.py`                             |
| Platform map       | `examples/config.batocera-retrobat.yml` (168 pairs, a starting point, not an answer: see below) |
| Schema for codegen | `GET /openapi.json` (served at the root, not under `/api`)                                      |

Published references: [Client API Tokens](https://docs.romm.app/latest/developers/client-api-tokens/)
and [Device Sync Protocol](https://docs.romm.app/latest/developers/device-sync-protocol/).
**The docs have drifted from the code** (they show `roms:[{saves:[...]}]` for negotiate and
`mac`/`paths` on device create; the real payloads are `saves:[...]` and
`mac_address`/`sync_config`). Generate the client from `/openapi.json` and treat the
backend as the contract.

### RetroBat's integration seams (no fork needed)

| Seam                                                  | Use                                                       |
| ----------------------------------------------------- | --------------------------------------------------------- |
| `roms/<system>/`                                      | Where ROMs land; folder names come from `es_systems.cfg`  |
| `roms/<system>/gamelist.xml`                          | Metadata ES reads directly                                |
| `roms/<system>/images`, `videos`, `manuals`           | Media siblings ES expects (per the RetroBat wiki)         |
| `saves/`                                              | Emulator save output                                      |
| `bios/`                                               | BIOS/firmware, flat at the root with few exceptions       |
| `emulationstation/.emulationstation/scripts/<event>/` | ES event hooks; RetroBat already drives these with `.bat` |
| `system/es_menu/*.menu`                               | How RetroBat registers launchable apps in the ES menu     |

ES events include `start`, `game-start`, `game-end`, `game-selected`, `system-selected`,
`quit`, `shutdown`, `sleep`, `wake`, `update-gamelists`. RetroBat ships
`.emulationstation/scripts/start/updatestores.bat` and
`.emulationstation/scripts/update-gamelists/updatestores.bat`, which proves the `.bat`
path works.

RetroBat's **Content Downloader is not an extension point.** It is an XML feed of
`<repository><name/><url/></repository>` pointing at static content packages with no
lifecycle or config surface, and the repository list ships with RetroBat. Keep it in mind
as a possible distribution channel later, not as the mechanism.

### Reference implementations to mine

| Source                                                           | Take                                                       |
| ---------------------------------------------------------------- | ---------------------------------------------------------- |
| `rommapp/playnite-plugin` `Models/RomM/*`                        | C# DTOs for rom/platform/collection/device/pairing         |
| `rommapp/playnite-plugin` `Downloads/DownloadQueueController.cs` | Concurrent download queue with progress                    |
| `rommapp/grout` `cfw/batocera/data/platforms.json`               | RomM slug → ES folder list, the exact shape to copy        |
| `rommapp/grout` `cfw/*/data/save_directories.json`               | RomM slug → emulator save subdirectory list                |
| `rommapp/grout` `cache/save_sync.go`, `cache/background_sync.go` | Sync state machine and conflict handling, already proven   |
| RomM `backend/utils/gamelist_exporter.py`                        | Authoritative field list for the `<game>` elements to emit |

---

## Project setup

### Built by an LLM, so the repo scaffolding is load-bearing

Development is primarily Claude Code driven, which makes the agent-facing documentation a
first-class deliverable rather than an afterthought. Both upstream projects are unusually
mature here, so copy their structure rather than inventing one, with RomM taking
precedence where the two differ.

Write these before M1, not after M8:

| File                        | Model it on                           | Contents                                                                                                             |
| --------------------------- | ------------------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| `CLAUDE.md`                 | RomM's root `CLAUDE.md`               | Stack table, directory map, repo-wide rules, quick command reference, and a skills index pointing at the files below |
| `.claude/skills/*/SKILL.md` | RomM's `.claude/skills/`              | One per problem domain, loaded on demand                                                                             |
| `DEVELOPER_SETUP.md`        | RomM's                                | Windows toolchain, how to point at a RomM instance, how to stand up a throwaway RetroBat                             |
| `CONTRIBUTING.md`           | RomM's                                | Including RomM's **AI-assistance disclosure** norm, which matters more here, not less                                |
| `docs/ARCHITECTURE.md`      | RomM's `docs/BACKEND_ARCHITECTURE.md` | Project layout, the sync state machine, the local schema                                                             |
| `docs/retrobat-findings.md` | new                                   | The M0 measurements, treated as source data the code cites                                                           |

Suggested skills, mirroring how RomM splits its own:

- `romm-api` - auth and pairing, the endpoints, the traps (sidecar flags, collection
  `rom_ids`, save slots, the server renaming uploads), and how to regenerate from
  `/openapi.json`.
- `retrobat-layout` - the folder tree, `es_systems.cfg`, `es_savestates.cfg`,
  `es_settings.cfg` precedence, hook `.bat` conventions, and the rule that RetroBat
  options are written, never emulator INIs.
- `platform-mapping` - the slug divergence, the layered resolution chain, how to add or
  correct a mapping.
- `save-sync` - the four save shapes, slot derivation, attribution, bundling and hashing.
- `offline-and-portable` - the outbox, relative paths, clock skew, filesystem constraints.
- `platform-certification` - the per-platform checklist below, run for each new platform.
- `pre-pr-verification` - directly analogous to RomM's: the checks that must pass before
  claiming done.

Two rules worth stating in `CLAUDE.md` explicitly, because both are easy for an agent to
get wrong and expensive to unwind: **never edit an emulator INI** (write the RetroBat
option), and **never persist an absolute path**.

### Development environment

Dev happens on Windows, which is the target platform anyway. RomM does not run comfortably
there and does not need to: point the client at an existing instance over the LAN.

- **Primary RomM: the existing stable instance (~85,000 games).** This is a genuine asset,
  not a compromise. It makes the M0 scale probe free and real, and it is the only honest
  way to validate the selective-sync design, since a seeded dev library would never
  reproduce the behaviour that motivated it.
- **Treat it as production, because it is.** Use a dedicated non-admin user account on
  that instance with its own scoped token and its own registered device, so RomMBat's
  writes (devices, `sync_config`, saves, play sessions) never touch the primary account's
  data. Reads are unrestricted; anything destructive belongs elsewhere.
- **Keep a disposable RomM for write-heavy and destructive tests**, via Docker on the dev
  machine or the existing Linux VM. Conflict resolution, overwrite paths, token expiry and
  revocation all want a server that can be reset.
- **RetroBat is trivially disposable.** It is portable by design, so keep a pristine
  extracted copy and clone it per test run. That is also the cleanest way to test the
  portable-move requirement and the first-run install path.
- Pin the OpenAPI schema: generate from a **known RomM version** and check the generated
  output in, so an upstream deploy cannot silently change the contract mid-session.

### Version compatibility is declared, checked, and visible

Every RomMBat release states the minimum RomM and RetroBat versions it supports. Start at
**RetroBat 8.2** (current: `build.ini` carries `retrobat_version=8.2.0`) and **RomM 5.1.0**.

- Read the RomM version from `GET /api/heartbeat` (`SYSTEM.VERSION`) at startup and the
  RetroBat version from `build.ini` in the tree.
- Below minimum: refuse with a clear message naming both versions. Above but untested:
  warn and continue.
- Gate features on version rather than assuming, so a newer RomM adding a field does not
  break an older client and vice versa.
- Consider Grout's versioning convention, which solves exactly this problem: the first
  three components of the client version track the required RomM version, and the fourth
  is the client's own patch number. It makes compatibility legible from the release tag
  alone.
- Keep a compatibility table in the README and treat adding a row to it as part of
  shipping.

---

## M0: spike first, before writing the app

Seven experiments on a real RetroBat install. Each can reshape a later milestone. Record
findings in `docs/retrobat-findings.md`.

1. **ES script hook arguments on Windows.** Batocera documents `game-start` as `$1` rom
   path, `$2` basename, `$3` system, `$4` emulator, `$5` core, and `game-end` as taking
   none. Confirm what RetroBat's ES actually passes to a `.bat`, **whether the hook
   blocks game launch and for how long**, and whether `game-end` fires on crash and on ES
   exit. Write an echo-to-log `.bat` in each event folder and capture the output. The
   blocking answer sets the hard budget for the hook path.
2. **Save file locations and shapes.** Map, per system, where RetroBat's emulators
   actually write saves, and classify each into the four shapes in M6 (one file, several
   files, directory per game, shared container). Produce `data/retrobat/save_directories.json`
   in Grout's shape plus `save_shapes.json` for the classification. Parse the shipped
   `.emulationstation/es_savestates.cfg` and confirm its templates match what the
   emulators really write, including the `libretro` core-scoped directory. Confirm the
   stock per-game defaults hold in practice (DuckStation `PerGameTitle`, Dolphin GCI
   folder), that setting `<system>["<rom>"].<key>` in `es_settings.cfg` survives an ES
   restart and is honoured by `emulatorlauncher`, and what Flycast does with Dreamcast
   VMUs, which is the one class-D case still unverified.
3. **Library refresh.** Determine how to make ES pick up newly added ROMs without a full
   restart, and whether writing `gamelist.xml` while ES is running is safe (ES may
   overwrite on exit). Check the `update-gamelists` hook and the `-updatestores` pattern
   in `emulatorlauncher.exe`.
4. **Install discovery and app registration, portable-first.** Confirm that walking up
   from `AppContext.BaseDirectory` to a marker (`retrobat.ini`, `emulationstation/`,
   `roms/`) reliably locates the root on both a portable and a fixed install, and find
   the idiomatic place inside the tree for a third-party tool to live. Confirm the
   minimum viable `system/es_menu/*.menu` entry, and whether it tolerates a **relative**
   executable path. Confirm the `%~dp0..\..\..\` pattern works from a hook `.bat`.
5. **Scale probe.** Point the client at the largest available RomM instance and measure:
   `GET /api/roms` page latency with the sidecar flags off versus on, the size of a
   `GET /api/collections` response, and how large a `gamelist.xml` EmulationStation can
   load before browsing degrades. These numbers set the default page size, the sync-set
   warning thresholds, and the per-system gamelist cap.
6. **Offline behaviour of the host.** Confirm what happens to a running sync when Wi-Fi
   drops mid-download, and how long a `GET /api/heartbeat` to an unreachable LAN address
   takes to fail. That timeout is the budget for every reachability check in the UI.
7. **Portable move test.** Install to a USB drive, pair, sync a couple of games, then
   change the drive letter and plug the drive into a second machine. Nothing may break:
   not root discovery, not the local file index, not the ES menu entry, not the hooks,
   not the device identity. Record the drive's filesystem and its mtime granularity, and
   note whether RetroBat itself stores any absolute paths that would constrain us.

---

## Architecture

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

### Projects

- `RomM.Client` - API client. Generate DTOs from `/openapi.json` (NSwag or Kiota), then
  hand-write what codegen handles badly: the device pairing poll loop, resumable
  downloads, multipart save upload, sync negotiation. All calls take a cancellation token
  and a short connect timeout.
- `RomMBat.Core` - local state and everything that knows about RetroBat's disk layout.
  Owns the SQLite schema, the outbox, sync-set resolution, and the gamelist merger.
- `RomMBat.Agent` - console exe. Subcommands: `pair`, `sync`, `game-start`, `game-end`,
  `flush`, `status`. The `game-*` subcommands are journal-only and must never open a
  socket.
- `RomMBat.UI` - full-screen gamepad-navigable app. Avalonia (cross-platform,
  gamepad-friendly) or WPF (Windows-only, matches RetroBat tooling). Pick in M7.
- `*.Tests` - xUnit.

Everything the app owns lives inside the RetroBat tree: binaries, SQLite database, logs,
outbox, and the device identity file. Nothing is written outside it, and no persisted
record contains an absolute path. See core principle 4 for token-at-rest handling, which
cannot use DPAPI.

---

## Milestones

### M1: pairing, identity, and the local store

**Device pairing is the only supported authentication path.** No password entry, no
token pasting, no `/api/token` OAuth flow, and not the `POST /api/client-tokens/exchange`
route either. A gamepad is a terrible keyboard, and the pairing flow exists precisely so
the credential never has to be typed.

- `POST /api/auth/device/init` (unauthenticated) with `{client_device_identifier, name,
client, platform, client_version, requested_scopes}` → `{device_code, user_code,
verification_path, verification_path_complete, expires_in: 600, interval: 5}`.
- Display two things: the `user_code`, and a QR encoding the **configured server origin
  joined with `verification_path_complete`**. The server deliberately returns a relative
  path (`/pair/device?user_code=...`) and stays origin-agnostic, so joining is the
  client's job. Use QRCoder (MIT, no native dependency).
- The code is **8 characters from `ABCDEFGHJKMNPQRSTUVWXYZ23456789`**, not 8 digits as the
  published docs claim. The alphabet excludes I, L, O, 0 and 1 to kill ambiguity. Display
  it grouped (`ABCD-EFGH`) for readability: the server runs `normalize_user_code`, which
  strips hyphens and spaces and uppercases, so grouping costs nothing.
- Poll `POST /api/auth/device/token` with `{device_code}` at the returned `interval` (5s),
  honouring `authorization_pending`, `slow_down`, `access_denied` and `expired_token`.
  Init is rate limited 10/min/IP, token 60/min/IP, plus per-code pacing. The pending state
  lives only in Redis with a hard 600s TTL, so the UI needs a visible countdown and a
  one-button restart when it lapses.
- `client_device_identifier` is a GUID generated once and stored in the tree. It is the
  device's identity, deliberately not the MAC or hostname, so it travels with the drive.
  Re-pairing with the same identifier updates the existing device instead of duplicating it.
- Store the returned `access_token` (`rmm_...`) and `device_id`. Approval creates the
  `Device` row and the bound `ClientToken` atomically, so `POST /api/devices` is not
  needed on this path, and avoiding it is what keeps identity host-independent.
- **Handle being granted less than you asked for.** The approving user picks
  `approved_scopes` in the web UI and can narrow them, and `/token` returns the granted
  set. Degrade by feature rather than failing, and say so in the UI instead of throwing
  403s at the user later.

**Document the scopes precisely, so nobody over-grants.** A user staring at an approval
screen with no guidance will either tick everything or tick too little. Publish this table
in the README and mirror it on the pairing screen itself, naming the exact feature each
scope buys and what breaks without it:

| Scope                                | Needed for                                        | Without it                        |
| ------------------------------------ | ------------------------------------------------- | --------------------------------- |
| `roms.read`                          | Browsing and downloading ROMs                     | Nothing works                     |
| `platforms.read`                     | Platform list, folder mapping                     | Nothing works                     |
| `collections.read`                   | Collection and smart-collection sync sets         | Only platform and filter scopes   |
| `firmware.read`                      | BIOS sync                                         | BIOS must be copied manually      |
| `assets.read`                        | Pulling saves and states down                     | Push-only                         |
| `assets.write`                       | Pushing saves and states up                       | Pull-only, local saves stay local |
| `devices.read` / `devices.write`     | Device identity, sync negotiation, roaming config | No save sync at all               |
| `roms.user.read` / `roms.user.write` | Play sessions, last-played, favourites            | No playtime tracking              |
| `me.read`                            | Reading own account details during pairing        | Pairing fails                     |

Explicitly call out what RomMBat **never** needs, since these are the dangerous ones and
the pairing screen is where people grant them by accident: `users.read`, `users.write`,
`roms.write`, `platforms.write`, `tasks.run`, `logs.read`. A token that carries any of
these is over-scoped for this app. Note also that a token can never exceed its owner's own
scopes, so an over-granted token usually means an admin paired it rather than a
purpose-made account.

- **An expired or revoked token must never cost data.** Approval accepts `expires_in`, and
  core principle 4 recommends defaulting portable installs to an expiring token, so 401 is
  an expected state, not an exception. On 401, keep the local database and outbox intact,
  drop to the pairing screen, and resume the flush after re-pairing on the same
  `client_device_identifier`.
- CSRF does not apply when an `Authorization` header is present, so no cookie handling.

**The one thing left to type is the server URL.** Pairing cannot start until the client
knows which origin to call, which is the real gamepad-hostile step. Provide a gamepad
on-screen keyboard, remember the value afterwards, and treat anything that avoids the
typing (an mDNS sweep of the LAN, or reading a URL left in a config file by whoever
prepared the drive) as a worthwhile follow-up rather than v1 scope.

- Stand up the SQLite schema now, including the outbox, the reachability/clock-skew
  bookkeeping, and the rule that every stored path is relative to the RetroBat root, so
  later milestones have somewhere honest to write.

**Done when:** pairing from a cold start, driven entirely by scanning the QR or typing the
8-character code, yields a working token that survives restart and survives moving the
install to a different machine under a different drive letter; the device appears in the
RomM UI as one device rather than two; a narrowed scope grant degrades cleanly instead of
erroring; and every subsequent milestone can be developed with the server switched off.

### M2: catalog browsing and sync sets

- Paged browse over `GET /api/roms?...&limit=<from M0>&offset=` with `search_term` and
  filters, always with the three sidecar flags off. No full-library mirror, ever.
- Sync set model: scope (collection / smart collection / virtual collection / platform /
  saved filter) plus policy (max games, max bytes, ordering, eviction rules).
- Resolve sets by paging `GET /api/roms?<scope>`, **not** by reading `rom_ids` from a
  collection payload.
- Re-resolve every set on every sync so smart-collection drift is picked up.
- Persist set definitions to `Device.sync_config` via `PUT /api/devices/{id}`.
  **Done when:** a user can define "my SNES favourites, max 40 games, 8 GB" and see exactly
  which games it resolves to, without the client ever holding the whole library in memory.

#### Platform mapping is a feature, not a lookup table

The two projects' platform vocabularies genuinely diverge, and treating this as "invert
the shipped YAML" would fail on roughly a third of a real install. Measured against
`system/configgen/systems_names.lst` on `RetroBat-Official/retrobat` and
`UniversalPlatformSlug` in `backend/handler/metadata/base_handler.py`:

| Fact                                                          | Count  |
| ------------------------------------------------------------- | ------ |
| RetroBat systems                                              | 240    |
| RomM known platform slugs                                     | 457    |
| Explicit pairs in `examples/config.batocera-retrobat.yml`     | 168    |
| **RetroBat systems with no mapping (37%)**                    | **91** |
| Of those, resolved by case/punctuation normalization alone    | 16     |
| Still unresolved after normalization                          | 75     |
| Shipped mappings pointing at folders RetroBat no longer lists | 19     |
| RomM slugs mapping to **more than one** RetroBat folder       | 13     |

Concretely: the YAML says `astrocde`, `bbc`, `ps` and `segacd` where RetroBat's own list
says `astrocade`, `bbcmicro`, `psx` and `megacd`. Normalization catches easy drift like
`actionmax` → `action-max` and `ti99` → `ti-99`, but only 16 of 91. And the relation is
many-to-many in the write direction: `arcade` alone maps to ten RetroBat folders
(`mame`, `fbneo`, `naomi`, `model2`, `model3`, `triforce`, `atomiswave`, …), while
`amiga` covers `amiga500`/`amiga1200`/`amiga4000`.

Most of the 75 hard-unresolved names are RetroBat **ports and launchers** with no RomM
platform by design: `cavestory`, `devilutionx`, `eduke32`, `ecwolf`, `gemrb`, `opengoal`,
`lowresnx`, plus storefront systems like `steam`, `gog`, `epic` and `amazon`. The rest is
genuinely missing hardware: `chihiro`, `dragon32`, `cassettevision`, `gaelco`, `gx4000`,
`neogeo64`.

**There is no authoritative source to lean on.** I checked the obvious candidates:
`platform.libretro_slug` is a libretro DAT name ("Nintendo - Super Nintendo Entertainment
System"), not a folder, and it over-collapses (Amiga and Amiga CD32 share one value).
`platform.family_slug` is IGDB's _manufacturer_ grouping, so it will not separate
regional twins. Neither can drive folder placement.

So resolve in layers, and make the unresolved remainder a visible, editable surface:

1. **User override** from the mapping table, persisted in `Device.sync_config` so it
   roams. Always wins.
2. **`platform.fs_slug` matched against `es_systems.cfg`.** When someone's RomM library is
   already laid out Batocera-style, `fs_slug` _is_ the RetroBat folder name and no
   translation is needed. Try this before any table.
3. **Bundled `data/retrobat/platforms.json`**, seeded from the YAML but corrected against
   `systems_names.lst` (fix the 19 stale entries) and shaped as slug → **ordered list** of
   folders, following `grout/cfw/batocera/data/platforms.json`. Where a slug maps to
   several folders, the first present in the target's `es_systems.cfg` wins, and the user
   can change it.
4. **Normalized-match suggestion**, offered for confirmation rather than applied silently.
5. **Unmapped.** Not an error. Model two first-class states: _RomM platform with no
   RetroBat folder_ (skip, explain) and _RetroBat system with no RomM platform_ (ignore
   entirely, this is the ports/storefront category).

Ship a **Platform Mapping screen** as core UI, not a settings afterthought: show every
platform in the user's sync sets, its resolved folder, where the resolution came from, and
let them fix it. Grout reached the same conclusion, and its user guide has both
`platform_mapping.png` and `sync_mappings.png` screens. Also read `es_systems.cfg` from the
actual install rather than trusting any bundled list, since RetroBat adds systems every
release and users add custom ones.

Two consequences that reach other milestones:

- **Arcade needs its own decision, not a mapping row.** Which of the ten folders is correct
  depends on the romset the file came from, and arcade ROM names are romset-versioned. For
  v1, require an explicit user choice per arcade sync set and do not guess.
- **Two RomM platforms can legitimately share one folder** (a user may point both `snes`
  and `sfam` at `snes`). See M4, which must therefore key gamelist generation by folder
  rather than by platform.

#### File extensions come from RetroBat, never from RomM

RomM will happily hold a file the target system cannot launch. Syncing it produces the
worst failure mode this app has: a game that appears in EmulationStation, looks correct,
and dies on launch. So the accepted-extension list is a **sync filter**, not a display
detail, and RetroBat is the only authority on it.

`es_systems.cfg` carries it per system, and it is read from the live install rather than
bundled, because it reflects that machine's actual emulator configuration:

```xml
<system>
  <name>snes</name>
  <fullname>Super Nintendo Entertainment System</fullname>
  <manufacturer>Nintendo</manufacturer>
  <hardware>console</hardware>
  <release>1990</release>
  <path>~\..\roms\snes</path>
  <extension>.smc .fig .sfc .gd3 .gd7 .dx2 .bsx .swc .rom .wad .zip .7z</extension>
  <command>...</command>
</system>
```

- Filter every sync-set candidate against the resolved folder's `<extension>` list, using
  RomM's `fs_extension`, and exclude non-matches from the set before anything is
  downloaded.
- Show the exclusions rather than hiding them: "12 games skipped, format not supported by
  this system" with the offending extensions, so the user can fix it in RomM.
- Watch the disc-image cases in particular, where RomM may hold a `.chd` while the
  configured emulator wants `.cue`/`.bin` or vice versa. This is the most common real
  mismatch and the plan should not pretend conversion is in scope.
- Archives (`.zip`, `.7z`) are accepted by some systems and not others, so honour the
  per-system list rather than assuming archives are universally fine.

The same file also carries `<manufacturer>`, `<hardware>` and `<release>`, which is how
the rollout order below can be derived rather than hand-maintained.

### M3: content sync and the disk budget

- Download `GET /api/roms/{id}/content/{fs_name}` **always sending `Range: bytes=0-`**.
  Single-file ROMs stream via nginx and resume natively; multi-file ROMs only take the
  resumable cached-zip path when a `Range` header is present, otherwise they arrive as a
  non-resumable mod_zip stream.
- Adopt files already on disk: hash local ROMs and match on `md5_hash`/`sha1_hash`, or
  query `GET /api/roms/by-hash`, so an existing library is not re-downloaded.
- Verify by size and hash, but note: for ROMs stored compressed, `crc_hash` is the CRC of
  the _uncompressed_ content, so do not compare it against downloaded bytes.
- Enforce the per-set and global disk budget. Eviction is a first-class operation with a
  dry-run: show what would be removed before removing it, and refuse to evict anything
  with unflushed local saves.
- Reconcile deletions against `GET /api/roms/identifiers`.
- Resume cleanly from `.part` files after a power loss or a Wi-Fi drop mid-download.
- **Detect the target filesystem before writing.** On FAT32, refuse or skip ROMs above
  4 GB with a clear message rather than failing partway through a large write. Removable
  media is also slow and prone to disconnection, so surface throughput and fail gracefully
  on a yanked drive.
- Compare by `content_hash` first and mtime second, since exFAT and FAT32 store coarser
  timestamps than NTFS and a mtime round-trip is not bit-stable across filesystems.

**Done when:** a set syncs to completion, a second run is a no-op **including after the
drive letter changes**, an interrupted download resumes, exceeding the budget evicts
predictably rather than filling the disk, and a FAT32 target refuses oversized ROMs
cleanly.

### M4: metadata and media

- **Key gamelist generation by folder, not by platform.** Because the mapping is
  many-to-many, two RomM platforms can resolve to the same RetroBat folder (`snes` and
  `sfam`, or several arcade platforms into `mame`). Generating one gamelist per platform
  would have the second write clobber the first. Group the locally present ROMs by their
  resolved folder, then emit one merged `gamelist.xml` per folder.
- Write `roms/<system>/gamelist.xml` **containing only locally present ROMs**. Use
  `backend/utils/gamelist_exporter.py` as the field reference: `path` (`./<fs_name>`),
  `name`, `desc`, `image`, `thumbnail`, `marquee`, `video`, `manual`, `developer`,
  `publisher`, `genre`, `family`, `players`, `lang`, `region`, `releasedate`
  (`YYYYMMDDT000000`), `rating` (0-1, 2 decimals).
- Download media into `images/`, `videos/`, `manuals/` next to the ROMs, named after the
  ROM file, per RetroBat's scraper convention. Media counts against the disk budget.
- **Merge, never clobber.** ES writes user edits (favourite, playcount, lastplayed,
  hidden) back into the same file. Read the existing gamelist, update only the fields
  RomMBat owns, preserve the rest, and write atomically via temp file plus rename.
- Generating the gamelist client-side is correct here. RomM's
  `POST /api/export/gamelist-xml` writes into the _server's_ library folders, which is a
  different machine.

**Done when:** ES shows box art, descriptions and videos for synced games, and a user's
manual metadata edit survives the next sync.

### M5: BIOS and firmware

A platform synced without its BIOS is dead weight in the gallery, so firmware is
**prioritised ahead of ROM content** for any platform being synced, and driven by what
RetroBat actually requires rather than by whatever the RomM library happens to hold.

**RetroBat ships the requirements manifest.** `batocera-systems/Resources/batocera-systems.json`
(in `emulatorlauncher`, and present in the tree) is machine-readable and complete: 99
systems, 353 BIOS entries, each `{"md5": ..., "file": "bios/<name>"}` giving both the hash
and the exact destination path. Use it as the requirements source. The wiki's per-system
BIOS pages are prose over the same data and are useful for user-facing text, not for logic.

**Join on md5, and do not trust RomM's `is_verified`.** The two projects' firmware
knowledge overlaps far less than expected. Measured against RomM's
`backend/models/fixtures/known_bios_files.json`:

|                                    |        |
| ---------------------------------- | ------ |
| Distinct md5s RetroBat requires    | 157    |
| Distinct md5s RomM knows           | 353    |
| **Overlap**                        | **63** |
| RetroBat-required, unknown to RomM | 94     |

So 60% of what RetroBat needs will never be flagged `is_verified` by RomM even when the
user has the correct file. The two also key differently: RomM by `platform_slug:file_name`,
RetroBat by destination path. **md5 is the only reliable join.** Filenames will not match
and must not be relied on.

The flow per synced platform:

1. Resolve required BIOS from `batocera-systems.json` for that RetroBat system.
2. List candidates with `GET /api/firmware?platform_id=` and join on `md5_hash`, ignoring
   both filename and `is_verified`.
3. Download matches via `GET /api/firmware/{id}/content/{file_name}` and **write to the
   path the manifest specifies**, renaming as needed. Firmware uses Starlette's
   `FileResponse`, so ranges work but there is no X-Accel path.
4. Skip files already present with the right md5. On a hash mismatch, warn and leave the
   existing file alone rather than overwriting something that works.
5. **Report the gap.** Required BIOS with no md5 match anywhere in RomM is the single most
   useful thing this feature can tell a user, so surface it per platform as "needed, not
   in your library" with the expected filename and hash.

**Done when:** syncing a BIOS-dependent platform lands the right files at the right paths
with no manual copying, files RomM does not have are listed explicitly rather than
failing silently at launch, and BIOS is fetched before that platform's ROMs.

### M6: offline-first save, state and playtime sync

The milestone with the most protocol nuance. Read `backend/endpoints/sync.py` and
`backend/endpoints/saves.py` before writing code.

- **The hooks are journal-only.** `game-start` appends a start record;
  `game-end` closes it. No HTTP, no blocking, no waiting on a lock. Budget from M0
  experiment 1. The hooks resolve the agent through `%~dp0..\..\..\`, never an absolute
  path, so they keep working when the drive letter changes.
- **The flush has no daemon to live in.** A portable install cannot register a service or
  a scheduled task, so the outbox is flushed by a short-lived agent process invoked from
  the `start`, `game-end` and `quit` hooks, and by the UI while it runs. Design for
  one-pass-and-exit, and make concurrent invocations safe with a lock file in the tree.
- **Slots are the pairing key.** Saves pair on `(rom_id, slot)`. Send a **stable,
  non-null** `slot` (for example `retroarch:<system>`). A null slot means "archival manual
  upload", is excluded from pairing, and so negotiates as `upload` forever, piling up
  duplicates on the server.
- `POST /api/sync/negotiate` with `{device_id, saves:[{rom_id, file_name, slot, emulator,
content_hash, updated_at, file_size_bytes}]}` → `{session_id, operations:[{action:
upload|download|conflict|no_op, rom_id, save_id, file_name, slot, emulator, reason,
server_updated_at, server_content_hash}], total_*}`. Send the **real local mtime** as
  `updated_at`, never the sync time, or offline edits silently lose every conflict.
- Upload: `POST /api/saves?rom_id=&slot=&emulator=&device_id=&session_id=&overwrite=` as
  `multipart/form-data` with `saveFile` and optional `screenshotFile`. **The server
  rewrites the filename** to `<name> [YYYY-MM-DD_HH-MM-SS]<ext>`, so persist the
  `file_name` from the response, not the one you sent. A 409 means the slot moved since
  the last sync; surface it and retry with `overwrite=true` only after resolution.
  Uploads are capped at 512 MiB.
- Download: `GET /api/saves/{id}/content?device_id=&session_id=`, then
  `POST /api/saves/{id}/downloaded` with `{device_id}` so the server records the sync.
- Close with `POST /api/sync/sessions/{session_id}/complete` carrying
  `{operations_completed, operations_failed, play_sessions:[...]}`.
- **States are not part of the negotiate protocol.** `POST /api/states` takes only
  `rom_id` and `emulator`, with no slot, device or conflict detection. Treat state sync as
  best-effort push, tracked locally, and say so in the UI.

#### Saves come in four shapes, and RomM's model only fits one

`Save` is strictly one file: `file_name`, `file_path`, `file_size_bytes`, `content_hash`
(32 chars, so MD5), `slot`, `emulator`. There is no directory or multi-file concept in the
API. Everything below has to be squeezed through that, and pretending otherwise is how
this milestone fails. Grout is thin prior art here: its `sync/directory_saves.go` marks
exactly one platform, `psp`, as directory-shaped, because Linux handhelds run a narrow
emulator set. RetroBat runs Dolphin, PCSX2, RPCS3, Cemu, Citra, DuckStation, Flycast,
ScummVM and MAME, so it meets every case.

**Save states are the easy half, because RetroBat hands us a schema.**
`.emulationstation/es_savestates.cfg` is a machine-readable, per-emulator description of
exactly where states live and what they are called:

```xml
<emulator name="pcsx2" firstslot="1" lastslot="10" autosave="true" incremental="true">
  <directory>{{system}}/pcsx2</directory>
  <file>{{romfilename}}.{{slot2d}}.p2s</file>
  <image>{{romfilename}}.{{slot2d}}.p2s.png</image>
  <autosave_file>{{romfilename}}.resume.p2s</autosave_file>
</emulator>
```

Parse it rather than hardcoding anything. It yields the directory and filename templates
(`{{system}}`, `{{core}}`, `{{romfilename}}`, `{{slot}}` / `{{slot0}}` / `{{slot2d}}`),
the slot bounds, the autosave names, and an `<image>` per state that maps directly onto
the optional `screenshotFile` on `POST /api/states`. It also gives a stable slot identity
for free: derive the RomM `slot` as `{emulator}:{core}:{slot}`.

Two cautions. RetroBat's own wiki carries a danger notice that states "will break when an
emulator is updated", so record the emulator, core and version alongside every state and
never restore one produced by a different version onto a machine silently. And the
`libretro` entry's directory is core-scoped (`{{system}}/libretro.{{core}}`), so the same
game has independent state sets per core.

**Battery and internal saves are the hard half.** Classify each platform, store it in
`data/retrobat/save_shapes.json` next to the save-directory map, and handle per class:

| Class | Shape                              | Examples                                                                                                                              | Handling                                                                                                           |
| ----- | ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------ |
| A     | One file per game                  | RetroArch `.srm`/`.sav`/`.eep`, most standalone                                                                                       | Direct 1:1 map to a `Save`. Slot `{emulator}:battery`                                                              |
| B     | Several files per game             | `.srm` + `.rtc`, ScummVM `.s00`…`.s99`                                                                                                | Either one slot per file, or bundle as class C. Prefer per-file slots when the set is small and stable             |
| C     | Directory per game                 | PPSSPP `PSP/SAVEDATA/<GAMEID>/`, RPCS3 `savedata/<TITLEID>/`, Cemu, Citra, Wii NAND `title/00010000/<id>/data/`, MAME `nvram/<game>/` | Bundle to a single archive, following `grout/sync/zip_save.go`, which already handles the multi-directory PSP case |
| D     | One container shared by many games | PCSX2 `Mcd001.ps2` (default), Dreamcast VMU                                                                                           | Convert to per-game via a RetroBat option. See below                                                               |

**Class D is a configuration problem, and RetroBat already has the switch.** A shared
memory card holds saves for twenty games, so it cannot be attributed to a `rom_id`. But
the emulators all support per-game virtual memory cards, and RetroBat exposes each as an
option that `emulatorlauncher` reads at launch:

| Emulator           | Option                    | Default                            | Per-game result                                                               |
| ------------------ | ------------------------- | ---------------------------------- | ----------------------------------------------------------------------------- |
| DuckStation (PS1)  | `duckstation_memcardtype` | **already `PerGameTitle`**         | one `.mcd` per game under `saves/<system>/duckstation/memcards/`              |
| Dolphin (GameCube) | `dolphin_slotA`           | **already GCI folder** (`SlotA=8`) | individual `.gci` files under `GCIFolderAPath`                                |
| PCSX2 (PS2)        | `pcsx2_slot1_memory`      | shared `Mcd001.ps2`                | `game` names the card after the ROM basename; `folder` gives a folder memcard |

So PS1 and GameCube are **already per-game in a stock RetroBat**, and the class-D list is
much shorter than it first appears. PS2 needs one option flipped. The earlier framing of
"detect and steer the user" understated what is achievable: RomMBat can make these
platforms syncable itself.

**Set the RetroBat option, never the emulator INI.** `emulatorlauncher` regenerates each
emulator's config from these options on every launch (`Duckstation.Generator.cs`,
`Pcsx2.Generator.cs`, `Dolphin.Generator.cs` all write the INI at launch time), so an INI
edit is clobbered on the next boot. The durable lever is `es_settings.cfg` in the ES home
directory, which `Program.cs:384-388` layers in this precedence:

```text
es_settings.cfg  ->  global.<key>  ->  <system>.<key>  ->  <system>["<rom filename>"].<key>
```

That last form is a genuine **per-game** override, which is exactly the granularity needed.
Writing `ps2["Game (USA).iso"].pcsx2_slot1_memory = game` makes PCSX2 use a card named
after the ROM, and PCSX2's per-game naming then makes attribution trivial: the card is
named after the ROM file, so ordinary class-A filename matching works.

Four things to keep honest about this:

- **It mutates the user's RetroBat configuration**, so it is opt-in, explained, and
  reversible. Never flip it silently.
- **ES owns `es_settings.cfg` and rewrites it on exit**, the same hazard as `gamelist.xml`
  in M4. Merge rather than clobber, write while ES is idle, and write atomically.
- **Switching modes strands existing saves** inside the old shared container, where the
  game will no longer look for them. Either migrate (parse the container and extract the
  per-game saves) or refuse to switch until the user has been warned clearly. Migration is
  real format work and should be scoped explicitly, not assumed.
- **Some games legitimately read another game's save** from the same card (sequel bonus
  detection, the Suikoden and Metal Gear cases). Per-game cards break that by design. Say
  so where the option is offered.

Dolphin's `.gci` files are named by game code rather than ROM filename, so those still need
the attribution route below. Dreamcast VMU handling is unverified and is an M0 item.

Whatever remains genuinely shared after all this is reported as "not syncable, here is
why" rather than silently ignored.

**Attribution is a real problem for classes C and D.** Directory saves are keyed by Game
ID (`UCUS98751`, a PS3 `TITLEID`, a GameCube disc ID), not by ROM filename, and Grout's
own comment says as much. RomM does not help: there is no serial, title ID or product
code anywhere on the ROM model or response schema, so no API lookup exists. Two client-side
routes, in order of preference:

1. **Correlate with the game-start journal.** The ES hooks already record which ROM
   launched and when. A save directory created or modified inside that window belongs to
   that ROM. This reuses infrastructure this plan already requires, needs no format
   parsing, and generalises to every odd case. Cache the learned Game ID → `rom_id`
   binding so it only has to be observed once.
2. **Read the ID out of the ROM** (PSP/PS3 `PARAM.SFO`, GameCube/Wii disc header) as a
   fallback for saves that predate any observed launch.

**Hash the contents, not the archive.** For bundled class C saves, defining
`content_hash` as the MD5 of the zip bytes is a trap: archive output is
implementation-dependent (entry ordering, timestamps, compression level differ between
Go's `archive/zip` and .NET's `ZipArchive`), so RomMBat and Grout would compute different
hashes for an identical logical save and conflict forever, and a library upgrade could do
the same to RomMBat alone. Define the hash over the logical contents instead: sorted
relative paths plus each file's own hash, folded into one digest. Keep the archive purely
as transport.

**Restores must be atomic.** A half-written directory save is a corrupt directory save.
Extract to a temporary directory beside the target, verify, then swap, and keep the
previous copy aside until the next successful sync.

- Play sessions: `{rom_id, save_slot, start_time, end_time, duration_ms}`, at most 100 per
  call, `end_time` strictly after `start_time`, microseconds truncated server-side for
  dedup. A long offline binge flushes in chunks; replaying a failed chunk is safe.
- Optionally `PUT /api/roms/{id}/props?update_last_played=true`. Note there is no
  `is_favorite` and no `playtime` field on rom props; favourites are collection
  membership, and playtime lives entirely in play sessions.
- Install the hook `.bat` files idempotently, appending to any existing scripts rather
  than replacing them, and uninstall cleanly.

**Done when:** with the server unplugged, play three games, exit, plug back in, and all
three saves plus all three play sessions land in RomM in one flush; then play the same
game elsewhere and the newer save comes back down as a conflict the user resolves. Prove
it on one game from **each** save shape, not three class-A games: a RetroArch `.srm`, a
PPSSPP `SAVEDATA/` directory, a PCSX2 save state with its screenshot, and a PS2 battery
save after opting that game into a per-game memory card. Anything still genuinely shared
must report itself unsyncable with an explanation rather than appearing to work.

### M7: gamepad UI

- Full-screen, controller-navigable: pair, manage sync sets, show sync progress,
  conflicts and the disk budget, browse and install individual games.
- **Two browse modes.** Online browse pages the server and supports search. Offline browse
  shows the local subset and says plainly that it is offline. Reachability checks use the
  short timeout from M0 experiment 6 so the UI never hangs on an unreachable LAN.
- Register via `system/es_menu/*.menu` (pattern confirmed in M0) so it is reachable from
  the couch. Follow Grout's screens as a model: login, platform list, platform mapping,
  games list, multi-select, sync summary, settings.
- No primary flow may require a mouse.

### M8: packaging, docs, release

- `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true` so no .NET
  install is needed. RetroBat already requires the VC++ redist; add nothing else.
- **A portable zip is the primary artefact**, extracted into the RetroBat tree, requiring
  no admin rights and touching no machine state. A conventional installer is at most a
  convenience wrapper over the same layout, never the only route.
- Setup writes the ES menu entry and the script hooks with relative paths, appends rather
  than replaces existing hooks, and removal takes the tree back to its prior state.
- Document the portable story explicitly, including the FAT32 4 GB ceiling and the
  recommendation to use exFAT or NTFS for any library containing disc images.
- README, getting-started guide, wombat mascot, and a compatibility table of tested
  RetroBat versions.
- Once it works, open a PR against `rommapp/romm` adding it to the Community section of
  the README, and post in the RomM Discord `#community-projects`.

---

## Platform rollout: certify one at a time

Once the framework exists (M1 through M6 working end to end on a single platform), stop
building horizontally and start certifying platforms one by one. Two reasons this beats a
big-bang approach: the most-used platforms get correct first, and each platform surfaces
its own edge cases in isolation instead of as a pile of intermixed bugs late on.

Certify **per RetroBat system**, not per aggregate. "RetroArch works" is not a claim
anything can be verified against, because each libretro core has its own save naming,
state directory (`{{system}}/libretro.{{core}}`) and BIOS needs.

**Per-platform certification checklist**, one pass each, recorded in
`docs/platforms/<system>.md`:

1. Folder mapping resolves, and by which layer.
2. `<extension>` list captured; a known-unsupported file is correctly excluded.
3. Required BIOS from `batocera-systems.json` resolved against RomM by md5; gaps listed.
4. Save shape classified (A/B/C/D) and battery save round-trips.
5. Save state round-trips including its screenshot, per `es_savestates.cfg`.
6. Where class D applies, the per-game memory card option is verified.
7. A game launches from ES after sync, with art and metadata.
8. Play session recorded and reaches RomM.
9. Re-sync is a clean no-op.

**Suggested order**, 2nd through 6th generation consoles, which is where the usage is and
where the save shapes stay tractable:

| Wave | Systems                                                                                      | Why here                                                                                       |
| ---- | -------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------- |
| 1    | `nes`, `snes`, `gb`, `gbc`, `gba`, `megadrive`, `mastersystem`                               | Class A saves, no BIOS, single files. Proves the spine                                         |
| 2    | `n64`, `psx`, `saturn`, `segacd`, `pcengine`, `pcenginecd`                                   | Introduces BIOS (`psx`, `saturn`, `segacd`) and disc formats                                   |
| 3    | `ps2`, `gamecube`, `dreamcast`, `xbox`                                                       | The hard save shapes: memory cards, GCI folders, VMU                                           |
| 4    | `neogeo`, `neogeocd`, `fbneo`                                                                | Arcade: romset-versioned naming, the ten-folder mapping question, 12 BIOS files for `neogeocd` |
| 5    | `wonderswan`, `wonderswancolor`, `ngp`, `ngpc`, `lynx`, `gamegear`, `atari2600`, `atari7800` | Long tail of the same manufacturers, mostly class A                                            |

Arcade is deliberately last. It is the only wave that needs the explicit
folder-choice decision from M2 and carries romset-version coupling nothing else does.

The order can be derived rather than hand-maintained: `es_systems.cfg` carries
`<manufacturer>`, `<hardware>` and `<release>` per system, so filtering to
`hardware=console` for Atari, Bandai, NEC, Nintendo, Sega, SNK and Sony and sorting by
release year reproduces roughly this list and stays correct as RetroBat adds systems.

---

## Risks and how to defuse them

| Risk                                                                                                                            | Mitigation                                                                                                                                                                                          |
| ------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Device offline for days, then floods the server on reconnect                                                                    | Durable outbox, chunked idempotent flush, exponential backoff, honest local mtimes                                                                                                                  |
| Device clock is wrong, so offline saves lose every conflict                                                                     | Monotonic sequence alongside wall clock; compare against the server `Date` header on reconnect and offer re-stamp                                                                                   |
| 100k-game library overwhelms the host or the UI                                                                                 | Catalog is never mirrored; content is opt-in via sync sets with hard game/byte budgets and eviction                                                                                                 |
| `GET /api/collections` returns every membership of every collection                                                             | Never read `rom_ids` from collection payloads; page `GET /api/roms?collection_id=` instead                                                                                                          |
| Huge `gamelist.xml` makes EmulationStation unusable                                                                             | Only locally present ROMs go in the gamelist; cap per system using the M0 measurement                                                                                                               |
| ES overwrites `gamelist.xml` on exit and loses synced metadata                                                                  | M0 experiment 3; write only when ES is idle, or via the `update-gamelists` / `quit` hooks                                                                                                           |
| Save corruption from a bad conflict resolution                                                                                  | Never auto-overwrite on 409; default to keeping both, copy aside before any overwrite                                                                                                               |
| Eviction deletes a game whose save has not synced yet                                                                           | Eviction is blocked on unflushed outbox entries for that ROM, with a dry-run preview                                                                                                                |
| ES hook slows or hangs game launch                                                                                              | Hooks are journal-only with a hard time budget from M0; all network work happens in the background agent                                                                                            |
| Drive letter changes and every stored path breaks                                                                               | Persist only paths relative to the RetroBat root; resolve to absolute at point of use; M0 experiment 7 proves it                                                                                    |
| Portable install moved to a new PC registers as a second device, or collides with another client                                | Anchor identity on `client_device_identifier` via the pairing flow, which never records MAC/IP/hostname. Never call `POST /api/devices` with fingerprint fields, whose dedup matches on MAC alone   |
| DPAPI-encrypted token is undecryptable on the next machine                                                                      | Do not use DPAPI. Default portable installs to a scoped, expiring token, offer an optional passphrase, make re-pairing cheap                                                                        |
| Pairing-only auth strands a user who cannot reach the web UI                                                                    | Accepted trade. The pairing code is short-lived and re-issuing is one button; document that approving needs a browser somewhere on the network                                                      |
| Token expires mid-session and the outbox is lost                                                                                | 401 is an expected state: keep the database and outbox, return to the pairing screen, resume the flush after re-pair on the same `client_device_identifier`                                         |
| Approver grants fewer scopes than requested                                                                                     | Read the granted set from `/token` and degrade by feature (pull-only, BIOS off) with a visible explanation, never a late 403                                                                        |
| Typing the server URL on a gamepad is the one hostile step                                                                      | On-screen keyboard, remembered after first use; mDNS discovery or a pre-seeded config file as a follow-up                                                                                           |
| FAT32 target silently fails on a ROM larger than 4 GB                                                                           | Detect the filesystem up front; skip or refuse oversized ROMs with an explanation instead of a partial write                                                                                        |
| Coarse FAT/exFAT mtime granularity causes false or missed conflicts                                                             | Compare on `content_hash` first, use mtime only as an ordering tiebreak                                                                                                                             |
| Long portable paths exceed MAX_PATH                                                                                             | Long-path-aware APIs and `\\?\` prefixes where needed                                                                                                                                               |
| Emulator save paths differ per system and RetroBat version                                                                      | Data-driven `save_directories.json`, user-overridable, with a clear "unmapped system" state                                                                                                         |
| Directory-shaped saves (PSP, PS3, Cemu, Citra, Wii, MAME) do not fit RomM's one-file `Save`                                     | Bundle as a single archive per `grout/sync/zip_save.go`, restore atomically via temp-dir-and-swap                                                                                                   |
| Shared memory cards cannot be attributed to a `rom_id`                                                                          | Convert to per-game cards via the RetroBat option (`pcsx2_slot1_memory`, `duckstation_memcardtype`, `dolphin_slotA`) written to `es_settings.cfg`; PS1 and GameCube are already per-game by default |
| Writing emulator INIs directly gets clobbered every launch                                                                      | `emulatorlauncher` regenerates them from options at launch; write `es_settings.cfg` instead, using its `<system>["<rom>"]` per-game form                                                            |
| `es_settings.cfg` is rewritten by ES on exit, like `gamelist.xml`                                                               | Merge rather than clobber, write while ES is idle, write atomically                                                                                                                                 |
| Switching a user to per-game cards strands their existing saves                                                                 | Opt-in and reversible, with either a migration path out of the old container or an explicit warning before the switch; note that per-game cards also break legitimate cross-game save reads         |
| Directory saves are keyed by Game ID and RomM stores no serial or title ID                                                      | Attribute by correlating with the `game-start` journal, cache the learned binding, fall back to reading `PARAM.SFO` / disc headers                                                                  |
| Hashing zip bytes makes RomMBat and Grout disagree on identical saves                                                           | Define `content_hash` over sorted relative paths plus per-file hashes; the archive is transport only                                                                                                |
| A save state restored across an emulator update corrupts or crashes                                                             | Record emulator, core and version per state; never silently restore across a version change (RetroBat's own wiki warns about this)                                                                  |
| Platform nomenclature diverges: 37% of RetroBat systems unmapped, 19 shipped entries stale, 13 slugs fan out to several folders | Layered resolution (override → `fs_slug` → bundled table → normalized suggestion → unmapped), a first-class mapping UI, and `es_systems.cfg` read from the live install                             |
| Two RomM platforms resolve to one folder and clobber each other                                                                 | Key gamelist generation and the local file index by resolved folder, not by platform; merge entries                                                                                                 |
| Arcade fans out to ten folders with romset-specific naming                                                                      | No guessing: require an explicit folder choice per arcade sync set in v1                                                                                                                            |
| Bundled mapping table goes stale as both projects add systems                                                                   | Table is a seed, not an authority; user overrides persist in `Device.sync_config`; unmapped is a normal state, not an error                                                                         |
| RetroBat changes its folder layout between releases                                                                             | Pin a tested-versions table; detect the version and refuse to write when the layout is unrecognised                                                                                                 |
| Socket.IO looks tempting for live updates                                                                                       | Not usable: the socket authenticates from the `romm_session` cookie only, and `sync:*` events go to a `user:{id}` room nothing ever joins. Poll REST                                                |
| Published RomM docs disagree with the server                                                                                    | Generate from `/openapi.json` at a pinned RomM version; gate features on `GET /api/heartbeat`                                                                                                       |
| Syncing a file the target emulator cannot launch: a game that appears in ES and dies                                            | Filter every candidate against the resolved system's `<extension>` list from the live `es_systems.cfg`, and show what was excluded and why                                                          |
| RomM's `is_verified` misses 94 of RetroBat's 157 required BIOS hashes                                                           | Join firmware on md5 against `batocera-systems.json`, ignore filenames and `is_verified`, and report required files RomM does not have                                                              |
| Dev writes land in a production RomM with 85,000 games                                                                          | A dedicated non-admin account, its own scoped token and device on that instance; destructive tests only against a disposable RomM                                                                   |
| Users over-grant scopes at the pairing screen                                                                                   | Publish the scope-to-feature table and name what RomMBat never needs (`users.*`, `roms.write`, `tasks.run`, `logs.read`)                                                                            |
| Client silently misbehaves against an untested RomM or RetroBat version                                                         | Declare minimum versions (RetroBat 8.2, RomM 5.1.0), check both at startup, refuse below and warn above                                                                                             |
| Building all platforms at once buries per-platform edge cases                                                                   | Certify one system at a time against the checklist, in the wave order above, `RetroArch` counted per core rather than as one thing                                                                  |

---

## Verification

- **Unit:** platform and save-directory mapping, gamelist merge (round-trip a real
  RetroBat `gamelist.xml` and assert user fields survive), slot derivation, hash matching,
  sync-set resolution and eviction ordering, outbox replay idempotency. Fixtures from a
  real install, checked in.
- **Mapping coverage, as a checked-in regression:** assert every bundled mapping resolves
  to a folder that exists in `systems_names.lst` (this catches the 19 stale entries today
  and will catch future drift), assert the multi-folder slugs resolve deterministically
  given a fixture `es_systems.cfg`, and assert that two platforms sharing a folder produce
  one merged gamelist rather than two competing writes. Track the unmapped count as a
  visible number so it cannot silently grow.
- **Offline simulation:** the highest-value test suite. Drive the whole client against a
  stubbed handler that can be switched to "unreachable" mid-operation, and assert that
  every operation either completes locally or queues, and that a subsequent flush is
  idempotent under replay and partial failure.
- **Scale simulation:** run sync-set resolution and gamelist generation against a
  synthetic 100k-ROM catalog fixture and assert bounded memory and bounded request count.
- **Portability:** a test that relocates a populated install (different root path,
  simulating a drive-letter change) and asserts the next sync is a clean no-op. Add a
  static check that fails the build if any absolute path reaches the database, and a
  FAT32-constraint test for the 4 GB ceiling and coarse mtime handling.
- **Integration against a live RomM:** run one locally per `DEVELOPER_SETUP.md` and
  exercise pair → resolve set → pull → negotiate → upload → complete end to end. Assert
  the device, `sync_config`, saves and play sessions land in the RomM UI.

  Pairing-only auth does **not** force browser automation here. `GET /api/auth/device/pending/{user_code}`
  and `POST /api/auth/device/approve` are ordinary protected routes needing `me.read` and
  `me.write`, so a test harness holding a pre-made token can play the part of the
  approving user and drive the real flow headlessly. Do it that way rather than adding a
  token-injection backdoor to the app: the shipped client then has exactly one auth path,
  and the tests still cover it. Cover the narrowed-scope grant and the denied and expired
  branches too, since those are reachable from the same harness.

- **End to end on Windows:** a RetroBat VM or spare box is required, there is no
  substitute. Full pass: install, pair, define a set, sync it, confirm ES shows art, launch
  a game, save, exit, **disconnect the network**, play two more games, reconnect, confirm
  all saves and play sessions arrive, then change a save on another client and confirm the
  conflict surfaces. Then **move the drive to a second machine under a different letter**
  and confirm the next sync is a no-op and RomM still shows exactly one device.
- **Regression:** re-run a sync with no changes and assert zero uploads, zero downloads and
  no gamelist churn. That is the single best signal that slots, cursors and set resolution
  are all correct.

---

## Optional follow-ups to RomM itself

Two small, separable PRs surfaced during research. Neither blocks this project, and each
should go up on its own branch rather than bundled:

1. `docs/developers/device-sync-protocol.md` and `client-api-tokens.md` describe payload
   shapes the server does not accept (`roms:[{saves:[]}]` for negotiate, `mac`/`paths` on
   device create), and call the pairing code "8 digits" when `generate_user_code` draws 8
   characters from `ABCDEFGHJKMNPQRSTUVWXYZ23456789`. Correct them against
   `backend/endpoints/sync.py`, `device.py` and `utils/device_auth.py`.
2. `backend/endpoints/sockets/sync.py` emits `sync:*` to `room=f"user:{user_id}"`, but
   nothing anywhere calls `enter_room` for that room, so those events are undeliverable.
   Either join the room on connect or drop the emitters.

A third, larger idea worth raising as a discussion rather than a PR: collection list
responses embedding full `rom_ids` sets does not scale, and a companion-app ecosystem
would benefit from a lighter list shape or an opt-out flag.

---

## Kickoff prompt for the new repo

Paste this into Claude Code from an empty directory:

> Build the first milestones of **RomMBat**, a Windows companion app that syncs a
> self-hosted RomM library with a RetroBat install. C# on .NET 10, published
> self-contained win-x64, GPL-3.0. The name is a portmanteau of RomM and RetroBat and
> the mascot is a wombat.
>
> Four constraints shape the whole design, so build to them from the first commit:
>
> 1. **Offline-first.** This runs on handheld Windows gaming PCs that are away from the
>    RomM instance for days. Local SQLite is the source of truth; the network is
>    optional. The EmulationStation `game-start` / `game-end` hooks run inside the game
>    launch path, so they append to a durable local journal and exit in milliseconds,
>    never opening a socket. A background agent flushes the outbox when
>    `GET /api/heartbeat` succeeds. Record real local mtimes, not sync times, or offline
>    edits lose every conflict. Assume the device clock may be wrong.
> 2. **Libraries reach 100,000+ games**, so never mirror the catalog. Online browsing is
>    a thin paged client over `GET /api/roms`; offline browsing shows only the local
>    subset. ROM content is strictly opt-in and bounded by a disk budget with eviction.
> 3. **Curation via Sync Sets**: a named scope (collection, smart collection, virtual
>    collection, platform, or a saved `/api/roms` filter) plus a policy (max games, max
>    bytes, ordering, eviction rules). Collections are the recommended default but not
>    the only scope type. Persist set definitions into the free-form `Device.sync_config`
>    dict via `PUT /api/devices/{id}` so config roams.
> 4. **Portable-first.** RetroBat is designed to run from a USB drive and move between
>    machines. Everything RomMBat owns lives inside the RetroBat tree: binaries, SQLite,
>    logs, outbox, device identity. No `%APPDATA%`, no registry, no service, no scheduled
>    task, no admin rights. **Never persist an absolute path**; store paths relative to
>    the RetroBat root and resolve at point of use. Locate the root by walking up from
>    `AppContext.BaseDirectory` to a marker file, and have the ES hook `.bat` files use
>    `%~dp0..\..\..\` the way RetroBat's own `updatestores.bat` does. Do not use DPAPI for
>    the token; it binds ciphertext to one machine or user profile and would make the
>    drive undecryptable on the next PC. Assume the filesystem may be exFAT or FAT32,
>    which means a hard 4 GB file ceiling on FAT32 and coarser mtimes than NTFS, so
>    compare saves on `content_hash` first and mtime only as a tiebreak. With no daemon
>    available, the outbox flush is a short-lived process invoked from the ES hooks and the
>    UI, guarded by a lock file.
>
> **Device pairing is the only authentication path you implement.** No password entry, no
> token pasting, no `/api/token` OAuth flow, not even the `POST /api/client-tokens/exchange`
> route. `POST /api/auth/device/init` returns a `user_code` and a relative
> `verification_path_complete`; display the code and a QR of the configured origin joined
> with that path (QRCoder, MIT), then poll `POST /api/auth/device/token`. The code is 8
> characters from `ABCDEFGHJKMNPQRSTUVWXYZ23456789`, not 8 digits as the docs say, and the
> server normalizes hyphens, spaces and case, so display it grouped. Pending state is
> Redis-only with a hard 600s TTL, so show a countdown and a one-button restart. The
> approver can narrow `approved_scopes`, so read the granted set back from `/token` and
> degrade by feature rather than erroring later. Treat 401 as expected, not exceptional:
> keep the local database and outbox, drop to the pairing screen, and resume after
> re-pairing on the same `client_device_identifier`. The only thing the user should ever
> have to type is the server URL, so give that a gamepad on-screen keyboard and remember it.
>
> RomM already has a companion-app protocol: the pairing flow above yields a long-lived
> `rmm_` bearer token plus a `device_id`; library reads at `GET /api/platforms` and
> `GET /api/roms` with `updated_after`; resumable downloads at
> `GET /api/roms/{id}/content/{fs_name}`; and save sync at `POST /api/sync/negotiate`,
> `POST /api/saves`, `POST /api/sync/sessions/{id}/complete`. Generate DTOs from the
> instance's `/openapi.json` (served at the root, not under `/api`); the published docs
> have drifted from the server, so treat the schema as the contract.
>
> Pairing-only auth does not mean untestable. `GET /api/auth/device/pending/{user_code}`
> and `POST /api/auth/device/approve` are ordinary protected routes needing `me.read` and
> `me.write`, so an integration harness holding a pre-made token can play the approving
> user and drive the real flow headlessly. Do that instead of building a token-injection
> backdoor, so the shipped client keeps exactly one auth path.
>
> RetroBat integration is purely through its existing seams: ROMs into `roms/<system>/`,
> metadata into `roms/<system>/gamelist.xml` with `images/`, `videos/` and `manuals/`
> siblings, BIOS into `bios/`, and `.bat` hooks in
> `emulationstation/.emulationstation/scripts/game-start/` and `game-end/`. Do not fork
> RetroBat.
>
> Because this project is built primarily by you rather than by hand, write the repo
> scaffolding first: `CLAUDE.md`, `.claude/skills/*/SKILL.md`, `DEVELOPER_SETUP.md`,
> `CONTRIBUTING.md` (carrying RomM's AI-assistance disclosure norm) and
> `docs/ARCHITECTURE.md`, modelled directly on RomM's equivalents. Suggested skills:
> `romm-api`, `retrobat-layout`, `platform-mapping`, `save-sync`, `offline-and-portable`,
> `platform-certification`, `pre-pr-verification`.
>
> Development is on Windows against an existing RomM instance with ~85,000 games, which
> makes the scale probe real rather than synthetic. Treat that instance as production: use
> a dedicated non-admin account with its own scoped token and device, and keep a disposable
> RomM (Docker or a VM) for conflict, overwrite, expiry and revocation tests. RetroBat is
> portable, so clone a pristine copy per test run. Declare minimum supported versions,
> starting at RetroBat 8.2 and RomM 5.1.0, check both at startup, and refuse below minimum.
>
> Two authority rules that are easy to get backwards. **File extensions come from
> RetroBat, never from RomM**: read `<extension>` per system out of the live
> `es_systems.cfg` and filter sync candidates against it, or you will sync files that
> appear in EmulationStation and die on launch. **Firmware requirements come from
> RetroBat too**: `batocera-systems/Resources/batocera-systems.json` lists 353 BIOS entries
> across 99 systems as `{md5, file}` with the exact destination path. Join it against
> `GET /api/firmware` on **md5 only**, because filenames differ and RomM's `is_verified`
> misses 94 of the 157 hashes RetroBat requires. Fetch BIOS before that platform's ROMs,
> and report required files RomM does not have.
>
> Start with M0: write throwaway probes that confirm what arguments RetroBat's
> EmulationStation passes to a `.bat` hook and **whether it blocks game launch**, where
> each emulator writes saves, whether writing `gamelist.xml` while ES runs is safe, how
> to locate a RetroBat root from a portable install, how a large RomM instance responds
> to paged `/api/roms` calls, and how long an unreachable-host `heartbeat` takes to fail.
> Finish M0 by installing to a USB drive, changing its drive letter, and plugging it into
> a second machine to prove nothing breaks. Record the answers in
> `docs/retrobat-findings.md` before building anything else.
>
> Then implement M1 (pairing, portable token storage, and the full SQLite schema including
> the outbox and relative-path rule) and M2 (paged catalog browsing and sync-set
> resolution). Once M1 through M6 work end to end on one platform, stop building
> horizontally and certify platforms one at a time against the checklist, starting with
> `nes`/`snes`/`gb`/`gbc`/`gba`/`megadrive`/`mastersystem`. Certify per RetroBat system,
> not per aggregate: "RetroArch works" is unverifiable, since each libretro core has its
> own save naming, state directory and BIOS needs.
>
> Mine these for prior art rather than starting cold: `rommapp/playnite-plugin`
> (`Models/RomM/*` for C# DTOs, `Downloads/DownloadQueueController.cs` for the queue),
> `rommapp/grout` (`cfw/batocera/data/platforms.json` and `cfw/*/data/save_directories.json`
> for mapping file shapes, `cache/save_sync.go` for the sync state machine), and RomM's
> `examples/config.batocera-retrobat.yml` as a **seed** for the platform map.
>
> Do not treat that YAML as the answer. RetroBat ships 240 systems and RomM knows 457
> platform slugs, but the YAML holds only 168 pairs: 91 RetroBat systems (37%) are
> unmapped, normalization rescues just 16 of them, 19 entries point at folder names
> RetroBat's own `system/configgen/systems_names.lst` does not contain (`astrocde` vs
> `astrocade`, `ps` vs `psx`, `segacd` vs `megacd`), and 13 RomM slugs fan out to several
> folders (`arcade` alone hits ten). `libretro_slug` is a DAT name and `family_slug` is a
> manufacturer, so neither can drive folder placement. Resolve in layers instead: user
> override, then `platform.fs_slug` matched against the live `es_systems.cfg`, then the
> bundled table, then a normalized-match _suggestion_, then unmapped as a normal state.
> Build the Platform Mapping screen as core UI, and key both the local file index and
> gamelist generation by **resolved folder** rather than by platform, since two platforms
> can legitimately share one.
>
> One caveat on that prior art: the Playnite plugin's `RomMRegisterDevice` carries
> `mac_address` and `hostname`, which is right for a fixed desktop and wrong for a
> portable drive. RomM's `get_device_by_fingerprint` matches on **MAC alone**, so a
> moving install would fingerprint differently per host and could collide onto another
> client. Anchor identity on `client_device_identifier` through the pairing flow, which
> looks up via `get_device_by_client_identifier` and records no host details at all.
>
> Saves are the part most likely to be underestimated. RomM's `Save` is strictly one file
> with a `slot` and an MD5 `content_hash`, but RetroBat produces four different shapes:
> one file per game (RetroArch `.srm`), several files per game (ScummVM `.s00`…),
> a directory per game (PPSSPP `SAVEDATA/<GAMEID>/`, RPCS3, Cemu, Citra, Wii NAND, MAME
> `nvram/`), and one container shared by many games (GameCube memory cards, PCSX2
> `Mcd001.ps2`, Dreamcast VMU). Grout's `sync/directory_saves.go` handles only `psp`, so
> it is a starting point, not a solution. Bundle directory saves with the approach in
> `grout/sync/zip_save.go`, but compute `content_hash` over sorted relative paths plus
> per-file hashes rather than over zip bytes, or you will disagree with Grout forever on
> identical saves. Treat shared containers as out of scope for v1: detect them, steer the
> user to their emulator's per-game memory card option, and report what cannot be synced.
> Directory saves are keyed by Game ID and RomM stores no serial or title ID anywhere, so
> attribute them by correlating with the `game-start` journal you are already writing, and
> cache the learned binding.
>
> Shared containers are smaller than they look, because the emulators all support per-game
> virtual memory cards and RetroBat exposes each as an option. DuckStation already defaults
> to `PerGameTitle` and Dolphin already defaults to GCI folder mode; only PCSX2 defaults to
> a shared `Mcd001.ps2`, and `pcsx2_slot1_memory=game` names the card after the ROM
> basename, which makes attribution trivial. Set these through `es_settings.cfg`, never by
> editing an emulator INI: `emulatorlauncher` rewrites those configs from the options on
> every launch, and `Program.cs:384-388` shows the precedence
> `es_settings.cfg -> global -> <system> -> <system>["<rom filename>"]`, that last form
> being a real per-game override. Treat it as opt-in and reversible, merge rather than
> clobber the file (ES rewrites it on exit, same hazard as `gamelist.xml`), and warn that
> switching strands saves in the old container and breaks games that legitimately read a
> prequel's save.
>
> Save states are the easy half: RetroBat ships `.emulationstation/es_savestates.cfg`, a
> per-emulator schema of directory, filename, screenshot, autosave and slot bounds. Parse
> it instead of hardcoding, map its `<image>` onto the `screenshotFile` upload, and record
> emulator, core and version with every state, because RetroBat's own docs warn that
> states break across emulator updates.
>
> Five traps to get right from the start: always pass
> `with_char_index=false&with_filter_values=false&with_rom_id_index=false` to
> `/api/roms`, since each sidecar scans the whole library; never read `rom_ids` off a
> collection response, because it is a full `set[int]` present even on the list endpoint,
> so page `GET /api/roms?collection_id=` instead; saves pair on `(rom_id, slot)` and a
> null slot uploads forever, so always send a stable slot name; the server rewrites
> uploaded save filenames to `<name> [YYYY-MM-DD_HH-MM-SS]<ext>`, so persist the
> `file_name` from the response rather than the one you sent; and no absolute path may
> ever reach the database, since the drive letter will change.
