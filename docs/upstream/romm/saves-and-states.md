---
summary: The save and state endpoints, sync negotiation, row identity, hashes and other writers.
read-when: Before calling a save, state or sync endpoint, or reasoning about a slot's rows.
---

# RomM: saves and states

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-126. `POST /api/states` upserts on `(rom_id, file_name)`, and the emulator is not part of the key

Verified: RomM 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: posted one name three times across two payloads, one name under four emulators, and two names differing in a bracketed tag; read `store_state_file`; on 5.4.0 posted the same three shapes again.
Three posts of one name reused one row. Four posts of one name under `libretro`, `bizhawk`,
`libretro.snes9x` and `libretro.bsnes` also reused one row, overwriting its emulator and moving
its stored file into that emulator's directory each time. Two names differing only in
`[libretro.snes9x]` and `[libretro.bsnes]` made two rows. So a replayed state push is idempotent,
there is no history to prune, and RomMBat puts the emulator and core in the uploaded name,
`<stem> [<emulator>[.<core>]]<ext>`, or two cores writing one filename for one ROM would
collapse into one row.

## RB-130. The server tags a slotted save's name with the upload time; a state and an unslotted save keep theirs

Verified: RomM 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: uploaded a slotted `.srm`, a slotted `.zip`, an unslotted `.srm` and a state, and read the names back; read `_apply_datetime_tag` at 5.3.1; on 5.4.0 uploaded the same four shapes and read `apply_datetime_tag` and `reserve_version_name`.
`Phantasy Star (Brazil).srm` into a slot came back `Phantasy Star (Brazil) [2026-09-29_08-06-46].srm`,
and a bundled `UCES01011.zip` came back `UCES01011 [2026-09-29_08-06-47].zip` with
`file_extension` `zip`. `Unslotted (USA).srm` with no slot, and every state, came back exactly as
sent. The tag is the server's local time, not UTC (`13-34-55` beside an `updated_at` of `17:34:55+00:00`).
RomMBat persists the `file_name` the response carries, never the one it sent.

## RB-131. A zero-byte screenshot is accepted and stored

Verified: RomM 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: posted a state with an empty `screenshotFile`; on 5.4.0 posted a state with an empty `screenshotFile` again.
The state linked a screenshot row with `file_size_bytes: 0`. Only an upper size limit is
checked. RetroBat's mirror can race the emulator writing the image, so RomMBat refuses the empty
case itself, because nothing downstream does.

## RB-133. `emulator` must be one folder name of at most 50 characters

Verified: RomM 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: posted a state and a save with `emulator=libretro/evil` and a state with a 51-character name; read `check_emulator_folder_name`.
`emulator=libretro/evil` answers 400, "Invalid emulator name: libretro/evil", on a save and on a
state, and a name over 50 characters answers 422. A dotted name such as `libretro.snes9x` still
passes, and becomes the directory under the stored file. RomMBat's own schema refuses a separator
in that column, so it never sends one.

## RB-151. Negotiate offers a download for every slot the device has no current record for

Verified: RomM 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: negotiated with an empty `saves` array as a fresh device, and ran `s1-browser-save-writer.py` case D; read `negotiate_sync`; on 5.4.0 negotiated with an empty `saves` array as a device that had synced two of five slots, and ran `s1-browser-save-writer.py` case D.
An empty `saves` array answered a download for each slot the device had not synced, for
slots the client never named. A slot the device synced and whose row has not moved since
is left out, read as a deliberate local delete. So negotiating with an empty array is the
fresh-device inventory pass, and RomMBat never returns early because the local save list is
empty. Every answer is scoped by `rom_ids` when the client sends it.

## RB-156. A save changed on both sides can negotiate as `upload` and then be refused 409

Verified: RomM 5.3.1 source, 2026-09-29, and 5.4.0, 2026-10-17. How: read `compare_save_state` and `add_save`'s 409 checks; on 5.4.0 ran `s3-slot-retention.py`, where an edited copy negotiated `upload` and its ordinary upload answered 409; read the 409 checks again.
When the device has no sync record for the newest row in the slot, negotiate compares only
timestamps, so a newer local mtime answers `upload`, "Client save is newer (no sync history)".
The upload then meets the check that refuses a device whose record for the slot's newest row is
missing or older, and answers 409. So RomMBat treats a 409 as the conflict path, not as an error.

## RB-160. A slotted upload makes a new row, whatever second it lands in

Verified: RomM 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: posted two versions into one slot inside one second, then one a second later; read `reserve_version_name`.
The server reserves a tagged name that no other version holds before it writes, and moves a taken
name on by a second (`reserve_version_name`). Two postings inside one second made rows 841 and 842,
tagged a second apart, and a third a second later made 843.
`overwrite=true` never replaces a row: it suppresses the 409 checks and the identical-content
dedup, and nothing else. So `--keep-local` appends, and the server's copy stays one row down.

## RB-161. Identical bytes into a slot reuse the row that holds them, unless `overwrite` is set

Verified: RomM 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: posted one payload twice, then again with `overwrite=true`; on 5.4.0 posted one payload twice, then again with `overwrite=true`.
The second post came back as the first row, 843, and the third made row 844. The check runs only
when `overwrite` is false. So a replayed flush is free, because a flush never sends `overwrite`,
and a repeated `--keep-local` is not.

## RB-162. An unregistered `device_id` is a 404, and an omitted one is accepted

Verified: RomM 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: posted with `device_id=not-a-registered-device` and `overwrite=true`, and with none; read `_resolve_device`; on 5.4.0 posted with an unregistered `device_id` and with none.
The unknown id answered 404, "Device with ID not-a-registered-device not found", and wrote
nothing. With no `device_id` the upload is taken and attributed to no device, skipping the 409
checks, which is how a second device is simulated on a one-device install. So the 404 bounds
impersonation, not participation.

## RB-163. Negotiate reads only the newest row in each `(rom_id, slot)`

Verified: RomM 5.3.1 source, 2026-09-29, and 5.4.0, 2026-10-17. How: read `negotiate_sync`; on 5.4.0 ran `s1-browser-save-writer.py` cases C and E, where the older row a `PUT` revives is offered again.
The server folds its slotted saves to one row per slot by `updated_at` before matching anything,
and both the pass over the client's saves and the pass over the slots it did not name walk that
fold. A row left one down by a later upload is never offered while it stays superseded. It comes
back when something makes it the newest again, which `PUT /api/saves/{id}` does (RM-4).

## RB-164. A negotiate leaves the device's previous session open

Verified: RomM 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: negotiated twice as one device, then completed both sessions; completed one session three times on 5.2.0, 2026-08-25; on 5.4.0 negotiated twice, completed both, then completed one again, and read `complete_sync_session` and the cleanup task.
Both sessions completed 200. A repeat completion answers 400, "Session is already COMPLETED",
not a 404, 409 or 410, and it means the first close landed. A session left open for 24 hours is
failed by a scheduled cleanup, and `/complete` still takes one the cleanup expired; one closed on
purpose is refused.

## RB-243. Negotiate answers from the device's sync record, not from what the client claims

Verified: RomM 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: uploaded as a device, then negotiated the slot with `content_hash: null`, `file_size_bytes: 0` and an old mtime; on 5.4.0 negotiated the slot claiming nothing.
It answered `no_op`, "No changes since last sync", with the right `save_id`, and an empty
`saves` array then left the slot out. So the one save a restore exists for, deleted locally
after this device uploaded it, is the one negotiate will not offer, and `saves restore` reads
`GET /api/saves` instead.

## RB-245. A save can have no slot, and a slotless save is outside the protocol

Verified: RomM 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: uploaded a save with no slot and read it back; read `negotiate_sync`; on 5.4.0 uploaded a save with no slot and read it back; read `negotiate_sync`.
The upload was taken with `"slot": null`. Negotiate pairs on `(rom_id, slot)` over slotted rows
only, so a slotless row is never offered and never conflicts: a device's record for a slot stays
current and its next upload goes over with no 409. Another client or RomM's own UI can write one
beside RomMBat's slotted rows, and it resolves to the same destination. RomMBat derives a slot
for a slotless row rather than storing it empty, since its store refuses an empty slot, and
`saves restore` offers the newest row per destination and names the rest.

## RB-246. A state has no hash, no slot and no sync record

Verified: RomM 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: read the keys of a posted state back; on 5.4.0 read the keys of a posted state back.
`StateSchema` carries no `content_hash`, `slot` or `device_syncs`, where a save carries all three,
and states have no `/track`, no `/downloaded` and no part in negotiate. So a state downloads
unverified, "is it in step" is answerable only from a hash the client recorded itself, and
RomMBat says so on the restore preview and after.

## RB-247. `file_name_no_tags` strips every trailing bracketed and parenthesized group

Verified: RomM 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: posted `Legend of Zelda, The (USA) (Rev 1) [libretro.nestopia].state1` and a slotted `Phantasy Star (Brazil).srm`; read `compute_file_name_no_tags`; on 5.4.0 posted the same two names.
The state read back `Legend of Zelda, The`, and the save `Phantasy Star`, losing region and
revision. Written to disk, either name is one the emulator never looks for, and reads as absent
rather than as an error. RomMBat names a restored save or state after the ROM on disk, through
the declared template, and takes only the extension from the server.

## RB-258. A state's screenshot is whichever image shares its name, found by lookup

Verified: RomM 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: posted states with an image named `<state name>.png`, with one named after the image's own file, and with none beside a slot 0 image; read `get_screenshot`; on 5.4.0 posted states with an image named `<state name>.png` and with one named after the image's own file; `get_screenshot` is unchanged.
There is no link column. `State.screenshot` finds an image whose `file_name` or
`file_name_no_ext` equals the state's, preferring an exact stem, then the highest id. The
extension is `\.(([a-z]+\.)*\w+)$`, so `.state1.png` loses only `.png` and `.state.png` loses
both parts. `P3C Link [libretro.x].state3.png` linked; `P3C Link.state4 [libretro.x].png` did not;
and slot 5, with no image, answered with slot 0's `P3C Zero0 [libretro.x].state.png`. RomMBat
uploads the image as the state's upload name plus the image's own extension, which matches for
every emulator in `es_savestates.cfg`, and restores or counts as kept only an image named after
its own state or the earlier name of its own image. A restore reads the slot out of the uploaded
name through the declared template rather than from the extension, because eight emulators write
the slot into the stem (`Game.QuickSave2.State`, `Game_0.jst`, `Game.01.p2s`).

## RB-259. Negotiate settles on the hash first, then on mtimes against the device's last sync

Verified: RomM 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: ran `s4-older-mtime.py`; read `compare_save_state`; on 5.4.0 ran `s4-older-mtime.py` again and read `compare_save_state`.
Equal hashes answer `no_op`, "Content is identical", whatever the mtimes (M4). Otherwise, with
a sync record, a side changed if its timestamp is newer than the record: a local save with
different bytes and an mtime older than this device's last upload answers `no_op`, "No changes
since last sync" (M1), and a newer one `upload` (M2). With no record, the newer timestamp wins,
so a peer's row answers `download`, "Server save is newer (no sync history)", over any local save
with an older mtime (M3). RomMBat uploads a `no_op` whose local hash differs from what it last
sent, unless the server's hash already equals the local one, and records a `download` over a save the server has never seen as a conflict.
From 5.4 a device's record also holds the hashes both sides had when it last synced
(`last_sync_hash` and `last_sync_server_hash`), and a side whose hash matches its baseline counts as
unchanged even when its mtime moved, so a baseline can remove a conflict and never make one. That is
read in `compare_save_state`, and RomMBat sends neither hash.

## RB-276. RomM keeps a save whose bytes are the JSON literal `null`

Verified: RomM 5.4.0, 2026-10-14, live, and 5.3.1 in source. How: uploaded the four bytes to `autosave`, a `libretro:` slot and no slot, read each back and deleted it; read `add_save` at the `5.3.1` tag, and the player's Save & Quit at `5.2.0`, `5.3.0` and `5.3.1`.
The server stores whatever bytes it is sent. The four bytes `null`, md5
`37a6259cc0c1dae299a7866489dff0bd`, land in all three places and come back unchanged. RomM
5.2.0's browser player sent them: its Save & Quit uploaded `getSaveFile()` unchecked, which is
`null` for a game with no SRAM. 5.3.0 checks `byteLength` first, but the rows an older player left
stay on the server, three on one rom of the library measured. Placed as a battery save, one
replaces a real save with a file no emulator loads. `SaveSync` refuses that hash before the transfer and those
bytes after it, never acknowledges the row, and counts it apart from failures.

## RB-303. A zipped save's `content_hash` is a digest of its members, not of its bytes

Verified: RomM 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: ran `s5-archive-content-hash.py`; on 5.4.0 ran `s5-archive-content-hash.py` again.
The server stored `4704b0bf...` for a zip whose bytes hash to `3c4f71d6...`. The value is
`hash_zip_contents`: the md5 of `<entry name>:<entry md5>` lines, sorted by name, joined with
`\n` and none trailing, directory entries skipped. So compression level and timestamps do not
move it, and renaming a member does. A plain file's `content_hash` is the md5 of its bytes.
`LogicalContentHash.Fold` is that rule, so a class C unit's local hash is the wire value, and
negotiate answers `no_op` only when it is sent.

## RB-308. A peer's upload of bytes a row in the slot already holds returns that row

Verified: RomM 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: ran `s4-older-mtime.py` case M5; on 5.4.0 ran it again and read `promote_to_slot_head`.
This device uploaded row 833; a peer uploaded different bytes as 834 and then the first bytes
again, which came back as 833 and became the slot's newest (`promote_to_slot_head` moves its
`updated_at` past the head's). Negotiate for this device then answered `no_op`, "Content is
identical", on 833. The dedup matches by hash across the whole slot, not only the newest
row.

## RB-309. Negotiate records a head that holds the device's own bytes, so the next upload lands

Verified: RomM 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: ran `s4-older-mtime.py` case M6; on 5.4.0 ran it again, where the edit landed with and without the acknowledgment; read `negotiate_sync`.
With this device's row deleted and a peer's row 838 holding the same bytes at the head,
negotiate answers `no_op`, "Content is identical", on 838 and records the device as in step with
it (`record_identical_content`), so an upload of an edit lands 200 without `POST
/api/saves/838/downloaded`. `SaveSync.SettleOnHeadAsync` still makes that acknowledgment, with no
transfer, which costs a call and changes nothing.

## RB-327. RomM's browser player can rewrite a save another emulator made, in a format of its own

Verified: RomM 5.3.0, 2026-09-24, driven, and 5.3.1, 2026-10-14, and 5.4.0, 2026-10-17, in source. How: read the row an accidental EmulatorJS launch wrote during a `mastersystem` pass, and what the next flush did with it; read `_EJS_CORES_MAP` at the `5.3.1` tag; on 5.4.0 read `saveSave` in the player and the `_EJS_CORES_MAP` diff, which only reorders the arcade cores.
It wrote save 496 into `libretro:battery` with no device (since 5.4 the player sends its own browser device, RM-4), named after PicoDrive's upload two
minutes earlier and holding 8,191 B: PicoDrive's 32 KB save as Genesis Plus GX, which EmulatorJS
runs, trims it. `sms` has no other EmulatorJS core, and a session's first write lands in the
newest slotted save's slot (RM-4). The next flush found the local `.srm` unchanged since it was last in step,
downloaded the newer row, and kept the displaced file in `emulators/rommbat/replaced/`.

## RM-3. `/api/memory-cards` is a card per `(user, emulator)`, with no ROM

Verified: RomM 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: ran `s2-memory-card-record.py`; read `MemoryCard`; on 5.4.0 ran `s2-memory-card-record.py` again.
A card record holds `id`, `user_id`, `emulator`, `name`, `slot` (always 1), `is_public`,
`platform_id` and timestamps, and one Dolphin card serves GameCube and Wii. A card never synced
answers 404 on its content. A version is the whole card, returned byte-identical; its
`content_hash` is taken over the zip's members; an identical upload makes a second version.
A bare `SRAM.USA.raw` is refused 400, and a zip holding one is accepted, so the server never
checks the layout inside. Streaming exchanges Dolphin's card as a zip of `.gci` files.

So a card is a class D container by construction, and the only raw card it would store is one
it does not validate. RomMBat does not call these routes: the per-game `.gci` files are already
class C units, and bridging a whole-card version would put two writers on one container.

## RM-4. RomM's browser player and streaming write the same slots RomMBat negotiates on

Verified: RomM 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: ran `s1-browser-save-writer.py`, which replays an in-place `PUT` and a slotless `POST` with no `device_id`; read `saveSave`, `Player.vue`, `v2/utils/saveSlots.ts` and the streaming handlers; on 5.4.0 ran it again, where cases A to E answer as the table says, and read `Player.vue`, `saveSave`, `v2/utils/saveSync/` and the streaming handlers at the `5.4.0` tag.
`PUT /api/saves/{id}` rewrites a row in place: the id, tagged name and slot stay, `content_hash`
and `updated_at` move, and there is no 409 check, dedup or device check. The browser player's
first write in a session `POST`s a new version with `overwrite=true` into the newest slotted
save's slot, or `autosave` when there is none, which for a game RomMBat syncs is RomMBat's slot.
A loaded save's slot wins over that choice (`loadedSave?.slot || props.saveSlot`). Later writes in
that session `PUT` that row, named `<fs_name_no_ext>.srm` under the EmulatorJS core. Since 5.4 the
player registers its own browser as a sync device (`browserDeviceId`) and sends it and a `content_hash`
with every write. When the loaded save is the slot's current head the first `POST` goes without
`overwrite`, so the 409 guard applies, and a refusal files the bytes as a slotless archive
(`uploadArchivedSave`); booting any other version is `overwrite=true`. The release
notes say ordinary play goes to `autosave`, which holds only for a game with no slotted save. That
first `POST` is read in source, not measured; to negotiate it is a newer row in the slot with no
device, so `download` if the local file is unchanged and `conflict` if not. For a bundled slot the
row is a raw `.srm` where RomMBat expects an archive. What negotiate answers to a `PUT`:

| Case                                                                           | Negotiate answers                                                         |
| ------------------------------------------------------------------------------ | ------------------------------------------------------------------------- |
| A. a PUT over this device's row, local unchanged                               | `download`, same save id, new hash, "Server save is newer than last sync" |
| B. the same, and local also changed                                            | `conflict`, "Both sides changed since last sync"; an ordinary upload 409s |
| C. after keep-local appends a row, a PUT into the older row this device synced | `conflict` against the older row, now the head                            |
| E. as C, but the older row was a peer's this device never synced               | `download`, "Server save is newer (no sync history)"                      |
| D. a slotless `POST`, as a web-UI upload makes, then two PUTs                  | one slotless row, same id throughout, never offered                       |

Case E would put the copy a person rejected back over the one they kept. So RomMBat records a
download naming a lower save id than the slot's recorded one as a conflict, since ids only grow.

The player posts states as `<rom> [<ISO timestamp>].state` under the EmulatorJS core, and the
console view as `state.save` under `emulatorjs`. Neither can equal RomMBat's
`<stem> [<emulator>[.<core>]]<ext>`, so neither lands on a row RomMBat sent. A manual upload
through the web UI under RomMBat's exact name replaces that row's bytes and clears its emulator,
and RomMBat's next local change overwrites it, since states have no conflict route.

Streaming, off on the measured server and read in source only, stores each pulled save archive
as a new slotless row, `<rom stem> [<emulator> <timestamp>].saves.zip`, dropped when its hash
matches any save the ROM holds, and files a RetroArch exit's bare save as an `autosave` version
unless it is a copy of the slot's newest. It prunes states past `STREAMING_STATE_HISTORY_LIMIT` (default 50) over every state the user holds for the ROM under that emulator name, whoever wrote it.
RomMBat's `pcsx2`, `dolphin` and `xemu` states share those names, so a heavily streamed game loses
this device's oldest state rows on the server. The local files survive and are not re-sent.
`libretro.<core>` does not collide with streaming's `retroarch`.

## RM-11. A slot keeps at most 50 versions, pruned on every slotted upload, and a slot name is at most 255 characters

Verified: RomM 5.3.1, 2026-09-29, and 5.4.0, 2026-10-17. How: ran `s3-slot-retention.py`; read `add_save`, `_slot_retention` and `prune_slot`; on 5.4.0 ran `s3-slot-retention.py` again, where a slot of 256 characters answered 422.
`add_save` keeps the tighter of `MAX_SAVES_PER_SLOT` (env, default 50, `0` disables it) and the
client's `autocleanup_limit` when it sets `autocleanup`, first clamped to 1 to
`MAX_AUTOCLEANUP_LIMIT` (env, default 100). It prunes past that on every slotted upload, keeping
the newest by `updated_at` then `id`, and deletes the rest with their files. `slot` longer than
255 characters is a 422. RomMBat sends `autocleanup=true&autocleanup_limit=10`
(`AutoCleanupLimit`), so the server cap bounds only other writers. After a peer with no device put
51 versions into a slot this device had synced:

| After 51 peer versions                  | Answer                                                                   |
| --------------------------------------- | ------------------------------------------------------------------------ |
| Rows in the slot                        | 50, and the device's own version is one of those deleted                 |
| Negotiate, the device's copy unchanged  | `download` of the newest, "Client holds a version removed on the server" |
| Negotiate, the device's copy edited     | `upload`, "Client save is newer (no sync history)"                       |
| The ordinary upload that answer invites | **409**, "Slot has a newer save since your last sync"                    |

`SaveSync` records a negotiated `upload` that comes back 409 as a conflict, so an edited copy is
settled rather than overwritten. A `PUT` onto the oldest surviving version kept it through the
next prune, and the next oldest went instead.

## RM-17. A save download records the device current on the request unless `optimistic=false`

Verified: RomM 5.1.1-beta.1, 2026-08-10, and 5.3.1 source, 2026-09-29, and 5.4.0, 2026-10-17. How: downloaded one save as two never-synced devices, with the default and with `optimistic=false`, and read `device_syncs` after each; on 5.3.1 read `download_save` and `confirm_download`; on 5.4.0 downloaded one save as two never-synced devices, with the default and with `optimistic=false`.
`optimistic` defaults to true, and the GET alone took the default device from `is_current:
false` to `true`. With `optimistic=false` it stayed false until `POST /api/saves/{id}/downloaded`
set it. So a download that dies mid-body leaves the server sure the device holds the save, and
the next negotiate answers `no_op`. RomMBat passes `optimistic=false` and acknowledges only
after the bytes are written and verified.

## RM-18. `device_id` scopes sync bookkeeping, never which saves are listed

Verified: RomM 5.3.1 source, 2026-09-29, and 5.4.0, 2026-10-17. How: read `get_saves` and `_build_save_schema`; on 5.1.1-beta.1, 2026-08-10, listed one ROM's saves as two devices and as none; on 5.4.0 listed one ROM's saves with and without a device and read `device_syncs`.
Both devices listed the same rows. `device_syncs` is empty when no `device_id` is passed. With
one, it holds every device's record, the caller's first, and the caller gets an entry with
`is_current: false` when it has never synced the save. `origin_device_id` names the device that
uploaded a save, kept through an `overwrite=true` replacement. So a missing entry means the
query named no device, never that nobody synced.

## RM-19. A 409 on upload is a bare string, and names no save and no time

Verified: RomM 5.1.1-beta.1, 2026-08-10, and 5.3.1 source, 2026-09-29, and 5.4.0, 2026-10-17. How: uploaded different content into one slot from a device that had not synced it; on 5.3.1 read `add_save`'s 409 checks; on 5.4.0 uploaded different content into one slot from a device that had not synced it.
The body is `{"detail": "Slot has a newer save since your last sync"}`, or "Save has been updated
since your last sync" for an unslotted save, and never an object. It fires when **this device's**
record is stale, so the device that wrote the current save may write again while one that never
synced it is refused (RB-156). Showing the user anything takes a separate fetch of the row.

## RM-20. `POST /api/play-sessions` takes an envelope, needs no sync session, and names duplicates

Verified: RomM 5.1.1-beta.1, 2026-08-11, and 5.3.1 source, 2026-09-29, and 5.4.0, 2026-10-17. How: posted a bare array, an envelope, the same envelope again, 101 entries, and an inverted range; on 5.3.1 read `ingest_play_sessions`; on 5.4.0 posted a bare array, an envelope, the same envelope again, 101 entries and an inverted range.
The body is `{device_id, sessions: [...]}` with `device_id` outside the entries, and a bare array
is a 422; a device-bound token supplies a missing `device_id`. It answers 201 with a per-index
`status` of `created`, `duplicate` or `error`, and `created_count` and `skipped_count`, so a
replayed flush is told what it skipped. 101 entries is a 400, "Batch size exceeds maximum of
100"; an `end_time` not after `start_time` is a 422; `rom_id` is optional.

## RM-21. A save's `download_path` carries its timestamp unencoded

Verified: RomM 5.1.1-beta.1, 2026-08-10, and 5.3.1 source, 2026-09-29, and 5.4.0, 2026-10-17. How: read `download_path` off a live save; on 5.3.1 read `models/assets.py`; on 5.4.0 read `download_path` off a live save and in `models/assets.py`.
It is `/api/saves/<id>/content?timestamp=<updated_at>&name=<file name>`, the datetime written as
Python prints it, raw space included, and only the name percent-encoded:
`?timestamp=2026-10-17 17:35:00&name=P54%20Conf%20%5B2026-10-17_13-35-00%5D.srm`. So it is not a
URL to request as served. RomMBat builds the content URL from the save's `id`.

## RM-22. `POST /api/saves/delete` stops at the first id it cannot find

Verified: RomM 5.3.1 source, 2026-09-29, and 5.4.0, 2026-10-17. How: read `delete_saves`; on 5.4.0 deleted a list holding a missing id between two real ones.
It deletes in list order and answers 404, `Save with ID <n> not found`, at the first missing
id, keeping the deletions before it and skipping every id after. The slot prune can remove an id
between a listing and a delete, so a batch built from a stale list half-lands.

## RM-23. Negotiate needs `device_id` unless the token is device-bound

Verified: RomM 5.1.1-beta.1, 2026-08-11, and 5.3.1 source, 2026-09-29, and 5.4.0, 2026-10-17. How: negotiated without `device_id` on an ordinary client token and on a pairing-minted one; on 5.3.1 read `negotiate`; on 5.4.0 negotiated without `device_id` on an ordinary client token.
The ordinary token answered 400, "device_id is required (either in the request payload or
implicit via a device-bound client token)". The pairing-minted token negotiated the same with
the field absent as present. RomMBat's token comes from pairing and it sends the field anyway.

## RM-24. The activity heartbeat lasts 90 seconds, and its `DELETE` reads `device_id` from the query

Verified: RomM 5.2.0, 2026-08-25, and 5.3.1 source, 2026-09-29, and 5.4.0, 2026-10-17. How: posted a heartbeat, listed `GET /api/activity`, and cleared it with a body and with a query; on 5.3.1 read `activity.py` and `activity_handler.py`; on 5.4.0 posted a heartbeat and cleared it with a body and with a query.
`POST /api/activity/heartbeat` takes `{rom_id, device_id}` and holds the entry for
`ACTIVITY_TTL`, 90 s, so a client repeats it while play continues. The `DELETE` sent the same
body answers 422 naming the missing query field; `?device_id=` answers 204. It is the presence
feed only, not `rom_user.now_playing`. RomMBat never posts it: `game-start` is on the launch
path, so any heartbeat would belong to the `background` pass.
