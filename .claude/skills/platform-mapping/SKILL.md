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
| Explicit pairs in `config.batocera-retrobat.yml` | 167                          |
| RetroBat systems with no mapping                 | 91 (37%)                     |
| Of those, resolved by normalization              | 16                           |
| YAML entries naming folders RetroBat lacks       | 18                           |
| RomM slugs mapping to several folders            | 13 (`arcade` fans out to 10) |

Known-stale YAML entries: `astrocde`/`astrocade`, `bbc`/`bbcmicro`, `ps`/`psx`,
`segacd`/`megacd`.

## Two identity traps, both measured live

**`platform.slug` is not unique. `fs_slug` and `id` are.** A real 123-platform library
carried **72 distinct slugs**, because that owner files demos, prototypes, unlicensed and
aftermarket titles under a parallel `-unofficial` folder per system, and RomM resolves both
folders to one platform (`fs_slug` `gb` and `gb-unofficial` are both `slug` `gb`). **This is
a user's filing scheme, not a RomM behaviour**, so how many such rows exist and what they are
called is unpredictable: do not special-case the `-unofficial` suffix, and do not assume the
collisions come in pairs. The local `platform_map` is keyed by `fs_slug`; the slug is only the
bundled table's lookup key. Key the map by slug and 51 of those 123 platforms disappear, and
nobody can point the extra sets anywhere else.

**`es_systems.cfg` `<name>` is not the folder. `<path>` is.** Five systems disagree in the
shipped 8.2.1 file: `gw` writes to `gameandwatch`, `powerbomberman` to `pb`, `casloopy` to
`loopy`, `Windows` to `windows`, and `starship` is used **twice**, for `ghostship` and
`starship`. Four more entries own no folder under `roms/` (`library`, `screenshots`, `kodi`,
and `retrobat` at `system/es_menu`) and `mess` declares no path at all; none is a sync
target. Match folders case-insensitively, and take the folder from the resolved `<path>`.

## A third vocabulary: the BIOS manifest's system names

`batocera-systems.json` is keyed by **batocera system names**, which are neither
`es_systems.cfg`'s `<name>` nor RomM's slug. 97 of its 99 keys are exactly a `<path>` basename
and so need no translation at all; the two that are not are aliased in
`tools/build-bios-manifest.py`, which fails the build if a third appears:

| Manifest key | RetroBat folder |
| ------------ | --------------- |
| `astrocde`   | `astrocade`     |
| `msx`        | `msx1`          |

`astrocde` is the same stale spelling the seed YAML has, which is already listed above. Note
also that RetroBat calls the Mega CD `megacd`, so a BIOS lookup for `segacd` finds nothing:
that is the seed's `segacd`/`megacd` divergence showing up in a second place.

## No authoritative source exists

- `platform.libretro_slug` is a libretro DAT name ("Nintendo - Super Nintendo Entertainment
  System"), not a folder, and it over-collapses (Amiga and Amiga CD32 share a value).
- `platform.family_slug` is IGDB's **manufacturer**, so it cannot separate regional twins.
- `platform.fs_slug` is the best signal, but only when the RomM library happens to be laid
  out Batocera-style.

## Resolution chain

1. **User override** from the mapping screen, keyed by `fs_slug` and persisted in
   `Device.sync_config`. Always wins.

   **The screen exists as of M7 stage 7b-3**, reached from a row on the root menu carrying the
   unmapped count, so an unmapped platform is found before a sync is attempted rather than by a
   resolve stopping partway through a collection that happened to hold one of its games. It
   takes no connection: `platform_map` is written by every resolve and every browse, so every
   row it shows and the override that fixes one are already local. Unmapped rows sort first,
   because alphabetical order buries the three a person came to fix among a hundred and twenty.
   Writing an override records `MappingSource.User`, which is what stops a later re-resolve
   overwriting it, and clearing one leaves the row unmapped for the chain to answer again.

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
- **Arcade refuses to guess only when the library has not already answered.** The `fs_slug`
  match against the live `es_systems.cfg` runs **ahead** of the arcade check, so a platform
  carrying `fs_slug: fbneo` on an install that has an `fbneo` system resolves there: naming
  the folder is how the person filing the library made the choice. An arcade slug whose
  `fs_slug` names no folder this install has still stops and asks, because which of the ten
  folders is right depends on the romset and arcade names are romset-versioned. Measured in
  M7 stage 7b-2a on a live install, where refusing regardless stopped a collection resolve
  part way to demand a choice that had already been made.
- **The bundled table is a seed, not an authority.** Correct it against
  `reference/systems_names.lst`, and expect drift as both projects add systems.

## Adding or fixing a mapping

Edit `tools/build-platform-map.py`, not the JSON: `data/retrobat/platforms.json` is
generated from the seed and regenerating overwrites a hand edit. Then run
`python tools/build-platform-map.py` and the mapping regression, which asserts every bundled
mapping resolves to a folder that exists in `systems_names.lst`, that multi-folder slugs
resolve deterministically against a fixture `es_systems.cfg`, and that an `arcade` slug
whose `fs_slug` names no folder does not resolve on its own while one whose `fs_slug` does
name a folder resolves there.

## One ROM in two folders is legitimate, and it used to be fatal

`folder_override` is per sync set and migration 002's header calls it "the only way an arcade
set resolves". So a `mame`-overridden platform set and an `fbneo`-overridden collection set drawn
from that same platform put **every shared game in both folders**, and **both sets are then
correct in EmulationStation**: each folder's `gamelist.xml` names the file beside it. Remapping a
platform between two syncs reaches the same state with no override at all.

- **Two Rom-kind `local_file` rows for one `rom_id` is a representable, reachable state.**
  `LocalFileStore.ForRom`'s remarks already said so and `ix_local_file_rom_kind` is not `UNIQUE`.
  `EvictionPlanner.Candidates` keyed its lookup with `ToDictionary(file => file.RomId)`, which
  throws on the second row and takes out every caller of `EvictionPlanner`: `evict`, the budget
  screen and both removal previews. No sync pass evicts, so a sync is not among them. The comment
  directly above it named that hazard and then fixed only the media half.
- **Each copy is its own eviction candidate**, with the artwork in its own folder attached to it.
  Attaching every copy's media to one candidate has the first removal delete the other folder's
  cover.
- **Refusing the second copy is the wrong fix, and it is the tidier-looking one.** It leaves the
  second set's gamelist naming a file outside its own folder, which ES cannot follow, so it
  breaks that set to tidy the inventory.
- **The bytes genuinely double and the budget is right to count them twice.** What was wrong was
  that nobody could see why. A browse row names every folder a game is in, and the game's detail
  screen says the room is taken twice and that both sets are correct.
