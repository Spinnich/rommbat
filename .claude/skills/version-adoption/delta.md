# Reading an upstream delta

The traps in working out what changed between two upstream builds, for both tracks of
[version adoption](SKILL.md).

## Reading a delta

**Take the file list from the trees, not from the compare endpoint.**
`GET /repos/{owner}/{repo}/compare/{a}...{b}` caps `files` at 300 and says nothing in the payload
when it truncates, so a delta that size reads as complete while hiding whichever files sort last.
`5.3.0-alpha.3` to `5.3.0-beta.1` hit exactly 300 and hid the browser save writer, which a
finding turned on. Diff blob shas from `git/trees/{ref}?recursive=1` at both tags instead.
Under the cap, a file's `patch` can still be missing: the 8.2.1-to-8.3.0 launcher compare returned
141 files and no patch for 19 of them, `es_features.cfg`, `Program.cs` and `Installer.cs` among
them. Diff those from their raw content at both refs.

**Read RomM from source.** The release notes are cumulative from the last minor and can say the
opposite of the code: 5.3's say ordinary browser play goes to `autosave`, and RM-4 is what the
player does for a game RomMBat syncs.

**RetroBat ships more than its repo.** A release's `build.ini` downloads emulatorlauncher and
EmulationStation from rolling `continuous` builds, so no tag pins them. `refresh.sh` takes the
last emulatorlauncher commit at or before the release was published, and the scout tree is what
the release actually installs. Where the two disagree, the tree wins. EmulationStation's
`continuous-master` build comes from its `master` branch, not the default `autopull`, so list its
commits with `?sha=master`; on the default branch the 8.3.0 window held none.

## Which facts a delta touches

**A fact is touched when the delta names something its heading or body names.** The subjects
differ by project.

**RetroBat**: a system, an emulator or core, an `es_features.cfg` option, a file or a path under
`saves/`, `bios/` or `emulators/`, and EmulationStation's own surface: an event hook, an
`es_settings.cfg` key, an HTTP API endpoint, a menu or a launch argument. Read, in order, and search
each for the fact's subject:

1. `git diff` of the vendored `reference/` files after `refresh.sh`. They are the files the design
   depends on, so a changed line there names what moved.
2. The emulatorlauncher commits between the previous and the new pinned ref, by generator name
   (`<Emulator>Generator.cs`) and by system, and the EmulationStation commits between its two
   refs, by event, setting, endpoint and menu.
3. The upstream changelog and the release's `upgrade.xml`, which names folders it moves.
4. **The emulator builds the release downloads.** RetroBat installs an emulator on its first
   launch at whatever build its updater names, so a fact about an emulator's own behavior can be
   touched by a build that no repo commit shows. Name the build in a fact's `How:` when it is
   known, and treat a moved build as touching.
5. **RetroBat's own tree between the two tags**, `compare/<previous>...<new>` on
   `RetroBat-Official/retrobat`. `system/templates/`, through `templates_files.lst`, seeds an
   emulator's config on a fresh install, so a template dropped or edited touches a fact whose path
   or key it held, unless the emulator's generator writes that key itself. `system/es_menu/`,
   `system/padtokey/` and the EmulationStation templates (`es_input.cfg`, `es_padtokey.cfg`,
   `gamecontrollerdb.txt`) ship as they are.

**RomM**: an endpoint, a request or response field, a scope, a config key, a status code or a
behavior of the server. Read the source diff between the two tags, from the trees (see above), and
the diff of the pinned OpenAPI schema after the capture below, and search each for the fact's
subject. The release notes are cumulative and can say the opposite of the code, so they only point
at where to look. A fact stamped against RomM source is carried only when the files it cites are
unchanged.

A changelog line nobody measured touches. A fact about RomMBat's own behavior that merely cites
upstream's is touched only when the cited behavior is. Write the reason a fact is carried as what
you searched and found nothing in, not as "unchanged": it is read by whoever reviews the move.

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
