"""What `overwrite=true` actually does to a slot, which two artefacts get wrong.

`docs/PLAN.md` says uploading changed content into a slot appends a row "unless
`overwrite=true`, which replaces in place", and `StubRomMServer.Saves.cs` models it that way,
reusing the row id in the slot. `SaveConflictResolver.KeepLocalAsync` is the only caller, so if
the server appends instead, choosing "keep my copy" leaves the server's copy beside it and the
slot grows a row per resolution.

One live observation in issue #39 said it appends, but that call did not send `autocleanup` and
used a different `device_id` from the row it was replacing, so neither variable was held. This
posts into one probe-only slot eight ways and reports the row ids after each, with `autocleanup`
off throughout so pruning cannot hide the answer:

  0-4  device_id held fixed: identical and different content, each with and without overwrite
  5-6  the same two overwrite postings from a second device_id
  7    an unregistered device_id
  8-9  two postings inside one second, then one across a second boundary, on a fresh slot

    python m6-probe7-slot-overwrite.py <rom_id>

Writes into the RomM instance named by ROMMBAT_TEST_SERVER, in a slot no client uses, and
deletes every row it created before it exits.
"""

from __future__ import annotations

import hashlib
import json as _json
import sys
import time

from _common import record, request

if len(sys.argv) != 2:
    print(__doc__)
    raise SystemExit(2)

rom_id = int(sys.argv[1])
SLOT = "rommbat-probe7-overwrite"
TIMING_SLOT = "rommbat-probe7-timing"
lines: list[str] = []
created: set[int] = set()


def log(text: str = "") -> None:
    lines.append(text)


def devices() -> list[str]:
    """Two registered device ids, because an unregistered one skips the conflict checks."""
    status, _headers, payload, _elapsed = request("GET", "/api/devices")
    if status != 200:
        return []
    rows = _json.loads(payload)
    rows = rows.get("items", rows) if isinstance(rows, dict) else rows
    return [str(row["id"]) for row in rows if row.get("id")]


def upload(body_text: bytes, *, device_id: str | None, overwrite: bool, slot: str = SLOT) -> dict:
    boundary = "----rommbatprobe7"
    body = b"".join([
        f"--{boundary}\r\n".encode(),
        b'Content-Disposition: form-data; name="saveFile"; filename="probe7.srm"\r\n',
        b"Content-Type: application/octet-stream\r\n\r\n",
        body_text,
        f"\r\n--{boundary}--\r\n".encode(),
    ])
    params = {
        "rom_id": rom_id,
        "slot": slot,
        "emulator": "libretro",
        "device_id": device_id,
        # Off throughout. The whole question is how many rows the slot ends up with, and
        # server-side pruning is exactly what would hide a wrong answer.
        "autocleanup": "false",
    }
    if overwrite:
        params["overwrite"] = "true"

    status, _headers, payload, _elapsed = request(
        "POST",
        "/api/saves",
        params=params,
        raw_body=body,
        content_type=f"multipart/form-data; boundary={boundary}",
    )
    try:
        parsed = _json.loads(payload) if payload else {}
    except ValueError:
        parsed = {"raw": payload[:300].decode("utf-8", "replace")}

    if isinstance(parsed, dict) and isinstance(parsed.get("id"), int):
        created.add(parsed["id"])

    return {"status": status, "body": parsed if isinstance(parsed, dict) else {}}


def slot_rows(slot: str = SLOT) -> list[dict]:
    status, _headers, payload, _elapsed = request("GET", "/api/saves", params={"rom_id": rom_id})
    if status != 200:
        return []
    try:
        rows = _json.loads(payload)
    except ValueError:
        return []
    rows = rows.get("items", rows) if isinstance(rows, dict) else rows
    if not isinstance(rows, list):
        return []
    return [row for row in rows if row.get("slot") == slot]


def show(label: str, result: dict, slot: str = SLOT) -> None:
    body = result["body"]
    log(f"  {label}")
    log(f"    status {result['status']}  row id {body.get('id')!r}")
    log(f"    file_name  {body.get('file_name')!r}")
    log(f"    hash       {body.get('content_hash')!r}")
    log(f"    origin     {body.get('origin_device_id')!r}")
    rows = slot_rows(slot)
    log(f"    slot now holds {len(rows)} row(s): {sorted(r.get('id') for r in rows)}")
    log()


ids = devices()

if len(ids) < 2:
    print("Needs at least two registered devices on the instance to hold device_id.")
    raise SystemExit(2)

first_device, second_device = ids[0], ids[1]

SAME = b"probe7 payload one"
OTHER = b"probe7 payload two, a different save entirely"

log(f"rom {rom_id}, slots {SLOT!r} and {TIMING_SLOT!r}, autocleanup off throughout")
log(f"device A {first_device}")
log(f"device B {second_device}")
log(f"md5 of payload one : {hashlib.md5(SAME).hexdigest()}")
log(f"md5 of payload two : {hashlib.md5(OTHER).hexdigest()}")
log()

existing = slot_rows()
log(f"slot holds {len(existing)} row(s) before anything: {sorted(r.get('id') for r in existing)}")
log()

log("=== device_id held fixed at A ===")
log()
show("0. seed the slot", upload(SAME, device_id=first_device, overwrite=False))
show("1. identical content, no overwrite", upload(SAME, device_id=first_device, overwrite=False))
show("2. identical content, overwrite=true", upload(SAME, device_id=first_device, overwrite=True))
show("3. different content, no overwrite", upload(OTHER, device_id=first_device, overwrite=False))
show("4. different content, overwrite=true", upload(OTHER, device_id=first_device, overwrite=True))

log("=== the same two postings from device B, which is the variable #39 could not hold ===")
log()
show("5. identical content, overwrite=true, device B", upload(SAME, device_id=second_device, overwrite=True))
show("6. different content, overwrite=true, device B", upload(OTHER, device_id=second_device, overwrite=True))

log("=== an unregistered device_id, which resolves to no device at all ===")
log()
show("7. different content, overwrite=true, unknown device", upload(OTHER, device_id="not-a-registered-device", overwrite=True))

log("=== what row identity actually is, which the run above stumbled on ===")
log()
log("    A slotted upload is renamed to carry a datetime tag at ONE-SECOND resolution, and the")
log("    row is then looked up by that tagged name. So whether a posting updates a row or appends")
log("    one depends on the wall clock. Step 3 above updated row 156 rather than appending purely")
log("    because it landed in the same second as the posting that created it.")
log()
log("    Forced here rather than left to timing, on a second slot with no conflict history and")
log("    with overwrite=true throughout, so neither the 409 checks nor the hash dedup is in play")
log("    and the tagged name is the only thing deciding.")
log()

# Start just after a tick, so the pair below lands inside one second rather than straddling one.
time.sleep(1.0 - (time.time() % 1.0) + 0.05)

show(
    "8a. seed the timing slot",
    upload(SAME, device_id=first_device, overwrite=True, slot=TIMING_SLOT),
    TIMING_SLOT,
)
show(
    "8b. different content, overwrite=true, same second",
    upload(OTHER, device_id=first_device, overwrite=True, slot=TIMING_SLOT),
    TIMING_SLOT,
)

time.sleep(1.3)

show(
    "9. different content, overwrite=true, a second later",
    upload(SAME, device_id=first_device, overwrite=True, slot=TIMING_SLOT),
    TIMING_SLOT,
)

log("=== the timing slot as it stands ===")
for row in sorted(slot_rows(TIMING_SLOT), key=lambda r: r.get("id") or 0):
    log(f"  id={row.get('id')} name={row.get('file_name')!r} hash={row.get('content_hash')}")
log()

log("=== the main slot as it stands ===")
for row in sorted(slot_rows(), key=lambda r: r.get("id") or 0):
    log(
        f"  id={row.get('id')} name={row.get('file_name')!r} "
        f"hash={row.get('content_hash')} size={row.get('file_size_bytes')} "
        f"origin={row.get('origin_device_id')!r}"
    )
log()

log("=== cleanup ===")
# POST /api/saves/delete, taking every id at once. Nothing this probe wrote is worth keeping,
# and leaving rows in a real library is litter.
if created:
    status, _headers, payload, _elapsed = request(
        "POST",
        "/api/saves/delete",
        json_body={"saves": sorted(created)},
    )
    log(f"  POST /api/saves/delete {sorted(created)} -> {status}")
    if status != 200:
        log(f"    body: {payload[:300].decode('utf-8', 'replace')}")

log(f"  main slot holds {len(slot_rows())} row(s) after cleanup")
log(f"  timing slot holds {len(slot_rows(TIMING_SLOT))} row(s) after cleanup")

record("probe7-slot-overwrite", lines)
