---
description: Start M7 stage 7b, the gamepad UI
argument-hint: "[branch name]"
---

# Start M7 stage 7b: the gamepad UI

Fresh session, branched off main. This is the session that writes the branch. What follows it is
`/review-pr` in a second fresh session and `/fix-pr` in a third, so the job is not "working code",
it is a branch that survives a reviewer who will have far less context than you and the repo's own
rules as its standing authority.

7a closed with PR #86, merged. It installed an ES menu entry that opens `RomMBat.exe`, and
`RomMBat.exe` prints one line to a stderr nobody sees and exits 70. **This stage is the first one
whose whole output is something a person looks at**, and it is the last thing standing between the
repo and the platform rollout, which by `docs/PLAN.md` cannot start until it lands.

Three PRs have landed since and none of them is UI work, so the stub is untouched, but each moved
something underneath this stage. **#87** mined Argosy, a shipped gamepad-first RomM launcher, and
left a note addressed to M7b that is the closest thing this repo has to prior art on controller
input. **#95** fixed a temp-tree teardown, and the rule it produced applies to code you are about
to write. **#97** moved the floor to RetroBat 8.2.1 and RomM 5.2.0, which is the pair your
hands-on pass runs against and the version the live install is on now. Branch off `7462f0c` or
later.

---

Variables for this run:

- BRANCH = $1, default `m7b-gamepad-ui`. Branch off main and stay off it; the pre-push hook will
  stop you anyway.
- NOTES = `~/rommbat-work/<PR>.md`, started once the PR number exists. `~/rommbat-work/86.md` is
  7a's ledger and `77.md` is M6's. Read 86 in full before designing, past the review rounds; it
  carries what 7a left for you by name. Then `87.md` and `97.md`, which are short: 87's scope
  calls and post-merge section, and 97's still-open section, each leave you something. #95 has no
  ledger of its own and sits in 87's post-merge section.

## Who is who

I am Spinnich, the only human on this repo. Claude writes the branch, a fresh Claude reviews it, a
third fixes it. You are the first of the three and the only one that will ever hold the reasoning
behind the code.

Anything true only in your head is invisible to the review, and the review is allowed to rule
against it. Put every non-obvious decision somewhere durable: a commit message, a line of
`docs/PLAN.md`, or a test that fails when someone undoes it.

Skip the preamble and the progress narration. Show me decisions, measurements, and diffs.

## Why this stage exists

Six milestones built a client that a person drives from a terminal. The plan has said since M0
that the terminal is not the product:

1. **The menu entry opens a stub.** `sync` tells me RomMBat can be opened from the couch, and what
   opens is `Console.Error.WriteLine` and `return 70` into a window ES is covering. 7a's own ledger
   files this under Open and rules it 7b's to fill.
2. **Nothing but the agent can read the queue.** Migration `012` exists because the UI runs under a
   live ES and can therefore never write `es_settings.cfg`. Its columns were shaped around a reader
   that does not exist yet, and until one does, the read side rests on tests.
3. **The rollout is gated on this.** Certification is one person launching games per
   `(system, emulator, core)`, and `docs/PLAN.md` puts the gate here explicitly, because doing that
   by alt-tabbing to a terminal between launches is how a rollout stops after three systems.

## Read before you design

1. **CLAUDE.md**, in full. Rule 1 binds anything this UI remembers. Rule 2 is the one this stage
   cannot bend: no emulator INI, and the `es_settings.cfg` half of it is stricter here than
   anywhere else in the repo, for the reason in Scope below.
2. **`docs/PLAN.md`**: M7 in full including 7a and 7c, the `RomMBat.UI` paragraph under "Projects"
   for the Avalonia ruling and its actual argument, core principle 1 on offline-first and principle
   4 on portability, M1's pairing section for why the server URL is the one gamepad-hostile step,
   and the risks table rows on typing a URL on a gamepad and on `es_settings.cfg` under a running
   ES. **The framework is settled and reopening it is out of scope**; if you think it is wrong, say
   so in NOTES and build on Avalonia anyway. 7a's closing paragraph on `POST /api/activity/heartbeat`
   is new since this brief was written: read it so you know the seam exists and that it is not yours.
3. **`docs/retrobat-findings.md`**: findings 118 (an ES-menu launch is identifiable, which is why
   RomMBat's own exit is not a play session), 203 (`/reloadgames` picks a new entry up in 209 ms),
   204, 205 and 207 on the `es_menu` gamelist, 208 (`POST /launch` answers 200 and does nothing),
   179 and 202 on when ES writes `es_settings.cfg`, and probe 1 on hook concurrency. #97 rewrote
   parts of that document for 8.2.1 and **none of those rows moved**, which is worth knowing before
   you re-derive one. The header now records the re-check, and the numbered rows still carry the
   version they were measured on.
4. **`docs/argosy-findings.md`, the section "A note addressed to M7b"**, which did not exist when
   this brief was written and is the reason #87 was worth doing. Argosy is a shipped gamepad-first
   RomM launcher at v2.8.0: A confirms and never adjusts, focus never moves an element, inline
   affordances are always visible, footer hints shed in a fixed order, and its `ControllerDetector`
   resolves a Nintendo-versus-Xbox face-button layout from vendor id then device-name patterns over
   the same handheld hardware a Windows RetroBat runs on. `docs/freegosy-findings.md` D7 is the
   same kind of lead from the other client. **All of it is design input and none of it is
   evidence**, and the controller lists are a starting point to check against real devices, never
   a table to copy into a data file. Where a probe of yours contradicts a lead, the probe wins and
   the lead gets amended.
5. **`~/rommbat-work/86.md`** in full, then `87.md`, `97.md` and `77.md`. Do not re-litigate a
   ruling in there without saying you are doing it and why.
6. **Skills**: `retrobat-layout` and `offline-and-portable` fully, `romm-api` for pairing and the
   401 path, `save-sync` for the conflict and queue surfaces, and `pre-pr-verification` before
   claiming anything is done.
7. **The code you are building on.** Everything this UI shows already exists behind a subcommand,
   and finding that seam is most of the design work: `PairCommand`, `StatusCommand`, `SetsCommand`,
   `BrowseCommand`, `BudgetCommand`, `EvictCommand`, `SyncCommand`, `SavesCommand`, `MenuCommand`,
   and `AgentContext` which is how all of them open the tree. Then in Core: `SetResolver`,
   `ContentPlanner`, `EvictionPlanner`, `ServerProbes`/`ServerContact`, `SaveConflictResolver`,
   `SaveConflictStore`, `PendingConfigStore`, `SettingStore`, `TreeLock`, `LocalStore`,
   `EmulationStationProcess` and `EmulationStationClient`. In `RomM.Client`: the pairing poll loop
   and `CatalogQuery` with its pager.

## Scope

**This is too big for one PR and the first thing I want from you is the cut.** M6 shipped as 2a,
2b and 2c for the same reason. My own reading, which you should argue with rather than accept:

- **7b-1, the shell and the way in.** An Avalonia app that comes up full-screen over ES, is driven
  entirely by a controller, pairs against a server whose URL was typed on an on-screen keyboard,
  and shows status: what device this is, what it is paired to, when it last synced, what is in the
  outbox, what is queued, what is in conflict. Read-only past pairing. Exits back to ES cleanly.
- **7b-2, sets and browse.** Sync sets, online paged browse with search, offline browse of the
  local subset, per-game install and evict, the disk budget, and sync progress.
- **7b-3, conflicts and settings.** Conflict resolution, the queued-config surface, platform
  mapping, and whatever 7b-1 and 7b-2 turned up.

If that cut is right, **this session ships 7b-1 and nothing else**, and the design work still
covers all three so the shell is not shaped wrong for what follows.

### The constraints that are not negotiable in any cut

- **Presentation owns no logic.** That sentence is in the plan and in the stub's own doc comment.
  If a screen needs a decision Core cannot answer, the fix is an API on Core with a test, not a
  method on a view model. This is the single thing most likely to go wrong in this stage and it is
  the reason deferring the framework was cheap.
- **The UI cannot write `es_settings.cfg`, ever.** It is launched from the ES menu, so ES is always
  up, and ES serialises a model loaded at startup over anything written underneath it. Every such
  change goes through `PendingConfigStore` and `background quit`. The plan's words are "there is no
  arrangement under which it can", and I mean them.
- **No primary flow may require a mouse**, and a gamepad is not a keyboard. A d-pad, a stick, and
  the face buttons have to reach everything, including the server URL.
- **Offline is a working state, not an error screen.** The reachability check gets the 2 s
  `ConnectTimeout` from M0 experiment 6, and the UI never blocks on it. An unreachable LAN host
  takes 21 seconds to fail by default and that is four rows of animation frames into a hang.
- **Never persist an absolute path**, including in whatever the UI remembers about where it was.
- **RomMBat's own launch fires an orphan `game-end`** carrying `-system retrobat`. That already does
  not become a play session, and this branch is the one that makes it happen for real rather than in
  a fixture.

**Out of scope**: packaging and the installer, which is M8; certifying anything; touching the
platform mapping table's contents; and any change to how sync itself decides what to fetch.

Three more that are out of scope because they are somebody's, just not this branch's:

- **`POST /api/activity/heartbeat`.** It works at 5.2.0, RomMBat has never posted one, and the
  only part of this design awake during play and allowed to use the network is the detached
  `background <event>` pass. That makes presence a milestone decision about that pass rather than
  a UI feature, and I want it decided on its own. If the UI wants to show who else is playing,
  say so in NOTES and stop there.
- **The seventeen open issues.** Several are surfaces this UI will put on screen: #78 on
  `sets add --scope` ignoring `--value`, #84 and #85 on saves, #88 on `CatalogQuery` paging 3.4 to
  3.7 times slower than it needs to be on a scoped walk, #90 on a swallowed session-close failure.
  **Show what Core reports, including when it is wrong, and do not fix it here.** If a screen
  cannot be built honestly on top of one of them, that is worth stopping over.
- **Adopting a newer upstream.** The floor is RetroBat 8.2.1 and RomM 5.2.0 as of #97. If either
  ships a new stable while this branch is open, that is its own PR under the version-move
  checklist in `pre-pr-verification`, and folding it into a UI branch buries it. Say it in NOTES.

## The rules that bite in this stage specifically

- **`OutputType` is still `Exe` and the manifest is already written.** Read `app.manifest` before
  you change anything: `asInvoker`, `longPathAware`, UTF-8, PerMonitorV2. Changing to `WinExe` is
  probably right and it is a decision with a console-window consequence, so say it out loud.
- **The publish is the artefact.** `publish-check` in CI already publishes `RomMBat.UI` as
  self-contained single-file win-x64, and it will be the first thing Avalonia breaks if it breaks
  anything. The agent already costs 76 MB on a portable drive and the size argument is the entire
  reason the framework decision went the way it did, so **measure the published size** and report
  it, trimmed and untrimmed.
- **The UI and a background agent pass can be running at the same time.** A `quit` pass, a spawned
  flush, and a person in the menu are three writers. `TreeLock` is the existing answer and how the
  UI behaves when it cannot get the lock is a design decision, not an exception handler.
- **Windows refuses a file operation two ways and only one of them is an `IOException`.** A second
  handle gives `ERROR_SHARING_VIOLATION` and `IOException`; a still-mapped native library or a
  read-only file gives `ERROR_ACCESS_DENIED` and `UnauthorizedAccessException`, which does not
  derive from it. That is what turned `main` red after #87 merged and what #95 fixed, and #96 is
  the same mistake still open in product code. A UI running beside a `background` pass is exactly
  the shape that hits it. `TreeLock.cs:70-78` catches both and is the shape to copy.
- **A 200 from the ES API is not evidence anything happened.** Finding 208 is the proof. If the UI
  calls `/reloadgames` after installing games, prove it took effect rather than trusting the code.
- **Fail closed on the ES check**, as 7a established.
- **English only, no em-dashes, comments say why and stay short.**

## Design questions to put to me rather than pick

- **The stage cut above.** Argue for it, against it, or for a different one, with a recommendation.
- **How the UI talks to Core: in-process, or by spawning the agent?** Both projects already
  reference Core. In-process is the obvious answer and it puts a long download inside the process
  a user can close; spawning `rommbat-agent.exe` reuses seven milestones of tested command paths
  and gives progress a parsing problem. I lean in-process with the lock story made explicit.
- **Gamepad input.** Avalonia gives you keyboard and pointer, and a controller is neither. SDL2,
  XInput through P/Invoke, or something else, and what it costs in published bytes and in
  dependencies. This one is a measurement before it is an opinion; see below. Argosy answers none
  of it: it is Android and Kotlin, so its input stack does not transfer and only its conventions do.
- **Whether the UI may run standalone**, launched from a terminal with no ES up. It is how I would
  develop it, and if it is allowed then "ES is always up" stops being an invariant and becomes an
  assumption. Say what it does then, and note that the answer does not unlock writing
  `es_settings.cfg` under any circumstances.
- **Whether the UI calls `/reloadgames` after installing games**, so the couch shows the new game
  without a restart. 203 says it costs 209 ms and works. I think yes; tell me what breaks.
- **What the shell remembers between runs**, and in which store. `SettingStore` exists. If you
  conclude no migration `013` is needed, say so and say what you deliberately did not add.
- **Whether any of this earns a new skill**, or whether `retrobat-layout` absorbs it.

## Measure before you commit to a shape

Four probes. The first two can move the design rather than confirm it, so do them first.

- **Does a controller reach an Avalonia window at all, and how?** Plug in the pad I actually use,
  put up a window, and record what arrives: nothing, keyboard events, or raw HID. Then measure the
  chosen input path end to end, including whether it keeps working when the window is not focused.
  **Record the vendor id and the device name string the pad reports**, whatever input path you
  pick. That is the one measurement that turns Argosy's `ControllerDetector` from a lead into
  something checked against a real device, and it costs nothing to take while the pad is plugged in.
- **What happens to ES when a real window opens over it from the menu entry?** Z-order, focus, and
  the question that decides the whole input design: **does ES keep reading the gamepad while it is
  behind us?** If it does, every press moves its cursor underneath and the fix is not a UI concern.
  Also record what ES does on our exit, and confirm the orphan `game-end` lands as 118 describes.
- **Published size and cold start**, self-contained single-file win-x64, trimmed and untrimmed,
  against the agent's 76 MB as the baseline. Start time from launch to first frame on the real
  machine, not the dev box.
- **An unreachable server through the real UI path**, so the 2 s budget is a measurement here and
  not an inherited claim.

Quote the numbers you took and never one you did not. Where a measurement contradicts a document,
amend it in this PR and say which fact moved. Probe artifacts go in `probe-output/`, which is
gitignored; if a test needs one, check in the fixture.

**Probes that write into the real install need my say-so first, every time.** Tell me how to put
back anything you change. `K:` is the live install, **now on 8.2.1**, and it has been written to
twice since 7a: 86.md records the hooks and the menu entry it was left with, and #97's Flycast
probe ran three times against it on 2026-08-25 and cleaned up after itself by exact path. Read
both before assuming the tree is where 86.md left it.

## Tests the review will look for

`/review-pr` checks for the specific test, not for some test.

- **The UI cannot write `es_settings.cfg`**, asserted structurally rather than by convention: the
  UI's own code path never reaches `EsSettingsFile`, and a queued change is what a settings action
  produces. This is the most important test in the PR and it has to fail when someone undoes it.
- **Presentation holds no logic**: whatever boundary you choose, one test that fails when a
  decision migrates into a view model.
- Navigation is reachable with the gamepad map alone, screen by screen, at the view-model level.
- An unreachable server leaves every screen responsive, within the 2 s budget, and offline browse
  shows the local subset and says it is offline.
- A 401 mid-session drops to pairing with the database and outbox intact, per M1's rule.
- RomMBat's own launch does not become a play session.
- The lock case: the UI opening while a `background` pass holds `TreeLock` behaves as designed, and
  the design is asserted rather than described.
- No absolute path reaches whatever the UI persists, with its row in `LocalStoreTests`' bad-value
  table if a column is added.
- The relocation test still a clean no-op with the UI's state present.
- The publish still produces a single self-contained file, which is `publish-check`'s job, so make
  sure it is still doing it.

## Schema

Possibly nothing. If you do add one, expect `013` and the same discipline as `003` through `012`:
its header states what the existing shape could not carry and why, one migration for the stage, no
column holds an absolute path, CHECK constraints on anything path- or name-shaped, rebuild rather
than ALTER when adding a CHECK, and copy rows even when you are sure the table is empty.

## Working shape

Commits that stand alone and explain why, in the style of the M1 through M7 commits. Scoped diff,
no unrelated cleanups riding along: every extra file is review surface, and review surface is what
the next two sessions cost. A UI branch is the easiest place in this repo to smuggle in three
hundred lines nobody asked for, so hold the line harder than usual.

If part of this turns out to be a design question rather than a coding one, stop and ask me rather
than picking.

## What the next two sessions need from you

- A PR body on `.github/PULL_REQUEST_TEMPLATE.md`, with the AI disclosure stating extent, not just
  the fact, and the measurement table in the M3 through M7 bodies' shape.
- **`docs/PLAN.md`**: the 7b section moved to what was built, with the stage cut recorded if there
  is one, and the "Projects" paragraph updated to say what the framework decision actually cost now
  that it has been paid.
- **`docs/ARCHITECTURE.md`**: the `RomMBat.UI` section, the seam it uses into Core, and anything in
  §2 or §4 the branch falsifies.
- **`README.md`**: the status table, and the sentence about opening RomMBat from the ES menu, which
  currently describes something that exits 70. A screenshot if there is one worth having; ask me.
- **`DEVELOPER_SETUP.md`**: how to run the UI without a RetroBat in front of you, whatever the
  answer to the standalone question turns out to be.
- **Skills**: `retrobat-layout` gets the ES-focus and input rules if the probes produce any, and
  `offline-and-portable` gets whatever the UI's reachability behaviour adds. A rule that exists
  only because it was measured belongs in a skill or the next session re-derives it from nothing.
- Say which documents you moved and which you read and found already correct.
- **Commit this brief.** #87 left `.claude/commands/start-m7b.md` untracked on purpose, ruling that
  M7b had not started and it was not that branch's to track. It is yours, and every other milestone
  brief is in the tree.
- No scratch in the tree.
- NOTES seeded with this session's rulings, carrying forward anything still open from `86.md`,
  which is a long list this stage will mostly not shorten. Say that plainly rather than omitting it.
  Two items from the PRs since are open and neither is yours to close: the claims still recorded
  against RomM 5.1.1-beta, which now sit below the floor, and #87's honesty item about two `a3b`
  blocks never run against the live instance. Carry them rather than dropping them.
- The full `pre-pr-verification` skill run, plus `reference/verify.py`, with a plain statement of
  what you verified and what you did not. `dotnet build -c Release -warnaserror` is CI's build.
  **The baseline you inherit is 838 tests green at `7462f0c`, five CI checks green**, so a red run
  is yours. `trunk` runs through WSL here, it is not on the Windows PATH. Build and test from a
  fresh clone too.
- **The hands-on pass, which is the whole claim of this stage and cannot be substituted.** Not a
  screenshot: a controller, a real RetroBat at 8.2.1, and **the keyboard and mouse physically
  unplugged**.
  Open RomMBat from the ES menu, do everything the shipped cut claims to do, get back to ES, and
  launch a game. Anything you had to plug a keyboard in for is a defect, and naming it is worth
  more than a passing suite. If the session cannot take the pass, name the claims that are
  unproven for that reason.

## Default

Read the scope, then show me your reading, your proposed stage cut, and the measurement plan before
you write code, with the two design-moving probes first in it. That is the cheapest place for me to
correct you.

Commit locally as you go. Ask before pushing, before opening the PR, and before anything that
writes into the real RetroBat install or its configuration.
