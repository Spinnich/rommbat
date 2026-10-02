---
summary: The certification record for `nes`: each row's standing at the floor, and the system's own steps.
read-when: Before certifying a `nes` row, or when asked whether a `nes` row works.
---

# nes

Nintendo Entertainment System / Famicom. RetroBat calls the folder `nes`, which is what this folder is named after.

**All nine rows `nes` declares are certified.** All nine steps hold on each at RomM
`5.3.0-beta.1` and RetroBat 8.2.1, with step 6 N/A because `nes` has no class D, and hold at the
floor ("Where each row stands").
`libretro`/`nestopia` was the first certified `(system, emulator, core)` row in the project,
re-driven on 2026-09-20. `libretro`/`fceumm` and `libretro`/`mesen` followed on 2026-09-21, and
`fceumm` is **the row a stock install gives a user**, selected with no override. The two `bizhawk`
cores, `jgenesis`, `mesen` standalone, `mednafen` and `ares` followed later the same day, the last
four on a build that first gave them battery rules and, for three of them, the state declarations
`es_savestates.cfg` does not carry.

**It certifies those nine rows and nothing wider.** Every `(emulator, core)` pair `nes` declares on
RetroBat 8.2.1 is one of them, which is as close to "`nes` works" as this checklist lets a record
come, and it is still one install at one pair of floors. It says nothing about any of these
emulators on another system: every rule and declaration the last four needed was scoped to `nes`,
because that is the only system they were measured on. `megadrive` has since been measured and
widened the `bizhawk` and `mednafen` rules to name it, with its own `jgenesis` and `ares` rules
beside these; [megadrive's record](../megadrive/index.md) is that record, and nothing here speaks for it.

**Step 5 rests on a state made after RB-258's fix**, and the restored screenshot was checked by
its bytes rather than by its arrival ([`nestopia`'s step 5](libretro-nestopia.md#5-save-state-and-screenshot)).
Step 2 asks that nothing be excluded for its extension ([step 2](#2-extensions)).

## Where each row stands

**Every certified row holds at the floor, RomM `5.3.1` and RetroBat 8.2.1.** Steps 1 and 9 were
re-driven at `5.3.1` on 2026-09-24, in #236, which maps the nine steps. The other steps carry from
the drive at `5.3.0-beta.1`, step 3 from 5.2.0, since nothing they exercise changed since. Nothing
is owed.

| File                                         | What it holds                                                                                                                   |
| -------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------- |
| This file                                    | Steps 1, 2 and 3, which are the system's; the set; where each row stores a save; what the pass turned up; RomM's browser player |
| [libretro-nestopia.md](libretro-nestopia.md) | `libretro`/`nestopia` in full, the row the nine steps were first driven against                                                 |
| [libretro.md](libretro.md)                   | The two other `libretro` rows                                                                                                   |
| [bizhawk.md](bizhawk.md)                     | The two `bizhawk` rows, and BizHawk's battery save across ROMs                                                                  |
| [standalone.md](standalone.md)               | `jgenesis`, `mesen`, `mednafen` and `ares`                                                                                      |
| [conflicts.md](conflicts.md)                 | Conflict resolution, driven both ways                                                                                           |
| [facts.md](facts.md)                         | The measured facts about `nes`'s emulators, with RB- IDs                                                                        |

## The install this was measured on

|             |                                                                                                                            |
| ----------- | -------------------------------------------------------------------------------------------------------------------------- |
| RetroBat    | `8.2.1-stable-win64`, the supported floor                                                                                  |
| RomM        | `5.3.0-beta.1` for the steps, with step 3 at 5.2.0 and steps 1 and 9 again at `5.3.1`; 5.2.0 for "What the pass turned up" |
| Client      | For `nestopia`, a deploy of `main` at 9ed1fd1 by `tools/publish.ps1 -Deploy`; each row file names its own                  |
| Root        | `R:\RetroBat`, found by walking up from the executable                                                                     |
| Store       | schema 16 of 16, WAL; 14 of 14 at 5.2.0                                                                                    |
| Budget      | `none`. A 2 GB free-space floor still applies; NTFS, 927.4 GB free                                                         |
| Media kinds | `ScrapeVideos` and `ScrapeManual` both `true`, as a fresh 8.2.1 ships them                                                 |

The budget being off is deliberate and narrows what this pass proves: **nothing here certifies
`budget`, `evict` or the eviction guards.** None of those is among the nine steps. It also means
a missing cover at step 7 cannot be a headroom problem, which is why it was switched off.

**Which row runs is set in two places, and a game's own pin wins.** `nes.emulator` and `nes.core`
in `es_settings.cfg` pick the row for the system, and `<emulator>` and `<core>` children of a
`<game>` in `gamelist.xml` pick it for one game without a trace in `es_settings.cfg`
(the `retrobat-layout` skill's `settings.md`). Eight games on this install carry such a pin,
among them StarTropics and Ultima on `bizhawk`, so every row file reads the launch line in
`emulatorLauncher.log` rather than the configuration. `mesen` standalone and
`jgenesis` declare no core and are launched with the system's `-core nestopia`, which both
ignore.

## Steps 1, 2 and 3, for every row

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

**This list is a union across every emulator the system declares, and `nestopia` does not promise
all of it.** RetroBat publishes no per-`(emulator, core)` extension data anywhere: `es_features.cfg`
mentions "extension" 28 times and every one is an N64 controller pak. `.fds` is Famicom Disk
System and `.wad` is not a NES container at all, so neither is a claim about this row until a
launch proves it.

Observed to launch under `libretro`/`nestopia`: **`.zip`**, which is what all 228 synced games
are and what the one logged launch used. `.nes`, `.fds`, `.wad` and `.7z` are declared by the
system and unproven for this row.

**Nothing was excluded, which is the half of the step that matters and it passes.** The set
resolved 228 of 228 twice on 2026-09-20, once as a preview and once for real, with no game
dropped for its extension and none reported as refused.

**The step asks that nothing be excluded, not that a known-unsupported file be refused**, because
the risk runs that way. A file wrongly downloaded costs
bytes and a game that does not appear, because EmulationStation filters by `<extension>` itself. A
file wrongly **excluded** is a game the user asked for silently missing, with no error and no line
in the report worth questioning. The union list above is the reason: it cannot be precise per
`(emulator, core)`, so over-rejection is the likelier of the two errors, and `.wad` sitting in a
NES list is the proof that the list is not a statement about what any core will take.

**Where a step 2 has something real to check, it is placement.** Wave 2's disc systems carry
`.chd`, `.cue`, `.bin` and `.m3u` in one set, and over-filtering there drops real games. That is
where multi-disc and multi-file placement is settled, as [psx's step 2](../psx/index.md#2-multi-file-games-and-extensions) does.

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

## The set

Not a hand-picked set. A smart collection, which is an ordinary scope:

|          |                                                    |
| -------- | -------------------------------------------------- |
| Name     | Spinnich's Nintendo Entertainment System Favorites |
| Scope    | `smart_collection 9`                               |
| Policy   | no game cap, no size cap, ordered by recent        |
| Resolves | 228 games, 30.2 MB, into `nes`                     |

## Where each row stores a save

Measured on this install, not read from a declaration. Every battery save was checked for content
rather than existence: the smallest is 499 non-zero bytes over 52 distinct values, where a file a
core writes at boot is uniform.

| Row                    | Battery save                               | Save state                                                    | Screenshot                     |
| ---------------------- | ------------------------------------------ | ------------------------------------------------------------- | ------------------------------ |
| `libretro`/`fceumm`    | `saves/nes/<rom>.srm`                      | `saves/nes/libretro.fceumm/<rom>.state1`                      | `.png` beside the state        |
| `libretro`/`mesen`     | `saves/nes/<rom>.srm`                      | `saves/nes/libretro.mesen/<rom>.state1`                       | `.png` beside the state        |
| `libretro`/`nestopia`  | `saves/nes/<rom>.srm`                      | `saves/nes/libretro.nestopia/<rom>.state1`                    | `.png` beside the state        |
| `mednafen`/`nes`       | `saves/nes/<rom>.<md5>.sav`                | `saves/nes/mednafen/sstates/<rom>.<md5>.mc0`                  | none (RB-275)                  |
| `mesen` standalone     | `saves/nes/<rom>.sav`                      | `saves/nes/mesen/SaveStates/<rom>_1.mss`                      | none (RB-275)                  |
| `ares`/`Famicom`       | `saves/nes/ares/Famicom/<rom>.ram`         | `saves/nes/ares/Famicom/<rom>.bs1`                            | none (RB-275)                  |
| `bizhawk`/`NesHawk`    | `saves/nes/bizhawk/<display name>.SaveRAM` | `saves/nes/bizhawk/sstates/NesHawk/<rom>.QuickSave0.State`    | inside the state (RB-268)      |
| `bizhawk`/`quickerNES` | `saves/nes/bizhawk/<display name>.SaveRAM` | `saves/nes/bizhawk/sstates/quickerNES/<rom>.QuickSave0.State` | inside the state (RB-268)      |
| `jgenesis`             | `saves/nes/jgenesis/nes/<rom>.sav`         | `saves/nes/jgenesis/states/<rom>_0.jst`                       | none, though declared (RB-275) |

`<md5>` is of the ROM: Final Fantasy came out as `24ae5edf8375162f91a6846d3202e3d6`, which is the
`.nes` less its 16-byte iNES header, and mednafen adds it only when `<rom>.sav` does not already
exist (RB-273 and RB-274). BizHawk's `<display name>` is its own title for the game, region tag
dropped (RB-263). **`mesen` standalone is not the `mesen` libretro core**: the two rows share a name
and write different files.

**Three groups of rows share one battery file, and no other two rows do.** The three `libretro`
cores all write `saves/nes/<rom>.srm`, so switching core continues the same save: Kirby's
Adventure was played under `nestopia`, then under `fceumm`, and the second session carried on from
the first and rewrote the same 8,192 bytes. The two `bizhawk` cores read each other's `.SaveRAM`
(RB-271). `mednafen` reads a plain `<rom>.sav` whenever one exists, so it shares `mesen`
standalone's file, which goes up as `mesen:battery` (RB-273). Across any other pair of rows the
same game holds two unrelated saves in incompatible formats, and neither sees the other.

**So `nes` is class A on `libretro` and on nothing else.** `save_shapes.json` gives the system one
entry, `class A`, evidence `loose .srm per rom, libretro`. Every other row is carried by a battery
rule of its own in `save_rules.json`'s `battery_saves` table, and the states of `mednafen`, `mesen`
and `ares`, which `es_savestates.cfg` does not declare, by the bundled
`es_savestates.supplement.xml`.

## What the pass turned up

**Two server save records that cannot verify, and refusing them is correct.** A flush reported
`2 failed, 21 skipped` for `River City Ransom (USA)`:

```text
saves/nes/River City Ransom (USA).srm: what arrived hashes to 8a7f1ea8506132daf84235d79f1430c8
and the server said 387fab0e16d9b314e6fa4c95addf8f38. Nothing was written and the server was
not told it arrived.
```

Investigated rather than assumed. The server holds three saves for this ROM:

| id  | declared | served   | md5 of bytes  | `content_hash` |
| --- | -------- | -------- | ------------- | -------------- |
| 101 | `.srm`   | a ZIP    | `8a7f1ea8...` | `387fab0e...`  |
| 100 | `.zip`   | a ZIP    | `8a7f1ea8...` | `387fab0e...`  |
| 82  | `.srm`   | raw, 4 B | `37a6259c...` | `37a6259c...`  |

Both zips hold one 4-byte member, `River City Ransom (USA).srm`, whose content is literally
`null` and whose md5 is `37a6259c...`. So the declared `387fab0e...` is **neither the archive
bytes nor the member**, and it is not the member-wise fold either, which was computed and
checked. Record 82, older, has a `content_hash` that matches its bytes exactly.

**So these are two stale records left by another client**, their stored paths naming `freegosy`
and `fceumm`, and RomMBat did the right thing: refused, named both hashes, wrote nothing, and did
not acknowledge. Incidentally this shows #81 is fixed, since the message names both sides.

**One latent defect it exposed, which is RomMBat's.** Record 101 declares `ext=srm` and the
server serves ZIP bytes for it. `SaveSync.DownloadAsync` byte-hashes the download for class A and
moves it to the destination with no unwrapping, so had the hash matched it would have written a
ZIP to `saves/nes/River City Ransom (USA).srm`, which nestopia cannot read. The mismatch masked
it. The trigger here is bad data rather than ordinary operation, so this wants filing and
measuring against a save RomMBat itself uploaded, not a fix written from this record alone.

**Eighteen states on this server can never be placed on this install, and none of them is
RomMBat's.** Their slots name a core where `es_savestates.cfg` names an emulator: `fceumm`,
`genesis_plus_gx`, `snes9x`, `mgba`, `mupen64plus_next`, `pcsx_rearmed`, `beetle_psx_hw` and
`dc`. They were written by another client against the same library, the same one that left the
two stale `River City Ransom` saves. RomMBat names each one and the reason rather than dropping
it, and they do not move the exit code (#148). Four of the
eighteen are `nes` rows under `fceumm`, so **this is what a second `(emulator, core)` row's
states would look like to a restore** if RomMBat ever scoped one by core alone. It does not:
`ScopeOf` writes `{emulator}.{core}`, which is what makes these rows unplaceable and RomMBat's
own placeable.

**A stock 8.2.1 install ships 1,231 MAME nvram directories**, and `saves` reports every one as a
directory save that is not sent, with 1,531 files unsyncable for `no matching ROM`. Nothing here
is the user's, and no MAME ROM is on the device. Same class as #83.

## RomM's browser player, driven at `5.3.0-alpha.3`

On `libretro`/`nestopia` with The Legend of Zelda (USA) (Rev 1), rom 158633, the maintainer in
RomM's v2 player and at EmulationStation. RB-259 and RM-4 hold the
server's side; it re-runs no checklist step.

| Session                                      | What reached RetroBat                                                                            |
| -------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| A, resuming this client's `libretro:battery` | `1 down`, raw 8,192 B at the `.srm` nestopia reads, **loaded with the browser's new file on it** |
| RetroBat launch after A                      | Nestopia rewrote the file once; identical rewrites after that re-upload on every flush (#206)    |
| B, a fresh game filed under `autosave`       | **Overwrote the played save with no conflict**, then went up into `libretro:battery`             |
| B again, on the #205 fix                     | **`1 conflicted`, the `.srm` untouched**; `--keep-local` then sent it into `autosave`            |

B is #205, and the fix records it as a conflict instead of writing, confirmed by driving B again on it. The played save was put back
from the copy aside and is the current `libretro:battery` version again.

**Row 2's re-upload is an `alpha.3` reading and does not reproduce from `5.3.0-beta.1` on.**
Probe case M4 answers `no_op (Content is identical)`, so the hash settles it server-side
(RB-259). #210 guards the client side anyway, because the loop it would prevent is silent.

## What this file will not claim

- **All nine rows are certified, and only on this install at these two floors.** None of the
  rules or declarations the last four needed reached past `nes` when this was measured, so none
  of these emulators is certified anywhere else by this record, and each system they run on
  starts from nothing, as `megadrive` did.
- **Step 5's screenshot half was byte-checked where a screenshot exists**: as a `.png` for
  `libretro` and inside the state for `bizhawk`. `jgenesis`, `mesen`, `mednafen` and `ares` write
  none, so on those four the step is the state alone.
- **Six passes made one state per row outside EmulationStation**, the two `bizhawk` rows and the
  last four, through `emulatorLauncher` with the row's arguments, because the keyboard slot keys
  did not cross RDP. The state is the emulator's and any mirror `emulatorLauncher`'s either way, but
  the hooks did not run for those launches.
- **A plain `<rom>.sav` is shared by `mesen` and `mednafen`** and goes up as `mesen:battery` even
  when mednafen wrote it last. That is the file's owner by rule and not a claim about who played.
- The conflict results are about RomMBat's handling of a divergence. Both sides were synthesized,
  so nothing here is evidence that a real two-device race produces one, or how often.
- The class D download path is untested anywhere in the project and `nes` contains no class D.
- Media coverage, as step 7 records it, is a dated observation about this RomM library and
  moves when an administrator rescrapes. It is never a platform result.
