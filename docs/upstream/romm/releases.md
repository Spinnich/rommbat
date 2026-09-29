---
summary: What each RomM 5.3 release changed for RomMBat, read from source, and what RomMBat did not adopt.
read-when: Before moving the RomM floor, or when a behaviour differs between 5.3 releases.
---

# RomM: release deltas

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RM-10. Not adopting (`notes`)

Recorded so the next session does not re-open them: Jukebox, walkthroughs, physical game
barcode scanning, library aware recommendations, the four new metadata sources (Steam, Demozoo,
Pouet, CSDb), js-dos and PICO-8 in browser play, and Emulator Streaming V2 as a feature.
RomMBat's job is a local RetroBat install, and none of these reach it. Streaming survives only
as RM-3 and RM-4, which are about contention rather than adoption.

The filesystem structure changes (Structure B detection removed, `filesystem.roms_folder` and
`filesystem.firmware_folder` gone, `GET /setup/library` reshaped) are **server administration**
and inert here, because RomMBat maps to RetroBat's folders and never to the server's. They are
listed so that inertness is a recorded conclusion rather than an omission.

**Inert for the client is not inert for whoever stands one up.** 5.3.0 makes the layout an
explicit declaration and **an instance refuses to start** until `filesystem.structure` is set in
`config.yml`, printing the equivalent template if the removed keys are still present. Anyone
following `DEVELOPER_SETUP.md` to raise a disposable RomM, for a schema capture or to answer one
of the questions below, hits that before the server ever listens. The structure-A equivalent is:

```yaml
filesystem:
  structure:
    default: "roms/{platform}/{game}"
    firmware: "bios/{platform}"
```

## RM-11. The `alpha.2` to `alpha.3` delta (`source`)

`5.3.0-alpha.3` was published 2026-09-17, 319 commits and 67 non-test backend files after
`alpha.2`. It was this adoption's target until RM-12 retargeted it at `beta.1`, which is the
floor. Read at both tags, and the served schema diffed against the pin it replaces. Nothing in
this section is measured yet. Of the three questions it opened, two were answered live and one
was read from source.

**The contract barely moves where this client reads.** One operation leaves, the streaming
`state-frame` route, and none arrive. On a route RomMBat calls, the only change is `slot` on
`POST /api/saves` gaining `maxLength: 255`, so a longer slot is now a 422 rather than a row; the
longest slot this client writes is a class C `{emulator}:{kind}` far below it. `GET /api/roms`
takes the same parameters in a different order, because #4487 moved them into one
`RomFilterParams` model shared with smart collections, and `collection_id` and
`smart_collection_id` now refuse a value below 1. The facet filters read the `roms_facets` mirror,
which already existed at `alpha.2`. The routes a download, a firmware fetch, a play session and a
negotiate use are unchanged, and `backend/main.py` is untouched, so the pin still depends on the
version and not the instance.

**#4540 caps every slot, whatever the client asks, and for this client the cap does not move.**
`add_save` computes the tighter of `MAX_SAVES_PER_SLOT` (env, default 50, `0` disables it) and
`autocleanup_limit` when the client set `autocleanup`, first clamping the client's ask to
`MAX_AUTOCLEANUP_LIMIT` (env, default 100) and to at least 1, and prunes past it on every slotted
upload, retries included. `prune_slot` keeps the newest by `updated_at` then `id` and deletes the
rest with their files and screenshots. Before, pruning ran only when a client asked.
**RomMBat has always asked**: `UploadSaveAsync` sends `autocleanup=true&autocleanup_limit=10` on
every save upload (`src/RomM.Client/Saves/RomMConnection.Saves.cs`, `AutoCleanupLimit`), which
[`docs/PLAN.md`](https://github.com/Spinnich/rommbat/blob/0278bb82c/docs/PLAN.md) records as the M6 decision. So its own slots were bounded at 10 before alpha.3 and
are bounded at 10 now, and #4540 changes nothing for what it uploads itself. What the server cap
governs is every **other** writer on those slots, which asks for no cleanup: a peer, and RomM's
browser player.

**Measured against such a writer, and it costs this client nothing**
(`tools/romm-5.3-probes/s3-slot-retention.py`, two runs on the live `alpha.3`). A device uploaded
and negotiated, then a peer with no device put 51 versions in the slot, sending no cleanup
parameters, which is the case `MAX_SAVES_PER_SLOT` actually governs:

| After 51 peer versions                  | Answer                                                             |
| --------------------------------------- | ------------------------------------------------------------------ |
| Rows in the slot                        | 50, and the device's own version is one of those deleted           |
| Negotiate, the device's copy unchanged  | `download` of the newest, "Server save is newer (no sync history)" |
| Negotiate, the device's copy edited     | `upload`, "Client save is newer (no sync history)"                 |
| The ordinary upload that answer invites | **409**, "Slot has a newer save since your last sync"              |

So deleting the version a sync was recorded against makes negotiate forget the history, and the
upload guard does not: it still holds this device's record for the slot. Two answers that disagree,
and this client already reconciles them, because `SaveSync` records a negotiated `upload` that
comes back 409 as a conflict, a path first driven on hardware for a different cause. An unchanged
copy takes the peer's newest version, which is right, and an edited one becomes a conflict to
settle rather than an overwrite. The prune ranks on `updated_at` as read: a `PUT` onto the oldest
surviving version kept it through the next upload, and the next oldest went instead.

**#4540 also renames a slotted save's screenshot to the save's stem, and does nothing for
states.** RB-258 is the state side.

**The browser player now writes into slots, and by default into this client's.** Three parts:

- **A session opens a new version, then rewrites only that one.** `saveSave` in
  `views/Player/EmulatorJS/utils.ts` holds the version the session created, starting from none:
  the first write `POST`s into the slot with `overwrite=true`, which skips both the stale-device
  409 and the content-hash dedup, and every later write in the session `PUT`s that new row in
  place. At `alpha.2` the player `PUT` the save it loaded, which is RM-4's case A. At `alpha.3`
  a loaded save is left alone and the session appends beside it.
- **The slot is the loaded save's, or the newest slotted save's.** `Player.vue` passes
  `loadedSave?.slot || props.saveSlot`, and the v2 player seeds `props.saveSlot` from
  `preferredSlot(rom.user_saves)`, the slot of the newest save that has one, falling back to
  `autosave`. So for a game this client has synced, a browser session writes into
  `libretro:battery` or whatever slot this client used.
- **The file is `<fs_name_no_ext>.srm` under the EmulatorJS core's name**, `autocleanup` only in
  `autosave`. For a class A libretro slot that is the shape this client downloads and writes to the
  ROM's stem. For a bundled slot it is a raw `.srm` in a slot this client expects to hold an
  archive.

To this client that is ordinary protocol: a newer row in a slot it holds, so `download` if the
local file is unchanged and `conflict` if not. What is not known is whether the bytes are right,
which is a question about EmulatorJS and not about RomM.

**Inert here**, listed so it is a conclusion and not an omission: nginx moves to TLS 1.2 and above
and serves precompressed frontend assets only, scoped away from `/library/` so a ROM download is
untouched; the patcher, Steam metadata and covers, the manual upload routes, the scan and title id
extraction order (#4566, which changes which disc of a set a title id is read from), multi-disc
playlist handling, and every streaming change. The two screenshot fixes in the notes, #4478 and
#4526, are the player's and streaming's

## RM-12. The `alpha.3` to `beta.1` delta (`source`)

`5.3.0-beta.1` was published 2026-09-18, one day after `alpha.3`, and was the floor from that
adoption until `5.3.0` replaced it (RM-13). 160 commits, and a recursive tree diff of the two tags gives 52 files added, 5 removed
and 367 modified. Of those, **31 are non-test backend files and the rest is frontend v2**.

**Count the files from the trees, not from the compare endpoint.** `GET /repos/{o}/{r}/compare/{a}...{b}`
caps its `files` array at 300 and returned exactly 300 here, with no flag in the payload saying
so. Two frontend files this finding turns on, `views/Player/EmulatorJS/utils.ts` and its
`Player.vue`, were missing from that list and are changed. The backend half happened to fit, so
every contract reading below stands, but the first pass read "the save writer is untouched" off a
truncated list and that was wrong. Compare blob shas from `git/trees?recursive=1` instead.

**The contract is the smallest move of the four pins.** The same 246 operations and 272 schemas,
none added, removed or renamed, and the generated diff is four lines. `RomFileSchema.last_modified`
becomes nullable, `SystemDict` gains `GIT_BRANCH` (a nullable string, filled only on a
`development` build), and the `X-Upload-Total-Size` and `X-Upload-Total-Chunks` headers on
`POST /api/roms/upload/start` drop to `minimum: 0` because an empty ROM file is accepted now. No
hand-written line names any of the three, and RomMBat does not upload ROMs. The pin's README has
the detail, including why the `required` on `GIT_BRANCH` does not reach the DTO.

**The backend delta is one shape repeated: answer for ids without building rows.** #4584, #4586,
#4587, #4589 and #4590 add `get_save_ids`, `get_state_ids` and the `RomVisibility`
`RomVisibilityLabel` and `RomDeletionTarget` named tuples, and point the identifier, visibility
and file routes at them. `GET /api/saves/identifiers` was building a `Save` per row to read
`.id` off it, and now projects the column.

The ROM identifiers route answers in under a second on 5.3.1 (RB-81), and RM-9's timings are
taken on 5.3.1.

**The per-slot cap that RM-11 measured is byte-identical here.** `add_save` and `prune_slot`
are untouched between the tags; the only save-side change is the ids projection. So the `alpha.3`
retention table describes the code `beta.1` ships, and `s3-slot-retention.py` says so in its
docstring rather than implying a re-run happened.

**The browser save writer was substantially rewritten, and RM-11's reading survives it.**
`utils.ts` gains a canvas screenshot capture per save version, an SRAM dump that flushes the core
before reading, and a pending-asset path; the naming moved into `services/api/save.ts` as
`sessionSaveFile` and `sessionScreenshotFile`, which confirm `<fs_name_no_ext>.srm` for a new
session save and the save's stem for its picture. **The slot decision is unchanged.** `Player.vue`
still resolves `loadedSave?.slot || props.saveSlot`, `saveSlot` is still
`chosenSlot(slotChoice)` seeded from `preferredSlot(rom.user_saves)`, and `v2/utils/saveSlots.ts`
is byte-identical at both tags: `preferredSlot` still returns the slot of the newest slotted save
and only falls back to `autosave` when there is none. `saveSave` still `POST`s with
`overwrite: true` into that slot and `PUT`s its own row afterwards.

So for a game this client has synced, a browser session still writes into `libretro:battery` or
whatever slot this client used. **The release notes say the opposite** ("Ordinary play goes to the
`autosave` slot"), and read against the source that line describes a game with no slotted save
rather than one this client syncs. This is the `notes` label earning its keep: acting on the
changelog here would have retired a live finding that is still true.

**The notes' breaking changes are not new since `alpha.3`.** The filesystem-structure declaration
and the one-entry-per-container streaming config both landed earlier in the 5.3.0 line, and no
config or structure file changes between the two tags; the release notes are cumulative from
5.2.0. They cost the operator a `config.yml` edit on upgrade and are not a client contract.
RM-1 is the folder-authority half of the same work.

**Inert here**, for the same reason the `alpha.3` list was written: the jukebox and soundtrack
player, walkthroughs, recommendations, physical games, js-dos and PICO-8, the Steam and demoscene
metadata sources, and the v2 UI work that is most of the 367 modified files.

## RM-13. The `beta.1` to `5.3.0` delta (`source`)

`5.3.0` was published 2026-09-21 at 15:07Z, the first stable of the line, and was the floor from
that adoption until `5.3.1` replaced it (RM-14). 14 commits across 79 files, counted by diffing blob shas from both tags'
recursive trees, neither truncated, and the compare endpoint lists the same 79.

**The contract did not move at all.** The capture is byte-identical to the `beta.1` pin once
`info.version` is set aside: the same 246 operations and 272 schemas, none added, removed or
changed, and `generate.sh` reproduces the committed DTOs exactly. This is the first pin move with
no generated diff.

**Most of the backend delta is one formatting commit.** #4638 moves the backend to Python 3.14's
unparenthesised `except A, B:` and parenthesised `with` blocks, which accounts for every one-line
change in `handler/`, `utils/`, `sync_watcher.py`, `endpoints/memory_cards.py` and the save and
sync tests. The behaviour those tests pin is unchanged.

**One change is on the authentication path, and it cannot reach this client.** #4648 stops a
bearer or basic header from skipping the CSRF check when the request also carries a session
cookie that resolves to a user, because the session authenticates first and the request runs as
the cookie's owner. RomMBat authenticates by bearer token only, and `RomMConnection` builds its one
handler with `UseCookies = false`, which every RomM request (the content download included) goes
through. So it never sends a session cookie, and its POSTs are exempt exactly as before.

**Inert here**: #4633 answers a hidden platform as a missing one on the chunked ROM upload, and
RomMBat does not upload ROMs. #4634 budgets a zip member in the patcher, and #4636 changes only a
comment on the inline HTML route. #4641 is the v2 gallery's sort in the URL. Nothing under
`frontend/src/v2/views/Player/` changed, so RM-12's reading of the browser save writer stands
at `5.3.0`.

**So every measurement attributed to `beta.1` carries to `5.3.0`**, and it stays attributed to
`beta.1`: `add_save`, `prune_slot`, negotiate and the play-session routes are byte-identical, and a
reading is not re-labelled because the build it describes did not change.

## RM-14. The `5.3.0` to `5.3.1` delta (`source`, then `measured`)

`5.3.1` was published 2026-09-23 at 03:05Z, a patch release two days after `5.3.0`, and is the
floor from this adoption. 161 commits across 225 files, counted by diffing blob shas from both
tags' recursive trees, neither truncated: 42 added, 1 removed, 182 modified. The compare endpoint
lists the same 225, under its 300 cap. 57 are non-test backend files and 111 are frontend.
**No migration is added.**

**The contract moves in two operation parameters and no schema.** Every `/api/music/*` page caps
`limit` at 1,000 where it took 10,000 (#4716), and `POST /api/streaming/sessions/{platform}/heartbeat`
gains an optional `container` query parameter (#4596). The 198 paths and 272 schemas are otherwise
identical, and `generate.sh` reproduces the committed DTOs exactly, because NSwag emits types here
and not a client. RomMBat calls neither route.

**Every route this client depends on is byte-identical**: `endpoints/saves.py`, `sync.py`,
`states.py`, `screenshots.py`, `play_sessions.py`, `device.py`, `firmware.py`, `platform.py`,
`collections.py`, `auth.py`, `activity.py` and `heartbeat.py`. `endpoints/roms/__init__.py`
changed, and only in typing: `CustomLimitOffsetPage` now extends a `TypedLimitOffsetPage` whose
`create` is cast for mypy, `total` keeps its nullable type, and `RomFiltersDict` moves module.
Most of the backend delta is that kind of change, a run of mypy PRs (#4655, #4709, #4711, #4713,
#4718, #4719, #4723) that swap `type: ignore` comments for types and route DML row counts through
one `affected_rows` helper.

**Two changes reach something RomMBat reads, and neither is on a route it calls differently.**

- **#4676 lets a platform's `fs_slug` change case on a rescan.** `get_platform_by_fs_slug` falls
  back to a case-insensitive match, and `scan_platform` writes the folder's on-disk spelling where
  it wrote the lowercased `config.yml` key. The platform keeps its id. RomMBat keys `platform_map`
  on the exact `fs_slug`, so a case change left the old row behind as a second platform, and
  `PlatformMapStore.Record` now rekeys a row whose id matches and whose key differs only in case.
  Sync sets store the platform id and are unaffected. **From source, not driven**: all 125
  platforms on the live library have a lowercase `fs_slug`, so nothing moved there.
- **#4687 changes GameCube's `save_target` to the ASCII game code.** The sigil pin moves, and
  `save_target` becomes `GAFE` where it was `47414645`; `title_id` stays hex. Only a rescan writes
  it: the 50 most recently updated GameCube rows on the live library, last written 2026-09-14,
  still carried hex in both fields on 2026-09-24. Nothing in RomMBat reads `save_target`, so this
  corrects a sentence in `save-sync` (RM-2's route 4 must accept both shapes) and changes no
  code.

**Inert here**: #4653 revokes an account's Redis sessions on a credential change and stops an
in-flight request resurrecting one, and RomMBat holds no session, only a bearer token. #4694
checks ownership on the RetroAchievements refresh, consumes invites atomically, and passes `--`
before a 7-Zip member name; none is on this client's path. The frontend delta includes
`views/Player/`, but only gamepad focus (#4681, #4707) and the streaming heartbeat: the browser
save writer and its `preferredSlot` are untouched, so RM-12's reading stands at `5.3.1`.

**Measured at `5.3.1`**: `s4-older-mtime.py` answers all six cases as at `5.3.0`, M1
`no_op (No changes since last sync)`, M2 `upload`, M3 `download (Server save is newer (no sync
history))`, M4 `no_op (Content is identical)`, M5 returning the existing row for a peer's identical
upload, and M6 refusing the edit 409 until the peer's row is acknowledged. A deploy of the adoption
branch, with `status` reading `5.3.1` as Supported, resolved every platform mapping identically,
answered `nothing to do` for all eight sets, left all eight gamelists byte-identical, and flushed
nothing in either direction.
