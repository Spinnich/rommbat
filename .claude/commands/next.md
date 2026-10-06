---
description: Rank the open issues, ask which to take, then run /start-issue on it
model: sonnet
effort: low
---

# Pick the next issue

1. `gh issue list --state open --limit 200 --json number,title,milestone,labels,createdAt` and
   `gh pr list --state open --json number,headRefName,body`. Drop an issue an open PR already
   names (`Fixes #`, `Closes #`, or `issue-<n>-` in the branch), and one labeled
   `needs-decision`.
2. Rank what is left:
   1. an `upstream` issue whose releases table lists a **stable**, because adopting it is due
      within one release;
   2. by milestone, the one due soonest first, when issues carry milestones;
   3. a defect that can lose a save, before any other defect;
   4. an `upstream` issue listing only prereleases, for its scout pass;
   5. certification in wave order (`platform-certification`, "Wave order"), a system at a time;
   6. the rest, oldest first.
3. Ask one multiple-choice question with the top four, your pick first and marked
   "(Recommended)", each with one line on why it ranks there.
4. Run `/start-issue` on the answer, `/certify <system>` when it is a `platform` issue titled
   `Certify <system>: ...`, or `/upstream <project>` when it is an `upstream` issue.
