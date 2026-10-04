---
name: offline-and-portable
description: Offline-first behaviour and portable-install constraints - the outbox, relative paths, clock skew, filesystem limits, token storage. Use when touching local state, the sync flush, file paths, or anything that must survive being unplugged or moved.
---

# Offline and portable

Two constraints that decide the data model. Build to them, do not retrofit.

## Map

This file holds the core offline rules, the background pass and portability. The rest is in topic
files beside it, by section:

| Section                                                                                                                                                           | File                           |
| ----------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------ |
| [Offline-first](transfers.md#offline-first): cancelled transfers, the `.part` rule and the `partial/` sweep                                                       | [transfers.md](transfers.md)   |
| [Offline-first](store.md#offline-first): the shared SQLite connection and `TreeLock`                                                                              | [store.md](store.md)           |
| [Offline-first](offline-ui.md#offline-first): which set and mapping operations work with the server off                                                           | [offline-ui.md](offline-ui.md) |
| [Browsing and removing, offline](offline-ui.md#browsing-and-removing-offline)                                                                                     | [offline-ui.md](offline-ui.md) |
| [Two kinds of list, and drawing one as the other](offline-ui.md#two-kinds-of-list-and-drawing-one-as-the-other)                                                   | [offline-ui.md](offline-ui.md) |
| [A footer offers a verb exactly when that verb works](offline-ui.md#a-footer-offers-a-verb-exactly-when-that-verb-works)                                          | [offline-ui.md](offline-ui.md) |
| [The claim rule: a game another enabled set still wants is held back](offline-ui.md#the-claim-rule-a-game-another-enabled-set-still-wants-is-held-back)           | [offline-ui.md](offline-ui.md) |
| [Removing content](offline-ui.md#removing-content)                                                                                                                | [offline-ui.md](offline-ui.md) |
| [`local_file` rows outlive their bytes, and the budget counts them forever](offline-ui.md#local_file-rows-outlive-their-bytes-and-the-budget-counts-them-forever) | [offline-ui.md](offline-ui.md) |

## Offline-first

The target is a handheld Windows gaming PC away from the server for days. Local SQLite is
the source of truth; the network is optional, probed with a short-timeout
`GET /api/heartbeat` (budget from RB-353).

- **`game-start` and `game-end` are journal-only.** Those two run inside the game-launch
  path: append and exit in milliseconds, never open a socket, never start a process.
  **`start` and `quit` are outside it** and each spawns a detached `background <event>`
  agent pass. The set is `SpoolRecord.BackgroundEvents`, which the hook compiles rather than
  references, and a test asserts it.
- **Everything produced offline goes to an outbox** with its real local mtime and content
  hash, never its sync time. A week offline is just a bigger negotiate payload; the protocol
  is full-state reconciliation and handles it natively when timestamps are honest.
  **An entry the server answers for and does not accept ends `failed`**, keeping its error: a
  replay is refused the same way, and a pending one would hold `SaveGuard` and `uninstall`
  forever. That a replay is refused the same way is inferred from the status, not measured
  against a live RomM. Offline, a 5xx, a 401 and a refused whole batch say nothing about one entry and stay
  pending. `outbox drop` is the only way a queued record is deleted unsent.
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
  [`.part` rule](transfers.md#offline-first): verify, then commit. See `save-sync` and RM-17.
- **Clock skew is a real failure mode.** A flat RTC produces timestamps that lose every
  conflict. Keep a monotonic sequence alongside wall clock, compare against the server's
  `Date` header on first contact, and offer to re-stamp the outbox past a threshold.
- **Conflicts are normal, not exceptional.** Default `keep_both`, never silently overwrite,
  always copy aside first. Keeping both means keeping the local side **local**: uploading it
  would make it the newest row in the slot and tell every other device to take it, which is an
  unresolved conflict resolving itself in favour of whoever synced last. The conflict is
  persisted and waits for a person to pick a side, with `saves resolve` or the UI's conflict
  screens.
- **Exit `Offline` (5) means unreachable and nothing else.** It is the one code that tells a
  script waiting will fix it. A 401 or 403 exits `NotPaired` (4). Any other server answer, or a
  result that could not be verified or written here, exits `ServerError` (8), so a locked
  destination or a path too long for this machine is 8 too. A run with several failures is
  `Offline` only when every one of them was unreachable, which is `FailureCause` ranked worst
  first and mapped in `ExitCode.For`. Classify there rather than returning `Offline` from a
  failed `RomMResponse` (#143).
- **No daemon exists.** A portable install cannot register a service or scheduled task, so
  the flush is a short-lived process, guarded by a lock file in the tree. One pass, then exit.

  **What invokes it: the `start` and `quit` hooks, `sync`, and a person typing `flush`.**
  The hook spawn is what makes an install nobody administers from a terminal work: without
  it, an install that is never synced spools events forever. The spawn costs the launch
  nothing (RB-195 and RB-197): ES does not wait for a hook, and the 75.9 MB agent reaches
  `Main` in 34 ms, against 49 ms for a whole invocation of the trimmed, ReadyToRun hook.
  **What limits the spawn is rule 4**, which allows it only from the two events outside the
  launch path.

  **`background quit` waits for the ES process to be gone before it writes any config**, and
  gives up rather than hanging. Measured: ES exits 48 to 68 ms after the quit hook stamps
  itself, and 10 ms after the pass starts looking on a real session. If it never exits, the
  config stays queued and the flush runs anyway, because the flush touches no file ES owns.

  **The same holds when a change throws rather than refusing**, which a full or read-only
  volume makes it do: the row is finished as `Failed` with the exception message, and the pass
  carries on. A throw left unrecorded is worse than it looks, because the row stays outstanding
  and every later quit re-enters it before reaching the flush, so one row that cannot be
  written stops that machine flushing at all. **Nothing on the config side may be able to end
  the pass before the flush.**

  **The pass logs to `emulators/rommbat/logs/background.log`.** It is started with
  `CreateNoWindow`, so nothing it prints reaches a person any other way, and "why did my save
  not go up" is the first question anyone asks about it.

  **A shared log needs an append-only handle, and `FileMode.Append` is not one.** It seeks to
  the end once, at open, and `FileStream` then writes at its own tracked offset, so two passes
  in flight overwrite each other: measured as four writes from two handles leaving one line,
  and seen on a real install as a line missing its first 18 characters (#153). `BackgroundLog`
  opens with `FileSystemRights.AppendData` only, writes each line as one buffer, and shares
  `Delete` so another pass can roll the file while this one holds it.

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
- **The app installs at `emulators/rommbat/`, not `plugins/`.** RB-384: a
  `system/es_menu/*.menu` entry resolves its executable under `emulators\` and
  `emulatorLauncher` refuses `..\` escapes outright. Anywhere else cannot be menu-launched.
- **Identity follows the drive.** A GUID in the tree sent as `client_device_identifier`.
  Never MAC or hostname. See `romm-api`.
- **The filesystem may be exFAT or FAT32.** All of this is measured, not assumed; see
  RB-48 and RB-393.
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
