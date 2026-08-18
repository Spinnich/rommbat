---
description: Start the M6 milestone, offline-first save, state and playtime sync
argument-hint: "[branch name]"
---

# Start M6: offline-first save, state and playtime sync

Fresh session, branched off main. This is the session that writes the branch. What follows
it is `/review-pr` in a second fresh session and `/fix-pr` in a third, so the job is not
"working code", it is a branch that survives a reviewer who will have far less context than
you and the repo's own rules as its standing authority.

This is the largest milestone in the plan and the only one where being wrong destroys a
user's data rather than costing them a re-download. Both facts change how you work, and the
first one has a question attached that comes before any code. See "Ask me this first".

---

Variables for this run:

- BRANCH = $1, default `m6-saves-states-and-playtime`. Branch off main and stay off it; the
  pre-push hook will stop you anyway.
- NOTES = `~/rommbat-work/<PR>.md`, started once the PR number exists. See "What the next
  two sessions need from you".

## Who is who

I am Spinnich, the only human on this repo. Claude writes the branch, a fresh Claude reviews
it, a third fixes it. You are the first of the three and the only one that will ever hold
the reasoning behind the code.

That is the whole constraint on how you work: anything true only in your head is invisible
to the review, and the review is allowed to rule against it. Put every non-obvious decision
somewhere durable, a commit message, a line of `docs/PLAN.md`, or a test that fails when
someone undoes it.

Skip the preamble and the progress narration. Show me decisions, measurements, and diffs.

## Ask me this first, before the reading plan

M6 as written is roughly four independent pieces: the hooks and the journal, save and state
discovery on disk, the negotiate and transfer protocol, and the class-D conversion. M1
through M5 were each one PR. Four hundred lines of plan in one PR is a review surface no
fresh reviewer can hold, and this is the milestone where a missed detail loses saves.

So propose a cut line and let me pick it before you design anything. Say what lands in this
branch, what the branch leaves working but unshipped, and what the seam between them looks
like in the schema. A staged M6 is fine. A silently narrowed M6 is not, and neither is one
enormous PR nobody can review honestly.

## Read before you design

1. **CLAUDE.md**, in full. Rule 2 is this milestone's sharpest edge: never edit an emulator
   INI, because `emulatorlauncher` regenerates every one at launch. Rule 4, the hooks never
   touch the network, is the second.
2. **`docs/PLAN.md`**, the sections that govern this change: M6 in full, core principle 1 on
   offline-first, principle 4 on portability, M3's disk budget and the `SaveGuard` seam it
   left open, and "Verification", where the offline simulation is called the highest-value
   suite in the project.
3. **`docs/retrobat-findings.md`**, probe 1 (hooks) and probe 2 (save locations and shapes)
   in full. Probe 2 is long and every section of it is load-bearing. Do not skim it: it is
   the difference between a parser that works and one that reads an empty directory as "this
   game has no states".
4. **`docs/freegosy-findings.md`**, F1, F2, F3, F4, F6, F8, F9, F10, F11, F12, F13, F17,
   F18, F19, F20 and F22. F1 and F20 are the two that change the design rather than a
   parameter. F18 is the one that reverses an instinct.
5. **Skills**: `save-sync` before anything, then `retrobat-layout` for the tree and
   `es_settings.cfg` precedence, `offline-and-portable` for the outbox, clock skew and the
   lock file, `romm-api` before choosing an endpoint, `platform-mapping` for the
   `(system, emulator)` join, `pre-pr-verification` before claiming anything is done, and
   `platform-certification`, whose steps 4 and 5 are this milestone and which no platform
   can currently pass.
6. **The code you are building on**: the `journal` and `outbox` tables declared in
   `001-initial.sql` and never yet written to, `OutboxStore`, `ClockStore`, `SaveGuard` and
   its `<remarks>`, which names the exact question you have to answer, `LocalFileStore`,
   `LocalStore`, `SyncCommand`, `BiosSync` and `BiosManifest` as the precedent for bundled
   runtime data, `EsSystemsFile`, `RelativePath`, `FilesystemLimits`, `FeatureAvailability`,
   and migrations `003` through `005` with their headers.

## Where M5 left off

`sync` is four passes: re-resolve the sets, BIOS, ROM content, media, then one merged
gamelist per folder. `local_file` holds one row per file with a `kind` of `rom`, `firmware`
or one of five media kinds, keyed by a path relative to the RetroBat root, and migration
`005` made `folder` nullable so a file can belong to a system rather than to a `rom_id`.

Nothing has ever written into `saves/`. Nothing has ever written a `journal` or `outbox`
row, though both tables have existed since `001` with their CHECK constraints already in
place, including the `kind IN ('save', 'state', 'play_session')` you are about to use.
`RomM.Client` has no saves, states, sync-negotiation or play-session surface at all: the
whole of `Content/` is ROM, media and firmware. There is no `game-start`, `game-end` or
`flush` command in the agent, no hook executable, and no installer for one.

`SaveGuard` exists and fails closed, and its own remarks say what it cannot answer: whether
a save exists on disk that has never been uploaded. Closing that is not a nice-to-have in
this milestone, it is the reason M3 shipped eviction with a mitigation instead of an answer.

`data/retrobat/save_shapes.json` and `save_directories.json` are bundled, generated from a
real install by `tools/m0-probes/probe2-emit-data.py`, and read by nothing. `save_shapes.json`
still carries 21 systems under `_unclassified`. M5 set the precedent for how a bundled
manifest is chosen over a live one and why; follow the reasoning rather than the outcome,
because unlike `batocera-systems.json` these files describe a tree the user's emulators own.

## Scope, which is the plan's M6

- Hook executables for `start`, `game-start`, `game-end` and `quit`, installed idempotently
  beside any existing scripts, uninstallable cleanly, resolving the agent from their own
  module path. They append to the journal and exit. No HTTP, ever.
- The journal, surviving interleaved appends from separate processes, with the lock file the
  plan makes mandatory rather than defensive.
- `emulatorLauncher.log` as the source of launch facts, both files of the rotation, since it
  is the only durable in-tree source carrying the rom path together with `-system`,
  `-emulator` and `-core`.
- Save and state discovery from `es_savestates.cfg`, `save_directories.json` and
  `save_shapes.json`, with attribution for classes A through D.
- The negotiate, upload, download and complete protocol, plus the standalone play-session
  ingest.
- `SaveGuard`'s third question, answered.
- Whatever remains genuinely unsyncable, reported as "not syncable, here is why".

**Done when** (the plan's words): with the server unplugged, play three games, exit, plug
back in, and all three saves plus all three play sessions land in RomM in one flush; then
play the same game elsewhere and the newer save comes back down as a conflict the user
resolves. Proved on one game from each save shape, not three class-A games.

**Out of scope**: the gamepad UI (M7), packaging (M8). Do not certify platforms in this
branch; M6 gives certification steps 4 and 5 something to run, and running them is a
separate PR.

## What the measurements already say, and what the plan section does not fold in

All of this is measured and checked in. Verify each before building on it, then decide what
it means. Several of these are single lines of code that look obviously right and are wrong.

- **Two `es_savestates.cfg` directories are lies.** `flycast` declares
  `{{system}}/flycast/sstates` and the emulator writes `saves/dreamcast/reicast/states/`,
  while its VMU path really is under `flycast/`. `openmsx` declares `saves/msx1/openmsx/`
  and writes `bios/openmsx/savestates/`, a different tree entirely. Both declared
  directories exist on disk and are empty, which is the trap: an empty declared directory
  means "you are looking in the wrong place", never "this game has no states".
- **Four traps inside the file itself.** `libretro`, the most important entry, declares no
  `firstslot` or `lastslot`. `desmume`'s `<image>` and `<file>` are the identical template,
  so uploading `<image>` as `screenshotFile` uploads the state. `bigpemu` has
  `firstslot="001"` and `lastslot="999"` against a two-digit `{{slot2d}}`. `bizhawk` is
  core-scoped like `libretro`. Twelve of the thirteen emulators have been driven to a real
  state, so the rest of the templates are confirmed rather than assumed.
- **PPSSPP writes states twice and RetroBat mirrors them 120 ms later**, and the
  `ppsspp/<rom filename>.txt` sidecar is the ES-name to game-ID mapping, not disposable. The
  ES-facing directory is the authoritative one. The mirrored screenshot came out correct,
  zero bytes and absent across three saves, so a state screenshot is best-effort by nature.
- **A launch alone writes a battery save** (F20). Master System, booted to the title screen,
  no save key pressed, produced an 8,188-byte `.srm` of legible ASCII. Freegosy's 100-byte
  floor does not catch it and no blankness test can. The consequence is narrow and real:
  the first save seen for a ROM with no local baseline is not evidence anything was played,
  and must not win a conflict on recency alone.
- **mtime cannot decide whether a file needs uploading, for any class.** A PS2 launch
  rewrites both shared memory cards untouched by the game, and F20 says the same for an
  ordinary class-A cart. Content hashing is general, not a class-D special case.
- **The save tree is two levels deep and not uniformly system-keyed.** `saves/<system>/<emulator>/`
  plus loose libretro `.srm` at `saves/<system>/`, plus emulator-named folders at the top
  level (`saves/dolphin/`, `saves/mesen/`, `saves/psxmame/`, `saves/amiga/`) that a
  first-segment-is-a-system parser will mis-attribute.
- **RPCS3 is 32,451 files under `saves/ps3/rpcs3/`** on a real library, and MAME is 1,231
  directories under `saves/mame/nvram/`. Any recursive content hash has a performance
  budget, and MAME is the friendly case because its short name is the rom basename.
- **The hook path arithmetic in the plan is off by one**, and `docs/PLAN.md` still carries
  the wrong count in three places. Three levels up from a hook reaches `emulationstation/`,
  four reaches the root. Fix the plan in this PR.
- **Both scripted hook forms fail on ordinary rom names**, `.bat` on a quoted argument and
  `.ps1` on a parenthesis, and on one of two test machines neither form could start at all,
  silently. Ship executables and still write the heartbeat the plan asks for.
- **`device_syncs` is empty when you do not pass `device_id`** (F8), and a device that never
  synced is absent rather than present with `is_current: false`. Absence means two different
  things depending on the query.
- **`origin_device_id` names the uploading device** (F9) and is not in the plan. It is the
  cheapest way to recognise your own upload coming back down.
- **`/api/saves/summary` is a per-slot inventory for one rom** (F11), `track` and `untrack`
  take `{device_id}` in the body and need `devices.write` (F10), and
  `GET /api/saves/identifiers` takes no parameters and has only ever been measured on an
  empty account (F13, still open, and not a lead to build on).
- **A device-bound token makes `device_id` optional on negotiate** (F22). Ours comes from
  pairing, so it qualifies; send the field anyway.
- **Identical uploads dedup within a slot** (F3), which is what makes replay idempotent, and
  it is exactly what Freegosy destroyed by stamping a timestamp into every archive. Our
  logical-content hash has to be deterministic or dedup cannot work.

## Measure before you commit to a shape

M1 through M5 each landed a measurement commit that amended `docs/PLAN.md`, and each found
something that changed the code. Assume M6 has its own, and it has more unknowns than any
milestone before it. Probe against the live instance in `DEVELOPER_SETUP.md` and against the
real RetroBat install.

Worth an actual probe, because each one changes code:

- **The full negotiate, upload, download, ack, complete round trip with a paired
  device-bound token**, including a real 409 and what a second device sees. The write probes
  so far have been narrow and single-device.
- **Concurrent hooks against the real journal.** Three `game-end` hooks were observed in
  flight at once. Drive that against your lock file and your append path, on the real
  filesystem, not a unit test with a fake clock.
- **The `emulatorLauncher.log` parser against the real file**, 268 KB for 5 weeks and 70
  launches, including a rotation happening between two reads, and including launches that
  failed outright.
- **The cost of hashing what is really there.** Time a logical-content hash over RPCS3's
  32,451 files and over MAME's 1,231 directories. If it is not viable, that is a design
  input and not a footnote.
- **What the four hook events actually fire on a normal session**, including RomMBat's own
  exit firing `game-end`, an ES-menu launch, and a launch that fails. Count the orphans.
- **`es_settings.cfg` contention** if class-D conversion is in your cut. ES rewrites the file
  only when a setting changed, preserves keys it does not know, and prunes any value equal to
  its own default. Write while ES is idle, merge, write atomically, and measure it.

Quote the numbers you took and never one you did not. Where a measurement contradicts the
plan, amend `docs/PLAN.md` in this PR and say which fact moved. Probe artifacts go in
`probe-output/`, which is gitignored; if a test needs one, check in the fixture instead.
Never hand-edit a vendored file.

**Probes that write into the real install need my say-so first, every time.** This milestone
probes a tree that holds someone's actual saves. Read-only inventory is fine; anything that
writes, deletes or flips an ES option is a question, not a step.

## The rules that bite in this milestone specifically

- **The hooks never touch the network**, and they run in the game-launch path. Append and
  exit. This is CLAUDE.md rule 4 and the review will check it as a rule, not a preference.
- **`optimistic=false` and the ack travel together.** The ack alone is decoration, because by
  then the server already believes the device is current. This is the same discipline as M3's
  verified `.part` rename, and F1 is what happens without it.
- **The name to persist and the name to write on disk are different fields.** Persist
  `file_name` from the response as server identity; write `file_name_no_tags` plus
  `file_extension` on disk, or the emulator will not find the save. No client-side regex.
- **Slots are non-null and stable.** A null slot is an archival upload, is excluded from
  pairing, and negotiates as `upload` forever.
- **Hash the logical contents, never the archive bytes.** Sorted relative paths plus each
  file's own hash, folded into one digest. Deterministic across implementations, or dedup
  and conflict detection both break.
- **Restores are atomic.** Extract beside the target, verify, swap, keep the previous copy
  until the next successful sync. A half-written directory save is a corrupt one.
- **Never edit an emulator INI.** `es_settings.cfg`, and for a per-game override the key must
  include the ROM's extension, built from RomM's `fs_name`. A bare stem is ignored silently.
- **Anything that mutates the user's RetroBat configuration is opt-in, explained and
  reversible**, and switching modes strands existing saves in the old container.
- **Never persist an absolute path**, including anything parsed out of `emulatorLauncher.log`,
  which is full of them.
- **ConnectTimeout on any new handler**, and classify the cancellation.
- **Offline is a working state, and here it is the headline feature.** Everything queues,
  every flush is idempotent under replay and partial failure, and the local mtime goes on the
  wire as `updated_at`, never the sync time.
- **Fail closed.** Where you cannot tell whether a save is safe, keep the file and say why.
  `SaveGuard` already sets that precedent, including on an unreadable database.

## Design questions to put to me rather than to pick

Beyond the cut line above, each of these changes the schema or the user-visible contract, so
it is cheap now and expensive at round two of `/fix-pr`.

- **Retention and conflict defaults together.** `autocleanup` is false and the limit is 10,
  and `keep_both` under an unbounded slot is how a library becomes unusable. These are one
  decision, not two.
- **Class B: one slot per file, or bundle as class C.** Saturn writes `.bcr` and `.bkr`, and
  megacd is B and D at once.
- **How far state sync goes in v1.** It is outside the negotiate protocol, best-effort push
  only, and bounded to the 13 emulators in `es_savestates.cfg`. Say what that means for the
  two whose declared directory is wrong.
- **Whether the class-D conversion ships here at all**, and if it does, whether migrating
  saves out of an existing shared container is in or explicitly refused with a warning.
- **Which attribution routes v1 carries.** The journal correlation is route 1 and reuses what
  this milestone builds anyway. Reading a game ID out of a ROM over a bounded Range is route
  2, and F17 says 17.5% of a real Wii library cannot be read by any constant offset.
- **What triggers a flush**, and whether hook installation is opt-in or happens on first sync.

## Schema

Expect a migration `006`. Same discipline as `003` through `005`: its header states what
shape could not carry the work and why, one migration for the milestone, no column holds an
absolute path, CHECK constraints on anything path- or name-shaped, rebuild rather than ALTER
when adding a CHECK, and copy rows even when you are sure the table is empty.

At least these have no home today: a save file on disk with its logical content hash and
whether it has ever been uploaded, which is what `SaveGuard` needs; the server-side identity
of a slot (`save_id`, `file_name`, `content_hash`, `server_updated_at`, `origin_device_id`);
a learned game ID to `rom_id` binding, so an odd case is observed once rather than every
sync; the read cursor into `emulatorLauncher.log` across a rotation; and whatever records
that a platform is unsyncable and why. `journal` and `outbox` already exist, so say what you
are adding to them and what you deliberately are not.

## Tests the plan already requires

Do not treat these as optional coverage; `/review-pr` checks for the specific test, not for
some test.

- **The offline simulation**, driven against a handler that flips to unreachable mid-operation,
  asserting every operation completes locally or queues, and that the flush is idempotent
  under replay and partial failure. The plan calls this the highest-value suite in the repo.
- Outbox replay idempotency, including a partially-failed play-session batch reconciled from
  the per-index result array rather than inferred.
- An orphan `game-end` is discarded, not attributed to whatever ran last, and RomMBat's own
  exit does not become a play session.
- Interleaved appends from separate processes leave a readable journal.
- Slot derivation across the four `es_savestates.cfg` traps, with the shipped file as the
  fixture.
- The logical content hash is stable across archive implementations and across two runs.
- A download that dies mid-body leaves the server not-current, and the ack happens only after
  the bytes are verified.
- The written filename is the untagged one and the persisted one is not.
- A per-game `es_settings.cfg` override round-trips through an ES-written file, keeps keys ES
  does not know, and is keyed with the extension.
- The relocation test, now with saves and states present, still a clean no-op.
- Eviction refuses a ROM with an un-uploaded save on disk, which is the M3 seam closing.
- Every path this milestone constructs passes the filesystem-limit and relative-path checks.

## Working shape

Commits that stand alone and explain why, in the style of the M1 through M5 commits. Scoped
diff, no unrelated cleanups riding along: every extra file is review surface, and review
surface is what the next two sessions cost.

If part of this turns out to be a design question rather than a coding one, stop and ask me
rather than picking. The list above is already long, and on this milestone I would rather be
asked twice than told once.

## What the next two sessions need from you

- A PR body on `.github/PULL_REQUEST_TEMPLATE.md`, with the AI disclosure stating extent, not
  just the fact, and the measurement table in the M3 through M5 bodies' shape. Ticked
  checkboxes are claims a reviewer will check against the diff.
- Every deviation from `docs/PLAN.md` amended in the plan, in this PR, including the hook
  path arithmetic.
- If M6 is staged, the plan says so, and says what the next stage owns.
- No scratch in the tree. Anything left behind comes back as a cleanup list.
- NOTES seeded with the rulings this session already made: the options weighed, what you
  chose, and why. The ledger is what stops round one re-litigating a decision you made with
  more context than the reviewer will have.
- The full `pre-pr-verification` skill run, plus `reference/verify.py`, with a plain
  statement of what you verified and what you did not. `dotnet build -c Release -warnaserror`
  is CI's build; a local Debug build hides exactly the warnings that will bounce the PR.

## Default

Answer the cut-line question first. Then read the scope and show me your reading plus the
measurement plan before you write code. That is the cheapest place for me to correct you.

Commit locally as you go. Ask before pushing, before opening the PR, and before anything
that writes into the real RetroBat install.
