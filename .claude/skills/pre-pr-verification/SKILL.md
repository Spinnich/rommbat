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

**A `--filter` matching nothing in one of the two test projects fails the whole run**, because a
module that runs zero tests is an error: `dotnet test --filter "FullyQualifiedName~LivePairingTests"`
exits 8 with 4 of 4 passed, since `RomMBat.Agent.Tests` matched none. Scope a filtered run with
`--project tests/RomMBat.Tests` as well, or a run where everything you aimed at passed still
reports failure.

`verify.py` drifting means an upstream fact moved. **Revisit `docs/PLAN.md`; do not just
update the expected number.**

## Test cost

**CI's Test step is the budget, not your local run.** A GitHub Windows runner ran this suite
more than ten times slower than a dev box, and the difference was disk: until #227 the step took
11 minutes for a suite that ran in a minute locally, almost all of it SQLite commits waiting on a
flush, and afterwards it took 50 seconds. A test's cost on your machine says little about its
cost there.

- **Profile before you cut anything.** Rank tests by duration from the CI log, where every
  `passed` line carries one, or locally with the test exe's `-xml` (see `DEVELOPER_SETUP.md`).
  An audit of 1,244 test methods found almost no duplication. The number of tests was never the
  cost; a few expensive patterns were.
- **A store-backed test opens its store through `LocalStore.Open` or `OpenAt`.**
  `Support/ThrowawayStores` makes both cheap for the whole suite: a new file starts as a copy of
  one migrated seed, and commits are not flushed. A raw `SqliteConnection` skips both, so use one
  only to write the old-version file a migration test starts from.
- **Use `TestTimeProvider` rather than the wall clock.** A test that has to prove something
  never happened waits its full budget, so run those waits side by side rather than one after
  another. `HookSpawnTests` fires both no-spawn hooks and then waits once.
- **Compare the PR's Test step with main's** (`gh run list --workflow Build --branch main`) when
  the change adds process-level, live or waiting tests. If it grew by a minute or more, find out
  why before merging.

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
| A minimum RomM or RetroBat version                           | The whole version-move checklist below                                                               |
| A new project, folder, probe set or bundled data file        | `README.md` repository layout, `docs/ARCHITECTURE.md` §2 and §3                                      |

Three rules that keep this from becoming its own scope creep:

- **Correct the sentences the change falsifies.** Do not rewrite a document to sound current.
- **A stale claim is a defect at the same severity as the bug it describes.** "Directory saves
  do not sync yet" in a release that syncs them is wrong in the same way a wrong return value is.
- **Say what you checked.** The PR body names the docs that moved and the ones you read and
  found already correct. "Docs unchanged" with no statement is indistinguishable from not looking.

## When the change moves the supported RomM or RetroBat version

RomMBat tracks the newest stable of both rather than supporting a range, so this happens on
a schedule rather than by accident. A version move is not a find-and-replace: some numbers in
this repo are the **current** supported version and must move, and some are the version a
measurement was taken on and must not.

1. `reference/refresh.sh`, then resolve every drift it reports rather than editing the
   expected number. A drift is a signal to revisit `docs/PLAN.md`.
2. Read the upstream changelog end to end, not just the entry you came for. Anything touching
   a rule in `docs/retrobat-findings.md` is the reason this step exists.
3. Move together, or the startup check disagrees with the README: `RetroBatVersion.Minimum`,
   `RetroBatVersion.LastTested`, `RetroBatRoot.MinimumVersion`, the `README.md` requirements
   table and the compatibility row. A test asserts the first three agree.
4. Re-check every open issue in `docs/retrobat-findings.md`. A fix upstream changes what
   RomMBat should do; **no workaround comes out until a hands-on pass has seen the fixed
   behaviour.** A changelog line is upstream's belief, not a measurement.
5. Leave provenance alone. `docs/retrobat-findings.md`'s header, `data/retrobat/*.json`'s
   `_retrobat_version`, and a live capture under `tests/.../fixtures/` all record the version
   something was **measured on**. Rewriting those to the new number silently reattributes a
   measurement to a build nobody ran it against.
6. A RomM move also moves the pinned OpenAPI schema, which is the minimum version on purpose.
   `RomMServerVersion.Minimum`, `RomMServerVersion.LastTested` and the `info.version` inside
   the pinned file move in one commit, and a test reads the pin rather than restating it so a
   pin that moves without the floor fails there. See `src/RomM.Client/openapi/README.md`.
7. **If the target is a prerelease, adopt the newest one and re-check it before the PR opens.**
   Prereleases supersede each other in hours: `5.3.0-alpha.2` shipped eight hours after
   `5.3.0-alpha.1`. Read the delta between the two rather than assuming it is cosmetic, because
   it decides which of your findings were attributed to the wrong tag. The pin cannot come from
   the public demo, which carries stable only, so it comes from a self-hosted instance whose
   `SYSTEM.VERSION` you **read at capture time**: a live library can be upgraded underneath the
   work, and a capture that does not match the floor is the wrong artifact even when it parses.
   **Take the file list from the trees, not from the compare endpoint.**
   `GET /repos/{owner}/{repo}/compare/{a}...{b}` caps `files` at 300 and says nothing in the
   payload when it truncates, so a delta that size reads as complete while hiding whichever
   files sort last. `alpha.3` to `beta.1` hit exactly 300 and hid the browser save writer, which
   a finding turned on. Diff blob shas from `git/trees/{ref}?recursive=1` at both tags instead.
8. **Map the move onto every record in `docs/platforms/`.** Each record gets the nine steps
   marked touched or carried, step 9 always touched, a one-line reason for each carried step,
   and the touched ones re-run or recorded as owed at the new floor. The rule and what counts
   as touched are in the `platform-certification` skill, "When the floor moves".

## Before claiming done

State plainly what was verified and what was not. Never claim a platform works without
having launched a game on it. If something was skipped, say so and why.

## PR description

- Base it on the repo's PR template.
- **Disclose AI assistance and its extent.** RomM requires this and RomMBat inherits it.
  Non-negotiable.
- Link the issue: `Fixes #NNNN` for bugs, `Closes #NNNN` for features.
- Note any change to the minimum supported RomM or RetroBat version.
