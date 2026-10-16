---
description: Plan a feature or investigate a bug in plan mode, check for duplicate and conflicting issues, and file a GitHub issue for /start-issue
argument-hint: "<feature idea or bug symptom>"
model: opus
effort: medium
---

# Plan an issue

TOPIC = $ARGUMENTS.

This turns an idea or a symptom into one well-evidenced GitHub issue that a fresh session can
take with `/start-issue`. It writes no code, makes no branch, and commits nothing. It stops for
the maintainer for design questions, for the plan's approval, and for any change to an existing
issue ([workflow](../../docs/contributing/workflow.md)).

## Step 0: enter plan mode

Call `EnterPlanMode` first. Everything until step 5 is read-only, and approving the plan is the
permission to file the issue. If TOPIC is empty, ask what to plan before entering.

## Step 1: classify

A **bug** is an investigation: find the mechanism, do not fix it. A **feature** is a design: fit
it to the project, do not build it. State which you chose in one line. Ask only when the answer
would change the research.

## Step 2: sweep for duplicates and conflicts

Run this before reading code, with several keyword sets drawn from TOPIC (the symptom, the
component, the user-visible word):

- `gh issue list --state all --limit 100 --search "<keywords>" --json number,title,state,labels,closedAt`.
  A closed `wontfix` or `invalid` issue is a conflict. A closed fix that matches a bug is a
  possible regression.
- `gh issue list --state open --label needs-decision` and `--label upstream`: either can block or
  subsume the idea.
- `gh pr list --state open --json number,title,headRefName,body` and
  `gh pr list --state merged --limit 30 --search "<keywords>"`, plus `git worktree list`. An open
  PR that names the issue (`Fixes #`, `Closes #`) or a branch `issue-<n>-` is work in flight.
- [docs/design/principles.md](../../docs/design/principles.md), the six rules in `CLAUDE.md`, and
  [docs/design/decisions/](../../docs/design/decisions/README.md): a recorded decision the idea
  contradicts is a conflict.

Classify every hit as **duplicate**, **overlaps**, **conflicts**, **stale** (already fixed on
main, superseded, or no longer true) or **unrelated**. A duplicate ends the planning: say which
issue it is, and go to step 4 to offer a comment on it instead of a new issue.

## Step 3: investigate on main

`git fetch origin`, then read the code as it is on `origin/main` (`git show origin/main:<path>`, or
the checkout when it is clean and level with it). Load what the `CLAUDE.md` routing table names for
the area, and nothing more. Use Explore agents for breadth (up to three) and a Plan agent for a
feature's design.

- **Bug:** reproduce it by reading. Name the mechanism and quote it as `file:line` at a commit
  sha. Say what stays unproven and what measurement or hands-on pass would settle it
  (`pre-pr-verification`, "Hands-on by change type"). Do not take that pass here.
- **Feature:** test it against the principles, the version floor, the offline and portable rules
  (`offline-and-portable`), and platform scope. List every design-of-record question: anything
  a doc under `docs/` or a skill would have to change to record.

Never rule from an issue's own text, or from a session's memory of the code. Quote what main says
today.

## Step 4: ask

One `AskUserQuestion` call, recommended option first, related decisions batched:

- each design-of-record question from step 3;
- one question per **stale**, **duplicate** or **overlapping** existing issue, with the evidence
  in the question. Options: close it as fixed (citing the commit), close it as a duplicate or
  superseded (linking the new issue), comment on it with the new findings, narrow its scope, or
  leave it. Closing as fixed is still the maintainer's call here, because the sweep is not
  `/start-issue`'s Step 0.

A reversible choice with a conventional default is not a question: make it and list it in the
issue under "Settled".

## Step 5: present the plan and exit

Write the issue draft to the plan file, plus what the sweep checked and found, and call
`ExitPlanMode`. The draft is the title, the labels and the body below. The maintainer's approval
is the go-ahead to file. When the sweep ended on a duplicate, the draft is the comment for that
issue instead, and step 6 files nothing new.

## Step 6: file

1. Write the body to a file in the scratchpad.
   `gh issue create --title "<title>" --label <bug|enhancement|platform> --body-file <file>`. One
   type label, as CONTRIBUTING's "Labels and release notes" has it: `platform` alone for a
   certified row or a system's support, otherwise `bug` or `enhancement`. Do not set `semver:*`;
   `/start-issue` picks it from the diff. Skip this when the sweep ended on a duplicate.
2. Apply what the maintainer approved on existing issues, linking the new number when there is one:
   `gh issue close <n> --reason completed|"not planned" --comment "<why, with the commit or #new>"`,
   `gh issue comment <n> --body "<findings>"`, or `gh issue edit <n>`. Do nothing they did not
   approve.
3. Print the new issue's URL and the next step: `/start-issue <n>`. Do not start it.

## The issue body

Written for `/start-issue`'s Step 0, which rules on code at `file:line`, so it must stand on its
own for a session with no memory of this one. Headings, not the templates' bold labels, in this
order:

- **Problem** (bug: what happens, expected versus actual, versions and the affected system) or
  **Goal** (feature: the need, and who has it).
- **Evidence:** the code on main, quoted as `file:line`, and the sha it was read at.
- **Mechanism** (bug) or **Approach** (feature): what to change, naming the files, tests and docs
  it touches. A bug's mechanism is marked confirmed or suspected.
- **Settled** and **Open:** decisions made, with the record they rest on, and questions that are
  the maintainer's. An open question stays open in the issue; do not guess it.
- **Related:** duplicates ruled out, overlaps and conflicts as `#n` links, and why each does or
  does not block this one. An issue labeled `needs-decision` belongs here as a blocker.
- **Verification owed:** the tests, the hands-on row, and what stays unproven without play.
- **Out of scope:** what `/start-issue` should file as a follow-up instead.
- The AI-assistance disclosure, stating that Claude Code planned it and did not fix it.

House style: American English, no em-dashes, and no secrets, tokens or install paths
([docs/contributing/writing.md](../../docs/contributing/writing.md)).
