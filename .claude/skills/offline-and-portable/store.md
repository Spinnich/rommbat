# The store and the tree lock

Part of the [offline-and-portable](SKILL.md) skill. How threads share the SQLite connection, and how processes share the tree.

## Offline-first

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
