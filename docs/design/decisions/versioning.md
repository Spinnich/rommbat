---
summary: RomMBat versions by Semantic Versioning 2.0.0, what each part means for an app, and the schemes rejected.
read-when: Picking a release's version bump, cutting a release, changing Directory.Build.props' version, or naming a tag.
---

# Versioning

**RomMBat uses [Semantic Versioning 2.0.0](https://semver.org/).** Tags are
`vMAJOR.MINOR.PATCH[-pre.N]`, and the release workflow refuses a tag that is not.

RomMBat is an app, not a library, so its public API is what a user or a script relies on across
an upgrade:

- **MAJOR**: an upgrade that needs the user to act or loses something they set up. Having to
  re-pair or redefine sync sets; removing or renaming an agent command or flag, or changing what
  an exit code means; changing the install layout under `emulators/rommbat`, the hook files or
  the ES menu entry so an existing install stops working; dropping a certified
  `(system, emulator, core)` row.
- **MINOR**: new features, a newly certified row, **a RomM or RetroBat floor move** (routine,
  because the floor tracks the newest stable), and any local store migration.
- **PATCH**: fixes with no store migration, so stepping back one patch release is always safe.

**Downgrade across a MINOR is unsupported.** A database from a newer build is already refused
(`offline-and-portable`), and the release notes say so whenever a migration ships.

Each PR carries one `semver:major`, `semver:minor` or `semver:patch` label, so the release that
collects it can pick the bump. A PR that changes nothing a user runs, such as docs or CI, is a
patch. The release job lists the MAJOR-impact PRs in the draft notes, and refuses a tag at 1.0.0
or later that is a smaller bump over the previous stable tag than the highest label asks for.
Under 0.x and for a prerelease it only reports.

## Path to 1.0

While the platform waves land, releases are `0.N.0-alpha.N`, then `-beta.N`. Under 0.x any MINOR
may break, as SemVer allows, and the notes say what broke. **1.0.0** is cut when M8 ships and
wave 1 is certified against an M8 package. A tag with a prerelease suffix publishes as a GitHub
prerelease.

## Rejected

- **Grout's `RomM.N` scheme** (`v5.3.1.2`). RomMBat has two floors, RomM and RetroBat, and a
  version string can encode only one. The RomM floor moves with every RomM stable, so upstream
  would be choosing RomMBat's majors and RomMBat's own breaking changes would never show. And a
  four-part version does not sort as SemVer.
- **CalVer.** It says nothing about compatibility.

Compatibility is read from the guide's [compatibility table](../../../wiki/reference/compatibility.md)
and each release's notes, which open with both floors.

## Mechanics

`Directory.Build.props` holds the next planned version as `VersionPrefix` with suffix `dev` for
local builds. A release build overrides it with `-p:Version=<tag without the v>`, and
`PairingService.ClientVersion()` reports the informational version without its `+sha`, so RomM's
device list shows `0.3.0-beta.1`. The label tally is `tools/release-impact.ps1`. The repository's
tag ruleset lets only an admin create, move or delete a `v*` tag.
Cutting a release is in
[DEVELOPER_SETUP.md](../../../DEVELOPER_SETUP.md#cutting-a-release).
