---
summary: Device pairing, device updates and the rate limits around them.
read-when: Before changing pairing, a device call, or a live test that pairs.
---

# RomM: auth and pairing

Facts RomMBat relies on, one per heading. [The upstream reference](../README.md) says what an entry holds and how IDs are kept.

## RB-73. Only when it carries just the fields being changed

Previously: `PUT /api/devices/{id}` takes `DeviceUpdatePayload` (`romm-api`, plan M2)

Measurement says: Only when it carries **just the fields being changed**. The generated payload serializes unset properties as explicit nulls and the server answers **500** with a plain-text body. Sending only `sync_config` answers 200 and preserves the rest

## RB-74. The live suite's own rate limit

Previously: (not addressed) the live suite's own rate limit

Measurement says: `POST /api/auth/device/init` is 10/min/IP. One pairing per live test exceeds it and looks like a client fault. Live catalog tests share one pairing per class

## RM-8. Grants tightened on the export endpoints (`source`)

`POST /export/gamelist-xml` and `POST /export/pegasus` now require a `PLATFORMS` / `WRITE`
grant and enforce platform visibility. **RomMBat calls neither**, confirmed by grep rather than
assumed: no hand written C# reaches either path, and the single hit repo-wide is the string
`pegasus_export` as a JSON property name at `src/RomM.Client/Generated/RomMApiSchema.g.cs:5639`,
which is a generated DTO field and not a call site. So the tightened grant costs the pairing
scope set nothing.

What #176 still owes is the line in the `romm-api` skill, because an answer that lives only in
this document is one the next session re-derives.
