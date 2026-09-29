---
summary: How RomM serialises datetimes.
read-when: Before reading any timestamp RomM sends.
---

# RomM: timestamps

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-260. A play session's datetimes carry no zone, and `System.Text.Json` reads them as local

Verified: RomM 5.3.1, 2026-09-29. How: read every datetime on ten `GET` routes and set `/api/play-sessions` against the response's `Date` header.
`/api/play-sessions` answers `start_time`, `end_time`, `created_at` and `updated_at` as
`2026-08-29T14:03:11`, with no offset and no `Z`, while storing UTC. Saves, states, ROMs,
platforms, devices, firmware and the user carry `+00:00`. `System.Text.Json` reads a zone-less
value as local, so a plain `DateTimeOffset` is out by the machine's own offset, and right only on
a UTC machine, which is what CI is. Every `DateTimeOffset` RomMBat reads off the server goes
through `UtcTimestampConverter`, which reads a zone-less value as UTC and honours an offset where
one is present. The test stub serves every timestamp zone-less, because a stub writing an offset
lets a broken client pass.
