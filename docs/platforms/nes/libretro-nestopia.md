---
summary: The certification record for `nes`: `libretro`/`nestopia` in full, the row the nine steps were first driven against.
read-when: When a result for one of these `nes` rows is needed, or before re-driving one.
---

# nes: `libretro`/`nestopia`, the worked example

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
to false, which is EmulationStation pruning a switch turned off, the behaviour RB-238
describes.

## Re-driven at `5.3.0-beta.1`, 2026-09-20

**Seven steps were re-run in one session against a deploy of `main` at 9ed1fd1**, made by
`tools/publish.ps1 -Deploy R:\RetroBat`, on a server reporting `5.3.0-beta.1`. That build refuses
any server below `beta.1`, so it carries the floor it was measured against.

| #   | Owed for                         | Re-run result                                                     |
| --- | -------------------------------- | ----------------------------------------------------------------- |
| 1   | The re-sourced alias table       | **Pass.** Both rows resolve as before, `fs_slug` and `bundled`    |
| 2   | The no-file-on-disk exclusion    | **Pass.** 228 of 228 resolve and nothing is excluded              |
| 4   | The superseded-row guard         | **Pass, both directions.** A new save round-tripped byte for byte |
| 5   | The screenshot fetch and RB-258  | **Pass, the screenshot linked.** See below                        |
| 7   | The retired identifiers endpoint | **Pass.** Art on screen, and the game list is unchanged           |
| 8   | The append-only background log   | **Pass.** See below                                               |
| 9   | Always owed on a move            | **Pass.** 0 downloaded, 0 written, `gamelist.xml` byte-identical  |

**Step 3 carries from 5.2.0 and was not re-run.** RetroBat lists no firmware for `nes`, and the
bundled manifest carries no entry for it, so `bios nes` reads an empty requirement whichever build
asks.

**Step 5 rests on this session.** RB-258's fix shows only on a state made after it, because an
unchanged state is never re-sent, so one was made and driven here.

## Checklist

All nine were stated at `5.3.0-beta.1`, the floor the client declared then, and hold at the
floor, as [Where each row stands](index.md#where-each-row-stands) says. Step 3 is carried from the
5.2.0 measurement, because `nes` requires no BIOS and no firmware code or bundled data changed; the
other eight were measured or re-measured on 2026-09-20.

| #   | Step                                                          | Result                                                                                     |
| --- | ------------------------------------------------------------- | ------------------------------------------------------------------------------------------ |
| 1   | Folder mapping resolves, layer recorded                       | **Pass**, at layer `fs_slug`. See below                                                    |
| 2   | `<extension>` captured; every resolved ROM survives the check | **Pass.** 228 of 228 resolved, nothing excluded. The step changed on 2026-09-20, see below |
| 3   | Listed BIOS resolved against RomM by md5                      | **Pass**, carried. RetroBat requires no BIOS for `nes`                                     |
| 4   | Save shape classified, battery save round-trips               | **Pass, both directions.** Class A, and the md5 is equal up and down. See below            |
| 5   | Save state round-trips with its screenshot                    | **Pass, both ways, screenshot included.** See below                                        |
| 6   | Per-game memory card where class D applies                    | **N/A.** See below                                                                         |
| 7   | Launches from EmulationStation with art and metadata          | **Pass.** Box art confirmed rendering in the game list, metadata present                   |
| 8   | Play session recorded and reaches RomM                        | **Pass.** Session 265 on the server, matching the journal to the second. See below         |
| 9   | Re-sync is a clean no-op                                      | **Pass.** 0 downloaded, 0 written, `gamelist.xml` byte-identical                           |

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

#### The save re-driven at `5.3.0-beta.1`, 2026-09-20

Owed because `SaveSync`, `SaveConflictResolver` and `SaveSlotStore` all changed across the move.
Same game, a second in-game save in a fresh session.

|                      | Value                                                               |
| -------------------- | ------------------------------------------------------------------- |
| Before the session   | `5867d7af3f402454c85464aa84b3457f`, 8,192 B                         |
| After it             | `620bd04701e61863cae7c18257a95cde`, 8,192 B, so the save did change |
| Uploaded             | by the detached `quit` pass, with nobody at a terminal              |
| Deleted and restored | `620bd04701e61863cae7c18257a95cde`, **equal**                       |

The restore named it `the newest of 8 server saves for this file` and listed the seven it did
not restore, three of them in `autosave` from the browser sessions in "RomM's browser player".
**A flush afterwards re-uploaded nothing**: `states: 15 already in step` and `playtime: nothing
queued`, exit 0. So the restore wrote its `local_save` and `local_state` rows back rather than
leaving a file the next scan would treat as new, which is the failure mode #206 describes for an
identical rewrite.

**A save carries no screenshot in RomM's UI, and that is correct rather than a gap.** The
maintainer noticed it beside the states, which do carry one. `POST /api/saves` does take an
optional `screenshotFile` and `SaveSchema` requires a `screenshot` member, so the model allows
it; what is absent is anything to send. RetroArch writes a `.png` beside a save state and never
beside an `.srm`, and the agent runs after the emulator has exited, so it has no framebuffer to
capture. The one `autosave` row on this ROM that does show a thumbnail was written by RomM's
browser player, which captures its own canvas at save time. `RomMConnection.Saves.cs` says as
much at the upload.

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
overwritten server row. RB-134 proved that collision; this is the fix holding on a second
platform.

**The state comes back down as well as up**, measured in the same exercise as step 4: deleted
from the tree and restored by `saves restore --apply` at an identical md5, into the same declared
directory it was written from.

**The screenshot of a state uploaded before RB-258's fix does not link.** RomMBat uploaded this
one as `Legend of Zelda, The (USA) (Rev 1).state1 [libretro.nestopia].png`, which RomM's lookup
by name can never match against the state's name, so the state reads `screenshot: null` and
a restore has nothing to follow. The image now goes up as the state's upload name plus `.png`,
and a restore fetches a linked screenshot into the declared `<image>` (#158). Screenshot 193
stays unlinked, because an unchanged state is not re-sent, so step 5 rests on a state made after
the fix.

**States carry no `content_hash` at all.** The state object has no such field, where the save has
one that matched. So a state cannot be verified on download the way a save can, and RomMBat has
nothing to compare against. The restore says so itself rather than implying a check it cannot
make, and it warns that a state carries emulator and core but no version. That is a property of
RomM's model rather than of this platform.

#### Driven on a state made after the fix, 2026-09-20, and it links

Everything above this heading describes states uploaded before RB-258 was fixed. Three new
ones were made in one EmulationStation session and they are what the step now rests on.

| Slot                  | On disk                                               | Screenshot md5 |
| --------------------- | ----------------------------------------------------- | -------------- |
| `libretro:nestopia:2` | `Legend of Zelda, The (USA) (Rev 1).state2`, 10,252 B | `ecd1d57f...`  |
| `libretro:nestopia:3` | `Legend of Zelda, The (USA) (Rev 1).state3`, 10,253 B | `d75aca69...`  |
| `libretro:nestopia:4` | `Legend of Zelda, The (USA) (Rev 1).state4`, 10,242 B | `d75aca69...`  |

**The slot is RetroArch's choice, not EmulationStation's, and this paragraph first said the
opposite.** `emulatorLauncher.log` logged `-state_slot 2` on the launch and the first state of the
session landed in slot 2, which was read as ES deciding it. The `libretro`/`mesen` pass on
2026-09-21 disproved that: `-state_slot 5` on the launch line, states written as `.state1` and
`.state2`. RetroArch runs with `savestate_auto_index` on and continues from the highest slot
already in the core's directory, and `libretro.nestopia/` already held slot 1 here, so slot 2 was
RetroArch's pick that happened to match. RB-261.

Slot 2 was deleted from the tree, with its `.png`, and restored:

```console
$ rommbat-agent saves restore 158633
  state  rom 158633  libretro.nestopia        10 KB  2026-09-20 14:23  saves/nes/libretro.nestopia/Legend of Zelda, The (USA) (Rev 1).state2
         with its screenshot, Legend of Zelda, The (USA) (Rev 1).state2.png
$ rommbat-agent saves restore 158633 --apply
restored 1 save(s) and 1 state(s), failed 0, 20.1 KB, with 1 screenshot(s)
```

|                        | Before the delete | After the restore |
| ---------------------- | ----------------- | ----------------- |
| `.state2`, 10,252 B    | `4e257f3b...`     | `4e257f3b...`     |
| `.state2.png`, 2,144 B | `ecd1d57f...`     | `ecd1d57f...`     |

**The screenshot was checked by its bytes, not by its arrival**, which is the check RB-258
makes necessary: RomM can answer a libretro slot with another slot's image, and a restore that
merely produces a `.png` would not notice. Slots 3 and 4 were saved on the same frame and share
one image, `d75aca69...`; slot 2's is its own. What came back is `ecd1d57f...`, so the link is to
this state and not to a neighbour, and not to the slot 1 image that has sat unlinked here since
September 12.

**Slot 1 is still unlinked and is left that way on purpose.** It was uploaded before the fix, an
unchanged state is never re-sent, and leaving it is what makes the contrast between the two
readable on one install.

### 6. Per-game memory card

**N/A, and recorded rather than left blank.** `save_shapes.json` gives `nes` no
`DependsOnEmulator` flag and no `per_game_conversion` block, so there is no shared container to
opt a game out of. This holds for every wave 1 system.

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
settings. Video at 94.7% here is well above the 19.1% RB-92 measures library-wide, and the
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
ordinary `Missing` case, per RB-239.

#### Re-driven at `5.3.0-beta.1`, 2026-09-20, and the video prediction came true

Owed because the catalog query changed when `GET /api/roms/identifiers` was retired, and that
query is what fills the game list. It passes: box art was confirmed on screen again, and the
played game's `gamelist.xml` entry carries `<image>`, `<marquee>`, `<video>`, `<manual>` and
`<desc>`.

On disk, against the earlier capture:

| Kind       | Files | Size   | Offered by the server now |
| ---------- | ----- | ------ | ------------------------- |
| ROMs       | 228   | 32 MB  | -                         |
| `images/`  | 676   | 168 MB | 224 of each of 3 kinds    |
| `videos/`  | 216   | 362 MB | **8**                     |
| `manuals/` | 215   | 423 MB | 209                       |

**The sync's media line fell from 1,088 to 889, and that is the administrator's deletion, not a
regression.** The paragraph above predicted exactly this. Traced rather than assumed: for 208 of
the 216 games with a video on disk, `path_video` now reads null, and their `ss_metadata` carries
`video_path` and `video_normalized_path` both null while still holding the ScreenScraper URLs
the asset would be fetched from. The eight survivors answer with a
`video_normalized/video-normalized.mp4` path. 224 + 224 + 224 + 209 + 8 is 889 exactly.

**The files already on disk stayed, and the gamelist still points at them.** `Removed` only fires
when a _kind_ stops being wanted, which is a setting the install owns; a kind that is still wanted
and merely no longer offered is the ordinary `Missing` case and reclaims nothing. That is the
right way round, because the artwork is still good and the alternative is deleting a user's media
whenever their server has a gap.

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
cleared.

#### The session re-driven at `5.3.0-beta.1`, 2026-09-20

Owed because the detached pass the hooks spawn writes its log through a new append-only handle.
The whole chain fired again, with nobody at a terminal:

```text
2026-09-20 14:20:50Z  start  background start started
2026-09-20 14:20:55Z  start  background start finished, flush exit 0
2026-09-20 14:23:47Z  quit   background quit started
2026-09-20 14:23:54Z  quit   background quit finished, flush exit 0
```

**The append-only handle appends**, which is the half this move owed: the file above carries
every pass back to 2026-09-12 with the new entries at the end, and the truncation visible in the
2026-09-12 block is older than the change.

The journal holds the session the hooks captured, and the correlation closed it:

| Event        | Recorded     | Correlated   | State        |
| ------------ | ------------ | ------------ | ------------ |
| `start`      | 14:20:50.841 | 14:20:51.028 | `correlated` |
| `game-start` | 14:21:42.517 | 14:23:47.876 | `correlated` |
| `game-end`   | 14:23:45.327 | 14:23:47.876 | `correlated` |
| `quit`       | 14:23:47.690 | 14:23:47.876 | `correlated` |

`game-start` carries `roms/nes/Legend of Zelda, The (USA) (Rev 1).zip` and nothing else does,
which is rule 4 holding: the two hooks inside the launch path journalled and started nothing,
and the two outside it each spawned the pass that drained what they left. EmulationStation's own
write agrees, at `<playcount>4</playcount>` and `<lastplayed>20260920T102345</lastplayed>`, the
`game-end` second.

**The server holds it, and that is what makes the step pass:**

| Field             | Session 265           | Matches                           |
| ----------------- | --------------------- | --------------------------------- |
| `start_time`      | `2026-09-20T14:21:42` | `game-start`, journalled at .517  |
| `end_time`        | `2026-09-20T14:23:45` | `game-end`, journalled at .327    |
| `duration_ms`     | `122809`              | the 123 s between them            |
| `device_id`       | `cf1cc550-...`        | `status`'s **`romm device`** line |
| `sync_session_id` | `null`                | -                                 |

**The row carries the RomM-side device id, not the local one**, and the two are different values
that `status` prints on adjacent lines. A `?device_id=` filter given the local id returns `200`
with zero rows, which reads exactly like a session that was never written.

**Reading it back needs a token for the account the install is paired as, and that is #208.**
`SendPlaySessionsAsync` posts and nothing in the client reads back, so `playtime: nothing queued`
says the outbox is empty rather than that a row exists. `GET /api/play-sessions` is scoped to the
authenticated user: with the correct device id above, a second account's token answers `200` with
**0** rows where the owning account's answers `200` with **5**. That is identity rather than
permission, and no scope changes it. A read-only `roms.user.read` token on the owning account is
what settled this step, and `GET /api/roms/{id}` is `403` under that scope, so `last_played` was
never read; the session row is the stronger evidence anyway.

**Closed since, and this paragraph is what it was measured against.** #208 added the read:
`status` now prints a `Playtime` block with the count and the last session, filtered by the
`romm device` id above, so a later pass settles step 8 from the agent rather than from a
second token. The two traps recorded here are unchanged and are why the block reports an empty
answer as found-nothing rather than sent-nothing.

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

#### The re-sync re-driven at `5.3.0-beta.1`, 2026-09-20

Run twice, once before the play session and once after it, and clean both times:

```text
plan:      nothing to do: all 228 games are already present and verified
done:      228 games already present, 0 downloaded, 0 written
media:     889 already present
gamelists: all 1 unchanged
```

`gamelist.xml` was md5'd either side of each run and is identical across both, exit 0.

**It is not identical across the session, and the writer is EmulationStation.** The file went
from `5a35c317...` to `585d259f...` between the two runs, because ES rewrote the played game's
`<playcount>` to 4 and its `<lastplayed>` to `20260920T102345`, which is the `game-end` second.
RomMBat reported `all 1 unchanged` on both sides of that and left the file alone, so the churn
step 9 watches for is absent; a record that md5'd only across the whole session would have read
ES's own write as a sync defect.
