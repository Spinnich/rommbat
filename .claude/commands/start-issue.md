---
description: Rule on an issue against main and, if it is real, fix it through to a merge-ready PR
argument-hint: "<issue number>"
---

# Start an issue

ISSUE = $1.

This runs from an issue to a merge-ready PR. It stops for the maintainer only for a ruling on a
verdict other than FIX, a design-of-record question, or a hands-on pass
([workflow](../../docs/contributing/workflow.md)).

## Step 0: is it still real?

An issue is a claim from a session with less context than you have now, usually one that found
the thing and was told not to fix it yet. Its quoted code has often moved. Read it
(`gh issue view $ISSUE --comments`), any PR it came from, and the code on `origin/main`.

**A verdict cites the code on main today**, quoted as `file:line`, or the commit that changed it.
Never rule from the issue's own text. Two failures are symmetric: fixing behaviour the code no
longer has, and closing a live defect because it is old or awkward.

| Verdict       | Means                                                          | Evidence                                                    | Then                                 |
| ------------- | -------------------------------------------------------------- | ----------------------------------------------------------- | ------------------------------------ |
| FIX           | Still true, and worth fixing now                               | The current code, quoted                                    | Carry on below                       |
| CLOSE-FIXED   | A later change fixed it                                        | The commit, plus the current code                           | Close it yourself, citing the commit |
| CLOSE-INVALID | It was never true                                              | The code, and what the issue misread. Needs the most        | Ask                                  |
| KEEP-DEFERRED | True, and the issue names a condition not yet met              | The condition, and why it is not met                        | Ask                                  |
| KEEP-BLOCKED  | Settling it needs a measurement or hardware this session lacks | Exactly what would settle it, and whether you could take it | Ask                                  |

A CLOSE-FIXED close is `gh issue close $ISSUE --reason completed --comment "<commit and one or two
sentences>"`. Check whether that commit left a doc describing the old behaviour; if so, the doc
fix is this issue's work and the verdict is FIX.

Every other non-FIX verdict goes to the maintainer as one multiple-choice question, verdict and
evidence in the question, recommended option first. Do not comment on or close the issue until
they answer.

## Branch

`EnterWorktree` with the name `issue-$ISSUE-<slug>`: a worktree under `.claude/worktrees/`, on a
new branch off `origin/main`. Never work on main; the pre-push hook refuses it anyway.

## Change

1. Load what the `CLAUDE.md` routing table names for each area the fix touches, and nothing more.
2. **Decide and disclose.** A reversible choice inside the change is yours: make it, and list it
   in the PR body under "Decisions for veto". A design-of-record question (anything the docs under
   `docs/` or a skill would have to change to record) is the maintainer's. Batch every such
   question into one `AskUserQuestion` call before writing the code it decides.
3. A failing test first, where the issue is a defect. The test asserts the mechanism, not the
   summary line.
4. The fix, scoped to the issue. A cleanup it tempts you into is a follow-up issue.
5. Docs travel with the code: work `pre-pr-verification`'s "Documentation parity" table and
   correct every sentence the change falsifies.
6. **A save-logic change owes a hands-on pass** of the shape it touches, on every emulator and
   save option that writes it. Schedule it with `/certify <system> --hands-on <PR>` once the PR
   is open. That stops for play. If it cannot happen, the PR body names which claims are unproven.
7. `pwsh -File tools/pre-pr.ps1`. Every gate green, apart from trunk, which a worktree skips.

## PR

Commit in the repo's style: an imperative subject that says what changed and why, the attribution
trailer, no em-dashes. `git push -u origin HEAD`, then `gh pr create --body-file <file>` on
`.github/PULL_REQUEST_TEMPLATE.md`:

- the AI disclosure, stating the extent;
- `Fixes #$ISSUE` for a bug, `Closes #$ISSUE` for a feature;
- the Step 0 verdict and its evidence, in two lines;
- "Decisions for veto", listing every reversible choice you made alone;
- which docs moved, and which you read and found already correct;
- what was verified and what was not.

Then run `/drive-pr` on the new PR.
