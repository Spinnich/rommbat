---
name: version-adoption
description: Scouting a RomM or RetroBat prerelease and adopting a stable, the two tracks that move RomMBat's supported version. Use when an `upstream` tracking issue is open, when running /upstream, or when touching RomMServerVersion, RetroBatVersion, the pinned OpenAPI schema or reference/refresh.sh.
---

# Version adoption

RomMBat's floor is the newest **stable** RomM and RetroBat, adopted within one RomMBat release of
upstream shipping it ([version compatibility](../../../docs/design/version-compatibility.md),
[decision](../../../docs/design/decisions/stable-only-floor.md)). An upstream prerelease never
becomes the floor. It gets a scout pass, so that by the time its stable ships the work adoption
owes is already known and mostly landed.

| Upstream ships | Track     | Moves a number | Ends in                                                           |
| -------------- | --------- | -------------- | ----------------------------------------------------------------- |
| A prerelease   | **Scout** | Never          | A findings comment on the tracking issue, and issues for the code |
| A stable       | **Adopt** | The floor      | One PR that moves the floor and closes the tracking issue         |

Both run through `/upstream <romm|retrobat> [tag]`, which picks the track from the tag. How to
read a delta, and the traps in it, is in [delta.md](delta.md).

## The tracking issue

`tools/upstream_watch.py`, run daily by `.github/workflows/upstream-watch.yml`, keeps one issue per
upstream version line above the floor, labelled `upstream` and titled
`<RomM|RetroBat> <major>.<minor>: scout and adopt`. It owns the releases table between its markers
and comments when a tag appears. Everything else in the issue is the pass's:

- **One findings comment per scouted tag**, edited in place when the same tag is scouted again.
  A newer prerelease gets a new comment that says which earlier findings still hold at it.
- The adoption PR says `Fixes #<n>`. A line whose stable ships while a scout is open goes straight
  to adoption, and the scout's findings become its starting list.
- The watch respects a closed issue until upstream ships a tag the issue does not list.

## Scout: a prerelease

Nothing a scout does may move the floor, `LastTested`, the pinned schema, a `Verified:` stamp, the
compatibility table or a certification record. Those describe the builds RomMBat supports, and a
prerelease is not one. A test asserts the floor and the tested row carry no prerelease suffix.

1. **Pick the target**: the newest prerelease of the line when the work starts. Check again
   before posting, because prereleases supersede each other in hours (delta.md).
2. **Read the delta** from the floor tag to the target, from the trees and not the compare
   endpoint (delta.md, "Reading a delta"). Keep the paths RomMBat depends on. Drop the features
   `version-compatibility.md` lists as never reaching a local install.
3. **Refresh into scratch**: `reference/refresh.sh --ref <project>=<tag> --out <scratchpad>/scout`.
   It runs `verify.py` and both generators' `--check` against the scratch copy and leaves
   `reference/` and `data/` alone. Each drift is a finding: name the docs that cite the number,
   and the generator adoption will run.
4. **RomM: diff the schema.** Read `SYSTEM.VERSION` from the disposable instance's
   `/api/heartbeat`. If it is the target, pull `/openapi.json` into scratch and diff it against
   the pin. Name every path and field the generated DTOs or `RomM.Client` use that moved, then
   run the live tests against that instance. If it is not the target, the schema and the live
   behaviour are unproven, and the findings say so; the maintainer owns that instance, so never
   upgrade it yourself.
5. **Preview the re-check list**: `python3 tools/docs/check.py --stale --floor <project>=<core>`,
   with the version less its suffix (`5.4.0`), lists the facts adoption will owe. Read every entry
   in `docs/upstream/issues.md` against the delta: an upstream fix is noted, never acted on.
6. **RetroBat: smoke the scout tree.** Rebuild `ROMMBAT_SCOUT_ROOT` from
   `./tools/retrobat-install.ps1 -Version <tag>` as `tools/handson/README.md`, "The scout tree",
   says, then `Use-ScoutTree` and run the agent tree's build steps on it with a small set.
   - The startup check warns that the version is newer than tested, and does not refuse.
   - Step 9: a second sync is a no-op, with no gamelist churn and no re-download.
   - Boot each row whose inputs moved (its `es_savestates.cfg` entry, its `<extension>` list, its
     BIOS md5 set, its emulator binary) through `emulatorLauncher` and record what it wrote.
     Nobody plays: a save that needs a pad is a row adoption owes.
   - `Use-AgentTree` when done.
7. **Draft the mapping.** Against each record in `docs/platforms/`, list the nine steps the move
   would touch (`platform-certification`, "When the floor moves"). It goes in the findings, not
   the records, and adoption starts from it.
8. **Post and file.** The findings comment names the target, what was read, measured and left
   unproven, and each finding. A finding that needs code becomes its own issue, citing the
   tracking issue, through `/start-issue`.

### A scout fix

It lands on main and must be **correct on both builds**: the floor's tests stay green, and where
the two behave differently the code branches on `RomMServerVersion` or `RetroBatVersion` read at
runtime, with a test per side. An upstream fact it relies on is stamped with the prerelease it was
measured on, and the adoption re-stamps it on the stable. A change only the prerelease can justify
(a field the floor's server does not send, a folder the floor's tree does not have) waits for
adoption. Gating it is still a branch nobody running the floor can reach.

## Adopt: a stable

A version move is not a find-and-replace: some numbers here are the **current** supported version
and must move, and some are the version a measurement was taken on and must not. Work in order.

1. `reference/refresh.sh --ref <project>=<stable tag>`, into `reference/`. Resolve every drift it
   reports rather than editing the expected number. A drift is a signal to revisit the docs that
   cite it. Run the generator it names and review that diff.
2. Move together, or the startup check disagrees with the README: `RetroBatVersion.Minimum`,
   `RetroBatVersion.LastTested`, `RetroBatRoot.MinimumVersion`, the `README.md` requirements
   table, and the guide's `wiki/getting-started/requirements.md` and the compatibility row in
   `wiki/reference/compatibility.md`. A test asserts the first three agree.
3. `python3 tools/docs/check.py --stale` then lists the re-check work: every fact whose
   `Verified:` stamp names a build below the new floor, and every fact with no stamp. It reads
   the floor from `RetroBatVersion.Minimum` and `RomMServerVersion.Minimum`, so run it once the
   floor being moved is in code: step 2 for RetroBat, step 6 for RomM. Re-measure each fact
   and restamp it, or delete it with its citations when it stopped being true. Read the upstream
   changelog end to end as well, for behaviour no fact records yet.
4. Re-check every entry in `docs/upstream/issues.md`. A fix upstream changes what
   RomMBat should do; **no workaround comes out until a hands-on pass has seen the fixed
   behaviour.** A changelog line is upstream's belief, not a measurement.
5. Leave provenance alone. `data/retrobat/*.json`'s `_retrobat_version`, and a live capture under
   `tests/.../fixtures/`, record the version something was **measured on**. Rewriting those to
   the new number silently reattributes a measurement to a build nobody ran it against.
6. A RomM move also moves the pinned OpenAPI schema, which is the minimum version on purpose.
   `RomMServerVersion.Minimum`, `RomMServerVersion.LastTested` and the `info.version` inside the
   pinned file move in one commit, and a test reads the pin rather than restating it, so a pin
   that moves without the floor fails there. Capture it as delta.md, "Capturing the schema", says,
   and see `src/RomM.Client/openapi/README.md`.
7. Resolve every scout finding on the tracking issue: done, carried into this PR, or filed with
   its reason. Delete each version gate the new floor makes dead, with its floor-side test.
8. **Map the move onto every record in `docs/platforms/`.** The PR description marks the nine
   steps touched or carried, step 9 always touched, with a one-line reason for each carried
   step. Each record's "Where each row stands" then says which steps were re-run at the new
   floor and which are owed. The rule and what counts as touched are in the
   `platform-certification` skill, "When the floor moves". For RetroBat, rebuild the agent tree
   from a pristine install of the new stable first (`tools/handson/README.md`, "The agent tree").

The PR takes `semver:minor` ([versioning](../../../docs/design/decisions/versioning.md)), closes
the tracking issue, and runs `/drive-pr`.
