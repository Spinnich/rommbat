---
description: Start M6 stage 2, save states, directory saves, shared containers and Game-ID attribution
argument-hint: "[branch name]"
---

# Start M6 stage 2: states, class C, class D and attribution

Fresh session, branched off main. This is the session that writes the branch. What follows it
is `/review-pr` in a second fresh session and `/fix-pr` in a third, so the job is not "working
code", it is a branch that survives a reviewer who will have far less context than you and the
repo's own rules as its standing authority.

Stage 1 shipped as PR #30 and is on main. It is the half of M6 that could be proved end to end
on class A. This stage is the half that carries the shapes the emulators actually impose, and
it is still the milestone where being wrong loses a user's save rather than costing them a
re-download.

---

Variables for this run:

- BRANCH = $1, default `m6-stage2-states-and-directory-saves`. Branch off main and stay off it;
  the pre-push hook will stop you anyway.
- NOTES = `~/rommbat-work/<PR>.md`, started once the PR number exists. `~/rommbat-work/30.md` is
  stage 1's ledger and it is the single most useful thing you can read before designing.

## Who is who

I am Spinnich, the only human on this repo. Claude writes the branch, a fresh Claude reviews
it, a third fixes it. You are the first of the three and the only one that will ever hold the
reasoning behind the code.

Anything true only in your head is invisible to the review, and the review is allowed to rule
against it. Put every non-obvious decision somewhere durable: a commit message, a line of
`docs/PLAN.md`, or a test that fails when someone undoes it.

Skip the preamble and the progress narration. Show me decisions, measurements, and diffs.

## Ask me this first, before the reading plan

Stage 1's cut line was chosen because it left a provably correct branch. Stage 2's contents are
not one piece either, and I would rather split again than review four hundred lines of new
surface in one PR. Propose the shape and let me pick it before you design anything.

What is on the table, in rough order of how much new machinery each drags in:

1. Save states across the 13 emulators in `es_savestates.cfg`, which is a parser, a slot
   derivation and a best-effort push, and which touches no negotiate protocol at all.
2. Class C bundling, which is the deterministic archive, the scoped save unit and the first
   real caller for `outbox.batch_key`.
3. Game-ID attribution, journal correlation as route 1 and the ROM header over a bounded Range
   as route 2, which classes C and D both need before either can upload anything.
4. Class D conversion and the `es_settings.cfg` per-game writer, which is the only piece here
   that mutates the user's RetroBat configuration.
5. What stage 1 left standing: issue #31's conflict resolution, the flush trigger measurement
   the plan explicitly assigned to this stage, and the two open download cases.

Say what lands in this branch, what it leaves working but unshipped, and what the seam looks
like in the schema. Note that item 3 gates items 2 and 4, and that the plan's "done when"
cannot be claimed without item 5's conflict resolution, since the sentence ends on "a conflict
**the user resolves**".

## Read before you design

1. **CLAUDE.md**, in full. Rule 2, never edit an emulator INI, is this stage's sharpest edge
   because this is the stage that writes `es_settings.cfg`. Rule 4, the hooks never touch the
   network, still binds anything you add to the hook.
2. **`docs/PLAN.md`**, M6 in full, and read the paragraphs headed "Amended after M6 stage 1"
   as the current position rather than the surrounding text. Three of them reverse what the
   original wording said. Then core principle 1 on offline-first, principle 4 on portability,
   M3's `SaveGuard` seam, and "Verification".
3. **`~/rommbat-work/30.md`**, stage 1's ledger, in full. It records the rulings I took, the
   decisions taken without asking and what they were weighed against, three bugs found before
   review, two the offline suite caught, and the list headed "Open going into round 2". Do not
   re-litigate a ruling in there without saying you are doing it and why.
4. **Issue #31**, both halves. Conflicts have nowhere to live, no resolution command, and
   `replaced/` is unpruned; `SpoolRecord.Parse` deletes any record whose version marker it does
   not recognise.
5. **`docs/retrobat-findings.md`**, probe 1 and probe 2 in full. Probe 2's state sections are
   the ones stage 1 never exercised, and every part of it is load-bearing. An empty declared
   state directory means "you are looking in the wrong place", never "this game has no states".
6. **`docs/freegosy-findings.md`**, F17 above all, then F18, F3, F12, F2, F6, F9 and F11. F17
   is the one that decides how far route 2 attribution can reach. F18 is the one that reverses
   an instinct.
7. **Skills**: `save-sync` first, then `retrobat-layout` for `es_savestates.cfg` and the
   `es_settings.cfg` precedence chain, `platform-mapping` for the `(system, emulator)` join,
   `offline-and-portable`, `romm-api` before choosing an endpoint, `pre-pr-verification` before
   claiming anything is done, and `platform-certification`, whose steps 4 and 5 this stage is
   meant to make passable.
8. **The stage 1 code you are building on**, which is all on main:
   `RomMBat.Core.Content.SaveScanner`, `SaveGuard` and its completed three questions,
   `LogicalContentHash`, `RomMBat.Core.RetroBat.SaveShapes` and `LaunchLog`,
   `RomMBat.Core.Sync.SaveSync`, `PlaytimeCorrelator`, `OutboxFlush`, `Spool`, `SpoolDrain`,
   `SpoolRecord`, `TreeLock`, the stores `SaveSlotStore`, `OutboxStore` and `UnsyncableStore`,
   `RomM.Client.Saves`, the `RomMBat.Hook` executable, and the agent's `SavesCommand`,
   `FlushCommand`, `GameEventCommand`, `HooksCommand` and `SyncCommand`. Read migration
   `006-saves.sql`'s header before you write `007`; it says what it declared for this stage and
   what it deliberately left empty.

## Where stage 1 left off

`sync` flushes first, then re-resolves sets, then BIOS, content, media, gamelists, and now
scans saves. `SaveScanner` walks `saves/`, attributes class A and B by filename, hashes their
logical content and records them in `local_save`. `SaveSync` negotiates, uploads, downloads
with `optimistic=false` and acks after verify, and completes the session. Playtime is
correlated from the spool plus `emulatorLauncher.log` and sent standalone. `SaveGuard` answers
its third question, so the M3 eviction seam is closed for the classes stage 1 discovers.

What does not exist at all:

- **No `es_savestates.cfg` parser anywhere in the tree**, no state discovery, no state upload,
  and no call to `POST /api/states` in `RomM.Client`. States are currently recorded as
  unsyncable and nothing more.
- **No archive code.** `LogicalContentHash` exists and is defined for the general case, but
  nothing bundles a directory, and `outbox.batch_key` has a schema, an index and no writer.
- **No attribution beyond the filename.** `game_id_binding` exists, admits `journal`,
  `rom_header` and `user` as `learned_from` values, and is empty. Nothing correlates a save
  directory against the journal's launch window and nothing reads a byte out of a ROM.
- **No `es_settings.cfg` writer**, so no class-D conversion, and all four class-D options plus
  `dolphin_sync_saves` were unset on the measured install.
- **No conflict resolution.** `SaveSync` copies the local file aside into
  `emulators/rommbat/replaced/`, reports the slot unresolved in memory, and `flush` prints it
  once. Nothing persists it, nothing prunes `replaced/`, and the same slot conflicts again on
  every flush.
- **Nothing spawns the agent.** The hook writes a spool file and exits. `sync` and a typed
  `flush` are the whole trigger set. The plan assigned the measurement behind that to this
  stage: the cost of starting an 11 MB process inside the game-launch path has to be measured
  before it is added, not assumed.

Bundled data: `save_shapes.json` still carries 21 systems under `_unclassified` and has no
`ports` entry, `save_rules.json` is stage 1's addition, and `save_directories.json` models both
levels of the tree. Neither level of that tree is positional in either direction, which is why
the shape definition names the paths and anything it does not name is reported as unknown.

## Scope, which is the plan's stage 2 column

- Save states for the 13 emulators `es_savestates.cfg` declares, parsed rather than hardcoded,
  with slot derivation `{emulator}:{core}:{slot}`, the `<image>` sidecar as the optional
  `screenshotFile`, and the emulator, core and version recorded alongside every state.
- Class C bundling to a single archive, with the save unit scoped by the shape definition and
  the hash taken over the logical contents, never the archive bytes.
- Class D conversion, the `es_settings.cfg` per-game override writer, and the decision about
  what to do with saves stranded in an existing shared container.
- Game-ID attribution: journal correlation as route 1, the ROM header over a bounded Range as
  route 2, and the learned binding cached so an odd case is observed once.
- `SaveGuard`'s third question widened to classes C and D.
- `outbox.batch_key` given its writer, so a class B sibling failing is reported as one batch.
- Whatever remains genuinely unsyncable, still reported with a reason, and the `_unclassified`
  list shortened by whatever this stage learns.

**Done when** (the plan's words, and the half stage 1 could not claim): proved on one game from
**each** save shape, not three class-A games. A RetroArch `.srm`, a PPSSPP `SAVEDATA/`
directory, a PCSX2 save state with its screenshot, and a PS2 battery save after opting that
game into a per-game memory card. Then the newer save comes back down as a conflict the user
resolves. Anything still genuinely shared reports itself unsyncable with an explanation rather
than appearing to work.

**Out of scope**: the gamepad UI (M7), packaging (M8). Certification is a separate PR, but say
plainly which of certification's steps 4 and 5 this branch makes runnable and which it does not.

## What the measurements already say

All of this is measured and checked in. Verify each before building on it, then decide what it
means. Several are single lines of code that look obviously right and are wrong.

- **Two declared state directories are lies.** `flycast` declares `{{system}}/flycast/sstates`
  and the emulator writes `saves/dreamcast/reicast/states/`; `openmsx` declares
  `saves/msx1/openmsx/` and writes `bios/openmsx/savestates/`. Both declared directories exist
  and are empty, which is the trap.
- **Four traps inside `es_savestates.cfg` itself.** `libretro`, the most important entry,
  declares no `firstslot` or `lastslot`. `desmume`'s `<image>` and `<file>` are the identical
  template, so uploading `<image>` as `screenshotFile` uploads the state. `bigpemu` has
  `firstslot="001"` and `lastslot="999"` against a two-digit `{{slot2d}}`. `bizhawk` is
  core-scoped like `libretro`, so the same game has independent state sets per core. The
  commented-out `<core name="..." enabled="false"/>` mechanism must be tolerated if a user
  enables it. Twelve of the thirteen have been driven to a real state.
- **PPSSPP writes states twice and RetroBat mirrors them 120 ms later.** The ES-facing
  directory is the authoritative one, the `ppsspp/<rom filename>.txt` sidecar is the ES-name to
  game-ID mapping and is not disposable, and the mirrored screenshot came out correct, zero
  bytes and absent across three saves. A state screenshot is best-effort by nature.
- **Scoping the save unit is the whole class C problem.** `saves/ps3/rpcs3` is 32,451 files,
  52.87 GB and 426 s warm to hash, because that is `dev_hdd0` entire. The save data is
  `dev_hdd0/home/<user>/savedata/`, 17 directories, 77 files, 16.3 MB, 0.06 s. A shape that
  names the emulator's data root is the bug. MAME is the friendly case: `saves/mame/nvram/`
  across 1,231 directories where the short name _is_ the rom basename, so it needs no lookup.
- **Do not convert DuckStation.** The stock `PerGameTitle` binds a disc set and
  `PerGameFileTitle` would split one. The card stem is the shipped `gamedb.yaml` `saveName`
  with the disc marker removed, a third string distinct from both obvious candidates, so PS1
  cards need database-backed attribution. RetroBat writes the serial into the save tree
  unprompted as a `.txt` beside the state. The mapping from a PS1 card to a `rom_id` is
  many-to-many, and 130 of 698 disc-set stems keep a subtitle behind the marker, which is where
  the two readings disagree.
- **PS2 has the same failure and no escape.** `pcsx2_slot1_memory=game` keys on the rom
  basename, so a multi-disc set loses its save at the disc change. The conversion is a per-game
  decision, not a per-system one, which is what `<system>["<rom filename>"]` is for.
- **Flycast's per-game VMU is keyed by disc serial**, `T40217N_vmu_save_A1.bin`, port 1 only,
  in the same directory as the shared files. Converted Dreamcast is Game-ID-keyed, exactly like
  class C.
- **GameCube GCI is per-game but not 1:1**: a region subdirectory, several `.gci` per game, and
  Dolphin soft-deletes with a `.gci.deleted` suffix that must be excluded.
- **`es_settings.cfg` per-game keys must include the ROM's extension**, built from RomM's
  `fs_name`. A bare stem is ignored silently while the emulator launches normally. ES rewrites
  the file only when a setting changed, preserves keys it does not know, and prunes any value
  equal to its own default, so a missing entry is never evidence the user reverted something.
- **F17 bounds route 2.** GameCube is 1,792 of 1,793 `.rvz` and Wii is 148 `.rvz` and 33 `.wad`
  with zero `.iso` across both, so an `.iso`-only reader resolves nothing and would read `RVZ.`
  as a game code. In an `.rvz` the code is at `0x58` and the format version after the
  `RVZ\x01` magic must be checked. A `.wad` has no disc header at all, so 17.5% of that library
  is unreachable by any constant offset. 256 bytes over a bounded Range is enough for the rest.
- **`dolphin_sync_saves` moves save files between two locations behind our back.** Detect it
  before treating either location as authoritative.
- **mtime cannot decide whether a file needs uploading, for any class**, and a launch alone
  writes a battery save. Content hashing is general.
- **Identical uploads dedup within a slot**, which is what makes replay idempotent, so the
  archive must be deterministic or dedup and conflict detection both break.

## Measure before you commit to a shape

Every milestone so far landed a measurement commit that amended `docs/PLAN.md`, and each found
something that changed the code. Probe against the live instance in `DEVELOPER_SETUP.md` and
against the real RetroBat install.

Worth an actual probe, because each changes code:

- **`POST /api/states` for real**, since it has never been called from this repo. What it does
  with `screenshotFile`, whether a replay dedups the way saves do, and what identity a state
  gets without a slot, device or conflict field.
- **The two open download cases** the plan records after stage 1: a slot this device has never
  negotiated, and a device holding no saves at all. Both turn on whether the server returns
  operations for slots the client did not submit, which no branch has driven live.
- **The cost of spawning the agent from the hook**, which the plan assigned to this stage by
  name. Measure it in the game-launch path on the real install, not in a benchmark.
- **`es_settings.cfg` contention with ES running**, merge, atomic write, and the pruning rule.
- **The hash cost of a correctly scoped class C unit** against RPCS3 and MAME, to confirm the
  scoping claim rather than inherit it.
- **A state produced by each emulator you claim to support.** Twelve of thirteen have been
  driven before; know which one has not and say so.

Quote the numbers you took and never one you did not. Where a measurement contradicts the plan,
amend `docs/PLAN.md` in this PR and say which fact moved. Probe artifacts go in
`probe-output/`, which is gitignored; if a test needs one, check in the fixture. Never
hand-edit a vendored file.

**Probes that write into the real install need my say-so first, every time**, and this stage
has more of them than stage 1 did. Flipping an ES option, converting a memory card and
migrating saves out of a shared container are all questions, not steps.

## The rules that bite in this stage specifically

- **Never edit an emulator INI.** `emulatorlauncher` regenerates every one at launch. The
  durable lever is `es_settings.cfg`, and the per-game key carries the extension.
- **Anything that mutates the user's RetroBat configuration is opt-in, explained and
  reversible.** Switching modes strands existing saves inside the old container where the game
  will no longer look for them. Migration is real format work: scope it explicitly or refuse it
  with a clear warning. Some games legitimately read another game's save from a shared card,
  and per-game cards break that by design.
- **Hash the logical contents, never the archive bytes.** Sorted relative paths plus each
  file's own hash, folded into one digest, deterministic across implementations and across runs.
- **Restores must be atomic**, because a half-written directory save is a corrupt one. Extract
  beside the target, verify, swap, keep the previous copy until the next successful sync. Met for
  a single file. **Not met for a class C unit and not reachable by swapping**, since the container
  is shared and only the unit's own members may move. Open as #38.
- **Never persist an absolute path**, including anything parsed out of a launch log or a
  memory card path.
- **Slots stay non-null and stable**, and a state's slot is `{emulator}:{core}:{slot}`.
- **States are outside the negotiate protocol.** Best-effort push, tracked locally, and the UI
  says so.
- **Record emulator, core and version with every state**, because RetroBat's own wiki warns
  states break when an emulator updates. Never restore one produced by a different version
  silently.
- **The hooks never touch the network**, whatever you decide about spawning.
- **Fail closed.** Where you cannot tell whether a save is safe, keep the file and say why.
- **Offline is a working state.** Everything queues, every flush is idempotent under replay and
  partial failure, and the local mtime goes on the wire as `updated_at`.

## Design questions to put to me rather than to pick

- **Whether class D conversion ships here at all**, and if it does, whether migrating saves out
  of an existing shared container is in scope or explicitly refused with a warning.
- **Which attribution routes v1 carries**, given F17 leaves 17.5% of a Wii library unreachable
  by route 2 and route 1 needs a launch to have been observed at all.
- **What a PS1 card binds to**, given the mapping to `rom_id` is many-to-many and the two
  readings of the disc marker disagree on 130 of 698 stems.
- **How far state sync goes in v1**, and what the two emulators with a wrong declared directory
  mean for the claim.
- **Whether issue #31's conflict resolution lands here**, and if it does, whether it is a
  `saves resolve` command, a state column or a table, since M7's UI binds to that seam.
- **Whether the hook spawns the agent**, once you have the measurement.

## Schema

Expect a migration `007`. Same discipline as `003` through `006`: its header states what shape
could not carry the work and why, one migration for the stage, no column holds an absolute
path, CHECK constraints on anything path- or name-shaped, rebuild rather than ALTER when adding
a CHECK, and copy rows even when you are sure the table is empty.

`006` was written for both stages, so start from what it already declared: `local_save` takes a
class C or D unit root in the same columns, `save_slot` holds the server-side identity,
`unsyncable` holds the report, `game_id_binding` is empty and already admits `journal`,
`rom_header` and `user`, and `outbox.batch_key` is waiting for its writer. Say what genuinely
has no home, and say what you deliberately did not add.

## Tests the plan already requires

`/review-pr` checks for the specific test, not for some test.

- Slot derivation across the four `es_savestates.cfg` traps, with the shipped file as the
  fixture, plus the two emulators whose declared directory is wrong.
- The logical content hash is stable across archive implementations and across two runs, and
  the archive round-trips.
- A per-game `es_settings.cfg` override round-trips through an ES-written file, keeps keys ES
  does not know, and is keyed with the extension.
- A `.gci.deleted` file is excluded, and several `.gci` for one game attribute to one ROM.
- An `.rvz` game code is read at `0x58` with the version checked, and a `.wad` is refused rather
  than misread.
- Eviction refuses a ROM with an un-uploaded class C or D save on disk.
- The offline simulation, extended to the shapes this stage adds, still asserting every
  operation completes locally or queues and that the flush is idempotent under replay and
  partial failure.
- A partially-failed batch is reported as one batch, which is what `batch_key` is for.
- The relocation test, now with states and directory saves present, still a clean no-op.
- Every path this stage constructs passes the filesystem-limit and relative-path checks.

## Working shape

Commits that stand alone and explain why, in the style of the M1 through M6 commits. Scoped
diff, no unrelated cleanups riding along: every extra file is review surface, and review
surface is what the next two sessions cost.

If part of this turns out to be a design question rather than a coding one, stop and ask me
rather than picking. On this milestone I would rather be asked twice than told once.

## What the next two sessions need from you

- A PR body on `.github/PULL_REQUEST_TEMPLATE.md`, with the AI disclosure stating extent, not
  just the fact, and the measurement table in the M3 through M6 bodies' shape.
- Every deviation from `docs/PLAN.md` amended in the plan, in this PR, including anything that
  supersedes an "Amended after M6 stage 1" paragraph.
- The stage table in `docs/PLAN.md` updated to say what is now shipped and what, if anything, a
  third stage owns.
- No scratch in the tree.
- NOTES seeded with the rulings this session made, carrying forward anything from
  `~/rommbat-work/30.md` that is still open.
- The full `pre-pr-verification` skill run, plus `reference/verify.py`, with a plain statement
  of what you verified and what you did not. `dotnet build -c Release -warnaserror` is CI's
  build. `trunk` runs through WSL here, it is not on the Windows PATH.
- Stage 1 could not certify a platform because the session was non-interactive and could not
  start EmulationStation. If that is true again, say which claims are unproven for that reason
  rather than letting them read as verified.

## Default

Answer the cut-line question first. Then read the scope and show me your reading plus the
measurement plan before you write code. That is the cheapest place for me to correct you.

Commit locally as you go. Ask before pushing, before opening the PR, and before anything that
writes into the real RetroBat install.
