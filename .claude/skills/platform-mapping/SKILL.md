---
name: platform-mapping
description: Resolving a RomM platform slug to a RetroBat system folder, and adding or correcting a mapping. Use when a platform is unmapped, a sync writes to the wrong folder, or the bundled mapping table needs changing.
---

# Platform mapping

The two vocabularies genuinely diverge. This is a feature with a UI, not a lookup table.

## The measured gap

Reproduce with `cd reference && ./refresh.sh`.

|                                                  |                              |
| ------------------------------------------------ | ---------------------------- |
| RetroBat systems                                 | 240                          |
| RomM known platform slugs                        | 457                          |
| Explicit pairs in `config.batocera-retrobat.yml` | 168                          |
| RetroBat systems with no mapping                 | 91 (37%)                     |
| Of those, resolved by normalization              | 16                           |
| YAML entries naming folders RetroBat lacks       | 19                           |
| RomM slugs mapping to several folders            | 13 (`arcade` fans out to 10) |

Known-stale YAML entries: `astrocde`/`astrocade`, `bbc`/`bbcmicro`, `ps`/`psx`,
`segacd`/`megacd`.

## No authoritative source exists

- `platform.libretro_slug` is a libretro DAT name ("Nintendo - Super Nintendo Entertainment
  System"), not a folder, and it over-collapses (Amiga and Amiga CD32 share a value).
- `platform.family_slug` is IGDB's **manufacturer**, so it cannot separate regional twins.
- `platform.fs_slug` is the best signal, but only when the RomM library happens to be laid
  out Batocera-style.

## Resolution chain

1. **User override** from the mapping screen, persisted in `Device.sync_config`. Always wins.
2. **`platform.fs_slug`** matched against the live `es_systems.cfg`. When the server is
   already Batocera-shaped, `fs_slug` _is_ the folder name.
3. **Bundled `data/retrobat/platforms.json`**, slug to an **ordered list** of folders. First
   one present in the target's `es_systems.cfg` wins.
4. **Normalized-match suggestion** (`actionmax` to `action-max`), offered for confirmation,
   never applied silently.
5. **Unmapped**, which is a normal state, not an error.

## Two kinds of unmapped, and only one matters to the user

- _RomM platform with no RetroBat folder_: skip, explain.
- _RetroBat system with no RomM platform_: ignore entirely. About 50 of the 75
  hard-unresolved names are ports and storefronts (`cavestory`, `devilutionx`, `eduke32`,
  `gemrb`, `opengoal`, `steam`, `gog`, `epic`, `amazon`) which have no RomM equivalent by
  design.

## Consequences elsewhere

- **Two platforms can share one folder** (`snes` and `sfam`, several arcade platforms into
  `mame`). Key the local file index and gamelist generation by **resolved folder**, or the
  second write clobbers the first.
- **Arcade needs an explicit user choice per sync set.** Which of the ten folders is right
  depends on the romset, and arcade names are romset-versioned. Do not guess.
- **The bundled table is a seed, not an authority.** Correct it against
  `reference/systems_names.lst`, and expect drift as both projects add systems.

## Adding or fixing a mapping

Edit `data/retrobat/platforms.json`, then run the mapping regression, which asserts every
bundled mapping resolves to a folder that exists in `systems_names.lst` and that multi-folder
slugs resolve deterministically against a fixture `es_systems.cfg`.
