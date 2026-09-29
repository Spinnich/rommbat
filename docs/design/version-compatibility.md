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

What adoption costs, each time: re-run `reference/refresh.sh` and resolve the drift, re-read
the upstream changelog for anything that touches a measured rule, move the floor and the
tested row together, and re-check every entry in `docs/upstream/issues.md`. Moving the
RomM floor also moves the pinned OpenAPI schema, because the pin is the minimum version on
purpose. And every platform record under `docs/platforms/` is mapped onto the nine steps: the
steps the move touches are re-run or recorded as owed at the new floor, step 9 always among
them, and each step carried over says why. A record is neither voided by a move nor carried
through one silently (#187, and the `platform-certification` skill).

**A prerelease is adoptable, and which prerelease needs a rule of its own.** "Within one
release" says when to move and not what to move to, and prereleases supersede each other on a
timescale the policy was not written for: `5.3.0-alpha.2` shipped about eight hours after
`5.3.0-alpha.1`, on the same day the assessment of `alpha.1` was being written. So **the
target is the newest prerelease of the version being adopted at the moment the work starts,
re-checked before the PR opens**, and adopting the older of two same-day prereleases is
adopting a build that was superseded before anyone could run it. Two consequences follow from
a prerelease specifically. The public demo will not carry it, so the pin comes from a
self-hosted instance and `SYSTEM.VERSION` is **read at capture time rather than assumed**,
because the instance can be upgraded underneath the work exactly as the tag was. And the delta
between two prereleases is read rather than waved through: `alpha.1` to `alpha.2` was 19
commits over 21 files, and reading them is what said which findings held at both tags and
which had to be re-attributed.

Read a RomM delta from source, counted from the tags' trees (`pre-pr-verification`, step 7).
The release notes are cumulative from the last minor and can say the opposite of the code: 5.3's
say ordinary browser play goes to `autosave`, and RM-4 is what the player does for a game RomMBat
syncs.

RomMBat does not adopt RomM features that never reach a local RetroBat install, so a delta that
adds only these needs no work: Jukebox and the soundtrack player, walkthroughs, barcode
scanning, recommendations, the Steam, Demozoo, Pouet and CSDb metadata sources, js-dos and
PICO-8 in-browser play, and Emulator Streaming as a feature. Streaming reaches RomMBat only as a
writer on its slots (RM-4). Server layout (RM-10) is administration and inert here too.

- Read the RomM version from `GET /api/heartbeat` (`SYSTEM.VERSION`) at startup and the
  RetroBat version from **`system/version.info`** in the tree.
- **There is no `build.ini`.** M0 confirmed it does not exist anywhere in a RetroBat 8.2
  tree. `system/version.info` is a single line carrying a channel and architecture suffix,
  `8.2.1-stable-win64`, so it is not a bare semantic version and must be split on `-`
  before comparison.
- **Both version strings can carry prerelease suffixes.** The instance M0 measured against
  reported `5.1.1-beta.1`. A comparison that assumes three numeric components will throw on
  real-world values from either side.
- Below minimum: refuse with a clear message naming both versions. Above but untested:
  warn and continue.
- Gate features on version rather than assuming, so a newer RomM adding a field does not
  break an older client and vice versa.
- Consider Grout's versioning convention, which solves exactly this problem: the first
  three components of the client version track the required RomM version, and the fourth
  is the client's own patch number. It makes compatibility legible from the release tag
  alone.
- Keep a compatibility table in the README and treat adding a row to it as part of
  shipping.
