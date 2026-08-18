---
description: Start the M4 milestone, gamelists, metadata and media
argument-hint: "[branch name]"
---

# Start M4: metadata and media

Fresh session, branched off main. This is the session that writes the branch. What follows
it is `/review-pr` in a second fresh session and `/fix-pr` in a third, so the job is not
"working code", it is a branch that survives a reviewer who will have far less context than
you and the repo's own rules as its standing authority.

---

Variables for this run:

- BRANCH = $1, default `m4-metadata-and-media`. Branch off main and stay off it; the
  pre-push hook will stop you anyway.
- NOTES = `~/rommbat-work/<PR>.md`, started once the PR number exists. See "What the next
  two sessions need from you".

## Who is who

I am Spinnich, the only human on this repo. Claude writes the branch, a fresh Claude
reviews it, a third fixes it. You are the first of the three and the only one that will
ever hold the reasoning behind the code.

That is the whole constraint on how you work: anything true only in your head is invisible
to the review, and the review is allowed to rule against it. Put every non-obvious decision
somewhere durable, a commit message, a line of `docs/PLAN.md`, or a test that fails when
someone undoes it.

Skip the preamble and the progress narration. Show me decisions, measurements, and diffs.

## Read before you design

1. **CLAUDE.md**, in full. The six rules are what a reviewer checks first.
2. **`docs/PLAN.md`**, the sections that govern this change: M4, M3 and M2 (what you
   consume), core principles 2 and 3 (the gamelist cap is principle 3's curation argument,
   not a technical ceiling), the mapping section's "two RomM platforms can share one
   folder", and "Verification".
3. **`docs/retrobat-findings.md`**, probe 3 and probe 5. Everything known about
   `/reloadgames`, ES's stale in-memory model, the rewrite on exit and what a reload costs
   is measured there, and it is more permissive than the plan originally assumed.
4. **Skills**: `retrobat-layout` before designing anything that writes into the tree,
   `romm-api` before choosing an endpoint, `platform-mapping` for the folder grouping,
   `offline-and-portable` for the filename and path constraints,
   `pre-pr-verification` before claiming anything is done, `platform-certification` if you
   end up claiming a system works.
5. **The code you are building on**: `ContentSync`, `ContentPlanner`, `EvictionPlanner`,
   `LocalFileStore`, `SettingStore`, `SetResolver`, `PlatformResolver`, `EsSystemsFile`,
   `RelativePath`, and migration `003-content.sql` with its header.

## Where M3 left off

ROMs land. `local_file` holds one row per downloaded or adopted file, keyed by a path
relative to the RetroBat root, with the folder, file name, hashes, what the hashes describe,
which check the file passed, and whether it was synced or adopted. The budget is two bounds,
`content.max_bytes` and `content.free_space_floor_bytes`, and eviction is a dry-run first
operation that asks `SaveGuard` before removing anything.

Nothing has ever written into `roms/<system>/gamelist.xml`, nothing has ever spoken to
EmulationStation, and no metadata beyond `display_name` and `sort_key` is stored anywhere.
`RomRow` deliberately carries none of it: M2 cut the walk down to what set resolution needs
because the full row is roughly seventy fields with eight metadata sub-objects across 333
pages. **M4 has to get metadata for the ROMs that are actually present without undoing
that**, and that is the milestone's central design question, not an implementation detail.

`local_file` also has no notion of what kind of file a row is. It was written when every
file was a ROM.

## Scope, which is the plan's M4 and nothing else

- Group locally present ROMs by their **resolved folder**, not by platform, and emit one
  merged `gamelist.xml` per folder. `snes` and `sfam` can both resolve to `snes`, and
  `arcade` fans out to ten folders. Two writes to one file is the failure this rule exists
  to prevent.
- Write only locally present ROMs. `path` is `./<fs_name>`.
- Fields per `backend/utils/gamelist_exporter.py`: `name`, `desc`, `image`, `thumbnail`,
  `marquee`, `video`, `manual`, `developer`, `publisher`, `genre`, `family`, `players`,
  `lang`, `region`, `releasedate` (`YYYYMMDDT000000`), `rating` (0-1, two decimals).
- Download media into `images/`, `videos/`, `manuals/` beside the ROMs, named after the ROM
  file, per RetroBat's scraper convention. **Media counts against the disk budget**, which
  means M3's planner and eviction both change.
- **Merge, never clobber.** ES writes `favorite`, `playcount`, `lastplayed` and `hidden`
  back into the same file. Read what is there, update only the fields RomMBat owns, preserve
  everything else including comments, and write atomically.
- **Then `GET http://127.0.0.1:1234/reloadgames`**, short timeout, carry on if it fails. The
  API only answers while ES runs, and write-then-reload is what makes the edit survive ES
  serialising its stale model at exit.
- Enforce a per-system entry cap for navigability. Probe 5 withdrew the technical ceiling:
  100,000 entries load in 2.07 s for 419 MB. **The number is a product decision, so propose
  one and ask me rather than picking it silently.**
- Do not use `POST /api/export/gamelist-xml`. It writes into the server's own library
  folders, which is a different machine.

**Done when** (the plan's words): ES shows box art, descriptions and videos for synced
games, and a user's manual metadata edit survives the next sync.

**Out of scope**: BIOS (M5), saves, states and playtime (M6), the gamepad UI and its
`system/es_menu` registration (M7). M7 needs the same merge-and-reload machinery for
`system/es_menu/gamelist.xml`, so build that as a component with a seam rather than
generalising it now, and write down in the plan what M7 reuses.

## Measure before you commit to a shape

M1, M2 and M3 each landed a measurement commit that amended `docs/PLAN.md`, and each found
something that changed the code: the `int32` overflow, the non-unique slug, the 504 on
`/api/roms/identifiers`, the archive hash scope. Assume M4 has its own. Probe against the
live instance in `DEVELOPER_SETUP.md` and against the real RetroBat install.

Worth an actual probe, because each one changes code:

- **Where media bytes actually come from.** `path_cover_small`, `path_cover_large`,
  `path_manual` and `path_video` are static resource paths, not `/api/.../content` routes,
  so the whole download path M3 built may not apply. Find out whether the device token
  authenticates there, whether ranges work, and what `url_cover` and `url_manual` point at.
  **If those are metadata-provider URLs rather than RomM's own, using them breaks the
  LAN-only story**, and that has to be established before a downloader is written.
- **What a real library actually holds.** The share of ROMs carrying a cover, a manual, a
  `path_video`, `merged_screenshots` and a populated `metadatum`. If videos are near zero,
  `<video>` is a promise the plan should stop making for v1 and say why.
- **The cost of metadata for present ROMs only.** `GET /api/roms/{id}` returns
  `DetailedRomSchema`: 80 fields including `all_user_saves`, `all_user_states`,
  `sibling_roms` and `all_user_notes`. That is the same family as the `GET /api/collections`
  trap under core principle 2, so measure it on the worst ROM you can find rather than a
  quiet one, and compare it against one paged read that carries `metadatum`. The answer
  decides whether M4 makes N requests or `ceil(N/250)`.
- **The conversions, each of which is silently wrong rather than loud.** What
  `metadatum.companies` contains and whether developer and publisher are separable at all;
  what scale `average_rating` uses against a 0-1 gamelist rating; what units and zone
  `first_release_date` uses against `YYYYMMDDT000000`; whether `player_count` maps onto
  `<players>` as ES reads it.
- **RetroBat's own media naming convention**, off a scraped install or ES's source rather
  than from memory. Our names must not collide with, or silently duplicate, art a user's own
  scrape already wrote.
- **A real ES-written `gamelist.xml`, checked in as a fixture.** The round-trip test the
  plan requires has nothing to run against today.
- **Whether `/reloadgames` acts while a game is running.** `/quit` and `/emukill` both
  answer 200 and do nothing in that state, so a 200 from this API is not evidence the action
  happened. Measure the not-running case too, and how fast it fails.
- **Whether ES reformats what we wrote.** Write a gamelist, start ES, reload, quit, diff. If
  ES normalises our output then the no-churn regression has to compare after ES has touched
  the file, not before.

Quote the numbers you took and never one you did not. Where a measurement contradicts the
plan, amend `docs/PLAN.md` in this PR and say which fact moved. Probe artifacts go in
`probe-output/`, which is gitignored; if a test needs one, check in the fixture instead.
`gamelist_exporter.py` is not vendored, so if you quote anything from it, add it through
`reference/refresh.sh` rather than pasting it.

## The rules that bite in this milestone specifically

- **ES owns this file.** Rule 2's reasoning applies to `gamelist.xml` exactly as it does to
  an emulator INI: another process regenerates it. Merge, write atomically, reload.
- **Relative paths, inside the file as well as in the database.** `./<fs_name>` and
  `./images/...` keep a populated install portable across a drive-letter change. An absolute
  path in a gamelist survives the move and points at nothing.
- **Names we construct, from names we were given.** Media file names derive from ROM names
  that came from RomM, so they can carry characters Windows refuses, and they extend a path
  that is already long. Handle that before writing, not at the failed write.
- **XML correctness under real data.** Ampersands, non-ASCII titles, control characters in a
  scraped description. A gamelist ES cannot parse loses the whole system, not one entry.
- **ConnectTimeout on the ES client too**, and a short one. It is loopback, ES is usually
  absent, and failing to reload is normal rather than an error.
- **Nothing outside the RetroBat tree.**
- **Eviction has to take a ROM's media with it**, and adoption must never mistake a user's
  own scraped art for something RomMBat downloaded and may delete.

## Schema

Expect a migration `004`. Same discipline as `003`: its header states what shape could not
carry the work and why, one migration for the milestone, no column holds an absolute path,
CHECK constraints on anything path- or name-shaped, rebuild rather than ALTER when adding a
CHECK, and copy rows even when you are sure the table is empty.

At least three facts have no home today: what kind of file a `local_file` row is, the
metadata needed to regenerate a gamelist with the server unreachable, and whatever lets a
second run know the file it would write is the file already there.

## Tests the plan already requires

Do not treat these as optional coverage; `/review-pr` checks for the specific test, not for
some test.

- Round-trip a real RetroBat `gamelist.xml` and assert the user fields survive.
- Two platforms sharing one folder produce one merged gamelist, not two competing writes.
  `PlatformMappingTests` already owns the other half of this case and `data/README.md` says
  the gamelist half arrives here.
- A no-op re-sync with **no gamelist churn**: byte-identical output, and it says so.
- The relocation test, now with media and a gamelist present.
- Gamelist generation with the server unreachable, from local state alone.
- Bounded memory and bounded request count against the synthetic large-catalog fixture.
- The per-system cap, and what an entry over it is told.

## Working shape

Commits that stand alone and explain why, in the style of the M1, M2 and M3 commits. Scoped
diff, no unrelated cleanups riding along: every extra file is review surface, and review
surface is what the next two sessions cost.

If part of this turns out to be a design question rather than a coding one, stop and ask me
rather than picking. The gamelist cap is already one of those. That is cheap now and
expensive at round two of `/fix-pr`, where a third pass over the same file means the design
was wrong all along.

## What the next two sessions need from you

- A PR body on `.github/PULL_REQUEST_TEMPLATE.md`, with the AI disclosure stating extent,
  not just the fact, and the measurement table in the M3 body's shape. Ticked checkboxes are
  claims a reviewer will check against the diff.
- Every deviation from `docs/PLAN.md` amended in the plan, in this PR. An undocumented
  deviation is a finding, and the reviewer will be right.
- No scratch in the tree. Anything left behind comes back as a cleanup list.
- NOTES seeded with the rulings this session already made: the options weighed, what you
  chose, and why. The ledger is what stops round one re-litigating a decision you made with
  more context than the reviewer will have.
- The full `pre-pr-verification` skill run, with a plain statement of what you verified and
  what you did not. `dotnet build -c Release -warnaserror` is CI's build; a local Debug
  build hides exactly the warnings that will bounce the PR.

## Default

Read the scope and show me your reading plus the measurement plan before you write code.
That is the cheapest place for me to correct you.

Commit locally as you go. Ask before pushing, before opening the PR, and before anything
that writes into the real RetroBat install or drives a running EmulationStation.
