---
summary: The minimum RomM and RetroBat versions, why the floor tracks the newest stable, and what adopting a new version costs.
read-when: Moving the supported RomM or RetroBat version, or touching the startup version check.
---

# Version compatibility

Every RomMBat release states the minimum RomM and RetroBat versions it supports. Currently
**RetroBat 8.2.1** and **RomM 5.3.1**.

**The floor tracks the newest stable, it does not sit at the oldest version that happens to
work.** RomMBat adopts a new RomM or RetroBat stable within one release of it appearing and
moves the minimum with it. Two reasons, both specific to this project. Every fact in
`docs/upstream/` is a measurement of one build's behaviour, and supporting a range
means owning that measurement on every version in the range, on a `(system, emulator, core)`
matrix that is already two to four passes per row. And RetroBat's own updater moves users
forward, so a wide floor buys compatibility with installs that mostly do not exist while
doubling what has to be certified. RetroBat 8.2.1 is the floor because 8.2.0's Flycast
save-state watcher read the wrong directory, and a release that supported both would have to
carry the workaround and the fix at once.

What adoption costs, each time: re-run `reference/refresh.sh` and resolve the drift, move the
floor and the tested row together, re-read the upstream changelog for anything that touches a
measured rule, re-check every fact `tools/docs/check.py --stale` lists as stamped below the new
floor, and every entry in `docs/upstream/issues.md`. Moving the
RomM floor also moves the pinned OpenAPI schema, because the pin is the minimum version on
purpose. And every platform record under `docs/platforms/` is mapped onto the nine steps: the
steps the move touches are re-run or recorded as owed at the new floor, step 9 always among
them, and each step carried over says why. A record is neither voided by a move nor carried
through one silently (#187, and the `platform-certification` skill).

**The floor is only ever a stable.** A RomM or RetroBat prerelease is scouted, never adopted:
a scout pass reads its delta, refreshes the reference data against it into scratch, smoke-tests a
RetroBat beta on a tree of its own, and lands the fixes the stable will need as changes that are
correct on the floor too. When the stable ships, adoption is moving the numbers and re-running
what moved, not discovering it. A prerelease floor would make every RomMBat release built on it
require a server or a RetroBat that upstream itself does not call finished, and prereleases
supersede each other in hours, so the floor would name a build nobody can still install
([decision](decisions/stable-only-floor.md)). Both tracks, the tracking issue each line gets and
how to read a delta are in the `version-adoption` skill.

RomMBat does not adopt RomM features that never reach a local RetroBat install, so a delta that
adds only these needs no work: Jukebox and the soundtrack player, walkthroughs, barcode
scanning, recommendations, the Steam, Demozoo, Pouet and CSDb metadata sources, js-dos and
PICO-8 in-browser play, and Emulator Streaming as a feature. Streaming reaches RomMBat only as a
writer on its slots (RM-4). Server layout (RM-10) is administration and inert here too.

- Read the RomM version from `GET /api/heartbeat` (`SYSTEM.VERSION`) at startup and the
  RetroBat version from **`system/version.info`** in the tree.
- **There is no `build.ini`.** It does not exist anywhere in a RetroBat 8.2
  tree (RB-377). `system/version.info` is a single line carrying a channel and architecture suffix,
  `8.2.1-stable-win64`, so it is not a bare semantic version and must be split on `-`
  before comparison.
- **Both version strings can carry prerelease suffixes.** A live instance has
  reported `5.1.1-beta.1`. A comparison that assumes three numeric components will throw on
  real-world values from either side.
- Below minimum: refuse with a clear message naming both versions. Above but untested:
  warn and continue.
- Gate features on version rather than assuming, so a newer RomM adding a field does not
  break an older client and vice versa.
- RomMBat's own version is SemVer, not tied to the RomM floor, and a floor move is a MINOR
  ([versioning](decisions/versioning.md)). Compatibility is read from the table below and each
  release's notes.
- Keep a compatibility table in the guide,
  [`wiki/reference/compatibility.md`](../../wiki/reference/compatibility.md), and treat adding
  a row to it as part of shipping.
