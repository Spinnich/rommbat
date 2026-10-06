---
summary: The supported RomM and RetroBat floor only ever names an upstream stable; a prerelease is scouted, not adopted.
read-when: Deciding whether to move RomMBat's floor, its tested row or the pinned schema to an upstream build, or changing what /upstream does with a prerelease.
---

# The floor is a stable, and a prerelease is scouted

**`Minimum`, `LastTested` and the pinned OpenAPI schema name an upstream stable, never a
prerelease.** A RomMBat release states what it needs from the user's server and install, and a
user who runs upstream's stable must be able to meet it. A prerelease floor asks them for a build
upstream does not call finished, and one that is gone within hours: RomM's `5.3.0-alpha.2`
replaced `alpha.1` eight hours after it shipped. A test asserts that the four values carry no
prerelease suffix.

**A prerelease still gets work, as a scout pass.** It reads the delta, refreshes the reference
data into scratch, diffs the schema when an instance runs the target, and smoke-tests a RetroBat
beta on a tree of its own. Fixes it finds land on main as long as they are correct on the floor as
well, version-gated at runtime where the builds differ, so adopting the stable is moving the
numbers and re-running what moved. Findings that only the prerelease can justify wait for the
stable.

**One tracking issue per upstream version line**, opened and kept current by a daily workflow
rather than by someone watching upstream's releases page. The floor moves within one RomMBat
release of a stable, and nothing reminds anyone of a deadline that a person has to notice first.

Rejected:

- **A prerelease floor for RomMBat's own prereleases.** It keeps two policies apart only by the
  release channel, and a RomMBat beta that requires an upstream alpha is untestable by most of the
  people a beta exists for.
- **Scout fixes on a long-lived branch per line.** It keeps main free of version gates, at the
  cost of a branch that drifts from main for weeks and lands as one large unreviewed merge.
- **Full re-certification against each prerelease.** It pays adoption's cost ahead of time, and
  again for each prerelease in the line.
