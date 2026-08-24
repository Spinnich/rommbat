---
description: Start M6 stage 2c, class D conversion and the es_settings.cfg writer
argument-hint: "[branch name]"
---

# Start M6 stage 2c: the shared container, and the config lever that breaks it apart

Fresh session, branched off main. This is the session that writes the branch. What follows it is
`/review-pr` in a second fresh session and `/fix-pr` in a third, so the job is not "working code",
it is a branch that survives a reviewer who will have far less context than you and the repo's own
rules as its standing authority.

Stage 1 shipped as PR #30, 2a as PR #32, 2b as PR #35, all on main, plus a seven-PR issue sweep
after 2b. **This is the last stage of M6**, and it is the only one that writes into the user's
RetroBat configuration rather than only reading it. Being wrong here does not lose a save by
mishandling it, it loses one by pointing an emulator at a different container while the old one
still holds the user's game.

---

Variables for this run:

- BRANCH = $1, default `m6-stage2c-class-d-and-es-settings`. Branch off main and stay off it; the
  pre-push hook will stop you anyway.
- NOTES = `~/rommbat-work/<PR>.md`, started once the PR number exists. `~/rommbat-work/35.md` is
  2b's ledger and it runs to four sections past the review, `32.md` is 2a's and `30.md` is stage
  1's. Read 35 in full before designing.

## Who is who

I am Spinnich, the only human on this repo. Claude writes the branch, a fresh Claude reviews it, a
third fixes it. You are the first of the three and the only one that will ever hold the reasoning
behind the code.

Anything true only in your head is invisible to the review, and the review is allowed to rule
against it. Put every non-obvious decision somewhere durable: a commit message, a line of
`docs/PLAN.md`, or a test that fails when someone undoes it.

Skip the preamble and the progress narration. Show me decisions, measurements, and diffs.

## Ask me this first, before the reading plan

**How much of class D is M6's, and does the milestone close in this PR or the one after it.**

The plan's stage table gives 2c one row, "class D conversion and the `es_settings.cfg` writer",
and the milestone's "done when" names exactly one thing: a PS2 battery save after opting that game
into a per-game memory card. Everything else this stage could take is breadth:

1. **The writer plus PS2.** `es_settings.cfg` read, merged, written atomically, the per-game key
   built from `fs_name`, `pcsx2_slot1_memory=game` behind an opt-in, and the resulting rom-named
   card syncing as ordinary class A. This alone closes M6's "done when".
2. **The rest of class D.** Dreamcast's `flycast_vmupergame`, which converts into a serial-keyed
   file rather than a rom-named one and therefore needs the attribution routes; PS1's stock
   DuckStation cards, which are database-keyed and must be read rather than converted; and
   everything that stays shared reported with a reason.
3. **The bundled download grammar**, which `docs/PLAN.md` assigns to 2c in the sync-protocol
   section rather than in the stage table. It is class C work, not class D, and it is the one
   piece here with no relationship to the writer at all.

2b's ruling 1 went against splitting because the expensive design landed in PR1 either way and the
second PR would have duplicated the reviewer's context. **That argument is weaker here**: the
writer is shared, but nothing in 2 or 3 changes its design, and 3 does not touch it.

Tell me which of the three this branch carries, and whether the PR that lands last also carries the
M6 closing claim (the stage table, the README row, and the statement that the four shapes are
proven), or whether that is a separate closing PR. Say what each option leaves working but
unshipped.

## Read before you design

1. **CLAUDE.md**, in full. Rule 2, never edit an emulator INI, has been a rule with no exercise for
   five milestones. **This is the stage that writes its alternative**, and every generator
   (`Duckstation.Generator.cs`, `Pcsx2.Generator.cs`, `Dolphin.Generator.cs`) rewrites its INI at
   launch, so an INI edit here is not merely against the rules, it is silently undone. Rule 1 binds
   the path to `es_settings.cfg` itself.
2. **`docs/PLAN.md`**, M6 in full, reading every paragraph headed "Amended after M6 stage 1", "2a"
   or "2b" as the current position rather than the surrounding text. Then, specifically: the class
   D section and its four-option table, the DuckStation measurement that reversed the plan's own
   earlier preference, the `es_settings.cfg` precedence block, the attribution routes as 2b amended
   them, the risks table rows on shared cards and multi-disc sets, and the sync-protocol paragraph
   that hands 2c the bundled download case. M4's gamelist rules are the precedent for writing into
   a file ES owns.
3. **`~/rommbat-work/35.md`**, in full and past the review: it carries the rulings, the decisions
   taken without asking, six defects with where each was caught, two hands-on passes, and an open
   list. Then `32.md` and `30.md`. Do not re-litigate a ruling in there without saying you are
   doing it and why.
4. **`docs/retrobat-findings.md`**: the per-game override probe with its six cases A to F, the
   finding that ES rewrites `es_settings.cfg` only when a setting changed and keeps keys it cannot
   understand, the default-pruning result, and the finding that a 200 from the ES API is not
   evidence the action happened (with finding 107 on `/reloadgames` alongside it).
5. **`docs/freegosy-findings.md`**, F18 first, then F17.
6. **Skills**: `save-sync` and `retrobat-layout` first, both of them fully, then `platform-mapping`
   for the `(system, emulator)` join, `offline-and-portable`, `romm-api` before choosing an
   endpoint, `pre-pr-verification` before claiming anything is done, and `platform-certification`.
7. **The code you are building on**, all on main: `SaveShapes`, `SaveUnitPath`, `SaveUnitScanner`,
   `SaveUnit`, `SaveArchive`, `SaveUnitTransfer`, `GameIdAttributor`, `SaveScanner`, `StateScanner`,
   `SaveGuard`, `SaveSync` including `DownloadAsync`, `SaveConflictResolver`, `RomIndex`,
   `LaunchLog`, `PlaytimeCorrelator`, `EmulationStationClient`, `EsSystemsFile`, and
   `GamelistDocument` with `GamelistSync`, which is the existing answer to "merge into a file ES
   owns and write it atomically". Then `SavesCommand` and its unsyncable report, and the stores.
   Read the headers of `006` through `009` before you write `010`.

## Where 2b left off

Classes A, B and C sync. Save states sync push-only across 13 emulators. Conflicts persist and
`saves resolve` settles them. Attribution has three routes, journal, ROM header and the save-state
sidecar, with a learned binding cached in `game_id_binding`, a negative binding row, a contested
value when routes disagree, and `saves bind` to settle or forget one by hand. A class C unit is a
`(container, key)` pair declared in `save_shapes.json`, bundled to one deterministic archive,
identified locally by the logical fold and on the wire by the digest the server last returned.

What does not exist at all:

- **Nothing in this repository reads or writes `es_settings.cfg`.** Grep for it across `src` and
  there are zero hits. The precedence chain, the escaping, the per-game form and the ES rewrite
  behaviour are all measured and all still only in documents.
- **No conversion of any kind**, and no record of one. Nothing knows which games RomMBat opted in,
  what the setting was before, or how to put it back.
- **Class D is reported and not handled.** `UnsyncableReason.SharedContainer` exists and is the end
  of the story for `ps2`, `dreamcast` and `xbox`, and `megacd` is class `BD` where only the B half
  moves.
- **The download side refuses a bundled slot this device holds nothing in**, with a reason, because
  it has no container and no unit key to place under. The plan calls that 2c's.
- **`save_shapes.json` still carries 21 `_unclassified` systems and no `ports` entry.** 2a and 2b
  both left them alone deliberately, each having measured nothing that shortened the list. Shorten
  it by what you actually prove and leave the rest.

Carried open across two or three ledgers, and this is the last stage that can settle any of it
inside M6: `MatchLaunch`'s 24 hour window and the unused `SuspiciouslyLong`; `IsOwnUpload` on the
download skip path, which 2b found is dead for class C by construction; the hook-spawn cost,
deferred three times with the reason recorded each time; `overwrite=true` appending rather than
replacing, observed on hardware and never probed properly; MAME's short-name join, structurally
sound and unprovable on this install; Wii's grammar, derived from tree structure with no game ever
launched. Issue #37, the sidecar route's underscore split, is attribution work and Dreamcast leans
on that route.

## Scope, which is the plan's stage 2c column

- **The `es_settings.cfg` writer**: locate it from the RetroBat root, parse what is there, merge,
  write atomically, preserve everything ES and the user put in it, and emit the per-game form
  `<system>["<rom filename>"].<key>` with ES's own escaping.
- **Class D conversion, opt-in, per game, reversible, and never silent.**
- **PS2 through `pcsx2_slot1_memory=game`**, which names the card after the rom basename and drops
  it into ordinary filename attribution.
- Dreamcast, PS1 and the download grammar, to whatever extent the split question puts them here.
- **`SaveGuard` widened to class D**, so eviction refuses a ROM whose converted card has never been
  uploaded.
- Whatever stays shared, still reported with a reason, and `xbox`'s 39 MB disk image never read.

**Done when**: a PS2 battery save, written by a real game after that game was opted into a per-game
memory card, goes up and comes back down, on a real install, driven through EmulationStation. That
is the fourth of the four shapes the milestone's "done when" names, and **with it M6 is claimable
for the first time**, so this stage owes the sentence that says so and the evidence under it.

**Out of scope**: the gamepad UI (M7) and packaging (M8). **Migrating saves out of a shared
container is a decision, not an omission**: the plan says switching modes strands the old
container's contents where the game will no longer look for them, and that parsing a memory card
format is real work that must be scoped explicitly. Say whether it is in or out, and if it is out,
say exactly what the user is told before the switch.

## What the measurements already say

All of this is measured and checked in. Verify each before building on it, then decide what it
means. Several are single lines of code that look obviously right and are wrong.

- **The per-game override works, in both halves, and the key must carry the extension.** Six cases
  drove it: system scope is honoured, per-game beats system, the override does not leak to another
  rom, and `ports["gong"].smooth` was **ignored** where `ports["gong.libretro"].smooth` took
  effect. **Getting this wrong fails silently**: the emulator launches normally and keeps writing
  to the shared container. Build the key from RomM's `fs_name`, never from a stem.
- **ES rewrites `es_settings.cfg` only when a setting changed that session**, and start-and-quit,
  and even a session that launched a game, left the file untouched. When it does rewrite, it
  **keeps keys it cannot understand**, including a deliberate nonsense key. So the hazard is
  ordinary two-writer contention and not ES eating the override.
- **ES prunes any setting whose value equals its own default**, measured on `Language`. A custom
  key has no default to match. **Never read a missing entry as evidence the user reverted
  something**, which is the same default-pruning that bit the gamelist in M4.
- **A 200 from the ES API is not evidence the action happened.** `/quit` returned cleanly and left
  ES running with RetroArch up. Anything that closes ES before touching the file has to poll for
  the process to actually exit.
- **PCSX2 keys on the rom basename and cannot bind discs at all**, so a multi-disc PS2 game loses
  its save at the disc change that the stock shared `Mcd001.ps2` would have carried. Convert single
  disc titles, leave sets shared, and report why. This is what makes the conversion a per-game
  decision rather than a per-system one.
- **Do not convert DuckStation.** The stock `PerGameTitle` binds a disc set through DuckStation's
  own database, with or without an `.m3u`: a two-disc set and a three-disc loose set each produced
  one card. The stem is the `gamedb` `saveName` with the disc marker removed, a third string that
  is neither the rom name nor the gamedb name, and **130 of 698 disc-set stems keep a subtitle
  behind the marker** where the two readings of the rule disagree. The mapping from a PS1 card to a
  `rom_id` is many-to-many. `PerGameFileTitle` is the regression that looks like an improvement.
- **A converted Dreamcast VMU is Game-ID keyed, not filename keyed.** Driven at M0:
  `vmu/T40217N_vmu_save_A1.bin`, the disc product number, with `Bangai-O (USA).chd` appearing
  nowhere in the path, and the shared files untouched beside it. **Port 1 only**; B, C and D stay
  shared and unattributable. Route 2 reads no `.chd`, so this needs route 1 or route 3.
- **PS1 is only per-game when DuckStation is the selected emulator**, and the measured install runs
  libretro for `psx` and writes plain class A `.srm`. Shape is a property of `(system, emulator)`.
- **`dolphin_sync_saves` moves save files between two locations behind our back**, and all four
  class D options plus that one are **unset** on the measured install, so stock is the case to
  build for.
- **`xbox` is `eeprom.bin` plus a 39 MB `xbox_hdd.qcow2` loose at the system root**, and it is 38
  MB of the 43 MB that stage 1's whole loose-file workload reads. It must not be read at all.
- **`megacd` is class B and D at once**, per-game `.brm` and `.srm` beside a shared 512 KB
  `4Mbit_cart.brm`, so excluding a shared container is a named list and never a positional rule.
- **Some games legitimately read another game's save** from the same card. Per-game cards break
  that by design, and the user has to be told where the option is offered.

## Measure before you commit to a shape

Every milestone so far landed a measurement commit that amended `docs/PLAN.md`, and each found
something that changed the code. Probe against the live instance in `DEVELOPER_SETUP.md` and
against the real RetroBat install.

Worth an actual probe, because each changes code:

- **The one two-writer case M0 did not cover.** Every override it measured was written **before**
  ES started, and ES round-tripped it. Nothing has measured a key written **while ES is running**,
  which is the case the agent is actually in. If ES holds the file in memory from boot and writes
  it whole on a dirty exit, a mid-session write is lost and the whole design has to move to
  "write while ES is down, or verify after it exits".
- **The PS2 conversion end to end.** Write the override for one real single-disc game, launch it
  through ES, and read what PCSX2 actually produced: the card's exact name and path, whether it
  carries the extension, what slot 2 does, and what happened to `Mcd001.ps2`.
- **The stranded save.** Write a save on the shared card first, then convert, then look for it.
  That is the number that decides whether migration is scoped or refused.
- **Whether the converted card attributes as plain class A** through the existing scanner without
  a new route, which is the claim the whole PS2 story rests on.
- **Dreamcast, if it is in scope**: re-drive `flycast_vmupergame`, confirm the serial-keyed name,
  and check whether route 1 or route 3 resolves it on a game with no observed launch.
- **DuckStation stems against the real library, if PS1 is in scope**: how many of the install's
  `psx` roms resolve to a card stem, and how many of the 130 subtitled sets are present.
- **The hook-spawn cost**, deferred by all three previous stages with the reason recorded. It needs
  the hook binary on a real install and a game launched to time it, and this is the last M6 stage
  in which it can be taken. If it is deferred a fourth time, say so plainly and say what it would
  take, rather than letting it disappear into M7.

Quote the numbers you took and never one you did not. Where a measurement contradicts the plan,
amend `docs/PLAN.md` in this PR and say which fact moved. Probe artifacts go in `probe-output/`,
which is gitignored; if a test needs one, check in the fixture. Never hand-edit a vendored file.

**Probes that write into the real install need my say-so first, every time**, and this stage's
probes write into my EmulationStation configuration rather than into a save tree, so the bar is
higher, not lower. Tell me how to put back anything you change.

## The rules that bite in this stage specifically

- **Never edit an emulator INI.** The option is the lever; the INI is regenerated from it.
- **Opt-in, explained, reversible, per game.** It mutates the user's RetroBat configuration.
  Never flip it silently, and never flip a whole system when the plan says per game.
- **Merge, never clobber.** Preserve every key, including ones RomMBat does not recognise, and
  write atomically. The gamelist writer is the precedent and it already learned this.
- **A missing entry means nothing.** ES prunes defaults. Absence is not a revert, and "the user
  turned it off" has to be distinguishable from "the value equalled the default".
- **Detect a setting the user made themselves and do not take it over.** RomMBat owning a key it
  did not write is how a user's deliberate choice disappears.
- **Never persist an absolute path**, including the path to `es_settings.cfg`.
- **Fail closed.** A converted Dreamcast VMU is identifier-keyed, and where attribution is
  uncertain the file is kept, reported, and never bound to a guessed `rom_id`.
- **Never convert a multi-disc set**, and say why in the report rather than skipping it silently.
- **The guard is the data-loss seam.** Eviction must refuse a ROM whose converted card holds a save
  that has never been uploaded, and the guard's question has to survive the shape changing under
  it when a game is converted.
- **Offline is a working state.** Everything queues, every flush is idempotent under replay and
  partial failure. Conversion is a local operation and must not need the server.
- **The hooks never touch the network**, and nothing in this stage belongs in a hook.

## Design questions to put to me rather than to pick

- **What the opt-in surface is**, given M7 owns the UI: a `saves convert` verb beside `saves bind`,
  a flag on `sync`, a per-game field on the sync set, or something else. Whatever it is, M7 has to
  be able to drive it later without a redesign.
- **Whether the writer refuses to run while ES is up**, waits, or writes and verifies afterwards,
  which the two-writer probe should decide rather than taste.
- **What un-converting does with the saves written under the per-game card**, which is the mirror
  of the stranded-save question and is the one a user hits second.
- **Whether stranded-save migration is in scope**, or refused with a warning.
- **Whether Dreamcast and PS1 land in M6 at all**, or are reported with a reason and deferred, and
  what that does to the milestone's claim.
- **Whether the bundled download grammar is 2c's**, as the plan says, or its own PR.
- **What slot a converted class D save takes**, since a rom-named PCSX2 card is class A by shape
  but not by provenance, and telling the two apart later may matter.
- **Whether `dolphin_sync_saves` detection lands here**, since it is the remaining "RetroBat moves
  files behind our back" case and nothing has looked for it yet.

## Schema

Expect a migration `010`. Same discipline as `003` through `009`: its header states what the
existing shape could not carry and why, one migration for the stage, no column holds an absolute
path, CHECK constraints on anything path- or name-shaped, rebuild rather than ALTER when adding a
CHECK, and copy rows even when you are sure the table is empty.

What it plausibly has to carry is the conversion record: which `(system, rom)` pairs RomMBat opted
in, when, and **what was there before**, because reversibility needs the prior value and "the key
was absent" is a different prior state from "the key was set to the stock value". If you conclude
no new table is needed, say that, and say what you deliberately did not add.

## Tests the plan already requires

`/review-pr` checks for the specific test, not for some test.

- The writer round-trips a real `es_settings.cfg`, preserves unknown keys, reproduces ES's own
  `&quot;` escaping, and writes atomically.
- The per-game key carries the rom's extension and is built from `fs_name`; a stem-only key is
  refused rather than written.
- An override written for one rom does not affect another.
- A value equal to the stock default is not read as a revert when it disappears.
- A multi-disc set is refused conversion, with the reason reaching the report.
- A shared container is never uploaded, and `xbox`'s disk image is never opened.
- Reverting a conversion restores the prior state, including the absent case.
- Eviction refuses a ROM whose converted card has an un-uploaded save.
- A converted Dreamcast VMU resolves by serial or is reported, never guessed (if Dreamcast is in).
- The offline simulation, extended to class D, still asserting that every operation completes
  locally or queues and that the flush is idempotent under replay and partial failure.
- The relocation test, now with an `es_settings.cfg` override present, still a clean no-op.
- Every path this stage constructs passes the filesystem-limit and relative-path checks.

## Working shape

Commits that stand alone and explain why, in the style of the M1 through M6 commits. Scoped diff,
no unrelated cleanups riding along: every extra file is review surface, and review surface is what
the next two sessions cost.

If part of this turns out to be a design question rather than a coding one, stop and ask me rather
than picking. On this milestone I would rather be asked twice than told once.

## What the next two sessions need from you

- A PR body on `.github/PULL_REQUEST_TEMPLATE.md`, with the AI disclosure stating extent, not just
  the fact, and the measurement table in the M3 through M6 bodies' shape.
- Every deviation from `docs/PLAN.md` amended in the plan, in this PR, including anything that
  supersedes an "Amended after M6 stage 1", "2a" or "2b" paragraph.
- The stage table in `docs/PLAN.md` updated, and **if this is the PR that closes M6, the
  milestone's own "done when" answered explicitly**, naming the four shapes and what proved each.
  A shape proven by a test and not by an emulator is named as such.
- The other four documents brought with it, per the `pre-pr-verification` trigger table: the
  `README.md` stage table and its claims about what syncs, `docs/ARCHITECTURE.md` for the store and
  the save model, `DEVELOPER_SETUP.md` for anything a developer now types, and the skills. **Two
  skills are owed here, not one**: `save-sync` for the class D rules and `retrobat-layout` for
  everything the writer learned about a file ES owns. Say which you moved and which you read and
  found already correct.
- No scratch in the tree.
- NOTES seeded with the rulings this session made, carrying forward anything still open from
  `30.md`, `32.md` and `35.md`, and flagging what has now survived three ledgers untouched, because
  after this stage there is no more M6 to carry it into.
- The full `pre-pr-verification` skill run, plus `reference/verify.py`, with a plain statement of
  what you verified and what you did not. `dotnet build -c Release -warnaserror` is CI's build.
  `trunk` runs through WSL here, it is not on the Windows PATH. Build and test from a fresh clone
  too; that is the check that catches a `.gitignore`-swallowed fixture locally instead of in CI.
- One hands-on pass on the shape this stage adds, per `docs/platforms/README.md`: the PS2 card,
  opted in, written by the game, synced. If the session cannot take one, name the claims that are
  unproven for that reason rather than letting them read as verified.

## Default

Answer the split question first. Then read the scope and show me your reading plus the measurement
plan before you write code, with the ES two-writer probe first in it, because it can move the
design rather than only confirm it. That is the cheapest place for me to correct you.

Commit locally as you go. Ask before pushing, before opening the PR, and before anything that
writes into the real RetroBat install or its configuration.
