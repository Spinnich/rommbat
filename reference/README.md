# Reference data

Upstream files RomMBat's design depends on, vendored so the numbers below, which the docs and
skills cite, can be re-derived offline and so drift is visible in a diff.

**These are upstream artifacts. Never hand-edit them.** Refresh with `refresh.sh` and
review the diff, because a change here can invalidate a design decision.

`refresh.sh` also checks `data/retrobat/bios.json` and `data/retrobat/platforms.json`
against the generators that derive them from these files, and exits non-zero naming the
generator to run. It never regenerates them itself: rewriting a committed generated file
mid-refresh would hide the change the script exists to surface.

| File                           | Source                                                                                  | What it settles                                                                                                                                                        |
| ------------------------------ | --------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `systems_names.lst`            | `RetroBat-Official/retrobat` `system/configgen/systems_names.lst`                       | The authoritative list of RetroBat system folder names (240)                                                                                                           |
| `es_systems.cfg`               | `RetroBat-Official/retrobat` `system/templates/emulationstation/es_systems.cfg`         | Per-system `<extension>`, plus `<manufacturer>`/`<hardware>`/`<release>` used to derive rollout order. **Read the live copy at runtime**; this is the shipped template |
| `es_savestates.cfg`            | `RetroBat-Official/emulatorlauncher` `.emulationstation/es_savestates.cfg`              | Per-emulator save-state schema: directory, file, image, autosave templates, slot bounds                                                                                |
| `batocera-systems.json`        | `RetroBat-Official/emulatorlauncher` `batocera-systems/Resources/batocera-systems.json` | Required BIOS manifest: 100 systems, 355 entries of `{md5, file}` with destination paths                                                                                |
| `romm-platform_slugs.py`       | `rommapp/romm` `backend/utils/platform_slugs.py`                                        | `UniversalPlatformSlug`, RomM's whole platform vocabulary (459). `romm-slugs.txt` beside it is the values alone, derived by `refresh.sh`                                |
| `romm-platform_aliases.py`     | `rommapp/romm` `backend/utils/platform_aliases.py`                                      | `PLATFORM_FS_ALIASES` and `resolve_platform_slug`: how RomM turns a folder name into a slug. Seed for the platform map. A seed, **not** an answer                       |
| `romm-known_bios_files.json`   | `rommapp/romm` `backend/models/fixtures/known_bios_files.json`                          | What RomM's `is_verified` flag is computed from                                                                                                                        |
| `romm-gamelist_exporter.py`    | `rommapp/romm` `backend/utils/gamelist_exporter.py`                                     | The gamelist field reference RomMBat's writer follows, and the source of two unit conversions RomMBat would otherwise have to guess at                                              |

## Derived facts

Run `python3 verify.py` to reproduce all of these. If any number moves, every doc and skill
that cites it needs revisiting.

**Platform mapping is many-to-many and incomplete**

|                                               |                   |
| --------------------------------------------- | ----------------- |
| RetroBat systems                              | 240               |
| RomM known platform slugs                     | 459               |
| Folder aliases upstream publishes             | 138               |
| Of those, RetroBat system folders             | 94                |
| Of those, naming folders RetroBat lacks       | 44                |
| RetroBat folders resolving to a RomM slug     | 166               |
| Of those, by identity (the folder is a slug)  | 72                |
| Of those, via the alias table                 | 94                |
| **RetroBat systems with no mapping**          | **74 (31%)**      |
| Of those, resolved by normalization alone     | 1                 |
| Distinct RomM slugs reached                   | 148               |
| RomM slugs mapping to several folders         | 10 (`arcade` → 7) |

**The seed is upstream's resolver, not a table of pairs.** `resolve_platform_slug` in
`backend/utils/platform_aliases.py` tries a config binding, then identity when the folder name is
itself a slug, then `PLATFORM_FS_ALIASES`. So `nes` maps to `nes` without appearing anywhere.

`tools/build-platform-map.py` walks **RetroBat's** system list and asks upstream what each folder
resolves to, rather than importing upstream's 138 keys and correcting them. Core principle 3 is
why: the alias table is a Batocera / RetroBat / ES-DE union and 44 of its keys name folders no
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

The operative consequence: **join firmware on md5 only.** Filenames differ between the two
projects, and RomM's `is_verified` misses 60% of what RetroBat requires.

**The gamelist exporter settles two units and gets a third field wrong**

`verify.py` asserts behaviours rather than counts here, because the writer depends on them.
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

The RetroBat files are RetroBat 8.2.1 (`system/version.info: 8.2.1-stable-win64`), pulled
2026-08-25. The `romm-*` files are `rommapp/romm` master, pulled 2026-09-14.

**The RetroBat files are level with the floor, not with master.** They stay at the newest
release, because a pull from master would put the vendored copy ahead of every shipped RetroBat.

**RetroBat master carries three changes the snapshot does not**, none of which moves a number in
`verify.py`. They are listed so the next RetroBat adoption starts from a list rather than a diff:

- `es_systems.cfg` adds `.decomp` to `cps3`, `naomi` and `naomi2`.
- `es_systems.cfg` adds `gearsystem` as a libretro core for one system.
- `es_savestates.cfg` gains an `amiberry` emulator block: slots 1 to 9, `{{system}}/amiberry`,
  files named `{{romfilename}}-{{slot0}}.uss`.
