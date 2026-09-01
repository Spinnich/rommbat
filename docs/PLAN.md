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
| Stack        | C# / .NET 10 (LTS), published self-contained win-x64: the agent and hook as one file, the UI as an exe plus its natives                |
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

  **Both halves are now measured, not inferred.** A byte-identical save posted twice into the
  same slot reuses the same row and the slot count does not move; a replayed play session
  comes back `"status": "duplicate"` in a per-index result array with `skipped_count`
  incremented, so the server names what it skipped rather than leaving the client to guess.
  This is also the reason a bundled directory save's archive **must** be deterministic: an
  archive that varies between runs defeats the dedup this principle rests on, which is
  precisely what Freegosy does by writing a timestamp file into every bundle. See
  [freegosy-findings.md](freegosy-findings.md), F3 and F4.

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

- Never call `GET /api/roms` without `with_char_index=false&with_filter_values=false`.
  Each of those sidecars scans the whole library.

  **`with_rom_id_index` is the exception and it follows the scope.** It was in that list
  until 2026-08-25 on the reasoning that it is whole-library metadata resent per page. That
  is true only of an unscoped request. Under `platform_ids` the index spans **that
  platform**, and it is what lets the server serve the page by primary key instead of
  `OFFSET n LIMIT m` on a sort with no covering index. Measured at 88,331 roms on 5.2.0,
  turning it off costs **3.4 to 3.7 times the latency on a scoped walk** (2.3 s to 8.5 s a
  page) to save 63 KiB, and about 1.15 times unscoped to save 604 KiB. Send it **off only
  for an unscoped walk**; leave it on for `platform_ids`, `collection_id`,
  `smart_collection_id` and `virtual_collection_id`. See
  [argosy-findings.md](argosy-findings.md), A1.

  `with_total` rides on the same decision: `resolve_total()` returns `len(rom_id_index)`,
  so the count is free with the index on (1 ms) and costs 124 ms with it off. A2.

- **Never read `rom_ids` off a collection response.** `BaseCollectionSchema.rom_ids` is a
  full `set[int]` and it is present on the _list_ endpoint too, so `GET /api/collections`
  on a large instance returns every membership of every collection in one payload.
  Resolve membership by paging `GET /api/roms?collection_id=` (or
  `smart_collection_id=` / `virtual_collection_id=`) instead.
- Use the `/identifiers` endpoints for deletion reconciliation rather than re-pulling full
  rows, **except `/api/roms/identifiers`, which does not scale**: it takes no parameters and
  answered 504 after 300 s on 83,131 ROMs, while its platform and collection siblings answer
  in under 1.5 s. Deletion of content is reconciled through set re-resolution instead; see
  M3 and finding 81.
- `gamelist.xml` only ever contains locally present ROMs. **Not because ES cannot take a
  large one**: M0 loaded a 100,000-entry gamelist in 2.07 s for 419 MB. A gamelist is a
  mirror of what is on disk, and that is the whole of the rule.

  **The per-system cap this bullet used to name is withdrawn, because it cannot do the job
  it was given.** ES lists ROM files it has no gamelist entry for, so dropping entries hides
  no games and only strips their art and description: the user still scrolls past exactly as
  many tiles, now blank. What bounds navigability is the sync set's own `max_games`, which
  is principle 3's argument and already exists. M4 reports a folder that grows past a
  threshold rather than truncating it. `ParseGamelistOnly` would make the gamelist
  authoritative and give a cap teeth, but it is a global ES setting affecting systems
  RomMBat does not manage, so RomMBat does not touch it. See finding 111.

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
  Everything lands under **`RetroBat/emulators/rommbat/`**, including the SQLite database,
  logs, and the outbox.

  **M0 settled this location, and it is not a free choice.** A `system/es_menu/*.menu`
  entry resolves its executable path under `emulators\`, and `emulatorLauncher` refuses
  `..\` escapes outright (`[Generator] Failed. path is null`, exit 204). An app installed
  anywhere else cannot be launched from the ES menu at all. See
  [retrobat-findings.md](retrobat-findings.md), probe 4.

- **Never persist an absolute path.** The local file index, sync-set definitions and
  outbox entries all store paths **relative to the RetroBat root**. Resolve to absolute
  only at the moment of use. A drive letter that shifts from `E:` to `F:` must be a
  non-event. Note the ES hooks receive an **absolute** rom path in `$1`, so relativising at
  that boundary is mandatory work, not an optimisation.
- **Find the root relative to the executable**, walking up from `AppContext.BaseDirectory`
  and confirming with a marker (`retrobat.ini`, `emulationstation/`, `roms/`). There is no
  `build.ini`; the version file is `system/version.info`. Registry and fixed-path lookups
  are a last-resort fallback for a fixed install, never the primary path. The ES hook
  `.bat` files use the same trick RetroBat's own scripts use, as seen in
  `.emulationstation/scripts/start/updatestores.bat`, but **mind the depth**: a hook lives
  at `.emulationstation/scripts/<event>/`, so `%~dp0..\..\..\` reaches `emulationstation/`
  (where `emulatorLauncher.exe` lives) and reaching the RetroBat root takes a fourth level,
  `%~dp0..\..\..\..\`.
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
    M0 measured the failure: `IOException`, Win32 112 `ERROR_DISK_FULL`, message **"There is
    not enough space on the disk"**, raised on a volume with 14.6 GB free. **Never surface
    that message**; it sends the user to delete files that are not the problem. Compare
    `fs_size_bytes` against the target filesystem before the download starts.
  - **FAT and exFAT store coarser modification timestamps than NTFS**, and M0 measured
    **exFAT to be no better than FAT32: 2 seconds on both**, even though exFAT's format
    allows 10 ms. Any conflict logic that leans on mtime equality will produce both false
    matches and spurious conflicts. Treat `content_hash` as the primary comparison and mtime
    only as an ordering tiebreak, and never assume a round-tripped mtime comes back
    bit-identical.
  - **A FAT timestamp rounds up, so it lands in the future.** A file written at 08:03:16.097
    is stored as 08:03:18.000, up to 2 seconds ahead of the clock that wrote it. The
    clock-skew check in principle 1 must carry at least a 2-second tolerance before treating
    a future timestamp as a bad RTC, and files written inside one 2-second window are not
    orderable by mtime at all.
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

All of this is on `rommapp/romm` master today. Verified against a local checkout of the
source, not just the docs.

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
| Platform map       | `examples/config.batocera-retrobat.yml` (167 pairs, a starting point, not an answer: see below) |
| Schema for codegen | `GET /openapi.json` (served at the root, not under `/api`)                                      |

Published references: [Client API Tokens](https://docs.romm.app/latest/developers/client-api-tokens/)
and [Device Sync Protocol](https://docs.romm.app/latest/developers/device-sync-protocol/).
**The docs have drifted from the code** (they show `roms:[{saves:[...]}]` for negotiate and
`mac`/`paths` on device create; the real payloads are `saves:[...]` and
`mac_address`/`sync_config`). Generate the client from `/openapi.json` and treat the
backend as the contract.

### RetroBat's integration seams (no fork needed)

| Seam                                                  | Use                                                                           |
| ----------------------------------------------------- | ----------------------------------------------------------------------------- |
| `roms/<system>/`                                      | Where ROMs land; folder names come from `es_systems.cfg`                      |
| `roms/<system>/gamelist.xml`                          | Metadata ES reads directly                                                    |
| `roms/<system>/images`, `videos`, `manuals`           | Media siblings ES expects (per the RetroBat wiki)                             |
| `saves/`                                              | Emulator save output                                                          |
| `bios/`                                               | BIOS/firmware, flat at the root with few exceptions                           |
| `emulationstation/.emulationstation/scripts/<event>/` | ES event hooks. RetroBat drives these with `.bat`; RomMBat must use an `.exe` |
| `system/es_menu/*.menu`                               | How RetroBat registers launchable apps in the ES menu                         |

ES events include `start`, `game-start`, `game-end`, `game-selected`, `system-selected`,
`quit`, `shutdown`, `sleep`, `wake`, `update-gamelists`. RetroBat ships
`.emulationstation/scripts/start/updatestores.bat` and
`.emulationstation/scripts/update-gamelists/updatestores.bat`, which proves the `.bat`
path works **for a script that takes no arguments**. It does not generalise: M0 measured a
`.bat` failing to start at all once ES quotes an argument, which it does for any value
containing a space. Nine event folders exist on disk; `game-selected` and `system-selected`
fire (ES logs them on every navigation move, with system, rom path and display name) but
ship no folder.

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
| `rommapp/argosy-launcher`                                        | **Mined and closed.** See the caveat below                 |
| `abduznik/Freegosy`                                              | **Mined and closed.** See the caveat below                 |

**Argosy was named twice during planning and then never read, and until 2026-08-25 this table
had no row for it** while [freegosy-findings.md](freegosy-findings.md) told its readers Argosy
had been "mined as trustworthy about the API". That was false, and both places are corrected
rather than quietly reworded. What the pass actually took is small and specific: it sent this
plan to re-measure `with_rom_id_index`, which turned out to be a **3.4 to 3.7 times regression
on a platform-scoped walk** that M2 and M4 are paying today, and to run the BIOS join that found
**84 of RetroBat's 353 requirements are `.zip` files no md5 comparison can ever match**. Neither
number is Argosy's; both are measured here. It targets Android, so **no path from it is valid for
RetroBat and none was taken**, and its own headline cost figure inverts on this library. The full
ledger, including the eighteen leads dropped at triage and the design notes addressed to M7b, is
[argosy-findings.md](argosy-findings.md). **Treat that document as closed.**

**Freegosy is the one source here that is not `rommapp` and not version-aligned**, and it was
mined under a correspondingly higher bar: it targets RomM 4.9 against our 5.2.0 baseline, it
is v0.5.x with one maintainer, and it targets desktop emulators, EmuDeck and RetroDECK, so
**none of its paths is valid for RetroBat and none was taken**. What it was good for was
pointing at save-protocol parameters this plan had never mentioned. Every claim was then
re-asked of the live server, and several of its own answers were wrong at 5.1.x: its play
session payload shape is a 422, its documented 409 body does not exist, and its per-device
isolation model is not what the server does. The full ledger, including the thirteen leads
dropped at triage and the six left open, is
[freegosy-findings.md](freegosy-findings.md). **Treat that document as closed**; re-reading
the client is unlikely to repay the effort.

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
  `es_settings.cfg` precedence, hook conventions, and the rule that RetroBat
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

Every RomMBat release states the minimum RomM and RetroBat versions it supports. Currently
**RetroBat 8.2.1** and **RomM 5.2.0**.

**The floor tracks the newest stable, it does not sit at the oldest version that happens to
work.** RomMBat adopts a new RomM or RetroBat stable within one release of it appearing and
moves the minimum with it. Two reasons, both specific to this project. Every rule in
`docs/retrobat-findings.md` is a measurement of one build's behaviour, and supporting a range
means owning that measurement on every version in the range, on a `(system, emulator, core)`
matrix that is already two to four passes per row. And RetroBat's own updater moves users
forward, so a wide floor buys compatibility with installs that mostly do not exist while
doubling what has to be certified. RetroBat 8.2.1 is the floor because 8.2.0's Flycast
save-state watcher read the wrong directory, and a release that supported both would have to
carry the workaround and the fix at once.

What adoption costs, each time: re-run `reference/refresh.sh` and resolve the drift, re-read
the upstream changelog for anything that touches a measured rule, move the floor and the
tested row together, and re-check every open issue in `docs/retrobat-findings.md`. Moving the
RomM floor also moves the pinned OpenAPI schema, because the pin is the minimum version on
purpose.

- Read the RomM version from `GET /api/heartbeat` (`SYSTEM.VERSION`) at startup and the
  RetroBat version from **`system/version.info`** in the tree.
- **There is no `build.ini`.** M0 confirmed it does not exist anywhere in a RetroBat 8.2
  tree. `system/version.info` is a single line carrying a channel and architecture suffix,
  `8.2.1-stable-win64`, so it is not a bare semantic version and must be split on `-`
  before comparison.
- **Both version strings can carry prerelease suffixes.** The instance M0 measured against
  reported `5.1.1-beta.1`. A comparison that assumes three numeric components will throw on
  real-world values from either side.
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

Seven experiments on a real RetroBat install. Each can reshape a later milestone. Findings
are recorded in **[retrobat-findings.md](retrobat-findings.md)**, which is the source of
truth for every measured number and supersedes any figure quoted elsewhere in this plan.

**Status: all seven answered**, measured against RetroBat 8.2.0-stable-win64 and RomM
5.1.1-beta.1, and the second host's hook failure is resolved. What is left is blocked on
hardware, not effort: `bizhawk` needs a gamepad attached, and `bigpemu` and `openmsx` need
Jaguar and MSX roms. Findings amended this document in thirty-six places;
the amendments are inline below and in the sections they affect, and the findings document
carries the full contradiction table. Reproduce any of it with the scripts in
`tools/m0-probes/`.

The six results that moved the design most:

- **A hook must be an `.exe`, and this is the big one.** ES fires every event and logs
  `executing:` for every script in the folder, but neither scripted form survives an ordinary
  filename. A `.bat` never starts once any argument is quoted, because the `batfile`
  association is `cmd /c "%1" %*` and cmd's quote-stripping rule mangles the line; one space
  anywhere is enough. A `.ps1` never starts once the name contains a parenthesis, because ES
  builds `powershell <script> <args>` with no `-File`, making it an implicit `-Command` that
  reparses the tail as code. An `.exe` hook receives all three arguments intact on a real
  No-Intro name. So `game-start` is usable, the earlier "ES never fires it" reading was
  wrong, and RomMBat's hooks ship as the agent exe rather than as `.bat` files.
- **Hooks do not block game launch.** `emulatorLauncher` started 30 ms after the
  `game-start` hook fired, three times out of three, while that hook still had 8 seconds of
  deliberate sleep ahead of it. The hook path is not latency-constrained. It _is_
  concurrency-constrained: hooks overlap freely, and three `game-end` hooks were observed in
  flight at once.
- **The install location is forced.** `.menu` executable paths resolve under `emulators\`
  and `..\` escapes are refused, so RomMBat lives at `emulators/rommbat/`, not `plugins/`.
- **An unreachable LAN host takes 21 seconds to fail** and a default `HttpClient` inherits
  every millisecond of it, so `ConnectTimeout` must be set explicitly everywhere.
- **The gamelist ceiling is a fiction.** ES loads 100,000 entries in 2.07 s for 419 MB. The
  per-system cap this once justified on navigability grounds is withdrawn as well, because
  ES lists ROM files with no gamelist entry: M4 reports a large folder rather than
  truncating it. See findings 111 and 106.
- **exFAT is no gentler than FAT32 on timestamps**: 2-second granularity on both, rounded
  **up**, which stamps a freshly written save as much as 2 seconds in the future. Every
  mtime comparison and the clock-skew check have to carry that tolerance.

1. **ES script hook arguments on Windows.** Batocera documents `game-start` as `$1` rom
   path, `$2` basename, `$3` system, `$4` emulator, `$5` core, and `game-end` as taking
   none. Confirm what RetroBat's ES actually passes to a `.bat`, **whether the hook
   blocks game launch and for how long**, and whether `game-end` fires on crash and on ES
   exit. Write an echo-to-log `.bat` in each event folder and capture the output. The
   blocking answer sets the hard budget for the hook path.

   **Answered.** Hooks do **not** block: the launcher started 30 ms after the hook fired,
   three times out of three, against an 8 s hook sleep. RetroBat passes **three** arguments
   to `game-start`, not five: `$1` absolute rom path, `$2` and `$3` (both `2048` for the
   game tested, so their identities are still ambiguous), and `$4`/`$5` **empty**. The
   system, emulator and core are given to `emulatorLauncher` and **withheld from the hook**,
   which breaks the `{emulator}:{core}:{slot}` slot derivation in M6. `game-end` takes no
   arguments and **also fires with no preceding `game-start`**, including for ES-menu
   launches and for launches that failed. Hooks run **concurrently**; three `game-end`
   hooks were observed in flight simultaneously. Every script in an event folder runs, in
   alphabetical order, so installing beside `updatestores.bat` works. `game-end` **does**
   fire when the emulator is killed (exit code 1), within 66 ms, as promptly as on a clean
   exit.

   **And the finding that reshapes M6, now with its mechanism.** A `.bat` hook does not run
   for a game whose gamelist `<name>` contains a space, confirmed by crossover. Probe 7b then
   isolated why, by turning ES's own debug logging on: ES fires the event, resolves the
   scripts and logs `executing:` for each one, so nothing fails on ES's side of the boundary.
   The failure is **per interpreter**, and it is reproducible outside EmulationStation:

   | Hook form | Fails when                                    | Mechanism                                                                                                                                          |
   | --------- | --------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
   | `.bat`    | any argument is quoted, so any space anywhere | ShellExecute uses the `batfile` association `cmd /c "%1" %*`, and cmd's quote-stripping rule mangles a line whose arguments carry their own quotes |
   | `.ps1`    | the name contains `(`, `)` or `,`             | ES builds `powershell <script> <args>` with no `-File`, so it is an implicit `-Command` and PowerShell reparses the tail as code                   |
   | `.exe`    | not observed to fail                          | no interpreter in the path; arguments arrive through normal `CommandLineToArgvW` splitting                                                         |

   Measured against `Gradius 2 (Japan, Europe) (En) (Wii U Virtual Console).zip`, the worst
   realistic case: both `.bat` files and the `.ps1` stayed silent, and the `.exe` received
   `ARGC=3` with every argument intact. **So hooks ship as an exe.** Even so, prefer
   `emulatorLauncher.log` for the launch facts, since the hook is never told the system,
   emulator or core, and a `.ps1` that does run receives the display name split across
   arguments. Filed upstream as `RetroBat-Official/retrobat#249`, closed there on 2026-08-21
   as an EmulationStation issue and refiled at
   [batocera-emulationstation#2196](https://github.com/batocera-linux/batocera-emulationstation/issues/2196),
   where it is open. Its title still describes only the `.bat` symptom and understates the
   scope.

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

   **Answered.** The tree is **`saves/<system>/<emulator>/`**, not `saves/<system>/`,
   and there are also emulator-named folders at the _top_ level (`saves/dolphin/` beside
   `saves/gamecube/dolphin-emu/`). **Flycast writes four port-keyed VMU files**
   (`flycast/vmu/vmu_save_A1.bin` through `D1`), shared by every game, so Dreamcast is class
   D by default, but `es_features.cfg` declares **`flycast_vmupergame`**, which converts it
   for **port 1 only**. Two class-D cases missing from the M6 table: megacd's shared
   `4Mbit_cart.brm` and xbox's `eeprom.bin` + `xbox_hdd.qcow2`. `es_savestates.cfg` is
   byte-identical to the vendored copy but has four parser traps, including **`libretro`
   declaring no slot bounds at all** and **`desmume`'s `<image>` being identical to its
   `<file>`**.

   **And the answer the class-D conversion story depends on: the per-game
   `es_settings.cfg` override works.** `emulatorlauncher` honours
   `<system>["<rom filename>"].<key>`, it outranks the system-scoped key, it stays scoped to
   the one rom, and it survives ES rewriting the file, which ES does only when a setting
   actually changed that session. Two constraints came with it. **The key must carry the rom
   extension**: `ports["gong"]` is ignored where `ports["gong.libretro"]` takes effect, and
   the failure is silent, so build the key from `fs_name`. And **ES prunes any setting whose
   value equals its own default**, so a key written at its stock value will simply vanish.
   Custom keys are unaffected; ES preserved a deliberately nonsense one intact.

   **PPSSPP's two state directories are also resolved, and neither is stale.** RetroBat
   mirrors PPSSPP's native `psp/PPSSPP_STATE/<GAMEID>_<ver>_<slot>.ppst` into the declared
   `psp/ppsspp/<rom filename>_<slot>.ppst` about 120 ms after each save, while the emulator
   is still running, and writes a `.txt` sidecar holding the native basename as the mapping
   between the two schemes. **The ES-facing directory is the authoritative one**: ES passes
   `-state_slot` and `-state_file` naming it, and the launcher hands that path to the
   emulator as `--state=`, so a state RomMBat writes there does reach the game. Two
   consequences: the `.txt` is part of the state and must be synced with it, and **the
   mirrored `<image>` screenshot is racy** (observed correct, zero-byte, and absent across
   three saves, because the watcher copies before PPSSPP has finished writing it), so
   `screenshotFile` must be treated as best-effort.

   **Twelve of the thirteen emulators have now been launched and eleven driven to a real save
   state.** `libretro`, `ppsspp`, `duckstation`, `pcsx2`, `dolphin`, `flycast` and `gopher64`
   were already installed; `desmume`, `mupen64`, `jgenesis`, `bizhawk` and `openmsx` were
   downloaded on demand. Only `bigpemu` resisted: it installs and runs, but its save state is
   reachable only through a gamepad-driven overlay menu, so its template is unverified.
   Results:
   - **The `<file>` template was correct for all eleven.** Filenames can be trusted, with one
     collision: **DeSmuME's `{{romfilename}}.ds{{slot0}}` also matches its own `.dsv` battery
     save** if the slot is expanded as a wildcard, so anchor the slot as a single digit.
   - **BizHawk is the clearest case of the mirroring model.** Natively it writes to
     `emulators/bizhawk/sstates/<system>/<internal title>.<core>.QuickSave0.State`, **outside
     the saves tree**, and RetroBat mirrors that into the declared, core-scoped
     `saves/nes/bizhawk/sstates/NesHawk/<rom filename>.QuickSave0.State` with a `.txt` giving
     the mapping. Deleting the native copy and relaunching rebuilt it from the ES-facing one.
     A `.State.rap` sibling stays native-only and does not round-trip.
   - **One `<directory>` declaration is wrong on 8.2.1, and it was two on 8.2.0.**
     **`openmsx` writes to `bios/openmsx/savestates/`**, a different top-level tree from the
     declared `saves/msx1/openmsx`, which stayed empty, and that is unfixed. `flycast` was
     the second: RetroBat's own launcher writes `Dreamcast.SavestatePath =
saves/dreamcast/reicast/states` while the file declares `{{system}}/flycast/sstates`,
     and on 8.2.0 that declared directory existed and stayed empty. **8.2.1 fixed it**
     (`emulatorlauncher#1336`) by pointing RetroBat's save-state watcher at the directory
     Flycast really writes, so the state is now mirrored into the declared path in the same
     millisecond, driven three times on a real install. The emulator's own path and the
     declaration both stayed put; only the mirror moved. One wrong is all the rule needs:
     `<directory>` must be cross-checked against the emulator's generated config, and an
     empty declared directory must never be read as "this game has no states".
   - **The `.txt` sidecar is written unconditionally, not only where naming differs.**
     `jgenesis` and `desmume` both wrote one containing the rom filename itself, so its
     presence proves nothing; its content is the mapping and travels with the state.
   - **`<image>` is absent more often than present**, so `screenshotFile` is best-effort
     everywhere.
   - A manual save is mirrored **live**, within about 120 ms; an autosave state appears **at
     exit**. `libretro` needs no mirroring at all, since RetroArch is pointed straight at the
     declared path via `savestate_directory`.

   Two obstacles met on the way are design input in their own right. **`bizhawk` installs and
   then crashes** in `BizhawkGenerator.CreateControllerConfiguration` unless the launcher is
   passed **`-core`**, because `inputPortNb[core]` is an unguarded dictionary lookup;
   EmulationStation always supplies one, direct invocation does not, so **anything RomMBat
   launches must pass `-core`**. And **installing an emulator on demand raises a modal dialog
   with no title and no timeout** ("The emulator '\<name\>' is not installed. Install now?"),
   which blocks the launch indefinitely: three launchers were found still waiting on it seven
   hours later. Anything that launches a game programmatically has to expect both, and must
   not record a play session for a launch that never happened.

   **Flycast's per-game VMU converts, but not into class A.** With
   `dreamcast["<rom>.chd"].flycast_vmupergame=1`, `emu.cfg` flips to `PerGameVmu = yes` and a
   new `flycast/vmu/T40217N_vmu_save_A1.bin` appears live, while the shared
   `vmu_save_A1.bin` is left untouched. **`T40217N` is the disc's serial, not the rom
   filename**, so this cannot be addressed from `fs_name`; attribution needs the serial or the
   launch window. That is the same shape DuckStation turns out to have under its stock memory
   card mode, so identifier-keyed attribution is the common case for disc systems rather than
   the exception.

   Also measured, and it changes M6's change-detection: **launching a PS2 game rewrites both
   shared memory cards without any in-game save**, and a Dreamcast launch rewrites the shared
   VMU the same way, so mtime cannot decide whether a class-D container needs uploading.
   Content hashing is mandatory there.

   **That hazard is not confined to class D.** A Master System cart driven under libretro
   `genesis_plus_gx`, booted to its title screen with no save key ever pressed and no progress
   made, wrote `saves/mastersystem/<rom>.srm` anyway: 65,536 bytes while running and 8,188
   bytes after a clean exit. Nor is it only written at exit, because RetroBat ships
   `autosave_interval = "10"`, so the file appears within seconds of boot and survives a crash
   or a kill. Its contents are the cartridge formatting its own backup RAM
   (`PHANTASY STAR   BACKUP RAM PROGRAMMED BY`), so **no property of the file in isolation
   identifies it as empty**: 8,188 bytes across 35 distinct byte values defeats both a size
   floor and a blankness test. Only comparison against a previously known state separates it
   from a real save, which makes `content_hash` the guard for class A as much as class D, and
   means **the first save seen for a ROM with no local baseline is not evidence that anything
   was played** and must not win a conflict on recency alone. See
   [freegosy-findings.md](freegosy-findings.md), F20.

   **`mastersystem` and `gamegear` are now classified**, both class A: a loose `.srm` at
   `saves/<system>/` named after the ROM. `mastersystem` is a **wave 1** platform, so its shape
   being a guess mattered more than the count suggests; `data/retrobat/save_shapes.json` drops
   from 23 unclassified systems to 21. The technique that got `gamegear` is worth reusing for
   the rest: **RetroArch names the destination in its own log even on a run that writes
   nothing** (`[Override] Redirecting save file to ...`), so a system can be classified from
   intent when its cart is never touched. See finding F19.

3. **Library refresh.** Determine how to make ES pick up newly added ROMs without a full
   restart, and whether writing `gamelist.xml` while ES is running is safe (ES may
   overwrite on exit). Check the `update-gamelists` hook and the `-updatestores` pattern
   in `emulatorlauncher.exe`.

   **Answered, and the mechanism is not the one this item guesses at.** `-updatestores`
   drives `batocera-store.exe` and has nothing to do with gamelists; `emulatorLauncher` has
   no gamelist switch at all, and ES's own CLI is startup-only. **EmulationStation instead
   runs an HTTP API on `127.0.0.1:1234`**, and `GET /reloadgames` makes it rescan roms and
   re-read gamelists live. It works with `PublicWebAccess` at its default, because that
   setting gates only non-local callers, so **no user configuration change is needed**.
   Also available: `/systems`, `/systems/<system>/games`, `/caps` (version), `/quit`,
   `/emukill`, `POST /launch`.

   Writing `gamelist.xml` under a running ES is **safe if followed by `/reloadgames`**. ES
   holds a stale in-memory model (proven: a rename on disk was invisible until reload) and
   rewrites the file on exit, merging in place rather than regenerating, so comments and
   element order survive. Write then reload and the edit persists; write without reloading
   and ES's stale model would land on top.

4. **Install discovery and app registration, portable-first.** Confirm that walking up
   from `AppContext.BaseDirectory` to a marker (`retrobat.ini`, `emulationstation/`,
   `roms/`) reliably locates the root on both a portable and a fixed install, and find
   the idiomatic place inside the tree for a third-party tool to live. Confirm the
   minimum viable `system/es_menu/*.menu` entry, and whether it tolerates a **relative**
   executable path. Confirm the `%~dp0..\..\..\..\` pattern works from a hook.

   **The count in that sentence was wrong for three revisions of this plan and is fixed
   here.** A hook lives at `emulationstation/.emulationstation/scripts/<event>/`, so three
   levels reaches `emulationstation/`, which is where `emulatorLauncher.exe` sits, and the
   RetroBat root takes a fourth. RetroBat's own `start/updatestores.bat` uses three because
   it is calling `emulatorLauncher.exe`, which is exactly the coincidence that made the
   wrong number look confirmed. RomMBat ships executable hooks that resolve the agent from
   their own module path rather than a shell expansion, so nothing depends on the count at
   runtime; it is corrected because the plan is read as documentation. See
   [retrobat-findings.md](retrobat-findings.md), probe 4.

   **Answered, and it changed the layout.** A `.menu` executable path is **required** to be
   relative, is resolved under **`emulators\`**, and `..\` escapes are **refused**
   (`[Generator] Failed. path is null`, exit 204). So RomMBat installs to
   `emulators/rommbat/`, not `plugins/rommbat/`. The launched process gets its own directory
   as CWD, and `.bat` targets work as well as `.exe`. A "minimum viable" entry is **two**
   files: the `.menu` plus a `<game>` element in `system/es_menu/gamelist.xml`, because
   `es_menu` is an ordinary ES system whose roms are `.menu` files, parsed by
   `emulatorLauncher` rather than by ES. The `%~dp0` pattern works but needs **four** levels
   to reach the root, not three. Root markers confirmed present: `retrobat.ini`,
   `emulationstation/`, `roms/`, `saves/`, `bios/`, `system/`, `emulators/`, `user/`. No
   `build.ini`.

5. **Scale probe.** Point the client at the largest available RomM instance and measure:
   `GET /api/roms` page latency with the sidecar flags off versus on, the size of a
   `GET /api/collections` response, and how large a `gamelist.xml` EmulationStation can
   load before browsing degrades. These numbers set the default page size, the sync-set
   warning thresholds, and the per-system gamelist cap.

   **Answered** against an 83,131 rom library. There are **four** default-on sidecar
   flags, not three, and they are a **flat ~841 KB resent on every page** rather than a
   per-page cost, dominated by `with_rom_id_index` (582 KB) and `with_filter_values`
   (280 KB). Server time is unaffected, so fetch them once and disable them thereafter.

   **Amended 2026-08-25: "server time is unaffected" holds for an unscoped walk and is
   wrong for a scoped one.** This probe measured bytes without separating the two scopes,
   and `with_rom_id_index=false` shipped on both paths as a result. Re-measured at 88,331
   roms on 5.2.0, a `platform_ids`-scoped page runs 2.3 s with the index on and 8.5 s with
   it off, at every offset, while the index costs only 63 KiB there rather than 582 KiB.
   Scoping also defeats sidecar memoisation on its own, which is why a scoped page is
   2.3 s where an unscoped one is 0.3 s. See [argosy-findings.md](argosy-findings.md), A1.
   Default page size **250 with sidecars off**; a full walk of 83k roms then takes about
   14 minutes, so incremental sync via `updated_after` is the normal path. A **single**
   `GET /api/collections` entry returned **715 KB**, 99% of it two inlined arrays of
   cover-art paths at two sizes, one per member rom, with no pagination available.

   **And the gamelist ceiling is not where this plan feared it was.** A synthetic
   **100,000-entry** gamelist (65 MB) in one system loads in **2.07 s** from a cold ES start
   and costs **419 MB** of working set, against 1.67 s and 211 MB for a 200-entry floor:
   roughly **2 MB per 1,000 entries**, with startup essentially flat to 25k. Repeating it
   with a real image file per entry cost **0.9 s more and no extra memory**, so ES decodes
   artwork lazily while browsing. `GET /reloadgames` answers in 1-2 ms and does the work
   afterwards; the change is visible **1.1 s** later at 100k. **So the per-system gamelist
   cap is a gamepad-navigability decision, not a technical ceiling**, and the claim under
   core principle 2 that a 100k gamelist "would make EmulationStation unusable" is withdrawn.
   M4's finding 111 then withdrew the cap itself: navigability is not something a cap can
   deliver, because ES lists ROM files that have no gamelist entry.
   What this does not measure is on-screen scroll smoothness, which ES exposes no way to read.

6. **Offline behaviour of the host.** Confirm what happens to a running sync when Wi-Fi
   drops mid-download, and how long a `GET /api/heartbeat` to an unreachable LAN address
   takes to fail. That timeout is the budget for every reachability check in the UI.

   **Answered.** An absent host on the local subnet takes **21 seconds** to fail, every
   time, and a default `HttpClient` inherits all of it.
   `SocketsHttpHandler.ConnectTimeout` caps it precisely; **2 s is the recommended
   interactive budget**, against a measured 39 ms connect to a healthy instance.
   `HttpClient.Timeout` is the wrong lever because it bounds the body too. A timeout and a
   user cancellation both surface as `TaskCanceledException` and differ only in the inner
   exception, so naive `catch (TaskCanceledException)` mislabels every offline server as a
   user action. Downloads **are** resumable: `Accept-Ranges`, `ETag`, correct `If-Range`
   handling (a stale validator returns a full 200 rather than a corrupt splice), and a
   kill-and-resume produced a byte-identical file.

7. **Portable move test.** Install to a USB drive, pair, sync a couple of games, then
   change the drive letter and plug the drive into a second machine. Nothing may break:
   not root discovery, not the local file index, not the ES menu entry, not the hooks,
   not the device identity. Record the drive's filesystem and its mtime granularity, and
   note whether RetroBat itself stores any absolute paths that would constrain us.

   **Passed, after a rerun.** The stick went G: to D: (second PC, different Windows user) to K:. Root
   discovery, launching, and writes back to the stick all worked on the second machine and
   after every letter change. RetroBat's live config holds **no absolute paths at all**: of
   5,636 config files, only 9 contain one, and every one is either MAME software-list
   metadata or a stale developer path baked into a `system/templates/` file that
   `emulatorlauncher` regenerates anyway (`F:\RetroBat-Wip\...` being the giveaway).

   **No hook produced any output on the second machine at first**, not even `start`, while ES
   demonstrably ran and rewrote a gamelist there. The rerun with three hook forms installed
   side by side explains it: **that host cannot launch a `.bat` or a `.ps1`, only an `.exe`**.
   All four events fired there and the exe recorded every one, while neither script form
   produced anything, including for the three events that pass no arguments. Every hook was a
   `.bat` on the first visit, hence total silence. `--home` was passed correctly and the
   volume was writable, so neither of those explains it. **Both causes are named and neither
   is security software**: Notepad++'s installer had replaced the `batfile` association
   (`HKCR\.bat` = `Notepad++_file`), and the PowerShell execution policy is the default
   `Restricted`. Defender, AppLocker and removable-media policy are all clean there, and an
   unsigned exe ran from the stick under Smart App Control. **This is the strongest single
   argument for shipping hooks as executables**, and "the hooks may simply not run here"
   remains a state RomMBat must detect and report.

   **The filesystem constraints were measured separately**, on a second stick formatted
   FAT32 then exFAT, since the RetroBat stick is NTFS. Three results, and two of them change
   the design. **FAT32's 4 GB ceiling reports itself as `ERROR_DISK_FULL`, "There is not
   enough space on the disk", on a volume with 14.6 GB free**, so that message must never
   reach the user and the check has to be a pre-flight against `fs_size_bytes`. **exFAT
   stores modification times exactly as coarsely as FAT32, 2 seconds**, despite its format
   allowing 10 ms, so the two are interchangeable for timestamp purposes. And **the rounding
   is up**: six files written across 1.7 s all carried one identical mtime, and every one of
   them was stamped **later than the clock that wrote it**, by up to 2 seconds. The FAT
   local-time DST hazard did **not** appear: local time round-tripped exactly in both winter
   and summer, with UTC converted using the offset in force on that date.

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
  `flush`, `background`, `status`, and more. The `game-*` subcommands are journal-only and
  must never open a socket; `background <event>` is the pass the `start` and `quit` hooks
  spawn, and it is the only subcommand a person is not expected to type.
- `RomMBat.UI` - full-screen gamepad-navigable app. **Avalonia**, settled in M7 stage 7a so
  7b does not reopen it, and the deciding argument is not the cross-platform one.

  **The argument is size on a portable drive, not start time.** WPF cannot be trimmed at
  all, so a WPF build has a floor it cannot get under, and it needs the Windows Desktop
  runtime inside a self-contained publish on top of the 76 MB the agent already costs.
  Avalonia can be trimmed, which is the only lever that moves that floor.

  **An earlier revision of this paragraph overstated it** by saying WPF was outside both of
  the levers M6 measured. It is outside one: `PublishReadyToRun` works with WPF, and only
  `PublishTrimmed` does not. The start-time argument that applied to the hook does not
  transfer here, and the size argument is the one doing the work.

  Avalonia renders through Skia, so what the handheld shows does not depend on the machine's
  Windows Desktop stack, and it supports trimming and AOT if either is ever needed. The
  cross-platform argument is real but is **not** why: RomMBat ships win-x64 and nothing in
  this plan targets anything else.

  This binds the framework and nothing else. Presentation owns no logic, which is what made
  deferring cheap and what keeps the decision cheap to revisit: set resolution, mapping,
  conflict handling and the outbox all live in Core.

  **What it cost, now stage 7b-1 has paid it.** Referenced as `Avalonia`, `Avalonia.Win32`,
  `Avalonia.Skia` and `Avalonia.Themes.Fluent`, never `Avalonia.Desktop`: that package pulls
  `Tmds.DBus.Protocol` in for the X11 backend, which raises `NU1903` for a known
  high-severity advisory and so fails the `-warnaserror` build, for a backend a win-x64 ship
  cannot use.

  | Publish variant              | Size         | First frame | Shape                     |
  | ---------------------------- | ------------ | ----------- | ------------------------- |
  | the console stub it replaced | 77.9 MB      | n/a         | one file                  |
  | **shipped: untrimmed**       | **101.1 MB** | **1041 ms** | **exe plus four natives** |
  | untrimmed + ReadyToRun       | 132.0 MB     | 533 ms      | exe plus four natives     |
  | trimmed + ReadyToRun         | 61.1 MB      | 517 ms      | bundled, refused below    |

  **Size is the total of the published files, and only the console stub is one of them.**
  The shipped 101.1 MB is the exe plus `av_libglesv2`, `libSkiaSharp`, `libHarfBuzzSharp` and
  `e_sqlite3`. `IncludeNativeLibrariesForSelfExtract` does produce a single file, and it is
  refused on core principle 4: self-extraction unpacks the natives into the **host's** temp
  directory rather than the tree, afresh on every machine a portable drive is carried to.
  `docs/ARCHITECTURE.md` lists the five files and their sizes.

  **The size argument holds and is not yet collected.** Trimming really does take this below
  the agent, and it is off because Core and `RomM.Client` raise 16 `IL2026` warnings across
  twelve reflection-based `System.Text.Json` call sites, whose failure mode is a runtime
  deserialisation fault in a build that linked cleanly. `SaveShapes` classifies every save.
  Making those trim-safe is #98 and belongs in its own PR, not a UI branch.

  **One correction to the reasoning above, from measuring it.** ReadyToRun is what buys the
  start time and trimming is purely a size lever: trimmed and untrimmed are equally fast once
  R2R is on. The 150 ms trimming appeared to cost in an early measurement was R2R being absent,
  not trimming being present.

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
scope buys and what breaks without it.

**Two roles, two scope sets, and conflating them costs an hour.** Everything below is what
the **device** requests. Approving a request is the other half of the flow and needs
`me.write`, which the device deliberately never asks for. In the web UI that comes from the
approver's session, so it never surfaces; it only bites when approval is driven by an API
token, as the integration harness does. A token missing `me.write` fails the route guard
with a bare 403 `Forbidden` **before** the code is looked up, which is what distinguishes it
from a scope-subset rejection (`Approved scopes exceed what's allowed for this user`). Note
also that `allowed_scopes` is computed from `request.user.oauth_scopes`, the **account's**
permissions, while the route guard checks the **token's**, so the two are checked
independently.

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

- Paged browse over `GET /api/roms?...&limit=250&offset=`, with `with_char_index` and
  `with_filter_values` off on every page. `with_total` stays on: M0 probe 5 measured it at
  zero bytes, and it is what lets an interrupted walk know how far it has left to go. No
  full-library mirror, ever.

  **`with_rom_id_index` is set from the scope, not held off as a constant.** Four of the
  five scope kinds send a scoping parameter and only `Filter` pages unscoped, and the flag
  costs 3.4 to 3.7 times the page latency when it is off on a scoped walk. Off for
  `Filter`, on for the other four. `CatalogQuery` already knows its own scope, so this
  belongs in `ToQueryString` beside the switch that emits the scope parameter.
  See [argosy-findings.md](argosy-findings.md), A1 and A2.

- Incremental by `updated_after`, recorded in `sync_cursor`. A full walk is a first-run or
  repair operation, takes about 14 minutes on 83k ROMs, and must resume from a recorded
  offset rather than restart.
- Sync set model: scope (collection / smart collection / virtual collection / platform /
  saved filter) plus policy (max games, max bytes, ordering, eviction rules).
- **The caps select in two stages, because one stage cannot be both bounded and independent
  of arrival order.** A worst-first buffer keeps the ordering-best candidates, then one pass
  over that buffer in the set's own order takes each candidate that still fits. Dropping the
  ordering-worst as a budget fills, on its own, throws away small games a later ordering-best
  candidate would have left room for, and makes the answer depend on the order the ROMs
  happened to be imported in. A game cap bounds the buffer exactly. A byte budget does not,
  since a candidate the budget turns away lets a later one in, so the buffer falls back to
  the same 50,000 ceiling an uncapped scope is refused above. That ceiling, not the library
  size, is what a resolve holds.
- **An interrupted walk accumulates; only a completed one decides.** Each segment writes its
  rows stamped with the walk's start and reads back what earlier segments found, so the caps
  apply to the walk rather than to each segment. Membership is only retired when the walk
  finishes: a departure is an eviction candidate, and half a walk is not evidence that
  anything left. Exclusions are deleted rather than departed, being a fact about the last
  resolution rather than something on disk.
- Resolve sets by paging `GET /api/roms?<scope>`, **not** by reading `rom_ids` from a
  collection payload.
- Re-resolve every set on every sync so smart-collection drift is picked up. A member that
  left the scope becomes an eviction candidate, never an immediate delete.
- Persist set definitions to `Device.sync_config` via `PUT /api/devices/{id}`, sending
  **only** `sync_config`. See the API traps below: the full update payload is a 500.
- **Do not use the generated DTOs for the paged read.** `SimpleRomSchema` and
  `PlatformSchema` both hold `fs_size_bytes` as an `int32`, which the pinned schema forces
  by declaring it a bare `integer`. Measured against a live instance, `GET /api/platforms`
  fails to deserialize on the first platform, and a 15.7 GB Switch title would do the same
  to a page of ROMs. `RomM.Client.Catalog.RomRow` and `PlatformRow` are hand-written slim
  rows carrying `long`, and they also avoid parsing seventy fields per ROM across 333 pages.

  **Done when:** a user can define "my SNES favourites, max 40 games, 8 GB" and see exactly
  which games it resolves to, without the client ever holding the whole library in memory.

#### Three API facts that decide the data model, measured against a live instance

- **`platform.slug` is not unique; `fs_slug` and `id` are.** A real 123-platform library
  carried only **72 distinct slugs**, because that owner files demos, prototypes, unlicensed
  and aftermarket titles under a parallel `-unofficial` folder per system, and RomM resolves
  both folders to the same platform (`fs_slug` `gb` and `gb-unofficial` are both `slug` `gb`).
  **That is a user's filing scheme, not a RomM behaviour**, which makes it worse rather than
  better for a client: the number of such rows, their names and which ones exist are entirely
  under the user's control and cannot be predicted or enumerated in advance. Anything keyed by
  slug silently loses 51 of those 123 platforms and leaves the extra sets unmappable, so the
  platform map is keyed by `fs_slug`. The slug stays as the bundled table's lookup key.
- **`PUT /api/devices/{id}` must carry only the fields being changed.** Sending the full
  `DeviceUpdatePayload` shape, whose unset properties serialize as explicit nulls, answers
  **500 Internal Server Error** with a plain-text body. The same request carrying only
  `sync_config` answers 200 and leaves name, platform and client version intact.
- **`POST /api/auth/device/init` is 10/min/IP**, which the live test suite hits on its own
  once several tests each pair. Live catalog tests share one pairing per class.
- **An unknown query parameter on `/api/roms` is silently ignored, so a misspelt scope is the
  whole library.** Measured: `platform_ids=<psx>` answers `total=9500`, while `platform_id=`
  singular and a parameter invented on the spot both answer 200 with `total=83131`. There is
  no 422, no warning, and no echo of what was actually applied, so **a scope typo cannot be
  told apart from a scope that genuinely matches everything** by reading the response.
  `CatalogQuery` sends the plural and is correct today; the cheap guard is a resolve-time
  assertion that a scoped walk's `total` is below the library total. See
  [freegosy-findings.md](freegosy-findings.md), F14.

#### Platform mapping is a feature, not a lookup table

The two projects' platform vocabularies genuinely diverge, and treating this as "invert
the shipped YAML" would fail on roughly a third of a real install. Measured against
`system/configgen/systems_names.lst` on `RetroBat-Official/retrobat` and
`UniversalPlatformSlug` in `backend/handler/metadata/base_handler.py`:

| Fact                                                          | Count  |
| ------------------------------------------------------------- | ------ |
| RetroBat systems                                              | 240    |
| RomM known platform slugs                                     | 457    |
| Explicit pairs in `examples/config.batocera-retrobat.yml`     | 167    |
| **RetroBat systems with no mapping (37%)**                    | **91** |
| Of those, resolved by case/punctuation normalization alone    | 16     |
| Still unresolved after normalization                          | 75     |
| Shipped mappings pointing at folders RetroBat no longer lists | 18     |
| RomM slugs mapping to **more than one** RetroBat folder       | 13     |

The pair and stale counts were 168 and 19 until M2 built the table from the same file.
`reference/verify.py` split the YAML on the first `platforms:` and matched every key at four
spaces, which also catches `scan.gamelist.export`, a boolean and not a platform. Both the
script and this table now read the block by indentation. Nothing upstream moved.

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
   translation is needed. Try this before any table. On the live instance M2 was built
   against, this layer alone answered most of the 123 platforms.
3. **Bundled `data/retrobat/platforms.json`**, seeded from the YAML but corrected against
   `systems_names.lst` (fix the 18 stale entries) and shaped as slug → **ordered list** of
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

- **Arcade needs its own decision, not a mapping row, unless the library already made it.**
  Which of the ten folders is correct depends on the romset the file came from, and arcade
  ROM names are romset-versioned, so v1 requires an explicit user choice per arcade sync set
  rather than guessing. The exception is the `fs_slug` match above, which runs first: a
  platform whose `fs_slug` already names a folder this install has needs no choice, because
  naming the folder is the choice. M7 stage 7b-2a measured the cost of refusing anyway, on
  RomM's "Arcade (FinalBurn Neo)" against an install with an `fbneo` system.
- **Two RomM platforms can legitimately share one folder** (a user may point both `snes`
  and `sfam` at `snes`). See M4, which must therefore key gamelist generation by folder
  rather than by platform.

#### File extensions come from RetroBat, never from RomM

RomM will happily hold a file the target system cannot launch. Syncing it produces the
worst failure mode this app has: a game that appears in EmulationStation, looks correct,
and dies on launch. So the accepted-extension list is a **sync filter**, not a display
detail, and RetroBat is the only authority on it.

**The folder is `<path>`, not `<name>`.** They are different vocabularies and the shipped
8.2.1 file disagrees on five systems: `gw` writes to `gameandwatch`, `powerbomberman` to
`pb`, `casloopy` to `loopy`, `Windows` to `windows`, and `starship` appears **twice**, once
for `ghostship` and once for `starship`. Keying on `<name>` loses a system outright and
mismatches four more. Four further entries own no folder under `roms/` at all (`library`,
`screenshots`, `kodi` and the `retrobat` menu system) and one, `mess`, declares no path;
none of them is a sync target. Match folders case-insensitively, because the file does not.

**8.2.1 is why this is read live.** It added `.decomp` to eleven systems (`mame`, `model2`,
`model3`, `snes`, `n64`, `gamecube`, `wii`, `psx`, `ps2`, `ps3`, `xbox`) for decompilation
projects and `.zar` to `ps4`. A bundled list would have silently refused to sync those files
on an install that can launch them, which is the same failure as syncing one that cannot,
pointed the other way.

`es_systems.cfg` carries the extension list per system, and it is read from the live install
rather than bundled, because it reflects that machine's actual emulator configuration:

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
- **Passing the filter is not evidence the file will launch.** `<extension>` is the authority
  on what EmulationStation indexes and offers, not on what the emulator behind the system can
  consume. The measured case is `.m3u`: `ps2` lists it, and RetroBat's wiki says "PCSX2 does
  not support m3u usage for multi-disc games". ES shows the playlist, `emulatorLauncher` hands
  it over, and the emulator does not understand it, which is the same
  appears-in-ES-and-dies failure this section exists to prevent. The extension list stays
  necessary; treat per-emulator capability as a separate fact the config does not carry.
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

- Download `GET /api/roms/{id}/content/{fs_name}` with **`Range: bytes=0-` on single-file
  ROMs only**. That answers 206 with a `Content-Range` and an `ETag`, and resumes cleanly.
  **Sending any `Range` on a multi-file ROM is refused 403 by nginx** (measured: `bytes=0-`,
  a bounded range and a mid-file range alike), and the plain request that does work carries
  no `ETag` and no `Accept-Ranges`, so a multi-file download is not resumable by any header.
  The earlier reading, that a `Range` header is what selects a resumable cached-zip path, is
  withdrawn. See findings 78 and 79.
- **Multi-file ROMs are out of scope for v1, and M3 gives them their own exclusion state**
  rather than letting the extension filter catch them, because telling someone their
  `.bin`/`.cue` set is the wrong _format_ sends them to fix the wrong thing. What a later
  milestone has to build is extraction: the served zip is the only form on offer, its
  `Content-Length` is stable on GET but wrong on HEAD, the ROM-level hashes describe neither
  it nor its members, and per-member `md5_hash` values do exist on `files[]`. See finding 83.

  **Finding 83's biconditional is half withdrawn, and the half that fails is the one a reader
  would lean on.** It said every multi-file ROM carries an empty `fs_extension` and every
  extensionless ROM is multi-file, 105 of 105 both ways. Re-measured over 2,000 ROMs on the
  same instance: **every ROM flagged `has_multiple_files` does carry an empty `fs_extension`,
  209 of 209, but only 209 of the 602 extensionless ROMs are multi-file.** The other 391 are
  `has_nested_single_file`, an ordinary ROM stored inside a folder, and 157 of those hold
  exactly one file. The schema carries **three** shape flags, not one:
  `has_simple_single_file`, `has_nested_single_file`, `has_multiple_files`. An empty
  `fs_extension` covers the last two and cannot separate them.

  The code was already right: `SetResolver.cs:242` keys the exclusion on `HasMultipleFiles`,
  never on the extension, so nothing mis-excludes today. Had anyone taken this plan at its
  word and simplified that check to the extension test it called equivalent, 391 of 602
  extensionless ROMs in that sample would have been wrongly excluded.

  **The seam a later milestone owns is a third state, not a second.** A
  `has_nested_single_file` ROM falls past the multi-file check into the extension check with
  an empty `fs_extension`, matches no `<extension>` entry, and is reported as "skipped, format
  not supported by this system" with the extension shown as `(none)`. For a Switch `.nsp`
  sitting in a folder that is the wrong sentence, and it is exactly the failure the multi-file
  state was added to avoid. See [freegosy-findings.md](freegosy-findings.md), F15.

- **A multi-disc set is not a playlist.** Across 445 multi-file ROMs in that sample, **not one
  carried a `.m3u` member**. Multi-disc titles are several images with the disc number in the
  member filename and no playlist at all, so whatever builds multi-disc support has to
  generate the `.m3u` from the member names rather than expect one in the payload. The
  commonest multi-file shape on that library is not discs at all: it is PS3 `.pkg` plus its
  `.rap` licence sibling. See finding F16.

  **Generating it is necessary and not sufficient, because `.m3u` support is per emulator.**
  A real `psx` folder holds three layouts at once: a single disc, three loose discs with no
  playlist, and a folder containing two discs plus a hand-made `.m3u` whose lines are bare
  filenames. RetroBat's wiki documents a fourth, the playlist flat beside the discs. **And the
  extension list cannot tell you which systems can use one**: `ps2` lists `.m3u` in
  `es_systems.cfg` while RetroBat's own wiki says "PCSX2 does not support m3u usage for
  multi-disc games" and sends the user to the emulator's quick menu instead. 44 of 243 systems
  list `.m3u`; how many can consume one is a per-emulator fact the config does not carry. See
  [freegosy-findings.md](freegosy-findings.md), F18.

- Adopt files already on disk: hash local ROMs and match on `md5_hash`/`sha1_hash`, or
  query `GET /api/roms/by-hash`, so an existing library is not re-downloaded. `by-hash`
  answers a hit in 133-385 ms and a **miss in 8.3 s**, so it attributes a handful of unknown
  files and is never a library-wide sweep.
- **All three of `md5_hash`, `sha1_hash` and `crc_hash` describe the _uncompressed_
  content**, not only `crc_hash` as this plan previously said. A `.zip` reports the hashes of
  the file inside it. So verification hashes inside a single-entry archive, adoption does the
  same to a local one, and comparing an archive's own bytes against `md5_hash` is always
  wrong. Where a multi-entry archive makes that rule meaningless, fall back to size and say
  so. See finding 80.
- **Not every ROM has a hash**: 91.0% carry md5 and 96.3% sha1. Verification degrades to
  size when the server has none, and reports which check it made.
- **Only `.zip` can be looked inside**, because it is the one archive format the base class
  library reads and reaching `.7z` means a new dependency. A `.7z` is therefore verified by
  size alone and says so. RetroBat accepts both formats for many systems, so this is a real
  and stated limitation rather than an oversight, and the fix is one package away when it
  earns its place. **`.rar` is in the same position**, and appears in real `<extension>` sets
  alongside `.7z`, so it degrades the same way.
- **An archive the code cannot see inside is hashed as a file, and that hash is evidence only
  when it agrees.** A `.7z`, a `.rar` and a multi-entry `.zip` are all hashed as their own
  bytes while the server's hash describes content, so a mismatch between the two says nothing
  and must not refuse the file: treating it as a mismatch fails a correct download, deletes
  it, and repeats the whole transfer on every run after that. **This governs adoption as much
  as verification**, or a user whose library is `.7z` re-downloads all of it every sync. A
  `.zip` that will not open at all is damaged rather than opaque, and there the mismatch
  stands and the file is refused.
- Enforce the per-set and global disk budget. **Two bounds, not one**: the budget counts what
  RomMBat downloaded, and a free-space floor covers the drive as a whole. Counting a user's
  own library against the budget would leave the app permanently over its cap, unable to fetch
  and unable to evict its way out, because it must never delete a file it did not download.
- Eviction is a first-class operation and a dry run by default: it shows what would be
  removed before anything is, and refuses to evict anything with unflushed local saves.
  **From M4 it takes a ROM's media and its gamelist entry with it**, and still never touches
  a file RomMBat did not download, which is what keeps a user's own scraped art safe.
  **Two of the plan's three eviction policies cannot be honoured yet, and the code says so
  rather than ignoring them.** "Keep favourites" needs a fact RomM does not carry on a ROM
  (favourites are collection membership) and "keep the last N played" needs the play sessions
  M6 owns. What M3 can order by is real: departures first, then games no set claims, then the
  lowest-ranked members of a set, with the ROM id breaking every tie so a dry run and the run
  that follows it agree.
- **What M6 has to connect to the save guard.** Eviction asks `SaveGuard` before removing
  anything, and today it answers from the two seams migration 001 already declared: an unsent
  `outbox` row for that ROM, and an `open` `journal` entry naming its path. It fails closed,
  so an unreadable store refuses rather than assumes. What it cannot yet see is a save file
  sitting on disk that nothing has ever uploaded, because attributing a file under `saves/` to
  a ROM needs the save shapes M6 owns. **When M6 lands, the guard grows a third question and
  this is the place it goes**; until then the gap is covered by eviction never touching a file
  RomMBat did not download.
- **Reconcile deletions through re-resolution, not through `GET /api/roms/identifiers`.**
  That endpoint answers **504 after 300 s** on 83,131 ROMs and takes no parameters, so it can
  be neither scoped nor paged, and the reconcile it was supposed to drive would never
  complete on the libraries this project exists for. Every ROM RomMBat holds belongs to a
  set, and M2 already marks a member a completed walk no longer finds as `departed`, which is
  the same fact arriving by a cheaper route. The endpoint is still attempted under a short
  budget, because it is quick on a small library, and its answer is a cross-check for
  orphans rather than the mechanism. See finding 81.
- Resume cleanly from `.part` files after a power loss or a Wi-Fi drop mid-download.
  **`.part` files live under `emulators/rommbat/partial/`, not beside the target**, so a
  power loss cannot leave a partial file in a folder EmulationStation scans. The finished
  file is renamed into place only after it verifies.
- **That directory needs sweeping, and neither of this milestone's two bounds can do it.**
  The budget counts through `local_file`, which has no row until commit, and the free-space
  floor reads the volume live, so an abandoned transfer is bytes gone from free space
  attributed to nothing. `evict` runs the sweep. It keeps a ROM partial while an enabled set
  still claims the game, keyed on **membership rather than age** because a transfer waiting to
  resume is indistinguishable from an orphan on disk. M5 and M6 added four more producers here
  (`bios-`, `save-`, `resolve-`, `unit-`), none of which resumes, so anything of theirs left
  behind is dead. **Each is matched on the whole name its producer writes, never on a prefix**,
  or `partial/save-notes.txt` becomes a candidate.
- **The sweep runs under the tree lock, because one of those candidates is live state.**
  `partial/unit-<guid>/` is where a class C restore extracts a unit before swapping it into a
  shared container, and no handle protects it: the archive's writers close inside the extract
  loop. A recursive delete landing in that window succeeds and leaves the container half
  swapped, which is the state the `Remove`-before-`Move` ordering exists to prevent, reached
  from outside the restore. A sentinel file inside the staging directory does not close it,
  because a recursive delete takes the siblings before it reaches the sentinel. So the sweep
  takes `TreeLock` and does nothing without it, and `saves resolve` takes it too, since it runs
  the same restore a flush does. Producers outside that lock (`sync`, `bios`) hold their partial
  with `FileShare.None` while writing, and a sweep that loses that race costs a transfer that
  starts again rather than data.
- **A `.part` that is already complete is verified and renamed, never resumed.** The body is
  flushed before the verify and the rename, so the power-cut window this whole design exists
  for leaves a complete, correct partial file and a live row. Asking to resume from the end of
  it is answered **416**, identically on every run, and the file that would have verified is
  never offered to the check that would have passed it. A resume point the server does refuse
  discards the partial file rather than keeping it, because keeping it plans the same refused
  request forever, and the message already tells the user it was discarded.
- **The `If-Range` validator is recorded when the response headers arrive, not when the body
  ends.** A transfer that finishes has its row deleted by the commit that follows it, so a
  validator written at the end is only ever written to a row that is about to go. The row that
  needs one belongs to the transfer that died, and that transfer never reaches its own end.
- **Detect the target filesystem before writing.** On FAT32, refuse or skip ROMs above
  4 GB with a clear message rather than failing partway through a large write; that is
  **3.05%** of a real library. Removable media is also slow and prone to disconnection, so
  surface throughput and fail gracefully on a yanked drive.
- Compare by `content_hash` first and mtime second, since exFAT and FAT32 store coarser
  timestamps than NTFS and a mtime round-trip is not bit-stable across filesystems.
- **The download needs its own request timeout.** `RomMClientOptions.RequestTimeout` bounds
  the whole response body, so the 30 s that suits an API call would abort every large ROM.
  Downloads run with no overall deadline and a stall watchdog on the read loop instead, and
  still classify the failure: a yanked drive and a user cancelling must not read alike.

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
- **Metadata rides the walk M2 already makes, and costs no extra request.** The paged read
  returns `SimpleRomSchema`, which carries `metadatum`, `summary`, every media path,
  `regions` and `languages`; M2's slim `RomRow` simply throws them away. `GET /api/roms/{id}`
  returns `DetailedRomSchema`, whose only additions are seven user arrays that were empty on
  every ROM tried, at 0.15 s per request, which is **150 s for a 1,000-game set** against
  zero. And `GET /api/roms` has no id-list parameter, so "metadata for exactly what is on
  disk" is not a query that can be asked. What M4 adds is a metadata row per **member**,
  written during resolution and read back offline. See findings 93 and 94.
- **Five conversions, none of them a copy, and each wrong quietly rather than loudly.**
  `first_release_date` is **milliseconds**, not seconds (read as seconds every value lands in
  year 0). `average_rating` is **0-100** against a gamelist `<rating>` of 0-1. `genres` and
  `franchises` are arrays against single-valued elements, joined with a comma and a space because that is
  what a real scraped install already contains (`Racing, Driving`), and `franchises` needs
  deduping first. `regions` and `languages` use a different vocabulary in both directions:
  `Japan` against `jp`, `English` against a comma-joined `en,fr`. Only `player_count` is a
  straight copy, because RomM already writes `1-2`. Findings 95 to 100.
- **`<developer>` carries the company list and `<publisher>` is not written at all.**
  `metadatum.companies` merges both roles into one flat array and sorts it alphabetically on
  4,197 of 4,197 rows that have one, so a positional reading is reading the alphabet, and
  Chrono Trigger reads `['Squaresoft', 'Squaresoft']`. No provider block separates them
  either. Writing the joined list into both fields would assert that BioWare published KOTOR;
  writing it into `<developer>` alone asserts only that these companies were involved.
  Finding 98.
- Download media into `images/`, `videos/`, `manuals/` next to the ROMs, named after the
  ROM file, per RetroBat's scraper convention: `<stem>-image.png`, `<stem>-thumb.png`,
  **`<stem>-marquee.png` (also under `images/`)**, `<stem>-video.mp4`,
  `<stem>-manual.pdf`. The marquee comes from **`ss_metadata.logo_path`, not
  `marquee_path`**, which is what upstream's own exporter does: EmulationStation's marquee is
  game logo art while ScreenScraper's marquee is an arcade cabinet marquee. It is the one
  field sourced from a provider block rather than a ROM-level one, so it is absent for about
  a fifth of a real library. Media counts against the disk budget, and it is not a rounding error:
  at the measured medians a game costs 525 KB of cover, 104 KB of thumbnail, 445 KB of
  marquee, 1.99 MB of video and 2.45 MB of manual, so a 100-game `nes` set is ~12.8 MB of
  ROMs against ~550 MB of media. **The default fetches covers, marquee and video, and leaves
  manuals opt-in**, which is ~3.1 MB per game and matches what this milestone is done-when.
  Finding 92. **Amended in 7b-2b**: video and manuals follow RetroBat's own scraper switches
  rather than this default, and RetroBat ships both on, so a stock install fetches manuals too
  and only a user who turned a switch off gets fewer kinds. Finding 238.
- **Media comes off static resource paths, not the `/api/roms/{id}/content` route M3 built**,
  and three things about them decide the code:
  - **Never use `url_cover` or `url_manual`.** They are ScreenScraper API URLs carrying a
    third party's credentials in the query string: off-LAN, which breaks principle 1, and not
    ours to send. Finding 88.
  - **Two path shapes.** `path_cover_small` and `path_cover_large` are already rooted at
    `/assets/romm/resources/` and carry a `?ts=` query holding a **raw space**;
    `path_manual`, `path_video` and `ss_metadata.logo_path` are relative to that prefix.
    Normalise onto the prefix exactly once, and drop the query. Finding 89.
  - **The wrong prefix answers 200.** Requesting the relative form as given returns the web
    UI's `index.html`, 5,826 bytes, with an `ETag` and `Accept-Ranges`, which would be
    written to disk as a PDF. Check the content type, not just the status. Finding 90.

  The good news is the rest: nginx serves media with ranges, an `ETag` and a 416 past the
  end, so M3's resume machinery applies unchanged, and no token is needed at all.

- **Names RomMBat constructs, from names it was given.** The ceiling is the **255-character
  file name**, not `MAX_PATH`, and `\\?\` does not lift it because it is a filesystem
  component limit. Sanitise before the write: `<>"|?*` and the separators fail loudly, but
  **`:` does not fail at all**, it writes an NTFS alternate data stream, so the call succeeds
  and the file the gamelist names is not there. Findings 109 and 110.
- **Merge, never clobber, on an allowlist of the fields RomMBat owns.** The four fields this
  plan used to name are not the surface: a real install's 4,531 entries carry `playcount`,
  `lastplayed` and **`gametime`**, and no `favorite` or `hidden` at all, plus `scrap` with its
  attributes, `id` and `source` on `<game>`, `cheevosHash`, `cheevosId`, `md5`, `crc32`,
  `arcadesystemname` and `multidisk`. Read the existing gamelist, replace only the elements
  RomMBat writes, leave every other node exactly where it is, and write atomically via temp
  file plus rename. Finding 102.
- **Do not depend on ES preserving what it reads.** When ES has a reason to rewrite the file
  it **drops every XML comment**, moves the entry it changed to the end, rewrites that
  entry's children into its own order, and prunes `<hidden>false</hidden>` as a default.
  Unknown elements and attributes do survive. When it has no reason, it leaves the file
  byte-identical, mtime included. Findings 103, 104 and 105.
- **Then call `GET http://127.0.0.1:1234/reloadgames`.** M0 measured that ES holds a stale
  in-memory model until asked to reload, and rewrites `gamelist.xml` from that model on
  exit. Write-then-reload makes the edit stick **and** shows it immediately without a
  restart; writing without reloading risks ES serialising its stale copy over the top. ES
  writes no `<game>` entry for a rom it has no metadata for. Two measured limits:
  - **It is ignored while a game is running**, 200 in 1 ms with nothing happening, exactly
    as `/quit` and `/emukill` are. A 200 is not evidence. Finding 107.
  - **A refused loopback connect costs 2.04 s**, so the project's 2 s interactive
    `ConnectTimeout` buys nothing here. ES being absent is the ordinary case for a background
    sync, not an error, so this client gets its own much shorter budget. Finding 108.
- **A stale entry is inert, and still worth removing.** ES does not list a `<game>` whose
  `<path>` names a file that is not on disk, so an entry left behind by an eviction is not a
  phantom game, but it does survive ES's own rewrite. Eviction removes a ROM's gamelist entry
  and its media in the same pass. Finding 106.
- Generating the gamelist client-side is correct here. RomM's
  `POST /api/export/gamelist-xml` writes into the _server's_ library folders, which is a
  different machine.
- **What M7 reuses.** The ES menu entry needs the same merge-and-reload against
  `system/es_menu/gamelist.xml`. M4 builds that as two components with a seam, not as one
  gamelist writer: a merge that owns "read this file, replace these elements, preserve
  everything else, write atomically" with no knowledge of ROMs, and an ES client that owns
  the reload. M7 supplies its own entry and reuses both unchanged.

**Done when:** ES shows box art, descriptions and videos for synced games, and a user's
manual metadata edit survives the next sync.

### M5: BIOS and firmware

A platform synced without its BIOS is dead weight in the gallery, so firmware is
**prioritised ahead of ROM content** for any platform being synced, and driven by what
RetroBat actually requires rather than by whatever the RomM library happens to hold.

**RetroBat ships the requirements manifest, and it is not a file.**
`batocera-systems/Resources/batocera-systems.json` (in `emulatorlauncher`) is machine-readable
and complete: 100 systems, 355 BIOS entries, each `{"md5": ..., "file": "bios/<name>"}` giving
both the hash and the exact destination path. **A real RetroBat 8.2.1 install contains no such
file.** The data ships as a .NET string resource named `batocera_systems` inside
`emulationstation/batocera-systems.exe`, and it is the vendored copy byte for byte apart from
a trailing newline. Re-checked on 8.2.1: the embedded resource is still identical to the
refreshed `reference/batocera-systems.json`, sha256 `e26811ef…`, so the bundling decision
survives the version move rather than needing a re-derivation. So the `es_systems.cfg` precedent, "read the live copy, the vendored one is
a template", has nothing to read: **the manifest is bundled in `data/retrobat/bios.json`**,
generated from `reference/` by `tools/build-bios-manifest.py` and embedded like
`platforms.json`. The wiki's per-system BIOS pages are prose over the same data and are useful
for user-facing text, not for logic.

**Join on md5, and do not trust RomM's `is_verified`.** The two projects' firmware
knowledge overlaps far less than expected. Measured against RomM's
`backend/models/fixtures/known_bios_files.json`:

|                                    |        |
| ---------------------------------- | ------ |
| Distinct md5s RetroBat requires    | 156    |
| Distinct md5s RomM knows           | 353    |
| **Overlap**                        | **63** |
| RetroBat-required, unknown to RomM | 93     |

So 60% of what RetroBat needs will never be flagged `is_verified` by RomM even when the
user has the correct file. The two also key differently: RomM by `platform_slug:file_name`,
RetroBat by destination path. **md5 is the only reliable join.** Filenames will not match
and must not be relied on.

**But md5 cannot join a `.zip`, and 84 of the 353 requirements are zips.** Only **20 of
those 84 carry an md5** (18 distinct), and they are the only ones that reach the library
join at all: the other 64 name no hash, so `BiosPlanner.Inspect` returns `Unverifiable` and
returns before the join. Measured 2026-08-25 against a library holding 708 firmware records:
of the 20 zip requirements RetroBat names an md5 for, **zero match**, and it is not a low
rate, it is none. `neogeocd` is the clean case.
RetroBat requires `bios/neogeo.zip` at `dffb72f1...` and `bios/neocdz.zip` at `c733b4b7...`;
the library holds files named exactly `neogeo.zip` and `neocdz.zip` at `c74b8945...` and
`c38cb8e5...`. The archives are the same BIOS and hash differently, because **a zip's md5 is
over container bytes** that depend on compression level, member order and stored timestamps.

**This plan already knows that argument and applied it somewhere else.** The save-sync
design at "Hashing zip bytes makes RomMBat and Grout disagree" resolves exactly this by
defining `content_hash` over sorted relative paths plus per-file hashes and treating the
archive as transport only. The same reasoning governs a BIOS zip and was never carried
across, so M5 currently reports `MissingFromLibrary` for a file the library is holding under
the right name. **The ceiling is 20 requirements, 5.7% of the manifest**, not the whole 84:
the 64 zip requirements with no md5 already report `Unverifiable`, which is the honest
verdict this section argues for five paragraphs down.

**The seam:** a zip requirement that carries an md5 wants the member-wise comparison
`LogicalContentHash` already computes for save archives, not a hash of the container. Separately, 20 of the 179
requirements RetroBat names no md5 for have an **exact filename match** in the library;
`Unverifiable` remains the honest verdict under an md5-only rule, but an exact-name match is
worth offering as a suggestion the user confirms. Neither changes the general join, and rule
3 stands: across the whole manifest, filenames disagree at scale and md5 is still the key.
See [argosy-findings.md](argosy-findings.md), A3.

**156, not the 157 this table used to say.** `verify.py` built its set without filtering the
empty string, and 179 of the 353 entries carry one. The same fault moved "unknown to RomM"
from 94 to 93; the overlap was never affected, because RomM's side has no blank hash to
match. Our counting, not upstream drift, in the same family as the YAML parser fault recorded
in `reference/README.md`.

**Those 179 blank entries are a third state, not a gap in the user's library.** They span 49
systems, and **28 systems have no joinable entry at all**, `mastersystem`, `ngp`, `ngpc`,
`sega32x`, `atarist` and `cdi` among them. An md5-only join can say nothing about these in
either direction, so they are reported as "RetroBat names no hash for these, so RomMBat
cannot check them" and never counted as missing. The rule also settles two categories for
free: all 64 `bios/mame/` entries are blank, so MAME's software lists are out of scope
without a special case, and so are all 7 entries that land outside `bios/` under
`emulators/jynx/` and `emulators/dolphin-emu/User/Triforce/`. **`bios/` is an enforced
prefix anyway**, so a future manifest entry that grows an md5 outside it is refused rather
than written into an emulator's install directory.

The flow per synced platform:

**Live measurement, and it is less harsh than the fixture comparison above.** Of the **156**
md5s RetroBat requires, a real 123-platform library holds **49**, and holds **46** of them as
bytes the server can actually serve. Against that 49, an `is_verified` filter loses **6** and
a filename join loses **2**. Both joins still lose files the user has and the emulator needs,
and the named renames are real (`SegaCDBIOS9303.bin` for `bios_CD_U.bin`, `flash.bin` for
`dc_flash.bin`, `sega_100.bin` for `saturn_bios.bin`, `pcfxbios.bin` for `pcfx.rom`,
`bios.col` for `coleco.rom`), but for four of the five the library also holds a copy under
the name RetroBat wants, which is what saves a filename join from losing them.

**The 11 and 10 this section used to quote were an artefact of the probe.** F21 keyed a
dictionary by md5, so with 235 md5s sitting on more than one platform row, whichever row
landed last decided both answers. A client joins on md5 across every row and takes any hit,
so the honest figures are 6 and 2. See [freegosy-findings.md](freegosy-findings.md), F21.

1. Resolve required BIOS from the bundled manifest for that RetroBat system. The manifest is
   keyed by **batocera system names**, a third vocabulary beside `es_systems.cfg`'s `<name>`
   and `<path>`: 97 of its 99 keys are exactly a `<path>` basename, and the two that are not
   (`astrocde` for `astrocade`, `msx` for `msx1`) are aliased in the bundled table. See the
   `platform-mapping` skill.
2. Join on `md5_hash`, ignoring both filename and `is_verified`.

   **Read the candidates off `GET /api/platforms`, not one `GET /api/firmware?platform_id=`
   per platform.** The platform list inlines a complete `firmware[]` array carrying
   `md5_hash` on every record: measured at 656 records across 79 of 123 platforms, all 656
   with an md5, in one 424 KB response taking 0.40 s, with `firmware_count` equal to
   `len(firmware)` on every platform and the same id set as the dedicated call. So a
   whole-library BIOS gap report is one request rather than 79.

   The per-platform endpoint stays the right call for a certification pass on one platform.

   **`missing_from_fs` means the row is not a match.** 142 of the 656 records carry it, and
   the content route for one answers **500** with a bare `Internal Server Error` rather than 404. Three of the 49 md5s this library holds are held only by such a row, so a join that
   ignored the flag would promise three files and fail mid-sync on each.

   **One md5 legitimately appears on several platform rows**, 504 of 656 records on the
   library measured, because a user may file one system under more than one folder and put the
   firmware under each. So a global md5 join returns several hits per required file: dedupe on
   md5 and take any one that is not `missing_from_fs`. Multiplicity is not ambiguity and is not
   a reason to download twice.

   **`psxonpsp660.bin` carries `is_verified: false` on every copy**, while its md5 is exactly
   what RetroBat requires. It is the sharpest instance of the rule above: filtering on that
   flag refuses the one file without which no PS1 game runs at all.

   This is an inlined array on a list endpoint, the same family as the `GET /api/collections`
   trap under core principle 2, but three orders of magnitude smaller: 424 KB for all 123
   platforms against 715 KB for a **single** collection entry. It is a cost to note, not a
   trap to avoid. See finding F5.

3. Download matches via `GET /api/firmware/{id}/content/{file_name}` and **write to the
   path the manifest specifies**, renaming as needed. **One download can owe several writes**:
   six required md5s name more than one destination, `coleco.rom`, `colecovision.rom` and
   `openMSX/share/systemroms/coleco.rom` being the same bytes and `saturn_bios.bin` being
   wanted at both `bios/` and `bios/kronos/`. No destination path ever takes two different
   md5s, so the path is the key and the md5 is not. Destinations reach **six** segments deep,
   so the writer creates directories and every constructed path goes through `RelativePath`
   and the filesystem-limit checks.

   **And it runs the other way too**: **six** destinations are required by more than one
   system, five of them carrying an md5, `bios/openMSX/share/systemroms/fmpac.rom` being
   wanted by all four MSX systems. A plan is built per system, so a destination arrives
   several times and must be **written, recorded and counted once**. Keyed on md5 alone, the
   second arrival copies the file onto itself and fails a pass that actually worked. The
   budget follows the same split: the bytes are charged to the network once per md5 and to
   the disk once per destination, because that is how many files land.

   Firmware uses Starlette's `FileResponse` and behaves exactly as M3's ROM route does:
   `accept-ranges: bytes`, an `etag`, a `content-range` on a 206, a byte-exact resume, **416**
   past the end, and a stale `If-Range` answered 200 with the whole body. Two differences
   worth knowing: the **file name in the URL is never read**, so the right id under any name
   serves the bytes, and the content type is guessed from the extension (`text/plain` for a
   `.rom`), so a type check may reject HTML but must not require `application/octet-stream`.

4. Skip files already present with the right md5. On a hash mismatch, warn and leave the
   existing file alone rather than overwriting something that works.

   **`bios/` is a shared tree and RomMBat owns almost none of it.** A real install holds
   **4,683 files and 373 MB** there before RomMBat writes anything, nearly all of it emulator
   data: `dolphin-emu` 2,508 files, `mame` 858, `nxengine` 436, blueMSX's `Machines` 296,
   plus openMSX's entire user-data directory, **save states included**. Exactly 3 files sat at
   a path the manifest names carrying the md5 it names. So a file present with the right md5
   that RomMBat did not download is **adopted as a fact, never as something it may later
   remove**, and eviction never touches this tree at all.

   **Adopting needs no server.** Recognising a file already at the path RetroBat wants is the
   manifest against the disk, so a pass whose only work is adoption runs with the network
   down, and a pass with nothing to download still has rows to write. Gating the apply on
   "is anything being fetched" would make adoption reachable only as a side effect of an
   unrelated download, and the fast path that skips re-hashing needs the row it writes.

5. **Report the gap.** Required BIOS with no md5 match anywhere in RomM is the single most
   useful thing this feature can tell a user, so surface it per platform as "needed, not
   in your library" with the expected filename and hash. Three states, never two: matched,
   missing from your library, and unverifiable because RetroBat names no hash.

   **The report is answerable offline**, from the bundled manifest plus what is on disk, which
   is what principle 1 requires of it. Without the server it splits into present and absent;
   with the server the absent half splits again into "RomM has it" and "not in your library".

**Budget and eviction.** Firmware counts against `content.max_bytes`, so `status` and
`budget` tell the truth about what RomMBat put on the disk, and is **never evicted**. Every
file the measured library can serve totals **18.5 MiB**, against roughly 550 MB of media for
a single 100-game set, so evicting firmware would free nothing measurable while leaving a
platform unable to boot.

**What triggers a pass.** Both: `sync` fetches a platform's BIOS before its ROMs, and a
`bios` command in the shape of `budget` and `evict` reports on its own, writing nothing
without `--apply` and answering with `--offline` too. `--dry-run` is `sync`'s flag and
belongs to no other command: previewing is the default here and writing is the opt-in. The whole-library report is one request, which is what makes the
standalone command cheap.

**Done when:** syncing a BIOS-dependent platform lands the right files at the right paths
with no manual copying, files RomM does not have are listed explicitly rather than
failing silently at launch, and BIOS is fetched before that platform's ROMs.

### M6: offline-first save, state and playtime sync

The milestone with the most protocol nuance. Read `backend/endpoints/sync.py` and
`backend/endpoints/saves.py` before writing code.

**M6 ships in three stages.** M1 through M5 were each one PR; this section is four independent
pieces and the one milestone where a missed detail loses a save rather than a download, so it
is split into review surfaces small enough to hold. The first cut is at the save-class
boundary. **The second cut, taken during stage 2, is at what each piece needs from Game-ID
attribution**, because that is the only hard dependency among the remaining pieces.

|                                                                          | Stage 2a                              | Stage 2b            | Stage 2c      |
| ------------------------------------------------------------------------ | ------------------------------------- | ------------------- | ------------- |
| Hooks, journal, lock file, `emulatorLauncher.log`                        | stage 1                               |                     |               |
| Play sessions, standalone ingest                                         | stage 1                               |                     |               |
| Class A and B saves, attributed by filename                              | stage 1                               |                     |               |
| Negotiate, upload, download, ack, complete, conflicts, atomic restore    | stage 1                               |                     |               |
| The logical-content hash                                                 | stage 1, defined for the general case | inherited unchanged |               |
| Save states, all 13 emulators                                            | **yes**                               |                     |               |
| Conflict resolution, `saves resolve`, pruning `replaced/`                | **yes**                               |                     |               |
| `SaveGuard`, widened to save states                                      | **yes**                               | **yes**, to C       | **yes**, to D |
| Game-ID attribution: journal, ROM header, and a third route              |                                       | **yes**             |               |
| Class C bundling, the save-unit grammar, the deterministic archive       |                                       | **yes**             |               |
| The class B batch report (`outbox.batch_key` stays unwritten, see below) |                                       | **yes**             |               |
| Class D conversion and the `es_settings.cfg` writer                      |                                       |                     | **yes**       |

**Why states go first and alone.** A save state needs no Game-ID attribution at all: every
`<file>` template in `es_savestates.cfg` is keyed on `{{romfilename}}`, and all twelve emulators
driven on a real install wrote the name their template predicted, so a state resolves through
the same `(folder, stem)` index a class A battery save does. Every other remaining piece is
gated on attribution, so states are the only one that can land without it. Conflict resolution
rides with them because it touches stage 1's code and nothing stage 2 adds, so the two surfaces
do not interact.

The first cut is at the class boundary rather than at the local/network boundary because that is
the only split where stage 1 is provably correct end to end: a local-then-network cut defers
every server-side surprise past a whole review cycle and leaves the offline simulation, the
suite this plan calls its highest value, with nothing to flush.

**Stage 1 satisfies the offline half of "done when" and not the breadth half.** Three games
played unplugged, one flush, and a newer save returning as a conflict are all provable on
class A. "One game from each save shape" is stage 2 by construction.

**Stage 2c adds the fourth and last shape, driven on hardware.** Armored Core 3 was opted into
a per-game PCSX2 memory card, the game wrote a save into it, RomMBat discovered and attributed
it by the ROM's stem, uploaded it, and the emulator loaded it back. The shared card it left
behind held saves for **11 distinct games**, which is the class D attribution problem measured
rather than argued. Eviction refused the ROM while that card was unsent and offered it after a
flush. See [retrobat-findings.md](retrobat-findings.md), 182 to 188.

**Stage 2b adds the third shape, driven on hardware.** A PPSSPP `SAVEDATA/` directory written by
the game itself went up as one archive, came back down as a conflict, was resolved, and the game
loaded what the restore wrote. The converted PS2 memory card was the last of the four and **2c
has landed it**, so the milestone's "done when" is answered at the end of this section.

**The pass also moved one of this plan's own assumptions.** Stage 1 designed conflict handling
around negotiate answering `conflict`. A real two-sided divergence does not take that route: it
negotiates as **`upload`**, because negotiate compares the hashes it was handed and the client's
mtime was newer, and the server then answers **409** because this device's sync record is stale,
which negotiate cannot see. So the 409 is the ordinary path to a conflict rather than the
exception, and it is recorded as one. See [retrobat-findings.md](retrobat-findings.md), 156.

**Stage 2a adds two of the four shapes the "done when" names and the sentence it ends on.** A
PCSX2 save state with its screenshot is provable here, and so is "a conflict **the user
resolves**", which stage 1 could detect and had no way to settle. The PPSSPP `SAVEDATA/`
directory is 2b and the converted PS2 memory card is 2c. **2c has landed and M6 is claimable;**
see the amended "done when" at the end of this section for what proved each shape.

**What stage 1 does with the classes it does not ship is report them, not ignore them.**
Everything class C, class D and every save state is recorded as unsyncable with a reason, so
a user is told that their PS3 saves are not going up rather than being left to notice.

**M3 left a seam here that has to be connected, and it is the one where being wrong destroys
data.** Eviction asks `RomMBat.Core.Content.SaveGuard` before removing any ROM, and today
that guard can only answer from an unsent `outbox` row or an `open` `journal` entry, because
attributing a file under `saves/` to a ROM needs the save shapes this milestone defines. Once
they exist, the guard grows a third question, "is there a save on disk that has never been
uploaded", and eviction stops depending on the outbox having been written first. Until then
the gap is covered by eviction never touching a file RomMBat did not download, which is a
mitigation and not an answer.

- **The hooks are journal-only.** `game-start` appends a start record; `game-end` closes it.
  No HTTP, no waiting on a lock.

  **M0 changed the reason, not the rule.** Hooks do **not** block game launch: ES spawns
  event scripts fire-and-forget and `emulatorLauncher` started 30 ms after the hook fired,
  three times out of three, against a deliberate 8 second hook sleep. So the constraint is
  not latency. It is **concurrency**: hooks overlap freely, and three `game-end` hooks were
  observed in flight at once, interleaving their writes to the same file. The journal must
  therefore survive interleaved appends from separate processes, and the lock file below is
  mandatory rather than defensive.

  The hooks resolve the agent relative to their own location, never an absolute path, so
  they keep working when the drive letter changes. Mind the depth: three levels reaches
  `emulationstation/`, so the agent at `emulators/rommbat/` is four levels up plus
  `emulators\rommbat\`. An exe hook takes that from its own module path, which ES sets as
  the working directory too, though nothing should depend on the working directory: a `.bat`
  hook is given its own folder as CWD while a `.ps1` hook is given ES's home.

- **`game-end` cannot identify its game, and may not have one.** It receives **zero**
  arguments, so it can only be paired with the preceding `game-start` in the journal. It
  also fires **without** a preceding `game-start`: ES-menu launches produce one, and so do
  launches that fail outright. **RomMBat's own exit will fire `game-end`**, since it is
  launched from that menu. An orphan `game-end` must be discarded, not attributed to
  whatever ran last.
- **`game-start` works, but only for an `.exe` hook.** M0 first read this as "ES never fires
  `game-start` for a game whose `<name>` contains a space". ES's own debug log refutes that:
  it fires the event and logs `executing:` for every script. What fails is the handoff to an
  interpreter, and it fails for `.bat` on any quoted argument and for `.ps1` on any
  parenthesis, both reproducible outside ES. An `.exe` hook received a full No-Intro name as
  three intact arguments. So the journal may open on `game-start` provided the hook is the
  agent exe. `game-end` is unaffected in every form and fires reliably, including on crashes.
- **Source the launch facts from `emulationstation/emulatorLauncher.log` instead.** It is
  written on every launch, timestamped to the millisecond, and is the only durable in-tree
  source that carries the rom path **together with** `-system`, `-emulator` and `-core`,
  which the hook withholds in any case (`$4` and `$5` arrive empty). That single source
  solves both problems at once, with a two-file rotation (`emulatorLauncher.log` plus
  `.log.old`), so the parser must read both and tolerate a rotation between reads.

  **M6 re-measured the file on the real library and the size figure this bullet used to
  quote, 268 KB for 5 weeks and 70 launches, describes a smaller install rather than the
  mechanism.** Live: **503,225 B, 2026-07-04 to 2026-08-16, 159 launches**, beside a
  **1,048,604 B** `.log.old` covering the three weeks before it at 265 launches. **Rotation
  is a size threshold near 1 MiB**, and the two files do not overlap, so reading `.old` then
  the live file yields launches in time order across the boundary. That is what a cursor has
  to survive; a per-launch rotation, which is what both ES logs do, would not be survivable
  at all.

  Six things about the file decide parser behaviour, and five of them are traps:
  - **730 `[Startup]` lines, of which only 424 are a game launch.** `emulatorLauncher.exe` is
    also invoked for `-updatestores` and similar. Keying on `[Startup]` over-counts by 72%;
    the discriminator is the presence of `-rom`.
  - **The rom path is rooted at whatever drive letter the install had at the time.** 295 of
    424 read `D:\RetroBat` and 129 read `E:\RetroBat`, in one continuous log for one install
    that moved. This is principle 4's own case appearing inside the file M6 depends on, so
    relativising by stripping the current root discards 70% of the history. Relativise on the
    `roms\<system>\` segment instead, and never store the result rooted.
  - **`-rom` is not a fixed shape.** It is unquoted once in 424, with spaces and parentheses
    in the path, so a `-rom "([^"]+)"` regex misses it. It is not the final flag 19 times,
    and `-core` is written **after** it 5 times, so a positional read misses those. Read the
    quoted form to its closing quote and the unquoted form to end of line.
  - **187 of the 424 launches never record `Process exited with code`.** End time cannot come
    from this file; `game-end`'s own timestamp is the end.
  - **The file opens with a UTF-8 BOM** and carries 15 unstamped continuation lines, .NET
    stack traces among them, so a line-per-record read has to tolerate both.
  - **An ES-menu launch is identifiable rather than inferred.** 27 carry `-system retrobat`
    with a `-rom` under `system\es_menu\`. So "RomMBat's own exit must not become a play
    session" becomes a rule keyed on observable data instead of a heuristic, which is
    stronger than this plan previously assumed it could be.

  Design the journal as: **`game-end` is the trigger, `emulatorLauncher.log` is the data.**
  That holds even though an exe `game-start` hook is reliable, because the hook is never told
  the system, emulator or core, and because `game-end` fires in cases that have no
  `game-start` at all. Use `game-start` to open the record and to corroborate, not as the
  source of truth.

  The hook's argument signature is `$1` absolute rom path, `$2` rom basename, `$3` gamelist
  display name, and an exe hook receives all three intact. Batocera documents `$3` as the
  system; that is wrong for RetroBat, and the system is not passed at all.

- **Do not assume the hooks run on every host.** In the M0 portable-move test the tree worked
  perfectly on a second machine while **no hook produced anything**. The rerun found the
  reason: that host cannot launch a `.bat`, because Notepad++'s installer took the `batfile`
  association, nor a `.ps1`, because the execution policy is the default `Restricted`. Every
  hook was a `.bat` at the time. An exe hook fires all four events there. How often this
  happens is unknown and not worth guessing from one machine in a sample of two; what matters
  is that **both failures are completely silent**, so when hooks produce nothing, check the
  association and the execution policy before concluding the events did not fire. Ship
  executables, and still
  write a heartbeat from the `start` hook, notice when play data exists with no corresponding
  hook activity, and report that state instead of silently losing every play session.
- **The flush has no daemon to live in.** A portable install cannot register a service or
  a scheduled task, so the outbox is flushed by a short-lived agent process. Design for
  one-pass-and-exit, and make concurrent invocations safe with a lock file in the tree.

  **`sync` flushes first, before anything else it does.** A user who never leaves ES and
  never opens the UI still gets their saves up, and a flush that has already happened costs
  one query.

  **Amended twice, and closed in M7 stage 7a.** The intent from the start was that `start`,
  `game-end` and `quit` each wake an agent and the UI drive one while it runs. Through M6
  none of that shipped: the hook wrote a spool file and exited without starting a process, so
  `sync` and a typed `flush` were the whole trigger set and an install nobody synced spooled
  events and sent nothing. It was tolerable only because draining is idempotent and a spool
  file waits indefinitely.

  **The reason recorded for not fixing it was wrong, and the measurement that settled it also
  removed the objection** (findings 195 and 197). ES does not wait for a hook: across 23 real
  launches the hook's own timestamp lands a median of 24 ms _after_ emulatorlauncher's, which
  then spends 0.5 s to 2.8 s before the emulator starts. And size was never the cost: the
  75.9 MB agent reaches `Main` in 34 ms while the 11 MB hook takes 60 ms, because trimming
  without `PublishReadyToRun` discards the framework's precompiled code.

  **So 7a has the `start` and `quit` hooks spawn `background <event>`, and leaves `game-start`
  and `game-end` spawning nothing.** That is CLAUDE.md rule 4 narrowed to what its own second
  sentence says rather than bent: the rule forbids a hook touching the network _because_ hooks
  run in the game-launch path, and only two of the four do. `game-end` was on the original
  wish list and stays off it for exactly that reason; the `quit` that follows it picks its
  work up. Driven on hardware: two sessions, `start` and `quit` each spawned a pass that
  reached the server and finished with exit 0, with no terminal used.

- **Hook installation happens on the first `sync`, announced, and `hooks uninstall` reverses
  it.** The opt-in rule that governs class D exists because flipping a memory card mode
  changes where an emulator writes and strands the saves already there. Installing a hook
  adds a file beside the existing scripts and changes nothing about how a game runs, so the
  same ceremony is not warranted; what is warranted is saying plainly what was added and
  where. Without hooks there is no playtime and no launch window at all, so making the
  milestone's headline feature off by default would be the worse failure.
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

  **The name to persist and the name to write on disk are different fields.** A file written
  under the tagged name is invisible to the emulator, which finds a battery save by matching
  the rom name, so `file_name` is kept only as the server-side identity. Freegosy hit this as
  its issues #42 and #28 and answered it by stripping the tag by hand.

  **Amended after M6 stage 2b: `file_name_no_tags` is not the name to write, and measurement
  130 half-saw this before a real save proved it.** The server does not undo its own timestamp
  tag, it runs a general tag stripper, and a real save measured live came back as
  `Phantasy Star (Brazil) [2026-08-17_17-01-00].srm` with `file_name_no_tags` of
  **`Phantasy Star`**: `(Brazil)` is part of the ROM's name and the server took it for a tag.
  Writing that produces a file libretro cannot see, which is the exact failure this rule exists
  to prevent, arrived at from the other direction.

  **The ROM's own filename stem is the only sound source**, joined to the extension, which is
  unambiguous from the tagged name. That is the same `(folder, stem)` key class A attribution
  already uses, run backwards. See [retrobat-findings.md](retrobat-findings.md), measurement 152.
  See [freegosy-findings.md](freegosy-findings.md), F6.

  **The 409 body carries nothing to show the user.** Measured at 5.1.1-beta.1 it is a bare
  string, `{"detail": "Slot has a newer save since your last sync"}`, not the structured
  `{error, message, save_id, current_save_time, device_sync_time}` other clients document. So
  surfacing a useful conflict needs a separate fetch of the save row for its timestamp and
  hash. The trigger is **this device's** sync record being stale for the slot, not the save
  being newest overall: the device that uploaded the current save may upload again, while a
  device that never synced it is refused. See finding F12.

  **Decide the retention policy rather than inheriting it.** `autocleanup` defaults to false
  and `autocleanup_limit` to 10, and a slot grows one row per genuine change forever without
  them. Measured: `autocleanup=true&autocleanup_limit=2` held a slot at exactly two across
  three further uploads, keeping the newest. This interacts with the `keep_both` conflict
  default, since `keep_both` under an unbounded slot is how a library becomes unusable. See
  finding F2.

  **What decides whether an upload appends is the clock, not `overwrite`.** Corrected against
  the server's own source at 5.1.0 and 5.1.1-beta.2 and measured live: a slotted upload is
  renamed to carry a `[YYYY-MM-DD_HH-MM-SS]` tag and the row is then looked up by **that**
  name, so two postings into one slot inside one second are one row and two a second apart are
  two. `overwrite=true` never replaces a row. What it does is suppress the 409 checks **and**
  the identical-content dedup, so an overwrite re-sending unchanged bytes makes a row where an
  ordinary upload would have reused one. Measurement 160, which withdraws the earlier reading
  in both this paragraph and measurement 150.

  **Decided, as one decision rather than two.** Every upload carries
  `autocleanup=true&autocleanup_limit=10`, and conflicts keep both, having first copied the
  local file aside. So principle 1's "default to `keep_both`, never silently overwrite" holds,
  and the growth it would otherwise cause is bounded at ten rows per slot by the server.
  Neither half works without the other: `keep_both` unbounded fills the library, and
  autocleanup without `keep_both` means a conflict silently discards one side.

  **Amended after M6 stage 1: keeping both does not mean uploading the local side.** The
  original wording said a conflict uploads without `overwrite` so the server appends a row.
  That is the wrong shape while there is no way to resolve a conflict. Appending makes the
  local side the newest row in the slot, so the next negotiate tells every other device to
  download it: an unresolved conflict would resolve itself in favour of whichever device
  synced last, silently, which is exactly what `keep_both` exists to prevent. It would also
  re-append on every flush, since nothing marks the conflict as handled. So stage 1 keeps both
  by holding the local side **local**: the file stays where it is, a dated copy goes to
  `emulators/rommbat/replaced/`, and the slot is reported unresolved. Nothing is overwritten
  and nothing is discarded. The upload with `overwrite=true` belongs to the resolution command
  that picks a side, tracked in issue #31, which is also where pruning `replaced/` lives.

  **Amended after M6 stage 2a: the resolution command exists, and it is the only caller of
  `overwrite=true` anywhere in this codebase.** `saves resolve <rom> <slot> --keep-local |
--keep-server` is the seam M7's UI binds to. There is deliberately **no default side**: either
  default silently discards somebody's progress, and the whole reason a conflict exists is that
  RomMBat cannot tell which side matters.

  `--keep-local` retries the upload with `overwrite=true`, which gets past the 409 and **appends
  a row rather than replacing one**, since no decision a person takes lands inside the same
  second as the save they are deciding against. The server's older copy stays one row down and
  `autocleanup_limit=10` is what bounds the slot, so a resolution is untidy rather than lossy.
  Measurement 160. **What makes it merely untidy is that negotiate pairs on the newest row per
  `(rom_id, slot)` and never looks at the rest**, so the copy the user rejected is history the
  moment the resolution lands and cannot be offered back to this device as a download.
  Measurement 163, read from the server's source at both the baseline and the running version
  and then driven against a live slot holding exactly that leftover row: the negotiate answered
  `no_op` on this device's copy and never mentioned the other, and an empty `saves` array
  mentioned neither. This is a dependency on server behaviour rather than a property of the
  client: were negotiate ever to volunteer a superseded row, the resolution would be undone by
  the next flush, because the client holds no sync record for the row it did not ack and
  `AlreadyHeld` compares hashes that by construction differ.
  A 409 that survives the overwrite means the slot moved again between the report the user read
  and the choice they made, so it is reported rather than forced. `--keep-server` runs the same
  verified restore an ordinary download does, one move for a single file and a per-member swap
  that rolls back on failure for a unit, and acks only after the bytes are written and checked. Both then prune the
  copy under `replaced/`, which is the first time anything in this codebase has been the "next
  successful sync" the retention rule was always written against.

  The conflict itself now lives in a `save_conflict` table rather than on an in-memory list, so
  it survives the flush that found it, and the copy aside is taken **once per conflict rather
  than once per flush**: a slot that conflicted and was never resolved used to gain one dated
  file per run with nothing pruning them, which was #31's third complaint.

- Download: `GET /api/saves/{id}/content?device_id=&session_id=`**`&optimistic=false`**, then
  `POST /api/saves/{id}/downloaded` with `{device_id}` **after the bytes are written and
  verified**, so the server records the sync only once the device really has the save.

  **`optimistic` defaults to true and that default loses saves on a flaky link.** Measured: a
  device that had never synced a save went from `is_current: false` to `is_current: true` by
  issuing the GET and nothing else, while the same request with `optimistic=false` left it
  false until the ack. A download that dies mid-body therefore leaves the server believing the
  device is current, the next negotiate answers `no_op` for that slot, and the save never comes
  down again, silently. This is the same discipline M3 landed for ROM content, where the
  `.part` file is verified before the rename. **The parameter and the ack have to travel
  together**; the ack alone is decoration, because by then the record is already written. See
  finding F1.

  **Open after M6 stage 1: a download has to have somewhere to go, and two cases still do
  not.** A restore writes `saves/<folder>/<file_name_no_tags>.<file_extension>`, taking the
  folder from the ROM's own `local_file` row, which covers a save this device once held and no
  longer does. Not covered: a slot this device has **never** negotiated, where the only name
  the operation carries is the tagged one and stripping that tag client-side is exactly what
  the rule above forbids; and a device holding no saves at all, which never negotiates, since
  the request is built from what is on disk. Both turn on whether the server returns operations
  for slots the client did not submit, which this branch did not drive live. Until that is
  measured, neither is worth guessing at.

  **Stage 2a settled this negatively, and M6 stage 2b withdraws that reading entirely.** 2a
  reported that negotiate never volunteers a slot the client did not submit, having sent an
  **empty** `saves` array and received `operations: []`. Re-driven at stage 2b, the same call
  returned **13 downloads across two ROMs**, one of which the client never named. The mechanism
  was then driven directly: 13 operations, then `GET /api/saves/134/content` with
  `optimistic=false` followed by `POST /api/saves/134/downloaded`, then 12 operations with that
  save no longer offered.

  **So negotiate returns a download for every save row the queried device has no current sync
  record for.** 2a's empty answer meant "nothing you do not already have", not "nothing is
  volunteered": that device was current for the only save then on the account. See
  [retrobat-findings.md](retrobat-findings.md), measurement 151, which withdraws 132.

  Two consequences, and neither is what 2a recorded. **The fresh-device gap closes for a save
  that is one file**: negotiating with an empty `saves` array _is_ the inventory pass, so a new
  install discovers the library's saves through the protocol it already speaks. And
  `SaveSlotStore.Map`'s fallback for a slot with no local file is **reachable**, not dead, so
  the two download cases stage 1 left open are open again and are answered below.

  **`SaveSync.RunAsync` returned before negotiating when the device held no attributed saves,
  which made that inventory pass unreachable from the shipped path.** The device with the
  strongest reason to pull was the one case that never asked: a freshly paired install with ROMs
  and no saves flushed to "nothing to sync" and made no negotiate call, and it only started
  pulling once it happened to write a save of its own. Removing the gate is half the fix; the
  other half is that a slot this device has never negotiated has no recorded identity, so the
  target is derived from the ROM's own folder and stem with the extension taken off the
  operation's tagged filename, which is the one part of that name safe to read. Fixed as #63.

  **It still does not close for a bundled one.** `SaveSync.DownloadAsync` selects the unit
  restore on the local row's shape class, so a download for a slot this device holds nothing in
  has no container to expand and no unit key to place under. It is **refused with a reason**
  rather than falling into the single-file branch, which would write a `.zip` under the ROM's
  stem and check it against `server_content_hash`, a digest this client cannot reproduce for an
  archive. The slot is recognised as bundled from the shapes table rather than from the
  filename. Closing it properly needs the container and the unit key derived from the server's
  row, which is a download-side grammar no measurement yet covers. **Still open after 2c**,
  which was expected to take it and did not: the bundled download grammar was cut from that
  stage's scope, and class C is the only shape it affects.

  **Amended after M6 stage 2c: it does close for a converted class D container, and that case
  had the opposite bug.** A converted card is one file whose name is the ROM's stem, and the
  shape declares the container it belongs in, so a device that has never run the game can be
  handed one. `ResolveTarget` was deriving `saves/<folder>/<stem><ext>` for it, which is right
  for class A and puts a PCSX2 memory card exactly where PCSX2 never looks, **quietly**: the
  bytes land, the ack is sent, and the flush reports success. It now asks the shape where the
  container goes, recognising the slot rather than the extension, because only the shape knows
  that a `.ps2` under `ps2` is a memory card.

- Close with `POST /api/sync/sessions/{session_id}/complete` carrying
  `{operations_completed, operations_failed, play_sessions:[...]}`.

  **Completing a session twice answers `400 {"detail":"Session is already COMPLETED"}`**,
  measured at 5.2.0, not the 404, 410 or 409 other clients special-case. A repeat close is
  therefore a non-event and safe to swallow deliberately, and every other failure is not.

  **Defect in landed M6 code, recorded and not fixed here.** `SaveSync` awaits
  `CompleteSyncSessionAsync` without reading the result and catches only
  `RomMUnreachableException`, while `PostAuthenticatedAsync` returns a failure rather than
  throwing. So **any HTTP failure to close a session is silently swallowed** and the pass
  still reports success. A 400 is harmless; a 403 from a token missing `assets.write` is
  not, and it is equally invisible. The fix is to read the response and add a problem to the
  outcome for everything except the already-completed case. Related: `FailureAsync` maps
  every unrecognised status, 400 included, to `RomMResponseStatus.ServerError`, so a client
  error is reported as a server one. See [argosy-findings.md](argosy-findings.md), A6.

- **States are not part of the negotiate protocol.** `POST /api/states` takes only
  `rom_id` and `emulator`, with no slot, device or conflict detection. Treat state sync as
  best-effort push, tracked locally, and say so in the UI.

  **M6 stage 2a drove it, because nothing in this repo had ever called it. Five results, and
  three of them change the client.**
  - **It is an upsert, not an append.** Three posts of one `file_name` reused a single row
    across two different payloads. So there is no slot history to prune, no `autocleanup` to
    ask for, and a replayed flush is idempotent for free. `PUT /api/states/{id}` works and is
    unnecessary.
  - **The upsert key is `(rom_id, file_name)` and the emulator is not part of it.** Five posts
    of one name under five different `emulator` values reused one row, overwriting the row's
    emulator and moving its stored file between directories while the id stayed put. **Two
    libretro cores writing one filename for one ROM therefore collapse into one server row and
    the second silently wins**, and that is not hypothetical: `libretro` declares
    `{{romfilename}}.state{{slot}}` while `gopher64` declares `{{romfilename}}.state{{slot0}}`,
    which render identically for slots 1 to 9, and both serve `n64`. **So the uploaded name has
    to carry the scope**, `<stem> [<emulator>[.<core>]]<ext>`, unconditionally rather than only
    where a collision is possible: a conditional rule gives two devices two names for one state,
    and two names is two rows. Two names differing only in a bracketed group were measured to
    produce two rows, so the group really does separate them.
  - **A state carries no `content_hash` and no `slot`**, confirmed in the live response as well
    as in the pinned schema. So this plan's "derive the RomM `slot` as `{emulator}:{core}:{slot}`"
    describes a **local** identity that never goes on the wire, and "does this state still need
    sending" is answerable only from the hash the device recorded when it last sent one.
  - **The server does not rename a state.** A save comes back tagged
    `<name> [YYYY-MM-DD_HH-MM-SS]<ext>` and a state comes back exactly as sent.
  - **A zero-byte `screenshotFile` is accepted and stored as a real screenshot row.** Given
    RetroBat's mirror races the emulator writing the image and a zero-byte result was measured,
    the client has to suppress the empty case, because nothing downstream will.

  One thing worth reporting upstream rather than working around: **the `emulator` query
  parameter is not sanitised.** It becomes a directory segment in the stored state's
  `file_path`, and `libretro/evil` was accepted and became two segments. RomMBat's own schema
  refuses a separator in that column, so it cannot send one.

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
game has independent state sets per core. RetroArch confirms this itself at launch:
`[Override] Redirecting save state to "...\saves\megadrive\libretro.genesis_plus_gx\<rom>.state"`.

**M0 parsed the shipped file and found four traps a parser written from the description
above would hit.** The live file is byte-identical to the vendored copy, and defines 13
emulators, which bounds state-sync coverage to those 13 rather than to the 244 declared
systems.

| Emulator   | Trap                                                                                                                                              |
| ---------- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| `libretro` | **Declares no `firstslot`/`lastslot` at all**, so "it yields the slot bounds" is false for the single most important entry. A default is required |
| `desmume`  | **`<image>` and `<file>` are the identical template**, so uploading `<image>` as `screenshotFile` would upload the state itself                   |
| `bigpemu`  | `firstslot="001"` is a zero-padded string, and `lastslot="999"` needs three digits while the template uses two-digit `{{slot2d}}`                 |
| `bizhawk`  | **Also core-scoped** (`{{system}}/bizhawk/sstates/{{core}}`), not just `libretro`                                                                 |

A commented-out `<core name="..." enabled="false"/>` mechanism ships disabled; the parser
must tolerate `<core>` children appearing, since a user can enable them.

**M6 stage 2a found that `libretro`'s trap is only a trap for a parser that expands a slot
range**, and built the parser the other way round. Compiling `<file>` into an anchored
expression and matching it against what is on disk reads the slot **off the filename**, so
`libretro` declaring no bounds needs no invented default. It settles one more question, which is
not in the table above: whether `{{slot}}` renders empty at slot zero stops being something the
client has to answer in advance. Declared bounds become a report rather than a refusal, since
the file on disk is evidence and the declaration is only a claim. `desmume` still needs
handling, because no reading of the file makes its `<image>` differ from its `<file>`.

**`bigpemu` reads as a contradiction and is not one, which measurement 166 settled by driving
it.** Six real states through the gamepad overlay: BigPEmu writes **three-digit** names in its
own tree, `emulators/bigpemu/userdata/game<ID>_state001.bigpstate`, keyed by an internal game id,
and RetroBat mirrors each to `saves/jaguar/bigpemu/<rom filename>_state01.bigpstate`, **two-digit
and rom-named**. So `firstslot`/`lastslot` describe the emulator's native range and `<file>`
describes the mirror; the two are not in conflict, and stage 2a's reading of them as a defect was
wrong in the other direction from the one it worried about. Reading the declared path is right,
and all six came back as slots 1 to 6 with nothing reported.

**The edges are still worth reporting, and are.** `StateScanner` carries a near-miss list: a name
matching an emulator's `<file>` template except for the width of its slot, and a slot outside the
declared `firstslot`/`lastslot`. Only the slot widens when looking for the first of those, so the
`.txt` sidecar and the screenshots in the same directory stay silent, confirmed against a real
install's whole state tree. Neither edge has been observed: a mirror name past slot 99 needs
about 94 more saves of one game, which is what #34 now stands on.

**Two things the same pass found, and neither is a save-state question.** BigPEmu's Jaguar
battery save, `game<ID>_eeprom.bigpeep`, is **never mirrored into `saves/`** at all (measurement
167), which is the same trap openMSX sets with its states and the concrete reason `jaguar` stays
in `save_shapes.json`'s `_unclassified` list. And its `.txt` sidecar holds the internal game id
its native filenames use (measurement 168), so the sidecar is the mapping between the two naming
schemes, which is what the sidecar is for everywhere else too.

The same reversal applies to `<directory>`: matching the template against directories that
exist recovers the system and the core from the tree, which answers `bizhawk`'s core scoping and
is the only reading that does not invent an emulator out of a directory name given that neither
level of the save tree is positional.

**The slot placeholder's width is load-bearing, not cosmetic.** `{{slot0}}` compiles to exactly
one digit, `{{slot2d}}` to exactly two. DeSmuME declares `{{romfilename}}.ds{{slot0}}` and writes
its **battery** save as `{{romfilename}}.dsv` in the same tree, so a one-character wildcard takes
the battery save for slot "v" and uploads it as a save state.

**Battery and internal saves are the hard half.** Classify each platform, store it in
`data/retrobat/save_shapes.json` next to the save-directory map, and handle per class:

| Class | Shape                              | Examples                                                                                                                              | Handling                                                                                                           |
| ----- | ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------ |
| A     | One file per game                  | RetroArch `.srm`/`.sav`/`.eep`, most standalone                                                                                       | Direct 1:1 map to a `Save`. Slot `{emulator}:battery`                                                              |
| B     | Several files per game             | `.srm` + `.rtc`, ScummVM `.s00`…`.s99`                                                                                                | **One slot per file**, `{emulator}:battery:{ext}`. Decided in M6 stage 1; see below                                |
| C     | Directory per game                 | PPSSPP `PSP/SAVEDATA/<GAMEID>/`, RPCS3 `savedata/<TITLEID>/`, Cemu, Citra, Wii NAND `title/00010000/<id>/data/`, MAME `nvram/<game>/` | Bundle to a single archive, following `grout/sync/zip_save.go`, which already handles the multi-directory PSP case |
| D     | One container shared by many games | PCSX2 `Mcd001.ps2` (default), Dreamcast VMU, **megacd `4Mbit_cart.brm`**, **xbox `eeprom.bin` + `xbox_hdd.qcow2`**                    | Convert to per-game via a RetroBat option where one exists. See below                                              |

**The save tree is two levels deep, not one.** M0 inventoried a real install: saves live at
**`saves/<system>/<emulator>/`**, and only libretro battery saves land loose at
`saves/<system>/*.srm`. There are also **emulator-named folders at the top level**
(`saves/dolphin/`, `saves/mesen/`, `saves/psxmame/`, `saves/amiga/`) sitting beside the
system folders, so `saves/dolphin/User/GC/SRAM.USA.raw` and
`saves/gamecube/dolphin-emu/User/GC/` coexist. Any path parser that assumes the first
segment is a system name will mis-attribute these. `data/retrobat/save_directories.json`
has to model both levels.

**M6 re-inventoried the tree and neither level is positional, in either direction.** The
consequence is that discovery cannot be built on path shape at all: **the shape definition
names the paths, and anything it does not name is reported as unknown rather than guessed.**

- **Nine top-level directories are not declared systems**, not the four this bullet names:
  `amiga`, `dolphin`, `gameandwatch`, `ghostship`, `loopy`, `mesen`, `pb`, `psxmame`,
  `windows`, against the 243 systems the live `es_systems.cfg` declares.
- **The second level is not an emulator either.** `mame/artwork`, `mame/cfg`, `mame/ctrlr`,
  `n64/sram`, `n64/games`, `n64/sstates`, `psp/SYSTEM`, `psp/Cheats`, `switch/user`,
  `switch/sdmc`, `rtcw/Main` and `dolphin/User` are all second-level directories that name
  no emulator. A parser that reads the second segment as one invents emulators.
- **The second level is emulator-**and-core** where states live**, so
  `saves/gbc/libretro.gambatte/` sits beside `saves/gbc/*.srm` and `saves/snes/bizhawk/`
  beside `saves/snes/libretro.snes9x/`. Battery saves and states share the parent.
- **Loose does not mean class A.** `xbox` holds `eeprom.bin` and a 39 MB `xbox_hdd.qcow2`
  loose at the system root and both are class D.
- **Class B and class D interleave at one level.** `megacd` holds per-game `.brm` and `.srm`
  beside the shared `4Mbit_cart.brm`, so excluding class D is a named-container list, never
  a positional rule.
- **The bundled data is short of the tree.** 21 systems that hold content on disk are still
  `_unclassified`, and `ports` is absent from `save_shapes.json` entirely.
- All four class-D options and `dolphin_sync_saves` are **unset** on the measured install, so
  stock is the case to build for and the conversion hazards are stage 2's to detect.

Two shapes observed that the table above did not predict: **saturn writes `.bcr` _and_
`.bkr` per game** (class B), and **megacd is B and D simultaneously**, with per-game `.brm`
plus `.srm` alongside a shared 512 KB `4Mbit_cart.brm`.

**Class B takes one slot per file, keyed `{emulator}:battery:{ext}`.** The observed sets are
two files with fixed extensions (saturn's `.bcr` at 512 KB with its `.bkr` at 32 KB, megacd's
`.brm` with its `.srm`), which is exactly the "small and stable" case. Bundling them would
pull the deterministic-archive machinery into stage 1, which is most of what stage 2 owns.
The cost is that a partial flush can land one file and not its sibling, so the two outbox
rows go up as one batch and a partial result is reported as one, rather than each file
looking independently fine.

**Amended after M6 stage 1: the batch is not built, and the cost above stands unmitigated.**
Saves never enter the outbox at all in stage 1. `SaveSync` reads `local_save` and posts
directly, so saturn's `.bcr` landing while its `.bkr` fails reports one up and one failed with
nothing tying them together, and `outbox.batch_key` has a schema and no writer.

**Amended after M6 stage 2b: class C does not give the column a second caller, and the batch
report is delivered without it.** The expectation above, repeated in migration 006's own header,
was that stage 2 would route class B and class C through the outbox and that class C would
supply the second row a batch needs to tie. It does not: a class C unit is one (container, key)
pair, so it bundles to **one archive, one slot and one upload**, GameCube's several `.gci` per
game code included. There is no second row.

So `batch_key`'s only genuine caller is class B's siblings, and class B is not in the outbox.
Rather than retrofit stage 1's proven upload path onto a queue it does not use, stage 2b delivers
the behaviour the column was a proxy for: `SaveSync` already holds every sibling of a slot in one
map, so a partial result is grouped by `(rom_id, base slot)` and reported as one batch. The column
stays unwritten and is kept, because a future queued-upload design would want it back and the
schema is already shipped. Until then a sibling that fails is simply retried by the
next flush, which is correct but says less than it should. **MAME is the friendly class C
case**: `saves/mame/nvram/<shortname>/` across 1231 directories, where the short name _is_
the rom basename, so attribution needs no Game ID lookup at all.

**RPCS3 was called the hostile one on a number that counts the wrong tree, and M6 measured
it.** `saves/ps3/rpcs3/` really is 32,451 files, and a logical-content hash over all of them
takes **426 s warm, 512 s cold, across 52.87 GB**. But that is `dev_hdd0` in its entirety,
installed games and firmware and caches included. The save data is
`dev_hdd0/home/<user>/savedata/`, which is **17 directories, 77 files, 16.3 MB, 0.06 s**.
MAME's whole `nvram` tree is 1,531 files and 8.0 s, and per game it is trivial.

So the design input is not "class C needs a hashing budget". It is **the shape definition
must scope the save unit, and hashing an emulator's data root is the bug**. A shape that
names `saves/ps3/rpcs3` is wrong in a way that costs seven minutes per sync; one that names
`saves/ps3/rpcs3/dev_hdd0/home/*/savedata/<TITLEID>/` is right and costs nothing.

**Amended after M6 stage 2b, which re-ran the measurement and confirmed it exactly** (32,451
files / 52,868.4 MB / **426.07 s** for the data root against 77 files / 16.3 MB / **0.06 s** for
the savedata subtree, and 1,531 files / 8.02 s for MAME's whole `nvram`), **and then found the
unit itself is not what this table says.**

**A class C save unit is not "a directory per game".** Measured on three systems at once:

```text
ps3   BLUS30109G6A383E91  BLUS30109G6A3B071C  BLUS30109S    three directories, one title id
      BCUS98111-AUTOSAVE  BCUS98111-USERDATA                two more, one title id
psp   UCES01011           ULES01513SYSDATA                  the key is a PREFIX of the segment
gc    69-GXBE-game1.ssx.gci   69-GXBE-settings.ssx.gci      two FILES, no directory exists
```

GameCube settles it: there is no directory that is the unit, so "the unit root is a directory"
cannot be the model. **A save unit is a (container path, key) pair**, and its members are the
entries under the container whose name carries the key. That covers all four cases with one
rule, and it is what `save_shapes.json` declares from stage 2b onward:

```text
mame       saves/mame/nvram                                   key = the directory name
psp        saves/psp/SAVEDATA                                 key = title-id prefix
ps3        saves/ps3/rpcs3/dev_hdd0/home/*/savedata           key = title-id prefix
gamecube   saves/gamecube/dolphin-emu/User/GC/<region>        key = game code, .gci.deleted excluded
wii        saves/wii/dolphin-emu/User/Wii/title/00010000      key = the ASCII game code in hex
```

**Wii's NAND is decided from data rather than left open.** `title/00010000/<hex>/` is the
disc-game tree and the hex is the ASCII game code, so `52534245` is `RSBE`, which joins exactly
to what route 2 reads at `0x58`. `title/00000001/*` is system titles, and `shared2/`, `sys/` and
`fst.bin` are system state; none is a save. A title holding `content/title.tmd` with no `data/`
is an installed stub rather than a save, so only `data/` travels. See
[retrobat-findings.md](retrobat-findings.md), measurements 140, 141, 142 and 146. Stage 1's
real workload, every loose file under every system folder, is **37 files, 43 MB, 0.51 s**,
and 38 MB of that is `xbox`'s class-D disk image which it must not read at all.

**Class D is a configuration problem, and RetroBat already has the switch.** A shared
memory card holds saves for twenty games, so it cannot be attributed to a `rom_id`. But
the emulators all support per-game virtual memory cards, and RetroBat exposes each as an
option that `emulatorlauncher` reads at launch:

M0 read the real option definitions out of `es_features.cfg`. All four exist, and the
choice lists are wider than the plan assumed:

| Emulator            | Option                    | Choices in `es_features.cfg`                                | Set it to       | Why                                                                       |
| ------------------- | ------------------------- | ----------------------------------------------------------- | --------------- | ------------------------------------------------------------------------- |
| DuckStation (PS1)   | `duckstation_memcardtype` | **`PerGameTitle`**, `Shared`, `PerGameFileTitle`, `PerGame` | **leave unset** | the stock `PerGameTitle` already binds a disc set; changing it breaks one |
| PCSX2 (PS2)         | `pcsx2_slot1_memory`      | `standard`, `folder`, **`game`**                            | **`game`**      | names the card after the rom basename                                     |
| Dolphin (GameCube)  | `dolphin_slotA`           | **`8`** (GCI folder), `1` (memory card)                     | **`8`**         | already the stock default                                                 |
| Flycast (Dreamcast) | `flycast_vmupergame`      | switch (`switchauto`, so unset by default)                  | **on**          | per-game VMU, **port 1 only**, and keyed by **disc serial** not filename  |

**Do not convert DuckStation. Read the stock `PerGameTitle` layout instead.** An earlier
revision of this plan preferred `PerGameFileTitle`, reasoning that a card named after the rom
file collapses class D straight into ordinary class-A attribution while a card named after
DuckStation's internal database title does not. A real card measured on a real install
reversed that, because the internal title is doing work the filename cannot.

A two-disc Metal Gear Solid set, launched once through its `.m3u` and played until the game
saved, produced **one card for the set**, under the stock configuration:

```text
[MemoryCards]                        saves/psx/duckstation/memcards/
Card1Type=PerGameTitle                 Metal Gear Solid (USA)_1.mcd    131072 B
Card2Type=PerGameTitle                 Metal Gear Solid (USA)_2.mcd    131072 B, empty
UsePlaylistTitle=true
```

`_1` and `_2` are the two console **slots**, not the two discs. The card stem is the shipped
`resources/gamedb.yaml` `saveName` with the disc marker removed, which is a third string
distinct from both of the obvious candidates:

```text
gamedb name      Metal Gear Solid (Disc 1)          minus the disc marker -> Metal Gear Solid
gamedb saveName  Metal Gear Solid (USA) (Disc 1)    minus the disc marker -> Metal Gear Solid (USA)   <- the card
rom / m3u stem   Metal Gear Solid (USA) (Rev 1)
```

So **regions stay separate and discs collapse together**, and revisions share a card because
they share a serial. That is the behaviour RomMBat wants. `PerGameFileTitle` would name the
card from the filename and split a set whose discs are separate files, which is the layout a
RomM sync produces. The conversion that looked like an improvement is the regression.

The cost of leaving it alone is that PS1 cards need **database-backed attribution** rather than
filename attribution: the card stem is a `saveName` prefix, not a rom name. RetroBat softens
this by writing the serial into the save tree unprompted, as a `.txt` beside the save state
holding exactly `SLUS-00594`, which is the join key that lookup would otherwise have to
reconstruct.

**The loose layout behaves the same way, which is the case that matters most**, because it is
what a RomM sync produces. Final Fantasy VII, three discs loose in `roms/psx` with no playlist
at all, launched as disc 1 alone, produced one card resolving to all three serials:

```text
duckstation/memcards/Final Fantasy VII (USA)_1.mcd
   matches a rom or playlist filename : False
   matches gamedb saveName with the disc marker removed: SCUS-94163, SCUS-94164, SCUS-94165
```

So the playlist is not what binds a set under DuckStation; the database lookup is, and it works
from any single disc. The `.m3u` still matters for libretro, which keys its `.srm` on the
playlist filename, and for emulators that need it to change discs at all.

**Two relationships exist at once, and M6 has to model both.** The same session left
`duckstation/Final Fantasy VII (USA) (Disc 1)_01.sav`, a save state named from the rom file and
therefore **per disc**, beside a memory card named from the database and therefore **per set**.
A `rom_id` that owns one card can own three states, so neither "one save per game" nor "one
save per file" is a safe assumption.

What stays unmeasured is narrower: the observed rule cannot distinguish "strip the disc marker
from `saveName`" from "look up the disc set", because both driven sets read
`Title (Region) (Disc N)` with nothing behind the marker. **130 of the database's 698 disc-set
stems keep a subtitle behind it** (`Biohazard 2 (Japan) (Disc 1) (Leon-hen)`), where the two
readings disagree. Do not assume one card per set, nor one set per card: the mapping from a PS1
card to a `rom_id` is many-to-many.

**PS2 has the same failure and no equivalent escape.** PCSX2 cannot bind discs at all, so
there is no title-keyed mode to fall back on: `pcsx2_slot1_memory=game` keys on the rom
basename, and a multi-disc PS2 game loses its save at the disc change where the stock shared
`Mcd001.ps2` would have carried it. **So the class-D conversion is a per-game decision, not a
per-system one**, which is exactly what the `<system>["<rom filename>"]` override form is for:
convert single-disc titles and leave multi-disc sets shared, reporting why. See
[freegosy-findings.md](freegosy-findings.md), F18.

**Watch out for `dolphin_sync_saves`**, also in `es_features.cfg`: "RetroBat will sync
dolphin and libretro-dolphin saves folders." **The description is wrong and so was this
paragraph** until finding 189 read `Dolphin.Generator.cs` and drove it. It is **GameCube only**,
it runs **once per launch inside emulatorlauncher, before Dolphin starts**, and the two
locations are `saves/gamecube/dolphin-emu/User/GC/<REGION>/` and a **`Card A/` subdirectory of
it**. Nothing moves while RomMBat is running.

`DolphinSaveSync` detects it and reports it, and never acts on it. The hazard is not the mtime
comparison, which a restored save always wins because it carries the current time: it is the
one-sided branch. **A `.gci` in `Card A` with nothing beside it is copied back out**, so a save
RomMBat removed reappears holding whatever that copy held, reported by RetroBat as one INFO
line. `Card A` is invisible to class C discovery, which is correct and is also why RomMBat
cannot see it coming, so the user is told instead.

So PS1 and GameCube are **already per-game in a stock RetroBat**, and the class-D list is
much shorter than it first appears. PS2 needs one option flipped. The earlier framing of
"detect and steer the user" understated what is achievable: RomMBat can make these
platforms syncable itself.

**M0 qualified both of those claims.**

- **PS1 is only per-game when DuckStation is the selected emulator.** The install measured
  runs libretro for `psx` and writes plain `saves/psx/*.srm`, which is class A and fine, but
  it is a different code path from the DuckStation memcard one. The emulator choice per
  system decides which shape applies, so shape is a property of `(system, emulator)`, not of
  the system alone.
- **Flycast's per-game VMU is keyed by the disc serial, not the rom file.** M0 drove it:
  with the option on, `emu.cfg` flips to `PerGameVmu = yes` and Bangai-O produces
  `saves/dreamcast/flycast/vmu/T40217N_vmu_save_A1.bin` while the shared `vmu_save_A1.bin`
  goes untouched. `T40217N` is the disc's product number; `Bangai-O (USA).chd` appears
  nowhere in the path. **So Dreamcast does not collapse into class A the way DuckStation
  does** - the key cannot be built from `fs_name`, and attribution needs either the serial
  read out of the image or the launch window from `emulatorLauncher.log`. Per-game and
  shared VMUs also share one directory, so both shapes are present at once.
- **GameCube's GCI folder is per-game but messier than 1:1.** Observed:
  `saves/gamecube/dolphin-emu/User/GC/USA/01-GALE-SuperSmashBros0110290334.gci`. There is a
  **region subdirectory** in the path, **one game can produce several `.gci` files**
  (`69-GXBE-game1.ssx.gci` and `69-GXBE-settings.ssx.gci`), and Dolphin **soft-deletes with
  a `.gci.deleted` suffix** that must be excluded or it syncs as a live save. Naming is
  `<makercode>-<gamecode>-<internal name>.gci`, keyed by game code, so the attribution route
  below is still required.

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

**M0 measured this rather than assuming it** (`tools/m0-probes/probe2-per-game-override.ps1`).
The per-game key is honoured by `emulatorlauncher`, outranks the system-scoped key, and
affects only its own ROM. **The key must include the ROM's extension**, exactly as written
above: the same option under a bare stem is ignored, and it is ignored silently, with the
emulator launching normally and continuing to write to the shared container. Build the key
from RomM's `fs_name`.

Five things to keep honest about this:

- **It mutates the user's RetroBat configuration**, so it is opt-in, explained, and
  reversible. Never flip it silently.
- **ES owns `es_settings.cfg`, and a key written while it is running does not survive.**
  **Amended after M6 stage 2c, which refutes what this bullet used to say.** M0 measured that
  ES preserves keys it does not recognise and concluded the override was durable and the hazard
  was ordinary two-writer contention. Every write M0 made happened **before** ES started. Driven
  the other way on a real install, with ES up, two custom keys were merged in atomically,
  confirmed on disk, and **discarded by ES's next write**. `Language` is the proof it is not a
  merge: ES added that key itself at startup and dropped it again on the same write, so what it
  serialises is a model loaded at boot rather than the file as it stands.

  **ES loads the file at startup and serialises that model on every write.** A key present at
  load survives, ones ES cannot understand included; a key that appears afterwards is discarded.
  M0's nonsense key survived because it predated the load. So "write while ES is idle" is not
  prudence, it is the only thing that works, and merging and atomicity do not help, because that
  write was both. **ES writes twice a session, at launch as well as on exit**, timed
  against the hook spool, so the safe window is strictly "while ES is not running". The writer therefore refuses
  to run while ES is up, says why, and re-reads the file afterwards to confirm the key is
  really there. See [retrobat-findings.md](retrobat-findings.md), 178 and 179.

- **ES prunes any setting whose value equals its own default.** An entry written at the
  stock value disappears on the next rewrite (`Language=en_US` vanished, `fr_FR` survived).
  Custom keys have no default to match and are kept. Never read a missing entry as evidence
  the user reverted something.
- **Switching modes strands existing saves** inside the old shared container, where the
  game will no longer look for them. Either migrate (parse the container and extract the
  per-game saves) or refuse to switch until the user has been warned clearly. Migration is
  real format work and should be scoped explicitly, not assumed.
- **Some games legitimately read another game's save** from the same card (sequel bonus
  detection, the Suikoden and Metal Gear cases). Per-game cards break that by design. Say
  so where the option is offered.

Dolphin's `.gci` files are named by game code rather than ROM filename, so those still need
the attribution route below.

**Dreamcast VMU is no longer unverified, and it does convert.** By default Flycast writes
four files keyed by **controller port**, not by game:

```text
saves/dreamcast/flycast/vmu/vmu_save_A1.bin   (also B1, C1, D1)
```

Every game shares them and nothing in the path identifies a game. But `es_features.cfg`
declares **`flycast_vmupergame`**: "PER GAME VMU. When enabled, each game will have its own
VMU **in port 1**." So Dreamcast joins the convertible set rather than the unsyncable one,
with the caveat that **only port 1 becomes per-game**; ports B, C and D stay shared and
remain unattributable.

M0 then drove the option, and the conversion is real but lands one bucket lower than hoped.
The new file is **`T40217N_vmu_save_A1.bin`**, named for the disc's product number rather
than for `Bangai-O (USA).chd`, and it appears in the same directory as the shared files
while those go untouched. **So a converted Dreamcast VMU is Game-ID-keyed, exactly like the
class-C cases**, and it needs the attribution routes immediately below rather than a filename
match. PS1 lands in the same place once DuckStation is left at its stock memory card mode, so
these routes carry more of the library than an earlier reading of this plan assumed. Detecting
the conversion is easy at least: the shared file stops being written and a serial-prefixed
sibling appears.

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

   **Amended after M6 stage 2b: this route reaches GameCube and Wii, and nothing else.** Every
   image in five systems was head-read on a real install: `gamecube` 178 `.rvz` at **100%**
   readable, `wii` 40 `.rvz` and 13 `.wad` at **75.5%**, and `psp` (147 `.cso`, 7 `.chd`), `ps3`
   (23 `.dec.iso`) and `psx` (386 `.chd`) at **0%**. No constant offset reaches a `.cso`, a
   `.chd` or an ISO9660 image, so **the system this milestone's "done when" names is exactly the
   one this route cannot serve**, and so is every disc system stage 2c needs. Route 1 and the
   sidecar route below carry those. See [retrobat-findings.md](retrobat-findings.md), 143.

   **`PARAM.SFO` is not the answer it looks like either.** Parsed on two real PSP save
   directories, its keys are `SAVEDATA_DIRECTORY`, which is the directory's own name and
   therefore says nothing new, and `TITLE`, a human string (`'echochrome'`,
   `'The 3rd Birthday'`). It buys a fuzzy title match and never an exact key. Measurement 144.

   **Measured, and the fallback is narrower than it sounds.** On a real library, GameCube is
   **1,792 of 1,793 `.rvz`** and Wii is **148 `.rvz`, 33 `.wad`, zero `.iso` across both**. So
   the raw disc header at offset `0x00` is correct in principle and never exercised: a reader
   that handles only `.iso` resolves nothing and would take the literal bytes `RVZ.` for a
   game code. In an `.rvz` the code sits at **`0x58`**, inside the copy of the disc's opening
   bytes that the container embeds for identification, confirmed as `GW7P` and `RUUE` on two
   real images; the format version follows the `RVZ\x01` magic and must be checked, since a
   later revision moving that field moves the offset. **A `.wad` has no disc header at all**
   and its title ID lives inside the ticket at an offset that depends on the preceding
   certificate-chain length, so **17.5% of that Wii library cannot be read by any constant
   offset**. Reading 256 bytes over a bounded `Range` is enough for all of this and no image
   need be downloaded. See [freegosy-findings.md](freegosy-findings.md), F17.

3. **Read the ID out of the save-state name-mapping sidecar, added in M6 stage 2b.** RetroBat
   writes a `.txt` beside a save state holding the emulator's native basename, and where that
   basename is identifier-keyed the sidecar is a Game ID already joined to a ROM filename, which
   `RomIndex` resolves. Measured: `ppsspp/3rd Birthday, The (Europe).txt` holds `ULES01513_1.00`,
   whose `ULES01513` prefix matches `SAVEDATA/ULES01513SYSDATA`. It reads no ROM and needs no
   observed launch, so it reaches saves that predate RomMBat on any game that has a state, and
   reaches nothing on a game that has none. Stage 2a already collects the sidecar's contents onto
   `local_state`, so the data is on disk before this route exists. Measurement 145.

   Its absence and its presence both mean nothing on their own: `libretro` writes no sidecar at
   all and `bizhawk` writes its own truncated name plus the core, so only the **contents** are
   ever worth anything, and only where they parse as an identifier.

**Hash the contents, not the archive.** For bundled class C saves, defining
`content_hash` as the MD5 of the zip bytes is a trap: archive output is
implementation-dependent (entry ordering, timestamps, compression level differ between
Go's `archive/zip` and .NET's `ZipArchive`), so RomMBat and Grout would compute different
hashes for an identical logical save and conflict forever, and a library upgrade could do
the same to RomMBat alone. Define the hash over the logical contents instead: sorted
relative paths plus each file's own hash, folded into one digest. Keep the archive purely
as transport.

**Amended after M6 stage 2b: the server does the same thing, by a different function, and
that means class C carries two hashes rather than one.** Measured live. RomM's `content_hash`
for a plain file is exactly the MD5 of the bytes uploaded, confirmed on four payloads. For an
**archive** it is not: the same single member rebuilt at a different compression level and
timestamp gives a different zip and **the same** digest, while renaming that member changes it.
So RomM also hashes member names and member contents and ignores archive framing, which is this
rule reached independently from the server side. Eight candidate reconstructions of the exact
function reproduce none of the observed values, so **it is not reproducible client-side and must
not be guessed at**.

The consequence is a hard one and it decides the code. Negotiate compares the client's
`content_hash` against the server's own, so for class C:

- **the logical fold is the local change detector**, answering "has this unit changed since the
  last upload", and it never goes on the wire;
- **the digest the server returned on the last successful upload is what goes on the wire**, and
  sending anything else answers `download (Server save is newer)` forever.

Driven: sending the server's returned digest answers `no_op (Content is identical)`; sending
the logical fold or the archive's MD5 answers `download`. **The two hashes are different
functions and comparing one against the other is always false**, which also means stage 1's
verification of downloaded bytes against `server_content_hash` is sound for class A and B and
would fail every class C restore. See [retrobat-findings.md](retrobat-findings.md),
measurements 148 and 149.

**Restores must be atomic.** A half-written directory save is a corrupt directory save.
Extract to a temporary directory beside the target, verify, then swap, and keep the
previous copy aside until the next successful sync.

**Met for class C as all-or-nothing rather than as one filesystem operation.** Everything that
can be done off to one side is: the archive is extracted, CRC-checked per entry and hashed, and
the previous members are copied under `replaced/`, all before the live tree is touched. The swap
that follows is per member, because a temp-dir-and-swap needs a directory of the unit's own and
a class C container is **shared**, with `saves/psp/SAVEDATA` holding every PSP game on the
install and a GameCube region folder holding every GameCube game. Only the unit's own members
may move.

A failure partway through that swap used to leave a mixed unit, which an emulator may read as
corrupt, with recovery manual. It is now undone: the members the pass placed are deleted and the
ones it removed are copied back from `replaced/`, so the unit is wholly new or wholly as it was
and the next flush tries again. The one case that still leaves a mixed unit is a rollback that
cannot finish, and it says so by name and names the `replaced/` copy rather than reporting a
generic write failure.

- Play sessions: `{rom_id, save_slot, start_time, end_time, duration_ms}`, at most 100 per
  call, `end_time` strictly after `start_time`, microseconds truncated server-side for
  dedup. A long offline binge flushes in chunks; replaying a failed chunk is safe.

  **All three of those are now measured rather than read off the backend, and the payload
  shape is an envelope.** `POST /api/play-sessions` takes
  `{device_id, sessions: [...]}` with `device_id` **outside** the entries; a bare array of
  entries carrying their own `device_id`, which Freegosy sends, answers 422. The response is a
  per-index result array, `{"results": [{"index", "status", "id"}], "created_count",
"skipped_count"}`, and a replayed session comes back `"status": "duplicate"` with
  `skipped_count` incremented, so **a partially-failed flush is reconciled exactly rather than
  inferred**. 101 entries answer 400 `Batch size exceeds maximum of 100`, and a reversed
  interval answers 422 `end_time must be after start_time`. `rom_id` is genuinely optional,
  which is not a licence to send a session without one: an orphan `game-end` is still
  discarded. See [freegosy-findings.md](freegosy-findings.md), F4.

  **This endpoint stands alone, so playtime need not wait on a save negotiation.** It works
  with no sync session open, which matters for the agent that wakes on `game-end` with nothing
  to negotiate: routing playtime only through `POST /api/sync/sessions/{id}/complete` couples
  two things that the server does not couple. Use `/complete` when a session is already open
  and the standalone route otherwise.

- Optionally `PUT /api/roms/{id}/props?update_last_played=true`. Note there is no
  `is_favorite` and no `playtime` field on rom props; favourites are collection
  membership, and playtime lives entirely in play sessions.
- Install the hook executables idempotently, adding a file beside any existing scripts
  rather than replacing them, and uninstall cleanly. They are `.exe`, not `.bat`: M0
  measured both scripted forms failing to start on ordinary rom names.

**Done when:** with the server unplugged, play three games, exit, plug back in, and all
three saves plus all three play sessions land in RomM in one flush; then play the same
game elsewhere and the newer save comes back down as a conflict the user resolves. Prove
it on one game from **each** save shape, not three class-A games: a RetroArch `.srm`, a
PPSSPP `SAVEDATA/` directory, a PCSX2 save state with its screenshot, and a PS2 battery
save after opting that game into a per-game memory card. Anything still genuinely shared
must report itself unsyncable with an explanation rather than appearing to work.

**Answered after M6 stage 2c, which is the stage that makes it claimable.** Each shape is
listed with what proved it, and **a shape proven by a test rather than by an emulator is named
as such**, because the two are different claims.

| Shape                          | Proved by                                                                                               | Stage |
| ------------------------------ | ------------------------------------------------------------------------------------------------------- | ----- |
| A, one file per game           | A RetroArch `.srm`, written offline and flushed on reconnect                                            | 1     |
| B, several files per game      | Saturn's `.bcr` and `.bkr` in their own slots. **Tests only: no Saturn game has ever been launched**    | 1     |
| C, a directory per game        | A PPSSPP `SAVEDATA/`, up as one archive, back as a conflict, resolved, and loaded by the game           | 2b    |
| D, a container shared by games | A PCSX2 card converted per game, written by Armored Core 3, synced, and **loaded back by the emulator** | 2c    |

The offline half is stage 1's and unchanged. The conflict a user resolves is 2a's, and its
server side was synthetic. The PCSX2 save state with its screenshot is 2a's, and the screenshot
uploaded but did not link to the state (finding 138).

**What the milestone does not claim.** Nothing is certified: certification is per
`(system, emulator, core)` across nine steps, and the rollout starts after M7. Class D was
driven on `(ps2, pcsx2)` only. **A converted card has never been downloaded onto a second real
device**, so the fresh-device half of class D rests on tests. Dreamcast and PS1 convert in
principle and are deliberately refused, each with its measured reason: Dreamcast's per-game VMU
is serial-keyed and needs the Game-ID routes, and DuckStation's stock mode already binds a disc
set through its own database, so converting PS1 is the change that would break one.

### M7: closing the loop, then the gamepad UI

**Three stages, and the first ships no interface at all.** That is the point of cutting it
out: everything in 7a is provable from EmulationStation with the agent alone, and none of it
has to wait on the framework choice above.

#### 7a: close the loop (done)

The question that opened M7's planning was whether EmulationStation is what a user lives in
after a sync, or whether that was only ever an aspiration. It is what has been built, and
until this stage the loop did not close: the hooks wrote a spool file and exited, and nothing
drained it except `sync` or a person typing `flush`.

- **The `start` and `quit` hooks spawn `background <event>`**, a detached agent pass, which
  is what makes an install that nobody administers from a terminal work at all.
  `game-start` and `game-end` spawn nothing, which is core principle 1's rule narrowed to
  the path it was always about rather than to all four events.
- **The ES menu entry**, which had been prose since M0 and is two files: a
  `system/es_menu/*.menu` naming `\rommbat\RomMBat.exe`, and a `<game>` element merged into
  `system/es_menu/gamelist.xml`. `sync` installs it beside the hooks and `menu uninstall`
  removes it. Measured live: a `.menu` written under a running ES appears after
  `/reloadgames` in 209 ms and its gamelist entry 262 ms later, so no restart is needed.
- **Queued configuration changes**, migration `012`. The UI is launched from the ES menu, so
  it always runs under a live ES and can therefore never write `es_settings.cfg`.
  `saves convert --at-quit` records the intent, `background quit` applies it once the ES
  process is confirmed gone, and the result survives being applied so the UI can say what
  happened while it was not running.

**Not in 7a, and worth a decision rather than a silent omission: `POST /api/activity/heartbeat`.**
It is declared at the 5.1.0 baseline this was written against, it works at 5.2.0, which is
now the baseline, and RomMBat had never mentioned it.
Posting `{rom_id, device_id}` registers the device as playing that game right now and
`GET /api/activity` lists it, so RomMBat is currently invisible in a presence feed the web UI
and every other client can see. `device_type` already reads `RomMBat`, carried from the device
pairing created. **The `DELETE` takes `device_id` as a query parameter**, not in the body the
`POST` takes; sent as a body it answers 422 naming the missing query field.

The reason it is not simply a gap is that presence has to be reported _while a game is
running_, and `game-start` is inside the launch path and may not touch the network. The only
part of this design awake during play and allowed to use the network is the detached
`background <event>` pass, so a repeating heartbeat is a milestone decision about what that
pass does, not a correction to 7a. See [argosy-findings.md](argosy-findings.md), A5.

#### 7b: the gamepad UI

**Cut into three, for the reason M6 was cut into 2a, 2b and 2c.** Only the first has landed.

##### 7b-1: the shell and the way in (done)

The one stage bounded by something other than taste: everything that has to exist before any
screen can, plus the two screens needing no new Core API at all.

- **A real full-screen Avalonia app**, `WinExe` rather than `Exe` so no console flashes behind
  it over a live EmulationStation. Published untrimmed at 101.1 MB, first frame 1041 ms; see
  the `RomMBat.UI` paragraph under "Projects" for what trimming would buy and why it is not on.
- **Input is read, never detected.** `es_input.cfg` records which physical input is `a` on that
  pad rather than what kind of pad it is, so there is no controller layout to detect and no
  vendor-id table anywhere in RomMBat. The leads mined from Argosy proposed exactly such a
  table and the first real device refutes it: vendor `0x2dc8` is 8BitDo, which that list calls
  a Nintendo layout, and the 8BitDo maps byte-identically to an Xbox pad. Findings 218 to 225.
- **Pairing from the couch**, with the address typed on an on-screen keyboard, the QR on screen
  and the code hyphenated to read aloud, and the requested scopes shown before approval rather
  than after.
- **Status**, read-only: what this device is, what it is paired to, what is in the outbox, what
  is in conflict, and what configuration is queued. That last one is the first reader migration
  `012` has ever had outside the agent.
- **Offline is a working state**, measured through the interface rather than inherited: an
  unreachable server settles in 2046, 2002 and 2004 ms and the screen stays navigable
  throughout. Finding 224.

**Two boundaries are asserted structurally against the built assembly**, so a helper in another
namespace or a call through an interface is caught where a grep would not: the UI never
references `EsSettingsFile`, and it never references `TreeLock`. The second is the less obvious
one, and it is a data-loss guard rather than tidiness: a flush treats a failed acquire as
success, so a UI taking the lock merely to report whether a pass was running would make a
concurrent `background quit` flush skip its upload and call it success.

**Not in 7b-1, and not implied by it: a live 401.** Nothing in this stage makes an
authenticated call after pairing completes, so there is no path on which a token can be
rejected mid-session. What is reachable, and is shown, is an expired token with a route back to
pairing. The drop-to-pairing-on-rejection rule from M1 belongs with the first screen that syncs.

##### 7b-2: sets, the sync run, and browse

**Cut into three, and the first has landed.** 7b-1's ledger owed a verdict on the split, since
7b-2 as first written is M2, M3 and `EvictionPlanner` given a face at once.

###### 7b-2a: the seam, and sets (done)

**Mostly not a UI stage.** Everything 7b-2 had to put on screen existed only inside the
agent's subcommands, welded to `Console`, so the interface could only have had a second copy
of each rule. The orchestration moved into `RomMBat.Core/Sets/` as console-free services that
return values with the words already chosen, report through `IProgress<T>` and take a
cancellation token: `SyncSetService`, `SetResolveService`, `LibrarySyncService`,
`EvictionService` and `RoamingConfigService`. The subcommands are printers over them.

**The evidence the refactor is correct is that nothing changed.** Twenty-four agent
invocations covering every refusal path produce byte-identical stdout, stderr and exit codes
against the commit before it.

**Where a sentence lives is a rule.** A sentence stating a rule or a fact about the library is
Core's, because it reads the same on either front end; a sentence naming a subcommand or a
flag is the caller's, because it would be false on the other one. A test sweeps every string
Core returns for the second kind.

On screen: the sets list, one set's detail with its exclusions, an editor that both creates and
edits, the scope, platform, collection and folder pickers, a resolve with progress, and the disk
budget and free-space floor.

**Per-set caps are not on the interface, which is a change to M2's shape and came from a
hands-on pass.** A set made from a collection or a platform is usually a mirror of something the
user already chose, and capping it to N leaves RomMBat guessing which N; no ordering makes that
guess good. The bound a person sets is the install-wide disk budget, which already existed and
which `ContentPlanner` and `EvictionPlanner` already enforce. `sets add` keeps `--max-games`,
`--max-bytes` and `--order`, and a set given caps from the console keeps them: the editor sends
no cap values at all rather than the cleared ones a hidden row would have produced.

**A set is named after what it mirrors.** A platform and a collection both already have a name
in RomM, so a platform or collection set is pick, pick, create, and the on-screen keyboard is
off the common path entirely.

**A filter scope is a saved search rather than a name match, and it is RomM's whole search.**
Eleven multi-selects, each with the `any` / `all` / `none` operator RomM's own `*_logic`
parameters take, and ten yes-or-no properties. The values come from the live library through
`with_filter_values`, which is the single job that sidecar exists for and which M2 wrote
`GetFilterValuesAsync` to serve.

**It shipped as five of the eleven and two of the ten, and the reasoning for that was wrong.**
The subset was chosen as "the ones `CatalogFilter` can persist, roam through
`Device.sync_config` and replay against a server that has never seen this device". They all
persist: it is one JSON column and one dictionary, so the constraint being satisfied was a
constraint on nothing, and what a person actually met was a filter screen offering a third of
what the web interface does, with no way to tell which third. A subset needs a reason a user
can state, and this one had none.

**Two of the eleven are not in the sidecar and are not derived from the library.** Statuses are
a vocabulary the user assigns, taken from the pinned schema's `RomUserStatus`. Metadata
providers have no enumeration in the schema at all, and the server **silently ignores** a value
it does not recognise, so a wrong entry would hand somebody the whole library while looking
like a filter: they were probed one at a time against a live instance (finding 236). Deriving
them from the rom row's `*_id` fields would have been wrong, which the probe is how we know.

**Four properties answer from RomM's records rather than from the game**, so a set carrying one
resolves differently on another account or after a scan. That is said on the row, once it is
set, rather than left in a document. A set is re-resolved on demand and is expected to move,
which is why this is a caveat and not a reason to withhold them.

**A filter can be changed after the set exists, which narrows "scope is not updatable".** That
rule stands for a scope's kind and its target: a set pointed at a different platform is a
different set. A filter's scope value is a query rather than an identity, and the rule's own
reason, that answering the new question means a re-resolve, was written when a resolve was a
terminal command and now costs one press. Changing one clears the resolution stamp and lands
on the set resolving, exactly as creating one does. The membership is deliberately **not**
deleted: that would orphan whatever is on disk and hand it to the next eviction pass on the
strength of an edit.

**A scope that can be picked has to be completable.** Virtual collections are offered and
disabled, because that route needs a `type` parameter the pinned 5.2.0 schema declares as a
bare string with no enumeration, and inventing a list of likely values is the vendor-id table
the input work threw out. A test asserts the general rule: every scope offered as pickable has
something that can produce its value.

**A resolve is minutes-long work, and both ends of that are measured.** A platform scope of
9,196 roms took **8 minutes 15 seconds** against a live 5.2.0 instance before #88, and a
collection of 4,773 took about **2 minutes 30 seconds** after it. So the resolve screen shows a
count that moves, names the set it is on and which of how many, and can be stopped.

**Stopping keeps what it found, and that took two attempts to get right.** Recording the cursor
was never the hard part; `SetResolver` threw on cancellation instead of returning, so the
membership that segment had accumulated was never written and the next walk resumed at the
right page with an empty accumulator. Cancellation is an interruption now, exactly as this
type's own remarks had claimed since the seam landed. Progress is reported on the walk's offset
rather than the segment's own count, or a resumed bar restarts at nothing while the work is
real.

**Creating a set lands on it and starts resolving.** A set that has never resolved holds
nothing and can do nothing, so returning to the list left the person one press short of what
they had just described. Starting minutes of network work uninvited is only reasonable because
stopping costs one press and keeps what it found.

**A set defined from the interface roams, and the resolve is what mirrors it.** `sets add` and
`sets resolve` both push `Device.sync_config`; the editor pushed nothing, so the same action
persisted differently depending on which front end took it, and a second device paired against
the same server found none of the sets made from the couch. The push hangs off the resolve
screen rather than off the save, because creating and editing both land there and it is the one
place with somewhere to say the push failed. Best effort as everywhere else: its own connection,
never on the screen's cancellation token, and a failure is a note appended to the result rather
than an error. Roaming is the mechanism M2 gave set definitions, and the front end with no
prompt is the one that needs it most.

###### 7b-2b: the sync run (done)

Sync from the interface with progress, cancellation and eviction. The first thing in this
design to put minutes-long cancellable work inside a process a user can close, which 7b-1's
shell is shaped for: a screen owns its work and is disposed when left. `LibrarySyncService`
and `EvictionService` landed in 7b-2a and were unfaced until here.

**The invariant the stage is built around.** A sync leaves every game either wholly present,
with its gamelist entry and whatever artwork the server actually had for it, or wholly absent.
Whether it ran to the end, was stopped, or lost the server. `GameSync` is the type that owns
that sentence: it groups a `ContentPlan` into games by `DiscSet`, fetches each game's ROMs and
then its artwork, and takes back any game that did not land whole.

**A stopped sync writes its gamelists, and getting that wrong is what the first hands-on pass
found.** The pass was handed the run's cancellation token, so on a stop it threw before writing
anything and every finished game sat on the drive invisible to EmulationStation. It runs on
`CancellationToken.None`, which is bounded: two local file writes and one reload with a 400 ms
connect timeout. The same defect existed a second time, on the path where a stop lands during a
game's artwork rather than during its ROMs.

**"With its artwork" cannot mean every configured kind, and the reason is not RomMBat's.**
Nothing guarantees a server holds it: the administrator may not have scraped that kind, and the
upstream source may never have had it for that game. `MediaSyncOutcome.Missing` counts exactly
that, no run can fix it, and a rule demanding every kind would declare most real libraries
permanently broken.

**The rollback fires on any incomplete game, not only on a stop.** A multi-disc title whose
second disc fails leaves half a game on disk with nobody pressing anything, and `ContentSync`'s
"a failure is per game, not per run" makes that the ordinary path. It is bounded to the ROMs:
once every ROM of a game has committed the game is playable and listed, and a stop during its
artwork leaves it present for the next run to finish. Three fences keep it to this run's own
writes, each with a test that fails when the fence is removed: only `FileOrigin.Synced` rows,
never a game that entered as `AlreadyPresent`, and the `local_file` row goes with the bytes.

**A stop is returned, not thrown**, which is 7b-2a's lesson about a cancelled resolve throwing
away what it found. The run carries on to write gamelists and report the budget, so a stopped
sync ends with a correct tree rather than with work postponed.

###### 7b-2b: what #102 turned out to be

**The issue's analysis was wrong twice over and is corrected here rather than closed quietly.**

**A sync cannot overshoot the budget by artwork.** `RomMConnection.Media` refuses a media file
whose `Content-Length` exceeds the room left, stops the read when the server declared no
length, and the caller discards the partial in both cases. What actually happened was quieter:
`ContentPlanner.Plan` filled the cap with ROMs, `MediaSync` then found `Room()` at or near zero,
and the games landed in EmulationStation with no covers, with no later run repairing it because
nothing frees space by itself.

**And interleaving does not recover that artwork. It concentrates the same bytes.** Measured on
the live instance, Atari 5200, 76 games, 757.5 KB of ROMs against a 1 MB budget:

|                               | complete games | partial | no artwork | budget            |
| ----------------------------- | -------------- | ------- | ---------- | ----------------- |
| before, media after every ROM | 0              | 8       | 68         | 1023.3 KB of 1 MB |
| after, media per game         | 0              | 8       | 68         | 1022.4 KB of 1 MB |

And with room to spare, Atari 2600, 53 games against 6 MB: 12 complete and 8 partial before, 12
complete and **3 partial** after. So the reproducible effect is that the games which get artwork
get all of it and half-decorated games roughly halve, not that more artwork arrives. The
interleave is kept because the whole-game rollback needs a per-game artwork unit, not because it
saves a byte.

**Media is not a rounding error on retro platforms, which is the one thing #102 understated.**
The plan said about 1.8 MB per game where a video is fetched, self-correcting rather than
cumulative. Measured on two Atari platforms with three kinds each and no video: 296.6 KB of
Atari 2600 ROMs pulled **28 MB** of artwork, and 757.5 KB of Atari 5200 ROMs pulled **46.8 MB**.
Artwork is 62 to 94 times the ROM bytes there, so on a small-ROM library the budget is spent
almost entirely on media.

**A reservation was added, for the ROMs and not for the media.** Interleaving broke the cap:
`MediaSync` bounds artwork by `cap - managed`, and `managed` is read when the call is made, so
one call per game sees the budget as it stands before most of the run's ROMs exist. A 1 MB
budget was measured finishing 703 KB over it. `GameSync` now passes the ROM bytes still ahead.
That reservation is possible precisely where a media one is not: a ROM's size is on the member
row and the plan already holds it, and RomM publishes no media size at all.

**Which media kinds are fetched follows RetroBat's own scraper settings.** `es_settings.cfg`
carries `ScrapeVideos` and `ScrapeManual`, and RomMBat was ignoring both: a hands-on pass turned
video off in RetroBat and RomMBat kept downloading it. They set the default now, with an
explicit `media.kinds` still winning. There is no upstream toggle for the cover, the thumbnail
or the marquee, so those three keep RomMBat's default and no keys are invented for them. Same
rule as the on-screen keyboard following `Language`: where RetroBat already has the setting,
RomMBat asks it.

**An absent scraper key is off, and getting that wrong cost two hands-on rounds.** RetroBat
seeds `es_settings.cfg` from `system/templates/` with both switches `true`, so a stock install
has them on. EmulationStation's own compiled defaults are the opposite, and it drops any key
equal to its default, so turning a switch off **deletes the key** and a literal `false` never
appears. Absent is therefore a deliberate no rather than an unknown. Reading it as RomMBat's own
default meant video was fetched whatever RetroBat said, and the round that followed found 389 MB
of video on one platform and 2.05 GB across the tree that no setting could reach. The trap worth
carrying forward is the layer, not the key: **RetroBat's templates override EmulationStation's
compiled defaults**, so upstream source is not evidence of what a RetroBat does. Finding 238.

**Turning a media kind off takes back what was already fetched.** It used to stop future
downloads and nothing else, so the artwork stayed for ever with nothing able to reclaim it:
eviction removes whole games under budget pressure and has no notion of a kind. Measured on the
live install, 1.09 GB of video on one platform and 566 MB on another. Only `FileOrigin.Synced`
goes, so a user's own scrape at the same name is untouched, which is the fence the sync rollback
already uses.

**A size that disagrees with the download is still refused, and the message now says why.** The
check was nearly weakened to accept a file whose hash matched on the theory that only the size
record had gone stale. Measured against the live instance, that case could not be produced: 120
`fbneo` ROMs and 40 `megadrive` ROMs all verified cleanly, so the benefit was unevidenced while
the cost was a weaker check on the one thing standing between a corrupt download and a game that
will not boot. **Refused as before**, with the message naming the likely cause, which is a
library record that has not been rescanned.

**What that argument did change is what gets hashed, and it is migration 013.** RomMBat computed
md5, sha1 and crc32 on every download and compared only md5, or sha1 where the server published
none. Measured across 1,616 rom rows from three platforms of a live library, not one carries a
sha1 without also carrying an md5, and crc32 was never compared anywhere: RomM hashes a file
once and sets every column or none. So `local_file` lost both, and hashing went from **339 MB/s
to 594 MB/s** on a 3.41 GB image with the file already cached, which are processor numbers.

**The development box is the wrong machine to have reasoned from**, which is the more useful
half of this. There a 34.5 MB/s download leaves verification an order of magnitude of headroom
and the cost is invisible. The target is a handheld off a cheap stick where the link can be
faster and the processor several times slower, and there verification is what decides how long a
sync takes.

**An advertised media path that answers 404 is forgotten rather than re-asked.** Measured on the
live library: 39 of 40 games on one platform advertised a video the server does not serve, so
every sync spent 39 requests and printed 39 problems, for ever. Forgetting the path turns it
into the ordinary `Missing` case and needs no new state, because a resolve rewrites `metadata`
from the server wholesale and puts it back the moment RomM starts serving it.

**A stopped transfer's partial is truncated before its handle closes.** The cancellation is
instant; closing a handle over a large part-written file waits for the drive's write cache, and
the file is deleted immediately afterwards. Measured on the live install: a stop 10.9 s into a
PS2-sized download took **20.1 s** in that close alone, and 0.2 s after the change.

**`/reloadgames` from the interface works, and the answer is not the one this plan assumed.**
Measured in 7b-2a (finding 233): a reload issued while RomMBat is the app in front of ES is
**deferred, not discarded**, and applies when RomMBat exits. ES does **not** rescan on resume
by itself, proven by a marker written with no reload at all, which never landed. So the sync
screen calls `/reloadgames` after writing gamelists exactly as the agent does, and the new
games appear the moment the user leaves RomMBat, which is when they would look for them. No
workaround is owed, nothing tells the user to restart the front end, and the call must not be
skipped on the theory that ES will notice by itself. Built that way: the sync screen runs the
same `GamelistSync` pass the agent does, through `LibrarySyncService`, including after a stop.

###### 7b-2c: browse

Online paged browse with search, offline browse of the local subset, per-game install and
evict.

**Per-game install needs a schema decision, taken in 7b-2a and not built there.** A hand-picked
set is a set: it has caps, an ordering and it evicts like any other, so the shape to leave room
for is a sixth `CatalogScopeKind` with its own migration, not an id list smuggled inside a
`Filter` scope and not an unmanaged download that `EvictionPlanner` has to be taught to ignore.
The second overloads one column with two meanings; the third means storing "this orphan is
deliberate", which is a set by another name.

##### 7b-3: conflicts and settings

Conflict resolution, acting on the queued-config surface 7b-1 only reads, platform mapping, and
whatever the two stages before it turn up.

**Re-checked after 7b-2b, and two things it was going to owe are already paid.** `FlushReport`
carries every open conflict as rows, so displaying them needs no new Core surface and only the
screen and the choice remain. And a refused token now ends a run as `SyncState.Rejected` with
pairing offered on the spot, so re-pairing is not a thing 7b-3 has to invent a route to.

**The mapping screen is reached after pairing, not discovered mid-resolve.** M2 already calls
for it as core UI; what 7b-2a's hands-on pass added is where it belongs in the flow. An
unmapped platform is currently found out by a resolve stopping partway through a collection
that happened to contain one of its games, and the only repair from the interface is a per-set
folder override, which is the wrong shape: the mapping is install-wide and `platform_map`
already holds it. So the status screen should say how many platforms are unmapped, and the
mapping screen should be reachable from there, before a sync is attempted rather than after one
fails.

- **Anything the UI wants to change in `es_settings.cfg` goes through the queue**, without
  exception. It cannot write that file itself and there is no arrangement under which it can.
- No primary flow may require a mouse.

**The on-screen keyboard is EmulationStation's, transcribed rather than resembled.** 7b-1
matched its bindings and invented its own grid; 7b-2a read upstream's source and took the grid
too, because the half that was still RomMBat's own was the half a RetroBat user had to relearn.
Findings 234 and 235 hold the detail. Two consequences shaped code rather than pixels: the
second face button finally earns a binding, since ES puts shift and reset on two of them, and
`InstallSession.EmulationStationLanguage` exists because the layout follows the language ES is
running in and the UI may not read `es_settings.cfg` itself.

**Deferred, and worth stating so it is not re-derived: RomMBat does not speak that language,
only types in it.** Reading `Language` to pick a keyboard is one setting and a three-way switch.
Localising the interface is a different thing entirely, and the cost is not the resource files:
Core returns records carrying **pre-written English sentences**, which is the decision 7b-1 made
deliberately so that a refusal reads identically on both front ends. Every one of those would
have to become a key plus arguments, across Core, the agent and the UI, and the agent's output
is what `sets`, `sync` and `evict` are tested on byte for byte. It also contradicts CLAUDE.md's
"English only outside of localisation files" as written, so the rule moves in the same change or
not at all. **A milestone, not a commit**, and nothing before M8 ships. Note also that only three
keyboards exist upstream, so a German or Japanese install already types on the US grid in ES
itself: matching the interface's language would outrun the keyboard's, not follow it.

#### 7c: the wave rollout gate

M7 is what makes the platform rollout bearable, because every certification pass needs a
person launching games and the gamepad UI is what they do it with. Nothing in the rollout
starts before 7b lands.

**Re-checked against the three-PR cut, and it does not move forward.** The gate is 7b, not
7b-2a. A certification pass needs a person to say what to sync, sync it, and launch a game;
7b-2a supplies only the first of those from the couch, since syncing was still a terminal
command until 7b-2b. The earliest the gate can open is when 7b-2b lands.

**With 7b-2b landed the gate is open for the first two of the three, and not yet the third.** A
person can now say what to sync and sync it from the couch, both measured against a live
instance. Launching a game has never needed RomMBat and does not now, so what 7c actually waits
on from here is nothing in this stage: 7b-2c adds browse and per-game install, which a
certification pass does not require. The honest statement is that the gate opens when a hands-on
pass has driven the sync screen with a controller, which is the one thing this branch could not
do for itself.

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

**Announcing RomMBat upstream is not a milestone step and is not automatic.** Listing it in
`rommapp/romm`'s README or posting it in the RomM Discord is a conversation to have with the
RomM maintainers, on their timing, and it is Spinnich's to open. Nothing in this plan should
be read as scheduling it.

---

## Platform rollout: certify one at a time

Once the framework exists (M1 through M6 working end to end on a single platform), stop
building horizontally and start certifying platforms one by one. Two reasons this beats a
big-bang approach: the most-used platforms get correct first, and each platform surfaces
its own edge cases in isolation instead of as a pile of intermixed bugs late on.

**The certification unit is `(system, emulator, core)`, not the system.** An earlier revision
of this plan said "per RetroBat system, not per aggregate", which was right about the aggregate
and wrong about the unit. Two emulators for one system do not behave alike, and the repository's
own measurements are the evidence:

- **The save shape is a property of `(system, emulator)`.** `psx` under libretro writes plain
  `saves/psx/*.srm` and is class A; `psx` under DuckStation writes a memory card named from an
  internal database title and needs Game-ID attribution. `save_shapes.json` carries a
  `DependsOnEmulator` flag for exactly this.
- **The state directory and filename are per emulator**, and `es_savestates.cfg` declares
  thirteen different ones. Two of the thirteen declare a directory the emulator does not write
  to, and which two is not derivable from the system.
- **libretro and bizhawk are core-scoped**, `{{system}}/libretro.{{core}}` and
  `{{system}}/bizhawk/sstates/{{core}}`, so the same game under two cores has independent state
  sets. That is what makes the third element of the triple necessary rather than tidy.
- **BIOS requirements move with the emulator too**, since `batocera-systems.json` keys firmware
  on the system while the emulator decides which of it is actually consulted.

So "snes is certified" is not a claim either. "`snes` under `libretro`/`snes9x` is certified" is,
and it says nothing about `snes` under `bizhawk`. **This multiplies the rollout table below by
roughly two to four**, and the honest reading is that the table names the systems to work
through, not the number of passes.

**Certification checklist**, one pass per `(system, emulator, core)`, recorded in
`docs/platforms/<system>.md` with a section per emulator:

1. Folder mapping resolves, and by which layer.
2. `<extension>` list captured; a known-unsupported file is correctly excluded.
3. Required BIOS from `batocera-systems.json` resolved against RomM by md5; gaps listed.
4. Save shape classified (A/B/C/D) **for this emulator** and battery save round-trips.
5. Save state round-trips including its screenshot, per this emulator's `es_savestates.cfg`
   entry, and the declared directory is confirmed to be where the emulator really writes.
6. Where class D applies, the per-game memory card option is verified.
7. A game launches from ES after sync, with art and metadata.
8. Play session recorded and reaches RomM.
9. Re-sync is a clean no-op.

Steps 1, 2, 3, 7, 8 and 9 are largely per system; **steps 4, 5 and 6 are the per-emulator
ones**, and they are also the three where being wrong destroys data rather than costing a
re-download.

**When this happens.** The full checklist needs a human at the machine for every pass, so the
wave rollout below **starts after M7**, when the gamepad UI makes the loop something other than
alt-tabbing to a terminal between launches, and finishes against an M8 package, which is what a
user would actually install.

**One thing does not wait, and should not.** Steps 4, 5 and 6 are the data-loss steps, and M6
ships them across three PRs. A save shape that has never had a real emulator write into it is an
unverified claim no matter how good the tests are, and a fault found at M8 is a fault in code
written milestones earlier. So each M6 stage owes **one** hands-on pass, not a certification: one
game, one emulator, one real save or state of the shape that stage added, driven through ES and
back. That is minutes rather than a wave, and it is the difference between "the tests pass" and
"an emulator wrote this and RomMBat handled it".

| M6 stage | The one shape to exercise by hand                                     |
| -------- | --------------------------------------------------------------------- |
| 2a       | A save state with its screenshot, ideally PCSX2 and one libretro core |
| 2b       | A PPSSPP `SAVEDATA/` directory, and MAME `nvram/` if convenient       |
| 2c       | A PS2 battery save after opting that game into a per-game memory card |

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

| Risk                                                                                                                            | Mitigation                                                                                                                                                                                                                                                                                               |
| ------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Device offline for days, then floods the server on reconnect                                                                    | Durable outbox, chunked idempotent flush, exponential backoff, honest local mtimes                                                                                                                                                                                                                       |
| Device clock is wrong, so offline saves lose every conflict                                                                     | Monotonic sequence alongside wall clock; compare against the server `Date` header on reconnect and offer re-stamp                                                                                                                                                                                        |
| 100k-game library overwhelms the host or the UI                                                                                 | Catalog is never mirrored; content is opt-in via sync sets with hard game/byte budgets and eviction                                                                                                                                                                                                      |
| `GET /api/collections` returns every membership of every collection                                                             | Never read `rom_ids` from collection payloads; page `GET /api/roms?collection_id=` instead                                                                                                                                                                                                               |
| Huge `gamelist.xml` makes EmulationStation unusable                                                                             | Withdrawn by the M0 measurement: 100,000 entries load in 2.07 s. Only locally present ROMs go in the gamelist, and a large folder is reported rather than capped (finding 111)                                                                                                                           |
| ES overwrites `gamelist.xml` on exit and loses synced metadata                                                                  | M0 experiment 3; write only when ES is idle, or via the `update-gamelists` / `quit` hooks                                                                                                                                                                                                                |
| Save corruption from a bad conflict resolution                                                                                  | Never auto-overwrite on 409; default to keeping both, copy aside before any overwrite                                                                                                                                                                                                                    |
| A save download dies mid-body and the server records the device as current, so the save never comes down again                  | Pass `optimistic=false` on `GET /api/saves/{id}/content` and send `POST /api/saves/{id}/downloaded` only after the bytes are written and verified. The default is `true` and records on request                                                                                                          |
| A restored save is written under the server's tagged filename and the emulator never finds it                                   | Write `file_name_no_tags` + `file_extension`; keep `file_name` only as the server-side identity                                                                                                                                                                                                          |
| A save slot grows one row per session until the user's RomM is unusable                                                         | `autocleanup=true` with an explicit `autocleanup_limit`; the defaults are off and 10, and `keep_both` conflicts compound it                                                                                                                                                                              |
| A misspelt `/api/roms` filter silently resolves the whole library instead of one platform                                       | Unknown parameters are ignored with a 200, so assert at resolve time that a scoped walk's `total` is below the library total                                                                                                                                                                             |
| Eviction deletes a game whose save has not synced yet                                                                           | Eviction is blocked on unflushed outbox entries for that ROM, with a dry run preview                                                                                                                                                                                                                     |
| ES hook slows or hangs game launch                                                                                              | Hooks are journal-only with a hard time budget from M0; all network work happens in the background agent                                                                                                                                                                                                 |
| Drive letter changes and every stored path breaks                                                                               | Persist only paths relative to the RetroBat root; resolve to absolute at point of use; M0 experiment 7 proves it                                                                                                                                                                                         |
| Portable install moved to a new PC registers as a second device, or collides with another client                                | Anchor identity on `client_device_identifier` via the pairing flow, which never records MAC/IP/hostname. Never call `POST /api/devices` with fingerprint fields, whose dedup matches on MAC alone                                                                                                        |
| DPAPI-encrypted token is undecryptable on the next machine                                                                      | Do not use DPAPI. Default portable installs to a scoped, expiring token, offer an optional passphrase, make re-pairing cheap                                                                                                                                                                             |
| Pairing-only auth strands a user who cannot reach the web UI                                                                    | Accepted trade. The pairing code is short-lived and re-issuing is one button; document that approving needs a browser somewhere on the network                                                                                                                                                           |
| Token expires mid-session and the outbox is lost                                                                                | 401 is an expected state: keep the database and outbox, return to the pairing screen, resume the flush after re-pair on the same `client_device_identifier`                                                                                                                                              |
| Approver grants fewer scopes than requested                                                                                     | Read the granted set from `/token` and degrade by feature (pull-only, BIOS off) with a visible explanation, never a late 403                                                                                                                                                                             |
| Typing the server URL on a gamepad is the one hostile step                                                                      | On-screen keyboard, remembered after first use; mDNS discovery or a pre-seeded config file as a follow-up                                                                                                                                                                                                |
| FAT32 target silently fails on a ROM larger than 4 GB                                                                           | Detect the filesystem up front; skip or refuse oversized ROMs with an explanation instead of a partial write                                                                                                                                                                                             |
| Coarse FAT/exFAT mtime granularity causes false or missed conflicts                                                             | Compare on `content_hash` first, use mtime only as an ordering tiebreak                                                                                                                                                                                                                                  |
| Long portable paths exceed MAX_PATH                                                                                             | Long-path-aware APIs and `\\?\` prefixes where needed                                                                                                                                                                                                                                                    |
| Emulator save paths differ per system and RetroBat version                                                                      | Data-driven `save_directories.json`, user-overridable, with a clear "unmapped system" state                                                                                                                                                                                                              |
| Directory-shaped saves (PSP, PS3, Cemu, Citra, Wii, MAME) do not fit RomM's one-file `Save`                                     | Bundle as a single archive per `grout/sync/zip_save.go`, stage and verify off to one side, copy the previous members aside, then move the unit's own members in. The container is shared, so it is a per-member swap rather than a whole-container one, rolled back from `replaced/` if it fails partway |
| Shared memory cards cannot be attributed to a `rom_id`                                                                          | Convert to per-game cards via the RetroBat option (`pcsx2_slot1_memory`, `dolphin_slotA`) written to `es_settings.cfg`; PS1 and GameCube are already per-game by default, and PS1 must be left alone rather than converted                                                                               |
| Converting a per-game memory card splits a multi-disc set and loses the save at the disc change                                 | Decide the conversion per game, not per system, using the `<system>["<rom>"]` form; never convert a set with several disc files, and leave DuckStation at stock `PerGameTitle`, which binds a set through its own database                                                                               |
| RetroArch writes an absolute image path into the save tree (`<playlist>.ldci`), which does not survive a drive-letter change    | Exclude it from the sync set, or rewrite `image_path` on restore; never round-trip it verbatim. Its `image_index` is worth keeping, so exclusion is a real cost, not a free win                                                                                                                          |
| An emulator-created empty memory card is the same size as a real one and uploads as if it were progress                         | Change detection cannot use size or existence; hash content, and treat a card whose byte histogram is that of a freshly formatted one as absent. How many cards appear is a property of the game, not the emulator: one title produced both slots, another only slot 1                                   |
| One PS1 game yields a card per disc set and a save state per disc, so a `rom_id` maps to saves many-to-many                     | Model the card and the state as separately keyed; never assume one save per game or one save per file, and never infer a set's membership from a card name alone                                                                                                                                         |
| Writing emulator INIs directly gets clobbered every launch                                                                      | `emulatorlauncher` regenerates them from options at launch; write `es_settings.cfg` instead, using its `<system>["<rom>"]` per-game form                                                                                                                                                                 |
| A key written into `es_settings.cfg` while ES is running is silently discarded                                                  | ES serialises a model loaded at startup, so merging and atomicity do not help. Refuse to write while ES is running, say why, and re-read the file afterwards to confirm the key is there                                                                                                                 |
| Switching a user to per-game cards strands their existing saves                                                                 | Opt-in and reversible, with either a migration path out of the old container or an explicit warning before the switch; note that per-game cards also break legitimate cross-game save reads                                                                                                              |
| Directory saves are keyed by Game ID and RomM stores no serial or title ID                                                      | Attribute by correlating with the `game-start` journal, cache the learned binding, fall back to reading `PARAM.SFO` / disc headers                                                                                                                                                                       |
| Hashing zip bytes makes RomMBat and Grout disagree on identical saves                                                           | Define `content_hash` over sorted relative paths plus per-file hashes; the archive is transport only                                                                                                                                                                                                     |
| A save state restored across an emulator update corrupts or crashes                                                             | Record emulator, core and version per state; never silently restore across a version change (RetroBat's own wiki warns about this)                                                                                                                                                                       |
| Platform nomenclature diverges: 37% of RetroBat systems unmapped, 19 shipped entries stale, 13 slugs fan out to several folders | Layered resolution (override → `fs_slug` → bundled table → normalized suggestion → unmapped), a first-class mapping UI, and `es_systems.cfg` read from the live install                                                                                                                                  |
| Two RomM platforms resolve to one folder and clobber each other                                                                 | Key gamelist generation and the local file index by resolved folder, not by platform; merge entries                                                                                                                                                                                                      |
| Arcade fans out to ten folders with romset-specific naming                                                                      | No guessing: require an explicit folder choice per arcade sync set in v1                                                                                                                                                                                                                                 |
| Bundled mapping table goes stale as both projects add systems                                                                   | Table is a seed, not an authority; user overrides persist in `Device.sync_config`; unmapped is a normal state, not an error                                                                                                                                                                              |
| RetroBat changes its folder layout between releases                                                                             | Pin a tested-versions table; detect the version and refuse to write when the layout is unrecognised                                                                                                                                                                                                      |
| Socket.IO looks tempting for live updates                                                                                       | Not usable: the socket authenticates from the `romm_session` cookie only, and `sync:*` events go to a `user:{id}` room nothing ever joins. Poll REST                                                                                                                                                     |
| Published RomM docs disagree with the server                                                                                    | Generate from `/openapi.json` at a pinned RomM version; gate features on `GET /api/heartbeat`                                                                                                                                                                                                            |
| Syncing a file the target emulator cannot launch: a game that appears in ES and dies                                            | Filter every candidate against the resolved system's `<extension>` list from the live `es_systems.cfg`, and show what was excluded and why                                                                                                                                                               |
| RomM's `is_verified` misses 93 of RetroBat's 156 required BIOS hashes                                                           | Join firmware on md5 against `batocera-systems.json`, ignore filenames and `is_verified`, and report required files RomM does not have                                                                                                                                                                   |
| Dev writes land in a production RomM with 85,000 games                                                                          | A dedicated non-admin account, its own scoped token and device on that instance; destructive tests only against a disposable RomM                                                                                                                                                                        |
| Users over-grant scopes at the pairing screen                                                                                   | Publish the scope-to-feature table and name what RomMBat never needs (`users.*`, `roms.write`, `tasks.run`, `logs.read`)                                                                                                                                                                                 |
| Client silently misbehaves against an untested RomM or RetroBat version                                                         | Declare minimum versions (RetroBat 8.2.1, RomM 5.2.0), track the newest stable, check both at startup, refuse below and warn above                                                                                                                                                                       |
| Building all platforms at once buries per-platform edge cases                                                                   | Certify one system at a time against the checklist, in the wave order above, `RetroArch` counted per core rather than as one thing                                                                                                                                                                       |

---

## Verification

- **Unit:** platform and save-directory mapping, gamelist merge (round-trip a real
  RetroBat `gamelist.xml` and assert user fields survive), slot derivation, hash matching,
  sync-set resolution and eviction ordering, outbox replay idempotency. Fixtures from a
  real install, checked in.
- **Mapping coverage, as a checked-in regression:** assert every bundled mapping resolves
  to a folder that exists in `systems_names.lst` (this catches the 18 stale entries today
  and will catch future drift), assert the multi-folder slugs resolve deterministically
  given a fixture `es_systems.cfg`, and assert that two platforms sharing a folder produce
  one merged gamelist rather than two competing writes. Track the unmapped count as a
  visible number so it cannot silently grow.
- **Gamelist merge, against a fixture taken from a real install:** round-trip an ES-written
  `gamelist.xml` and assert `playcount`, `lastplayed`, `gametime`, `scrap` with its
  attributes, `id` and `source` on `<game>`, `cheevosHash` and every other node RomMBat does
  not own survive untouched, while the fields it does own are updated. Assert an ampersand, a
  non-ASCII title and a control character in a description all come back out parseable, since
  a gamelist ES cannot parse loses the whole system rather than one entry.
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

  **"No churn" is a claim about the file ES leaves behind, not the one RomMBat wrote.** ES
  leaves a gamelist byte-identical when it has nothing to change, so the test is meaningful;
  but once a game has been played, ES reorders the entries, rewrites that entry's children
  into its own order and drops every comment, so the second write has to be a no-op against
  that file rather than against its own previous output. See findings 103 to 105.

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
3. `backend/utils/gamelist_exporter.py` writes `companies[0]` into `<developer>` and
   `companies[1]` into `<publisher>`. That array merges both roles and is alphabetically
   sorted on every row measured, so the two fields carry the alphabet: KOTOR exports with
   Activision as its developer and Aspyr Media as its publisher, and Chrono Trigger exports
   `Squaresoft` twice. The same file writes `regions[0]` and `languages[0]` verbatim, so
   `<region>` gets `USA` where EmulationStation's own vocabulary is `us`. Neither is fixable
   without a role on the company record, which is the real ask.

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
>    `AppContext.BaseDirectory` to a marker file, and have the ES hook executables resolve
>    relatively the way RetroBat's own `updatestores.bat` does, remembering that reaching
>    the root from a hook takes four levels, not three. Do not use DPAPI for
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
> currently RetroBat 8.2.1 and RomM 5.2.0, check both at startup, and refuse below minimum.
>
> Two authority rules that are easy to get backwards. **File extensions come from
> RetroBat, never from RomM**: read `<extension>` per system out of the live
> `es_systems.cfg` and filter sync candidates against it, or you will sync files that
> appear in EmulationStation and die on launch. **Firmware requirements come from
> RetroBat too**: `batocera-systems/Resources/batocera-systems.json` lists 353 BIOS entries
> across 99 systems as `{md5, file}` with the exact destination path. Join it against
> `GET /api/firmware` on **md5 only**, because filenames differ and RomM's `is_verified`
> is false on hashes RetroBat requires, `psxonpsp660.bin` among them. Fetch BIOS before
> that platform's ROMs, and report required files RomM does not have.
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
> `nes`/`snes`/`gb`/`gbc`/`gba`/`megadrive`/`mastersystem`. Certify per
> `(system, emulator, core)`, never per system alone and never per aggregate: two emulators
> for one console differ on save shape, state directory and BIOS needs, and `libretro` and
> `bizhawk` are core-scoped on top of that.
>
> Mine these for prior art rather than starting cold: `rommapp/playnite-plugin`
> (`Models/RomM/*` for C# DTOs, `Downloads/DownloadQueueController.cs` for the queue),
> `rommapp/grout` (`cfw/batocera/data/platforms.json` and `cfw/*/data/save_directories.json`
> for mapping file shapes, `cache/save_sync.go` for the sync state machine), and RomM's
> `examples/config.batocera-retrobat.yml` as a **seed** for the platform map.
>
> Do not treat that YAML as the answer. RetroBat ships 240 systems and RomM knows 457
> platform slugs, but the YAML holds only 167 pairs: 91 RetroBat systems (37%) are
> unmapped, normalization rescues just 16 of them, 18 entries point at folder names
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
