---
summary: Paging the ROM list, its sidecars, identifiers, hashes, filters and platform identity.
read-when: Before reading the catalog, filtering it, or trusting a field on a ROM row.
---

# RomM: catalog

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-354. `GET /api/roms` resends about 1 MB of sidecars on every page unless told not to

Verified: RomM 5.3.1, 2026-09-28. How: timed and sized pages of 10, 50 and 250 with each flag on alone, best of three.
Four flags default to true: `with_rom_id_index`, `with_filter_values`, `with_char_index` and
`with_total`; `with_files` is opt-in. The sidecars are a flat 1,050,782 bytes at every page
size, 672 KB of it the rom id index and 379 KB the filter values, against 77 KB for a page of
ten. What they cost is bandwidth and parsing, not server time. RomMBat turns all but
`with_total` off on every page of a walk, reads the filter values in one request of its own, and
pages 250 rows at a time; a walk of every platform of a 95,989-ROM library that way took 3 min
41 s.

## RB-355. `GET /api/collections` inlines two cover paths and an id per member ROM, unpaged

Verified: RomM 5.1.1, 2026-08-09, and 5.3.1 source, 2026-09-28. How: sized one live collection's response; on 5.3.1 read `BaseCollectionSchema` and `get_collections`.
One collection of 4,455 ROMs answered 714.8 KB, 99% of it `path_covers_small` and
`path_covers_large`. The route takes only `updated_after`, so there is no page to ask for. The
cover arrays held 4,433 entries against 4,455 `rom_ids`, so they are not aligned with the ids.
`/api/collections/virtual` answers 422 without a `type`. RomMBat lists collections only to name
them and resolves membership by paging `/api/roms` with the collection's id.

## RB-71. The pinned schema declares every size as a bare `integer`, which generates an `int32`

Verified: RomM 5.3.1 schema, 2026-09-28. How: read `fs_size_bytes` and `file_size_bytes` in `romm-5.3.1.json` and the generated DTOs; counted platforms over 2 GiB live.
`SimpleRomSchema`, `DetailedRomSchema` and `PlatformSchema` carry `fs_size_bytes`, and
`RomFileSchema` `file_size_bytes`, with no format, and the generated types read all of them as
`int`. 52 of 128 platforms on a live library exceed 2 GiB, so `GET /api/platforms` fails to
deserialize through them. RomMBat reads rows and platforms through slim hand-written types
with a `long`.

## RB-72. A platform's `slug` is not unique; `fs_slug` and `id` are

Verified: RomM 5.1.1, 2026-08-10, and 5.3.1, 2026-09-28. How: counted distinct values over `GET /api/platforms`.
A library of 128 platforms carries 72 distinct slugs. Every `-unofficial` folder shares its
system's slug, and several folders share one (`fbneo`, `atomiswave` and `triforce` are all
`arcade`). RomMBat keys platforms by `fs_slug` and `id`, because keying by slug drops every
platform after the first that shares one.

## RB-77. `order_by=id` pages in ascending id order

Verified: RomM 5.1.1, 2026-08-10, and 5.3.1, 2026-09-28. How: read two consecutive pages live and compared their ids.
Page two starts after page one, with no overlap. A ROM added mid-walk takes a higher id and
lands past the cursor, which is what lets `RomPager` page by offset through a changing library.

## RB-82. Every multi-file ROM has an empty `fs_extension`; the converse does not hold

Verified: RomM 5.3.1, 2026-09-28. How: walked every platform and crossed `fs_extension` with `has_multiple_files`.
Of 95,989 rows, 1,522 are multi-file and every one has an empty extension; so do 362 rows
with `has_multiple_files` false, and no row has an extension and several files.
A ROM stored as a folder has no extension, whether the folder holds several files or one, so an
empty extension says nothing about the shape, and RomMBat reads `has_multiple_files` for it.

## RB-81. `GET /api/roms/identifiers` returns every visible ROM id and takes no parameters

Verified: RomM 5.1.1, 2026-08-10, and 5.3.1, 2026-09-28. How: timed five uncancelled calls live; on 5.3.1 read `get_rom_identifiers` and `get_rom_ids`.
It answers 95,989 ids, 656 KiB, in 0.55 to 0.92 s, because `get_rom_ids` projects the id
column. With no parameters it cannot be scoped to a set or paged. RomMBat reconciles deletions
by re-resolving each set, whose walk already yields that set's ids, and does not call it.

## RB-84. `GET /api/roms/by-hash` finds one ROM by any of four hashes

Verified: RomM 5.1.1, 2026-08-10, and 5.3.1, 2026-09-28. How: looked one ROM up by md5, sha1 and crc live, and read `get_rom_by_hash`.
It takes `md5_hash`, `sha1_hash`, `crc_hash` or `ra_hash`, answers 400 with none, and returns a
`DetailedRomSchema` of about 9 KB. On 5.3.1 a hit takes 0.2 to 0.8 s and a miss is a 404 after
1.4 to 1.8 s. `FindRomByHashAsync` uses it to attribute a handful of unknown local files, never
as a sweep of the library.

## RB-87. `GET /api/roms/{id}/simple` answers in under half a second, hit or miss

Verified: RomM 5.3.1, 2026-09-28. How: timed three hits and three misses live.
A hit took 0.16 to 0.46 s and a 404 0.09 to 0.42 s. That is cheap for a handful of ROMs and
still a request per ROM, so RomMBat reads rows during the walk and fetches by id only for a
picked set roamed to another device, where no walk can name the ids (RB-94).

## RB-93. A paged row already carries the metadata; the detail route adds only user arrays

Verified: RomM 5.1.1, 2026-08-11, and 5.3.1, 2026-09-28. How: compared a live `DetailedRomSchema`'s keys with a paged row's.
`SimpleRomSchema` has `metadatum`, `summary`, every media path, `regions` and `languages`.
`DetailedRomSchema` adds eight fields, all per-user arrays: `user_saves`, `user_states`,
`user_screenshots`, `user_collections` and the four `all_user_*` lists. RomMBat reads metadata
during the walk and never calls the detail route for it.

## RB-94. `GET /api/roms` cannot be asked for a list of ROM ids

Verified: RomM 5.1.1, 2026-08-11, and 5.3.1 schema, 2026-09-28. How: read the route's 58 parameters in `romm-5.3.1.json`.
The scopes are `platform_ids`, `collection_id`, `virtual_collection_id` and
`smart_collection_id`, and the rest are filters. So the metadata for exactly what is on disk is
not a query: it comes from the walk, or from one request per ROM.

## RB-181. A missing hash is an empty string, never null

Verified: RomM 5.3.1, 2026-09-28. How: `tools/romm-5.3-probes/r7-hash-coverage.py` classified every hash on every single-file row as null, `''` or a value.
A row with no hash sends `md5_hash`, `sha1_hash` and `crc_hash` as `''`. `RomRow` and
`FirmwareRow` read a blank as absent, `ContentPlanner` tests the md5 for blank rather than for
null, and `StubRomMServer` answers `''` so the whole suite exercises the shape.

## RB-257. The three hashes are set together or blank together

Verified: RomM 5.3.0-alpha.2, 2026-09-16, and 5.3.1, 2026-09-28. How: `tools/romm-5.3-probes/r7-hash-coverage.py` over every platform.
Of 94,467 single-file ROMs with a file on disk, all three hashes carry a value on 93,868 (99.4%)
and all three are `''` on 599 (0.6%). No row carries a sha1 without an md5. So RomMBat verifies
a download by md5 alone, and only that 0.6% falls back to size.

## RB-236. An unrecognised `metadata_providers` value is ignored, and returns the whole library

Verified: RomM 5.2.0, 2026-08-31, and 5.3.1, 2026-09-28. How: sent each value alone and compared `total` with the unfiltered one; on 5.3.1 read `METADATA_SOURCE_FACET_COLUMNS`.
The filter maps a value to a provider's id column and drops any value it has no column for, so
`zzz-not-a-provider`, `sgdb`, `screenscraper` and `playmatch` each return all 95,989 ROMs. The
15 it knows are `igdb`, `ss`, `moby`, `launchbox`, `ra`, `hasheous`, `tgdb`, `flashpoint`,
`hltb`, `demozoo`, `pouet`, `csdb`, `steam`, `gamelist` and `libretro`. An unrecognised
`statuses` value returns zero rows instead, so the authority for statuses is `RomUserStatus`.
RomMBat's picker offers only values the server knows.

## RB-237. `filter_values` is not the list of filters `GET /api/roms` accepts

Verified: RomM 5.2.0, 2026-08-31, and 5.3.1, 2026-09-28. How: read the live sidecar's keys; on 5.3.1 read `RomFiltersDict` and `RomFilterParams`.
The sidecar reports 13 keys, and the route takes 13 array filters besides `platform_ids`. Eleven
match. `game_modes` has no filter behind it, and `platforms` is a scope, sent as ids where every
other key is names. `statuses` and `metadata_providers` are filters with no values in the
sidecar (RB-236). RomMBat builds its filter screen from the route's parameters and uses the
sidecar only for values.

## RB-242. `GET /api/roms` ignores an unknown parameter, so `platform_id` returns everything

Verified: RomM 5.2.0, 2026-09-01, and 5.3.1, 2026-09-28. How: compared `total` for `platform_id` and `platform_ids` on one platform live.
`platform_id=<nes>` returned the whole library, 95,989, and `platform_ids=<nes>` the platform's
7,568. The route treats an unrecognised query parameter as absent rather than as an error, so a
misspelt scope reads as a scope that matched everything. `CatalogQuery` sends `platform_ids`.

## RM-1. `utils/platform_aliases.py` maps RomM's folder names to its slugs

Verified: RomM 5.3.1 source, 2026-09-28. How: diffed the vendored `reference/romm-platform_aliases.py` against the tag and ran `reference/verify.py`.
`resolve_platform_slug` takes a config binding first, then the folder name itself when it is
already a slug, then `PLATFORM_FS_ALIASES`, then the folder name unchanged. `resolve_fs_slug`
goes back from slug to folder and returns `None` when several folders collapse onto one, as
`arcade` does. The table has 138 alias keys: 94 are RetroBat system folders and 44 are ES-DE or
Batocera spellings no RetroBat install has. RomMBat seeds its mapping from the table and the slug
enum, filtered by the live `es_systems.cfg` (`platform-mapping`).

## RM-10. A RomM instance exits at start on a retired folder key or an undeclared `{platform}/roms` library

Verified: RomM 5.3.1 source, 2026-09-29. How: read `_check_retired_filesystem_keys` and `check_library_layout` in `config/config_manager.py`.
`filesystem.roms_folder` or `filesystem.firmware_folder` in `config.yml` exits with the equivalent
`filesystem.structure` template printed. A library laid out as `{platform}/roms` with no
`structure.default` exits the same way, because only a declared template selects it. A
`roms/{platform}` library needs nothing:

```yaml
filesystem:
  structure:
    default: "roms/{platform}/{game}"
    firmware: "bios/{platform}"
```

The server's layout is inert for RomMBat, which maps to RetroBat's folders, and matters to anyone
standing up a disposable instance from `DEVELOPER_SETUP.md`.

## RM-2. Every ROM row carries `title_id`, `save_target` and `save_target_layout`

Verified: RomM 5.3.0-alpha.2, 2026-09-16, and 5.3.1, 2026-09-29. How: rescanned named rows per system and read the fields back; on 5.3.1 read `RomSchema` and `SaveTargetLayout`, and paged every GameCube row.
The scan reads the triple out of the game binary when the heartbeat's
`TITLE_ID_EXTRACTION_ENABLED` is on, and only for a row scanned since the feature landed.
`save_target_layout` is one of `folder-exact`, `folder-prefix`, `file-exact`, `file-prefix` and
`folder-split`. `save_target` is computed from the id to name where saves land: xbox `MS-100`
becomes `4D530064`, and ps2 prefixes `BA`. 28 of 33 rows answered:

| System    | Container          | Answered | `title_id`         | `save_target`       | Layout          |
| --------- | ------------------ | -------- | ------------------ | ------------------- | --------------- |
| psx       | `.chd`             | 3 of 4   | `SLES-00972`       | `SLES-00972`        | `file-prefix`   |
| ps2       | `.chd`             | 4 of 4   | `SCUS-97472`       | `BASCUS-97472`      | `folder-prefix` |
| psp       | `.cso`             | 4 of 4   | `ULES00151`        | `ULES00151`         | `folder-prefix` |
| ps3       | `.dec.iso`, folder | 4 of 4   | `BLUS30443`        | `BLUS30443`         | `folder-prefix` |
| 3ds       | `.zcci`            | 3 of 3   | `00040000000EC400` | `00040000/000ec400` | `folder-split`  |
| dreamcast | `.chd`             | 3 of 3   | `MK-5100050`       | `MK-5100050`        | `file-prefix`   |
| xbox      | `.xiso.iso`        | 3 of 3   | `MS-100`           | `4D530064`          | `folder-exact`  |
| xbox360   | `.iso`             | 3 of 3   | `4D5307E6`         | `4D5307E6`          | `folder-exact`  |
| switch    | folder             | 0 of 3   |                    |                     |                 |
| psvita    | `.zip`             | 0 of 2   |                    |                     |                 |

Switch needs `prod.keys`, which RomM leaves out deliberately, PSN `.pkg` content is not covered,
and a Vita `.zip` answers nothing; `Metal Gear Solid (Europe) (Disc 1).chd` failed where three
`.chd` neighbours did not. On GameCube `title_id` is the header's four bytes in hex (`47414645`)
and `save_target` the same four in ASCII (`GAFE`), as a Dolphin `.gci` name carries them. Only a
rescan writes the ASCII form: on 2026-09-29, 1,790 of 1,794 rows carried it and 4 still held hex
in both fields. The 1,793 rows the probe read carry 1,601 distinct ids: `tools/romm-5.3-probes/r5-gamecube-title-ids.py` finds 104 groups
over 232 rows after folding disc sets, all revisions and re-releases. A serial is not unique per
ROM, and `save-sync` uses it as one attribution route among several, never as identity.

## RM-5. A ROM's row is keyed on its path, and a scan carries a moved file's row over by hash

Verified: RomM 5.3.0-alpha.2, 2026-09-16, and 5.3.1 source, 2026-09-28. How: moved, restored and renamed one `.zip` on a live platform and quick-scanned; on 5.3.1 read `models/rom.py` and `endpoints/sockets/scan.py`.
The unique index is `(platform_id, full_path_hash)`, a sha256 of the path. A scan flags every
absent path `missing_from_fs` first, then matches a new file's hash to a missing row and keeps
that row, its id, `created_at` and file row, with `fs_name` and `full_path` following the file.
With `SKIP_HASH_CALCULATION` on there is no match, and a move leaves an orphan beside a new row.
A file moved into a new subfolder becomes a folder-shaped ROM with no extension. The
reassociation matches a zip's member hashes (RB-80), so renaming the archive cannot break it.
`PUT /api/roms/{id}/identity` stores a client's RM-2 triple under `roms.write` and rebinds
nothing. RomMBat keys its bindings on the ROM id, which a move keeps.

## RM-6. A catalog row can have no file to download

Verified: RomM 5.3.0-alpha.2, 2026-09-16, and 5.3.1 source, 2026-09-28. How: read a live page's fields; on 5.3.1 read `RomSchema` and `Rom.has_file_on_disk`.
Every row carries `is_physical`, `missing_from_fs` and `has_file_on_disk`, the last computed as
`not is_physical and not missing_from_fs`. `POST /api/roms/physical` creates a row with no file,
and a file deleted from the server's disk leaves one. `RomRow.HasFileOnDisk` derives the answer
when the field is absent, and `SetResolver` counts such a row as `ExcludedNoFileOnDisk` ahead of
the shape and extension checks. RomMBat drops these rows itself rather than sending
`missing=false` and `physical=false`, because a row the server drops never reaches the sync
summary to be reported.

## RM-9. Under a scope, turning the rom id index off costs nothing

Verified: RomM 5.3.1, 2026-09-28. How: timed `limit=100` pages five times on `psx` (9,194 ROMs) and unscoped, walked `psx` at 250 a page, and ran `tools/romm-5.3-probes/r2-scoped-index-bandwidth.py`.

| Reading                    | Index on   | Index off  |
| -------------------------- | ---------- | ---------- |
| Scoped page, `limit=100`   | 267-529 ms | 301-339 ms |
| Unscoped page, `limit=100` | 238-269 ms | 326-341 ms |
| `psx` walk, 250 a page     | 24.1 s     | 22.5 s     |

Off saves a 250-row page 63 KiB on `psx` and 114 KiB on a 16,687-ROM virtual collection, and
657 KiB of a 100-row unscoped page. `CatalogQuery` sends `with_rom_id_index=false` under every
scope. With the index off, `with_total` is a separate count: inside the noise of a scoped page
and about 140 ms of an unscoped one. `total` comes back null without it, so it stays on.

## RM-16. A smart collection pages back as the caller, but advertises its owner's count

Verified: RomM 5.3.0-alpha.2, 2026-09-16, and 5.3.1, 2026-09-28. How: `tools/romm-5.3-probes/r6-smart-collections.py` listed every smart collection and paged each.
All 29 this account lists are public, owned by another account and filter on `favorite`. Each
advertises between 6 and 594 ROMs and pages back 0. `refresh_smart_collection` stores
`rom_count` and `rom_ids` as its owner computed them, and `smart_collection_id` applies the
criteria as the caller, who has favourited none of them. RomMBat's picker shows no count for a
smart collection, and a resolve's `total` is its size.

## RM-14. A rescan can change a platform's `fs_slug` case, and the platform keeps its id

Verified: RomM 5.3.1, 2026-09-29. How: read `get_platform_by_fs_slug` and `scan_platform`; listed every platform's `fs_slug` live.
`get_platform_by_fs_slug` matches case-insensitively, and a scan writes the folder's on-disk
spelling. `PlatformMapStore` keys `platform_map` on the exact `fs_slug`, so `Record` rekeys a row
whose id matches and whose key differs only in case, rather than leaving a second platform behind.
Sync sets store the platform id. All 128 platforms on the live library have a lowercase `fs_slug`,
so the rekey has not been driven.

## RM-25. `GET /api/platforms` inlines every firmware record, and one md5 sits on several platforms

Verified: RomM 5.1.1-beta.1, 2026-08-10, and 5.3.1 source, 2026-09-29. How: read `/api/platforms` against `/api/firmware?platform_id=` for the largest set, and counted md5s across platform rows; on 5.3.1 read `PlatformSchema`.
Each platform carries its whole `firmware[]`, `md5_hash` on every record: 656 records on 79 of
123 platforms in one 424 KB response, `firmware_count` equal to the array's length on every
platform and the same 75 ids as the dedicated call for the largest. 504 of the 656 share an md5
with a record on another platform, a user's `-unofficial` twin among them. So a BIOS gap report
is one request under `platforms.read`, and a join dedupes on md5 and takes any copy.

## RM-26. Firmware `is_verified` is RomM's own filename check, false on files RetroBat requires

Verified: RomM 5.1.1-beta.1, 2026-08-11, and 5.3.1 source, 2026-09-29. How: joined RetroBat's required md5s against a live library's firmware, then filtered on the flag and on file name; on 5.3.1 read `Firmware.verify_file_hashes`.
The flag is true only when RomM's own known-BIOS list has an entry for `<platform>:<file name>`
whose size and one hash match. `psxonpsp660.bin`, which RetroBat's `psx` requires, is false on
every copy. Of the 49 required files one library held, filtering on the flag lost 6 and joining
on `file_name` in place of `md5_hash` lost 2. RomMBat joins on md5 and reads neither.

## RM-27. A firmware row marked `missing_from_fs` has no content to serve

Verified: RomM 5.3.1 source, 2026-09-29. How: read `_resolve_firmware_content`.
The scan keeps a row whose file has gone and marks it; 142 of 656 firmware records carried the
flag on the library measured. Its content route answers 404, `Firmware file '<name>' is
missing from filesystem`. RomMBat skips such a record before offering it, or a sync promises a
file and fails mid-pass.
