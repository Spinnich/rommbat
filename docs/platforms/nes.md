# nes

Nintendo Entertainment System / Famicom. RetroBat calls the folder `nes`, which is what this
file is named after.

**Not certified, on any of its nine rows.** Steps 1, 3, 4, 7, 8 and 9 hold and step 6 is N/A.
**Two remain open**: step 2's exclusion cannot be exercised on this platform, and step 5's
screenshot does not link, which is now a loss rather than a cosmetic gap, because a restore cannot
bring back what the state does not point at. A pass is not done at eight of nine.

**This file is in two parts.** The first is `libretro`/`nestopia` in full, which is the row the
nine steps were driven against. The second is the other eight rows, driven on steps 4 and 5 only,
which is where the save and state shapes live and where six of the nine turn out to write
something RomMBat does not sync. Read "The other eight rows" before trusting any sentence here
about `nes` as a whole, because most of them are about `libretro`.

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

## The move to `5.3.0-alpha.2`

**Every checklist result below was taken on RomM 5.2.0, and no step has been re-run on
`5.3.0-alpha.2`**, the floor the client now refuses anything below. One later observation, the
slot record in "Conflict resolution, driven both ways", was driven on it and says itself that it
re-runs no step. Mapped onto the nine steps per the
`platform-certification` skill's "When the floor moves", from the `src/` and `data/` diff across
#179 to #189 and `docs/romm-5.3-findings.md`:

| #   | At `5.3.0-alpha.2` | Why                                                                                                                                                         |
| --- | ------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | **Owed**           | `data/retrobat/platforms.json` was re-sourced from upstream's alias table (#166)                                                                            |
| 2   | **Owed**           | The resolver gained a no-file-on-disk exclusion ahead of the extension check (#167), and the index flag on a scoped page changed (#188)                     |
| 3   | Carried            | No firmware code or bundled BIOS data changed, and the findings doc records no firmware route change                                                        |
| 4   | **Owed**           | `SaveSync`, `SaveConflictResolver` and `SaveSlotStore` changed to refuse a superseded row the browser's writer revives (#170)                               |
| 5   | Carried            | `StateSync` and the states client changed in comments only, and the findings doc records no change to `POST /api/states`                                    |
| 6   | N/A                | Unchanged: `nes` has no class D                                                                                                                             |
| 7   | Carried            | No media or gamelist code changed. The row's new company split is not read, and the one server-side exporter change (#171) is a route RomMBat does not call |
| 8   | Carried            | No play-session code changed, and the findings doc records no change to `POST /api/play-sessions`                                                           |
| 9   | **Owed**           | Always owed on a move                                                                                                                                       |

So the record stands as a 5.2.0 measurement, and the row owes steps 1, 2, 4 and 9 before any
result here speaks for the current floor.

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
nothing to restore. **Since fixed by #147** in stage 2 of #195: the find scans the tree before it
reads `local_save`.

**`--apply` exited 7 with `failed 0`, which is #148.** Eighteen states on this server were
written by another client that scopes a state by core, and `es_savestates.cfg` names emulators,
so they can never be placed here. They are folded into the exit code, so on this install the
command cannot exit 0 no matter what it restores. `saves restore 158633` exits 0, because
narrowing to one ROM drops them. **Since fixed by #148** in stage 2 of #195: unplaceable rows are
listed and no longer move the exit code.

**Nothing in the tool surfaced the loss**, which is #142 and is unchanged by either PR.
`status --check-files` answered `1,316 recorded, all present`, which is exactly 228 ROMs plus
1,088 media. That sweep covers downloaded content and never looks at saves, so the save was
recoverable and still invisible. **Since fixed by #142** in stage 2 of #195: the sweep reads
`local_save` and names a missing save, and never repairs its row.

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

**The missing link is only half of it, and the other half is RomMBat's.** `RestorableState`
carries no screenshot member, `RestoreAsync` fetches `DownloadStateAsync` and nothing else, and
`RomMConnection.States` has an upload path for a screenshot with no download counterpart. So a
state whose `screenshot` field did link would still not bring its `.png` back today. Step 5 needs
#158 as well as the RomM-side link, and no `(system, emulator, core)` row can pass it on either
alone.

**Since closed on RomMBat's side by #158**, in stage 2 of #195: a restore now fetches a linked
screenshot into the declared `<image>`. Not re-driven on a linked screenshot, because none exists:
at the 5.3.0-alpha.2 floor all 9 `nes` states on the instance read `screenshot: null`, and 7 of 7
re-uploaded ones came back unlinked. The `.srm` and `.state1` of Crystalis (USA) were deleted and
restored byte-identically in the same pass. The RomM-side link is still what keeps step 5 open. Findings 138 and 256 in `docs/retrobat-findings.md`.

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

The hook chain fired on a fresh install with nobody at a terminal, which is rule 4 working. The
block is the earlier capture, taken before the Zelda launch, which is why it stops at 18:13:51:

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
it, which is right, but it also folds them into the exit code, which is #148 (since fixed in
stage 2 of #195, where they no longer move it). Four of the
eighteen are `nes` rows under `fceumm`, so **this is what a second `(emulator, core)` row's
states would look like to a restore** if RomMBat ever scoped one by core alone. It does not:
`ScopeOf` writes `{emulator}.{core}`, which is what makes these rows unplaceable and RomMBat's
own placeable.

**A stock 8.2.1 install ships 1,231 MAME nvram directories**, and `saves` reports every one as a
directory save that is not sent, with 1,531 files unsyncable for `no matching ROM`. Nothing here
is the user's, and no MAME ROM is on the device. Same class as #83.

## The other eight rows

`nes` declares **nine** `(emulator, core)` rows, and the one above is one of them. All nine have
now been driven by hand on this install: a real player battery save and a save state in each,
launched from EmulationStation, with the emulator confirmed from `emulatorLauncher.log` rather
than from configuration.

**This does not certify them.** Steps 4 and 5 are driven for all nine and steps 1, 3, 7, 8 and 9
carry across from the row above, but step 2 is unexercisable on this platform and step 5's
screenshot half fails, so every row stands where the first one does: eight of nine at best, and
six of the nine cannot sync what they wrote.

### How each row was selected

EmulationStation keeps a per-game emulator choice in **`gamelist.xml`**, as `<emulator>` and
`<core>` children of the `<game>` element, and not in `es_settings.cfg`. That was established by
setting one game through ES's own menu and diffing: `es_settings.cfg` came back byte-identical.

**Fourteen `nes["<rom>.zip"].emulator` keys written into `es_settings.cfg` were ignored.** They
survived ES's startup and exit rewrites untouched and were never read: five launches made under
them all ran the system-level `nes.emulator` / `nes.core` pair, `libretro` / `nestopia`. The
`<system>["<rom>"].<key>` chain is `emulatorlauncher`'s, and it covers feature keys; which emulator
runs is resolved by ES before `emulatorlauncher` exists. The skill now says so.

**Two rows declare no core and inherited `-core nestopia` from the system key.** `mesen` standalone
and `jgenesis` were both launched that way and both ignored it, running `Mesen.exe` and
`jgenesis-cli.exe`. For an emulator that declares no core, `-core` in the log is noise.

**`mesen` standalone is not the `mesen` libretro core**, and the two are separate rows that share a
name. They were driven on different games and wrote to different places, which is the clearest way
to see that they are not one thing.

### Where each row actually stores a save

Measured, not declared. Every battery save below was checked for content rather than existence:
the smallest is 499 non-zero bytes over 52 distinct values, where a file a core writes at boot is
uniform.

| Row                    | Battery save                               | Save state                                                    |
| ---------------------- | ------------------------------------------ | ------------------------------------------------------------- |
| `libretro`/`fceumm`    | `saves/nes/<rom>.srm`                      | `saves/nes/libretro.fceumm/<rom>.state1` + `.png`             |
| `libretro`/`mesen`     | `saves/nes/<rom>.srm`                      | `saves/nes/libretro.mesen/<rom>.state1` + `.png`              |
| `libretro`/`nestopia`  | `saves/nes/<rom>.srm`                      | `saves/nes/libretro.nestopia/<rom>.state1` + `.png`           |
| `mednafen`/`nes`       | `saves/nes/<rom>.<md5>.sav`                | `saves/nes/mednafen/sstates/<rom>.<md5>.mc0`                  |
| `mesen` standalone     | `saves/nes/<rom>.sav`                      | `saves/nes/mesen/SaveStates/<rom>_1.mss`                      |
| `ares`/`Famicom`       | `saves/nes/ares/Famicom/<rom>.ram`         | `saves/nes/ares/Famicom/<rom>.bs1`                            |
| `bizhawk`/`NesHawk`    | `saves/nes/bizhawk/<display name>.SaveRAM` | `saves/nes/bizhawk/sstates/NesHawk/<rom>.QuickSave0.State`    |
| `bizhawk`/`quickerNES` | `saves/nes/bizhawk/<display name>.SaveRAM` | `saves/nes/bizhawk/sstates/quickerNES/<rom>.QuickSave0.State` |
| `jgenesis`             | `saves/nes/jgenesis/nes/<rom>.sav`         | `saves/nes/jgenesis/states/<rom>_0.jst`                       |

`<md5>` is of the ROM: Final Fantasy came out as `24ae5edf8375162f91a6846d3202e3d6`.

**The screenshot half of that column was recorded for the three `libretro` rows only, and the gap
is not cosmetic here.** `es_savestates.cfg` declares an `<image>` for `bizhawk`
(`{{romfilename}}.QuickSave{{slot0}}.png`) and for `jgenesis` (`{{romfilename}}_{{slot0}}.png`), so
whether those five rows wrote one is a measurable fact this pass did not capture. The other three
declare no entry at all, so there is no `<image>` template to check them against and anything they
wrote would be in their own tree, unread for the same reason their states are. Step 5's screenshot
half
is this platform's open gap, so which rows write one decides how wide that gap is. It needs another
hands-on pass.

**So `nes` is class A on `libretro` and on nothing else.** `save_shapes.json` gives the system one
entry, `class A`, `provenance: observed`, evidence `loose .srm per rom, libretro`, and the evidence
string was already telling the truth: six of the nine rows write something else entirely, in four
different shapes. The file's own `_note` says shape is a property of `(system, emulator)`; this is
that note measured on one platform across every emulator it declares, and it is the first time the
claim has been checked rather than repeated.

**Only the three libretro rows share one battery file.** All three write
`saves/nes/<rom>.srm`, so switching core continues the same save: Kirby's Adventure was played
under `nestopia`, then under `fceumm`, and the second session carried on from the first and
rewrote the same 8,192 bytes. Across emulator families they are separate files in incompatible
formats, so the same game under `mesen` standalone and under `libretro` holds two unrelated saves
and neither sees the other.

### What RomMBat does with them

| Row                   | Battery save           | Save state          |
| --------------------- | ---------------------- | ------------------- |
| the three `libretro`  | synced, class A        | synced, core-scoped |
| `bizhawk`, both cores | **deferred**           | synced, core-scoped |
| `jgenesis`            | **deferred**           | synced              |
| `ares`                | **deferred**           | **invisible**       |
| `mednafen`            | **no shape claims it** | **invisible**       |
| `mesen` standalone    | **no shape claims it** | **invisible**       |

**Those two battery states are different things, and the distinction decides what to build next.**
`UnsyncableReason` separates them where the report's wording does not:

- **Deferred** is `NotInThisVersion`, defined as "The shape is understood and this build does not
  carry it. Stage 2's list." These are the emulators' own subdirectories under `saves/nes/`:
  `bizhawk/`, `jgenesis/nes/`. Understood, planned, not carried. `ares/Famicom/` is the same
  deferral and reports under `NoStateDeclaration` instead, because it holds a save state as well
  and that is the row that does not promise states sync.
- **No shape claims it** is `UnknownShape`. `mednafen` and `mesen` write their battery save **loose
  under `saves/nes/`**, beside the `.srm` files, which is structurally class A already. The only
  thing rejecting them is the extension: `save_rules.json` recognises `.bcr`, `.bkr`, `.brm` and
  `.srm`, and these two are `.sav`.

**The cheap-looking fix is a trap.** `save_rules.json` also hard-wires `loose_emulator` to
`libretro`, so adding `.sav` to the extension list alone would give mesen's `Crystalis (USA).sav`
the slot `libretro:battery` for that ROM and collide with libretro's own `Crystalis (USA).srm`,
which already syncs in that slot. This install holds both, because that game was driven under
`nestopia` and under `mesen` standalone. The extension list and `loose_emulator` have to stop being
independent globals first. Issue #151.

The battery column is a **reported** limitation and the right behaviour for this release. Nothing
is dropped silently. The pass that drove these nine rows read one `saves` row covering all five
directories: "ares, bizhawk, jgenesis, mednafen, mesen hold shared containers or a shape no
declaration covers. This release syncs the battery saves loose under `saves/nes/` (class A), the
save states beside them, and the directory saves the shape definition names."

**That last clause was false for three of the five, and that was the defect.** `mednafen`, `mesen`
and `ares` declare no `es_savestates.cfg` entry, and `StateScanner` finds states only from that
file, so the states they wrote are not scanned, not uploaded and not restorable. They were
mentioned, and that is the part to be precise about: `CountFiles` excludes only declared state
directories, so these three undeclared ones were counted and named by the same `AddSubdirectories`
row. A person with a save state was not told there was none. They were told the directory held
something unsyncable, under a sentence promising that "the save states beside them" sync, with the
state files folded into a count whose reason is about battery saves and shared containers.

Issue #150 split that row in two, on whether `es_savestates.cfg` names the emulator at all. `nes`
now reports `bizhawk, jgenesis` under the sentence above, and `ares, mednafen, mesen` under a
`no_state_declaration` row that repeats the shape half and ends "a save state written under these
is found only where RetroBat mirrors it into a declared path, and is otherwise not scanned, not
uploaded and not restorable". **Which is a correct message, not a fix.** The states are still
invisible, and making them syncable needs a bundled supplement carrying directory, filename and
slot per row. The reach past this platform is unchanged: 30 of wave 1's 81 rows are in that
family, and `docs/platforms/README.md` had been reading "declares no directory" as "writes no
state".

The split is untested against a real install. It was driven against the `nes` tree this pass
measured, reproduced as a fixture, and the shipped `es_savestates.cfg`; nobody has re-run `saves`
on the machine that produced the states.

Two smaller findings from the same pass:

- **BizHawk names a battery save after the display name**, dropping the region tag:
  `StarTropics (USA).zip` produced `bizhawk/StarTropics.SaveRAM`. Its own state sidecar spells the
  convention out, `StarTropics.NesHawk`, so the join key exists. Issue #151.
- **`saves` groups two unsyncable files under `nes/libretro`**, a directory that does not exist,
  and names no filenames. Issue #152.

### The hooks carried the whole session

Seventeen launches across eight emulators, with nobody at a terminal, and every `start` and `quit`
pass in `background.log` exited 0. The states were already uploaded by the `quit` hook's detached
pass before a flush was run by hand, which is what the arrangement is for. One line of that log
lost its first eighteen characters, which is issue #153.

## Conflict resolution, driven both ways

Driven on the two `libretro` rows, which are the only ones whose battery saves sync at all. Both
branches of `saves resolve` were exercised, plus the negotiate-driven download that had never been
driven before.

**Both sides of every conflict here were synthesized, and that bounds the claim.** The server side
was uploaded by hand and the local side was byte-edited, so what this proves is RomMBat's handling
of a divergence, not that a real two-device race produces one. No second device exists on this
install.

### Staging one is harder than it looks, and the reason is a finding

**A save uploaded through RomM's web UI cannot conflict with anything.** `slot` is an optional
query parameter on `POST /api/saves` and the web UI does not set it, so the upload lands with
`slot: null`, negotiate keys on the slot, and this device's record for `libretro:battery` never
goes stale. Measured: a web upload at 12:39 left the next flush uploading cleanly with no 409.

It is visible from the restore side, with the local file moved aside so every server row became a
candidate:

```console
$ rommbat-agent saves restore 158593
  save   rom 158593  libretro:battery          8 KB  2026-09-13 11:32  saves/nes/Kirby's Adventure (USA) (Rev 1).srm
  save   rom 158593  libretro:battery          8 KB  2026-09-13 11:38  saves/nes/Kirby's Adventure (USA) (Rev 1).srm
  save   rom 158593  (no slot)                 8 KB  2026-09-13 12:39  saves/nes/Kirby's Adventure (USA) (Rev 1).srm
  save   rom 158593  libretro:battery          8 KB  2026-09-13 12:46  saves/nes/Kirby's Adventure (USA) (Rev 1).srm
```

That is #138 from a second direction: a null-slot save is not only never fetched by negotiate, it
also **cannot conflict**, so a client that does not speak RomMBat's slot convention can never
collide with one. It is also #156, since all four rows resolve to one destination and the offer
says nothing about that. **Since fixed by #156** in stage 2 of #195: the offer is one row per
destination, the newest, and it names the rows it folded.

Staging one needs `POST /api/saves?slot=libretro:battery`. Note `device_id` is validated: an
invented one is refused with `404 Device with ID ... not found`, so the upload was made without it.

### What the machinery does

|                          | `--keep-local`, rom 158593            | `--keep-server`, rom 159313         |
| ------------------------ | ------------------------------------- | ----------------------------------- |
| Detection                | `1 conflicted`, nothing overwritten   | `1 conflicted`, nothing overwritten |
| Copy aside before acting | yes                                   | yes                                 |
| Outcome on disk          | local bytes kept                      | the server's `f78ab191` written     |
| On the server            | sent as save 212, **210 still stood** | 213 untouched                       |
| Copy aside afterwards    | pruned                                | pruned                              |

The conflict report names both hashes, the time it was first seen and the copy-aside path before
asking for a decision, and neither branch is a default:

```text
  rom 158593, slot libretro:battery, since 2026-09-13 12:57:52Z
    here    saves/nes/Kirby's Adventure (USA) (Rev 1).srm  7fda7607
    server  6799e326  2026-09-13 12:56:42Z
    a copy of the local file is at emulators/rommbat/replaced/20260913T125752-Kirby's ... .srm
```

**So `overwrite` means supersede, and the resolver's remark is confirmed on a second shape.** It
was measured on a `psp` class C unit during 7b-3; this is class A on `nes` and behaves the same.

### The download path works, and it had never been driven

Leaving one local file untouched while the server moved produced `1 down (8 KB)`: a save from
elsewhere came down through negotiate rather than through `saves restore`. **It also copied the
local file aside before overwriting**, so the copy-aside rule holds on the download path and not
only on conflicts.

That is the mechanism #155 is about, now known to work when it is not racing a launch.

### Two defects, #157

**A class A download leaves `save_slot` naming the superseded save.** After pulling save 211 down,
the row still read `save_id 209` with the pre-download hash, while 211's content sat on disk. It
does not self-correct: the local file is then in step, so the slot is never negotiated again.
`--keep-local` writes the row correctly. Nothing visibly breaks, because the server-side sync
record **is** updated, so the damage is confined to the device's picture of the server and is
silent.

**It is class A's, not every restore's, and keep-server is a second instance rather than an
inheritance.** `SaveSync.RestoreUnitAsync` and `SaveConflictResolver.FinishUnitAsync`, the class C
halves, both call `SaveSlots.RecordRestored`. `SaveSync.RecordRestored` and
`SaveConflictResolver.KeepServerAsync`, the class A halves, both write `local_save` and stop.
`KeepServerAsync` does not call the download path, so it is broken separately and a fix to the
download alone would leave it broken. Only class A was driven here, which is what this pass can
speak to; the class C recording is read from the code and from the 7b-3 measurement it cites.

**Both class A writers record the slot now**, fixed with the RomM 5.3.0 stage 4 work, which needed
the recorded save id to recognise a superseded row returning to the head of a slot. What is above
is the behaviour this pass measured before the fix. Driven after it on this row, with
`Destiny of an Emperor (USA)`: keep-server left `save_slot` naming the save it took, and a plain
download moved it to the newer save. Recorded in finding 4 of
[romm-5.3-findings.md](../romm-5.3-findings.md); it is not a re-run of any certification step.

**A download's copy aside is never pruned.** Both resolutions removed theirs. The plain download's
copy is still there with no decision to attach to it and no mechanism that will remove it.

## What this file will not claim

- The nine rows are driven on steps 4 and 5 and **none of them is certified**, because step 2
  cannot be exercised here and step 5's screenshot does not link on any of them.
- The conflict results are about RomMBat's handling of a divergence. Both sides were synthesized,
  so nothing here is evidence that a real two-device race produces one, or how often.
- Six of the nine cannot sync what they wrote, so a save made on those rows exists only on the
  device. Nothing here should be read as those rows working.
- The class D download path is untested anywhere in the project and `nes` contains no class D.
- Media coverage, once step 7 records it, is a dated observation about this RomM library and
  moves when an administrator rescrapes. It is never a platform result.
