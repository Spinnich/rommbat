---
description: Start M7 stage 7b-2c, browse, per-game install and removal
argument-hint: "[branch name]"
---

# Start M7 stage 7b-2c: browse, per-game install, and removal from the couch

Fresh session, branched off main. This is the session that writes the branch. What follows it is
`/review-pr` in a second fresh session and `/fix-pr` in a third, so the job is not "working code",
it is a branch that survives a reviewer who will have far less context than you and the repo's own
rules as its standing authority.

7b-2b closed with PR #109, merged at `27890a4`. A person can define what this device holds, resolve
it, sync it with live progress, and stop part way without leaving half a game behind. **They still
cannot find one game, and they cannot remove anything at all.** Every route to content is
set-shaped, and eviction came off the interface in 7b-2b on the ruling that freeing space belongs
to the user. The plan's own replacement sentence, "dropping a sync set or (once 7b-2c lands) a
single game", is unpaid on both halves.

Branch off `27890a4` or later.

---

Variables for this run:

- BRANCH = $1, default `m7b2c-browse-and-picks`. Branch off main and stay off it; the pre-push
  hook will stop you anyway.
- NOTES = `~/rommbat-work/<PR>.md`, started once the PR number exists. **`~/rommbat-work/109.md` is
  7b-2b's ledger and you read it in full before you design anything.** Then `103.md` for the seam
  and the thirteen hands-on rounds, `99.md` for 7b-1's shell, and `77.md` for M6 if you touch
  save-adjacent code, which removal does.

## Who is who

I am Spinnich, the only human on this repo. Claude writes the branch, a fresh Claude reviews it, a
third fixes it. You are the first of the three and the only one that will ever hold the reasoning
behind the code.

Anything true only in your head is invisible to the review, and the review is allowed to rule
against it. Put every non-obvious decision somewhere durable: a commit message, a line of
`docs/PLAN.md`, or a test that fails when someone undoes it.

Skip the preamble and the progress narration. Show me decisions, measurements, and diffs.

## The two sentences this branch is built around

**A person can find one game and put it on the device.** Search, one press, and it lands, with the
same whole-or-absent guarantee a set sync has.

**A person can take a game back off, and their saves are demonstrably safe when they do.** Per
game from browse, and per set by deleting the set, on one shared path.

Everything below is in service of those two, and it is what the tests assert.

## Ruled with me before this brief was written

Do not spend the session re-opening these.

1. **Install writes the pick and then syncs that one game immediately.** One press, game on disk.
   Not "added to a set, sync later".
2. **Removal is per game and per set in this branch**, on one member-scoped entry point. #110 is
   the set half and rides along rather than waiting for 7b-3.
3. **One hand-picked set**, implicit, created on the first pick. Ordinary in every other way:
   listed, synced, evicted, roamed, renameable, deletable. Nothing offers to make a second and
   nothing in the schema forbids one.
4. **It roams as an id array in `scope_value`**, hydrated on a receiving device through
   `GET /api/roms/{id}`. `RoamingSyncConfig` is unchanged.
5. **No cover art.** Text rows, like every other screen. Art is its own stage with its own
   measurement.
6. **One browse screen that degrades**, saying which of the two things it is showing, not two
   screens chosen up front and not an online-only screen with a refusal.
7. **A game another enabled set still claims is held back**, with "still in `<set>`" beside
   SaveGuard's refusals.
8. **One ROM in two folders is legitimate.** Fix the crash it currently causes and make the
   doubled bytes visible. No refusal, no schema change.
9. **#113, #111, #114, #105, #106 and #107 ride along.**

## Why this stage exists

§7c's gate is open for two of its three requirements and the third has never needed RomMBat, so
this branch is not what the rollout waits on. What it is, is the last stage before 7b-3 that adds
a new kind of thing to the interface rather than a new screen over an existing one: a scope kind, a
removal path, and the first screen that pages the server rather than reading the store.

It is also the first branch that removes a user's content on purpose. `GameSync`'s rollback removes
things, but only what the run itself just wrote and only to keep a game whole. This removes files
the user asked for, which is a different risk with a different guard.

## What 7b-2c is, concretely

### Half one: the sixth `CatalogScopeKind`

`docs/PLAN.md` §7b-2c already ruled the shape and 7b-2a deliberately did not build it: a hand-picked
set is a set, so it is a scope kind with a migration, not an id list smuggled inside a `Filter`
scope and not an unmanaged download `EvictionPlanner` has to be taught to ignore.

**Migration `014`.** `sync_set.scope_kind`'s `CHECK` gains `'picked'`. Same discipline as `003`
through `013`: header states what the existing shape could not carry and why, rebuild rather than
`ALTER` when touching a `CHECK`, copy rows even though you are sure the constraint only widens, no
column holds an absolute path. **No new column.** For this scope the id list is the definition, so
it lives in `scope_value` exactly as a filter's JSON already does.

**`/api/roms` has no id-list parameter.** Verified against the pinned `romm-5.2.0.json`: the
scoping parameters are `platform_ids`, `collection_id`, `virtual_collection_id` and
`smart_collection_id`. So a picked set can never be resolved by a page walk, and that is a property
of the scope rather than a defect to work around.

- **On the picking device there is nothing to resolve.** The browse page already carries every
  field `sync_set_member` wants: `fs_name`, `fs_extension`, `size_bytes`, the three hashes,
  `has_multiple_files`, `platform_slug`, `platform_fs_slug`, the sort key. A pick writes its member
  row from the `RomRow` in hand.
- **On a device it roams to**, hydrate by fetching each id. One new method on
  `RomMConnection.Catalog` over `GET /api/roms/{id}` returning a `RomRow`. M4 measured ~0.15 s per
  ROM, which is seconds at the tens of games this scope is for and is the reason it is not offered
  for anything larger.

Six sites have to learn the kind and every one of them is real. `CatalogScopeKind` and
`CatalogQuery.ToQueryString`, which must **refuse** rather than silently page unscoped, because a
`Picked` scope reaching the pager is a bug and not a filter. `SyncSetStore.ScopeText` and
`ParseScope`. `SyncSetService.Scopes()`, where it is offered and **not pickable**, with the reason,
through `CatalogScopeService.WhyNotListable`, which is the existing home for that sentence and means
the existing "every pickable scope is completable" test covers it for free. `SetResolver`, which
takes the hydrate path. And `SyncSetService.FilterOf`, which you verify rather than assume.

### Half two: browse

New, `src/RomMBat.UI/Screens/BrowseViewModel.cs`, reached from the root status screen through an
`OpenBrowse` hook beside the `OpenSets` and `OpenBudget` already in `App.Build`.

**One page in memory, ever.** `RomRow`'s remarks, `RomPager`'s remarks and M2's "no full-library
mirror, ever" all say the same thing three times. Browse holds one page and moves by page: past the
bottom fetches the next offset, past the top fetches the previous. **Page size 50**, not
`RomPager.DefaultPageSize`'s 250: `ListWindow.Capacity` is 8 rows, so 250 is 31 screens of
scrolling per fetch, and at the measured ~10 ms per ROM a 50-row page is about half a second.

**It degrades rather than refusing.** With a server it pages `GET /api/roms` through the existing
`RomPager` and `CatalogQuery`. Without one it lists what this device holds, out of `local_file`
joined to `sync_set_member`. The screen says which of the two it is showing. That is `ListScreen`'s
existing `Load` pattern; the paging is the part `ListScreen` cannot do, which is why this is its own
view model and not a fifth `ListScreen` caller.

**Every row says whether it is on this device**, and in which folder or folders.

**Search reuses `OnScreenKeyboard` unchanged.** 7b-2a transcribed all four faces of all three ES
layouts, so the "third layer" the 7b-2 brief left for you does not exist any more. Check that
before you build anything. The platform filter reuses `SyncSetService.PlatformsKnownHere()`.

**A game's detail screen** is a `ListScreen` in `Reading` mode: name, platform, size, hashes, where
it is on this device, which sets claim it, and the two verbs.

### Half three: install one game

`Verbs` on the detail screen writes the member row into the picked set, creating the set on the
first pick, then returns `ScreenCommand.ReplaceThenOpen` so the sync screen opens over the set the
game was just added to. That is the shape `SetEditorViewModel` already uses for create-then-resolve
and the reason `ReplaceThenOpen` exists.

Core needs a per-game entry point. **My reading, which you should argue with: a narrow method on
`LibrarySyncService`** running Content, Media, Gamelists and Budget for one `PlannedGame`, reusing
`GameSync` untouched so the invariant, the three rollback fences and the `CancellationToken.None`
gamelist write all come along. It does **not** re-run Resolve, there being nothing to resolve, and
does **not** re-run Flush, which 7b-2b put first for eviction's benefit and nothing here evicts.
`SyncViewModel` takes it as a second construction shape rather than growing a mode.

A game already present is `ContentAction.AlreadyPresent` and the screen says so rather than
pretending to download. Worth having on its own terms: it is how a user adds a second claim to a
game a platform set already holds, which protects it from that set's eviction.

### Half four: removal

**One Core entry point and two callers.** `EvictionService` gains a member-scoped preview beside its
byte-scoped one; `ApplyAsync` is unchanged and shared. Browse passes one id, #110's set delete
passes the set's members, and everything below is common.

The order is #110's shape and every step is load-bearing:

1. **Flush first**, through `SaveFlushService`, which 7b-2b made front-end agnostic. The commonest
   `SaveGuard` refusal is an unsent save, and flushing resolves it rather than blocking the
   removal.
2. **Plan against the given ids**, not against a byte target. `Plan(bytesToFree)` returns early
   when nothing is over budget, so the existing entry point cannot serve this at all.
3. **Refuse per game on `SaveGuard`**, which already answers per ROM and whose refusals
   `EvictionPlan.Refused` already carries with reasons.
4. **Hold back a game another enabled set still claims.** The claim walk already exists inside
   `EvictionPlanner.Candidates()`, keeping the best claim across every enabled set, with a comment
   saying exactly why. **Lift it into a method both paths call.** Without it, deleting one set
   silently removes a game a set the user never touched still wants, and the next sync
   re-downloads it.
5. **Name what cannot be vouched for.** A Class D shared container has no `rom_id` by definition
   and a Class C unit whose attribution failed has a null one, so `SaveGuard` cannot attribute
   either. Name the container and let the user decide. Do not claim safety.

**`local_file` cannot hold a save.** Its seven kinds are `rom`, `image`, `thumbnail`, `marquee`,
`video`, `manual` and `firmware`, enforced by a `CHECK`; saves live in `local_save` and
`local_state`. Anything that removes content walks `local_file`, so it **cannot** delete a save.
That is schema-level rather than careful coding and it belongs in the confirmation's words.

**Preview then confirm, and the preview is the screen rather than a flag.** Say "preview":
`dry-run` names `sync`'s flag and nothing else. `EvictionService.Describe` already words both
candidate kinds; quote it rather than rewording.

**`SetsScreens.ConfirmDelete` currently says "The set is forgotten. Nothing on disk is touched and
no game is removed."** This branch makes that false and owes the correction in the same PR.

### Half five: one ROM in two folders, which currently crashes

`folder_override` exists because arcade has no single RetroBat folder. Migration `002`'s header
says so and `SetResolver` calls it "the only way an arcade set resolves". So a `mame`-overridden
platform set and an `fbneo`-overridden collection set drawn from that same platform put every
shared game in both folders, and **both sets are then correct in EmulationStation**. Remapping a
platform between two syncs reaches the same state with no override at all.

`EvictionPlanner.Candidates()` builds its ROM lookup with `ToDictionary(file => file.RomId!.Value)`,
which throws on a duplicate key. The comment directly above it says keying on `rom_id` alone "would
throw on the second", but it only fixed the media case by filtering to `Kind == Rom`. Two Rom-kind
rows for one `rom_id` is exactly the state `LocalFileStore.ForRom`'s own remarks say is
representable, `ix_local_file_rom_kind` is not `UNIQUE`, and nothing prevents it. Reaching it takes
out `evict`, the budget screen and the eviction path inside every sync.

Group by `rom_id` instead, so each copy is its own candidate evicting on its own merits, with media
attached to each because each folder's gamelist references its own. Then make it visible: a sync
reports the second copy as a line and browse names every folder a game is in. The bytes genuinely
double and the budget is right to count them twice; what was wrong is that nobody could see why.

**Refusing the second copy is the wrong fix** and I want it stated in the code, because it is the
tidier-looking one: it would leave the second set's gamelist naming a file outside its own folder,
which breaks that set in ES.

## The ride-alongs, and why each one is here rather than elsewhere

- **#113, phantom `local_file` rows.** 1,284 rows on the live `K:` install point at files that are
  gone, 14.92 GiB, so `budget` reads 18.3 GB against roughly 3.4 GB real and an 8 GB cap blocks
  every game with nothing pointing at the cause. Browse's on-this-device marker and per-game
  removal both read that table, so this branch would show the same lie in a new place. Take the
  issue's own preference order: `status` counts it, and a repair path removes rows whose file is
  gone, safe by the rollback's own argument that a row must never outlive its bytes.
- **#111, per-game store reads.** `SyncSetService.OnDisk` is one query per member per set and
  `MediaSync` reads the whole `local_file` table per game. A browse page marking 50 rows installed
  is the same shape on the drawing thread. One aggregate query each, no behaviour change, and
  `OnDisk`'s remarks survive the rewrite.
- **#114, a budget-blocked sync reporting `Done`.** 7b-2b patched `SyncViewModel.Settle` only, and
  per-game install goes through the same path. **Answer it in Core and take the issue's option 3, a
  distinct `SyncState`.** Option 2 makes `rommbat-agent sync` exit `Offline` for a full disk, which
  is a different lie. A fourth state costs `SyncCommand` one mapping arm and stops every future
  consumer having to remember to check `Blocked`. **This changes an agent exit code** and gets
  called out in the PR body rather than absorbed.
- **#105, #106, #107.** `SetEditorViewModel` drops the connection factory, which is what stops
  create-then-resolve being driven against `StubRomMServer`, and fixing it is what makes this
  branch's screens testable end to end. Then `Rows[Cursor]` with no empty guard, and
  `ResolveViewModel`'s unreachable cancelled summary.

## Out of scope, and each is somebody's, just not this branch's

- **Conflict resolution, the queued-config surface, and the mapping screen.** 7b-3.
- **Cover art of any kind.** Ruled out above with a reason; do not reintroduce it as "just the
  selected row".
- **A second hand-picked set.** The schema allows it, nothing offers it.
- **`POST /api/activity/heartbeat`.** Still a milestone decision about the `background` pass.
- **#78**, `sets add --scope filter` silently ignoring `--value`. Preserved and asserted. **Do not
  fix it while passing.**
- **#96, #98, #101.** Not yours.
- **Adopting a newer upstream.** The floor is RetroBat 8.2.1 and RomM 5.2.0. If either ships a new
  stable while this branch is open, that is its own PR under the version-move checklist.

## Read before you design

1. **CLAUDE.md**, in full. Rule 1 binds every path this branch persists, and browse is the first
   screen that shows a user where a file is.
2. **`docs/PLAN.md`**: M2 in full, because browse is M2 given a face and the scope model is M2's;
   then M3 for the planner and the budget, M7 in full, and core principles 1, 2 and 4.
3. **`~/rommbat-work/109.md` in full**, then `103.md`. The hands-on rounds are where the screens
   this branch extends were actually shaped.
4. **`docs/ARCHITECTURE.md`**, the `src/RomMBat.UI` section and sections 2 and 9. All three carry
   claims this branch can falsify.
5. **Skills**: `offline-and-portable` and `save-sync` fully, because removal is save-adjacent code;
   `romm-api` for the paged read, the search parameters and the 401 path; `platform-mapping` for
   the folder resolution the two-folders case turns on; `pre-pr-verification` **early, not at the
   end**.
6. **The code**: `RomPager`, `CatalogQuery`, `RomRow`, `EvictionPlanner`, `EvictionService`,
   `SaveGuard`, `ContentPlanner`, `GameSync`, `LibrarySyncService`, `SetResolver`, `SyncSetStore`,
   and on the UI side `ListScreen` (its `Load`, `Verbs` and `Reading` members and their remarks),
   `SyncViewModel`, `ResolveViewModel`, `SetsScreens`, `Navigator` and `ScreenView`.

## The rules that bite in this stage specifically

- **Presentation owns no logic.** Browse is a screen over an API you are also writing, which is the
  condition that tempts a view model to answer an awkward question itself. Both previous stages
  named this as the single thing most likely to go wrong.
- **Nothing holds more than one page.** This is the rule browse is most likely to break, and it
  breaks silently and only at scale. A test asserts it.
- **The UI may never write `es_settings.cfg`** and may never name `TreeLock`. Both are asserted
  structurally against the built assembly.
- **Never persist an absolute path.** Browse displays paths for the first time; displaying is not
  persisting, and the line between them is one careless `Set` call.
- **Offline is a working state.** Browse, removal preview and the whole sets surface work with the
  server off.
- **Windows refuses a file operation two ways and only one is an `IOException`.**
  `UnauthorizedAccessException` does not derive from it. Removal deletes files and will meet both.
  #96 is the same mistake still open in product code and is not yours to fix.
- **A screen cannot name a face button**, in a hint or in prose.
- **English only, no em-dashes, comments say why and stay short.**

## Design questions to put to me rather than pick

- **The per-game sync entry point's signature**, before you write it. My reading is above; argue
  with it. 7b-3 inherits whatever this is.
- **What the member-scoped preview returns.** `EvictionReport` as it stands, or a sibling type. One
  of those makes `ApplyAsync` shared for free and the other makes the two previews honest about
  being different questions.
- **How browse says "on this device" when the answer is two folders.** It is the row's second
  column, and the row is already carrying platform and size.
- **Whether the picked set's name is fixed or typed on the first pick.** Fixed is one fewer
  keyboard on the commonest path; typed means two devices do not collide on the roam.
- **What browse does when the cursor reaches the end of the last page.** Wrap, stop, or say so.
  `ListScreen`'s cursor wraps and a paged list that wraps to page one is a different promise.
- **Whether any of this earns a new skill**, or whether `offline-and-portable` and
  `platform-mapping` absorb it.

## Measure before you commit to a shape

Quote the numbers you take and never one you did not. Probe artifacts go in `probe-output/`, which
is gitignored; if a test needs one, check in the fixture. **Probes that write into the real install
need my say-so first, every time.** `K:` is the live install on 8.2.1; read `109.md`'s last round
before assuming the tree is where it left it.

1. **A browse page against the live instance at 50 and at 250 rows**, timed. The page size above is
   reasoned from M0's 10 ms per ROM, not measured on this path. If 50 is wrong, change it and say
   why.
2. **One install from browse**, timed and sized, from press to the game being listed in ES.
3. **A removal on the live install with `local_save` rows present**, showing what was kept and why.
   This is removal's only real evidence.
4. **The two-folders case reproduced**, and `evict` throwing on it before your fix. That is what
   turns the crash from an argument into a finding.
5. **Published size and first frame**, against 7b-2b's numbers on the dev box.
6. **#113's count on the live install**, before and after whatever repair lands.

**Kill orphaned test hosts before believing any timing.** #103 lost real time to three holding
1179s, 1275s and 679s of CPU while it measured a "slow" box.

## Tests the review will look for

`/review-pr` checks for the specific test, not for some test.

- **The claim rule, both directions.** Removing a set holds back a game a second enabled set still
  claims, and removes it once that set is gone.
- **Two Rom-kind rows for one `rom_id` no longer throw** in `Candidates()`, and each copy is its
  own candidate. Write this against the current code first and watch it throw.
- **The picked set**: created on the first pick, members written from a `RomRow` with no resolve,
  and `EvictionPlanner` never calling its games `Orphaned`.
- **The roam round trip**: ids into `sync_config` and back out, hydrating through a stubbed
  `GET /api/roms/{id}`.
- **Removal never touches a save**: `local_save` and `local_state` rows for the removed game both
  surviving, plus a Class D container named rather than guarded.
- **Browse holds one page**: the row count never exceeds the page size across several pages.
- **Browse is reachable and leavable with the gamepad map alone**, at the view-model level with no
  window, driving install end to end against `StubRomMServer`. #105 is what unblocks this.
- **An unreachable server leaves browse responsive within the 2 s budget** and showing the local
  subset.
- **Nothing a screen shows names a face button.** A sweep over every string a screen produces, not
  a check at one site.
- **The two structural boundaries still hold**, `EsSettingsFile` and `TreeLock`, with #100's
  anti-vacuity companion still passing.
- No absolute path reaches anything persisted, with its row in `LocalStoreTests`' bad-value table
  if a column is added.
- `publish-check` still produces the agent and hook as one file each and the UI as five.

**Check your important assertions failing before you restore them.** 7b-1 did that for six and two
compiled with zero errors. Put the table in the PR body.

## Schema

**Migration `014`, and it is the only one.** `sync_set.scope_kind`'s `CHECK` gains `'picked'`,
rebuilt rather than altered, rows copied. If #113's repair path wants a column, say what is missing
before you add it and expect to be told to use `setting` instead.

## Working shape

**Separable commits, at least five**: the migration and the sixth scope kind; browse; install;
removal including #110; and each ride-along on its own. A reviewer needs to read those claims apart
from each other, and #114 in particular changes an agent exit code and must not arrive inside a
screen commit. Scoped diff, no unrelated cleanups riding along beyond the six named issues.

If part of this turns out to be a design question rather than a coding one, stop and ask me rather
than picking.

## What the next two sessions need from you

- A PR body on `.github/PULL_REQUEST_TEMPLATE.md`, with the AI disclosure stating extent, not just
  the fact, and the measurement table in the M3 through M7 bodies' shape.
- **`docs/PLAN.md`**: §7b-2c rewritten to what was built, §7b-3 re-checked against what landed, and
  §7c's gate stated as open or not.
- **`docs/ARCHITECTURE.md`**: the `src/RomMBat.UI` section, which names 7b-2c as pending; section 2
  if the scope kind moves who owns what; and the size and first-frame numbers.
- **`README.md`**: the M7 row, which says in as many words that browse and per-game install are
  7b-2c.
- **`DEVELOPER_SETUP.md`**: its line about freeing space names 7b-2c as pending, and driving browse,
  install and removal with no RetroBat in front of you.
- **Skills**: `offline-and-portable` takes what browse does with the server off and the claim rule;
  `save-sync` takes the Class D naming rule; `platform-mapping` takes the two-folders finding. A
  rule that exists only because it was measured belongs in a skill, or the next session re-derives
  it from nothing.
- **Issues**: close what you fixed, and re-body #110 and #114 if what you built differs from what
  they propose, because their analysis is what a future session would trust.
- Say which documents you moved and which you read and found already correct.
- **Commit this brief**, as every other milestone brief is in the tree.
- No scratch in the tree.
- NOTES seeded with this session's rulings, carrying forward everything still open from `109.md`,
  `103.md`, `99.md`, `86.md`, `87.md` and `97.md`. That list is long and this stage will mostly not
  shorten it. Say so plainly rather than omitting it.
- The full `pre-pr-verification` run, plus `reference/verify.py`, with a plain statement of what you
  verified and what you did not. `dotnet build -c Release -warnaserror` is CI's build and an
  incremental green is not evidence: `dotnet clean` first. Plain `dotnet test` is the test command,
  because **`--nologo` silently reports zero tests** on this repo's Microsoft.Testing.Platform
  setup. **The baseline you inherit is 1055 tests green and five CI checks green**, so a red run is
  yours. `trunk` runs through WSL here. Build and test from a fresh clone too.
- **The hands-on pass.** Open RomMBat from the ES menu, search for a game, install it, watch it
  land, leave and see it in EmulationStation, then remove it and see it go. With a controller.
  Thirteen rounds in 7b-2a and eight in 7b-1 found something every single time and the rate never
  fell off. If the session cannot take the pass, name the claims that are unproven for that reason
  rather than letting the test suite stand in for evidence.
