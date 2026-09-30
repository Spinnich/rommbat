---
summary: The certification record for `nes`: each row's standing at the floor, and the system's own steps.
read-when: Before certifying a `nes` row, or when asked whether a `nes` row works.
---

# nes

Nintendo Entertainment System / Famicom. RetroBat calls the folder `nes`, which is what this
file is named after.

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

**Step 5 closed on 2026-09-20 and is the first time any row has passed it.** RB-258's fix
was driven on a state made after it, and the restored screenshot was checked by its bytes rather
than by its arrival. Step 2 changed on the same day, from requiring an exclusion this library
cannot offer to requiring that nothing be excluded. See
[Re-driven at `5.3.0-beta.1`](libretro-nestopia.md#re-driven-at-530-beta1-2026-09-20).

## Where each row stands

**Every certified row holds at the floor, RomM `5.3.1` and RetroBat 8.2.1.** Steps 1 and 9 were
re-driven at `5.3.1` on 2026-09-24. The other steps carry from the drive at `5.3.0-beta.1`, since
nothing they exercise changed between the two. Nothing is owed.

| File                                         | What it holds                                                                                     |
| -------------------------------------------- | ------------------------------------------------------------------------------------------------- |
| This file                                    | Steps 1, 2 and 3, which are the system's; the set; what the pass turned up; RomM's browser player |
| [libretro-nestopia.md](libretro-nestopia.md) | `libretro`/`nestopia` in full, the row the nine steps were first driven against                   |
| [libretro.md](libretro.md)                   | The two other `libretro` rows                                                                     |
| [bizhawk.md](bizhawk.md)                     | The two `bizhawk` rows                                                                            |
| [standalone.md](standalone.md)               | `jgenesis`, `mesen`, `mednafen` and `ares`                                                        |
| [first-pass.md](first-pass.md)               | The eight rows after `nestopia` as first driven, on steps 4 and 5 only                            |
| [conflicts.md](conflicts.md)                 | Conflict resolution, driven both ways                                                             |
| [facts.md](facts.md)                         | The measured facts about `nes`'s emulators, with RB- IDs                                          |

## The install this was measured on

|             |                                                                            |
| ----------- | -------------------------------------------------------------------------- |
| RetroBat    | `8.2.1-stable-win64`, the supported floor                                  |
| RomM        | 5.2.0, the floor at the time                                               |
| Root        | `R:\RetroBat`, found by walking up from the executable                     |
| Store       | schema 14 of 14, WAL                                                       |
| Budget      | `none`. A 2 GB free-space floor still applies; NTFS, 927.4 GB free         |
| Media kinds | `ScrapeVideos` and `ScrapeManual` both `true`, as a fresh 8.2.1 ships them |

**This record spans two sessions and two builds**, and the difference matters to step 4. The
first ran before `saves restore` existed and measured the download half as broken. The second ran
on a deploy of `main` carrying #136 and #140, made by `tools/publish.ps1 -Deploy R:\RetroBat`,
and measured it working. Where the two disagree the second one is the result, and the file says
which is which rather than quietly replacing the earlier text.

The budget being off is deliberate and narrows what this pass proves: **nothing here certifies
`budget`, `evict` or the eviction guards.** None of those is among the nine steps. It also means
a missing cover at step 7 cannot be a headroom problem, which is why it was switched off.

**The 2026-09-20 re-drive ran on the same install moved forward**, and the two rows that changed
are the ones a result can turn on: the server reports `5.3.0-beta.1` and the store is at schema
16 of 16. RetroBat, root, budget and media kinds are as above. The client is a deploy of `main`
at 9ed1fd1, which is the first build here to declare `beta.1` as its floor rather than to
tolerate it.

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

**The step used to ask for the reverse, and held this row open for a year of calendar time over a
property of the library.** It required a known-unsupported file to be excluded and reported, and
every NES ROM here is `.zip`, so there was nothing to refuse. Changed on 2026-09-20 after the
question was put to the maintainer: the risk runs the other way. A file wrongly downloaded costs
bytes and a game that does not appear, because EmulationStation filters by `<extension>` itself. A
file wrongly **excluded** is a game the user asked for silently missing, with no error and no line
in the report worth questioning. The union list above is the reason: it cannot be precise per
`(emulator, core)`, so over-rejection is the likelier of the two errors, and `.wad` sitting in a
NES list is the proof that the list is not a statement about what any core will take.

**The exclusion half is not abandoned, it moved to where it is real.** Wave 2's disc systems carry
`.chd`, `.cue`, `.bin` and `.m3u` in one set with no manufacturing required, and over-filtering
there drops real games. That is also where multi-disc and multi-file placement has to be settled,
which is the thing this step was a poor proxy for.

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
it, which is right, but it also folds them into the exit code, which is #148 (since fixed in
stage 2 of #195, where they no longer move it). Four of the
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

**Row 2's re-upload does not reproduce at `5.3.0-beta.1` or `5.3.0`**, and the table is left as it
was measured on `5.3.0-alpha.3`. Probe case M4 answers `no_op (Content is identical)` there, so
the hash settles it server-side; RB-259 carries the re-check. #210 guards the client side
anyway, because the loop it would prevent is silent.

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
- Media coverage, once step 7 records it, is a dated observation about this RomM library and
  moves when an administrator rescrapes. It is never a platform result.
