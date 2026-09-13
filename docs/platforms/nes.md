# nes

Nintendo Entertainment System / Famicom. RetroBat calls the folder `nes`, which is what this
file is named after.

**Not certified.** Steps 1, 3, 4, 7, 8 and 9 hold and step 6 is N/A. **Two remain open**: step 2's
exclusion cannot be exercised on this platform, and step 5's screenshot does not link, which is
now a loss rather than a cosmetic gap, because a restore cannot bring back what the state does
not point at. A pass is not done at eight of nine.

## The row

|              |                                                                                                                                      |
| ------------ | ------------------------------------------------------------------------------------------------------------------------------------ |
| System       | `nes`                                                                                                                                |
| Emulator     | `libretro`                                                                                                                           |
| Core         | `nestopia`                                                                                                                           |
| Selected by  | **Explicit configuration, not RetroBat's default.** `nes.emulator = libretro` and `nes.core = nestopia` are set in `es_settings.cfg` |
| Confirmed by | `emulatorLauncher.log`, which logged `-system nes -emulator libretro -core nestopia` and ran `nestopia_libretro.dll`                 |
| Save class   | A, `provenance: observed` in `save_shapes.json`                                                                                      |

**This row is not the one a user gets out of the box.** With those two keys absent the choice
falls through to the first-listed emulator and core in `es_systems.cfg`, which is
`libretro`/`fceumm`. That is a different row and it is uncertified. Certifying under a forced
setting says nothing about the default, which is why the record names how the row was selected.

`libretro` and `bizhawk` are core-scoped, so this says nothing about `nes` under `fceumm`,
`mesen`, `bizhawk`/`NesHawk`, `bizhawk`/`quickerNES`, `mednafen`, `ares` or `jgenesis` either.

Three further `nes.*` keys were set at the same time and are recorded because they are part of
the configuration this row was measured under: `nes.nestopia_nospritelimit = 1`,
`nes.video_driver = vulkan`, `nes.xbox_layout = 1`. `nes.ungroup` was **removed** rather than set
to false, which is EmulationStation pruning a switch turned off, the behaviour finding 238
describes.

## The install this was measured on

|             |                                                                            |
| ----------- | -------------------------------------------------------------------------- |
| RetroBat    | `8.2.1-stable-win64`, the supported floor                                  |
| RomM        | 5.2.0, the supported floor                                                 |
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

## Checklist

| #   | Step                                                           | Result                                                                                    |
| --- | -------------------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| 1   | Folder mapping resolves, layer recorded                        | **Pass**, at layer `fs_slug`. See below                                                   |
| 2   | `<extension>` captured; unsupported file excluded and reported | **Partial, and not exercisable here.** The library is `.zip` throughout for this platform |
| 3   | Required BIOS resolved against RomM by md5                     | **Pass.** RetroBat requires no BIOS for `nes`                                             |
| 4   | Save shape classified, battery save round-trips                | **Pass, both directions.** Class A, and the md5 is equal up and down. See below           |
| 5   | Save state round-trips with its screenshot                     | **State yes, both ways. Screenshot no**, a finding 138 recurrence                         |
| 6   | Per-game memory card where class D applies                     | **N/A.** See below                                                                        |
| 7   | Launches from EmulationStation with art and metadata           | **Pass.** Box art confirmed rendering in the game list, metadata present                  |
| 8   | Play session recorded and reaches RomM                         | **Pass.** `last_played` updated, driven entirely through the hooks                        |
| 9   | Re-sync is a clean no-op                                       | **Pass.** 0 downloaded, 0 written, `gamelist.xml` byte-identical                          |

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

The other half of the step, that a file this folder cannot launch is excluded from the sync set
and reported, **cannot be exercised on this platform.** Every NES ROM in this library is `.zip`,
which is the sensible choice for the platform, so there is no unsupported file to offer and no
refusal to record. Manufacturing one by dropping a file into `roms/nes/` would test nothing
RomMBat does.

**This is recorded as a gap rather than a pass**, and it carries to a platform whose library
holds mixed formats. Wave 2's disc systems are the natural place, since `psx` and the CD systems
mix `.chd`, `.cue`, `.bin` and `.m3u` and a folder's `<extension>` refuses some of them.

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

## The set

Not a hand-picked set. A smart collection, which is an ordinary scope:

|          |                                                    |
| -------- | -------------------------------------------------- |
| Name     | Spinnich's Nintendo Entertainment System Favorites |
| Scope    | `smart_collection 9`                               |
| Policy   | no game cap, no size cap, ordered by recent        |
| Resolves | 228 games, 30.2 MB, into `nes`                     |

### 4. Battery save

Driven on **The Legend of Zelda (USA) (Rev 1)**, played for about two minutes from
EmulationStation, with an in-game save made on the save screen.

|             |                                                                                      |
| ----------- | ------------------------------------------------------------------------------------ |
| On disk     | `saves/nes/Legend of Zelda, The (USA) (Rev 1).srm`, 8,192 B                          |
| Shape       | **Class A confirmed**: a loose `.srm` under `saves/<system>/`, keyed by ROM filename |
| Local md5   | `0dda7bfc92305642b6120324911e0362`                                                   |
| Uploaded as | save **196**, `... [2026-09-12_18-27-13].srm`, 8,192 B                               |
| Server hash | `0dda7bfc92305642b6120324911e0362`, **equal to the local one**                       |

**Checked as content, not as existence.** No `.srm` existed for this system before the session,
and the file is 6,311 non-zero bytes of 8,192 carrying the save-slot name `LINK` at offset 2 in
Zelda's own character encoding. So this is a player save rather than a file the core wrote at
boot, which is the trap `save_shapes.json` records for `mastersystem` and which a size check
alone would not catch.

**The download half was driven twice, and the second time it worked.**

The first pass, before #136, deleted the backed-up `.srm` from the tree and found that neither
`flush` nor `sync` brought it back, which was **#84 reproduced deliberately** rather than
inferred. The mechanism was that save sync is scan-driven: candidates come from saves found on
disk, so a save with no local file was never considered. #136 added `saves restore`, which walks
the server's save list instead, and #140 did the same for states.

**The second pass, on the merge of both, brings it back byte for byte.** Same file, deleted from
the tree again after a backup and an md5:

```console
$ rommbat-agent saves restore --apply
  save   rom 158633  libretro:battery          8 KB  2026-09-12 18:27  saves/nes/Legend of Zelda, The (USA) (Rev 1).srm
  state  rom 158633  libretro.nestopia      10.2 KB  2026-09-12 18:27  saves/nes/libretro.nestopia/Legend of Zelda, The (USA) (Rev 1).state1
restored 1 save(s) and 1 state(s), failed 0, 18.2 KB
```

|                     | Before the delete                  | After the restore                  |
| ------------------- | ---------------------------------- | ---------------------------------- |
| `.srm`, 8,192 B     | `0dda7bfc92305642b6120324911e0362` | `0dda7bfc92305642b6120324911e0362` |
| `.state1`, 10,459 B | `2e4d4b06ade2cfd5cbe2f769531720e7` | `2e4d4b06ade2cfd5cbe2f769531720e7` |

**So step 4 closes in both directions**, and step 5's state half closes with it. A `flush`
afterwards reported `saves: 3 of 3 attributed` and `states: 1 already in step`, and
`saves restore 158633` then answered that everything the server holds for that ROM is already
here. No duplicate server row was created by the round trip.

Three things this pass adds, none of which stops step 4 passing.

**The first `saves restore` after the delete offered nothing, which is #147.** The save became
visible only after an unrelated `saves` run scanned the tree, because the restore asks
`local_save` what this device holds and nothing in the command rebuilds it. The state half of
the same run did not have the bug and offered its row immediately, which is what made the shape
obvious. A person whose save has just vanished runs this command first and is told there is
nothing to restore.

**`--apply` exited 7 with `failed 0`, which is #148.** Eighteen states on this server were
written by another client that scopes a state by core, and `es_savestates.cfg` names emulators,
so they can never be placed here. They are folded into the exit code, so on this install the
command cannot exit 0 no matter what it restores. `saves restore 158633` exits 0, because
narrowing to one ROM drops them.

**Nothing in the tool surfaced the loss**, which is #142 and is unchanged by either PR.
`status --check-files` answered `1,316 recorded, all present`, which is exactly 228 ROMs plus
1,088 media. That sweep covers downloaded content and never looks at saves, so the save was
recoverable and still invisible.

### 5. Save state and screenshot

Made in the same session, slot 1.

|            | On disk                                                                           | On the server               |
| ---------- | --------------------------------------------------------------------------------- | --------------------------- |
| State      | `saves/nes/libretro.nestopia/Legend of Zelda, The (USA) (Rev 1).state1`, 10,459 B | state **176**, 10,459 B     |
| Screenshot | `... .state1.png`, 3,367 B                                                        | screenshot **193**, 3,367 B |

**The declared `<directory>` is confirmed to be where nestopia really writes.**
`es_savestates.cfg` declares `{{system}}/libretro.{{core}}` for `libretro`, and
`saves/nes/libretro.nestopia/` is where the files appeared, created at first launch. The
filenames match `{{romfilename}}.state{{slot}}` and its `.png` sibling exactly.

**The uploaded name carries the core scope**, `Legend of Zelda, The (USA) (Rev 1)
[libretro.nestopia].state1`, which is what stops two cores writing one filename from becoming one
overwritten server row. Finding 134 proved that collision; this is the fix holding on a second
platform.

**The state comes back down as well as up**, measured in the same exercise as step 4: deleted
from the tree and restored by `saves restore --apply` at an identical md5, into the same declared
directory it was written from.

**The screenshot did not link, which is finding 138 recurring.** It uploaded, stored against the
ROM at the right name and size, and the state still reads `screenshot: null`. Finding 138
measured this at roughly a third of thirty-five attempts on `mastersystem` under
`genesis_plus_gx`; seeing it on `nes` under `nestopia` shows it is **not specific to a platform or
a core**. Nothing here suggests a RomMBat fault: the asset is on the server, correctly named.

**What changed is the cost of that gap.** While states only went up, an unlinked screenshot was
cosmetic. Now that a state comes back, the screenshot does not come with it: the restore has only
the state's own `screenshot` field to follow, that field is null, and screenshot 193 sits on the
server unreachable. `.state1.png` was the one file of the three that did not return. So step 5
stays open on the screenshot, and the reason it stays open is now a loss rather than an untidy
record.

**States carry no `content_hash` at all.** The state object has no such field, where the save has
one that matched. So a state cannot be verified on download the way a save can, and RomMBat has
nothing to compare against. The restore says so itself rather than implying a check it cannot
make, and it warns that a state carries emulator and core but no version. That is a property of
RomM's model rather than of this platform.

### 9. Re-sync

```text
plan:      nothing to do: all 228 games are already present and verified
done:      228 games already present, 0 downloaded, 0 written
media:     1088 already present
gamelists: all 1 unchanged
```

`gamelist.xml` was compared byte for byte before and after and is **identical**, rather than
taken from the command's own report. A second `flush` exited 0, re-attributed the same one save
and one state, and created **no duplicate server rows**: still save 196 and state 176 alone.

Two junk save records for `River City Ransom (USA)` failed every flush until they were deleted
server-side, which is what the earlier exit 7 was. Their removal is why this run is clean, and
that was confirmed against the server rather than assumed from the quieter output.

### 7. Launch, art and metadata

A game launched from EmulationStation after the sync, and `emulatorLauncher.log` records the
emulator and core, which is what makes the row's claim checkable rather than assumed.

`gamelist.xml` carries **228 `<game>` elements** with names and descriptions. On disk:

| Kind       | Files | Size   | Coverage of 228    |
| ---------- | ----- | ------ | ------------------ |
| ROMs       | 228   | 31 MB  | -                  |
| `images/`  | 665   | 166 MB | about 2.9 per game |
| `videos/`  | 216   | 362 MB | 94.7%              |
| `manuals/` | 207   | 404 MB | 90.8%              |

**Media is 30 times the ROM bytes here**, 932 MB against 31 MB, which is the ratio the rollout
warns a budget has to be sized for. This wave ran with the budget off, so nothing was truncated.

**Those coverage figures are a dated observation about this RomM library, not a platform result
and not a RomMBat capability.** They say when this platform was last scraped and with what
settings. Video at 94.7% here is well above the 72.1% finding 92 measured library-wide, and the
administrator is actively removing videos, so this number is expected to fall. An absent kind is
the ordinary `Missing` case.

**Box art renders correctly in the EmulationStation game list**, confirmed on screen rather than
inferred from files on disk, so step 7 passes.

**But the art is the wrong source, and that is a defect rather than an observation.** This
install has RetroBat's default `ScrapperImageSrc = sstitle`, meaning Title Screenshot.
`GameMetadata.cs:183-185` hardwires the three tags instead:

```csharp
Add(MediaKind.Image, row.CoverLargePath);
Add(MediaKind.Thumbnail, row.CoverSmallPath);
Add(MediaKind.Marquee, row.ScreenScraper?.LogoPath);
```

`path_cover_large` is RomM's cover, which it sources from ScreenScraper's 2D box: this library's
`url_cover` for a NES game ends `media=box-2D%28us%29` outright. So `<image>` is the box, the
user asked for the title screen, and `ss_metadata.title_screen_path` is populated and unread.

**RomMBat silently ignores a setting the user changed**, which is why this belongs above the
"observation" line. #108 already predicted this exact symptom and had confirmed it on two
installs; this is the third, and the first where a person noticed it from the game list rather
than from reading config. #108 also already designs the re-fetch a source change needs, by
recording which source filled a slot on the `local_file` row. What is arguably wrong there is the
`enhancement` label: the mapping is an enhancement, but "the setting does nothing" is a bug.

RomM exposes fourteen `ss_metadata` paths against the nine values the pickers offer, so the fix
is a mapping rather than new plumbing. Whether a given library actually holds a given kind is the
ordinary `Missing` case, per finding 239.

### 8. Play session

The hook chain fired on a fresh install with nobody at a terminal, which is rule 4 working:

```text
2026-09-12 18:01:34Z  start  background start started
2026-09-12 18:01:45Z  start  background start finished, flush exit 0
2026-09-12 18:13:46Z  quit   background quit started
2026-09-12 18:13:51Z  quit   background quit finished, flush exit 7
```

`start` and `quit` each spawned a detached pass; `game-start` and `game-end` journalled and
started nothing. **The step passes**, confirmed on the server rather than from the hook log:
`last_played` moved at 18:27:15 from a quit hook that fired at 18:27:07, and `now_playing`
cleared. An earlier revision of this section said the step was not passed, which was true of the
first accidental ten-second launch and was left standing after the Zelda session closed it.

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
it, which is right, but it also folds them into the exit code, which is #148. Four of the
eighteen are `nes` rows under `fceumm`, so **this is what a second `(emulator, core)` row's
states would look like to a restore** if RomMBat ever scoped one by core.

**A stock 8.2.1 install ships 1,231 MAME nvram directories**, and `saves` reports every one as a
directory save that is not sent, with 1,531 files unsyncable for `no matching ROM`. Nothing here
is the user's, and no MAME ROM is on the device. Same class as #83.

## What this file will not claim

- Nothing here is evidence about any other `(emulator, core)` row for `nes`.
- The class D download path is untested anywhere in the project and `nes` contains no class D.
- Media coverage, once step 7 records it, is a dated observation about this RomM library and
  moves when an administrator rescrapes. It is never a platform result.
