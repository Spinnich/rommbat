---
description: Start the M5 milestone, BIOS and firmware
argument-hint: "[branch name]"
---

# Start M5: BIOS and firmware

Fresh session, branched off main. This is the session that writes the branch. What follows
it is `/review-pr` in a second fresh session and `/fix-pr` in a third, so the job is not
"working code", it is a branch that survives a reviewer who will have far less context than
you and the repo's own rules as its standing authority.

---

Variables for this run:

- BRANCH = $1, default `m5-bios-and-firmware`. Branch off main and stay off it; the
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

1. **CLAUDE.md**, in full. Rule 3 is this milestone's whole thesis: RetroBat is the
   authority on required BIOS, and the join is md5 only.
2. **`docs/PLAN.md`**, the sections that govern this change: M5, M3 and M4 (what you
   consume and what you have to keep working), core principle 2 on inlined list endpoints,
   principle 1 on working with the server unreachable, and "Verification".
3. **`docs/freegosy-findings.md`**, F5, F5b and F21. F5 is why the whole-library join is one
   request. F5b is M5's flow already done by hand, including the retraction of the
   "firmware hides on the `-unofficial` twin" claim. F21 is where the `is_verified` and
   filename numbers come from, and it rejects the idea that a curated md5 list adds anything.
4. **`docs/retrobat-findings.md`** on what else lives under `bios/`. openMSX keeps its whole
   user-data directory there and writes save states into `bios/openmsx/savestates/`, and
   `bios/mame/hash/*.xml` are MAME software-list metadata rather than firmware. `bios/` is
   not a tree RomMBat owns.
5. **Skills**: `romm-api` before choosing an endpoint, `retrobat-layout` before writing
   anything into the tree, `platform-mapping` for folder resolution and the manifest join,
   `offline-and-portable` for path and filename constraints, `pre-pr-verification` before
   claiming anything is done, `platform-certification` because step 3 of its checklist is
   exactly this milestone and a certified platform now has to be able to pass it.
6. **The code you are building on**: `ContentSync`, `ContentPlanner`, `EvictionPlanner`,
   `MediaSync`, `MediaPolicy`, `LocalFileStore` and its `kind` column, `SettingStore`,
   `PlatformResolver`, `EsSystemsFile`, `RelativePath`, `FilesystemLimits`,
   `FeatureAvailability`, `SyncCommand`, and migrations `003-content.sql` and
   `004-metadata-and-media.sql` with their headers.

## Where M4 left off

`sync` is three passes in one order that is argued for in `SyncCommand`: re-resolve the
sets, pull ROM content, then media, then write one merged gamelist per folder. `local_file`
holds one row per file with a `kind` that is `rom` or one of five media kinds, keyed by a
path relative to the RetroBat root. The budget counts everything RomMBat downloaded,
including media, and eviction takes a ROM's media with it and asks `SaveGuard` first.

Nothing has ever written into `bios/`. `RomM.Client` has no firmware call at all: the only
thing in the tree that knows firmware exists is `FeatureAvailability`, which already
declares `RomMFeature.Firmware`, requires `firmware.read`, and degrades with "BIOS files
must be copied into bios/ by hand". M5 is what makes that sentence a lie in the good case
and keeps it honest in the degraded one.

Nothing reads `batocera-systems.json` either. It is vendored in `reference/` for the
numbers in the plan, and `data/retrobat/` is where bundled runtime data lives
(`platforms.json`, `save_directories.json`, `save_shapes.json`). Which of those the running
app reads from, or whether it prefers a copy inside the live RetroBat install, is a design
decision this milestone has to make and justify. The es_systems.cfg precedent is "read the
live copy, the vendored one is a template", and the reason that precedent exists applies
here too: the manifest belongs to the same `emulatorlauncher` that will consume the files.

## Scope, which is the plan's M5 and nothing else

- Resolve required BIOS per synced RetroBat system from `batocera-systems.json`.
- Join candidates on `md5_hash`, ignoring filename and ignoring `is_verified`. Read the
  candidates from the inlined `firmware[]` on `GET /api/platforms` for a whole-library
  report; `GET /api/firmware?platform_id=` stays the per-platform certification call.
- Dedupe several hits on one md5 and download once. Multiplicity is a user filing the same
  system under two folders, not ambiguity.
- Download via `GET /api/firmware/{id}/content/{file_name}` and write to the path the
  manifest specifies, renaming as needed.
- Skip a file already present with the right md5. On a mismatch, warn and leave the existing
  file alone.
- Report the gap: required BIOS with no md5 match anywhere in RomM, per platform, with the
  expected filename and hash. The plan calls this the single most useful thing the feature
  can tell a user, so it is a first-class output and not a log line.
- **BIOS is fetched before that platform's ROMs**, which means `SyncCommand`'s pass order
  changes and the change has to be visible in its remarks, not just in the call order.

**Done when** (the plan's words): syncing a BIOS-dependent platform lands the right files at
the right paths with no manual copying, files RomM does not have are listed explicitly
rather than failing silently at launch, and BIOS is fetched before that platform's ROMs.

**Out of scope**: saves, states and playtime (M6), the gamepad UI (M7). Do not start
certifying wave 2 platforms in this branch; M5 gives certification step 3 something to run,
and running it is a separate PR.

## What the manifest actually contains, which the plan does not yet say

Reproduced against `reference/batocera-systems.json` at today's pin. Verify each one
yourself before building on it, then decide what it means.

- **179 of the 353 entries carry an empty `md5` string**, across 49 systems, and 20 systems
  are entirely blank, including `mastersystem`, `ngp`, `ngpc`, `sega32x`, `atarist` and
  `cdi`. An md5-only join cannot say anything about these, in either direction. They are
  neither "matched" nor "missing from your library", and reporting them as missing would tell
  a user to go looking for a file we cannot recognise if they already have it.
- **So the requirement count is 156 joinable md5s, not 157.** `verify.py` builds its set
  without filtering the blank string, and the plan's table inherits that. Same shape as the
  YAML parser fault already recorded in `reference/README.md`: our counting, not upstream
  drift. Overlap with RomM stays 63, and "RetroBat-required, unknown to RomM" is 93 rather
  than 94. Fix the tool and the plan's table together, in this PR, and say which number moved
  and why.
- **Six md5s have more than one destination path**, so one download can owe several writes:
  `coleco.rom`, `colecovision.rom` and `openMSX/share/systemroms/coleco.rom` are the same
  bytes, and `saturn_bios.bin` is wanted at both `bios/` and `bios/kronos/`. No destination
  path ever takes two different md5s, so the path is a key and the md5 is not.
- **Seven entries land outside `bios/` entirely**, under `emulators/jynx/` and
  `emulators/dolphin-emu/User/Triforce/`. All seven are blank-md5. Decide deliberately
  whether a v1 writes outside `bios/` at all.
- **Destination paths go up to five levels deep**, so the writer creates directories, and
  `RelativePath` and the filesystem-limit checks have to cover paths we construct from the
  manifest rather than from a ROM name.
- **The manifest is keyed by batocera system names**, 99 of them, and two (`astrocde`, `msx`)
  are not in `systems_names.lst`. It is a third vocabulary beside `<name>` and `<path>` in
  `es_systems.cfg`, and `platform-mapping` is where that resolution belongs.
- **`bios/mame/hash/*.xml` and `bios/mame/samples/bbc.zip`** are MAME software-list metadata,
  already recorded in `docs/retrobat-findings.md` as not firmware. They are blank-md5 too, so
  the blank rule may already handle them, but say so rather than leaving it to luck.

## Measure before you commit to a shape

M1 through M4 each landed a measurement commit that amended `docs/PLAN.md`, and each found
something that changed the code. Assume M5 has its own. Probe against the live instance in
`DEVELOPER_SETUP.md` and against the real RetroBat install.

Worth an actual probe, because each one changes code:

- **Where the manifest lives in a real RetroBat 8.2.1 install**, and whether it is
  byte-identical to the vendored copy, the way `es_savestates.cfg` turned out to be. The plan
  asserts it is "present in the tree" and nothing has checked. If it is absent, or stale
  against the installed `emulatorlauncher`, that settles the bundled-versus-live question.
- **What `GET /api/platforms` costs today**, re-measured rather than quoted: F5 has 656
  records over 79 platforms in 424 KB and 0.39 s. Confirm the shape still holds, and confirm
  `firmware[]` is complete rather than a preview against one dedicated per-platform call.
- **The download route's real behaviour.** Ranges, `Content-Length`, what a resumed request
  answers, and what happens on a name with a space or a bracket in it. Firmware uses
  Starlette's `FileResponse`, so M3's resume and `.part` machinery should apply unchanged;
  prove it rather than assuming it.
- **Whether the device token authenticates the firmware routes**, and what a token missing
  `firmware.read` actually returns, so the degraded path in `FeatureAvailability` is driven
  by a status code you have seen.
- **The live gap for two BIOS-dependent platforms**, one that the test library holds
  (`psx`, where `psxonpsp660.bin` is the `is_verified: false` case) and one it does not.
  Quote the counts: required, matched, matched-under-a-different-name, unjoinable, missing.
- **What is already sitting in the install's `bios/`**, because adoption meets a user's own
  files here far more often than it did for ROMs, and because openMSX's user data is in that
  tree. Count what a first run would adopt, what it would skip on a hash mismatch, and
  confirm nothing it would touch is a save state.

Quote the numbers you took and never one you did not. Where a measurement contradicts the
plan, amend `docs/PLAN.md` in this PR and say which fact moved. Probe artifacts go in
`probe-output/`, which is gitignored; if a test needs one, check in the fixture instead.
Never hand-edit a vendored file; if the manifest moved upstream, that is `refresh.sh`.

## The rules that bite in this milestone specifically

- **md5 only, and prove the negative.** A filename join and an `is_verified` filter are both
  one-line changes a later contributor will make in good faith. `is_verified` would have
  discarded 11 of the 49 correct files the measured library holds, and a filename join would
  have discarded 10. Write the test that fails when someone reintroduces either, with those
  filenames in the fixture.
- **`bios/` is shared, so eviction and adoption both have to be conservative.** RomMBat may
  delete only what it downloaded, and there are emulator user data and save states in that
  tree. A file present with the right md5 that RomMBat did not download is adopted as a fact,
  not as something it may later remove.
- **Never overwrite on mismatch.** The plan says warn and leave the file alone. A user's
  working BIOS beats our idea of the correct one, and the report is how they find out.
- **Relative paths, as everywhere.** The manifest path is relative to the RetroBat root by
  construction, which is convenient and also the only form allowed to reach the database.
- **ConnectTimeout on any new handler**, and classify the cancellation. Same rule 5 as
  everywhere else.
- **Offline is a working state.** With the server unreachable, the gap report must still be
  answerable from the manifest plus what is on disk. That is the whole point of principle 1,
  and it is what makes this useful on a handheld away from the network.

## Design questions to put to me rather than to pick

Each of these changes the schema or the user-visible contract, so it is cheap now and
expensive at round two of `/fix-pr`.

- **Does BIOS count against the disk budget, and is it ever evictable?** Media counts, per
  M4. Firmware is small and a platform without it is dead weight, which argues for counted
  but never evicted while any ROM for that system is present. Propose, do not assume.
- **How the unjoinable 179 are reported.** "Cannot verify" is a third state beside matched
  and missing, and it has to read as a fact about the manifest rather than as a fault of the
  user's library.
- **Whether v1 writes outside `bios/`**, and whether MAME's software lists are in scope at
  all.
- **What triggers a BIOS pass.** Every synced folder on every sync, or a `bios` command of
  its own with a dry run, in the shape of `budget` and `evict`. The whole-library report is
  one request, which makes a standalone command cheap.

## Schema

Expect a migration `005`. Same discipline as `003` and `004`: its header states what shape
could not carry the work and why, one migration for the milestone, no column holds an
absolute path, CHECK constraints on anything path- or name-shaped, rebuild rather than ALTER
when adding a CHECK, and copy rows even when you are sure the table is empty.

At least these have no home today: a `local_file` row that is firmware rather than a ROM or
media, a file that belongs to a system rather than to a `rom_id`, and whatever lets the gap
report be produced with the server unreachable.

## Tests the plan already requires

Do not treat these as optional coverage; `/review-pr` checks for the specific test, not for
some test.

- The md5 join, with a fixture where the filename differs and `is_verified` is false. Use the
  real pairs: `segacdbios9303.bin` for `bios_cd_u.bin`, `flash.bin` for `dc_flash.bin`,
  `sega_100.bin` for `saturn_bios.bin`, `pcfxbios.bin` for `pcfx.rom`, `bios.col` for
  `coleco.rom`, and `psxonpsp660.bin` for the flag.
- One md5 owing several destination paths downloads once and writes each path.
- A blank-md5 requirement is reported in its own state and never as missing.
- An existing file with the right md5 is skipped; with the wrong md5 it is warned about and
  left byte-identical.
- The gap report with the server unreachable, from the manifest and local state alone.
- Eviction and adoption leave a file under `bios/` that RomMBat did not download, including
  something under `bios/openmsx/`.
- The relocation test, now with firmware present, still a clean no-op.
- Every manifest destination path passes the filesystem-limit and relative-path checks.

## Working shape

Commits that stand alone and explain why, in the style of the M1 through M4 commits. Scoped
diff, no unrelated cleanups riding along: every extra file is review surface, and review
surface is what the next two sessions cost.

If part of this turns out to be a design question rather than a coding one, stop and ask me
rather than picking. The four above are already on that list.

## What the next two sessions need from you

- A PR body on `.github/PULL_REQUEST_TEMPLATE.md`, with the AI disclosure stating extent,
  not just the fact, and the measurement table in the M3 and M4 bodies' shape. Ticked
  checkboxes are claims a reviewer will check against the diff.
- Every deviation from `docs/PLAN.md` amended in the plan, in this PR, including the 157 and
  94 corrections if they survive your own check.
- No scratch in the tree. Anything left behind comes back as a cleanup list.
- NOTES seeded with the rulings this session already made: the options weighed, what you
  chose, and why. The ledger is what stops round one re-litigating a decision you made with
  more context than the reviewer will have.
- The full `pre-pr-verification` skill run, plus `reference/verify.py`, with a plain
  statement of what you verified and what you did not. `dotnet build -c Release -warnaserror`
  is CI's build; a local Debug build hides exactly the warnings that will bounce the PR.

## Default

Read the scope and show me your reading plus the measurement plan before you write code.
That is the cheapest place for me to correct you.

Commit locally as you go. Ask before pushing, before opening the PR, and before anything
that writes into the real RetroBat install.
