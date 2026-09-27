---
summary: The save and state endpoints, sync negotiation, row identity, hashes and other writers.
read-when: Before calling a save, state or sync endpoint, or reasoning about a slot's rows.
---

# RomM: saves and states

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-126. It is an upsert, not an append

Previously: `POST /api/states` had never been called from this repo (plan M6)

Measurement says: **It is an upsert, not an append.** Three posts of one `file_name` reused one row (id 115) across two different payloads. So there is no slot history to prune, no `autocleanup` to ask for, and a replayed flush is idempotent for free. `PUT /api/states/{id}` works and is unnecessary

## RB-127. It does not. The key is `(rom_id, file_name)` and nothing else

Previously: The `emulator` distinguishes one state from another (plan M6, by implication)

Measurement says: **It does not. The key is `(rom_id, file_name)` and nothing else.** Five posts of one name under `libretro`, `bizhawk`, `libretro.snes9x`, `libretro.bsnes` and `libretro/evil` all reused id 119, overwriting the row's emulator and moving its stored file between directories each time

## RB-128. Whether a bracketed tag separates two states

Previously: (not addressed) whether a bracketed tag separates two states

Measurement says: **It does.** `TagProbe [libretro.snes9x].state1` and `TagProbe [libretro.bsnes].state1` produced ids 121 and 122. So the key is the whole `file_name`, not `file_name_no_tags`, and scoping the uploaded name is a working fix for 127

## RB-129. Neither exists, in the pinned schema or in the live response

Previously: A state has a `slot` and a `content_hash` (plan M6, by implication)

Measurement says: **Neither exists**, in the pinned schema or in the live response. `{emulator}:{core}:{slot}` is therefore a local identity only, and "is this state in step" is answerable only from a hash the device recorded itself

## RB-130. Not for states

Previously: The server renames an upload (F6, for saves)

Measurement says: **Not for states.** A save came back `Probe Save [2026-08-17_12-27-44].srm`; a state came back exactly as sent. `file_name_no_tags` is still computed, and strips `(USA)` out of a real ROM name, so it is not a way to recover the name that was sent

## RB-131. Accepted and stored as a real screenshot row

Previously: (not addressed) what a zero-byte `screenshotFile` does

Measurement says: **Accepted and stored as a real screenshot row** (id 151, `file_size_bytes: 0`). Since RetroBat's mirror races the emulator writing the image and a zero-byte result was measured across three saves of one game, the client has to suppress it, because nothing downstream does

## RB-132. It never does, so both cases resolve negatively

Previously: Two open download cases turn on whether negotiate volunteers slots (plan M6)

Measurement says: **It never does, so both cases resolve negatively.** A device with a save on the server negotiated an **empty** `saves` array and got `operations: []`; negotiating one unrelated slot returned exactly that slot. Negotiate is client-driven over the set the client names, so a fresh device cannot discover its saves through it. **Withdrawn. See RB-151**: that device was simply current for the only save on the account

## RB-133. Whether `emulator` is sanitised server-side

Previously: (not addressed) whether `emulator` is sanitised server-side

Measurement says: **It is not.** `libretro/evil` was accepted and became two path segments in the stored state's `file_path`. Reported upstream as rommapp/romm#4839. RomMBat's own schema refuses a separator in that column, so it cannot send one

## RB-138. True, and there is a second reason

Previously: A state screenshot is best-effort because the emulator may not write one

Measurement says: **True, and there is a second reason.** The image is uploaded, stored against the ROM at the right name and size, and then **not linked to the state**, which reads `screenshot: null` and stays so. Roughly a third of thirty-five attempts. Not reproducible on demand; the request is provably well formed. **Superseded by RB-256**: not specific to a platform or core, and a loss rather than an untidy record once a state comes back down. The "third" is explained by RB-258

## RB-147. True for a bundled directory save too, and the untagged name is the unit key

Previously: The server renames a save (F6, RB-130)

Measurement says: **True for a bundled directory save too, and the untagged name is the unit key.** `UCES01011.zip` came back `'UCES01011 [2026-08-17_23-52-18].zip'` with `file_name_no_tags` `'UCES01011'` and `file_extension` `'zip'`

## RB-148. True for a plain file, false for an archive

Previously: `content_hash` is the MD5 of the bytes uploaded (F3, and the download verify)

Measurement says: **True for a plain file, false for an archive.** A 24 B payload, 570 B of `'A'`, 570 B random and 570 B of NUL all match exactly. A 570 B zip does not, independent of `Content-Type` and of filename. Rebuilding one member at a different compression level and timestamp gives a different zip and **the same** digest; renaming the member changes it

## RB-149. The server's own returned digest, and only that

Previously: (not addressed) what negotiate compares for a bundled save

Measurement says: **The server's own returned digest, and only that.** Sending it answers `no_op (Content is identical)`; sending our logical fold or the archive's MD5 answers `download (Server save is newer)`. Eight candidate reconstructions of the server function reproduce none of the observed values, so it is **not reproducible client-side** and must not be guessed at. **Withdrawn by 303**: the function is reproducible and the fold now is it

## RB-150. Not on this version. The key is `(rom_id, slot, file_name)` and it replaces

Previously: Different content into one slot appends a row (F3)

Measurement says: **Not on this version. The key is `(rom_id, slot, file_name)` and it replaces.** Same name and different content reused id 136 with the content hash updated and no `overwrite` flag; a different name in the same slot made a second row. F3's two uploads shared a name, so its reading does not hold here. **Withdrawn. See RB-160**: this was a same-second update read as the general rule

## RB-151. Refuted, and 132 is withdrawn

Previously: Negotiate never volunteers a slot the client did not submit (**RB-132**)

Measurement says: **Refuted, and 132 is withdrawn.** An **empty** `saves` array returned **13 downloads across two ROMs**, one never named by the client. The mechanism, driven: 13 ops, then `GET /api/saves/134/content` plus `POST /api/saves/134/downloaded`, then 12 ops with that save gone. Negotiate returns a download for every save the **device** has no current sync record for. **Refined by 163**: read "every save" as the newest row per slot, which is the only row negotiate ever looks at

**What 151 costs, beyond the correction.** [`docs/PLAN.md`](https://github.com/Spinnich/rommbat/blob/11348108f/docs/PLAN.md), the `save-sync` skill and stage 2a's
ledger all record "a fresh device cannot discover the saves the server holds for it" as a real
functional gap needing a separate inventory pass. There is no gap: negotiating with an empty
`saves` array **is** the inventory pass. And `SaveSlotStore.Map`'s fallback for a slot with no
local file, which 2a called provably unreachable, is provably reachable, so the two download
cases 2a closed negatively are open again.

## RB-152. Refuted on a real save, which is what 130 half-saw

Previously: A restore writes `file_name_no_tags` plus `file_extension` (plan, M6; F6)

Measurement says: **Refuted on a real save, which is what 130 half-saw.** `Phantasy Star (Brazil) [2026-08-17_17-01-00].srm` has `file_name_no_tags` `'Phantasy Star'`: the server strips `(Brazil)` as a tag. Writing that produces a filename libretro cannot see. The ROM's own stem plus the extension is the only sound source, and the negotiate operation carries neither

## RB-156. Not for a real divergence

Previously: A conflict arrives as a negotiate `conflict` action (stage 1's whole design)

The pass says: **Not for a real divergence.** A save changed on both sides negotiated as **`upload`**, reason `Client save is newer (no sync history)`, and the POST returned **409**. Negotiate decides from the hashes it is handed; the stale sync record is the part it cannot see. So 409 is the path, not the exception

## RB-157. Confirmed on real data

Previously: The server's archive digest is not our fold (148, 149)

The pass says: **Confirmed on real data.** Our fold `4eea879a…` against the server's `a92d31a4…` for the same unit, and a re-sync answered `no_op` only when the server's own value went back on the wire

## RB-160. It never replaces. Row identity is the datetime-tagged filename at one-second resolution, so the clock

Previously: `overwrite=true` replaces the row in the slot (plan, M6; **RB-150**)

The probe says: **It never replaces. Row identity is the datetime-tagged filename at one-second resolution, so the clock decides.** Two postings inside one second updated row 167 in place; the same pair a second apart made rows 167 and 168. `overwrite` is not part of row identity at all: it suppresses the 409 checks **and** the identical-content dedup. 150 read a same-second update as the general rule and is withdrawn

## RB-161. Only without `overwrite`

Previously: Identical content into one slot reuses the row unconditionally (F3, 148, and the stub)

The probe says: **Only without `overwrite`.** The server guards that check with `not overwrite`, measured: identical bytes with `overwrite=true` made row 164 where the same bytes without it reused row 163. So a replayed flush is still free, because a flush never sends `overwrite`, and a repeated `--keep-local` is not

## RB-162. It is a 404

Previously: An unregistered `device_id` resolves to no device and skips the conflict checks

The probe says: **It is a 404.** `overwrite=true` with `device_id=not-a-registered-device` answered 404 and wrote nothing, so an unregistered id cannot be used to impersonate a device. **The clause that followed, "a client must send a registered device id and cannot dodge the 409 path by omitting one", was never measured and is withdrawn**: omitting `device_id` is accepted, RB-249

## RB-163. It cannot. Negotiate only ever looks at the newest row per `(rom_id, slot)`

Previously: (not addressed) whether the row an append leaves behind can come back as a download

The probe says: **It cannot. Negotiate only ever looks at the newest row per `(rom_id, slot)`.** `backend/endpoints/sync.py` folds the user's slotted saves to one row per slot by `updated_at` before matching anything, and both the client-submitted pass and the unsubmitted-slot pass iterate that fold, which the source comments as "superseded older rows per slot are history, not downloads". Identical at `5.1.0` and `5.1.1-beta.2`. **Driven by probe 8**, which built the shape a resolution leaves (device B's row 169, then device A's row 170 with `overwrite=true` a second later) and negotiated as device A: naming the slot answered `no_op (Content is identical)` on row 170 and never mentioned 169, and an empty `saves` array mentioned neither. So the copy a `--keep-local` leaves one row down is unreachable through the sync protocol, and refines 151

**What this changes.** `SaveConflictResolver.KeepLocalAsync` is the only caller of
`overwrite=true`, so a resolution appends a row and leaves the server's copy one row down.
That is untidy rather than lossy, and 163 is the part that decides it: the row left behind is
no longer the newest in its slot, so negotiate stops mentioning it entirely and cannot offer
the rejected copy back as a download. **Until something writes into it:** `PUT /api/saves/{id}`
makes the row it touches the newest again, and on RomM 5.3.0-alpha.2 negotiate then offered a
peer's rejected copy as a download, so the client now refuses that case itself
(RM-4, case E). `autocleanup=true&autocleanup_limit=10` on every upload
then bounds the history. Without 163 this paragraph was reasoning rather than measurement, and
it is reasoning of the same kind that produced 150. The command's own success message said it
"replaced the server's copy" and no longer does. The stub modelled the replacement, so no test
in the suite could see the append.

**What remains a dependency rather than a gap.** `KeepLocalAsync` records a sync record for the
row it wrote and never acks the row it superseded, so that row keeps a permanent hole in this
device's sync history. 163 makes the hole unreachable rather than filling it, driven rather than
reasoned, and that stays a dependency on server behaviour rather than on anything this client
controls: it holds while negotiate pairs on the newest row per slot, and the mechanism #53 set
out would fire exactly as written if that ever stopped.

## RB-164. No. A negotiate cancels the device's previous active session, and completing a cancelled one is a 400

Previously: (not addressed) whether a sync session can always be completed

The probe says: **No. A negotiate cancels the device's previous active session, and completing a cancelled one is a 400.** Probe 8's two negotiates left sessions 193 and 194: `/sessions/193/complete` answered **400** and 194 answered 200. `sync.py` cancels active sessions for the device before creating the new one, and `complete` refuses any status outside `PENDING`/`IN_PROGRESS` with `Session is already {status}`. So a client that negotiates twice without completing cannot tidy the first one up

## RB-165. It does, driven end to end on the real install at `K:\RetroBat` against the live instance

Previously: (not addressed) whether the client end of all this behaves as documented

The probe says: **It does, driven end to end on the real install at `K:\RetroBat` against the live instance.** Phantasy Star (Brazil), `libretro:battery`, rom 239719: the peer diverged the server side (row 171) and the local side was diverged independently, the flush reported a **conflict rather than an error** and copied the local file to `replaced/`, `--keep-local` **appended row 172 beside 171 rather than replacing it**, said "sent it as the newest copy in the slot", pruned the copy aside, and **the next flush answered `2 already in step` with the local file byte-identical**. So 163 holds for RomMBat itself and not only for raw HTTP

**Taken by hand, once, on the real install.** RB-165 is the only place any of this has
been driven through `SaveConflictResolver` and `SaveSync` against a live server rather than
through the stub or through raw HTTP, and it is what closes the gap between "the server appends
and never re-offers" and "the product does the right thing with that". **It is not a
certification**: one shape (class A), one system, one emulator, and the diverged local save was
written by editing the file in place rather than by a game launch, because the launch exercises
save detection rather than conflict resolution and nothing in that path changed.

## RB-243. It cannot, and this is why #84 could not be fixed in the client's request

Question: (not addressed) whether `POST /api/sync/negotiate` can ever offer back a save this device uploaded

Measured: **It cannot, and this is why #84 could not be fixed in the client's request.** The server answers from its own per-device sync record rather than from what the client claims. A save acknowledged at upload and then deleted from the tree comes back `no_op`, "No changes since last sync", carrying the right `save_id` and `server_content_hash`, **even when the slot is claimed with `content_hash: null` and `file_size_bytes: 0`**. So the one save a restore exists for is exactly the one this endpoint will not offer, and `saves restore` reads `GET /api/saves` instead

## RB-244. It returns everything the server believes the device lacks

Question: (not addressed) whether an **empty** negotiate returns anything

Measured: **It returns everything the server believes the device lacks**, so enumerating absent slots into the request adds nothing. An empty `saves` array returned session 506 and **21** operations, each `"Save exists on server but not on client"`. Checked against local membership: **0 of the 21** were for an installed ROM and 21 of 21 were not, which is the ordinary `skipped, for games not synced here` count and is correct. The two facts together are what make 243 a server-state problem rather than a client-request one

## RB-245. Whether a save on the server always carries a slot

Question: (not addressed) whether a save on the server always carries a slot

Measured: **No.** Rows written by another client come back with `"slot": null`; two of the three saves for one ROM on the measured library did. `local_save.slot` is `CHECK`ed non-empty, so keying null as the empty string threw **SQLite error 19** from `LocalSaveStore.Record` **after the bytes were already on disk**, leaving a save in the tree with no row behind it and killing the rest of the batch. The slot has to be derived. The derived value is provisional: the next scan re-keys a loose save to the loose emulator, measured as `fceumm:battery` becoming `libretro:battery`, with no duplicate row

## RB-246. No, and nothing in the API allows it

Question: (not addressed) whether a state can be verified on arrival the way a save is

Measured: **No, and nothing in the API allows it.** `StateSchema` and `UserStateSchema` carry **no hash field of any kind** in the pinned 5.2.0 schema, where a save carries `content_hash`. States also have no `/track`, no `/downloaded` and no negotiate participation, so there is no per-device record and no acknowledgement either. A state download writes what arrives and says it is unverified; that is the ceiling of the API rather than a shortcut. A round trip was still confirmed byte-exact by hashing locally before and after: `2e4d4b06...` both sides

## RB-247. It strips parenthesised groups as well as bracketed ones

Question: (not addressed) what RomM puts in `file_name_no_tags`

Measured: **It strips parenthesised groups as well as bracketed ones**, so a state uploaded as `Legend of Zelda, The (USA) (Rev 1) [libretro.nestopia].state1` reads back as **`Legend of Zelda, The`**, losing region and revision. Building a restore destination from that name writes a state the emulator never looks for, and the file then reads as simply absent rather than as an error. `es_savestates.cfg` declares `{{romfilename}}.state{{slot}}`, so the **ROM on disk** names a restored state and only the extension comes from the server

## RB-249. No. Omitting it is accepted

Question: Omitting `device_id` on `POST /api/saves` is refused the way an unregistered one is (**162**)

Measured: **No. Omitting it is accepted**, and is how a second device is simulated on a one-device install. 162's 404 bounds impersonation, not participation, and its closing clause is withdrawn. Staging a conflict for `saves resolve` therefore takes `POST /api/saves?rom_id=<id>&slot=<slot>` with no `device_id`, plus a local edit

## RB-250. Nothing: it is a save outside the protocol

Question: (not addressed) what a **null-slot** save on the server can do

Measured: **Nothing: it is a save outside the protocol.** Negotiate keys on the slot, so it is never fetched (#138), and it also **cannot conflict**: a save uploaded through RomM's own web UI, which sets no slot, left this device's record for `libretro:battery` current and the next flush uploaded over it with no 409 and no mention. It still resolves to the **same destination path** as the slotted rows, so `saves restore` offered all of them together with nothing to say which wins (#156). **It now offers the newest and names the rest**, in stage 2 of #195, and this is what it did before. Never read the absence of a conflict as the server holding nothing newer

## RB-255. The class C paths do and the class A paths do not

Question: (not addressed) whether every path that writes server bytes updates `save_slot`

Measured: **The class C paths do and the class A paths do not.** `SaveSync.RestoreUnitAsync` and `SaveConflictResolver.FinishUnitAsync` both call `SaveSlots.RecordRestored`; `SaveSync.RecordRestored` and `SaveConflictResolver.KeepServerAsync` write `local_save` and stop. Measured: after a download of save 211 the row still read `save_id 209` with the pre-download hash. It does not self-correct, because the local file is then in step and the slot is never negotiated again, and the server-side record **is** updated, so nothing visibly breaks (#157). The two class A writers are independent, so fixing the download alone leaves keep-server broken. **Both now record it**, in the RomM 5.3.0 stage 4 change, and this row is what they did before

## RB-256. A state screenshot that does not link is untidy, and specific to where it was measured (138)

Question: A state screenshot that does not link is untidy, and specific to where it was measured (**138**)

Measured: **Neither.** It recurred on `nes` under `libretro`/`nestopia`, a different platform, emulator family and core from 138's `mastersystem` under `genesis_plus_gx`, uploaded and stored against the ROM at the right name and 3,367 B and still reading `screenshot: null`. And it is **a loss** once a state comes back down: a restore has only the state row's `screenshot` field to follow, so a real round trip returned the `.srm` and the `.state1` byte-identically and not the `.state1.png`. RomMBat's half, a restore that would not fetch even a linked image, is closed (#158); the unlinked row is RomM's, and step 5 of `platform-certification` cannot pass on the screenshot until it is. **At the 5.3.0-alpha.2 floor it is not a third.** Re-measured in stage 2 of #195: 7 of 7 `nes` states re-uploaded with a non-empty screenshot, under `nestopia`, `fceumm` and `mesen`, came back unlinked, and every one of the 9 `nes` states on the instance read `screenshot: null`. The multipart field is still `screenshotFile` in the pinned schema, so it is not a renamed field; the cause is not diagnosed. `docs/platforms/nes.md` section 5. **Superseded by RB-258**: diagnosed, and RomMBat's

## RB-258. RomMBat's, and a second defect sat beside it

Question: Why a state screenshot does not link, and whether it is RomM's (**138, 256**)

Measured: **RomMBat's, and a second defect sat beside it.** RomM has no link column: `State.screenshot` is `db_screenshot_handler.get_screenshot`, which matches an image whose `file_name` or `file_name_no_ext` equals the state's `file_name` or `file_name_no_ext`, where the extension is `\.(([a-z]+\.)*\w+)$`. RomMBat scoped the image's own name, so `Game.state1.png` went up as `Game.state1 [libretro.snes9x].png` against `Game [libretro.snes9x].state1` and could never match. Run over every emulator in `es_savestates.cfg`: the five whose `<image>` is `<file>.png` (libretro, dolphin, gopher64, pcsx2, mupen64) never linked, and the seven whose `<image>` replaces the extension (bigpemu, bizhawk, flycast, jgenesis, duckstation, openmsx, ppsspp) did, which is 138's "a third" on a `mastersystem` pass mixing libretro with bizhawk and jgenesis, and 256's 7 of 7 on three libretro cores. **The restore was the complement**: it built a state's name from the ROM stem and the server's extension, which placed nothing for the eight emulators that write the slot into the stem, and the restore preview on this install listed all three `nes` bizhawk and jgenesis states as "could not tell which slot it is". So no declared emulator could round-trip a state with its screenshot. Fixed by uploading the image as the state's upload name plus the image's own extension (`.jpg` for ppsspp), which matches for every declared emulator, and by reading the slot out of the uploaded name through the template. **Matching is not owning**: the pattern strips a lowercase-letters-only run as one extension, so libretro slot 0's `Game [libretro.x].state.png` has the `file_name_no_ext` of every slot of that game and core, and the lookup (exact-stem first, then highest id) answers a slot with no image of its own with slot 0's; an old-name slot 0 image `Game.state [libretro.x].png` answers for the autosave the same way. So the restore follows, and the push counts as kept, only an image named after its own state or the earlier name of its own image. `StubRomMServer.Binds` ports the filter and `ScreenshotFor` the ranking. Screenshots already uploaded for the five stay unlinked, because an unchanged state is not re-sent; the seven's earlier images still restore. The alpha.3 changelog's two screenshot fixes, #4478 and #4526, touch RomM's own player and streaming only; #4540 now renames a slotted **save's** screenshot to the save's stem server-side and does nothing for states. **Proven on a real emulator, 2026-09-20**, at the `5.3.0-beta.1` floor on `nes` under `libretro`/`nestopia`: three states made in one EmulationStation session, slots 2, 3 and 4 (RB-261: RetroArch's auto-index chose them, not `-state_slot`), all three rendering their own thumbnail in RomM's UI beside the 2026-09-12 slot 1 state that still shows none. Slot 2 was deleted with its `.png` and restored, and the image came back at md5 `ecd1d57f...`, **its own bytes**, where slots 3 and 4 share `d75aca69...` from the same frame. So the check is against the image of that slot and not merely against one arriving, which is what the second half of this finding makes necessary. `docs/platforms/nes.md`, step 5

## RB-259. Driven on `nes`, `libretro`/`nestopia`, with the maintainer at RomM's v2 player and at RetroBat

Question: (not addressed) what RomM 5.3.0-alpha.3's browser player does to a save this client syncs, on RetroBat

Measured: **Driven on `nes`, `libretro`/`nestopia`, with the maintainer at RomM's v2 player and at RetroBat.** Resuming this client's save (session A) put a new version in `libretro:battery`, and the flush brought it down as an ordinary `1 down`, raw 8,192 B battery RAM at `saves/nes/<rom>.srm` with a new name at offset 10, which nestopia then loaded from EmulationStation. The first nestopia launch rewrote 917 of the 1,180 changed bytes differently from EmulationStation's core and the quit hook uploaded that once; a second launch with nothing done rewrote the file with **identical content** and a newer mtime, and **that does go up, on every flush**: negotiate answers `upload` because the local mtime is newer than the row's `updated_at`, the server deduplicates the identical bytes into the same row without moving its `updated_at`, and the next flush asks again. Measured as `1 up` on three consecutive flushes with the slot still naming save 336. An earlier reading here said nothing went up, which took an unchanged `save_slot` for no upload; a deduplicated one leaves it unchanged too (#206). **This half does not reproduce at the `5.3.0-beta.1` floor, re-checked 2026-09-20 for #206 and left attributed to `alpha.3`**: asked directly, `s4-older-mtime.py` case M4 answers `no_op (Content is identical)` for a save whose bytes equal the row's with a local mtime 9.5 hours newer, and driven as a flush on the same install, touching `Legend of Zelda, The (USA) (Rev 1).srm`'s mtime forward moved nothing even with the client-side guard disabled. So the server compares the hash here at the floor and the repeat never starts. The guard is kept as defence rather than as a live fix: an `upload` whose offered `server_content_hash` equals the local `content_hash`, with nothing unsent, is answered as a no-op without a round trip. **Starting fresh (session B) is the defect**: the session filed under `autosave`, a slot this device held no row for, whose derived destination is the same `.srm`. The flush wrote it over the played save, reported `1 down` and no conflict, and re-keyed the file to `autosave`; the next scan re-keyed it to `libretro:battery` and uploaded the browser's fresh game into this client's own slot. Only the copy aside kept the played save. Fixed for #205: that download is now a conflict on the offered slot, and driven on the fixed build it reported `1 conflicted`, left the `.srm` untouched, and `saves resolve --keep-local` sent it into `autosave` as save 343. **Seen while putting it back**: negotiate answered `no_op` for a local file whose content differed but whose mtime was older than this device's last upload, and `s4-older-mtime.py` shows negotiate decides on `updated_at` alone there (#206). **Re-confirmed at the `5.3.0-beta.1` floor, 2026-09-20**: M1 answers `no_op (No changes since last sync)`, M2 `upload`, M3 `download (Server save is newer (no sync history))`, which is the issue's table unchanged. **Fixed for #206**: a `no_op` for a slot whose `content_hash` differs from `uploaded_content_hash` is uploaded instead of believed, because that inequality is evidence the server has never seen these bytes and the server has no way to be told so. Driven on the install at the floor: `Super Mario Bros. (World).srm` given different content and an mtime older than its row went up as `saves: 1 up` as save 345, where the same flush with the correction disabled reported nothing at all; restoring the original bytes deduplicated back into save 198, which is RB-160 holding

## RB-276. It did, and now it does not

Question: Whether a save another client wrote as the four bytes `null` reaches the tree

Measured: **It did, and now it does not.** RomM's browser player uploads the JSON literal `null` when it has no save to send, md5 `37a6259cc0c1dae299a7866489dff0bd`, and the server keeps it as a save. The flush's negotiated download wrote one as `saves/megadrive/Bare Knuckle III (Japan) [T-En by Twilight Translations v1.0].srm`, and earlier ones as `nes`'s Ninja Gaiden II and Super Mario Bros. Rom 189465 holds three: slotless, `autosave` (save 94, 2026-08-01) and `libretro:battery`, the last with no upload record in this device's store; Old Towers (rom 173367) holds a slotless one. `SaveSync` now refuses the hash before the transfer and the bytes after it, never acknowledges, and counts `refused, not a save` apart from failures, off the exit code; the restore preview lists the row as unplaceable. Driven on both paths against the live server

## RB-303. Yes, and 149's "not reproducible" is withdrawn

Question: Whether a client can reproduce the `content_hash` of an archive

Measured: **Yes, and 149's "not reproducible" is withdrawn.** RomM stored `4704b0bf...`, which is `hash_zip_contents` in `assets_handler.py` at the 5.2.0 and 5.3.0 tags: the md5 of `<entry name>:<entry md5>` lines, sorted by name, joined with `\n` and none trailing, directory entries skipped. The md5 of the zip's bytes was `3c4f71d6...`. 148's observations fit that rule exactly: framing is ignored and a rename moves it. `LogicalContentHash.Fold` is that rule, so the class C fold is the wire value and a restore is verified against it.

## RB-308. That row, not a new one

Question: What a peer's upload of bytes a row in the slot already holds produces

Measured: **That row, not a new one.** This device uploaded row 465; the peer device, having acknowledged it, uploaded different bytes as 466 and then the first bytes again, which came back as 465. Negotiate for this device then answered `download save_id=466 (Server save is newer (no sync history))`. A device with no sync record for a slot that has rows is refused 409 on its first upload into it.

## RB-309. Whether a device must acknowledge a peer's head that holds its own bytes

Question: Whether a device must acknowledge a peer's head that holds its own bytes

Measured: **Yes.** With this device's row 467 deleted and the peer's 469 holding the same bytes at the head, negotiate answered `no_op save_id=469 (Content is identical)`. This device's next edit was refused 409 "Slot has a newer save since your last sync"; after `POST /api/saves/469/downloaded` for this device the same upload landed as 470. `SaveSync.SettleOnHeadAsync` makes that acknowledgement, with no transfer.

## RB-327. It rewrote one, with no device, and RomMBat took it correctly

Question: What RomM's browser player does to a save another client made

Measured: **It rewrote one, with no device, and RomMBat took it correctly.** The game launched by accident in RomM's EmulatorJS during a Mesen session produced save 496 in `libretro:battery` at 15:28:26Z, with no device, named after PicoDrive's upload two minutes earlier and holding 8,191 B, `b98e4e38...`: PicoDrive's 32 KB save as Genesis Plus GX, which EmulatorJS runs, trims it. The next flush found the local `.srm` unchanged since it was last in step and the server's newer, downloaded it, and kept the displaced file in `emulators/rommbat/replaced/`. The same save data, and the behaviour the design asks for

## RM-3. Memory card endpoints are for the browser player, not for us (`source`, then `measured`)

Eleven new endpoints under `/memory-cards`, with versions, sharing and visibility. Read
alongside the Streaming V2 entry, these exist to give container pooled PS2 and GameCube browser
sessions a server managed card.

**That is the opposite direction from RomMBat's model**, which is a real emulator on a real
disk writing real files, and whose class C and class D work converts shared containers to per
game wherever the emulator allows. Memory cards are not our save transport, and this finding
exists mainly so the next session does not re-litigate it.

Two uses were filed as surviving, both narrow: #82's raw GameCube card finding a first class home
server side, and interop with a card a browser streaming session wrote. **Stage 4 (#169) settled
both against the conclusion**, first from source at tag `5.3.0-alpha.2`, then on the live server,
which needs no streaming broker for the card routes.

**Read in source.** `MemoryCard` is scoped by `(user, emulator)`, and its own docstring says one
Dolphin card serves both GameCube and Wii. There is no ROM on the record at all, so a card is a
class D container by construction rather than something that can be made per game. Its data is a
history of `MemoryCardVersion` rows, each "the whole card image, e.g. the zipped PCSX2 folder
card". The streaming claim describes a Dolphin card by reading `.gci` names out of the zip
(`summarize_card`), so the Dolphin card upstream exchanges is a **GCI folder**, which is class C,
and not the raw `SRAM.<REGION>.raw` that #82 is about.

**Measured, 2026-09-16,** with `tools/romm-5.3-probes/s2-memory-card-record.py`, on a throwaway card
deleted afterwards:

| Question                            | Answer                                                                                                   |
| ----------------------------------- | -------------------------------------------------------------------------------------------------------- |
| What a card record holds            | `id`, `user_id`, `emulator`, `name`, `slot` (always 1), `is_public`, `platform_id`, timestamps           |
| A card never synced                 | `GET /{id}/content` is 404, "Memory card has no stored data yet"                                         |
| Is a version whole or a delta       | **Whole.** A two-game zip came back byte-identical, 376 B, both `.gci` members                           |
| `content_hash`                      | over the zip's contents, not its bytes: `5fa2e028...` stored against an MD5 of `a398e3dd...`             |
| Is an identical upload deduplicated | **No.** Two uploads of one zip made two versions with one hash, as source says of the upload route       |
| A bare `SRAM.USA.raw`               | **400**, "not a readable zip archive"                                                                    |
| A zip holding `SRAM.USA.raw`        | **200.** The server never looks at the layout inside, so it cannot say whether an emulator would take it |

**What that does to the two uses.** #82 gains nothing: the only card the server would store is a
zip it does not validate, and the format streaming would hydrate for Dolphin is a GCI folder, the
per-game `.gci` files this client already syncs as class C units. Interop would mean reading
`.gci` members out of a whole-card version for the games a device holds, and writing back would
mean rebuilding a whole card, which is two writers on one container with no per-game identity on
either side. So the conclusion stands and is now evidence rather than a reading: **not a
transport, and not a bridge worth building**. PCSX2's card is a folder card upstream, which points
the same way as steering PCSX2 itself onto `folder`; what that choice writes on a RetroBat install
is unmeasured and is #80.

## RM-4. Two new writers on the saves the conflict route was measured against (`source`, then `measured`)

`emulatorjs.auto_save_sync` uploads whenever the emulator writes. Streaming V2 pulls saves and
states back with history, under `STREAMING_STATE_HISTORY_LIMIT` defaulting to 50 states per
rom, emulator and user.

Server side save rows can now change far more often, and from more directions, than when this
repo's negotiation behaviour was measured at 5.2.0. That lands on #156, #157 and #138, and on
the in flight write guard. Neither feature is on by default, which bounds the blast radius and
does not remove it.

**Stage 4 (#170) measured the browser writer and read the streaming one.** The server behind the
`Live*` tests runs with `EJS_ENABLE_AUTO_SAVE_SYNC` false and streaming disabled with no containers,
so nothing here was a browser session or a streaming session. What made the browser half
measurable anyway is that the flag adds no route.

**Read in source: the browser writes in place, and only the rate is new.**
`frontend/src/views/Player/EmulatorJS/utils.ts` `saveSave` makes one of two calls. With a save
loaded it sends `PUT /api/saves/{id}` with the row's own `file_name` and `device_id` only; with none
loaded it sends `POST /api/saves` with `emulator` set to the core and **no slot**, then holds the row
it made. `auto_save_sync` calls that on EmulatorJS's save interval, gated on two identical ticks,
where Save & Quit called it once. The update route keeps the id, the name and the slot, writes
`content_hash`, lets `updated_at` move on update, and runs no 409 check, no dedup and no device
check.

**Measured, 2026-09-16,** with `tools/romm-5.3-probes/s1-browser-save-writer.py`, which replays
those exact calls against a throwaway slot on one ROM, with `device_id` omitted on the browser's
calls as an ordinary web login sends it, and deletes every row and device afterwards:

| Case                                                                            | What negotiate answered                                                   |
| ------------------------------------------------------------------------------- | ------------------------------------------------------------------------- |
| A. browser PUT over this device's row, local unchanged                          | `download`, same save id, new hash, "Server save is newer than last sync" |
| B. the same, and local also changed                                             | `conflict`, "Both sides changed since last sync"; ordinary upload 409     |
| C. keep-local appends a row, then the browser PUTs into the older row it loaded | `conflict` against the **older** row, which now lists first               |
| E. as C, but the older row was a peer's, never synced by this device            | **`download`**, "Server save is newer (no sync history)"                  |
| D. nothing loaded: one POST, then two PUTs                                      | one null-slot row, same id throughout; a fresh device is never offered it |

**The conflict route recognises the browser writer, with one exception, and the exception is a
rule this repo wrote down as a dependency.** A, B and C are the answers a client wants. E is the
case [`docs/PLAN.md`](https://github.com/Spinnich/rommbat/blob/b1648fb05/docs/PLAN.md) described as "were negotiate ever to volunteer a superseded row, the resolution
would be undone by the next flush", and it is what happens: with no sync record for the revived row
negotiate compares timestamps, the browser's write is newer, and the next flush would overwrite the
save a person chose to keep with a continuation of the one they rejected. **Acted on in stage 4**:
a download naming a save id lower than the slot's recorded one is recorded as a conflict, since
ids only grow and a lower id at the head of a slot means something wrote into an older row or
deleted the newer one. Only the first was measured to reach a download; the second is unmeasured,
and a conflict is the answer that loses nothing either way. That
reads the recorded id, which is what made #157 a prerequisite rather than a tidy-up, and both
class A writers now record it.

**Driven on hardware after the fix**, RetroBat 8.2.1 on the install's own account, `nes` under
`libretro`/`nestopia`. EmulationStation launched `Destiny of an Emperor (USA)` with no save
anywhere, RetroArch wrote an 8,192 B `.srm` on close, and the `quit` hook's pass uploaded it as
save 225. That is the core initialising cartridge RAM, 4 non-zero bytes, and not a player save.
Then case E by hand: a peer row 226, a local edit, a flush that recorded the conflict through the
409, keep-local appending 227, and the browser's `PUT` into 226. **The next flush recorded a
conflict naming 226 as older than 227 and wrote nothing**, where the build before it would have
taken the download. Keep-server then brought the browser's bytes down and `save_slot` read 226; a
flush with nothing changed moved nothing; a newer peer row 228 came down as an ordinary download
with `save_slot` following it to 228, so neither #157 path and not the new refusal misfired. Every
row and file the pass made was deleted afterwards.

**D is #138 at a higher rate, not a new defect.** A browser session that loads nothing makes one
null-slot row and keeps writing into it, so it does not pile up rows, and the protocol still cannot
see it. `saves restore` still can, with #156's collision, since fixed in stage 2 of #195 by
offering only the newest row per destination and naming the rest.

**Read in source at `5.3.0-alpha.2`: the browser's states (#190). Only one writer rewrites a state
in place, and it cannot reach a row this client holds, so nothing was measured.** Three frontend
paths post a state, and none of them calls `stateApi.updateState`, which is defined in
`services/api/state.ts` and called nowhere.

| Writer                                                     | Route, name, emulator                                                        | In place |
| ---------------------------------------------------------- | ---------------------------------------------------------------------------- | -------- |
| `views/Player/EmulatorJS` (the v2 player wraps the same)   | `POST /api/states`, `<fs_name_no_ext> [<ISO timestamp>].state`, the EJS core | no       |
| `console/views/Play.vue`                                   | `POST /api/states`, `state.save` every time, `emulatorjs`                    | **yes**  |
| `v2/components/GameDetails/SaveDataTab.vue`, manual upload | `POST /api/states`, the uploaded file's own name, no emulator                | by name  |

`auto_save_sync` changes nothing for states: `installAutoSaveSync` subscribes to `saveSaveFiles`
only, so a state is still written on a save-state press and on Save & Quit. The console view
rewrites because `store_state_file` in `handler/asset_store.py` updates whatever row already holds
`(user, rom, file_name)`, the upsert `romm-api` records as measured, and a fixed name makes every
save that one row.

**Neither player reaches `StateSync`.** Every upload this client makes is named
`<stem> [<emulator>[.<core>]]<ext>`, and neither a timestamp nor `state.save` can equal that, so no
player write lands on a row this device sent. Restore reports both shapes as unrestorable rather
than placing them: `emulatorjs` is not declared in `es_savestates.cfg`, and the two EJS cores that
share a declared emulator name, `ppsspp` and `desmume`, produce a `.state` that neither emulator's
`<file>` template matches.

**A manual upload under this client's exact name is the one writer that can land on its row**,
such as a state downloaded from the web UI and uploaded again. It replaces the bytes under the same
id and **clears the emulator**, because the update writes the caller's emulator and the upload
sends none. Read in code, `StateSync` does nothing about it: `RunAsync` decides what needs sending
from the local hash alone and never reads the server row, and restore skips a destination that
already exists. The next local change overwrites the upload without a word. That is the same last
writer wins that two devices on one account already get, because states have no conflict route.
Recorded rather than acted on, since no player write can take this path.

**Read in source, not measured: streaming.** `handler/streaming/saves.py` stores each pulled save
archive as a **new null-slot row**, `<rom stem> [<emulator> <timestamp>].saves.zip`, dropped when
its hash matches any save already held for the ROM. Negotiate never offers one. `saves restore`
would list one as restorable to `saves/<system>/<stem>.zip`, because a null slot matches no
declared unit path, and `--apply` then fails its hash check, because the server's digest over a
zip is not the MD5 of its bytes: closed, with a preview that promised more.

`handler/streaming/states.py` keeps every capture as a state row and prunes past the limit with
`user_states_for_emulator`, which filters the user's states for the ROM **on the emulator name
alone**. It does not ask who wrote them. This client uploads states with the emulator field set to
`emulator[.core]`, which is `pcsx2`, `dolphin` and `xemu` for three standalones streaming also names
that way, so a game streamed past fifty captures loses this device's oldest state rows on the
server. The local files survive and are not re-sent, because a state is in step by the hash this
device recorded. Libretro states go up as `libretro.<core>` and do not share streaming's
`retroarch` budget. Recorded rather than acted on: streaming is off on the only server reachable,
so nothing here has seen it happen.
