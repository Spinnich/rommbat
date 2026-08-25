---
description: Mine the Argosy launcher for leads, verify each one against a primary source, and land the survivors in the plan, docs and skills
argument-hint: "[branch name]"
---

# Mine Argosy

`rommapp/argosy-launcher` is RomM's native Android client and the most mature companion app
in the org. It has shipped answers to most of what RomMBat still has open: save and state
sync against a live negotiate protocol, per-device ownership, save shapes across sixteen-odd
platforms, download resume and archive expansion, BIOS placement, library sync cost, and a
full gamepad-navigable launcher UI. It was named twice during planning and then never read.
This session reads it, works out which of its answers are true **here**, and folds those into
`docs/PLAN.md`, `docs/`, the skills and the bundled data.

This session writes **no product code**. Its output is documentation, and the reason is that a
wrong fact in `docs/PLAN.md` is more expensive than a wrong function: the function fails a
test, the fact quietly shapes three milestones.

**Timing matters.** M7b, the gamepad UI, has not started. Argosy is a shipped, gamepad-first
RomM launcher, so its UI and input work is the one part of this mining that has a consumer
waiting. Keep those leads clearly separated from the factual ones anyway, per the rules below.

---

Variables for this run:

- BRANCH = $1, default `mine-argosy`. Branch off main and stay off it.
- LEDGER = `docs/argosy-findings.md`, new, in the shape of `docs/freegosy-findings.md`, which
  is in turn in the shape of `docs/retrobat-findings.md`. Read the Freegosy ledger first: it
  is the model for how a mining session records itself, and its rejections tell you which
  questions are already answered.

## Who is who

I am Spinnich, the only human on this repo. Skip the preamble and the progress narration. Show
me the triage table, the experiments, and the diffs.

## The correction this session owes before it starts

`docs/PLAN.md` says Argosy is one of the sources already mined, and
`docs/freegosy-findings.md` repeats it: both assert that Grout, Argosy and the Playnite plugin
"were mined into the plan already, and their rows sit in `docs/PLAN.md`". **Argosy has no row
in "Reference implementations to mine" and never did.** Its only appearances anywhere in the
repo are as a licence precedent and as an example of a RomM client existing. That is a false
statement in the design of record, and per the repo's own rule about docs travelling with
code, this session owns fixing it, whatever else it finds. Fix it as a correction, naming what
was wrong, not by quietly rewriting the sentence.

## Why this source is not Freegosy, and where that does and does not move the bar

Freegosy was held to a high bar for five reasons. Argosy clears four of them:

- It is **in `rommapp`**, alongside Grout and the Playnite plugin.
- It is **GPL-3.0**, same as us. MIT-into-GPL was a live question with Freegosy and is not one
  here. That still does not make this a code-lifting session.
- It **tracks the server we target.** Its declared support floor is the latest three minor
  releases, 4.9, 5.0 and 5.1 as of 2026-08-03, and it keeps a three-version Docker testbed to
  compare response shapes across them. There is no two-minor version skew to discount.
- It is **v2.8.0, released 2026-08-23, with daily commits and about 500 stars.** It is not a
  one-maintainer v0.5.

And it fails the fifth, harder than Freegosy did:

- **It targets Android**: libretro through a vendored `libretrodroid`, plus standalone Android
  emulators. RetroBat is Windows. **No path from Argosy is ever valid here.** Save _shapes_
  transfer, save _locations_ never do, and copying one into
  `data/retrobat/save_directories.json` is the fastest way to put a lie in a generated file.

So the version-skew discount goes away and the platform discount gets sharper. What replaces
the version worry is a subtler failure mode, and it is the one to actually guard against:

**Argosy is well enough documented that reading it feels like measuring.**
`docs/save-id-to-path.md` and `docs/romm-sync-optimization.md` are careful, versioned, and
explicit about which claims came from reading server source and which came from a live
instance. That care is exactly what makes them dangerous to quote: a paragraph that reads like
a measurement is still someone else's measurement, of someone else's library, through someone
else's client. Their sync numbers are median-of-three against a 23,873-rom library on a
5.1.0-alpha.4 instance. Ours is not that library and not that instance.

**One belief with two copies is still one belief.** And note that Argosy agreeing with Grout is
_not_ independent corroboration: same org, same lineage, possibly the same reading of the same
server source.

## The bar a claim has to clear

Each candidate graduates by exactly one route, and the route gets recorded next to it:

| Class of claim                            | What settles it                                                                                              |
| ----------------------------------------- | ------------------------------------------------------------------------------------------------------------ |
| RomM API behaviour                        | A request you made against the live instance, with the request and the response quoted                       |
| RetroBat on-disk behaviour                | A probe against a real install, in the style of `tools/m0-probes/`, driven far enough to see the file        |
| Upstream file content or a derived number | Vendored under `reference/` and re-derived by `verify.py`, never a number typed by hand                      |
| A performance or cost number              | Re-measured here. An Argosy number is a strong reason to measure and is never the measurement                |
| A pure shape or algorithm                 | An argument from first principles **plus** a test that fails if it is wrong. Label it reasoned, not measured |
| A UI or input decision                    | Cannot be verified in the sense this session means. Label it a design lead and keep it out of factual prose  |
| Anything else                             | Stays an unverified lead. It may be recorded, and it may not enter `docs/PLAN.md` as fact                    |

None of the following is verification: Argosy's README says so; Argosy's code does it; Argosy's
tests pass; one of Argosy's design docs asserts it; Grout appears to agree; it is in `rommapp`
so it must be right; it sounds right.

Where a probe is the only route and the probe is not runnable here, say so plainly and leave
the lead open. **An open lead honestly labelled is worth more than a plausible sentence in the
plan**, because the plan is read as settled.

## Triage before verification

Do not verify everything. Argosy is roughly 3,500 files and far larger than anything mined so
far, most of it Android UI and a vendored emulator core that bear on nothing here. Read first,
produce the full candidate list, then rank it by one question: **if this were true, what would
change in RomMBat?** A finding that changes no decision, no schema, no data file and no skill
is dropped at triage and recorded as dropped. It never gets a probe.

For each survivor, record five things:

1. The claim, one sentence.
2. What in RomMBat it would touch: a milestone, a doc section, a skill, a data file.
3. What would have to be true for it to hold here.
4. The cheapest experiment that settles it.
5. The verdict, once run.

Show me that table before you start running experiments. That is the cheapest point for me to
cut half of it.

## Where to look, and what it bears on

Recon was run on 2026-08-25 against `main` at `3971bee4`, so these paths were real then. The
readings of what each file _does_ are inferred from names and from the two design docs, and
several will be wrong. Treat the annotations as guesses and the paths as the only part checked.

**Highest value, because it lands on decisions we have already made and could have got wrong:**

- `docs/save-id-to-path.md`. The single densest file in the repo for us. It defines a five-way
  taxonomy of how a save id spends itself on disk, `FOLDER_EXACT`, `FOLDER_PREFIX`,
  `FILE_EXACT`, `FILE_PREFIX`, `FOLDER_SPLIT`, and derives a path per platform from it. Our
  equivalent is the four save shapes in `save-sync` plus `data/retrobat/save_shapes.json`.
  **The taxonomy is the finding, if anything is.** Compare the axes, not the paths, and where
  their classification of an emulator disagrees with ours, that is a reason to re-run M0 probe
  2 for that emulator. The probe decides, never the disagreement.
- `app/src/main/kotlin/com/nendo/argosy/data/sync/strategy/`: `NegotiatorSaveSyncStrategy.kt`,
  `LegacySaveSyncStrategy.kt`, `SaveSyncStrategySelector.kt`, `ConflictAutoResolver.kt`,
  `ReconcilePlan.kt`. The names say they gate a negotiate-based path against an older one by
  server version. We refuse below our minimum outright, so their _selector_ is not a lead, but their
  _reconcile plan_ is the closest thing in existence to our full-state reconciliation at
  `docs/PLAN.md` line 68 and the negotiate work at 1763.
- `data/remote/romm/RomMCapabilities.kt`. Version-gated feature detection. We check versions at
  startup and refuse below baseline; this is the other design. Worth understanding what they
  found needed gating, since each gate is a place the server changed shape.
- `data/sync/SaveOwnershipTracker.kt`, `StateOwnershipTracker.kt`,
  `data/local/dao/Save{Ownership,Channel,Sync,Cache}Dao.kt`, `StateTombstoneDao.kt`.
  Attribution and per-device ownership. **This is the direct cross-check on a Freegosy
  rejection**: the Freegosy ledger concluded its per-device isolation model is not what the
  server does. Argosy tracks 5.1 and has a "channel" concept we have no analogue for. Find out
  what a channel is before deciding whether we need one.
- `data/repository/SaveDownloader.kt`, `SaveSyncApiClient.kt`, `SaveSyncOrchestrator.kt`,
  `SaveSyncConflictResolver.kt`. **Check specifically whether they pass `optimistic=false` on
  `GET /api/saves/{id}/content`.** Freegosy finding F1, the one that would have cost real data,
  turned on that parameter. If Argosy passes `optimistic=true` or omits it, that is a live bug
  in a shipped client and worth reporting upstream, which is out of scope for this session but
  in scope for the ledger to note.
- `data/remote/romm/RomMPlaySessionModels.kt`. M7a landed play sessions. Freegosy's
  play-session payload was measured as a 422 at 5.1.x. Argosy's should be correct. **If it
  disagrees with what M7a shipped, that is a defect in landed code, not a lead**, and it gets
  probed first and reported loudest.
- `data/sync/platform/`: `PlatformSaveHandlerRegistry.kt`, `RetroArchSaveHandler.kt`,
  `FolderSaveHandler.kt`, `GciSaveHandler.kt`, `SwitchSaveHandler.kt`, `DefaultSaveHandler.kt`,
  and the four `SwitchSaveHandler*Test.kt` files. Shapes only. `GciSaveHandler` is GameCube and
  overlaps our dolphin work; RetroArch overlaps everything libretro.
- `data/emulator/`: `SavePathRegistry.kt`, `StatePathRegistry.kt`,
  `LibretroSavePathResolver.kt`, `LibretroStatePathResolver.kt`, `SavePathValidator.kt`,
  `StateSupportResolver.kt`, `savepath/SavePathAuthority.kt`, `savepath/SavePathShapeRule.kt`.
  The word "authority" and the split between a registry and a shape rule is the interesting
  structure. Their paths are Android paths and are worthless to us.
- `data/emulator/BiosPathRegistry.kt`, `data/repository/BiosRepository.kt`,
  `data/local/{dao,entity}/Firmware*.kt`. M5. We join firmware on **md5 only**, deliberately,
  because `reference/README.md` measures filename overlap as bad. If they key on filename that
  confirms our rule. What is worth extracting is any md5 our `batocera-systems.json` join
  misses.

**Worth reading, real but narrower yield:**

- `docs/romm-sync-optimization.md`. Measured cost of `GET /api/roms` paging, including the four
  sidecar aggregates that default to `true`, the fact that sidecar memoisation only applies to
  an unscoped request so `platform_ids` defeats it, and a `with_rom_id_index=false` change that
  was **built, measured, and reverted as a regression**. That reversal is worth more than the
  wins: it is a documented dead end we would otherwise walk. Their query is `platform_ids`,
  `order_by=id`, `order_dir=asc`, `limit=100`, `offset`, `with_char_index=false`,
  `with_filter_values=false`, `with_files=true`. Compare with what M2 and M4 send. Their numbers
  do not transfer; the parameter list and the dead end do.
- `data/remote/romm/`: `RomMApi.kt`, `RomMApiClient.kt`, `RomMModels.kt`, `RomMSyncModels.kt`,
  `RomMSaveModels.kt`, `RomMDeviceModels.kt`, `DeviceAuthPoller.kt`. Endpoints, parameters,
  pagination, the device pairing poll. Anything here that contradicts `romm-api` is a probe, not
  an edit. Their issue #173 is the shape of what to look for: root game files come back as
  `category: "game"` on 5.1 while three of their call sites still test `category == null`. **Go
  and check what our client does with `category`.**
- `data/download/`: `DownloadManager.kt`, `RomStagingManager.kt`, `ArchiveExpansion.kt`,
  `ZipExtractor.kt`, `ExtContentOrganizer.kt`. M3 landed resume and `Range: bytes=0-`. Look for
  what they do about validator staleness, since a stale `If-Range` that splices silently is the
  failure M3 feared most. `RomStagingManager` is worth reading against our own staging.
  `DownloadThermalManager` is an Android concern and is not one of ours.
- `domain/usecase/game/LaunchWithSyncUseCase.kt`,
  `domain/usecase/state/PreLaunchStateSyncUseCase.kt`,
  `domain/usecase/save/SyncSaveOnSessionEndUseCase.kt`,
  `domain/usecase/state/SyncStatesOnSessionEndUseCase.kt`, `data/sync/SaveRecoveryGate.kt`.
  This is their answer to our `game-start` and `game-end` problem, and the constraint is
  inverted: they are in-process and can sync on the launch path, we are a hook that must not
  touch the network. **Read it for what it reveals about the risk, not for the design.** A
  recovery gate implies they have seen the launch-path sync fail in a way that needed one.
- `app/src/test/kotlin/.../integration/RomM*Test.kt`, eight files hitting a real server. Their
  structure is a lead for how we would shape our own offline stub. Their assertions encode their
  beliefs.
- `testbed/romm/` and `scripts/fetch-romm-fixtures.py`. See the next section.

**Design leads only, never facts:**

- `core/input/ControllerDetector.kt`, `ConnectedControllerTracker.kt`, `SoundConfig.kt`, and the
  `ui/` tree, particularly `ui/input/` and `ui/screens/`. **M7b's raw material.** Argosy is a
  shipped gamepad-first launcher and we are about to write one. Their `.claude/skills/` carries
  `menu-patterns`, `design-tokens` and `dual-screen`, which is where their UI reasoning actually
  lives. None of this is empirically verifiable in the sense this session means, so it all lands
  as design leads, in the ledger, addressed to M7b, and out of the plan's factual sections.
- `.claude/hooks/verification-guard.py`, `smell-guard.py`, `scripts/ci/agentic-smells.py`,
  `scripts/ci/coupling-guard.py`, `AGENTS.md`. Argosy is developed the same way this repo is, and
  has built machinery to stop an agent claiming a thing is verified when it is not. That is our
  exact failure mode. Read it, and if something is worth adopting say so as a proposal, not as a
  change. Repo process is out of scope for this branch.
- `libretrodroid/`, about 1,700 files of vendored emulator core, and
  `app/src/main/assets/platforms/`, several hundred platform SVGs. Skim, expect nothing. Note
  that the SVG set is a complete RomM slug list and may be the cheapest cross-check that
  `data/retrobat/platforms.json` is not missing a slug.

## The version testbed, which is a process lead worth its own decision

`testbed/romm/` stands up RomM 4.9.2, 5.0.0 and 5.1.0 side by side on one shared read-only
library so a response shape can be compared across versions. Its README says plainly why it
exists: `RomMCapabilities` gated features by version, but nothing recorded how the response
_shape_ changed, and issue #173 is what fell through that gap.

We have the same gap and a narrower version window, since we refuse below our minimum. So the question
is not whether to copy their testbed, it is whether our single live instance is enough to keep
making API claims from. Put the question in the ledger with a recommendation. **Do not build it
on this branch.**

## Probing rules

- Probe the live instance per `DEVELOPER_SETUP.md`. Prefer a throwaway instance for anything that
  writes.
- **Ask before any write-path probe against real data.** Uploading a save, completing a session
  or creating a device all leave marks in someone's library.
- Probe artifacts go in `probe-output/`, which is gitignored. If a test needs one, check in a
  fixture instead.
- New probe scripts follow `tools/m0-probes/` naming, live in `tools/argosy-probes/` alongside
  `tools/freegosy-probes/`, and are checked in, because a finding nobody can re-run decays into
  folklore.
- **Quote what you measured and never what you did not.** Redact the instance host. Versions of
  both server and RetroBat get recorded next to every measurement, the way
  `docs/retrobat-findings.md` does, because a number without its version is not a fact.
- Pin the Argosy commit you read at the top of the ledger. It ships every few days and an
  unpinned reading is not reproducible.

## What lands

- **LEDGER**, the durable record. Every candidate, including the dropped and the unverifiable,
  with its route and verdict. **The rejections are half the value**: without them someone mines
  this repo again in six months and re-walks the same dead ends. If the honest total is "three
  findings and thirty dead ends", write that. Argosy being credible makes a thin result more
  likely, not less: a mature client that agrees with us is a confirmation, and confirmations are
  the cheapest finding to write and the easiest to overstate.
- **`docs/PLAN.md`**, amended where something moved, plus the correction it already owes about
  Argosy having been mined. Say which fact moved and why, in the plan itself. Argosy gets a row
  in "Reference implementations to mine" naming what was actually taken.
- **Skills**, where a verified finding changes how work gets done: `save-sync`, `romm-api`,
  `platform-mapping`, `offline-and-portable`, `retrobat-layout`. A rule that exists only because
  something was measured belongs in the skill for that area.
- **`data/retrobat/*.json`**, only through their generators and only behind a probe. Never
  hand-edit an emitted file, and never let an Argosy path reach one.
- **A note addressed to M7b**, in the ledger, collecting the UI and input design leads in one
  place so `/start-m7b` has somewhere to read them.
- **`reference/`**, almost certainly nothing. It vendors upstream files our design _depends_ on,
  and we do not depend on Argosy. If you think something belongs there, ask first.

Contradictions are findings. If Argosy does something we ruled out and the live server confirms
our ruling, record the confirmation: a decision that has survived a challenge is worth more than
one that was never tested, and this is how the plan already treats the Playnite
`RomMRegisterDevice` disagreement at line 182. If Argosy contradicts something we have written
down as measured, **their being the mature client is not the tiebreak, the probe is.**

## Out of scope

Product code. New milestones. Starting M7b. Re-mining Grout, Freegosy or the Playnite plugin.
Building the version testbed. Changes to this repo's agent process, hooks or CI, however good
theirs looks. Issues filed upstream against Argosy, though the ledger may note one worth filing.
Any change to M3's, M6's or M7a's landed work, unless a probe shows landed code is wrong, in
which case write the defect down and stop.

If a verified finding implies code, write down the seam and stop.

## The repo rules that bite in a docs-only session

- No absolute paths, no instance hostnames, no tokens, anywhere, including in quoted probe
  output.
- No em-dashes. English only. Comments and docs say how it behaves, not what changed.
- `trunk fmt && trunk check` still applies, and the tables in this repo are formatted.
- Run `pre-pr-verification` before claiming done, and state what you verified and what you did
  not.

## Default

Read the sources, then show me the triage table and the experiment plan before running anything.
Commit locally as you go, one commit per coherent finding rather than one for the session. Ask
before pushing, before opening the PR, and before any probe that writes.
