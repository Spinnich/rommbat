---
name: romm-api
description: Calling the RomM API from RomMBat - device pairing auth, the endpoints a sync client needs, required scopes, and the API's non-obvious traps. Use whenever writing or changing code in RomM.Client, choosing an endpoint, or debugging a 401/403/409.
---

# RomM API

The backend is the contract. DTOs are generated from `/openapi.json` (served at the
**root**, not under `/api`) and **committed**, pinned to RomM 5.2.0, the minimum supported
version. The floor tracks the newest RomM stable, so the pin moves with it and the two are
one decision. The published docs at docs.romm.app have drifted from the server on exactly the
payloads this client needs most, so never code from them.

- The pin, the generator, and why the schema is normalised first:
  `src/RomM.Client/openapi/README.md`. Regenerate only when deliberately moving the pin.
- **`SocketsHttpHandler.ConnectTimeout` is set explicitly on every handler** (2 s
  interactive). Nothing sets it by default and an unreachable LAN host stalls 21 s.
- **Never `catch (TaskCanceledException)` bare.** A connect timeout and a user cancellation
  are the same type; route everything through `RomMTransportErrors.Classify`.
- **401 and 403 are results, not exceptions.** Authenticated calls return `RomMResponse<T>`.
  Only transport failures throw (`RomMUnreachableException`).

## Auth: device pairing only

No password entry, no token pasting, not `POST /api/client-tokens/exchange`. A gamepad is
a terrible keyboard and the pairing flow exists to avoid typing a credential.

1. `POST /api/auth/device/init` (unauthenticated) with `{client_device_identifier, name,
client, platform, client_version, requested_scopes}` returns `{device_code, user_code,
verification_path, verification_path_complete, expires_in: 600, interval: 5}`.
2. Show `user_code` plus a QR of **the configured origin joined with
   `verification_path_complete`**. The server returns a relative path on purpose and stays
   origin-agnostic, so joining is the client's job.
3. Poll `POST /api/auth/device/token` with `{device_code}` at `interval`, handling
   `authorization_pending`, `slow_down`, `access_denied`, `expired_token`. **Every one of
   those arrives as HTTP 400 with the reason in `detail`**, so none of them is an exception;
   429 is the rate limit and also not a failure. `DevicePairing.AwaitApprovalAsync` owns the
   loop.

Token expiry is the **approver's** choice, not the client's: `expires_in` is a field on
`/approve` and accepts only `30d`, `90d`, `1y` or `never`. The client reads `expires_at`
back off `/token` and stores it.

The code is **8 characters from `ABCDEFGHJKMNPQRSTUVWXYZ23456789`**, not 8 digits. I, L, O,
0 and 1 are excluded. The server normalises hyphens, spaces and case, so display it
grouped (`ABCD-EFGH`).

Pending state is Redis-only with a hard 600s TTL: show a countdown and a one-button
restart. Rate limits: init 10/min/IP, token 60/min/IP, plus per-code pacing. **The init
limit binds the test suite too:** one pairing per live test exceeds it, so live tests share
one pairing per class.

**Identity is `client_device_identifier`**, a GUID stored in the tree. Pairing looks the
device up with `get_device_by_client_identifier` and records no host details, which is what
makes a portable install survive moving between machines. **Do not call `POST /api/devices`
with `mac_address`/`hostname`**: its fingerprint dedup matches on MAC alone and would
collide with other clients.

Handle a narrowed grant: the approver can reduce `approved_scopes`, and `/token` returns
what was actually granted. Degrade by feature, never 403 later. Treat 401 as expected, not
exceptional: keep the database and outbox, return to pairing, resume after re-pair.

CSRF does not apply when an `Authorization` header is present.

## A rom row advertises media it does not serve

**Measured on a live 5.2.0 library: 39 of 40 games on one platform advertised a video path that
answered 404.** The rom row carries the path whether or not the asset is on disk, so a client
that trusts the row asks for every one of them on every sync, gets a 404 each time, and reports
it as a failure.

**Treat a 404 on an advertised asset as a fact about the library, not about the request.** It is
the one media refusal that will answer identically until the row itself changes, which makes it
the same class of thing as a 416 on a resume. `RomMResponseStatus.NotFound` exists to keep it
apart from `ServerError` for exactly that reason.

RomMBat's answer is to forget the path, which turns the case from a failure into the ordinary
`Missing` one and needs no new state: a resolve rewrites `metadata` from the server wholesale,
so the path returns the moment RomM starts serving it.

**Do not assume the whole kind is absent for the platform.** It was 39 of 40, not 40 of 40.

## A coverage percentage is a fact about one library on one day

**How much of a kind a library holds says when its platforms were last scraped, and with what
settings.** It says nothing about what RomM can serve, what ScreenScraper holds, or what the
next instance will look like. Sampling three platforms on a live 5.2.0 instance showed
`box2d_path` at 0% on megadrive and 100% on atari2600, with `box3d_path` the exact inverse,
which reads as a per-platform property of the upstream scraper and is not one: that library's
older platforms predate `box2d` in that form, and its owner had recently stopped storing
`box3d`, so only the recently scraped platform reflects the new setting.

**So never decide what to support from a coverage number.** Support what the schema exposes and
let an absent path be the ordinary `Missing` case, which is what `MediaSync` already reports.
The same reasoning is already in `GameSync`'s remarks for why artwork cannot be part of the
whole-game invariant: an administrator may not have scraped a kind, and upstream may never have
held it, and neither is a fault or fixable by re-running. A kind measured at 0% today is one
rescrape away from 100%.

**Quote the library and the date beside any such number**, or a later session will read it as a
property of RomM. Finding 239 is the worked example, including the wrong reading first.

## `fs_size_bytes` can be stale against the file the server serves

**Reported from a live library on `fbneo`: the size on the rom row disagreed with the bytes the
content endpoint delivered**, because the records on that instance were out of date relative to
the files. The client is right to notice, and a rescan on the server is the fix, but the failure
surfaces as RomMBat refusing a download that is perfectly good.

Two consequences for anything verifying a transfer:

- **Say what a mismatch probably means.** "The download is X but the server said Y" is accurate
  and tells nobody what to do. The likely cause is a library record that has not been rescanned,
  and the message says so.
- **Do not accept the file on a matching hash instead.** It was considered and dropped: if the
  file on the server was replaced, its size and its hash go stale together, so the extra hash
  confirms the refusal rather than rescuing anything. The case where only the size is stale
  could not be produced in 120 `fbneo` and 40 `megadrive` downloads against a live instance, and
  weakening the one check between a corrupt download and an unbootable game needs evidence, not
  a plausible story.

**Verify with md5 and nothing else.** Every successful download is hashed; the size test is a
fast rejection in front of it. RomMBat used to compute md5, sha1 and crc32 in one pass and
compare only md5, or sha1 where the server published no md5. Measured across **1,616 rom rows**
from three platforms of a live library, **not one carries a sha1 without also carrying an md5**:
RomM hashes a file once and sets every hash column or none, so the sha1 comparison served
nothing. crc32 was never compared anywhere at all.

The cost of that, measured on a 3.41 GB image already in the OS cache so the numbers are
processor rather than disk:

|            | throughput   |
| ---------- | ------------ |
| read only  | 7,637 MB/s   |
| md5 only   | **594 MB/s** |
| md5 + sha1 | 339 MB/s     |

**Do not reason about this from a development box.** There a 34.5 MB/s download leaves an order
of magnitude of headroom and verification looks free. RomMBat's target is a handheld off a cheap
stick, where the link can be several times faster and the processor several times slower, and
verification is then the thing that decides how long a sync takes. Migration 013 dropped both
columns for that reason.

## Scopes

**Two roles. Do not conflate them.**

| Role                                   | Scopes                                                                                                                                                       |
| -------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| The **device** (what RomMBat requests) | `me.read`, `roms.read`, `platforms.read`, `collections.read`, `firmware.read`, `assets.read`, `assets.write`, `devices.read`, `devices.write`, `roms.user.*` |
| The **approver** (test harness only)   | `me.read` and `me.write`, nothing else. Its **account** needs the device set, since that is what caps `allowed_scopes`                                       |

Never needed by either, and dangerous to grant: `users.read`, `users.write`, `roms.write`,
`platforms.write`, `tasks.run`, `logs.read`.

`me.write` is **not** a device scope and RomMBat never asks for it. `/approve` and `/deny`
require it, so only a harness token carries it. A token without it fails the route guard
with a bare 403 `Forbidden` before the code is looked up; a scope-subset rejection instead
says `Approved scopes exceed what's allowed for this user`. The route guard checks the
**token's** scopes, `allowed_scopes` is computed from the **account's**.

## Endpoints that matter

| Need                     | Call                                                                              |
| ------------------------ | --------------------------------------------------------------------------------- |
| Version/capability probe | `GET /api/heartbeat` (unauthenticated, `SYSTEM.VERSION`)                          |
| Platforms                | `GET /api/platforms?updated_after=`                                               |
| ROMs                     | `GET /api/roms?...&with_files=true&limit=&offset=`                                |
| Deletion reconcile       | Set re-resolution. **Not** `GET /api/roms/identifiers`, which 504s at scale       |
| Match local files        | `GET /api/roms/by-hash?md5_hash=` (a miss costs 8.3 s)                            |
| Download a ROM           | `GET /api/roms/{id}/content/{fs_name}`                                            |
| Firmware, one platform   | `GET /api/firmware?platform_id=`, `GET /api/firmware/{id}/content/{file_name}`    |
| Presence, "playing now"  | `POST /api/activity/heartbeat`, `GET /api/activity`, `GET /api/activity/rom/{id}` |
| Save negotiation         | `POST /api/sync/negotiate`                                                        |
| Save upload              | `POST /api/saves?rom_id=&slot=&emulator=&device_id=&session_id=&autocleanup=`     |
| Save download            | `GET /api/saves/{id}/content?device_id=&optimistic=false`                         |
| Save download ack        | `POST /api/saves/{id}/downloaded`, body `{device_id}`, after the bytes verify     |
| Slot inventory for a ROM | `GET /api/saves/summary?rom_id=`                                                  |
| Close session            | `POST /api/sync/sessions/{session_id}/complete`                                   |
| Playtime                 | `POST /api/play-sessions`, body `{device_id, sessions: [...]}`                    |
| Roaming config           | `PUT /api/devices/{id}` (free-form `sync_config` dict)                            |
| Firmware, whole library  | `GET /api/platforms`, whose inlined `firmware[]` carries every `md5_hash`         |

## Traps

- **Always** pass `with_char_index=false&with_filter_values=false` to `/api/roms`; they cost
  a flat 841 KB per request. Page size 250, `order_by=id&order_dir=asc` so a ROM added
  mid-walk lands past the cursor instead of shifting every later page.
- **`with_rom_id_index` follows the scope; it is not always-off.** Under a scoping parameter
  (`platform_ids`, `collection_id`, `smart_collection_id`, `virtual_collection_id`) the index
  spans that scope, not the library, and it is what lets the server serve the page by primary
  key. Measured at 88,331 roms on 5.2.0: **scoped, turning it off costs 3.4 to 3.7 times the
  latency** (2.3 s to 8.5 s a page) to save 63 KiB. Unscoped it costs about 1.15 times to
  save 604 KiB. **Off only when the request is unscoped.**

  **`CatalogQuery` obeys this as of M7 stage 7b-2a, and did not before.** This rule was written
  from A1's measurement and the code went on sending a constant `false` for a further two
  stages, which is #88. What it cost end to end: a platform-scoped resolve of 9,196 roms took
  **8 m 15 s**, 13.4 s a page at 250, where `RomPager`'s own comment records 2.5 s a page for
  the unscoped case. **A rule in a skill is not a rule in the code**, and this one went
  unnoticed until a stage put the walk behind a screen somebody had to sit and watch.

- **`with_total` rides on that flag.** `resolve_total()` returns `len(rom_id_index)`, so the
  count is free with the index on (1 ms) and costs 124 ms with it off. Keep it on; do not
  pair it with an index opt-out on a scoped walk, which is the one combination that pays for
  both. See [argosy-findings.md](../../../docs/argosy-findings.md), A1 and A2.
- **`fs_size_bytes` is an `int32` in the generated DTOs.** The pinned schema declares a bare
  `integer`, so `SimpleRomSchema`, `PlatformSchema` and `RomFileSchema` all overflow.
  `GET /api/platforms` fails to deserialize on the **first** platform of a real library. Use
  `RomM.Client.Catalog.RomRow` and `PlatformRow`, which are slim and carry `long`.
- **`platform.slug` is not unique; `fs_slug` and `id` are.** 123 platforms, 72 slugs, on a
  real instance, whose owner files demos and prototypes under a parallel `-unofficial` folder
  per system. **A user's filing scheme, not a RomM behaviour**, so the number and naming of
  such rows is unpredictable. Never key anything by slug.
- **`PUT /api/devices/{id}` takes only the fields you are changing.** The generated
  `DeviceUpdatePayload` serializes unset properties as explicit nulls and the server answers
  **500** with a plain-text body. Send a bare `{"sync_config": {...}}`, and merge into what
  is already there so another client's keys survive.
- **Never read `rom_ids` from a collection response.** `BaseCollectionSchema.rom_ids` is a
  full `set[int]` present even on the list endpoint, so `GET /api/collections` on a large
  instance returns every membership of every collection. Page
  `GET /api/roms?collection_id=` instead.
- **An unknown query parameter is silently ignored**, so a misspelt filter returns the whole
  library with a 200. `platform_ids=<psx>` answers `total=9500`; `platform_id=` singular and
  an invented parameter both answer `total=83131`. No 422, no warning, no echo of what was
  applied. Check parameter names against the pinned schema, and treat a scoped walk whose
  `total` equals the library total as a bug in the query.
- **An unrecognised _value_ of a known filter is ignored the same way, and that is worse.**
  `metadata_providers=zzz-not-a-provider` returns the whole library, so a picker offering a
  value the server does not know hands somebody every game while looking like a filter.
  **Never invent a vocabulary for a parameter the schema declares as a bare array of strings.**
  Two of them are: `metadata_providers` has no enumeration anywhere and was probed one value at
  a time against a live instance, where a recognised value moves the total and an unrecognised
  one does not. Deriving it from `SimpleRomSchema`'s `*_id` fields would have been wrong,
  because `sgdb` is one of those and the filter ignores it. `statuses` cannot be probed that
  way at all, since an unrecognised status returns **zero** rather than everything and so looks
  exactly like a real status nobody has used; its authority is `RomUserStatus` in the schema.
  Finding 236.
- **`filter_values` is not the list of what you can filter on.** The sidecar reports
  `game_modes`, which no query parameter accepts, and omits `statuses` and
  `metadata_providers`, which two do. It also reports `platforms`, which is a scope rather than
  a facet, and as **ids** where every other key is names. Drive a filter screen off the
  endpoint's parameters and use the sidecar only for the values. Finding 237.
- **The sidecar rides on `/api/roms`, so asking for it means being sent a row.** Read it with a
  shape that ignores `items` entirely. Deserialising the row through the generated page type
  put an `int32` on `fs_size_bytes`, and a single ROM at or above 2 GiB then failed the whole
  body, leaving every facet empty on a library with thousands of values. `RomRow` exists for
  that overflow and says so; `RomFilterValuesPage` is the same lesson for this call.
- **Send `Range: bytes=0-` on a single-file ROM download, and never on a multi-file one.**
  Single-file answers 206 with an `ETag` (nginx's `hex(mtime)-hex(size)`) and resumes; a
  stale `If-Range` returns a full 200 rather than a corrupt splice. **Any `Range` on a
  multi-file ROM is refused 403 by nginx**, and the plain request that works carries no
  `ETag` and no `Accept-Ranges`, so multi-file is not resumable at all.
- **Multi-file is `has_multiple_files`, and an empty `fs_extension` is not the same thing.**
  The schema carries three shape flags: `has_simple_single_file`, `has_nested_single_file`,
  `has_multiple_files`. Every multi-file ROM does have an empty extension (209 of 209
  sampled), but only 209 of 602 extensionless ROMs are multi-file; the other 391 are
  `has_nested_single_file`, an ordinary ROM inside a folder, 157 of them holding one file.
  Key off the flag, never off the extension.
- **`download_path` on a save is not a usable URL.** It is served with a raw space and an
  unencoded `+`: `/api/saves/130/content?timestamp=2026-08-10 23:00:25.474218+00:00`. Build
  the URL from the save `id`.
- **Media is static files under `/assets/romm/resources/`, not an API route, and the fields
  come in two shapes.** `path_cover_small` and `path_cover_large` are already rooted at that
  prefix and carry a `?ts=` query with a **raw space**; `path_manual`, `path_video` and
  `ss_metadata.logo_path` are relative to it. **The relative form requested as given
  answers 200 with the web UI's `index.html`**, 5,826 bytes, with an `ETag` and
  `Accept-Ranges`, so a status check will not catch it and the content type must be. Normalise
  onto the prefix exactly once and drop the query. nginx serves them: ranges work, 416 past
  the end, and **no token is required at all**. EmulationStation's marquee is
  `ss_metadata.logo_path`, never the similarly named `marquee_path`, which is an arcade
  cabinet marquee.
- **Never use `url_cover` or `url_manual`.** They are `neoclone.screenscraper.fr` API URLs
  carrying a third party's `devid` and `devpassword` in the query string. Off-LAN, and not
  yours to send.
- **The paged read already carries the metadata; `GET /api/roms/{id}` does not add any.**
  `SimpleRomSchema` has `metadatum`, `summary`, the media paths, `regions` and `languages`.
  `DetailedRomSchema` adds only seven user arrays. And **`/api/roms` has no id-list
  parameter**, so a set of known ROM ids cannot be asked for: read metadata during the walk.

  **Re-verified against the pinned `romm-5.2.0.json`, because a whole scope kind turns on it.**
  The scoping parameters are `platform_ids`, `collection_id`, `virtual_collection_id` and
  `smart_collection_id`, and there is nothing else. So a hand-picked set **cannot be resolved by
  a page walk at all**, and that is a property of the scope rather than a defect to work around:
  `CatalogQuery.ToQueryString` throws for it rather than falling through, because every scoping
  parameter would be omitted and the query would match the entire library, which reads as a
  picked set resolving to everything.

  The way round it is per-id fetches, at the ~0.15 s each M4 measured, and only on a device the
  set roamed to: the device that did the picking already has every field the membership needs on
  the browse row it was looking at. Fetch one at a time, drop a 404 as ordinary drift, and stop
  on a 401 because every remaining fetch would send the same refused token.

  **Read one ROM as a `RomRow`, not through the generated detail schema.** Same trap as the
  paged read: the pinned schema declares `fs_size_bytes` a bare `integer`, so a generated DTO
  fails outright on any ROM at or above 2 GiB, which is most disc images.
- **`metadatum` units and scales agree with nothing.** `first_release_date` is **milliseconds**
  (read as seconds, every value lands in year 0); `average_rating` is **0-100**;
  `player_count` is a **string** already in EmulationStation's `1-2` form; `companies` is one
  flat array merging developer and publisher, **alphabetically sorted on 4,197 of 4,197**
  rows, so the roles cannot be recovered from it or from any provider block.
- **`POST /api/saves/delete` fails the whole batch if one id is already gone**, answering 404
  and deleting nothing. Autocleanup can remove an id between listing and deleting, so delete
  one at a time or re-list immediately before.
- **`POST /api/devices` answers `{device_id, name, created_at}`**, not a `DeviceSchema`.
  `GET /api/devices` keys the same value `id`.
- **`md5_hash`, `sha1_hash` and `crc_hash` all describe the _uncompressed_ content**, not
  just the CRC. A `.zip` reports the hashes of the file inside it, so hash inside a
  single-entry archive rather than over its bytes. Only 91% of ROMs carry an md5 and 96% a
  sha1, so verification must degrade to size and say so.

  **That rule is about a single-entry ROM archive. A multi-member firmware archive is
  hashed as a container.** Measured: the library's 34-member `neogeo.zip` carries an
  `md5_hash` equal to the md5 of the downloaded bytes exactly. So **a firmware `.zip` can
  never be joined on md5** against a manifest that hashed a differently-built archive of the
  same members. 84 of RetroBat's 353 BIOS requirements are zips, and **20 of those carry an
  md5**, which is the whole defect surface: the other 64 name no hash, so `BiosPlanner.Inspect`
  answers `Unverifiable` before the join. Compare members, the way `LogicalContentHash` does
  for saves. See
  [argosy-findings.md](../../../docs/argosy-findings.md), A3.

- **`GET /api/roms/identifiers` does not scale.** It takes no parameters and answered 504
  after 300 s on an 83k library; the platform and collection siblings answer in under 1.5 s.
  Reconcile deleted content through set re-resolution instead. `GET /api/roms/by-hash` is
  133-385 ms on a hit but **8.3 s on a miss**, and `GET /api/roms/{id}/simple` 4.2 s on a
  hit, so neither is a sweep.
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
  `autocleanup_limit` to 10, so a slot grows unboundedly unless you ask it not to.
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
  two rows, so tagging works. `PUT /api/states/{id}` exists and is unnecessary.
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
  in a slot is history and is never offered as an operation**, which is what makes an appending
  upload untidy rather than dangerous. Measurement 163, read from source and then driven against
  a slot holding a superseded row.
- **A negotiate cancels the device's previous active session**, so `/sessions/{id}/complete` on
  that earlier one answers **400** `Session is already cancelled`. Complete a session before
  negotiating again, or accept that the first one can never be tidied up. Measurement 164.
- **There is no `is_favorite` and no `playtime` on rom props.** Favourites are collection
  membership; playtime lives in play sessions.
- **Socket.IO is unusable.** It authenticates from the `romm_session` cookie only, and
  `sync:*` events are emitted to a `user:{id}` room nothing ever joins. Poll REST.
- **`is_verified` on firmware is unreliable here.** See `platform-mapping` and the BIOS
  section of the plan: it is false on files RetroBat requires, `psxonpsp660.bin` among them.
  Measured against a real library, filtering on it discards 6 of the 49 required files that
  library holds, and joining on `file_name` instead of `md5_hash` discards 2. Join on md5 and
  nothing else. (The separate figure of 93 of the 156 required md5s is what RomM has no record
  of at all, which no join can rescue.)
- **`missing_from_fs` means the row outlived the file, and its content route answers 500.**
  142 of 656 firmware records carried it on the library measured, and a bare
  `Internal Server Error` in `text/plain` is what a request for one gets, not a 404. Skip such
  a record before offering it, or a sync promises a file and fails mid-pass.
- **The firmware content route ignores the file name in the URL.** The right id under any name
  serves the bytes. It otherwise behaves exactly as the ROM route does, though it is
  Starlette's `FileResponse` rather than nginx: `accept-ranges`, an `etag`, a `content-range`
  on a 206, a byte-exact resume, 416 past the end, and a stale `If-Range` answered 200 with the
  whole body. **Its content type is guessed from the extension**, so a `.rom` arrives as
  `text/plain`; a type check may refuse HTML but must not require `application/octet-stream`.
- **A token missing `firmware.read` answers a bare `Forbidden`.** The scope guard runs before
  the handler, so the body names nothing, the same shape as the `me.write` case above. The
  client has to name the missing scope itself. `platforms.read` alone still carries every
  firmware `md5_hash`, because the records are inlined on the platform list, so a BIOS **gap
  report** survives a narrowed grant and only the fetch is refused.

## Presence: `POST /api/activity/heartbeat`

Registers this device as playing one game right now. `{"rom_id": ..., "device_id": ...}`
answers 200 with a full presence record: user, rom name, cover and title-screen paths,
platform, `device_type` (already `RomMBat`, carried from pairing) and `started_at`.
`GET /api/activity` lists every current entry and `GET /api/activity/rom/{id}` filters to one,
both under 0.1 s.

- **The `DELETE` takes `device_id` as a query parameter**, not in the body the `POST` takes.
  Sent as a body it answers **422** naming the missing query field, which reads like a
  malformed payload rather than a misplaced one. `DELETE /api/activity/heartbeat?device_id=`
  answers 204.
- Argosy's client says the server holds a heartbeat for 90 seconds and it must be repeated
  while play continues. **That was not measured here** and is not a fact.
- **`game-start` may not call this.** It is inside the launch path. Only the detached
  `background <event>` pass may touch the network during play.
