---
description: Start the M3 milestone, content sync and the disk budget
argument-hint: "[branch name]"
---

# Start M3: content sync and the disk budget

Fresh session, branched off main. This is the session that writes the branch. What follows
it is `/review-pr` in a second fresh session and `/fix-pr` in a third, so the job is not
"working code", it is a branch that survives a reviewer who will have far less context than
you and the repo's own rules as its standing authority.

---

Variables for this run:

- BRANCH = $1, default `m3-content-sync-and-disk-budget`. Branch off main and stay off it;
  the pre-push hook will stop you anyway.
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
2. **`docs/PLAN.md`**, the sections that govern this change, not all 1800 lines: M3, M2
   (what you consume), core principles 1, 2 and 4, "Verification", and M0 probes 6 and 7
   for what has already been measured about resumability, the 21 s stall, and the portable
   move.
3. **Skills**: `offline-and-portable` and `romm-api` before designing, `retrobat-layout`
   for where files land, `pre-pr-verification` before claiming anything is done,
   `platform-certification` if you end up claiming a system works.
4. **The code you are building on**, not just its surface: migration `002-sync-sets.sql`
   and its header, `SyncSetStore`, `SetResolver`, `RomPager`, `RomRow`, `EsSystemsFile`,
   `RelativePath`, `LocalStore`.

## Where M2 left off

Sets resolve. `sync_set_member` holds, per rom, the resolved RetroBat folder, `fs_name`,
`fs_extension`, size, ordering position and a state that already distinguishes a member
from a departure from an exclusion. Extension filtering and platform mapping are done and
are not yours to re-decide. `002`'s header says `excluded_over_bytes` was declared for
M3's eviction pass to write into, so check that it fits before inventing a state.

Nothing has ever been downloaded. There is no inventory of what is on disk, and `RomRow`
carries no hashes. M3 turns resolved membership into files in the RetroBat tree plus a
local record the next run can trust without the server.

## Scope, which is the plan's M3 and nothing else

- Download `GET /api/roms/{id}/content/{fs_name}`, always sending `Range: bytes=0-`.
- Adopt files already on disk by hash rather than re-downloading them.
- Verify by size and hash, remembering `crc_hash` is the CRC of uncompressed content.
- Enforce per-set and global disk budgets. Eviction is a first-class operation with a
  dry-run that shows what would go before anything goes.
- Reconcile deletions against `GET /api/roms/identifiers`.
- Resume from `.part` after power loss or a dropped link.
- Detect the target filesystem before writing, and refuse ROMs over 4 GB on FAT32 with a
  clear message rather than failing partway through the write.
- Compare on `content_hash` first, mtime second.

**Done when** (the plan's words): a set syncs to completion, a second run is a no-op
including after the drive letter changes, an interrupted download resumes, exceeding the
budget evicts predictably rather than filling the disk, and a FAT32 target refuses
oversized ROMs cleanly.

**Out of scope**: gamelists and media (M4), BIOS (M5), saves, states and playtime (M6),
the gamepad UI (M7). If the work needs something a later milestone owns, define the seam
and stop there.

One exception, and it is deliberate: eviction must refuse to remove anything with unflushed
local saves, and M6 owns saves. Build that check against the seam that exists today, fail
closed when it cannot answer, and write down in the plan what M6 has to connect to it.

## Measure before you commit to a shape

M1 and M2 each landed a measurement commit that amended `docs/PLAN.md`, and each found
something that changed the code: the `int32` overflow, the non-unique slug, the 500 on a
full device payload. Assume M3 has its own and go looking before you have written a
thousand lines around a guess. Probe against the live instance in `DEVELOPER_SETUP.md`.

Worth an actual probe:

- Whether `Range: bytes=0-` really takes the cached-zip path for a multi-file ROM, and what
  arrives when it does not.
- Validator stability: does the `ETag` survive a server restart or a re-scan? That decides
  whether a `.part` from yesterday can be resumed or must be discarded, and a stale
  `If-Range` that splices silently is the worst outcome this milestone can produce.
- What `GET /api/roms/by-hash` accepts and answers, and whether it beats hashing locally
  for the adoption pass on a library that is already populated.
- The shape and cost of `GET /api/roms/identifiers` on a real library, since deletion
  reconcile runs every sync.
- Where hashes come from at all. `RomRow` carries none, so find out whether the paged read
  can carry md5 and sha1 without the sidecar cost M2 measured, or whether adoption needs a
  per-rom detail call.
- Whether `fs_size_bytes` agrees with `Content-Length` for single and multi-file ROMs. The
  budget is arithmetic on that number, so an error there compounds across a whole set.

Quote the numbers you took and never one you did not. Where a measurement contradicts the
plan, amend `docs/PLAN.md` in this PR and say which fact moved. Probe artifacts go in
`probe-output/`, which is gitignored; if a test needs one, check in the fixture instead.
That was already a correction once, in `01e418a`.

## The rules that bite in this milestone specifically

- **Absolute paths.** M3 is the first milestone with a file inventory, so this is where it
  goes wrong. Relative to the RetroBat root, through `RelativePath`, with the CHECK
  constraint and a row in the `LocalStoreTests` bad-values table.
- **`.part`, verify, rename.** A power loss must never leave a partial file that ES will
  happily list and try to launch.
- **ConnectTimeout on the download handler too**, then classify the failure. A user
  cancelling and a drive being yanked are both `TaskCanceledException`, and the user needs
  to see the difference.
- **Nothing outside the RetroBat tree**, including wherever `.part` files live.
- **FAT32's 4 GB ceiling is checked before the download starts**, not discovered at byte 4294967296.
- **Extensions and folders are settled.** M2 filters and resolves; do not re-decide either.

## Schema

Expect a migration `003`. Same discipline as `002`: its header states what shape could not
carry the work and why, one migration for the milestone, no column holds an absolute path,
CHECK constraints on anything path- or name-shaped, rebuild rather than ALTER when adding a
CHECK, and copy rows even when you are sure the table is empty.

## Tests the plan already requires

Do not treat these as optional coverage; `/review-pr` checks for the specific test, not for
some test.

- A no-op re-sync: zero downloads, zero writes, and it says so.
- The relocation test: populated install, different root, next sync is a clean no-op.
- The offline stub switched to unreachable mid-download, and a resume that produces a
  byte-identical file.
- The FAT32 4 GB refusal and coarse-mtime handling.
- Eviction ordering, and a dry-run that matches what the real run then does.
- Bounded memory and bounded request count against the synthetic large-catalog fixture.

## Working shape

Commits that stand alone and explain why, in the style of the M1 and M2 commits. Scoped
diff, no unrelated cleanups riding along: every extra file is review surface, and review
surface is what the next two sessions cost.

If part of this turns out to be a design question rather than a coding one, stop and ask me
rather than picking. That is cheap now and expensive at round two of `/fix-pr`, where a
third pass over the same file means the design was wrong all along.

## What the next two sessions need from you

- A PR body on `.github/PULL_REQUEST_TEMPLATE.md`, with the AI disclosure stating extent,
  not just the fact, and the measurement table in the M2 body's shape. Ticked checkboxes
  are claims a reviewer will check against the diff.
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
destructive against the live instance, which in this milestone means any eviction or delete
path pointed at real data.
