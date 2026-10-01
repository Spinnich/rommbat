---
description: Rank the open issues, ask which to take, then run /start-issue on it
---

# Pick the next issue

1. `gh issue list --state open --limit 200 --json number,title,milestone,labels,createdAt` and
   `gh pr list --state open --json number,headRefName,body`. Drop an issue an open PR already
   names (`Fixes #`, `Closes #`, or `issue-<n>-` in the branch), and one labelled
   `needs-decision`.
2. Rank what is left:
   1. by milestone, the one due soonest first, when issues carry milestones;
   2. a defect that can lose a save, before any other defect;
   3. certification in wave order (`platform-certification`, "Wave order"), a system at a time;
   4. the rest, oldest first.
3. Ask one multiple-choice question with the top four, your pick first and marked
   "(Recommended)", each with one line on why it ranks there.
4. Run `/start-issue` on the answer, or `/certify <system>` when it is a `platform` issue titled
   `Certify <system>: ...`.
