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

- **`game-start` and `game-end` are journal-only.** Those two run inside the game-launch
  path: append and exit in milliseconds, never open a socket, never start a process.
  **`start` and `quit` are outside it** and each spawns a detached `background <event>`
  agent pass. The set is `SpoolRecord.BackgroundEvents`, which the hook compiles rather than
  references, and a test asserts it.
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

  **What invokes it: the `start` and `quit` hooks, `sync`, and a person typing `flush`.**
  The hook spawn landed in M7 stage 7a and is what makes an install nobody administers from a
  terminal work; before it, `sync` and a typed `flush` were the whole trigger set and an
  install that was never synced spooled events forever. The reason recorded for the delay was
  an 11 MB process start inside the game-launch path, and **that measurement went the other
  way** (findings 195 and 197): ES does not wait for a hook, and the 75.9 MB agent starts
  faster than the 11 MB hook because trimming without `PublishReadyToRun` discards the
  framework's precompiled code. Cost was never the reason to refuse a spawn. **Rule 4 was**,
  and 7a narrowed it to the two events that are not in the launch path rather than bending it.

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

- **A cancelled transfer's partial is truncated before its handle is closed, and this is a
  measurement about slow drives rather than about tidiness.** Cancelling a download is instant.
  What is not instant is closing the handle over a large part-written file, because that waits
  for the drive's write cache: measured on the live install, stopping 10.9 s into a PS2-sized
  download spent **20.1 s** in `FileStream.DisposeAsync` alone, and the file was deleted a
  moment later. RomMBat was paying to flush bytes it was about to discard. Truncating first took
  the same stop from 20.2 s to 0.2 s. It costs the resume, so it is done **only** on a user
  cancellation, where the transfer is discarded by ruling. An unreachable server keeps its
  partial, because resuming from it is the whole reason one is written.

  Portable installs are exactly where this bites: the same code on an internal SSD would hide
  it, and RomMBat lives on the removable drive by design.

- **Never take `TreeLock` to find out whether it is held.** Failing to acquire is a _success_
  for a flush: it concludes another pass is draining the queue and exits, reporting `Ok`
  (`FlushCommand.cs:68-72`). So anything that grabs the lock for an instant just to look at it
  makes a `background quit` flush starting in that instant skip the upload entirely and call it
  success, leaving the user's save in the outbox until the next quit with nothing saying why.
  **Take the lock only around work you are actually going to do**, and hold it for the whole of
  that work. To show whether a pass is running, find another way or do not show it.

  **Reading needs no lock at all.** The store is SQLite in WAL mode, so a reader and a writer
  coexist. The gamepad UI is read-only through stage 7b-1 and therefore never touches the lock,
  which a structural test asserts against the built assembly.

  **The UI writes as of stage 7b-2a and the assertion still holds, because a Core service takes
  the lock and the UI never names the type.** Two rules fall out, and the first is the one that
  looks wrong:
  - **A write to SQLite alone takes no lock.** Defining, editing or deleting a sync set, and
    setting the disk budget, are rows in a WAL database. The tree lock serialises writers of
    _files in the tree_, and taking it for a set definition would refuse a user's set because
    somebody else was draining the outbox: two unrelated things sharing a mutex. A test asserts
    a set is definable while a background pass holds it.
  - **A write to files takes the lock inside Core and returns the refusal as a value.**
    `PartialSweep.Apply` already did this before the seam existed, returning
    `PartialSweepOutcome.Skipped` with its own sentence ("partial/ was left alone: another
    agent is writing there. The next pass sweeps it."). `EvictionService` surfaces that rather
    than reimplementing it. **This is the pattern to copy**: never a throw, never a silent
    no-op, and never a lock taken speculatively to answer a question.

  `UiTreeLockTests` carries the anti-vacuity companion as of #100: Core must still _define_
  `TreeLock`, or renaming it would disarm the boundary with nothing saying so.

  **The flush settles this for good as of 7b-2b: it takes the lock itself and returns
  `FlushState.Skipped`.** `SaveFlushService` is one Core service that both `flush` and the sync
  screen are printers over, so the lock is acquired in exactly one place and the refusal reaches
  either front end as a value with its own sentence. Nothing outside Core needs to know the lock
  exists. `FlushCommand` keeps only `--quiet`, the conflict block and the exit codes.

- **Which operations work with the server off, on the sets surface.** Listing sets, defining
  one, editing its caps and ordering, deleting it, and setting the disk budget and free-space
  floor are all local and all answerable offline. **The whole eviction surface is offline too**,
  added in 7b-2b: the preview is two local scans and a walk of `local_file`, and carrying it out
  deletes files and rewrites gamelists from local state. **Only resolving and syncing need the
  network**, and they are the only screens that say so. A screen that cannot tell those two apart is wrong: the
  whole point of defining a set on a handheld away from its server is that it can be done.

  **A resolve is minutes-long work, measured rather than assumed:** a platform scope of 9,196
  roms took **8 minutes 15 seconds** against a live 5.2.0 instance at 250 rows a page. So
  cancelling it is the ordinary case, not a failure path, and a cancel records its offset
  exactly as an unreachable server does so the next run continues. Discarding the walk on
  cancel would make the feature worse than not offering it.

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
  transfer that starts again rather than data. **Reproduced on a real install, not reasoned
  about**: against a staging directory holding three real PPSSPP `SAVEDATA` members, the
  pre-lock build reported "1 abandoned transfer removed" and took all three while a flush held
  the lock; the same scenario on the fixed build left them alone and reclaimed them on the next
  pass.

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
