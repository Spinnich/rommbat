---
description: Scout a RomM or RetroBat prerelease, or adopt a stable as the new floor, from its tracking issue
argument-hint: "<romm|retrobat> [tag]"
model: opus
effort: medium
---

# Scout or adopt an upstream release

PROJECT = the first word of $ARGUMENTS (`romm` or `retrobat`). TAG = the second, upstream's
own tag (`5.4.0-alpha.1`, `beta_8.3.0`, `8.3.0`).

Load `version-adoption` before anything else. `SKILL.md` holds both checklists and `delta.md`
the traps in reading a delta. This stops for the maintainer only for a design-of-record question,
a play session, or a merge ([workflow](../../docs/contributing/workflow.md)).

## 1. The issue and the target

Find the line's tracking issue:
`gh issue list --label upstream --state open --json number,title,body`. With no TAG, take the
newest release in the table of the only open issue for PROJECT, and ask with one multiple-choice
question when there are several. With a TAG and no issue, run `python3 tools/upstream_watch.py`
from the main checkout to open it; never write the issue by hand, because the watch has to be able
to find its table again.

Read the release on GitHub (`gh release view TAG -R <repo>`). **A release GitHub flags as a
prerelease is a scout. Anything else is an adoption.** A scout targets the newest prerelease of
the line, whatever TAG said, and says so when the two differ. Read the issue's earlier scout
comments: they are this pass's starting list.

## 2. Branch

After `git fetch origin`, run
`git worktree add -b upstream-<project>-<line> .claude/worktrees/upstream-<project>-<line> origin/main`
(`upstream-romm-5.4`) and `cd` into it once, as `/start-issue` does and for the same reasons. A
scout needs no branch until a finding needs code, and that code goes on its own issue's branch.

## 3. Run the track

- **Scout**: `version-adoption`'s "Scout: a prerelease", steps 1 to 8. The scratch refresh, the
  schema and every other capture go in the scratchpad, never the tree. The RetroBat smoke runs on
  the scout tree, which is the agent's to build and drive without asking. If `ROMMBAT_SCOUT_ROOT`
  is not in `.env`, say so, record step 6 as unproven, and carry on with the rest.
- **Adopt**: `version-adoption`'s "Adopt: a stable", steps 1 to 8, in that order. A re-run that
  needs a pad goes to the maintainer as a play sheet, the way `/certify` does it, and only for
  the rows the mapping says are touched.

A behaviour nobody has recorded is a new finding, filed the way `docs/upstream/README.md` says.

## 4. Record and ship

- **Scout**: post or edit the findings comment on the tracking issue, then file each code finding
  as its own issue citing the tracking issue. Offer to `/start-issue` the first one. Nothing is
  committed to this branch.
- **Adopt**: `pwsh -File tools/pre-pr.ps1 -Quiet`, then commit and open the PR the way
  `/start-issue` does. Label it `semver:minor` and the type `/start-issue` would give it. The body
  has the nine-step mapping per record, the AI disclosure, and `Fixes #<tracking issue>`. Then run
  `/drive-pr` on it.
