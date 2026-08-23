---
name: offline-and-portable
description: Offline-first behaviour and portable-install constraints - the outbox, relative paths, clock skew, filesystem limits, token storage. Use when touching local state, the sync flush, file paths, or anything that must survive being unplugged or moved.
---

# Offline and portable

Two constraints that decide the data model. Build to them, do not retrofit.

## Offline-first

The target is a handheld Windows gaming PC away from the server for days. Local SQLite is
the source of truth; the network is optional, probed with a short-timeout
`GET /api/heartbeat` (budget from `docs/retrobat-findings.md`).

- **Hooks are journal-only.** `game-start` and `game-end` run inside the game-launch path:
  append and exit in milliseconds, never open a socket.
- **Everything produced offline goes to an outbox** with its real local mtime and content
  hash, never its sync time. A week offline is just a bigger negotiate payload; the protocol
  is full-state reconciliation and handles it natively when timestamps are honest.
- **Retries are safe by design, and this is measured.** A byte-identical save re-uploaded into
  the same slot reuses the row; a repeated play session comes back `"status": "duplicate"` in
  a per-index result array carrying `created_count`/`skipped_count`, so a partial flush is
  reconciled exactly rather than inferred. Lean on that instead of inventing an ack protocol.
  **The precondition is that "identical" really is identical**, so a bundled directory save
  has to archive deterministically or every flush mints a new server row.
- **Uploads are safe to replay; downloads are not safe to abandon.** `GET /api/saves/{id}/content`
  records the device as current **on the request** unless `optimistic=false` is passed, so a
  transfer killed mid-body by a dropped link leaves the server sure the device holds a save it
  does not, and the next negotiate answers `no_op`. Pass `optimistic=false` and ack with
  `POST /api/saves/{id}/downloaded` after the bytes are written and verified. Same shape as the
  `.part` rule below: verify, then commit. See `save-sync` and `docs/freegosy-findings.md` F1.
- **Clock skew is a real failure mode.** A flat RTC produces timestamps that lose every
  conflict. Keep a monotonic sequence alongside wall clock, compare against the server's
  `Date` header on first contact, and offer to re-stamp the outbox past a threshold.
- **Conflicts are normal, not exceptional.** Default `keep_both`, never silently overwrite,
  always copy aside first. Keeping both means keeping the local side **local**: uploading it
  would make it the newest row in the slot and tell every other device to take it, which is an
  unresolved conflict resolving itself in favour of whoever synced last. The conflict is
  persisted and waits for `saves resolve` to pick a side.
- **No daemon exists.** A portable install cannot register a service or scheduled task, so
  the flush is a short-lived process, guarded by a lock file in the tree. One pass, then exit.

  **What actually invokes it today is `sync` and a person typing `flush`, and nothing else.**
  The intent was that `start`, `game-end` and `quit` each wake an agent and the UI drive one
  while it runs; neither shipped. The hook writes a spool file and exits without starting a
  process, and the UI is M7. That is tolerable because draining is idempotent and a spool file
  waits indefinitely, and it is deliberately not fixed by having the hook spawn something: that
  puts an 11 MB process start inside the game-launch path, and the cost has to be measured on a
  real install first. **That measurement is still outstanding.**

- Partial downloads survive power loss: write `.part`, verify, rename. **The `.part` lives
  under `emulators/rommbat/partial/`, never beside the target**, so a power loss cannot leave
  a half-written file in a folder EmulationStation scans and offers to launch. Only a
  verified file is renamed into `roms/`.

- **`partial/` needs its own sweep, because neither bound can see it.** The budget counts
  through `local_file` and a partial has no row until commit; the free-space floor reads the
  volume, so the bytes are gone from free space attributed to nothing. `evict` runs
  `PartialSweep` for that. Five producers write here and they die differently: only the ROM
  transfer resumes, so only it is kept, and it is kept on **set membership rather than age**,
  because an interrupted transfer waiting to resume looks exactly like an orphan on disk. The
  other four (`bios-`, `save-`, `resolve-`, `unit-`) open with `FileMode.Create` or delete in a
  `finally`, so anything of theirs left behind is from a pass that died. A name none of the five
  writes is left alone, and that means **matching the whole name each producer writes**
  (`bios-<32 hex>.part`, `save-<int>.part`, `resolve-<int>.part`, `unit-<32 hex>` with an
  optional `.zip`), because a prefix match makes `partial/save-notes.txt` a candidate.

- **`partial/unit-<guid>/` is live state, not litter, so the sweep holds the tree lock.** It is
  where a class C restore extracts a unit before swapping members into a shared container, and
  nothing holds a handle on it: `SaveArchive.Extract` closes each entry's writer inside its own
  loop. Delete it in that window and the restore fails partway through its moves with the
  container half swapped, which is exactly what the `Remove`-before-`Move` ordering exists to
  prevent. **A `FileShare.None` sentinel inside the directory does not fix this**, measured
  rather than assumed: `Directory.Delete(recursive: true)` removes the siblings and only then
  fails on the sentinel, so the staged members are gone either way. `PartialSweep.Apply` takes
  `TreeLock` and returns having done nothing when it cannot get it, and both routes into a
  restore (`flush`, and `saves resolve`) hold the same lock. Producers that run outside it
  (`sync`, `bios`) rely on `FileShare.None` while writing, where losing the race costs a
  transfer that starts again rather than data.

## Portable

RetroBat runs from a USB drive and moves between machines.

- **Nothing outside the tree.** No `%APPDATA%`, no registry, no service, no scheduled task,
  no admin rights. Database, logs, outbox and device identity all live under the install.
- **Never persist an absolute path.** Store relative to the RetroBat root, resolve at point
  of use. A drive letter changing from `E:` to `F:` must be a non-event. Three layers
  enforce it and none of them is optional: the `RelativePath` type is the only path shape a
  store API accepts, every path column carries a `CHECK` constraint, and a test drives the
  same table of bad values through both. `RetroBatInstall.Resolve` and `.Relativize` are the
  only places the two representations convert.
- **Upstream does not follow that rule, and one of its files is inside the sync set.** A
  multi-disc launch leaves `saves/<system>/<playlist stem>.ldci`, RetroArch's record of which
  disc was in the drive, whose `image_path` is absolute down to the drive letter. Anything
  RomMBat copies out of the save tree can carry a foreign machine's paths, so **treat the save
  tree as untrusted for portability**: exclude the file, or rewrite the path on restore. The
  three layers above protect what RomMBat writes, not what it relays.
- **Find the root relative to the executable.** Walk up from `AppContext.BaseDirectory` to a
  marker (`retrobat.ini`, `emulationstation/`, `roms/`). There is no `build.ini`; the version
  lives in `system/version.info`. From a hook, `%~dp0..\..\..\` reaches `emulationstation/`
  and the **root needs a fourth level**, `%~dp0..\..\..\..\`.
- **The app installs at `emulators/rommbat/`, not `plugins/`.** M0 measured this: a
  `system/es_menu/*.menu` entry resolves its executable under `emulators\` and
  `emulatorLauncher` refuses `..\` escapes outright. Anywhere else cannot be menu-launched.
- **Identity follows the drive.** A GUID in the tree sent as `client_device_identifier`.
  Never MAC or hostname. See `romm-api`.
- **The filesystem may be exFAT or FAT32.** All of this is measured, not assumed; see
  `docs/retrobat-findings.md`, probe 7.
  - FAT32 cannot hold a file over 4 GB, which excludes many PS2/GameCube/Wii images. Detect
    and refuse cleanly rather than failing mid-write. The write fails with Win32 112
    `ERROR_DISK_FULL`, **"There is not enough space on the disk"**, on a volume with plenty
    free, so **never surface that message**: compare `fs_size_bytes` up front instead.
  - **exFAT is no finer than FAT32: 2-second mtime granularity on both.** Its format allows
    10 ms; Windows does not use it. `content_hash` is the primary comparison and mtime only a
    tiebreak.
  - **FAT rounds mtime up**, so a file is stamped up to 2 s **later** than it was written,
    and files written inside one 2-second window share an identical mtime. Give any
    "timestamp is in the future" skew check a 2-second tolerance, and never order class-B
    files by mtime.
  - No ACLs, no symlinks. Neither can be part of any design.
- **No DPAPI.** `CurrentUser` binds ciphertext to a profile on one machine, `LocalMachine`
  to that machine; either makes the drive undecryptable on the next PC. On a portable
  install the token is only as protected as the drive, so default to a scoped, expiring
  token, offer an optional passphrase (`TokenProtector`, AES-GCM over PBKDF2), and make
  re-pairing cheap. A passphrase-protected install cannot flush unattended; say so rather
  than pretending the option is free.
- **Identity is a file, not a row.** The `client_device_identifier` GUID lives in
  `emulators/rommbat/device.id` so it outlives the database. A rebuilt store must not become
  a second device in the RomM UI.
- **The local store is SQLite with `PRAGMA user_version` migrations.** Add a new
  `Store/Migrations/NNN-*.sql`; never edit `001`. A database from a newer build is refused,
  because a portable stick may have met one.
- **Long paths.** Deep portable paths plus long ROM names plus `images/` siblings can cross
  `MAX_PATH`. Use long-path-aware APIs and `\\?\` prefixes where needed.
