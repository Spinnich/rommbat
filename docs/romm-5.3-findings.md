# RomM 5.3.0 findings

What RomM 5.3.0-alpha.2 changes for RomMBat, what it falsifies in this repo, and what it is
too early to say. Written in the shape of [retrobat-findings.md](retrobat-findings.md), and
it carries the same warning with one extra clause.

**Almost nothing here has been measured.** Every row below was settled by reading upstream
source or upstream's own release notes, and this repo's standing rule is that a changelog line
is upstream's belief rather than a measurement. Rows are labelled by the route that settled
them. A row marked `source` was read in `rommapp/romm` and is a fact about upstream's code. A
row marked `notes` is upstream's claim and nothing more. **Neither is yet a fact about
behaviour**, and no workaround comes out of this repo on the strength of one.

The exception is labelled `measured`, and there is currently one: section C, taken against a
live `5.3.0-alpha.2` server. It is the only row here that has standing to change code.

|                        |                                                                                      |
| ---------------------- | ------------------------------------------------------------------------------------ |
| Release                | `5.3.0-alpha.2`, 2026-09-13, and the floor this adopts                               |
| API read at            | tag `5.3.0-alpha.1`, plus the `alpha.1` to `alpha.2` delta, see below                |
| Vendored files read at | `master`, because `reference/refresh.sh` fetches the default branch and takes no ref |
| Floor before this      | RomM `5.2.0`, pin `romm-5.2.0.json`, RetroBat `8.2.1`                                |
| Measured against       | `5.3.0-alpha.2`, one live server, section C only. Everything else is unmeasured      |
| Date                   | 2026-09-13                                                                           |

## Three things are already true, before any adoption decision

### A. `reference/refresh.sh` is broken against upstream today

`refresh.sh` fetches `examples/config.batocera-retrobat.yml` from the default branch with no
`ref`, and that file is the seed for the platform map. Upstream gutted it. The 167 explicit
pairs [reference/README.md](../reference/README.md) counts are now **4 overrides**:

```yaml
system:
  # Batocera and RetroBat folder names are recognised automatically. The entries
  # below override folders RomM would otherwise match to their own platform.
  platforms:
    atari800: atari8bit
    model2: arcade
    model3: arcade
    pico: sega-pico
```

Confirmed on both the tag and `master`. Anyone running `refresh.sh` now gets a 4 entry seed,
`tools/build-platform-map.py --check` fails, and all four numbers in the mapping table
(167 pairs, 91 unmapped, 18 stale, 13 many to many) stop meaning anything.

**This is not drift to absorb, it is an authority that moved.** Per `CLAUDE.md`, a drift in
`reference/` is a signal to revisit the design, and the design answer here is to re-seed from
where the knowledge went rather than to accept the collapse or to freeze the old file.

**Resolved in stage 1 (#166), and it was three failures rather than one.** The seed was the
known one. The second is that `refresh.sh` also extracted the slug enum from
`backend/handler/metadata/base_handler.py`, which no longer holds it: upstream moved
`UniversalPlatformSlug` to `backend/utils/platform_slugs.py`, so the `grep` matched nothing,
and under `set -o pipefail` that killed the script before it reached the seed at all. The third
is `verify.py`'s `companies` assertion, which is #171 and is untouched here.

The re-seed walks **RetroBat's** system list and asks upstream's own rules what each folder
resolves to, rather than importing the alias table and correcting it, so the 44 keys naming
folders RetroBat lacks are never seen. Re-derived, not adjusted: 166 folders resolve (72 by
identity, 94 by alias), 74 are unmapped, normalization rescues 1 of those where it used to
rescue 16, and 10 slugs fan out where 13 did. Two slugs the old seed carried, `daphne` and
`rpgmaker`, are not `UniversalPlatformSlug` values at all and could never have matched a row.

**Four entries left the table, and two of the four name real slugs.** `odyssey` and `atari8bit`
are `UniversalPlatformSlug` values, so "two slugs RomM never had" is not the whole accounting.
`odyssey` is a seed error corrected: Magnavox Odyssey is not the Odyssey², and `odyssey-2` →
`odyssey2` now carries the real case. `atari8bit` is upstream's suggested binding for `atari800`,
recorded and not applied, so layer 2 covers it wherever the RomM folder is itself named
`atari800`; a RomM folder RetroBat lacks (`atari-8bit`, `a800`) that resolves to `atari8bit` has
no layer-3 entry, cannot be reached by normalization, and needs a manual mapping. Accepted, and
recorded in the `platform-mapping` skill.

**One slug changed which folder wins.** `model2` resolved to `["lindbergh"]` and now resolves to
`["model2", "lindbergh"]`. `lindbergh` is not a RomM slug, so the old first choice could only
ever have come from the seed; a sync set scoped to `model2` wrote into `roms/lindbergh` and now
writes into `roms/model2`, which relocates games for anyone who already synced it. Unlike
`arcade`, `model2` is not in `REQUIRES_EXPLICIT_CHOICE`, so it auto-resolves and the change is
silent. Kept, as a seed error corrected.

### B. 5.3.0 servers already meet today's client

Above `LastTested` RomMBat warns and continues, so a 5.3.0 server and a 5.2.0 floor client
are a combination that exists right now, in the field, regardless of when the floor moves.
Anything 5.3.0 adds that today's code mishandles is a **bug against the current floor**, not
adoption work that can wait. Finding 6 is the one that bites.

`ProductVersion` handles `5.3.0-alpha.2` correctly and on purpose: it compares numeric
components and ignores the suffix, so the alpha ranks as `5.3.0` and lands above `LastTested`
in the warn band. That is the documented lenient direction. **Do not "fix" it to be
semver-strict**, which would rank every prerelease below its release and, more to the point,
would refuse every stock RetroBat install whose suffix names a channel.

### C. Multi-file ROMs answer a `Range` now, and the two answers are different files (`measured`)

**Measured, not read.** `LiveContentTests.A_range_on_a_multi_file_rom_is_refused_which_is_why_none_is_sent`
fails against a live `5.3.0-alpha.2`, which is the job that test was written to do: it is kept as
a test rather than as prose "so a server that changes its mind is noticed here instead of in the
field". The server changed its mind.

The rule it encodes is a 5.2.0 measurement, recorded at `PLAN.md:1226` and in the `romm-api`
skill: **any `Range` on a multi-file ROM is refused 403 by nginx**, so multi-file is not
resumable by any header. Re-measured on one multi-file ROM, `neogeocd`, repeated and stable:

| Request               | Status | `Content-Length` | `ETag`              | `Accept-Ranges` |
| --------------------- | ------ | ---------------- | ------------------- | --------------- |
| no `Range`            | 200    | 2,740,790        | absent              | absent          |
| `Range: bytes=0-`     | 206    | 2,740,768        | `"6aa70200-29d220"` | implied by 206  |
| `Range: bytes=0-1023` | 206    | 1,024            | same                | implied by 206  |

The 403 is gone and multi-file is range-served. **The interesting part is the 22 byte
difference.** The two responses to one URL describe different resources: only the ranged one
carries a validator, and `Content-Range` totals 2,740,768 against the plain response's
2,740,790. Both figures are stable across repeated requests, so this is not a timestamp in a
zip rebuilt per call. Range semantics assume one representation, and here there are two.

**This is finding B again, and it is the sharper case.** A 5.3.0 server already meets today's
client, so this is a bug against the current floor rather than adoption work. **Today's code is
still correct, and its stated reason is now wrong.** `RomMConnection.Content.cs:86` gates the
header on `!request.IsMultiFile` and the comment above it gives the 403 as the reason, which is
the thing that stopped being true. The behaviour must not change on the strength of the refusal
having gone: a resume that begins on the plain response and continues on the ranged one splices
two artifacts, and the plain response carries no `ETag`, so the stale-validator restart that
protects the single-file path has nothing to compare against here.

**Single-file is unaffected**, re-checked on a 47 GB `.iso` rather than inferred: the plain 200
carries `ETag`, `Accept-Ranges: bytes` and a `Content-Length` of 47,976,480,768, and the 206
reports the same `ETag` and the same total. The two representations agree, which is nginx
serving one file from disk, and it is the contrast that makes the multi-file mismatch a finding
rather than a quirk of the measurement.

**Re-measured across a second platform in stage 1**, which #180 asked for before anything
acted on the first reading. It reproduces, and it is worse than "22 bytes" suggested:

| ROM                      | plain 200 `Content-Length` | ranged `Content-Range` total | gap |
| ------------------------ | -------------------------- | ---------------------------- | --- |
| `neogeocd`, rom 272137   | 2,740,866                  | 2,740,768                    | 98  |
| `pcenginecd`, rom 272627 | 9,439,703                  | 9,439,567                    | 136 |

**The gap is per-ROM, not a constant.** The plain length is stable across repeats within a
session (three requests, 2,740,866 every time), so it is still not a per-call rebuild, but the
first neogeocd reading above recorded 2,740,790 for that platform and this one reads 2,740,866,
so it is not stable across days either. Neither figure is retracted; both are what was served
when they were taken.

**The mechanism is now visible in the `ETag`.** nginx's validator is `hex(mtime)-hex(size)`, and
its size half is exactly the ranged total in both rows: `0x29d220` is 2,740,768 and `0x90094f` is
9,439,567. So the ranged answer is **nginx serving a zip that exists on disk** while the plain
answer is **one the application builds for the request**. They are two artifacts by construction,
which is why no amount of retrying will make them agree and why the mismatch, not the withdrawn
403, is the durable reason not to send the header.

**Acted on in stage 1 (#180).** The test is re-aimed rather than made version-aware: it now
asserts that the two answers are not interchangeable, which a 403 satisfies and a mismatched 206
satisfies, and it fails loudly on the day a server makes them agree. Four comments that gave the
403 as their reason now give the mismatch, in `RomMConnection.Content.cs`, `RomContent.cs`,
`RomRow.cs` and `SyncSetStore.cs`. Multi-file resume is **still not built**: it would need the
transfer pinned to the ranged representation for its whole life, and the plain response offers no
validator by which a mix could be detected.

**Owed:** the 5.2.0 reading stays where it is, labelled, per the version move checklist's rule on
provenance, and both sites name this section.

## Findings

### 1. The platform folder authority moved into source (`source`)

`backend/utils/platform_aliases.py` is the new home:

| Symbol                                | What it is                                                                                                                    |
| ------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| `PLATFORM_FS_ALIASES`                 | 138 folder name to slug entries, for folder names that differ from RomM slugs                                                 |
| `PLATFORM_SLUG_FOLDERS`               | the reverse map, built by `_unambiguous_folders()`                                                                            |
| `resolve_platform_slug(fs_slug, cfg)` | config binding first, then identity if the folder name is itself a slug, then the alias table, then the folder name unchanged |
| `resolve_fs_slug(slug, cfg)`          | slug back to folder, `None` when nothing maps or several folders collapse onto it                                             |

Two structural points fall out, and both matter more than the entry count.

**The identity cases became implicit.** `resolve_platform_slug` returns `key` when the folder
name is already a valid `UniversalPlatformSlug`, which is why `3do: 3do`, `nes: nes` and the
rest left the YAML. Reconstructing the full mapping means the alias table **plus** the slug
enum, and this repo already vendors the enum as `reference/romm-slugs.txt`. So the re-seed
source is strictly better than the file it replaces: it is the code RomM runs, not an example
nobody executes.

**The table is a three way union, and 44 of its keys are not RetroBat.** Counted against
`reference/systems_names.lst`:

|                                                  |     |
| ------------------------------------------------ | --- |
| Alias keys                                       | 138 |
| Of those, names that are RetroBat system folders | 94  |
| Of those, names RetroBat does not have           | 44  |

The 44 are ES-DE and Batocera spellings (`atarijaguar`, `atarilynx`, `atarixe`, `gc`,
`megadrivejp`, `cdimono1`, `fba`, `mame-advmame`). RetroBat's own names for those are
`jaguar`, `lynx`, `xegs`, `gamecube`, `megadrive`. **Core principle 3 is unchanged by this**:
RetroBat's live `es_systems.cfg` remains the authority on what folders exist, and upstream's
table is a better seed for what they map to. Feeding the 44 into our map unfiltered would put
folders in it that no RetroBat install has.

`_unambiguous_folders()` dropping many to one collapses independently confirms the existing
finding that the mapping is many to many, and `resolve_fs_slug` returning `None` for `arcade`
is upstream reaching the same conclusion this repo did.

**Owed:** `refresh.sh` re-sourced, `tools/build-platform-map.py` re-pointed, `verify.py`'s
four numbers re-derived rather than edited, the mapping table in `reference/README.md` and the
`platform-mapping` skill corrected.

### 2. A ROM identity triple lands on the ROM schema (`source`)

`DetailedRomSchema` gains **three** fields, not two, and all three are declared columns readable
in source rather than a claim in a release note:

| Field                | Declared at                                               |
| -------------------- | --------------------------------------------------------- |
| `title_id`           | `backend/models/rom.py:806`, response schema `rom.py:407` |
| `save_target`        | `backend/models/rom.py:810`, response schema `rom.py:408` |
| `save_target_layout` | `backend/models/rom.py:814`, response schema `rom.py:409` |

Upstream carries them as one unit, `RomIdentity` at `backend/models/rom.py:153-155`, described
there as "the triple that travels from the scan through to the `Rom` columns of the same names".
`save_target_layout` is an enum, `SaveTargetLayout` at `backend/models/rom.py:137-142`:

```python
class SaveTargetLayout(enum.StrEnum):
    FOLDER_EXACT = "folder-exact"
    FOLDER_PREFIX = "folder-prefix"
    FILE_EXACT = "file-exact"
    FILE_PREFIX = "file-prefix"
    FOLDER_SPLIT = "folder-split"
```

The values are extracted from game binaries during scan, gated on `TITLE_ID_EXTRACTION_ENABLED`
in the heartbeat. Upstream names PSX, PS2, PS3, PSP, PS Vita, Switch, 3DS, Wii, Wii U, GameCube,
Dreamcast, Xbox and Xbox 360, and says the purpose includes determining save locations for
device sync.

**Two different things are deferred here, and only one of them is unmeasured.** That the columns
exist, and what shapes `save_target` can take, are settled in source today. Whether any real
library actually carries values in them, and whether those values agree with what this repo
measured, needs a live instance and is what #168 gates. Filing the first as belief is what left
the falsified premise below standing unqualified.

**This falsifies a claim this repo repeats in four places.** "RomM stores no serial, title ID
or product code anywhere, so no API lookup exists" appears in
[save-sync](../.claude/skills/save-sync/SKILL.md) and at `PLAN.md` lines 2602, 3699 and 3957.
It was true at 5.2.0 and it is the premise the whole attribution design rests on. **All four now
carry a one-line qualifier naming this finding**, because `save-sync` is the file a later session
loads and this document is linked from nothing.

The complement is close to exact. This repo measured header reads at GameCube 100%, Wii 75.5%,
and **0% of PSP, PS3 and PSX**, because no constant offset reaches a `.cso`, a `.chd` or an
ISO9660 filesystem. Upstream is claiming coverage of precisely the systems where our own route
returns nothing.

**What this is not.** It is not a replacement for the three existing routes, and the existing
design already says why: routes are asked in parallel because their agreement is the only
evidence a binding has, and disagreement fails closed. `title_id` becomes a fourth route with
the same standing as the others. It is also capability gated, so a server with extraction off,
or a library scanned before the feature existed, answers nothing. The header and sidecar
routes stay regardless of what the measurement shows.

**`save_target_layout` names the same distinctions this repo measured, which makes the
comparison a real one.** `save-sync` records that psp's key is a _prefix_ of the segment
(`ULES01513SYSDATA`), that `ps3` keeps three directories under one title id, and that `gamecube`
has no per-game directory at all. Upstream's five members read as the same taxonomy arrived at
independently, and independent agreement is worth more than either reading alone. The open
question is therefore sharper than "what shape is `save_target`": it is **whether the layout
upstream assigns agrees, per system, with the shape this repo measured**, and a disagreement is
the interesting case rather than a tie break.

**Owed, and in this order:** measure coverage on a real library first, then decide. Nothing in
`save-sync` changes on the strength of a release note.

### 3. Memory card endpoints are for the browser player, not for us (`notes`)

Eleven new endpoints under `/memory-cards`, with versions, sharing and visibility. Read
alongside the Streaming V2 entry, these exist to give container pooled PS2 and GameCube browser
sessions a server managed card.

**That is the opposite direction from RomMBat's model**, which is a real emulator on a real
disk writing real files, and whose class C and class D work converts shared containers to per
game wherever the emulator allows. Memory cards are not our save transport, and this finding
exists mainly so the next session does not re-litigate it.

Two genuine uses survive, and both are narrow:

1. **Issue #82**, GameCube as class D when `dolphin_slotA` is `MEMORY CARD`. A raw card now has
   a first class place to live server side, where before there was nowhere sensible to put one.
2. **Interop.** A card written by a browser streaming session is currently invisible to the
   desktop, and a user with both is a user whose two save paths do not see each other.

### 4. Two new writers on the saves the conflict route was measured against (`notes`)

`emulatorjs.auto_save_sync` uploads whenever the emulator writes. Streaming V2 pulls saves and
states back with history, under `STREAMING_STATE_HISTORY_LIMIT` defaulting to 50 states per
rom, emulator and user.

Server side save rows can now change far more often, and from more directions, than when this
repo's negotiation behaviour was measured at 5.2.0. That lands on #156, #157 and #138, and on
the in flight write guard. Neither feature is on by default, which bounds the blast radius and
does not remove it.

### 5. ROM identity is content hashed, and rebindable (`notes`)

Identity is now derived from content, so saves and play history survive a file moving, and
`PUT /roms/{id}/identity` rebinds after a move. Mostly good news: it is the failure mode this
repo handles as orphans. The open question is whether a rebind can change what a rom id means
under a cached attribution binding or a set membership row, which is a question for a live
instance and not for a release note.

### 6. Physical games mean a sync set can hold a row with no file (`source`, now `measured`)

`POST /roms/physical` creates a rom with no file on disk, and `DetailedRomSchema` gains
`is_physical` and `has_file_on_disk`. A catalog page can now return rows that cannot be
downloaded.

**This is finding B's concrete case.** It needs no floor move and no pin move to reach a user,
and the fix is a filter plus a null check rather than a feature.

**Upgraded from `notes` in stage 1 (#167), and the hazard is wider than physical games.** Both
fields are declared on `RomSchema`, the base that `SimpleRomSchema` extends, so they are on every
row `GET /api/roms` returns rather than only on the detail route; confirmed on a live
`5.3.0-alpha.2` page, which carries `is_physical`, `has_file_on_disk` and `missing_from_fs`
together. And `has_file_on_disk` is a property, not a column:

```python
return not self.is_physical and not self.missing_from_fs
```

`missing_from_fs` is **required at the 5.2.0 floor**, so a ROM deleted from the server's disk has
been reaching the download path since before any of this, with no physical game involved. That is
the half of the bug that was already in the field.

**What was built.** The drop is client-side and derives the answer where the server does not give
it: `RomRow.HasFileOnDisk` honours `has_file_on_disk` when present and falls back to
`not is_physical and not missing_from_fs` when it is absent, which is what keeps a 5.2.0 server's
whole library from being excluded. `SetResolver` gives it its own `ExcludedNoFileOnDisk` state
ahead of the shape and extension checks, because neither is what is wrong with the row, and
`PickedSetService` refuses the same case per game.

**Server-side filtering is deliberately not used.** `GET /api/roms` takes both a `missing` and a
`physical` boolean, but only `missing` exists at 5.2.0; `physical` arrived with this release. One
client-side rule covers both server generations, and it is worth revisiting when #174 moves the
floor.

### 7. The gamelist exporter was substantially rewritten (`source`)

`backend/utils/gamelist_exporter.py` on `master` no longer resembles the vendored copy. It now
parses and merges existing gamelists through `defusedxml` (upstream's "merge instead of
overwriting" fix), took `ASSET_DIRS` into `config.PLATFORM_MEDIA_DIRS`, and gained
`HAS_FILE_ON_DISK_FILTERS`, `rel_platform_folder` and `join_rel_path`.

This repo derives two unit conversions from that file, that `first_release_date` is
milliseconds and `average_rating` is on 0 to 100, and `verify.py` asserts them as behaviours
rather than counts precisely so a rewrite like this one is survivable. **Both survive**, read at
`master`: `first_release_date` is still divided by 1000 at line 125, and `average_rating` by 100
at line 340 with the "0-100 scale" comment intact. Asserting behaviours rather than line numbers
is what made that a two minute read instead of a re-derivation.

What #171 still owes is the part a rewrite can move without moving a constant: the three
documented deliberate divergences (companies, region and lang, genre) and the `marquee` /
`logo_path` rule.

**One divergence may be able to end.** 5.3.0 splits company metadata into `publishers` and
`developers` on `DetailedRomSchema`. That is this repo's own upstream follow up 3
(`PLAN.md` "Optional follow ups to RomM itself"), which argued that `companies[0]` and
`companies[1]` write the alphabet into two role bearing fields. If the split is real, the
reason this repo joins companies into `developer` and omits `publisher` goes away.

`reference/romm-known_bios_files.json` is **unchanged** on `master`.

### 8. Grants tightened on the export endpoints (`source`)

`POST /export/gamelist-xml` and `POST /export/pegasus` now require a `PLATFORMS` / `WRITE`
grant and enforce platform visibility. **RomMBat calls neither**, confirmed by grep rather than
assumed: no hand written C# reaches either path, and the single hit repo-wide is the string
`pegasus_export` as a JSON property name at `src/RomM.Client/Generated/RomMApiSchema.g.cs:5639`,
which is a generated DTO field and not a call site. So the tightened grant costs the pairing
scope set nothing.

What #176 still owes is the line in the `romm-api` skill, because an answer that lives only in
this document is one the next session re-derives.

### 9. Performance measurements move, and must not be edited (`notes`)

`SCAN_WORKERS` and `WEB_SERVER_CONCURRENCY` both default to 4, up from 1. An N+1 in
`GET /api/roms` is fixed. Pooled connections are now recycled before the server drops them.

Every timing in this repo is a measurement of 5.2.0 at concurrency 1, and each one carries the
scope it was taken on: **8 minutes 15 seconds for a platform scope of 9,196 roms** at 250 rows a
page before #88, 2.3 s against 8.5 s for a `platform_ids` scoped page at a library of 88,331,
and the 300 s session timeout. Per the version move checklist those are provenance and **do not
get rewritten to new numbers**, and that includes the scope: a re-measure that walks the whole
88,331 rom library has measured a different thing and has nothing to compare against. They get
re-measured on the same scope, and the new numbers get their own attribution.

### 10. Not adopting (`notes`)

Recorded so the next session does not re-open them: Jukebox, walkthroughs, physical game
barcode scanning, library aware recommendations, the four new metadata sources (Steam, Demozoo,
Pouet, CSDb), js-dos and PICO-8 in browser play, and Emulator Streaming V2 as a feature.
RomMBat's job is a local RetroBat install, and none of these reach it. Streaming survives only
as findings 3 and 4, which are about contention rather than adoption.

The filesystem structure changes (Structure B detection removed, `filesystem.roms_folder` and
`filesystem.firmware_folder` gone, `GET /setup/library` reshaped) are **server administration**
and inert here, because RomMBat maps to RetroBat's folders and never to the server's. They are
listed so that inertness is a recorded conclusion rather than an omission.

**Inert for the client is not inert for whoever stands one up.** 5.3.0 makes the layout an
explicit declaration and **an instance refuses to start** until `filesystem.structure` is set in
`config.yml`, printing the equivalent template if the removed keys are still present. Anyone
following `DEVELOPER_SETUP.md` to raise a disposable RomM, for a schema capture or to answer one
of the questions below, hits that before the server ever listens. The structure-A equivalent is:

```yaml
filesystem:
  structure:
    default: "roms/{platform}/{game}"
    firmware: "bios/{platform}"
```

## What the floor move costs, and what gates it

**The decision is to move the floor to 5.3.0-alpha.2**, not to the `alpha.1` this document was
first written against. `alpha.2` was published on 2026-09-13, about eight hours after `alpha.1`,
which is the same day this assessment was made: the tag moved underneath the work rather than
after it. The repo's standing rule is to track the newest stable and to adopt within one release
of it appearing, and picking the older of two same-day prereleases would be adopting a build
that was already superseded before the PR opened. It is also the version a live server is
actually running, which is what turns the remaining questions from reading into measurement.

**What that costs is one read, and it is done.** `alpha.1` to `alpha.2` is 19 commits across 21
files, every one of them under `backend/`: twelve migrations revised, `roms_handler.py`
(+340/-142), `endpoints/roms/__init__.py`, `utils/database.py`, and their tests. Two things
follow. It does not touch `backend/models/rom.py` or `backend/endpoints/responses/rom.py`, so
**finding 2's source reads hold at either tag**. It does land squarely on the `GET /api/roms`
query path, which is where finding 9's timings and this repo's paging and scoping behaviour
live, so the re-measurement that finding 9 owes is attributed to `alpha.2` and not to `alpha.1`.

The version move checklist in the `pre-pr-verification` skill is the procedure.

| Step                                                       | State                                                                |
| ---------------------------------------------------------- | -------------------------------------------------------------------- |
| `refresh.sh`, resolve drift rather than editing the number | Finding 1 done. **Still owes finding 7**, which flips on the re-pull |
| Read the upstream changelog end to end                     | Done, this document                                                  |
| Move `Minimum`, `LastTested`, README table and compat row  | Ready, and a test asserts the trio agree                             |
| Re-check open issues in `retrobat-findings.md`             | Not applicable, no RetroBat move in this adoption                    |
| Leave provenance alone                                     | See finding 9                                                        |
| **Move the pinned OpenAPI schema**                         | **Unblocked by the floor choice**, see below                         |

`src/RomM.Client/openapi/generate.sh` regenerates DTOs from the pinned file, but the pinned
file itself is a byte exact `/openapi.json` captured from a running server, per
`DEVELOPER_SETUP.md`. FastAPI builds that schema at runtime and the repo does not hold one.

**The server that answers is the server the floor now names.** The instance the `Live*` tests
point at runs `5.3.0-alpha.2`, which is how section C came to be measured, and with the floor at
`alpha.2` it is also a legitimate source for the pin: a capture from it describes the same build
the floor declares. That removes the Docker step from this adoption. `docker compose` v5.1.4 is
on this machine with the daemon stopped, and a disposable instance is still the documented route
whenever the floor and the reachable server disagree, which is the situation this adoption
avoided rather than solved.

Two conditions on the capture, because a pin is a byte exact artifact and not a convenience.
It must come from a server that reports `5.3.0-alpha.2` at `/api/heartbeat`, checked at capture
time rather than assumed, since the instance is somebody's live library and can be upgraded
underneath the work exactly as upstream's tag was. And the capture is what regenerates the DTOs,
so `src/RomM.Client/openapi/generate.sh` runs against it and the diff gets reviewed, per the
committed-DTOs rule.

**Findings 2, 5, 6 and 9 are now measurable rather than parked**, on that same server, with
every reading attributed to `alpha.2`.

## Open, and needing a live instance

**Three questions left this table without one.** Whether the exporter's unit conversions survived
the rewrite, and whether RomMBat calls either export endpoint, are answered above from upstream
source and a repo grep. Finding 6's row shape is answered from the schema's inheritance and then
confirmed on a live page in stage 1. Reach for the cheaper route first: a question parked behind a
Docker daemon that a read answers today is a question nobody answers.

| #   | Question                                                                             |
| --- | ------------------------------------------------------------------------------------ |
| 2   | What fraction of a real PSX, PS2, PS3 and PSP library actually carries a `title_id`? |
| 2   | Does `save_target_layout` agree, per system, with the shape this repo measured?      |
| 5   | Can an identity rebind change what a rom id means to a cached binding or a set row?  |
| 9   | Every timing in this repo, re-measured at concurrency 4                              |
