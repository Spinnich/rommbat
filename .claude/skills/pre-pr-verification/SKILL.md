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

**`dotnet test` here is Microsoft.Testing.Platform, not VSTest**, opted in through
`global.json`, and it takes a different set of options. An option it does not recognise is
forwarded to the test module, which refuses it and reports **`Zero tests ran` with exit code
5**, naming neither the option nor the problem. `--nologo` does exactly this. **Read a zero-test
run as a bad command line, not as a broken environment**, and never as a pass.

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

## Documentation parity

`docs/PLAN.md` is the design of record and is usually the one that gets amended. It is not the
only document the change can falsify. Work the table, and grep rather than remember: search the
docs for the terms the diff touches (the command name, the class, the table, the version).

| The diff contains                                            | Then re-read, and correct what it falsifies                                                          |
| ------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------- |
| A new or changed subcommand, flag, or user-visible output    | `README.md` command blocks and the prose around them, `DEVELOPER_SETUP.md` examples                  |
| A save shape, class or platform that now syncs, or stops     | `README.md`: the pre-release warning, "What it does", the status and stage tables                    |
| A migration, table or column                                 | `docs/ARCHITECTURE.md` §4, both the table and the count of migrations in the paragraph               |
| Sync protocol, the save or state model, attribution, hashing | `docs/ARCHITECTURE.md` §9 and the `save-sync` skill                                                  |
| A rule that only exists because something was measured       | The skill for that area, plus `docs/retrobat-findings.md`, and `docs/PLAN.md` if it amends a reading |
| A milestone or stage changing state                          | The stage tables in `README.md` and `docs/PLAN.md`, which are separate and both go stale             |
| A minimum RomM or RetroBat version                           | `README.md` requirements and compatibility tables, and the startup check                             |
| A new project, folder, probe set or bundled data file        | `README.md` repository layout, `docs/ARCHITECTURE.md` §2 and §3                                      |

Three rules that keep this from becoming its own scope creep:

- **Correct the sentences the change falsifies.** Do not rewrite a document to sound current.
- **A stale claim is a defect at the same severity as the bug it describes.** "Directory saves
  do not sync yet" in a release that syncs them is wrong in the same way a wrong return value is.
- **Say what you checked.** The PR body names the docs that moved and the ones you read and
  found already correct. "Docs unchanged" with no statement is indistinguishable from not looking.

## Before claiming done

State plainly what was verified and what was not. Never claim a platform works without
having launched a game on it. If something was skipped, say so and why.

## PR description

- Base it on the repo's PR template.
- **Disclose AI assistance and its extent.** RomM requires this and RomMBat inherits it.
  Non-negotiable.
- Link the issue: `Fixes #NNNN` for bugs, `Closes #NNNN` for features.
- Note any change to the minimum supported RomM or RetroBat version.
