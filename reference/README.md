# Reference data

Upstream files RomMBat's design depends on, vendored so the numbers in `docs/PLAN.md` can
be re-derived offline and so drift is visible in a diff.

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
| `batocera-systems.json`        | `RetroBat-Official/emulatorlauncher` `batocera-systems/Resources/batocera-systems.json` | Required BIOS manifest: 99 systems, 353 entries of `{md5, file}` with destination paths                                                                                |
| `romm-platform_slugs.py`       | `rommapp/romm` `backend/utils/platform_slugs.py`                                        | `UniversalPlatformSlug`, RomM's whole platform vocabulary (459). `romm-slugs.txt` beside it is the values alone, derived by `refresh.sh`                                |
| `romm-platform_aliases.py`     | `rommapp/romm` `backend/utils/platform_aliases.py`                                      | `PLATFORM_FS_ALIASES` and `resolve_platform_slug`: how RomM turns a folder name into a slug. Seed for the platform map. A seed, **not** an answer                       |
| `romm-known_bios_files.json`   | `rommapp/romm` `backend/models/fixtures/known_bios_files.json`                          | What RomM's `is_verified` flag is computed from                                                                                                                        |
| `romm-gamelist_exporter.py`    | `rommapp/romm` `backend/utils/gamelist_exporter.py`                                     | The gamelist field reference M4 writes to, and the source of two unit conversions RomMBat would otherwise have to guess at                                              |

## Derived facts

Run `python3 verify.py` to reproduce all of these. If any number moves, the corresponding
section of `docs/PLAN.md` needs revisiting.

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

**The source of these moved, and so did the shape of the question.** Until RomM 5.3.0 the
seed was `examples/config.batocera-retrobat.yml`, 167 explicit `folder: slug` pairs. Upstream
cut that file to four suggested overrides and moved the authority into
`backend/utils/platform_aliases.py`, where `resolve_platform_slug` tries a config binding,
then identity when the folder name is itself a slug, then `PLATFORM_FS_ALIASES`. Identity
cases therefore left the YAML rather than being deleted: `nes: nes` is now implicit.

`tools/build-platform-map.py` walks **RetroBat's** system list and asks upstream what each
folder resolves to, rather than importing upstream's 138 keys and correcting them. Core
principle 3 is why: the alias table is a Batocera / RetroBat / ES-DE union and 44 of its keys
name folders no RetroBat install has, so walking from RetroBat's side never sees them. The
old `STALE_KEYS` correction list is gone with it, because there is nothing left to correct.

Two facts fell out of the re-source that the YAML had hidden. The old seed carried two slugs
RomM has never had, `daphne` and `rpgmaker`, so neither could ever match a platform row;
`rpgmaker` is now `rpg-maker` and `daphne` has no RomM equivalent at all. And normalization's
share collapsed from 16 to 1, because identity resolution catches almost everything it used to
rescue. The one survivor is `actionmax` against `action-max`.

The 167/91/18/13 figures held at RomM 5.2.0 and are kept here as what the YAML said, not as
something to reconcile. The pair and stale counts read 168 and 19 until M2, when `verify.py`
stopped counting `scan.gamelist.export` as a platform; that was a parser fault here, not drift
upstream.

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

`verify.py` asserts behaviours rather than counts here, because that is what M4 reads off it.
Confirmed in upstream's own code: `first_release_date` is divided by 1000, so it is
**milliseconds**, and `average_rating` is divided by 100, so it is on a **0-100** scale, with
a comment saying as much. Both match what RomMBat measured live.

**RomMBat deliberately diverges in three places**, and the checks exist so the divergence
stays visible rather than becoming an accidental difference:

- `developer` and `publisher` are `companies[0]` and `companies[1]` upstream. That array is
  alphabetically sorted on every row measured, so indexing it writes the alphabet into two
  role-bearing fields: KOTOR gets Activision as developer and Aspyr Media as publisher.
  RomMBat writes the joined list into `developer` and omits `publisher`.
- `region` and `lang` are `regions[0]` and `languages[0]` verbatim, so upstream writes `USA`
  and `English` where EmulationStation's own vocabulary is `us` and `en`. RomMBat maps them.
- `genre` is `genres[0]`. RomMBat joins with `, `, which is what a real scraped install
  already contains (`Racing, Driving` in 2,079 of 4,440 entries).

One thing to copy rather than diverge from: **`marquee` is sourced from ScreenScraper's
`logo_path`, not its `marquee_path`.** EmulationStation's marquee is game logo art;
ScreenScraper's marquee is an arcade cabinet marquee.

## Snapshot

Captured 2026-08-25 against RetroBat 8.2.1 (`system/version.info: 8.2.1-stable-win64`) and
`rommapp/romm` master, **except the two platform files, re-pulled 2026-09-14**.

**This snapshot is deliberately not uniform.** `romm-platform_slugs.py`,
`romm-platform_aliases.py` and the `romm-slugs.txt` derived from them were re-pulled on their
own, because the platform map had no working source until they were (#166). Everything else is
still the 2026-08-25 pull. The next full `refresh.sh` will move
`romm-gamelist_exporter.py` too, and **that is expected to fail `verify.py`**: upstream replaced
`companies[0]` / `companies[1]` with `primary_developer` / `primary_publisher`, so the
"upstream still indexes companies" check flips the moment the exporter is re-pulled. Resolving
that is #171, not a regression in the re-source.

**What 8.2.1 moved.** `es_systems.cfg` gained `.decomp` on eleven systems (`mame`, `model2`,
`model3`, `snes`, `n64`, `gamecube`, `wii`, `psx`, `ps2`, `ps3`, `xbox`) and `.zar` on `ps4`,
and promoted `pcsx2x6` ahead of `play` for `namco2x6`. `batocera-systems.json` gained a
`namco2x6` entry of two files, which is where all four firmware counts below moved from;
both entries carry an empty md5, so nothing new became joinable. `systems_names.lst` and
`es_savestates.cfg` are unchanged.

**What `rommapp/romm` master moved.** `romm-gamelist_exporter.py` gave `miximage_v2` its own
asset directory (`miximages_v2`, previously shared with `miximage`) and its own gamelist
element name, and started falling back to the gamelist provider's path as well as
ScreenScraper's. Inert here: nothing hand-written references `miximage`, and `verify.py`'s
gamelist checks are unaffected. Recorded because a vendored file moving is the signal, not
the consequence.
