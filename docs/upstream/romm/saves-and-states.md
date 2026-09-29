---
summary: The save and state endpoints, sync negotiation, row identity, hashes and other writers.
read-when: Before calling a save, state or sync endpoint, or reasoning about a slot's rows.
---

# RomM: saves and states

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-126. `POST /api/states` upserts on `(rom_id, file_name)`, and the emulator is not part of the key

Verified: RomM 5.3.1, 2026-09-29. How: posted one name three times across two payloads, one name under four emulators, and two names differing in a bracketed tag; read `store_state_file`.
Three posts of one name reused one row. Four posts of one name under `libretro`, `bizhawk`,
`libretro.snes9x` and `libretro.bsnes` also reused one row, overwriting its emulator and moving
its stored file into that emulator's directory each time. Two names differing only in
`[libretro.snes9x]` and `[libretro.bsnes]` made two rows. So a replayed state push is idempotent,
there is no history to prune, and RomMBat puts the emulator and core in the uploaded name,
`<stem> [<emulator>[.<core>]]<ext>`, or two cores writing one filename for one ROM would
collapse into one row.

## RB-130. The server tags a slotted save's name with the upload time; a state and an unslotted save keep theirs

Verified: RomM 5.3.1, 2026-09-29. How: uploaded a slotted `.srm`, a slotted `.zip`, an unslotted `.srm` and a state, and read the names back; read `_apply_datetime_tag`.
`Phantasy Star (Brazil).srm` into a slot came back `Phantasy Star (Brazil) [2026-09-29_08-06-46].srm`,
and a bundled `UCES01011.zip` came back `UCES01011 [2026-09-29_08-06-47].zip` with
`file_extension` `zip`. `Unslotted (USA).srm` with no slot, and every state, came back exactly as
sent. RomMBat persists the `file_name` the response carries, never the one it sent.

## RB-131. A zero-byte screenshot is accepted and stored

Verified: RomM 5.3.1, 2026-09-29. How: posted a state with an empty `screenshotFile`.
The state linked a screenshot row with `file_size_bytes: 0`. Only an upper size limit is
checked. RetroBat's mirror can race the emulator writing the image, so RomMBat refuses the empty
case itself, because nothing downstream does.

## RB-133. `emulator` becomes a directory under the stored file, unchecked

Verified: RomM 5.3.1, 2026-09-29. How: posted a state with `emulator=libretro/evil` and read its `file_path`; read `_build_asset_file_path`.
It was accepted and its `file_path` ended `312519/libretro/evil`, two segments. Saves are joined
the same way. romm#4851 rejects any value that is not one folder name with a 400, in no release
yet; a dotted name such as `libretro.snes9x` still passes it. RomMBat's own schema refuses a
separator in that column, so it never sends one ([issues](../issues.md)).

## RB-151. Negotiate offers a download for every slot the device has no current record for

Verified: RomM 5.3.1, 2026-09-29. How: negotiated with an empty `saves` array as a fresh device, and ran `s1-browser-save-writer.py` case D; read `negotiate_sync`.
An empty `saves` array answered four downloads, each "Save exists on server but not on client",
for slots the client never named. A slot the device synced and whose row has not moved since
is left out, read as a deliberate local delete. So negotiating with an empty array is the
fresh-device inventory pass, and RomMBat never returns early because the local save list is
empty. Every answer is scoped by `rom_ids` when the client sends it.

## RB-156. A save changed on both sides can negotiate as `upload` and then be refused 409

Verified: RomM 5.3.1 source, 2026-09-29. How: read `compare_save_state` and `add_save`'s 409 checks.
When the device has no sync record for the newest row in the slot, negotiate compares only
timestamps, so a newer local mtime answers `upload`, "Client save is newer (no sync history)".
The upload then meets the check that refuses a device whose record for the slot's newest row is
missing or older, and answers 409. So RomMBat treats a 409 as the conflict path, not as an error.

## RB-160. A slotted upload makes a new row unless it lands in the same second as the last

Verified: RomM 5.3.1, 2026-09-29. How: posted two versions into one slot inside one second, then two a second apart.
The row is looked up by the tagged name RB-130 describes, at one-second resolution. Two postings
inside one second updated row 564 in place; two a second apart made rows 565 and 566.
`overwrite=true` never replaces a row: it suppresses the 409 checks and the identical-content
dedup, and nothing else. So `--keep-local` appends, and the server's copy stays one row down.

## RB-161. Identical bytes into a slot reuse the row that holds them, unless `overwrite` is set

Verified: RomM 5.3.1, 2026-09-29. How: posted one payload twice, then again with `overwrite=true`.
The second post came back as the first row, 567, and the third made row 568. The check runs only
when `overwrite` is false. So a replayed flush is free, because a flush never sends `overwrite`,
and a repeated `--keep-local` is not.

## RB-162. An unregistered `device_id` is a 404, and an omitted one is accepted

Verified: RomM 5.3.1, 2026-09-29. How: posted with `device_id=not-a-registered-device` and `overwrite=true`, and with none; read `_resolve_device`.
The unknown id answered 404, "Device with ID not-a-registered-device not found", and wrote
nothing. With no `device_id` the upload is taken and attributed to no device, skipping the 409
checks, which is how a second device is simulated on a one-device install. So the 404 bounds
impersonation, not participation.

## RB-163. Negotiate reads only the newest row in each `(rom_id, slot)`

Verified: RomM 5.3.1 source, 2026-09-29. How: read `negotiate_sync`.
The server folds its slotted saves to one row per slot by `updated_at` before matching anything,
and both the pass over the client's saves and the pass over the slots it did not name walk that
fold. A row left one down by a later upload is never offered while it stays superseded. It comes
back when something makes it the newest again, which `PUT /api/saves/{id}` does (RM-4).

## RB-164. A negotiate cancels the device's previous active session

Verified: RomM 5.3.1, 2026-09-29. How: negotiated twice as one device, then completed both sessions.
Completing the first answered 400, "Session is already CANCELLED", and the second answered 200.
So a client that negotiates twice without completing the first session cannot tidy it up.

## RB-243. Negotiate answers from the device's sync record, not from what the client claims

Verified: RomM 5.3.1, 2026-09-29. How: uploaded as a device, then negotiated the slot with `content_hash: null`, `file_size_bytes: 0` and an old mtime.
It answered `no_op`, "No changes since last sync", with the right `save_id`, and an empty
`saves` array then left the slot out. So the one save a restore exists for, deleted locally
after this device uploaded it, is the one negotiate will not offer, and `saves restore` reads
`GET /api/saves` instead.

## RB-245. A save can have no slot, and a slotless save is outside the protocol

Verified: RomM 5.3.1, 2026-09-29. How: uploaded a save with no slot and read it back; read `negotiate_sync`.
The upload was taken with `"slot": null`. Negotiate pairs on `(rom_id, slot)` over slotted rows
only, so a slotless row is never offered and never conflicts: a device's record for a slot stays
current and its next upload goes over with no 409. Another client or RomM's own UI can write one
beside RomMBat's slotted rows, and it resolves to the same destination. RomMBat derives a slot
for a slotless row rather than storing it empty, since its store refuses an empty slot, and
`saves restore` offers the newest row per destination and names the rest.

## RB-246. A state has no hash, no slot and no sync record

Verified: RomM 5.3.1, 2026-09-29. How: read the keys of a posted state back.
`StateSchema` carries no `content_hash`, `slot` or `device_syncs`, where a save carries all three,
and states have no `/track`, no `/downloaded` and no part in negotiate. So a state downloads
unverified, "is it in step" is answerable only from a hash the client recorded itself, and
RomMBat says so on the restore preview and after.

## RB-247. `file_name_no_tags` strips every trailing bracketed and parenthesised group

Verified: RomM 5.3.1, 2026-09-29. How: posted `Legend of Zelda, The (USA) (Rev 1) [libretro.nestopia].state1` and a slotted `Phantasy Star (Brazil).srm`; read `compute_file_name_no_tags`.
The state read back `Legend of Zelda, The`, and the save `Phantasy Star`, losing region and
revision. Written to disk, either name is one the emulator never looks for, and reads as absent
rather than as an error. RomMBat names a restored save or state after the ROM on disk, through
the declared template, and takes only the extension from the server.

## RB-258. A state's screenshot is whichever image shares its name, found by lookup

Verified: RomM 5.3.1, 2026-09-29. How: posted states with an image named `<state name>.png`, with one named after the image's own file, and with none beside a slot 0 image; read `get_screenshot`.
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

Verified: RomM 5.3.1, 2026-09-29. How: ran `s4-older-mtime.py`; read `compare_save_state`.
Equal hashes answer `no_op`, "Content is identical", whatever the mtimes (M4). Otherwise, with
a sync record, a side changed if its timestamp is newer than the record: a local save with
different bytes and an mtime older than this device's last upload answers `no_op`, "No changes
since last sync" (M1), and a newer one `upload` (M2). With no record, the newer timestamp wins,
so a peer's row answers `download`, "Server save is newer (no sync history)", over any local save
with an older mtime (M3). RomMBat uploads a `no_op` whose local hash differs from what it last
sent, unless the server's hash already equals the local one, and records a `download` over a save the server has never seen as a conflict.

## RB-276. RomM keeps a save whose bytes are the JSON literal `null`

Verified: RomM 5.3.0, 2026-09-21. How: read the rows a flush had placed on `megadrive` and `nes`, and their hashes.
RomM's browser player uploaded the four bytes `null` when it had no save to send, md5
`37a6259cc0c1dae299a7866489dff0bd`, and the server stored them in `autosave`, in
`libretro:battery` and slotless. Rom 189465 held three. Placed as a battery save, one replaces a
real save with a file no emulator loads. `SaveSync` refuses that hash before the transfer and those
bytes after it, never acknowledges the row, and counts it apart from failures.

## RB-303. A zipped save's `content_hash` is a digest of its members, not of its bytes

Verified: RomM 5.3.1, 2026-09-29. How: ran `s5-archive-content-hash.py`.
The server stored `4704b0bf...` for a zip whose bytes hash to `3c4f71d6...`. The value is
`hash_zip_contents`: the md5 of `<entry name>:<entry md5>` lines, sorted by name, joined with
`\n` and none trailing, directory entries skipped. So compression level and timestamps do not
move it, and renaming a member does. A plain file's `content_hash` is the md5 of its bytes.
`LogicalContentHash.Fold` is that rule, so a class C unit's local hash is the wire value, and
negotiate answers `no_op` only when it is sent.

## RB-308. A peer's upload of bytes a row in the slot already holds returns that row

Verified: RomM 5.3.1, 2026-09-29. How: ran `s4-older-mtime.py` case M5.
This device uploaded row 551; a peer uploaded different bytes as 552 and then the first bytes
again, which came back as 551 without moving its `updated_at`. Negotiate for this device then
answered `download` of 552. The dedup matches by hash across the whole slot, not only the newest
row.

## RB-309. A device must acknowledge a peer's head row that holds its own bytes before it can upload

Verified: RomM 5.3.1, 2026-09-29. How: ran `s4-older-mtime.py` case M6.
With this device's row deleted and a peer's row 555 holding the same bytes at the head,
negotiate answered `no_op`, "Content is identical", on 555. An upload of an edit was refused 409;
after `POST /api/saves/555/downloaded` the same upload landed. `SaveSync.SettleOnHeadAsync`
makes that acknowledgement, with no transfer.

## RB-327. RomM's browser player can rewrite a save another emulator made, in a format of its own

Verified: RomM 5.3.0, 2026-09-24. How: read the row an accidental EmulatorJS launch wrote during a `mastersystem` pass, and what the next flush did with it.
It wrote save 496 into `libretro:battery` with no device, named after PicoDrive's upload two
minutes earlier and holding 8,191 B: PicoDrive's 32 KB save as Genesis Plus GX, which EmulatorJS
runs, trims it. The next flush found the local `.srm` unchanged since it was last in step,
downloaded the newer row, and kept the displaced file in `emulators/rommbat/replaced/`.

## RM-3. `/api/memory-cards` is a card per `(user, emulator)`, with no ROM

Verified: RomM 5.3.1, 2026-09-29. How: ran `s2-memory-card-record.py`; read `MemoryCard`.
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

Verified: RomM 5.3.1, 2026-09-29. How: ran `s1-browser-save-writer.py`, which replays an in-place `PUT` and a slotless `POST` with no `device_id`; read `saveSave`, `Player.vue`, `v2/utils/saveSlots.ts` and the streaming handlers.
`PUT /api/saves/{id}` rewrites a row in place: the id, tagged name and slot stay, `content_hash`
and `updated_at` move, and there is no 409 check, dedup or device check. The browser player's
first write in a session `POST`s a new version with `overwrite=true` into the newest slotted
save's slot, or `autosave` when there is none, which for a game RomMBat syncs is RomMBat's slot.
A loaded save's slot wins over that choice (`loadedSave?.slot || props.saveSlot`). Later writes in
that session `PUT` that row, named `<fs_name_no_ext>.srm` under the EmulatorJS core. The release
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
matches any save the ROM holds. It prunes states past `STREAMING_STATE_HISTORY_LIMIT` (default 50) over every state the user holds for the ROM under that emulator name, whoever wrote it.
RomMBat's `pcsx2`, `dolphin` and `xemu` states share those names, so a heavily streamed game loses
this device's oldest state rows on the server. The local files survive and are not re-sent.
`libretro.<core>` does not collide with streaming's `retroarch`.

## RM-11. A slot keeps at most 50 versions, pruned on every slotted upload, and a slot name is at most 255 characters

Verified: RomM 5.3.1, 2026-09-29. How: ran `s3-slot-retention.py`; read `add_save`, `_slot_retention` and `prune_slot`.
`add_save` keeps the tighter of `MAX_SAVES_PER_SLOT` (env, default 50, `0` disables it) and the
client's `autocleanup_limit` when it sets `autocleanup`, first clamped to 1 to
`MAX_AUTOCLEANUP_LIMIT` (env, default 100). It prunes past that on every slotted upload, keeping
the newest by `updated_at` then `id`, and deletes the rest with their files. `slot` longer than
255 characters is a 422. RomMBat sends `autocleanup=true&autocleanup_limit=10`
(`AutoCleanupLimit`), so the server cap bounds only other writers. After a peer with no device put
51 versions into a slot this device had synced:

| After 51 peer versions                  | Answer                                                             |
| --------------------------------------- | ------------------------------------------------------------------ |
| Rows in the slot                        | 50, and the device's own version is one of those deleted           |
| Negotiate, the device's copy unchanged  | `download` of the newest, "Server save is newer (no sync history)" |
| Negotiate, the device's copy edited     | `upload`, "Client save is newer (no sync history)"                 |
| The ordinary upload that answer invites | **409**, "Slot has a newer save since your last sync"              |

`SaveSync` records a negotiated `upload` that comes back 409 as a conflict, so an edited copy is
settled rather than overwritten. A `PUT` onto the oldest surviving version kept it through the
next prune, and the next oldest went instead.
