# nes

Nintendo Entertainment System / Famicom. RetroBat calls the folder `nes`, which is what this
file is named after.

**Not certified. Steps 1, 2 and 3 hold; 4 through 9 have not been run.** A pass is not done at
eight of nine, and this one is at three.

## The row

|             |                                                                                                                                                                                                        |
| ----------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| System      | `nes`                                                                                                                                                                                                  |
| Emulator    | `libretro`                                                                                                                                                                                             |
| Core        | `fceumm`                                                                                                                                                                                               |
| Selected by | **RetroBat's own default.** No `nes.emulator` or `nes.core` key exists in `es_settings.cfg`, so the choice falls through to the first-listed emulator and core in `es_systems.cfg`. Nothing was forced |
| Save class  | A, `provenance: observed` in `save_shapes.json`                                                                                                                                                        |

`libretro` and `bizhawk` are core-scoped, so this row says nothing about `nes` under
`libretro`/`nestopia`, `libretro`/`mesen`, `bizhawk`/`NesHawk`, `bizhawk`/`quickerNES`,
`mednafen`, `mesen`, `ares` or `jgenesis`. Those are separate rows.

**The core has not been confirmed from `emulatorLauncher.log` yet**, because no game has been
launched. Until it is, "fceumm" is what the configuration implies, not what ran.

## The install this was measured on

|             |                                                                            |
| ----------- | -------------------------------------------------------------------------- |
| RetroBat    | `8.2.1-stable-win64`, the supported floor                                  |
| RomM        | 5.2.0, the supported floor                                                 |
| Root        | `R:\RetroBat`, found by walking up from the executable                     |
| Store       | schema 14 of 14, WAL                                                       |
| Budget      | `none`. A 2 GB free-space floor still applies; NTFS, 927.4 GB free         |
| Media kinds | `ScrapeVideos` and `ScrapeManual` both `true`, as a fresh 8.2.1 ships them |

The budget being off is deliberate and narrows what this pass proves: **nothing here certifies
`budget`, `evict` or the eviction guards.** None of those is among the nine steps. It also means
a missing cover at step 7 cannot be a headroom problem, which is why it was switched off.

## Checklist

| #   | Step                                                           | Result                                                           |
| --- | -------------------------------------------------------------- | ---------------------------------------------------------------- |
| 1   | Folder mapping resolves, layer recorded                        | **Pass**, at layer `fs_slug`. See below                          |
| 2   | `<extension>` captured; unsupported file excluded and reported | **Partial.** List captured; the exclusion has not been exercised |
| 3   | Required BIOS resolved against RomM by md5                     | **Pass.** RetroBat requires no BIOS for `nes`                    |
| 4   | Save shape classified, battery save round-trips                | Not run                                                          |
| 5   | Save state round-trips with its screenshot                     | Not run                                                          |
| 6   | Per-game memory card where class D applies                     | **N/A.** See below                                               |
| 7   | Launches from EmulationStation with art and metadata           | Not run                                                          |
| 8   | Play session recorded and reaches RomM                         | Not run                                                          |
| 9   | Re-sync is a clean no-op                                       | Not run                                                          |

### 1. Mapping

Resolved at layer **`fs_slug`**, not `bundled`. RomM's `fs_slug` for this platform is literally
`nes`, which is already a folder in this install, so layer 2 matches and the bundled table is
never consulted.

**That is a property of this RomM instance, not of the platform.** An instance whose library
folders carry RomM's canonical slugs would resolve the same platform at `bundled` instead. Both
are correct; the record names which happened here.

**Two RomM platforms map to this one folder:**

| `fs_slug`        | id  | Resolved by | Name                                                     |
| ---------------- | --- | ----------- | -------------------------------------------------------- |
| `nes`            | 279 | `fs_slug`   | Nintendo - Family Computer / NES (Headered)              |
| `nes-unofficial` | 306 | `bundled`   | Nintendo - Family Computer / NES (Headered) (Unofficial) |

So `roms/nes/` can receive games from either, and a set scoped to one of them is not the whole
of what lands in the folder.

### 2. Extensions

From the **live** `es_systems.cfg` on this install, not the vendored copy:

```text
.fds .nes .wad .zip .7z
```

**This list is a union across every emulator the system declares, and `fceumm` does not promise
all of it.** RetroBat publishes no per-`(emulator, core)` extension data anywhere: `es_features.cfg`
mentions "extension" 28 times and every one is an N64 controller pak. `.fds` is Famicom Disk
System and `.wad` is not a NES container at all, so neither is a claim about this row until a
launch proves it.

Observed to launch under `libretro`/`fceumm`: **nothing yet.** To be filled in by step 7.

The other half of the step, that a file this folder cannot launch is excluded from the sync set
and reported, has not been exercised. It will be driven through the shipped path, by attempting
to pick a rom whose `fs_extension` the folder refuses and recording the sentence `MemberFor`
produces, rather than by putting a file into `roms/nes/` by hand.

### 3. BIOS

```console
$ rommbat-agent bios nes
RetroBat requires no BIOS for nes.
$ echo $?
0
```

**A real system with nothing to fetch, which counts as step 3 passing.** `nes` is one of the
sixteen systems whose requirement is empty, so none of the four fetch states arises: nothing is
present, fetched, missing from the library, or hashless.

The refusal path was checked at the same time, because "no BIOS needed" and "that system does
not exist" must not look alike:

```console
$ rommbat-agent bios ness
'ness' is not a system in this install's es_systems.cfg.
Run 'platforms' to see the systems this install declares.
$ echo $?
2
```

A mistyped name is refused with a non-zero exit rather than reported as a system needing no
firmware.

### 6. Per-game memory card

**N/A, and recorded rather than left blank.** `save_shapes.json` gives `nes` no
`DependsOnEmulator` flag and no `per_game_conversion` block, so there is no shared container to
opt a game out of. This holds for every wave 1 system.

## What this file will not claim

- Nothing here is evidence about any other `(emulator, core)` row for `nes`.
- The class D download path is untested anywhere in the project and `nes` contains no class D.
- Media coverage, once step 7 records it, is a dated observation about this RomM library and
  moves when an administrator rescrapes. It is never a platform result.
