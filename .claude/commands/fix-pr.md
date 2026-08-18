---
description: Triage and fix review feedback and CI failures on an open PR
argument-hint: "[PR number] [notes path]"
---

# Respond to review feedback on a PR

Run after the PR is open, whenever CI fails or a review lands. Fresh session each round.
Repeat as needed, but see "Don't circle": this loop has a stop condition and reaching it is
part of the job.

---

Variables for this run:

- PR = $1 (if that is empty, resolve it from the current branch)
- NOTES = $2 (round log for this PR, e.g. `~/rommbat-work/<PR>.md`. The repo has no
  convention for this, so keep it outside the tree. If none exists, start one and tell me
  where.)

## Who is who

I am Spinnich, the only human on this repo. Everything else in this loop is Claude: Claude
wrote the branch, a fresh Claude session reviewed it, and now you are fixing it. That
changes the weight of each input, and getting this backwards is how the loop goes bad.

- **CI cannot be argued with.** Green or it does not land.
- **The repo's own rules are the standing authority**: CLAUDE.md, CONTRIBUTING.md,
  docs/PLAN.md, and the skills in `.claude/skills/`. A finding that cites one of those is
  presumptively right. A finding that cites nothing is an opinion from a session that had
  less context than you do.
- **The review pass is peer output, not a verdict.** It ran fresh so it would not grade its
  own homework, which also means it never saw the reasoning behind the code. Rule on every
  finding. Obey none of them on authority.
- **My comments are decisions.** Do them. If you think I am wrong, say so once with
  evidence, and if I repeat myself, do it my way and note the disagreement in NOTES.

Skip the contributor etiquette. No thanking me for the review, no "good catch", no
apologising. Short, specific, evidence first.

## Gather

```bash
gh pr checks $PR
gh pr view $PR --comments
gh api repos/Spinnich/rommbat/pulls/$PR/comments   # inline comments, absent from pr view
gh api repos/Spinnich/rommbat/pulls/$PR/reviews    # review verdicts
gh run view <run-id> --log-failed                  # for each failing check
```

Inline comments do not appear in `gh pr view`, so don't skip that third command. A review
run in a terminal may never have been posted to GitHub at all; if I have pasted one in this
session, that is the review. If there is neither, ask before guessing at what to fix.

Then read NOTES for what earlier rounds already ruled on, and the skill matching the area
the diff touches.

## Triage

Three buckets. Give me the counts before you change a line.

**1. CI failures.** Not negotiable.

- Local `dotnet build` is Debug; CI builds Release with `-warnaserror`. Reproduce with
  `dotnet build -c Release -warnaserror` before theorising.
- `publish-check` is its own failure class: code that compiles but cannot be published
  self-contained single-file. Reflection, trimming, `Assembly.Location`. Reproduce with the
  publish command from `build.yml`, not with a plain build.
- `trunk-check` and `reference-verify` run on ubuntu while you are on Windows. The usual
  divergences are line endings against `.gitattributes` (`reference/**` and
  `tests/**/fixtures/**` are byte-exact `-text`; `.bat`, `.cmd`, `.ps1` are crlf), path
  casing, and python3.12.
- `reference-verify` failing means an upstream fact moved. Never edit a vendored file or an
  expected number to make it pass. Work out which fact moved, and bring it to me as a
  `docs/PLAN.md` question.
- If main has moved under the branch, rebase and say so.

**2. Findings from the review pass.** Rule each one: valid, partially valid, or wrong, with
the reason. A wrong finding gets a written rebuttal, not a code change.

Changing correct code to satisfy a reviewer that has no more authority than you do is the
failure mode I care most about, and this repo is more exposed to it than most, because both
sides of the exchange are the same model. A rebuttal needs evidence: a test that already
covers the case, a line of PLAN.md, a rule in CLAUDE.md, or the actual behaviour of the
code. "I don't think that's a problem" is not a ruling.

The reverse trap is just as real: "the reviewer misunderstood" is not a general purpose
escape hatch. If you use it twice in one round, you are probably wrong once.

**3. My comments.** Every thread gets a response, including ones you end up disagreeing
with.

## Don't circle

The failure mode of this loop is not a missed bug, it is two Claude sessions trading polish
until the diff is unrecognisable and nothing is more correct. Guard against it:

- **A ruling is durable.** A finding ruled wrong in an earlier round stays wrong. Re-open
  it only on new evidence, and name the evidence.
- **Only this PR's code is in scope.** Findings against code the branch did not touch are
  follow-up issues, however true. Say so and move on. **A doc this branch falsified is in
  scope**, though, even though the file is untouched: the diff is what made the sentence wrong,
  so correcting it belongs here rather than in a follow-up that leaves every reader in between
  misinformed. CLAUDE.md, and the trigger table in `pre-pr-verification`.
- **Taste is not a finding.** If no rule in CLAUDE.md, CONTRIBUTING.md, PLAN.md, or a skill
  states it, and it is not a correctness, security, or performance defect, it is a
  preference. Log it, don't act on it.
- **Third round on the same file means the design is wrong, not the code.** Stop patching,
  state the design question, and ask me.
- **Reach the stop condition and say so plainly.** CI green, every blocking finding fixed
  or ruled with reasons, every thread answered: that is done. Say "this PR is done" instead
  of proposing another review pass. A round that produces only non-blocking nits on code
  the previous round already rewrote is the signal to stop, not to keep going.

## Fix

A test first where the feedback describes a real defect. Scoped changes only, no unrelated
cleanups riding along; scope creep is the mechanism by which this loop never ends. If a
finding needs a change bigger than the PR's current scope, or reaches into work a later
milestone owns, propose a follow-up issue instead of growing this one.

If a finding shows the plan is wrong rather than the code, amend `docs/PLAN.md` in this PR
and say what moved. That is the established pattern here. Deviating from the plan without
amending it is not.

The plan is not the only document the round can falsify, and a round that fixes behaviour
almost always moves one of the other four. Walk the `pre-pr-verification` trigger table before
pushing: `README.md`, `docs/ARCHITECTURE.md`, `DEVELOPER_SETUP.md`, and the skill for the area.
A rule this round learned the hard way, from hardware or from a real install, goes into that
skill in this PR; a commit message is not somewhere a later session reads.

If the fix touches a platform, re-run the certification checklist for it. A fix does not
inherit the previous round's certification, and nothing is certified without a game having
been launched on it.

## Before pushing

Re-run the whole `pre-pr-verification` skill, not just the check that failed. The six rules
apply to the fix as much as to the original: absolute paths, emulator INIs, extension and
BIOS authority, hooks staying off the network, ConnectTimeout and TaskCanceledException
classification, committed DTOs. Then state what you verified and what you did not.

## Commits

New commits on top. Do not amend or force-push over reviewed history, so the next pass can
see exactly what changed since the last one. Reference the finding being addressed in the
message where it isn't obvious from the diff.

## Reply drafts

One code block per thread, ready to paste. Short, specific, pointing at the commit that
addressed it. Where you are disagreeing, lead with the evidence and let the conclusion
follow.

The PR body already discloses that this is Claude Code work, so replies do not each need a
disclaimer. If I am pasting a round of them verbatim, one note in the thread covers it.

## Record

Append a round to NOTES: what each item asked for, the ruling, what changed and in which
commit, what you pushed back on and with what evidence, and what is still open. The ledger
of rulings is what stops the next round from re-litigating this one, and it is what I read
when I come back to this three days later.

## Default

Read-only: no pushes, no comments posted with gh. Show me the commits and the replies
first.
