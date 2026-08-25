---
description: Start M7 stage 7a, closing the loop between EmulationStation and RomM
argument-hint: "[branch name]"
---

# Start M7 stage 7a: close the loop

Fresh session, branched off main. This is the session that writes the branch. What follows it is
`/review-pr` in a second fresh session and `/fix-pr` in a third, so the job is not "working code",
it is a branch that survives a reviewer who will have far less context than you and the repo's own
rules as its standing authority.

M6 closed with PR #77. **This stage ships no user interface at all**, which is the point of cutting
it out of M7: everything here is provable from EmulationStation with the agent alone, and none of
it has to wait on a framework choice.

---

Variables for this run:

- BRANCH = $1, default `m7a-close-the-loop`. Branch off main and stay off it; the pre-push hook
  will stop you anyway.
- NOTES = `~/rommbat-work/<PR>.md`, started once the PR number exists. `~/rommbat-work/77.md` is
  2c's ledger, `35.md` is 2b's. Read 77 in full before designing; it carries what M6 left open.

## Who is who

I am Spinnich, the only human on this repo. Claude writes the branch, a fresh Claude reviews it, a
third fixes it. You are the first of the three and the only one that will ever hold the reasoning
behind the code.

Anything true only in your head is invisible to the review, and the review is allowed to rule
against it. Put every non-obvious decision somewhere durable: a commit message, a line of
`docs/PLAN.md`, or a test that fails when someone undoes it.

Skip the preamble and the progress narration. Show me decisions, measurements, and diffs.

## Why this stage exists

The question that opened M7's planning was whether EmulationStation is what a user actually lives
in after a sync, or whether that was only ever an aspiration. **It is what has been built.** ROMs
land in `roms/<system>/` filtered against the live `<extension>` list, M4 writes merged gamelists
and artwork, `/reloadgames` makes ES see them, games launch through RetroBat's own
`emulatorLauncher`, and the hooks record the play session that attributes a save.

**And the loop does not close.** Three facts, all already in the repository:

1. The hooks write a spool file and exit. **Nothing drains it** except `sync` or a person typing
   `flush`. `FlushCommand`'s own doc comment says so, and `docs/ARCHITECTURE.md` names it "M7's
   call". A user who never opens a terminal accumulates saves and play sessions forever.
2. **No `es_menu` writer exists.** Grep `src` for `es_menu` and the hits are a path exclusion in
   `EsSystemsFile`, a launch-classification check in `LaunchLog`, and comments. **Nothing creates
   an entry.** The registration shape was measured at M0 and has stayed prose for seven milestones.
3. The UI is a stub returning `EX_SOFTWARE`, so there is nothing for a menu entry to open yet.
   That is 7b's problem and deliberately not yours: **the entry can point at a stub**, and proving
   the entry works is worth more than waiting for something behind it.

## Read before you design

1. **CLAUDE.md**, in full. **Rule 4 is this stage's sharpest edge and this stage narrows it.** It
   reads "The ES hooks never touch the network" and gives its reason in the next sentence: _they
   run inside the game-launch path_. `start` fires when ES starts and `quit` when it exits, and
   neither is in that path. Rule 1, never persist an absolute path, binds the menu entry and the
   new table.
2. **`docs/PLAN.md`**: M7 in full, core principle 1 on offline-first, principle 4 on portability
   and the `emulators/rommbat/` location it was forced into, and the risks table rows on hooks, on
   `es_settings.cfg` written under a running ES, and on shared containers.
3. **`docs/retrobat-findings.md`**, probe 4 in full: the `.menu` format, the three-variant path
   test that refuted `plugins/`, and "`es_menu` is an ordinary ES system, and registration takes
   two files". Then probe 1 on hook arguments and concurrency, probe 7b on why hooks are
   executables, finding 118 on ES-menu launches being identifiable, and findings 178, 179 and 195.
4. **`~/rommbat-work/77.md`**, in full and past the review, then `35.md`. Do not re-litigate a
   ruling in there without saying you are doing it and why.
5. **Skills**: `retrobat-layout` and `offline-and-portable` fully, then `save-sync` for the
   conversion queue, and `pre-pr-verification` before claiming anything is done.
6. **The code you are building on**: `EsHooks` (the model for the menu writer, including its
   append-don't-replace rule and its byte comparison), `RomMBat.Hook/Program.cs` (all 38 lines of
   it), `GamelistDocument` and `GamelistSync` (the existing answer to "merge into a file ES owns"),
   `EsSettingsFile`, `EmulationStationProcess`, `EmulationStationClient`, `SpoolDrain`, `Spool`,
   `SpoolRecord`, `FlushCommand`, `TreeLock`, `SaveConverter` and `SaveConversionStore`. Read the
   headers of `010` and `011` before you write `012`.

## Scope

Three things, and they belong in one PR because the third depends on the second.

### 1. The ES menu entry

A new `EsMenuEntry` in `src/RomMBat.Core/RetroBat/`, modelled directly on `EsHooks`:
`Install`, `Uninstall`, `IsInstalled`, an outcome record naming every path it touched, idempotent,
and only ever removing RomMBat's own files. A `menu install|uninstall|status` subcommand mirrors
`HooksCommand`, and `sync` installs it the way it installs the hooks, announced, naming every path.

Registration is **two** files, not one:

- `system/es_menu/rommbat.menu`, plain text, line 1 the executable. Paths resolve under
  `emulators\` and `..\` escapes are refused, so the line is `\rommbat\RomMBat.exe`.
- A `<game>` element in `system/es_menu/gamelist.xml` pointing at `./rommbat.menu`. **Merge, never
  clobber**, through `GamelistDocument`. Probe 7 recorded that ES did **not** rewrite this
  particular gamelist across two sessions, which makes it the gentler case and not a free one.

Artwork: read RetroBat's shipped entries for the real convention and dimensions rather than
guessing, then **tell me exactly what you need and I will provide it**. A placeholder is fine and
is better than no `<image>`, because an entry with no image shows as a bare filename.

### 2. The background pass

A `background <event>` subcommand: the pass a hook spawns. Named separately from `flush` because it
does more than flush, and because a hook-spawned pass should be greppable as one.

- `background start` → the flush pass, quiet. `flush --quiet` already exists and was written for
  exactly this.
- `background quit` → poll `EmulationStationProcess.Check` until ES is really gone, bounded and
  giving up rather than hanging, then apply queued config changes, then the flush pass.

`RomMBat.Hook/Program.cs` gains **one branch**: after committing the spool record, if the event is
`start` or `quit`, spawn `<root>/emulators/rommbat/rommbat-agent.exe background <event>` detached,
`UseShellExecute=false`, `CreateNoWindow=true`, no wait. **Nothing is spawned for `game-start` or
`game-end`.** The hook still opens no socket, takes no lock and touches no database, so the comment
at the top of that file is amended rather than deleted.

### 3. Queued configuration changes

`saves convert --apply` writes `es_settings.cfg` and refuses while ES is running, because ES
serialises a model loaded at startup and discards anything written underneath it. **The UI in 7b is
launched from the ES menu, so it always runs under a live ES**, and it can therefore never write
that file. It needs somewhere to put the intent instead.

A migration `012` adding a pending-config table: the target `(system, rom)`, the keys and values,
why, when it was queued, and what the prior state was. `saves convert` gains an `--at-quit` form
that queues instead of writing; `background quit` drains it once ES is confirmed gone; `--revert`
cancels a queued change that has not applied yet. The result of an applied change has to be
readable afterwards, because 7b's UI has to be able to say what happened while it was not running.

**Done when**: hooks and menu entry installed by `sync`, RomMBat visible in the ES menu with its
name and artwork, a game played and exited and ES quit **with no terminal touched at any point**,
and the play session and the save both in RomM afterwards. Then a conversion queued with
`--at-quit` applies at the next ES quit and not before.

**Out of scope**: the UI itself, any framework package, browse, and packaging. The menu entry
points at a stub in this PR and that is correct.

## The rules that bite in this stage specifically

- **Rule 4 is narrowed here, not bent.** Say so in the diff. `game-start` and `game-end` spawn
  nothing and that has to be enforced by a test, not by a comment, because the next person editing
  the hook will not have read this.
- **`CreateNoWindow` is load-bearing.** The agent is a console app and ES is fullscreen. A console
  window flashing over it at every boot is a defect, not a cosmetic issue.
- **The hook stays dependency-free.** It is 12.8 MB because it compiles three types from Core
  rather than referencing it. `Process.Start` needs nothing new; if your design wants a fourth
  type, say why it is worth the size.
- **Merge, never clobber**, in `system/es_menu/gamelist.xml`. Somebody else's entries live there.
- **Only ever remove RomMBat's own files.** `EsHooks.Uninstall` is the precedent, and its comment
  explains why the folder itself is never touched.
- **Never persist an absolute path**, including in the new table and in the menu file.
- **Offline is a working state.** A `quit` pass with the server unreachable leaves everything
  queued and is idempotent on replay.
- **A 200 from the ES API is not evidence anything happened**, and `EmulationStationProcess`
  deliberately does not use that API to answer "is ES running". Do not undo that.
- **Fail closed on the ES check.** A process whose path cannot be read counts as running, and the
  cost of guessing wrong here is applying a config change while ES is up, which is silently lost.

## Design questions to put to me rather than pick

- **What `background quit` should do if ES never exits** inside its budget: leave everything
  queued and exit, or flush anyway and leave only the config queued. The second is probably right
  and I want to be told rather than to discover it.
- **Whether `background start` should do anything beyond a flush**, given a boot is the one moment
  a machine is reliably idle. A full `sync` there would download content unasked, which I think is
  wrong, but say what you considered.
- **Whether the menu entry should be installed by `sync`** the way the hooks are, or should stay an
  explicit `menu install`. The hooks' argument was that a hook changes nothing about how a game
  runs; a menu entry adds a visible item to my front end, which is a slightly different claim.
- **What the queued-config table's relationship to `save_conversion` is**: a second table, a state
  column on the first, or something else. `010`'s header will tell you what it was built to carry.
- **Whether `menu status` should report an entry a user edited by hand**, and what it does about
  it, which is the same "do not take over a key the user set" rule 2c established for settings.

## Measure before you commit to a shape

Two probes, both cheap, both able to move the design rather than only confirm it:

- **Does a `.menu` added while ES is running appear after `GET /reloadgames`, or does it need a
  restart?** The answer decides whether `sync` can tell me the entry is ready or has to tell me to
  restart the front end. `es_menu` is an ordinary ES system, so `/reloadgames` ought to pick it up,
  and "ought to" is not a measurement.
- **Does the `quit` hook fire before or after ES writes `es_settings.cfg` on exit?** This is the
  race the whole apply-at-quit design sits on. If ES writes the file after the hook fires, then
  polling for the process to exit is not merely tidy, it is the only correct order, and the budget
  has to cover ES's own shutdown write.

Quote the numbers you took and never one you did not. Where a measurement contradicts a document,
amend it in this PR and say which fact moved. Probe artifacts go in `probe-output/`, which is
gitignored; if a test needs one, check in the fixture.

**Probes that write into the real install need my say-so first, every time.** This stage's probes
write into my EmulationStation menu and my hook folder. Tell me how to put back anything you change.

## Tests the review will look for

`/review-pr` checks for the specific test, not for some test.

- The hook spawns for `start` and `quit` and **not** for `game-start` or `game-end`. This is the
  rule-4 boundary and it is the single most important test in the PR.
- Menu install is idempotent; a second run reports "current" and rewrites nothing.
- Menu install merges into a `system/es_menu/gamelist.xml` that already holds entries, and leaves
  every one of them byte-identical.
- Uninstall removes RomMBat's two files and nothing else, and reports absent cleanly when there is
  nothing to remove.
- The queued-config table round-trips, a revert cancels an unapplied change, and applying records a
  result something else can read.
- No absolute path reaches the new table: its `CHECK` constraint plus a row in `LocalStoreTests`'
  table of bad values.
- The offline simulation extended to the `quit` pass: unreachable server, everything queues, replay
  is idempotent.
- The relocation test, now with a menu entry installed, still a clean no-op.
- A re-run of `sync` with no changes produces no churn in `system/es_menu/gamelist.xml` either.

## Schema

Expect migration `012`, and the same discipline as `003` through `011`: its header states what the
existing shape could not carry and why, one migration for the stage, no column holds an absolute
path, CHECK constraints on anything path- or name-shaped, rebuild rather than ALTER when adding a
CHECK, and copy rows even when you are sure the table is empty. If you conclude no new table is
needed and a column on `save_conversion` does the job, say that, and say what you deliberately did
not add.

## Working shape

Commits that stand alone and explain why, in the style of the M1 through M6 commits. Scoped diff,
no unrelated cleanups riding along: every extra file is review surface, and review surface is what
the next two sessions cost.

If part of this turns out to be a design question rather than a coding one, stop and ask me rather
than picking.

## What the next two sessions need from you

- A PR body on `.github/PULL_REQUEST_TEMPLATE.md`, with the AI disclosure stating extent, not just
  the fact, and the measurement table in the M3 through M6 bodies' shape.
- **`docs/PLAN.md`**: the M7 section rewritten to the three-stage shape, and the framework decision
  recorded in "Projects" even though no framework lands here, so 7b does not reopen it.
- **`CLAUDE.md`**: rule 4 narrowed to name the game-launch path explicitly, since this PR is what
  makes the distinction load-bearing.
- **`docs/ARCHITECTURE.md`**: §2's subcommand table gains `menu` and `background`; §4 gains the new
  table and its migration count moves; and the paragraph that currently says nothing invokes
  `flush` is now false and is the whole point of the PR.
- **`README.md`**: the status table, and the command blocks, and the sentence that says `sync`
  installs the hooks, which now installs a menu entry too.
- **`.claude/skills/retrobat-layout/SKILL.md`**: the two-file `.menu` registration rule and the
  apply-at-quit window. Both exist only because they were measured, so the skill is where they go
  or the next session re-derives them.
- Say which documents you moved and which you read and found already correct.
- No scratch in the tree.
- NOTES seeded with this session's rulings, carrying forward anything still open from `77.md`.
- The full `pre-pr-verification` skill run, plus `reference/verify.py`, with a plain statement of
  what you verified and what you did not. `dotnet build -c Release -warnaserror` is CI's build.
  `trunk` runs through WSL here, it is not on the Windows PATH. Build and test from a fresh clone
  too.
- **The hands-on pass, which is the whole claim of this stage and cannot be substituted**: a real
  RetroBat, a real game, played and exited, ES quit, and the session and save in RomM, with a
  terminal used at no point in the sequence. If the session cannot take one, name the claims that
  are unproven for that reason rather than letting the suite read as evidence.

## Default

Read the scope, then show me your reading plus the measurement plan before you write code, with the
quit-ordering probe first in it, because it can move the design rather than only confirm it. That
is the cheapest place for me to correct you.

Commit locally as you go. Ask before pushing, before opening the PR, and before anything that
writes into the real RetroBat install or its configuration.
