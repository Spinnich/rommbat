# Bundled data

Tables RomMBat ships and reads at runtime. Not to be confused with
[reference/](../reference/), which vendors upstream files purely so the numbers in
[docs/PLAN.md](../docs/PLAN.md) can be re-derived offline.

| File                             | Shape                                                        | Derived from                                                                 | Arrives in |
| -------------------------------- | ------------------------------------------------------------ | ---------------------------------------------------------------------------- | ---------- |
| `retrobat/platforms.json`        | RomM slug to an **ordered list** of RetroBat folders         | RomM's `config.batocera-retrobat.yml`, corrected against `systems_names.lst` | M2         |
| `retrobat/save_directories.json` | **RetroBat system** to save subdirectories, in Grout's shape | M0 probe 2, generated from a real install                                    | M6         |
| `retrobat/save_shapes.json`      | RetroBat system to save class A/B/C/D                        | M0 probe 2, generated from a real install                                    | M6         |

## The two save tables depart from Grout in two ways, deliberately

Grout's `cfw/*/data/save_directories.json` keys by **RomM slug** and its values are one
segment deep. Both differ here, and the emitted files say so in their own `_comment`:

- **Keyed by RetroBat system folder, not RomM slug.** This file describes RetroBat's disk
  layout. Joining a slug to a system folder is what `platforms.json` above is for, and
  duplicating that mapping into a second file would let the two drift.
- **Paths are two segments deep**, because M0 found the real tree is
  `saves/<system>/<emulator>/` rather than `saves/<system>/`, with some emulator-named
  folders sitting at the top level beside the system ones.

Both files carry a `_provenance` field per entry. `observed` means it was seen on a real
install; `declared` means it comes from `es_savestates.cfg` or `es_features.cfg` and has
not yet been seen written. Regenerate with
`python tools/m0-probes/probe2-emit-data.py <retrobat-root>`.

**`_state_directory_templates` is copied from `es_savestates.cfg` and is not trustworthy on
its own.** Three sibling keys qualify it: `_state_directory_verified` lists the nine
emulators whose declared directory was confirmed by driving a real save state,
`_state_directory_corrections` carries the one that is wrong (`flycast` writes
`reicast/states`, not the declared `flycast/sstates`), and `_state_file_template_note`
records that a `{{slot}}` placeholder must expand to a single digit rather than a wildcard,
because DeSmuME's `.ds{{slot0}}` otherwise matches its own `.dsv` battery save.

**Save shape is a property of `(system, emulator)`, not of system alone.** `psx` is the
worked example: libretro writes a class-A `.srm`, DuckStation writes memory cards. Any
consumer that looks up a shape by system name alone will be wrong for every system with
more than one emulator.

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
