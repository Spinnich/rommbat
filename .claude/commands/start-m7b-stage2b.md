---
description: Start M7 stage 7b-2b, the sync run
argument-hint: "[branch name]"
---

# Start M7 stage 7b-2b: the sync run

Fresh session, branched off main. This is the session that writes the branch. What follows it is
`/review-pr` in a second fresh session and `/fix-pr` in a third, so the job is not "working code",
it is a branch that survives a reviewer who will have far less context than you and the repo's own
rules as its standing authority.

7b-2a closed with PR #103, merged at `7e18e30`. The orchestration is in Core, sets have a face, and
a person can define what this device should hold from the couch and resolve it. **They still cannot
sync it.** `LibrarySyncService` and `EvictionService` have never driven a screen, which #103's own
ledger names as the gap in as many words: _"Nobody has seen a sync, which is the next stage's whole
subject."_

Branch off `7e18e30` or later.

---

Variables for this run:

- BRANCH = $1, default `m7b2b-the-sync-run`. Branch off main and stay off it; the pre-push hook
  will stop you anyway.
- NOTES = `~/rommbat-work/<PR>.md`, started once the PR number exists. **`~/rommbat-work/103.md` is
  7b-2a's ledger and you read it in full before you design anything.** It carries the four probes,
  the thirteen hands-on rounds, the review round and the fourteenth round after it. Then `99.md`
  for 7b-1's shell, and `77.md` for M6 if you touch save-adjacent code, which the flush lift does.

## Who is who

I am Spinnich, the only human on this repo. Claude writes the branch, a fresh Claude reviews it, a
third fixes it. You are the first of the three and the only one that will ever hold the reasoning
behind the code.

Anything true only in your head is invisible to the review, and the review is allowed to rule
against it. Put every non-obvious decision somewhere durable: a commit message, a line of
`docs/PLAN.md`, or a test that fails when someone undoes it.

Skip the preamble and the progress narration. Show me decisions, measurements, and diffs.

## The invariant this branch is built around

**A sync leaves every game either wholly present, with its artwork and its gamelist entry, or
wholly absent.** Whether it ran to the end, was stopped, or lost the server.

Nothing in the current design holds that. Artwork is one pass after every ROM of every set, so a
run that stops leaves games with no covers and no gamelist entries, and a stop lands wherever the
cancellation token happened to be checked. Everything below is in service of that sentence, and it
is what the tests assert.

**"A game" is a disc set, and one member is always one file.** `SetResolver` refuses multi-file
ROMs outright (`MemberState.MultiFile`, "which v1 does not sync"), so the only case where two
members belong to one game is a multi-disc title, and `DiscSet.Parse` already groups those by base
title. Nothing new has to learn what a game is.

## Ruled with me before this brief was written

Do not spend the session re-opening these.

1. **The flush lifts into Core**, as `SaveFlushService`.
2. **Artwork is interleaved per game**, fetched straight after that game's ROMs.
3. **Stop removes the game it was in**, immediately, rather than waiting for it to finish.
4. **#104, #105 and #106 ride along.** #107 is decided alongside the sync screen's stop.
5. **#102 is misfiled and is corrected rather than fixed.** See below.

## Why this stage exists

`docs/PLAN.md` §7c gates the whole platform rollout on 7b landing, and re-checked that gate against
the three-PR cut: a certification pass needs a person to say what to sync, sync it, and launch a
game, and 7b-2a supplies only the first of those. **The earliest the gate can open is when this
branch lands.**

The other reason is that a sync is the first minutes-long, destructive-adjacent, network-bound
thing this interface has ever done. Resolving was minutes long and wrote two SQLite rows. This
writes gigabytes into the user's tree.

## What 7b-2b is, concretely

### Half one: the flush seam

`FlushCommand.RunAsync` is 289 lines of orchestration welded to `Console`, and it is the last pass
`LibrarySyncService` cannot run for itself: the service takes the flush as a delegate, and its own
remarks say lifting it belongs with the stage that gives it a face. This is that stage.

New: `src/RomMBat.Core/Sync/SaveFlushService.cs`, composing `SpoolDrain`, `PlaytimeCorrelator`,
`StateScanner`, `SaveScanner`, `OutboxFlush`, `SaveSync` and `StateSync`. **None of them is
touched.** This is 7b-2a's move again and the same rule applies: lift the orchestration that
composes them, not the things composed.

Three rules the extraction has to keep, each already load-bearing:

- **The lock is taken here, and a failed acquire is an outcome rather than an error.**
  `FlushCommand.cs:66-73` treats it as success today and that behaviour does not change. This is
  the pattern `PartialSweep.Apply` already sets, and it is what keeps the UI from ever naming
  `TreeLock`.
- **The local half always runs and only sending needs a link.** Drain, correlate and both scans
  work with the server unreachable.
- **States are scanned before saves** (#64) and sent last, and the reason for each is in
  `FlushCommand`'s own comments. Move the comments with the code.

`FlushCommand` keeps `--quiet`, the conflict block and the exit-code mapping. Every sentence that
names `rommbat-agent saves resolve` stays in the agent, because it would be false on the other
front end. That is 7b-2a's rule and a test already sweeps Core's strings for it.

**Show me the public surface before you write it.** One `RunAsync` returning a report, or an event
stream like `SyncEvent`, and what `FlushState` has to distinguish. 7b-2c and 7b-3 inherit it.

### Half two: artwork per game, and why #102 is wrong

**Read this before you touch the media pass, because `docs/PLAN.md` currently states the opposite
as fact.**

A sync **cannot** overshoot the budget by artwork. `RomMConnection.Media.cs:85` refuses a media
file when its `Content-Length` exceeds the room left, `:105` stops the read when the server
declared no length, and the caller discards the partial in both cases. The parameter's own doc
comment says the check exists "so a disk budget refuses a file rather than overshooting it, which
is the only pre-flight available".

What actually happens is quieter and worse: `ContentPlanner.Plan` fills the cap with ROMs,
`MediaSync` then finds `Room()` at or near zero and blocks every asset with "the budget is full, so
no more artwork was fetched". The games land in EmulationStation with no covers, and no later run
repairs it because nothing frees space by itself.

**No reservation is added.** The size is free at fetch time and unknowable at plan time: RomM
publishes no media size on the rom row, so a reservation would need one HEAD per kind per game and
the M4 medians are the only cheap estimate. Interleaving is the fix instead, and it is
copy-and-paste behaviour rather than a prediction: artwork for game one is fetched before game
forty's ROM, so a budget that runs out truncates the tail of the library instead of stripping the
artwork off all of it. `MediaSync.ApplyAsync` already takes a collection of rom ids, so this is a
change of caller.

**Three consequences, named rather than absorbed:**

- `sync`'s output reorders, so probe 4's byte-identical diff breaks on purpose. Quote the moved
  lines in the PR body the way #103 quoted its four.
- `LibrarySyncService.Order` no longer has Media as one pass after Content, and
  `LibrarySyncOrderTests` currently blesses that. **Rewrite it, do not delete it**, rename it to
  say what it now guards, and put why in its remarks. This repository has twice had a test bless a
  wrong rule, both times written beside the code it blessed.
- `MediaSync` recomputes `managed` from `local_file` per call. Fine at tens of games; say so in a
  comment for whoever syncs thousands.

**#102 is re-titled and re-bodied, not closed quietly.** Its analysis is what a future session
would otherwise trust.

### Half three: stop removes the game it was in

One press, instant. The in-flight transfer is abandoned, and **every file this run wrote for that
game goes with it**, so no half-finished multi-disc title is left behind. My ruling, taken against
a recommendation to keep what had landed, and I want it built as ruled.

**Bounded to this run's own writes, and this is the part the review will press on.** Removing
content is `evict`'s job and happens behind a preview, and that rule is not being weakened: what
goes here is what this very run placed seconds ago, on the game the user just stopped, at their own
press. Three fences make that true and each is owed an assertion:

- Only `FileOrigin.Synced` rows created during this run. Never `Adopted`, which is the user's own
  scrape or their own ROM.
- Never a step that entered the run as `ContentAction.AlreadyPresent`. A game that was on disk
  before the sync started is not this run's to remove.
- The `local_file` row goes with the bytes, which is `ContentSync`'s existing rule that neither
  outlives the other.

**The footer says what the press does**, and it may not name a button. "Stop for now" is the
resolve screen's label and it is honest there because nothing is lost; this press loses something
and the label has to say so.

**Nothing else about a stop changes.** Completed games keep their artwork, the gamelist pass runs
for the folders they are in, and the budget line is reported. A stopped sync ends with a correct
tree rather than with work postponed.

### Half four: the sync screen, and eviction

`SyncViewModel`, `IScreen` + `ILiveScreen` + `IDisposable`, the shape `ResolveViewModel` proves,
with one arm added to `ScreenView`.

- **Reached two ways**, mirroring `sync [set]`: over every set from the sets list, and over one set
  from its detail screen.
- **It shows the pass and the work inside it.** `SyncEvent` is a 14-case union with one consumer
  until now and this is the second: current pass, current game with its transfer fraction from
  `ContentSyncProgress`, a running count, and problems as they arrive rather than only at the end.
- **State is published as one value.** Events arrive on the thread pool. #103's `c735636` fixed
  exactly this on `ListScreen` by making the rows and the cursor one record, and the reasoning is
  in `ListState`'s remarks.
- **The budget is on this screen**, because it is where it is being spent: what the run took, what
  the cap is, and what was blocked. A `Blocked` step already carries "the N budget is full" as its
  reason, so a run that hits the cap says so per game.
- **`/reloadgames` is called exactly as the agent calls it**, through `GamelistSync`. Finding 233
  measured that a reload issued while RomMBat is in front of ES is **deferred, not discarded**, and
  that ES does not rescan on resume by itself. Nothing tells the user to restart the front end, and
  the call is not skipped on the theory that ES will notice.

Eviction, reached from the disk screen and from a sync that blocked games:

- **Preview by default and one confirmation before anything goes.** The preview is not a flag here,
  it is the screen. Say "preview": `dry-run` names `sync`'s flag and nothing else.
- **What is kept, and why, beside what goes.** `EvictionPlan.Refused` carries `SaveGuard`'s
  refusals, and a person freeing space from a sofa has no other way to learn that a game was kept
  because its saves are not up yet.
- **Dead transfers under `partial/` are their own line**, reclaimed even when nothing is over
  budget, and `PartialSweepOutcome.Skipped` is the ordinary outcome it already says it is.
- `EvictionService.Describe` already words both candidate kinds. Quote it rather than rewording.

## Out of scope, and each is somebody's, just not this branch's

- **Browse, search and per-game install.** 7b-2c. Say what the hand-picked `CatalogScopeKind`
  needs; add nothing and add no migration for it.
- **Conflict resolution and acting on the queued-config surface.** 7b-3. The flush lift brings
  conflict _display_ with it because the report carries the rows; acting on them does not follow.
- **`POST /api/activity/heartbeat`.** Still a milestone decision about the `background` pass.
- **#78**, `sets add --scope filter` silently ignoring `--value`. Preserved and asserted. **Do not
  fix it while passing.**
- **#88's platform re-measurement**, **#96**, **#98**, **#101**. Not yours.
- **Adopting a newer upstream.** The floor is RetroBat 8.2.1 and RomM 5.2.0. If either ships a new
  stable while this branch is open, that is its own PR under the version-move checklist.

## Read before you design

1. **CLAUDE.md**, in full. Rule 1 binds every path this branch persists. Rule 4 is live here: the
   flush lift moves code that `background <event>` runs, and nothing you do may make `game-start`
   or `game-end` touch the network.
2. **`docs/PLAN.md`**: M3 and M4 in full, because this stage is those two given a face and the
   media argument you are about to change lives in M4. Then M6 for what a flush is, M7 in full, and
   core principles 1 and 4.
3. **`~/rommbat-work/103.md` in full**, then `99.md`. Round 13 and the review round are the two
   that matter most for a screen you are about to write: the windowing bug that was fixed at the
   class rather than the instance, and the thread-pool publication fix.
4. **`docs/ARCHITECTURE.md`**, the `src/RomMBat.UI` section and sections 2 and 9. All three carry
   claims this branch can falsify.
5. **`docs/retrobat-findings.md`**: 233 on `/reloadgames` behind a running RomMBat, 107 and 203 for
   what it corrected, and 220 to 232 on input.
6. **Skills**: `offline-and-portable` and `save-sync` fully, because the flush lift is save-adjacent
   code; `retrobat-layout` for the gamelist and menu seams; `romm-api` for the media and content
   endpoints and the 401 path; `pre-pr-verification` **early, not at the end**.
7. **The code**: `FlushCommand`, `LibrarySyncService`, `MediaSync`, `ContentPlanner`, `ContentSync`,
   `EvictionService`, `EvictionPlanner`, `PartialSweep`, `SaveGuard`, `DiscSet`, `TreeLock`, and on
   the UI side `ResolveViewModel` as the live-screen pattern, `ListScreen`, `Navigator`,
   `ScreenView`, `BudgetViewModel` and `SetsScreens`.

## The rules that bite in this stage specifically

- **Presentation owns no logic.** Every screen here is over an API you are also writing, which is
  the exact condition that tempts a view model to answer an awkward question itself. The fix is an
  API on Core with a test. 7b-2a said this was the single thing most likely to go wrong and it
  holds double here.
- **The UI may never write `es_settings.cfg`** and may never name `TreeLock`. Both are asserted
  structurally against the built assembly, and the flush lift is exactly the kind of change that
  drags a type into the UI's reference closure by accident.
- **Never persist an absolute path.** The sync screen remembers nothing, but the rollback reads and
  deletes by path and every one of those is relative until the moment it is resolved.
- **Offline is a working state.** An eviction preview, the budget and the whole sets surface work
  with the server off. A sync that loses the server mid-run leaves a correct tree and says so.
- **Windows refuses a file operation two ways and only one is an `IOException`.**
  `UnauthorizedAccessException` does not derive from it. `TreeLock.cs:70-78` is the shape to copy.
  The rollback deletes files and will meet both. #96 is the same mistake still open in product code
  and is not yours to fix.
- **A screen cannot name a face button, in a hint or in prose.**
- **English only, no em-dashes, comments say why and stay short.**

## Design questions to put to me rather than pick

- **`SaveFlushService`'s public surface**, before you write it. A report, or an event stream like
  `SyncEvent`? What `FlushState` must distinguish? 7b-2c and 7b-3 inherit it.
- **Where the rollback lives.** `ContentSync`, which knows what it placed, or `LibrarySyncService`,
  which owns the run. One of those is the type the brief said to compose rather than touch.
- **What the sync screen shows while forty games go by.** A live tail of what is happening, a fixed
  set of fields that update in place, or both. It is the first screen here with more happening than
  fits.
- **#107, decided with the sync screen's stop rather than before it.** Whichever way it goes, the
  resolve screen and the sync screen answer Back the same way, or a user learns two rules.
- **A live 401 mid-sync.** My reading, which you should argue with: the screen shows the expiry and
  offers pairing, and the run stops rather than retrying. Nothing has ever driven a rejection
  through this path.
- **Whether the media interleave belongs in `LibrarySyncService` or in a smaller type between it and
  the two syncs.** Show me the shape before the commit.

## Measure before you commit to a shape

Quote the numbers you take and never one you did not. Probe artifacts go in `probe-output/`, which
is gitignored; if a test needs one, check in the fixture. **Probes that write into the real install
need my say-so first, every time.** `K:` is the live install on 8.2.1, and read `103.md`'s round 14
before assuming the tree is where it left it.

1. **A real sync from the interface, timed and sized.** Tens of games against the live instance:
   wall clock, bytes, and the artwork actually fetched per game read off `Content-Length`. That is a
   second measurement of M4's medians and it is what says whether the interleave changed anything a
   person feels.
2. **A stop mid-game, and what the tree holds after.** The interrupted game gone with nothing of it
   left, the completed games present with artwork and gamelist entries, and ES showing exactly those
   on the way out. **This is the invariant's only real evidence.**
3. **A sync that fills the budget, before and after the interleave**: how many games end up with
   artwork under each. The current answer should be near zero, and that is the case for the change.
4. **Probe 4 re-run** over `flush`, `sync --dry-run`, `sync --offline` and `evict`. Identical but
   for the media reordering, quoted line for line. Normalise the free-space field as #103 did.
5. **Published size and first frame**, against 7b-2a's five files at 96.6 MB and 934 ms on the dev
   box. The flush lift moves code the UI links, so this can only go one way and I want the number.
6. **A live 401**, if it can be produced cheaply. If not, say so.

**Kill orphaned test hosts before believing any timing.** #103 lost real time to three holding
1179s, 1275s and 679s of CPU while it measured a "slow" box.

## Tests the review will look for

`/review-pr` checks for the specific test, not for some test.

- **The agent's existing suite passes over the flush lift, unchanged.** State the count; it is 32.
  If a test had to change, that is a behaviour change and it gets named, not absorbed.
- **`SaveFlushService` is tested without a console**: the lock refusal as a value, the local half
  with the server unreachable, states scanned before saves, states sent last, and a second pass
  after a first being idempotent.
- **The invariant, asserted directly.** A sync stopped mid-game leaves that game wholly absent,
  disc siblings and `local_file` rows included, and leaves every completed game present with its
  artwork and its gamelist entry.
- **The three fences on the rollback**: an adopted file survives, a file that predates the run
  survives, and a row never outlives its bytes.
- **Ordering, rewritten**: BIOS before every ROM, flush before everything, and a game's artwork
  never before its own ROM. Renamed to say what it now guards, with why in its remarks.
- **#104's missing test**, in the shape #103 wrote for cancellation: an unreachable server mid-walk
  keeps the membership that segment found. Check it failing first.
- **The sync screen is reachable and leavable with the gamepad map alone**, at the view-model level
  with no window, driving a whole sync against `StubRomMServer` end to end. #105 is what makes
  create, resolve and sync drivable that way.
- **Nothing a screen shows names a face button.** A sweep over every string a screen produces, not
  a check at one site.
- **An unreachable server leaves every new screen responsive within the 2 s budget**, and the
  eviction preview and the budget work with the server off.
- **The two structural boundaries still hold**, `EsSettingsFile` and `TreeLock`, with #100's
  anti-vacuity companion still passing.
- No absolute path reaches anything persisted, with its row in `LocalStoreTests`' bad-value table if
  a column is added.
- `publish-check` still produces the agent and hook as one file each and the UI as five.

**Check your important assertions failing before you restore them.** 7b-1 did that for six and two
compiled with zero errors. Put the table in the PR body.

## Schema

Probably nothing. The rollback needs to know which rows this run wrote, and `local_file` already
carries `VerifiedAt` and `Origin`; if that is not enough, say what is missing before you add a
column. If you do add one, it is `013` with the same discipline as `003` through `012`: the header
states what the existing shape could not carry and why, one migration for the stage, no column
holds an absolute path, CHECK constraints on anything path- or name-shaped, rebuild rather than
ALTER when adding a CHECK, and copy rows even when you are sure the table is empty.

## Working shape

**Three separable commits at least**: the flush lift, which moves code and changes nothing; the
media interleave, which changes behaviour on purpose; and the screens, which are new. A reviewer
needs to read those three claims apart from each other. Scoped diff, no unrelated cleanups riding
along beyond #104, #105 and #106: every extra file is review surface, and review surface is what the
next two sessions cost.

If part of this turns out to be a design question rather than a coding one, stop and ask me rather
than picking.

## What the next two sessions need from you

- A PR body on `.github/PULL_REQUEST_TEMPLATE.md`, with the AI disclosure stating extent, not just
  the fact, and the measurement table in the M3 through M7 bodies' shape.
- **`docs/PLAN.md`**: 7b-2b rewritten to what was built; **the #102 paragraph corrected**, since it
  states the overshoot as fact; 7b-2c and 7b-3 re-checked against what landed; §7c's gate stated as
  open or not.
- **Issue #102** re-titled and re-bodied, because its analysis is what a future session would trust.
- **`docs/ARCHITECTURE.md`**: the `src/RomMBat.UI` section, which names 7b-2b as pending; section 2
  where the flush lift moved which project owns what; section 9 if the media ordering touches it;
  and the size and first-frame numbers.
- **`README.md`**: the M7 row, which says in as many words that downloading is still a terminal
  command.
- **`DEVELOPER_SETUP.md`**: driving a sync and an eviction from the new screens with no RetroBat in
  front of you.
- **Skills**: `offline-and-portable` takes the lock rule the flush lift settles and which operations
  work offline; `save-sync` takes whatever the lift changes about where the flush passes live;
  `retrobat-layout` takes what the stop probe says about gamelists and `/reloadgames`. A rule that
  exists only because it was measured belongs in a skill, or the next session re-derives it from
  nothing.
- Say which documents you moved and which you read and found already correct.
- **Commit this brief**, as every other milestone brief is in the tree.
- No scratch in the tree.
- NOTES seeded with this session's rulings, carrying forward everything still open from `103.md`,
  `99.md`, `86.md`, `87.md` and `97.md`. That list is long and this stage will mostly not shorten
  it. Say so plainly rather than omitting it. Carry at least: the claims still recorded against RomM
  5.1.1-beta which sit below the floor, #87's two `a3b` blocks never run against the live instance,
  the reconnect-recovery case ruled out of scope, RomMBat's behaviour when its own window is not
  focused, and the roam proven as a mechanism but not attributed to the interface.
- The full `pre-pr-verification` run, plus `reference/verify.py`, with a plain statement of what you
  verified and what you did not. `dotnet build -c Release -warnaserror` is CI's build and an
  incremental green is not evidence: `dotnet clean` first. Plain `dotnet test` is the test command,
  because **`--nologo` silently reports zero tests** on this repo's Microsoft.Testing.Platform
  setup. **The baseline you inherit is 1055 tests green and five CI checks green**, so a red run is
  yours. `trunk` runs through WSL here. Build and test from a fresh clone too.
- **The hands-on pass.** Open RomMBat from the ES menu, define a set, resolve it, sync it, stop it
  mid-game and check what the tree holds, fill the budget, free space, and get back to ES with the
  new games showing. With a controller. Thirteen rounds in 7b-2a and eight in 7b-1 found something
  every single time and the rate never fell off. If the session cannot take the pass, name the
  claims that are unproven for that reason rather than letting the test suite stand in for evidence.

## Default

Read the scope, then show me your reading, the public surface you propose for `SaveFlushService`,
where you would put the rollback, and the measurement plan with the stop probe first in it. That is
the cheapest place for me to correct you.

Commit locally as you go. Ask before pushing, before opening the PR, and before anything that writes
into the real RetroBat install or its configuration.
