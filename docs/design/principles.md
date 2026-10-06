---
summary: What RomMBat is, the decisions that fix its shape, and the four core principles every change is built to.
read-when: Before starting any work, and whenever a change seems to pull against offline use, library scale, curation or portability.
---

# Principles

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

## Core principles

These four constraints cut across every feature. They are not a late-stage polish
pass; they decide the data model, so build to them from the start.

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

  **Both halves are measured.** A byte-identical save posted twice into the same slot reuses
  the same row and the slot count does not move (RB-161); a replayed play session comes back
  `"status": "duplicate"` in a per-index result array with `skipped_count` incremented, so the
  server names what it skipped rather than leaving the client to guess (RM-20). This is also
  the reason a bundled directory save's archive **must** be deterministic: an archive that
  varies between runs, as one holding a timestamp file does, defeats the dedup this principle
  rests on.

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

  **`with_rom_id_index=false` too, under every scope.** Under a scoping parameter the index
  spans the scope rather than the library and is resent on every page: 63 KiB a 250-row page on
  a 9,194-ROM platform and 114 KiB on a 16,687-ROM virtual collection, for no latency. #188, and
  RM-9.

  `with_total` stays on and is what keeps `total` non-null with the index off. With the index
  off it is a separate count, whose cost under a scope is inside the noise of the page (RM-9).

- **Never read `rom_ids` off a collection response.** `BaseCollectionSchema.rom_ids` is a
  full `set[int]` and it is present on the _list_ endpoint too, so `GET /api/collections`
  on a large instance returns every membership of every collection in one payload.
  Resolve membership by paging `GET /api/roms?collection_id=` (or
  `smart_collection_id=` / `virtual_collection_id=`) instead.
- Use the `/identifiers` endpoints for deletion reconciliation rather than re-pulling full
  rows, **except `/api/roms/identifiers`**. It answers quickly, but it takes no parameters, so
  it returns the whole library's ids and cannot be scoped to a set. Deletion of content is
  reconciled through set re-resolution instead, whose walk already yields each set's ids; see
  RB-81.
- `gamelist.xml` only ever contains locally present ROMs. **Not because ES cannot take a
  large one**: ES loads a 100,000-entry gamelist in 2.07 s for 419 MB (RB-356). A gamelist is a
  mirror of what is on disk, and that is the whole of the rule.

  **There is no per-system cap, because a cap cannot bound what a person scrolls
  past.** ES lists ROM files it has no gamelist entry for, so dropping entries hides
  no games and only strips their art and description: the user still scrolls past exactly as
  many tiles, now blank. What bounds navigability is the sync set's own `max_games`, which
  is principle 3's argument and already exists. A sync reports a folder that grows past a
  threshold rather than truncating it. `ParseGamelistOnly` would make the gamelist
  authoritative and give a cap teeth, but it is a global ES setting affecting systems
  RomMBat does not manage, so RomMBat does not touch it. See RB-111.

- Warn before a set resolves to more than a configurable game count or byte size.

### 3. Curation, so the device shows what the user cares about

A 100k library is unnavigable from a couch with a gamepad. The organizing abstraction is
a **Sync Set**: a named scope plus a policy.

- **Scope** can be a collection, a smart collection, a virtual collection, a platform, or
  a saved filter over any supported `GET /api/roms` query. Collections are the
  recommended default and the best first implementation, but they are deliberately _one_
  scope type rather than the only mechanism, so users who do not curate collections in
  RomM are not stranded. Useful ready-made filters include `favorite`, `last_played`,
  `has_saves`, `playable`, plus the multi-value `genres` / `franchises` / `companies` /
  `regions` / `player_counts` filters with their `any`/`all`/`none` logic operators.
- **Policy** covers: max games, max bytes, ordering (name, recently added, recently
  played), and eviction rules (keep favorites, keep the last N played, and **never evict
  a game with unflushed local saves**).
- Smart collections are re-evaluated server-side and their membership drifts, so
  re-resolve every set on every sync: new members are added, departed members become
  eviction candidates rather than immediate deletions.
- **A smart collection's listed `rom_count` is its owner's, not the caller's.** It is stored,
  and recomputed as the owner, while paging applies the criteria as whoever asks. So a public
  collection filtering on `favorite` lists another account's favorites and pages back only
  the caller's: 29 of 29 on a live instance advertised 6 to 594 and paged 0 (RM-16). The
  picker shows no count for one, and the resolve reports what the set really holds (#193).
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

  **This location is not a free choice.** A `system/es_menu/*.menu`
  entry resolves its executable path under `emulators\`, and `emulatorLauncher` refuses
  `..\` escapes outright (`[Generator] Failed. path is null`, exit 204). An app installed
  anywhere else cannot be launched from the ES menu at all. See
  RB-384.

- **Never persist an absolute path.** The local file index, sync-set definitions and
  outbox entries all store paths **relative to the RetroBat root**. Resolve to absolute
  only at the moment of use. A drive letter that shifts from `E:` to `F:` must be a
  non-event. Note the ES hooks receive an **absolute** rom path in `$1`, so relativizing at
  that boundary is mandatory work, not an optimization.
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
    The failure is an `IOException`, Win32 112 `ERROR_DISK_FULL`, message **"There is
    not enough space on the disk"**, raised on a volume with 14.6 GB free (RB-48). **Never surface
    that message**; it sends the user to delete files that are not the problem. Compare
    `fs_size_bytes` against the target filesystem before the download starts.
  - **FAT and exFAT store coarser modification timestamps than NTFS**, and
    **exFAT is no better than FAT32: 2 seconds on both** (RB-393), even though exFAT's format
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
