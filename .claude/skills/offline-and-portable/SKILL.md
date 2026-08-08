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
  of use. A drive letter changing from `E:` to `F:` must be a non-event. There is a build
  check for this; do not defeat it.
- **Find the root relative to the executable.** Walk up from `AppContext.BaseDirectory` to a
  marker. Hooks use `%~dp0..\..\..\`.
- **Identity follows the drive.** A GUID in the tree sent as `client_device_identifier`.
  Never MAC or hostname. See `romm-api`.
- **The filesystem may be exFAT or FAT32.**
  - FAT32 cannot hold a file over 4 GB, which excludes many PS2/GameCube/Wii images. Detect
    and refuse cleanly rather than failing mid-write.
  - FAT and exFAT store coarser mtimes than NTFS (FAT32 is 2-second granularity), so
    `content_hash` is the primary comparison and mtime only a tiebreak.
  - No ACLs, no symlinks. Neither can be part of any design.
- **No DPAPI.** `CurrentUser` binds ciphertext to a profile on one machine, `LocalMachine`
  to that machine; either makes the drive undecryptable on the next PC. On a portable
  install the token is only as protected as the drive, so default to a scoped, expiring
  token, offer an optional passphrase, and make re-pairing cheap.
- **Long paths.** Deep portable paths plus long ROM names plus `images/` siblings can cross
  `MAX_PATH`. Use long-path-aware APIs and `\\?\` prefixes where needed.
