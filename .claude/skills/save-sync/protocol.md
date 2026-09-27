# Sync protocol

Part of the [save-sync](SKILL.md) skill. How a slot moves between this device and RomM, and what the slot records.

## Protocol rules

- Pair on `(rom_id, slot)`. Always send a stable, non-null slot.
- Send the **real local mtime** as `updated_at`, never the sync time.
- Compare on `content_hash` first, mtime second: **exFAT and FAT32 both quantise mtime to
  2 seconds and round up**, so a save can be stamped 2 s in the future and several saves
  written together share one timestamp. Mtimes are not bit-stable across filesystems.
- **`overwrite=true` does not replace a row in the slot, whatever it looks like.** A slotted
  upload is renamed with a `[YYYY-MM-DD_HH-MM-SS]` tag and the row is keyed on that name, so the
  clock decides: same second updates, a second later appends. `overwrite` only suppresses the 409
  checks and the identical-content dedup. So `--keep-local` appends, the server's copy stays one
  row down until something writes into it in place (see "Other writers on the same slots"), and `autocleanup_limit=10` is what bounds the slot rather than the resolution bounding
  it at one. Never tell a user their copy replaced the server's. Measurement 160.
- An unregistered `device_id` is a **404**, not a request that quietly proceeds without a device.
  **Omitting it altogether is accepted**, and produces a save attributed to no device, which is
  how a second device is simulated on an install that has only one. So the 404 bounds
  impersonation, not participation. Measurement 162.
- 409 means the slot moved. Surface it; retry with `overwrite=true` only after resolution.
  **The body is a bare string** with no save id and no timestamps, so fetch the save row if
  you want to show the user what they are conflicting with. It fires when **this device's**
  sync record is stale, not when the save is newest overall.
- **Restore cannot be built on `negotiate`, and this is measured.** The server answers negotiate
  from its own per-device sync record, not from what the client claims. A save this device
  uploaded and acknowledged comes back `no_op`, "No changes since last sync", **even with the
  file deleted from the tree and even when the slot is claimed with `content_hash: null`**. So
  the one save a restore exists for is precisely the one negotiate will never offer.

  Two things that are easy to get backwards here. An **empty** negotiate does return work: 21
  operations on the measured install, each reading "Save exists on server but not on client".
  Every one of them was for a ROM **not** installed, which is the ordinary
  `skipped, for games not synced here` line and is correct. And enumerating absent slots into
  the request adds nothing, because the server was already volunteering everything it believed
  the device lacked.

  `saves restore` therefore walks `GET /api/saves` and filters locally. Unfiltered on purpose:
  the parameters are `rom_id`, `platform_id`, `device_id` and `slot`, none of which takes a
  list, and a save exists only where someone played, so one request returned 55 rows against a
  96,000-ROM library.

  **It is never automatic.** A save that reappears because a flush decided it should is
  indistinguishable from a bug to whoever deleted it deliberately, so finding is separate from
  restoring and `--apply` is required for either.

  **The find scans the tree before it reads `local_save`.** A row already held is not a
  candidate, and the store is only the tree as of the last scan, so a save deleted by hand stayed
  held and the first `saves restore` after the loss offered nothing. Measured on `nes`: the same
  command offered it once `saves` had run in between (#147). The scan lives in
  `SaveSync.FindRestorableAsync` rather than in the subcommand, states first for the #64 reason,
  so every caller of the find gets a true store. It costs a full save scan, the one `saves` pays
  on every run.

  **The find applies every guard the flush's download applies, at the same decision point.** A
  restore reaches `DownloadAsync` with a null local save, which is the same state an unsolicited
  negotiate download arrives in, so a rule written into the flush and not into the find is a rule
  the restore path does not have. The class C guard is the one that bites: with the ROM installed
  and no local unit, a `ppsspp:savedata` row resolves a target, passes `File.Exists` and takes the
  **class A** path, where a null `content_hash` skips verification entirely and the archive lands
  as `saves/psp/<stem>.zip`. Listing it in a preview is already a claim that it can be brought
  back, so it is named with a reason instead, the same reason the flush gives.

  **The write half holds `TreeLock` and refuses rather than skipping.** `saves restore --apply` is
  a third holder beside `saves resolve` and `evict`'s sweep, and for the same reason: it does
  `MoveAside`, `File.Move` and `Saves.Record` over the files a flush is concurrently hashing and
  uploading, and the ES `quit` hook spawns a detached `background quit` flush that nobody sees. A
  flush treats a held lock as success because somebody else is doing its work; nobody is doing a
  restore's, so a failed acquire is `Refused` with an exit code. The `partial/save-<id>.part`
  write is **not** the part that needs it: it is opened `FileShare.None`, which is what producers
  outside the lock rely on. The tree write is.

- **A server save can carry no slot at all**, written by a client that sets none, and
  `local_save.slot` is `CHECK`ed non-empty. Keying that as the empty string threw SQLite error
  19 **after the bytes were on disk**, leaving a save in the tree with no row behind it and
  aborting the rest of the batch. Derive a slot instead. **Null is not the only value that does
  this**: the CHECK refuses the empty string and whitespace the same way, `local_save.emulator`
  has its own, and `SaveScanner.SlotFor` throws on a blank emulator before a CHECK is reached, so
  the test is blankness rather than nullness on both columns. It reaches further than it looks:
  a restore covers **any** installed ROM, not only this device's own uploads, so the values
  arriving are other clients' free text. The derived value is provisional: the
  next scan re-keys a loose save to the loose emulator, measured as `fceumm:battery` becoming
  `libretro:battery`, which is the scanner being authoritative and costs one correction with no
  duplicate row.

- **States download too now, and nothing verifies them.** `StateSchema` carries no hash field of
  any kind, where a save carries `content_hash`, and states have no `/track`, no `/downloaded`
  and no negotiate participation. So a state arrives unverified and the command says so on the
  preview as well as after; that is the ceiling of the API rather than a shortcut. Finding 246.

  **A restored state is named after the ROM on disk, never after the server row.** RomM strips
  parenthesised groups as tags, so `Legend of Zelda, The (USA) (Rev 1) [libretro.nestopia].state1`
  reads back as `Legend of Zelda, The`. Writing the server's name puts a state where the emulator
  never looks, and it then reads as absent rather than as an error. Finding 247.

  **The slot is read out of the uploaded name, not the extension.** `StateSync.SentNameFor`
  strips the scope group back off `file_name`, the template matches what is left, and
  `SaveStateTemplate.FileFor` renders it onto the local ROM's stem. Taking only the extension
  worked for libretro, dolphin, gopher64 and mupen64, and placed nothing for the eight emulators
  that write the slot into the stem (`Game.QuickSave2.State`, `Game_0.jst`, `Game.01.p2s`) or a
  libretro autosave (`Game.state.auto`): every one reported "could not tell which slot it is".
  A name without the group, another client's, falls back to the extension. Finding 258.

  **A restore writes a `local_state` row, or the next flush sends back what it just fetched.**
  `RestoreAsync` records with `uploaded_content_hash` equal to what is now on disk, because both
  sides hold the same bytes. Without it the next scan reads the file as never sent, `NeedsUpload`
  is true, and the upsert on the server accepts every re-send without complaint. This hits every
  state that came from another device, because such a state never had a local row to begin with;
  the same-device case fails too as soon as any flush has run while the file was absent, since
  `ForgetMissing` drops the row. Proven by the no-op re-sync assertion: restore, scan, push, zero
  uploaded.

  **A restored state brings its screenshot only when the server links one** (#158). The find
  carries the id from the state row's `screenshot` field, and the write fetches
  `GET /api/screenshots/{id}/content` into the emulator's declared `<image>`, named from the ROM
  on disk through the template for the reason the state is. Best-effort, like the upload: a fetch
  that fails costs a line and the state still counts as restored, and an image already in the
  tree is left alone. **Both halves were RomMBat's.** A state whose image RomM stored and did not
  link reads `screenshot: null`, and there is nothing to follow, so that state comes back without
  one. Every libretro-shaped state uploaded before finding 258 is in that position and stays
  there, because an unchanged state is not re-sent. So does a
  linked one for an emulator whose `<image>` is its `<file>`, DeSmuME, which has nowhere to put
  it; the find keeps the id either way, so the preview says per row which of the three it is. `DetailedRomSchema.user_screenshots` might reach such an orphan
  by name, and that is unmeasured, so it is not built on.

  **The version rule is a statement here, not a comparison, and saying so is the whole of it.**
  [states.md](states.md) says never silently restore a state made by a different emulator
  version. Neither side can perform that check: `ScopeOf` uploads `emulator[.core]` with no
  version, and `StateScanner.ReadEmulatorVersion` declines on every emulator measured, so the
  local column is null too. So the command prints the ceiling on the preview and before applying,
  and `RecordRestored` leaves `emulator_version` null rather than stamping the build that happens
  to be installed now. Stamping it would be the exact "a wrong version is worse than no version"
  case the scanner's own remarks warn about.

  **The state half of `saves restore` holds `TreeLock` and refuses, like the save half.** Same
  race, same reason: the ES `quit` hook spawns a detached `background quit` that holds the lock
  across `StateScanner.Scan()` over these same directories. A refusal on the save half also
  skips the state half, or a run that promised "nothing was changed" would still write states.
  A refusal on the state half alone is `Partial` rather than `Refused`, because the saves landed.

  **A failure reading `/api/states` must not take the save restore down with it.** They are
  independent reads. A token whose scopes do not cover the route, or a 500 from it, used to
  return `Offline` before a single save was written. It is now reported and carried, and an
  `--apply` that could not see the state half ends `Partial`. A state it can see and cannot place
  does not, for the rule under "Where the flush passes live".

  **The `<slot>` positional narrows saves only, and the help says so.** A state's slot lives in
  its file extension and shares no namespace with a save's key, so matching one against the
  other would be filtering on a coincidence.

- **A conflict is persisted, not printed.** It goes in `save_conflict` and outlives the flush
  that found it, the local file is copied aside **once per conflict rather than once per
  flush**, and `saves resolve <rom> <slot> --keep-local | --keep-server` ends it. There is no
  default side, because either default silently discards somebody's progress. `--keep-local` is
  the only caller of `overwrite=true` in the codebase; a 409 that survives it means the slot
  moved again between the report and the decision, so it is reported rather than forced. Both
  outcomes prune the copy aside.

  **`overwrite=true` supersedes, it does not replace in place.** Measured on the live instance in
  M7 stage 7b-3: a keep-local on a psp class C unit created a new save row and left the previous
  one standing, one second apart. Confirmed on a second shape, class A on `nes`: a keep-local sent
  save 212 and left 210 standing. The flag is what gets past the 409 an ordinary upload earns
  when this device's sync record is stale; it is not an instruction to the server to reuse the
  row. Anything reasoning about how many rows a slot has after a resolution has to expect two,
  subject to the one-second rule above: a decision taken later than the same second appends, and
  one taken inside it updates the row in place.

- **`ConflictResolutionService` takes `TreeLock`, and refuses rather than treating a held lock
  as done.** It runs the same `SaveUnitTransfer.Restore` a flush does, so two of them at once, or
  one racing `evict`'s sweep of `partial/`, leaves a shared container half swapped. Unlike a
  flush, where failing to acquire means another agent is already doing the work, a person asked
  for this one and silently returning `Ok` would read as having resolved it: it exits `Refused`
  and says why.

  **The rule is Core's, not `saves resolve`'s, and that is a boundary rather than tidiness.** M7
  stage 7b-3 gave the interface a conflict screen, and the UI never referencing `TreeLock` is
  asserted structurally against the built assembly: a flush treats a failed acquire as success,
  so a second caller taking the lock would make a concurrent `background quit` flush skip its
  upload and call it success. `saves resolve` is a shell over the service and keeps only its
  argument parsing and its exit-code mapping.

  **It takes a connection factory, not a connection, and the order is the point.** The lock is
  taken first, because a resolution that cannot run is not worth a round trip to the server. A
  caller handed in an already-open connection would have paid for it before finding out.
  `ConflictOutcomeState` separates `Busy` from `Failed` for the same reason: nothing was tried.

  **A null connection is `NotPaired`, never `Offline`, and the difference is a real install's
  only way out.** `InstallSession.Authenticate` decides it without touching the wire: the two
  returns carrying a null connection are a missing pairing row and a token that will not unlock.
  Calling that offline tells a person whose passphrase-protected store will not open to try
  again when they are back on the network, which will never be true, and it withheld the screen's
  pairing offer at the same time, since that offer was gated on `Failed`. A paired install with
  no `RomMDeviceId` is `NoDeviceId` for the same reason: its message is "Pair again", so it owes
  `ExitCode.NotPaired` rather than the `Partial` a default arm gave it. `Offline` is left for the
  front end that catches `RomMUnreachableException` round the call, which the service itself
  never raises.

- **`partial/unit-<guid>/` is live state for the length of a class C restore, and nothing holds
  a handle on it.** `SaveArchive.Extract` closes each entry's writer inside its own loop, so the
  staging directory sits unprotected across the hash, the copy aside, the `Remove` and the whole
  move loop. Anything that deletes under `partial/` must hold `TreeLock`. **A `FileShare.None`
  sentinel inside the directory is not a substitute**, measured rather than assumed:
  `Directory.Delete(recursive: true)` removes the siblings first and only then fails on the
  sentinel, so the staged members are gone regardless.
- **A server time shown beside a local one has to be read as UTC first.** RomM serialises
  `updated_at`, `server_updated_at`, `created_at`, `start_time` and `end_time` with no zone while
  storing UTC, so a plain `DateTimeOffset` is out by the machine's own offset and the conflict
  block shows the two sides on different clocks. `UtcTimestampConverter` is on every one of them;
  see the `romm-api` skill and finding 260. **Rows written before that fix carry the shifted value** in
  `save_slot.updated_at` and `save_conflict.server_updated_at`, and nothing rewrites them: they
  correct themselves when the slot is next negotiated or the conflict resolved, and they are
  display-only in the meantime, since ordering compares server rows only against each other.
- **Negotiate falls back to `updated_at` wherever the hashes do not settle it, so two of its
  answers have to be overruled here and a third is guarded against.** The repo's own rule is that
  mtime never decides whether a save changed, and this is the server applying that reasoning on
  the other side of the wire. `tools/romm-5.3-probes/s4-older-mtime.py` asks it directly, six
  cases, and is the instrument to re-run rather than reasoning from a flush (#206, finding 259).
  At `beta.1`, `5.3.0` and the `5.3.1` floor alike: M1 `no_op (No changes since last sync)`, M2
  `upload`, M3 `download (Server save is newer (no sync history))`, M4 `no_op (Content is
  identical)`. M5 and M6, added at `5.3.0` and answering the same at `5.3.1`, are the peer-row
  cases under "Hash contents, not the archive".
  - **A `no_op` for a slot whose `content_hash` differs from `uploaded_content_hash` is
    uploaded, unless the offered `server_content_hash` equals the local `content_hash`.** That
    exception covers every shape, not only class C: the head already holds these bytes, so the
    save is recorded as sent, and the head acknowledged first when it is not the row this device
    last exchanged (`SaveSync.HoldsHead`, `SettleOnHeadAsync`). Otherwise the inequality is the
    client holding evidence the server lacks: this device has a change the server has never
    seen, whatever the timestamps say. The server answers `no_op`,
    "No changes since last sync", for content that differs whenever the local mtime is older than
    this device's own last upload, so a save restored from a backup, copied off another machine
    or extracted from an archive was never sent and the flush said nothing at all. Uploading is
    safe rather than merely better than silence: identical content into one slot reuses the row,
    and a stale device record still answers 409, which lands as a conflict.
  - **An `upload` of bytes the server already holds is a no-op without a round trip, and this
    one is defence rather than a live fix.** Finding 259 measured `1 up` on three consecutive
    flushes for save 336 on 5.3.0-alpha.3, an emulator rewriting a save with identical bytes
    moving the mtime and nothing else. **It does not reproduce at the floor**: M4 answers `no_op
(Content is identical)`, so the hash settles it server-side. Kept because it costs one
    comparison against a value the operation already carries, and because the failure it
    prevents is a silent upload on every flush forever.
  - **That guard must not ask who uploaded the row.** `AlreadyHeld` requires the slot to name
    this device, which is right for a download and wrong for an upload: whether the server holds
    these bytes has nothing to do with who put them there. `AlreadySent` is the upload-direction
    form and drops that test. The first cut shared `AlreadyHeld` and so missed its own headline
    case, caught on the install, where `Legend of Zelda, The (USA) (Rev 1).srm` holds
    `libretro:battery` as save 344 with a **null** `origin_device_id`, because that row came down
    rather than up. Every slot whose current row arrived from a peer or from RomM's browser
    player is in that state.
  - **A `download` over a local save the server has never seen is a conflict** (#211,
    `SaveSync.UnsentLocalWouldBeReplaced`). M3 is the case: a slot this device has no sync record
    for is answered "Server save is newer (no sync history)" whatever the device holds, so two
    devices playing one game offline, the ordinary case for a handheld, had the second one's
    save replaced on its first flush with no conflict, leaving only a copy under `replaced/` that
    nothing points to. The test is the `no_op` rule's, from the other side: an unsent save, or one
    changed since its upload, is evidence the server lacks. Identical bytes download as before.
    The restore find is untouched: it reaches the download only with no local save.
- **Negotiate returns a download for every save the device has no sync record for**, including
  slots the client did not submit. An **empty** `saves` array came back with 13 downloads across
  two ROMs, one never named by the client, and acking one dropped the next answer to 12. An
  earlier reading of this was backwards: a device that is already current for everything gets no
  operations, which is not the same as nothing being volunteered. **So negotiating with an empty
  array is the fresh-device inventory pass**, and no separate one over `GET /api/saves` is
  needed. A restore onto a device that never held the slot is an ordinary case, not a dead one,
  so **never return early because the local save list is empty**: that is the device with the
  strongest reason to pull. The target for such a slot comes from the ROM's own folder and stem,
  with only the extension read off the operation's tagged filename. **That target is usually a
  file another slot already keeps**, the loose class A save, and writing it was a silent overwrite
  that the next scan then uploaded into the other slot (#205, finding 259: RomM 5.3.0-alpha.3's
  browser files a fresh session under `autosave`, which lands on the `.srm` this device keeps as
  `libretro:battery`). So a download for a slot this device holds no row for, whose destination
  another slot's `local_save` holds, is **recorded as a conflict on the offered slot** and never
  written; `--keep-local` finds its local side by the conflict's path, since the offered slot has no
  row. **When the offered `content_hash` equals the holder's, it is a no-op instead**, or the save
  `--keep-local` just sent comes back offered and reopens the conflict it settled; a plain file hash
  is enough there because the bundled case below was refused first. **A bundled slot is the
  exception and is refused with a reason**, because a class C restore needs a container and a
  unit key and both come from a local unit this device does not have; recognise it from the
  shapes table, never from a `.zip` extension.
- **Unscoped negotiate means most of the answer is for games the device does not hold, and that
  is not a failure.** A device carrying a 10-game sync set out of a 500-save library is offered
  every one of them, and each has no local ROM, so no folder and no stem to build a target from.
  Counting those as failures gives a per-operation stderr line each and a `Partial` exit on every
  flush a partial-library device ever runs, which is what sync sets are for. It is one count in
  the summary, and it is kept out of `Problems` so a quiet hook-driven flush stays quiet. Check
  it **before** the bundled-slot refusal. Those two do not compete today, because
  `IsUnplaceableUnit` needs the ROM's folder to reach the shapes table and so answers false for
  an absent ROM: driven on a real install, a `ppsspp:savedata` operation for an unsynced game
  fell through it to "nowhere to write it". Deciding the absent ROM first keeps that from
  depending on the shape lookup's internals.
- Persist the `file_name` the server returns, not the one you sent, and **write a different name
  to disk**, because `Game [2026-08-10_22-58-26].srm` is invisible to an emulator matching on the
  rom name. **The name to write is the ROM's own stem plus `file_extension`, and it is not
  `file_name_no_tags`.** The server strips general tags rather than only its own timestamp: a real
  save came back as `Phantasy Star (Brazil) [2026-08-17_17-01-00].srm` with `file_name_no_tags` of
  `Phantasy Star`, because `(Brazil)` is part of the ROM's name. Writing that produces a file the
  emulator cannot see, which is the exact failure this rule exists to prevent. The ROM stem needs
  no regex: it is the `(folder, stem)` key class A attribution already uses, run backwards.
- **Download with `optimistic=false`, then ack.** The parameter defaults to true and records
  the device as current on the request rather than on receipt, so a transfer that dies
  mid-body leaves the server sure the device has a save it does not, and the next negotiate
  answers `no_op`. Send `POST /api/saves/{id}/downloaded` only after the bytes are written
  and verified. Same discipline as M3's `.part`: verify, then commit.
- **Decide retention.** `autocleanup` defaults to false and `autocleanup_limit` to 10, and a
  writer that leaves them alone gained a row per genuine change forever up to 5.3.0-alpha.2,
  which the `keep_both` conflict default compounds. **This client is not that writer**: every
  save upload sends `autocleanup=true&autocleanup_limit=10`, so its slots have been bounded at
  10 since M6. **From alpha.3 the server prunes every slotted upload whatever the client asks**,
  keeping the newest by `updated_at` then `id` past the **tighter** of `MAX_SAVES_PER_SLOT` (env,
  50 by default, `0` disables) and the client's `autocleanup_limit`. So the cap here stays 10,
  and `MAX_SAVES_PER_SLOT` only ever bites on versions written by something that asks for no
  cleanup: a peer, or RomM's browser player. **Measured against such a writer, and safe as long
  as the upload 409 stays a conflict**: a version this device still names in `save_slot` can be
  deleted under it, negotiate then forgets the history and answers `upload` for an edited copy,
  while the upload guard still holds the device's record and refuses 409, which `SaveSync`
  records as a conflict. An unchanged copy downloads the newest. Finding 11 of
  `romm-5.3-findings.md`, `s3-slot-retention.py`.
- Restores stage everything off to one side: extract to a temp directory beside the target, keep
  the previous copy until the next successful sync. **A class C swap is not one filesystem
  operation, and do not write that it is.** Members are removed and moved in one at a time,
  because a whole-container swap is the wrong fix: the container is shared, and
  `saves/psp/SAVEDATA` holds every PSP game on the install. It is **all-or-nothing anyway**: a
  failure partway is rolled back, the members the pass placed deleted and the ones it removed
  copied back from `replaced/`, so the unit ends up wholly new or wholly as it was. The one case
  that still leaves a mixed unit is a rollback that cannot finish, and the message says so by
  name and names the `replaced/` copy.
- **A class C restore whose contents already match the tree does not write the tree.** The fold
  of what arrived is computed before anything live is touched, so it is free. The transfer is
  not avoidable and the write is: a peer holding identical bytes carries a digest this device has
  never seen, so negotiate answers `download` and no local comparison can rule it out. The ack
  and the slot record still run, or the next negotiate answers `upload` for a unit in step.
- **A bundled restore replaces the unit, it does not merge into it.** Delete the members the
  archive does not name before moving the new ones in; they are under `replaced/` by then. The
  members the archive omits are the slots another device deleted, and leaving them makes the
  fold over the tree disagree with the fold over the archive, so the next scan reads the unit
  as changed and puts the merged copy back over the server's. Somebody who chose to discard the
  local side gets a merge instead, and it propagates.
- **Record the slot's server identity when a bundled restore lands**, not only the local fold.
  The recorded save id is what recognises a superseded row returning and what scopes the in-step
  rule above. Measured before 303: the flush after a class C restore reported one upload, which
  the server then deduplicated into a row it already had.
- **A settled conflict is settled for one server row, not for one digest.** `content_hash` is
  over an archive's contents, so a slot returning to contents it held before carries a digest
  that was already decided while being a different row. Compare the save id too, or a real
  conflict is dropped: no row to list, `resolve` answering "already resolved", and the local
  write refused with a 409 on every flush with no way out.
- Never evict a ROM whose saves are still in the outbox.

## `device_id` is bookkeeping, never a filter

Both devices see the same save rows; nothing is isolated per device. What is per device is
the sync record, exposed as `device_syncs` on a save, and **that array is empty unless the
request carries `device_id`**. Empty therefore reads exactly like "nobody has ever synced
this", which is why it must not be read that way. With `device_id` set it lists every device
that has a record, the queried one first, and a device that has never synced is **absent**
rather than `is_current: false`. Treat a missing entry as the strongest reason to pull.
`origin_device_id` names the uploader, which is how a device recognises its own save
returning.

## The slot is the key to everything, so a save without one is inert

`slot` is an **optional** query parameter on `POST /api/saves`, and a client that omits it
produces a row that RomMBat can neither reconcile nor collide with. Measured end to end on a live
5.2.0 instance:

- Negotiate keys on the slot, so a null-slot save is **never fetched** (#138). **Reported, and no
  slot is derived for it** (ruled): a derived slot may not match what the originating client
  would use, and two clients keying one save differently is worse than a save sitting visible.
  `saves` reads `GET /api/saves` when the install is paired and lists every blank-slot row for a
  ROM on this device, via `SaveSync.FindSlotlessAsync`. It is the one network read in that
  report, so `--offline` skips it and a failed read costs one line and not the exit code.
- It also **cannot conflict**. A save uploaded through RomM's own web UI, which sets no slot, left
  the device's record for `libretro:battery` current, and the next flush uploaded over it with no
  409 and no mention.
- It still **resolves to the same destination path** as the slotted rows for that ROM. So does a
  slot's own history. `saves restore` used to offer every one as its own restore, and applying
  them wrote one file repeatedly and kept whichever came last (#156). **The find now keeps the
  newest row per destination** by `updated_at` then save id, whatever its slot, and the preview
  names the rows it folded and says when a null-slot row and a slotted one share the file. It
  narrows by `<rom> <slot>` before folding, so asking for a slot by name gets that slot's newest
  row even where a newer null-slot row shares the file. **The flush had the same gap for slotted
  rows and it is closed separately**: a negotiated download for a slot this device never held,
  landing on another slot's file, is a conflict and not a write (#205).

So a null slot is not a save in a different slot, it is a save outside the protocol. Never treat
the absence of a conflict as evidence that the server holds nothing newer: it may hold something
newer that the protocol cannot see.

**Staging a conflict therefore takes a slotted upload**, `POST /api/saves?rom_id=<id>&slot=<slot>`,
with `device_id` omitted for the reasons in the protocol rules above, plus a local edit. Nothing
reachable from RomM's UI will do it.

## Every path that writes server bytes writes the slot

`save_slot` holds this device's picture of what the server has in a slot, and every path that
writes server bytes to disk owes it an update:

| Path                                                | Class | Records `save_slot` |
| --------------------------------------------------- | ----- | ------------------- |
| `SaveSync.RestoreUnitAsync`, download               | C     | yes                 |
| `SaveConflictResolver.FinishUnitAsync`, keep-server | C     | yes                 |
| `SaveSync.RecordRestored`, download                 | A     | yes                 |
| `SaveConflictResolver.KeepServerAsync`, keep-server | A     | yes                 |

**The class A rows said no until #157**, and it was measured before it was fixed: after a
negotiate-driven download of save 211 the row still read `save_id 209` with the pre-download hash
while 211's content sat on disk. It does not self-correct, because the local file is then in step
and the slot is never negotiated again. The server-side sync record **is** updated, which is why
nothing visibly broke.

**Fixing the download alone would have left keep-server broken.** They are two independent
writers of `local_save`, not one path with a caller: `KeepServerAsync` does not call the download.
A restore of a save the server holds **with no slot** records no slot identity, because that row
is outside the protocol and its save id would otherwise stand in for a slot it never belonged to.

**The recorded save id is now load-bearing**, which is why this was fixed rather than left
cosmetic: it is what recognises a superseded row returning to the head of its slot. See "[Other
writers on the same slots](other-writers.md#other-writers-on-the-same-slots)".

**Copy aside before overwriting is honoured on the download path too**, not just on conflicts,
which is worth knowing before assuming a download is safe to make silent. A resolution prunes its
copy; a download's copy is currently never pruned. Since #211 a download only ever replaces
bytes this device already sent, so that copy duplicates the server rather than being the last
record of a save.

## Determinism is what makes replay safe

Identical content uploaded twice into one slot reuses the same row, which is what makes a
replayed flush idempotent. That only holds if the bytes are identical, so a bundled class-C
save must produce a **byte-identical archive** for unchanged contents. Freegosy writes a
timestamp file into every bundle specifically to defeat this dedup, and pays for it with a
new server row on every sync of an unchanged save. Hash the logical contents, and keep the
archive deterministic. **The dedup is off under `overwrite=true`**, measured, so it covers the
flush path and not `saves resolve --keep-local`: a repeated resolution makes a row.

Play sessions replay safely too, and say so: `POST /api/play-sessions` returns a per-index
result array and marks a repeat `"status": "duplicate"`, so a partial flush is reconciled
exactly rather than inferred. It needs no open sync session.
