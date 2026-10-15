---
summary: The difference between reference/ and the tables under data/ that RomMBat ships and reads at runtime.
read-when: Before adding or changing a bundled table, or reading a number from reference/.
---

# Reference data and bundled tables

Two different things, easy to confuse.

**`reference/`** vendors upstream files so the numbers the docs quote are reproducible
offline. It is an audit trail, not a runtime input. Never hand-edit it, and never resolve
a drift by updating the expected number.

**`data/retrobat/`** holds the tables RomMBat actually ships and reads at runtime, below, and two
it does not: `save_directories.json`, a probe's output that nothing embeds, and the firmware row
map, which only the vendoring step and `verify.py` read:

| File                           | Shape                                                                  | Derived from                                                                                  |
| ------------------------------ | ---------------------------------------------------------------------- | --------------------------------------------------------------------------------------------- |
| `platforms.json`               | RomM slug to an **ordered list** of RetroBat folders                   | `systems_names.lst`, each folder resolved by RomM's `backend/utils/platform_aliases.py`       |
| `save_shapes.json`             | RetroBat system to save class A/B/C/D                                  | [Save locations](../upstream/retrobat/save-locations.md)                                      |
| `save_rules.json`              | Which files under `saves/` and two emulators' folders are whose saves  | `tools/m6-probes/m6-emit-save-rules.py`, plus one hand-measured rule per `(system, emulator)` |
| `es_savestates.supplement.xml` | State entries for emulators `es_savestates.cfg` leaves out, per system | Driven on a real install, one row at a time, from `nes` to `n64`                              |
| `bios.json`                    | RetroBat system to the firmware slots its emulators read               | `tools/build-bios-manifest.py`, over `batocera-systems.json` and `reference/retrobios/`       |
| `multi_file.json`              | Systems whose multi-file ROMs sync, and how each lands                 | One certification pass per system; `psx` first, driven on every row RetroBat offers           |

Every one of these is a **seed, not an authority**. The live install always wins: read
`es_systems.cfg` from the actual tree, because RetroBat adds systems every release and
users add custom ones.

**`data/media/`** is the other shipped folder and is not a table. `rommbat-logo.png` is the
ES menu entry's artwork, embedded into `RomMBat.Core` and written to
`system/es_menu/media/` by `menu install`. Embedded rather than shipped beside the agent for
the same reason as the tables: a single-file publish carries it with no second file to lose.

**`data/retrobat/firmware-rows.json`** ships nothing and nothing reads it at runtime. It joins
each `(emulator, core)` row in `es_systems.cfg` to the retrobios source that describes the
firmware its code reads, where the core name a profile declares does not already. It also names,
with the `RB-` fact for each, the directory an emulator reads firmware from (`directories`); the
retrobios system ids that are each folder's own, for profiles covering several systems
(`systems`, and `bizhawk_systems` for BizHawk's vocabulary); and the firmware an emulator installs
through its own menu (`installed_by_emulator`). `tools/vendor-retrobios.py` chooses the vendored
slice of `reference/retrobios/` by it, `tools/build-bios-manifest.py` builds `bios.json` from it,
and `verify.py` fails on a row it neither resolves nor marks, or a map entry that is missing or
stale.

**`docs/platforms/firmware.md`** is `bios.json` made readable, a table per system, with the
emulator source lines the retrobios profiles cite. `tools/build-firmware-docs.py` writes it, and
its `--check` runs beside the manifest's in `tools/pre-pr.ps1`, CI and `refresh.sh`.
`tools/firmware.py` answers the same questions per row, offline, and compares a system with the
test server.

**`data/certification.json`** ships nothing and nothing reads it at runtime. It holds one
row per `(system, emulator, core)` that a record under `docs/platforms/` covers, with its
status and the floor it holds at, and `PlatformSupportPageTests` builds the guide's
`wiki/platforms/index.md` from it. The same test fails when a system in the file lacks a
row the vendored `es_systems.cfg` declares for it, or lists them out of order, so a
refresh that adds a core shows up as a row to add as `untested`. It also fails when a system
with a certified row has no hand-written guide page, `wiki/platforms/<system>.md`.
