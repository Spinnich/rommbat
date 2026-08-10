---
name: romm-api
description: Calling the RomM API from RomMBat - device pairing auth, the endpoints a sync client needs, required scopes, and the API's non-obvious traps. Use whenever writing or changing code in RomM.Client, choosing an endpoint, or debugging a 401/403/409.
---

# RomM API

The backend is the contract. DTOs are generated from `/openapi.json` (served at the
**root**, not under `/api`) and **committed**, pinned to RomM 5.1.0, the minimum supported
version. The published docs at docs.romm.app have drifted from the server on exactly the
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

| Need                     | Call                                                                           |
| ------------------------ | ------------------------------------------------------------------------------ |
| Version/capability probe | `GET /api/heartbeat` (unauthenticated, `SYSTEM.VERSION`)                       |
| Platforms                | `GET /api/platforms?updated_after=`                                            |
| ROMs                     | `GET /api/roms?...&with_files=true&limit=&offset=`                             |
| Deletion reconcile       | Set re-resolution. **Not** `GET /api/roms/identifiers`, which 504s at scale    |
| Match local files        | `GET /api/roms/by-hash?md5_hash=` (a miss costs 8.3 s)                         |
| Download a ROM           | `GET /api/roms/{id}/content/{fs_name}`                                         |
| Firmware                 | `GET /api/firmware?platform_id=`, `GET /api/firmware/{id}/content/{file_name}` |
| Save negotiation         | `POST /api/sync/negotiate`                                                     |
| Save upload              | `POST /api/saves?rom_id=&slot=&emulator=&device_id=&session_id=`               |
| Save download ack        | `POST /api/saves/{id}/downloaded`                                              |
| Close session            | `POST /api/sync/sessions/{session_id}/complete`                                |
| Playtime                 | `POST /api/play-sessions`                                                      |
| Roaming config           | `PUT /api/devices/{id}` (free-form `sync_config` dict)                         |

## Traps

- **Always** pass `with_char_index=false&with_filter_values=false&with_rom_id_index=false`
  to `/api/roms`; they cost a flat 841 KB per request. Keep `with_total=true`: it is an
  integer, costs nothing, and bounds a resumable walk. Page size 250, `order_by=id&order_dir=asc`
  so a ROM added mid-walk lands past the cursor instead of shifting every later page.
- **`fs_size_bytes` is an `int32` in the generated DTOs.** The pinned schema declares a bare
  `integer`, so `SimpleRomSchema`, `PlatformSchema` and `RomFileSchema` all overflow.
  `GET /api/platforms` fails to deserialize on the **first** platform of a real library. Use
  `RomM.Client.Catalog.RomRow` and `PlatformRow`, which are slim and carry `long`.
- **`platform.slug` is not unique; `fs_slug` and `id` are.** 123 platforms, 72 slugs, on a
  real instance: every system has an `-unofficial` twin. Never key anything by slug.
- **`PUT /api/devices/{id}` takes only the fields you are changing.** The generated
  `DeviceUpdatePayload` serializes unset properties as explicit nulls and the server answers
  **500** with a plain-text body. Send a bare `{"sync_config": {...}}`, and merge into what
  is already there so another client's keys survive.
- **Never read `rom_ids` from a collection response.** `BaseCollectionSchema.rom_ids` is a
  full `set[int]` present even on the list endpoint, so `GET /api/collections` on a large
  instance returns every membership of every collection. Page
  `GET /api/roms?collection_id=` instead.
- **Send `Range: bytes=0-` on a single-file ROM download, and never on a multi-file one.**
  Single-file answers 206 with an `ETag` (nginx's `hex(mtime)-hex(size)`) and resumes; a
  stale `If-Range` returns a full 200 rather than a corrupt splice. **Any `Range` on a
  multi-file ROM is refused 403 by nginx**, and the plain request that works carries no
  `ETag` and no `Accept-Ranges`, so multi-file is not resumable at all. Multi-file ROMs are
  identifiable before the request: `has_multiple_files`, and equivalently an empty
  `fs_extension`.
- **`md5_hash`, `sha1_hash` and `crc_hash` all describe the _uncompressed_ content**, not
  just the CRC. A `.zip` reports the hashes of the file inside it, so hash inside a
  single-entry archive rather than over its bytes. Only 91% of ROMs carry an md5 and 96% a
  sha1, so verification must degrade to size and say so.
- **`GET /api/roms/identifiers` does not scale.** It takes no parameters and answered 504
  after 300 s on an 83k library; the platform and collection siblings answer in under 1.5 s.
  Reconcile deleted content through set re-resolution instead. `GET /api/roms/by-hash` is
  133-385 ms on a hit but **8.3 s on a miss**, and `GET /api/roms/{id}/simple` 4.2 s on a
  hit, so neither is a sweep.
- **Saves pair on `(rom_id, slot)`.** A null slot means "archival manual upload" and
  negotiates as `upload` forever. Always send a stable, non-null slot.
- **The server renames uploaded saves** to `<name> [YYYY-MM-DD_HH-MM-SS]<ext>`. Persist the
  `file_name` from the response, not the one you sent.
- **Asset uploads are capped at 512 MiB** and rejected with 413 before the body is spooled.
- **States are not in the negotiate protocol.** `POST /api/states` has no slot, device or
  conflict detection. Best-effort only.
- **There is no `is_favorite` and no `playtime` on rom props.** Favourites are collection
  membership; playtime lives in play sessions.
- **Socket.IO is unusable.** It authenticates from the `romm_session` cookie only, and
  `sync:*` events are emitted to a `user:{id}` room nothing ever joins. Poll REST.
- **`is_verified` on firmware is unreliable here.** See `platform-mapping` and the BIOS
  section of the plan: it misses 94 of the 157 md5s RetroBat requires.
