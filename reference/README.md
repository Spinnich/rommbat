# Reference data

Upstream files RomMBat's design depends on, vendored so the numbers below, which the docs and
skills cite, can be re-derived offline and so drift is visible in a diff.

**These are upstream artifacts. Never hand-edit them.** Refresh with `refresh.sh` and
review the diff, because a change here can invalidate a design decision.

`refresh.sh` also checks `data/retrobat/bios.json` and `data/retrobat/platforms.json`
against the generators that derive them from these files, and exits non-zero naming the
generator to run. It never regenerates them itself: rewriting a committed generated file
mid-refresh would hide the change the script exists to surface.

**The files are fetched at the floor's release tags**, read from `RomMServerVersion` and
`RetroBatVersion`, so they describe the builds RomMBat supports and not upstream's default branch.
`--ref romm=<tag>` or `--ref retrobat=<tag>` takes another tag, and `--out <dir>` writes and checks
there instead of here, which is how a scout pass reads a prerelease (the `version-adoption` skill).
A RetroBat release downloads emulatorlauncher from its rolling `continuous` build, so the two
emulatorlauncher files come from its last commit at or before the RetroBat release was published.

**retrobios is pinned to a commit**, because it versions its firmware packs and not its
metadata. The commit is `_ref` in `retrobios/database-subset.json`; a plain refresh re-pulls it,
and `--ref retrobios=<sha>` moves it. Its metadata is MIT. Its firmware files are licensed to no
one, and `tools/vendor-retrobios.py` never fetches one.

**Each upstream's license is fetched at the same ref as its files.** RomM is AGPL-3.0, vendored as
`romm-LICENSE`. RetroBat's `license.txt`, vendored as `retrobat-license.txt`, puts the RetroBat
Team's code under LGPL-3.0 and the project as a work under CC BY-NC-SA 4.0. emulatorlauncher has
no license file at the vendored commit, so nothing stands beside `es_savestates.cfg` and
`batocera-systems.json`.

| File                              | Source                                                                                  | What it settles                                                                                                                                                                 |
| --------------------------------- | --------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `systems_names.lst`               | `RetroBat-Official/retrobat` `system/configgen/systems_names.lst`                       | The authoritative list of RetroBat system folder names (241)                                                                                                                    |
| `es_systems.cfg`                  | `RetroBat-Official/retrobat` `system/templates/emulationstation/es_systems.cfg`         | Per-system `<extension>`, plus `<manufacturer>`/`<hardware>`/`<release>` used to derive rollout order. **Read the live copy at runtime**; this is the shipped template          |
| `retrobat-license.txt`            | `RetroBat-Official/retrobat` `license.txt`                                              | The notice the two files above are copied under: LGPL-3.0 for the RetroBat Team's code, CC BY-NC-SA 4.0 for the project as a work                                               |
| `es_savestates.cfg`               | `RetroBat-Official/emulatorlauncher` `.emulationstation/es_savestates.cfg`              | Per-emulator save-state schema: directory, file, image, autosave templates, slot bounds                                                                                         |
| `batocera-systems.json`           | `RetroBat-Official/emulatorlauncher` `batocera-systems/Resources/batocera-systems.json` | Required BIOS manifest: 101 systems, 360 entries of `{md5, file}` with destination paths                                                                                        |
| `romm-platform_slugs.py`          | `rommapp/romm` `backend/utils/platform_slugs.py`                                        | `UniversalPlatformSlug`, RomM's whole platform vocabulary (459). `romm-slugs.txt` beside it is the values alone, derived by `refresh.sh`                                        |
| `romm-platform_aliases.py`        | `rommapp/romm` `backend/utils/platform_aliases.py`                                      | `PLATFORM_FS_ALIASES` and `resolve_platform_slug`: how RomM turns a folder name into a slug. Seed for the platform map. A seed, **not** an answer                               |
| `romm-known_bios_files.json`      | `rommapp/romm` `backend/models/fixtures/known_bios_files.json`                          | What RomM's `is_verified` flag is computed from                                                                                                                                 |
| `romm-gamelist_exporter.py`       | `rommapp/romm` `backend/utils/gamelist_exporter.py`                                     | The gamelist field reference RomMBat's writer follows, and the source of two unit conversions RomMBat would otherwise have to guess at                                          |
| `romm-datetime.py`                | `rommapp/romm` `backend/utils/datetime.py`                                              | The millisecond-to-UTC conversion the gamelist exporter calls for `releasedate`                                                                                                 |
| `romm-LICENSE`                    | `rommapp/romm` `LICENSE`                                                                | The AGPL-3.0 the `romm-*` files above are copied under                                                                                                                          |
| `retrobios/emulators/*.yml`       | `Abdess/retrobios` `emulators/`, at the commit in `database-subset.json`'s `_ref`       | Which files each emulator's code reads, cited file:line, with any hash it checks. Only the 221 profiles a RetroBat row resolves to (`tools/firmware_sources.py`)                |
| `retrobios/platforms/bizhawk.yml` | `Abdess/retrobios` `platforms/bizhawk.yml`, same commit                                 | BizHawk's `FirmwareDatabase.cs` as retrobios scrapes it, by sha1. BizHawk has no emulator profile                                                                               |
| `retrobios/LICENSE`               | `Abdess/retrobios` `LICENSE`, same commit                                               | The MIT notice the files above are copied under. It excludes retrobios's firmware files, which nothing here fetches                                                             |
| `retrobios/database-subset.json`  | `Abdess/retrobios` `database.json`, same commit, **filtered**                           | md5, sha1, size and dump-catalog provenance for the 688 of 19,005 rows a vendored source or RetroBat's list reaches. Rows are copied unchanged; only the selection is RomMBat's |

## Derived facts

Run `python3 verify.py` to reproduce all of these. If any number moves, every doc and skill
that cites it needs revisiting.

**Platform mapping is many-to-many and incomplete**

|                                              |                   |
| -------------------------------------------- | ----------------- |
| RetroBat systems                             | 241               |
| RomM known platform slugs                    | 459               |
| Folder aliases upstream publishes            | 138               |
| Of those, RetroBat system folders            | 93                |
| Of those, naming folders RetroBat lacks      | 45                |
| RetroBat folders resolving to a RomM slug    | 166               |
| Of those, by identity (the folder is a slug) | 73                |
| Of those, via the alias table                | 93                |
| **RetroBat systems with no mapping**         | **75 (31%)**      |
| Of those, resolved by normalization alone    | 1                 |
| Distinct RomM slugs reached                  | 148               |
| RomM slugs mapping to several folders        | 10 (`arcade` → 7) |

**The seed is upstream's resolver, not a table of pairs.** `resolve_platform_slug` in
`backend/utils/platform_aliases.py` tries a config binding, then identity when the folder name is
itself a slug, then `PLATFORM_FS_ALIASES`. So `nes` maps to `nes` without appearing anywhere.

`tools/build-platform-map.py` walks **RetroBat's** system list and asks upstream what each folder
resolves to, rather than importing upstream's 138 keys and correcting them. Core principle 3 is
why: the alias table is a Batocera / RetroBat / ES-DE union and 45 of its keys name folders no
RetroBat install has, so walking from RetroBat's side never sees them.

Identity catches almost everything, so normalization rescues one folder, `actionmax` against
`action-max`. RetroBat's `daphne` has no RomM equivalent. RetroBat's `odyssey2` is the Odyssey², RomM's
`odyssey-2`, and not Magnavox Odyssey's `odyssey`. `atari8bit` is upstream's
suggested binding for `atari800`, recorded and not applied: layer 2 covers it whenever the RomM
folder is itself named `atari800`. The `platform-mapping` skill has the detail.

**Firmware knowledge barely overlaps**

|                                    |     |
| ---------------------------------- | --- |
| Distinct md5s RetroBat requires    | 156 |
| Distinct md5s RomM knows           | 353 |
| Overlap                            | 63  |
| RetroBat-required, unknown to RomM | 93  |

The operative consequence: **join firmware on md5, never on a name alone.** Filenames differ
between the two projects, and RomM's `is_verified` misses 60% of what RetroBat requires.

**retrobios knows which emulator reads a file far more often than it knows the file's hash**

`verify.py` sorts RetroBat's 186 hashless entries by what the vendored sources state about them.
A name alone identifies nothing: retrobios's own by-name index files the Store Display Unit BIOS
under `bios_U.sms`. So a single file counts only where a source states a hash for it, or where its
name is exactly one dump-catalog entry's name, or where RomM's list names one hash for it.

|                                                 |     |
| ----------------------------------------------- | --- |
| RetroBat rows (system, emulator, core)          | 709 |
| ...described by a retrobios profile             | 562 |
| ...described by BizHawk's firmware list         | 55  |
| ...marked as having no retrobios source         | 92  |
| Hashless entries that are MAME software lists   | 63  |
| Hashless BIOS zips                              | 64  |
| ...whose contents a row's profile lists         | 24  |
| Hashless single files under `bios/`             | 53  |
| ...a profile or BizHawk states a hash for       | 7   |
| ...named exactly as one dump-catalog entry      | 3   |
| ...RomM's `known_bios_files` names one hash for | 12  |
| ...with no identity anywhere                    | 31  |

The software lists are MAME's own hash metadata, which RetroBat ships, not firmware. A zip's md5
depends on how it was packed, so a zip is identified by the crc32 and size of the files inside
it, which MAME's source states and retrobios records.

A profile that covers several systems contributes to a folder only the files tagged with that
folder's own retrobios system ids, and its untagged files only where the folder's map names
`untagged:<profile>`, through `data/retrobat/firmware-rows.json`. BizHawk keeps its own ids.

**The manifest RomMBat ships is the union, as firmware slots**

`verify.py` runs `tools/build-bios-manifest.py` and counts what it emits.

|                                                   |       |
| ------------------------------------------------- | ----- |
| Folders with a firmware slot                      | 127   |
| Firmware slots                                    | 943   |
| ...RetroBat's list names                          | 296   |
| ...of those, with RetroBat's md5                  | 179   |
| ...of those, hashless and identified by a source  | 46    |
| ...with no identity anywhere                      | 71    |
| ...zips identified by their members               | 331   |
| ...where several accepted files compete           | 14    |
| Distinct accepted md5s                            | 352   |
| Distinct destination paths                        | 615   |
| Read by some emulator, reported and never fetched | 2,287 |
| ...of those, MAME software lists                  | 63    |

296 is RetroBat's 360 entries less the 6 outside `bios/` and the 63 software lists, plus the 5
`gb` takes from its siblings. The 46 are the 24 zips and 22 single files above. A path several
folders name, such as `ATARIOSB.ROM` for `atari5200` and `atari800`, is one file on disk, so each
folder's slot on it accepts what any of them accepts; that union is most of the 14.

**The gamelist exporter settles two units and gets a third field wrong**

`verify.py` asserts behaviors rather than counts here, because the writer depends on them.
Confirmed in upstream's own code: `first_release_date` is divided by 1000, so it is
**milliseconds**, and `average_rating` is divided by 100, so it is on a **0-100** scale, with
a comment saying as much. Both match what RomMBat measured live.

**RomMBat deliberately diverges in three places**, and the checks exist so each divergence
stays visible rather than becoming an accidental difference:

- `region` and `lang` are `regions[0]` and `languages[0]` verbatim, so upstream writes `USA`
  and `English` where EmulationStation's own vocabulary is `us` and `en`. RomMBat maps them.
- `genre` is `genres[0]`. RomMBat joins with `, `, which is what a real scraped install
  already contains (`Racing, Driving` in 2,079 of 4,440 entries).
- `developer` and `publisher` are `primary_developer` and `primary_publisher`, which read the
  `developers` and `publishers` split and **fall back to `companies[0]` and `companies[1]`**
  on a row without it (RM-7). A row carries the split only once it has been scanned since
  5.3.0, and `companies` is sorted, so on the rest upstream writes the alphabet's first and
  second company. RomMBat writes `<developer>` as `companies` joined, and no `<publisher>`
  (RB-98).

One thing to copy rather than diverge from: **`marquee` is sourced from ScreenScraper's
`logo_path`, not its `marquee_path`.** EmulationStation's marquee is game logo art;
ScreenScraper's marquee is an arcade cabinet marquee.

## Snapshot

The files are at the floor:

| Files                                                         | Ref                                                                                             |
| ------------------------------------------------------------- | ----------------------------------------------------------------------------------------------- |
| `systems_names.lst`, `es_systems.cfg`, `retrobat-license.txt` | `RetroBat-Official/retrobat` tag `8.3.1`, published 2026-10-16                                  |
| `es_savestates.cfg`, `batocera-systems.json`                  | `RetroBat-Official/emulatorlauncher` commit `b4e600bbf4dc`, the last before 8.3.1 was published |
| `romm-*`                                                      | `rommapp/romm` tag `5.4.0`                                                                      |
| `retrobios/`                                                  | `Abdess/retrobios` commit `527526a8e2be`, `_ref` in `database-subset.json`                      |

A pull from either upstream's default branch would put the vendored copy ahead of every shipped
release, so what master carries beyond the floor is a scout pass's to report, not this
directory's (the `version-adoption` skill). `refresh.sh` prints each ref it fetched at, and a
refresh that moves one rewrites this table, since no check compares it to the code.
