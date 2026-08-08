# Reference data

Upstream files RomMBat's design depends on, vendored so the numbers in `docs/PLAN.md` can
be re-derived offline and so drift is visible in a diff.

**These are upstream artifacts. Never hand-edit them.** Refresh with `refresh.sh` and
review the diff, because a change here can invalidate a design decision.

| File                           | Source                                                                                  | What it settles                                                                                                                                                        |
| ------------------------------ | --------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `systems_names.lst`            | `RetroBat-Official/retrobat` `system/configgen/systems_names.lst`                       | The authoritative list of RetroBat system folder names (240)                                                                                                           |
| `es_systems.cfg`               | `RetroBat-Official/retrobat` `system/templates/emulationstation/es_systems.cfg`         | Per-system `<extension>`, plus `<manufacturer>`/`<hardware>`/`<release>` used to derive rollout order. **Read the live copy at runtime**; this is the shipped template |
| `es_savestates.cfg`            | `RetroBat-Official/emulatorlauncher` `.emulationstation/es_savestates.cfg`              | Per-emulator save-state schema: directory, file, image, autosave templates, slot bounds                                                                                |
| `batocera-systems.json`        | `RetroBat-Official/emulatorlauncher` `batocera-systems/Resources/batocera-systems.json` | Required BIOS manifest: 99 systems, 353 entries of `{md5, file}` with destination paths                                                                                |
| `config.batocera-retrobat.yml` | `rommapp/romm` `examples/config.batocera-retrobat.yml`                                  | Seed for the platform map (folder → RomM slug). A seed, **not** an answer                                                                                              |
| `romm-known_bios_files.json`   | `rommapp/romm` `backend/models/fixtures/known_bios_files.json`                          | What RomM's `is_verified` flag is computed from                                                                                                                        |

## Derived facts

Run `python3 verify.py` to reproduce all of these. If any number moves, the corresponding
section of `docs/PLAN.md` needs revisiting.

**Platform mapping is many-to-many and incomplete**

|                                            |                    |
| ------------------------------------------ | ------------------ |
| RetroBat systems                           | 240                |
| RomM known platform slugs                  | 457                |
| Explicit pairs in the YAML                 | 168                |
| RetroBat systems with no mapping           | 91 (37%)           |
| Of those, resolved by normalization alone  | 16                 |
| YAML entries naming folders RetroBat lacks | 19                 |
| RomM slugs mapping to several folders      | 13 (`arcade` → 10) |

**Firmware knowledge barely overlaps**

|                                    |     |
| ---------------------------------- | --- |
| Distinct md5s RetroBat requires    | 157 |
| Distinct md5s RomM knows           | 353 |
| Overlap                            | 63  |
| RetroBat-required, unknown to RomM | 94  |

The operative consequence: **join firmware on md5 only.** Filenames differ between the two
projects, and RomM's `is_verified` misses 60% of what RetroBat requires.

## Snapshot

Captured 2026-08-08 against RetroBat 8.2.0 (`build.ini: retrobat_version=8.2.0`) and
`rommapp/romm` master.
