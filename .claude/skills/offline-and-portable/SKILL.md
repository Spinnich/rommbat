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

  **The rollback above it has to obey the same rule, and it did not.** `GameSync` takes a game
  back whenever it did not land whole, which is a stop _or_ a failure, and its first version
  deleted the `.part` and the `local_file` download row on both. A 929 MB image that lost the
  LAN at 800 MB was therefore rolled back correctly and made unresumable silently, since no file
  had been removed and no `GameRolledBack` event fired to say so. The rollback now takes the
  bytes and leaves the partial unless the user cancelled. **A size-mismatch test cannot catch
  this**, because `ContentSync` deletes the partial itself on a verification failure and the two
  paths then look identical from outside: it needs a transfer the server drops.

  **Bytes are what make a partial worth keeping, and an empty one is discarded either way.**
  `ContentSync` opens the `.part` before it makes the request, so a response that never carries
  a body still leaves an empty file and a download row. Measured on a live install during a
  hands-on pass: one RomM answering **502 for three seconds left 155 empty partials and 155
  download rows**, not one of them resumable, and the person watching reasonably read the pile
  as a fault. Keeping a partial with nothing in it is litter rather than progress.

- **One `SqliteConnection` is shared by every store class, and it is gated inside the process.**
  `SqliteConnection` is not thread-safe and nothing serialised it until M7 stage 7b-2b, which is
  the stage that made the race reachable: before it the only background work touching the store
  was a resolve, and a sync writes from a background thread for minutes while the drawing thread
  reads the same connection on every redraw. The symptom is not a clean exception but
  "Collection was modified" thrown out of `SqliteCommand.Dispose`, from two threads mutating one
  connection's prepared-statement list.

  The gate is entered when a command is created and left when it is disposed, which covers the
  reader because every call site reads inside the command's own `using` scope.

  **A transaction is not a command, and it has to take the gate itself.** `BeginTransaction` and
  `Commit` issue their own `BEGIN` and `COMMIT` through the connection directly, so relying on
  the store calls inside the transaction to gate themselves drops the gate between every
  statement: another thread creates and disposes its own command mid-transaction, which is the
  unserialised prepared-statement-list mutation this exists to stop, and its reads land inside an
  open transaction and see uncommitted rows. `InTransaction` therefore enters the gate around the
  whole thing. `NextSequence` and `CurrentSequence` go through `Command()` for the same reason:
  a raw `CreateCommand` is ungated by construction. This was wrong when the gate was added, in
  the code and in three documents, and `A_transaction_holds_the_gate_for_the_whole_transaction`
  is what pins it.

  **A command must be created and disposed on the same thread.** The gate is a `Monitor`, which
  belongs to the thread that took it, so an `await` between opening a command and disposing it
  can resume elsewhere and the release then finds a gate it does not hold, holding it for ever.
  Every store method is synchronous, which is what makes this safe; an `async` one needs a
  different primitive, and a **re-entrant** one, because `InTransaction` holds the gate across
  the store calls inside the transaction.

  **`StoreGate.Leave` returns rather than throwing when this thread is not the holder, and that
  guard is load-bearing.** `SqliteConnection.Close` disposes every command the connection still
  tracks, on whatever thread closed it, which fires the `Disposed` handler that releases the
  gate. A background loader still mid-read when the store is disposed therefore gets its command
  released by the disposing thread. Removing the guard to make the release diagnosable was tried
  on the strength of a review finding and two tests caught it.

  **Closing the connection is gated too, and for a long time it was the one path that was not.**
  `LocalStore.Dispose` called `_connection.Dispose()` with no gate at all, so `Close` enumerated
  the prepared-statement list while a background reader mutated it and threw out of `Dispose`
  itself. **Do not match on one exception string**: the same race lands as either the
  `InvalidOperationException` "Collection was modified" from the enumeration, or an
  `ObjectDisposedException` naming `SQLitePCL.sqlite3_stmt` under
  `SqliteDataRecord.AddChanges`, when the reader's statement is torn down first. Reproducing it
  six times on `a7b103a` gave four of the first and two of the second. It surfaced as the screen
  sweeps failing **only when
  both test projects ran together**: a screen's loader is cancelled when the screen is disposed
  and never waited for, so under load it is still running when the session closes. Measured on
  main at `a7b103a`, one and then two of 1177 failing across two runs, while
  `tests/RomMBat.Tests` alone passed 1145 of 1145, which is exactly how a race of this shape
  looks when you only run one project.

  So `Dispose` takes the gate through `StoreGate.EnterForClose`, which also sets a `Closing` flag
  that makes `Leave` inert. Without the flag the first abandoned command's `Disposed` handler
  would run on the closing thread, find the gate entered because the closing thread is the one
  holding it, and release it half way through the close, letting another thread back onto a
  connection being torn down. The gate is **released** after the close rather than held, so a
  thread arriving afterwards is answered by the disposed connection with an ordinary exception
  instead of blocking on a gate nothing will ever open.
  `Disposing_the_store_under_a_running_reader_does_not_throw` is what pins the ordering, and it
  reproduces the failure on the first pass without the fix.

  **The `Closing` flag itself is unreached, and that is worth knowing before you spend a day on
  it.** Taking the gate for the close is what excludes the case, not the flag: a command still
  tracked when the close begins has not been disposed, so its thread holds the gate and
  `EnterForClose` has not returned, and a command disposed beforehand is no longer tracked by
  the connection, so `Close` never re-fires its `Disposed`. Instrumenting `Leave` to count
  entries taken while `Closing` is set found **none across all 1178 tests**, and removing the
  flag and its guard fails nothing. It is the assumption written down rather than a tested path,
  and it starts mattering the moment a second path closes the connection, or disposes a command,
  without holding the gate.

  **This orders threads inside one process and nothing else.** The database is WAL and the hooks
  write to it from their own processes; `TreeLock` and the busy timeout are what order those.

- **Never take `TreeLock` to find out whether it is held.** Failing to acquire is a _success_
  for a flush: it concludes another pass is draining the queue and exits, reporting `Ok`
  (`SaveFlushService.cs:168-176`, moved out of `FlushCommand` in 7b-2b so both front ends get
  the same answer). So anything that grabs the lock for an instant just to look at it
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
  floor are all local and all answerable offline. **Only resolving and syncing need the
  network**, and they are the only screens that say so. A screen that cannot tell those two apart
  is wrong: the whole point of defining a set on a handheld away from its server is that it can
  be done.

  **The platform mapping is offline too, and its repair is install-wide.** `platform_map` is
  written by every resolve and every browse, so every row the mapping screen shows, and the
  override that fixes one, are local. A screen that waited on an unreachable LAN host to show
  them would trade the working state for nothing, which is why 7b-3's screen takes no connection
  at all. The agent's `platforms list` refreshes first because it can; the interface does not,
  and that is a decision rather than a gap.

  **A per-set folder override is not the repair for an unmapped platform**, and reaching for it
  is the mistake this screen exists to stop. The mapping is install-wide and an override mends
  one set while leaving every other set and every future set with the same hole.

  **Eviction is offline too and has no screen**, which are two separate facts and 7b-2b settled
  both. `EvictionService` in Core is a preview from two local scans and a walk of `local_file`,
  and carrying it out deletes files and rewrites gamelists from local state, so `rommbat-agent
evict` works with the server off. What 7b-2b removed is the interface to it: RomMBat guessing
  which games matter least is a bad policy even when a person starts it, so freeing space is the
  user's, by dropping a sync set or a single game. Do not go looking for eviction screens.

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

## Browsing and removing, offline

- **Browse degrades, it does not refuse.** With a server it pages `GET /api/roms`; without one
  it lists what the device holds, out of `local_file` joined to `sync_set_member`. That is M2's
  own rule about the offline browsable set being the locally present subset, which is what
  EmulationStation shows anyway. **It says which of the two it is showing, always**, not only
  when it degraded: a person who never sees the online form cannot otherwise tell the offline
  one apart from a library that has shrunk. `BrowseService` decides which; the screen words it.
- **Nothing holds more than one page.** Moving past the bottom fetches the next offset and
  **replaces** what is held. A screen that appended would look identical for the first few pages
  and hold an 83k library by the end, which is why the assertion is a row count across several
  pages rather than a look at one.
- **50 rows a page, measured, not 250.** `RomPager.DefaultPageSize` is for a resolve, which
  wants the fewest requests for a whole scope. Against the live 96,060-rom instance, warm: 50
  rows 280 ms, 250 rows 611 ms. 250 is cheaper per row and more than twice the wait for the page
  a person is looking at, and at `ListWindow.Capacity`'s eight rows it is 31 screens of scrolling
  per fetch.
- **It starts on the platform list, not on the library.** A live instance holds 96,060 games, so
  opening on all of them is 1,922 pages and somebody after one console is shown another's games
  first. Narrowing is the first thing anyone does, so it is the first thing offered.
- **Ask for name order explicitly.** `CatalogQuery` defaults to ascending id because that is what
  makes a resumable walk survive a library changing underneath it. A person scrolling wants
  alphabetical, and id order is the worst kind of wrong here: a library imported in name order
  carries ids in roughly that order, so the list reads as sorted until it is not. Measured, an
  id-ordered snes page put "3 Ninjas Kick Back" before "3-jigen Kakutou Ballz" and then dropped
  the latter out of sequence. Name it rather than leaving `order_by` empty, which the schema
  documents as relevance ordering on MySQL and name ordering elsewhere: an order that depends on
  the server's database is not one a person can learn.
- **A row needs the title and the whole file name, and both were measured.** 750 rows a platform:
  every arcade file name is a romset code with no tags at all (`10yard.zip`) and 87.3% differ
  from the display name, so the title has to be the label; 69 megadrive and 67 psx titles are
  shared by two or more rows, so the file name has to be under it. Showing tags on some platforms
  and the file name on others makes the rule change under a person's feet.
- **A paged list stops at the end; it does not wrap.** Every other list in the UI wraps. Wrapping
  to page one after nine thousand rows of paging silently undoes them and looks exactly like the
  stall a failed fetch produces. Stopping _silently_ is worse again, so there is a row saying so.
  A library that fits one page still wraps: there is no paging to undo.

### Two kinds of list, and drawing one as the other

- **A list of choices** has a cursor, wraps, and draws each row as a panel that fills when
  selected. **A pane of facts has no cursor at all**, scrolls by an offset, clamps at both ends,
  and draws its rows as plain lines. `ListScreen.Reading` is which one a screen is.
- **Dressing the second as the first was reported twice on one pass**, first as a highlight
  walking rows that do nothing and then, with the highlight gone, as the rows still being drawn
  as buttons. Both are the same mistake and the fix is at the class, not the screen.
- **The offset matters rather than being a detail.** A cursor is kept off the edge where there is
  room, which is right for choices and wrong with nothing highlighted: the first presses would
  move something invisible and leave the view still, so the screen reads as ignoring the pad.
- **Pair the row count with the row height.** They were chosen in two files, so a screen could
  compute a window of eight and be drawn at the taller reading height, overflowing by exactly the
  margin the reading capacity exists to avoid. A screen answers "am I reading" once and both
  follow from it.

### A footer offers a verb exactly when that verb works

Both halves are one rule, and three screens got it wrong three different ways in a single
hands-on pass: two answered a press and never offered it, so the footer named nothing but Back
while the verb quietly worked, and one offered it always, including when the preview had just
said nothing would happen. A footer promising an action that does nothing and a footer silent
about one that does are the same defect pointed two ways.

- **A verb that depends on loaded state needs a hint that does too.** `ListScreen.ExtraHints` and
  `OfferAcceptWhen` are functions for the reason `Note` became one.
- **Sweep every action in both directions, not just Accept.** The sweep that existed checked one
  action and one direction, which is why all three shipped.
- **"Did something" means navigated or changed what the screen shows.** A form that answers a
  press by staying put and saying why has plainly done something.

### The claim rule: a game another enabled set still wants is held back

**One method, `EvictionPlanner.Claims`, and both paths call it.** The budget path uses it so
trimming one set cannot take a game another set wants near the top; the removal path uses it so
deleting one set cannot silently take a game a set the user never touched still claims, **only
for the next sync to fetch it again**. Written twice it would have been two rules with one name.

- **The sets being removed from are released**, or their own membership would hold every game
  back against the person removing it. Everything else enabled still counts, and the refusal
  names the set: `still in '<name>'`.
- **A disabled set makes no claim.** That is what "enabled" in the rule means, and it has a test.
- **The order matters.** "Another set still wants this" is reported ahead of a `SaveGuard`
  refusal when both are true, because the second is temporary and the first is the user's own
  other set.

### Removing content

- **The flush runs first.** The commonest `SaveGuard` refusal is a save that has not reached the
  server, and flushing resolves it rather than blocking the removal. **Offline it is skipped and
  said so**, and the unsent save then keeps its game, which is the correct answer.
- **`Plan(bytesToFree)` cannot serve a removal at all.** It returns early when nothing is over
  budget, and its whole ordering answers "which games matter least", which is the question the
  ruling that took eviction off the interface says RomMBat should not answer.
  `PlanRemoval(romIds, releasing)` is the entry point.
- **`local_file` has no save kind.** Its seven are `rom`, `image`, `thumbnail`, `marquee`,
  `video`, `manual` and `firmware`, enforced by a `CHECK`; saves live in `local_save` and
  `local_state`. Anything that removes content walks `local_file`, so it _cannot_ delete a save.
  That is schema-level rather than careful coding, and it belongs in what the confirmation says.

### `local_file` rows outlive their bytes, and the budget counts them forever

Measured on the live install: **5,512 of 5,932 rows pointed at files that were not there,
claiming 18.22 GiB against 1.41 GiB of real content.** An 8 GB cap read as permanently 10 GB
over, so every sync blocked every game with 334 problems and nothing pointing at the cause. It
took a database diff to explain. `ContentPlanner` re-downloads a row whose file is gone, so it
self-heals for a game somebody re-syncs and never for one nobody does.

`InventorySweep` counts it and offers to forget the rows, which is safe by the rollback's own
argument that a row must never outlive its bytes.

**The guard that matters was found by a probe, after the first argument for it turned out to be
wrong.** The claim was that an unplugged drive cannot reach the sweep, since a tree that does not
open has no session. True, and not enough: **a tree carrying `retrobat.ini`,
`system/version.info` and the database but no `roms/` opens perfectly and reports every row
missing.** A copied install, a restored backup and a `roms/` on a second volume all reach it, and
a repair there empties the whole inventory and costs a re-download of the entire library.
`InventoryReport.NothingFound` refuses it, and one surviving file is enough to trust the tree,
because the real state looks nothing like that: 420 rows were still there.

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
