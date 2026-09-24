---
name: platform-mapping
description: Resolving a RomM platform slug to a RetroBat system folder, and adding or correcting a mapping. Use when a platform is unmapped, a sync writes to the wrong folder, or the bundled mapping table needs changing.
---

# Platform mapping

The two vocabularies genuinely diverge. This is a feature with a UI, not a lookup table.

## The measured gap

Reproduce with `cd reference && ./refresh.sh`.

|                                           |                             |
| ----------------------------------------- | --------------------------- |
| RetroBat systems                          | 240                         |
| RomM known platform slugs                 | 459                         |
| Folder aliases upstream publishes         | 138                         |
| Of those, RetroBat system folders         | 94                          |
| Of those, naming folders RetroBat lacks   | 44                          |
| RetroBat folders resolving to a RomM slug | 166                         |
| Of those, by identity                     | 72                          |
| Of those, via the alias table             | 94                          |
| RetroBat systems with no mapping          | 74 (31%)                    |
| Of those, resolved by normalization       | 1                           |
| RomM slugs mapping to several folders     | 10 (`arcade` fans out to 7) |

## Where the seed comes from, and why the walk goes RetroBat-first

Until RomM 5.3.0 the seed was `examples/config.batocera-retrobat.yml`, 167 explicit
`folder: slug` pairs. Upstream cut that file to four suggested overrides and moved the
authority into `backend/utils/platform_aliases.py`. `resolve_platform_slug` there tries a
config binding, then **identity** when the folder name is itself a `UniversalPlatformSlug`,
then `PLATFORM_FS_ALIASES`. `nes: nes` left the YAML because it became implicit, not because
it stopped being true, so reconstructing the map needs the alias table **and** the slug enum.
Both are vendored, as `reference/romm-platform_aliases.py` and
`reference/romm-platform_slugs.py`.

**`tools/build-platform-map.py` walks RetroBat's system list, not upstream's alias keys.**
`PLATFORM_FS_ALIASES` is a Batocera / RetroBat / ES-DE union and 44 of its 138 keys name
folders no RetroBat install has (`atarijaguar`, `atarilynx`, `gc`, `megadrivejp` against
RetroBat's `jaguar`, `lynx`, `gamecube`, `megadrive`). Walking from RetroBat's side never sees
them, which is core principle 3 applied rather than restated, and it is why the generator no
longer carries a list of stale seed keys to correct.

**The config-binding layer is recorded and not applied.** Upstream's example config suggests
four bindings for a Batocera or RetroBat install (`atari800: atari8bit`, `model2: arcade`,
`model3: arcade`, `pico: sega-pico`), and they are in the generated JSON as
`_upstream_suggested_bindings` so nobody re-discovers the disagreement. They stay unapplied
because they describe one server's scan, and a library scanned that way reports
`platform.fs_slug` as the folder name, which layer 2 matches and which outranks the bundled
table anyway.

**Two things the old YAML had hidden.** It carried two slugs RomM has never had, `daphne` and
`rpgmaker`, so neither could ever match a platform row; `rpgmaker` is now `rpg-maker` and
`daphne` has no RomM equivalent. And normalization's share fell from 16 to 1, because identity
resolution now catches what it used to rescue. `actionmax` against `action-max` is the only
survivor, and it is the case the mapping regression asserts.

**Four slugs left the table, and two of them are real.** `daphne` and `rpgmaker` are the
harmless pair above. `odyssey` and `atari8bit` are `UniversalPlatformSlug` values, so the
accounting is not "two slugs RomM never had" and nothing else:

- `odyssey` was a seed error. Magnavox Odyssey is not the Odyssey², and the seed pointed
  `odyssey` at folder `odyssey2`. `odyssey-2` → `odyssey2` now carries the real case, so the
  drop is a correction.
- `atari8bit` is upstream's suggested binding for folder `atari800`, recorded and not applied,
  so it has no layer-3 entry. Layer 2 covers it whenever the RomM library folder is itself
  named `atari800`, bound or unbound, which is the common case. **It does not cover a RomM
  folder named something RetroBat lacks** (`atari-8bit`, `a800`) that RomM resolved to
  `atari8bit`: layer 2 misses on `fs_slug`, layer 3 has nothing, and normalization cannot
  bridge `atari8bit` to `atari800`. That set syncs nothing and needs a manual mapping. Narrow,
  and accepted, because the RetroBat-first walk is what core principle 3 asks for.

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

**An `fs_slug` is unique, but from RomM 5.3.1 it is not fixed in case.** A rescan matches a
folder to its platform ignoring case and rewrites the same id with the folder's on-disk
spelling (rommapp/romm#4676), so a platform stored as `psx` can come back as `PSX` where a
`config.yml` binding lowercased it before. `PlatformMapStore.Record` rekeys the row when the
id matches and the keys differ only in case, keeping a user's choice; migration 002's "stable
across a rescan" predates this. It needs the id as well, because a case-sensitive server
filesystem can hold `psx` and `PSX` as two platforms. No platform on the live library carries
a mixed-case `fs_slug` (125 of 125 lowercase, 2026-09-24), so this is from source, not driven.

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

`astrocde` is the same spelling RomM's alias table uses, so the divergence shows up in two
places at once. Note also that RetroBat calls the Mega CD `megacd` while upstream's table keys
`megacd` and `segacd` both, so a BIOS lookup for `segacd` finds nothing.

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
- _RetroBat system with no RomM platform_: ignore entirely. About 50 of the 73 left after
  normalization are ports, engines and storefronts (`cavestory`, `devilutionx`, `eduke32`,
  `gemrb`, `opengoal`, `gog`, `epic`, `amazon`, the four `pinballfx*` launchers) which have no
  RomM equivalent by design. The rest is genuinely missing hardware: `chihiro`, `daphne`,
  `gaelco`, `cassettevision`, `neogeo64`, `vg5k`.

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
- **The bundled table is a seed, not an authority.** It is derived from
  `reference/systems_names.lst` rather than checked against it, so expect drift as both
  projects add systems, and expect a RomM release to move it.

## Adding or fixing a mapping

Edit `tools/build-platform-map.py`, not the JSON: `data/retrobat/platforms.json` is
generated and regenerating overwrites a hand edit. A mapping that is wrong because **upstream**
is wrong belongs in an issue against `rommapp/romm`, not in a local correction table: the
generator deliberately has no such table any more, so adding one back is a decision and not a
tidy-up. Then run
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
