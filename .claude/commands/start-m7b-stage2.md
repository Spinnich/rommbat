---
description: Start M7 stage 7b-2, the Core seam and the sets screens
argument-hint: "[branch name]"
---

# Start M7 stage 7b-2: the seam, and sets

Fresh session, branched off main. This is the session that writes the branch. What follows it is
`/review-pr` in a second fresh session and `/fix-pr` in a third, so the job is not "working code",
it is a branch that survives a reviewer who will have far less context than you and the repo's own
rules as its standing authority.

7b-1 closed with PR #99, merged. `RomMBat.exe` is a real full-screen gamepad app now: it pairs
behind an on-screen keyboard and shows status, and a person has driven it from the EmulationStation
menu with the keyboard and mouse unplugged. **It shipped with zero new logic in Core**, because
`StatusCommand` already read every field the status screen shows and `PairCommand` was a
presentation shell over `PairingService`.

**This stage is where that bill comes due, and the first PR is mostly not a UI PR.** Everything
7b-2 has to put on screen exists only inside the agent's subcommands, welded to `Console`.

Branch off `3f2be95` or later. Nothing has landed on main since #99 except a dependabot bump.

---

Variables for this run:

- BRANCH = $1, default `m7b2-sets-and-seam`. Branch off main and stay off it; the pre-push hook
  will stop you anyway.
- NOTES = `~/rommbat-work/<PR>.md`, started once the PR number exists. **`~/rommbat-work/99.md` is
  7b-1's ledger and you read it in full, past the eight rounds, before you design anything.** It is
  859 lines and it is the densest document in this repo about how this UI actually behaves. Then
  `86.md` for 7a, and `77.md` for M6 if you touch save-adjacent code.

## Who is who

I am Spinnich, the only human on this repo. Claude writes the branch, a fresh Claude reviews it, a
third fixes it. You are the first of the three and the only one that will ever hold the reasoning
behind the code.

Anything true only in your head is invisible to the review, and the review is allowed to rule
against it. Put every non-obvious decision somewhere durable: a commit message, a line of
`docs/PLAN.md`, or a test that fails when someone undoes it.

Skip the preamble and the progress narration. Show me decisions, measurements, and diffs.

## The cut is already made, and this session ships the first PR

**Ruled with me before this brief was written, so do not spend the session re-opening it.** 7b-2 as
`docs/PLAN.md` describes it is M2, M3 and `EvictionPlanner` given a face at once, and 7b-1's ledger
owed a verdict on splitting it. Three PRs:

- **7b-2a, the seam and sets.** This session. The orchestration moves into Core, and sets get a
  face. No downloads, no eviction UI, no browse.
- **7b-2b, the sync run.** Progress, cancellation, eviction, and whatever probe 1 below says about
  `/reloadgames`.
- **7b-2c, browse.** Online paged browse with search, offline browse of the local subset, per-game
  install and evict. Issue #88 is fixed there.

**Design all three, build the first.** That is the same instruction 7b-1 got and the reason its
shell was not shaped wrong for what came after. If your design work says the cut is wrong, say so
in NOTES with an argument and build 7b-2a anyway.

## Why this stage exists

`docs/PLAN.md` gates the entire platform rollout on 7b landing, because certification is one person
launching games per `(system, emulator, core)` and the gamepad UI is what they do it with. That is
still true and it is not the interesting reason.

The interesting reason is that **the loop a user actually cares about is not reachable from the
couch yet**. Six milestones built "define what this device syncs, then sync it", and every step of
it is a terminal command. A person who opens RomMBat from the ES menu today can pair, and can look
at what a sync did. They cannot say what to sync.

## What 7b-2a is, concretely

### Half one: the seam. This is the larger half and the one the review will judge hardest

Move sets, sync and eviction orchestration out of `RomMBat.Agent` into `RomMBat.Core`, as
console-free services that return values with the words already chosen, report through
`IProgress<T>`, and take a `CancellationToken`. The agent's subcommands become printers over them.

**This is the same move 7b-1 made when `AgentContext` became `InstallSession`, and it was ruled for
the same reason**: the alternative is two implementations of what a sync does, with nothing keeping
them agreeing. Read `InstallSession`'s class comment; it states the pattern you are extending, down
to "it decides, and it does not report".

What is entangled, measured rather than guessed:

| File                         | What is buried in it                                                                                                                                                                                                                                                                           |
| ---------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `SetsCommand.cs`, 505 lines  | scope parsing, the granted-scope refusal for collection scopes, `fs_slug` to platform-id resolution, filter JSON assembly, folder validation against `es_systems.cfg`, `ResolveSetsAsync` with its walk cursor and resume, `Report`'s completed-versus-partial rule, `PushConfigAsync` roaming |
| `SyncCommand.cs`, 525 lines  | six ordered passes with the ordering argument in the doc comment, hook install, menu install, flush, filesystem limits, BIOS ahead of ROMs, plan, `ContentSync`, media, gamelists, budget report                                                                                               |
| `EvictCommand.cs`, 214 lines | state scan then save scan in that order and #64's reason for it, `EvictionPlanner`, `PartialSweep`, the preview-versus-`--apply` split, gamelist rewrite                                                                                                                                       |

Note what is _not_ entangled and must not be rewritten: `SetResolver`, `ContentPlanner`,
`ContentSync` (which already takes `IProgress<ContentSyncProgress>` and a token),
`BiosPlanner`/`BiosSync`, `MediaSync`, `GamelistSync`, `EvictionPlanner`, `PartialSweep`,
`SaveGuard` and `FilesystemLimits` are all already public in Core and already free of `Console`.
**The job is to lift the orchestration that composes them, not to touch them.**

**The strongest evidence this refactor is correct is the agent's existing suite passing unchanged.**
Say so in the PR body with the number. If a test had to change, that is a behaviour change and it
gets named, not absorbed.

### Half two: sets on screen

- List the sets, each with what it resolves to and when it last did.
- Create one: pick a scope, pick its value, set the caps and the ordering, optionally a folder
  override. **The scope picker is the interesting screen**, because `platform` wants the list this
  install already knows and `collection` needs a granted scope this pairing may not have.
- Edit the caps and ordering of an existing set. Delete one, which touches nothing on disk and
  should say so, as `sets remove` already does.
- Resolve a set, and show the outcome including the exclusion groups `sets show` prints. Resolve
  needs the network and can be slow, so it is the first place in the UI where an operation that is
  neither instant nor minutes-long has to have a shape.
- The disk budget and the free-space floor. Two `SettingStore` writes, and a precondition for
  7b-2b rather than an afterthought.

### Out of scope, and each is somebody's, just not this branch's

- **Downloading anything.** No `sync` from the UI, no per-game install. 7b-2b and 7b-2c.
- **Browse, search, and the on-screen keyboard's third layer.** 7b-2c.
- **Conflict resolution and acting on the queued-config surface.** 7b-3.
- **`POST /api/activity/heartbeat`.** Still a milestone decision about the `background` pass and
  still not a UI feature. Say it in NOTES if a screen wants it and stop there.
- **Issue #78**, `sets add --scope filter` silently ignoring `--value`. It is a defect on the exact
  surface you are building a face for, and it stays filed: the sets screen builds a filter from
  fields rather than from a `--value`, so it does not trigger it. **Do not fix it while passing.**
  If you cannot build the scope picker honestly without touching it, that is worth stopping over.
- **Issue #88**, the slow scoped catalog walk. Ruled into 7b-2c, where browse makes a user feel it.
- **Adopting a newer upstream.** The floor is RetroBat 8.2.1 and RomM 5.2.0. If either ships a new
  stable while this branch is open, that is its own PR under the version-move checklist.

## Read before you design

1. **CLAUDE.md**, in full. Rule 1 binds every path a set definition remembers. Rule 3 is live here:
   `SetsCommand` validates a folder override against the live `es_systems.cfg` and your picker must
   too.
2. **`docs/PLAN.md`**: M2 and M3 in full, because this stage is those two milestones given a face
   and their reasoning is the reasoning of your screens. Then M7 in full, the `RomMBat.UI`
   paragraph under "Projects", and core principles 1 and 4.
3. **`~/rommbat-work/99.md`, in full.** Especially rounds 5, 7 and 8. Two of them exist because I
   asked "hasn't RetroBat already solved this?" and both times the answer was yes and RomMBat had
   been contradicting it. **That question is worth asking first in this stage**: ES has a games
   list, a filter dialog and a scroll model already built, and you are about to write a list screen
   and a picker.
4. **`docs/ARCHITECTURE.md`**, the `src/RomMBat.UI` section and section 2. Both carry claims this
   branch can falsify.
5. **`docs/retrobat-findings.md`**: 107 and 208 on what a 200 from the ES API is worth, 203 on the
   menu entry, 220 to 232 on input and the real hands-on pass.
6. **Skills**: `retrobat-layout` and `offline-and-portable` fully, `romm-api` for the catalog
   endpoints and the 401 path, `platform-mapping` because the scope picker resolves platforms, and
   `pre-pr-verification` before claiming anything is done. **Load `pre-pr-verification` early**,
   not at the end: 7b-1 lost time inventing an explanation for a broken `dotnet test` that the
   skill names in as many words.
7. **The code**: the three commands above, `InstallSession`, `SyncSetStore`, `SettingStore`,
   `PlatformMapStore`, `SetResolver`, `ContentPlanner`, `EvictionPlanner`, `TreeLock`, and on the
   UI side `IScreen`, `Navigator`, `NavRepeat`, `ScreenView`, `ShellWindow`, `StatusViewModel` as
   the read-only pattern and `PairingViewModel` as the `ILiveScreen`-plus-cancellable-work pattern.

## The rules that bite in this stage specifically

- **Presentation owns no logic, and this is the stage where that stops being free.** 7b-1 could
  claim it by construction. Every screen you write is over an API you are also writing, which means
  the temptation is to answer a screen's awkward question in the view model. The fix is an API on
  Core with a test. This is the single thing most likely to go wrong.
- **The UI still may never write `es_settings.cfg`**, and `EsSettingsBoundaryTests` asserts it
  structurally against the built assembly. Nothing in 7b-2a wants to, but the seam you are building
  is exactly the kind of thing that drags a type into the UI's reference closure by accident.
- **`TreeLock`: the UI writes now, and the structural assertion must survive anyway.** 7b-1 asserts
  that the UI assembly never names `TreeLock`, and the reason is data loss rather than tidiness:
  `FlushCommand.cs:66-73` treats a failed acquire as _success_ and exits having done nothing, so a
  UI holding the lock for an instant to look at it makes a concurrent `background quit` flush skip
  its upload and report success. **My reading, which you should argue with: the Core service takes
  the lock for the duration of its own write and returns "another RomMBat pass is running" as a
  value, and the UI never names the type.** That keeps the assertion true rather than deleting it,
  and it puts the decision where the rest of the decisions are. Issue #100 is the missing
  anti-vacuity companion for that same test, and closing it here is cheap and in scope.
- **`sync` does not currently take the lock at all**, only `flush`, `saves resolve` and
  `PartialSweep` do. Do not "fix" that in passing. Work out what the seam needs and say what you
  concluded.
- **Never persist an absolute path**, including a folder override and anything a picker remembers.
- **Offline is a working state.** Listing sets, editing caps and setting the budget all work with
  the server switched off; only resolve needs it. A screen that cannot tell those apart is wrong.
- **Windows refuses a file operation two ways and only one is an `IOException`.** `IOException` for
  a sharing violation, `UnauthorizedAccessException` for access denied, and the second does not
  derive from the first. `TreeLock.cs:70-78` is the shape to copy. #96 is the same mistake still
  open in product code and is not yours.
- **A screen cannot name a face button, and it cannot name one in prose either.** `FooterHint`
  carries a `NavAction` by construction, and round 8's first blocking finding was "Press A" in a
  status row, which on a Switch Pro is `Back` and closes RomMBat. Quote the footer's own label.
- **`select` is unbound**, deliberately, and 7b-1 ruled that this stage is where a secondary button
  would earn its place. A list screen with a per-row action is exactly that case. Decide it out
  loud rather than by adding a binding.
- **English only, no em-dashes, comments say why and stay short.**

## Design questions to put to me rather than pick

- **What the Core services are called and where their boundaries fall.** One service per verb, or
  one `SyncSetService` that covers define-resolve-sync-evict? Show me the public surface before you
  write it. This is the decision 7b-2b and 7b-2c inherit and cannot cheaply change.
- **How a Core service reports.** 7b-1 shipped `InstallSession` returning records with pre-written
  sentences, and its own ledger files that as arguable because it puts user-facing English in Core.
  You are about to do a great deal more of it. Argue it either way and pick, but pick knowingly.
- **`ScreenView` is a static switch over screen types**, and 7b-1's ledger names it as the file that
  will grow worst in this stage. This is the PR that finds out. Views attached to screens, or the
  switch kept? Recommend one.
- **What a resolve looks like while it runs.** It is neither instant nor minutes-long, it can hit an
  unreachable server, and it can resume a partial walk. Does it get `ILiveScreen`, a blocking
  overlay, or something else?
- **The scope picker when the pairing lacks `collections.read`.** `SetsCommand` refuses at
  definition time with a sentence naming the missing scope. A picker can instead not offer the
  option at all. One of those teaches the user something and the other is tidier.
- **Per-game install, which is 7b-2c's but whose model is decided by the schema you touch now.**
  `CatalogScopeKind` has five members and none of them means "these ones, by hand", and
  `SyncSetStore.ReplaceMembers` is driven by a resolve walk. Meanwhile `EvictionPlanner` has
  `EvictionReason.Orphaned`, "in no set", so a one-off download outside every set is an eviction
  candidate on the next pass. Three shapes I can see: a hand-picked scope kind with a migration, an
  id list carried inside a `Filter` scope, or an unmanaged download with eviction taught to leave it
  alone. **Do not build it. Tell me which one the seam should leave room for.**
- **Whether any of this earns a new skill**, or whether `retrobat-layout` and `offline-and-portable`
  absorb it.

## Measure before you commit to a shape

**The first one can move 7b-2b's whole design and costs one probe. Do it first, even though this
PR does not install anything.**

- **Does `GET /reloadgames` do anything while RomMBat is the app in front of EmulationStation?**
  Finding 107 says it is _ignored while a game is running_: 200 in 1 ms, a ROM added to the folder
  still unreported five seconds later, reproduced twice. `99.md`'s probe P2 proved RomMBat is
  launched through `emulatorLauncher` and **suspended exactly as a game is**, which is why ES fires
  zero navigation events behind it. Finding 203, which is where "the reload works" comes from, was
  measured with **no app in front of ES**. So the reload the UI wants to issue after installing
  games may be structurally impossible until RomMBat exits, and both `docs/PLAN.md` and
  `start-m7b.md` currently assume otherwise. Probe it: RomMBat up from the menu, write a `.menu` or
  drop a ROM, `GET /reloadgames`, and check `/systems/<system>` `totalGames` rather than the 200.
  Then check again after exit. **Whichever way it goes, a document moves and a finding is owed.**
- **A resolve against the real instance, timed**, at whatever library size mine is. The set screen's
  shape depends on whether a resolve is two seconds or forty, and `sets resolve` already prints
  enough to time it without new code.
- **Published size and first frame after the refactor**, against 7b-1's five files at 101.1 MB and
  1041 ms on the dev box. Moving orchestration into Core moves code the UI links, so this can only
  go one way and I want the number.
- **The agent's own behaviour is unchanged**, which is a measurement and not a claim: run the same
  `sets`, `sync --dry-run` and `evict` invocations before and after the refactor and diff the
  output. A byte-identical diff is the strongest thing this PR can show.

Quote the numbers you took and never one you did not. Probe artifacts go in `probe-output/`, which
is gitignored; if a test needs one, check in the fixture. `tools/m7b-probes/` holds 7b-1's five and
its README carries two hard-won rules, including that a console process reports zero joysticks while
three are attached.

**Probes that write into the real install need my say-so first, every time.** `K:` is the live
install on 8.2.1, and 7b-1 left the published UI installed over the console stub;
`RomMBat.exe.pre-7b1.bak` holds the stub verbatim, md5 `3f75b6db1803df7eac9fac2a4950f671`. Read
`99.md`'s round 7 before assuming the tree is where 86.md left it.

## Tests the review will look for

`/review-pr` checks for the specific test, not for some test.

- **The agent's existing suite passes over the refactor, unchanged.** State the count.
- **Every extracted service is tested without a console**, which is the point of extracting it: the
  scope refusal, the `fs_slug` resolution, the folder validation, the partial-walk resume rule, and
  the pass ordering in a sync (BIOS before ROMs, flush before everything) each get an assertion that
  fails when the order is swapped.
- **The two structural boundaries still hold**, `EsSettingsFile` and `TreeLock`, and the tree-lock
  one gains the anti-vacuity companion #100 asks for.
- **The lock case is asserted rather than described**: a UI-initiated write while a `background`
  pass holds the lock behaves as designed.
- Every new screen is reachable and leavable with the gamepad map alone, at the view-model level,
  with no window.
- **Nothing a screen shows names a face button.** Round 8 found that one field over from where it
  was structurally impossible. A test that _sweeps_ every string a screen produces, not one that
  checks a site.
- An unreachable server leaves every sets screen responsive within the 2 s budget, and listing and
  editing work with the server off.
- No absolute path reaches anything a set definition or a picker persists, with its row in
  `LocalStoreTests`' bad-value table if a column is added.
- The relocation test still a clean no-op with the new state present.
- `publish-check` still produces the agent and hook as one file each and the UI as five.

**Check your important assertions failing before you restore them.** 7b-1 did that for six and two
of them compiled with zero errors, which is exactly why it was worth doing. Put the table in the PR
body.

## Schema

Possibly nothing. `SettingStore` is free-form and `sync_set` already carries caps, ordering and the
folder override. If you do add one, expect `013` and the same discipline as `003` through `012`: its
header states what the existing shape could not carry and why, one migration for the stage, no
column holds an absolute path, CHECK constraints on anything path- or name-shaped, rebuild rather
than ALTER when adding a CHECK, and copy rows even when you are sure the table is empty.

**If per-game install needs a migration, that migration is 7b-2c's, not this branch's.** Say what
you would need; do not add it now.

## Working shape

Commits that stand alone and explain why. **Keep the refactor and the screens in separate commits**,
because a reviewer needs to read "this moved and nothing changed" apart from "this is new". Scoped
diff, no unrelated cleanups riding along: every extra file is review surface, and review surface is
what the next two sessions cost.

If part of this turns out to be a design question rather than a coding one, stop and ask me rather
than picking.

## What the next two sessions need from you

- A PR body on `.github/PULL_REQUEST_TEMPLATE.md`, with the AI disclosure stating extent, not just
  the fact, and the measurement table in the M3 through M7 bodies' shape.
- **`docs/PLAN.md`**: the 7b-2 section rewritten to the three-PR cut with what 7b-2a built, and 7c's
  gate re-checked against it.
- **`docs/ARCHITECTURE.md`**: the `src/RomMBat.UI` section, and section 2 wherever the seam changed
  which project owns what. The sentence saying sets and browse are "7b-2 and 7b-3" is now partly
  false.
- **`README.md`**: the status table, and anything claiming what opening RomMBat from the menu gets
  you.
- **`DEVELOPER_SETUP.md`**: how to drive the new screens without a RetroBat in front of you.
- **Skills**: `retrobat-layout` takes whatever probe 1 turns up about `/reloadgames` under a running
  app, and it is a correction rather than an addition if it goes the way I expect.
  `offline-and-portable` takes whatever the seam adds about the lock and about which operations
  work offline. A rule that exists only because it was measured belongs in a skill or the next
  session re-derives it from nothing.
- Say which documents you moved and which you read and found already correct.
- **Commit this brief**, as every other milestone brief is in the tree.
- No scratch in the tree.
- NOTES seeded with this session's rulings, carrying forward everything still open from `99.md`,
  `86.md`, `87.md` and `97.md`, which is a long list this stage will mostly not shorten. Say that
  plainly rather than omitting it. Carry at least: the claims still recorded against RomM
  5.1.1-beta which now sit below the floor, #87's two `a3b` blocks never run against the live
  instance, the reconnect-recovery case I ruled out of scope, and RomMBat's behaviour when its own
  window is not focused, which nobody has measured.
- The full `pre-pr-verification` skill run, plus `reference/verify.py`, with a plain statement of
  what you verified and what you did not. `dotnet build -c Release -warnaserror` is CI's build, and
  plain `dotnet test` is the test command: **`--nologo` silently reports zero tests** on this repo's
  Microsoft.Testing.Platform setup. **The baseline you inherit is 917 tests green, five CI checks
  green**, so a red run is yours. `trunk` runs through WSL here, it is not on the Windows PATH.
  Build and test from a fresh clone too.
- **The hands-on pass.** Open RomMBat from the ES menu, define a set with a controller, set a
  budget, resolve the set, and get back to ES. Keyboard and mouse unplugged. Eight rounds of this in
  7b-1 found something every single time and the density never fell off, including three defects on
  a screen that had passed 877 tests. If the session cannot take the pass, name the claims that are
  unproven for that reason.

## Default

Read the scope, then show me your reading, the public surface you propose for the Core services, and
the measurement plan, with the `/reloadgames` probe first in it. That is the cheapest place for me
to correct you.

Commit locally as you go. Ask before pushing, before opening the PR, and before anything that writes
into the real RetroBat install or its configuration.
