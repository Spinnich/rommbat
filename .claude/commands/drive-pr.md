---
description: Move an open PR forward until it is merge-ready, fixing CI and running review rounds
argument-hint: "<PR number>"
---

# Drive a PR to merge-ready

Idempotent: run it on a PR in any state and it takes the next step, then the one after, until
the PR is merge-ready or needs the maintainer. Running it twice does no harm.

PR = $1. If empty, take the PR for the current branch (`gh pr view --json number`).

The maintainer's jobs are decide, play and merge. Stop only for one of those. Everything else,
including pushes to this PR's branch and comments on it, you do without asking
([workflow](../../docs/contributing/workflow.md)).

## Who decides what

- **CI cannot be argued with.** Green, or it does not land.
- **The repo's rules are the standing authority**: `CLAUDE.md`, the skills, `docs/`. A finding
  citing one is presumptively right. A finding citing nothing is an opinion.
- **The reviewer is a peer, not a verdict.** It ran fresh so it would not grade its own homework,
  which also means it never saw why the code is the way it is. Rule on every finding and obey
  none on authority. Changing correct code to satisfy it is the failure this loop is most exposed
  to, because both sides are the same model.
- **The maintainer's comments on the PR are decisions.** Do them. If you think one is wrong, say
  so once, with evidence, in the ledger.

## The ledger

One comment on the PR holds every ruling. It starts with `<!-- rommbat-ledger -->` and is edited
in place, never reposted. Create it on the first run:

```markdown
<!-- rommbat-ledger -->

## Ledger

| ID  | Severity | Ruling | Evidence or commit |
| --- | -------- | ------ | ------------------ |

Round: 0. Decisions made without the maintainer, for veto: none yet.
```

Edit it with `gh api -X PATCH repos/{owner}/{repo}/issues/comments/<id> -F body=@<file>`, the
body written to a file first ([Windows hazards](../../docs/contributing/windows-agent-hazards.md)).
Find it with `gh api -X GET repos/{owner}/{repo}/issues/<PR>/comments --paginate`.

Four rulings. Each needs its evidence in the row.

| Ruling     | Means                                                                                    |
| ---------- | ---------------------------------------------------------------------------------------- |
| FIXED      | Changed. The row names the commit                                                        |
| REJECTED   | Wrong. The row names the test, rule, doc line or behaviour that shows it                 |
| DEFERRED   | Real but not this PR's: large or unrelated. The row names the follow-up issue you opened |
| MAINTAINER | A design-of-record question. Goes to the maintainer as a multiple-choice question        |

A ruling is durable. A REJECTED finding stays rejected unless a later round brings new evidence,
and the row says what that evidence was.

## Each run

1. **State.** `gh pr view $PR --json state,isDraft,mergeable,headRefName,labels`,
   `gh pr checks $PR`, the ledger, every review comment (`<!-- rommbat-review`), and every
   comment and inline review thread from the maintainer (`issues/<PR>/comments` and
   `pulls/<PR>/comments`) that the ledger or a reply does not already answer. A closed or merged PR: say so and stop.
   No type label from CONTRIBUTING's "Labels and release notes" table: add the one the diff fits,
   and list it in the ledger for veto.
2. **Check out.** Work in the PR's branch, in its worktree if one exists (`git worktree list`).
3. **Behind main?** If the PR conflicts, or main moved under a file it touches, rebase onto
   `origin/main` and `git push --force-with-lease`. Put the old head sha in the ledger so the next
   round can `git range-diff` it. Otherwise leave history alone: new commits on top, no amend.
4. **CI.** Wait for it (`gh pr checks $PR --watch`). Fix every failure before the review round,
   because a review of a red branch is wasted. The failure classes:
   - `build`: Release with `-warnaserror`. Reproduce with `tools/pre-pr.ps1`, never a plain build.
   - `publish-check`: compiles but cannot publish single-file (reflection, trimming,
     `Assembly.Location`). Reproduce with the publish command in `build.yml`.
   - `trunk-check`, `docs-check`, `reference-verify`, `line-endings` run on Linux. The usual
     divergence is line endings against `.gitattributes`, or a new file never `git add`ed, which
     `check.py` accepts locally.
   - `guide`: `mkdocs build --strict` on the pages in `wiki/`, usually a link or anchor that
     leaves `wiki/` or names nothing. Reproduce with the `guide` gate of `tools/pre-pr.ps1`.
   - `reference-verify` drifting means an upstream fact moved. Never edit a vendored file or an
     expected number. That is a MAINTAINER question.
5. **Review round.** The round is the count of `<!-- rommbat-review` markers on the PR plus
   one, the count `/review-pr` uses, so a round it ran in between is not reused. Set the
   ledger's round to it. Spawn the `pr-reviewer` agent with the prompt `PR <n>, round <r>` and
   wait for it. It posts its own comment.
6. **Rule.** Every finding gets a row. Roll the reviewer's small pre-existing items that sit in
   files this PR edits into this PR; open an issue for each large or unrelated one (never labelled
   `good first issue`). Reply on each of the maintainer's threads.
7. **Fix.** A failing test first, where the finding is a defect. Scoped changes only: no cleanup
   rides along. A fix that falsifies a doc corrects it in the same commit (`pre-pr-verification`,
   "Documentation parity"). A fix to save logic owes the hands-on pass for that shape: run
   `/certify <system> --hands-on $PR`, or record in the ledger which claims are unproven. Then
   `tools/pre-pr.ps1`, commit naming the finding IDs, push, update the ledger.
8. **Decide the next step.**
   - Anything FIXED this round changed the code, so go to 3 for the next round.
   - An open MAINTAINER row: **needs the maintainer**, whatever else holds.
   - Nothing blocking left unruled, nothing FIXED this round, CI green: **merge-ready**.
   - A third round would be needed: stop and escalate. Three rounds means the design is wrong,
     not the code. State the design question.
   - The reviewer re-raised a REJECTED finding with new evidence and you still disagree after
     round 2: stop and ask.

## Stopping

**Merge-ready.** Remove `needs-decision`, add `ready-to-merge`, and write the ledger's final line:
"Merge-ready at `<sha>`: CI green, every finding ruled." Then auto-merge if, and only if, the PR
is one of these and every file fits:

- Docs only: every file under `docs/` or `wiki/`, or `README.md`, `DEVELOPER_SETUP.md`,
  `CONTRIBUTING.md`. Anything under `.claude/`, any `CLAUDE.md` or `AGENTS.md` is not docs only.
- Tests only: every file under `tests/`.
- A Dependabot patch or minor bump.

Auto-merge is `gh pr merge $PR --auto --merge`. Every other PR waits for the maintainer to merge.

**Needs the maintainer.** Add `needs-decision`, then ask one multiple-choice question with
`AskUserQuestion`, batching every open MAINTAINER row, recommended option first. Record the
answer in the ledger and carry on from step 7.

Either way, send a push notification leading with what they would act on: "PR 251 ready to
merge" or "PR 251: one design question on slot naming".

## Tone

No etiquette: no thanks, no "good catch", no apology. Evidence first, then the conclusion.
The PR body already discloses AI assistance, so replies do not repeat it.

## Before claiming done

State what was verified and what was not, in the ledger's last line or the handoff. A hands-on
pass that did not happen is named as a gap, never implied.
