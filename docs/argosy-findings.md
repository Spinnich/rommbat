# Argosy findings

What was taken from reading [`rommapp/argosy-launcher`](https://github.com/rommapp/argosy-launcher),
what survived verification against a primary source, and what did not. Written in the shape of
[freegosy-findings.md](freegosy-findings.md), which is in turn in the shape of
[retrobat-findings.md](retrobat-findings.md).

**Argosy is a hypothesis generator, never evidence.** It clears four of the five bars Freegosy
failed, which changes how likely a lead is to be worth chasing and changes nothing about what
settles one. Every row carries the route that settled it, and the rows that no route settled are
labelled open rather than quietly promoted.

|                    |                                                                        |
| ------------------ | ---------------------------------------------------------------------- |
| Source read        | `rommapp/argosy-launcher` at `3971bee4`, tag `v2.8.0`, 2026-08-24      |
| Source targets     | Android: libretro via a vendored `libretrodroid`, plus standalone apps |
| Source licence     | GPL-3.0, same as ours                                                  |
| RomM under test    | `5.2.0`, read from `GET /api/heartbeat` -> `SYSTEM.VERSION`            |
| Library under test | 88,331 roms, 708 firmware records across 52 platforms                  |
| Schema cross-check | `src/RomM.Client/openapi/romm-5.1.0.json`, the pinned minimum          |
| RetroBat           | `8.2.0-stable-win64`, read from `system/version.info`                  |
| Date               | 2026-08-25                                                             |

The instance host, the token and every device id are redacted throughout, per the repo rules.
`libretrodroid` (about 1,700 files) was excluded from the clone. **`sigil` is a git submodule and
its source is not in this clone**, so every statement here about sigil is a statement about
Argosy's prose describing it, never about its code.

## The correction this session owes

`docs/PLAN.md` has never contained a row for Argosy in "Reference implementations to mine", and
it never said Argosy was mined. The false statement is in two other places:

- [freegosy-findings.md](freegosy-findings.md), in "Why this source needed a higher bar than the
  last three": "Grout, Argosy and the Playnite plugin sit under `rommapp`, track the server
  closely, and **were mined as trustworthy about the API**."
- `.claude/commands/mine-freegosy.md`, which repeats it and adds that "their rows sit in
  `docs/PLAN.md`".

Argosy's only appearances anywhere in this repository before this session were as a licence
precedent (the decisions table, and `README.md`) and as an example of a RomM client existing
(the Context section). **It had never been read.** A reader of the Freegosy ledger was told Argosy
was mined and sent to a table that has never listed it. Both statements are corrected in place
rather than rewritten away, and Argosy now has a row naming what was actually taken.

## The honest total

32 candidates read, 18 dropped at triage, 14 probed or reasoned. Of those: **6 confirmed,
3 rejected, 2 corrected, 3 confirmations of decisions already made.** No lead was left open,
because the development RetroBat install closed the one route that looked unavailable at triage.

**The one that costs real work** is A3: **84 of RetroBat's 353 BIOS requirements are `.zip`
files, and of the 20 that carry an md5 not one can ever match on it**, because a zip's hash is
over archive bytes that depend on compression and member order. This repository already knows
that argument. It is written down for saves, under "Hashing zip bytes makes RomMBat and Grout
disagree", and the conclusion there is to hash content rather than container bytes. Nobody
applied it to BIOS, and M5's md5-only join inherits the defect for those 20 requirements, 5.7%
of the manifest. The other 64 zip requirements name no md5 and never reach the join:
`BiosPlanner.Inspect` returns `Unverifiable` first, which is the honest verdict.

**The one that costs measurable time** is A1: RomMBat sends `with_rom_id_index=false`, which
Argosy built, measured as a regression and reverted. Measured here it is **3.4 to 3.7 times
slower on a platform-scoped walk** and roughly neutral unscoped, and RomMBat pages
platform-scoped as a first-class user-selectable case.

**Argosy's headline number does not transfer, and that is the point.** Its sync doc says
`merged_ra_metadata` is 45% of the `/api/roms` payload and that an achievement-count scalar is
worth more than everything else combined. On this library `merged_ra_metadata` does not reach
the top fifteen fields. The cost here is `ss_metadata` at 46.4% and `igdb_metadata` at 20.5%.
Same endpoint, same client shape, entirely different answer, because it is a different library.

**One decision survived a challenge.** `GET /api/roms/identifiers` still answers **504 after
exactly 300 s** at 88,331 roms on 5.2.0. Argosy reconciles deletions through it at 23,873 roms
and is right to. We refuse to and are right to. Being the mature client is not the tiebreak.

**Argosy is wrong about two things** at 5.2.0, which is the argument for the bar: its
already-finalized status list for `/complete` does not include the status the server actually
returns, and two of its three save-download call sites omit the `optimistic=false` fence its own
primary path is careful to pass.

## Verification routes

| Route         | What it means                                                                            |
| ------------- | ---------------------------------------------------------------------------------------- |
| **live**      | A request made against the live instance, with the request and response quoted           |
| **probe**     | A probe against the development RetroBat install, driven far enough to see the file      |
| **reference** | Derived from files vendored under `reference/`, never a number typed by hand             |
| **reasoned**  | An argument from first principles plus a test that fails if it is wrong. Not measured    |
| **source**    | A fact about Argosy's own code. Settles nothing about RomM or RetroBat                   |
| **dropped**   | Cut at triage because nothing in RomMBat would change if it were true. Never got a probe |

Probe scripts are in `tools/argosy-probes/` and are checked in. Their output goes to
`probe-output/argosy/`, which is gitignored.

**One number here has no script: A3's `neogeo.zip` container hash.** It was a hand-run
`GET /api/firmware/{id}/content/` followed by a local hash of the bytes, and the transcript
below is the record of it. Everything else in A3 is re-runnable:
`a3b-bios-requirement-join.py` derives the 63/111/179 split, the 84/20/64 zip breakdown and
the filename matches from `data/retrobat/bios.json` against `/api/platforms`.

---

## Triage

32 candidates came out of the read. 14 survived, 18 were cut. The cuts are recorded because the
whole point of writing them down is that nobody mines this repository again in six months and
re-walks the same dead ends.

### Survivors

| #   | Claim                                                                                                                     | Touches                                    | Cheapest experiment                                                       | Verdict                  |
| --- | ------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------ | ------------------------------------------------------------------------- | ------------------------ |
| A1  | `with_rom_id_index=false`, which RomMBat sends, is the parameter Argosy built, measured as a regression and reverted      | M2, M4, `CatalogQuery`, `romm-api`         | Time our real page query with the flag both ways, scoped and unscoped     | **live, confirmed**      |
| A2  | `with_total` is free while the id index is on and paid for with it off                                                    | M2, M4, `CatalogQuery`                     | Four-cell matrix at offset 0                                              | **live, confirmed**      |
| A3  | RetroBat names one acceptable md5 per file, so a working BIOS under another revision reads as missing                     | M5, `BiosPlanner`, `bios.json`             | Join every RetroBat requirement against every firmware md5 in the library | **live, corrected**      |
| A4  | 30 of Argosy's 55 md5-to-RetroArch-filename pairs are absent from our manifest                                            | M5, `bios.json`                            | Extract their map, diff against our 156                                   | **reference, confirmed** |
| A5  | `POST /api/activity/heartbeat` exists at our baseline and RomMBat has never mentioned it                                  | M7a follow-on, `romm-api`                  | Post one, read `/api/activity` back, delete it                            | **live, confirmed**      |
| A6  | A 4xx from `/complete` means already-finalized, and retrying forever makes a zombie session                               | M6, `romm-api`, `SaveSync`                 | Complete one session three times                                          | **live, corrected**      |
| A7  | The RetroArch state path's core segment comes from `sort_savestates_enable`, which defaults **on** when the key is absent | M6 stage 2, `StateSync`, `retrobat-layout` | Read the generated `retroarch.cfg` and the states on disk                 | **probe, rejected**      |
| A8  | The five-usage taxonomy is a match-rule axis, orthogonal to our A/B/C/D cardinality axis                                  | `save-sync`, `save_shapes.json`            | Argue it against our existing `unit_paths` rows                           | **reasoned, confirmed**  |
| A9  | Save-path config must key on `(emulator, platform)`; emulator alone caused two shipped Argosy bugs                        | `save-sync`, `save_shapes.json`            | Read our resolver against RetroBat's tree                                 | **reasoned, confirmed**  |
| A10 | `GET /api/roms/identifiers` is usable for deletion reconcile, as Argosy uses it                                           | `romm-api`, PLAN 112 and 1217              | Re-time it once                                                           | **live, rejected**       |
| A11 | A sweep must withhold entirely on missing evidence, or a failed page deletes the library                                  | M3 eviction, `SetResolver`                 | Read what authorises a member retirement                                  | **reasoned, confirmed**  |
| A12 | `merged_ra_metadata` is 45% of the page payload                                                                           | M2/M4 scale budget                         | Measure one page's composition                                            | **live, rejected**       |
| A13 | Argosy resumes with a bare `Range` and no `If-Range`, where M3 sends the validator                                        | M3, confirmation                           | Read both                                                                 | **source, confirmed**    |
| A14 | Argosy omits `optimistic=false` on two of three save-download paths                                                       | Ledger note, upstream report               | Read their call sites                                                     | **source, confirmed**    |

### Dropped at triage

| #   | Claim                                                                        | Why it was dropped                                                                                                                                        |
| --- | ---------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------- |
| A15 | Issue #173: root files return `category: "game"` at 5.1                      | `SetResolver` excludes multi-file ROMs outright and never reads `category`. The trap has no call site here                                                |
| A16 | `file_ids` selects members of a multi-file rom to download                   | Same reason. Parked for whenever multi-file ROMs stop being excluded                                                                                      |
| A17 | The bundled platform SVGs are a complete RomM slug list to cross-check       | **84 files.** `reference/romm-slugs.txt` holds 457. It is a subset and proves nothing                                                                     |
| A18 | `SaveRecoveryGate`, a boot barrier against uploading a stale cached save     | Argosy caches saves; our flush reads live disk. The hazard needs a cache we do not have                                                                   |
| A19 | `save_channels` as a cross-check on the Freegosy per-device rejection        | A channel is a **local-only placeholder for a slot with nothing saved in it yet**, because the server has no concept of an empty slot. Not a server model |
| A20 | `SaveOwnershipEntity`, per-account ownership of the bytes at a live path     | Multi-account switching is not in v1 scope                                                                                                                |
| A21 | The PS2 save-id region prefix rule (`E`->`BE`, `P`/`J`/`K`->`BI`, else `BA`) | Our PCSX2 per-game conversion keys on the rom stem, not the disc serial. The rule has no call site here                                                   |
| A22 | `updated_after` for incremental pulls                                        | Their own doc lists four blockers and says it needs design rather than a parameter. We have no server-anchored clock either                               |
| A23 | `sibling_roms` is a view whose grouping is exactly reproducible client-side  | We exclude multi-file ROMs and consume no sibling data                                                                                                    |
| A24 | `with_files=true` causes a per-file `track_meta` N+1 and 502s                | `CatalogQuery` already sends `with_files=false`. Already avoided                                                                                          |
| A25 | Their floor warns rather than refuses below `MIN_SUPPORTED_VERSION`          | We refuse below 5.1.0 by decision. Nothing moves                                                                                                          |
| A26 | `RomMCapabilities` version gates                                             | Every gate sits at 4.9.0 or 5.0.0, below our floor. All would evaluate true                                                                               |
| A27 | Promoting a full-length `.tmp` instead of requesting a `Range` a server 416s | Real, tiny, and with no doc or plan consequence                                                                                                           |
| A28 | `file_name_no_tags` and the server's filename rewrite                        | Settled already, and settled further than Argosy: measurement 152 and finding F6                                                                          |
| A29 | Their negotiate payload and response field list                              | Matches ours field for field. Recorded as prose below, not as a row with an experiment                                                                    |
| A30 | `LaunchWithSyncUseCase` and the pre-launch state sync                        | Their constraint is inverted: in-process and free to sync on the launch path. Ours must not touch the network                                             |
| A31 | `verification-guard.py`, `smell-guard.py`, `coupling-guard.py`, `AGENTS.md`  | Repo process, explicitly out of scope for this branch. Noted as a proposal at the end                                                                     |
| A32 | The three-version Docker testbed                                             | Out of scope to build. Recorded as a recommendation at the end                                                                                            |

---

## A1: `with_rom_id_index=false` is a regression on a scoped walk, and RomMBat sends it. **Confirmed**

`CatalogQuery.ToQueryString` sends `with_rom_id_index=false` on every page, reasoning that the
index is "whole-library index metadata, not per-page data, and the server resends it in full each
time". That is the same reasoning Argosy's sync doc opens with, and its own measurement says the
premise is wrong in two places: under a platform filter the index spans **that platform**, not
the library, and it is what lets the server serve the page by primary key rather than by
`OFFSET n LIMIT m` on a sort with no covering index.

Their numbers are against a 23,873-rom library on 5.1.0-alpha.4. Re-measured here against 88,331
roms on 5.2.0, median of three, `limit=100`, largest platform is `psx` at 9,196 roms:

**Platform-scoped (`platform_ids`), which is how a platform sync set walks:**

| offset | index on | index off | slower by | index costs |
| ------ | -------- | --------- | --------- | ----------- |
| 0      | 2494 ms  | 8452 ms   | 3.4x      | 63 KiB      |
| 1000   | 2288 ms  | 8305 ms   | 3.6x      | 63 KiB      |
| 3000   | 2392 ms  | 8567 ms   | 3.6x      | 63 KiB      |
| 6000   | 2350 ms  | 8665 ms   | 3.7x      | 63 KiB      |

**Unscoped, which is how a filter-scoped walk pages:**

| offset | index on | index off | slower by | index costs |
| ------ | -------- | --------- | --------- | ----------- |
| 0      | 309 ms   | 364 ms    | 1.18x     | 603 KiB     |
| 1000   | 377 ms   | 440 ms    | 1.17x     | 604 KiB     |
| 3000   | 1038 ms  | 1169 ms   | 1.13x     | 604 KiB     |
| 6000   | 967 ms   | 1101 ms   | 1.14x     | 604 KiB     |

Two things fall out, and the second is the one worth keeping.

**The flag is scope-dependent, and nobody had said so.** Scoped, turning the index off costs
between six and 6.3 seconds a page to save 63 KiB. Unscoped it costs about 130 ms to save
600 KiB, which on anything slower than a LAN is a fair trade and on a LAN is a wash.

**The byte figures confirm the mechanism rather than just the result.** Argosy says the index
under a platform filter spans the platform and saves "about 37 KiB per page". Measured here it
is 63 KiB scoped against 604 KiB unscoped, a factor of ten between the two scopes on the same
endpoint. That is the index spanning the filtered set, exactly as described, and it is why the
bandwidth argument that motivated our flag only ever applied to the unscoped case.

Note also that scoped paging is slower than unscoped at every offset even with the index on
(2494 ms against 309 ms at offset 0), which is sidecar memoisation applying only to an unscoped
request. That is a second cost of scoping and is not something the flag can fix.

**RomMBat pages scoped as a first-class case.** `CatalogScopeKind` has five members and four of
them scope the request: `Platform`, `Collection`, `SmartCollection` and `VirtualCollection`. Only
`Filter` pages unscoped. `BrowseCommand` selects `Platform` whenever a platform is named, and a
platform sync set is the most ordinary thing a user builds.

**The seam, written down rather than built:** the flag wants to follow the scope, not be a
constant. Off for `Filter`, on for the four scoped kinds. `CatalogQuery` already knows its own
`Scope`, so the change is local to `ToQueryString`, and the comment above the parameter needs to
stop asserting the whole-library premise for the scoped case.

## A2: `with_total` is free with the index on and paid for with it off. **Confirmed**

`resolve_total()` returns `len(rom_id_index)` while the index is being built, so the count is a
byproduct when the index is on and a separate computation when it is not. Measured at offset 0,
unscoped, median of three:

| index | `with_total` | median | total returned |
| ----- | ------------ | ------ | -------------- |
| on    | on           | 288 ms | 88331          |
| on    | off          | 287 ms | 88331          |
| off   | on           | 353 ms | 88331          |
| off   | off          | 229 ms | `null`         |

With the index on, asking for the total costs 1 ms. With it off, it costs 124 ms. **RomMBat sends
`with_rom_id_index=false` together with `with_total=true`**, which is the one combination that
pays for the count, and it does so on every page of every walk.

The total is genuinely wanted: `CatalogQuery` keeps it because "it is the only way a resumable
walk knows how far it has left to go", which is true. The finding is not that the total should
go. It is that turning the index back on where the scope calls for it makes the total free as a
side effect, so A1's seam settles A2 as well.

## A3: The md5-only BIOS join cannot match a `.zip`, and 84 requirements are zips. **Corrected**

The candidate that went into triage was Argosy's: RetroBat names one acceptable md5 per file
where several valid dumps exist, so a library holding a working BIOS under another revision would
read as `MissingFromLibrary`. The probe corrected it into something larger and more specific.

Joining every one of RetroBat's 353 requirements against every firmware md5 in the library
(343 distinct md5s across 52 platforms, 708 records):

| Outcome                       | Requirements |
| ----------------------------- | ------------ |
| md5 found in the library      | 63           |
| md5 named, not in the library | 111          |
| RetroBat names no md5 at all  | 179          |

Three separate things are inside that, and only the first is a defect.

**84 of the 353 requirements are `.zip` files. Twenty of those carry an md5, and zero of the
twenty match.** Not a low match rate: none. The other 64 name no hash at all, so they have
nothing to match with and `BiosPlanner.Inspect` classifies them `Unverifiable` before the
library join runs (`src/RomMBat.Core/Content/BiosPlanner.cs:355-363`). The 20 are the seam.
`neogeocd` is the clean example. RetroBat requires `bios/neogeo.zip` at md5
`dffb72f1...` and `bios/neocdz.zip` at `c733b4b7...`. The library holds files named exactly
`neogeo.zip` and `neocdz.zip`, at md5 `c74b8945...` and `c38cb8e5...`.

**That both sides hash the container is measured, not inferred.** Downloading the library's
`neogeo.zip` and hashing what arrived. This one was run by hand rather than by a checked-in
probe, so the transcript is the evidence:

```text
GET /api/firmware/973/content/neogeo.zip -> 200, 1861788 bytes
server md5_hash            c74b89453a828ba7e434c610214bf6a1
md5 of the bytes received  c74b89453a828ba7e434c610214bf6a1
zip members                34
  fc7599f3f871578fe9a0453662d1c966  000-lo.lo
  5b2d6f653ba4cf36e7fe237e4acb2f50  japan-j3.bin
  aa2b5d0eae4158ffc0d7d63481c7830b  sfix.sfix
```

The record's `md5_hash` is the hash of the container exactly, over a 34-member archive, and
RetroBat's is a different container hash of a functionally equivalent set. Two zips holding the
same members hash differently whenever compression level, member order or stored timestamps
differ, so the comparison can never succeed on anything but a byte-identical archive.

**This is a real nuance against a rule already in the `romm-api` skill**, which says a `.zip`
reports the hashes of the file inside it and to hash inside a single-entry archive rather than
over its bytes. That holds for a **single-entry ROM** archive. A multi-member firmware archive is
hashed as a container, so the two statements are about different cases and the skill now says
which is which.

**This repository already knows that argument and applied it somewhere else.** `docs/PLAN.md`
the risk table records that Go's `archive/zip` and .NET's `ZipArchive` produce different bytes for the
same members, and the conclusion drawn there is to define `content_hash` over sorted relative
paths plus per-file hashes and treat the archive as transport only. The same reasoning governs a
BIOS zip and was never carried across. M5 inherits the defect for the 20 zip requirements that
carry an md5, 5.7% of the manifest, and it fails in the direction that wastes a user's time: it
reports `MissingFromLibrary` for a file the library is holding under the right name. For the
other 64 the report says `Unverifiable`, which is what `docs/PLAN.md` already argues for.

**Nine systems have firmware in the library and no md5 overlap**: `atari7800`, `atomiswave`,
`msx`, `msx2`, `n64dd`, `naomi`, `neogeocd`, `sgb`, `xbox`. Several are zip cases. `sgb` is not a
defect at all: RomM's slug list has `gb`, `gba` and `gbc` and no Super Game Boy platform, so
there is no firmware to have matched and the mapping is the only one available.

**Twenty of the 179 no-md5 requirements have an exact filename match in the library.**
`neogeocd` again: RetroBat names `bios/neocd/neocd_f.rom` with no md5, and the library holds
`neocd_f.rom` with md5 `8834880c...`. M5 correctly reports these as `Unverifiable` rather than
missing, which is the honest answer under the current rule, but the file is plainly there.

**Rule 3 is not overturned.** Joining on filename across the whole manifest remains wrong for the
reason `reference/verify.py` asserts: RetroBat requires 156 distinct md5s, RomM knows 353, and
only 63 overlap, so filenames disagree at scale. (156, not the 157 `reference/README.md` still
carries; `docs/PLAN.md` records why that table was wrong.) What the probe shows is that md5 is the wrong
key for two bounded subsets, and each wants its own rule rather than a relaxation of the general
one.

**The seam, written down rather than built:** a zip requirement needs a member-wise comparison,
the same shape `LogicalContentHash` already computes for save archives, rather than a hash of the
container. A no-md5 requirement could offer an exact filename match as a suggestion the user
confirms, which is weaker than a hash and stronger than `Unverifiable` with nothing attached.
Neither changes the general join.

## A4: 30 of Argosy's 55 md5-to-filename pairs are absent from our manifest. **Confirmed**

`BiosPathRegistry.retroArchBiosNames` maps 55 md5s to the filename RetroArch cores insist on,
because RomM serves firmware under RomM's name and the cores are strict. Diffed against the 156
distinct md5s in `data/retrobat/bios.json`: 25 present, **30 absent**.

They are alternate revisions RetroBat does not require, not gaps: six extra 3DO dumps
(`panafz10-norsa`, `panafz1j`, `sanyotry` and others), seven PSX BIOS revisions where RetroBat
names only `psxonpsp660.bin`, DSi firmware, and seven Amiga Kickstart versions.

**Nothing here belongs in `data/retrobat/bios.json`.** The manifest is generated from
`batocera-systems.json` and RetroBat is the authority on what it requires. An md5 RetroBat does
not name is not a requirement RomMBat may invent. The value of the diff is that it is what sent
this session to run A3's join, and A3 is the finding.

## A5: `POST /api/activity/heartbeat` exists, works, and RomMBat has never mentioned it. **Confirmed**

Declared in the pinned `romm-5.1.0.json` with `post` and `delete`, alongside `GET /api/activity`
and `GET /api/activity/rom/{rom_id}`. It appears nowhere in `docs/PLAN.md`, nowhere in the
`romm-api` skill, and nowhere in `RomM.Client`. M7a closed the loop between EmulationStation and
RomM without it.

```text
POST /api/activity/heartbeat
{"rom_id": 1393, "device_id": "<device>"}

-> 200 in 0.24s
{"user_id":18,"username":"rommbat-user","rom_id":1393,
 "rom_name":"Star Wars: Knights of the Old Republic",
 "rom_cover_path":"roms/20/1393/cover/small.png",
 "screenshot_path":"/assets/romm/resources/roms/20/1393/title_screen/title_screen.png",
 "platform_slug":"xbox","platform_name":"Microsoft - Xbox",
 "device_id":"<device>","device_type":"RomMBat",
 "started_at":"2026-08-25T10:27:03.078534+00:00"}
```

`GET /api/activity` then lists it and `GET /api/activity/rom/1393` filters to it, both in under
0.1 s. `device_type` already reads `RomMBat`, carried from the device the pairing flow created.

**A trap worth the skill entry: the `DELETE` takes `device_id` as a query parameter, not in the
body the `POST` takes.** Sent as a body it answers 422 naming the missing query field, which
reads like a malformed payload rather than a misplaced one:

```text
DELETE /api/activity/heartbeat  {"rom_id":..., "device_id":"<device>"}
-> 422 {"detail":[{"type":"missing","loc":["query","device_id"],"msg":"Field required"}]}

DELETE /api/activity/heartbeat?device_id=<device>
-> 204
```

The heartbeat this probe created was cleared, and `GET /api/activity` reads zero entries.

**What it does not settle.** Argosy's `RomMApi` KDoc says the server holds a heartbeat for 90
seconds and it must be repeated while play continues. That was not measured here and is not
recorded as fact.

**Why this is a design question and not a straightforward gap.** Presence has to be reported
while a game is running, and `game-start` is inside the launch path and may not touch the
network. Argosy is in-process and has no such constraint. Anything RomMBat does here belongs to
the detached `background <event>` pass, which is the only part of the design that is awake during
play and allowed to use the network. That is a milestone decision, not a correction, so it is
recorded as a lead against a future stage rather than added to M7a.

## A6: A repeat `/complete` answers 400, and RomMBat cannot see it. **Corrected**

Argosy's `NegotiatorSaveSyncStrategy.completeSession` treats 404, 410 and 409 as
already-finalized, and any other 4xx as "drop local rows to avoid a zombie". The specific list is
wrong at 5.2.0. Opening a session and completing it three times:

```text
POST /api/sync/negotiate  {"device_id":"<device>","saves":[]}
-> 200  session_id=248  operations=5  download=5

POST /api/sync/sessions/248/complete  {"operations_completed":0,"operations_failed":0,"play_sessions":[]}
attempt 1 -> 200  {"session":{"id":248,...,"status":"COMPLETED",...},"play_session_ingest":null}
attempt 2 -> 400  {"detail":"Session is already COMPLETED"}
attempt 3 -> 400  {"detail":"Session is already COMPLETED"}
```

**400, not 404, 410 or 409.** Argosy's catch-all 4xx branch saves it, but its named list matches
nothing the server sends and would mislead anyone reading it as documentation.

**The defect this exposes is ours, and it is the opposite of the one Argosy guards against.**
RomMBat never retries, so it cannot build a zombie session. What it does instead is discard the
result: `SaveSync` awaits `CompleteSyncSessionAsync` without assigning it, and catches only
`RomMUnreachableException`. `PostAuthenticatedAsync` turns any non-success into a
`RomMResponse.Failure` and returns it rather than throwing, so **every HTTP failure to close a
session is silently swallowed** and the pass reports success. A 400 here is harmless. A 403 from a
token missing `assets.write` is not, and it would be equally invisible.

There is a smaller thing alongside it: `FailureAsync` maps every unrecognised status, 400
included, to `RomMResponseStatus.ServerError`, so a client error is labelled a server one.

**This is landed M6 code and the brief says to write the defect down and stop.** The seam is that
`SaveSync` should read the response and add a problem to the outcome when the close fails,
distinguishing "already completed", which is a non-event worth swallowing deliberately, from
every other failure, which is not. No code was changed on this branch.

## A7: RetroBat never leaves `sort_savestates_enable` to its default. **Rejected**

Argosy's `save-id-to-path.md` records that a `retroarch.cfg` omitting `sort_savestates_enable` is
treated as **on** while the matching `sort_savefiles_enable` is treated as **off**, and that a
state resolved without a core segment is written where the emulator will never look, silently.
That is the kind of asymmetry that costs data, so it went into triage as the highest-priority
probe.

It does not arise here. RetroBat's `emulatorlauncher` regenerates `retroarch.cfg` on every launch
and writes both keys explicitly, then achieves the per-core split by writing the full path rather
than by asking the sort flag to build one:

```text
<retrobat root>/emulators/retroarch/retroarch.cfg
  savefile_directory          = "<root>\saves\mastersystem"
  savestate_directory         = "<root>\saves\mastersystem\libretro.genesis_plus_gx"
  sort_savefiles_by_content_enable  = "false"
  sort_savefiles_enable            = "false"
  sort_savestates_by_content_enable = "false"
  sort_savestates_enable           = "false"
```

The states on disk agree, across three systems and four cores:

```text
saves/mastersystem/libretro.genesis_plus_gx/Phantasy Star (Brazil).state1
saves/mastersystem/libretro.picodrive/Phantasy Star (Brazil).state1
saves/psx/libretro.mednafen_psx_hw/Metal Gear Solid (USA) (Rev 1).state1
saves/ports/libretro.2048/2048.state1
```

**Two things worth keeping from a rejected lead.** First, the core folder is named
`libretro.<core>`, which is RetroBat's own convention and **not** the libretro `corename` Argosy
documents, so their naming rule is wrong here even where the shape rhymes. Second, our model does
not read `retroarch.cfg` for this at all: `es_savestates.cfg` declares
`<directory>{{system}}/libretro.{{core}}</directory>` and `StateSync` follows that declaration.
Deriving it from RetroBat's own manifest rather than from a per-launch generated file is the
stronger source, and it is why the asymmetric default was never reachable.

This is also a direct application of rule 2. The cfg is regenerated per launch, and the one read
here names `mastersystem` only because that was the last game launched.

## A8: The five usages are a match-rule axis, not a rival to A/B/C/D. **Reasoned**

Argosy classifies a save by how its id is spent on disk: `FOLDER_EXACT`, `FOLDER_PREFIX`,
`FILE_EXACT`, `FILE_PREFIX`, `FOLDER_SPLIT`. RomMBat classifies by how many files a save unit is:
one file (A), several files (B), a directory (C), a shared container (D).

**These are orthogonal axes and neither subsumes the other.** Theirs answers "given an id, which
names on disk belong to this game", ours answers "how many things move as one unit". PSP is
`FOLDER_PREFIX` and class C at once: the prefix rule is how `ULUS10064` claims `ULUS10064SYSDATA`
as well, and the class is why all of them bundle into one archive.

RomMBat already carries the match rule, informally, in the `key` field of a `unit_paths` entry:
`title_id` for PSP, `hex_ascii` for the Wii NAND, `game code` for GameCube, `rom stem` for a
converted PCSX2 card. What it does not carry is whether the match is exact or a prefix, which is
the distinction that decides whether one folder or several belong to a unit. For PSP that is
already handled in code because the bundling was written knowing it. For a platform added later
it is the thing an implementer would get wrong.

Recorded as a lead for `save-sync` rather than a schema change: the four classes stay, and the
`key` field wants to say exact or prefix alongside what it keys on.

## A9: `(emulator, platform)` keying is structurally enforced by RetroBat's tree. **Reasoned**

Argosy's `SavePathAuthority` takes the platform slug as a required argument because two shipped
bugs came from resolving by emulator alone: the Wii platform row displayed GameCube's path
(their #380), and a shared config id meant a GameCube save-path override silently became the Wii
one. They added a CI rule refusing new emulator-only call sites.

**The bug class cannot arise the same way here, because the system is in the path.** RetroBat's
tree is `saves/<system>/<emulator>/`, so `saves/gamecube/dolphin-emu/` and
`saves/wii/dolphin-emu/` are distinct locations before any lookup happens, and
`data/retrobat/save_shapes.json` is keyed by RetroBat system folder for the same reason.

The part that does transfer is smaller and real. Our `shapes` map holds one `class` per system
with `shape_depends_on_emulator` as an escape hatch, used for `psx` where libretro writes a loose
`.srm` and DuckStation writes `memcards/<name>_<slot>.mcd`. Argosy's experience says the
per-emulator dimension is the normal case and the single-class system is the special one. Ours is
built the other way round.

Recorded as a lead, not a change: nothing measured says the current shape is producing a wrong
answer today, and rewriting a generated file's schema on that basis would be inventing a problem.

## A10: `GET /api/roms/identifiers` still does not scale. **Rejected, and the decision holds**

Argosy reconciles deletions through this endpoint and builds real safety machinery on top of it.
`retrobat-findings.md` measurement 81 recorded it at **504 after 300 s** on 83,131 roms, which is
why `docs/PLAN.md` rules it out in both the core-principle guardrails and M3, and the `romm-api` skill says so twice.

Re-measured on 5.2.0, with the library now at 88,331 roms:

```text
GET /api/roms/identifiers          -> 504 in 300.0s, 164 bytes
GET /api/platforms/identifiers     -> 200 in 0.27s,  494 bytes
GET /api/collections/identifiers   -> 200 in 1.23s,    5 bytes
```

Unchanged, to the second. The endpoint family is fine and this member of it is not.

**This is the confirmation the brief asked for.** Argosy is the mature client, it is in
`rommapp`, it tracks the server, and it uses this endpoint successfully at 23,873 roms. Our
library is 3.7 times larger and the endpoint takes no parameters, so it cannot be scoped or
paged out of the problem. A decision that has survived a challenge from a credible source is
worth more than one that was never tested, and this one has now survived two.

## A11: Withholding on missing evidence is already how our resolver works. **Reasoned**

Argosy's `reconcileDeletedRoms` withholds entirely on missing evidence: no id set, an empty id
set, an unavailable visibility answer, or any platform error. The reasoning is that a sweep
acting on a partial answer deletes the library.

`SetResolver` reaches the same property independently. A failed page sets `failure` and breaks
the walk, the outcome becomes `ResolutionOutcome.Interrupted` rather than `Resolved`, and
`SetsCommand` passes `complete = resolution.Outcome == ResolutionOutcome.Resolved` into
`ReplaceMembers`, which is what authorises retiring a row the pass did not see. The comment
already states the rule: "A segment of a walk is an accumulator, not a statement about what the
set holds, so only a completed walk retires the rows it did not find."

Recorded as a confirmation with nothing to change. It is worth recording precisely because it is
the kind of property that is easy to break later without noticing, and now there is a second
implementation on record that needed it.

## A12: `merged_ra_metadata` is not where our payload goes. **Rejected**

Argosy's sync doc is emphatic: `merged_ra_metadata` is 173 MiB of a 383 MiB sync, 45% of the
payload, "the whole game", and an achievement-count scalar "is worth more than everything else in
this file combined".

Measured on one 100-rom page of this library, `with_files=false`, 1431 KiB body:

| Field           | Share |
| --------------- | ----- |
| `ss_metadata`   | 46.4% |
| `igdb_metadata` | 20.5% |
| `summary`       | 7.2%  |
| `metadatum`     | 4.4%  |
| `rom_user`      | 4.1%  |

`merged_ra_metadata` does not reach the top fifteen. This library has essentially no
RetroAchievements metadata, and two provider blobs Argosy treats as minor are two thirds of the
cost here.

**This is the clearest example in the ledger of why their numbers do not transfer.** Same
endpoint, same parameters, a client with the same shape of need, and the headline conclusion
inverts. Their 45% is a fact about their library, not about `GET /api/roms`.

What survives is the shape of the question rather than any number: two provider metadata blobs
are 67% of a page, and RomMBat's gamelist generation reads a small, known field list. Whether
that is worth an upstream request depends on numbers that would have to be measured per library,
so nothing is recommended on this basis and nothing enters the plan as a cost figure.

## A13: Argosy resumes without a validator. **Confirmed, ours is the stronger design**

`DownloadManager` computes `rangeHeader = "bytes=$existingBytes-"` and sends it with no
`If-Range`. If the file on the server changed between attempts, the server answers 206 for the
new file and the client splices new bytes onto old ones. Nothing detects it.

M3 sends `If-Range` with the ETag captured before a byte of the body was read, and treats a 200
where a 206 was expected as a stale validator, discarding what is on disk. That is precisely the
failure this design feared most, and the mature client has it.

Recorded as a confirmation. Nothing changes.

One detail of theirs is genuinely nice and not worth adopting on its own: they promote a `.tmp`
whose length already equals the expected total rather than requesting a range the server would 416. Noted and dropped as A27.

## A14: Argosy omits `optimistic=false` on two of three download paths. **Confirmed, theirs**

Freegosy finding F1 established that `GET /api/saves/{id}/content` marks the device as current on
the request rather than on receipt unless `optimistic=false` is passed, and that the ack at
`POST /api/saves/{id}/downloaded` is what closes the fence.

Argosy declares the parameter with a Kotlin default of `true`:

```kotlin
suspend fun downloadSaveContentWithDevice(
    @Path("id") saveId: Long,
    @Query("device_id") deviceId: String,
    @Query("optimistic") optimistic: Boolean = true
): Response<ResponseBody>
```

Its primary sync path passes `optimistic = false` explicitly and calls `confirmSaveDownloaded`
afterwards, so the main flow is correct. Two other call sites do not: `downloadSaveById` and
`downloadSaveAsChannel` both call `downloadSaveContentWithDevice(serverSaveId, deviceId)` with the
argument omitted, taking the `true` default. Both also prefer a raw asset URL when the save row
carries one, which bypasses the device endpoint entirely.

**Worth filing upstream, and filing it is out of scope for this branch.** Recorded here so the
next session has the call sites rather than the impression.

---

## What Argosy confirmed without changing anything

Three things matched exactly, and a confirmation is the cheapest finding to write and the easiest
to overstate, so they are here rather than in the table.

**The play-session payload M7a shipped is correct.** The brief said to probe this first and
report it loudest if it disagreed. Argosy's `RomMPlaySessionEntry` is
`{rom_id, save_slot, start_time, end_time, duration_ms}` and its ingest payload is
`{device_id, sessions[]}`. Our generated `PlaySessionEntry` and `PlaySessionIngestPayload` carry
the same five and the same two fields, with the same names and the same nullability. No defect.

**The negotiate payload and response match field for field.** Their `RomMClientSaveState` sends
`rom_id, file_name, slot, emulator, content_hash, updated_at, file_size_bytes`, and their
`ReconcileOperation` reads `action, rom_id, save_id, file_name, slot, emulator, reason,
server_updated_at, server_content_hash`. `docs/PLAN.md` lists both in the M6 protocol rules, identically. Note
this is not independent corroboration: same org, same server, plausibly the same reading of the
same source.

**`with_files=false` and the sidecar opt-outs.** Argosy's doc measures `with_files=true` into a
per-file `track_meta` N+1 producing roughly 11,000 extra queries, 9 MiB and 6.2 s on a 100-rom
Nintendo Switch page, and a 502 on the request after it. `CatalogQuery` already sends
`with_files=false`, and `with_char_index=false` and `with_filter_values=false` alongside it.

---

## A note addressed to M7b

None of this is verifiable in the sense this session means. It is design input for the gamepad UI
and it is deliberately kept out of the plan's factual sections. Argosy is a shipped gamepad-first
RomM launcher at v2.8.0, which makes it the best available prior art and still not evidence.

Their UI reasoning lives in `.claude/skills/menu-patterns`, `design-tokens` and `dual-screen`,
plus `design-handoff/CONTROL-FOUNDATIONS.md` which those skills name as the design authority.

**Input conventions they treat as non-negotiable:**

- **A/Confirm means enter, commit or toggle, and never adjust.** A on an enum row opens the full
  option list as a modal; left and right cycle it. A on a stepper does nothing at all. This is the
  rule most likely to be got wrong by writing the obvious thing first.
- **Focus never moves an element.** Fill, stripe, ring and halo only. A focus style that shifts
  layout makes a gamepad list feel unanchored.
- **Inline affordances are always visible on every row**, not revealed on focus, so a row's
  interaction model is readable before you reach it.
- **Footer hints shed in a fixed priority order** as width shrinks, rather than wrapping or
  truncating.
- Enum controls use small filled triangles that rhyme with the d-pad glyphs, never text chevrons.
- Stepper bounds clamp with a distinct haptic rather than wrapping.
- Menu rows are a fixed height, 40dp, or 52 for a two-line row.

**Controller layout detection**, in `core/input/ControllerDetector.kt`, is the piece most directly
reusable in shape. It resolves a Nintendo-versus-Xbox face-button layout from, in order: USB
vendor id, device name patterns, a system property, then build identity, and it reports which
source answered. The name-pattern list is a catalogue of handheld brands (Anbernic `rg351`
through `rg556`, Retroid, Miyoo, Powkiddy, TrimUI, 8BitDo `sn30`/`sf30`), and vendor ids
`0x2dc8`, `0x20d6`, `0x0f0d` are called out as Nintendo-layout despite not being Nintendo. A
Windows handheld running RetroBat has exactly this problem and the vendor ids are the same
hardware. **The list is a starting point to verify against real devices, never a table to copy
into a data file.**

`ConnectedControllerTracker.kt` and `SoundConfig.kt` are the neighbouring pieces; the `ui/input/`
and `ui/screens/` trees are where the patterns are applied.

---

## Two process recommendations, neither built here

**The version testbed.** `testbed/romm/` stands up RomM 4.9.2, 5.0.0 and 5.1.0 against one shared
read-only library so a response shape can be compared across versions. Its README is explicit
about why: `RomMCapabilities` gated features by version and nothing recorded how the response
_shape_ changed, and their issue #173 is what fell through that gap.

We have a narrower version window, since we refuse below 5.1.0, and a real gap of the same kind:
**every API claim in this repository comes from one live instance**, and this session measured
that instance at **5.2.0**, above our declared baseline and above the 5.1.1-beta.1 the Freegosy
ledger measured against. So our claims are increasingly claims about a server newer than the one
we say we support, and nothing would tell us if 5.1.0 answered differently.

The recommendation is a single pinned 5.1.0 container to re-ask baseline questions of, not their
three-version matrix. That is enough to make "supported at 5.1.0" a measured statement instead of
an assumption, and it is a fraction of the work. **Not built on this branch, per the brief.**

Their README also records two setup traps worth having if this is ever done: the CSRF cookie is
`romm_csrftoken` and must be echoed as a header on user creation, and `email` is required on
`POST /api/users` even though the UI implies otherwise.

**Their agent-process machinery.** `.claude/hooks/verification-guard.py`, `smell-guard.py`,
`scripts/ci/agentic-smells.py` and `coupling-guard.py` exist to stop an agent claiming something
is verified when it is not, which is this repository's exact failure mode and the reason this
ledger has a route column. `scripts/ci/smell-rules.json` carries the `platform-blind-save-config`
rule that refuses new emulator-only save-path call sites, which is a nice example of turning a
bug class into a gate. Recorded as a proposal. Repo process is out of scope for this branch.

---

## What has to be true for this document to stay true

- The server was **5.2.0** and the library **88,331 roms with 708 firmware records**. Every
  timing and every share is against that. A1's ratios are a property of the library size and the
  scope, not constants.
- RetroBat was **8.2.0-stable-win64** on the development install. A7 depends on
  `emulatorlauncher` continuing to write both sort keys explicitly, which is a behaviour of that
  version.
- Argosy was read at **`3971bee4`, v2.8.0**. It ships every few days. A14's two call sites and
  A6's status list are the state of that commit.
- **`sigil` was not read.** It is a submodule and its source is not in this clone.
