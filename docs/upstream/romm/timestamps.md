---
summary: How RomM serialises datetimes.
read-when: Before reading any timestamp RomM sends.
---

# RomM: timestamps

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-260. No, and every server timestamp this client reads was out by the machine's own offset

Question: Whether a datetime RomM sends can be read as the instant it means (**#208, #210**)

Measured: **No, and every server timestamp this client reads was out by the machine's own offset.** RomM serialises every datetime with **no zone** while storing UTC, and `System.Text.Json` reads a zone-less value as **local**. So a plain `DateTimeOffset` property is silently wrong everywhere except on a machine already at UTC, which is what CI is. Driven at the floor on a machine four hours behind UTC: a play session `status` had just read back came out **four hours after the same run's `Date` header**, which put a finished session in the future. Read straight off the wire for confirmation, `GET /api/play-sessions?limit=1` at the floor answers `"start_time": "2026-08-29T14:03:11"`, no offset and no `Z`. Not only the new read. `SaveRow.updated_at`, `SyncOperation.server_updated_at` and both of `StateRow`'s fields carry it too, so `saves restore`, `saves conflicts` and `flush`'s conflict block had all been printing shifted times. `UtcTimestampConverter` reads these as the UTC they are and honours an offset where one is present, so it is safe whichever way a later RomM sends a field. Rows already in `save_slot.updated_at` and `save_conflict.server_updated_at` keep the shifted value and nothing rewrites them: they correct themselves on the next negotiate or resolution, and ordering only ever compares server rows against each other, so they are display-only until then. **A test stub that writes an offset hides this**, and hides it hardest on a UTC machine, so the stub serves every timestamp zone-less.
