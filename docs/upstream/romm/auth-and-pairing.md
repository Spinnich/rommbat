---
summary: Device pairing, device updates, the rate limits around them, and the export grants RomMBat does not need.
read-when: Before changing pairing, a device call, or a live test that pairs.
---

# RomM: auth and pairing

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-73. `PUT /api/devices/{id}` writes every field it is sent, nulls included

Verified: RomM 5.1.1, 2026-08-10, and 5.3.1, 2026-09-28. How: on 5.1.1 sent both shapes to a throwaway device; on 5.3.1 read `update_device` and ran `LiveCatalogTests`' `sync_config` round trip.
The server applies `model_dump(exclude_unset=True)`, so a property sent as an explicit null is
written. The generated `DeviceUpdatePayload` serializes every unset property that way, and
`sync_enabled` and `sync_mode` are not nullable in the device table, so the full shape answers **500** with a
plain-text body. A bare `{"sync_config": {...}}` answers 200 and leaves the rest intact, which is
all `UpdateDeviceSyncConfigAsync` sends.

## RB-74. Pairing init allows 10 requests a minute per IP, and token polling 60

Verified: RomM 5.1.1, 2026-08-10, and 5.3.1, 2026-09-28. How: on 5.1.1 hit the limit with the live suite; on 5.3.1 read `utils/device_auth.py`.
Over either limit the server answers 429. `POST /api/auth/device/init` is the one the live suite
exhausts on its own: a pairing per test goes over it and reads as a pairing defect. Live catalog
tests share one pairing per class, and `LivePairingTests`, which must pair per test, skips on the 429.

## RM-8. The export endpoints need `platforms.write`, which RomMBat never requests

Verified: RomM 5.3.0, 2026-09-14, and 5.3.1, 2026-09-28. How: read `endpoints/export.py`, and grepped `src/`, `tests/` and `tools/` for both routes.
`POST /api/export/gamelist-xml` and `POST /api/export/pegasus` require the grant and 404 a platform
hidden from the caller. No hand-written code names either route; the one hit is `pegasus_export`,
a generated DTO property. `GamelistSync` writes RetroBat's gamelists itself, so the device scope
set does not carry the grant.
