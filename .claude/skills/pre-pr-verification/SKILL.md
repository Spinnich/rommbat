---
name: pre-pr-verification
description: The checks that must pass before committing, opening a PR, or telling the user a change is done. Use when wrapping up any change.
---

# Pre-PR verification

## Always

```bash
dotnet build                    # no warnings introduced
dotnet test                     # full suite green
trunk fmt && trunk check        # never commit with --no-verify
cd reference && python3 verify.py
```

`verify.py` drifting means an upstream fact moved. **Revisit `docs/PLAN.md`; do not just
update the expected number.**

## Invariants worth re-checking by hand

- No absolute path reaches the database. Three layers enforce it (the `RelativePath` type,
  a `CHECK` on every path column, and `LocalStoreTests` binding the two to one table of bad
  values). A new path column needs its `CHECK` and a row in that test.
- No emulator INI was written. Configuration goes through `es_settings.cfg`.
- Nothing was written outside the RetroBat tree.
- No secret, token or instance URL is in the diff.
- Every new user-visible string is reachable without a mouse.

## When the change touches sync

- Re-run a sync with no changes: zero uploads, zero downloads, no gamelist churn.
- Exercise the offline path: switch the stub to unreachable mid-operation and confirm work
  either completes locally or queues, and that a later flush is idempotent under replay.
- **If save logic changed**, one real emulator must have written one real save or state of the
  affected shape, and RomMBat must have handled it. That is **not** a certification and must
  not be recorded as one: the wave rollout starts after M7, because every pass needs a person
  launching games and the gamepad UI is what makes that bearable. It is one game, one emulator,
  one shape, through EmulationStation and back.

  A session that cannot do it, because it is non-interactive or has no permission to touch a
  real install, **says which claims are unproven for that reason** rather than letting the test
  suite stand in for evidence. Naming the gap is acceptable; implying it is closed is not.

## When the change touches a platform

Run the full `platform-certification` checklist. A platform is not done at eight of nine.

## Before claiming done

State plainly what was verified and what was not. Never claim a platform works without
having launched a game on it. If something was skipped, say so and why.

## PR description

- Base it on the repo's PR template.
- **Disclose AI assistance and its extent.** RomM requires this and RomMBat inherits it.
  Non-negotiable.
- Link the issue: `Fixes #NNNN` for bugs, `Closes #NNNN` for features.
- Note any change to the minimum supported RomM or RetroBat version.
