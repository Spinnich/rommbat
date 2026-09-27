# Saves, states and play sessions

Part of the [romm-api](SKILL.md) skill. The save, state, play-session and sync-session routes, and what they do that the schema does not say.

## Traps

- **`download_path` on a save is not a usable URL.** It is served with a raw space and an
  unencoded `+`: `/api/saves/130/content?timestamp=2026-08-10 23:00:25.474218+00:00`. Build
  the URL from the save `id`.
- **`POST /api/saves/delete` fails the whole batch if one id is already gone**, answering 404
  and deleting nothing. Autocleanup can remove an id between listing and deleting, so delete
  one at a time or re-list immediately before.
- **Saves pair on `(rom_id, slot)`.** A null slot means "archival manual upload" and
  negotiates as `upload` forever. Always send a stable, non-null slot.
- **The server renames uploaded saves** to `<name> [YYYY-MM-DD_HH-MM-SS]<ext>`. Persist the
  `file_name` from the response, not the one you sent. **To write one to disk use
  `file_name_no_tags` + `file_extension` instead**: an emulator finds a battery save by rom
  name and never sees the tagged one. No client-side regex; the server returns the stem.
- **`optimistic` on `GET /api/saves/{id}/content` defaults to true and records the device
  sync on the request**, before the client has the bytes. A device that had never synced went
  to `is_current: true` by issuing the GET alone. Always pass `optimistic=false` and send
  `POST /api/saves/{id}/downloaded` after the bytes are written and verified, or a download
  that dies mid-body leaves the server sure the device is current and the next negotiate
  answers `no_op` forever.
- **What decides whether a slotted upload appends is the clock, not `overwrite`.** The server
  renames the upload to carry a `[YYYY-MM-DD_HH-MM-SS]` tag and then looks the row up by
  **that** name, at one-second resolution, so two postings into one slot inside one second are
  one row and two a second apart are two. **`overwrite=true` never replaces a row.** What it
  does is suppress the 409 checks **and** the identical-content dedup. Measurement 160.
- **Identical uploads dedup within a slot** (same row reused, count unchanged) **only when
  `overwrite` is absent**, which is what makes a replayed flush safe and a repeated
  `--keep-local` not. Measurement 161. `autocleanup` defaults to **false** and
  `autocleanup_limit` to 10, so a slot grew unboundedly unless you asked it not to, up to
  5.3.0-alpha.2. **From alpha.3 the server prunes on each slotted upload whatever the client
  sends**, past the tighter of `MAX_SAVES_PER_SLOT` (env, default 50, `0` disables) and the
  client's own `autocleanup_limit`. RomMBat sends `autocleanup=true&autocleanup_limit=10`, so its
  cap is 10 and the server's bites only on other writers' versions. `slot` is capped at 255
  characters, a 422 past it. Read in source; `romm-5.3-findings.md` finding 11.
- **An unregistered `device_id` is a 404**, not a request that quietly proceeds device-less, so
  a client cannot dodge the 409 path by sending an id the server does not know. Measurement 162.
- **A 409 on upload carries a bare string**, `{"detail": "Slot has a newer save since your
last sync"}`, with no save id and no timestamps. Fetch the save row separately to show the
  user anything. It fires when **this device's** record is stale, so the device that wrote the
  current save may write again while a device that never synced it is refused.
- **`device_syncs` is empty unless you pass `device_id`**, and empty reads exactly like
  "nobody has synced this". With `device_id` set it lists every device that has a record, the
  queried one first. A device that never synced is **absent** rather than `is_current: false`,
  so treat a missing entry as the strongest reason to pull.
- **`origin_device_id`** names the device that uploaded a save, which is how you recognise
  your own upload coming back.
- **`POST /api/play-sessions` takes an envelope**, `{device_id, sessions: [...]}`, with
  `device_id` outside the entries; a bare array is a 422. It answers a per-index result array
  with `created_count`/`skipped_count` and reports a replay as `"status": "duplicate"`. Cap
  100 per call (101 entries answers 400), `end_time` strictly after `start_time`, `rom_id`
  optional. It needs **no** open sync session, so playtime can flush on its own.
- **`GET /api/play-sessions` can answer `200` with zero rows for a session that exists**, two
  ways, and neither is distinguishable from one never written. It is scoped to the authenticated
  user (`roms.user.read`), so a token on any other account reads nothing for an install it did
  not pair, whatever its scopes. And the row carries the **RomM-side** `device_id`, not the local
  `client_device_identifier`, which are different values `status` prints on adjacent lines, so a
  `?device_id=` filter given the local one matches nothing. Reading a session back needs a token
  on the account the install is paired as; `DEVELOPER_SETUP.md` covers the one certification
  passes use. **`RomMConnection.ListPlaySessionsAsync` is the client's read and `status` prints
  it** under a `Playtime` block, filtered by `RomMDeviceId` and only when the server is reachable
  and `roms.user.read` was granted, which is what makes step 8 answerable from the agent (#208);
  `docs/platforms/nes.md` step 8 is the worked case from before it existed. The endpoint promises
  no order, so take the newest by `end_time` rather than the first row, and sort by it before
  listing the ten newest, which `status` does under `recent:`, or the whole window with
  `--all-sessions`. An empty answer stays
  ambiguous however it is read, so whatever prints it says so.
- **Ingesting a play session sets `rom_user.now_playing`, and nothing clears it.** Every
  session RomMBat sends is finished by construction, so a client that only posts sessions
  leaves the user's library claiming they are playing every game they have ever launched.
  Measured on the live instance during M7 stage 7b-3: ten roms RomMBat had reported a session
  for were all `now_playing=true`, one of them played two days earlier, against a rom it had
  never reported reading false. **Clear it with `PUT /api/roms/{id}/props`**, per rom rather
  than per session, and only for the entries the batch's result array says were accepted.
  See the Presence section for why the heartbeat is not the answer.
- **`PUT /api/roms/{id}/props` with a partial body leaves the other seven properties alone.**
  Measured, not read off the schema: the schema declares all eight nullable with none
  required, which is equally consistent with "an omitted field is set to null", and a wrong
  guess would silently wipe a user's rating, difficulty, completion and notes. A write
  carrying only `now_playing` left a rating of 7 in place. It answers a body; a client that
  wants only "it worked" should not read it, since a 2xx with an empty or non-JSON body
  otherwise throws out of whatever tidy-up made the call.
- **`POST /api/sync/negotiate` requires `device_id`** unless the client token is device-bound,
  in which case the server infers it. Measured both ways: a pairing-minted token negotiates
  with the field absent, an ordinary client token answers 400 naming the condition. RomMBat's
  token comes from pairing, so it may omit it; send it anyway, it is more explicit.
- **A sync session cannot be deleted.** `/api/sync/sessions` is read-only apart from
  `/complete`, so every negotiate leaves a permanent row. Tests and probes that negotiate
  accumulate them.
- **Asset uploads are capped at 512 MiB** and rejected with 413 before the body is spooled.
- **States are not in the negotiate protocol.** `POST /api/states` has no slot, device or
  conflict detection, and `StateSchema` carries **no `content_hash`**. Best-effort only, and
  "is it in step" is answerable only from a hash the client recorded itself.
- **`POST /api/states` is an upsert keyed on `(rom_id, file_name)`, and the `emulator` is not
  part of the key.** Three posts of one name reused one row across two payloads; five posts of
  one name under five different emulator values also reused one row, overwriting its emulator
  and moving its stored path. So there is no append to prune and no `autocleanup` to ask for,
  but **the uploaded name has to carry the emulator and core** or two cores writing one filename
  for one ROM collapse into a single row. Two names differing only in a bracketed tag do produce
  two rows, so tagging works. `PUT /api/states/{id}` exists and is unnecessary, and no frontend
  path at 5.3.0-alpha.2 calls it either. The browser rewrites a state only through this upsert,
  from the console view's fixed `state.save` name (finding 4).
- **`PUT /api/saves/{id}` rewrites a save row in place.** Id, tagged `file_name` and slot stay,
  `content_hash` and `updated_at` move, and there is no 409 check, dedup or device check. RomMBat
  never sends it. At 5.3.0-alpha.2 RomM's browser player did, for the save it loaded and on every
  save tick under `auto_save_sync`; from alpha.3 it `PUT`s only the version its own session
  created. A save id therefore does not name its bytes, and a superseded row can return to the
  head of its slot, now by the server's per-slot prune deleting the row above it. `save-sync`
  holds the consequences.
- **`/api/memory-cards` is not called and is not a save transport.** A card is scoped by
  `(user, emulator)` with no ROM, a version is a whole zipped card, and only a zip is accepted.
  Measured at 5.3.0-alpha.2; `save-sync` again.
- **The server does not rename a state.** A save comes back tagged
  `<name> [YYYY-MM-DD_HH-MM-SS]<ext>`; a state comes back exactly as sent.
- **A zero-byte `screenshotFile` is accepted and stored** as a real screenshot row, so the
  client has to refuse the empty case itself.
- **`emulator` is not sanitised.** It becomes a directory segment in the stored asset's
  `file_path`, and a value containing `/` became two segments. Never send one.
- **`POST /api/sync/negotiate` volunteers slots the client did not submit**, so negotiating
  with an **empty** `saves` array is the inventory pass a fresh device needs. It answers a
  `download` for every slot the device has no current sync record for, and stays quiet about a
  slot the device did sync and no longer sends, which it reads as a deliberate local delete.
  Measurement 151, which withdraws 132.
- **What negotiate pairs on is the newest row per `(rom_id, slot)`, and only that row.** Read
  from `backend/endpoints/sync.py` at both `5.1.0` and `5.1.1-beta.2`, which are identical
  here: the server folds its slotted saves to one row per slot by `updated_at` before matching
  anything, and both the submitted and the unsubmitted pass walk that fold. **A superseded row
  in a slot is history and is never offered as an operation while it stays superseded.** That no
  longer makes an appending upload safe on its own: the in-place `PUT` above puts one back at the
  head of the slot, where negotiate can offer it as a `download`, and the client refusing that is
  what keeps a keep-local from being undone (`save-sync`). Measurement 163, read from source and then driven against
  a slot holding a superseded row.
- **A negotiate cancels the device's previous active session**, so `/sessions/{id}/complete` on
  that earlier one answers **400** `Session is already cancelled`. Complete a session before
  negotiating again, or accept that the first one can never be tidied up. Measurement 164.
  Closing needs `devices.write`, and a refused close returns a failure rather than throwing, so
  `SaveSync` reads it and reports every refusal except `already COMPLETED`, which means the
  close landed. A 403 there otherwise reads as a clean sync with the session left open (#90), so
  a refused close sets `SaveSyncOutcome.SessionLeftOpen` and the flush ends `Partial`, with the
  403 naming `devices.write` (#148).
- **There is no `is_favorite` and no `playtime` on rom props.** Favourites are collection
  membership; playtime lives in play sessions.
