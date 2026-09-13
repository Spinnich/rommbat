# RomM 5.3.0 findings

What RomM 5.3.0-alpha.1 changes for RomMBat, what it falsifies in this repo, and what it is
too early to say. Written in the shape of [retrobat-findings.md](retrobat-findings.md), and
it carries the same warning with one extra clause.

**Nothing here has been measured.** Every row below was settled by reading upstream source or
upstream's own release notes, and this repo's standing rule is that a changelog line is
upstream's belief rather than a measurement. Rows are labelled by the route that settled them.
A row marked `source` was read in `rommapp/romm` and is a fact about upstream's code. A row
marked `notes` is upstream's claim and nothing more. **No row here is yet a fact about
behaviour**, and no workaround comes out of this repo on the strength of one.

|                        |                                                                                      |
| ---------------------- | ------------------------------------------------------------------------------------ |
| Release                | `5.3.0-alpha.1`, 2026-09                                                             |
| API read at            | tag `5.3.0-alpha.1`                                                                  |
| Vendored files read at | `master`, because `reference/refresh.sh` fetches the default branch and takes no ref |
| Floor before this      | RomM `5.2.0`, pin `romm-5.2.0.json`, RetroBat `8.2.1`                                |
| Measured against       | nothing yet. No live 5.3.0 instance has been stood up                                |
| Date                   | 2026-09-13                                                                           |

## Two things are already true, before any adoption decision

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

### B. 5.3.0 servers already meet today's client

Above `LastTested` RomMBat warns and continues, so a 5.3.0 server and a 5.2.0 floor client
are a combination that exists right now, in the field, regardless of when the floor moves.
Anything 5.3.0 adds that today's code mishandles is a **bug against the current floor**, not
adoption work that can wait. Finding 6 is the one that bites.

`ProductVersion` handles `5.3.0-alpha.1` correctly and on purpose: it compares numeric
components and ignores the suffix, so the alpha ranks as `5.3.0` and lands above `LastTested`
in the warn band. That is the documented lenient direction. **Do not "fix" it to be
semver-strict**, which would rank every prerelease below its release and, more to the point,
would refuse every stock RetroBat install whose suffix names a channel.

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

### 2. `title_id` and `save_target` land on the ROM schema (`notes`)

`DetailedRomSchema` gains `title_id` and `save_target`, extracted from game binaries during
scan, gated on `TITLE_ID_EXTRACTION_ENABLED` in the heartbeat. Upstream names PSX, PS2, PS3,
PSP, PS Vita, Switch, 3DS, Wii, Wii U, GameCube, Dreamcast, Xbox and Xbox 360, and says the
purpose includes determining save locations for device sync.

**This falsifies a claim this repo repeats in four places.** "RomM stores no serial, title ID
or product code anywhere, so no API lookup exists" appears in
[save-sync](../.claude/skills/save-sync/SKILL.md) and at `PLAN.md` lines 2602, 3699 and 3957.
It was true at 5.2.0 and it is the premise the whole attribution design rests on.

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

### 6. Physical games mean a sync set can hold a row with no file (`notes`)

`POST /roms/physical` creates a rom with no file on disk, and `DetailedRomSchema` gains
`is_physical` and `has_file_on_disk`. A catalog page can now return rows that cannot be
downloaded.

**This is finding B's concrete case.** It needs no floor move and no pin move to reach a user,
and the fix is a filter plus a null check rather than a feature.

### 7. The gamelist exporter was substantially rewritten (`source`)

`backend/utils/gamelist_exporter.py` on `master` no longer resembles the vendored copy. It now
parses and merges existing gamelists through `defusedxml` (upstream's "merge instead of
overwriting" fix), took `ASSET_DIRS` into `config.PLATFORM_MEDIA_DIRS`, and gained
`HAS_FILE_ON_DISK_FILTERS`, `rel_platform_folder` and `join_rel_path`.

This repo derives two unit conversions from that file, that `first_release_date` is
milliseconds and `average_rating` is on 0 to 100, and `verify.py` asserts them as behaviours
rather than counts precisely so a rewrite like this one is survivable. Both need re-deriving
against the new file, along with the three documented deliberate divergences.

**One divergence may be able to end.** 5.3.0 splits company metadata into `publishers` and
`developers` on `DetailedRomSchema`. That is this repo's own upstream follow up 3
(`PLAN.md` "Optional follow ups to RomM itself"), which argued that `companies[0]` and
`companies[1]` write the alphabet into two role bearing fields. If the split is real, the
reason this repo joins companies into `developer` and omits `publisher` goes away.

`reference/romm-known_bios_files.json` is **unchanged** on `master`.

### 8. Grants tightened on the export endpoints (`notes`)

`POST /export/gamelist-xml` and `POST /export/pegasus` now require a `PLATFORMS` / `WRITE`
grant and enforce platform visibility. RomMBat writes its own gamelists and is not believed to
call either, which needs confirming rather than assuming, because the `romm-api` skill
documents the scope set and a sixth scope kind landed recently.

### 9. Performance measurements move, and must not be edited (`notes`)

`SCAN_WORKERS` and `WEB_SERVER_CONCURRENCY` both default to 4, up from 1. An N+1 in
`GET /api/roms` is fixed. Pooled connections are now recycled before the server drops them.

Every timing in this repo (8 minutes 15 seconds for a full roms walk at 88,331 roms, 2.3 s
against 8.5 s for a `platform_ids` scoped page, the 300 s session timeout) is a measurement of
5.2.0 at concurrency 1. Per the version move checklist those are provenance and **do not get
rewritten to new numbers**. They get re-measured, and the new numbers get their own attribution.

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

## What the floor move costs, and what gates it

The decision is to move the floor to 5.3.0-alpha.1. The version move checklist in the
`pre-pr-verification` skill is the procedure, and two of its steps cannot be run yet.

| Step                                                       | State                                                              |
| ---------------------------------------------------------- | ------------------------------------------------------------------ |
| `refresh.sh`, resolve drift rather than editing the number | **Blocked on finding 1.** Re-source first, or it fails on the seed |
| Read the upstream changelog end to end                     | Done, this document                                                |
| Move `Minimum`, `LastTested`, README table and compat row  | Ready, and a test asserts the trio agree                           |
| Re-check open issues in `retrobat-findings.md`             | Not applicable, no RetroBat move in this adoption                  |
| Leave provenance alone                                     | See finding 9                                                      |
| **Move the pinned OpenAPI schema**                         | **Gated on a live 5.3.0-alpha.1**                                  |

`src/RomM.Client/openapi/generate.sh` regenerates DTOs from the pinned file, but the pinned
file itself is a byte exact `/openapi.json` captured from a running server, per
`DEVELOPER_SETUP.md`. FastAPI builds that schema at runtime and the repo does not hold one.

**A disposable RomM via Docker is the documented route for exactly this**, and this machine has
`docker compose` v5.1.4 with the daemon not running. Standing up `rommapp/romm:5.3.0-alpha.1`
long enough to capture `/openapi.json` unblocks the pin, and the same instance is what
findings 2, 5, 6 and 9 need in order to stop being release notes.

## Open, and needing a live instance

| #   | Question                                                                             |
| --- | ------------------------------------------------------------------------------------ |
| 2   | What fraction of a real PSX, PS2, PS3 and PSP library actually carries a `title_id`? |
| 2   | What shape is `save_target`, and does it name a path, a key, or a container?         |
| 5   | Can an identity rebind change what a rom id means to a cached binding or a set row?  |
| 6   | What does a `is_physical` row look like on `GET /api/roms`, and is it filterable?    |
| 7   | Do the two unit conversions survive the exporter rewrite?                            |
| 8   | Does RomMBat call either export endpoint anywhere?                                   |
| 9   | Every timing in this repo, re-measured at concurrency 4                              |
