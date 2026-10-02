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
`libretro`/`fceumm`, a different row with [its own record](libretro.md). Certifying under a forced
setting says nothing about the default, which is why the record names how the row was selected.

`libretro` and `bizhawk` are core-scoped, so this says nothing about `nes` under `fceumm`,
`mesen`, `bizhawk`/`NesHawk`, `bizhawk`/`quickerNES`, `mednafen`, `ares` or `jgenesis` either.

Three further `nes.*` keys were set at the same time and are recorded because they are part of
the configuration this row was measured under: `nes.nestopia_nospritelimit = 1`,
`nes.video_driver = vulkan`, `nes.xbox_layout = 1`. `nes.ungroup` was **removed** rather than set
to false, which is EmulationStation pruning a switch turned off, the behaviour RB-238
describes.

## Checklist

Eight steps were driven on 2026-09-20 in one session, against a deploy of `main` at 9ed1fd1 made
by `tools/publish.ps1 -Deploy R:\RetroBat`, on a server reporting `5.3.0-beta.1`. **Step 3 carries
from 5.2.0**: RetroBat lists no firmware for `nes` and the bundled manifest carries no entry for
it, so `bios nes` reads an empty requirement whichever build asks. All nine hold at the floor, as
[Where each row stands](index.md#where-each-row-stands) says.

| #   | Step                                                          | Result                                                                             |
| --- | ------------------------------------------------------------- | ---------------------------------------------------------------------------------- |
| 1   | Folder mapping resolves, layer recorded                       | **Pass**, at layer `fs_slug`. See [the system's steps](index.md#1-mapping)         |
| 2   | `<extension>` captured; every resolved ROM survives the check | **Pass.** 228 of 228 resolved, nothing excluded                                    |
| 3   | Listed BIOS resolved against RomM by md5                      | **Pass**, carried. RetroBat requires no BIOS for `nes`                             |
| 4   | Save shape classified, battery save round-trips               | **Pass, both directions.** Class A, and the md5 is equal up and down. See below    |
| 5   | Save state round-trips with its screenshot                    | **Pass, both ways, screenshot included.** See below                                |
| 6   | Per-game memory card where class D applies                    | **N/A.** See below                                                                 |
| 7   | Launches from EmulationStation with art and metadata          | **Pass.** Box art confirmed rendering in the game list, metadata present           |
| 8   | Play session recorded and reaches RomM                        | **Pass.** Session 265 on the server, matching the journal to the second. See below |
| 9   | Re-sync is a clean no-op                                      | **Pass.** 0 downloaded, 0 written, `gamelist.xml` byte-identical                   |

### 4. Battery save

Driven on **The Legend of Zelda (USA) (Rev 1)**, with an in-game save made in an
EmulationStation session.

|                      | Value                                                                                |
| -------------------- | ------------------------------------------------------------------------------------ |
| On disk              | `saves/nes/Legend of Zelda, The (USA) (Rev 1).srm`, 8,192 B                          |
| Shape                | **Class A confirmed**: a loose `.srm` under `saves/<system>/`, keyed by ROM filename |
| Before the session   | `5867d7af3f402454c85464aa84b3457f`                                                   |
| After it             | `620bd04701e61863cae7c18257a95cde`, so the save did change                           |
| Uploaded             | `libretro:battery`, by the detached `quit` pass, with nobody at a terminal           |
| Deleted and restored | `620bd04701e61863cae7c18257a95cde`, **equal**                                        |

**Checked as content, not as existence.** The file carries the save-slot name `LINK` at offset 2
in Zelda's own character encoding, so it is a player save rather than a file the core wrote at
boot, which is the trap `save_shapes.json` records for `mastersystem` and which a size check alone
would not catch.

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

Three states made in one EmulationStation session:

| Slot                  | On disk                                               | Screenshot md5 |
| --------------------- | ----------------------------------------------------- | -------------- |
| `libretro:nestopia:2` | `Legend of Zelda, The (USA) (Rev 1).state2`, 10,252 B | `ecd1d57f...`  |
| `libretro:nestopia:3` | `Legend of Zelda, The (USA) (Rev 1).state3`, 10,253 B | `d75aca69...`  |
| `libretro:nestopia:4` | `Legend of Zelda, The (USA) (Rev 1).state4`, 10,242 B | `d75aca69...`  |

**The declared `<directory>` is confirmed to be where nestopia really writes.**
`es_savestates.cfg` declares `{{system}}/libretro.{{core}}` for `libretro`, and
`saves/nes/libretro.nestopia/` is where the files appeared. The filenames match
`{{romfilename}}.state{{slot}}` and its `.png` sibling exactly.

**The uploaded name carries the core scope**, `Legend of Zelda, The (USA) (Rev 1)
[libretro.nestopia].state2`, which is what stops two cores writing one filename from becoming one
overwritten server row. RB-134 proved that collision; this is the fix holding on a second
platform. The screenshot goes up as that name plus `.png`, which is what RomM's lookup by name
links to the state, and a restore fetches a linked screenshot into the declared `<image>` (RB-258,
#158).

**The slot is RetroArch's choice, not EmulationStation's.** `emulatorLauncher.log` logged
`-state_slot 2` on the launch and the first state landed in slot 2, but that is a coincidence:
RetroArch runs with `savestate_auto_index` on and continues from the highest slot already in the
core's directory, and `libretro.nestopia/` already held slot 1. The `libretro`/`mesen` row shows it
plainly, with `-state_slot 5` on the launch line and states written as `.state1` and `.state2`.
RB-261.

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
this state and not to a neighbour.

**States carry no `content_hash` at all.** The state object has no such field, where the save has
one that matched. So a state cannot be verified on download the way a save can, and RomMBat has
nothing to compare against. The restore says so itself rather than implying a check it cannot
make, and it warns that a state carries emulator and core but no version. That is a property of
RomM's model rather than of this platform.

### 6. Per-game memory card

**N/A, and recorded rather than left blank.** `save_shapes.json` gives `nes` no
`DependsOnEmulator` flag and no `per_game_conversion` block, so there is no shared container to
opt a game out of. This holds for every wave 1 system.

### 7. Launch, art and metadata

A game launched from EmulationStation after the sync, and `emulatorLauncher.log` records the
emulator and core, which is what makes the row's claim checkable rather than assumed. **Box art
renders correctly in the EmulationStation game list**, confirmed on screen rather than inferred
from files on disk, and the played game's `gamelist.xml` entry carries `<image>`, `<marquee>`,
`<video>`, `<manual>` and `<desc>`. `gamelist.xml` carries **228 `<game>` elements** with names
and descriptions. On disk:

| Kind       | Files | Size   | Offered by the server  |
| ---------- | ----- | ------ | ---------------------- |
| ROMs       | 228   | 32 MB  | -                      |
| `images/`  | 676   | 168 MB | 224 of each of 3 kinds |
| `videos/`  | 216   | 362 MB | **8**                  |
| `manuals/` | 215   | 423 MB | 209                    |

**Media is 30 times the ROM bytes here**, 953 MB against 32 MB, which is the ratio the rollout
warns a budget has to be sized for. This install runs with the budget off, so nothing was
truncated.

**Those coverage figures are a dated observation about this RomM library, not a platform result
and not a RomMBat capability.** The sync's media line is 889, which is 224 + 224 + 224 + 209 + 8.
The 208 videos on disk the server no longer offers were removed by the administrator: their
`path_video` reads null, and their `ss_metadata` carries `video_path` and `video_normalized_path`
both null while still holding the ScreenScraper URLs the asset would be fetched from. The eight it
offers answer with a `video_normalized/video-normalized.mp4` path. An absent kind is the ordinary
`Missing` case.

**The files already on disk stay, and the gamelist still points at them.** `Removed` only fires
when a _kind_ stops being wanted, which is a setting the install owns; a kind that is still wanted
and merely no longer offered is the ordinary `Missing` case and reclaims nothing. That is the
right way round, because the artwork is still good and the alternative is deleting a user's media
whenever their server has a gap.

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

**RomMBat silently ignores a setting the user changed**, which is #108. That issue designs the
re-fetch a source change needs, by recording which source filled a slot on the `local_file` row.
RomM exposes fourteen `ss_metadata` paths against the nine values the pickers offer, so the fix
is a mapping rather than new plumbing. Whether a given library actually holds a given kind is the
ordinary `Missing` case, per RB-239.

### 8. Play session

The hook chain fired with nobody at a terminal, which is rule 4 working:

```text
2026-09-20 14:20:50Z  start  background start started
2026-09-20 14:20:55Z  start  background start finished, flush exit 0
2026-09-20 14:23:47Z  quit   background quit started
2026-09-20 14:23:54Z  quit   background quit finished, flush exit 0
```

**`background.log` appends**: the file carries every pass back to 2026-09-12, with these entries
at the end.

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

**Reading a session back needs a token for the account the install is paired as.**
`GET /api/play-sessions` is scoped to the authenticated user: with the correct device id above, a
second account's token answers `200` with **0** rows where the owning account's answers `200`
with **5**. That is identity rather than permission, and no scope changes it. This step was
settled with a read-only `roms.user.read` token on the owning account, under which
`GET /api/roms/{id}` is `403`, so `last_played` was never read; the session row is the stronger
evidence anyway. `status`'s `Playtime` block reads the same rows, filtered by the `romm device`
id, and reports an empty answer as found-nothing rather than sent-nothing because of these two
traps (#208).

### 9. Re-sync

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
