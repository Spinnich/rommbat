---
description: Start M6 stage 2b, Game-ID attribution and class C directory saves
argument-hint: "[branch name]"
---

# Start M6 stage 2b: attribution, and the directory save

Fresh session, branched off main. This is the session that writes the branch. What follows it is
`/review-pr` in a second fresh session and `/fix-pr` in a third, so the job is not "working code",
it is a branch that survives a reviewer who will have far less context than you and the repo's own
rules as its standing authority.

Stage 1 shipped as PR #30, stage 2a as PR #32, both on main. 2a took the piece that needed no
attribution at all. **This stage is the gate**: 2c cannot start until Game-ID attribution exists,
and class C is attribution's first real caller. It is still the milestone where being wrong loses
a user's save rather than costing them a re-download.

---

Variables for this run:

- BRANCH = $1, default `m6-stage2b-attribution-and-class-c`. Branch off main and stay off it; the
  pre-push hook will stop you anyway.
- NOTES = `~/rommbat-work/<PR>.md`, started once the PR number exists. `~/rommbat-work/32.md` is
  2a's ledger and `~/rommbat-work/30.md` is stage 1's. Read both before designing.

## Who is who

I am Spinnich, the only human on this repo. Claude writes the branch, a fresh Claude reviews it, a
third fixes it. You are the first of the three and the only one that will ever hold the reasoning
behind the code.

Anything true only in your head is invisible to the review, and the review is allowed to rule
against it. Put every non-obvious decision somewhere durable: a commit message, a line of
`docs/PLAN.md`, or a test that fails when someone undoes it.

Skip the preamble and the progress narration. Show me decisions, measurements, and diffs.

## Ask me this first, before the reading plan

The three-way cut is already taken and I am not reopening it. What is open is whether **2b itself
is one PR or two**, because it carries two surfaces that meet at exactly one seam:

1. **Game-ID attribution**: journal correlation as route 1, the ROM header over a bounded Range as
   route 2, the learned binding cached in `game_id_binding`, and a decision procedure when the two
   routes disagree. This ships nothing a user can see on its own.
2. **Class C bundling**: the scoped save unit, the deterministic archive, the logical-content hash
   as transport-independent identity, `SaveGuard` widened to C, and `outbox.batch_key`'s writer.

Attribution has no user-visible effect without a caller, and class C cannot upload a PSP or PS3
save without attribution, but **MAME needs no attribution at all** (the nvram short name is the rom
basename) so bundling is provable on its own. Tell me whether that makes a clean two-PR cut, with
MAME proving the archive and PSP proving the routes, or whether splitting here just doubles the
review overhead for one seam.

Say what lands, what it leaves working but unshipped, and what the seam looks like in the schema.
Note that the milestone's "done when" names a **PPSSPP `SAVEDATA/` directory**, so whichever way
this splits, the PR that claims 2b has to carry that shape end to end.

## Read before you design

1. **CLAUDE.md**, in full. Rule 1, never persist an absolute path, bites hardest in the stage that
   parses a save-unit root out of a directory tree. Rule 4 still binds anything you add to the hook.
2. **`docs/PLAN.md`**, M6 in full, and read every paragraph headed "Amended after M6 stage 1" or
   "Amended after M6 stage 2a" as the current position rather than the surrounding text. Then core
   principle 1 on offline-first, principle 4 on portability, M3's `SaveGuard` seam, and
   "Verification". The attribution routes and the RPCS3 scoping measurement are the two passages
   this stage is built on.
3. **`~/rommbat-work/32.md`**, 2a's ledger, in full, then `30.md`. Between them they hold the
   rulings taken, the decisions taken without asking and what they were weighed against, the bugs
   caught before review, the review's seven findings, and two lists headed "Open going into round
   2". Do not re-litigate a ruling in there without saying you are doing it and why.
4. **`docs/retrobat-findings.md`**, the class C sections of probe 1 and probe 2, plus findings 134
   to 139 from 2a's hands-on pass.
5. **`docs/freegosy-findings.md`**, F17 above all, then F18, F3 and F12. F17 is the one that bounds
   route 2, and it bounds it harder than the prose suggests.
6. **Skills**: `save-sync` first, then `platform-mapping` for the `(system, emulator)` join,
   `offline-and-portable`, `retrobat-layout`, `romm-api` before choosing an endpoint,
   `pre-pr-verification` before claiming anything is done, and `platform-certification`.
7. **The code you are building on**, all on main: `SaveScanner`, `SaveGuard` and its now four
   questions, `LogicalContentHash`, `SaveShapes`, `LaunchLog`, `PlaytimeCorrelator` and its
   `MatchLaunch`, `SaveSync`, `SaveConflictResolver`, `StateScanner`, `SaveStateSchema`, `RomIndex`,
   `Spool`, `SpoolDrain`, `OutboxFlush`, the stores `SaveSlotStore`, `OutboxStore`, `LocalStateStore`
   and `SaveConflictStore`, and `RomM.Client.Saves`. Read the headers of `006-saves.sql` and
   `007-states-and-conflicts.sql` before you write `008`; `006` says what it declared for this stage
   and deliberately left empty.

## Where 2a left off

States sync end to end, push-only, scoped `{emulator}:{core}:{slot}` locally and by a scoped
uploaded filename on the server. Conflicts persist in `save_conflict` and `saves resolve` settles
them with no default side. `SaveGuard` answers four questions, the fourth over `local_state`.

What does not exist at all:

- **No attribution beyond the filename.** `game_id_binding` exists, admits `journal`, `rom_header`
  and `user` as `learned_from`, and is empty. Nothing correlates a save directory against a launch
  window and nothing reads a byte out of a ROM.
- **No archive code.** `LogicalContentHash` exists and is defined for the general case, and nothing
  bundles a directory.
- **`outbox.batch_key` has a schema, an index and no writer.** Note the trap: **saves never enter
  the outbox at all.** `SaveSync` reads `local_save` and posts directly, so giving `batch_key` a
  writer is not "fill in a column", it is a question about whether class C, and retroactively class
  B, route through the outbox. Answer that explicitly rather than by implementation.
- **No scoped path in any class C shape.** `save_shapes.json` gives `ps3`, `psp`, `mame`, `gamecube`
  and `wii` a `class` and an `evidence` string and nothing machine-readable. The scoping is the
  whole problem and it currently lives in prose.
- **No `es_settings.cfg` writer and no class D conversion.** That is 2c and stays there.

Bundled data: `save_shapes.json` still carries 21 systems under `_unclassified` and has no `ports`
entry. 2a left both untouched deliberately, because states taught it nothing about battery shapes.
This stage learns things about directory shapes, so shorten the list by what you actually prove and
leave the rest alone rather than quietly editing it.

## Scope, which is the plan's stage 2b column

- Game-ID attribution: journal correlation as route 1, the ROM header over a bounded Range as route
  2, the learned binding cached so an odd case is observed once, and a stated rule for disagreement.
- Class C bundling to a single archive, with the save unit **scoped by the shape definition** and
  the hash taken over the logical contents, never the archive bytes.
- `SaveGuard`'s question widened to class C.
- `outbox.batch_key` given its writer, so a class B sibling failing is reported as one batch.
- The `_unclassified` list shortened by whatever this stage proves, and the `ports` gap closed if
  what you learn closes it.
- Whatever remains genuinely unsyncable, still reported with a reason.

**Done when**: a PPSSPP `SAVEDATA/` directory goes up and comes back down as a conflict the user
resolves, on a real install. That is the third of the four shapes the milestone's "done when"
names. The converted PS2 memory card is 2c, and **M6 is not claimable as done until 2c lands**.

**Out of scope**: the gamepad UI (M7), packaging (M8), class D and the `es_settings.cfg` writer
(2c). Certification is a separate PR; say plainly which of certification's steps this branch makes
runnable and which it does not.

## What the measurements already say

All of this is measured and checked in. Verify each before building on it, then decide what it
means. Several are single lines of code that look obviously right and are wrong.

- **Scoping the save unit is the whole class C problem.** `saves/ps3/rpcs3` is 32,451 files, 52.87
  GB and 426 s warm to hash, because that is `dev_hdd0` entire, installed games and firmware and
  caches included. The save data is `dev_hdd0/home/<user>/savedata/<TITLEID>/`: 17 directories, 77
  files, 16.3 MB, 0.06 s. **A shape that names the emulator's data root is the bug**, and it costs
  seven minutes per sync.
- **MAME is the friendly case.** `saves/mame/nvram/<shortname>/` across 1,231 directories where the
  short name is the rom basename, so it needs no lookup at all. The whole tree is 1,531 files, 8.0 s.
- **GameCube GCI is per-game but not 1:1**: a region subdirectory, several `.gci` per game named by
  game code, and Dolphin soft-deletes with a `.gci.deleted` suffix that must be excluded.
- **`dolphin_sync_saves` moves save files between two locations behind our back.** Detect it before
  treating either location as authoritative.
- **Wii's NAND tree mixes per-game saves with system state.** It sits under `Wii/title/` and it is
  not all attributable. Deciding what of it is a save unit is a design question, not a path join.
- **F17 bounds route 2 hard.** GameCube is 1,792 of 1,793 `.rvz` and Wii is 148 `.rvz` and 33 `.wad`
  with zero `.iso` across both, so an `.iso`-only reader resolves nothing and would read the literal
  bytes `RVZ.` as a game code. In an `.rvz` the code is at `0x58` and the format version after the
  `RVZ\x01` magic must be checked, since a later revision moving that field moves the offset. A
  `.wad` has no disc header at all and its title ID sits behind a variable-length certificate chain,
  so **17.5% of that Wii library is unreachable by any constant offset**. 256 bytes over a bounded
  Range is enough for the rest, and no image need be downloaded.
- **Route 1 needs a launch to have been observed**, so it resolves nothing for a save that predates
  RomMBat. Route 2 is the only thing that reaches those, and it reaches them unevenly.
- **`MatchLaunch`'s 24-hour window stops being cosmetic here.** It has been carried forward
  unresolved through two ledgers because playtime was its only caller. Route 1 makes it load-bearing
  for attribution, and `SuspiciouslyLong` is still unused. Settle both or say why not.
- **Identical uploads dedup within a slot**, which is what makes replay idempotent, so the archive
  must be deterministic or dedup and conflict detection both break. .NET's `ZipArchive` and Go's
  `archive/zip` differ on entry ordering, timestamps and compression level, which is exactly why the
  hash is defined over the logical contents and the archive is transport only.
- **The server renames a save and does not rename a state.** Measurement 130: a save came back as
  `Probe Save [2026-08-17_12-27-44].srm`. Whatever a bundled directory is uploaded as, the name that
  comes back is not the name you sent.
- **Negotiate never volunteers a slot the client did not submit** (measurement 132), so nothing about
  class C changes the fact that a fresh device cannot discover what the server holds for it.
- **mtime cannot decide whether a file needs uploading, for any class**, and a launch alone writes a
  battery save. Content hashing is general.

## Measure before you commit to a shape

Every milestone so far landed a measurement commit that amended `docs/PLAN.md`, and each found
something that changed the code. Probe against the live instance in `DEVELOPER_SETUP.md` and against
the real RetroBat install.

Worth an actual probe, because each changes code:

- **Uploading a directory save for real.** What `POST /api/saves` does with an archive: the name that
  comes back, whether the `emulator` field is in the upsert key the way it was not for states
  (measurement 127), whether a byte-identical replay dedups, and what the download side gives back.
- **The hash cost of a correctly scoped class C unit** against RPCS3 and MAME, to confirm the scoping
  claim rather than inherit it.
- **The `.rvz` header on two real images**, code at `0x58` with the version checked, and a `.wad`
  refused rather than misread.
- **A PPSSPP `SAVEDATA/` directory on the real install**: where it actually sits, what a `PARAM.SFO`
  gives you, and whether the game ID it names resolves to a `rom_id` by either route.
- **Journal correlation against real spool data**, including the orphan `game-end` case and two
  launches inside one window.
- **The cost of spawning the agent from the hook**, deferred by both previous stages with the reason
  recorded. It needs the hook binary on a real install and a game launched to time it. If it is
  deferred a third time, say so plainly rather than letting it disappear.

Quote the numbers you took and never one you did not. Where a measurement contradicts the plan,
amend `docs/PLAN.md` in this PR and say which fact moved. Probe artifacts go in `probe-output/`,
which is gitignored; if a test needs one, check in the fixture. Never hand-edit a vendored file.

**Probes that write into the real install need my say-so first, every time.**

## The rules that bite in this stage specifically

- **Hash the logical contents, never the archive bytes.** Sorted relative paths plus each file's own
  hash, folded into one digest, deterministic across implementations and across runs.
- **Restores are atomic.** Extract beside the target, verify, swap, keep the previous copy until the
  next successful sync. A half-written directory save is a corrupt one.
- **Never persist an absolute path**, including a save-unit root discovered by walking a tree.
- **Fail closed.** Where attribution is uncertain, keep the file, report it, and do not guess a
  `rom_id`. A wrong binding uploads one game's save under another game's name and the cache makes the
  mistake permanent, so say how a learned binding can be unlearned.
- **Slots stay non-null and stable.**
- **Offline is a working state.** Everything queues, every flush is idempotent under replay and
  partial failure, and the local mtime goes on the wire as `updated_at`.
- **The hooks never touch the network.**
- **Never edit an emulator INI.** Nothing in this stage should need to; if you find yourself wanting
  to, that is 2c's problem and it is solved with `es_settings.cfg`.

## Design questions to put to me rather than to pick

- **Whether 2b is one PR or two**, per the top of this file.
- **Which attribution routes v1 carries**, given route 2 leaves 17.5% of a Wii library unreachable
  and route 1 needs a launch to have been observed at all.
- **What happens when the two routes disagree**, and whether `user` as a `learned_from` value means a
  command exists in this stage or is reserved for M7's UI.
- **Where the scoped save unit is declared**: a path grammar inside `save_shapes.json`, a new key in
  `save_directories.json`, or code. Whichever it is, an unnamed path must report as unknown rather
  than defaulting to the emulator root.
- **Whether class C routes through the outbox, and whether class B is retrofitted onto it**, which is
  what `batch_key` having a writer actually asks.
- **How much of Wii's NAND tree counts as a save unit**, or whether Wii stays reported-unsyncable
  with a reason until someone can drive it.
- **Whether the fresh-device inventory pass** over `/api/saves` lands here. It is a real gap, it is
  written into the plan, and it is outside M6's "done when".

## Schema

Expect a migration `008`. Same discipline as `003` through `007`: its header states what the existing
shape could not carry and why, one migration for the stage, no column holds an absolute path, CHECK
constraints on anything path- or name-shaped, rebuild rather than ALTER when adding a CHECK, and copy
rows even when you are sure the table is empty.

Start from what `006` already declared for this stage: `local_save` takes a class C or D unit root in
the same columns, `game_id_binding` is empty and already admits all three `learned_from` values, and
`outbox.batch_key` is waiting for its writer. It is entirely possible this stage needs no new table
at all. If so, say that, and say what you deliberately did not add.

## Tests the plan already requires

`/review-pr` checks for the specific test, not for some test.

- The logical content hash is stable across two runs and across archive implementations, and the
  archive round-trips.
- A shape naming an emulator data root is refused or reported, not hashed. The RPCS3 case is the
  fixture.
- A `.gci.deleted` file is excluded, and several `.gci` for one game attribute to one ROM.
- An `.rvz` game code is read at `0x58` with the version checked, and a `.wad` is refused rather than
  misread.
- Journal correlation attributes a save unit to the launch that covers it, discards an orphan
  `game-end`, and refuses rather than guesses when two launches cover one window.
- A learned binding is reused without re-reading the ROM, and a wrong one can be corrected.
- Eviction refuses a ROM with an un-uploaded class C save on disk.
- A partially-failed batch is reported as one batch, which is what `batch_key` is for.
- The offline simulation, extended to class C, still asserting every operation completes locally or
  queues and that the flush is idempotent under replay and partial failure.
- The relocation test, now with directory saves present, still a clean no-op.
- Every path this stage constructs passes the filesystem-limit and relative-path checks.

## Working shape

Commits that stand alone and explain why, in the style of the M1 through M6 commits. Scoped diff, no
unrelated cleanups riding along: every extra file is review surface, and review surface is what the
next two sessions cost.

If part of this turns out to be a design question rather than a coding one, stop and ask me rather
than picking. On this milestone I would rather be asked twice than told once.

## What the next two sessions need from you

- A PR body on `.github/PULL_REQUEST_TEMPLATE.md`, with the AI disclosure stating extent, not just
  the fact, and the measurement table in the M3 through M6 bodies' shape.
- Every deviation from `docs/PLAN.md` amended in the plan, in this PR, including anything that
  supersedes an "Amended after M6 stage 1" or "stage 2a" paragraph.
- The stage table in `docs/PLAN.md` updated to say what is now shipped and what 2c still owns.
- The other four documents brought with it, per the `pre-pr-verification` trigger table: the
  `README.md` stage table and the claims about what syncs, `docs/ARCHITECTURE.md` for the store
  and the save model, `DEVELOPER_SETUP.md` for anything a developer now types, and the
  `save-sync` skill for every rule the measurements produced. Say which you moved and which you
  read and found already correct.
- No scratch in the tree.
- NOTES seeded with the rulings this session made, carrying forward anything still open from
  `~/rommbat-work/30.md` and `~/rommbat-work/32.md`, including the items that have now survived two
  ledgers untouched.
- The full `pre-pr-verification` skill run, plus `reference/verify.py`, with a plain statement of what
  you verified and what you did not. `dotnet build -c Release -warnaserror` is CI's build. `trunk`
  runs through WSL here, it is not on the Windows PATH. Build and test from a fresh clone too; that
  is the check that catches a `.gitignore`-swallowed fixture locally instead of in CI.
- One hands-on pass on the save shape this stage adds, per `docs/platforms/README.md`. If the session
  cannot take one, name the claims that are unproven for that reason rather than letting them read as
  verified.

## Default

Answer the split question first. Then read the scope and show me your reading plus the measurement
plan before you write code. That is the cheapest place for me to correct you.

Commit locally as you go. Ask before pushing, before opening the PR, and before anything that writes
into the real RetroBat install.
