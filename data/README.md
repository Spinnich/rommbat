# Bundled data

Tables RomMBat ships and reads at runtime. Not to be confused with
[reference/](../reference/), which vendors upstream files purely so the numbers in
[docs/PLAN.md](../docs/PLAN.md) can be re-derived offline.

| File                             | Shape                                                       | Derived from                                                                 | Arrives in |
| -------------------------------- | ----------------------------------------------------------- | ---------------------------------------------------------------------------- | ---------- |
| `retrobat/platforms.json`        | RomM slug to an **ordered list** of RetroBat folders        | RomM's `config.batocera-retrobat.yml`, corrected against `systems_names.lst` | M2         |
| `retrobat/save_directories.json` | RomM slug to emulator save subdirectories, in Grout's shape | M0 experiment 2                                                              | M6         |
| `retrobat/save_shapes.json`      | RetroBat system to save class A/B/C/D                       | M0 experiment 2                                                              | M6         |

## Every one of these is a seed, not an authority

The live install always wins. Read `es_systems.cfg` from the actual RetroBat tree,
because RetroBat adds systems every release and users add custom ones, and a bundled
table that is treated as truth will place files in folders that do not exist.

Platform resolution runs in layers, and the bundled table is only the third of five:

1. User override, persisted in `Device.sync_config` so it roams. Always wins.
2. `platform.fs_slug` matched against the live `es_systems.cfg`.
3. `retrobat/platforms.json`.
4. Normalized-match **suggestion**, offered for confirmation, never applied silently.
5. Unmapped, which is a normal state and not an error.

## Why the table is not just the upstream YAML inverted

Measured against RetroBat's `systems_names.lst` and RomM's `UniversalPlatformSlug`:
240 RetroBat systems, 457 RomM slugs, but only 168 explicit pairs in the YAML. 91
RetroBat systems (37%) are unmapped, normalization rescues 16 of them, 19 YAML entries
name folders RetroBat does not have (`astrocde` vs `astrocade`, `ps` vs `psx`, `segacd`
vs `megacd`), and 13 RomM slugs fan out to several folders, `arcade` alone to ten.

`python3 ../reference/verify.py` re-derives all of those numbers. If one moves, revisit
the plan rather than the expected value.

## Changing a mapping

Mapping changes need a test. There is a checked-in regression asserting that every
bundled mapping resolves to a folder that exists in `systems_names.lst`, that
multi-folder slugs resolve deterministically against a fixture `es_systems.cfg`, and
that two platforms sharing a folder produce one merged gamelist rather than two
competing writes. Load the `platform-mapping` skill first.
