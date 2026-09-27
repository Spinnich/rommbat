---
description: Run one pr-reviewer round on a PR, for forks, Dependabot or a second opinion
argument-hint: "<PR number>"
---

# One review round

PR = $1. If empty, take the PR for the current branch (`gh pr view --json number`).

Use this where `/drive-pr` is not wanted: a PR from a fork, a Dependabot bump, or a second
opinion on a PR someone else is driving. It never stops for the maintainer and changes no code.

1. Count the `<!-- rommbat-review` markers on the PR. The round is that count plus one.
2. Spawn the `pr-reviewer` agent with the prompt `PR <n>, round <r>` and wait for it. It posts
   the round as one comment on the PR.
3. Report its verdict and the comment's URL, and nothing else. Do not rule on the findings, fix
   anything or touch the ledger: that is `/drive-pr`'s job.

On a PR from a fork the reviewer reads CI's results and never runs the branch, because its code
would run as the maintainer.
