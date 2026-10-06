# Transfers and partials

Part of the [offline-and-portable](SKILL.md) skill. What a canceled or dropped transfer leaves behind, and how `partial/` is kept clean.

## Offline-first

- **A canceled transfer's partial is truncated before its handle is closed, and this is a
  measurement about slow drives rather than about tidiness.** Canceling a download is instant.
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
  bytes and leaves the partial unless the user canceled. **A size-mismatch test cannot catch
  this**, because `ContentSync` deletes the partial itself on a verification failure and the two
  paths then look identical from outside: it needs a transfer the server drops.

  **Bytes are what make a partial worth keeping, and an empty one is discarded either way.**
  `ContentSync` opens the `.part` before it makes the request, so a response that never carries
  a body still leaves an empty file and a download row. Measured on a live install during a
  hands-on pass: one RomM answering **502 for three seconds left 155 empty partials and 155
  download rows**, not one of them resumable, and the person watching reasonably read the pile
  as a fault. Keeping a partial with nothing in it is litter rather than progress.

- Partial downloads survive power loss: write `.part`, verify, rename. **The `.part` lives
  under `emulators/rommbat/partial/`, never beside the target**, so a power loss cannot leave
  a half-written file in a folder EmulationStation scans and offers to launch. Only a
  verified file is renamed into `roms/`.

- **`partial/` needs its own sweep, because neither bound can see it.** The budget counts
  through `local_file` and a partial has no row until commit; the free-space floor reads the
  volume, so the bytes are gone from free space attributed to nothing. `evict` runs
  `PartialSweep` for that. Six producers write here and they die differently: only the ROM
  transfer resumes, so only it is kept, and it is kept on **set membership rather than age**,
  because an interrupted transfer waiting to resume looks exactly like an orphan on disk. The
  other five (`bios-`, `save-`, `resolve-`, `state-`, `unit-`) open with `FileMode.Create` or
  delete in a `finally`, so anything of theirs left behind is from a pass that died. A name none
  of the six writes is left alone, and that means **matching the whole name each producer
  writes** (`bios-<32 hex>.part`, `save-<int>.part`, `resolve-<int>.part`, `state-<int>.part`,
  `unit-<32 hex>` with an optional `.zip`), because a prefix match makes `partial/save-notes.txt`
  a candidate. **A new producer owes `Classify` a branch in the same change**: the sweep leaves
  what it does not recognize alone, so an unlisted name is not reclaimed by anything, ever, and
  is invisible to the budget because a partial has no `local_file` row.

- **`partial/unit-<guid>/` is live state, not litter, so the sweep holds the tree lock.** It is
  where a class C restore extracts a unit before swapping members into a shared container, and
  nothing holds a handle on it: `SaveArchive.Extract` closes each entry's writer inside its own
  loop. Delete it in that window and the restore fails partway through its moves with the
  container half swapped, which is exactly what the `Remove`-before-`Move` ordering exists to
  prevent. **A `FileShare.None` sentinel inside the directory does not fix this**, measured
  rather than assumed: `Directory.Delete(recursive: true)` removes the siblings and only then
  fails on the sentinel, so the staged members are gone either way. `PartialSweep.Apply` takes
  `TreeLock` and returns having done nothing when it cannot get it, and all three routes into a
  restore (`flush`, `saves resolve` and `saves restore --apply`) hold the same lock. Producers that run outside it
  (`sync`, `bios`) rely on `FileShare.None` while writing, where losing the race costs a
  transfer that starts again rather than data. **Reproduced on a real install, not reasoned
  about**: against a staging directory holding three real PPSSPP `SAVEDATA` members, the
  pre-lock build reported "1 abandoned transfer removed" and took all three while a flush held
  the lock; the same scenario on the fixed build left them alone and reclaimed them on the next
  pass.
