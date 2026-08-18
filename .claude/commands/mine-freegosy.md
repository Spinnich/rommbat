---
description: Mine the Freegosy client for leads, verify each one against a primary source, and land the survivors in the plan, docs and skills
argument-hint: "[branch name]"
---

# Mine Freegosy

`abduznik/Freegosy` is another RomM client. It has solved, in its own way, several problems
RomMBat has ahead of it: per-device save sync, BIOS placement, download resume, platform
mapping, gamepad navigation. This session reads it, works out which of its answers are
actually true, and folds the true ones into `docs/PLAN.md`, `docs/`, the skills and the
bundled data.

**Freegosy is a hypothesis generator, never evidence.** That is the whole discipline here.
It gets to tell you what to go and check. It never gets to tell you what is true, and a
finding that rests on Freegosy's code, README, tests or docs has not been verified, it has
only been read.

This session writes **no product code**. Its output is documentation, and the reason is
that a wrong fact in `docs/PLAN.md` is more expensive than a wrong function: the function
fails a test, the fact quietly shapes three milestones.

---

Variables for this run:

- BRANCH = $1, default `mine-freegosy`. Branch off main and stay off it.
- LEDGER = `docs/freegosy-findings.md`, new, in the shape of `docs/retrobat-findings.md`.
  If you think the findings belong somewhere else, say so before you start writing, not
  after.

## Who is who

I am Spinnich, the only human on this repo. Skip the preamble and the progress narration.
Show me the triage table, the experiments, and the diffs.

## Why this source needs a higher bar than the last three

Grout, Argosy and the Playnite plugin were mined into the plan already, and their rows sit
in `docs/PLAN.md` under "Reference implementations to mine". All three live under `rommapp`,
track the server closely, and were treated as trustworthy about the API. Freegosy is not in
that category, on five counts that each translate to a specific way a claim can be wrong:

1. **It targets RomM 4.9.** Our declared baseline is RomM 5.1.0 and the live instance in
   `DEVELOPER_SETUP.md` is 5.1.1-beta.1. Every API claim you find is a claim about a server
   two minor versions behind ours. Route shapes, parameter names and required scopes are all
   fair game to have moved.
2. **It is at v0.5.x with one maintainer**, outside the `rommapp` org. Nothing has been
   reviewed by the server's authors.
3. **It targets standalone desktop emulators, EmuDeck and RetroDECK.** RetroBat is none of
   those. **No path from Freegosy is ever valid here** and copying one is the fastest way to
   put a lie in `data/retrobat/save_directories.json`. Save _shapes_ may transfer, save
   _locations_ never do.
4. **It is Dart, and MIT.** We are C#, and GPL-3.0. MIT into GPL-3.0 is compatible and needs
   the notice retained, but that only matters if code is ever lifted, which is a later
   decision and not this session's. Facts about how a server behaves are not anyone's
   copyright. Keep the session on facts.
5. **Its tests and its mock server encode its beliefs, not the server's behaviour.**
   `test/mock_romm_server.py` is the clearest trap in the repo: it will look like a
   specification of the RomM API and it is a specification of what Freegosy expects.

## The bar a claim has to clear

Each candidate finding graduates by exactly one of these routes, and the route gets recorded
next to it:

| Class of claim                            | What settles it                                                                                              |
| ----------------------------------------- | ------------------------------------------------------------------------------------------------------------ |
| RomM API behaviour                        | A request you made against the live instance, with the request and the response quoted                       |
| RetroBat on-disk behaviour                | A probe against a real install, in the style of `tools/m0-probes/`, driven far enough to see the file        |
| Upstream file content or a derived number | Vendored under `reference/` and re-derived by `verify.py`, never a number typed by hand                      |
| A pure shape or algorithm                 | An argument from first principles **plus** a test that fails if it is wrong. Label it reasoned, not measured |
| Anything else                             | Stays an unverified lead. It may be recorded, and it may not enter `docs/PLAN.md` as fact                    |

None of the following is verification: Freegosy's README says so; Freegosy's code does it;
Freegosy's tests pass; Freegosy's `docs/romm_49_save_sync_research.md` asserts it; Grout or
Argosy appears to agree; it sounds right. If a claim's only support is that two clients both
believe it, that is one belief with two copies.

Where a probe is the only route and the probe is not runnable here, say so plainly and leave
the lead open. **An open lead honestly labelled is worth more than a plausible sentence in
the plan**, because the plan is read as settled.

## Triage before verification

Do not verify everything. Read first, produce the full candidate list, then rank it by one
question: **if this were true, what would change in RomMBat?** A finding that changes no
decision, no schema, no data file and no skill is dropped at triage and recorded as dropped.
It never gets a probe.

For each survivor, record five things:

1. The claim, one sentence.
2. What in RomMBat it would touch: a milestone, a doc section, a skill, a data file.
3. What would have to be true for it to hold here.
4. The cheapest experiment that settles it.
5. The verdict, once run.

Show me that table before you start running experiments. That is the cheapest point for me
to cut half of it.

## Where to look, and what it bears on

Recon has already been done, so start from this rather than re-deriving it. Treat the list
itself as unverified: it came from a summarizer and file paths may be wrong or stale.

**Highest value, because it lands where our plan is thinnest:**

- `docs/romm_49_save_sync_research.md`. The one design doc in the repo, on the exact subject
  of M6. Read it as a list of questions to re-ask against 5.1.x, not as answers.
- `lib/core/save/save_sync_service.dart`, `background_sync_queue.dart`,
  `backup_service.dart`, `backup_repository.dart`. M6 and the outbox. We already have a
  negotiate protocol written down at `docs/PLAN.md` around line 1267. Compare against what
  they actually call, then check the difference against the live server, not against them.
- **Per-device save isolation.** They claim RomM 4.9+ isolates saves per device. Our device
  identity comes from pairing and `client_device_identifier`, and `docs/PLAN.md` already
  passes `device_id` on save upload and download. Whether isolation is the server's
  behaviour or their convention is a question the live instance can answer directly, and the
  answer reaches conflict handling.
- `lib/core/save/strategies/`, roughly sixteen per-emulator save strategies. Overlaps our
  M0 probe 2 on dolphin, pcsx2, duckstation, ppsspp, rpcs3, cemu, azahar/citra, melonds,
  mgba, retroarch. **Shapes only.** Where their classification disagrees with
  `data/retrobat/save_shapes.json`, that is a reason to re-run probe 2 for that emulator,
  and the probe decides, not the disagreement.
- `lib/core/emulator/bios_registry.dart`, `firmware_service.dart`. M5. We join firmware on
  **md5 only**, deliberately, because `reference/README.md` measures the filename overlap as
  bad. If they key on filename, that is a confirmation of our rule, not a challenge to it.
  What is worth extracting is any md5 our `batocera-systems.json` join misses.

**Worth reading, lower expected yield:**

- `lib/core/romm/romm_service.dart`, `romm_models.dart`, `rom_constants.dart`. Endpoints,
  parameters, pagination, error handling. Anything here that contradicts `romm-api` is a
  probe, not an edit.
- `lib/core/downloader/download_service.dart`. M3 has just landed, including resume and
  `Range: bytes=0-`. Look specifically for what they do about validator staleness, since a
  stale `If-Range` that splices silently is the failure M3 feared most.
- `lib/core/storage/rom_mapping_service.dart`, `rom_lookup_service.dart`,
  `directory_service.dart`. M2 platform mapping and adoption by hash.
- `test/unit/multi_disc_detection_test.dart`. Multi-disc and `.m3u` handling is thin in our
  plan. If they have hit real cases, those are cases to check against RetroBat's own
  behaviour.
- `lib/core/storage/secure_storage_service.dart`. Token storage, against
  `offline-and-portable`. We are portable and they are not, so expect divergence and
  understand why before adopting anything.
- `test/mock_romm_server.py`. Useful as a structural analogue for our offline stub. Its
  contents are their assumptions. Read it for shape, cite it for nothing.

**Design leads only, never facts:**

- `lib/core/input/gamepad_service.dart`, `known_controllers.dart`, `screenshots/`, and the
  provider layout under `lib/providers/`. M7. UI decisions are not empirically verifiable in
  the sense this session means, so anything from here is labelled a design lead and stays out
  of the plan's factual sections.
- `agent.md`, `analysis_options.yaml`, `.github/`. Repo scaffolding. Skim, expect nothing.

## Probing rules

- Probe the live instance per `DEVELOPER_SETUP.md`. Prefer a throwaway instance for anything
  that writes.
- **Ask before any write-path probe against real data.** Uploading a save, completing a
  session or creating a device all leave marks in someone's library.
- Probe artifacts go in `probe-output/`, which is gitignored. If a test needs one, check in a
  fixture instead.
- New probe scripts follow `tools/m0-probes/` naming and are checked in, because a finding
  nobody can re-run decays into folklore.
- **Quote what you measured and never what you did not.** Redact the instance host. Versions
  of both server and RetroBat get recorded next to every measurement, the way
  `docs/retrobat-findings.md` does, because a number without its version is not a fact.

## What lands

- **LEDGER**, the durable record. Every candidate, including the dropped and the
  unverifiable, with its route and verdict. **The rejections are half the value**: without
  them someone mines this repo again in six months and re-walks the same dead ends. If the
  honest total is "two findings and eighteen dead ends", write that.
- **`docs/PLAN.md`**, amended where something moved. Say which fact moved and why, in the
  plan itself. A row in "Reference implementations to mine" only if something genuinely
  survived verification, with the caveat that this source is not `rommapp` and is version
  skewed.
- **Skills**, where a verified finding changes how work gets done: `save-sync`, `romm-api`,
  `platform-mapping`, `offline-and-portable`, `retrobat-layout`.
- **`data/retrobat/*.json`**, only through their generators and only behind a probe. Never
  hand-edit an emitted file, and never let a Freegosy path reach one.
- **`reference/`**, almost certainly nothing. It vendors upstream files our design _depends_
  on, and we do not depend on Freegosy. If you think something belongs there, ask first.

Contradictions are findings. If Freegosy does something we ruled out, and the live server
confirms our ruling, record the confirmation: a decision that has survived a challenge is
worth more than one that was never tested, and this is precisely how the plan already treats
the Playnite `RomMRegisterDevice` disagreement at line 182.

## Out of scope

Product code. New milestones. Re-mining Grout, Argosy or the Playnite plugin. Issues filed
upstream against Freegosy. Any change to M3's landed work. If a verified finding implies code,
write down the seam and stop.

## The repo rules that bite in a docs-only session

- No absolute paths, no instance hostnames, no tokens, anywhere, including in quoted probe
  output.
- No em-dashes. English only. Comments and docs say how it behaves, not what changed.
- `trunk fmt && trunk check` still applies, and the tables in this repo are formatted.
- Run `pre-pr-verification` before claiming done, and state what you verified and what you
  did not.

## Default

Read the sources, then show me the triage table and the experiment plan before running
anything. Commit locally as you go, one commit per coherent finding rather than one for the
session. Ask before pushing, before opening the PR, and before any probe that writes.
