# Other writers

Part of the [save-sync](SKILL.md) skill. Processes and devices that write the same saves this client does.

## Somebody else may be writing to the same directory

**`dolphin_sync_saves` is the one measured case, and the repository described it wrongly for
four documents.** It is not a background schedule and it is not two emulator folders. It is
GameCube only, it runs once per launch inside `emulatorlauncher` before Dolphin starts, and it
reconciles `saves/gamecube/dolphin-emu/User/GC/<REGION>/` against a **`Card A/` subdirectory of
that same folder**. Newest wins by mtime, the loser is renamed `.old`, and every failure is
swallowed by a bare `catch`.

**The hazard is the one-sided branch, not the mtime comparison.** A save RomMBat restores is
written with the current time, so it always wins; that direction is safe. But a `.gci` sitting in
`Card A` with nothing beside it is copied **back out**, so a save RomMBat removed reappears
holding whatever `Card A` captured at some earlier launch. Driven on hardware: deleting the
region-root file and launching restored the _previous_ session's bytes, and the only trace was
`[INFO] GameCube saves have been synced.` RB-190 and RB-191.

`Card A` is invisible to class C discovery, and that is correct rather than a bug:
`SaveUnitScanner` enumerates one level, so it can neither double-count the copies nor be fooled
by a `.gci.old`. It is also why RomMBat cannot see the resurrection coming, which is why
`DolphinSaveSync` exists.

**Detect and report, never act.** `DolphinSaveSync.Inspect` reads the key at the global and
system levels, then finds any per-game `gamecube["<rom>"]` key by name because it has no ROM
to ask about, and walks the three region folders, and the result becomes an
`UnsyncableReason.ManagedElsewhere` row. Two writers reconciling one directory by different
rules is how saves get lost, so RomMBat does not read `Card A`, does not upload it and does not
delete it. **Report when the option is off too**: turning it off deletes nothing, so the copies
outlive the setting and regain their effect the moment it comes back on.

### The other writer is usually the running emulator

**Nothing may write a save for a game that is in flight, and `Sync/InFlightGuard` is the only
thing that knows.** A download landing while the emulator holds the file is overwritten by the
emulator's own copy on exit, so the other device's save is gone and the write happened under an
open handle. Measured on this install (#155): ES's own launch write lands 1.6 to 4.9 s before the
`start` hook fires and the background pass then takes 5 to 11 s, so a user pressing A promptly
starts the emulator inside the window the download is still in.

**Guard the write, not the launch.** `game-start` and `game-end` stay inert, because they run in
the game-launch path and CLAUDE.md rule 4 is not negotiable; a launch that waits on a round trip
is worse than one that occasionally plays a stale save. So the deferral is the fix, and it is
cheap: nothing is acknowledged, so the next negotiate offers the same save again.

**Every route that writes a save into the tree asks, which is the set the tree lock already
groups.** The flush's download and `StateSync.RestoreAsync`, `saves restore --apply`, and
`saves resolve --keep-server`, whose class C half swaps unit members into a container the running
emulator holds open. A guard on the flush alone leaves the two routes a person reaches by hand
writing into a file being played, which is the same data loss on a slower path.

Five things about it that are decisions rather than detail:

- **Read the journal _and_ the spool.** The journal covers a game launched before the pass, since
  the flush drains and correlates before it downloads and `PlaytimeCorrelator` leaves an unmatched
  `game-start` open on purpose. The spool covers a game launched _during_ the pass, whose `.hook`
  file the drain has already gone past. Either alone misses half the window.
- **A stale `game-start` is bounded by a sequence, not a clock.** A machine that loses power
  mid-game leaves the row open forever, and the block has to lift on its own. The bound is the
  last `start` or `quit` row's `local_sequence`, because ES starting or exiting ends every game
  that was running, and sequence order survives a flat RTC where wall-clock order does not.
- **Per ROM, widened to a shared container.** One game being played must not stall a library
  sync, and a class A save is one file beside its own rom. But a container the shape file
  declares as shared is held by whichever game is running, so a `gamecube` launch defers another
  GameCube game's `.gci` in the same region folder. A file-shaped declaration matches only
  itself: a converted per-game `.ps2` card beside `Mcd001.ps2` is a different file.
- **A launch nobody can name is bounded by EmulationStation, because the spool has no sequence.**
  A record written by a newer hook is left on disk rather than deleted, so a newer agent recovers
  the play session it describes (#31), which also means the same file is read again on every
  pass. Nothing the front end launched outlives the front end, so `EmulationStationProcess` ends
  it: the install defers while ES is up and syncs on the pass the `quit` hook spawns. Without
  that, one unparseable file would stop every save the install ever downloads, permanently, which
  is the failure #31 exists to prevent. The check is injectable for the reason `SaveConverter`'s
  is: nothing is ever running on a build agent, so the branch is otherwise untestable.
- **A `game-end` pops the newest launch, whoever owns it, and that is safe only because of
  EmulationStation.** `game-end` carries no arguments, so `ApplySpool` removes the last entry
  unconditionally, the stack model `PlaytimeCorrelator` uses over the journal. `retrobat-layout`
  records that a **failed** launch fires `game-end` with no `game-start`, and that orphan would
  pop a different game that really is running and let a write land under its open handle. It
  cannot happen today because ES starts no second game while one runs, so an orphan never
  coexists with another launch in flight. **The argument is ES's, not RomMBat's**: a launch
  route that overlays a game on a running one, or an ES build that allows it, retires it
  silently. The fix then is to correlate the pop by rom path through the launch log, the way
  `PlaytimeCorrelator.MatchLaunch` finds a `game-end`'s launch, and drop a `game-end` that matches
  nothing. The correlator pops its own stack the same way and would want the same change (#165).

**Freshness before play is still not solved, and is still open on #155.** A save arriving while ES
sits idle for hours is picked up at the next ES start and not before, and nothing gates a launch
on the check having finished. The guard turns the data-loss ordering into a deferral; it does not
make the launch see a newer save.

**Never convert a multi-disc set, and never convert DuckStation at all.** A two-disc set
driven under stock `PerGameTitle` produced **one card for the set**:
`memcards/Metal Gear Solid (USA)_1.mcd` and `_2.mcd`, where the suffix is the console **slot**
and `_2` is an empty formatted card. The stem is `gamedb.yaml`'s `saveName` with the disc
marker removed, so it carries the region (`(USA)`) but not the disc and not the rom's
`(Rev 1)`. DuckStation binds a disc set through its own database, which is exactly what
`PerGameFileTitle` would throw away by keying on three separate filenames.

**The playlist is not what binds the set; the database is.** Final Fantasy VII, three discs
loose with no `.m3u` and launched as disc 1 alone, produced one `Final Fantasy VII (USA)_1.mcd`
resolving to all three serials. That is the layout a RomM sync creates, so stock is safe on it.

**But the card and the state are keyed differently, in the same session.** The card is per disc
**set**; the save state is `Final Fantasy VII (USA) (Disc 1)_01.sav`, named from the rom file
and therefore per **disc**. A `rom_id` can own one card and three states, so never assume one
save per game or one save per file. The mapping is many-to-many: 130 of the database's 698
disc-set stems keep a subtitle behind the disc marker
(`Biohazard 2 (Japan) (Disc 1) (Leon-hen)`), where set membership is not recoverable from the
card name.

The price of leaving it stock is that PS1 cards need Game-ID attribution rather than filename
attribution. RetroBat pays part of it already: a `.txt` beside the DuckStation save state holds
the bare serial (`SLUS-00594`, `SCUS-94163`).

PS2 has the same failure with no escape, because PCSX2 cannot bind discs at all. So conversion
is **per game**, which is what the `<system>["<rom>"]` form is for: convert single-disc titles,
leave sets alone, and say why.

**Driven end to end in M6 stage 2c, and the details are what make it work.** The card PCSX2
writes is `<rom stem>.ps2`: **the extension is replaced, not appended**, so the name is exactly
the `(folder, stem)` key class A attribution already uses and no new route is needed. Note the
asymmetry with the setting that causes it, because it is the trap: the `es_settings.cfg` key
must carry `.chd` or it is ignored silently, while the card it produces drops it. Both rules
are right and they point opposite ways.

It lands **three levels down**, `saves/ps2/pcsx2/memcards/`, where class A discovery only reads
files loose directly under `saves/<system>/`, so the container is **declared in
`save_shapes.json` and never discovered**. That declaration is also what the download side
needs: a converted card arriving for a device that has never run the game must go into the
container, and the class A rule would put it loose where PCSX2 never looks, quietly.

**Record it as class D, not class A.** One file per game is its shape; what it _is_ is a class D
system whose container was made per-game, and the row is the only place that stays true once
the setting is out of sight. Class D rows are forgettable like class A, keyed on the path: a row
left behind for a deleted card blocks eviction for that ROM forever.

**Discovery must not consult the conversion record.** A card named after a ROM in the declared
container is that ROM's save whether RomMBat set the option or the user did.

**What converting really costs, measured on a real card.** The shared `Mcd001.ps2` held saves
for **11 distinct games**, and after the conversion it was **untouched**: same mtime, same md5.
So the redirect is total and the stranded save really is stranded. Console **slot 2 stays
shared** (`slot1_memory` converts slot 1 only) and `Mcd002.ps2` moved its mtime without changing
a byte, which is one more reason nothing may trust mtime.

Caveats, all user-visible: it mutates their config so it is opt-in and reversible;
switching strands existing saves inside the old container unless migrated; and per-game
cards break games that legitimately read a prequel's save.

**"Auto" in the ES menu means the key is absent**, not that a value is set: `es_features.cfg`
declares three choices for `pcsx2_slot1_memory` and no `auto`, and ES synthesises AUTO for any
unset feature. Two things follow. Reverting has to restore **absence** rather than a plausible
stock value, or the user lands somewhere they never were, which is why the conversion record
stores absent and present-with-a-value as different states. And after a conversion the
system-scoped menu still reads Auto while the per-game key silently outranks it, so **the ES
menu shows no sign that a game has been converted**.

**Never write `es_settings.cfg` while EmulationStation is running.** It loads the file at
startup and serialises that model on every write, so a key that appears afterwards is
discarded, merged and atomic or not. See `retrobat-layout`. Refuse, say why, and re-read after
writing rather than trusting the rename.

**Never use mtime to decide whether a save changed, in any class.** Launching a PS2 game
rewrote both `Mcd001.ps2` and `Mcd002.ps2` with no in-game save at all, and a Dreamcast launch
rewrites the shared VMU the same way, so a mtime check uploads the container after every
session. Hash the content.

**Class A does it too, and no size floor catches it.** A Master System cart booted to its
title screen under libretro `genesis_plus_gx`, with no save key pressed and no progress made,
wrote an 8,188-byte `.srm` whose contents are the cart formatting its own backup RAM. 35
distinct byte values, legible ASCII: a minimum-upload-size check passes it and a blankness
check passes it. `autosave_interval = "10"` means it lands within seconds of boot, so waiting
for a clean exit protects nothing either. **The first save seen for a ROM with no local
baseline is not evidence that anything was played**, so it must not win a conflict on recency
alone.

**Dreamcast converts, but not into class A.** With `flycast_vmupergame=1` the new file is
`vmu/T40217N_vmu_save_A1.bin`, named for the **disc serial**, while the shared
`vmu_save_A1.bin` stops being written and both live in one directory. The rom filename appears
nowhere, so this is Game-ID attribution like class C. Only port 1 converts. PS1 lands in the
same place under its stock memory card mode, so identifier-keyed attribution is the normal case
for disc systems, not an exception two of them make.

**A save state is not always one file, and a save tree is not always portable.** A libretro
state comes with a real `.state1.png` screenshot beside it, which bundling has to keep
together. A multi-disc launch also leaves `saves/<system>/<playlist stem>.ldci`, RetroArch's
record of which disc was in the drive, and its `image_path` is an **absolute path with a drive
letter**. Syncing that verbatim restores a dangling pointer on any install at a different root,
so exclude it or rewrite it on restore.

## Other writers on the same slots

RomM 5.3.0 adds two writers to the saves this protocol was measured against, and a third route
that looks like save transport and is not. RM-3 and RM-4
hold the evidence; these are the rules.

**`PUT /api/saves/{id}` rewrites a row in place, and a save id does not name its bytes.** It keeps
the id, the tagged `file_name` and the slot, changes `content_hash`, moves `updated_at`, and runs
no 409 check, no dedup and no device check. At 5.3.0-alpha.2 RomM's browser player sent it for
whichever save it loaded, at Save & Quit and, under 5.3.0's `emulatorjs.auto_save_sync`, **on every
save tick**. **At alpha.3 it no longer touches the save it loaded** (read, not measured; RM-11):
a session's first write `POST`s a new version with `overwrite=true`
into the loaded save's slot, or the newest slotted save's, which for a game this client syncs is
this client's slot, and later writes `PUT` only that new row. To this client that is a newer row in
its own slot, so `download` or `conflict`, and the table below is the alpha.2 writer.
**Still true at the `5.3.1` floor**, re-read at `beta.1` because the writer was rewritten around
it (RM-12), and untouched from `beta.1` through `5.3.1` (RM-13 and RM-14): `preferredSlot` is byte-identical and still prefers the newest slotted save over
`autosave`, so the release notes' "ordinary play goes to the `autosave` slot" describes a game
with no slotted save and not one this client syncs. What is new is a screenshot on every save
version, which is inert here because only states carry one on this side. So
compare the hash wherever the question is "is this the save I had", which `save_conflict` already
does and must keep doing. Measured on 5.3.0-alpha.2 with `tools/romm-5.3-probes/s1-browser-save-writer.py`,
which replays the browser's own calls:

| Case                                                                          | Negotiate answers                                        |
| ----------------------------------------------------------------------------- | -------------------------------------------------------- |
| A. browser writes over this device's row, local unchanged                     | `download`, same save id, new hash                       |
| B. the same, local also changed                                               | `conflict`; an ordinary upload is 409                    |
| C. after keep-local, browser writes into the older row, this device synced it | `conflict` against the **older** row                     |
| E. the same, but the older row came from a peer this device never synced      | **`download`**, "Server save is newer (no sync history)" |
| D. no save loaded                                                             | one null-slot row, updated in place, never offered       |

**A superseded row does not stay one row down.** Negotiate pairs on the newest `updated_at` per
slot, and a PUT makes the row it touched the newest, so the copy a keep-local rejected comes back
to the head of the slot. Case E is the one that bites: taking the download would put the rejected
branch over the kept side and say nothing. **So a download naming a save id lower than the slot's
recorded one is recorded as a conflict instead** (`SaveSync.SupersededRowReturned`). Ids only grow,
so a lower id at the head of a slot is an in-place write or a deleted head row. Only the write was
measured to reach a download, and the refusal covers both without telling them apart. Case A, the
same id, is an ordinary download.

**Streaming V2 writes saves no slot can see.** Read at tag 5.3.0-alpha.2, not measured, because
the server measured has streaming off. A session's saves land as a **null-slot** row named
`<rom stem> [<emulator> <timestamp>].saves.zip`, one per pull, deduplicated by hash against every
save for the ROM. Negotiate never offers one (#138). `saves restore` would list one as restorable
to `saves/<system>/<stem>.zip`, because a null slot matches no declared unit, and `--apply` then
fails the hash check, since RomM's digest over an archive is not the MD5 of its bytes. It fails
closed, with a misleading preview.

**Streaming prunes states by emulator name, and the name is shared with this client.** Also read,
not measured. Each capture adds a state row, and past `STREAMING_STATE_HISTORY_LIMIT` (default 50)
the oldest are deleted from every state the user holds for that `(rom, emulator)`, not only the
ones streaming wrote. This client uploads PS2, GameCube and Xbox states under `pcsx2`, `dolphin`
and `xemu`, which are the names streaming uses, so a heavily streamed game deletes this device's
oldest server copies. The local file survives and is never re-sent, because "in step" is decided
from the hash this device recorded. `libretro.<core>` does not collide with streaming's `retroarch`.

**The browser writes states as new rows, and only a same-named manual upload rewrites one this
client holds** (#190, read at 5.3.0-alpha.2, RM-4). The player posts
`<rom> [<timestamp>].state` under the EJS core, and `auto_save_sync` does not touch states. The
console view posts `state.save` under `emulatorjs` every time, so the upsert rewrites one row per
ROM, but that name can never equal this client's `<stem> [<emulator>[.<core>]]<ext>`, and restore
reports both shapes unrestorable. A web-UI upload of a file carrying this client's exact name does
replace that row's bytes and clears its emulator. Nothing notices: `RunAsync` never reads the
server row, so the next local change overwrites it. Do not build a state conflict route on the
strength of this; no player write reaches it.

**The browser also writes saves that are not saves: the four bytes `null`.** Measured, not read:
it uploads the JSON literal when it has no save to send, md5 `37a6259cc0c1dae299a7866489dff0bd`,
and the server keeps it in `autosave`, in a slotted row, or in none. Placed as a battery save it
replaces a real one with a file no emulator loads, which this client did on `megadrive` and twice on
`nes` (RB-276). **`SaveSync.DownloadAsync` refuses it**, by the
server's hash before the transfer and by the bytes after, and never acknowledges it, so the server
keeps offering it. Count it as `Rejected`, never as `Failed`: nothing on the device can fix it, so
a failure would exit `Partial` on every flush until someone deletes the row in RomM. The restore
preview lists it as unrestorable with the same reason.

**A guard in `DownloadAsync` alone is too late, because the conflict route writes too.** The
flush's `Download` case turns an offer into a conflict before the download at three points, and
the one that bites is `HeldByAnotherSlot`: `null` in `autosave` resolves to the `.srm` that
`libretro:battery` keeps, and "keep server" then wrote the four bytes over it. So the case asks
the hash ahead of those checks and records no conflict. A 409 and the server's own `conflict`
action still record one, since the local side is real and keeping it is the answer, so
`SaveConflictResolver.KeepServerAsync` refuses too, by the recorded hash and by the bytes. Widen the test only from a measurement:
the hash is the whole rule because no emulator writes a save of exactly those bytes.

**Unless the local side is gone too, and then either answer closes the conflict.** A conflict
against a `null` whose local file was later removed had no way out: keep-local had nothing to send,
keep-server refused the `null`, and keep-local's message sent the user to a delete no command
offers. Measured on the test install with Bare Knuckle III's, reported on every flush after the file
left the tree. Now, when the device holds no save for the conflict and the server's copy is not a
save, keep-local and keep-server both close it with nothing written, and leave the copy taken when it
was recorded where it is, since that may be the only trace of the local side. For class C "holds
no save" means the unit is gone, not the container, which is shared and outlives it. Keep-server
also closes when only the downloaded bytes show the `null`, the recorded hash having been real,
because keep-local cannot know that and sends the user to keep-server.

**Memory card endpoints are not a save transport.** Measured with `s2-memory-card-record.py`: a
card is scoped by `(user, emulator)` with **no ROM**, so it is a class D container by construction;
a version is the whole card, stored byte for byte and not deduplicated; a bare `SRAM.USA.raw` is
refused 400 while a zip holding one is accepted, so the server never checks the layout inside.
Read in source, upstream's Dolphin card is a zip of `.gci` files, the class C shape this client
already syncs per game, and its PCSX2 card is a folder card. Neither gives #82's raw card a home the server
would validate, and interop with a streaming card means rebuilding a whole card, which is two
writers on one container. The direction is the opposite one: steer emulators off shared cards.
PCSX2's `folder` choice is unmeasured here (#80).
