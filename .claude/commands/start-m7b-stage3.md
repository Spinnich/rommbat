---
description: Start M7 stage 7b-3, conflicts, settings, platform mapping and the button model
argument-hint: "[branch name]"
---

# Start M7 stage 7b-3: conflicts and settings

Fresh session, branched off main. This is the session that writes the branch. What follows it is
`/review-pr` in a second fresh session and `/fix-pr` in a third, so the job is not "working code",
it is a branch that survives a reviewer who will have far less context than you and the repo's own
rules as its standing authority.

7b-2c closed with PR #115, merged at `a7b103a`, and #119 landed the store disposal race on top of
it. A person can define what this device holds, sync it, browse the library, install one game and
take it back off. **They still cannot resolve a conflict, cannot see or change the mapping that
decides where their games land, and cannot touch the queued-config surface 7b-1 taught them to
read.** This stage is the last of the five 7b was cut into.

Branch off `f4ac9b7` or later.

---

Variables for this run:

- BRANCH = $1, default `m7b3-conflicts-and-settings`. Branch off main and stay off it.
- NOTES = `~/rommbat-work/<PR>.md`, started once the PR number exists. **`115.md` is 7b-2c's
  ledger and you read it in full before you design anything.** Then `109.md` for the sync run,
  `103.md` for the seam and the thirteen hands-on rounds, `99.md` for 7b-1's shell, and `77.md`
  for M6, which conflict resolution is squarely inside.

## Who is who

I am Spinnich, the only human on this repo. Claude writes the branch, a fresh Claude reviews it,
a third fixes it. You are the first of the three and the only one that will ever hold the
reasoning behind the code.

Anything true only in your head is invisible to the review, and the review is allowed to rule
against it. Put every non-obvious decision somewhere durable: a commit message, a line of
`docs/PLAN.md`, or a test that fails when someone undoes it.

Skip the preamble and the progress narration. Show me decisions, measurements, and diffs.

## The sentence this branch is built around

**Everything RomMBat knows is wrong or waiting is visible from the couch, and fixable there.**
A conflicted save, a platform with nowhere to put its games, and a setting held back until
EmulationStation closes are the three things the interface knew about and could not act on.

## Ruled with me before this brief was written

Do not spend the session re-opening these.

1. **One branch, all four halves.** The button model, conflicts, the queued-config surface and
   the mapping screen. Each is a screen over a Core API that already exists, so there is no new
   Core design and no migration.
2. **The root becomes a menu of verbs**, with the facts moved behind a "This device" row. Each
   verb row carries the count that motivates it, so the conflict and unmapped counts are visible
   without opening anything.
3. **Queueing a conversion lives on browse's game detail screen**, which is the one place with a
   game in hand. The queued-changes screen lists and cancels.
4. **#116, #117, #118, #101, #96 and #78 all ride along.** #78 is a reversal: it has been
   preserved and asserted twice, and it is fixed here.

## Why the button model comes first

The root puts one action on each of Accept, Start, Extra and Alternate, which is every button a
screen has. This stage needs three more entry points than that. A list grows by a row where a
footer cannot grow by a button, and `docs/PLAN.md` §7b-2c already assigned the change here: "the
rest, where every verb becomes a selectable row, is 7b-3's."

Build it first and commit it alone. Every other screen in this branch hangs off it.

## What 7b-3 is, concretely

### Half one: the root as a list

`RootScreens.Menu` returns a `ListScreen`. `StatusViewModel` keeps `Sections()` and loses its
verbs and its four route hooks. The shell hands the routes to the menu.

**Check whether the status pane windows before you move it.** It predates `ListWindow` and it is
the one screen that never got the folder picker's fix.

### Half two: conflicts

`store.SaveConflicts.ListOpen()` and `SaveConflictResolver.ResolveAsync` both exist. What does not
exist is a way for the UI to hold `TreeLock`, which resolving a class C conflict must, and which
the UI may never name. **That is a Core service, and `saves resolve` becomes a shell over it**,
the same shape 7b-2b's flush lift took.

**Preserve the agent's ordering**: the lock is taken before authenticating, because a resolution
that cannot run is not worth a round trip.

No default side and no resolve-all. Either default is the guess the conflict exists to avoid.

### Half three: the platform mapping

`PlatformMapStore` has `List`, `Find`, `SetOverride` and `ClearOverride`, and `platforms
list/map/unmap` shows the write path. This is a screen over them and needs no Core change.

Unmapped sorts first. No connection: the rows are already local.

### Half four: the queue

`PendingConfigStore` reads and cancels; `SaveConverter.Preview` and `Queue` are the write half.
Ask `SaveConverter` whether a game can be converted rather than working the rule out in a screen.

**There is no apply path from the interface and there cannot be one.** Assert it.

## The rules that bite in this stage specifically

- **The UI may never name `TreeLock` or `EsSettingsFile`.** Both are asserted structurally against
  the built assembly, and the first is what makes half two a Core service rather than a screen.
- **Reading-pane rows must be unavailable**, or the footer promises an Accept that does nothing.
- **`ListScreen.ExtraHints` replaces the constructor's hints rather than adding to them.**
- **Offline is a working state.** Conflicts need the server; the mapping and the queue do not.
- **A screen cannot name a face button**, in a hint or in prose.
- **English only, no em-dashes, comments say why and stay short.**

## Tests the review will look for

- **Every row on the root opens a screen.** The failure mode moved from a verb with nowhere to go
  to a row that goes nowhere.
- **The status pane windows**, and a block of its lines is never taller than an ordinary list.
- **A resolution is refused while something else holds the tree**, with the connection factory
  throwing if it is called, which is what asserts the ordering.
- **Offline leaves the conflict open** and says so.
- **Choosing a folder writes a user override**, not a per-set one.
- **Nothing on the convert screen offers to write the setting now.**
- **A search submitted while a page is in flight does not start a second fetch.** Hold the page
  open in the stub and count arrivals: a version that counts answers passes with the guard
  deleted, and `OnScreenKeyboard.Commit` refuses an empty string, so a test that presses straight
  through never reaches the search path at all.
- Nothing a screen shows names a face button, swept over every new screen.
- The two structural boundaries still hold.

**Check your important assertions failing before you restore them.** Put the table in the PR body.

## Working shape

**Separable commits, at least six**: the button model; conflicts; the mapping; the queue; the
three browse and eviction ride-alongs; and #78, #101 and #96, which change agent behaviour and
must not arrive inside a screen commit.

## What the next two sessions need from you

- A PR body on `.github/PULL_REQUEST_TEMPLATE.md`, with the AI disclosure stating extent.
- **`docs/PLAN.md`**: §7b-3 rewritten to what was built, §7b-2c's forward reference paid, and
  §7c's gate stated as open or not.
- **`docs/ARCHITECTURE.md`**: the `src/RomMBat.UI` section, which names 7b-3 as pending.
- **`README.md`**: the M7 row.
- **Skills**: `save-sync` takes the lock rule's move into Core; `offline-and-portable` and
  `platform-mapping` take the mapping screen.
- **Issues**: close what you fixed.
- **Commit this brief**, as every other milestone brief is in the tree.
- The full `pre-pr-verification` run. `dotnet clean` first, because an incremental green is not
  evidence. Plain `dotnet test`, because **`--nologo` silently reports zero tests**. `trunk` runs
  through WSL here. **The baseline you inherit is 1,178 tests green.**
- **The hands-on pass.** 7b-2c owes one and this stage owes another, and this one moved every
  screen's way in. If the session cannot take it, name the claims that are unproven for that
  reason rather than letting the test suite stand in for evidence.
