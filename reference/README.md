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
| `config.batocera-retrobat.yml` | `rommapp/romm` `examples/config.batocera-retrobat.yml`                                  | Seed for the platform map (folder → RomM slug). A seed, **not** an answer                                                                                              |
| `romm-known_bios_files.json`   | `rommapp/romm` `backend/models/fixtures/known_bios_files.json`                          | What RomM's `is_verified` flag is computed from                                                                                                                        |
| `romm-gamelist_exporter.py`    | `rommapp/romm` `backend/utils/gamelist_exporter.py`                                     | The gamelist field reference M4 writes to, and the source of two unit conversions RomMBat would otherwise have to guess at                                              |

## Derived facts

Run `python3 verify.py` to reproduce all of these. If any number moves, the corresponding
section of `docs/PLAN.md` needs revisiting.

**Platform mapping is many-to-many and incomplete**

|                                            |                    |
| ------------------------------------------ | ------------------ |
| RetroBat systems                           | 240                |
| RomM known platform slugs                  | 457                |
| Explicit pairs in the YAML                 | 167                |
| RetroBat systems with no mapping           | 91 (37%)           |
| Of those, resolved by normalization alone  | 16                 |
| YAML entries naming folders RetroBat lacks | 18                 |
| RomM slugs mapping to several folders      | 13 (`arcade` → 10) |

The pair and stale counts read 168 and 19 until M2. `verify.py` split the YAML on the first
`platforms:` and matched every key indented four spaces, which also catches
`scan.gamelist.export`, a boolean rather than a platform. It now walks the block by
indentation, the same way `tools/build-platform-map.py` does, so the two agree on what a
pair is. **This was a parser fault here, not drift upstream.**

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
`rommapp/romm` master.

**What 8.2.1 moved.** `es_systems.cfg` gained `.decomp` on eleven systems (`mame`, `model2`,
`model3`, `snes`, `n64`, `gamecube`, `wii`, `psx`, `ps2`, `ps3`, `xbox`) and `.zar` on `ps4`,
and promoted `pcsx2x6` ahead of `play` for `namco2x6`. `batocera-systems.json` gained a
`namco2x6` entry of two files, which is where all four firmware counts below moved from;
both entries carry an empty md5, so nothing new became joinable. `systems_names.lst` and
`es_savestates.cfg` are unchanged.
