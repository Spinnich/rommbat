---
name: pre-pr-verification
description: The checks that must pass before committing, opening a PR, or telling the user a change is done. Use when wrapping up any change.
---

# Pre-PR verification

## Always

`pwsh -File tools/pre-pr.ps1` runs every gate below, plus the hook and agent publish CI does
so the process-level tests run and CI's `tools/publish.ps1` package, and prints which failed.
In a git worktree it skips trunk, which cannot read one from WSL; CI's trunk check covers it.
`-Fix` runs `trunk fmt` first. `-Quiet` prints only each gate's name and the last 80 lines of
a gate that fails; an agent session runs it that way, so the full output stays out of its
context. By hand:

```bash
dotnet build -c Release -warnaserror --no-incremental   # what CI builds
dotnet test -c Release --no-build                       # full suite green, only after that build
trunk fmt && trunk check        # never commit with --no-verify
python3 -m unittest discover -s tools/docs   # the docs checker's own tests
python3 tools/docs/check.py     # links, anchors, fact citations, size budgets
mkdocs build --strict           # the guide in wiki/: its links, anchors and nav
cd reference && python3 verify.py
python3 tools/build-platform-map.py --check     # bundled data is what its generator emits
python3 tools/build-bios-manifest.py --check
git ls-files --eol -- '*.sh'    # every line must start i/lf
```

**Build with `--no-incremental`, because an incremental build can hide an error the previous one
reported.** On PR #196 a nullable warning (CS8602) failed one build under `-warnaserror`; a second
incremental build finished in a second with "0 Error(s)", and the tests passed against stale
binaries. Nothing in the tree escalates CS8602, so without `-warnaserror` it is only a warning, in
Debug or Release. Grep the output for `error` rather than trusting the summary line.

**`dotnet test` here is Microsoft.Testing.Platform, not VSTest**, opted in through
`global.json`, and it takes a different set of options. An option it does not recognize is
forwarded to the test module, which refuses it and reports **`Zero tests ran` with exit code
5**, naming neither the option nor the problem. `--nologo` does exactly this. **Read a zero-test
run as a bad command line, not as a broken environment**, and never as a pass.

**A `--filter` matching nothing in one of the two test projects fails the whole run**, because a
module that runs zero tests is an error: `dotnet test --filter "FullyQualifiedName~LivePairingTests"`
exits 8 with 4 of 4 passed, since `RomMBat.Agent.Tests` matched none. Scope a filtered run with
`--project tests/RomMBat.Tests` as well, or a run where everything you aimed at passed still
reports failure.

`verify.py` drifting means an upstream fact moved. **Revisit the docs and skills citing it; do
not just update the expected number.**

## Test cost

**CI's Test step is the budget, not your local run.** A GitHub Windows runner ran this suite
more than ten times slower than a dev box, and the difference was disk: until #227 the step took
11 minutes for a suite that ran in a minute locally, almost all of it SQLite commits waiting on a
flush, and afterwards it took 50 seconds. A test's cost on your machine says little about its
cost there.

- **Profile before you cut anything.** Rank tests by duration from the CI log, where every
  `passed` line carries one, or locally with the test exe's `-xml` (`docs/contributing/testing.md`).
  An audit of 1,244 test methods found almost no duplication. The number of tests was never the
  cost; a few expensive patterns were.
- **A store-backed test opens its store through `LocalStore.Open` or `OpenAt`.**
  `Support/ThrowawayStores` makes both cheap for the whole suite: a new file starts as a copy of
  one migrated seed, and commits are not flushed. A raw `SqliteConnection` skips both, so use one
  only to write the old-version file a migration test starts from, and give it `Pooling=False`:
  a pooled connection keeps the file open after its `using` ends, the tree's teardown cannot
  delete it, and `TempTreeLeakCheck` fails the run.
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

- Re-run a sync with no changes on the agent tree ("Hands-on by change type"): zero uploads,
  zero downloads, no gamelist churn.
- Exercise the offline path: switch the stub to unreachable mid-operation and confirm work
  either completes locally or queues, and that a later flush is idempotent under replay.
- **If save logic changed**, a real emulator must have written a real save or state of the
  affected shape, and RomMBat must have handled it, through EmulationStation and back. Drive
  **every emulator the system offers and every save option RetroBat exposes within each** (PCSX2's
  memory card type, including folder cards), because the shape varies by both and one
  combination says nothing about the others. List the emulators and cores from `es_systems.cfg`
  and the save-affecting options from `es_features.cfg` before sitting down. That is **not** a
  certification and must not be recorded as one: it covers the changed shape, not the nine steps.

  A session that cannot do it, because it is non-interactive or has no permission to touch a
  real install, **says which claims are unproven for that reason** rather than letting the test
  suite stand in for evidence. Naming the gap is acceptable; implying it is closed is not.

## When the change touches a platform

Run the full `platform-certification` checklist. A platform is not done at eight of nine.

## Documentation parity

`docs/design/` is the design of record: the principles, and one record per standing decision
under `docs/design/decisions/`. It is not the only place the change can falsify. Work the table, and grep rather than remember: search the
docs for the terms the diff touches (the command name, the class, the table, the version).

| The diff contains                                            | Then re-read, and correct what it falsifies                                                                 |
| ------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------- |
| A new or changed subcommand, flag, or user-visible output    | The guide page in `wiki/` that names it                                                                     |
| A gamepad screen's rows, labels, buttons or wording          | The guide page in `wiki/` that walks through that screen                                                    |
| A new page in `wiki/`                                        | Its entry in `nav` in `mkdocs.yml`, which `mkdocs build --strict` fails without                             |
| A change to `rommbat-agent --help`                           | Regenerate `wiki/reference/cli.md` (`wiki/README.md`); `CliReferencePageTests` fails until you do           |
| A save shape, class or platform that now syncs, or stops     | `README.md`'s warning and feature list, and the guide's `wiki/saves/` page for it                           |
| A row certified, driven, or owed after a floor move          | Its row in `data/certification.json`, then regenerate `wiki/platforms/index.md` (`wiki/README.md`)          |
| A finding that changes which emulator to pick, BIOS or saves | The system's guide page, `wiki/platforms/<system>.md`, which a system's first certified row owes            |
| A migration, table or column                                 | `docs/architecture/local-store.md`: its table, and the out-of-`saves/` folders below it                     |
| Sync protocol, the save or state model, attribution, hashing | `docs/architecture/saves.md` and the `save-sync` skill                                                      |
| A rule that only exists because something was measured       | The skill for that area, plus its fact in `docs/upstream/`, and a `docs/design/decisions/` record it amends |
| A milestone or stage changing state                          | The GitHub milestone and issue that track it                                                                |
| A minimum RomM or RetroBat version                           | The `version-adoption` skill, "Adopt: a stable"                                                             |
| A new project, folder, probe set or bundled data file        | `docs/architecture/projects.md` and `reference-data.md`                                                     |
| A folder, type or trap a nested `CLAUDE.md` names            | That project's `CLAUDE.md`, and the routing table in the root `CLAUDE.md`                                   |

Three rules that keep this from becoming its own scope creep:

- **Correct the sentences the change falsifies.** Do not rewrite a document to sound current.
- **A stale claim is a defect at the same severity as the bug it describes.** "Directory saves
  do not sync yet" in a release that syncs them is wrong in the same way a wrong return value is.
- **Say what you checked.** The PR body names the docs that moved and the ones you read and
  found already correct. "Docs unchanged" with no statement is indistinguishable from not looking.

## When the change moves the supported RomM or RetroBat version

The `version-adoption` skill owns it: "Adopt: a stable" is the checklist, and a prerelease never
moves the floor. A PR that moves a floor and skips that checklist is not done, whatever the tests
say.

## Hands-on by change type

**A change a user can see or a server can receive is driven on a real install before it is
done**, not only tested. The tests prove the mechanism; a hands-on pass proves the shipped build
does it. The install is the agent tree, `ROMMBAT_AGENT_ROOT` in `.env`, which is the agent's to
deploy to and drive without asking, and the kit is `tools/handson/` (its README has the how).
Start with `Test-HandsOnEnv -Gui`, then `Publish-ToAgentTree`, so the pass runs this branch's build.

| The diff changes                     | Owed, on the agent tree with the deployed build                                                                                                   |
| ------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| A gamepad screen                     | Walk every changed state with `Send-Key`, screenshot each, and read the screenshots. Include the empty, offline and error states the change moves |
| A CLI command or its output          | Run it with `Invoke-Agent` and quote what it printed                                                                                              |
| Sync, hooks, the spool, ES           | `Start-ES`, launch a game, `Stop-ES`. The spool drains, `background.log` shows both passes, `status` shows the session, and a re-sync is a no-op  |
| Pairing or a call to the server      | `Connect-AgentTree -Repair`, or the changed call made against the real server                                                                     |
| Save logic                           | `/certify <system> --hands-on <PR>`, as "When the change touches sync" says                                                                       |
| Docs, tests, CI or dev tooling alone | Nothing                                                                                                                                           |

A diff in two rows owes both. **A pass that cannot run is named, never skipped silently**: a
disconnected session, ES or an emulator already running outside `/certify` (the kit refuses to
take the screen then, because the maintainer may be playing), or a server down. Say which claims that leaves
unproven. The PR body's **Hands-on** section says what was driven, on which build (the line in
`emulators/rommbat/deployed.txt`), and what each screenshot or output showed.

## Before claiming done

State plainly what was verified and what was not. Never claim a platform works without
having launched a game on it. If something was skipped, say so and why.

## PR description

- Base it on the repo's PR template.
- One type label, from CONTRIBUTING's "Labels and release notes" table. The release notes
  are grouped by it.
- **Disclose AI assistance and its extent.** RomM requires this and RomMBat inherits it.
  Non-negotiable.
- Link the issue: `Fixes #NNNN` for bugs, `Closes #NNNN` for features.
- Note any change to the minimum supported RomM or RetroBat version.
- One `semver:major`, `semver:minor` or `semver:patch` label, as
  `docs/design/decisions/versioning.md` defines them, so the release that collects the PR can
  pick its bump. A floor move or a store migration is a minor; a change that makes the user
  re-pair or redefine sets is a major; docs and CI are a patch. A major also says in the body
  what breaks and what the user does about it, for the release notes.
