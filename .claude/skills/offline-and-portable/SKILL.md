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
- **Retries are safe by design.** Play sessions dedup on truncated-to-the-second timestamps
  server-side; save uploads dedup on `content_hash` within a slot. Lean on that instead of
  inventing an ack protocol.
- **Clock skew is a real failure mode.** A flat RTC produces timestamps that lose every
  conflict. Keep a monotonic sequence alongside wall clock, compare against the server's
  `Date` header on first contact, and offer to re-stamp the outbox past a threshold.
- **Conflicts are normal, not exceptional.** Default `keep_both`, never silently overwrite,
  always copy aside first.
- **No daemon exists.** A portable install cannot register a service or scheduled task, so
  the flush is a short-lived process invoked from `start`, `game-end` and `quit` hooks and
  from the UI, guarded by a lock file in the tree. One pass, then exit.
- Partial downloads survive power loss: write `.part`, verify, rename.

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
