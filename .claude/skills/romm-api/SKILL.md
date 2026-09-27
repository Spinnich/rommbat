---
name: romm-api
description: Calling the RomM API from RomMBat - device pairing auth, the endpoints a sync client needs, required scopes, and the API's non-obvious traps. Use whenever writing or changing code in RomM.Client, choosing an endpoint, or debugging a 401/403/409.
---

# RomM API

The backend is the contract. DTOs are generated from `/openapi.json` (served at the
**root**, not under `/api`) and **committed**, pinned to RomM 5.3.1, the minimum supported
version. The floor tracks the newest RomM stable, or a prerelease ahead of it when one is
adopted early, so the pin moves with it and the two are one decision. The published docs at docs.romm.app have drifted from the server on exactly the
payloads this client needs most, so never code from them.

- The pin, the generator, and why the schema is normalised first:
  `src/RomM.Client/openapi/README.md`. Regenerate only when deliberately moving the pin.
- **`SocketsHttpHandler.ConnectTimeout` is set explicitly on every handler** (2 s
  interactive). Nothing sets it by default and an unreachable LAN host stalls 21 s.
- **`HttpClient.Timeout` stops at the headers under `ResponseHeadersRead`.** Measured (finding
  267): a body that went silent was still reading at 8 s under a 2 s timeout. So the two kinds of
  call are sent differently. A JSON call goes through `SendAsync`, which buffers the body with
  `ResponseContentRead` so `RequestTimeout` covers it. A body copied to a stream goes through
  `SendStreamedAsync` or the download client and then `CopyAsync`, whose per-read watchdog is
  `RomMClientOptions.StallTimeout` and raises `RomMUnreachableException`. Until #198 every call
  was streamed: a JSON body could hang a call, and saves, states and screenshots had their own
  copy with no watchdog. `ReadDetailAsync` bounds itself, since it reads error bodies past
  streamed headers.
- **The two timeouts differ only one level down.** Both arrive as `TaskCanceledException`
  wrapping `TimeoutException`. Under `HttpClient.Timeout` that `TimeoutException` wraps a
  further `TaskCanceledException`, and under `ConnectTimeout` it wraps nothing (M0 probe 6b).
  `Classify` reads that to report `RequestTimeout` or `ConnectTimeout`. Nothing branches on the
  reason yet.
- **A heartbeat answer that is not RomM's is no contact, not a crash.** A captive portal's page
  or a proxy's 502 makes `ProbeAsync` throw `RomMApiException`, and `ServerProbes.ContactAsync`
  returns it as a failure beside unreachable, flagged `Answered` so a caller can say something
  answered. `status` used to die on it (#211).
- **Never `catch (TaskCanceledException)` bare.** A connect timeout and a user cancellation
  are the same type; route everything through `RomMTransportErrors.Classify`.
- **401 and 403 are results, not exceptions.** Authenticated calls return `RomMResponse<T>`.
  Only transport failures throw (`RomMUnreachableException`).

## Map

This file holds the transport rules, pairing, scopes, endpoints, the cross-cutting traps and
presence. The rest is in topic files beside it, by section:

| Section                                                                                                                                     | File                     |
| ------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------ |
| [A rom row advertises media it does not serve](library.md#a-rom-row-advertises-media-it-does-not-serve)                                     | [library.md](library.md) |
| [A coverage percentage is a fact about one library on one day](library.md#a-coverage-percentage-is-a-fact-about-one-library-on-one-day)     | [library.md](library.md) |
| [`fs_size_bytes` can be stale against the file the server serves](library.md#fs_size_bytes-can-be-stale-against-the-file-the-server-serves) | [library.md](library.md) |
| [Traps](library.md#traps): the catalog walk, downloads, media, metadata, hashes and firmware                                                | [library.md](library.md) |
| [Traps](saves.md#traps): saves, states, play sessions, rom props and sync sessions                                                          | [saves.md](saves.md)     |

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
one pairing per class. `LivePairingTests` is the exception and cannot: pairing is what it
tests, so it spends four of the ten and two suite runs inside a minute exhaust the budget.
It skips on the 429 rather than failing, because a spent budget is not a defect in pairing
and the server's `detail` names the limit without naming the remedy.

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

**RomMBat calls neither `POST /api/export/gamelist-xml` nor `POST /api/export/pegasus`, so
neither grant is requested.** Both tightened at RomM 5.3.0 to require a `PLATFORMS` / `WRITE`
grant and to enforce platform visibility, which would otherwise have landed on the device
scope set. Confirmed by grep rather than assumed: no hand-written C# names either route, and
the one repo-wide hit is `pegasus_export` as a generated DTO property describing the server's
own config. That follows from the design, since `GamelistSync` writes RetroBat gamelists into
the install directly and has no reason to ask the server for one. Do not re-run that grep; #176.

`me.write` is **not** a device scope and RomMBat never asks for it. `/approve` and `/deny`
require it, so only a harness token carries it. A token without it fails the route guard
with a bare 403 `Forbidden` before the code is looked up; a scope-subset rejection instead
says `Approved scopes exceed what's allowed for this user`. The route guard checks the
**token's** scopes, `allowed_scopes` is computed from the **account's**.

## Endpoints that matter

| Need                     | Call                                                                                                                            |
| ------------------------ | ------------------------------------------------------------------------------------------------------------------------------- |
| Version/capability probe | `GET /api/heartbeat` (unauthenticated, `SYSTEM.VERSION`)                                                                        |
| Platforms                | `GET /api/platforms?updated_after=`                                                                                             |
| ROMs                     | `GET /api/roms?...&with_files=true&limit=&offset=`                                                                              |
| Deletion reconcile       | Set re-resolution. **Never** `GET /api/roms/identifiers`: unscopable, unpageable, and its server work outlives a client timeout |
| Match local files        | `GET /api/roms/by-hash?md5_hash=` (a miss costs 8.3 s)                                                                          |
| Download a ROM           | `GET /api/roms/{id}/content/{fs_name}`                                                                                          |
| Firmware, one platform   | `GET /api/firmware?platform_id=`, `GET /api/firmware/{id}/content/{file_name}`                                                  |
| Presence, "playing now"  | `POST /api/activity/heartbeat`, `GET /api/activity`, `GET /api/activity/rom/{id}`                                               |
| Stop "now playing"       | `PUT /api/roms/{id}/props`, body `{"now_playing": false}`. **Not** the heartbeat                                                |
| Save negotiation         | `POST /api/sync/negotiate`                                                                                                      |
| Save upload              | `POST /api/saves?rom_id=&slot=&emulator=&device_id=&session_id=&autocleanup=`                                                   |
| Save download            | `GET /api/saves/{id}/content?device_id=&optimistic=false`                                                                       |
| Save download ack        | `POST /api/saves/{id}/downloaded`, body `{device_id}`, after the bytes verify                                                   |
| Slot inventory for a ROM | `GET /api/saves/summary?rom_id=`                                                                                                |
| Close session            | `POST /api/sync/sessions/{session_id}/complete`                                                                                 |
| Playtime                 | `POST /api/play-sessions`, body `{device_id, sessions: [...]}`                                                                  |
| Playtime read-back       | `GET /api/play-sessions?device_id=&rom_id=&start_after=&end_before=&limit=&offset=`                                             |
| Roaming config           | `PUT /api/devices/{id}` (free-form `sync_config` dict)                                                                          |
| Firmware, whole library  | `GET /api/platforms`, whose inlined `firmware[]` carries every `md5_hash`                                                       |

## Traps

These cross every area. The rest are in [library.md](library.md#traps) (catalog, downloads,
media, metadata, firmware) and [saves.md](saves.md#traps) (saves, states, play sessions, a 409).

- **RomM serialises every datetime without a zone and stores UTC, and
  `System.Text.Json` reads a zone-less value as local.** So a plain `DateTimeOffset` property is
  wrong by the machine's own offset, silently, and reads as right on a UTC machine, which is what
  CI is. Driven against the live instance while adding the play-session read: a session the agent
  had just fetched came back four hours ahead of the same run's `Date` header, putting a finished
  session in the future. Finding 260 in `docs/retrobat-findings.md`. **Put `[JsonConverter(typeof(UtcTimestampConverter))]` on any
  `DateTimeOffset` read off the server**, which honours an offset where one is present, so it is
  safe whether or not the field names a zone. `RomRow.UpdatedAtUtc` does the same by hand because
  its raw field is a string. The stub serves every timestamp zone-less for this reason; a stub
  writing an offset lets the broken client pass.
- **`PUT /api/devices/{id}` takes only the fields you are changing.** The generated
  `DeviceUpdatePayload` serializes unset properties as explicit nulls and the server answers
  **500** with a plain-text body. Send a bare `{"sync_config": {...}}`, and merge into what
  is already there so another client's keys survive.
- **`POST /api/devices` answers `{device_id, name, created_at}`**, not a `DeviceSchema`.
  `GET /api/devices` keys the same value `id`.
- **Socket.IO is unusable.** It authenticates from the `romm_session` cookie only, and
  `sync:*` events are emitted to a `user:{id}` room nothing ever joins. Poll REST.

## Presence: `POST /api/activity/heartbeat`

Registers this device as playing one game right now. `{"rom_id": ..., "device_id": ...}`
answers 200 with a full presence record: user, rom name, cover and title-screen paths,
platform, `device_type` (already `RomMBat`, carried from pairing) and `started_at`.
`GET /api/activity` lists every current entry and `GET /api/activity/rom/{id}` filters to one,
both under 0.1 s.

**This is not the mechanism behind the "playing now" flag on a rom, and the two wear similar
names.** The heartbeat is the presence feed at `GET /api/activity`, which RomMBat never posts
to and which is empty on an install that only syncs. The flag a library actually shows is
`rom_user.now_playing`, set by ingesting a play session and cleared only by
`PUT /api/roms/{id}/props`. **`DELETE /api/activity/heartbeat` answers 204 and does not touch
it**, measured during M7 stage 7b-3, so a session that reaches for the heartbeat to stop a
game reading as in progress has reached for the wrong one.

- **The `DELETE` takes `device_id` as a query parameter**, not in the body the `POST` takes.
  Sent as a body it answers **422** naming the missing query field, which reads like a
  malformed payload rather than a misplaced one. `DELETE /api/activity/heartbeat?device_id=`
  answers 204.
- Argosy's client says the server holds a heartbeat for 90 seconds and it must be repeated
  while play continues. **That was not measured here** and is not a fact.
- **`game-start` may not call this.** It is inside the launch path. Only the detached
  `background <event>` pass may touch the network during play.
