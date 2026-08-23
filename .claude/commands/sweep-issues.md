---
description: Triage every open issue, fix what is still real, close what is stale
argument-hint: "[branch name]"
---

# Sweep the open issues

Fresh session, branched off main. This is the session that writes the branch, so what follows it
is `/review-pr` in a second fresh session and `/fix-pr` in a third. The job is not "close some
issues", it is a branch whose every verdict survives a reviewer who will re-read the same code
with none of your reasoning.

There are 15 open issues as of 2026-08-18, spanning four milestones. They were written by Claude
sessions in review of PRs #9, #26, #32 and #35. Some describe code that three milestones have
since rewritten. Some argue in their own text against being fixed yet. Deciding which is which is
most of this task; the code changes are the smaller half.

---

Variables for this run:

- BRANCH = $1, default `issue-sweep-after-m6-stage-2b`. Branch off main and stay off it; the
  pre-push hook will stop you anyway.
- NOTES = `~/rommbat-work/<PR>.md`, started once the PR number exists. The ledgers from the PRs
  these issues came out of are in the same folder: `30.md`, `32.md` and whatever #35 left. Several
  of these issues are items those ledgers carried forward, and the ledger says why they were
  carried rather than fixed.

## Who is who

I am Spinnich, the only human on this repo. Every open issue was written by a Claude session, most
of them by a review session that found the thing and was told not to fix it in that round. That
makes an issue a claim from a session with less context than you have now, not a work order.

You are allowed to rule an issue wrong. You are not allowed to rule it wrong quietly.

Skip the preamble and the progress narration. Show me verdicts, evidence, and diffs.

## The two failure modes, which are symmetric

- **Manufacturing work.** Implementing a fix for behaviour the code no longer has, or for
  something the issue's own text says to defer until a condition is met. The diff looks like
  progress and buys nothing.
- **Dodging work.** Closing a live defect as stale because it is old, awkward, or needs a
  measurement you would rather not take.

One rule prevents both: **a verdict cites the code on main today**, quoted with `file:line`, or
the commit that changed it. Never rule on an issue from the issue's own text. Half of them quote
code that has since moved.

## Ask me this first, before you write anything

**What the cut is.** Give me a table of all 15 with a proposed verdict each, and your proposed
split into PRs. Then stop. Candidate shape, not a decision:

- The self-contained fixes, small and provable from the test suite.
- `#24` on its own, because it is around 101 call sites across 12 files and a mechanical diff that
  big buries anything shipped beside it.
- `#29` on its own, because it starts with a project-layout decision that is mine.
- The measurement-blocked ones, which may produce a probe and a corrected document rather than a
  code change.

Say for each which of the five verdicts below it gets and what your evidence was. That table is
the cheapest place for me to correct you, and it becomes the spine of the PR body.

## Five verdicts, not two

Each has its own evidence bar. State the verdict and the evidence in one or two lines per issue.

1. **FIX.** Still true on main, worth fixing now, in scope for this branch. Evidence: the current
   code, quoted.
2. **CLOSE-FIXED.** A later milestone already fixed it. Evidence: the commit that did it, plus the
   current code. Check whether that commit also left a doc claiming the old behaviour.
3. **CLOSE-INVALID.** It was never true, or rests on a reading the code does not support. Evidence:
   the code, and what the issue misread. This is the verdict most likely to be wrong, so it is the
   one that needs the most.
4. **KEEP-DEFERRED.** Still true, and the issue itself names the condition under which it becomes
   worth doing. Not a fix and not a close. Evidence: the condition, and whether it has been met.
5. **KEEP-BLOCKED.** Still open, and settling it needs a measurement, a probe, or hardware this
   session cannot reach. Say exactly what would settle it, and whether you could take it.

An issue can be KEEP-DEFERRED on the code and still owe a doc correction in this PR. See #38.

## The inventory, and the trap in each

Verify all of this rather than inheriting it. It is what I know, not what is true.

**From review of #9, M3, 2026-08-10. Three milestones have landed on this code since.**

- `#10` prune orphaned `.part` files from `emulators/rommbat/partial/`. Keys on set membership, not
  age. Note the live `content_download` row case, which is an interrupted transfer and not an orphan.
- `#11` carry `IsMultiFile` on `SyncSetMember`. Touches the sync-set schema, and its own text says it
  may belong with the multi-file extraction work instead. Check whether M4 or M6 changed the resolver
  in a way that moves the answer.
- `#12` `EvictionPlanner.Plan` does the full candidate walk when `target <= 0`. M6 widened `SaveGuard`
  to four questions and then to class C, so the cost this describes has grown since it was written.

**From the xunit.v3 move, #25.**

- `#24` adopt xUnit1051, dropping the `NoWarn` last. The site count in the issue predates M5 and M6,
  so recount before quoting it.

**From review of #26, M5, 2026-08-16.**

- `#27` `bios <folder>` with an unknown name reads as a clean bill of health. The issue proposes the
  three-outcome table and says the check belongs at argument-read time, not in the planner.
- `#28` `reference/refresh.sh` does not run the two generators' `--check`. Check rather than
  regenerate, deliberately, and the issue says why.
- `#29` no test project references `RomMBat.Agent`. Ten uncovered command gates, and it names the
  real defect that gap hid in #26. **Starts with a decision for me**: a second test project or a
  `ProjectReference` on the existing one.

**From the stage 2a era, 2026-08-17.**

- `#33` `es_savestates.cfg` `<core>` overrides parsed but never applied.
- `#34` bigpemu three-digit slots against a two-digit template.
- **Both have no body.** The body is the literal string `@-`, from a `gh issue create` that ate its
  stdin. You have the title and nothing else, so these two need reconstructing from the code and
  from `retrobat-layout` plus `save-sync` before they can be ruled on at all. If they turn out to be
  real, they also need their bodies written, which is a `gh issue edit` and therefore something you
  ask me about first.

**From review of #35, M6 stage 2b, 2026-08-18. One day old, so the prior is that all six are live.**

- `#36` `RomIndex.InFolder` linear prefix scan, once per system. **The issue argues against fixing
  it now**: fine at every size measured, worth doing when there is a library big enough to measure
  the difference. Ruling FIX here means overriding the session that found it, which needs a reason.
- `#37` the sidecar route splits the native name on the first underscore. No counter-example known.
  What would close it is a measurement pass over a real install's sidecars, not a speculative
  system-aware split. Note the second half, sidecar last-wins against header first-wins, which is a
  real inconsistency and settleable without a counter-example.
- `#38` the class C restore's move loop is not atomic. Note what the issue says last: the class doc
  on `SaveUnitTransfer` and the remark on `SaveSync.RestoreUnitAsync` **both call the restore atomic
  today, and that is false right now**. Whatever the code verdict is, those two sentences are a
  doc-parity defect this PR owes under CLAUDE.md. A whole-container swap is wrong, because the
  container is shared across games; read that paragraph before designing anything.
- `#39` `overwrite=true` appended rather than replacing. One `curl` observation against RomM
  5.1.1-beta.2, with `autocleanup` unsent and a different `device_id`, so neither variable was held.
  **The `save-sync` skill and `StubRomMServer.Saves.cs` both encode the opposite**, and a stub that
  models the server wrongly cannot catch the divergence. The issue specifies the probe:
  `tools/m6-probes/`, alongside probe 6, four postings into one slot with `autocleanup` off. Taking
  that probe is the highest-value thing in this sweep, because it either corrects two artefacts or
  retires the doubt.
- `#40` the own-upload download skip can never fire for a bundled save, because the fold and the
  server digest are different functions by construction. Costs a transfer, not a save. Depends on
  `dcc2dbb` having made the slot row current after a restore, so check that first.
- `#41` `LiveCatalogTests` share one store and the sync-config round trip fails on test order. **CI
  never sees this**, because it has no credentials. It also means you can only prove the fix if this
  machine has live credentials in the environment; say plainly whether you ran the live suite and
  what it did. The issue asks for the other live classes to be checked for the same shape.

## Read before you rule

1. **CLAUDE.md** in full, including the six rules, the doc-parity rule and the disclosure rule.
2. **The skill for each issue's area**: `save-sync` for #37 through #41, `retrobat-layout` for #33
   and #34, `offline-and-portable` for #10, `platform-mapping` where a mapping is in play,
   `pre-pr-verification` before claiming anything.
3. **The PR each issue came out of**, and the ledger for it in `~/rommbat-work/`. An issue that a
   ledger says was carried forward twice already has a history of being deferred, which is
   information about it.
4. **`docs/PLAN.md`** only where an issue touches a decision it records. It is 1800 lines.
5. **The code**, which is the only authority here. Every quoted snippet in every issue predates at
   least one merge.

## Closing an issue is outward facing

Anything that writes to GitHub waits for me: `gh issue close`, `gh issue comment`, `gh issue edit`,
and posting the PR. Draft the text, show me, wait.

A close comment states the evidence and stops: the commit or the current code, one or two
sentences, no thanks and no summary of the issue back at its author. Use `completed` for something
that is now fixed and `not planned` for something that was never real, and reach for the `wontfix`
or `invalid` labels only if I say so. `Closes #N` in the PR body belongs only on issues this branch
actually fixes; a close for staleness is a separate action and should not ride in on a merge.

## Rules that bite in a sweep specifically

- **One commit per issue**, naming what changed and why in the repo's commit style. A sweep is the
  easiest branch in the world to make unreviewable, and the per-issue commit is what keeps the next
  two sessions able to rule on each piece separately.
- **No refactor rides along.** If a fix wants a cleanup of surrounding code, that is a follow-up
  issue, even in a PR whose whole subject is follow-up issues.
- **A test that fails without the fix**, per issue. #12 and #10 both describe defects invisible in
  the output, so the test has to assert the mechanism, not the summary.
- **The six rules apply to every fix.** #10 walks the filesystem and #38 builds paths in a shared
  container, so rule 1 is live in both. Nothing absolute reaches the database.
- **Never edit a vendored file or an expected number in `verify.py`.** #28 touches `refresh.sh`,
  which is the closest this branch gets to that line.
- **If a fix turns into a design question, stop and ask me** rather than picking. On a sweep I would
  rather be asked twice than told once.

## Verification

Run the whole `pre-pr-verification` skill, not the parts that seem relevant.

```bash
dotnet build -c Release -warnaserror   # CI's build; a plain build is Debug and hides warnings
dotnet test
trunk fmt && trunk check               # trunk runs through WSL here, not on the Windows PATH
cd reference && python3 verify.py
```

Also build and test from a fresh clone, which is what catches a `.gitignore`-swallowed fixture
locally instead of in CI. If anything touched save logic, take one hands-on pass on the shape it
touches, per `docs/platforms/README.md`, and if you cannot, name the claims that are unproven for
that reason rather than letting the suite stand in for evidence.

Walk the `pre-pr-verification` doc-parity trigger table before pushing. This branch is unusually
likely to owe a doc: #38 has two false sentences in the code's own remarks today, #39 may correct a
line in the `save-sync` skill, #28 changes what a documented script does, and #27 changes
user-visible output, which is a `README.md` question.

## What the next two sessions need from you

- A PR body on `.github/PULL_REQUEST_TEMPLATE.md`, with the AI disclosure stating extent rather
  than just the fact, and the triage table as its spine: every one of the 15, its verdict, and one
  line of evidence. The issues this branch does not touch matter as much as the ones it does, and
  the review session should not have to re-derive why.
- `Closes #N` for each issue actually fixed here, and nothing else.
- Draft comments for every issue I am being asked to close, and for every issue staying open where
  the sweep learned something worth recording on it.
- Every deviation from `docs/PLAN.md` amended in the plan, in this PR.
- The other four documents brought with it, per the trigger table, with a statement of which you
  moved and which you read and found already correct.
- No scratch in the tree. Probe artefacts go in `probe-output/`, which is gitignored; if a test
  needs one, check in the fixture.
- NOTES seeded with the rulings this session made, including the ones where you overrode an issue's
  own recommendation and what the reason was.
- A plain statement of what you verified and what you did not.

## Default

Answer the cut question first, with the table. Read-only until I answer it. Then commit locally as
you go, and ask before pushing, before opening the PR, before writing to any issue on GitHub, and
before anything that writes into the real RetroBat install.
