# Reading an upstream delta

The traps in working out what changed between two upstream builds, for both tracks of
[version adoption](SKILL.md).

## Reading a delta

**Take the file list from the trees, not from the compare endpoint.**
`GET /repos/{owner}/{repo}/compare/{a}...{b}` caps `files` at 300 and says nothing in the payload
when it truncates, so a delta that size reads as complete while hiding whichever files sort last.
`5.3.0-alpha.3` to `5.3.0-beta.1` hit exactly 300 and hid the browser save writer, which a
finding turned on. Diff blob shas from `git/trees/{ref}?recursive=1` at both tags instead.

**Read RomM from source.** The release notes are cumulative from the last minor and can say the
opposite of the code: 5.3's say ordinary browser play goes to `autosave`, and RM-4 is what the
player does for a game RomMBat syncs.

**RetroBat ships more than its repo.** A release's `build.ini` downloads emulatorlauncher and
EmulationStation from rolling `continuous` builds, so no tag pins them. `refresh.sh` takes the
last emulatorlauncher commit at or before the release was published, and the scout tree is what
the release actually installs. Where the two disagree, the tree wins.

## Which prerelease

**Target the newest prerelease of the line when the work starts, and check again before
posting.** `5.3.0-alpha.2` shipped about eight hours after `5.3.0-alpha.1`, on the same day the
assessment of `alpha.1` was being written. Scouting the older of two same-day prereleases is
scouting a build that was superseded before anyone could run it.

**Read the delta between two prereleases rather than waving it through.** `alpha.1` to
`alpha.2` was 19 commits over 21 files, and reading them is what said which findings held at
both tags and which had to be re-attributed. A new scout comment says which of the last one's
findings still hold.

## Capturing the schema

The public demo at `demo.romm.app` carries stable only, and lags it by days, so a prerelease's
schema and often a new stable's come from a self-hosted instance. **Read `SYSTEM.VERSION` at
capture time rather than assuming it**: a live library can be upgraded underneath the work, and a
capture that does not match the target is the wrong artifact even when it parses. Search the file
for the instance's hostname before committing it, and record its sha256 in
`src/RomM.Client/openapi/README.md`.
