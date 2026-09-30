---
summary: The difference between reference/ and the tables under data/ that RomMBat ships and reads at runtime.
read-when: Before adding or changing a bundled table, or reading a number from reference/.
---

# Reference data and bundled tables

Two different things, easy to confuse.

**`reference/`** vendors upstream files so the numbers the docs quote are reproducible
offline. It is an audit trail, not a runtime input. Never hand-edit it, and never resolve
a drift by updating the expected number.

**`data/retrobat/`** holds tables RomMBat actually ships and reads at runtime:

| File                           | Shape                                                                  | Derived from                                                                                  |
| ------------------------------ | ---------------------------------------------------------------------- | --------------------------------------------------------------------------------------------- |
| `platforms.json`               | RomM slug to an **ordered list** of RetroBat folders                   | `systems_names.lst`, each folder resolved by RomM's `backend/utils/platform_aliases.py`       |
| `save_directories.json`        | **RetroBat system** to emulator save subdirectories                    | M0 experiment 2, in Grout's shape                                                             |
| `save_shapes.json`             | RetroBat system to save class A/B/C/D                                  | M0 experiment 2                                                                               |
| `save_rules.json`              | Which files under `saves/`, and gopher64's folder, are whose saves     | `tools/m6-probes/m6-emit-save-rules.py`, plus one hand-measured rule per `(system, emulator)` |
| `es_savestates.supplement.xml` | State entries for emulators `es_savestates.cfg` leaves out, per system | Driven on a real install, one row at a time, from `nes` to `n64`                              |
| `bios.json`                    | RetroBat system to the firmware it requires                            | `tools/build-bios-manifest.py`, over `reference/batocera-systems.json`                        |
| `multi_file.json`              | Systems whose multi-file ROMs sync, and how each lands                 | One certification pass per system; `psx` first, driven on every row RetroBat offers           |

Every one of these is a **seed, not an authority**. The live install always wins: read
`es_systems.cfg` from the actual tree, because RetroBat adds systems every release and
users add custom ones.

**`data/media/`** is the other shipped folder and is not a table. `rommbat-logo.png` is the
ES menu entry's artwork, embedded into `RomMBat.Core` and written to
`system/es_menu/media/` by `menu install`. Embedded rather than shipped beside the agent for
the same reason as the tables: a single-file publish carries it with no second file to lose.
