"""Whether the row a `saves resolve --keep-local` leaves behind comes back as a download.

#44 settled that `overwrite=true` never replaces a row, so a resolution appends and the copy the
user rejected stays on the server. #53 asks the one consequence that decides whether that is
harmless: `SaveConflictResolver.KeepLocalAsync` records only the row it wrote and never acks the
row it superseded, so this device has no sync record for the loser, permanently. If negotiate
offers it, `SaveSync.AlreadyHeld` cannot suppress it, because it compares the offered hash
against the local one and the two differ by construction, and the next flush writes the rejected
copy back over the copy the user kept.

Reading `backend/endpoints/sync.py` at `5.1.0` and `5.1.1-beta.2` says it cannot happen:
negotiate folds the user's slotted saves to one row per `(rom_id, slot)` by `updated_at` before
matching anything, and both its passes walk that fold. This drives it instead.

The shape is built exactly as a resolution leaves it, on a probe-only slot:

  1  device B uploads the loser, so device A never gets a sync record for that row
  2  device A uploads the winner with overwrite=true, a second later so it appends
  3  negotiate as device A naming the slot, which is the ordinary post-resolution flush
  4  negotiate as device A with an empty saves array, which is measurement 151's shape

    python m6-probe8-leftover-row.py <rom_id>

Writes into the RomM instance named by ROMMBAT_TEST_SERVER, in a slot no client uses, and deletes
every row it created before it exits. A negotiate also leaves a sync-session row, which the API
cannot delete; both sessions are completed rather than left active.
"""

from __future__ import annotations

import hashlib
import json as _json
import sys
import time
from datetime import datetime, timezone

from _common import record, request

if len(sys.argv) != 2:
    print(__doc__)
    raise SystemExit(2)

rom_id = int(sys.argv[1])
SLOT = "rommbat-probe8-leftover"
FILE_NAME = "probe8.srm"
lines: list[str] = []
created: set[int] = set()
sessions: set[int] = set()

LOSER = b"probe8 the peer copy, which the user rejects"
WINNER = b"probe8 this device copy, which the user keeps"


def log(text: str = "") -> None:
    lines.append(text)


def devices() -> list[str]:
    status, _headers, payload, _elapsed = request("GET", "/api/devices")
    if status != 200:
        return []
    rows = _json.loads(payload)
    rows = rows.get("items", rows) if isinstance(rows, dict) else rows
    return [str(row["id"]) for row in rows if row.get("id")]


def upload(body_text: bytes, *, device_id: str, overwrite: bool) -> dict:
    boundary = "----rommbatprobe8"
    disposition = 'Content-Disposition: form-data; name="saveFile"; filename="' + FILE_NAME + '"'
    body = b"".join([
        f"--{boundary}\r\n".encode(),
        disposition.encode() + b"\r\n",
        b"Content-Type: application/octet-stream\r\n\r\n",
        body_text,
        f"\r\n--{boundary}--\r\n".encode(),
    ])
    params = {
        "rom_id": rom_id,
        "slot": SLOT,
        "emulator": "libretro",
        "device_id": device_id,
        # Off, so nothing this probe writes is pruned before the negotiate reads it.
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


def slot_rows() -> list[dict]:
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
    return [row for row in rows if row.get("slot") == SLOT]


def negotiate(device_id: str, saves: list[dict]) -> dict:
    status, _headers, payload, _elapsed = request(
        "POST",
        "/api/sync/negotiate",
        json_body={"device_id": device_id, "saves": saves},
    )
    try:
        parsed = _json.loads(payload) if payload else {}
    except ValueError:
        parsed = {"raw": payload[:300].decode("utf-8", "replace")}
    if isinstance(parsed, dict) and isinstance(parsed.get("session_id"), int):
        sessions.add(parsed["session_id"])
    return {"status": status, "body": parsed if isinstance(parsed, dict) else {}}


def report(label: str, result: dict, loser_id: int, winner_id: int) -> None:
    """Counts across the whole library, then every op naming a row this probe made."""
    body = result["body"]
    ops = body.get("operations") or []
    log(f"  {label}")
    log(f"    status {result['status']}  session {body.get('session_id')!r}")
    log(
        f"    {len(ops)} operation(s) across the library: "
        f"{body.get('total_upload')} upload, {body.get('total_download')} download, "
        f"{body.get('total_conflict')} conflict, {body.get('total_no_op')} no_op"
    )

    mine = [op for op in ops if op.get("save_id") in (loser_id, winner_id)]
    if not mine:
        log("    no operation names either row this probe made")
    for op in mine:
        which = "LOSER" if op.get("save_id") == loser_id else "winner"
        log(
            f"    {which} save_id={op.get('save_id')} action={op.get('action')!r} "
            f"reason={op.get('reason')!r}"
        )

    came_back = any(op.get("save_id") == loser_id for op in ops)
    log(
        "    does the leftover row appear at all? "
        + ("YES, it came back" if came_back else "no, the leftover row is not mentioned")
    )
    log()


ids = devices()

if len(ids) < 2:
    print("Needs at least two registered devices on the instance to hold device_id.")
    raise SystemExit(2)

device_a, device_b = ids[0], ids[1]

log(f"rom {rom_id}, slot {SLOT!r}, autocleanup off throughout")
log(f"device A (keeps its copy) {device_a}")
log(f"device B (the peer)       {device_b}")
log(f"md5 of the loser  : {hashlib.md5(LOSER).hexdigest()}")
log(f"md5 of the winner : {hashlib.md5(WINNER).hexdigest()}")
log()
log(f"slot holds {len(slot_rows())} row(s) before anything")
log()

log("=== building the shape a --keep-local resolution leaves behind ===")
log()

loser = upload(LOSER, device_id=device_b, overwrite=False)
loser_id = loser["body"].get("id")
log("  1. device B uploads the loser, so device A never acks this row")
log(f"    status {loser['status']}  row id {loser_id!r}  name {loser['body'].get('file_name')!r}")
log()

# Past a second boundary, so the resolution appends rather than updating in place. That is what a
# real one does: no decision a person takes lands inside the same second as the save.
time.sleep(1.3)

winner = upload(WINNER, device_id=device_a, overwrite=True)
winner_id = winner["body"].get("id")
log("  2. device A uploads the winner with overwrite=true, a second later")
log(f"    status {winner['status']}  row id {winner_id!r}  name {winner['body'].get('file_name')!r}")
log()

rows = sorted(slot_rows(), key=lambda r: r.get("id") or 0)
log(f"  the slot now holds {len(rows)} row(s):")
for row in rows:
    log(
        f"    id={row.get('id')} name={row.get('file_name')!r} "
        f"hash={row.get('content_hash')} origin={row.get('origin_device_id')!r} "
        f"updated_at={row.get('updated_at')!r}"
    )
log()

if loser_id == winner_id:
    log("  ABORT: the two uploads reused one row, so there is no leftover row to test.")
else:
    log("=== what the next negotiate does with it, as device A ===")
    log()

    report(
        "3. naming the slot, holding the winner locally (the ordinary post-resolution flush)",
        negotiate(
            device_a,
            [
                {
                    "rom_id": rom_id,
                    "file_name": FILE_NAME,
                    "slot": SLOT,
                    "emulator": "libretro",
                    "content_hash": hashlib.md5(WINNER).hexdigest(),
                    "updated_at": datetime.now(timezone.utc).isoformat(),
                    "file_size_bytes": len(WINNER),
                }
            ],
        ),
        loser_id,
        winner_id,
    )

    report(
        "4. an empty saves array, which is measurement 151's shape and the harsher case",
        negotiate(device_a, []),
        loser_id,
        winner_id,
    )

log("=== cleanup ===")

for session_id in sorted(sessions):
    status, _headers, _payload, _elapsed = request(
        "POST",
        f"/api/sync/sessions/{session_id}/complete",
        json_body={"operations_completed": 0, "operations_failed": 0},
    )
    log(f"  POST /api/sync/sessions/{session_id}/complete -> {status}")

if created:
    status, _headers, payload, _elapsed = request(
        "POST",
        "/api/saves/delete",
        json_body={"saves": sorted(created)},
    )
    log(f"  POST /api/saves/delete {sorted(created)} -> {status}")
    if status != 200:
        log(f"    body: {payload[:300].decode('utf-8', 'replace')}")

log(f"  slot holds {len(slot_rows())} row(s) after cleanup")

record("probe8-leftover-row", lines)
