---
summary: Paging the ROM list, its sidecars, identifiers, hashes, filters and platform identity.
read-when: Before reading the catalog, filtering it, or trusting a field on a ROM row.
---

# RomM: catalog

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-12. There are four default-on flags, plus `with_files` as a fifth opt-in

Plan says: "the three sidecar flags" on `GET /api/roms` (L377-378)

Measurement says: There are **four** default-on flags, plus `with_files` as a fifth opt-in

## RB-13. It is a flat ~841 KB resent on every page, and costs bandwidth not server time

Plan says: Sidecar cost is framed as a per-page latency question (L377-380)

Measurement says: It is a flat ~841 KB resent on every page, and costs bandwidth not server time. Fetch once, then disable

## RB-14. 715 KB for a single collection, 99% of it inlined cover-art paths, with no pagination available

Plan says: `GET /api/collections` response size is an open question (L378-379)

Measurement says: 715 KB for a **single** collection, 99% of it inlined cover-art paths, with no pagination available

## RB-71. `fs_size_bytes` is an `int32` in `SimpleRomSchema`, `PlatformSchema` and `RomFileSchema`, because the pinned

Previously: Generated DTOs are usable for the paged read (plan M2, rule 6)

Measurement says: **`fs_size_bytes` is an `int32`** in `SimpleRomSchema`, `PlatformSchema` and `RomFileSchema`, because the pinned schema declares a bare `integer`. `GET /api/platforms` fails to deserialize on the **first** platform of a real library. Slim hand-written rows instead

## RB-72. Not unique

Previously: `platform.slug` identifies a platform (plan M2, `platform-mapping`)

Measurement says: **Not unique.** A real 123-platform library has **72 distinct slugs**: every system has an `-unofficial` twin sharing one. `fs_slug` and `id` are unique. Keyed by slug, 51 of 123 platforms vanish from the mapping surface

## RB-75. 167 and 18

Previously: The YAML has 168 explicit pairs and 19 stale entries (plan L848, L852)

Measurement says: 167 and 18. `verify.py` split on the first `platforms:` and matched any four-space key, which also catches `scan.gamelist.export`, a boolean. Both the script and the plan now read the block by indentation. Nothing upstream moved

## RB-76. Whether `fs_extension` is always present

Previously: (not addressed) whether `fs_extension` is always present

Measurement says: No. 23 ROMs on one platform of a real instance carry an empty `fs_extension`. They are excluded like any other unlaunchable format, but must not be reported as format "`.`"

## RB-77. Whether `order_by=id` is accepted

Previously: (not addressed) whether `order_by=id` is accepted

Measurement says: It is, and ascending id is what makes offset paging survive a library changing mid-walk: new ROMs get higher ids and land past the cursor. Verified live that page two starts after page one with no overlap

## RB-81. 504 after 300 s on 83,131 ROMs, so the mechanism does not work at the scale the plan exists to serve

Previously: Reconcile deletions against `GET /api/roms/identifiers` (plan M3, core principle 2)

Measurement says: **504 after 300 s** on 83,131 ROMs, so the mechanism does not work at the scale the plan exists to serve. It takes no parameters, so it cannot be scoped or paged. `/api/platforms/identifiers` answers in **0.3 s** (490 bytes, 123 ids) and `/api/collections/identifiers` in **1.4 s**, so the endpoint family is fine and this member of it is not

## RB-82. The rule behind it: every multi-file ROM is extensionless and every extensionless ROM is multi-file, 105 of

Previously: 23 ROMs on one platform carry an empty `fs_extension` (RB-76)

Measurement says: The rule behind it: **every multi-file ROM is extensionless and every extensionless ROM is multi-file**, 105 of 105 both ways in the sample. M2's extension filter therefore already excludes **100%** of multi-file ROMs, which is why nothing above reaches a sync set today. **Half withdrawn**: the forward direction holds and the converse does not. An extensionless ROM can be a folder holding one file, with `has_multiple_files` false. See [freegosy-findings.md](../../freegosy-findings.md), which found 391 of 602 extensionless ROMs like that, and reproduced again on 5.3.0-alpha.2 by moving a lone `.zip` into a new subfolder. The code was always keyed on the flag, so nothing mis-excluded

## RB-84. What `GET /api/roms/by-hash` costs

Previously: (not addressed) what `GET /api/roms/by-hash` costs

Measurement says: It accepts `md5_hash`, `sha1_hash` or `crc_hash` and all three returned the same ROM. A hit is a ~12 KB `DetailedRomSchema` in **133-385 ms**; a **miss is a 404 after 8.3 s**. Usable to attribute a handful of unknown local files, never as a library-wide adoption sweep

## RB-85. Whether every ROM carries a hash

Previously: (not addressed) whether every ROM carries a hash

Measurement says: No. Of 1,895 single-file ROMs sampled, **1,724 (91.0%) carry `md5_hash`** and **1,824 (96.3%) `sha1_hash`**. Verification has to degrade to size when the server has no hash to compare against, and say that it did **Superseded by RB-257**: across the whole library the three hashes are set together or blank together, and no row carries a sha1 without an md5

## RB-87. The cost of a per-ROM existence check

Previously: (not addressed) the cost of a per-ROM existence check

Measurement says: `GET /api/roms/{id}/simple` took **4.2 s** for a hit and **0.45 s** for a miss, so checking locally present ROMs one at a time is not a cheap substitute for a reconcile either

## RB-93. It costs N requests and buys nothing

Previously: `GET /api/roms/{id}` is how M4 gets metadata (plan M4 by implication)

Measurement says: **It costs N requests and buys nothing.** `SimpleRomSchema`, which the paged read already returns, carries `metadatum`, `summary`, every media path, `regions` and `languages`. `DetailedRomSchema`'s 7 extra fields are all user arrays and were **empty on every ROM tried**. Per-ROM: 0.15 s each, **150 s for 1,000 games**. Per-page: 0 extra requests, 15.7% of a page M2 already reads

## RB-94. Whether the present ROMs can be asked for by id

Previously: (not addressed) whether the present ROMs can be asked for by id

Measurement says: **No.** `GET /api/roms` has 47 query parameters and not one of them takes ROM ids. "Metadata for exactly what is on disk" is not a query, so it is either the walk carrying it or one request per ROM

## RB-181. It reports an empty string

The claim being checked: A rom carrying no md5 reports it as null, so `nothingToCompare` reaches the size fallback (**`ContentPlanner`**)

What was measured: **It reports an empty string.** Rom 191723 comes back with `md5_hash: ''` and `crc_hash: ''` rather than null, with only `sha1_hash` populated. `ContentPlanner.nothingToCompare` tests `member.Md5Hash is null && member.Sha1Hash is null`, and an empty string is not null, so for a rom whose hashes are **all** empty strings the size-only adoption path is unreachable and the file is re-downloaded and re-refused on every sync. Not reached here, because this rom's sha1 is populated, and not investigated further because it is M3's code rather than this stage's. RB-85 measured that 9% of a real library carries no md5, so how RomM represents that absence decides whether that 9% is adoptable. **Fixed in #79**: `RomRow` and `FirmwareRow` now read a blank as absent, so a blank never reaches a consumer, and `ContentPlanner` tests the md5 for blank rather than for null because a member can be built by a caller that never passed the boundary. `StubRomMServer` answers `''` the way the server does, so the shape is exercised by the whole suite rather than by one test

## RB-236. Probed, because the schema does not say and the server does not complain

Question: (not addressed) which values `metadata_providers` and `statuses` accept, since the filter screen has to offer a list

Measured: **Probed, because the schema does not say and the server does not complain.** The pinned 5.2.0 schema declares both as a bare array of strings with no enumeration, and `GET /api/roms` **silently ignores** a `metadata_providers` value it does not recognise: `metadata_providers=zzz-not-a-provider` returned the full **96,060**, identical to no filter at all. So a wrong entry in a picker is worse than a missing one, since the user picks a provider and is handed the whole library. Probed one at a time against the live instance, where a recognised value moves the total and an unrecognised one does not: **igdb 473, ss 87,079, ra 32,537, hasheous 66**, and **moby, launchbox, tgdb, flashpoint, hltb, gamelist, libretro all 0**, which still proves recognition. **`sgdb` is ignored**, so deriving the list from `SimpleRomSchema`'s `*_id` fields would have shipped a dead option; `screenscraper` and `playmatch` are ignored too. Eleven recognised in all. **Statuses cannot be probed the same way**: an unrecognised status returns zero rather than everything, so `backlogged` looks exactly like a real status nobody has used, and `RomUserStatus` in the schema is the authority for its five

## RB-237. No, and the mismatch runs both ways

Question: (not addressed) whether `filter_values` covers every filter `GET /api/roms` accepts

Measured: **No, and the mismatch runs both ways.** The sidecar reports **eleven** keys: genres, franchises, collections, companies, game_modes, age_ratings, player_counts, regions, languages, tags, platforms. The endpoint accepts **eleven** array filters: the same list minus `game_modes` and `platforms`, plus `statuses` and `metadata_providers`. So `game_modes` is a value list with no filter behind it and must not become a picker, `platforms` is the scope rather than a facet, and the two the endpoint adds have no value list and need RB-236's vocabularies. A screen driven off the sidecar would offer one facet that does nothing and miss two that work

## RB-242. No, and it is silently ignored exactly as 236 found for `metadata_providers`

Question: (not addressed) whether `platform_id` filters `GET /api/roms`

Measured: **No, and it is silently ignored exactly as 236 found for `metadata_providers`.** `platform_id=303` returned `total` **96,060**, the whole library, identical to no filter; the parameter is `platform_ids`, plural, which returned 3,006 for megadrive and 3,454 for snes. Two probes run minutes apart returned byte-identical counts for two different platforms, which is what exposed it. Second instance of the same trap on this endpoint, so treat an unrecognised query parameter here as returning everything rather than erroring

## RB-257. Migration 013 was right, and RB-85 does not reproduce

Question: RB-85's 91.0% md5 against 96.3% sha1 contradicts migration 013's sample (**85, 181, #112**)

Measured: **Migration 013 was right, and RB-85 does not reproduce.** `tools/romm-5.3-probes/r7-hash-coverage.py` walked every platform of the live `5.3.0-alpha.2` library: 94,472 single-file roms with a file on disk. `md5_hash`, `sha1_hash` and `crc_hash` each carry a value on **93,873 (99.4%)** and `''` on **599 (0.6%)**, null on none, with **0 rows holding a sha1 and no md5**. Counting `''` as present reads 100% of both, so neither counting reproduces 91.0 and 96.3, and neither sample recorded its query. The instance moved after 2026-08-10: a 5.1.x server is now 5.3.0-alpha.2, the library grew, and rows were rescanned. Rom 191723, which RB-181 read as `md5_hash: ''` with only a sha1, now carries all three, and its sha1 is `0dd306bc...`, the real file's hash from RB-180. So only the 0.6% lacks a hash to verify, and dropping the sha1 comparison cost nothing here

## RB-354. The sidecars are a fixed cost repeated on every page

`GET /api/roms` has **four** flags that default to `true`, not three as the plan says:
`with_char_index`, `with_filter_values`, `with_rom_id_index`, `with_total`. A fifth,
`with_files`, is opt-in.

| Page size    | Sidecars on | Sidecars off | On      | Off     | Sidecar bytes |
| ------------ | ----------- | ------------ | ------- | ------- | ------------- |
| 10           | 366 ms      | 142 ms       | 879 KB  | 38 KB   | 841 KB        |
| 25           | 481 ms      | 345 ms       | 1137 KB | 295 KB  | 842 KB        |
| 50 (default) | 746 ms      | 677 ms       | 1298 KB | 456 KB  | 842 KB        |
| 100          | 1181 ms     | 1084 ms      | 1816 KB | 975 KB  | 841 KB        |
| 250          | 2616 ms     | 2454 ms      | 2913 KB | 2072 KB | 841 KB        |
| 500          | 4881 ms     | 5012 ms      | 5235 KB | 4394 KB | 841 KB        |
| 1000         | 8638 ms     | 8391 ms      | 8846 KB | 8004 KB | 842 KB        |

**The sidecar payload is a flat ~841 KB on every request regardless of page size.** It does
not scale with the page, it is simply resent. Isolated at `limit=10`, where the page itself
is only 38 KB:

| Flag                 | Cost when on      |
| -------------------- | ----------------- |
| `with_rom_id_index`  | **582 KB**        |
| `with_filter_values` | **280 KB**        |
| `with_char_index`    | 0.3 KB            |
| `with_total`         | 0 KB (an integer) |

At the default page size of 50, **65% of the response body is sidecar**. At the default of
`limit=50` with defaults on, walking the whole 83k library takes 1663 pages and would resend
roughly **1.4 GB** of identical sidecar data.

Server time is barely affected (per-flag deltas at `limit=100` were within noise, plus or
minus 55 ms), so **this is a bandwidth and parsing cost, not a database cost**. That is
good news: the fix is free.

**Conclusions for M2:**

- **Request the sidecars once, then disable all four for every subsequent page.** They are
  index and filter metadata for the whole library, not per-page data.
- **Default page size 250 with sidecars off.** Latency is close to linear in page size
  (roughly 10 ms per rom across the range), so larger pages buy little per-rom throughput
  but cost responsiveness and make resumption coarser. 250 gives a 2.5 s page and 333 pages
  for this library.
- **A full catalog walk of 83k roms takes about 14 minutes** at that setting. Sync must be
  resumable and incremental (`updated_after` exists on the endpoint and should be the normal
  path); a full walk is a first-run or repair operation, not routine.

## RB-355. `GET /api/collections` is not a cheap list

One collection returned **714.8 KB**. The breakdown explains it:

| Field               | Size      | Items |
| ------------------- | --------- | ----- |
| `path_covers_small` | 359,073 B | 4433  |
| `path_covers_large` | 350,207 B | 4433  |
| `rom_ids`           | 35,640 B  | 4455  |
| everything else     | ~130 B    |       |

**99% of the response is two inlined arrays of cover-art paths, one entry per member rom,
duplicated at two sizes.** There is no pagination on `/api/collections`. A user with 20
collections of this size would pull roughly 14 MB to render a list of 20 names.

Note also `rom_ids` has 4455 entries while the cover arrays have 4433, so the arrays are not
positionally aligned with `rom_ids` and must not be zipped together.

`/api/collections/smart` returned 28 items in 67 KB, which is proportionate.
`/api/collections/virtual` returned **HTTP 422** without parameters, so it requires
arguments the other two do not; the client must not treat the three as interchangeable.

## RM-1. The platform folder authority moved into source (`source`)

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

## RM-2. A ROM identity triple lands on the ROM schema (`source`)

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
[save-sync](../../../.claude/skills/save-sync/SKILL.md) and at [`docs/PLAN.md`](https://github.com/Spinnich/rommbat/blob/c7ca9d153/docs/PLAN.md) lines 2602, 3699 and 3957.
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

**Measured, 2026-09-16, by rescanning named rows rather than reading a library-wide fraction.**
The first reading was confounded: extraction runs during a scan and `_should_extract_title_ids`
re-reads only a row carrying no id, so a platform scanned before the feature existed reads 0%
whatever the extractor can do. `should_scan_rom` honours a `roms_ids` list, so the experiment is
a handful of named rows per system rather than a platform of 9,196. **28 of 33 rows answered.**

| System    | Container          | Result | `title_id`           | `save_target`       | Layout          |
| --------- | ------------------ | ------ | -------------------- | ------------------- | --------------- |
| psx       | `.chd`             | 3 of 4 | `SLES-00972`         | `SLES-00972`        | `file-prefix`   |
| ps2       | `.chd`             | 4 of 4 | `SCUS-97472`         | `BASCUS-97472`      | `folder-prefix` |
| psp       | `.cso`             | 4 of 4 | `ULES00151`          | `ULES00151`         | `folder-prefix` |
| ps3       | `.dec.iso`, folder | 4 of 4 | `BLUS30443`          | `BLUS30443`         | `folder-prefix` |
| 3ds       | `.zcci`            | 3 of 3 | `00040000000EC400`   | `00040000/000ec400` | `folder-split`  |
| dreamcast | `.chd`             | 3 of 3 | `MK-5100050`         | `MK-5100050`        | `file-prefix`   |
| xbox      | `.xiso.iso`        | 3 of 3 | `MS-100`             | `4D530064`          | `folder-exact`  |
| xbox360   | `.iso`             | 3 of 3 | `4D5307E6`           | `4D5307E6`          | `folder-exact`  |
| switch    | folder             | 0 of 3 | encrypted, see below |                     |                 |
| psvita    | `.zip`             | 0 of 2 | nothing              |                     |                 |

**The premise this repo carried is now falsified in the client's favour.** The measured claim was
0% of PSP, PS3 and PSX "because no constant offset reaches a `.cso`, a `.chd` or an ISO9660
filesystem". That is still true of a **constant offset**, and upstream does not use one: it
parses the container. So the three systems this repo reads nothing from are three the server
reads almost everything from, and the complement is close to exact rather than merely claimed.

**`save_target` is computed, not copied, which is what settles what these fields are for.** Xbox
is the clearest: `MS-100` becomes `4D530064`, which is `ascii("MS")` followed by `100` as a
16-bit hex number, and `TC-003` becomes `54430003` the same way. PS2 prefixes `BA` to reach the
memory card folder PCSX2 creates, and 3ds splits the 16 hex digits in half and lower-cases the
second. A field carrying identity would need none of that. `title_id` is what was read out of the
binary and `save_target` is where saves land, and the two are different questions.

**Where both routes answer they agree exactly.** This repo's header route reads `head[0x58..0x5C]`,
four ASCII bytes. Upstream stores the same four bytes hex encoded: every one of the 1,601 distinct
GameCube ids on the live library is eight hex digits decoding to a printable `A-Z0-9` code, 1,549
leading `G`, 34 `D` and 18 `P`. `47553459` is `GU4Y`. That is independent agreement on the value,
not merely on the taxonomy, and it is the answer #168 asked for third.

**A serial is not unique per ROM, and that is the point rather than a limitation.** On GameCube,
scanned end to end, 1,793 rows carry an id and 1,601 are distinct: 167 ids are shared by 359 rows.
**Two thirds of that is intrinsic and one third is a property of the library it was measured on.**
The instance's multi-disc releases sit as loose files rather than one folder per game, so RomM
holds a row per disc where a foldered library would hold one row with several files. Folding each
disc set back into one game leaves **101 groups over 222 rows, 12% of the platform**, and those
are revisions and re-releases. Both kinds are correct: Disc 1 and Disc 2 share a memory card, and
a patched revision does not move the player's save, so a key that separated either would be
useless for the job it has. The existing first-wins rule already covers it and already gives this
as its reason, and the hash remains what identifies a ROM.

**Re-taken 2026-09-16 with a committed probe**, `tools/romm-5.3-probes/r5-gamecube-title-ids.py`,
because the sample above recorded no method. The first three numbers reproduce exactly: 1,793 rows,
1,601 distinct ids, all 1,601 decoding to a game code (G 1,549, D 34, P 18), 167 shared over 359.
The fold does not quite: removing `(Disc N)` from each name leaves **104 groups over 232 rows**,
13% of the platform, against 101 over 222. The probe lists every group it keeps, and they are the
same kinds the paragraph above names: revisions, `(USA)` against `(USA, Canada)` and `(Korea)`,
retitled re-releases, and two 2-in-1 discs whose disc tag is followed by a title the fold does not
strip. So the difference is the folding rule and not the library, and the conclusion does not
move. Compare a later reading against 104 and 232, which the script can reproduce.

**Read the 12% rather than the 20% when reasoning about the field**, and read the 20% when
reasoning about what a real install will hand the attributor, because loose multi-disc files are
common and RomMBat does not get to require otherwise.

**Three systems answer nothing, for three different reasons.**

- **switch** needs `prod.keys` to decrypt a header, and upstream leaves that out deliberately.
  Not a gap to work around. The 4% that do answer are the family whose files may carry the id in
  the filename, which is why `_should_extract_title_ids` re-reads the Switch family every scan.
- **ps3-psn**, 24 rows and 0 answering, against 170 of 170 on decrypted discs. PSN `.pkg` content
  is a shape the extractor does not cover yet rather than one it fails at.
- **psvita**, 12 rows of `.zip` and 0 answering, which is the container rather than the platform.
- **One row failed where its neighbours did not**: `Metal Gear Solid (Europe) (Disc 1).chd`, a
  single 402 MB `.chd` in the same platform and category as three `.chd` rows that answered. The
  shape explains nothing, so this one is upstream's to explain, and it is reported.

**What this does not change.** It is still a fourth route and not a replacement. It is capability
gated on `TITLE_ID_EXTRACTION_ENABLED`, it answers only for rows scanned since the feature landed,
and the agreement of routes is still the only evidence a binding has. What it does change is the
shape of the answer, because **`PUT /roms/{id}/identity` makes the route run both ways**: on
GameCube and Wii, where this repo reads 100% and 75.5%, RomMBat is the client that endpoint's own
description is about.

## RM-5. ROM identity is keyed on the path, and the new endpoint is a write-back (`source`)

Read at `5.3.0-alpha.2`, both halves of the release note are wrong as written, and each
pointed at a different conclusion than the source does.

**Identity is not content hashed.** `0126_unique_rom_full_path` moves the unique index off
`(platform_id, fs_name)` and onto `(platform_id, full_path_hash)`, where `full_path_hash` is
`sha256(fs_path + "/" + fs_name)`. The digest is there because that path is 5804 bytes of
utf8mb4 against InnoDB's 3072 byte key limit, not because content decides identity. What it
buys is a custom library structure, where one platform may hold identically named files in
different folders. The direction of travel is the opposite of the note's: a file that moves
between folders inside a platform used to keep its row for free, because the filename was the
key, and now does not.

**What survives a move is a rescue, not a key.** `scan.py` flags every entry whose path is
absent as `missing_from_fs` before it identifies files, then, for a file with no full path
match, hashes it and looks for a missing entry with the same hash, so a moved or renamed ROM
carries over its collections, notes and uploaded assets instead of spawning a duplicate. That
is scan time behaviour and it is gated on hashing being on at all
(`calculate_hashes = not cnfg.SKIP_HASH_CALCULATION`), so an instance with hashing off turns
every move into an orphan next to a duplicate.

**`PUT /roms/{id}/identity` rebinds nothing.** It stores the identity triple `title_id`,
`save_target` and `save_target_layout` that a client extracted from a ROM the server cannot
read, under scope `roms.write`, normalising the triple for the Switch family on the way in.
That makes it the return path for RM-2 rather than a repair tool: the systems where this
repo's header reads succeed and upstream's extractor answers nothing are systems where RomMBat
could supply the id rather than only consume one.

**Measured on 2026-09-16, and it fires.** A lone `.zip` was moved into a new subfolder of its
platform, keeping the filename byte for byte, and the platform was quick scanned.

|                   | Before                                         | After                 |
| ----------------- | ---------------------------------------------- | --------------------- |
| rom id            | 160135                                         | **160135**            |
| `created_at`      | 2026-03-17T00:32:41Z                           | **unchanged**         |
| rom file row id   | 517802                                         | **517802**            |
| `md5_hash`        | `0823b8d4...`                                  | **unchanged**         |
| `fs_name`         | `Snorlax's Lunch Time (Europe) (GameCube).zip` | `moved`               |
| `full_path`       | `roms/pokemini/Snorlax's ... .zip`             | `roms/pokemini/moved` |
| `missing_from_fs` | false                                          | false                 |

The platform still holds 38 rows, no row is flagged missing, and no duplicate was spawned, so
the reassociation carried the row rather than the scan creating a second one. **A cached binding
against a rom id survives a move**, which is the question #177 asked.

**That experiment was impure, and the impurity is the more useful half.** A directory under a
platform folder is RomM's own convention for one game held as several files, so the move did not
read as "same ROM, new path". It read as a new folder-shaped ROM, and the row's `fs_name` became
the folder name while `name` stayed `Lunch Time`. Within one platform there is no subfolder that
is not already a game, so isolating the path needs a rename instead.

**Both controls were run, and the mechanism holds in every direction.** Moving the file back and
rescanning returned **every** field to its original value except `updated_at`, including
`fs_name`, `fs_extension` and `has_multiple_files`, so the round trip is lossless at the row
level and a reorganisation is recoverable. Renaming the archive in place, same folder, then
isolates the path change: rom id, `created_at`, file row id, all three hashes and `name` were
untouched, `fs_name` and `full_path` followed the new filename, the platform stayed at 38 rows and
nothing was flagged missing. So the reassociation fires on a pure path change and the move result
was not an artifact of the folder conversion.

**Why a rename cannot break it, which is a property of a measured rule rather than luck.** The
archive was renamed and the member inside it was not, and RomM's rom hashes describe the member
rather than the container (RB-80: a `.zip` reports the hashes
of the file inside it and nothing about the archive). A container rename is invisible to the
value the reassociation matches on. Untested, and worth knowing before relying on this: whether
renaming the **member** also leaves the match intact.

**What it cost is the filename, not the identity, and the filename is load bearing.** RomMBat
writes `roms/{folder}/{fs_name}` and keys `es_settings.cfg` per-game overrides on the rom
filename, which `EsSettingsFile.PerGameKey` refuses to build without an extension because
`emulatorlauncher` ignores a key built from a stem. The row that comes back from a move like this
one has no extension. Nothing breaks today, because per-game conversion exists only for
`dreamcast`, `gamecube`, `ps2` and `psx` and a multi-disc row is already refused by name. It
stops being hypothetical the moment a library is tidied: **foldering a multi-disc set is exactly
what turns a refusal into a row `PerGameKey` cannot express**, and foldering is the correct way
to store those games, so a correctly stored multi-disc game would convert worse than an
incorrectly stored one. #183, which is a design question about what a folder-shaped ROM lands as
on disk rather than a missing guard.

**It also reproduced a known falsification.** The row is extensionless with `has_multiple_files`
false, so "every extensionless ROM is multi-file" is wrong in a second, independent way. That was
already found in `docs/freegosy-findings.md`; RB-82 still
asserted it and now carries the withdrawal. The code reads the flag and never the extension, so
nothing mis-excluded either time.

## RM-6. Physical games mean a sync set can hold a row with no file (`source`, now `measured`)

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
`physical` boolean, but only `missing` exists at 5.2.0; `physical` arrived with this release. That
was the first reason, and the floor move retired it. The one that holds at `alpha.2` is that a row
the server filters out never reaches the resolver, so the sync summary could not count it as
skipped for having no file, and a game would disappear from a set with nothing said.

## RM-9. Performance measurements move, and must not be edited (`measured`)

`SCAN_WORKERS` and `WEB_SERVER_CONCURRENCY` both default to 4, up from 1. An N+1 in
`GET /api/roms` is fixed. Pooled connections are now recycled before the server drops them.

Every timing in this repo is a measurement of 5.2.0 at concurrency 1, and each one carries the
scope it was taken on: **8 minutes 15 seconds for a platform scope of 9,196 roms** at 250 rows a
page before #88, 2.3 s against 8.5 s for a `platform_ids` scoped page at a library of 88,331,
and the 300 s session timeout. Per the version move checklist those are provenance and **do not
get rewritten to new numbers**, and that includes the scope: a re-measure that walks the whole
88,331 rom library has measured a different thing and has nothing to compare against. They get
re-measured on the same scope, and the new numbers get their own attribution.

**Re-measured 2026-09-14 on `5.3.0-alpha.2`, and the scoped penalty is gone.** Same probes, same
flags, same page sizes, same platform. The `limit=100` page rows and the `with_total` pairs below
are `tools/argosy-probes/a1-a2-rom-id-index.py`, run unchanged from the commit that took the 5.2.0
readings, so the two columns come from one script. The two walk rows are
`tools/romm-5.3-probes/r1-page-walk.py`. Two things differ from the 5.2.0 pass besides the server,
and both belong in any reading of the table: the library is 95,993 roms against 88,331, and the
metadata mix has moved with it.

| Reading, on the largest platform (`psx`, 9,196 roms) | 5.2.0, 88,331 roms | 5.3.0-alpha.2, 95,993 roms |
| ---------------------------------------------------- | ------------------ | -------------------------- |
| Scoped page, `limit=100`, rom id index on            | 2,288 to 2,494 ms  | 285 to 353 ms              |
| Scoped page, `limit=100`, rom id index off           | 8,305 to 8,665 ms  | 274 to 328 ms              |
| What turning the index off costs, scoped             | **3.4 to 3.7x**    | **nothing**                |
| Unscoped page, `limit=100`, index on against off     | 1.13 to 1.18x      | 1.15 to 1.20x              |
| Full scoped walk, 250 a page, index off              | **8 m 15 s**       | **22.5 s**                 |
| Full scoped walk, 250 a page, index on               | not walked         | 24.1 s                     |
| `GET /api/roms/identifiers`                          | 504 after 300.0 s  | 200 after 176.7 s          |

**What the N+1 fix bought is the scoped case specifically.** Unscoped, the index is worth the
same 1.15x it was worth at 5.2.0, and costs the same 604 KiB a page. Scoped, the 3.4 to 3.7x that
`romm-api`'s "off only when the request is unscoped" rule was written from does not reproduce at
all: index off is inside the noise, and marginally ahead. The rule survives on bandwidth, which
was never its argument, and its stated reason is a 5.2.0 reason. With the floor at `alpha.2` that
reason holds on no supported server. The behaviour was kept at the time and the decision went to
#188, because changing it wanted a bandwidth reading this section did not take

**Decided 2026-09-16 on that reading: the index is off under every scope.**
`tools/romm-5.3-probes/r2-scoped-index-bandwidth.py`, at the client's 250 a page, index on and
off interleaved within each of five repeats, on the widest scopes this account can page:

| Scope, 250 a page                     | Ids    | Index on   | Index off  | Off saves a page |
| ------------------------------------- | ------ | ---------- | ---------- | ---------------- |
| `platform_ids` psx                    | 9,196  | 548-604 ms | 557-575 ms | 63 KiB           |
| `virtual_collection_id` Single-player | 16,441 | 543-953 ms | 487-936 ms | 112 KiB          |

`total` stayed non-null in every off row because `with_total=true` is sent, and under a scope its
separate computation was inside the noise of the page. The account has no regular collection, and
its largest smart collection advertises 594 roms and pages to a total of 0, so neither kind has a
reading; a virtual collection spanning platforms is the widest scope a set can name, and it was
measured. What the index costs is its id count, so a wider scope only widens the saving.

**The 594 against 0 is the server working as written, not a defect** (#193).
`tools/romm-5.3-probes/r6-smart-collections.py` read all 29 smart collections this account can
list: every one is public, owned by another account, and filters on `favorite` and `platform_ids`,
and every one pages back 0 while advertising between 6 and 594. `refresh_smart_collection` stores
`rom_count` and `rom_ids` computed as the owner and says so in its docstring, and
`smart_collection_id` applies the same criteria as the caller, who has marked none of those
favourites. The picker no longer shows the stored count, and the rule is in `romm-api`.

**A2 is unchanged in shape.** `with_total` is free with the index on (262 ms against 264 ms) and
costs with it off (320 ms against 187 ms), which is `resolve_total()` returning the length of an
index that is already being built. With the index off under every scope, `CatalogQuery` pays for
that count on every walk: inside the noise of a scoped page (R2), and 133 ms unscoped.

**`/api/roms/identifiers` moved from impossible to merely unusable.** It completes now, at
176.7 s for 95,993 ids, where 5.2.0 answered 504 on the 300 s wall at a smaller library. The
decision to refuse it stands: three minutes is not a page a sync can wait on, and the endpoint
still takes no parameters, so it can be neither scoped nor paged. The rejection is now a
judgement about latency rather than a report that it does not work. A one-off probe re-took it on
2026-09-16 at **200 after 181.4 s**, 95,993 ids in a 656 KiB body, on the same library, so the two
agree.

**And it is a judgement about the server's memory, which is why no probe for it is committed.**
The route eager-loads every ROM's relationships to return ids and keeps running after the client
disconnects. The live suite's budgeted call gave up at 10 s, and about sixty runs of it in one
session took the container from 2 GB to **20.9 GiB**, the web workers holding their peak until
recycled. Reported as rommapp/romm#4577. The live test, the client method and both probes that
called it are removed, so the two readings above are not re-taken at the next adoption until
upstream fixes it.

**The payload share moved, and the library moved with it, so this one attributes to neither.**
`ss_metadata` is 61.2% of a 100 row page against 46.4% at 5.2.0, and `igdb_metadata`, 20.5%
then, is out of the top fifteen now. The A12 conclusion is unchanged and was never a number:
a payload share is a property of a library's metadata mix, not of a client or a release.
